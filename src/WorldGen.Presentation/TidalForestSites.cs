using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public sealed class TidalForestSite
    {
        public int Cell;
        public V Position,Seaward;
        public double Height,ForestCoverage;
        public bool Submerged;
    }
    /// <summary>Offline/review discovery of real mapped tidal forests. Ordinary
    /// gameplay does not scan the planet or create new mangrove provinces.</summary>
    public static class TidalForestSites
    {
        public static IEnumerable<TidalForestSite> Nearshore(World world,LocalTerrain model)
        {
            double radius=model.RadiusM*model.HorizontalScale;
            var cells=Enumerable.Range(0,world.Surface.Cells.Length)
                .Where(i=>model.Profile(i).Biome==Biome.Mangrove&&TreeFlora.TidalCode(model.Profile(i).Forest.MorphologyCode)&&model.Profile(i).Forest.Coverage>.08)
                .Where(i=>world.Surface.Cells[i].Neighbors.Any(n=>world.Hydrology.OceanConnected[n]))
                .OrderByDescending(i=>model.Profile(i).Forest.Coverage).Take(96);
            foreach(int cell in cells)
            {
                V land=world.Surface.Cells[cell].Position;
                foreach(int sea in world.Surface.Cells[cell].Neighbors.Where(n=>world.Hydrology.OceanConnected[n]).OrderBy(n=>world.ElevationM[n]))
                {
                    V ocean=world.Surface.Cells[sea].Position;V previous=land;var previousSample=model.Patch(land,5000).Sample(land);
                    for(int step=1;step<=32;step++)
                    {
                        V at=(land*(1-step/32d)+ocean*(step/32d)).Unit;var sample=model.Patch(at,5000).Sample(at);
                        if(previousSample.ElevationM>.35&&sample.ElevationM<=.35)
                        {
                            V high=previous,low=at;
                            for(int j=0;j<18;j++)
                            {
                                V middle=(high+low).Unit;var s=model.Patch(middle,5000).Sample(middle);
                                if(s.ElevationM>.35)high=middle;else low=middle;
                            }
                            V shore=(high+low).Unit;var root=model.Patch(shore,5000).Sample(shore);
                            if(model.Profile(root.Cell).Biome!=Biome.Mangrove||!TreeFlora.TidalCode(root.Forest.MorphologyCode)||root.Forest.Coverage<.08||root.Underwater||root.WaterKind!=LocalWaterKind.Ocean||root.WaterSurfaceM!=0)break;
                            V toward=V.Tangent(shore,ocean).Unit;bool flat=true;
                            foreach(int sign in new[]{-1,1})
                            {
                                var p=(shore+toward*(sign*8/radius)).Unit;var s=model.Patch(p,5000).Sample(p);
                                if(Math.Abs(s.ElevationM-root.ElevationM)>3)flat=false;
                            }
                            if(flat)yield return new TidalForestSite{Cell=root.Cell,Position=shore,Seaward=toward,Height=root.ElevationM,ForestCoverage=root.Forest.Coverage,Submerged=root.Underwater};
                            break;
                        }
                        previous=at;previousSample=sample;
                    }
                }
            }
        }
    }
}
