using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>World-addressed hierarchy over the SAME candidate lattice as
    /// LocalScatter. Level zero is an exact candidate census, not a second
    /// planting rule; physical root admission is a separate near-field query.</summary>
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
        // Expected potential trees in a coarse packet, not a tracked population.
        public double CanopyTreeCount;
        public int Population;
        // A fixed 8x8 candidate block. Corner probabilities are shared by the
        // CPU interaction predicate and GPU expansion, not estimated trees.
        public float P00,P10,P01,P11;
        public ulong AllowedMask;
    }
    public sealed class ForestPage
    {
        public ForestPageKey Key;
        public ForestRepresentative[] Roots=Array.Empty<ForestRepresentative>();
        public int Probes;
    }
    public sealed partial class LocalScatter
    {
        public const int ForestPageSide=128;
        public const int ForestPlantingSide=8;
        public const int ForestStandTreeCount=64;
        // One coarse anchor expands its prepared tree billboards on the GPU;
        // it does not retain a duplicate hidden fallback record.
        public const int MaximumForestRepresentationsPerAnchor=1;
        public const int MaximumForestRoots=1048576;
        public const double ForestIdentityDistanceM=2000;
        public int TreeLatticeCount=>checked(Divisions(false)*TreeCandidateSide);
        public double TreeLatticeStep=>2*terrain.RadiusM*scale/TreeLatticeCount;
        public int ForestCanopyMaximumLevel=>Math.Max(1,(int)Math.Floor(Math.Log(2048/(ForestPageSide*TreeLatticeStep),2)));
        public static bool ForestCanopyUsesStand(in ForestRepresentative root)=>root.Population<0||root.Population==0&&root.CanopyTreeCount>=ForestStandTreeCount;
        public static int ForestCanopyAnchorCount(in ForestRepresentative root)
            =>root.Population!=0?1:checked((int)Math.Ceiling(root.CanopyTreeCount/(ForestCanopyUsesStand(root)?ForestStandTreeCount:1)));
        public void ForestCoordinates(V position,out int face,out int x,out int y)
        {
            face=Address(position).Face;Basis(face,out V n,out V u,out V v);
            double dot=V.Dot(position,n);int count=TreeLatticeCount;
            x=Math.Max(0,Math.Min(count-1,(int)Math.Floor((V.Dot(position,u)/dot+1)*.5*count)));
            y=Math.Max(0,Math.Min(count-1,(int)Math.Floor((V.Dot(position,v)/dot+1)*.5*count)));
        }
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
        public ForestPageKey[] PlanForest(V eye,double range,double exactRange=1600,int maximumPages=4096,double innerRange=0,double altitudeM=0)
        {
            if(range<0||range>500000||double.IsNaN(range)||maximumPages<16)throw new ArgumentOutOfRangeException();
            // Pages only schedule work. They never select another population or
            // coarser planting rule. A GPU block represents the same 64 cells
            // whether the player is standing in it or flying kilometres away.
            int top=0;while((long)ForestPageSide*(1L<<top)<TreeLatticeCount)top++;
            var result=new List<ForestPageKey>();
            void Visit(ForestPageKey key)
            {
                long size=(long)ForestPageSide*(1L<<key.Level);
                if(key.X*size>=TreeLatticeCount||key.Y*size>=TreeLatticeCount)return;
                ForestBounds(key,out V center,out double radius);
                double groundDistance=Math.Max(0,(center-eye).Length*terrain.RadiusM*scale-radius);
                if(groundDistance*groundDistance+altitudeM*altitudeM>range*range)return;
                if(key.Level==0){result.Add(key);return;}
                for(int y=0;y<2;y++)for(int x=0;x<2;x++)Visit(new ForestPageKey(key.Face,key.X*2+x,key.Y*2+y,key.Level-1));
            }
            for(int face=0;face<6;face++)Visit(new ForestPageKey(face,0,0,top));
            result.Sort((a,b)=>{ForestBounds(a,out V ac,out double ar);ForestBounds(b,out V bc,out double br);return ((ac-eye).Length*terrain.RadiusM*scale-ar).CompareTo((bc-eye).Length*terrain.RadiusM*scale-br);});
            return result.ToArray();
        }
        public ForestPage BuildForest(ForestPageKey key,CancellationToken token=default)
        {
            if(key.Face<0||key.Face>5||key.X<0||key.Y<0||key.Level!=0)throw new ArgumentOutOfRangeException(nameof(key));
            int left=key.X*ForestPageSide,bottom=key.Y*ForestPageSide,count=TreeLatticeCount,probes=0;
            if(left>=count||bottom>=count)throw new ArgumentOutOfRangeException(nameof(key));
            var roots=new List<ForestRepresentative>();
            var center=ForestPosition(key.Face,left+ForestPageSide*.5,bottom+ForestPageSide*.5);
            var patch=terrain.Patch(center,840/scale);
            if(patch.MaximumForestCoverage>0)
            for(int y=bottom;y<bottom+ForestPageSide&&y<count;y+=ForestPlantingSide)
            for(int x=left;x<left+ForestPageSide&&x<count;x+=ForestPlantingSide)
            {
                token.ThrowIfCancellationRequested();probes++;
                var block=PlantingBlock(key.Face,x,y,patch);
                if(block==null||block.Mask==0||Math.Max(Math.Max(block.P00,block.P10),Math.Max(block.P01,block.P11))<=0)continue;
                var tree=block.Tree;
                roots.Add(new ForestRepresentative{Tree=tree,CellX=x,CellY=y,Population=-ForestStandTreeCount,
                    CanopyTreeCount=ForestStandTreeCount,CrownScale=1,P00=block.P00,P10=block.P10,P01=block.P01,P11=block.P11,AllowedMask=block.Mask});
            }
            return new ForestPage{Key=key,Roots=roots.ToArray(),Probes=probes};
        }
    }
}
