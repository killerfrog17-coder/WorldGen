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
        // read these same planet-addressed nodes. Exact interactive root water
        // and height come from Geometry; distant candidates do not query them.
        const int TreeHabitatStep=16,TreeHabitatLimit=32768;
        sealed class TreeHabitatNodeData
        {
            public LocalTerrainSample Sample;public V Position;
            // One requested regional flora array per node; an ecotone may
            // replace it, but cannot grow an unbounded region/model cache.
            public TreeSpecies[] Species;public double[] Weights;
            public PlantingBlockData Planting;public bool PlantingReady;public float Probability;
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
            // Habitat inputs have a fixed physical footprint. Do not evaluate
            // sub-metre terrain detail for a shared ~64m planting-map node.
            var node=new TreeHabitatNodeData{Sample=patch.SampleMap(point,TreeHabitatStep*TreeLatticeStep),Position=point};
            node.Probability=PlantingProbability(node.Sample,point);
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
        public static double TreeSlopeRoll(ulong id)
        {
            uint identity=(uint)id^Rng.Mix((uint)(id>>32));
            return (Rng.Mix(identity^0x9a65c4u)&0xffffffu)/16777216.0;
        }
        /// <summary>Physical interaction admission over the shared candidate.
        /// Detailed water/slope checks may reject it, never reroll its anatomy.</summary>
        public bool TryTree(ScatterKey key,int x,int y,LocalTerrain.LocalTerrainPatch patch,out ScatterInstance tree)
        {
            if(key.Detail||x<0||x>=TreeCandidateSide||y<0||y>=TreeCandidateSide)throw new ArgumentOutOfRangeException();
            bool found=CachedTree(key,y*TreeCandidateSide+x,out tree,out bool known);if(known)return found;
            if(!TryTreeCandidate(key,x,y,patch,out tree))return false;
            // Admission is immutable and shared with the GPU. Interaction
            // only resolves the exact ground, never rerolls a visible forest.
            var geometry=patch.Geometry(tree.Position);
            tree.GroundM=geometry.ElevationM;return true;
        }
        /// <summary>The ONE candidate identity shared by local meshes and all
        /// forest LODs. Only shared habitat nodes sample terrain; there is no
        /// exact root surface query or physical-cache dependency on this path.</summary>
        public bool TryTreeCandidate(ScatterKey key,int x,int y,LocalTerrain.LocalTerrainPatch patch,out ScatterInstance tree)
        {
            tree=default;if(key.Detail||x<0||x>=TreeCandidateSide||y<0||y>=TreeCandidateSide)throw new ArgumentOutOfRangeException();
            int gx=key.X*TreeCandidateSide+x,gy=key.Y*TreeCandidateSide+y;
            int left=gx/ForestPlantingSide*ForestPlantingSide,bottom=gy/ForestPlantingSide*ForestPlantingSide;
            var block=PlantingBlock(key.Face,left,bottom,patch);if(block==null)return false;
            int child=(gy-bottom)*ForestPlantingSide+gx-left;if(!block.Mask.Contains(child))return false;
            uint h=PlantingHash(key.Face,gx,gy,seed);PlantingOffset(h,out float ox,out float oy);
            float u=(gx-left+ox)/ForestPlantingSide,v=(gy-bottom+oy)/ForestPlantingSide;
            float probability=(block.P00+(block.P10-block.P00)*u)*(1-v)+(block.P01+(block.P11-block.P01)*u)*v;
            if(PlantingUnit(h^0x27d4eb2du)>=probability)return false;
            var prototype=block.Trees[block.Species[child]];
            PlantingDimensions(h,prototype.Variant,out float height,out float width,out float yaw,out float tint);
            var at=ForestPosition(key.Face,gx+ox,gy+oy);
            tree=prototype;tree.Id=key.Id(0,y*TreeCandidateSide+x);tree.Position=tree.Normal=at;
            tree.WidthM*=width;tree.HeightM*=height;tree.YawDegrees=yaw*180/Math.PI;tree.Color=new V(tint,tint,tint);return true;
        }
        sealed class PlantingBlockData
        {
            public ScatterInstance[] Trees;public byte[] Species;public ForestPlantingMask[] SpeciesMasks;
            public float P00,P10,P01,P11;public ForestPlantingMask Mask;
        }
        float PlantingProbability(LocalTerrainSample sample,V position)
        {
            var p=sample.Forest;var biome=TreeFlora.TidalCode(p.MorphologyCode)?terrain.Profile(sample.Cell).Biome:sample.Biome;
            if(!TreeFlora.Rooting(p,sample.ElevationM,sample.Underwater,sample.Underwater?sample.WaterSurfaceM-sample.ElevationM:0,biome))return 0;
            double warm=p.WarmC-(sample.ElevationM-p.ReferenceElevationM)*.006;
            return(float)D.Clamp(p.Coverage*TreeFlora.Stocking(p)*TreeFlora.Stand(p,position,seed,terrain.RadiusM*scale)*D.Clamp((warm-6)/5));
        }
        public static uint PlantingMix(uint x){x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;return x^(x>>16);}
        public static uint PlantingHash(int face,int x,int y,int worldSeed)=>PlantingMix(unchecked((uint)x*73856093u^(uint)y*19349663u^(uint)face*83492791u^(uint)worldSeed));
        public static float PlantingUnit(uint x)=>(PlantingMix(x)&0xffffffu)/16777216f;
        public static void PlantingOffset(uint h,out float x,out float y)
        {x=.08f+.84f*PlantingUnit(h^0x68bc21ebu);y=.08f+.84f*PlantingUnit(h^0x02e5be93u);}
        public static void PlantingDimensions(uint h,int variant,out float height,out float width,out float yaw,out float tint)
        {
            float cohort=PlantingUnit(h^0x967a889bu),r=PlantingUnit(h^0x368cc8b7u);
            bool tropical=((variant>>22)&3)==(int)TreeCrownStrategy.TropicalCanopy&&((variant>>28)&7)==(int)TreeGrowthForm.BroadCrown;
            height=tropical?(cohort<.69f?.10f+.35f*r:cohort>.99f?1.28f+.28f*r:.82f+.25f*r):
                cohort<.12f?.18f+.16f*r:cohort<.30f?.40f+.25f*r:.72f+.36f*r;
            width=height*(.88f+.24f*PlantingUnit(h^0x9e3779b9u));yaw=6.28318530718f*PlantingUnit(h^0x85ebca6bu);tint=.93f+.12f*PlantingUnit(h^0xa24baed5u);
        }
        PlantingBlockData PlantingBlock(int face,int left,int bottom,LocalTerrain.LocalTerrainPatch patch)
        {
            var a=TreeHabitatNode(face,left/TreeHabitatStep,bottom/TreeHabitatStep,patch);
            lock(a)if(a.PlantingReady)return a.Planting;
            var b=TreeHabitatNode(face,left/TreeHabitatStep+1,bottom/TreeHabitatStep,patch);
            var c=TreeHabitatNode(face,left/TreeHabitatStep,bottom/TreeHabitatStep+1,patch);
            var d=TreeHabitatNode(face,left/TreeHabitatStep+1,bottom/TreeHabitatStep+1,patch);
            PlantingBlockData block=null;
            if(Math.Max(Math.Max(a.Probability,b.Probability),Math.Max(c.Probability,d.Probability))>0)
            {
                V center=ForestPosition(face,left+ForestPlantingSide*.5,bottom+ForestPlantingSide*.5);
                var species=terrain.FloraRegion(a.Sample.Cell).Trees;Span<double> weights=stackalloc double[13];
                var wa=TreeSpeciesWeights(a,species);var wb=TreeSpeciesWeights(b,species);
                var wc=TreeSpeciesWeights(c,species);var wd=TreeSpeciesWeights(d,species);
                double sum=0;for(int i=0;i<species.Length;i++)sum+=wa[i]+wb[i]+wc[i]+wd[i];
                if(sum>0)
                {
                    var sample=a.Sample;
                    double elevation=(a.Sample.ElevationM+b.Sample.ElevationM+c.Sample.ElevationM+d.Sample.ElevationM)*.25;
                    double warm=sample.Forest.WarmC-.006*(elevation-sample.Forest.ReferenceElevationM),growth=D.Clamp((warm-5)/12);growth=growth*growth*(3-2*growth);
                    growth=(.28+.72*growth)*(.65+.35*D.Clamp(sample.Ground.DepthM/.6));
                    V n=Normals[face];
                    double dot=V.Dot(center,n),area=TreeLatticeStep*TreeLatticeStep*dot*dot*dot;
                    float cap=(float)Math.Min(1,area*TreeFlora.PlacementPerM2);
                    block=new PlantingBlockData{P00=a.Probability*cap,P10=b.Probability*cap,P01=c.Probability*cap,P11=d.Probability*cap,
                        Trees=new ScatterInstance[species.Length],Species=new byte[ForestStandTreeCount],SpeciesMasks=new ForestPlantingMask[species.Length]};
                    for(int i=0;i<species.Length;i++)
                    {
                        var s=species[i];
                        block.Trees[i]=new ScatterInstance{Id=new ScatterKey(face,left/TreeCandidateSide,bottom/TreeCandidateSide).Id(0,(bottom%TreeCandidateSide)*TreeCandidateSide+left%TreeCandidateSide),
                            Kind=ScatterKind.GeneratedTree,Variant=s.MorphologyCode,Position=center,Normal=center,GroundM=elevation,HeightM=s.HeightM*growth,WidthM=s.WidthM*growth,Color=new V(1,1,1)};
                    }
                    // Catchment flow is nonzero across whole uplands. It is
                    // not a water footprint and must not trigger 64 full terrain
                    // queries for every dry block. Existing finite river spans
                    // and pond descriptors conservatively select actual banks.
                    double margin=TreeLatticeStep*ForestPlantingSide*1.5,physical=terrain.RadiusM*scale;
                    bool NearLevel(LocalTerrainSample v)=>v.Underwater||v.WaterKind!=LocalWaterKind.None&&!double.IsNaN(v.WaterSurfaceM)&&Math.Abs(v.ElevationM-v.WaterSurfaceM)<margin*2;
                    bool water=NearLevel(a.Sample)||NearLevel(b.Sample)||NearLevel(c.Sample)||NearLevel(d.Sample);
                    if(!water)foreach(var span in patch.RiverSpans())
                    {
                        V along=span.B-span.A,delta=center-span.A;double t=D.Clamp(V.Dot(delta,along)/Math.Max(1e-30,V.Dot(along,along)));
                        double limit=margin+Math.Max(span.WidthA,span.WidthB)*1.5;
                        V nearest=(delta-along*t)*physical;if(V.Dot(nearest,nearest)<=limit*limit){water=true;break;}
                    }
                    if(!water&&terrain.Amplification!=null)water=terrain.Amplification.SmallWaterMayIntersect(center,margin);
                    double step=TreeHabitatStep*TreeLatticeStep;
                    for(int y=0;y<ForestPlantingSide;y++)for(int x=0;x<ForestPlantingSide;x++)
                    {
                        if(left+x>=TreeLatticeCount||bottom+y>=TreeLatticeCount)continue;
                        uint h=PlantingHash(face,left+x,bottom+y,seed);PlantingOffset(h,out float ox,out float oy);
                        double u=(x+ox)/ForestPlantingSide,v=(y+oy)/ForestPlantingSide;
                        // Species are selected at each stable world-space tree, not
                        // once for the square scheduling block. Nearby habitat
                        // weights vary continuously; the hash only selects among
                        // species that actually suit that local environment.
                        double total=0;
                        for(int i=0;i<species.Length;i++)
                        {weights[i]=(wa[i]*(1-u)+wb[i]*u)*(1-v)+(wc[i]*(1-u)+wd[i]*u)*v;total+=weights[i];}
                        if(total<=0)continue;
                        double pick=PlantingUnit(h^0x41a8c3u)*total;int chosen=species.Length-1;
                        for(int i=0;i<species.Length;i++){pick-=weights[i];if(pick<0){chosen=i;break;}}
                        int child=y*ForestPlantingSide+x;block.Species[child]=(byte)chosen;
                        double dx=((b.Sample.ElevationM-a.Sample.ElevationM)*(1-v)+(d.Sample.ElevationM-c.Sample.ElevationM)*v)/step;
                        double dz=((c.Sample.ElevationM-a.Sample.ElevationM)*(1-u)+(d.Sample.ElevationM-b.Sample.ElevationM)*u)/step;
                        if(PlantingUnit(h^0x9a65c4u)>TreeFlora.SlopeAdmission(1/Math.Sqrt(1+dx*dx+dz*dz)))continue;
                        if(water)
                        {
                            var at=ForestPosition(face,left+x+ox,bottom+y+oy);var g=patch.Geometry(at);
                            if(!TreeFlora.Rooting(new ForestCoverProfile{MorphologyCode=species[chosen].MorphologyCode},g.ElevationM,g.Underwater,g.Underwater?g.WaterSurfaceM-g.ElevationM:0,sample.Biome))continue;
                        }
                        block.Mask.Add(child);block.SpeciesMasks[chosen].Add(child);
                    }
                }
            }
            lock(a){if(!a.PlantingReady){a.Planting=block;a.PlantingReady=true;}return a.Planting;}
        }
    }
}
