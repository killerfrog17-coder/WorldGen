using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    [Serializable] public sealed class SmallIsland
    {
        public int Id, Cell, SourceFeatureId=-1, PeakM;
        public string Origin="";
        public long AreaM2;
        public V Center;
        public V[] Outline=Array.Empty<V>();
    }
    [Serializable] public sealed class IslandCell
    {
        public int Cell, FirstIsland, IslandCount;
        public long TotalAreaM2, LargestIslandAreaM2;
        public double CoverageFraction;
    }
    [Serializable] public sealed class SubcellResult
    {
        public SmallIsland[] Islands=Array.Empty<SmallIsland>();
        public IslandCell[] Cells=Array.Empty<IslandCell>();
        public string Source="";
    }
    /// <summary>Persistent unresolved land, separate from whole-cell terrain and ocean connectivity.</summary>
    public static class SubcellGeography
    {
        public static IslandCell Find(World w,int cell)
        {
            var groups=w.Subcells?.Cells;if(groups==null)return null;
            int a=0,b=groups.Length-1;while(a<=b){int m=(a+b)/2;if(groups[m].Cell==cell)return groups[m];if(groups[m].Cell<cell)a=m+1;else b=m-1;}return null;
        }
        public static void Build(World w,CancellationToken token)
        {
            var islands=new List<SmallIsland>();
            if(w.Recipe.TerrainSource=="Earth")EarthTerrain.SmallIslands(w,islands,token);
            else Procedural(w,islands,token);
            islands=islands.OrderBy(i=>i.Cell).ThenBy(i=>i.Id).ToList();
            for(int i=0;i<islands.Count;i++)islands[i].Id=i;
            var groups=new List<IslandCell>();
            foreach(var group in islands.GroupBy(i=>i.Cell))
            {
                long area=group.Sum(i=>i.AreaM2);
                groups.Add(new IslandCell{Cell=group.Key,FirstIsland=group.First().Id,IslandCount=group.Count(),TotalAreaM2=area,
                    LargestIslandAreaM2=group.Max(i=>i.AreaM2),CoverageFraction=D.Q(area/(double)w.Surface.Cells[group.Key].AreaM2,1000000000)});
            }
            w.Subcells=new SubcellResult{Islands=islands.ToArray(),Cells=groups.ToArray(),Source=w.Recipe.TerrainSource=="Earth"?EarthTerrain.Dataset:"geological-islands-331"};
            w.Metric("subcell_islands",islands.Count);w.Metric("subcell_island_area_km2",islands.Sum(i=>(double)i.AreaM2)/1e6);
        }
        public static void Validate(World w)
        {
            var data=w.Subcells;if(data==null || data.Islands==null || data.Cells==null)throw new ArgumentException("Missing sub-cell geography.");
            int next=0,previous=-1;
            foreach(var group in data.Cells)
            {
                if(group==null || group.Cell<=previous || group.Cell>=w.Surface.Cells.Length || !w.Hydrology.OceanConnected[group.Cell] || group.FirstIsland!=next || group.IslandCount<=0 || group.IslandCount>data.Islands.Length-next)throw new ArgumentException("Invalid island-cell index.");
                long sum=0,largest=0;
                for(int k=0;k<group.IslandCount;k++)
                {
                    var island=data.Islands[next];
                    bool Unit(V p)=>!double.IsNaN(p.Length) && Math.Abs(p.Length-1)<.00001;
                    if(island==null || island.Id!=next || island.Cell!=group.Cell || island.AreaM2<=0 || island.PeakM<0 || island.Outline==null || island.Outline.Length<3 || !Unit(island.Center) || island.Outline.Any(p=>!Unit(p)))throw new ArgumentException("Invalid island record.");
                    sum=checked(sum+island.AreaM2);largest=Math.Max(largest,island.AreaM2);next++;
                }
                double coverage=sum/(double)w.Surface.Cells[group.Cell].AreaM2;
                if(sum!=group.TotalAreaM2 || largest!=group.LargestIslandAreaM2 || coverage>.5 || !double.IsFinite(group.CoverageFraction) || Math.Abs(coverage-group.CoverageFraction)>1e-9)throw new ArgumentException("Island area accounting mismatch.");
                previous=group.Cell;
            }
            if(next!=data.Islands.Length)throw new ArgumentException("Unindexed island records.");
        }
        static void Procedural(World w,List<SmallIsland> islands,CancellationToken token)
        {
            int n=w.Surface.Cells.Length;double radius=w.Recipe.RadiusKm*1000.0;
            var source=Enumerable.Repeat(-1,n).ToArray();
            foreach(var f in w.Features)
                if(f.Kind==FeatureKind.Hotspot || f.Kind==FeatureKind.RidgeHotspot || f.Kind==FeatureKind.IslandChain || f.Kind==FeatureKind.IslandArc || f.Kind==FeatureKind.RelictArc)
                    foreach(int i in f.Cells)if(source[i]<0)source[i]=f.Id;
            for(int i=0;i<n;i++)
            {
                if((i&1023)==0)token.ThrowIfCancellationRequested();
                if(!w.Hydrology.OceanConnected[i])continue;
                var cell=w.Surface.Cells[i];bool volcanic=source[i]>=0;
                // Shelf fragments and reef/cay groups are clustered, not uniformly sprinkled over abyssal oceans.
                double shelf=D.Clamp((w.ElevationM[i]+320)/320.0);
                double cluster=D.Clamp((Terrain.Noise(cell.Position,w.Recipe.Seed^44191,18)-.04)*4);
                double density=volcanic?.00012:(shelf*cluster*.00010);
                if(density<=0)continue;
                var rng=new Rng(w.Recipe.Seed,"subcell-islands-331",i);
                int count=Math.Min(24,(int)Math.Floor(cell.AreaM2/1e6*density+rng.Unit()));if(count==0)continue;
                double inner=cell.Neighbors.Min(j=>D.Angle(cell.Position,w.Surface.Cells[j].Position))*radius*.45;
                long remaining=(long)(cell.AreaM2*.2);var accepted=new List<Tuple<V,double>>();
                V east=V.Tangent(cell.Position,new V(0,0,1));if(east.Length<.5)east=V.Tangent(cell.Position,new V(1,0,0));V north=V.Cross(cell.Position,east).Unit;
                for(int k=0;k<count;k++)
                {
                    double theta=rng.Unit()*2*Math.PI,offset=inner*.6*Math.Sqrt(rng.Unit());
                    V center=(cell.Position+(east*Math.Cos(theta)+north*Math.Sin(theta))*(offset/radius)).Unit;
                    // Wide size distribution: many cays, occasional substantial islands.
                    double area=Math.Exp(Math.Log(50000)+rng.Unit()*Math.Log((volcanic?800000000.0:180000000.0)/50000));
                    area=Math.Min(area,Math.Min(remaining,Math.PI*Math.Pow((inner-offset)*.55,2)));
                    if(area<10000)continue;
                    var island=Create(islands.Count,i,center,area,1+rng.Unit()*1.6,rng.Unit()*Math.PI,
                        volcanic?80+(int)(rng.Unit()*1100):2+(int)(rng.Unit()*35),w.Recipe.Seed^i*997^k*7919,radius);
                    double extent=island.Outline.Max(p=>D.Angle(p,center))*radius;
                    if(accepted.Any(a=>D.Angle(a.Item1,center)*radius<extent+a.Item2+100))continue;
                    if(island.Outline.Any(p=>cell.Neighbors.Any(j=>V.Dot(p,w.Surface.Cells[j].Position)>V.Dot(p,cell.Position))))continue;
                    island.SourceFeatureId=source[i];island.Origin=volcanic?"Volcanic archipelago":"Shelf island / cay";
                    islands.Add(island);accepted.Add(Tuple.Create(center,extent));remaining-=island.AreaM2;
                }
            }
        }
        public static SmallIsland Create(int id,int cell,V center,double area,double aspect,double orientation,int peak,int seed,double radius)
        {
            V east=V.Tangent(center,new V(0,0,1));if(east.Length<.5)east=V.Tangent(center,new V(1,0,0));V north=V.Cross(center,east).Unit;
            V axis=east*Math.Cos(orientation)+north*Math.Sin(orientation),side=V.Cross(center,axis).Unit;
            const int count=16;var x=new double[count];var y=new double[count];double sum=0;
            double phase=Rng.Value(seed,"island-outline",0)*2*Math.PI;
            for(int k=0;k<count;k++){double a=k*2*Math.PI/count,r=1+.12*Math.Sin(3*a+phase)+.07*Math.Sin(5*a-phase);x[k]=Math.Cos(a)*Math.Sqrt(aspect)*r;y[k]=Math.Sin(a)/Math.Sqrt(aspect)*r;}
            for(int k=0;k<count;k++){int j=(k+1)%count;sum+=x[k]*y[j]-x[j]*y[k];}
            double scale=Math.Sqrt(area/(Math.Abs(sum)*.5));var outline=new V[count];
            for(int k=0;k<count;k++)outline[k]=(center+(axis*x[k]+side*y[k])*(scale/radius)).Unit;
            return new SmallIsland{Id=id,Cell=cell,Center=center,AreaM2=(long)Math.Round(area),PeakM=peak,Outline=outline};
        }
    }
}
