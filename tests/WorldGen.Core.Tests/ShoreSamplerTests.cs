using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class ShoreSamplerTests
{
    public static void Run(Action<bool,string> check)
    {
        var recipe=Recipe.Preset("Preview");recipe.Detail=5000;var w=new WorldPipeline().Generate(recipe);string before=StateHash.Compute(w);
        var sampler=new ShoreTerrainSampler(w);var repeat=new ShoreTerrainSampler(WorldFile.Read(WorldFile.Write(w)));
        check(sampler.Features.Length>0,"Shore features available");
        check(StateHash.Compute(sampler.Features)==StateHash.Compute(repeat.Features),"Versioned derived features repeat after save/load");
        check(sampler.Features.Count(f=>f.Kind==ShoreKind.Island)==w.Subcells.Islands.Length,"Every individual island retained");
        check(w.Subcells.Cells.Any(c=>c.IslandCount>1),"Multi-island cells exist");
        foreach(var f in sampler.Features.Take(50))
        {
            var low=sampler.Outline(f,32);var high=sampler.Outline(f,128);
            check(low.Select((p,i)=>(p-high[i*4]).Length).All(d=>d<1e-8),"Outline detail cannot move the shoreline");
            check(high.All(p=>Math.Abs(f.Coordinate(p,recipe.RadiusKm*1000)-1)<.002),"Rendered outline uses sample field zero contour");
            check(StateHash.Compute(sampler.Sample(f.Center))==StateHash.Compute(repeat.Sample(f.Center)),"Point queries repeat after load");
        }
        int n=w.Surface.Cells.Length,a=Enumerable.Range(0,n).First(i=>!w.Hydrology.OceanConnected[i]),b=w.Surface.Cells[a].Neighbors[0],c=w.Surface.Cells[b].Neighbors.First(i=>i!=a&&!w.Surface.Cells[a].Neighbors.Contains(i));
        var hydro=w.Hydrology;var ecology=w.Ecology;
        w.Hydrology=new HydrologyResult{OceanConnected=new bool[n],LakeCoverageFraction=new double[n],LakeWaterVolumeM3=new double[n],WaterBody=Enumerable.Repeat(-1,n).ToArray(),Waters=new[]{new WaterBody{SurfaceElevationM=25}}};w.Ecology=null;
        foreach(int i in new[]{a,b,c}){w.Hydrology.WaterBody[i]=0;w.Hydrology.LakeCoverageFraction[i]=i==b?.02:.3;w.Hydrology.LakeWaterVolumeM3[i]=w.Surface.Cells[i].AreaM2*w.Hydrology.LakeCoverageFraction[i]*8;}
        var lakes=new ShoreTerrainSampler(w).Features.Where(f=>f.Kind==ShoreKind.Lake).ToArray();
        check(lakes.Length==3,"Two-percent middle cell does not force a channel between lakes");
        check(Math.Abs(lakes.Sum(f=>f.AreaM2)-new[]{a,b,c}.Sum(i=>w.Surface.Cells[i].AreaM2*w.Hydrology.LakeCoverageFraction[i]))<1,"Aggregate lake allocation preserved");
        foreach(int i in new[]{a,b,c})w.Hydrology.LakeWaterVolumeM3[i]=w.Surface.Cells[i].AreaM2*w.Hydrology.LakeCoverageFraction[i]*.4;
        var wet=new ShoreTerrainSampler(w);check(wet.Features.Where(f=>f.Kind!=ShoreKind.Island).All(f=>f.Kind==ShoreKind.Wetland),"Shallow inundation becomes wetland, not open-water polygons");
        var wetFeature=wet.Features.First(f=>f.Kind==ShoreKind.Wetland);var wetSample=wet.Sample(wetFeature.Center);check(wetSample.Wetland&&!wetSample.OpenWater,"Sampler distinguishes wetland from open lake water");
        w.Hydrology=hydro;w.Ecology=ecology;
        int cold=Enumerable.Range(0,n).OrderBy(i=>w.Climate.NorthernSummer.TemperatureTenthsC[i]+w.Climate.NorthernWinter.TemperatureTenthsC[i]).First();
        int hot=Enumerable.Range(0,n).OrderByDescending(i=>w.Climate.NorthernSummer.TemperatureTenthsC[i]+w.Climate.NorthernWinter.TemperatureTenthsC[i]).First();
        check((NaturalSurface.IslandColor(w,cold,20)-NaturalSurface.IslandColor(w,hot,20)).Length>.1,"Island colors respond to climate");
        check(before==StateHash.Compute(w),"Derived shore sampling does not mutate saved world");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();try{new ShoreTerrainSampler(w,cancelled.Token);check(false,"Cancellation");}catch(OperationCanceledException){check(true,"Cancellation");}
    }
}
