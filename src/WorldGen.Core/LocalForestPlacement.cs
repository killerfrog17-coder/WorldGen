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
        public const int ForestCanopyTrees=16;
        public const double ForestIdentityDistanceM=2000;
        public int TreeLatticeCount=>checked(Divisions(false)*TreeCandidateSide);
        public double TreeLatticeStep=>2*terrain.RadiusM*scale/TreeLatticeCount;
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
            // A recognisable tree is the same candidate at every representation.
            // Statistical regional canopy belongs beyond this band, never in
            // place of the forest through which the player is approaching.
            exactRange=Math.Max(exactRange,Math.Min(range,ForestIdentityDistanceM));
            int top=0;while((long)ForestPageSide*(1L<<top)<TreeLatticeCount)top++;
            var result=new List<ForestPageKey>();double error=.04;
            for(int attempt=0;attempt<14;attempt++)
            {
                result.Clear();
                void Visit(ForestPageKey key)
                {
                    long size=(long)ForestPageSide*(1L<<key.Level);
                    if(key.X*size>=TreeLatticeCount||key.Y*size>=TreeLatticeCount)return;
                    ForestBounds(key,out V center,out double radius);double groundDistance=Math.Max(0,(center-eye).Length*terrain.RadiusM*scale-radius),distance=Math.Sqrt(groundDistance*groundDistance+altitudeM*altitudeM);
                    if(distance>range||(center-eye).Length*terrain.RadiusM*scale+radius<innerRange)return;
                    double step=4*TreeLatticeStep*(1<<key.Level);
                    if(key.Level>0&&(distance<exactRange||key.Level>1&&step>Math.Max(TreeLatticeStep,distance*error)))
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
                    if(key.Level>0){expected+=64*ForestCanopyTrees;continue;}
                    ForestBounds(key,out V center,out double radius);var forest=terrain.Profile(terrain.NearestCell(center)).Forest;double support=forest.Coverage;
                    Basis(key.Face,out V normal,out V a,out V b);double dot=V.Dot(center,normal),u=V.Dot(center,a)/dot,v=V.Dot(center,b)/dot;
                    double area=TreeLatticeStep*TreeLatticeStep/Math.Pow(1+u*u+v*v,1.5);
                    double single=D.Clamp(area*TreeFlora.PlacementPerM2)*D.Clamp(support*TreeFlora.Stocking(forest)*TreeFlora.Stand(forest,center,seed,terrain.RadiusM*scale));
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
            var result=new List<ForestRepresentative>();int span=checked(ForestPageSide*(1<<key.Level)),x0=checked(key.X*span),y0=checked(key.Y*span),count=TreeLatticeCount,probes=0;
            if(x0>=count||y0>=count)throw new ArgumentOutOfRangeException(nameof(key));
            if(key.Level>0)return BuildCanopy(key,x0,y0,span,count,token);
            var patches=new Dictionary<ScatterKey,LocalTerrain.LocalTerrainPatch>();
            LocalTerrain.LocalTerrainPatch Patch(ScatterKey address)
            {
                if(!patches.TryGetValue(address,out var patch))
                {if(patches.Count>=64)patches.Clear();patch=terrain.Patch(Position(address),840/scale);patches.Add(address,patch);}return patch;
            }
            for(int y=0;y<ForestPageSide&&y0+y<count;y++)for(int x=0;x<ForestPageSide&&x0+x<count;x++)
            {
                token.ThrowIfCancellationRequested();int gx=x0+x,gy=y0+y;
                var address=new ScatterKey(key.Face,gx/TreeCandidateSide,gy/TreeCandidateSide);
                var patch=Patch(address);if(patch.MaximumForestCoverage<=0)continue;
                probes++;if(!TryTree(address,gx%TreeCandidateSide,gy%TreeCandidateSide,patch,out var tree))continue;
                result.Add(new ForestRepresentative{Tree=tree,CellX=gx,CellY=gy,CrownScale=1,Population=1});
            }
            return new ForestPage{Key=key,Roots=result.ToArray(),Probes=probes};
        }
        // A distant canopy is a regional coverage descriptor, NOT a sampled
        // population of real trees. No terrain, soil, slope or TryTree census.
        // The renderer drapes these cheap GPU patches over resident ground.
        ForestPage BuildCanopy(ForestPageKey key,int x0,int y0,int span,int count,CancellationToken token)
        {
            var roots=new List<ForestRepresentative>(64);double cell=span/8.0;
            for(int y=0;y<8;y++)for(int x=0;x<8;x++)
            {
                token.ThrowIfCancellationRequested();
                int left=(int)(x0+x*cell),bottom=(int)(y0+y*cell);if(left>=count||bottom>=count)continue;
                double nx=Math.Min(cell,count-left),ny=Math.Min(cell,count-bottom);
                uint hash=Rng.Mix(unchecked((uint)left*73856093u^(uint)bottom*19349663u^(uint)key.Face*83492791u));
                double jx=.2+.6*((hash&65535)/65535.0),jy=.2+.6*((hash>>16)/65535.0);
                int gx=(int)(left+nx*jx),gy=(int)(bottom+ny*jy);var p=ForestPosition(key.Face,gx,gy);
                var region=terrain.Profile(terrain.NearestCell(p));var forest=region.Forest;
                if(forest.Coverage<=.005||region.Biome==Biome.Ocean||region.Biome==Biome.Freshwater||region.Biome==Biome.SalineLake)continue;
                double cover=D.Clamp(forest.Coverage*TreeFlora.Stand(forest,p,seed,terrain.RadiusM*scale));
                if(cover<=.005)continue;
                Basis(key.Face,out V normal,out V u,out V v);double dot=V.Dot(p,normal),a=V.Dot(p,u)/dot,b=V.Dot(p,v)/dot;
                double spacing=TreeLatticeStep*Math.Min(nx,ny)/Math.Pow(1+a*a+b*b,.75);
                var address=new ScatterKey(key.Face,gx/TreeCandidateSide,gy/TreeCandidateSide);ulong id=address.Id(0,gy%TreeCandidateSide*TreeCandidateSide+gx%TreeCandidateSide);
                var tree=new ScatterInstance{Id=id,Kind=ScatterKind.GeneratedTree,Variant=forest.MorphologyCode,Position=p,Normal=p,
                    GroundM=region.ElevationM,HeightM=Math.Max(1,forest.HeightM),WidthM=Math.Max(.5,forest.WidthM),
                    Color=forest.Color*(.90+.20*Rng.Value(seed,"regional-canopy-colour",unchecked((int)id))),YawDegrees=Rng.Value(seed,"regional-canopy-yaw",unchecked((int)id))*360};
                // Coverage is an area, not the width of one tree. Keep the
                // physical crown dimensions and transport the packet footprint
                // separately; stretching a single tree across this span made
                // kilometre-scale leaves and flattened horizon cutouts.
                roots.Add(new ForestRepresentative{Tree=tree,CellX=left,CellY=bottom,CrownScale=Math.Max(2,spacing*Math.Sqrt(cover)*1.12),Population=0});
            }
            return new ForestPage{Key=key,Roots=roots.ToArray(),Probes=0};
        }
    }
}
