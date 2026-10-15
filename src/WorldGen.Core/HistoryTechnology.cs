using System;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        bool[] localCoastalNavigationSupport;
        void PrepareLocalNavigationSupport(PrehistoryState s,CancellationToken token)
        {
            localCoastalNavigationSupport=new bool[s.Society.Cultures.Length];
            var coastalTowns=s.Society.Settlements.Where(t=>t.Population>=1200&&(World.Hydrology.OceanConnected[t.Cell]||World.Surface.Cells[t.Cell].Neighbors.Any(j=>World.Hydrology.OceanConnected[j]))).Select(t=>t.CultureId).ToHashSet();
            foreach(int cell in CulturalCells(s))
            {
                if((cell&2047)==0)token.ThrowIfCancellationRequested();int owner=s.Society.CellCulture[cell];if(owner<0||localCoastalNavigationSupport[owner]||CellPatch[cell]<0)continue;
                if((World.Hydrology.OceanConnected[cell]||World.Surface.Cells[cell].Neighbors.Any(j=>World.Hydrology.OceanConnected[j]))&&(CoastalStagingResidents(s,cell,owner)>=1200||coastalTowns.Contains(owner))&&CultureOpportunity(s,cell)>=.3)localCoastalNavigationSupport[owner]=true;
            }
        }
        // Sustained support for specialists, not calendar unlocks or a ranking of peoples.
        // Thresholds are explicit game priors; aquatic surplus can support non-farming institutions.
        bool SupportsHistoricalPractice(PrehistoryState s,CultureIdentity c,HistoricalCulture hc,Settlement[] towns,int technology,bool contact)
        {
            bool water=towns.Any(t=>World.Surface.Cells[t.Cell].Neighbors.Any(i=>World.Hydrology.OceanConnected[i])||HistoricalRiverAccess(t.Cell)>.02);
            if(ExperimentalHistory(s)&&technology==(int)HistoricalTechnology.Navigation)
                water=World.Hydrology.OceanConnected[c.OriginCell]||World.Surface.Cells[c.OriginCell].Neighbors.Any(i=>World.Hydrology.OceanConnected[i])||towns.Any(t=>World.Surface.Cells[t.Cell].Neighbors.Any(i=>World.Hydrology.OceanConnected[i]));
            bool coastalSpecialists=localCoastalNavigationSupport!=null&&c.Id<localCoastalNavigationSupport.Length&&localCoastalNavigationSupport[c.Id];
            return HistoricalPracticeRequirements.Supports(s.Years,c,hc,towns,(HistoricalTechnology)technology,contact,water,s.History.Version,coastalSpecialists);
        }
    }
    public static class HistoricalPracticeRequirements
    {
        // Shared pure diagnostic boundary: hosts do not implement a second technology model.
        public static bool Supports(int year,CultureIdentity c,HistoricalCulture hc,Settlement[] towns,HistoricalTechnology practice,bool contact,bool water,string version="history-5",bool coastalSpecialists=false)
        {
            if(Prehistory.IsAggregateHistoryVersion(version))version="history-16";
            int technology=(int)practice;
            var prerequisites=HistoricalTechnologies.Requirements(version,technology);
            if(prerequisites.Any(p=>hc.Technology[p]==0))return false;
            long urban=towns.Where(t=>t.FoodCapacity>=t.Population*.8&&t.Stability>=450).Sum(t=>t.Population);
            long largest=towns.Select(t=>t.Population).DefaultIfEmpty(0).Max();
            int prerequisiteYear=prerequisites.Select(p=>hc.Technology[p]-1).DefaultIfEmpty(0).Max();
            int maturation=technology>=9?600:technology>=5?300:100;
            if((version=="history-13"||(version=="history-14"||(version=="history-15"||version=="history-16")))&&technology>=15)maturation=technology>=27?100:180;
            if(year-prerequisiteYear<(contact?100:maturation))return false;
            if(technology>=15)
            {
                if(version!="history-13"&&(version!="history-15"&&version!="history-16"))return false;
                int specialistThreshold=technology>=27?12000:technology>=20?10000:5000;
                if(urban<specialistThreshold||hc.Institution<550||hc.Surplus<350)return false;
                if((version=="history-15"||version=="history-16")&&towns.Where(t=>t.PolityId>=0&&t.FoodCapacity>=t.Population*.8&&t.Stability>=450).Sum(t=>t.Population)<specialistThreshold)return false;
                if(version=="history-16")
                {
                    var supported=towns.Where(t=>t.PolityId>=0&&t.Population>=1000&&t.FoodCapacity>=t.Population*.8&&t.Stability>=450).ToArray();
                    int centers=technology>=27?4:technology>=20?3:2;long residents=technology>=27?100000:technology>=20?50000:15000;
                    if(supported.Length<centers||supported.Sum(t=>t.Population)<residents)return false;
                }
                switch(practice)
                {
                    case HistoricalTechnology.OceanNavigation:return water;
                    case HistoricalTechnology.CropRotation:case HistoricalTechnology.ArtificialFertilizers:case HistoricalTechnology.MechanizedAgriculture:return c.Farming>=400;
                    case HistoricalTechnology.Railways:case HistoricalTechnology.Telegraph:case HistoricalTechnology.Telecommunications:return towns.Length>=2;
                    default:return true;
                }
            }
            // Actual occupied coastal specialist sites supply this context; the
            // whole people's fishing/institution indices need not be coastal.
            if((version=="history-11"||(version=="history-12"||(version=="history-13"||(version=="history-14"||(version=="history-15"||version=="history-16")))))&&practice==HistoricalTechnology.Navigation)
                return coastalSpecialists&&hc.Sedentism>=300&&year-c.FoundedYear>=500;
            // Oral seafaring expertise does not require literate urban institutions.
            // Still requires a sustained community, surplus, mature sailing and coastal experience.
            if((version=="history-6"||version=="history-7"||version=="history-8"||version=="history-9"||(version=="history-10"||(version=="history-11"||(version=="history-12"||(version=="history-13"||(version=="history-14"||(version=="history-15"||version=="history-16")))))))&&practice==HistoricalTechnology.Navigation)
                return water&&c.Population>=(contact?600:1200)&&c.Fishing>=200&&hc.Sedentism>=300&&hc.Institution>=180&&hc.Surplus>=100;
            switch((HistoricalTechnology)technology)
            {
                case HistoricalTechnology.Pottery:return hc.Sedentism>=220;
                case HistoricalTechnology.Sailing:return c.Fishing>=200||water;
                case HistoricalTechnology.Irrigation:return c.Farming>=150&&hc.Institution>=180;
                case HistoricalTechnology.Copper:return urban>=350&&hc.Surplus>=120;
                case HistoricalTechnology.Writing:return largest>=2000&&urban>=3000&&hc.Institution>=450&&hc.Surplus>=350&&((version!="history-15"&&version!="history-16")||contact||towns.Any(t=>t.PolityId>=0&&t.Population>=1000));
                case HistoricalTechnology.Bronze:return urban>=1500&&hc.Institution>=250&&hc.Surplus>=200;
                case HistoricalTechnology.Roads:return (towns.Length>=2||largest>=5000)&&hc.Institution>=350;
                case HistoricalTechnology.Iron:return urban>=2500&&hc.Institution>=400&&hc.Surplus>=320;
                case HistoricalTechnology.Coinage:return urban>=5000&&hc.Institution>=450;
                case HistoricalTechnology.Mills:return urban>=1200&&c.Farming>=200;
                case HistoricalTechnology.Steel:return largest>=2000&&urban>=4000&&hc.Surplus>=350;
                case HistoricalTechnology.Navigation:return largest>=3000&&urban>=6000&&water&&hc.Institution>=450&&hc.Surplus>=350;
                case HistoricalTechnology.Scholasticism:return urban>=8000&&hc.Institution>=500&&(towns.Length>=2||largest>=8000);
                default:return true;
            }
        }
    }
}
