using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckNpcSafety(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok){File.WriteAllLines(Path.Combine(output,"failed-checks.txt"),report.Concat(new[]{"FAIL "+text}));Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);Debug.Log("NPC SAFETY PASS "+text);}
        npcModel??=new NpcModelClient();npcModel.Config.Enabled=false;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();
        double until=Time.unscaledTimeAsDouble+90;while(livingPeople.Values.Count(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18)<2&&Time.unscaledTimeAsDouble<until)yield return null;
        var people=livingPeople.Values.Where(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18&&p.Health>0).Take(2).ToArray();Check(people.Length==2,"Actual adult residents exist for local violence checks");
        var victim=people[0];var hidden=people[1];paused=true;
        void Place(HumanActor p,double x,double z,float yaw){p.Motor.Place(x,z,CampFloor(x,z));p.Planet=frame.Position(x,z);p.Root.transform.localPosition=new Vector3((float)(x-originX),(float)(p.Motor.Y-TerrainMotion.EyeHeightM),(float)(z-originZ));p.Yaw=p.TargetYaw=yaw;p.Root.transform.localRotation=Quaternion.Euler(0,yaw,0);p.CurrentActivity=HumanActivity.Watch;p.Threat=null;}
        double x=victim.Motor.X,z=victim.Motor.Z;Place(victim,x,z,270);motion.Place(x-1.4,z,CampFloor(x-1.4,z));UpdateCamera();Place(hidden,x-5,z,90);
        var wall=new GameObject("Attack hidden witness fixture");wall.layer=27;wall.transform.SetParent(transform,false);wall.transform.localPosition=new Vector3((float)(x-originX-3),(float)(victim.Motor.Y-TerrainMotion.EyeHeightM)+1.5f,(float)(z-originZ));wall.AddComponent<BoxCollider>().size=new Vector3(.3f,5,8);Physics.SyncTransforms();
        var spear=PortableTechnology.Create(PossessionKind.WoodenSpear,victim.Planet);spear.Id="defender-probe-"+victim.ResidentId;NpcOwnership.Assign(spear,victim.Resident,victim.GroupId);victim.Belongings.Items.Add(spear);NpcBelongings.Equip(victim.Belongings,new[]{spear},victim.Planet);PublishNpcGroundGoods(victim);
        int hiddenBefore=NpcMemoryFor(hidden.ResidentId).Social.EventSequence>int.MaxValue?0:NpcMemoryFor(hidden.ResidentId).Social.Incidents.Count;
        try
        {
            DamageHuman(victim,4,null,transform.right);
            Check(victim.Threat!=null&&victim.Threat.Human==PlayerPropertyKey,"The hurt resident identifies the actually visible attacker");
            Check(hidden.Threat==null&&NpcMemoryFor(hidden.ResidentId).Social.Incidents.Count==hiddenBefore,"A person behind a real solid receives no telepathic group alarm");
            var routine=victim.Routine;V goal=victim.Planet;Check(ApplyNpcSafety(victim,ref routine,ref goal),"A witnessed threat overrides the actual physical routine");
            Check(victim.Threat.Response==NpcDangerResponse.Defend&&victim.CurrentActivity==HumanActivity.Watch,"A healthy armed adult can stand and defend instead of universally fleeing");
            // The contact path uses the real weapon and posed collision surface.
            double before=Vitals.Health;PlayerRules.Mode=GameMode.Survival;Vitals.Restore();ApplyPlayerMode();victim.NpcAttackAt=0;TryNpcDefensiveAttack(victim,victim.Threat);
            Check(Vitals.Health<before,"An NPC defensive strike with a real spear can hit the player's actual body");
            PlayerRules.Mode=GameMode.Creative;Vitals.Restore();ApplyPlayerMode();
            int incidents=NpcMemoryFor(victim.ResidentId).Social.Incidents.Count;
            victim.Bleeding=new BleedingState();DamageHuman(victim,1,null,Vector3.up,woundOnly:true);
            Check(NpcMemoryFor(victim.ResidentId).Social.Incidents.Count==incidents,"Ongoing wound damage is not reported as a new player assault");
            victim.Threat=null;Place(victim,x,z,270);DamageHuman(victim,1000,null,Vector3.up,woundOnly:true);
            Check(victim.Health<=0&&victim.Ragdoll!=null,"A real resident dies and becomes a physical ragdoll");
            Check(!NpcMemoryFor(hidden.ResidentId).Social.KnownPeople.Any(p=>p.Person==victim.ResidentId&&p.SeenDead),"A hidden bandmate does not know that the resident died");
            DestroyImmediate(wall);Physics.SyncTransforms();Place(hidden,x-1,z,90);DiscoverNpcDeath(hidden,victim);
            var social=NpcMemoryFor(hidden.ResidentId).Social;
            Check(social.KnownPeople.Any(p=>p.Person==victim.ResidentId&&p.SeenDead)&&social.Grief>0&&social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Death&&e.Subject==victim.ResidentId),"Actually discovering the body creates remembered death and grief");
            Check(!social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Assault&&e.Actor==PlayerPropertyKey),"Finding the body does not reveal an unwitnessed killer");
            LivingState.Copy().Validate();Inventory.Validate();Check(true,"Witnessed consequences and dropped possessions validate together");
            npcThoughts=true;paused=false;yield return NpcLifeImage(output,"witnessed-loss");paused=true;
            File.WriteAllText(Path.Combine(output,"witness-memory.json"),JsonUtility.ToJson(NpcMemoryFor(hidden.ResidentId),true));
        }
        finally{if(wall!=null)DestroyImmediate(wall);Physics.SyncTransforms();paused=false;}
        DisposeNpcMinds();
    }
}
