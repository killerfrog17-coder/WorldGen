using System;
using System.Collections.Generic;

namespace WorldGen.Presentation
{
    public static class TerrainEdges
    {
        // Unity's LINQ SequenceEqual enumerates arrays through interfaces and
        // allocates two enumerators on each resident-tile check. Keep precisely
        // Double.Equals semantics (including NaN), without per-frame garbage.
        public static bool Equal(double[] a,double[] b)
        {if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(!a[i].Equals(b[i]))return false;return true;}
        struct Side{public double Start,End;public TerrainTileKey Tile;public int Index;public double Step;}
        sealed class Line{public readonly List<Side> Low=new List<Side>(),High=new List<Side>();}
        public static Dictionary<TerrainTileKey,double[]> Build(IReadOnlyDictionary<TerrainTileKey,int> leaves)
        {
            var result=new Dictionary<TerrainTileKey,double[]>(leaves.Count);
            var vertical=new Dictionary<(double,double),Line>();var horizontal=new Dictionary<(double,double),Line>();
            void Add(Dictionary<(double,double),Line> lines,double at,TerrainTileKey tile,double start,double end,int index,bool high,double step)
            {var key=(tile.Scale,at);if(!lines.TryGetValue(key,out var line)){line=new Line();lines.Add(key,line);}(high?line.High:line.Low).Add(new Side{Start=start,End=end,Tile=tile,Index=index,Step=step});}
            foreach(var pair in leaves)
            {
                var k=pair.Key;result.Add(k,new double[4]);double size=k.SizeM,x=k.X*size,z=k.Z*size,step=size/pair.Value;
                Add(vertical,x,k,z,z+size,0,false,step);Add(vertical,x+size,k,z,z+size,1,true,step);
                Add(horizontal,z,k,x,x+size,2,false,step);Add(horizontal,z+size,k,x,x+size,3,true,step);
            }
            void Match(Dictionary<(double,double),Line> lines)
            {
                foreach(var line in lines.Values)
                {
                    line.High.Sort((a,b)=>a.Start.CompareTo(b.Start));
                    foreach(var a in line.Low)foreach(var b in line.High)
                    {
                        if(b.Start>=a.End)break;
                        if(Math.Max(a.Start,b.Start)<Math.Min(a.End,b.End))
                        {result[a.Tile][a.Index]=Math.Max(result[a.Tile][a.Index],b.Step);result[b.Tile][b.Index]=Math.Max(result[b.Tile][b.Index],a.Step);}
                    }
                }
            }
            Match(vertical);Match(horizontal);return result;
        }
    }
}
