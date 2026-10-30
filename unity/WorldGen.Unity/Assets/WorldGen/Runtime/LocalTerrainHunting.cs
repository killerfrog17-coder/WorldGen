using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    double huntingCoverAt,huntingCover,huntingLight=1,huntingSpeed,huntingNoiseUntil,huntingNoiseStrength;
    Vector3 huntingNoisePoint;readonly RaycastHit[] huntingRayHits=new RaycastHit[16];
    bool PlayerCrouching=>!motion.Flying&&!motion.Swimming&&ActiveKeyboard?.ctrlKey.isPressed==true;
    void RefreshHuntingCover()
    {
        if(Time.unscaledTimeAsDouble<huntingCoverAt)return;huntingCoverAt=Time.unscaledTimeAsDouble+.35;
        var s=walkingPatch.Sample(Position);double height=PlayerCrouching?1.05:1.7;
        huntingCover=Math.Min(.8,s.Cover.Grasses.Length*Math.Max(0,s.Cover.Stature-.12)/height);
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
    void ReportHuntingNoise(Vector3 position,double loudness)
    {if(loudness<=0)return;huntingNoisePoint=transform.InverseTransformPoint(position);huntingNoiseStrength=Math.Max(huntingNoiseStrength,loudness);huntingNoiseUntil=Time.unscaledTimeAsDouble+2;}
    void PerceivePlayer(Animal animal,double dt,double distance)
    {
        RefreshHuntingCover();var senses=animal.Senses;double dx=motion.X-animal.X,dz=motion.Z-animal.Z;
        double facing=(Math.Sin(animal.Heading*Math.PI/180)*dx+Math.Cos(animal.Heading*Math.PI/180)*dz)/Math.Max(.01,distance);
        double visual=0,sound=0,scent=0;bool playerEligible=!Spectating&&!DeathVisible&&(!motion.Flying||HeightAboveGround<12);
        if(playerEligible&&(animal.Spawn.Profile.medium!="water"||motion.Swimming))
        {
            double posture=PlayerCrouching?.55:1,cover=huntingCover;
            // Concealment is physical vegetation, not a graphics-quality switch.
            double potential=WildlifePerception.Vision(senses,distance,facing,huntingSpeed,posture,cover,huntingLight,true,animal.BehaviourAction=="eat"||animal.BehaviourAction=="sleep");
            if(potential>.035&&WildlifeSight(animal,motion.X,motion.Z,motion.Y+humanEyeOffset))visual=potential;
            double footstep=huntingSpeed<.08?0:Math.Min(1.8,Math.Pow(huntingSpeed/3,1.3))*(PlayerCrouching?.25:1);
            double wind=Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps);
            sound=WildlifePerception.Hearing(senses,distance,footstep,wind,CurrentWeather.RainMmPerHour);
            scent=WildlifePerception.Scent(senses,-dx,-dz,CurrentWeather.WindEastMps,CurrentWeather.WindNorthMps,CurrentWeather.RainMmPerHour);
        }
        double tx=motion.X,tz=motion.Z;
        if(sound*.8>visual&&sound>=scent*.9)
        {double error=Math.Min(12,distance*.18),angle=(animal.Spawn.Hash%628)/100.0;tx+=Math.Sin(angle)*error;tz+=Math.Cos(angle)*error;}
        else if(scent*.75>Math.Max(visual,sound*.8))
        {
            double wind=Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps);
            tx=animal.X-CurrentWeather.WindEastMps/Math.Max(.1,wind)*Math.Min(distance,35);tz=animal.Z-CurrentWeather.WindNorthMps/Math.Max(.1,wind)*Math.Min(distance,35);
        }
        if(Time.unscaledTimeAsDouble<huntingNoiseUntil)
        {
            double nd=Math.Sqrt(Math.Pow(huntingNoisePoint.x+originX-animal.X,2)+Math.Pow(huntingNoisePoint.z+originZ-animal.Z,2));
            double noise=WildlifePerception.Hearing(senses,nd,huntingNoiseStrength,0,CurrentWeather.RainMmPerHour);
            if(noise>sound){sound=noise;tx=huntingNoisePoint.x+originX;tz=huntingNoisePoint.z+originZ;}
        }
        else huntingNoiseStrength=0;
        WildlifePerception.Observe(animal.Awareness,senses,dt,visual,sound,scent,tx,tz);
        if(animal.Awareness.Suspicion>.5&&Time.unscaledTimeAsDouble>animal.AlertVoiceAt)
        {animal.AlertVoiceAt=Time.unscaledTimeAsDouble+12+animal.Spawn.Hash%9;AnimalVoiceEvent(animal,"alarm");}
    }
    bool ShareAnimalAlarm(Animal animal,Animal neighbour,double distance)
    {
        if(neighbour.Awareness.Mode!=WildlifeAwareness.Fleeing||neighbour.Awareness.LastCueSeconds>10||distance>28)return false;
        bool related=neighbour.Spawn.GroupId!=0&&neighbour.Spawn.GroupId==animal.Spawn.GroupId||neighbour.Spawn.Profile.slug==animal.Spawn.Profile.slug;
        if(!related&&(!AnimalBehaviour(animal.Spawn.Profile,animal.Spawn.Taxon).Herd||!AnimalBehaviour(neighbour.Spawn.Profile,neighbour.Spawn.Taxon).Herd))return false;
        if(!AnimalSight(animal,neighbour)&&distance>12)return false;
        if(animal.Awareness.Suspicion<.78){WildlifePerception.Alarm(animal.Awareness,neighbour.Awareness.LastX,neighbour.Awareness.LastZ);animal.Awareness.Suspicion=.82;}
        return true;
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
    void RoutineAnimal(Animal animal,WildlifeBehaviourProfile b,WildlifeGroup group,double latitude)
    {
        var p=animal.Spawn.Profile;
        if(p.medium=="air"&&UpdateBirdBehaviour(animal,b))return;
        if(animal.Awareness.Mode==WildlifeAwareness.Suspicious||animal.Awareness.Mode==WildlifeAwareness.Alert||animal.Awareness.Mode==WildlifeAwareness.Searching)
        {
            animal.TargetHeading=(float)(Math.Atan2(animal.Awareness.LastX-animal.X,animal.Awareness.LastZ-animal.Z)*180/Math.PI);animal.TargetSpeed=0;animal.BehaviourAction="watch";animal.ActionUntil=0;AnimalRole(animal,"idle");return;
        }
        if(animal.Life<animal.ActionUntil){animal.TargetSpeed=0;if(group!=null&&animal.BehaviourAction=="eat")group.Hunger=Math.Max(0,group.Hunger-.015);return;}
        double activity=WildlifeBehaviour.Activity(b,LocalSolarHour,WildlifeBehaviour.IsNocturnal(p.slug,p.nocturnal),CurrentWeather.RainMmPerHour,Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps));
        if(group!=null)
        {
            var at=GroupMemberPosition(animal.Spawn,group);TerrainHandoff.Coordinates(frame,at,out double gx,out double gz);double gap=Math.Sqrt(Math.Pow(gx-animal.X,2)+Math.Pow(gz-animal.Z,2));
            if(group.Intent==WildlifeIntent.Drink&&gap<3){AnimalActivity(animal,"drink",8+animal.Random.Unit()*9);return;}
            if(group.Intent==WildlifeIntent.Rest||group.Intent==WildlifeIntent.Shelter)
            {
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
        if(p.medium=="ground"&&choice>(.25+.4*activity)){AnimalActivity(animal,activity<.35?"sleep":"eat",10+animal.Random.Unit()*20);return;}
        if(p.medium=="ground"&&choice<.35){AnimalActivity(animal,"eat",7+animal.Random.Unit()*16);return;}
        double a=animal.Random.Unit()*Math.PI*2,reach=p.medium=="air"?35:4+animal.Random.Unit()*9;
        SetAnimalTravel(animal,animal.X+Math.Sin(a)*reach,animal.Z+Math.Cos(a)*reach,false);animal.BehaviourAction="forage";
        if(p.medium=="air"&&animal.Model.Asset.Clips.Any(c=>c.role=="glide")&&animal.Random.Unit()<.5)AnimalRole(animal,"glide");
    }
}
