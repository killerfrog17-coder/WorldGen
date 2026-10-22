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
    }
}
