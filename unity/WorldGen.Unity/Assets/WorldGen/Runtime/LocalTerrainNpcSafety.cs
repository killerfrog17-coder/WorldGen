using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class NpcThreat
    {
        public long Human,Victim;public ulong Animal;public V Point;
        public double Until,CheckedAt,SeenAt;public bool Direct,Violence,Visible;
        public NpcDangerResponse Response;
    }
    double NpcCloseness(HumanActor actor,HumanActor other)
    {
        if(actor==other)return 1;if(other?.Resident==null||actor.GroupId!=other.GroupId)return 0;
        var group=livingGroups[actor.GroupId];string kin=ForagerSocialLife.Relationship(actor.Resident,other.Resident,group.Residents);
        double kinship=kin=="a fellow band member"?.15:kin=="a member of your household"?.6:.9;
        return Math.Max(kinship,Math.Max(0,NpcSocialRules.Affinity(sourceWorld.Prehistory.Foragers.Cultures[group.Residents.Culture],actor.Resident,other.Resident,group.Residents,NpcMemoryFor(actor.ResidentId).Social)));
    }
    bool NpcHasWeapon(HumanActor actor)=>actor.Belongings!=null&&actor.Belongings.Items.Any(p=>(p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand)&&ItemUseRules.Profile(p).Weapon&&ItemUseRules.Condition(p)>0);
    Vector3 NpcThreatBody(long person,ulong animal,out bool valid)
    {
        if(animal!=0&&animals.TryGetValue(animal,out var beast)&&!beast.Dead){valid=true;return beast.Renderer.bounds.center;}
        if(person==PlayerPropertyKey&&humanPlayer!=null&&!DeathVisible){valid=true;return transform.TransformPoint(PlayerFeet())+transform.up;}
        if(person>0&&livingPeople.TryGetValue(person,out var human)&&human.Health>0){valid=true;return human.Root.transform.position+transform.up;}
        valid=false;return Vector3.zero;
    }
    void SetNpcThreat(HumanActor actor,long human,ulong animal,long victim,V point,bool direct,bool violence)
    {
        if(actor.Resident==null)return;
        var t=actor.Threat;
        if(t!=null&&t.Direct&&!direct&&actor.SimulationAge<t.Until)return;
        bool fresh=t==null||t.Human!=human||t.Animal!=animal;
        if(fresh)actor.Threat=t=new NpcThreat();
        t.Human=human;t.Animal=animal;t.Victim=victim;t.Point=point;t.Direct|=direct;t.Violence|=violence;t.Until=actor.SimulationAge+(violence?40:12);t.SeenAt=actor.SimulationAge;t.CheckedAt=0;
        if(fresh)
        {
            if(actor.Resident.AgeYears>=14&&!NpcHasWeapon(actor)&&actor.Belongings!=null)
            {
                var weapon=actor.Belongings.Items.Where(p=>actor.Belongings.Carried(p)&&ItemUseRules.Profile(p).Weapon).OrderByDescending(p=>ItemUseRules.Profile(p).ReachM).FirstOrDefault();
                if(weapon!=null){NpcBelongings.Equip(actor.Belongings,new[]{weapon},actor.Planet);PublishNpcGroundGoods(actor);}
            }
            npcPlans.Remove(actor.ResidentId);
            if(npcConversation!=null&&(npcConversation.A==actor.ResidentId||npcConversation.B==actor.ResidentId))CancelNpcConversation();
        }
    }
    void NoticeNpcAnimalThreat(HumanActor observer,Animal animal,double distance)
    {
        bool attacking=animal.BehaviourAction=="attack"||animal.BehaviourAction=="defend"||animal.BehaviourAction=="hunt";
        if(distance<14||attacking&&distance<28)SetNpcThreat(observer,0,animal.Spawn.Id,0,animal.Planet,false,attacking&&distance<8);
    }
    void WitnessNpcAssault(HumanActor victim,long aggressor,ulong animal)
    {
        if(victim.Resident==null)return;
        Vector3 source=NpcThreatBody(aggressor,animal,out bool sourceExists);string attackerName=animal!=0&&animals.TryGetValue(animal,out var beast)?beast.Spawn.Profile.name:aggressor==PlayerPropertyKey?PlayerRules.Identity?.Person?.Name??"the visitor":livingPeople.TryGetValue(aggressor,out var person)?person.Resident.Name:"an unidentified attacker";
        V attackPoint=sourceExists?frame.Position(transform.InverseTransformPoint(source).x+originX,transform.InverseTransformPoint(source).z+originZ):victim.Planet;
        foreach(var witness in livingPeople.Values)
        {
            if(witness.Health<=0||witness.Resident.AgeYears<3||witness.CurrentActivity==HumanActivity.Sleep&&witness!=victim||HumanDistance(witness.Planet,victim.Planet)>35)continue;
            bool self=witness==victim,seeingVictim=self||NpcSees(witness,victim.Root.transform.position+transform.up*.8f,NpcSightRange(witness));
            bool seeingAttacker=sourceExists&&NpcSees(witness,source,NpcSightRange(witness));
            if(!seeingVictim)
            {
                if(HumanDistance(witness.Planet,victim.Planet)<18&&NpcCanHear(witness,victim.Root.transform.position+transform.up,18))
                {NpcMemoryFor(witness.ResidentId).Social.Record(new NpcIncident{Kind=NpcIncidentKind.DistressHeard,Subject=victim.ResidentId,SolarDay=PlayerRules.SolarDays,Position=victim.Planet,Detail="You heard a nearby human cry of pain; you have not identified an attacker."});SetNpcThreat(witness,0,0,victim.ResidentId,victim.Planet,false,false);}
                continue;
            }
            string detail=seeingAttacker?attackerName+" attacked "+(self?"you":victim.Resident.Name)+".":self?"You were struck; you have not identified who struck you.":"You saw "+victim.Resident.Name+" being hurt, but did not identify the attacker.";
            NpcSocialRules.Assault(NpcMemoryFor(witness.ResidentId).Social,witness.ResidentId,victim.ResidentId,seeingAttacker?aggressor:0,NpcCloseness(witness,victim),PlayerRules.SolarDays,victim.Planet,detail,seeingAttacker?animal:0);
            SetNpcThreat(witness,seeingAttacker?aggressor:0,seeingAttacker?animal:0,victim.ResidentId,attackPoint,self,seeingAttacker);
            QueueNpcEvent(witness,"You personally witnessed: "+detail+" Your immediate safety response happens physically. Do not claim anyone unseen knows this.",priority:3);
        }
        CaptureLivingPerson(victim);
    }
    void DiscoverNpcDeath(HumanActor observer,HumanActor body)
    {
        if(!NpcSocialRules.DiscoverDeath(NpcMemoryFor(observer.ResidentId).Social,body.ResidentId,NpcCloseness(observer,body),PlayerRules.SolarDays,body.Planet,body.Resident.Name))return;
        QueueNpcEvent(observer,"You have personally discovered "+body.Resident.Name+" dead. React to your actual relationship and mortuary beliefs. You do not know who caused it unless you witnessed the attack.",priority:3);
    }
    bool ApplyNpcSafety(HumanActor actor,ref HumanActivitySample routine,ref V target)
    {
        var t=actor.Threat;if(t==null)return false;
        if(actor.SimulationAge>t.Until){actor.Threat=null;return false;}
        if(actor.SimulationAge>=t.CheckedAt)
        {
            t.CheckedAt=actor.SimulationAge+.6;
            Vector3 body=NpcThreatBody(t.Human,t.Animal,out bool exists);t.Visible=exists&&NpcSees(actor,body,NpcSightRange(actor));
            if(t.Visible){var local=transform.InverseTransformPoint(body);t.Point=frame.Position(local.x+originX,local.z+originZ);t.SeenAt=actor.SimulationAge;}
            int allies=0;bool protecting=false;
            foreach(var other in livingPeople.Values)
            {
                if(other==actor||other.GroupId!=actor.GroupId||other.Health<=0||HumanDistance(actor.Planet,other.Planet)>16)continue;
                if(!NpcSees(actor,other.Root.transform.position+transform.up*.7f,20))continue;
                if(other.Resident.AgeYears>=16&&other.Health>40&&NpcHasWeapon(other))allies++;
                if(other.ResidentId==t.Victim&&NpcCloseness(actor,other)>.4)protecting=true;
            }
            double distance=HumanDistance(actor.Planet,t.Point);bool closing=t.Violence&&t.Visible;
            t.Response=NpcSocialRules.Danger(actor.Resident.AgeYears,actor.Health,NpcAwarenessFor(actor).Personality.Courage,NpcHasWeapon(actor),allies,1,distance,t.Direct,protecting,closing);
            // A cry with no identified assailant can orient/search, never turn
            // into an order to attack the person who happens to be nearby.
            if(t.Human==0&&t.Animal==0&&t.Response!=NpcDangerResponse.Escape)t.Response=NpcDangerResponse.Watch;
        }
        double dx,dz;TerrainHandoff.Coordinates(frame,t.Point,out double hx,out double hz);dx=actor.Motor.X-hx;dz=actor.Motor.Z-hz;double range=Math.Sqrt(dx*dx+dz*dz);
        bool escape=t.Response==NpcDangerResponse.Escape||t.Response==NpcDangerResponse.Withdraw;
        if(escape)
        {
            if(range<.2){double yaw=actor.Yaw*Math.PI/180;dx=-Math.Sin(yaw);dz=-Math.Cos(yaw);range=1;}
            double retreat=t.Response==NpcDangerResponse.Escape?32:18;
            target=range<retreat?frame.Position(hx+dx/range*retreat,hz+dz/range*retreat):actor.Planet;
        }
        else if(t.Response==NpcDangerResponse.Defend||t.Response==NpcDangerResponse.Protect)
        {
            var weapon=actor.Belongings.QuickHeld;double reach=Math.Max(.7,ItemUseRules.Profile(weapon).ReachM*.8);
            target=t.Visible&&range<22?frame.Position(hx+dx/Math.Max(.1,range)*reach,hz+dz/Math.Max(.1,range)*reach):actor.Planet;
            TryNpcDefensiveAttack(actor,t);
        }
        else target=actor.Planet;
        actor.CurrentActivity=escape?HumanActivity.Retreat:HumanActivity.Watch;
        actor.TaskWaiting=false;actor.FetchingGear=false;actor.TaskDetail=t.Response==NpcDangerResponse.Protect?"protecting a bandmate":t.Response==NpcDangerResponse.Defend?"defending against an identified attacker":escape?"moving away from a perceived threat":"watching the last noticed disturbance";
        routine=new HumanActivitySample{Person=actor.ResidentId,Group=actor.GroupId,Activity=actor.CurrentActivity,Position=target,Destination=target};
        return true;
    }
    void TryNpcDefensiveAttack(HumanActor actor,NpcThreat threat)
    {
        if(!threat.Visible||!threat.Violence||actor.SimulationAge<actor.NpcAttackAt)return;
        var item=actor.Belongings.QuickHeld;var profile=ItemUseRules.Profile(item);if(!profile.Weapon||HumanDistance(actor.Planet,threat.Point)>profile.ReachM+.35)return;
        var target=NpcThreatBody(threat.Human,threat.Animal,out bool valid);if(!valid)return;
        var start=NpcEye(actor)-transform.up*.4f;var delta=target-start;var ray=new Ray(start,delta.normalized);
        var obstruction=TraceCombatObstacle(ray,(float)profile.ReachM+.3f,checkGround:false);
        var hit=TraceLiving(ray,obstruction?.Distance??(float)profile.ReachM+.3f,true,actor);
        if(hit==null||hit.Human!=null&&(hit.Human==humanPlayer?threat.Human!=PlayerPropertyKey:hit.Human.ResidentId!=threat.Human)||hit.Animal!=null&&hit.Animal.Spawn.Id!=threat.Animal)return;
        actor.NpcAttackAt=actor.SimulationAge+Math.Max(1,profile.RecoverySeconds*1.6);actor.NpcAttackStarted=actor.SimulationAge;
        double damage=ItemUseRules.MeleeDamage(item,.65+.2*NpcAwarenessFor(actor).Personality.Courage);
        RegisterCombatWound(hit,item,damage,delta.normalized);
        if(hit.Human!=null)DamageHuman(hit.Human,damage,hit,delta.normalized,0,actor.ResidentId);
        else {DamageAnimal(hit.Animal,damage,hit.Point,delta.normalized,0,false);hit.Animal.NpcAssailant=actor.ResidentId;}
    }
}
