using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Bounded explicit review discovery, not a gameplay tick or a
    /// new habitat generator. Follows mapped saltmarsh to connected ocean.</summary>
    public static class TidalMarshSites
    {
        public static IEnumerable<TidalForestSite> Nearshore(World world,LocalTerrain model,int source=-1)
        {
            double radius=model.RadiusM*model.HorizontalScale;
            var cells=Enumerable.Range(0,world.Surface.Cells.Length)
                .Where(i=>model.Profile(i).Biome==Biome.SaltMarsh&&(source<0||source==i))
                .Where(i=>world.Surface.Cells[i].Neighbors.Any(n=>world.Hydrology.OceanConnected[n]))
                .OrderByDescending(i=>model.Profile(i).MidCover.Coverage).Take(64);
            foreach(int cell in cells)
            {
                V land=world.Surface.Cells[cell].Position;
                foreach(int sea in world.Surface.Cells[cell].Neighbors.Where(n=>world.Hydrology.OceanConnected[n]).OrderBy(n=>world.ElevationM[n]))
                {
                    V ocean=world.Surface.Cells[sea].Position,previous=land;
                    var patch=model.Patch((land+ocean).Unit,Math.Max(5000,(land-ocean).Length*model.RadiusM));
                    var highSample=patch.Sample(land);
                    for(int step=1;step<=32;step++)
                    {
                        V at=(land*(1-step/32d)+ocean*(step/32d)).Unit;var sample=patch.Sample(at);
                        if(highSample.ElevationM>.65&&sample.ElevationM<=.65)
                        {
                            V high=previous,low=at;
                            for(int j=0;j<18;j++)
                            {V middle=(high+low).Unit;if(patch.Sample(middle).ElevationM>.65)high=middle;else low=middle;}
                            V shore=(high+low).Unit;var root=patch.Sample(shore);
                            if(root.Biome!=Biome.SaltMarsh||root.Underwater||root.WaterKind!=LocalWaterKind.Ocean||root.WaterSurfaceM!=0||root.Province.Wet<.5)break;
                            V toward=V.Tangent(shore,ocean).Unit;bool flat=true;
                            foreach(int sign in new[]{-1,1})
                            {var s=patch.Sample((shore+toward*(sign*8/radius)).Unit);if(Math.Abs(s.ElevationM-root.ElevationM)>2)flat=false;}
                            if(flat)yield return new TidalForestSite{Cell=root.Cell,Position=shore,Seaward=toward,Height=root.ElevationM,ForestCoverage=root.Forest.Coverage,Submerged=false};
                            break;
                        }
                        previous=at;highSample=sample;
                    }
                }
            }
        }
    }
}
