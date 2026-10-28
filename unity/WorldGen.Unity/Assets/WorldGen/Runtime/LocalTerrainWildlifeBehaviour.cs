using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    readonly Dictionary<(int,int),List<Animal>> wildlifeNeighbours=new Dictionary<(int,int),List<Animal>>();double nextWildlifeIndex;
    double wildlifeVisualCover;
    void IndexWildlifeNeighbours()
    {
        if(!WildlifeBehaviourEnabled||Time.unscaledTimeAsDouble<nextWildlifeIndex)return;nextWildlifeIndex=Time.unscaledTimeAsDouble+.5;
        var sample=walkingPatch.Sample(Position);wildlifeVisualCover=TreeFlora.CanopyShade(sample.Forest,Position,terrain.SourceSeed,frame.RadiusM);
        foreach(var key in wildlifeNeighbours.Where(p=>p.Value.Count==0).Select(p=>p.Key).ToArray())wildlifeNeighbours.Remove(key);
        foreach(var list in wildlifeNeighbours.Values)list.Clear();
        foreach(var animal in animals.Values)
        {
            var key=((int)Math.Floor(animal.X/40),(int)Math.Floor(animal.Z/40));if(!wildlifeNeighbours.TryGetValue(key,out var list)){list=new List<Animal>();wildlifeNeighbours.Add(key,list);}list.Add(animal);
        }
    }
    IEnumerable<Animal> NearbyAnimals(Animal animal)
    {
        int x=(int)Math.Floor(animal.X/40),z=(int)Math.Floor(animal.Z/40);
        for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)if(wildlifeNeighbours.TryGetValue((x+dx,z+dz),out var list))foreach(var candidate in list)if(candidate!=animal&&!candidate.Retired&&candidate.Root!=null)yield return candidate;
    }
    void AnimalRole(Animal animal,string role)
    {
        var clip=animal.Model.Asset.Clips.FirstOrDefault(c=>c.role==role)??animal.Model.Asset.Clips.FirstOrDefault(c=>c.role=="walk")??animal.Model.Asset.Clips.FirstOrDefault(c=>c.role=="idle")??animal.Model.Asset.Clips[0];
        if(clip==animal.Clip)return;double phase=animal.Clip==null?0:animal.Time/Math.Max(.01,animal.Clip.duration)%1;animal.OldClip=animal.Clip;animal.OldTime=animal.Time;animal.Time=phase*clip.duration;animal.Clip=clip;animal.BlendAge=0;
        animal.NominalSpeed=(float)(clip.role=="run"?animal.Spawn.Profile.runMps:clip.role=="trot"?(animal.Spawn.Profile.runMps+animal.Spawn.Profile.walkMps)*.5:animal.Spawn.Profile.walkMps);animal.NominalSpeed=Math.Max(.01f,animal.NominalSpeed);
    }
    void SetAnimalTravel(Animal animal,double x,double z,bool run)
    {
        animal.TargetHeading=(float)(Math.Atan2(x-animal.X,z-animal.Z)*180/Math.PI);animal.TargetSpeed=(float)(run?animal.Spawn.Profile.runMps:animal.Spawn.Profile.walkMps);
        AnimalRole(animal,animal.Spawn.Profile.medium=="air"?"fly":animal.Spawn.Profile.medium=="water"?"swim":run?"run":"walk");
    }
    bool WildlifeSight(Animal animal,double x,double z,double targetY=double.NaN)
    {
        if(double.IsNaN(targetY))targetY=motion.Y;
        double y=animal.Root.transform.localPosition.y+Math.Max(.1,animal.Spawn.Profile.lengthM*animal.Model.Asset.Height*.6);
        for(int i=1;i<6;i++){double t=i/6.0,px=animal.X+(x-animal.X)*t,pz=animal.Z+(z-animal.Z)*t,py=y+(targetY-y)*t;
            if(VisibleGround(px,pz,double.NegativeInfinity)>py+.1)return false;}
        Vector3 start=transform.TransformPoint(new Vector3((float)(animal.X-originX),(float)y,(float)(animal.Z-originZ))),end=transform.TransformPoint(new Vector3((float)(x-originX),(float)targetY,(float)(z-originZ)));
        var delta=end-start;return TraceCombatObstacle(new Ray(start,delta.normalized),Mathf.Max(0,delta.magnitude-.3f),null,false)==null;
    }
    bool AnimalSight(Animal observer,Animal target)=>!target.Retired&&target.Root!=null&&WildlifeSight(observer,target.X,target.Z,target.Root.transform.localPosition.y+Math.Max(.1,target.Spawn.Profile.lengthM*target.Model.Asset.Height*.55));
    void UpdateAnimalBehaviour(Animal animal)
    {
        if(!WildlifeBehaviourEnabled||animal.Dead||Time.unscaledTimeAsDouble<animal.BehaviourAt)return;
        var profile=animal.Spawn.Profile;var behaviour=AnimalBehaviour(profile,animal.Spawn.Taxon);
        double playerDistance=Math.Sqrt(Math.Pow(animal.X-motion.X,2)+Math.Pow(animal.Z-motion.Z,2));
        animal.BehaviourAt=Time.unscaledTimeAsDouble+(playerDistance<60?.25:playerDistance<200?.8:2.5)+(animal.Spawn.Hash%5)*.01;
        TerrainMapViewport.Degrees(animal.Planet,out double latitude,out _);bool hurt=animal.ThreatUntil>animal.Life;
        double wind=Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps),downwind=wind>0?((animal.X-motion.X)*CurrentWeather.WindEastMps+(animal.Z-motion.Z)*CurrentWeather.WindNorthMps)/Math.Max(1,playerDistance*wind):0;
        double danger=behaviour.NoticeM*(survivalSprinting?1.2:.85)*(1-wildlifeVisualCover*.45)*(1+downwind*.2*wind/(wind+2))/(1+CurrentWeather.RainMmPerHour*.015);
        bool seesPlayer=!Spectating&&!DeathVisible&&HeightAboveGround<8&&playerDistance<danger&&WildlifeSight(animal,motion.X,motion.Z);
        if(animal.PerchSelected&&seesPlayer&&playerDistance<behaviour.FlightM){animal.PerchSelected=false;animal.Perched=false;animal.AirTargetHeight=Math.Max(4,animal.Spawn.HeightM);}
        if(seesPlayer&&(playerDistance<behaviour.FlightM||hurt&&animal.ThreatWasPlayer))
        {
            bool defend=behaviour.Defensive&&(playerDistance<behaviour.DefendM||hurt&&playerDistance<14)||behaviour.Predator&&hurt&&playerDistance<3.5;
            if(behaviour.Herd&&WildlifeBehaviour.Rut(skyYear,latitude)&&(animal.Spawn.Hash&1)==0&&playerDistance<3.5)defend=true;
            if(defend){SetAnimalTravel(animal,motion.X,motion.Z,true);if(playerDistance<Math.Max(1.2,profile.lengthM*.65))AnimalContactAttack(animal,null);return;}
            if(behaviour.Defensive&&!hurt){animal.TargetHeading=(float)(Math.Atan2(motion.X-animal.X,motion.Z-animal.Z)*180/Math.PI);animal.TargetSpeed=0;AnimalRole(animal,"idle");return;}
            double dx=animal.X-motion.X,dz=animal.Z-motion.Z;SetAnimalTravel(animal,animal.X+dx*3,animal.Z+dz*3,true);animal.ThreatUntil=Math.Max(animal.ThreatUntil,animal.Life+4);animal.ThreatX=motion.X;animal.ThreatZ=motion.Z;
            if(wildlifeGroups.TryGetValue(animal.Spawn.GroupId,out var fleeing)){fleeing.Intent=WildlifeIntent.Flee;fleeing.Goal=frame.Position(animal.X+dx*4,animal.Z+dz*4);fleeing.NextDecisionDays=PlayerRules.SolarDays+12/(PlayerRules.Settings.RealMinutesPerDay*60);}
            return;
        }
        Animal predator=null,prey=null;double threatDistance=double.MaxValue,preyDistance=double.MaxValue;
        bool hungry=!wildlifeGroups.TryGetValue(animal.Spawn.GroupId,out var group)||group.Hunger>.3;
        Taxon taxon=animal.Spawn.Taxon>=0&&animal.Spawn.Taxon<sourceWorld.Ecology.Taxa.Length?sourceWorld.Ecology.Taxa[animal.Spawn.Taxon]:null;
        foreach(var other in NearbyAnimals(animal))
        {
            if(other.Dead)continue;double distance=Math.Sqrt(Math.Pow(other.X-animal.X,2)+Math.Pow(other.Z-animal.Z,2));var otherBehaviour=AnimalBehaviour(other.Spawn.Profile,other.Spawn.Taxon);
            if(otherBehaviour.Predator&&other.Spawn.Profile.slug!=profile.slug&&other.MassKg>animal.MassKg*.3&&distance<threatDistance){predator=other;threatDistance=distance;}
            bool edible=behaviour.Predator&&hungry&&other.Spawn.Profile.slug!=profile.slug&&WildlifeBehaviour.EdiblePrey(animal.MassKg,behaviour.PackHunter,taxon?.MinPreyKg??animal.MassKg*.015,taxon?.MaxPreyKg??0,other.MassKg);
            if(behaviour.Predator&&edible&&!otherBehaviour.Predator&&distance<preyDistance){prey=other;preyDistance=distance;}
        }
        if(predator!=null&&threatDistance<Math.Max(8,behaviour.FlightM)&&AnimalSight(animal,predator))
        {
            if(behaviour.Defensive&&threatDistance<behaviour.DefendM*1.6&&animal.MassKg>predator.MassKg)
            {SetAnimalTravel(animal,predator.X,predator.Z,true);if(threatDistance<Math.Max(1.2,profile.lengthM*.65))AnimalContactAttack(animal,predator);}
            else SetAnimalTravel(animal,animal.X+(animal.X-predator.X)*3,animal.Z+(animal.Z-predator.Z)*3,true);return;
        }
        if(prey!=null&&hungry&&preyDistance<55&&AnimalSight(animal,prey))
        {SetAnimalTravel(animal,prey.X,prey.Z,true);if(preyDistance<Math.Max(.6,profile.lengthM*.65))AnimalContactAttack(animal,prey);return;}
        if(hurt&&animal.Life<animal.ThreatUntil)
        {SetAnimalTravel(animal,animal.X+(animal.X-animal.ThreatX)*3,animal.Z+(animal.Z-animal.ThreatZ)*3,true);return;}
        if(profile.medium=="air"&&!profile.tiny&&UpdateBirdBehaviour(animal,behaviour))return;
        if(group!=null&&!animal.Manual)
        {
            var destination=GroupMemberPosition(animal.Spawn,group);TerrainHandoff.Coordinates(frame,destination,out double gx,out double gz);
            double gap=Math.Sqrt(Math.Pow(gx-animal.X,2)+Math.Pow(gz-animal.Z,2));
            if(group.Intent==WildlifeIntent.Rest||group.Intent==WildlifeIntent.Shelter||gap<2){animal.TargetSpeed=0;AnimalRole(animal,"idle");}
            else SetAnimalTravel(animal,gx,gz,group.Intent==WildlifeIntent.Flee||group.Intent==WildlifeIntent.Hunt&&gap>12);
        }
        else
        {
            double active=WildlifeBehaviour.Activity(behaviour,LocalSolarHour,profile.nocturnal,CurrentWeather.RainMmPerHour,Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps));
            if((animal.Spawn.Hash+(uint)(animal.Life/10))%100>active*100){animal.TargetSpeed=0;AnimalRole(animal,"idle");}
        }
        if(group!=null)animal.NextDecision=Math.Max(animal.NextDecision,animal.Life+4);
    }
    bool ReachesAnimalContact(Animal attacker,Animal prey)
    {
        if(attacker==null||attacker.Retired||attacker.Dead||attacker.Root==null||attacker.Renderer==null)return false;
        Bounds targetBounds;
        if(prey!=null)
        {
            if(prey.Retired||prey.Dead||prey.Root==null||prey.Renderer==null)return false;
            targetBounds=prey.Renderer.bounds;
        }
        else
        {
            if(humanPlayer==null||humanPlayer.Root==null)return false;
            float height=Mathf.Max(.8f,humanPlayer.Definition.EyeHeight+.12f);
            targetBounds=new Bounds(humanPlayer.Root.transform.position+Vector3.up*height*.5f,new Vector3(height*.34f,height,height*.34f));
        }
        Vector3 from=attacker.Renderer.bounds.ClosestPoint(targetBounds.center),to=targetBounds.ClosestPoint(from);
        float allowance=Mathf.Clamp((float)attacker.Spawn.Profile.lengthM*.15f,.15f,.6f);
        return (to-from).sqrMagnitude<=allowance*allowance;
    }
    void AnimalContactAttack(Animal attacker,Animal prey)
    {
        if(!ReachesAnimalContact(attacker,prey))return;
        attacker.TargetSpeed=0;if(Time.unscaledTimeAsDouble<attacker.AttackAt)return;attacker.AttackAt=Time.unscaledTimeAsDouble+1.7;
        double damage=Math.Max(2,Math.Min(40,5*Math.Pow(attacker.MassKg,.3)));Vector3 direction;
        if(prey!=null)
        {
            direction=(prey.Root.transform.position-attacker.Root.transform.position).normalized;
            DamageAnimal(prey,damage,prey.Renderer.bounds.ClosestPoint(attacker.Root.transform.position+Vector3.up*.5f),direction,attacker.MassKg*.1);
            if(prey.Dead&&wildlifeGroups.TryGetValue(attacker.Spawn.GroupId,out var group)){group.Hunger=Math.Max(0,group.Hunger-.6);group.Intent=WildlifeIntent.Rest;group.NextDecisionDays=PlayerRules.SolarDays+25/(PlayerRules.Settings.RealMinutesPerDay*60);}
        }
        else
        {
            if(humanPlayer==null||!SurvivalActive)return;direction=(humanPlayer.Root.transform.position-attacker.Root.transform.position).normalized;
            CombatHit hit=null;float closest=float.MaxValue;Vector3 contact=attacker.Root.transform.position+Vector3.up*(float)Math.Min(1.4,attacker.Spawn.Profile.lengthM*.5);
            foreach(var renderer in humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var skin=PosedHuman(renderer);for(int i=0;i<skin.Triangles.Length;i+=3){int a=skin.Triangles[i],b=skin.Triangles[i+1],c=skin.Triangles[i+2];var p=(skin.Vertices[a]+skin.Vertices[b]+skin.Vertices[c])/3;float d=(p-contact).sqrMagnitude;if(d>=closest)continue;closest=d;hit=new CombatHit{Human=humanPlayer,HumanRenderer=renderer,Point=p,Normal=-direction,Face=i/3,Vertices=skin.Vertices,Uvs=skin.Uvs,Triangles=skin.Triangles};}
            }
            DamageHuman(humanPlayer,damage,hit,direction,attacker.MassKg*.1);
        }
    }
    void AvoidAnimalSolids(Animal animal)
    {
        if(!WildlifeBehaviourEnabled||animal.Spawn.Profile.medium!="ground"||animal.TargetSpeed<.1||Time.unscaledTimeAsDouble<animal.ObstacleAt)return;
        double distance=(animal.Planet-Position).Length*frame.RadiusM;animal.ObstacleAt=Time.unscaledTimeAsDouble+(distance<70?.35:1.2);if(distance>180)return;
        float radius=Mathf.Clamp((float)animal.Spawn.Profile.lengthM*.13f,.025f,.7f),height=Mathf.Clamp((float)(animal.Spawn.Profile.lengthM*animal.Model.Asset.Height*.4),.08f,1.5f),reach=radius+Mathf.Min(1.8f,animal.Speed*.35f+.4f);
        Vector3 direction=transform.TransformDirection(Quaternion.Euler(0,animal.Heading,0)*Vector3.forward),side=Vector3.Cross(direction,Vector3.up),origin=animal.Root.transform.position+Vector3.up*height;
        foreach(float offset in new[]{0f,-radius,radius})
        {
            var obstacle=TraceCombatObstacle(new Ray(origin+side*offset,direction),reach);if(obstacle==null)continue;
            var normal=transform.InverseTransformDirection(obstacle.Normal);float angle=Mathf.Atan2(normal.x,normal.z)*Mathf.Rad2Deg,turn=(animal.Spawn.Hash&1)==0?80:-80;
            animal.TargetHeading=angle+turn;animal.TargetSpeed=Math.Min(animal.TargetSpeed,Math.Max(.1f,(float)animal.Spawn.Profile.walkMps*.35f));break;
        }
    }
}
