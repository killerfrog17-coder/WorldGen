using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalOceanSettlement
    {
        public int Id,Year,SourceCulture,DestinationCulture,FromCell,ToCell,DistanceM;
        public long Migrants;
        [System.Runtime.Serialization.OptionalField] public long Provisions;
        [System.Runtime.Serialization.OptionalField] public int DepartureTownId=-1;
        [System.Runtime.Serialization.OptionalField] public long UrbanMigrants;
        [System.Runtime.Serialization.OptionalField] public long ArrivalFood;
        [System.Runtime.Serialization.OptionalField] public int SailingDistanceM;
        [System.Runtime.Serialization.OnDeserializing] void InitializeDeparture(System.Runtime.Serialization.StreamingContext context){DepartureTownId=-1;}
        public HistoricalOceanSettlement Copy()=>(HistoricalOceanSettlement)MemberwiseClone();
    }
    public static class HistoricalVoyageEffects
    {
        public readonly struct FounderAllocation
        {public readonly long Rural,Urban;public long Total=>Rural+Urban;public FounderAllocation(long rural,long urban){Rural=rural;Urban=urban;}}
        public static FounderAllocation AllocateFounders(long requested,long room,long localRural,long patchPopulation,long townPopulation)
        {
            long availableRural=Math.Min(Math.Max(0,localRural)/8,Math.Max(0,patchPopulation)/10),availableUrban=Math.Max(0,townPopulation)/10;
            long total=Math.Max(0,Math.Min(requested,Math.Min(room,Math.Min((Math.Max(0,localRural)+Math.Max(0,townPopulation))/8,availableRural+availableUrban))));
            if(total<50)return new FounderAllocation(0,0);long rural=Math.Min(total,availableRural);return new FounderAllocation(rural,total-rural);
        }
        // A sampled settlement opportunity over a cultural-update window, not
        // a tactical boat/weather simulation or an automatically known sea lane.
        public static double SettlementChance(int distanceM,int navigationYears,int fishing,double storage,double windSupport,long stagingResidents=1200,int maximumDistanceM=3500000,double vesselSupport=1)
        {
            if(distanceM<=900000||distanceM>maximumDistanceM||navigationYears<200||fishing<200||storage<.2)return 0;
            double experience=.35+.65*(1-Math.Exp(-(navigationYears-200)/1600.0));
            double chance=.18*Math.Exp(-distanceM/(1800000.0*Math.Max(1,maximumDistanceM/3500000.0)))*experience*D.Clamp(fishing/450.0)*D.Clamp(storage)*(.35+.65*D.Clamp(windSupport))*Math.Max(1,Math.Min(3,vesselSupport));
            double crews=Math.Min(12,Math.Max(.5,Math.Sqrt(Math.Max(0,stagingResidents)/1000.0)));
            return D.Q(1-Math.Pow(1-chance,crews),1000000000);
        }
        public static long Provisioning(long migrants,int distanceM)
            =>migrants<=0?0:Math.Max(1,(long)Math.Ceiling(D.Q(migrants*(30+Math.Max(0,distanceM)/90000.0)/365*1.5,1000000)));
    }
    public sealed partial class Prehistory
    {
        CellLocator voyageCoastLocator,voyageWorldLocator;int[] voyageCoastCells;
        readonly Dictionary<(int,int,int,int),Route[]> voyageDestinations=new Dictionary<(int,int,int,int),Route[]>();
        public Route[] AssessRemoteOceanRoutes(int cell,CancellationToken token=default)
        {
            if(cell<0||cell>=CellPatch.Length||CellPatch[cell]<0)throw new ArgumentOutOfRangeException(nameof(cell));
            return RemoteVoyageDestinations(cell,token).Select(r=>new Route{A=r.A,B=r.B,CellA=r.CellA,CellB=r.CellB,DistanceM=r.DistanceM,Sea=true}).ToArray();
        }
        int VoyageLandmass(int cell)=>World.Hydrology.OceanConnected[cell]?World.Surface.Cells.Length+cell:World.Landmass[cell];
        long CoastalStagingResidents(PrehistoryState s,int cell,int culture)
        {
            int patch=CellPatch[cell];long residents=RuralCellPopulation(s,cell);
            foreach(int next in World.Surface.Cells[cell].Neighbors)if(CellPatch[next]==patch&&s.Society.CellCulture[next]==culture)residents+=RuralCellPopulation(s,next);
            return Math.Min(s.Population[patch],residents);
        }
        Route[] RemoteVoyageDestinations(int cell,CancellationToken token,int maximumDistanceM=3500000,int minimumCapacity=25,int maximumCandidates=12)
        {
            var cacheKey=(cell,maximumDistanceM,minimumCapacity,maximumCandidates);if(voyageDestinations.TryGetValue(cacheKey,out var cached))return cached;
            if(voyageCoastLocator==null)
            {
                voyageCoastCells=Enumerable.Range(0,World.Surface.Cells.Length).Where(i=>CellPatch[i]>=0&&Patches[CellPatch[i]].Capacity>=25&&(World.Hydrology.OceanConnected[i]||World.Surface.Cells[i].Neighbors.Any(j=>World.Hydrology.OceanConnected[j]))).ToArray();
                voyageCoastLocator=new CellLocator(voyageCoastCells.Select(i=>World.Surface.Cells[i]).ToArray());voyageWorldLocator=new CellLocator(World.Surface.Cells);
            }
            var point=World.Surface.Cells[cell].Position;double chord=2*D.Sin(maximumDistanceM/1000.0/(2*World.Recipe.RadiusKm));
            var candidates=new SortedDictionary<int,(int Cell,int Distance)>();
            foreach(int index in voyageCoastLocator.Within(point,chord*chord))
            {
                token.ThrowIfCancellationRequested();int to=voyageCoastCells[index];if(VoyageLandmass(to)==VoyageLandmass(cell)||Patches[CellPatch[to]].Capacity<minimumCapacity)continue;
                int distance=(int)Math.Round(D.Q(D.Angle(point,World.Surface.Cells[to].Position)*World.Recipe.RadiusKm*1000,1000000));
                if(distance<=900000)continue;int landmass=VoyageLandmass(to);
                if(!candidates.TryGetValue(landmass,out var old)||distance<old.Distance||distance==old.Distance&&to<old.Cell)candidates[landmass]=(to,distance);
            }
            var routes=new List<Route>();
            var choices=candidates.Values.OrderBy(x=>x.Distance).ThenBy(x=>x.Cell).Take(maximumCandidates<=12?maximumCandidates:maximumCandidates*3/4);
            if(maximumCandidates>12)choices=choices.Concat(candidates.Values.OrderByDescending(x=>Patches[CellPatch[x.Cell]].Capacity).ThenBy(x=>x.Distance).ThenBy(x=>x.Cell).Take(maximumCandidates/4)).GroupBy(x=>x.Cell).Select(g=>g.First()).OrderBy(x=>x.Distance).ThenBy(x=>x.Cell);
            foreach(var candidate in choices)
            {
                token.ThrowIfCancellationRequested();var destination=World.Surface.Cells[candidate.Cell].Position;int steps=Math.Max(12,(int)Math.Ceiling(candidate.Distance/80000.0));bool ocean=true;
                // Do not sail across an intervening continent. Large intermediate
                // islands are their own nearer opportunities, not ignored water.
                for(int k=1;k<steps;k++)
                {
                    var sample=(point*(1-k/(double)steps)+destination*(k/(double)steps)).Unit;int at=voyageWorldLocator.Nearest(sample);
                    if(!World.Hydrology.OceanConnected[at]&&VoyageLandmass(at)!=VoyageLandmass(cell)&&VoyageLandmass(at)!=VoyageLandmass(candidate.Cell)){ocean=false;break;}
                    if(!World.Hydrology.OceanConnected[at]&&k>2&&k<steps-2){ocean=false;break;}
                }
                // Modern callers may investigate a bounded number of detours
                // after filtering live occupied/unsuitable destinations. Do not
                // hide those opportunities in this immutable geometry shortlist.
                if(ocean||maximumCandidates>12&&maximumDistanceM>3500000)routes.Add(new Route{A=CellPatch[cell],B=CellPatch[candidate.Cell],CellA=cell,CellB=candidate.Cell,DistanceM=candidate.Distance,Sea=true});
            }
            cached=routes.ToArray();voyageDestinations[cacheKey]=cached;return cached;
        }
        double VoyageWindSupport(int from,int to)
        {
            var a=World.Surface.Cells[from].Position;var b=World.Surface.Cells[to].Position;var middle=(a+b).Unit;int at=voyageWorldLocator.Nearest(middle);
            V east=new V(-middle.Y,middle.X,0).Unit,north=V.Cross(middle,east).Unit,direction=(b-a).Unit;
            double support=0;foreach(var season in new[]{World.Climate.NorthernSpring,World.Climate.NorthernSummer,World.Climate.NorthernAutumn,World.Climate.NorthernWinter})
            {
                double x=season.WindEastMilli[at]+season.CurrentEastMilli[at],y=season.WindNorthMilli[at]+season.CurrentNorthMilli[at];
                double along=V.Dot(east*x+north*y,direction)/Math.Max(1,Math.Sqrt(x*x+y*y));support=Math.Max(support,.5+.5*along);
            }
            return D.Q(D.Clamp(support),1000000);
        }
        void AdvanceRemoteOceanSettlements(PrehistoryState s,CancellationToken token,double elapsedYears)
        {
            if(ModernHistory(s)){AdvanceModernOceanSettlements(s,token,elapsedYears);return;}
            if(!DelegatedHistory(s)||s.Society.Cultures.Length==0)return;int count=s.Society.Cultures.Length;
            var shores=CulturalCells(s).Where(i=>CellPatch[i]>=0&&(World.Hydrology.OceanConnected[i]||World.Surface.Cells[i].Neighbors.Any(j=>World.Hydrology.OceanConnected[j]))).GroupBy(i=>s.Society.CellCulture[i]).OrderBy(g=>g.Key).ToArray();
            foreach(var group in shores)
            {
                token.ThrowIfCancellationRequested();int source=group.Key;if(source>=count)continue;var culture=s.Society.Cultures[source];var hc=s.History.Cultures[source];
                int adoption=hc.Technology[(int)HistoricalTechnology.Navigation];if(adoption<=0||s.Years-adoption+1<200)continue;
                Route best=null;double probability=0;long bestResidents=0;int bestDepot=-1;
                // Up to four productive coastal staging areas; repeated routine
                // opportunities are not a fully connected global maritime graph.
                var staging=new List<int>();var shore=group.Where(i=>CoastalStagingResidents(s,i,source)>=600&&CultureOpportunity(s,i)>=.2).ToArray();
                if(shore.Length>0)staging.Add(shore.OrderByDescending(i=>RuralCellPopulation(s,i)).ThenBy(i=>i).First());
                while(staging.Count<4&&staging.Count<shore.Length){int next=shore.Where(i=>!staging.Contains(i)).OrderByDescending(i=>staging.Min(j=>D.Angle(World.Surface.Cells[i].Position,World.Surface.Cells[j].Position))).ThenBy(i=>i).First();staging.Add(next);}
                foreach(int from in staging)
                {
                    int patch=CellPatch[from];long residents=CoastalStagingResidents(s,from,source);if(residents<600||CultureOpportunity(s,from)<.2)continue;
                    int market=s.History.CellMarket.Length>from?s.History.CellMarket[from]:-1;
                    if(market<0)continue;var depot=s.Society.Settlements[market];
                    if(depot.CultureId!=source||depot.Population<500||depot.FoodReserve<depot.Population/2||s.History.Towns[depot.Id].DiseaseUntil>=s.Years)continue; // Provisioning for a multi-week passage.
                    foreach(var route in RemoteVoyageDestinations(from,token))
                    {
                        int target=route.B;if(s.Population[target]>0||s.Society.CellCulture[route.CellB]>=0||Patches[target].Capacity<50)continue;
                        double chance=HistoricalVoyageEffects.SettlementChance(route.DistanceM,s.Years-adoption+1,Math.Max(250,culture.Fishing),CultureOpportunity(s,from),VoyageWindSupport(from,route.CellB),residents);
                        if(chance>probability||chance==probability&&best!=null&&route.CellB<best.CellB){best=route;probability=chance;bestResidents=residents;bestDepot=depot.Id;}
                    }
                }
                if(best==null||HistoryRoll("remote-ocean-settlement-11",source,s.Years)>=ElapsedChance(probability,elapsedYears/50))continue;
                long migrants=Math.Min(Patches[best.B].Capacity,50+(int)(100*HistoryRoll("ocean-founders-11",source,s.Years)));
                migrants=Math.Min(migrants,Math.Min(bestResidents/8,s.Population[best.A]/10));if(migrants<50)continue;
                long provisions=HistoricalVoyageEffects.Provisioning(migrants,best.DistanceM);var departure=s.Society.Settlements[bestDepot];if(departure.FoodReserve<provisions)continue;departure.FoodReserve-=provisions;
                s.Population[best.A]-=migrants;s.Population[best.B]+=migrants;s.ArrivalYear[best.B]=s.Years;
                for(int k=0;k<8;k++)s.Ancestry[best.B*8+k]=s.Ancestry[best.A*8+k];
                var child=FoundCulture(s,best.B,best.CellB);InitializeHistoricalCulture(s,child);InheritHistoricalCommunity(s,child,source,true);
                var records=s.History.OceanSettlements??Array.Empty<HistoricalOceanSettlement>();
                s.History.OceanSettlements=records.Concat(new[]{new HistoricalOceanSettlement{Id=records.Length,Year=s.Years,SourceCulture=source,DestinationCulture=child.Id,FromCell=best.CellA,ToCell=best.CellB,DistanceM=best.DistanceM,Migrants=migrants,Provisions=provisions}}).ToArray();
                HistoryEvent(s,"overseas-settlement",culture.Name+" voyagers establish a distant coastal community after an open-ocean passage of approximately "+(best.DistanceM/1000)+" km. The "+child.Name+" retain related seafaring and food traditions without becoming territory of the source court.",best.CellB,child.Id,category:ChronicleCategory.Society);
            }
        }
    }
}
