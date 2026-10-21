using System;

namespace WorldGen.Core
{
    public enum MidGrowthForm { LeafyBush, OpenScrub, DwarfHeath, ArchingCane, LargeFern, ReedStand, Succulent }
    public enum MidFruitForm { None, Berry, FleshyDrupe, DryCapsule, SeedHead }
    public enum PlantDispersal { Wind, AnimalFruit, Gravity }
    public enum PlantPollination { Wind, Animal, Mixed }

    /// <summary>Fictional ecological potential, not medical dosage or implemented
    /// harvesting. Toxicities are relative 0..1 ingestion hazards, independently
    /// specified by tissue; fruit colour never implies edibility. Species-specific
    /// animal tolerance, preparation, dose and life-stage effects remain future.</summary>
    public readonly struct MidReproduction
    {
        public readonly bool HasFlowers;
        public readonly MidFruitForm Fruit;
        public readonly PlantDispersal Dispersal;
        public readonly PlantPollination Pollination;
        public readonly double FlowerFraction, FruitFraction, FruitDiameterM;
        public readonly double FlowerToFruitDays, FruitPersistenceDays;
        public readonly double FleshToxicity, SeedToxicity, LeafToxicity, ContactIrritation, FruitNutrition;
        public MidReproduction(MidGrowthForm form, int code)
        {
            var r=new Rng(code&~3,"mid-reproduction-1");
            HasFlowers=form!=MidGrowthForm.LargeFern;
            bool fleshy=HasFlowers&&form!=MidGrowthForm.ReedStand&&r.Unit()<.65;
            Fruit=form==MidGrowthForm.LargeFern?MidFruitForm.None:form==MidGrowthForm.ReedStand?MidFruitForm.SeedHead:
                fleshy?(r.Unit()<.7?MidFruitForm.Berry:MidFruitForm.FleshyDrupe):MidFruitForm.DryCapsule;
            Pollination=form==MidGrowthForm.ReedStand?PlantPollination.Wind:r.Unit()<.8?PlantPollination.Animal:PlantPollination.Mixed;
            Dispersal=fleshy?PlantDispersal.AnimalFruit:form==MidGrowthForm.LargeFern||form==MidGrowthForm.ReedStand?PlantDispersal.Wind:PlantDispersal.Gravity;
            FlowerFraction=HasFlowers?(form==MidGrowthForm.ReedStand?.3:.10+.32*r.Unit()):0;
            FruitFraction=fleshy?.12+.28*r.Unit():0;FruitDiameterM=fleshy?.005+.018*r.Unit():0;
            FleshToxicity=fleshy?(r.Unit()<.55?0:.12+.88*r.Unit()):0;
            SeedToxicity=Fruit==MidFruitForm.None?0:r.Unit()<.40?0:.15+.85*r.Unit();
            LeafToxicity=r.Unit()<.38?0:.08+.92*r.Unit();ContactIrritation=r.Unit()<.17?.15+.75*r.Unit():0;
            FruitNutrition=fleshy?.25+.65*r.Unit():0;
            FlowerToFruitDays=fleshy?25+70*r.Unit():0;FruitPersistenceDays=fleshy?12+100*r.Unit():0;
        }
    }

    public readonly struct MidMorphology
    {
        public readonly MidGrowthForm Form;
        public readonly int Branches, Leaves, Petals;
        public readonly double LeafAspect, CrownSpread, Flexibility, Thorniness;
        public readonly V Leaf, Wood, Blossom, Fruit;
        public readonly MidReproduction Reproduction;
        public MidMorphology(int code)
        {
            Form=(MidGrowthForm)((code>>27)&7);var r=new Rng(code&~3,"mid-morphology-1");
            Branches=4+(int)(r.Unit()*5);Leaves=5+(int)(r.Unit()*5);Petals=3+(int)(r.Unit()*5);
            LeafAspect=.22+.58*r.Unit();CrownSpread=.65+.55*r.Unit();
            Flexibility=Form==MidGrowthForm.Succulent?0:Form==MidGrowthForm.ReedStand?.8:Form==MidGrowthForm.LargeFern?.65:
                Form==MidGrowthForm.DwarfHeath?.15:Form==MidGrowthForm.OpenScrub?.20:.28+.28*r.Unit();
            Thorniness=Form==MidGrowthForm.OpenScrub||Form==MidGrowthForm.ArchingCane?.3+.7*r.Unit():0;
            Leaf=new V(.18+.15*r.Unit(),.29+.16*r.Unit(),.08+.11*r.Unit());
            if(Form==MidGrowthForm.OpenScrub)Leaf=Leaf*.5+new V(.46,.45,.30)*.5;
            if(Form==MidGrowthForm.DwarfHeath)Leaf=Leaf*.55+new V(.37,.35,.25)*.45;
            Wood=new V(.25+.13*r.Unit(),.16+.11*r.Unit(),.09+.08*r.Unit());
            double hue=r.Unit();Blossom=Pigment(hue,.25+.65*r.Unit(),.75+.2*r.Unit());
            Fruit=Pigment(r.Unit(),.55+.35*r.Unit(),.25+.45*r.Unit());Reproduction=new MidReproduction(Form,code);
        }
        static V Pigment(double h,double s,double v)
        {double C(double q){double k=(h*6+q)%6;return v*(1-s*Math.Max(0,Math.Min(1,Math.Min(k,4-k))));}return new V(C(5),C(3),C(1));}
    }

