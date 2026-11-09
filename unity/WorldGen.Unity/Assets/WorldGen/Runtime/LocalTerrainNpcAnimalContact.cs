using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    readonly Dictionary<(int,int),List<HumanActor>> wildlifeHumanNeighbours=new Dictionary<(int,int),List<HumanActor>>();
    int npcAnimalContactFrame=-1;
    void IndexWildlifeHumans()
    {
        foreach(var key in wildlifeHumanNeighbours.Where(p=>p.Value.Count==0).Select(p=>p.Key).ToArray())wildlifeHumanNeighbours.Remove(key);
        foreach(var list in wildlifeHumanNeighbours.Values)list.Clear();
        foreach(var p in livingPeople.Values)
        {
            if(p.Health<=0||p.Root==null||!p.Root.activeInHierarchy)continue;
            var key=((int)Math.Floor(p.Motor.X/40),(int)Math.Floor(p.Motor.Z/40));if(!wildlifeHumanNeighbours.TryGetValue(key,out var list))wildlifeHumanNeighbours[key]=list=new List<HumanActor>();list.Add(p);
        }
    }
    IEnumerable<HumanActor> WildlifeHumanCandidates(Animal animal)
    {
        int x=(int)Math.Floor(animal.X/40),z=(int)Math.Floor(animal.Z/40);
        foreach(var offset in wildlifeNeighbourOrder)if(wildlifeHumanNeighbours.TryGetValue((x+offset.x,z+offset.z),out var list))foreach(var p in list)if(p.Health>0&&p.Root!=null&&p.Root.activeInHierarchy)yield return p;
    }
    bool AnimalNpcResponse(Animal animal,WildlifeGroup group,bool hungry,double preyDistance,double dt)
    {
        HumanActor closest=null;double distance=40;
        foreach(var p in WildlifeHumanCandidates(animal))
        {
            double dx=p.Motor.X-animal.X,dz=p.Motor.Z-animal.Z,gap=Math.Sqrt(dx*dx+dz*dz);if(gap>=distance)continue;
            // A recently perceived closer player remains the salient cue.
            if(animal.Awareness.ThreatIsPlayer&&animal.Awareness.LastCueSeconds<1&&Math.Sqrt(Math.Pow(motion.X-animal.X,2)+Math.Pow(motion.Z-animal.Z,2))<gap)continue;
            closest=p;distance=gap;
        }
        if(closest==null)return false;
        double targetY=transform.InverseTransformPoint(NpcEye(closest)).y,dxTo=closest.Motor.X-animal.X,dzTo=closest.Motor.Z-animal.Z;
        double facing=(Math.Sin(animal.Heading*Math.PI/180)*dxTo+Math.Cos(animal.Heading*Math.PI/180)*dzTo)/Math.Max(.1,distance);
        double cover=HuntingColumn(closest.Motor.X,closest.Motor.Z,out var plants,out _,out var slope)?WildlifePerception.BodyCover(plants,Math.Max(.5,targetY-closest.Root.transform.localPosition.y),slope):0;
        double light=(SkyFrame==null?1:Math.Min(1,SkyFrame.Daylight+SkyFrame.NightIlluminanceLux*.08))*(1-CurrentWeather.CloudCover*.55);
        bool sleeping=closest.CurrentActivity==HumanActivity.Sleep;
        double vision=WildlifePerception.Vision(animal.Senses,distance,facing,closest.Speed,1,cover,light,true,animal.BehaviourAction=="eat",animal.BehaviourAction=="sleep"&&animal.Life<animal.ActionUntil);
        if(vision>.02)vision*=WildlifeVisibility(animal,closest.Motor.X,closest.Motor.Z,targetY);
        double hearing=WildlifePerception.Hearing(animal.Senses,distance,sleeping?0:Math.Min(1.5,closest.Speed/3),0,CurrentWeather.RainMmPerHour);
        double scent=animal.Spawn.Profile.medium=="water"?0:WildlifePerception.Scent(animal.Senses,animal.X-closest.Motor.X,animal.Z-closest.Motor.Z,CurrentWeather.WindEastMps,CurrentWeather.WindNorthMps,CurrentWeather.RainMmPerHour)*WildlifePerception.ScentHeight(AnimalEye(animal)-targetY);
        if(vision<=.02&&hearing<=.15&&scent<=.35)return false;
        double approaching=((animal.X-closest.Motor.X)*Math.Sin(closest.Yaw*Math.PI/180)+(animal.Z-closest.Motor.Z)*Math.Cos(closest.Yaw*Math.PI/180))*closest.Speed/Math.Max(.1,distance);
        WildlifePerception.Observe(animal.Awareness,animal.Senses,dt,vision,hearing,scent,closest.Motor.X,closest.Motor.Z,distance,animal.Behaviour.FlightM,approaching);animal.Awareness.ThreatIsPlayer=false;
        // Hearing/scent can create concern without identifying a target to attack.
        if(vision<.06)return false;
        var behaviour=animal.Behaviour;bool wounded=animal.NpcAssailant==closest.ResidentId&&animal.ThreatUntil>animal.Life;
        TerrainMapViewport.Degrees(animal.Planet,out double latitude,out _);bool male=group?.Sex>=0?group.Sex==1:(animal.Spawn.Hash&1)!=0;
        bool defensive=WildlifeBehaviour.Defend(behaviour,distance,wounded,animal.CorneredUntil>animal.Life,male&&WildlifeBehaviour.Rut(behaviour,skyYear,latitude))||behaviour.Predator&&wounded&&distance<4;
        bool atWater=false;
        if(animal.Spawn.Profile.medium=="water")
        {var ground=walkingPatch.Sample(closest.Planet);atWater=double.IsFinite(ground.WaterSurfaceM)&&ground.ElevationM<ground.WaterSurfaceM+.15;if(!atWater)return false;}
        int support=0;foreach(var p in WildlifeHumanCandidates(animal))if(p!=closest&&p.GroupId==closest.GroupId&&p.Resident.AgeYears>=16&&HumanDistance(p.Planet,closest.Planet)<10)support++;
        bool vulnerable=closest.Resident.AgeYears<14||closest.Health<35||sleeping;
        double boldness=Rng.Mix(animal.Spawn.Hash^HumanHash(closest.ResidentId)^0xbca74d19u)/(double)uint.MaxValue;
        bool hunting=hungry&&distance<preyDistance&&support<2&&behaviour.CanHunt&&behaviour.HasNaturalHistory&&animal.Health>animal.MaximumHealth*.6&&WildlifeNaturalHistory.HuntPerson(behaviour.NaturalHistory,group?.Hunger??.5,boldness,vulnerable,atWater);
        if(!defensive&&!hunting)return false;
        if(defensive&&!wounded&&animal.DefendWarnUntil==0){animal.DefendWarnUntil=animal.Life+2;animal.TargetSpeed=0;animal.BehaviourAction="watch";AnimalRole(animal,AvailableAnimalRole(animal,"alert","idle"));return true;}
        if(defensive&&!wounded&&animal.Life<animal.DefendWarnUntil){animal.TargetSpeed=0;animal.BehaviourAction="watch";return true;}
        animal.NpcHuntTarget=closest.ResidentId;animal.HuntTargetId=0;animal.ActionUntil=0;SetAnimalTravel(animal,closest.Motor.X,closest.Motor.Z,defensive||distance<12);animal.BehaviourAction=defensive?"defend":"hunt";
        if(distance<Math.Max(1.2,animal.Spawn.Profile.lengthM*.6))AnimalContactNpc(animal,closest);return true;
    }
    void AnimalContactNpc(Animal attacker,HumanActor target)
    {
        if(target==null||target.Health<=0||attacker.Dead||attacker.Life<attacker.AttackAt||npcAnimalContactFrame==Time.frameCount)return;
        float height=(target.AgeModel?.EyeHeight??target.Definition.EyeHeight)*target.Root.transform.localScale.y+.12f;
        var bounds=new Bounds(target.Root.transform.position+transform.up*height*.5f,new Vector3(height*.35f,height,height*.35f));
        Vector3 from=attacker.Renderer.bounds.ClosestPoint(bounds.center),to=bounds.ClosestPoint(from);float allowance=Mathf.Clamp((float)attacker.Spawn.Profile.lengthM*.15f,.15f,.6f);
        if((to-from).sqrMagnitude>allowance*allowance)return;
        var start=attacker.Renderer.bounds.center;Vector3 direction=(bounds.center-start).normalized;float reach=Vector3.Distance(start,bounds.center)+height*.4f;
        if(TraceCombatObstacle(new Ray(start,direction),Math.Max(0,Vector3.Distance(start,bounds.center)-.2f),checkGround:false)!=null)return;
        npcAnimalContactFrame=Time.frameCount;var hit=TraceLiving(new Ray(start,direction),reach);
        if(hit?.Human!=target)return;
        attacker.AttackAt=attacker.Life+1.7;attacker.TargetSpeed=0;AnimalVoiceEvent(attacker,"threat");
        if(attacker.Model.Asset.Clips.Any(c=>c.role=="attack")){attacker.BehaviourAction="attack";attacker.ActionUntil=attacker.Life+.65;AnimalRole(attacker,"attack");}
        double damage=Math.Max(2,Math.Min(40,5*Math.Pow(attacker.MassKg,.3)));
        target.Bleeding??=new BleedingState();BleedingRules.Wound(target.Bleeding,damage/100,.55,1,.025+Math.Min(.1,attacker.MassKg*.0002),TorsoContact(hit));
        DamageHuman(target,damage,hit,direction,attacker.MassKg*.1,0,attacker.Spawn.Id);
        if(target.Health<=0){attacker.NpcHuntTarget=0;AnimalActivity(attacker,"eat",15);}
    }
}
