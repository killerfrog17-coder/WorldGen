using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    // On-demand discovery of already-generated shores, never a live simulation
    // or a per-frame coast scan. Shared by reviews and the debug landmark tool.
    public static class CoastalSites
    {
        public readonly struct Site
        {
            public readonly int Cell;public readonly V Position,Seaward;public readonly double Exposure,Height;
            public Site(int cell,V position,V seaward,double exposure,double height){Cell=cell;Position=position;Seaward=seaward;Exposure=exposure;Height=height;}
        }
        public static IEnumerable<Site> WavePlatforms(World world,LocalTerrain terrain,int maximumCandidates=160)
        {
            var coast=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>!world.Hydrology.OceanConnected[i]&&world.Surface.Cells[i].Neighbors.Any(n=>world.Hydrology.OceanConnected[n]));
            foreach(int cell in coast.OrderByDescending(i=>terrain.Profile(i).CoarseReliefM).ThenBy(i=>i).Take(maximumCandidates))
            {
                int ocean=world.Surface.Cells[cell].Neighbors.First(n=>world.Hydrology.OceanConnected[n]);
                V land=world.Surface.Cells[cell].Position,sea=world.Surface.Cells[ocean].Position;
                var patch=terrain.Patch((land+sea).Unit,(land-sea).Length*terrain.RadiusM+2000);
                V low=land,high=sea;if(patch.Sample(low).Underwater)continue;
                for(int j=0;j<20;j++){V mid=(low+high).Unit;if(patch.Sample(mid).ElevationM>0)low=mid;else high=mid;}
                V direction=V.Tangent(low,sea).Unit;
                foreach(double offset in new[]{220d,110,55,28,12})
                {
                    V p=(low-direction*(offset/(terrain.RadiusM*terrain.HorizontalScale))).Unit;var s=patch.Sample(p);
                    if(s.Underwater||s.WaterKind!=LocalWaterKind.Ocean||s.Province.ShoreExposure<.5||s.ElevationM>20||s.Biome==Biome.Ice)continue;
                    // Pick an actual walkable bench, not merely an exposed
                    // point on the scarp. Debug discovery must demonstrate the
                    // feature without manufacturing a flatter review scene.
                    double step=5/(terrain.RadiusM*terrain.HorizontalScale);
                    var inland=patch.Sample((p-direction*step).Unit);
                    var outward=patch.Sample((p+direction*step).Unit);
                    if(outward.Underwater||Math.Abs(inland.ElevationM-outward.ElevationM)>3)continue;
                    yield return new Site(cell,p,direction,s.Province.ShoreExposure,s.ElevationM);break;
                }
            }
        }
    }
}
