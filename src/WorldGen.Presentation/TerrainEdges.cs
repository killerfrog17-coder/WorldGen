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
        struct Side{public long Start,End;public TerrainTileKey Tile;public int Index;public double Step;}
        struct Span{public int Edge;public double Start,End,Step;}
        // Four legacy maxima, optionally followed by edge/start/end/spacing
        // records for edges whose neighbours do not share one sampling grid.
        // Intervals are local metres. Keep uniform-edge requests compact.
        static double[] Pack(TerrainTileKey tile,List<Span> spans)
        {
            var data=new List<double>{0,0,0,0};
            for(int edge=0;edge<4;edge++)
            {
                var parts=spans.FindAll(s=>s.Edge==edge);if(parts.Count==0)continue;
                parts.Sort((a,b)=>a.Start.CompareTo(b.Start));var joined=new List<Span>();
                foreach(var part in parts)
                {
                    data[edge]=Math.Max(data[edge],part.Step);
                    if(joined.Count>0&&joined[joined.Count-1].Step==part.Step&&Math.Abs(joined[joined.Count-1].End-part.Start)<tile.SizeM*1e-10)
                    {var last=joined[joined.Count-1];last.End=part.End;joined[joined.Count-1]=last;}
                    else joined.Add(part);
                }
                if(joined.Count==1&&Math.Abs(joined[0].Start)<tile.SizeM*1e-10&&Math.Abs(joined[0].End-tile.SizeM)<tile.SizeM*1e-10)continue;
                foreach(var part in joined){data.Add(edge);data.Add(part.Start);data.Add(part.End);data.Add(part.Step);}
            }
            return data.ToArray();
        }
        public static double At(double[] edges,int edge,double localMetres)
        {
            bool sliced=false;double spacing=0;
            for(int i=4;i<edges.Length;i+=4)if((int)edges[i]==edge)
            {sliced=true;double epsilon=Math.Max(1e-8,Math.Abs(edges[i+2])*1e-10);if(localMetres>=edges[i+1]-epsilon&&localMetres<=edges[i+2]+epsilon)spacing=Math.Max(spacing,edges[i+3]);}
            return sliced?spacing:edges[edge];
        }
        public static void Validate(double[] edges)
        {
            if(edges.Length<4||(edges.Length-4)%4!=0||edges.Length>16388)throw new ArgumentException("Invalid terrain edge intervals.");
            foreach(double value in edges)if(!double.IsFinite(value)||value<0)throw new ArgumentException("Invalid terrain edge interval value.");
            for(int i=4;i<edges.Length;i+=4)if(edges[i]>3||edges[i]!=(int)edges[i]||edges[i+2]<edges[i+1])throw new ArgumentException("Invalid terrain edge interval bounds.");
        }
        public static double[] For(TerrainTileKey tile,IReadOnlyDictionary<TerrainTileKey,int> leaves)
        {
            var spans=new List<Span>();long x=(long)tile.X<<tile.Level,z=(long)tile.Z<<tile.Level,size=1L<<tile.Level;double unit=TerrainLod.BaseSizeM*tile.Scale;
            foreach(var pair in leaves)
            {
                var k=pair.Key;if(k.Scale!=tile.Scale)continue;long a=(long)k.X<<k.Level,b=(long)k.Z<<k.Level,other=1L<<k.Level;double step=k.SizeM/pair.Value;
                long lo=Math.Max(z,b),hi=Math.Min(z+size,b+other);
                if(lo<hi){if(a+other==x)spans.Add(new Span{Edge=0,Start=(lo-z)*unit,End=(hi-z)*unit,Step=step});if(a==x+size)spans.Add(new Span{Edge=1,Start=(lo-z)*unit,End=(hi-z)*unit,Step=step});}
                lo=Math.Max(x,a);hi=Math.Min(x+size,a+other);
                if(lo<hi){if(b+other==z)spans.Add(new Span{Edge=2,Start=(lo-x)*unit,End=(hi-x)*unit,Step=step});if(b==z+size)spans.Add(new Span{Edge=3,Start=(lo-x)*unit,End=(hi-x)*unit,Step=step});}
            }
            return Pack(tile,spans);
        }
        sealed class Line{public readonly List<Side> Low=new List<Side>(),High=new List<Side>();}
        public static Dictionary<TerrainTileKey,double[]> Build(IReadOnlyDictionary<TerrainTileKey,int> leaves)
        {
            var result=new Dictionary<TerrainTileKey,double[]>(leaves.Count);
            var spans=new Dictionary<TerrainTileKey,List<Span>>(leaves.Count);
            var vertical=new Dictionary<(double,long),Line>();var horizontal=new Dictionary<(double,long),Line>();
            void Add(Dictionary<(double,long),Line> lines,long at,TerrainTileKey tile,long start,long end,int index,bool high,double step)
            {var key=(tile.Scale,at);if(!lines.TryGetValue(key,out var line)){line=new Line();lines.Add(key,line);}(high?line.High:line.Low).Add(new Side{Start=start,End=end,Tile=tile,Index=index,Step=step});}
            foreach(var pair in leaves)
            {
                var k=pair.Key;spans.Add(k,new List<Span>());long size=1L<<k.Level,x=(long)k.X<<k.Level,z=(long)k.Z<<k.Level;double step=k.SizeM/pair.Value;
                Add(vertical,x,k,z,z+size,0,false,step);Add(vertical,x+size,k,z,z+size,1,true,step);
                Add(horizontal,z,k,x,x+size,2,false,step);Add(horizontal,z+size,k,x,x+size,3,true,step);
            }
            void Match(Dictionary<(double,long),Line> lines)
            {
                foreach(var line in lines.Values)
                {
                    line.High.Sort((a,b)=>a.Start.CompareTo(b.Start));
                    foreach(var a in line.Low)foreach(var b in line.High)
                    {
                        if(b.Start>=a.End)break;
                        if(Math.Max(a.Start,b.Start)<Math.Min(a.End,b.End))
                        {
                            long lo=Math.Max(a.Start,b.Start),hi=Math.Min(a.End,b.End);double unit=TerrainLod.BaseSizeM*a.Tile.Scale;
                            spans[a.Tile].Add(new Span{Edge=a.Index,Start=(lo-a.Start)*unit,End=(hi-a.Start)*unit,Step=b.Step});
                            spans[b.Tile].Add(new Span{Edge=b.Index,Start=(lo-b.Start)*unit,End=(hi-b.Start)*unit,Step=a.Step});
                        }
                    }
                }
            }
            Match(vertical);Match(horizontal);foreach(var pair in spans)result.Add(pair.Key,Pack(pair.Key,pair.Value));return result;
        }
    }
}
