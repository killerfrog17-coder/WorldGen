using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    // The same material-backed structures as player construction. Only nearby
    // camps are materialised; this creates no permanent world-sized object list.
    public static class HumanCampLayout
    {
        public static V HouseholdOffset(ForagerBandSample people,long household,long site,int seed)
        {
            var homes=people.Members.Select(p=>p.Household).Distinct().OrderBy(h=>h).ToArray();int index=Array.IndexOf(homes,household);
            double orientation=Rng.Value(seed,"camp-layout",unchecked((int)site^(int)(site>>32)))*Math.PI*2;
            double bearing=orientation+Math.PI*2*Math.Max(0,index)/Math.Max(1,homes.Length),ring=Math.Max(6,Math.Sqrt(homes.Length)*3.6);
            return new V(Math.Sin(bearing)*ring,0,Math.Cos(bearing)*ring);
        }
        public static CampObject[] Create(HumanGroupSituation group,ForagerBandSample people,V anchor,double radiusM,int seed,double campSeconds=0)
        {
            var result=new List<CampObject>();long band=group.Band.Id,site=group.Camp.Id;
            string prefix="npc-"+band.ToString("x")+"-"+site.ToString("x");int itemIndex=0;
            double Unit(string stream,long key)=>Rng.Value(seed,stream,unchecked((int)key^(int)(key>>32)));
            V At(double east,double north)
            {
                var e=V.Cross(new V(0,0,1),anchor).Unit;if(e.Length<.1)e=new V(0,1,0);var n=V.Cross(anchor,e).Unit;
                double length=Math.Sqrt(east*east+north*north);return length<1e-6?anchor:(anchor*Math.Cos(length/radiusM)+(e*east+n*north)*(Math.Sin(length/radiusM)/length)).Unit;
            }
            CampObject Object(CampKind kind,string name,V position)=>new CampObject{Id=prefix+"-"+name,Kind=kind,NpcGroup=band,NpcSite=site,Position=position,LastSimulatedSeconds=campSeconds,Heading=Unit("camp-orientation",site)*360};
            Possession Item(PossessionKind kind,V position,double kg=0)
            {
                var p=PortableTechnology.Item(kind)!=null?PortableTechnology.Create(kind,position):new Possession{Kind=kind,Position=position,WoodSoundness=.9,WoodStraightness=.8};
                p.Id=prefix+"-material-"+itemIndex++;if(kg>0)p.MassKg=kg;return p;
            }
            double completion=group.Phase==HumanCampPhase.SettingUp?group.Progress:group.Phase==HumanCampPhase.Packing?1-group.Progress:group.Phase==HumanCampPhase.Travelling?0:1;
            var households=people.Members.GroupBy(p=>p.Household).OrderBy(h=>h.Key).ToArray();
            double ring=Math.Max(6,Math.Sqrt(households.Length)*3.6),orientation=Unit("camp-layout",site)*Math.PI*2;
            foreach(var household in households)
            {
                int ordinal=Array.IndexOf(households,household);double bearing=orientation+Math.PI*2*ordinal/Math.Max(1,households.Length);
                var position=At(Math.Sin(bearing)*ring,Math.Cos(bearing)*ring);
                if(group.Band.Techniques[34]||group.Band.Techniques[3])
                {
                    var shelter=Object(CampKind.Blueprint,"house-"+household.Key,position);
                    shelter.Heading=bearing*180/Math.PI+180; // openings face the shared centre
                    var plan=CampBlueprints.Create(group.Camp.Design,group.Camp.ShelterWidth+.18*Math.Max(0,household.Count()-4),group.Camp.ShelterDepth,group.Camp.Cover);shelter.Blueprint=plan;
                    var components=CampBlueprints.Components(plan).OrderBy(c=>c.Stage).ToArray();
                    double budget=components.Sum(c=>c.Seconds)*completion;
                    foreach(var part in components)
                    {
                        if(budget+1e-7<part.Seconds)break;budget-=part.Seconds;
                        var kind=part.Kind==CampPartKind.Stonework?PossessionKind.LargeRock:part.Kind==CampPartKind.Bonework?PossessionKind.Bone:part.Kind==CampPartKind.Binding?PossessionKind.Cordage:part.Kind==CampPartKind.Cover?plan.Cover==CampCover.Hide?PossessionKind.WorkedHide:plan.Cover==CampCover.Bark?PossessionKind.BarkSheet:plan.Cover==CampCover.Reed?PossessionKind.ReedBundle:PossessionKind.DryGrass:PossessionKind.Branch;
                        var material=Item(kind,position,part.Kg>0?part.Kg:0);
                        if(part.Kind==CampPartKind.Deck){material.HeightM=1;material.WidthM=.03;material.WoodSoundness=.9;}
                        if(part.Kg==0){material.HeightM=part.LengthM+.01;material.WidthM=part.WidthM*1.1;material.DepthM=material.WidthM;material.MassKg=Math.Max(.01,material.HeightM*material.WidthM*material.WidthM*450);}
                        plan.Parts.Add(new CampPartState{Key=part.Key,WorkSeconds=part.Seconds,Materials=new List<Possession>{material}});
                    }
                    if(plan.Parts.Count>0)result.Add(shelter);
                }
                if(completion>.25)
                {
                    var bed=Object(CampKind.Bedding,"bed-"+household.Key,position);bed.Heading=bearing*180/Math.PI;
                    bed.Materials.Add(Item(group.Camp.Cover==CampCover.Reed?PossessionKind.ReedBundle:PossessionKind.DryGrass,position,.9+Math.Min(5,household.Count())*.5));result.Add(bed);
                }
            }
            if(group.Band.Techniques[5]&&completion>.1)
            {
                int hearths=Math.Max(1,(int)Math.Ceiling(households.Length/3d));
                for(int k=0;k<hearths;k++)
                {
                    var position=At(hearths==1?0:Math.Sin(orientation+k*Math.PI*2/hearths)*3,hearths==1?0:Math.Cos(orientation+k*Math.PI*2/hearths)*3);
                    var fire=Object(CampKind.Fire,"hearth-"+k,position);fire.AshKg=.12;
                    fire.Materials.Add(Item(PossessionKind.KindlingBundle,position,.18));
                    var wood=Item(PossessionKind.Branch,position,2.4);wood.HeightM=.4;wood.WidthM=.04;wood.DepthM=.04;fire.Materials.Add(wood);
                    bool active=group.LocalHour<8||group.LocalHour>16||group.Camp.Cover==CampCover.Hide;
                    fire.Burning=active&&group.Phase!=HumanCampPhase.Packing;fire.EmberSeconds=active?3600:1600;result.Add(fire);
                }
            }
            return result.ToArray();
        }
    }
}