    public sealed class MidPlantSpecies
    {
        public ulong Id;public int RegionId,ProvinceId,MorphologyCode;public string Name="",Founder="";
        public MidGrowthForm Form;public double HeightM,WidthM,Moisture,Shade,MinWarmC,MaxWarmC,MinPH,MaxPH,MaxSalinity,ColonyM;
        public double FloodTolerance,DroughtTolerance;
        public V LeafColor;
        public MidMorphology Morphology=>new MidMorphology(MorphologyCode);
        public MidReproduction Reproduction=>new MidReproduction(Form,MorphologyCode);
    }

    /// <summary>Derived regional mid-height flora, lazy and independent of saved
    /// ecology/history rules. Four model variants per species, not one mesh per plant.</summary>
    public static class MidFlora
    {
        public const string Version="mid-flora-1";
        static readonly string[] syllables={"al","ber","cer","dor","el","fen","gal","hir","il","jun","kor","lin","mer","nor","or","pel","ran","sil","tor","val"};
        static string Word(ref Rng r)=>syllables[(int)(r.Unit()*syllables.Length)]+syllables[(int)(r.Unit()*syllables.Length)];
        public static MidPlantSpecies[] Create(World world,int region,int province)
        {
            HabitatCell h=null;var e=world.Ecology;
            if(e!=null&&region>=0&&region<e.Regions.Length){int id=e.Regions[region].RepresentativeCell;if(id>=0&&id<e.Habitat.Length)h=e.Habitat[id];}
            double warm=h==null?18:h.WarmTenthsC*.1,wet=h==null?.8:h.AridityMilli*.001;
            var result=new MidPlantSpecies[14];
            for(int i=0;i<result.Length;i++)
            {
                var form=(MidGrowthForm)(i/2);var a=new Rng(world.Recipe.Seed,"mid-founder-1",unchecked(province*37+i));
                var b=new Rng(world.Recipe.Seed,"mid-region-1",unchecked(region*37+i));
                int code=((int)a.Next()&0x07ffffc0)|((int)(b.Unit()*16)<<2)|((int)form<<27);
                string genus=Word(ref a)+"a",name=char.ToUpperInvariant(genus[0])+genus.Substring(1)+" "+Word(ref b)+"is";
                double height=form==MidGrowthForm.DwarfHeath?.16+.44*a.Unit():form==MidGrowthForm.LargeFern?.65+1.05*a.Unit():
                    form==MidGrowthForm.ReedStand?1.2+1.8*a.Unit():form==MidGrowthForm.Succulent?.35+1.5*a.Unit():.65+1.95*a.Unit();
                if(h!=null&&(h.Biome==Biome.Tundra||h.Biome==Biome.Alpine)&&form!=MidGrowthForm.DwarfHeath)height*=.48;
                double moisture=form==MidGrowthForm.OpenScrub||form==MidGrowthForm.Succulent?.12+.35*a.Unit():
                    form==MidGrowthForm.ReedStand?1.1+.65*a.Unit():form==MidGrowthForm.LargeFern?.95+.65*a.Unit():D.Clamp(wet*(.8+.4*a.Unit()),.3,1.6);
                double shade=form==MidGrowthForm.LargeFern?.48+.30*a.Unit():form==MidGrowthForm.LeafyBush?GroundSpecialists.Canopy(h?.Biome??Biome.Grassland)*.65+.25*a.Unit():.03+.2*a.Unit();
                result[i]=new MidPlantSpecies{Id=((ulong)(uint)region<<32)|(uint)i,RegionId=region,ProvinceId=province,MorphologyCode=code,Name=name,Founder=genus,LeafColor=new MidMorphology(code).Leaf,
                    Form=form,HeightM=D.Q(height,1000),WidthM=D.Q(height*(form==MidGrowthForm.DwarfHeath?2.4:form==MidGrowthForm.ReedStand?.65:form==MidGrowthForm.Succulent?1:1.3)*(.8+.4*b.Unit()),1000),
                    Moisture=moisture,Shade=shade,MinWarmC=warm-12-8*a.Unit(),MaxWarmC=warm+12+7*a.Unit(),MinPH=3.9+1.5*a.Unit(),MaxPH=7.5+1.1*a.Unit(),
                    MaxSalinity=h!=null&&(h.Biome==Biome.SaltMarsh||h.Biome==Biome.Mangrove)?.65+.3*a.Unit():.04+.15*a.Unit(),
                    ColonyM=12+65*a.Unit(),FloodTolerance=form==MidGrowthForm.ReedStand?.95:.15+.45*a.Unit(),DroughtTolerance=form==MidGrowthForm.Succulent||form==MidGrowthForm.OpenScrub?.85:.2+.4*a.Unit()};
            }
            return result;
        }
        public static double Suitability(MidPlantSpecies s,HabitatCell h,GroundProfile soil,Biome biome,double warm)
        {
            if(biome==Biome.Ice||biome==Biome.Ocean||biome==Biome.Freshwater||biome==Biome.SalineLake)return 0;
            bool cold=biome==Biome.Tundra||biome==Biome.Alpine;double wet=h==null?.8:h.AridityMilli*.001;
            double flood=h==null?0:Math.Max(h.FloodFractionPermille*.001,D.Clamp((100-h.WaterTableCm)/100d));
            double shade=GroundSpecialists.Canopy(biome),weight=1;
            switch(s.Form)
            {
                case MidGrowthForm.LeafyBush:weight=cold?.18:biome==Biome.Desert?0:.85;break;
                case MidGrowthForm.OpenScrub:weight=cold?.06:D.Clamp((1.2-wet)/.9);break;
                case MidGrowthForm.DwarfHeath:weight=cold?1:biome==Biome.BorealForest||biome==Biome.Peatland?.6:.08;break;
                case MidGrowthForm.ArchingCane:weight=cold||biome==Biome.Desert?0:.5*D.Clamp((wet-.25)/.5);break;
                case MidGrowthForm.LargeFern:weight=cold||biome==Biome.Desert?0:shade*D.Clamp((wet-.55)/.65)*D.Clamp((warm-6)/10);break;
                case MidGrowthForm.ReedStand:weight=cold?.08:flood*D.Clamp((wet-.30)/.6);break;
                case MidGrowthForm.Succulent:weight=cold?0:D.Clamp((warm-12)/10)*D.Clamp((.85-wet)/.55);break;
            }
            double moisture=Math.Exp(-Math.Pow((wet*(.8+.3*D.Clamp(soil.WaterCapacityMm/100))-s.Moisture)/Math.Max(.3,s.Moisture*.8),2));
            double light=Math.Exp(-Math.Pow((shade-s.Shade)/.65,2));
            double temperature=D.Clamp((warm-s.MinWarmC)/6)*D.Clamp((s.MaxWarmC-warm)/6);
            double ph=D.Clamp((soil.PH-s.MinPH)/.8)*D.Clamp((s.MaxPH-soil.PH)/.8),salt=1-D.Clamp(soil.Salinity/Math.Max(.01,s.MaxSalinity));
            double roots=D.Clamp(soil.DepthM/(s.Form==MidGrowthForm.DwarfHeath?.12:.28))*(.55+.45*soil.Fertility)*(1-.6*soil.Gravel);
            return D.Clamp(weight*moisture*light*temperature*ph*salt*roots*(1-flood*(1-s.FloodTolerance)));
        }
        public static double Colony(MidPlantSpecies s,V position,int seed,double physicalRadiusM)
        {
            double n=.5+.5*SphereNoise.Sample(position*(physicalRadiusM/1000),seed^(s.MorphologyCode&~3),1000/s.ColonyM);
            double t=D.Clamp((n-.29)/.43);return t*t*(3-2*t);
        }
        public static double StandDensity(V position,int seed,double physicalRadiusM)
        {
            double n=.5+.5*SphereNoise.Sample(position*(physicalRadiusM/1000),seed^0x5a27ed,8);
            double t=D.Clamp((n-.30)/.38);return t*t*(3-2*t);
        }
        public static int ColonyMembers(MidGrowthForm form)=>form==MidGrowthForm.DwarfHeath?4:
            form==MidGrowthForm.ArchingCane||form==MidGrowthForm.ReedStand?3:form==MidGrowthForm.LargeFern?2:1;
        public static MidCoverProfile Describe(HabitatCell h,GroundProfile ground,MidPlantSpecies[] species,Biome biome)
        {
            var p=new MidCoverProfile();double sum=0,area=0;
            foreach(var s in species)
            {
                double w=Suitability(s,h,ground,biome,h==null?18:h.WarmTenthsC*.1);sum+=w;
                p.Color+=s.LeafColor*w;p.HeightM+=s.HeightM*w;p.WidthM+=s.WidthM*w;
                area+=s.WidthM*s.WidthM*.60*ColonyMembers(s.Form)*w;
            }
            if(sum>.0001){p.Color/=sum;p.HeightM/=sum;p.WidthM/=sum;}
            // Expected projected crown area from the same capped placement density.
            p.Coverage=sum>.0001?D.Clamp(.012*D.Clamp(sum*.55*.45)*area/sum,0,.55):0;
            return p;
        }
    }
    public struct MidCoverProfile
    {
        public V Color;public double Coverage,HeightM,WidthM;
        internal void Add(MidCoverProfile p,double w){Color+=p.Color*w;Coverage+=p.Coverage*w;HeightM+=p.HeightM*w;WidthM+=p.WidthM*w;}
        internal void Divide(double w){Color/=w;Coverage/=w;HeightM/=w;WidthM/=w;}
    }
}
