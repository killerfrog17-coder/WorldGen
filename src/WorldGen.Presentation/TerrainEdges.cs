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
        sealed class PackingScratch
        {public readonly List<double> Data=new List<double>();public readonly List<Span> Parts=new List<Span>(),Joined=new List<Span>(),Points=new List<Span>();public readonly HashSet<(int,long)> Seen=new HashSet<(int,long)>();}
        static double[] Pack(TerrainTileKey tile,List<Span> spans,PackingScratch scratch,double step,Dictionary<(double,long,long),double> corners)
        {
            var data=scratch.Data;data.Clear();for(int i=0;i<4;i++)data.Add(0);
            for(int edge=0;edge<4;edge++)
            {
                var parts=scratch.Parts;parts.Clear();foreach(var span in spans)if(span.Edge==edge)parts.Add(span);if(parts.Count==0)continue;
                parts.Sort((a,b)=>a.Start.CompareTo(b.Start));var joined=scratch.Joined;joined.Clear();
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
            // Zero-length records carry the maximum incident sampling radius
            // at corners/T-junctions. They never change geometric edge grids.
            var points=scratch.Points;points.Clear();scratch.Seen.Clear();
            long ox=(long)tile.X<<tile.Level,oz=(long)tile.Z<<tile.Level,size=1L<<tile.Level;double unit=TerrainLod.BaseSizeM*tile.Scale;
            void Point(int edge,long along)
            {
                if(!scratch.Seen.Add((edge,along)))return;
                long x=edge<2?ox+(edge==1?size:0):ox+along,z=edge<2?oz+along:oz+(edge==3?size:0);
                if(corners.TryGetValue((tile.Scale,x,z),out double radius)&&radius>step*1.001)
                    points.Add(new Span{Edge=edge,Start=along*unit,End=along*unit,Step=radius});
            }
            Point(0,0);Point(0,size);Point(1,0);Point(1,size);
            foreach(var span in spans){Point(span.Edge,(long)Math.Round(span.Start/unit));Point(span.Edge,(long)Math.Round(span.End/unit));}
            points.Sort((a,b)=>a.Edge!=b.Edge?a.Edge.CompareTo(b.Edge):a.Start.CompareTo(b.Start));
            foreach(var point in points){data.Add(point.Edge);data.Add(point.Start);data.Add(point.End);data.Add(point.Step);}
            return data.ToArray();
        }
        public static double At(double[] edges,int edge,double localMetres)
        {
            bool sliced=false;double spacing=0;
            for(int i=4;i<edges.Length;i+=4)if((int)edges[i]==edge&&edges[i+1]!=edges[i+2])
            {sliced=true;double epsilon=Math.Max(1e-8,Math.Abs(edges[i+2])*1e-10);if(localMetres>=edges[i+1]-epsilon&&localMetres<=edges[i+2]+epsilon)spacing=Math.Max(spacing,edges[i+3]);}
            return sliced?spacing:edges[edge];
        }
        public static double SurfaceAt(double[] edges,int edge,double localMetres)
        {
            bool sliced=false;double spacing=0,corner=double.NegativeInfinity;
            for(int i=4;i<edges.Length;i+=4)
            {
                if((int)edges[i]!=edge)continue;
                double start=edges[i+1],end=edges[i+2];
                if(start!=end)
                {
                    sliced=true;double epsilon=Math.Max(1e-8,Math.Abs(end)*1e-10);
                    if(localMetres>=start-epsilon&&localMetres<=end+epsilon)spacing=Math.Max(spacing,edges[i+3]);
                }
                else if(Math.Abs(localMetres-start)<=Math.Max(1e-8,Math.Abs(start)*1e-10))corner=Math.Max(corner,edges[i+3]);
            }
            return Math.Max(sliced?spacing:edges[edge],corner);
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
            var corners=new Dictionary<(double,long,long),double>();
            long tx=(long)tile.X<<tile.Level,tz=(long)tile.Z<<tile.Level,ts=1L<<tile.Level;
            void Corner(long xx,long zz)
            {
                double radius=0;foreach(var pair in leaves){var k=pair.Key;if(k.Scale!=tile.Scale)continue;long size=1L<<k.Level,ax=(long)k.X<<k.Level,az=(long)k.Z<<k.Level;
                    if(xx>=ax&&xx<=ax+size&&zz>=az&&zz<=az+size)radius=Math.Max(radius,k.SizeM/pair.Value);}
                corners[(tile.Scale,xx,zz)]=radius;
            }
            Corner(tx,tz);Corner(tx+ts,tz);Corner(tx,tz+ts);Corner(tx+ts,tz+ts);
            foreach(var span in spans)foreach(double offset in new[]{span.Start,span.End})
            {long along=(long)Math.Round(offset/unit);Corner(span.Edge<2?tx+(span.Edge==1?ts:0):tx+along,span.Edge<2?tz+along:tz+(span.Edge==3?ts:0));}
            return Pack(tile,spans,new PackingScratch(),tile.SizeM/leaves[tile],corners);
        }
        sealed class Line{public readonly List<Side> Low=new List<Side>(),High=new List<Side>();public readonly List<long> HighPrefixEnd=new List<long>();}
        public static Dictionary<TerrainTileKey,double[]> Build(IReadOnlyDictionary<TerrainTileKey,int> leaves)=>new Workspace().Build(leaves);

        // One serial caller owns a workspace. Only intermediate neighbour lists
        // are reused; returned edge arrays/dictionaries remain independent.
        public sealed class Workspace
        {
            readonly Dictionary<TerrainTileKey,List<Span>> spans=new Dictionary<TerrainTileKey,List<Span>>();
            readonly Dictionary<(double,long),Line> vertical=new Dictionary<(double,long),Line>(),horizontal=new Dictionary<(double,long),Line>();
            readonly List<Line> lines=new List<Line>();readonly List<List<Span>> spanLists=new List<List<Span>>();
            readonly PackingScratch packing=new PackingScratch();int lineIndex,spanIndex;
            readonly Dictionary<(double,long,long),double> corners=new Dictionary<(double,long,long),double>();
            void Corner(double scale,long x,long z,double step)
            {var key=(scale,x,z);if(!corners.TryGetValue(key,out double prior)||step>prior)corners[key]=step;}
            public Dictionary<TerrainTileKey,double[]> Build(IReadOnlyDictionary<TerrainTileKey,int> leaves)
            {
                // Exceptional giant plans must not permanently enlarge a normal
                // explorer's scratch retention. Their output remains unchanged.
                return leaves.Count>4096?new Workspace().BuildCore(leaves):BuildCore(leaves);
            }
            Line TakeLine()
            {
                if(lineIndex==lines.Count)lines.Add(new Line());var line=lines[lineIndex++];line.Low.Clear();line.High.Clear();return line;
            }
            List<Span> TakeSpans()
            {
                if(spanIndex==spanLists.Count)spanLists.Add(new List<Span>());var result=spanLists[spanIndex++];result.Clear();return result;
            }
            void Add(Dictionary<(double,long),Line> target,long at,TerrainTileKey tile,long start,long end,int index,bool high,double step)
            {
                var key=(tile.Scale,at);if(!target.TryGetValue(key,out var line)){line=TakeLine();target.Add(key,line);}
                (high?line.High:line.Low).Add(new Side{Start=start,End=end,Tile=tile,Index=index,Step=step});
            }
            void Match(Dictionary<(double,long),Line> target,bool verticalEdge)
            {
                foreach(var entry in target)
                {
                    var line=entry.Value;
                    line.High.Sort((a,b)=>a.Start.CompareTo(b.Start));
                    line.HighPrefixEnd.Clear();long maximumEnd=long.MinValue;
                    foreach(var b in line.High){maximumEnd=Math.Max(maximumEnd,b.End);line.HighPrefixEnd.Add(maximumEnd);}
                    foreach(var a in line.Low)
                    {
                        // Skip only a prefix whose every interval ends before
                        // this start. Nested/overlapping intervals retain the
                        // original remaining order and exact intersection test.
                        int first=0,last=line.HighPrefixEnd.Count;
                        while(first<last){int middle=first+(last-first)/2;if(line.HighPrefixEnd[middle]<=a.Start)first=middle+1;else last=middle;}
                        for(int i=first;i<line.High.Count;i++)
                        {
                            var b=line.High[i];if(b.Start>=a.End)break;
                            if(Math.Max(a.Start,b.Start)<Math.Min(a.End,b.End))
                            {
                                long lo=Math.Max(a.Start,b.Start),hi=Math.Min(a.End,b.End);double unit=TerrainLod.BaseSizeM*a.Tile.Scale;
                                spans[a.Tile].Add(new Span{Edge=a.Index,Start=(lo-a.Start)*unit,End=(hi-a.Start)*unit,Step=b.Step});
                                spans[b.Tile].Add(new Span{Edge=b.Index,Start=(lo-b.Start)*unit,End=(hi-b.Start)*unit,Step=a.Step});
                                double radius=Math.Max(a.Step,b.Step);
                                Corner(entry.Key.Item1,verticalEdge?entry.Key.Item2:lo,verticalEdge?lo:entry.Key.Item2,radius);
                                Corner(entry.Key.Item1,verticalEdge?entry.Key.Item2:hi,verticalEdge?hi:entry.Key.Item2,radius);
                            }
                        }
                    }
                }
            }
            Dictionary<TerrainTileKey,double[]> BuildCore(IReadOnlyDictionary<TerrainTileKey,int> leaves)
            {
                lineIndex=spanIndex=0;spans.Clear();vertical.Clear();horizontal.Clear();corners.Clear();
                var result=new Dictionary<TerrainTileKey,double[]>(leaves.Count);
                foreach(var pair in leaves)
                {
                    var k=pair.Key;spans.Add(k,TakeSpans());long size=1L<<k.Level,x=(long)k.X<<k.Level,z=(long)k.Z<<k.Level;double step=k.SizeM/pair.Value;
                    Corner(k.Scale,x,z,step);Corner(k.Scale,x+size,z,step);Corner(k.Scale,x,z+size,step);Corner(k.Scale,x+size,z+size,step);
                    Add(vertical,x,k,z,z+size,0,false,step);Add(vertical,x+size,k,z,z+size,1,true,step);
                    Add(horizontal,z,k,x,x+size,2,false,step);Add(horizontal,z+size,k,x,x+size,3,true,step);
                }
                Match(vertical,true);Match(horizontal,false);foreach(var pair in spans)result.Add(pair.Key,Pack(pair.Key,pair.Value,packing,pair.Key.SizeM/leaves[pair.Key],corners));return result;
            }
        }
    }
}
