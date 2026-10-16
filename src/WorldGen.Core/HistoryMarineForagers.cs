using System;
namespace WorldGen.Core
{
    public static class HistoricalMarineForagerEffects
    {
        // Cultural aggregation is not farming or a state. Productive aquatic
        // ecotones can support stored surplus despite poor cereal soils.
        public static double FoodSupport(bool coast,bool fish,bool shellfish,double shelf,double freshwater,int warmTenthsC,bool permanentIce)
        {
            if(!coast||!fish&&!shellfish||permanentIce)return 0;
            double foods=(fish?.55:0)+(shellfish?.45:0);
            double accessible=.15+.85*D.Clamp((warmTenthsC+80)/180.0);
            return D.Q(foods*(.30+.70*D.Clamp(shelf))*D.Clamp(freshwater)*accessible,1000000);
        }
        public static double IndependentCultureOpportunity(double marineFood,double occupied,double homeland)
            =>.0008*Math.Pow(D.Clamp(marineFood),2)*D.Clamp(occupied)*D.Clamp(homeland);
    }
    public sealed partial class Prehistory
    {
        int marineForageFishTaxon=-2,marineShellfishTaxon=-2;
        void PrepareMarineForagerResources()
        {
            if(marineForageFishTaxon!=-2)return;
            marineForageFishTaxon=marineShellfishTaxon=-1;
            // Catalog setup only; no taxon search during an origin event. Other
            // life packs without these feeding pools simply retain old priors.
            foreach(var taxon in World.Ecology.Taxa)
            {if(taxon.ScientificName=="Coastal fish guild lineage")marineForageFishTaxon=taxon.Id;else if(taxon.ScientificName=="Coastal invertebrate guild lineage")marineShellfishTaxon=taxon.Id;}
        }
        double MarineForagerSupport(PrehistoryState s,int cell)
        {
            if(!LivingWorldHistory(s)||marineForageFishTaxon==-2)return 0;
            var habitat=World.Ecology.Habitat[cell];int ocean=0,shallow=0;
            var neighbors=World.Surface.Cells[cell].Neighbors;
            for(int k=0;k<Math.Min(8,neighbors.Length);k++)if(World.Hydrology.OceanConnected[neighbors[k]])
            {ocean++;if(World.ElevationM[neighbors[k]]>-350)shallow++;}
            if(ocean==0)return 0;
            bool fish=marineForageFishTaxon>=0&&Biogeography.Present(World.Ecology,cell,marineForageFishTaxon),shell=marineShellfishTaxon>=0&&Biogeography.Present(World.Ecology,cell,marineShellfishTaxon);
            // The marine biome records adjacent-sea access. Shelf availability
            // is a modest productivity proxy, not a new upwelling simulation.
            double drinking=D.Clamp(habitat.AridityMilli/500.0+riverAccess[cell]*12+(habitat.WaterTableCm<=300&&habitat.SalinityMilliPpt<3000?.15:0));
            return HistoricalMarineForagerEffects.FoodSupport(true,fish,shell,shallow/(double)ocean,drinking,habitat.WarmTenthsC,habitat.Biome==Biome.Ice);
        }
        void InitializeMarineForagerCulture(PrehistoryState s,CultureIdentity culture,int cell)
        {
            if(!LivingWorldHistory(s))return;double food=MarineForagerSupport(s,cell);if(food<.2)return;
            culture.Fishing=Math.Min(650,300+(int)Math.Round(food*350));culture.Hunting=Math.Min(culture.Hunting,1000-culture.Fishing);culture.Gathering=1000-culture.Fishing-culture.Hunting;
            culture.Resources="Coastal fish and shellfish, potable water, seasonal gathering and local exchange";
            culture.Subsistence="Specialized fishing and gathering with seasonal storage; not an agricultural state";
        }
    }
}
