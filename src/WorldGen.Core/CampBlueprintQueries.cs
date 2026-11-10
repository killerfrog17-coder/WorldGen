using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace WorldGen.Core
{
    public static partial class CampBlueprints
    {
        // Geometry is independent of supplied goods/work/weathering. Keep the
        // private query copy weakly owned by its plan; public Components still
        // returns a fresh, caller-owned list. Compare every shape input, including
        // in-place floor edits, rather than trusting a construction revision.
        sealed class QueryGeometry
        {
            public int Version;public CampDesign Design;public CampCover Cover;public CampStore Store;
            public double Width,Depth,Height;public double[] Floor;public List<CampComponent> Parts;
            public bool Matches(CampBlueprint p)
            {
                if(Parts==null||Version!=p.Version||Design!=p.Design||Cover!=p.Cover||Store!=p.Store||!Width.Equals(p.WidthM)||!Depth.Equals(p.DepthM)||!Height.Equals(p.ScreenHeightM))return false;
                if(Floor==null||p.FloorHeights==null)return Floor==p.FloorHeights;
                if(Floor.Length!=p.FloorHeights.Length)return false;
                for(int i=0;i<Floor.Length;i++)if(!Floor[i].Equals(p.FloorHeights[i]))return false;
                return true;
            }
        }
        static readonly ConditionalWeakTable<CampBlueprint,QueryGeometry> queryGeometry=new ConditionalWeakTable<CampBlueprint,QueryGeometry>();
        static readonly ConditionalWeakTable<CampBlueprint,QueryGeometry>.CreateValueCallback createQueryGeometry=_=>new QueryGeometry();
        static List<CampComponent> QueryComponents(CampBlueprint p)
        {
            var cached=queryGeometry.GetValue(p,createQueryGeometry);
            lock(cached)
            {
                if(!cached.Matches(p))
                {
                    cached.Parts=Components(p);cached.Version=p.Version;cached.Design=p.Design;cached.Cover=p.Cover;cached.Store=p.Store;
                    cached.Width=p.WidthM;cached.Depth=p.DepthM;cached.Height=p.ScreenHeightM;cached.Floor=p.FloorHeights==null?null:(double[])p.FloorHeights.Clone();
                }
                return cached.Parts;
            }
        }
    }
}
