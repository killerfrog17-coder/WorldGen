using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public sealed class VegetationBenchmarkSite {public string Name="";public V Position;public double CanopyShade;public string Biome="";}
    public sealed class VegetationBenchmarkSiteSet {public string HistoryHash="";public VegetationBenchmarkSite[] Sites=Array.Empty<VegetationBenchmarkSite>();}
    public static class VegetationBenchmarkSites
    {
        // Offline site selection only. Ordinary gameplay never scans the map.
        public static VegetationBenchmarkSiteSet Dense(World world,LocalTerrain terrain)
        {
            var result=new List<VegetationBenchmarkSite>();double radius=terrain.RadiusM*terrain.HorizontalScale;
            foreach(var biome in new[]{Biome.TropicalRainforest,Biome.TemperateForest,Biome.BorealForest,Biome.Savanna})
            {
                VegetationBenchmarkSite best=null;double bestScore=0;
                var candidates=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>!world.Hydrology.OceanConnected[i]&&terrain.Profile(i).Biome==biome)
                    .OrderByDescending(i=>biome==Biome.Savanna?terrain.Profile(i).Cover.Stature*(terrain.Profile(i).Cover.Grasses.X+terrain.Profile(i).Cover.Grasses.Y+terrain.Profile(i).Cover.Grasses.Z)*(1-terrain.Profile(i).Forest.Coverage):TreeFlora.CanopyShade(terrain.Profile(i).Forest,world.Surface.Cells[i].Position,world.Recipe.Seed,radius)).Take(48);
                foreach(int cell in candidates)
                {
                    var patch=terrain.Patch(world.Surface.Cells[cell].Position,10000);
                    foreach(var offset in new[]{new V(0,0,0),new V(1200,0,0),new V(0,0,1200)})
                    {
                        V at=patch.Position(offset.X,offset.Z);var sample=patch.Sample(at);if(sample.Underwater||sample.ElevationM<5||sample.Biome!=biome)continue;
                        double step=20/terrain.HorizontalScale;
                        double dx=(patch.Sample(patch.Position(offset.X+step,offset.Z)).ElevationM-sample.ElevationM)/20;
                        double dz=(patch.Sample(patch.Position(offset.X,offset.Z+step)).ElevationM-sample.ElevationM)/20;
                        double up=1/Math.Sqrt(1+dx*dx+dz*dz);if(up<.85)continue;
                        var forest=TreeFlora.Surface(sample.Forest,at,world.Recipe.Seed,radius,sample.ElevationM,up,false);
                        double shade=TreeFlora.CanopyShade(sample.Forest,at,world.Recipe.Seed,radius);
                        double score=biome==Biome.Savanna?sample.Cover.Stature*(sample.Cover.Grasses.X+sample.Cover.Grasses.Y+sample.Cover.Grasses.Z)*(1-shade):shade*Math.Min(1,forest.Coverage/.03);
                        if(score<=bestScore)continue;bestScore=score;
                        best=new VegetationBenchmarkSite{Name=biome==Biome.Savanna?"savanna-tall":"dense-"+biome.ToString().ToLowerInvariant(),Position=at,CanopyShade=shade,Biome=biome.ToString()};
                    }
                    if(bestScore>.96)break;
                }
                if(best==null)throw new InvalidOperationException("No actual dry vegetation benchmark context for "+biome);result.Add(best);
            }
            return new VegetationBenchmarkSiteSet{HistoryHash=world.Prehistory.Hash,Sites=result.ToArray()};
        }
    }
}
