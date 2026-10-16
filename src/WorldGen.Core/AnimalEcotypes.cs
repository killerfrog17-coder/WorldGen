using System;

namespace WorldGen.Core
{
    public static class AnimalEcotypes
    {
        // A constant-time regional realization: no ancestry walks, membership
        // census or query-order-dependent registry. Parent trophic envelopes
        // remain deliberately conservative; these are not evolving populations.
        public static RegionalAnimalVariant Create(World w,Taxon t,Ecoregion region,int sampleCell=-1)
        {
            if(t.Traits==null||t.Lineage==null)return null;
            if(sampleCell<0)sampleCell=region.RepresentativeCell;
            var h=w.Ecology.Habitat[sampleCell];var l=t.Lineage;
            int cold=(int)(1000*D.Clamp((120-h.ColdTenthsC)/400.0));
            int hot=(int)(1000*D.Clamp((h.MeanTenthsC-100)/220.0));
            int canopy=Canopy(h);int dryness=(int)(1000*D.Clamp(1-h.AridityMilli/1000.0));
            int key=unchecked(region.ProvinceId*8191+t.Id*31+(cold/250)*7+(canopy/250)*193);
            double drift=Rng.Value(w.Recipe.Seed,"animal-lineage-ecotype-3",key)-.5;
            // Bergmann/Allen-like tendencies are variable comparative priors,
            // not universal rules (especially not for ectotherms or birds).
            double thermal=(l.Ectotherm?(hot-500)/1000.0:(cold-hot)/1800.0)*l.ThermalSizeSensitivityPermille/1000.0;
            double forest=t.MassKg>500?-canopy/2400.0:0;
            int body=Math.Max(450,Math.Min(1700,1000+(int)(l.BodyVariationPermille*(thermal+forest+drift*.45))));
            int ears=Math.Max(400,Math.Min(1600,1000+l.EarVariationPermille*(hot-cold)/1000+(int)(l.EarVariationPermille*drift*.25)));
            int limbs=Math.Max(450,Math.Min(1600,1000+l.AppendageVariationPermille*(hot-cold)/1300+(int)(l.AppendageVariationPermille*drift*.3)));
            int display=750+(int)(500*Rng.Value(w.Recipe.Seed,"animal-display-ecotype-3",key));
            int insulation=l.Ectotherm?100:Math.Max(100,Math.Min(950,t.Traits.InsulationPermille+(cold-hot)*350/1000));
            int whitening=t.Traits.SeasonalWhite?(int)(1000*D.Clamp((-h.ColdTenthsC-30)/220.0)*D.Clamp((h.WarmTenthsC-h.ColdTenthsC)/180.0)):0;
            string habitat=l.RequiresCoast?(h.WarmTenthsC<100?"Polar-coast":"Maritime"):canopy>=650?"Forest":dryness>=700?"Dry-country":cold>=500?"Cold-country":h.ReliefM>900?"Highland":"Open-country";
            return new RegionalAnimalVariant{Name=habitat+" "+t.Name.ToLowerInvariant(),MassKg=D.Q(t.MassKg*body/1000.0,1000000),InsulationPermille=insulation,
                CoatLightnessPermille=Math.Max(100,Math.Min(900,t.Traits.CoatLightnessPermille+dryness/5-canopy/6+(int)(drift*140))),SeasonalWhiteningPermille=whitening,
                Docility=Math.Max(0,Math.Min(1000,t.Traits.Docility+(int)(drift*100))),PanicResistance=Math.Max(0,Math.Min(1000,t.Traits.PanicResistance+(int)(drift*100))),
                Ecotype=new AnimalEcotype{LineageKey=l.Key,SampleCell=sampleCell,HabitatForm=habitat,CoatPattern=whitening>500?"seasonally white":l.Pattern,BodySizePermille=body,EarSizePermille=ears,
                    AppendageSizePermille=limbs,DisplaySizePermille=display,DisplayTrait=l.DisplayTrait}};
        }
        public static int Canopy(HabitatCell h)
        {
            switch(h.Biome)
            {
                case Biome.TropicalRainforest:return 950;
                case Biome.TemperateRainforest:return 900;
                case Biome.TropicalSeasonalForest:return 780;
                case Biome.TemperateForest:return 800;
                case Biome.BorealForest:return 750;
                case Biome.Mangrove:return 850;
                case Biome.MediterraneanWoodland:return 450;
                case Biome.Savanna:return 250;
                case Biome.Floodplain:return h.ColdTenthsC>180&&h.AridityMilli>1300?700:250;
                default:return (h.Habitat&Habitat.Forest)!=0?750:0;
            }
        }
        public static double Domesticability(Taxon taxon,RegionalAnimalVariant variant=null)
        {
            if(taxon.Traits==null)return 0;
            if(variant==null)return NativeLifePack.Domesticability(taxon.Traits);
            var traits=taxon.Traits.Copy();traits.Docility=variant.Docility;traits.PanicResistance=variant.PanicResistance;
            return NativeLifePack.Domesticability(traits);
        }
        public static int RidingSuitability(Taxon taxon)
        {
            if(taxon.Lineage!=null)return taxon.Lineage.RidingSuitabilityPermille;
            // Explicit historical pack compatibility, not "every traction
            // animal is cavalry" or an English substring classifier.
            return taxon.ScientificName=="Equus ferus przewalskii"?900:0;
        }
    }
}
