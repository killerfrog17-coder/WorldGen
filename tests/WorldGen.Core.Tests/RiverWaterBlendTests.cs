using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class RiverWaterBlendTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        Span<RiverWaterBlend.Contribution> storage=stackalloc RiverWaterBlend.Contribution[2];var blend=new RiverWaterBlend(storage);
        try
        {
            Check(!blend.Sample().Present,"Empty channel support has no invented water");
            blend.Add(11,100,20,3,10,new V(1,0,0),2,.2);var first=blend.Sample();
            Check(first.Present&&first.Level==100&&first.Contributors==1,"One channel keeps its actual stage");
            blend.Add(11,100,20,3,10,new V(1,0,0),2,.2);
            Check(blend.Sample().Contributors==1&&blend.Sample().Level==100,"Repeated adjacent sections do not multiply one reach's influence");
            blend.Add(12,90,20,3,10,new V(0,1,0),2,.2);var mixed=blend.Sample();
            Check(mixed.Level>90&&mixed.Level<100&&mixed.Contributors==2,"Overlapping channels form one bounded intermediate surface");
            Check(mixed.Flow.X>0&&mixed.Flow.Y>0&&mixed.Speed<2,"Intersecting currents combine instead of retaining an arbitrary branch direction");
            blend.Add(12,80,20,3,15,new V(0,1,0),2,.2);
            Check(blend.Sample().Contributors==2&&blend.Sample().Level<mixed.Level,"A stronger section replaces rather than duplicates its reach");
            for(int i=0;i<40;i++)blend.Add(100+i,95+i*.01,5,1,3,new V(1,0,0),1,.1);
            Check(blend.Sample().Contributors==42&&double.IsFinite(blend.Sample().Level),"Crowded support preserves all channels beyond stack capacity");
            blend.Add(999,10000,10,1,0,new V(1,0,0),1,0);
            Check(blend.Sample().Contributors==42,"A channel vanishes continuously at its finite bank");
        }
        finally{blend.Dispose();}
        double previous=100;
        for(int i=0;i<=100;i++)
        {
            var local=new RiverWaterBlend(storage);
            try
            {
                local.Add(1,100,20,3,10,new V(1,0,0),2,.1);local.Add(2,90,20,3,i*.2,new V(0,1,0),2,.1);double h=local.Sample().Level;
                Check(h<=previous+1e-12&&h>=90&&h<=100,"Entering a tributary's support changes the common stage gradually and monotonically");previous=h;
            }
            finally{local.Dispose();}
        }
        Console.WriteLine($"PASS {checks} combined river water controls");
    }
    public static void World(string path,string output,int[] sources)
    {
        var world=WorldFile.ReadFile(path);var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);int probes=0,mixed=0,bad=0;double max=0;var failures=new List<object>();
        foreach(int source in sources)
        {
            var spans=terrain.Amplification.RiverReach(source);if(spans.Length==0)throw new Exception("Missing actual field-control reach "+source);
            foreach(var station in new[]{spans[0],spans[spans.Length/2],spans[^1]})
            {
                V at=(station.A+station.B).Unit;double extent=Math.Max(100,Math.Max(station.WidthA,station.WidthB)*2);var frame=new LocalTerrainFrame(at,terrain.RadiusM,0,.1);var patch=terrain.Patch(at,Math.Max(40000,extent*25));patch.RiverSpans();
                for(int z=-8;z<=8;z++)for(int x=-8;x<=8;x++)
                {
                    V p=frame.Position(x*extent/8,z*extent/8);var actual=patch.Geometry(p);var hydro=patch.RiverWater(p);
                    if(!hydro.Present||!double.IsFinite(actual.WaterSurfaceM))continue;probes++;if(hydro.Contributors>1)mixed++;
                    double delta=Math.Abs(hydro.Level-actual.WaterSurfaceM);max=Math.Max(max,delta);
                    if(delta>.00001){bad++;if(failures.Count<20)failures.Add(new{source,x,z,Direct=hydro.Level,Geometry=actual.WaterSurfaceM,Kind=actual.WaterKind.ToString(),hydro.Contributors});}
                }
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllText(output,JsonSerializer.Serialize(new{Version=LocalTerrain.Version,probes,mixed,bad,max,failures},new JsonSerializerOptions{WriteIndented=true}));
        if(bad>0||probes<100||mixed<10)throw new Exception($"Combined river field: {bad}/{probes} unequal queries, {mixed} actual mixed samples");
        Console.WriteLine($"PASS {probes} paired exact water queries, {mixed} actual overlap samples; max delta {max:R}");
    }
}
