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
        var clip=animal.Model.Asset.Clips.FirstOrDefault(c=>c.role.Equals(role,StringComparison.OrdinalIgnoreCase)||c.name.Equals(role,StringComparison.OrdinalIgnoreCase))??animal.Model.Asset.Clips.FirstOrDefault(c=>c.role=="walk")??animal.Model.Asset.Clips.FirstOrDefault(c=>c.role=="idle")??animal.Model.Asset.Clips[0];
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
        var profile=animal.Spawn.Profile;if(profile.tiny)return;
        var behaviour=AnimalBehaviour(profile,animal.Spawn.Taxon);double now=Time.unscaledTimeAsDouble;
        double distance=Math.Sqrt(Math.Pow(animal.X-motion.X,2)+Math.Pow(animal.Z-motion.Z,2));
        double dt=animal.LastPerception==0?.3:Math.Max(0,Math.Min(3,animal.Life-animal.LastPerception));animal.LastPerception=animal.Life;
        animal.BehaviourAt=now+(distance<75?.3:distance<200?1:3)+(animal.Spawn.Hash%7)*.013;
        TerrainMapViewport.Degrees(animal.Planet,out double latitude,out _);
        PerceivePlayer(animal,dt,distance);
        wildlifeGroups.TryGetValue(animal.Spawn.GroupId,out var group);
        bool hungry=group==null?animal.Life%180>100:group.Hunger>.35;
        Animal threat=null,prey=null;double threatDistance=double.MaxValue,preyDistance=double.MaxValue;int considered=0;
        Taxon taxon=animal.Spawn.Taxon>=0&&animal.Spawn.Taxon<sourceWorld.Ecology.Taxa.Length?sourceWorld.Ecology.Taxa[animal.Spawn.Taxon]:null;
        foreach(var other in NearbyAnimals(animal))
        {
            if(other.Dead||++considered>24)continue;
            double gap=Math.Sqrt(Math.Pow(other.X-animal.X,2)+Math.Pow(other.Z-animal.Z,2));
            if(behaviour.Herd||profile.slug.Contains("kangaroo"))ShareAnimalAlarm(animal,other,gap);
            var ob=AnimalBehaviour(other.Spawn.Profile,other.Spawn.Taxon);
            if(ob.Predator&&other.Spawn.Profile.slug!=profile.slug&&other.MassKg>animal.MassKg*.3&&gap<threatDistance){threat=other;threatDistance=gap;}
            if(behaviour.Predator&&hungry&&!ob.Predator&&other.Spawn.Profile.slug!=profile.slug&&WildlifeBehaviour.EdiblePrey(animal.MassKg,behaviour.PackHunter,taxon?.MinPreyKg??0,taxon?.MaxPreyKg??0,other.MassKg)&&gap<preyDistance){prey=other;preyDistance=gap;}
        }
        if(threat!=null)
        {
            double hearing=WildlifePerception.Hearing(animal.Senses,threatDistance,Math.Min(1.5,threat.Speed/3),0,CurrentWeather.RainMmPerHour);
            double scent=WildlifePerception.Scent(animal.Senses,animal.X-threat.X,animal.Z-threat.Z,CurrentWeather.WindEastMps,CurrentWeather.WindNorthMps,CurrentWeather.RainMmPerHour);
            if(threatDistance<animal.Senses.VisionM&&(hearing>.35||scent>.65||AnimalSight(animal,threat)))
            {
                if(threatDistance<Math.Max(10,behaviour.FlightM)||hearing>.65)WildlifePerception.Alarm(animal.Awareness,threat.X,threat.Z);
                else WildlifePerception.Observe(animal.Awareness,animal.Senses,dt,.35,hearing,scent,threat.X,threat.Z);
                if(behaviour.Defensive&&threatDistance<behaviour.DefendM&&animal.MassKg>threat.MassKg)
                {SetAnimalTravel(animal,threat.X,threat.Z,true);AnimalContactAttack(animal,threat);return;}
            }
        }
        bool dangerous=profile.slug.Contains("lion")||profile.slug.Contains("tiger")||profile.slug.Contains("polar-bear")||behaviour.PackHunter&&group?.Members>=3;
        bool mayHuntPlayer=behaviour.Predator&&dangerous&&hungry&&!animal.ThreatWasPlayer&&animal.Health>animal.MaximumHealth*.6;
        if(animal.Awareness.Mode==WildlifeAwareness.Fleeing&&!mayHuntPlayer)
        {
            bool lastPlayer=animal.Awareness.Sense!=WildlifeSense.Social&&Math.Sqrt(Math.Pow(animal.Awareness.LastX-motion.X,2)+Math.Pow(animal.Awareness.LastZ-motion.Z,2))<15;
            bool defence=behaviour.Defensive&&distance<behaviour.DefendM||behaviour.Predator&&animal.ThreatWasPlayer&&distance<4;
            if(lastPlayer&&defence&&WildlifeSight(animal,motion.X,motion.Z))
            {animal.ActionUntil=0;animal.BehaviourAction="defend";SetAnimalTravel(animal,animal.Awareness.LastX,animal.Awareness.LastZ,true);AnimalContactAttack(animal,null);return;}
            FleeKnownThreat(animal);return;
        }
        if(prey!=null&&preyDistance<animal.Senses.VisionM&&AnimalSight(animal,prey))
        {
            animal.ActionUntil=0;animal.BehaviourAction="hunt";
            bool charge=preyDistance<16||prey.Awareness.Mode==WildlifeAwareness.Fleeing;
            SetAnimalTravel(animal,prey.X,prey.Z,charge);if(!charge)animal.TargetSpeed*=.65f;
            if(preyDistance<Math.Max(.7,profile.lengthM*.7))AnimalContactAttack(animal,prey);return;
        }
        // Large opportunistic predators stalk a perceived human, rather than
        // attacking every player anywhere in a hard-coded detection circle.
        if(dangerous&&hungry&&animal.Awareness.Suspicion>.5&&distance<35&&WildlifeSight(animal,motion.X,motion.Z)&&SurvivalActive)
        {animal.ActionUntil=0;SetAnimalTravel(animal,motion.X,motion.Z,distance<12);animal.BehaviourAction="hunt";if(distance<Math.Max(1.2,profile.lengthM*.6))AnimalContactAttack(animal,null);return;}
        RoutineAnimal(animal,behaviour,group,latitude);
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
        AnimalVoiceEvent(attacker,"threat");
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
