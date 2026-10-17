using System;

namespace WorldGen.Core
{
    public static class HistoricalCropSelectionEffects
    {
        public static int FractionalYears(double expected,double roll)
        {
            expected=Math.Max(0,expected);int whole=(int)Math.Floor(expected);
            return whole+(roll<expected-whole?1:0);
        }
        public static int LatitudeMovement(int current,int target,int years,double roll)
        {
            double expected=Math.Min(Math.Abs(target-current),Math.Max(0,years)*.02);
            return FractionalYears(expected,roll);
        }
        // Legacy mode reproduces the preceding dense solver's arithmetic.
        // New aggregate selection must not undo stronger inherited phenotypes
        // merely because the old aggregate curve used different coefficients.
        public static void Apply(HistoricalCultivar cultivar,int years,int targetLatitudeTenths,bool preserveSelectedTraits=false,int latitudeMovement=-1)
        {
            if(years<=0)return;
            int maturity=cultivar.MaturityDays,water=cultivar.WaterRequirementPermille,warmth=cultivar.MinimumWarmTenthsC,yield=cultivar.YieldPermille;
            cultivar.SelectedYears=Math.Min(20000,cultivar.SelectedYears+years);
            var definition=HistoricalCropCatalog.Crops[cultivar.CropId];double selection=D.Clamp(cultivar.SelectedYears/6000.0);
            cultivar.MaturityDays=(int)Math.Round(definition.MaturityDays*(1-.3*selection));
            cultivar.WaterRequirementPermille=(int)Math.Round(definition.MinimumWaterPermille*(1-.15*selection));
            cultivar.MinimumWarmTenthsC=definition.MinimumWarmC*10-(int)Math.Round(40*selection);
            cultivar.YieldPermille=650+(int)Math.Round(650*D.Clamp(cultivar.SelectedYears/8000.0));
            if(preserveSelectedTraits)
            {
                if(maturity>0)cultivar.MaturityDays=Math.Min(maturity,cultivar.MaturityDays);
                if(water>0)cultivar.WaterRequirementPermille=Math.Min(water,cultivar.WaterRequirementPermille);
                if(warmth>0)cultivar.MinimumWarmTenthsC=Math.Min(warmth,cultivar.MinimumWarmTenthsC);
                cultivar.YieldPermille=Math.Max(yield,cultivar.YieldPermille);
            }
            int move=latitudeMovement>=0?Math.Min(Math.Abs(targetLatitudeTenths-cultivar.DaylengthLatitudeTenths),latitudeMovement):(int)Math.Round(Math.Min(Math.Abs(targetLatitudeTenths-cultivar.DaylengthLatitudeTenths),years*.02));
            cultivar.DaylengthLatitudeTenths+=Math.Sign(targetLatitudeTenths-cultivar.DaylengthLatitudeTenths)*move;
        }
    }
    public sealed partial class Prehistory
    {
        static bool LocalCropSelectionHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=10;
        bool LocalCropSelectionFieldOccupied(PrehistoryState s,int culture,int cell)
        {
            if(cell<0||cell>=CellPatch.Length||CellPatch[cell]<0)return false;
            var a=s.History.Aggregates;int city=a.TownAt[cell];
            if(city>=0&&s.Society.Settlements[city].CultureId==culture&&s.Society.Settlements[city].Population>=100&&a.Living.Cities[city].AbandonedYear<0)return true;
            return HistoricalCells.Culture(s,cell)==culture&&RuralCellPopulation(s,cell)>=10;
        }
        void AdvanceAggregateLocalCropSelection(PrehistoryState s,int culture,int group,int elapsed,int urbanSite)
        {
            if(!LocalCropSelectionHistory(s)||elapsed<=0)return;
            var crops=s.History.Agriculture[culture];var cultivar=crops.Cultivars?[group];
            if(crops.AdoptedYear[group]<=0||cultivar==null||s.Society.Cultures[culture].Farming<50)return;
            bool managed=HistoricalTechnologies.Has(s.History.Cultures[culture],HistoricalTechnology.CropRotation);
            int cell=crops.PracticeCell[group];
            double fit=LocalCropSelectionFieldOccupied(s,culture,cell)?CommunityTransferFit(s,cell,cultivar,managed):0;
            if(fit<.08)
            {
                // Only the existing culture event's actual sampled town is an
                // alternative. No whole-culture settlement/terrain search.
                if(urbanSite==cell||!LocalCropSelectionFieldOccupied(s,culture,urbanSite))return;
                fit=CommunityTransferFit(s,urbanSite,cultivar,managed);if(fit<.08)return;
                crops.HusbandryYears[group]=(int)Math.Round(crops.HusbandryYears[group]*(CropHabitat(s,cell).Biome==CropHabitat(s,urbanSite).Biome?.25:0));
                crops.PracticeCell[group]=cell=urbanSite;
            }
            double practiced=Math.Min(elapsed,Math.Max(0,s.Years-(crops.AdoptedYear[group]-1)));
            double expected=D.Q(practiced*Math.Min(1,s.Society.Cultures[culture].Farming/400.0)*fit,1000000000);
            int years=HistoricalCropSelectionEffects.FractionalYears(expected,HistoryRoll("aggregate-local-selection-years-10-"+group,culture,s.Years));if(years<=0)return;
            int target=(int)Math.Round(CropLatitude(cell)*10);
            double latitudeRoll=HistoryRoll("aggregate-local-selection-latitude-10-"+group,culture,s.Years);
            int move=LocalSeedlotHistory(s)?HistoricalLocalSeedSelectionEffects.LatitudeMovement(cultivar.DaylengthLatitudeTenths,target,years,latitudeRoll):HistoricalCropSelectionEffects.LatitudeMovement(cultivar.DaylengthLatitudeTenths,target,years,latitudeRoll);
            crops.HusbandryYears[group]=Math.Min(20000,crops.HusbandryYears[group]+years);
            if(LocalSeedlotHistory(s))HistoricalLocalSeedSelectionEffects.Apply(cultivar,years,target,move);else HistoricalCropSelectionEffects.Apply(cultivar,years,target,true,move);
            InvalidateCultivarFits(culture);
        }
    }
}
