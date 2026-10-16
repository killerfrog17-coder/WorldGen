using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WorldGen.Core
{
    public static class Biogeography
    {
        public static bool Suitable(Taxon t,HabitatCell a,int elevation)
            => (t.Habitats&a.Habitat)!=0 && a.ColdTenthsC>=t.MinColdTenthsC && a.ColdTenthsC<=t.MaxColdTenthsC && a.WarmTenthsC>=t.MinWarmTenthsC && a.WarmTenthsC<=t.MaxWarmTenthsC
            && a.AridityMilli>=t.MinAridityMilli && a.AridityMilli<=t.MaxAridityMilli && elevation<=t.MaxElevationM
            && a.SalinityMilliPpt<=t.MaxSalinityMilliPpt && (a.FloodFractionPermille<500 || a.FloodDaysPerYear<=t.MaxFloodDays)
            && (t.Lineage==null || (( !t.Lineage.RequiresCoast||(a.Habitat&Habitat.Coastal)!=0)
                && (!t.Lineage.RequiresFreshwater||((a.Habitat&Habitat.Freshwater)!=0||a.FloodFractionPermille>=10&&a.SalinityMilliPpt<3000))
                && a.SalinityMilliPpt>=t.Lineage.MinSalinityMilliPpt
                && AnimalEcotypes.Canopy(a)>=t.Lineage.MinCanopyPermille&&AnimalEcotypes.Canopy(a)<=t.Lineage.MaxCanopyPermille));

        public static bool CanEat(Taxon consumer,Taxon resource)
        {
            if(consumer.Id==resource.Id || consumer.Producer || consumer.BasalResource || (consumer.Eats&resource.Provides)==0) return false;
            if(consumer.RequiredResource.Length>0 && resource.ScientificName!=consumer.RequiredResource) return false;
            if(resource.BasalResource) return true;
            if((consumer.Hunts&resource.Occupies)==0) return false;
            if(resource.Producer) return true;
            // A pack's large-ungulate prey envelope must not make adult apex
            // predators routine prey. Smaller predatory animals remain possible.
            if((resource.Eats&Food.Vertebrate)!=0 && resource.MassKg>consumer.MassKg*.25) return false;
            return resource.MassKg>=consumer.MinPreyKg && resource.MassKg<=consumer.MaxPreyKg;
        }
        public static bool Present(EcologyResult e,int cell,int taxon)
        { int stride=(e.Taxa.Length+31)/32; return (e.SpeciesBits[cell*stride+taxon/32]&(1<<(taxon%32)))!=0; }
        static void Set(EcologyResult e,int cell,int taxon,bool present)
        { int offset=cell*((e.Taxa.Length+31)/32)+taxon/32, bit=1<<(taxon%32); if(present) e.SpeciesBits[offset]|=bit; else e.SpeciesBits[offset]&=~bit; }

        public static void Build(World w,CancellationToken token)
        {
            var e=w.Ecology;bool fauna=w.Recipe.EcologyProfile==EarthFaunaPack.Version; bool native=fauna||w.Recipe.EcologyProfile==NativeLifePack.Version;
            e.Taxa=fauna?EarthFaunaPack.Create():native?NativeLifePack.Create():EarthLifePack.Create();e.PackVersion=fauna?EarthFaunaPack.Version:native?NativeLifePack.Version:"earth-starter-1"; int n=w.Surface.Cells.Length;
            e.SpeciesBits=new int[n*((e.Taxa.Length+31)/32)]; e.Origins=Enumerable.Repeat(-1,e.Taxa.Length).ToArray();
            var homelands=new Dictionary<string,int>(StringComparer.Ordinal); var used=new List<int>();
            var landSizes=w.Landmass.Where(id=>id>=0).GroupBy(id=>id).ToDictionary(g=>g.Key,g=>g.Count());int largestLand=landSizes.Count==0?1:landSizes.Values.Max();
            var historical=new bool[e.Taxa.Length][];
            // Homeland selection depends on earlier origins, so retain its exact order.
            foreach(var t in e.Taxa)if(t.Rank!=TaxonRank.FunctionalGuild)
            {
                if(fauna&&t.Lineage!=null)
                {
                    foreach(var source in t.Lineage.Sources)
                    {
                        source.OriginCell=FindOrigin(w,t,homelands,used,landSizes,largestLand,token,source);
                        if(e.Origins[t.Id]<0&&source.OriginCell>=0)e.Origins[t.Id]=source.OriginCell;
                    }
                }
                else e.Origins[t.Id]=FindOrigin(w,t,homelands,used,landSizes,largestLand,token);
            }
            int workers=Environment.ProcessorCount>1 && n>=20000?Math.Min(3,Environment.ProcessorCount):1;
            for(int first=0;first<e.Taxa.Length;first+=workers)
            {
                int start=first,count=Math.Min(workers,e.Taxa.Length-first);var presence=new bool[count][];
                void Disperse(int slot)
                {
                var t=e.Taxa[start+slot];
                token.ThrowIfCancellationRequested(); bool guild=t.Rank==TaxonRank.FunctionalGuild;
                if(guild)
                {
                    var cells=new bool[n];presence[slot]=cells;
                    for(int i=0;i<n;i++){if((i&2047)==0)token.ThrowIfCancellationRequested();cells[i]=!w.Hydrology.OceanConnected[i] && Suitable(t,e.Habitat[i],w.ElevationM[i]);}
                    return;
                }
                int origin=e.Origins[t.Id];if(origin<0)return;
                var current=Reach(w,t,origin,false,token); var past=t.HistoricalBridgeAllowed ? Reach(w,t,origin,true,token) : current;
                if(fauna&&t.Lineage!=null)foreach(var source in t.Lineage.Sources)
                    if(source.OriginCell>=0&&source.OriginCell!=origin)
                    {
                        var alternative=Reach(w,t,source.OriginCell,false,token);var former=t.HistoricalBridgeAllowed?Reach(w,t,source.OriginCell,true,token):alternative;
                        for(int i=0;i<n;i++){if((i&2047)==0)token.ThrowIfCancellationRequested();current[i]|=alternative[i];past[i]|=former[i];}
                    }
                historical[t.Id]=new bool[e.Regions.Length];
                for(int i=0;i<n;i++)
                {
                    if((i&2047)==0) token.ThrowIfCancellationRequested();
                    bool suitable=(current[i] || past[i]) && !w.Hydrology.OceanConnected[i] && Suitable(t,e.Habitat[i],w.ElevationM[i]);
                    if(suitable && !current[i] && e.Region[i]>=0)historical[t.Id][e.Region[i]]=true;
                    current[i]=suitable;
                }
                presence[slot]=current;
                }
                if(workers==1)Disperse(0);else Parallel.For(0,count,new ParallelOptions{CancellationToken=token,MaxDegreeOfParallelism=workers},Disperse);
                // Merge bits serially: different taxa can share the same bitset word.
                for(int slot=0;slot<count;slot++)if(presence[slot]!=null)
                    for(int i=0;i<n;i++){if((i&2047)==0)token.ThrowIfCancellationRequested();if(presence[slot][i])Set(e,i,e.Taxa[start+slot].Id,true);}
            }
            // An Arctic subspecies replaces, rather than duplicates, generic wolf presence.
            foreach(var sub in e.Taxa.Where(t=>t.Rank==TaxonRank.Subspecies))
            {
                var parent=e.Taxa.FirstOrDefault(t=>t.ScientificName==sub.ParentSpecies);
                if(parent!=null) for(int i=0;i<n;i++) if(Present(e,i,sub.Id)) Set(e,i,parent.Id,false);
            }
            var pairs=new List<Tuple<int,int>>();
            foreach(var consumer in e.Taxa) foreach(var resource in e.Taxa) if(CanEat(consumer,resource)) pairs.Add(Tuple.Create(consumer.Id,resource.Id));
            var pairIndices=Enumerable.Range(0,e.Taxa.Length).Select(_=>new List<int>()).ToArray();for(int k=0;k<pairs.Count;k++)pairIndices[pairs[k].Item1].Add(k);
            // Root every retained consumer in a local producer/detrital resource.
            // This removes unsupported predator cycles rather than inventing prey.
            var supported=new bool[e.Taxa.Length];
            var dependents=Enumerable.Range(0,e.Taxa.Length).Select(_=>new List<int>()).ToArray();
            foreach(var pair in pairs)dependents[pair.Item2].Add(pair.Item1);
            var queue=new int[e.Taxa.Length];
            for(int i=0;i<n;i++)
            {
                if((i&511)==0) token.ThrowIfCancellationRequested();
                if(w.Hydrology.OceanConnected[i])continue; // No native-pack taxon is placed in open ocean.
                int head=0,tail=0;
                for(int t=0;t<supported.Length;t++){supported[t]=Present(e,i,t) && (e.Taxa[t].Producer || e.Taxa[t].BasalResource);if(supported[t])queue[tail++]=t;}
                // Least basal-rooted closure; each consumer enters once. This is
                // identical to repeated fixed-point scans, without a depth cap.
                while(head<tail)foreach(int consumer in dependents[queue[head++]])
                    if(!supported[consumer]&&Present(e,i,consumer)){supported[consumer]=true;queue[tail++]=consumer;}
                for(int t=0;t<supported.Length;t++) if(!supported[t]) Set(e,i,t,false);
            }
            foreach(var r in e.Regions)
            {
                token.ThrowIfCancellationRequested(); var areas=new long[e.Taxa.Length]; var links=new long[pairs.Count];
                int[] formCells=fauna?Enumerable.Repeat(-1,e.Taxa.Length).ToArray():null,formCosts=fauna?Enumerable.Repeat(int.MaxValue,e.Taxa.Length).ToArray():null;
                foreach(int i in r.Cells)
                {
                    if((i&1023)==0) token.ThrowIfCancellationRequested(); long area=w.Surface.Cells[i].AreaM2;
                    int formCost=fauna?D.M(1000000*(1-V.Dot(w.Surface.Cells[i].Position,w.Surface.Cells[r.RepresentativeCell].Position))):0;
                    for(int t=0;t<areas.Length;t++) if(Present(e,i,t)){areas[t]+=area;if(fauna&&(formCost<formCosts[t]||formCost==formCosts[t]&&i<formCells[t])){formCosts[t]=formCost;formCells[t]=i;}foreach(int k in pairIndices[t])if(Present(e,i,pairs[k].Item2))links[k]+=area;}
                }
                var presence=new List<SpeciesPresence>();
                for(int t=0;t<areas.Length;t++) if(areas[t]>0) presence.Add(new SpeciesPresence { TaxonId=t, SuitableAreaPermille=Math.Max(1,D.M(1000.0*areas[t]/r.AreaM2)),OriginCell=e.Origins[t], UsedHistoricalBridge=historical[t]!=null && historical[t][r.Id] });
                r.Species=presence.ToArray();if(native)foreach(var species in r.Species)
                {
                    var t=e.Taxa[species.TaxonId];species.Variant=fauna?AnimalEcotypes.Create(w,t,r,formCells[t.Id]):NativeLifePack.Variant(w,t,r);
                    if(fauna&&t.Lineage!=null)
                    {
                        double best=double.MaxValue;
                        foreach(var source in t.Lineage.Sources)if(source.OriginCell>=0)
                        {double distance=D.Angle(w.Surface.Cells[source.OriginCell].Position,w.Surface.Cells[r.RepresentativeCell].Position);if(distance<best){best=distance;species.OriginCell=source.OriginCell;}}
                    }
                }
                var web=new List<FoodLink>();
                for(int k=0;k<pairs.Count;k++) if(links[k]>0) web.Add(new FoodLink { ConsumerId=pairs[k].Item1,ResourceId=pairs[k].Item2,Resource=e.Taxa[pairs[k].Item1].Eats&e.Taxa[pairs[k].Item2].Provides,OverlapPermille=Math.Max(1,D.M(1000.0*links[k]/r.AreaM2)) });
                r.FoodWeb=web.ToArray();
            }
            w.Metric("earth_pack_taxa",e.Taxa.Length); w.Metric("ecological_food_links",e.Regions.Sum(r=>r.FoodWeb.Length));
        }

        static int FindOrigin(World w,Taxon t,Dictionary<string,int> homes,List<int> used,Dictionary<int,int> landSizes,int largestLand,CancellationToken token,AnimalNativeSource source=null)
        {
            bool earth=w.Recipe.TerrainSource=="Earth";
            string group=source==null?t.OriginGroup:source.Realm;
            int home=homes.TryGetValue(group,out int h) ? h : -1;
            double lat=(source==null?t.EarthLatitude:source.Latitude)*D.Pi/180,lon=(source==null?t.EarthLongitude:source.Longitude)*D.Pi/180;
            var anchor=earth ? new V(D.Sin(D.Pi/2-lat)*D.Sin(D.Pi/2-lon),D.Sin(D.Pi/2-lat)*D.Sin(lon),D.Sin(lat)) : home>=0 ? w.Surface.Cells[home].Position : new Rng(w.Recipe.Seed,"life-homeland",t.Id).Vector();
            int best=-1; long score=long.MaxValue;
            for(int i=0;i<w.Surface.Cells.Length;i++)
            {
                if((i&2047)==0) token.ThrowIfCancellationRequested();
                if(w.Hydrology.OceanConnected[i] || !Suitable(t,w.Ecology.Habitat[i],w.ElevationM[i])) continue;
                if(!earth && home>=0 && w.Landmass[i]!=w.Landmass[home]) continue;
                double km=D.Angle(anchor,w.Surface.Cells[i].Position)*6371;
                // Earth anchors establish native provenance, not geographic climate fixes.
                if(earth && km>3500) continue;
                long cost=D.M(km*100);
                // Do not accidentally strand an entire continental fauna on a
                // tiny islet chosen for the first species in its realm.
                if(!earth&&home<0&&(w.Recipe.EcologyProfile==NativeLifePack.Version||w.Recipe.EcologyProfile==EarthFaunaPack.Version)&&landSizes.TryGetValue(w.Landmass[i],out int landSize))cost+=D.M(1000000*Math.Max(0,1-landSize/(largestLand*.15)));
                if(!earth && home<0) foreach(int other in used) cost+=D.M(Math.Max(0,4500-D.Angle(w.Surface.Cells[other].Position,w.Surface.Cells[i].Position)*6371)*150);
                if(!earth && home>=0 && w.Landmass[i]!=w.Landmass[home]) cost+=10000000;
                if(cost<score) { score=cost; best=i; }
            }
            if(best>=0 && home<0) { homes[group]=best; used.Add(best); }
            return best;
        }

        // Deterministic resistance-distance accessibility. MaxGap limits each stretch
        // of unsuitable habitat; total budget bounds accumulated migration cost.
        // Past pass exposes shallow shelves at -120m and cools the route by5C.
        public static bool[] Reach(World w,Taxon t,int origin,bool historical,CancellationToken token)
        {
            int n=w.Surface.Cells.Length; var reached=new bool[n]; if(origin<0) return reached;
            var cost=Enumerable.Repeat(long.MaxValue,n).ToArray(); var gap=new int[n]; var seaRun=new int[n]; var heap=new Heap();
            cost[origin]=0; heap.Push(0,origin);
            while(heap.Count>0)
            {
                token.ThrowIfCancellationRequested(); var item=heap.Pop(); int i=item.Id; if(item.Key!=cost[i]) continue; reached[i]=true;
                var cell=w.Surface.Cells[i];
                for(int k=0;k<cell.Neighbors.Length;k++)
                {
                    int j=cell.Neighbors[k]; var a=w.Ecology.Habitat[j]; int cold=a.ColdTenthsC-(historical?50:0),warm=a.WarmTenthsC-(historical?50:0);
                    bool ocean=w.Hydrology.OceanConnected[j], shelf=historical && ocean && w.ElevationM[j]>=-120;
                    bool sea=ocean && !shelf; int length=Math.Max(1,cell.DistanceM[k]);
                    if(t.Lineage!=null&&t.Lineage.AdjacentSeaOnly&&!ocean&&(a.Habitat&Habitat.Coastal)==0)continue;
                    if(!sea && (cold<t.PassageColdTenthsC || cold>t.MaxColdTenthsC+40 || warm>t.PassageMaxWarmTenthsC || w.ElevationM[j]>t.MaxElevationM+800)) continue;
                    int oceanDistance=sea ? seaRun[i]+length : 0;
                    if(sea && oceanDistance>t.OceanCrossingKm*1000) continue;
                    bool habitat=!ocean && Suitable(t,a,w.ElevationM[j]);
                    int gapDistance=habitat ? 0 : gap[i]+length;
                    // Exposed shelf is transit habitat if cold tolerance permits it;
                    // it does not become a present-day species residence.
                    if(shelf) gapDistance=0;
                    if(gapDistance>t.MaxGapKm*1000 && !sea) continue;
                    long relief=t.Lineage!=null&&t.Lineage.AdjacentSeaOnly?0:Math.Abs(w.ElevationM[j]-w.ElevationM[i])*20L;
                    long next=item.Key+length*(sea ? 12L : habitat ? 1L : 3L)+relief;
                    if(next>t.SpreadBudgetKm*1000L) continue;
                    if(next<cost[j]) { cost[j]=next; gap[j]=gapDistance; seaRun[j]=oceanDistance; heap.Push(next,j); }
                }
            }
            return reached;
        }
    }
}
