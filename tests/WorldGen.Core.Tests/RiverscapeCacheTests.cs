using System.Reflection;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class RiverscapeCacheTests
{
    public static void Run(string input)
    {
        var world=WorldFile.ReadFile(input);var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        var hydro=terrain.Amplification;var gate=typeof(HyperAmplification).GetField("riverGate",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(hydro)!;
        int count=0;
        foreach(var channel in terrain.Hydrology.Channels.Where(c=>c.WidthM>8).Take(24))
        {
            var expected=hydro.RiverReach(channel.From);if(expected.Length==0)continue;
            Task<RiverscapeSpan[]> query;bool finished;
            // Reproduce the expensive fitting lock being held by a different
            // query. An existing immutable reach needs no access to that lock.
            lock(gate){query=Task.Run(()=>hydro.RiverReach(channel.From));finished=query.Wait(TimeSpan.FromSeconds(2));}
            var actual=query.GetAwaiter().GetResult();
            if(!finished)throw new Exception("Cached river query blocked behind unrelated fitting");
            if(!actual.SequenceEqual(expected))throw new Exception("Concurrent cached reach changed any span field");
            Parallel.For(0,16,_=>{if(!hydro.RiverReach(channel.From).SequenceEqual(expected))throw new Exception("Concurrent cache readers changed river geometry");});
            actual[0]=default;if(!hydro.RiverReach(channel.From).SequenceEqual(expected))throw new Exception("Public reach exposed mutable cache storage");
            count++;
        }
        if(count<8)throw new Exception("Insufficient actual river cache coverage");
        Console.WriteLine($"PASS {count} actual cached river reaches: query completes while unrelated fitting lock is held; 16 concurrent readers per reach preserve every span field; public copies remain isolated. This is a contention proof, not game FPS.");
    }
}
