using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        public const string AggregateHistoryVersion="history-17";
        static bool AggregateHistory(PrehistoryState s)=>s?.History?.Aggregates!=null;
        // Explicit migration, not an invisible edit to a historical save. The
        // one-time census below is the only fine-terrain aggregation pass.
        public PrehistoryState UpgradeToAggregateHistory(PrehistoryState source,CancellationToken token=default)
        {
            Validate(source);if(AggregateHistory(source))return source;
            if(!ModernHistory(source))source=UpgradeToCurrentHistory(source);if(AggregateHistory(source))return source;
            var s=source.Copy();var h=s.History;int n=CellPatch.Length;
            foreach(var crops in h.Agriculture){if(crops.HusbandryYears==null)crops.HusbandryYears=new int[6];if(crops.Cultivars==null)crops.Cultivars=new HistoricalCultivar[6];}
            PrepareAggregateEnvironment(s);
            var a=new HistoricalAggregateState();h.Aggregates=a;
            a.Cells=HistoricalCellTree.From(checked(n*5),key=>{int cell=key/5;switch(key%5){case 0:return cell<s.Society.CellCulture.Length?s.Society.CellCulture[cell]:-1;case 1:return cell<h.CellPolity.Length?h.CellPolity[cell]:-1;case 2:return cell<h.CellReligion.Length?h.CellReligion[cell]:-1;case 3:return cell<h.CellMarket.Length?h.CellMarket[cell]:-1;default:return cell<h.CellStage.Length?h.CellStage[cell]:0;}});
            a.Successors=new HistoricalCellTree(33554432);a.TownAt=new HistoricalCellTree(n);
            a.Cultures=s.Society.Cultures.Select(c=>new HistoricalCultureAggregate{Id=c.Id,LastYear=s.Years,Region=CellPatch[c.OriginCell],CivilizationSince=c.Stage==SocietyStage.Civilization?c.StageYear:-1}).ToArray();
            a.Polities=h.Polities.Select(p=>new HistoricalPolityAggregate{Id=p.Id,LastYear=s.Years,LastSuccession=p.RulerSince,SuccessionDue=p.RulerSince+30,ReachKm=100}).ToArray();
            a.Cities=s.Society.Settlements.Select(t=>new HistoricalCityAggregate{Id=t.Id,LastYear=s.Years,LastHarvestYear=s.Years,LastPoliticalYear=s.Years,FoodYieldMilli=(int)(agriculturalYield[t.Cell]*1000),StoredYieldMilli=(int)(storedYield[t.Cell]*1000),FloodMilli=(int)(riverAccess[t.Cell]*1000),SoilMilli=World.Ecology.Habitat[t.Cell].SoilFertilityPermille,BaseUrbanCapacity=Math.Max(300,t.FoodCapacity),BaseFarming=s.Society.Cultures[t.CultureId].Farming,BaseFoodMultiplier=HistoricalModernEffects.FoodMultiplier(h.Cultures[t.CultureId],s.Years)}).ToArray();
            var adjacent=Enumerable.Range(0,Patches.Length).Select(_=>new List<int>()).ToArray();
            for(int r=0;r<Routes.Length;r++){adjacent[Routes[r].A].Add(r);adjacent[Routes[r].B].Add(r);}
            a.Regions=new HistoricalRegionAggregate[Patches.Length];
            for(int p=0;p<Patches.Length;p++)
            {
                token.ThrowIfCancellationRequested();var patch=Patches[p];var region=new HistoricalRegionAggregate{Id=p,LastYear=s.Years,Capacity=patch.Capacity,BestCell=patch.Cell,Routes=adjacent[p].ToArray()};double score=-1,area=0,yield=0,stored=0,soil=0,temp=0,aridity=0,relief=0,opportunity=0;
                foreach(int cell in patch.Cells)
                {
                    double weight=Math.Max(.001,inhabitableArea[cell]);var habitat=World.Ecology.Habitat[cell];area+=weight;yield+=agriculturalYield[cell]*weight;stored+=storedYield[cell]*weight;soil+=habitat.SoilFertilityPermille*weight;temp+=habitat.MeanTenthsC*weight;aridity+=habitat.AridityMilli*weight;relief+=habitat.ReliefM*weight;opportunity+=settlementOpportunity[cell]*weight;
                    double local=storedYield[cell]+.35*riverSites[cell]+.1*wildCropStorage[cell];if(local>score){score=local;region.BestCell=cell;}region.NativeMask|=nativeCrops[cell];
                }
                area=Math.Max(.001,area);region.AreaKm2=(long)Math.Round(area);region.YieldMilli=(int)Math.Round(yield/area*1000);region.StoredMilli=(int)Math.Round(stored/area*1000);region.SoilMilli=(int)Math.Round(soil/area);region.MeanTenthsC=(int)Math.Round(temp/area);region.AridityMilli=(int)Math.Round(aridity/area);region.ReliefM=(int)Math.Round(relief/area);region.OpportunityMilli=(int)Math.Round(opportunity/area*1000);
                region.Culture=HistoricalCells.Culture(s,region.BestCell);a.Regions[p]=region;
            }
            // Catchment ownership is prepared once, with a multi-source terrain
            // search. Afterwards a city owns the population/account, not a list
            // that needs to be traversed at harvest or taxation time.
            var cityAt=Enumerable.Repeat(-1,n).ToArray();var distance=Enumerable.Repeat(long.MaxValue,n).ToArray();var heap=new Heap();
            foreach(var t in s.Society.Settlements){cityAt[t.Cell]=t.Id;distance[t.Cell]=0;heap.Push(0,t.Cell);a.TownAt=a.TownAt.With(t.Cell,t.Id);a.Cultures[t.CultureId].Cities.Add(t.Id);a.Cultures[t.CultureId].CityCount++;if(t.PolityId>=0&&h.Polities[t.PolityId].EndedYear<0){a.Polities[t.PolityId].Cities.Add(t.Id);a.Polities[t.PolityId].CityCount++;}}
            while(heap.Count>0)
            {
                var item=heap.Pop();int cell=item.Id;if(item.Key!=distance[cell])continue;if((cell&1023)==0)token.ThrowIfCancellationRequested();var c=World.Surface.Cells[cell];
                for(int j=0;j<c.Neighbors.Length;j++){int next=c.Neighbors[j];if(CellPatch[next]<0)continue;long d=item.Key+(long)Math.Round(c.DistanceM[j]*(1+World.Ecology.Habitat[next].ReliefM/1200.0));if(d<distance[next]||d==distance[next]&&cityAt[cell]<cityAt[next]){distance[next]=d;cityAt[next]=cityAt[cell];heap.Push(d,next);}}
            }
            var links=Enumerable.Range(0,a.Cities.Length).Select(_=>new HashSet<int>()).ToArray();
            foreach(var c in s.Society.Cultures){c.Population=0;c.AreaKm2=0;}
            foreach(var p in h.Polities){if(p.EndedYear<0){p.Population=0;p.AreaKm2=0;}}
            for(int cell=0;cell<n;cell++)
            {
                if((cell&1023)==0)token.ThrowIfCancellationRequested();int patch=CellPatch[cell];if(patch<0)continue;int culture=HistoricalCells.Culture(s,cell),polity=HistoricalCells.Polity(s,cell),city=cityAt[cell];long rural=RuralCellPopulation(source,cell),area=(long)Math.Round(inhabitableArea[cell]);
                if(culture>=0){s.Society.Cultures[culture].Population+=rural;s.Society.Cultures[culture].AreaKm2+=area;}
                if(polity>=0){h.Polities[polity].Population+=rural;h.Polities[polity].AreaKm2+=area;}
                // Only actual city supply regions become city rural population.
                // Remote residents remain in their ecoregion population store.
                bool catchment=city>=0&&distance[cell]<=180000&&(culture==s.Society.Settlements[city].CultureId||polity>=0&&polity==s.Society.Settlements[city].PolityId);
                if(catchment){var ca=a.Cities[city];ca.Rural+=rural;ca.RuralCapacity+=Math.Max(rural,ruralWeight[cell]/1000);ca.AreaKm2+=area;s.Population[patch]-=rural;a.Regions[patch].Capacity=Math.Max(0,a.Regions[patch].Capacity-ruralWeight[cell]/1000);}
                else if(polity>=0){a.Polities[polity].Remote+=rural;a.Polities[polity].RemoteCapacity+=Math.Max(rural,ruralWeight[cell]/1000);s.Population[patch]-=rural;a.Regions[patch].Capacity=Math.Max(0,a.Regions[patch].Capacity-ruralWeight[cell]/1000);}
                foreach(int next in World.Surface.Cells[cell].Neighbors)
                {
                    if(culture>=0&&HistoricalCells.Culture(s,next)!=culture)a.Cultures[culture].Frontier.Add(cell);
                    if(polity>=0&&HistoricalCells.Polity(s,next)!=polity)a.Polities[polity].Frontier.Add(cell);
                    int other=cityAt[next];if(city>=0&&other>=0&&other!=city)links[city].Add(other);
                }
            }
            for(int i=0;i<a.Cities.Length;i++)
            {
                var city=a.Cities[i];var t=s.Society.Settlements[i];city.Neighbors=links[i].OrderBy(j=>D.Angle(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[s.Society.Settlements[j].Cell].Position)).ThenBy(j=>j).Take(12).ToArray();
                city.Supported=t.FoodCapacity>=t.Population*.8&&t.Stability>=450;
                city.LastVoyageYear=s.Years;
                city.BaseCropFactor=.4+.6*AggregateCropSupport(s,t.CultureId,t.Cell,false);
                var c=a.Cultures[t.CultureId];c.Urban+=t.Population;c.Rural+=city.Rural+city.Suburban;c.LargestCity=Math.Max(c.LargestCity,t.Population);c.FoodCapacity+=t.FoodCapacity;if(t.FoodCapacity>=t.Population*.8&&t.Stability>=450){c.SupportedCities++;c.SupportedUrban+=t.Population;}
                s.Society.Cultures[t.CultureId].Population+=t.Population;
                if(t.PolityId>=0&&h.Polities[t.PolityId].EndedYear<0){var p=a.Polities[t.PolityId];p.Urban+=t.Population;p.Rural+=city.Rural;h.Polities[t.PolityId].Population+=t.Population;}
            }
            a.TotalUrban=s.Society.Settlements.Sum(t=>t.Population);a.TotalPopulation=s.Population.Sum()+a.TotalUrban+a.Cities.Sum(t=>t.Rural+t.Suburban)+a.Polities.Sum(p=>p.Remote);
            a.FirstMaximumTechnologyYear=h.Cultures.Where(c=>c.Technology.All(y=>y>0)).Select(c=>c.Technology.Max()-1).DefaultIfEmpty(-1).Min();
            foreach(var p in h.Polities)if(p.EndedYear<0){var account=a.Polities[p.Id];account.RemoteAreaKm2=p.AreaKm2;p.Population=account.Urban+account.Rural+account.Suburban+account.Remote;}
            a.ArchiveBaseCount=h.Archive.Count;a.ArchiveBaseHash=StateHash.Compute(h.Archive);a.ArchiveHash=a.ArchiveBaseHash;a.LeaderBaseCount=h.Dynamics.Leaders.Length;a.LeaderBaseHash=StateHash.Compute(h.Dynamics.Leaders);
            var leaderWords=new int[h.Dynamics.Leaders.Length*8];for(int i=0;i<h.Dynamics.Leaders.Length;i++){string digest=StateHash.Compute(h.Dynamics.Leaders[i]);for(int k=0;k<8;k++)leaderWords[i*8+k]=unchecked((int)Convert.ToUInt32(digest.Substring(k*8,8),16));}
            a.LeaderDigests=HistoricalCellTree.From(leaderWords.Length,key=>leaderWords[key]).Grow(33554432);
            h.CellPolity=h.CellReligion=h.CellMarket=h.CellStage=Array.Empty<int>();s.Society.CellCulture=Array.Empty<int>();
            a.PreviousAdministration=new HistoricalFrozenAdministration(h.Administration);h.Faith=null;h.RegionalLearning=null;h.Administration=new HistoricalAdministration();h.Version=AggregateHistoryVersion;
            s.Timeline=InitialAggregateTimeline(s);s.Seal();return s;
        }
    }
}
