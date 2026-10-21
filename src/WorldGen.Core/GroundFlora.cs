using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace WorldGen.Core
{
    public enum GroundGrowthForm { CoolGrass, WarmGrass, DryTuft, SedgeRush, Forb, Fern, Moss, Lichen, Succulent }
    public sealed class GroundPlantSpecies
    {
        public int RegionId,ProvinceId;
        public GroundGrowthForm Form;
        public string Name="",Ancestor="";
        public V Color;
        public double Stature,LeafWidth;
    }
    public sealed class GroundFloraRegion
    {
        public int RegionId,ProvinceId;
        public GroundPlantSpecies[] Species=Array.Empty<GroundPlantSpecies>();
    }
    /// <summary>Compact potential natural ground flora. Rebuilt from saved abiotic/ecoregion
    /// data, not a mutation of old ecology, food webs or history. Ancestral province families
    /// acquire bounded regional traits; this is static biogeography, not simulated evolution.</summary>
    public sealed class GroundFlora
    {
        public const string Version="ground-flora-2";
        static readonly ConditionalWeakTable<World,GroundFlora> contexts=new ConditionalWeakTable<World,GroundFlora>();
        readonly World world;
        readonly ConcurrentDictionary<int,GroundFloraRegion> regions=new ConcurrentDictionary<int,GroundFloraRegion>();
        public static GroundFlora For(World world)=>contexts.GetValue(world,w=>new GroundFlora(w));
        GroundFlora(World world){this.world=world;}
        static readonly string[] syllables={"ala","bri","cal","den","eri","fen","gal","hel","iri","jun","kel","lin","mor","nel","ori","pel","qui","ren","sil","tor","uli","val"};
        static readonly V[] colors={new V(.28,.39,.12),new V(.40,.45,.17),new V(.53,.48,.25),new V(.25,.38,.16),new V(.24,.39,.14),new V(.18,.34,.14),new V(.23,.32,.13),new V(.48,.49,.35),new V(.34,.43,.25)};
        public GroundFloraRegion Region(int id)
        {
            return regions.GetOrAdd(id,key=>
            {
                int province=world.Ecology!=null&&key>=0&&key<world.Ecology.Regions.Length?world.Ecology.Regions[key].ProvinceId:key;
                var result=new GroundFloraRegion{RegionId=key,ProvinceId=province,Species=new GroundPlantSpecies[9]};
                for(int f=0;f<9;f++)
                {
                    var ancestor=new Rng(world.Recipe.Seed,"ground-flora-founder-1",unchecked(province*31+f));
                    var local=new Rng(world.Recipe.Seed,"ground-flora-branch-1",unchecked(key*31+f));
                    string genus=syllables[(int)(ancestor.Unit()*syllables.Length)]+syllables[(int)(ancestor.Unit()*syllables.Length)]+"a";
                    string epithet=syllables[(int)(local.Unit()*syllables.Length)]+syllables[(int)(local.Unit()*syllables.Length)]+"is";
                    double tint=.86+.24*ancestor.Unit(),mutation=.93+.14*local.Unit();
                    result.Species[f]=new GroundPlantSpecies{RegionId=key,ProvinceId=province,Form=(GroundGrowthForm)f,
                        Ancestor=genus+" / "+((GroundGrowthForm)f),Name=char.ToUpperInvariant(genus[0])+genus.Substring(1)+" "+epithet,
                        Color=colors[f]*tint*mutation,Stature=(.70+.65*ancestor.Unit())*(.88+.24*local.Unit()),LeafWidth=(.75+.5*ancestor.Unit())*(.88+.24*local.Unit())};
                }
                return result;
            });
        }
        public GroundCoverProfile Cell(int cell,GroundProfile soil)
        {
            var e=world.Ecology;
            var h=e!=null&&cell<e.Habitat.Length?e.Habitat[cell]:null;
            int region=e!=null&&cell<e.Region.Length?e.Region[cell]:0;
            var flora=Region(region);
            return Describe(h,soil,flora);
        }
        public static GroundCoverProfile Describe(HabitatCell h,GroundProfile soil,GroundFloraRegion flora)
        {
            if(h!=null&&(h.Biome==Biome.Ocean||h.Biome==Biome.Ice))return default;
            double warm=h==null?18:h.WarmTenthsC*.1,wet=h==null?.8:h.AridityMilli*.001;
            double cold=D.Clamp((warm+1)/9),hot=D.Clamp((warm-18)/14),moist=D.Clamp((wet-.12)/.85);
            double flood=h==null?0:Math.Max(h.FloodFractionPermille*.001,D.Clamp((120-h.WaterTableCm)/120.0));
            double shade=h==null?0:h.Biome==Biome.TropicalRainforest?.94:h.Biome==Biome.TemperateRainforest?.88:
                h.Biome==Biome.BorealForest?.78:h.Biome==Biome.TemperateForest?.75:h.Biome==Biome.TropicalSeasonalForest?.58:h.Biome==Biome.MediterraneanWoodland?.3:0;
            double salt=Math.Max(soil.Salinity,h==null?0:D.Clamp(h.SalinityMilliPpt/35000.0));
            double roots=D.Clamp(soil.DepthM/.25)*(.4+.6*soil.Fertility)*(1-.7*soil.Gravel);
            var profile=new GroundCoverProfile();double sum=0;
            for(int f=0;f<9;f++)
            {
                double w;
                switch((GroundGrowthForm)f)
                {
                    case GroundGrowthForm.CoolGrass:w=.95*moist*(1-hot*.8)*(1-shade*.88)*(1-flood*.6)*(1-salt*.95)*roots*cold;break;
                    case GroundGrowthForm.WarmGrass:w=1.05*moist*hot*(1-shade*.96)*(1-flood*.6)*(1-salt*.95)*roots*cold;break;
                    case GroundGrowthForm.DryTuft:w=.52*D.Clamp((wet-.055)/.25)*D.Clamp((1.15-wet)/.8)*(1-shade*.9)*(1-flood)*(1-salt*.8)*roots*cold;break;
                    case GroundGrowthForm.SedgeRush:w=.78*flood*cold*(.4+.6*moist)*(1-shade*.5)*(.5+.5*roots)*(1-salt*.4);break;
                    case GroundGrowthForm.Forb:w=.32*moist*cold*(1-shade*.65)*(1-flood*.65)*(1-salt)*roots;break;
                    case GroundGrowthForm.Fern:w=.5*shade*D.Clamp((wet-.5)/.9)*D.Clamp((warm-5)/12)*(1-flood*.8)*(1-salt)*roots;break;
                    case GroundGrowthForm.Moss:w=(.13+.65*shade)*D.Clamp((wet-.18)/.85)*(1-hot*.65)*cold*(.45+.55*soil.Organic)*(.55+.45*(1-soil.Drainage))*(1-salt*.95);break;
                    case GroundGrowthForm.Lichen:w=(.10+.32*(1-cold)+.12*soil.Gravel)*(1-shade*.7)*D.Clamp((1.1-wet)/.8)*(.5+.5*D.Clamp((warm+8)/12))*(1-flood);break;
                    default:w=.10*hot*D.Clamp((.8-wet)/.65)*(1-shade)*(1-flood)*roots;break;
                }
                var species=flora.Species[f];profile.SetWeight(f,w);sum+=w;
                profile.Color+=species.Color*w;profile.Stature+=species.Stature*w;profile.LeafWidth+=species.LeafWidth*w;
            }
            if(sum>0){profile.Color/=sum;profile.Stature/=sum;profile.LeafWidth/=sum;}
            // Potential mature leafy canopy, not the maximum height of flowering culms.
            // Moist fertile open sites support tallgrass; dry/shallow sites stay short.
            double tall=D.Clamp((wet-.38)/.7)*D.Clamp((warm-12)/15)*D.Clamp(soil.DepthM/.75)
                *(.4+.6*soil.Fertility)*(1-shade)*(1-.65*salt);
            profile.Stature*=.65+2.0*tall+.55*flood;
            if(sum>.95)profile.ScaleWeights(.95/sum);
            return profile;
        }
        /// <summary>Continuous physical-space patching and local exclusion. Never reshapes ground/water.</summary>
        public static GroundCoverProfile Surface(GroundCoverProfile profile,V point,int seed,double radiusM,
            GroundProfile ground,double elevationM,double sourceElevationM,double up,bool underwater,double ice)
        {
            if(underwater||elevationM<.3||ice>.5)return default;
            double patch=.84+.28*SphereNoise.Sample(point*(radiusM/1000),seed^0x55a31,24);
            double slope=D.Clamp((up-.46)/.43),altitude=D.Clamp((2200-(elevationM-sourceElevationM))/1800);
            profile.ScaleWeights(D.Clamp(patch)*slope*altitude*(1-ice));
            // Broad, coherent stands, rather than independently random tall/short blades.
            profile.Stature*=.90+.20*SphereNoise.Sample(point*(radiusM/1000),seed^0x41b29,3);
            return profile;
        }
    }
    public struct GroundCoverProfile
    {
        public V Grasses,Herbs,Mats,Color;
        public double Stature,LeafWidth;
        public double Coverage=>Grasses.X+Grasses.Y+Grasses.Z+Herbs.X+Herbs.Y+Herbs.Z+Mats.X+Mats.Y+Mats.Z;
        public double Weight(int f)=>f<3?(f==0?Grasses.X:f==1?Grasses.Y:Grasses.Z):f<6?(f==3?Herbs.X:f==4?Herbs.Y:Herbs.Z):(f==6?Mats.X:f==7?Mats.Y:Mats.Z);
        public void SetWeight(int f,double w)
        {if(f<3)Grasses=new V(f==0?w:Grasses.X,f==1?w:Grasses.Y,f==2?w:Grasses.Z);else if(f<6)Herbs=new V(f==3?w:Herbs.X,f==4?w:Herbs.Y,f==5?w:Herbs.Z);else Mats=new V(f==6?w:Mats.X,f==7?w:Mats.Y,f==8?w:Mats.Z);}
        public void ScaleWeights(double w){Grasses*=w;Herbs*=w;Mats*=w;}
        internal void Add(GroundCoverProfile p,double w){Grasses+=p.Grasses*w;Herbs+=p.Herbs*w;Mats+=p.Mats*w;Color+=p.Color*w;Stature+=p.Stature*w;LeafWidth+=p.LeafWidth*w;}
        internal void Divide(double w){Grasses/=w;Herbs/=w;Mats/=w;Color/=w;Stature/=w;LeafWidth/=w;}
    }
}
