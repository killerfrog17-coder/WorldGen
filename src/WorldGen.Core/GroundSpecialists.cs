using System;

namespace WorldGen.Core
{
    public enum SpecialistForm { Flower, Rosette, Spike, Fern, Fungus }
    public enum GroundFungalRole { None, Decomposer, RootAssociate }
    /// <summary>Derived regional herb-layer taxon, not an Earth species template,
    /// saved population, or historical food-web upgrade.</summary>
    public sealed class GroundSpecialist
    {
        public ulong Id;
        public int RegionId,ProvinceId,MorphologyCode;
        public string Name="",Founder="";
        public SpecialistForm Form;
        public GroundFungalRole FungalRole;
        public double HeightM,WidthM,Moisture,Shade,MinWarmC,MaxWarmC,MinPH,MaxPH,MaxSalinity,ColonyM;
        public HerbMorphology Morphology=>HerbMorphology.Decode(MorphologyCode);
        public PlantMotionTraits Motion=>PlantMotion.Herb(MorphologyCode);
    }
    public struct HerbMorphology
    {
        public SpecialistForm Form;
        public int Petals,LeafPairs,Heads,Fronds;
        public double PetalAspect,Opening,HeadRadius,LeafWidth,Bend,CapRoundness;
        public V Blossom,Leaf,Stem;
        static V Hsv(double h,double s,double v)
        {
            double C(double offset){double k=(h*6+offset)%6;return v*(1-s*Math.Max(0,Math.Min(1,Math.Min(k,4-k))));}
            return new V(C(5),C(3),C(1));
        }
        public static HerbMorphology Decode(int code)
        {
            var r=new Rng(code&~15,"herb-morphology-1");double branch=.92+(code&15)/15.0*.16;
            var form=(SpecialistForm)((code>>24)&7);double hue=r.Unit();if(hue>.18&&hue<.5)hue=.10+hue*.08;
            double saturation=r.Unit()<.27?.04+.12*r.Unit():.35+.48*r.Unit();
            V blossom=Hsv(hue,saturation,.72+.25*r.Unit());
            if(form==SpecialistForm.Fungus)blossom=Hsv(.035+.10*r.Unit(),.12+.48*r.Unit(),.48+.42*r.Unit());
            return new HerbMorphology{Form=form,Petals=3+(int)(r.Unit()*6),LeafPairs=2+(int)(r.Unit()*4),Heads=1+(int)(r.Unit()*3),Fronds=4+(int)(r.Unit()*4),
                PetalAspect=(.45+1.1*r.Unit())*branch,Opening=.12+.65*r.Unit(),HeadRadius=(.045+.07*r.Unit())*branch,
                LeafWidth=(.035+.055*r.Unit())*branch,Bend=(.025+.10*r.Unit())*branch,CapRoundness=.22+.62*r.Unit(),
                Blossom=blossom*branch,Leaf=Hsv(.23+.10*r.Unit(),.48+.3*r.Unit(),.30+.20*r.Unit()),Stem=Hsv(.22+.1*r.Unit(),.4+.25*r.Unit(),.27+.15*r.Unit())};
        }
    }
    public static class GroundSpecialists
    {
        public const string Version="ground-specialists-1";
        static readonly string[] syllables={"al","bel","cor","dar","el","fen","gal","hel","ir","jun","kel","lin","mor","nel","or","pel","ran","sil","tor","val"};
        static string Word(ref Rng r)=>syllables[(int)(r.Unit()*syllables.Length)]+syllables[(int)(r.Unit()*syllables.Length)];
        public static double Canopy(Biome biome)=>biome==Biome.TropicalRainforest?.92:biome==Biome.TemperateRainforest?.88:biome==Biome.BorealForest?.78:biome==Biome.TemperateForest?.74:biome==Biome.TropicalSeasonalForest?.6:biome==Biome.MediterraneanWoodland?.35:biome==Biome.Mangrove?.65:.08;
        public static GroundSpecialist[] Create(World world,int region,int province)
        {
            var ecology=world.Ecology;HabitatCell h=null;
            if(ecology!=null&&region>=0&&region<ecology.Regions.Length){int cell=ecology.Regions[region].RepresentativeCell;if(cell>=0&&cell<ecology.Habitat.Length)h=ecology.Habitat[cell];}
            double warm=h==null?18:h.WarmTenthsC*.1,wet=h==null?.8:h.AridityMilli*.001,shade=h==null?.2:Canopy(h.Biome);
            bool cold=h!=null&&(h.Biome==Biome.Tundra||h.Biome==Biome.Alpine);
            var result=new GroundSpecialist[10];
            for(int i=0;i<result.Length;i++)
            {
                var founder=new Rng(world.Recipe.Seed,"ground-specialist-founder-1",unchecked(province*31+i));
                var branch=new Rng(world.Recipe.Seed,"ground-specialist-branch-1",unchecked(region*31+i));
                var form=i<4?SpecialistForm.Flower:i<6?SpecialistForm.Spike:i==6?SpecialistForm.Rosette:i==7?SpecialistForm.Fern:SpecialistForm.Fungus;
                int genome=(int)(founder.Next()&0x00fffff0)|(int)(branch.Unit()*16)|((int)form<<24);
                string genus=Word(ref founder)+"a",epithet=Word(ref branch)+"is";
                double height=form==SpecialistForm.Fungus?.045+.20*founder.Unit():form==SpecialistForm.Rosette?.06+.14*founder.Unit():form==SpecialistForm.Fern?.25+.65*founder.Unit():.22+.65*founder.Unit();
                height*=cold?.20+.20*branch.Unit():.85+.3*branch.Unit();
                var species=new GroundSpecialist{Id=((ulong)(uint)region<<32)|(uint)i,RegionId=region,ProvinceId=province,MorphologyCode=genome,Form=form,
                    Name=char.ToUpperInvariant(genus[0])+genus.Substring(1)+" "+epithet,Founder=genus+" / "+form,
                    FungalRole=form==SpecialistForm.Fungus?(i==8?GroundFungalRole.Decomposer:GroundFungalRole.RootAssociate):GroundFungalRole.None,
                    HeightM=D.Q(height,1000),WidthM=D.Q(height*(form==SpecialistForm.Fungus?.8+founder.Unit():form==SpecialistForm.Rosette?1.6:form==SpecialistForm.Fern?1.5:.45+.4*founder.Unit()),1000),
                    Moisture=D.Clamp(wet*(.65+.65*founder.Unit()),.15,2.2),Shade=D.Clamp(shade+(founder.Unit()-.5)*.65),
                    MinWarmC=Math.Max(-5,warm-8-12*founder.Unit()),MaxWarmC=warm+8+10*founder.Unit(),MinPH=4.2+1.2*founder.Unit(),MaxPH=7.2+1.3*founder.Unit(),
                    MaxSalinity=.025+.08*founder.Unit(),ColonyM=5+22*founder.Unit()};
                if(form==SpecialistForm.Fern){species.Moisture=Math.Max(.75,species.Moisture);species.Shade=Math.Max(.48,species.Shade);}
                if(form==SpecialistForm.Fungus){species.Moisture=Math.Max(.6,species.Moisture);species.Shade=i==8?Math.Max(.25,species.Shade):Math.Max(.55,species.Shade);}
                if(form!=SpecialistForm.Fungus&&h!=null&&(h.Biome==Biome.SaltMarsh||h.Biome==Biome.Mangrove))species.MaxSalinity=.6+.35*founder.Unit();
                result[i]=species;
            }
            return result;
        }
        public static double Suitability(GroundSpecialist s,HabitatCell h,GroundProfile soil,Biome biome,double warm,double shade)
        {
            if(biome==Biome.Ocean||biome==Biome.Ice||biome==Biome.Desert||biome==Biome.Freshwater||biome==Biome.SalineLake)return 0;
            double wet=h==null?.8:h.AridityMilli*.001;
            wet*=.75+.35*D.Clamp(soil.WaterCapacityMm/100)+.2*(1-soil.Drainage);
            double moisture=Math.Exp(-Math.Pow((wet-s.Moisture)/Math.Max(.25,s.Moisture*.65),2));
            double light=Math.Exp(-Math.Pow((shade-s.Shade)/.45,2));
            double temperature=D.Clamp((warm-s.MinWarmC)/5)*D.Clamp((s.MaxWarmC-warm)/5);
            double ph=D.Clamp((soil.PH-s.MinPH)/.7)*D.Clamp((s.MaxPH-soil.PH)/.7),salt=1-D.Clamp(soil.Salinity/Math.Max(.01,s.MaxSalinity));
            double flooding=h==null?0:h.FloodFractionPermille*.001;
            double roots=D.Clamp(soil.DepthM/.18)*(.45+.55*soil.Fertility)*(1-.7*soil.Gravel);
            double floodPenalty=.8-.6*D.Clamp((s.Moisture-.9)/.8);
            double suitability=moisture*light*temperature*ph*salt*roots*(1-floodPenalty*flooding);
            if(s.Form==SpecialistForm.Fungus)suitability*=D.Clamp((wet-.3)/.55)*(.2+.8*soil.Organic)*(s.FungalRole==GroundFungalRole.RootAssociate?D.Clamp((shade-.18)/.5):.4+.6*shade);
            if(s.Form==SpecialistForm.Fern)suitability*=D.Clamp((wet-.35)/.55)*D.Clamp((shade-.12)/.4);
            return D.Clamp(suitability);
        }
    }
}
