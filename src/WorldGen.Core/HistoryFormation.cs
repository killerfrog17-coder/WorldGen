using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    // Explicit game priors, not geographic bans or a calendar unlock.
    public static class HistoricalFormationEffects
    {
        public static double EmergenceFloor(int knowledge)
            =>D.Q(.34-.14*D.Clamp((knowledge-6500)/3500.0),1000000);
        public static int Preparation(int previous,long population,long foodCapacity,long reserve,int stability,int institutions,double localStorage)
        {
            bool supported=population>=5000&&foodCapacity>=population&&reserve>=population&&stability>=650&&institutions>=500&&localStorage>=.23;
            return supported?Math.Min(1000,previous+1):Math.Max(0,previous-2);
        }
        public static double CultivationEffort(Biome biome,bool introduced)
            =>biome==Biome.TropicalRainforest?(introduced?.7:.4):1;
    }
    public static class HistoricalExchange
    {
        // Bounded inhabited-path search. Foragers can relay knowledge without
        // acquiring the source identity, political ownership or food plants.
        // At most one physical trip is exposed here; later contacts relay it.
        public static int[] NearbyCultures(int culture,IEnumerable<int> sources,Func<int,IEnumerable<int>> neighbors,Func<int,bool> inhabited,Func<int,int> owner,Func<int,int,int> cost,int limit,CancellationToken token=default)
        {
            var distance=new Dictionary<int,int>();var heap=new Heap();var found=new SortedSet<int>();
            foreach(int source in sources.Distinct().OrderBy(i=>i))if(inhabited(source)){distance[source]=0;heap.Push(0,source);}
            while(heap.Count>0)
            {
                token.ThrowIfCancellationRequested();var x=heap.Pop();if(x.Key!=distance[x.Id])continue;
                int other=owner(x.Id);if(other>=0&&other!=culture)found.Add(other);
                foreach(int next in neighbors(x.Id))
                {
                    if(!inhabited(next))continue;long d=x.Key+Math.Max(1,cost(x.Id,next));
                    if(d>limit||distance.TryGetValue(next,out int old)&&d>=old)continue;
                    distance[next]=(int)d;heap.Push(d,next);
                }
            }
            return found.ToArray();
        }
    }
    public sealed partial class Prehistory
    {
        double LocalStoredSupport(PrehistoryState s,int culture,int cell)
        {
            var c=s.Society.Cultures[culture];
            // Crop storage traits already distinguish grain from gardens. Do not
            // impose the generic grain-season penalty on stored roots again.
            double crops=CultureAgriculture(s,cell)*c.Farming/850.0*CropYieldFactor(s,culture,cell,true);
            if(ModernHistory(s))crops*=ManagedLandMultiplier(s,culture,cell);
            if(RealismHistory(s)&&HistoricalLandscapeEffects.EverWet(CropHabitat(s,cell)))crops*=.25+.75*D.Clamp((s.History.Agriculture[culture].Cultivars?.Where(v=>v!=null).Select(v=>v.SelectedYears).DefaultIfEmpty(0).Max()??0)/12000.0);
            double aquatic=CultureOpportunity(s,cell)*c.Fishing/1100.0;
            return D.Q(D.Clamp(crops+aquatic),1000000);
        }
        void HistoricalExchangeContacts(PrehistoryState s,SortedSet<(int,int)> contacts,CancellationToken token)
        {
            var society=s.Society;var settlements=society.Settlements.GroupBy(t=>t.CultureId).ToDictionary(g=>g.Key,g=>g.Where(t=>t.Population>=100).Select(t=>t.Cell).ToArray());
            var populatedTowns=new HashSet<int>(society.Settlements.Where(t=>t.Population>=100).Select(t=>t.Cell));
            bool Inhabited(int cell)=>CellPatch[cell]>=0&&!World.Hydrology.OceanConnected[cell]&&World.Hydrology.LakeCoverageFraction[cell]<.9&&(populatedTowns.Contains(cell)||RuralCellPopulation(s,cell)>=10);
            foreach(var c in society.Cultures)
            {
                if(c.Population<100)continue;var hc=s.History.Cultures[c.Id];
                var sources=settlements.TryGetValue(c.Id,out var towns)?towns.Concat(new[]{c.OriginCell}):new[]{c.OriginCell};
                int Cost(int from,int to)
                {
                    double river=(World.Hydrology.Receiver[from]==to||World.Hydrology.Receiver[to]==from)&&HistoricalRiverAccess(from)>.01&&HistoricalRiverAccess(to)>.01?.65:1;
                    int metres=(int)Math.Round(D.Angle(World.Surface.Cells[from].Position,World.Surface.Cells[to].Position)*World.Recipe.RadiusKm*1000);
                    double cost=TravelCost(from,to,metres,hc)*river;
                    if(EmergenceHistory(s))cost*=HistoricalCropDiffusionEffects.EnvironmentalCost(World.Ecology.Habitat[from],World.Ecology.Habitat[to],World.Ecology.Habitat[c.OriginCell].AridityMilli<400);
                    if(ModernHistory(s))cost/=HistoricalModernEffects.LandTransport(hc,s.Years);
                    return Math.Max(1,(int)Math.Round(cost));
                }
                int limit=HistoricalTechnologies.Has(hc,HistoricalTechnology.Roads)?450000:250000;
                foreach(int other in HistoricalExchange.NearbyCultures(c.Id,sources,i=>World.Surface.Cells[i].Neighbors,Inhabited,i=>society.CellCulture[i],Cost,limit,token))
                    if(society.Cultures[other].Population>=100)contacts.Add((Math.Min(c.Id,other),Math.Max(c.Id,other)));
            }
        }
    }
}
