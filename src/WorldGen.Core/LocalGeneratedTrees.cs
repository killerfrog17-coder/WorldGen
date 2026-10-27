using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    public sealed partial class LocalScatter
    {
        sealed class CachedTrees {public ScatterInstance[] Trees;public long Used;}
        readonly object treeCacheGate=new object();readonly Dictionary<ScatterKey,CachedTrees> treeCache=new Dictionary<ScatterKey,CachedTrees>();long treeCacheClock,treeCacheBytes;
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
                double admission=r.Unit();if(admission>patch.MaximumForestCoverage)return false;
                double cu=(key.X+u)*2/divisions-1,cv=(key.Y+v)*2/divisions-1,jacobian=1/Math.Pow(1+cu*cu+cv*cv,1.5);
                // Cheap upper admission before expensive local surface queries.
                double densityCap=D.Clamp(nominal*nominal*jacobian/(side*side)*TreeFlora.PlacementPerM2);
                if(r.Unit()>densityCap)return false;
                var forest=patch.ForestProfile(at);double potential=forest.Coverage,stand=TreeFlora.Stand(forest,at,seed,terrain.RadiusM*scale);
                if(admission>potential*stand*TreeFlora.Stocking(forest))return false;
                var sample=patch.Sample(at);if(sample.Underwater||sample.ElevationM<2)return false;
                var e=world.Ecology;var h=e!=null&&sample.Cell<e.Habitat.Length?e.Habitat[sample.Cell]:null;
                var species=terrain.FloraRegion(sample.Cell).Trees;double sum=0;
                for(int i=0;i<species.Length;i++)
                {
                    weights[i]=TreeFlora.SpeciesWeight(species[i],h,sample,at,seed,terrain.RadiusM*scale,world.ElevationM[sample.Cell]);sum+=weights[i];
                }
                if(sum<.001)return false;
                double pick=r.Unit()*sum;int chosen=species.Length-1;for(int i=0;i<species.Length;i++){pick-=weights[i];if(pick<=0){chosen=i;break;}}
                var s=species[chosen];V east=V.Cross(Math.Abs(at.Z)<.99?new V(0,0,1):new V(0,1,0),at).Unit,north=V.Cross(at,east).Unit;
                double delta=1.5/(terrain.RadiusM*scale);
                double sx=(patch.Elevation((at+east*delta).Unit)-patch.Elevation((at-east*delta).Unit))/3;
                double sz=(patch.Elevation((at+north*delta).Unit)-patch.Elevation((at-north*delta).Unit))/3;
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
                double localWarm=(h==null?18:h.WarmTenthsC*.1)-.006*(sample.ElevationM-world.ElevationM[sample.Cell]);
                double growth=D.Clamp((localWarm-5)/12);growth=growth*growth*(3-2*growth);
                size*=(.28+.72*growth)*(.65+.35*D.Clamp(sample.Ground.DepthM/.6));
                double tint=.93+.12*r.Unit();
                tree=new ScatterInstance{Id=id,Kind=ScatterKind.GeneratedTree,Variant=s.MorphologyCode|(int)(r.Unit()*6),Position=at,Normal=(at-east*sx-north*sz).Unit,
                    GroundM=sample.ElevationM,HeightM=D.Q(s.HeightM*size,1000),WidthM=D.Q(s.WidthM*size*(.88+.24*r.Unit()),1000),
                    YawDegrees=D.Q(r.Unit()*360,1000),Color=new V(tint,tint,tint)};return true;
        }
    }
}
