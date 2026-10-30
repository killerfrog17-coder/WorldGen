using System;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    Taxon AnimalTaxon(Animal animal)=>animal.Spawn.Taxon>=0&&animal.Spawn.Taxon<sourceWorld.Ecology.Taxa.Length?sourceWorld.Ecology.Taxa[animal.Spawn.Taxon]:null;
    double AnimalEye(Animal animal)=>animal.Root.transform.localPosition.y+Math.Max(.03,animal.Spawn.Profile.lengthM*animal.Model.Asset.Height*.55);

    double AnimalCover(Animal animal)
    {
        if(Time.unscaledTimeAsDouble<animal.CoverAt)return animal.VisualCover;
        animal.CoverAt=Time.unscaledTimeAsDouble+2.5;
        if(animal.Spawn.Profile.medium!="ground")return animal.VisualCover=0;
        var sample=walkingPatch.Sample(animal.Planet);
        double height=Math.Max(.1,animal.Spawn.Profile.lengthM*animal.Model.Asset.Height);
        return animal.VisualCover=Math.Min(.8,(sample.Cover.Stature*sample.Cover.Grasses.Length+sample.MidCover.Coverage*.25)/height);
    }

    double AnimalVision(Animal observer,Animal target,double horizontal)
    {
        double dx=target.X-observer.X,dz=target.Z-observer.Z;
        double facing=(Math.Sin(observer.Heading*Math.PI/180)*dx+Math.Cos(observer.Heading*Math.PI/180)*dz)/Math.Max(.01,horizontal);
        double dy=AnimalEye(target)-AnimalEye(observer),range=Math.Sqrt(horizontal*horizontal+dy*dy);
        double light=(SkyFrame?.Daylight??1)*(1-CurrentWeather.CloudCover*.55)*(1-wildlifeVisualCover*.6);
        double evidence=WildlifePerception.Vision(observer.Senses,range,facing,target.Speed,1,AnimalCover(target),light,true,observer.BehaviourAction=="eat",observer.BehaviourAction=="sleep"&&observer.Life<observer.ActionUntil);
        return evidence>.035&&AnimalSight(observer,target)?evidence:0;
    }

    bool ReachablePrey(Animal hunter,Animal prey)
    {
        if(!WildlifeNaturalHistory.CompatiblePrey(AnimalTaxon(hunter),AnimalTaxon(prey)))return false;
        string medium=hunter.Spawn.Profile.medium,other=prey.Spawn.Profile.medium;
        double preyHeight=prey.Root.transform.localPosition.y-prey.GroundY;
        if(medium=="ground")
        {
            if(other=="air"&&!prey.Perched&&preyHeight>Math.Max(.5,hunter.Spawn.Profile.lengthM*.35))return false;
            if(other=="water")
            {
                double bankGap=Math.Abs(prey.Root.transform.localPosition.y-hunter.Root.transform.localPosition.y);
                if(bankGap>1||Math.Abs(prey.GroundY-hunter.GroundY)>1.5)return false;
            }
        }
        if(medium=="water"&&other!="water")
        {
            // An aquatic hunter cannot pursue land prey up an entire valley.
            var sample=walkingPatch.Sample(prey.Planet);
            if(double.IsNaN(sample.WaterSurfaceM)||sample.ElevationM>sample.WaterSurfaceM+.35)return false;
        }
        if(medium=="air"&&other=="water")
        {
            double surface=frame.SurfaceY(prey.Planet,prey.Spawn.WaterM);
            if(double.IsNaN(surface)||surface-AnimalEye(prey)>(hunter.Spawn.Profile.slug=="kingfisher"?1.2:.5))return false;
        }
        return true;
    }

    bool MayHuntPlayer(Animal animal,WildlifeBehaviourProfile profile,WildlifeGroup group,double distance)
    {
        if(!SurvivalActive||DeathVisible||Spectating||!profile.CanHunt||animal.Health<animal.MaximumHealth*.6||animal.ThreatWasPlayer)return false;
        if(!profile.HasNaturalHistory)return false;
        bool water=motion.Swimming||HeightAboveGround<.4&&distance<5&&animal.Spawn.Profile.medium=="water";
        if(animal.Spawn.Profile.medium=="water"&&!water)return false;
        double boldness=Rng.Mix(animal.Spawn.Hash^0xbca74d19u)/(double)uint.MaxValue;
        bool vulnerable=PlayerCrouching||huntingSpeed>4||Vitals.Health<35;
        return WildlifeNaturalHistory.HuntPerson(profile.NaturalHistory,group?.Hunger??.5,boldness,vulnerable,water);
    }

    bool DefendPlayer(Animal animal,WildlifeBehaviourProfile profile,double distance)
    {
        if(!SurvivalActive||!animal.Awareness.ThreatIsPlayer||animal.Awareness.LastCueSeconds>3)return false;
        bool wounded=animal.ThreatWasPlayer&&animal.ThreatUntil>animal.Life;
        if(profile.Defensive&&animal.MassKg>=250&&!wounded&&animal.DefendWarnUntil>0&&animal.Life<animal.DefendWarnUntil+10&&distance>profile.DefendM&&distance<profile.DefendM*2.5)
        {
            // A short defensive charge ends when its space is respected. Hold
            // watch briefly instead of switching straight into a fleeing run.
            animal.TargetSpeed=0;animal.BehaviourAction="watch";animal.ActionUntil=0;
            animal.TargetHeading=(float)(Math.Atan2(animal.Awareness.LastX-animal.X,animal.Awareness.LastZ-animal.Z)*180/Math.PI);AnimalRole(animal,AvailableAnimalRole(animal,"alert","idle"));return true;
        }
        if(profile.Defensive&&animal.MassKg>800&&!wounded&&distance>profile.DefendM*1.25&&distance<Math.Max(15,profile.DefendM*3)&&animal.Awareness.Suspicion>.5)
        {
            animal.TargetSpeed=0;animal.BehaviourAction="watch";animal.ActionUntil=0;
            animal.TargetHeading=(float)(Math.Atan2(animal.Awareness.LastX-animal.X,animal.Awareness.LastZ-animal.Z)*180/Math.PI);AnimalRole(animal,"idle");return true;
        }
        TerrainMapViewport.Degrees(animal.Planet,out double latitude,out _);
        bool male=wildlifeGroups.TryGetValue(animal.Spawn.GroupId,out var social)&&social.Sex>=0?social.Sex==1:(animal.Spawn.Hash&1)!=0;
        bool rut=male&&WildlifeBehaviour.Rut(profile,skyYear,latitude);
        bool danger=WildlifeBehaviour.Defend(profile,distance,wounded,animal.CorneredUntil>animal.Life,rut)||profile.Predator&&wounded&&distance<4;
        if(!danger)return false;
        if(!WildlifeSight(animal,motion.X,motion.Z))return false;
        if(animal.Spawn.Profile.slug=="porcupine")
        {
            animal.TargetSpeed=0;animal.ActionUntil=0;animal.BehaviourAction="defend";
            animal.TargetHeading=(float)(Math.Atan2(animal.X-animal.Awareness.LastX,animal.Z-animal.Awareness.LastZ)*180/Math.PI);AnimalRole(animal,"idle");
            if(distance<.7)AnimalContactAttack(animal,null);return true;
        }
        if(!wounded&&animal.DefendWarnUntil==0)
        {
            animal.DefendWarnUntil=animal.Life+1.2+(animal.Spawn.Hash%10)*.06;
            AnimalVoiceEvent(animal,"threat");
        }
        animal.ActionUntil=0;animal.BehaviourAction="defend";
        if(!wounded&&animal.Life<animal.DefendWarnUntil)
        {animal.TargetSpeed=0;animal.TargetHeading=(float)(Math.Atan2(animal.Awareness.LastX-animal.X,animal.Awareness.LastZ-animal.Z)*180/Math.PI);AnimalRole(animal,"idle");return true;}
        SetAnimalTravel(animal,animal.Awareness.LastX,animal.Awareness.LastZ,true);AnimalContactAttack(animal,null);return true;
    }

    void PursuePrey(Animal animal,Animal prey,double gap,WildlifeBehaviourProfile profile)
    {
        animal.HuntTargetId=prey.Spawn.Id;animal.HuntX=prey.X;animal.HuntZ=prey.Z;
        animal.HuntMemoryUntil=animal.Life+(profile.NaturalHistory.Hunt==WildlifeHuntStyle.Chase?25:12);
        animal.ActionUntil=0;animal.BehaviourAction="hunt";
        bool chase=profile.NaturalHistory.Hunt==WildlifeHuntStyle.Chase;
        double pounce=profile.NaturalHistory.Hunt==WildlifeHuntStyle.Ambush?5:12;
        bool charge=chase||gap<pounce||prey.Awareness.Mode==WildlifeAwareness.Fleeing;
        SetAnimalTravel(animal,prey.X,prey.Z,charge);
        if(!charge)animal.TargetSpeed*=profile.NaturalHistory.Hunt==WildlifeHuntStyle.Stalk?.45f:.65f;
        if(animal.Spawn.Profile.medium=="air")
        {
            animal.PerchSelected=animal.Perched=false;
            double altitude=prey.Root.transform.localPosition.y-animal.GroundY;
            animal.AirTargetHeight=Math.Max(.12,altitude+(gap<8?.12:Math.Min(8,gap*.18)));
        }
        if(gap<Math.Max(.7,animal.Spawn.Profile.lengthM*.7))AnimalContactAttack(animal,prey);
    }

    void UpdateSmallAnimal(Animal animal,double dt)
    {
        // Insects and tiny ground/water animals use a cheap local disturbance
        // response. They do not run vegetation scans, scent plumes or pack AI.
        var p=animal.Spawn.Profile;
        double dx=animal.X-motion.X,dz=animal.Z-motion.Z,distance=Math.Sqrt(dx*dx+dz*dz);
        if(!Spectating&&!DeathVisible&&distance<Math.Min(4,animal.Senses.VisionM)&&huntingSpeed>.3&&Math.Abs(AnimalEye(animal)-motion.Y)<2)
        {
            animal.TargetHeading=(float)(Math.Atan2(dx,dz)*180/Math.PI);
            animal.TargetSpeed=(float)p.runMps;AnimalRole(animal,p.medium=="air"?"fly":p.medium=="water"?"swim":"run");
            animal.NextDecision=animal.Life+2;
        }
        else if(animal.Life>=animal.NextDecision)
        {
            if(p.medium=="water"&&animal.Behaviour.NaturalHistory.Social==WildlifeSocial.School&&UpdateSmallSchool(animal))return;
            DecideAnimal(animal);
        }
    }
    bool UpdateSmallSchool(Animal animal)
    {
        double cx=0,cz=0,hx=0,hz=0;int members=0,checkedCount=0;
        Animal disturbance=null;double nearest=25;
        foreach(var other in NearbyAnimals(animal))
        {
            if(++checkedCount>16)break;if(other.Dead||other.Spawn.Profile.medium!="water")continue;
            double dx=other.X-animal.X,dz=other.Z-animal.Z,dy=other.Root.transform.localPosition.y-animal.Root.transform.localPosition.y;
            double d2=dx*dx+dz*dz+dy*dy;
            if(other.Behaviour.CanHunt&&other.MassKg>animal.MassKg*3&&(other.Speed>.2||d2<1)&&d2<nearest)
            {disturbance=other;nearest=d2;}
            if(other.Spawn.Profile.slug!=animal.Spawn.Profile.slug||d2>64||Math.Abs(dy)>2)continue;
            cx+=dx;cz+=dz;hx+=Math.Sin(other.Heading*Math.PI/180);hz+=Math.Cos(other.Heading*Math.PI/180);members++;
            if(d2<Math.Pow(animal.Spawn.Profile.lengthM*1.5,2))
            {double separation=Math.Max(.01,d2);hx-=dx/separation;hz-=dz/separation;}
        }
        if(disturbance!=null)
        {
            animal.TargetHeading=(float)(Math.Atan2(animal.X-disturbance.X,animal.Z-disturbance.Z)*180/Math.PI);
            animal.TargetSpeed=(float)animal.Spawn.Profile.runMps;animal.NextDecision=animal.Life+.5;AnimalRole(animal,"swim");return true;
        }
        if(members==0)return false;
        animal.TargetHeading=(float)(Math.Atan2(hx+cx*.12,hz+cz*.12)*180/Math.PI);
        animal.TargetSpeed=(float)animal.Spawn.Profile.walkMps;animal.NextDecision=animal.Life+.7+animal.Spawn.Hash%5*.07;AnimalRole(animal,"swim");return true;
    }
    void ApproachCarrion(Animal animal,Animal carcass)
    {
        double angle=animal.Spawn.Hash%628/100d,margin=carcass.Spawn.Profile.lengthM*.25+animal.Spawn.Profile.lengthM*.18;
        double x=carcass.X+Math.Sin(angle)*margin,z=carcass.Z+Math.Cos(angle)*margin;
        double gap=Math.Sqrt(Math.Pow(x-animal.X,2)+Math.Pow(z-animal.Z,2));
        if(animal.Spawn.Profile.medium=="air")
        {
            animal.Perched=animal.PerchSelected=false;animal.AirTargetHeight=gap>8?Math.Min(6,gap*.15):.08;
        }
        if(gap>Math.Max(.3,animal.Spawn.Profile.lengthM*.2)||animal.Spawn.Profile.medium=="air"&&animal.AirHeight>.4)
        {SetAnimalTravel(animal,x,z,false);if(animal.Spawn.Profile.medium=="air"&&gap<5)animal.TargetSpeed=Math.Min(animal.TargetSpeed,(float)Math.Max(.1,gap*.8));animal.BehaviourAction="scavenge";return;}
        animal.HuntTargetId=0;animal.HuntMemoryUntil=0;
        if(animal.Spawn.Profile.medium=="air")
        {
            animal.Perched=animal.PerchSelected=true;animal.PerchPosition=animal.Planet;animal.PerchHeight=.03;animal.PerchUntil=animal.Life+25;animal.AirTargetHeight=.03;
        }
        animal.TargetHeading=(float)(Math.Atan2(carcass.X-animal.X,carcass.Z-animal.Z)*180/Math.PI);
        AnimalActivity(animal,"eat",18+animal.Random.Unit()*12);
    }
}
