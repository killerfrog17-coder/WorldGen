using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    public sealed partial class LocalTerrain
    {
        /// <summary>Worker-owned height-only map cursor. Uses the same amplified
        /// height/water hierarchy without constructing terrain/material patches.
        /// The full-detail controller/mesh path is untouched.</summary>
        public sealed class MapHeightCursor
        {
            readonly LocalTerrain owner;
            readonly Dictionary<(int,double),HyperAmplification.Patch> roots=new Dictionary<(int,double),HyperAmplification.Patch>();
            readonly Queue<(int,double)> order=new Queue<(int,double)>();
            internal MapHeightCursor(LocalTerrain owner){this.owner=owner;}
            public LocalTerrainSample Sample(V point,double footprint)
            {
                if(owner.Amplification==null||footprint<=128)
                    return owner.MapPatch(point,Math.Min(250000,2048/owner.HorizontalScale)).SampleMap(point,footprint);
                int cell=owner.NearestCell(point);
                var key=(cell,footprint);
                if(!roots.TryGetValue(key,out var patch))
                {
                    // A local face star, not a planet-wide face search. Include
                    // neighbour stars for robust spherical point location.
                    var ids=new SortedSet<int>();
                    void Add(int id){for(int k=owner.faceOffsets[id];k<owner.faceOffsets[id+1];k++)ids.Add(owner.faces[k]);}
                    Add(cell);foreach(int neighbor in owner.world.Surface.Cells[cell].Neighbors)Add(neighbor);
                    var faces=new int[ids.Count];ids.CopyTo(faces);patch=owner.Amplification.Region(faces);
                    if(order.Count>=128)roots.Remove(order.Dequeue());roots.Add(key,patch);order.Enqueue(key);
                }
                var s=patch.Sample(point,false,footprint);
                return new LocalTerrainSample{Cell=cell,ElevationM=D.Q(s.Height,1000),BaseElevationM=D.Q(s.UnshapedHeight,1000),PreWaterElevationM=D.Q(s.UnshapedHeight,1000),
                    WaterSurfaceM=s.Water,WaterMaskM=s.Mask,WaterKind=s.Kind,WaterBodyKey=s.BodyKey,WaterGradient=s.Gradient,WaterFootprintM=s.Footprint,
                    WaterMinimumM=s.Minimum,WaterMaximumM=s.Maximum,WaterFlow=s.Flow,WaterSpeedMPerS=s.Speed,WaterTurbulence=s.Turbulence,
                    RiverInfluence=s.RiverInfluence,ContinuousWaterSurface=s.Kind==LocalWaterKind.River};
            }
        }
        public MapHeightCursor CreateMapHeightCursor()=>new MapHeightCursor(this);
        /// <summary>Same sorted compact-support material kernel as Sample, without
        /// cloning whole profile arrays for thousands of distant map patches.</summary>
        public void FillMapMaterials(V point,ref LocalTerrainSample sample,List<int> scratch)
        {
            locator.WithinTo(point,Math.Pow(Math.Min(2,(maximumSupport+20)/RadiusM),2),scratch);
            double px=point.X*RadiusM,py=point.Y*RadiusM,pz=point.Z*RadiusM,sum=0,best=-1,hills=0,mountains=0,ocean=0,red=0,green=0,blue=0;
            var ground=new GroundProfile();var cover=new GroundCoverProfile();var mid=new MidCoverProfile();var forest=new ForestCoverProfile();
            foreach(int id in scratch)
            {
                ref readonly var f=ref profiles[id];double x=px-f.Center.X*RadiusM,y=py-f.Center.Y*RadiusM,z=pz-f.Center.Z*RadiusM;
                double dist2=(x*x+y*y+z*z)/(f.SupportM*f.SupportM);if(dist2>=1)continue;
                double r=Math.Sqrt(dist2),t=1-r,w=t*t*t*t*(1+4*r);sum+=w;
                ground.Add(f.Ground,w);cover.Add(f.Cover,w);mid.Add(f.MidCover,w);forest.Add(f.Forest,w);
                hills+=w*f.HillHeightM;mountains+=w*f.MountainHeightM;
                var color=CoverColor(f.Biome);red+=w*color.X;green+=w*color.Y;blue+=w*color.Z;
                if(f.Cell<world.Hydrology.OceanConnected.Length&&world.Hydrology.OceanConnected[f.Cell])ocean+=w;
                if(w>best){best=w;sample.Cell=f.Cell;sample.Biome=f.Biome;ground.Bedrock=f.Ground.Bedrock;ground.Deposit=f.Ground.Deposit;}
            }
            if(sum<=0)throw new InvalidOperationException("Map material query has no supporting world cells.");
            ground.Divide(sum);cover.Divide(sum);mid.Divide(sum);forest.Divide(sum);
            sample.Ground=ground;sample.Cover=cover;sample.MidCover=mid;sample.Forest=forest;
            sample.HillHeightM=hills/sum;sample.MountainHeightM=mountains/sum;sample.OceanInfluence=ocean/sum;sample.CoverColor=new V(red/sum,green/sum,blue/sum);
        }
    }
}
