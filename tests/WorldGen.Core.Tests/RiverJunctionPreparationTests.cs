using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class RiverJunctionPreparationTests
{
    static bool Equal(object a,object b)
    {
        if(a is double x&&b is double y)return BitConverter.DoubleToInt64Bits(x)==BitConverter.DoubleToInt64Bits(y);
        if(a is V u&&b is V v)return Equal(u.X,v.X)&&Equal(u.Y,v.Y)&&Equal(u.Z,v.Z);
        return a.Equals(b);
    }
    public static void Run(string input,string output)
    {
        var world=WorldFile.ReadFile(input);var hash=StateHash.Compute(world);
        string cache=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!,"node-order-cache-"+Path.GetFileNameWithoutExtension(output));
        var first=LocalTerrainDerivationCache.Create(world,horizontalScale:.1,directory:cache);var basis=first.Amplification.ExportRiverDatums();
        var active=(bool[])typeof(HyperAmplification).GetField("riverActive",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(first.Amplification)!;
        var available=Enumerable.Range(0,active.Length).Where(i=>active[i]).ToHashSet();
        var sources=new[]{126812,127655,129629,74799,46966,134628,91811,104048}.Where(available.Contains)
            .Concat(available.OrderBy(i=>Rng.Mix((uint)i^0x74b21u)).Take(16)).Distinct().ToArray();
        var expected=new Dictionary<int,RiverscapeSpan[]>();var preparation=new List<object>();int checks=0,reduced=0;
        void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        foreach(int source in sources)
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();var spans=first.Amplification.RiverReach(source);expected.Add(source,spans);
            preparation.Add(new{source,Milliseconds=timer.Elapsed.TotalMilliseconds,Spans=spans.Length});
            Check(spans.Length>0,"An actual admitted source has its complete prepared reach");
            foreach(var s in spans)Check(s.LevelA>=s.LevelB-1e-9&&double.IsFinite(s.LevelA)&&double.IsFinite(s.LevelB),"Prepared local stage stays finite and downstream");
            if(spans[0].LevelA<basis[source]-.00001||spans[^1].To>=0&&spans[^1].LevelB<basis[spans[^1].To]-.00001)reduced++;
            if(spans[^1].To>=0)
            {
                var next=first.Amplification.RiverReach(spans[^1].To);
                if(next.Length>0)Check(Equal(spans[^1].LevelB,next[0].LevelA),"Independently prepared neighbouring reaches share the exact actual stage");
            }
        }
        var after=first.Amplification.ExportRiverDatums();Check(basis.Zip(after).All(p=>Equal(p.First,p.Second)),"Local fitting never changes globally cached graph datums");
        var second=LocalTerrainDerivationCache.Create(world,horizontalScale:.1,directory:cache);
        var actual=new Dictionary<int,RiverscapeSpan[]>();foreach(int source in sources.Reverse())actual.Add(source,second.Amplification.RiverReach(source));
        var fields=typeof(RiverscapeSpan).GetFields(BindingFlags.Public|BindingFlags.Instance);
        foreach(int source in sources)
        {
            var a=expected[source];var b=actual[source];Check(a.Length==b.Length,"Preparation order preserves station count");
            for(int i=0;i<a.Length;i++)foreach(var field in fields)Check(Equal(field.GetValue(a[i])!,field.GetValue(b[i])!),$"Preparation order changed source{source}/station{i}/{field.Name}");
        }
        Check(reduced>0,"Actual local fitting exercises a real correction below the broad graph level");
        Check(StateHash.Compute(world)==hash,"Local derived geography never mutates world or history");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllText(output,JsonSerializer.Serialize(new{Version=LocalTerrain.Version,Checks=checks,Sources=sources.Length,Reduced=reduced,Preparation=preparation,Scope="Exact public station data from forward/reverse preparation, with shared immutable broad datums; no native FPS claim"},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {checks} local river-stage/order controls across {sources.Length} sources; {reduced} real local head corrections");
    }
}
