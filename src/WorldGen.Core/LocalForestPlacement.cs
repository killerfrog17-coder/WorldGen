using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>World-addressed hierarchy over the SAME candidate lattice as
    /// LocalScatter. Level zero is an exact census, not a second planting rule.</summary>
    public readonly struct ForestPageKey : IEquatable<ForestPageKey>
    {
        public readonly int Face,X,Y,Level;
        public ForestPageKey(int face,int x,int y,int level){Face=face;X=x;Y=y;Level=level;}
        public bool Equals(ForestPageKey b)=>Face==b.Face&&X==b.X&&Y==b.Y&&Level==b.Level;
        public override bool Equals(object o)=>o is ForestPageKey b&&Equals(b);
        public override int GetHashCode()=>unchecked(Face*83492791^X*73856093^Y*19349663^Level*15485863);
        public ForestPageKey Parent=>new ForestPageKey(Face,X>>1,Y>>1,Level+1);
        public override string ToString()=>$"{Face}/{X}/{Y}/L{Level}";
    }
    public struct ForestRepresentative
    {
        public ScatterInstance Tree;
        public int CellX,CellY;
        public double CrownScale;
        public int Population;
    }
    public sealed class ForestPage
    {
        public ForestPageKey Key;
        public ForestRepresentative[] Roots=Array.Empty<ForestRepresentative>();
        public int Probes;
    }
    public sealed partial class LocalScatter
    {
        public const int ForestPageSide=32;
        public int TreeLatticeCount=>checked(Divisions(false)*TreeCandidateSide);
        public double TreeLatticeStep=>2*terrain.RadiusM*scale/TreeLatticeCount;
        public V ForestPosition(int face,double x,double y)
        {Basis(face,out V n,out V u,out V v);double count=TreeLatticeCount;return (n+u*(x*2/count-1)+v*(y*2/count-1)).Unit;}
        public void ForestBounds(ForestPageKey key,out V center,out double radius)
        {
            double size=(double)ForestPageSide*(1<<key.Level),x=key.X*size,y=key.Y*size;
            double right=Math.Min(TreeLatticeCount,x+size),top=Math.Min(TreeLatticeCount,y+size);
            center=ForestPosition(key.Face,(x+right)*.5,(y+top)*.5);radius=0;
            for(int a=0;a<2;a++)for(int b=0;b<2;b++)radius=Math.Max(radius,(ForestPosition(key.Face,a==0?x:right,b==0?y:top)-center).Length*terrain.RadiusM*scale);
            radius=radius*1.05+100;
        }
        public ForestPageKey[] PlanForest(V eye,double range,double exactRange=1600,int maximumPages=4096,double innerRange=0)
        {
            if(range<0||range>500000||double.IsNaN(range)||maximumPages<16)throw new ArgumentOutOfRangeException();
            int top=0;while((long)ForestPageSide*(1L<<top)<TreeLatticeCount)top++;
            var result=new List<ForestPageKey>();double error=.008;
            for(int attempt=0;attempt<14;attempt++)
            {
                result.Clear();
                void Visit(ForestPageKey key)
                {
                    long size=(long)ForestPageSide*(1L<<key.Level);
                    if(key.X*size>=TreeLatticeCount||key.Y*size>=TreeLatticeCount)return;
                    ForestBounds(key,out V center,out double radius);double distance=Math.Max(0,(center-eye).Length*terrain.RadiusM*scale-radius);
                    if(distance>range||(center-eye).Length*terrain.RadiusM*scale+radius<innerRange)return;
                    double step=TreeLatticeStep*(1<<key.Level);
                    if(key.Level>0&&(distance<exactRange||step>Math.Max(TreeLatticeStep,distance*error)))
                    {for(int y=0;y<2;y++)for(int x=0;x<2;x++)Visit(new ForestPageKey(key.Face,key.X*2+x,key.Y*2+y,key.Level-1));}
                    else result.Add(key);
                }
                for(int face=0;face<6;face++)Visit(new ForestPageKey(face,0,0,top));
                // Budget the expected LIVE representation, not just page count.
                // Dense forests otherwise exhaust the root pool before reaching
                // the horizon. Leave capacity for ready fallback/replacements.
                double expected=0;
                foreach(var key in result)
                {
                    ForestBounds(key,out V center,out double radius);double support=terrain.Profile(terrain.NearestCell(center)).Forest.Coverage;
                    Basis(key.Face,out V normal,out V a,out V b);double dot=V.Dot(center,normal),u=V.Dot(center,a)/dot,v=V.Dot(center,b)/dot;
                    double area=TreeLatticeStep*TreeLatticeStep/Math.Pow(1+u*u+v*v,1.5);
                    double single=D.Clamp(area*TreeFlora.PlacementPerM2)*D.Clamp(support*TreeFlora.Stand(center,seed,terrain.RadiusM*scale,support));
                    double probability=1-Math.Pow(1-single,(1L<<key.Level)*(1L<<key.Level));
                    expected+=ForestPageSide*ForestPageSide*probability;
                }
                if(result.Count<=maximumPages&&expected<=380000)break;error*=1.22;
            }
            result.Sort((a,b)=>{ForestBounds(a,out V ac,out double ar);ForestBounds(b,out V bc,out double br);return ((ac-eye).Length*terrain.RadiusM*scale-ar).CompareTo((bc-eye).Length*terrain.RadiusM*scale-br);});
            return result.ToArray();
        }
        public ForestPage BuildForest(ForestPageKey key,CancellationToken token=default)
        {
            if(key.Face<0||key.Face>5||key.X<0||key.Y<0||key.Level<0||key.Level>15)throw new ArgumentOutOfRangeException(nameof(key));
            var result=new List<ForestRepresentative>();int stride=1<<key.Level,span=checked(ForestPageSide*stride),x0=checked(key.X*span),y0=checked(key.Y*span),count=TreeLatticeCount,probes=0;
            if(x0>=count||y0>=count)throw new ArgumentOutOfRangeException(nameof(key));
            var patches=new Dictionary<ScatterKey,LocalTerrain.LocalTerrainPatch>();
            LocalTerrain.LocalTerrainPatch Patch(ScatterKey address)
            {
                if(!patches.TryGetValue(address,out var patch))
                {if(patches.Count>=64)patches.Clear();patch=terrain.Patch(Position(address),840/scale);patches.Add(address,patch);}return patch;
            }
            for(int y=0;y<ForestPageSide&&y0+y*stride<count;y++)for(int x=0;x<ForestPageSide&&x0+x*stride<count;x++)
            {
                token.ThrowIfCancellationRequested();int left=x0+x*stride,bottom=y0+y*stride;
                int nx=Math.Min(stride,count-left),ny=Math.Min(stride,count-bottom),area=checked(nx*ny);
                // Exact census through 4x4; sparse nodes stay exact through 16x16.
                var middle=new ScatterKey(key.Face,Math.Min(Divisions(false)-1,(left+nx/2)/TreeCandidateSide),Math.Min(Divisions(false)-1,(bottom+ny/2)/TreeCandidateSide));
                double support=Patch(middle).ForestPotential(ForestPosition(key.Face,left+nx*.5,bottom+ny*.5));
                int budget=area<=16||support<.08&&area<=256?area:Math.Min(support>=.15?8:32,area);
                int admitted=0,minX=int.MaxValue,minY=int.MaxValue,maxX=int.MinValue,maxY=int.MinValue;ScatterInstance representative=default;double best=double.PositiveInfinity;
                // Odd multiplier gives a permutation of power-of-two squares;
                // clipped cube edges use a coprime stride instead (no duplicates).
                ulong hash=Rng.Mix(unchecked((uint)left*73856093u^(uint)bottom*19349663u^(uint)key.Face*83492791u));
                int offset=(int)(hash%(uint)area),increment=(int)((hash>>8)%(uint)area)|1;
                int Gcd(int a,int b){while(b!=0){int t=a%b;a=b;b=t;}return a;}
                while(Gcd(increment,area)!=1)increment+=2;
                for(int i=0;i<budget;i++)
                {
                    int at=(int)(((long)offset+(long)i*increment)%area),gx=left+at%nx,gy=bottom+at/nx;
                    var address=new ScatterKey(key.Face,gx/TreeCandidateSide,gy/TreeCandidateSide);
                    probes++;if(!TryTree(address,gx%TreeCandidateSide,gy%TreeCandidateSide,Patch(address),out var tree))continue;
                    admitted++;minX=Math.Min(minX,gx);maxX=Math.Max(maxX,gx);minY=Math.Min(minY,gy);maxY=Math.Max(maxY,gy);
                    uint rank=Rng.Mix((uint)tree.Id^(uint)(tree.Id>>32));
                    // Projected crown area, not stem count: distant canopy must
                    // not collapse to the numerous small understory individuals.
                    double priority=(rank+.5)/Math.Max(1,tree.WidthM*tree.HeightM);
                    if(priority<best){representative=tree;best=priority;}
                }
                if(admitted==0)continue;
                int population=Math.Max(admitted,(int)Math.Round(admitted*(double)area/budget));
                // Only small projected nodes aggregate. Keep the chosen REAL
                // root/height/yaw; widen crown coverage, never invent a new root.
                double spread=TreeLatticeStep*Math.Max(maxX-minX+1,maxY-minY+1);
                double crown=Math.Max(1,Math.Min(Math.Sqrt(population),Math.Min(spread,TreeLatticeStep*stride*.95)/Math.Max(1,representative.WidthM)));
                result.Add(new ForestRepresentative{Tree=representative,CellX=left,CellY=bottom,CrownScale=crown,Population=population});
            }
            return new ForestPage{Key=key,Roots=result.ToArray(),Probes=probes};
        }
    }
}
