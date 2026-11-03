using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    // The same material-backed structures as player construction. Only nearby
    // camps are materialised; this creates no permanent world-sized object list.
    public static class HumanCampLayout
    {
        public static void FitShelter(CampObject shelter,double[] floorHeights,double completion=1)
        {
            if(shelter?.Blueprint==null||floorHeights==null||floorHeights.Length!=9||floorHeights.Any(h=>!double.IsFinite(h)))throw new ArgumentException("A shelter and nine finite footing heights are required.");
            shelter.Blueprint.FloorHeights=(double[])floorHeights.Clone();FillShelter(shelter,D.Clamp(completion));
        }
        static void FillShelter(CampObject shelter,double completion)
        {
            var plan=shelter.Blueprint;var components=CampBlueprints.Components(plan).OrderBy(c=>c.Stage).ToArray();plan.Parts.Clear();
            double budget=components.Sum(c=>c.Seconds)*completion;
            foreach(var part in components)
            {
                if(budget+1e-7<part.Seconds)break;budget-=part.Seconds;
                var kind=part.Kind==CampPartKind.Stonework?PossessionKind.LargeRock:part.Kind==CampPartKind.Bonework?PossessionKind.Bone:part.Kind==CampPartKind.Binding?PossessionKind.Cordage:part.Kind==CampPartKind.Cover?plan.Cover==CampCover.Hide?PossessionKind.WorkedHide:plan.Cover==CampCover.Bark?PossessionKind.BarkSheet:plan.Cover==CampCover.Reed?PossessionKind.ReedBundle:PossessionKind.DryGrass:PossessionKind.Branch;
                var material=PortableTechnology.Item(kind)!=null?PortableTechnology.Create(kind,shelter.Position):new Possession{Kind=kind,Position=shelter.Position,WoodSoundness=.9,WoodStraightness=.8};
                material.Id=shelter.Id+"-material-"+part.Key;
                if(part.Kg>0)material.MassKg=part.Kg;
                if(part.Kind==CampPartKind.Deck){material.HeightM=1;material.WidthM=.03;material.WoodSoundness=.9;}
                if(part.Kg==0){material.HeightM=part.LengthM+.01;material.WidthM=part.WidthM*1.1;material.DepthM=material.WidthM;material.MassKg=Math.Max(.01,material.HeightM*material.WidthM*material.WidthM*450);}
                plan.Parts.Add(new CampPartState{Key=part.Key,WorkSeconds=part.Seconds,Materials=new List<Possession>{material}});
            }
        }
        public static void AgeBedding(CampObject bed,double calendarDays,double annualRainMm)
        {
            if(bed.Kind!=CampKind.Bedding||calendarDays<=0)return;
            double decay=annualRainMm>2000?28:annualRainMm>800?60:120;
            foreach(var material in bed.Materials)if(!CampRules.Hide(material))material.MassKg*=Math.Exp(-calendarDays/decay);
            bed.Materials.RemoveAll(material=>material.MassKg<.000001);
        }
        public static V HouseholdOffset(ForagerBandSample people,long household,long site,int seed)
        {
            var homes=people.Members.Select(p=>p.Household).Distinct().OrderBy(h=>h).ToArray();int index=Array.IndexOf(homes,household);
            double orientation=Rng.Value(seed,"camp-layout",unchecked((int)site^(int)(site>>32)))*Math.PI*2;
            double jitter=Rng.Value(seed,"household-layout",unchecked((int)household^(int)(household>>32)));
            if(people.Residence==ForagerResidence.AquaticResidence)
            {
                double across=(index/2-(Math.Ceiling(homes.Length/2d)-1)*.5)*6.2;
                double along=(index%2==0?-1:1)*(4.3+jitter*.6);
                return new V(across*Math.Cos(orientation)+along*Math.Sin(orientation),0,along*Math.Cos(orientation)-across*Math.Sin(orientation));
            }
            if(people.Residence==ForagerResidence.CollectorCamps)
            {
                int cluster=index/3,clusters=(int)Math.Ceiling(homes.Length/3d);double theta=(index%3)*Math.PI*2/3+orientation;
                double cx=(cluster%3-(Math.Min(3,clusters)-1)*.5)*15,cz=(cluster/3-(Math.Ceiling(clusters/3d)-1)*.5)*15;
                double east=cx+Math.Sin(theta)*4.2,north=cz+Math.Cos(theta)*4.2;
                return new V(east*Math.Cos(orientation)+north*Math.Sin(orientation),0,north*Math.Cos(orientation)-east*Math.Sin(orientation));
            }
            double bearing=orientation+Math.PI*2*Math.Max(0,index)/Math.Max(1,homes.Length)+(jitter-.5)*.35;
            double ring=Math.Max(6,Math.Sqrt(homes.Length)*3.6)*(.85+.3*Rng.Value(seed,"household-court",unchecked((int)household)));
            return new V(Math.Sin(bearing)*ring,0,Math.Cos(bearing)*ring);
        }
        public static double Radius(ForagerBandSample people,long site,int seed)
            =>people.Members.Select(p=>p.Household).Distinct().Select(h=>HouseholdOffset(people,h,site,seed).Length).DefaultIfEmpty(6).Max()+6;
        public static V HearthOffset(ForagerBandSample people,int hearth,long site,int seed)
        {
            var homes=people.Members.Select(p=>p.Household).Distinct().OrderBy(h=>h).Skip(hearth*3).Take(3).ToArray();
            if(homes.Length==0)return new V();var sum=new V();foreach(long h in homes)sum+=HouseholdOffset(people,h,site,seed);
            return sum/homes.Length*(people.Residence==ForagerResidence.CollectorCamps?.96:people.Residence==ForagerResidence.AquaticResidence?.85:.42);
        }
        public static CampObject[] Create(HumanGroupSituation group,ForagerBandSample people,V anchor,double radiusM,int seed,double campSeconds=0)
        {
            var result=new List<CampObject>();long band=group.Band.Id,site=group.Camp.Id;
            string prefix="npc-"+band.ToString("x")+"-"+site.ToString("x");
            double Unit(string stream,long key)=>Rng.Value(seed,stream,unchecked((int)key^(int)(key>>32)));
            V At(double east,double north)
            {
                var e=V.Cross(new V(0,0,1),anchor).Unit;if(e.Length<.1)e=new V(0,1,0);var n=V.Cross(anchor,e).Unit;
                double length=Math.Sqrt(east*east+north*north);return length<1e-6?anchor:(anchor*Math.Cos(length/radiusM)+(e*east+n*north)*(Math.Sin(length/radiusM)/length)).Unit;
            }
            CampObject Object(CampKind kind,string name,V position)=>new CampObject{Id=prefix+"-"+name,Kind=kind,NpcGroup=band,NpcSite=site,Position=position,LastSimulatedSeconds=campSeconds,Heading=Unit("camp-orientation",site)*360};
            Possession Item(PossessionKind kind,V position,string key,double kg=0)
            {
                var p=PortableTechnology.Item(kind)!=null?PortableTechnology.Create(kind,position):new Possession{Kind=kind,Position=position,WoodSoundness=.9,WoodStraightness=.8};
                p.Id=prefix+"-material-"+key;if(kg>0)p.MassKg=kg;return p;
            }
            double completion=group.Phase==HumanCampPhase.SettingUp?group.Progress:group.Phase==HumanCampPhase.Packing?1-group.Progress:group.Phase==HumanCampPhase.Travelling?0:1;
            var households=people.Members.GroupBy(p=>p.Household).OrderBy(h=>h.Key).ToArray();
            double ring=Math.Max(6,Math.Sqrt(households.Length)*3.6),orientation=Unit("camp-layout",site)*Math.PI*2;
            foreach(var household in households)
            {
                var offset=HouseholdOffset(people,household.Key,site,seed);double bearing=Math.Atan2(offset.X,offset.Z);
                var position=At(offset.X,offset.Z);
                double floorW=group.Camp.ShelterWidth,floorD=group.Camp.ShelterDepth;
                if(group.Camp.ShelterAvailable)
                {
                    var shelter=Object(CampKind.Blueprint,"house-"+household.Key,position);
                    shelter.NpcHousehold=household.Key;
                    shelter.Heading=bearing*180/Math.PI+180; // openings face the shared centre
                    var plan=CampBlueprints.Create(group.Camp.Design,group.Camp.ShelterWidth+.18*Math.Max(0,household.Count()-4),group.Camp.ShelterDepth+.15*Math.Max(0,household.Count()-4),group.Camp.Cover);shelter.Blueprint=plan;
                    if(plan.Design==CampDesign.LeanTo)shelter.Heading=bearing*180/Math.PI; // high/open side is local -Z
                    if(CampBlueprints.Has(plan.Design,CampFunction.Shelter))
                    {
                        int places=(int)Math.Ceiling(household.Sum(p=>p.AgeYears>=13?1:p.AgeYears<3?.2:.55));
                        for(int step=0;step<36&&CampBlueprints.SleepingPlaces(plan)<places;step++)
                        {
                            var limits=CampBlueprints.Limits(plan.Design);double w=plan.WidthM,d=plan.DepthM;
                            if(w<limits.maxW&&(w<=d||d>=limits.maxD))w+=.25;else if(d<limits.maxD)d+=.25;else break;
                            plan=CampBlueprints.Create(plan.Design,w,d,plan.Cover);shelter.Blueprint=plan;
                        }
                        if(CampBlueprints.SleepingPlaces(plan)<places&&plan.Design!=CampDesign.ConicalTent)
                        {plan=CampBlueprints.Create(CampDesign.LongHut,Math.Max(3,plan.WidthM),Math.Min(8,places*1.7/.75/Math.Max(3,plan.WidthM)),plan.Cover);shelter.Blueprint=plan;}
                    }
                    floorW=plan.WidthM;floorD=plan.DepthM;
                    FillShelter(shelter,completion);
                    if(plan.Parts.Count>0)result.Add(shelter);
                }
                if(completion>.25)
                {
                    var bed=Object(CampKind.Bedding,"bed-"+household.Key,position);bed.Heading=bearing*180/Math.PI;
                    bed.NpcHousehold=household.Key;
                    bed.ClearedWidthM=floorW;bed.ClearedDepthM=floorD;
                    bed.Materials.Add(Item(group.Camp.Cover==CampCover.Reed?PossessionKind.ReedBundle:PossessionKind.DryGrass,position,"bed-"+household.Key,.9+Math.Min(5,household.Count())*.5));result.Add(bed);
                }
            }
            if(group.Band.Techniques[5]&&completion>.1)
            {
                int hearths=Math.Max(1,(int)Math.Ceiling(households.Length/3d));
                for(int k=0;k<hearths;k++)
                {
                    var hearthOffset=HearthOffset(people,k,site,seed);var position=At(hearthOffset.X,hearthOffset.Z);
                    var fire=Object(CampKind.Fire,"hearth-"+k,position);fire.AshKg=.12;
                    // Shared meals and evening seats need a usable trampled
                    // court, not only a bare patch underneath the flame itself.
                    fire.ClearedWidthM=fire.ClearedDepthM=7;
                    fire.Materials.Add(Item(PossessionKind.KindlingBundle,position,"hearth-"+k+"-kindling",.18));
                    var wood=Item(PossessionKind.Branch,position,"hearth-"+k+"-wood",2.4);wood.HeightM=.4;wood.WidthM=.04;wood.DepthM=.04;fire.Materials.Add(wood);
                    bool active=group.LocalHour<8||group.LocalHour>16||group.Camp.Cover==CampCover.Hide;
                    fire.Burning=active&&group.Phase!=HumanCampPhase.Packing;fire.EmberSeconds=active?3600:1600;result.Add(fire);
                }
            }
            return result.ToArray();
        }
    }
}
