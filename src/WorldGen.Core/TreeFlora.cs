using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    public enum TreeGrowthForm { BroadCrown, NeedleCrown, FrondCrown, TreeFern, ArborescentSucculent, TallCane }
    public enum TreeLeafShape { Oval, Lanceolate, Lobed, Compound, NeedleSpray, PinnateFrond, FanFrond, Fleshy }
    public enum TreeBarkPattern { Smooth, Fissured, Scaly, Fibrous, Peeling }
    public enum TreeLeafHabit { Evergreen, ColdDeciduous, DroughtDeciduous }
    public enum TreeFruitForm { Spores, Cone, FleshyFruit, Nut, DryCapsule, WingedSeed }
    public enum TreeCrownStrategy { General, TropicalCanopy, OpenWoodland }

    /// <summary>Species-space anatomy: inherited founder traits, a small province
    /// branch and smaller regional variation. Low three code bits are individuals'
    /// reusable structural variants, NEVER a second species or different material.</summary>
    public readonly struct TreeMorphology
    {
        public readonly TreeGrowthForm Form;
        public readonly TreeCrownStrategy Strategy;
        public readonly TreeLeafShape LeafShape;
        public readonly TreeBarkPattern BarkPattern;
        public readonly double CrownBase, CrownAspect, TrunkRadius, BranchLift, Forking, LeafAspect, LeafLobes, Openness;
        public readonly double Flexibility, WoodDensityKgM3, HardnessIndex, DecayResistance, RelativeGrowth;
        public readonly V Leaf, Bark, Heartwood, Sapwood, Blossom, Fruit;
        public readonly int Branches, Leaflets;
        public TreeMorphology(int code)
        {
            Form=(TreeGrowthForm)((code>>28)&7);
            Strategy=Form==TreeGrowthForm.BroadCrown?(TreeCrownStrategy)((code>>22)&3):TreeCrownStrategy.General;
            int founder=(code>>8)&16383;var r=new Rng(founder,"tree-founder-anatomy-1");
            var province=new Rng((code>>24)&15,"tree-province-anatomy-1",founder);
            var local=new Rng((code>>3)&31,"tree-regional-anatomy-1",founder);
            double branch=.94+.12*province.Unit(),mutation=.97+.06*local.Unit();
            CrownBase=(.28+.23*r.Unit())*branch;
            CrownAspect=(.55+.55*r.Unit())*branch;BranchLift=.15+.55*r.Unit();Forking=.35+.65*r.Unit();
            if(Strategy==TreeCrownStrategy.TropicalCanopy){CrownBase=.58+.10*r.Unit();CrownAspect=.85+.20*r.Unit();BranchLift=.35+.25*r.Unit();}
            if(Strategy==TreeCrownStrategy.OpenWoodland){CrownBase=.64+.10*r.Unit();CrownAspect=1.05+.25*r.Unit();BranchLift=.12+.16*r.Unit();}
            TrunkRadius=(.014+.015*r.Unit())*branch;Branches=8+(int)(r.Unit()*6);Leaflets=5+(int)(r.Unit()*4);
            if(Strategy==TreeCrownStrategy.TropicalCanopy)TrunkRadius*=.55;
            LeafAspect=.22+.48*r.Unit();LeafLobes=3+(int)(r.Unit()*3);Openness=.08+.30*r.Unit();
            LeafShape=Form==TreeGrowthForm.NeedleCrown?TreeLeafShape.NeedleSpray:
                Form==TreeGrowthForm.FrondCrown?(r.Unit()<.6?TreeLeafShape.PinnateFrond:TreeLeafShape.FanFrond):
                Form==TreeGrowthForm.TreeFern?TreeLeafShape.PinnateFrond:Form==TreeGrowthForm.ArborescentSucculent?TreeLeafShape.Fleshy:
                Form==TreeGrowthForm.TallCane?TreeLeafShape.Lanceolate:(TreeLeafShape)(int)(r.Unit()*4);
            BarkPattern=(TreeBarkPattern)(int)(r.Unit()*5);
            WoodDensityKgM3=(Form==TreeGrowthForm.NeedleCrown?330+260*r.Unit():420+420*r.Unit())*branch;
            HardnessIndex=D.Clamp((WoodDensityKgM3-250)/700*.75+.25*r.Unit());
            DecayResistance=.15+.75*r.Unit();RelativeGrowth=D.Clamp(1.15-WoodDensityKgM3/1100+(r.Unit()-.5)*.25);
            Flexibility=Form==TreeGrowthForm.ArborescentSucculent?0:Form==TreeGrowthForm.FrondCrown||Form==TreeGrowthForm.TallCane?.16:.025+.04*r.Unit();
            double hue=.21+.12*r.Unit(),value=(.27+.19*r.Unit())*mutation;
            Leaf=Pigment(hue,.42+.33*r.Unit(),value);
            if(Form==TreeGrowthForm.NeedleCrown)Leaf=Leaf*.75+new V(.13,.24,.19)*.25;
            double light=.24+.45*r.Unit();Bark=Pigment(.065+.045*r.Unit(),.08+.33*r.Unit(),light)*mutation;
            Heartwood=Pigment(.045+.065*r.Unit(),.22+.40*r.Unit(),.32+.4*r.Unit());
            Sapwood=Heartwood*.45+new V(.79,.71,.54)*.55;
            Blossom=Pigment(r.Unit(),.10+.55*r.Unit(),.78+.18*r.Unit());
            Fruit=Pigment(r.Unit(),.35+.50*r.Unit(),.30+.40*r.Unit());
        }
        static V Pigment(double h,double s,double v)
        {double C(double q){double k=(h*6+q)%6;return v*(1-s*Math.Max(0,Math.Min(1,Math.Min(k,4-k))));}return new V(C(5),C(3),C(1));}
    }

    public readonly struct TreeReproduction
    {
        public readonly bool HasFlowers;
        public readonly TreeFruitForm Fruit;
        public readonly double FleshToxicity, SeedToxicity, LeafToxicity, ContactIrritation, SeedNutrition, FruitDiameterM;
        public readonly double MaturationDays, PersistenceDays;
        public TreeReproduction(int code)
        {
            var form=(TreeGrowthForm)((code>>28)&7);var r=new Rng(code&~7,"tree-reproduction-1");
            HasFlowers=form!=TreeGrowthForm.TreeFern&&form!=TreeGrowthForm.NeedleCrown;
            Fruit=form==TreeGrowthForm.TreeFern?TreeFruitForm.Spores:form==TreeGrowthForm.NeedleCrown?TreeFruitForm.Cone:
                form==TreeGrowthForm.FrondCrown?TreeFruitForm.FleshyFruit:(TreeFruitForm)(2+(int)(r.Unit()*4));
            FleshToxicity=Fruit==TreeFruitForm.FleshyFruit&&r.Unit()>.6?.12+.88*r.Unit():0;
            SeedToxicity=Fruit==TreeFruitForm.Spores?0:r.Unit()<.45?0:.1+.9*r.Unit();
            LeafToxicity=r.Unit()<.3?0:.1+.9*r.Unit();ContactIrritation=r.Unit()<.12?.2+.7*r.Unit():0;
            SeedNutrition=Fruit==TreeFruitForm.Nut?.65+.3*r.Unit():.25+.4*r.Unit();
            FruitDiameterM=Fruit==TreeFruitForm.FleshyFruit?.012+.075*r.Unit():.008+.035*r.Unit();
            MaturationDays=45+150*r.Unit();PersistenceDays=10+160*r.Unit();
        }
    }

    public sealed class TreeSpecies
    {
        public ulong Id;public int RegionId,ProvinceId,FamilyId,MorphologyCode;
        public string Name="",FamilyName="",ProvinceBranch="";
        public TreeGrowthForm Form;public TreeLeafHabit LeafHabit;
        public double HeightM,WidthM,LeafLengthM,Moisture,ShadeTolerance,MinWarmC,MaxWarmC,MinColdC,MaxSalinity,FloodTolerance,DroughtTolerance;
        public double LongevityYears,SeedMassG;
        public TreeMorphology Morphology;
        public TreeReproduction Reproduction;
    }

    public struct ForestCoverProfile
    {
        public V Color;public double Coverage,HeightM,WidthM,Openness,WarmC,ReferenceElevationM,GapThreshold,StandVariation;
        public int MorphologyCode;double dominantWeight;
        internal void Add(ForestCoverProfile p,double w){Color+=p.Color*w;Coverage+=p.Coverage*w;HeightM+=p.HeightM*w;WidthM+=p.WidthM*w;Openness+=p.Openness*w;WarmC+=p.WarmC*w;ReferenceElevationM+=p.ReferenceElevationM*w;GapThreshold+=p.GapThreshold*w;StandVariation+=p.StandVariation*w;if(w*p.Coverage>dominantWeight){dominantWeight=w*p.Coverage;MorphologyCode=p.MorphologyCode;}}
        internal void Divide(double w){Color/=w;Coverage/=w;HeightM/=w;WidthM/=w;Openness/=w;WarmC/=w;ReferenceElevationM/=w;GapThreshold/=w;StandVariation/=w;}
    }

    /// <summary>Static derived lineages, not literal Earth species or simulated
    /// evolution/populations. Inheritance changes bounded traits, not arbitrary
    /// random combinations at every region or plant. Saved food webs stay literal.</summary>
    public static class TreeFlora
    {
        public const string Version="tree-flora-4";
        public const double PlacementPerM2=.055;
        public static int RenderCode(int code)=>code&0x70ffff00;
        public static int ModelCode(int code,int lod)
        {if(lod>=2)return RenderCode(code);var form=(TreeGrowthForm)((code>>28)&7);return form==TreeGrowthForm.BroadCrown||form==TreeGrowthForm.NeedleCrown?code&~7:code;}
        public static double HabitatAdmission(LocalTerrainSample sample)=>D.Clamp(sample.Forest.Coverage*Stocking(sample.Forest));
        public static double SlopeAdmission(double up)=>D.Clamp((up-.60)/.22);
        public static double SpeciesWeight(TreeSpecies species,HabitatCell habitat,LocalTerrainSample sample,V at,int seed,double radius,double sourceElevation)
        {
            double fit=Suitability(species,habitat,sample.Ground,sample.Biome,sample.ElevationM-sourceElevation);
            if(fit<=0)return 0;
            double colony=.5+.5*SphereNoise.Sample(at*(radius/1000),seed^(species.MorphologyCode&~255),2.4);
            return fit*(.25+.75*D.Clamp((colony-.2)/.6));
        }
        static readonly string[] syllables={"al","ber","cer","dor","el","fen","gal","hir","il","jun","kor","lin","mer","nor","or","pel","ran","sil","tor","val"};
        static string Word(ref Rng r)=>syllables[(int)(r.Unit()*syllables.Length)]+syllables[(int)(r.Unit()*syllables.Length)];
        public static TreeSpecies[] Create(World world,int region,int province)
        {
            HabitatCell h=null;var e=world.Ecology;
            if(e!=null&&region>=0&&region<e.Regions.Length){int c=e.Regions[region].RepresentativeCell;if(c>=0&&c<e.Habitat.Length)h=e.Habitat[c];}
            double wet=h==null?.9:h.AridityMilli*.001,warm=h==null?18:h.WarmTenthsC*.1,cold=h==null?5:h.ColdTenthsC*.1;
            var result=new List<TreeSpecies>();
            for(int family=0;family<13;family++)
            {
                var form=family<6?TreeGrowthForm.BroadCrown:family<9?TreeGrowthForm.NeedleCrown:family==9?TreeGrowthForm.FrondCrown:
                    family==10?TreeGrowthForm.TreeFern:family==11?TreeGrowthForm.ArborescentSucculent:TreeGrowthForm.TallCane;
                if((form==TreeGrowthForm.FrondCrown||form==TreeGrowthForm.TreeFern||form==TreeGrowthForm.TallCane)&&(warm<18||cold<1))continue;
                if(form==TreeGrowthForm.ArborescentSucculent&&(warm<19||cold< -5||wet>1))continue;
                var founder=new Rng(world.Recipe.Seed,"tree-world-family-1",family);
                var branch=new Rng(world.Recipe.Seed,"tree-province-branch-1",unchecked(province*31+family));
                var local=new Rng(world.Recipe.Seed,"tree-region-branch-1",unchecked(region*31+family));
                var strategy=form!=TreeGrowthForm.BroadCrown?TreeCrownStrategy.General:
                    h!=null&&(h.Biome==Biome.TropicalRainforest||h.Biome==Biome.TropicalSeasonalForest)?TreeCrownStrategy.TropicalCanopy:
                    h!=null&&(h.Biome==Biome.Savanna||h.Biome==Biome.MediterraneanWoodland)?TreeCrownStrategy.OpenWoodland:TreeCrownStrategy.General;
                int code=((int)(founder.Next()&16383)<<8)|((int)strategy<<22)|((int)(branch.Unit()*16)<<24)|((int)(local.Unit()*32)<<3)|((int)form<<28);
                string genus=Word(ref founder)+"a",epithet=Word(ref local)+"is";
                var m=new TreeMorphology(code);double founderHeight=form==TreeGrowthForm.FrondCrown?16+12*founder.Unit():
                    form==TreeGrowthForm.TreeFern?5+7*founder.Unit():form==TreeGrowthForm.ArborescentSucculent?5+10*founder.Unit():
                    form==TreeGrowthForm.TallCane?7+13*founder.Unit():14+22*founder.Unit();
                double height=founderHeight*(.90+.20*branch.Unit())*(.95+.10*local.Unit());
                if(strategy==TreeCrownStrategy.TropicalCanopy)height=(29+10*founder.Unit())*(.94+.12*branch.Unit())*(.96+.08*local.Unit());
                if(strategy==TreeCrownStrategy.OpenWoodland)height=(7+8*founder.Unit())*(.94+.12*local.Unit());
                double width=height*(form==TreeGrowthForm.NeedleCrown?.32:form==TreeGrowthForm.FrondCrown?.55:form==TreeGrowthForm.TreeFern?.85:
                    form==TreeGrowthForm.ArborescentSucculent?.45:form==TreeGrowthForm.TallCane?.25:.45+.40*founder.Unit());
                if(strategy==TreeCrownStrategy.OpenWoodland)width=height*(1.0+.5*founder.Unit());
                if(strategy==TreeCrownStrategy.TropicalCanopy)width=height*(.55+.20*founder.Unit());
                double ancestralMoisture=.35+1.3*founder.Unit(),moisture=ancestralMoisture*.65+D.Clamp(wet,.2,2)*.35;
                if(form==TreeGrowthForm.TreeFern)moisture=Math.Max(1.1,moisture);
                if(form==TreeGrowthForm.ArborescentSucculent)moisture=.12+.2*founder.Unit();
                var habit=form==TreeGrowthForm.NeedleCrown||form==TreeGrowthForm.FrondCrown||form==TreeGrowthForm.TreeFern||form==TreeGrowthForm.ArborescentSucculent?
                    TreeLeafHabit.Evergreen:cold< -2?TreeLeafHabit.ColdDeciduous:wet<.7?TreeLeafHabit.DroughtDeciduous:TreeLeafHabit.Evergreen;
                result.Add(new TreeSpecies{Id=((ulong)(uint)region<<32)|(uint)family,RegionId=region,ProvinceId=province,FamilyId=family,MorphologyCode=code,
                    Name=char.ToUpperInvariant(genus[0])+genus.Substring(1)+" "+epithet,FamilyName=genus,ProvinceBranch=province+" / "+genus,
                    Form=form,LeafHabit=habit,HeightM=D.Q(height,1000),WidthM=D.Q(width,1000),Morphology=m,Reproduction=new TreeReproduction(code),
                    LeafLengthM=form==TreeGrowthForm.FrondCrown||form==TreeGrowthForm.TreeFern?1.5+2*founder.Unit():form==TreeGrowthForm.NeedleCrown?.015+.045*founder.Unit():.04+.15*founder.Unit(),
                    Moisture=moisture,ShadeTolerance=form==TreeGrowthForm.TreeFern?.65+.25*founder.Unit():.15+.65*founder.Unit(),
                    MinWarmC=form==TreeGrowthForm.NeedleCrown?5:form==TreeGrowthForm.BroadCrown?9:18,MaxWarmC=form==TreeGrowthForm.NeedleCrown?32:48,
                    MinColdC=form==TreeGrowthForm.NeedleCrown?-55:form==TreeGrowthForm.BroadCrown?-35:form==TreeGrowthForm.ArborescentSucculent?-5:1,
                    MaxSalinity=h!=null&&h.Biome==Biome.Mangrove?.7+.25*founder.Unit():.035+.10*founder.Unit(),FloodTolerance=h!=null&&h.Biome==Biome.Mangrove?.95:.12+.55*founder.Unit(),
                    DroughtTolerance=form==TreeGrowthForm.ArborescentSucculent?.95:D.Clamp(1.1-moisture*.55),LongevityYears=50+450*founder.Unit(),SeedMassG=.005+Math.Pow(founder.Unit(),3)*12});
            }
            return result.ToArray();
        }
        public static double Potential(Biome biome)
        {
            switch(biome)
            {
                case Biome.TropicalRainforest:case Biome.TemperateRainforest:return .92;
                case Biome.TemperateForest:return .85;
                case Biome.BorealForest:return .76;
                case Biome.TropicalSeasonalForest:return .70;
                case Biome.MediterraneanWoodland:return .26;
                case Biome.Mangrove:return .85;
                case Biome.Savanna:return .045;
                case Biome.Floodplain:return .18;
                case Biome.Grassland:return .0012;
                default:return 0;
            }
        }
        public static double Suitability(TreeSpecies s,HabitatCell h,GroundProfile soil,Biome biome,double elevationOffset=0)
        {
            // Tundra/alpine have zero interior forest potential, but a blended
            // ecotone must not be guillotined by the nearest biome label.
            // Their actual warmth/permafrost/slope still limit surviving trees.
            if(biome==Biome.Ice||biome==Biome.Ocean||biome==Biome.Freshwater||biome==Biome.SalineLake)return 0;
            // Mangrove salt/flood tolerance must not turn every unrelated
            // regional lineage into a mangrove-shaped palm or tree fern.
            if(biome==Biome.Mangrove&&s.Form!=TreeGrowthForm.BroadCrown)return 0;
            double wet=h==null?.9:h.AridityMilli*.001,warm=(h==null?18:h.WarmTenthsC*.1)-elevationOffset*.006,cold=(h==null?5:h.ColdTenthsC*.1)-elevationOffset*.006;
            double fit=D.Clamp((warm-s.MinWarmC)/5)*D.Clamp((s.MaxWarmC-warm)/7)*D.Clamp((cold-s.MinColdC)/8);
            fit*=Math.Exp(-Math.Pow((wet-s.Moisture)/Math.Max(.3,s.Moisture*.75),2));
            fit*=D.Clamp(soil.DepthM/.4)*(.4+.6*soil.Fertility)*(1-.65*soil.Gravel)*(1-D.Clamp(soil.Salinity/s.MaxSalinity));
            double flood=h==null?0:h.FloodFractionPermille*.001;fit*=1-flood*(1-s.FloodTolerance);
            if(h!=null)fit*=1-.85*h.PermafrostPermille*.001;
            if(s.Form==TreeGrowthForm.NeedleCrown)fit*=biome==Biome.BorealForest?1.8:biome==Biome.TropicalRainforest||biome==Biome.Mangrove||biome==Biome.Savanna?0:.6;
            if(s.Form==TreeGrowthForm.NeedleCrown&&biome==Biome.TropicalSeasonalForest)fit*=D.Clamp((25-warm)/7);
            if(s.Form==TreeGrowthForm.BroadCrown)fit*=biome==Biome.BorealForest?.25:1;
            if(s.Form==TreeGrowthForm.TreeFern)fit*=D.Clamp((wet-.8)/.6)*GroundSpecialists.Canopy(biome);
            if(s.Form==TreeGrowthForm.FrondCrown)fit*=biome==Biome.Mangrove?.12:.65;
            if(s.Form==TreeGrowthForm.ArborescentSucculent)fit*=D.Clamp((.8-wet)/.5);
            if(s.Form==TreeGrowthForm.TallCane)fit*=.25*D.Clamp((wet-.6)/.6);
            if(biome==Biome.Desert)return s.Form==TreeGrowthForm.ArborescentSucculent?fit*.3:0;
            return Math.Max(0,fit);
        }
        public static double Stand(V position,int seed,double physicalRadiusM,double potential)
            =>Stand(new ForestCoverProfile{Coverage=potential,GapThreshold=.38,StandVariation=.45},position,seed,physicalRadiusM);
        /// <summary>Static stand mosaic, not a simulated fire or treefall history.
        /// Regional community traits control how often small canopy gaps survive;
        /// the SAME field drives local trees, distant pages and understory light.</summary>
        public static double Stand(ForestCoverProfile profile,V position,int seed,double physicalRadiusM)
        {
            V km=position*(physicalRadiusM/1000);
            double n=.5+.5*SphereNoise.Sample(km,seed^0x6937af,3.3);
            double opening=.5+.5*SphereNoise.Sample(km,seed^0x41a8c3,19);
            double open=(.08+.92*Smooth((n-.28)/.38))*(.14+.86*Smooth((opening-.22)/.32));
            double variation=D.Clamp(profile.StandVariation);
            double closed=(1-variation+variation*Smooth((n-.25)/.48))*(.86+.14*Smooth((opening-.20)/.40));
            // The previous .16 threshold was in an almost unreachable noise
            // tail: a closed biome stayed almost identically packed for miles.
            double gap=Smooth((profile.GapThreshold-opening)/.10);
            closed*=1-.96*gap;return open+(closed-open)*Smooth((profile.Coverage-.15)/.45);
        }
        static double ExpectedCrownArea(ForestCoverProfile p)
        {
            // Account for the existing size cohorts; do not place 550 mature
            // 15-25m crowns/ha simply because 550 narrow stems fit the lattice.
            bool tropical=((p.MorphologyCode>>22)&3)==(int)TreeCrownStrategy.TropicalCanopy;
            return Math.PI*.25*p.WidthM*p.WidthM*(tropical?.31:.64);
        }
        public static double Stocking(ForestCoverProfile p)
        {
            var form=(TreeGrowthForm)((p.MorphologyCode>>28)&7);
            var strategy=(TreeCrownStrategy)((p.MorphologyCode>>22)&3);
            double budget=form==TreeGrowthForm.BroadCrown&&strategy==TreeCrownStrategy.TropicalCanopy?3.6:
                form==TreeGrowthForm.BroadCrown&&strategy==TreeCrownStrategy.OpenWoodland?1.5:2.2;
            double packed=Math.Min(1,budget/Math.Max(.001,PlacementPerM2*ExpectedCrownArea(p)));
            // Sparse sites are not competing closed stands. Applying the same
            // self-thinning to rare savanna/grassland trees erased woodlands.
            return 1+(packed-1)*Smooth((p.Coverage-.025)/.30);
        }
        public static double CanopyShade(ForestCoverProfile p,V position,int seed,double radius)
        {
            double area=PlacementPerM2*ExpectedCrownArea(p)*Stocking(p)*D.Clamp(p.Coverage)*Stand(p,position,seed,radius);
            return D.Clamp(1-Math.Exp(-area*(1-.55*D.Clamp(p.Openness))));
        }
        static double Smooth(double t){t=D.Clamp(t);return t*t*(3-2*t);}
        public static ForestCoverProfile Describe(HabitatCell h,GroundProfile soil,TreeSpecies[] species,Biome biome)
        {
            var p=new ForestCoverProfile{WarmC=h==null?18:h.WarmTenthsC*.1};double sum=0,best=0;
            foreach(var s in species){double w=Suitability(s,h,soil,biome);sum+=w;p.Color+=s.Morphology.Leaf*w;p.HeightM+=s.HeightM*w;p.WidthM+=s.WidthM*w;p.Openness+=s.Morphology.Openness*w;if(w>best){best=w;p.MorphologyCode=s.MorphologyCode;}}
            if(sum>1e-6){p.Color/=sum;p.HeightM/=sum;p.WidthM/=sum;p.Openness/=sum;}
            double potential=biome==Biome.Desert?.006:Potential(biome);
            if(biome==Biome.Savanna&&h!=null)
            {double opportunity=D.Clamp((h.AridityMilli*.001-.45)/.65)*(1-.75*h.FirePotentialPermille*.001);potential=.035+.15*opportunity*opportunity;}
            double productivity=h==null?1:D.Clamp(.65+h.ProductivityGCM2Year/1800.0,.65,1.15);
            double fire=h==null?1:1-.42*h.FirePotentialPermille*.001;
            double support=D.Clamp(sum*1.5);
            p.Coverage=potential*support*productivity*fire;
            // Small treefall gaps recover fastest in wet, closed communities.
            // Fire-prone/drier stands vary more; do not insert a meadow quota.
            bool wetForest=biome==Biome.TropicalRainforest||biome==Biome.TemperateRainforest||biome==Biome.Mangrove;
            p.GapThreshold=wetForest?.26:biome==Biome.TemperateForest?.35:biome==Biome.BorealForest?.34:.34;
            p.StandVariation=wetForest?.24:biome==Biome.TemperateForest?.52:.60;
            if(h!=null){p.GapThreshold+=.045*h.FirePotentialPermille*.001;p.StandVariation+=.15*h.FirePotentialPermille*.001;}
            return p;
        }
        public static ForestCoverProfile Surface(ForestCoverProfile p,V point,int seed,double radius,double elevation,double up,bool underwater)
        {
            p.Coverage*=underwater||elevation<2?0:Stand(p,point,seed,radius)*Stocking(p)*D.Clamp((up-.60)/.22)*D.Clamp((p.WarmC-(elevation-p.ReferenceElevationM)*.006-6)/5);
            return p;
        }
    }
}
