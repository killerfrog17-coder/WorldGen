using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    public sealed partial class LocalScatter
    {
        sealed class CachedTrees {public ScatterInstance[] Trees;public long Used;}
        readonly object treeCacheGate=new object();readonly Dictionary<ScatterKey,CachedTrees> treeCache=new Dictionary<ScatterKey,CachedTrees>();long treeCacheClock,treeCacheBytes;
        // Habitat varies over metres/tens of metres, rather than independently
        // for every four-metre candidate. Both local and forest-page planting
        // read these same planet-addressed nodes. Exact root water/height still
        // comes from Geometry; this memo never owns rendering or tree identity.
        const int TreeHabitatStep=8,TreeHabitatLimit=8192;
        sealed class TreeHabitatNodeData
        {
            public LocalTerrainSample Sample;public V Position;
            // One requested regional flora array per node; an ecotone may
            // replace it, but cannot grow an unbounded region/model cache.
            public TreeSpecies[] Species;public double[] Weights;
        }
        readonly struct TreeHabitatBlend
        {
            public readonly TreeHabitatNodeData A,B,C,D;public readonly double U,V;
            public TreeHabitatBlend(TreeHabitatNodeData a,TreeHabitatNodeData b,TreeHabitatNodeData c,TreeHabitatNodeData d,double u,double v){A=a;B=b;C=c;D=d;U=u;V=v;}
        }
        readonly object treeHabitatGate=new object();
        readonly Dictionary<(int Face,int X,int Y),TreeHabitatNodeData> treeHabitat=new Dictionary<(int,int,int),TreeHabitatNodeData>();
        readonly Queue<(int Face,int X,int Y)> treeHabitatOrder=new Queue<(int,int,int)>();
        TreeHabitatNodeData TreeHabitatNode(int face,int x,int y,LocalTerrain.LocalTerrainPatch patch)
        {
            var key=(face,x,y);TreeHabitatNodeData cached;
            lock(treeHabitatGate)if(treeHabitat.TryGetValue(key,out cached))return cached;
            int count=TreeLatticeCount;
            var point=ForestPosition(face,Math.Min(count,x*TreeHabitatStep),Math.Min(count,y*TreeHabitatStep));
            var node=new TreeHabitatNodeData{Sample=patch.Sample(point),Position=point};
            lock(treeHabitatGate)
            {
                if(treeHabitat.TryGetValue(key,out cached))return cached;
                while(treeHabitat.Count>=TreeHabitatLimit)treeHabitat.Remove(treeHabitatOrder.Dequeue());
                treeHabitat.Add(key,node);treeHabitatOrder.Enqueue(key);
            }
            return node;
        }
        double[] TreeSpeciesWeights(TreeHabitatNodeData node,TreeSpecies[] species)
        {
            lock(node)
            {
                if(ReferenceEquals(node.Species,species))return node.Weights;
                var sample=node.Sample;var ecology=world.Ecology;
                HabitatCell? habitat=ecology!=null&&sample.Cell<ecology.Habitat.Length?ecology.Habitat[sample.Cell]:null;
                var weights=new double[species.Length];
                for(int i=0;i<species.Length;i++)
                    weights[i]=TreeFlora.SpeciesWeight(species[i],habitat,sample,node.Position,seed,terrain.RadiusM*scale,world.ElevationM[sample.Cell]);
                node.Species=species;node.Weights=weights;return weights;
            }
        }
        double BlendTreeSpeciesWeights(in TreeHabitatBlend blend,TreeSpecies[] species,Span<double> weights)
        {
            // Species colony wavelengths are hundreds of metres. Evaluate
            // those noise fields at the existing habitat nodes, then reuse
            // their regional weights for neighbouring individual candidates.
            var a=TreeSpeciesWeights(blend.A,species);var b=TreeSpeciesWeights(blend.B,species);
            var c=TreeSpeciesWeights(blend.C,species);var d=TreeSpeciesWeights(blend.D,species);
            double u=blend.U,v=blend.V,sum=0;
            for(int i=0;i<species.Length;i++)
            {weights[i]=(a[i]*(1-u)+b[i]*u)*(1-v)+(c[i]*(1-u)+d[i]*u)*v;sum+=weights[i];}
            return sum;
        }
        LocalTerrainSample TreeHabitatAt(ScatterKey key,V at,LocalTerrain.LocalTerrainPatch patch,LocalTerrainSample geometry,out double sx,out double sz,out bool preciseSlope,out TreeHabitatBlend blend)
        {
            // Jitter belongs to the original candidate RNG; derive the exact
            // fractional lattice coordinate from that point, never a page LOD.
            Basis(key.Face,out V normal,out V a,out V b);double dot=V.Dot(at,normal),count=TreeLatticeCount;
            double gx=(V.Dot(at,a)/dot+1)*.5*count/TreeHabitatStep,gy=(V.Dot(at,b)/dot+1)*.5*count/TreeHabitatStep;
            int ix=(int)Math.Floor(gx),iy=(int)Math.Floor(gy);double u=gx-ix,v=gy-iy;
            var aNode=TreeHabitatNode(key.Face,ix,iy,patch);var bNode=TreeHabitatNode(key.Face,ix+1,iy,patch);
            var cNode=TreeHabitatNode(key.Face,ix,iy+1,patch);var dNode=TreeHabitatNode(key.Face,ix+1,iy+1,patch);
            blend=new TreeHabitatBlend(aNode,bNode,cNode,dNode,u,v);
            var aa=aNode.Sample;var ab=bNode.Sample;var ba=cNode.Sample;var bb=dNode.Sample;
            double side=TreeHabitatStep*TreeLatticeStep;
            double du=((ab.ElevationM-aa.ElevationM)*(1-v)+(bb.ElevationM-ba.ElevationM)*v)/side;
            double dv=((ba.ElevationM-aa.ElevationM)*(1-u)+(bb.ElevationM-ab.ElevationM)*u)/side;
            // Cube-face grid directions are neither unit nor orthogonal away
            // from a face centre. Convert that derivative into the root's
            // physical tangent basis instead of inventing a slope at seams.
            V xu=(a-at*V.Dot(at,a))*dot,yv=(b-at*V.Dot(at,b))*dot;
            V east=V.Cross(Math.Abs(at.Z)<.99?new V(0,0,1):new V(0,1,0),at).Unit,north=V.Cross(at,east).Unit;
            double ax=V.Dot(xu,east),az=V.Dot(xu,north),bx=V.Dot(yv,east),bz=V.Dot(yv,north),det=ax*bz-az*bx;
            sx=(du*bz-dv*az)/det;sz=(dv*ax-du*bx)/det;
            double coarse=(aa.ElevationM*(1-u)+ab.ElevationM*u)*(1-v)+(ba.ElevationM*(1-u)+bb.ElevationM*u)*v;
            preciseSlope=1/Math.Sqrt(1+sx*sx+sz*sz)<.9||Math.Abs(geometry.ElevationM-coarse)>1;
            return LocalTerrain.LocalTerrainPatch.BlendEcology(aa,ab,ba,bb,u,v,geometry);
        }
        bool CachedTree(ScatterKey key,int slot,out ScatterInstance tree,out bool known)
        {
            CachedTrees cached;lock(treeCacheGate){known=treeCache.TryGetValue(key,out cached);if(known)cached.Used=++treeCacheClock;}
            tree=default;if(!known)return false;int low=0,high=cached.Trees.Length-1;
            while(low<=high){int mid=(low+high)>>1,value=(int)(cached.Trees[mid].Id&65535);if(value==slot){tree=cached.Trees[mid];return true;}if(value<slot)low=mid+1;else high=mid-1;}return false;
        }
        void RememberTrees(ScatterKey key,List<ScatterInstance> result,int first)
        {
            var data=result.GetRange(first,result.Count-first).ToArray();long bytes=data.LongLength*128+64;
            lock(treeCacheGate)
            {
                if(treeCache.TryGetValue(key,out var previous))treeCacheBytes-=previous.Trees.LongLength*128+64;
                treeCache[key]=new CachedTrees{Trees=data,Used=++treeCacheClock};treeCacheBytes+=bytes;
                while(treeCache.Count>128||treeCacheBytes>96L*1024*1024)
                {
                    ScatterKey oldest=default;long age=long.MaxValue;foreach(var item in treeCache)if(item.Value.Used<age){age=item.Value.Used;oldest=item.Key;}
                    treeCacheBytes-=treeCache[oldest].Trees.LongLength*128+64;treeCache.Remove(oldest);
                }
            }
        }
        void AddGeneratedTrees(ScatterKey key,LocalTerrain.LocalTerrainPatch patch,List<ScatterInstance> result,CancellationToken token)
        {
            const int side=TreeCandidateSide;int first=result.Count;
            for(int y=0;y<side;y++)for(int x=0;x<side;x++)
            {
                token.ThrowIfCancellationRequested();if(TryTree(key,x,y,patch,out var tree))result.Add(tree);
            }
            RememberTrees(key,result,first);
        }
        public const int TreeCandidateSide=128;
        /// <summary>Bounded sound/perch attachment using the existing planting
        /// predicate/cache, independent of visible tree quality or draw toggles.</summary>
        public bool TryFaunaTree(V point,LocalTerrain.LocalTerrainPatch patch,out ScatterInstance tree)
        {
            var key=Address(point);Basis(key.Face,out V n,out V u,out V v);double d=V.Dot(point,n);int count=Divisions(false);
            int x=(int)Math.Floor(((V.Dot(point,u)/d+1)*.5*count-key.X)*TreeCandidateSide);
            int y=(int)Math.Floor(((V.Dot(point,v)/d+1)*.5*count-key.Y)*TreeCandidateSide);
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
            {
                int xx=x+dx,yy=y+dy;if(xx<0||yy<0||xx>=TreeCandidateSide||yy>=TreeCandidateSide)continue;
                if(TryTree(key,xx,yy,patch,out tree)&&(tree.Position-point).Length*terrain.RadiusM*scale<16)return true;
            }
            tree=default;return false;
        }
        /// <summary>The ONE planting predicate, used by local meshes and distant
        /// forest pages. Never seeds on a render chart, LOD or completion order.</summary>
        public bool TryTree(ScatterKey key,int x,int y,LocalTerrain.LocalTerrainPatch patch,out ScatterInstance tree)
        {
                tree=default;if(key.Detail||x<0||x>=TreeCandidateSide||y<0||y>=TreeCandidateSide)throw new ArgumentOutOfRangeException();
                bool found=CachedTree(key,y*TreeCandidateSide+x,out tree,out bool known);if(known)return found;
                const int side=TreeCandidateSide;int divisions=Divisions(false);double nominal=2*terrain.RadiusM*scale/divisions;
                Span<double> weights=stackalloc double[13];int slot=y*side+x;ulong id=key.Id(0,slot);
                var r=new Rng(seed,"generated-tree-placement-3",unchecked((int)Rng.Mix((uint)id^(uint)(id>>32))));
                double u=(x+.08+.84*r.Unit())/side,v=(y+.08+.84*r.Unit())/side;V at=Position(key,u,v);
                // A blended profile cannot exceed the largest supporting source.
                // Skip vast sparse areas before detailed terrain or species work.
                double admission=r.Unit();if(patch.MaximumForestCoverage<=0||admission>patch.MaximumForestCoverage)return false;
                double cu=(key.X+u)*2/divisions-1,cv=(key.Y+v)*2/divisions-1,jacobian=1/Math.Pow(1+cu*cu+cv*cv,1.5);
                // Cheap upper admission before expensive local surface queries.
                double densityCap=D.Clamp(nominal*nominal*jacobian/(side*side)*TreeFlora.PlacementPerM2);
                if(r.Unit()>densityCap)return false;
                var forest=patch.ForestProfile(at);double potential=forest.Coverage,stand=TreeFlora.Stand(forest,at,seed,terrain.RadiusM*scale);
                if(admission>potential*stand*TreeFlora.Stocking(forest))return false;
                // One exact physical query protects channels, lakes and coasts.
                // Species/soil and smooth slope share a roughly 32m grid rather
                // than five full terrain evaluations for every admitted root.
                var geometry=patch.Geometry(at);
                var sample=TreeHabitatAt(key,at,patch,geometry,out double sx,out double sz,out bool preciseSlope,out var habitatBlend);
                var rootBiome=TreeFlora.TidalCode(sample.Forest.MorphologyCode)?terrain.Profile(sample.Cell).Biome:sample.Biome;
                if(!TreeFlora.Rooting(sample.Forest,sample.ElevationM,sample.Underwater,sample.Underwater?sample.WaterSurfaceM-sample.ElevationM:0,rootBiome))return false;
                // The raw forest profile is only an upper admission bound.
                // Final substrate/wetland exclusions own actual near/far trees.
                if(sample.Forest.Coverage<forest.Coverage-1e-9)
                {
                    forest=sample.Forest;stand=TreeFlora.Stand(forest,at,seed,terrain.RadiusM*scale);
                    if(admission>forest.Coverage*stand*TreeFlora.Stocking(forest))return false;
                }
                var e=world.Ecology;HabitatCell? h=e!=null&&sample.Cell<e.Habitat.Length?e.Habitat[sample.Cell]:null;
                var species=terrain.FloraRegion(sample.Cell).Trees;double sum=BlendTreeSpeciesWeights(habitatBlend,species,weights);
                if(sum<.001)return false;
                double pick=r.Unit()*sum;int chosen=species.Length-1;for(int i=0;i<species.Length;i++){pick-=weights[i];if(pick<=0){chosen=i;break;}}
                var s=species[chosen];V east=V.Cross(Math.Abs(at.Z)<.99?new V(0,0,1):new V(0,1,0),at).Unit,north=V.Cross(at,east).Unit;
                // Retain precise local exposure on steep ground. Broad smooth
                // hills use the shared gradient; cliffs must not gain trees
                // merely because their habitat-grid corners average together.
                if(preciseSlope)
                {
                    double delta=1.5/(terrain.RadiusM*scale);
                    sx=(patch.Elevation((at+east*delta).Unit)-patch.Elevation((at-east*delta).Unit))/3;
                    sz=(patch.Elevation((at+north*delta).Unit)-patch.Elevation((at-north*delta).Unit))/3;
                }
                double up=1/Math.Sqrt(1+sx*sx+sz*sz);
                if(r.Unit()>TreeFlora.SlopeAdmission(up))return false;
                // Static size cohorts, not simulated ages. Saplings keep their adult lineage.
                double cohort=r.Unit(),size=cohort<.12?.18+.16*r.Unit():cohort<.30?.40+.25*r.Unit():.72+.36*r.Unit();
                if(s.Morphology.Strategy==TreeCrownStrategy.TropicalCanopy)
                    size=cohort<.69?.10+.35*r.Unit():cohort>.99?1.28+.28*r.Unit():.82+.25*r.Unit();
                // Shade tolerance matters for seedlings, not a ban on adult
                // pioneer trees remaining in a closed stand. Suppressed young
                // cohorts stay smaller; openings release the same lineage.
                if(size<.65)size*=1-.35*TreeFlora.CanopyShade(forest,at,seed,terrain.RadiusM*scale)*(1-s.ShadeTolerance);
                double localWarm=(h==null?18:h.Value.WarmTenthsC*.1)-.006*(sample.ElevationM-world.ElevationM[sample.Cell]);
                double growth=D.Clamp((localWarm-5)/12);growth=growth*growth*(3-2*growth);
                size*=(.28+.72*growth)*(.65+.35*D.Clamp(sample.Ground.DepthM/.6));
                double height=D.Q(s.HeightM*size,1000);
                if(sample.Underwater&&s.Morphology.Strategy==TreeCrownStrategy.Mangrove)
                {
                    double inundation=TreeFlora.TidalInundation(s.MorphologyCode,height,sample.WaterSurfaceM-sample.ElevationM);
                    if(inundation<=0||r.Unit()>inundation)return false;
                }
                double tint=.93+.12*r.Unit();
                tree=new ScatterInstance{Id=id,Kind=ScatterKind.GeneratedTree,Variant=s.MorphologyCode|(int)(r.Unit()*6),Position=at,Normal=(at-east*sx-north*sz).Unit,
                    GroundM=sample.ElevationM,HeightM=height,WidthM=D.Q(s.WidthM*size*(.88+.24*r.Unit()),1000),
                    YawDegrees=D.Q(r.Unit()*360,1000),Color=new V(tint,tint,tint)};return true;
        }
    }
}
