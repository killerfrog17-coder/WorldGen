using System;
using System.Linq;
namespace WorldGen.Core
{
    public static class HistoricalCommunityEffects
    {
        // Net growth priors, after ordinary mortality, not biological birth
        // rates. Frontier booms are local and brief, not a global Neolithic
        // population explosion. Modern medicine changes the mortality regime.
        public static double RuralGrowth(HistoricalCulture c,int year,int pioneerAge,bool settlement=false)
        {
            double pioneer=(settlement?.0065:.0032)*Math.Max(0,1-pioneerAge/(settlement?650.0:450.0));
            double medicine=.006*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Sanitation,year)
                +.004*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Vaccination,year)
                +.003*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Antibiotics,year);
            double rotation=.00025*HistoricalModernEffects.Adoption(c,HistoricalTechnology.CropRotation,year);
            double transition=1-.5*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Telecommunications,year);
            return D.Q((.00035+pioneer+rotation+medicine)*transition,1000000000);
        }
        public static double MarketIntegration(HistoricalCulture c,int year,bool water)
        {
            double roads=.08*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Writing,year)+.12*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Roads,year);
            double ships=water?(.20*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Sailing,year)+.25*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Navigation,year)):0;
            return D.Q(D.Clamp(roads+ships+.8*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Railways,year)),1000000);
        }
    }
    public sealed partial class Prehistory
    {
        double CommunityRuralGrowth(PrehistoryState s,int culture,int founded=-1)
        {
            var crops=s.History.Agriculture[culture];
            int start=founded>=0?founded:crops.AdoptedYear.Where(y=>y>0).DefaultIfEmpty(1).Min()-1;
            double rate=HistoricalCommunityEffects.RuralGrowth(s.History.Cultures[culture],s.Years,Math.Max(0,s.Years-start),founded>=0);
            if(ColonialHistory(s))
            {
                // 0.035% per year was a near-stagnation prior even for established
                // cultivators with usable land. A modest net agrarian increase is
                // still capped by local food and reduced by actual disease/war.
                double cultivation=0;for(int k=0;k<6;k++)if(crops.AdoptedYear[k]>0)cultivation=Math.Max(cultivation,crops.Practice[k]/1000.0);
                rate+=.00065*D.Clamp(cultivation)*(1-.7*HistoricalModernEffects.Adoption(s.History.Cultures[culture],HistoricalTechnology.Telecommunications,s.Years));
            }
            return D.Q(rate,1000000000);
        }
        long CommunityRuralCapacity(PrehistoryState s,int city)
        {
            var a=s.History.Aggregates;var town=s.Society.Settlements[city];var c=a.Cities[city];var hc=s.History.Cultures[town.CultureId];
            double farm=(100.0+s.Society.Cultures[town.CultureId].Farming)/(100+c.BaseFarming);
            double crop=(.4+.6*AggregateCropSupport(s,town.CultureId,town.Cell,false))/c.BaseCropFactor;
            long capacity=Math.Max(1,(long)(c.RuralCapacity*farm*crop*EffectiveFoodMultiplier(s,hc)*a.Living.Cities[city].Productivity*AnimalFoodMultiplier(s,town.CultureId,town.Cell)*(1-HistoricalProvincialEffects.UrbanShare(hc,s.Years))*(LivingWorldHistory(s)?LivingClimateFood(s,town.Cell,town.CultureId):1)));
            return PreparedTownRuralCapacity(s,city,capacity);
        }
        long CommunityUrbanCapacity(PrehistoryState s,int city)
            =>CommunityUrbanCapacity(s,city,CommunityRuralCapacity(s,city));
        long CommunityUrbanCapacity(PrehistoryState s,int city,long ruralCapacity)
        {
            var h=s.History;var a=h.Aggregates;var t=s.Society.Settlements[city];var c=a.Cities[city];var hc=h.Cultures[t.CultureId];
            double share=HistoricalProvincialEffects.UrbanShare(hc,s.Years);
            double storage=PreparedFieldHistory(s)?PreparedTownLandscape(s,city,true):LivingCropLandscape(s,t.CultureId,t.Cell,true);
            double produce=(.65+1.35*D.Clamp(storage/.35))*a.Living.Cities[city].Productivity*AnimalFoodMultiplier(s,t.CultureId,t.Cell);
            // Potential acreage alone never feeds a town. Actual farm workers,
            // storable local produce and transported surplus must support it.
            double local=c.Rural*share/Math.Max(.1,1-share)*produce*EffectiveFoodMultiplier(s,hc);
            if(LivingWorldHistory(s))local*=LivingClimateFood(s,t.Cell,t.CultureId);
            double potential=ruralCapacity*share/Math.Max(.1,1-share)*produce;
            long capacity=(long)Math.Min(local,potential);int owner=a.Resolve(t.PolityId,h);
            if(owner>=0&&!CoastalFarmDescendant(s,city))capacity+=CommunityUrbanImports(s,owner,city);
            return Math.Max(0,capacity);
        }
        long CommunityUrbanImports(PrehistoryState s,int polity,int city)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var hc=h.Cultures[p.CultureId];var t=s.Society.Settlements[city];
            bool water=riverAccess[t.Cell]>.02||World.Surface.Cells[t.Cell].Neighbors.Any(n=>World.Hydrology.OceanConnected[n]);
            double integration=HistoricalCommunityEffects.MarketIntegration(hc,s.Years,water),share=HistoricalProvincialEffects.UrbanShare(hc,s.Years);
            double potential=a.Provinces.FarmPotential[polity]*D.Clamp(s.Society.Cultures[p.CultureId].Farming/600.0)*EffectiveFoodMultiplier(s,hc);
            double market=Math.Min(potential*share,a.Polities[polity].Remote*share/Math.Max(.1,1-share));
            double weight=(t.Population+250.0)/Math.Max(1,a.Polities[polity].Urban+250.0*a.Polities[polity].CityCount);
            long supplied=(long)(market*integration*weight/1.15);
            return LivingWorldHistory(s)?Math.Min(supplied,LivingUrbanImportLimit(s,polity,city)):supplied;
        }
    }
}
