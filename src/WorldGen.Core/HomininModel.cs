using System;
using System.Linq;

namespace WorldGen.Core
{
    [Flags] public enum LithicKnowledge : long
    {
        None=0,Flakes=1L<<0,Woodworking=1L<<1,Handaxes=1L<<2,FireKeeping=1L<<3,Spears=1L<<4,
        FireMaking=1L<<5,Hafting=1L<<6,HideWorking=1L<<7,PreparedCores=1L<<8,Adhesives=1L<<9,
        Blades=1L<<10,BoneTools=1L<<11,Fishing=1L<<12,Watercraft=1L<<13,Projectiles=1L<<14,
        Needles=1L<<15,Symbols=1L<<16,FoodStorage=1L<<17,Microliths=1L<<18,
        PlantManagement=1L<<19,Cultivation=1L<<20,Herding=1L<<21,Pottery=1L<<22,Copper=1L<<23,Bronze=1L<<24
    }
    public enum LithicStage { LowerPaleolithic,MiddlePaleolithic,UpperPaleolithic,Epipaleolithic,Neolithic,Chalcolithic,BronzeAge }
    [Serializable] public sealed class HomininLineage
    {
        public int Id,Parent=-1,OriginRegion,FoundedYear,ExtinctYear=-1;public string Name="";
        public double Communication=.2,Learning=.25,BodyCompactness=.45,Stature=.45;
        public bool SapiensLike;
        public HomininAncestry[] FoundingAncestry=Array.Empty<HomininAncestry>();
        public HomininLineage Copy(){var l=(HomininLineage)MemberwiseClone();l.FoundingAncestry=FoundingAncestry.Select(a=>a.Copy()).ToArray();return l;}
    }
    [Serializable] public sealed class HomininTradition
    {
        public int Id,Parent=-1,FoundedYear,OriginRegion;public string Name="";
        public LithicKnowledge Knowledge=LithicKnowledge.Flakes;
        public double Teaching=.1,Network,Retention=.35,Sedentism,Cultivation,Herding;
        public HomininTradition Copy()=>(HomininTradition)MemberwiseClone();
    }
    [Serializable] public sealed class HomininAncestry
    {public int Lineage;public double Share;public HomininAncestry Copy()=>(HomininAncestry)MemberwiseClone();}
    [Serializable] public sealed class HomininPopulation
    {
        public int Id,Region,Lineage,Tradition,Language,FoundedYear,LastContactYear,LastMoveYear;
        public double People,ColdAdaptation=.25,Compactness=.45,Stature=.45,Communication=.2,Learning=.25,GeneticDrift;
        public int[] Appearance=new int[]{5000,5000,5000,5000,5000,5000,5000,5000};
        public HomininAncestry[] Ancestry=Array.Empty<HomininAncestry>();
        public HomininPopulation Copy(){var p=(HomininPopulation)MemberwiseClone();p.Ancestry=Ancestry.Select(a=>a.Copy()).ToArray();p.Appearance=(int[])Appearance.Clone();return p;}
    }
    [Serializable] public sealed class HomininHabitat
    {
        public int Region,UpdatedYear;public double WoodyCover,GrassCover,LargePrey,SmallPrey,Aquatic,Ice,TemperatureDelta,RainFactor=1;
        public HomininHabitat Copy()=>(HomininHabitat)MemberwiseClone();
    }
    [Serializable] public sealed class HomininEvent
    {public int Year,Region,Lineage,Tradition;public string Kind="",Text="";public HomininEvent Copy()=>(HomininEvent)MemberwiseClone();}
    [Serializable] public sealed class HomininHistory
    {
        public int Version=1,OriginYearsBeforePresent=PleistoceneClimate.DefaultOriginYearsBeforePresent,LastBoundaryYear,NextBoundaryYear=1000,NextPopulation=1,NextLanguage=1;
        public bool Active=true,EarthLikeTrajectory=true;
        public HomininLineage[] Lineages=Array.Empty<HomininLineage>();public HomininTradition[] Traditions=Array.Empty<HomininTradition>();
        public HomininPopulation[] Populations=Array.Empty<HomininPopulation>();public HomininHabitat[] Habitats=Array.Empty<HomininHabitat>();public HomininEvent[] Events=Array.Empty<HomininEvent>();
        public HomininHistory Copy()
        {
            var h=(HomininHistory)MemberwiseClone();h.Lineages=Lineages.Select(x=>x.Copy()).ToArray();h.Traditions=Traditions.Select(x=>x.Copy()).ToArray();
            h.Populations=Populations.Select(x=>x.Copy()).ToArray();h.Habitats=Habitats.Select(x=>x.Copy()).ToArray();h.Events=Events.Select(x=>x.Copy()).ToArray();return h;
        }
        public int YearsBeforePresent(int elapsed)=>OriginYearsBeforePresent-elapsed;
        public static LithicStage Stage(HomininTradition tradition)
        {
            var k=tradition.Knowledge;if((k&LithicKnowledge.Bronze)!=0)return LithicStage.BronzeAge;
            if((k&LithicKnowledge.Copper)!=0)return LithicStage.Chalcolithic;
            if((k&(LithicKnowledge.Cultivation|LithicKnowledge.Herding))!=0&&(tradition.Cultivation>.3||tradition.Herding>.3))return LithicStage.Neolithic;
            if((k&LithicKnowledge.Microliths)!=0)return LithicStage.Epipaleolithic;
            if((k&(LithicKnowledge.Blades|LithicKnowledge.BoneTools|LithicKnowledge.Symbols))==(LithicKnowledge.Blades|LithicKnowledge.BoneTools|LithicKnowledge.Symbols))return LithicStage.UpperPaleolithic;
            if((k&LithicKnowledge.PreparedCores)!=0)return LithicStage.MiddlePaleolithic;
            return LithicStage.LowerPaleolithic;
        }
        public static string StageName(LithicStage stage)
        {
            switch(stage){case LithicStage.LowerPaleolithic:return "Lower Paleolithic";case LithicStage.MiddlePaleolithic:return "Middle Paleolithic";case LithicStage.UpperPaleolithic:return "Upper Paleolithic";case LithicStage.Epipaleolithic:return "Epipaleolithic / Mesolithic";case LithicStage.Neolithic:return "Neolithic";case LithicStage.Chalcolithic:return "Chalcolithic";default:return "Bronze Age";}
        }
    }
}
