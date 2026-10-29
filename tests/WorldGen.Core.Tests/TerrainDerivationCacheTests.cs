using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
using System.Text.Json;

static class TerrainDerivationCacheTests
{
    public static void Run(string path,string output)
    {
        Directory.CreateDirectory(output);string directory=Path.Combine(output,"cache-"+Guid.NewGuid().ToString("N"));var world=WorldFile.ReadFile(path);string sourceHash=StateHash.Compute(world);var report=new List<string>();
        void Check(bool ok,string text){if(!ok)throw new Exception(text);report.Add("PASS "+text);Console.WriteLine(report.Last());}
        var watch=System.Diagnostics.Stopwatch.StartNew();var a=Task.Run(()=>LocalTerrainDerivationCache.Create(world,horizontalScale:.1,directory:directory));var b=Task.Run(()=>LocalTerrainDerivationCache.Create(world,horizontalScale:.1,directory:directory));Task.WaitAll(a,b);
        Check(a.Result.Amplification.UsedCachedRiverDatums!=b.Result.Amplification.UsedCachedRiverDatums,"Concurrent player/worker fitting runs once and shares its completed cache");
        double coldSeconds=watch.Elapsed.TotalSeconds;var cold=a.Result.Amplification.UsedCachedRiverDatums?b.Result:a.Result;
        string cache=LocalTerrainDerivationCache.FileFor(world,.1,directory);Check(new FileInfo(cache).Length==40L+world.Surface.Cells.Length*8L,"Derived cache has exact bounded record length");
        var points=new[]{TerrainMapViewport.FromDegrees(-17.625665274156873,.6638456954785965),TerrainMapViewport.FromDegrees(-8.248825835553712,17.85174984290807),TerrainMapViewport.FromDegrees(8.025672555331576,-66.52253994300001)};
        void Same(LocalTerrain other)
        {
            Check(cold.Amplification.ExportRiverDatums().SequenceEqual(other.Amplification.ExportRiverDatums()),"Every cached graph datum equals the cold derivation exactly");
            foreach(var p in points){var x=cold.Patch(p,3000).Sample(p);var y=other.Patch(p,3000).Sample(p);Check(x.ElevationM==y.ElevationM&&x.WaterSurfaceM.Equals(y.WaterSurfaceM)&&x.WaterKind==y.WaterKind,"Cached and cold actual water/ground samples agree exactly");}
        }
        Same(a.Result.Amplification.UsedCachedRiverDatums?a.Result:b.Result);
        watch.Restart();var warm=LocalTerrainDerivationCache.Create(world,horizontalScale:.1,directory:directory);double warmSeconds=watch.Elapsed.TotalSeconds;
        Check(warm.Amplification.UsedCachedRiverDatums&&warm.Amplification.RiverscapeFittingMilliseconds<50,"Subsequent world entry skips all bank-fitting queries");Same(warm);
        using(var stream=new FileStream(cache,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){stream.Position=stream.Length-1;int value=stream.ReadByte();stream.Position=stream.Length-1;stream.WriteByte((byte)(value^1));}
        var repaired=LocalTerrainDerivationCache.Create(world,horizontalScale:.1,directory:directory);Check(!repaired.Amplification.UsedCachedRiverDatums,"A corrupt checksum triggers safe re-derivation");Same(repaired);
        var next=LocalTerrainDerivationCache.Create(world,horizontalScale:.1,directory:directory);Check(next.Amplification.UsedCachedRiverDatums,"Repaired cache is atomically reusable");
        Check(LocalTerrainDerivationCache.FileFor(world,.2,directory)!=cache,"Different horizontal scales cannot share cached levels");
        var otherSource=new World{Stages=new List<StageRecord>{new StageRecord{Hash=new string('a',64)}}};Check(LocalTerrainDerivationCache.FileFor(otherSource,.1,directory)!=cache,"Different source generation fingerprints cannot share cached levels");
        bool cancelled=false;using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{LocalTerrainDerivationCache.Create(world,cancellation.Token,.1,Path.Combine(directory,"cancelled"));}catch(OperationCanceledException){cancelled=true;}}
        Check(cancelled&&!Directory.Exists(Path.Combine(directory,"cancelled")),"Cancellation returns before cache or fitting work starts");
        Check(StateHash.Compute(world)==sourceHash,"Derived caching leaves the original world unchanged");
        File.WriteAllLines(Path.Combine(output,"checks.txt"),report);File.WriteAllText(Path.Combine(output,"timings.json"),JsonSerializer.Serialize(new{coldConcurrentSeconds=coldSeconds,warmSeconds,cacheBytes=new FileInfo(cache).Length,checks=report.Count},new JsonSerializerOptions{WriteIndented=true}));
    }
}
