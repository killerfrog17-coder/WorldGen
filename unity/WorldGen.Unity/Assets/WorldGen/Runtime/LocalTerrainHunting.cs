using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    int huntingDecisionFrame=-1;long huntingDecisionTicks;int huntingDecisions,huntingDeferred;
    double huntingCoverAt,huntingCover,huntingLight=1,huntingSpeed,huntingNoiseUntil,huntingNoiseStrength;
    Vector3 huntingNoisePoint;readonly RaycastHit[] huntingRayHits=new RaycastHit[16];
    bool huntingNoiseFromPlayer=true;
    bool PlayerCrouching=>!motion.Flying&&!motion.Swimming&&ActiveKeyboard?.ctrlKey.isPressed==true;
    double HuntingGrassAdmission(double x,double z,double floor,LocalTerrainSample sample)
    {
        if(sample.Underwater||sample.Cover.Grasses.Length<=0)return 0;
        // Match the physical grass rule, rather than granting regional grass
        // camouflage on a bare cliff. Cached cover refreshes own these four
        // local probes; no per-observer vegetation or terrain scan is added.
        double gx=VisibleGround(x+.5,z,floor)-VisibleGround(x-.5,z,floor);
        double gz=VisibleGround(x,z+.5,floor)-VisibleGround(x,z-.5,floor);
        return GroundFlora.GrassSlopeAdmission(1/Math.Sqrt(1+gx*gx+gz*gz));
    }
    void RefreshHuntingCover()
    {
        if(Time.unscaledTimeAsDouble<huntingCoverAt)return;huntingCoverAt=Time.unscaledTimeAsDouble+.35;
        var s=walkingPatch.Sample(Position);double height=PlayerCrouching?1.05:1.7;
        double grass=HuntingGrassAdmission(motion.X,motion.Z,motion.Y-TerrainMotion.EyeHeightM,s);
        huntingCover=Math.Min(.8,s.Cover.Grasses.Length*Math.Max(0,s.Cover.Stature-.12)*grass/height);
        var feet=PlayerFeet();
        foreach(var chunk in scatterChunks.Values)
        {
            if(chunk.Roots==null||chunk.AllBounds.SqrDistance(feet)>9)continue;
            foreach(var group in chunk.Groups)
            {
                if(group.Bounds.SqrDistance(feet)>9)continue;
                foreach(int index in group.Indices)
                {
                    var plant=chunk.Source.Instances[index];if(plant.Kind!=ScatterKind.MidPlant&&plant.Kind!=ScatterKind.Shrub)continue;
                    var root=chunk.Roots[index];double d=Math.Sqrt(Math.Pow(root.x-feet.x,2)+Math.Pow(root.z-feet.z,2));
                    if(d<plant.WidthM*.55+.2)huntingCover=Math.Max(huntingCover,Math.Min(.95,plant.HeightM/height*.75));
                }
            }
        }
        double canopy=TreeFlora.CanopyShade(s.Forest,Position,terrain.SourceSeed,frame.RadiusM);
        huntingLight=(SkyFrame?.Daylight??1)*(1-CurrentWeather.CloudCover*.65)*(1-canopy*.75);
        if(weatherScreen!=null&&huntingLight>.12)
        {
            var ray=new Ray(view.transform.position,weatherScreen.SunDirection);
            if(Physics.RaycastNonAlloc(ray,huntingRayHits,60,~((1<<29)|(1<<30)),QueryTriggerInteraction.Ignore)>0)huntingLight*=.35;
        }
    }
    void ReportHuntingNoise(Vector3 position,double loudness,bool fromPlayer=true)
    {
        if(loudness<=0)return;double now=Time.unscaledTimeAsDouble;
        if(now<huntingNoiseUntil&&loudness<huntingNoiseStrength)return;
        huntingNoisePoint=transform.InverseTransformPoint(position);huntingNoiseStrength=loudness;huntingNoiseUntil=now+2;huntingNoiseFromPlayer=fromPlayer;
    }
    void PerceivePlayer(Animal animal,double dt,double distance)
    {
        RefreshHuntingCover();var senses=animal.Senses;double dx=motion.X-animal.X,dz=motion.Z-animal.Z;
        double facing=(Math.Sin(animal.Heading*Math.PI/180)*dx+Math.Cos(animal.Heading*Math.PI/180)*dz)/Math.Max(.01,distance);
        double eye=animal.Root.transform.localPosition.y+Math.Max(.1,animal.Spawn.Profile.lengthM*animal.Model.Asset.Height*.6),playerEye=motion.Y+humanEyeOffset;
        double range=Math.Sqrt(distance*distance+(playerEye-eye)*(playerEye-eye));
        double visual=0,sound=0,scent=0;bool playerEligible=!Spectating&&!DeathVisible&&(!motion.Flying||HeightAboveGround<12);
        if(playerEligible&&(animal.Spawn.Profile.medium!="water"||motion.Swimming))
        {
            double posture=PlayerCrouching?.55:1,cover=huntingCover;
            // Concealment is physical vegetation, not a graphics-quality switch.
            double potential=WildlifePerception.Vision(senses,range,facing,huntingSpeed,posture,cover,huntingLight,true,animal.BehaviourAction=="eat",animal.BehaviourAction=="sleep"&&animal.Life<animal.ActionUntil);
            if(potential>.035&&WildlifeSight(animal,motion.X,motion.Z,motion.Y+humanEyeOffset))visual=potential;
            double footstep=huntingSpeed<.08?0:Math.Min(1.8,Math.Pow(huntingSpeed/3,1.3))*(PlayerCrouching?.25:1);
            double wind=Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps);
            sound=WildlifePerception.Hearing(senses,range,footstep,wind,CurrentWeather.RainMmPerHour);
            // Atmospheric wind is not an underwater chemical plume.
            if(animal.Spawn.Profile.medium!="water")scent=WildlifePerception.Scent(senses,-dx,-dz,CurrentWeather.WindEastMps,CurrentWeather.WindNorthMps,CurrentWeather.RainMmPerHour)*WildlifePerception.ScentHeight(playerEye-.65-eye);
        }
        double tx=motion.X,tz=motion.Z;bool cueFromPlayer=true;
        if(sound*.8>visual&&sound>=scent*.9)
        {double error=Math.Min(12,distance*.18),angle=(animal.Spawn.Hash%628)/100.0;tx+=Math.Sin(angle)*error;tz+=Math.Cos(angle)*error;}
        else if(scent*.75>Math.Max(visual,sound*.8))
        {
            double wind=Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps);
            tx=animal.X-CurrentWeather.WindEastMps/Math.Max(.1,wind)*Math.Min(distance,35);tz=animal.Z-CurrentWeather.WindNorthMps/Math.Max(.1,wind)*Math.Min(distance,35);
        }
        if((playerEligible||!huntingNoiseFromPlayer)&&Time.unscaledTimeAsDouble<huntingNoiseUntil&&animal.Spawn.Profile.medium!="water")
        {
            double nd=Math.Sqrt(Math.Pow(huntingNoisePoint.x+originX-animal.X,2)+Math.Pow(huntingNoisePoint.z+originZ-animal.Z,2)+Math.Pow(huntingNoisePoint.y-eye,2));
            double noise=WildlifePerception.Hearing(senses,nd,huntingNoiseStrength,0,CurrentWeather.RainMmPerHour);
            if(noise>sound)
            {
                sound=noise;
                if(noise*.8>Math.Max(visual,scent*.75))
                {tx=huntingNoisePoint.x+originX;tz=huntingNoisePoint.z+originZ;cueFromPlayer=huntingNoiseFromPlayer;}
            }
        }
        else huntingNoiseStrength=0;
        WildlifePerception.Observe(animal.Awareness,senses,dt,visual,sound,scent,tx,tz);
        if(Math.Max(visual,Math.Max(sound*.8,scent*.75))>.055)animal.Awareness.ThreatIsPlayer=cueFromPlayer;
        if(animal.Awareness.Suspicion>.5&&Time.unscaledTimeAsDouble>animal.AlertVoiceAt)
        {animal.AlertVoiceAt=Time.unscaledTimeAsDouble+12+animal.Spawn.Hash%9;AnimalVoiceEvent(animal,"alert");}
    }
    bool ShareAnimalAlarm(Animal animal,Animal neighbour,double distance)
    {
        if(animal.Awareness.Suspicion>=.78||neighbour.Awareness.Mode!=WildlifeAwareness.Fleeing||neighbour.Awareness.LastCueSeconds>10||distance>28)return false;
        bool related=neighbour.Spawn.GroupId!=0&&neighbour.Spawn.GroupId==animal.Spawn.GroupId||neighbour.Spawn.Profile.slug==animal.Spawn.Profile.slug;
        if(!related&&(!AnimalBehaviour(animal.Spawn.Profile,animal.Spawn.Taxon).Herd||!AnimalBehaviour(neighbour.Spawn.Profile,neighbour.Spawn.Taxon).Herd))return false;
        if(!AnimalSight(animal,neighbour)&&distance>12)return false;
        return WildlifePerception.RelayAlarm(animal.Awareness,neighbour.Awareness);
    }
    void FleeKnownThreat(Animal animal)
    {
        var a=animal.Awareness;double dx=animal.X-a.LastX,dz=animal.Z-a.LastZ,range=Math.Sqrt(dx*dx+dz*dz);
        if(range<1){double angle=animal.Heading*Math.PI/180;dx=Math.Sin(angle);dz=Math.Cos(angle);range=1;}
        animal.PerchSelected=animal.Perched=false;animal.ActionUntil=0;animal.BehaviourAction="flee";
        if(animal.Spawn.Profile.medium=="air")animal.AirTargetHeight=Math.Max(6,animal.Spawn.HeightM);
        // Escape headings retain the last sensed location; no hidden-player tracking.
        SetAnimalTravel(animal,animal.X+dx/range*70,animal.Z+dz/range*70,true);
        if(a.MemorySeconds>0&&a.LastCueSeconds>10&&range>65){animal.TargetSpeed=Math.Min(animal.TargetSpeed,(float)Math.Max(animal.Spawn.Profile.walkMps,animal.Spawn.Profile.runMps*.45));AnimalRole(animal,"trot");}
        animal.ThreatUntil=Math.Max(animal.ThreatUntil,animal.Life+2);animal.ThreatX=a.LastX;animal.ThreatZ=a.LastZ;
        if(wildlifeGroups.TryGetValue(animal.Spawn.GroupId,out var group))
        {group.Intent=WildlifeIntent.Flee;group.Goal=frame.Position(animal.X+dx/range*100,animal.Z+dz/range*100);group.NextDecisionDays=Math.Max(group.NextDecisionDays,PlayerRules.SolarDays+20/(PlayerRules.Settings.RealMinutesPerDay*60));}
    }
    string AvailableAnimalRole(Animal animal,params string[] roles)
    {foreach(string role in roles)if(animal.Model.Asset.Clips.Any(c=>c.role.Equals(role,StringComparison.OrdinalIgnoreCase)||c.name.Equals(role,StringComparison.OrdinalIgnoreCase)))return role;return "idle";}
    void AnimalActivity(Animal animal,string activity,double seconds)
    {
        animal.TargetSpeed=0;animal.BehaviourAction=activity;animal.ActionUntil=animal.Life+seconds;
        AnimalRole(animal,activity=="eat"?AvailableAnimalRole(animal,"eating","eat","graze","grazing"):activity=="sleep"?AvailableAnimalRole(animal,"sleep","rest","sit"):activity=="drink"?AvailableAnimalRole(animal,"drink","drinking","eating"):"idle");
    }
    void SpaceHerdMember(Animal animal)
    {
        if(animal.TargetSpeed<.1||animal.BehaviourAction=="hunt"||animal.BehaviourAction=="defend")return;
        double rx=0,rz=0;int checkedCount=0;
        foreach(var other in NearbyAnimals(animal))
        {
            if(other.Dead||other.Spawn.Profile.slug!=animal.Spawn.Profile.slug)continue;if(++checkedCount>12)break;
            double dx=animal.X-other.X,dz=animal.Z-other.Z,gap=Math.Sqrt(dx*dx+dz*dz),space=(animal.Spawn.Profile.lengthM+other.Spawn.Profile.lengthM)*.28;
            if(gap<.01||gap>space)continue;rx+=dx/gap*(1-gap/space);rz+=dz/gap*(1-gap/space);
        }
        if(rx*rx+rz*rz<.001)return;double heading=animal.TargetHeading*Math.PI/180;
        animal.TargetHeading=(float)(Math.Atan2(Math.Sin(heading)+rx,Math.Cos(heading)+rz)*180/Math.PI);
    }
    void RoutineAnimal(Animal animal,WildlifeBehaviourProfile b,WildlifeGroup group,double latitude,double elapsed=.3)
    {
        var p=animal.Spawn.Profile;
        if(animal.Awareness.Mode==WildlifeAwareness.Suspicious||animal.Awareness.Mode==WildlifeAwareness.Alert||animal.Awareness.Mode==WildlifeAwareness.Searching)
        {
            animal.TargetHeading=(float)(Math.Atan2(animal.Awareness.LastX-animal.X,animal.Awareness.LastZ-animal.Z)*180/Math.PI);animal.BehaviourAction="watch";animal.ActionUntil=0;
            if(p.medium=="air"&&!animal.Perched&&p.slug!="hummingbird"){animal.TargetSpeed=(float)p.walkMps;AnimalRole(animal,"fly");}
            else {animal.TargetSpeed=0;AnimalRole(animal,AvailableAnimalRole(animal,"alert","idle"));}return;
        }
        if(animal.Life<animal.ActionUntil){animal.TargetSpeed=0;if(group!=null&&animal.BehaviourAction=="eat")group.Hunger=Math.Max(0,group.Hunger-elapsed*.006/Math.Max(1,group.Members));return;}
        if(p.medium=="air"&&UpdateBirdBehaviour(animal,b))return;
        double activity=WildlifeBehaviour.Activity(b,LocalSolarHour,WildlifeBehaviour.IsNocturnal(p.slug,p.nocturnal),CurrentWeather.RainMmPerHour,Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps),SkyFrame?.Daylight??double.NaN);
        if(group!=null)
        {
            var at=GroupMemberPosition(animal.Spawn,group);TerrainHandoff.Coordinates(frame,at,out double gx,out double gz);double gap=Math.Sqrt(Math.Pow(gx-animal.X,2)+Math.Pow(gz-animal.Z,2));
            if(group.Intent==WildlifeIntent.Drink&&gap<3){AnimalActivity(animal,"drink",8+animal.Random.Unit()*9);return;}
            if(group.Intent==WildlifeIntent.Rest||group.Intent==WildlifeIntent.Shelter)
            {
                if(p.medium=="air"&&!animal.Perched)
                {
                    // Roosting needs an actual landing/perch. A missing perch
                    // must not freeze a sleeping bird in the air.
                    if(animal.TargetSpeed<.1)SetAnimalTravel(animal,animal.X+Math.Sin(animal.Heading*Math.PI/180)*20,animal.Z+Math.Cos(animal.Heading*Math.PI/180)*20,false);
                    animal.BehaviourAction="seeking-roost";AnimalRole(animal,"fly");return;
                }
                if(group.Intent==WildlifeIntent.Rest&&b.SocialAlarm&&group.Members>=3&&animal.Spawn.GroupIndex==group.Decision%group.Members)
                {animal.TargetSpeed=0;animal.BehaviourAction="sentry";AnimalRole(animal,"idle");return;}
                if(!animal.SeekingCover&&p.medium=="ground")
                {
                    animal.SeekingCover=true;double best=0;V chosen=animal.Planet;
                    for(int i=0;i<5;i++){double angle=i*2.39996+animal.Spawn.Hash%31;var q=frame.Position(animal.X+Math.Sin(angle)*12,animal.Z+Math.Cos(angle)*12);var s=walkingPatch.Sample(q);double cover=s.Underwater?-1:s.Cover.Stature*s.Cover.Grasses.Length+s.MidCover.Coverage+s.Forest.Coverage*.2;if(cover>best){best=cover;chosen=q;}}
                    TerrainHandoff.Coordinates(frame,chosen,out animal.HomeX,out animal.HomeZ);
                }
                double d=Math.Sqrt(Math.Pow(animal.HomeX-animal.X,2)+Math.Pow(animal.HomeZ-animal.Z,2));
                if(d>1){SetAnimalTravel(animal,animal.HomeX,animal.HomeZ,false);return;}
                AnimalActivity(animal,"sleep",15+animal.Random.Unit()*30);return;
            }
            animal.SeekingCover=false;
            if(gap>Math.Max(4,p.lengthM*2)&&group.Intent!=WildlifeIntent.Forage){SetAnimalTravel(animal,gx,gz,false);animal.BehaviourAction="travel";return;}
            if(group.Intent==WildlifeIntent.Forage&&gap>35){SetAnimalTravel(animal,gx,gz,false);return;}
        }
        if(animal.Life<animal.NextDecision)return;
        animal.NextDecision=animal.Life+5+animal.Random.Unit()*10;
        double choice=animal.Random.Unit();
        Taxon data=animal.Spawn.Taxon>=0&&animal.Spawn.Taxon<sourceWorld.Ecology.Taxa.Length?sourceWorld.Ecology.Taxa[animal.Spawn.Taxon]:null;
        bool groundFood=data==null?!b.Predator:WildlifeNaturalHistory.GroundForaging(data.Eats);
        if(p.medium=="ground"&&choice>(.25+.4*activity)){AnimalActivity(animal,activity<.35||!groundFood?"sleep":"eat",10+animal.Random.Unit()*20);return;}
        if(p.medium=="ground"&&choice<.35&&groundFood){AnimalActivity(animal,"eat",7+animal.Random.Unit()*16);return;}
        double a=animal.Random.Unit()*Math.PI*2,reach=p.medium=="air"?35:4+animal.Random.Unit()*9;
        SetAnimalTravel(animal,animal.X+Math.Sin(a)*reach,animal.Z+Math.Cos(a)*reach,false);animal.BehaviourAction="forage";
        if(p.medium=="air"&&animal.Model.Asset.Clips.Any(c=>c.role=="glide")&&animal.Random.Unit()<.5)AnimalRole(animal,"glide");
    }
}
