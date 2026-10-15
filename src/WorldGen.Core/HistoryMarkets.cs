using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        double RegionalFoodDensity(PrehistoryState s,CultureIdentity culture,int cell)
        {
            double cultivated=culture.Farming/850.0;
            double crop=StoredYield(s,cell)*cultivated*1.25*CropYieldFactor(s,culture.Id,cell,true)+AgriculturalYield(s,cell)*cultivated*.35*CropYieldFactor(s,culture.Id,cell,false);
            if(RealismHistory(s))crop*=6*AnimalFoodMultiplier(s,culture.Id,cell);
            if(ModernHistory(s))crop*=ManagedLandMultiplier(s,culture.Id,cell);
            bool coast=World.Surface.Cells[cell].Neighbors.Any(j=>World.Hydrology.OceanConnected[j]);
            return crop+((coast?.06:0)+Math.Min(.10,riverAccess[cell]*.6))*culture.Fishing/450.0;
        }
        long RegionalFoundingFood(PrehistoryState s,CultureIdentity culture,int cell)
        {
            PrepareHistoricalEnvironment();double reach=MarketReach(s,s.History.Cultures[culture.Id],cell)/1000;
            double radius=Math.Sqrt(World.Surface.Cells[cell].AreaM2/Math.PI)/1000;
            // Conservative host-cell part of the same physical catchment model.
            double area=Math.Min(Math.PI*reach*reach,HistoricalLandArea(cell)*D.Clamp((reach+radius)/(2*Math.Max(.001,radius))));
            return (long)(area*RegionalFoodDensity(s,culture,cell));
        }
        // Political separation alone is not an embargo. Informal peaceful exchange
        // has friction; a treaty removes that friction, while war blocks provisioning.
        // Shared pure rule for diagnostics. Negative means no accessible market edge.
        public static double MarketBorderCost(bool foreign,bool war,bool treaty)=>!foreign?1:war?-1:treaty?1:1.35;
        double MarketReach(PrehistoryState s,HistoricalCulture c,int cell)
        {
            if(!EmergenceHistory(s))return 65000+(HistoricalTechnologies.Has(c,HistoricalTechnology.Roads)?35000:0)+(HistoricalTechnologies.Has(c,HistoricalTechnology.Writing)?25000:0)+(HistoricalRiverAccess(cell)>.03?35000:0);
            double reach=35000+(HistoricalTechnologies.Has(c,HistoricalTechnology.Roads)?25000:0)+(HistoricalTechnologies.Has(c,HistoricalTechnology.Writing)?20000:0)+(HistoricalRiverAccess(cell)>.03?HistoricalTechnologies.Has(c,HistoricalTechnology.Sailing)?35000:15000:0);
            return RealismHistory(s)?reach*Math.Sqrt(HistoricalModernEffects.LandTransport(c,s.Years)):reach;
        }
        void HistoricalMarketsRevised(PrehistoryState s,CancellationToken token)
        {
            PrepareHistoricalEnvironment();var h=s.History;var towns=s.Society.Settlements;int n=World.Surface.Cells.Length;
            var distance=Enumerable.Repeat(int.MaxValue,n).ToArray();h.CellMarket=Enumerable.Repeat(-1,n).ToArray();var heap=new Heap();
            foreach(var t in towns){h.Towns[t.Id].FoodBase=0;if(t.Population<100)continue;distance[t.Cell]=0;h.CellMarket[t.Cell]=t.Id;heap.Push(0,t.Cell);}
            var trade=new HashSet<(int,int)>();var wars=new HashSet<(int,int)>();foreach(var r in h.Relations){if(r.Trade&&r.WarSince<0){trade.Add((r.A,r.B));trade.Add((r.B,r.A));}if(FrontierHistory(s)&&r.WarSince>=0){wars.Add((r.A,r.B));wars.Add((r.B,r.A));}}
            while(heap.Count>0)
            {
                var item=heap.Pop();int i=item.Id;if(item.Key!=distance[i])continue;token.ThrowIfCancellationRequested();
                var town=towns[h.CellMarket[i]];var hc=h.Cultures[town.CultureId];var c=World.Surface.Cells[i];double reach=MarketReach(s,hc,town.Cell);
                for(int k=0;k<c.Neighbors.Length;k++)
                {
                    int j=c.Neighbors[k];if(CellPatch[j]<0||World.Hydrology.OceanConnected[j]||World.Hydrology.LakeCoverageFraction[j]>=.9)continue;
                    int other=h.CellPolity[j];bool foreign=town.PolityId>=0&&other>=0&&town.PolityId!=other;double border=1;
                    if(FrontierHistory(s)){border=MarketBorderCost(foreign,wars.Contains((town.PolityId,other)),trade.Contains((town.PolityId,other)));if(border<0)continue;}
                    else if(foreign&&!trade.Contains((town.PolityId,other)))continue;
                    double river=(World.Hydrology.Receiver[i]==j||World.Hydrology.Receiver[j]==i)&&riverAccess[i]>.01&&riverAccess[j]>.01?.65:1;
                    long d=distance[i]+(long)(TravelCost(i,j,c.DistanceM[k],hc)*river*border);
                    double cellRadius=Math.Sqrt(World.Surface.Cells[j].AreaM2/Math.PI);
                    if(d>reach+cellRadius)continue;
                    if(d<distance[j]||d==distance[j]&&town.Id<h.CellMarket[j]){distance[j]=(int)d;h.CellMarket[j]=town.Id;heap.Push(d,j);}
                }
            }
            // Integrate a distance-decaying catchment; area is physical, not a fixed per-cell food allowance.
            var areas=new double[n];var total=new double[towns.Length];
            for(int i=0;i<n;i++)if(h.CellMarket[i]>=0)
            {
                var t=towns[h.CellMarket[i]];double reach=MarketReach(s,h.Cultures[t.CultureId],t.Cell),r=Math.Sqrt(World.Surface.Cells[i].AreaM2/Math.PI);
                double fraction=D.Clamp((reach-distance[i]+r)/(2*r));
                areas[i]=HistoricalLandArea(i)*fraction;total[t.Id]+=areas[i];
            }
            for(int i=0;i<n;i++)if(h.CellMarket[i]>=0)
            {
                var t=towns[h.CellMarket[i]];var c=s.Society.Cultures[t.CultureId];var hc=h.Cultures[t.CultureId];double reach=MarketReach(s,hc,t.Cell)/1000;
                double area=areas[i]*Math.Min(1,Math.PI*reach*reach/Math.Max(1,total[t.Id]));
                if(RegionalHistory(s)){h.Towns[t.Id].FoodBase+=(long)(area*RegionalFoodDensity(s,c,i));continue;}
                // Exported surplus, after local consumption: gardening is productive without supporting an equal tax surplus.
                double cultivation=c.Farming/850.0;double export=storedYield[i]*cultivation*1.25+agriculturalYield[i]*cultivation*.35;
                if(EraHistory(s))export=storedYield[i]*cultivation*1.25*CropYieldFactor(s,c.Id,i,true)+agriculturalYield[i]*cultivation*.35*CropYieldFactor(s,c.Id,i,false);
                bool coast=World.Surface.Cells[i].Neighbors.Any(j=>World.Hydrology.OceanConnected[j]);
                double aquatic=(coast?.06:0)+Math.Min(.10,riverAccess[i]*.6);
                h.Towns[t.Id].FoodBase+=(long)(area*(export+aquatic*c.Fishing/450.0));
            }
        }
    }
}
