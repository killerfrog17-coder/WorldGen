using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class RiverFittingParallelAudit
{
    static string Hash(LocalTerrainMesh mesh)
    {
        double elapsed=mesh.BuildMilliseconds;
        using var bytes=new MemoryStream();
        try{mesh.BuildMilliseconds=0;using(var writer=new BinaryWriter(bytes,System.Text.Encoding.UTF8,true))TerrainMeshWire.WriteMesh(writer,mesh);}
        finally{mesh.BuildMilliseconds=elapsed;}
        return Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
    }
    public static void Run(string input,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadFile(input);var timer=Stopwatch.StartNew();
        int samples=0,packets=0;var rows=new List<object>();
        try
        {
            TerrainQueryTiming.SerialRiverFitting=true;
            var literal=new LocalTerrain(world,horizontalScale:.1);double literalSeconds=timer.Elapsed.TotalSeconds;
            Console.WriteLine($"FITTING serial: {literalSeconds:F3}s, fit {literal.Amplification.RiverscapeFittingMilliseconds:F1}ms");
            TerrainQueryTiming.SerialRiverFitting=false;timer.Restart();
            var cursor=new LocalTerrain(world,horizontalScale:.1);double cursorSeconds=timer.Elapsed.TotalSeconds;
            Console.WriteLine($"FITTING parallel: {cursorSeconds:F3}s, fit {cursor.Amplification.RiverscapeFittingMilliseconds:F1}ms");
            var a=literal.Amplification.ExportRiverDatums();var b=cursor.Amplification.ExportRiverDatums();
            int unequal=Enumerable.Range(0,a.Length).Count(i=>BitConverter.DoubleToInt64Bits(a[i])!=BitConverter.DoubleToInt64Bits(b[i]));
            File.WriteAllText(Path.Combine(output,"preparation.json"),JsonSerializer.Serialize(new{Version=LocalTerrain.Version,LiteralSeconds=literalSeconds,ParallelSeconds=cursorSeconds,LiteralFittingMs=literal.Amplification.RiverscapeFittingMilliseconds,ParallelFittingMs=cursor.Amplification.RiverscapeFittingMilliseconds,Datums=a.Length,UnequalDatums=unequal,Scope="Same exact fine-resolution model, one fresh CoreCLR constructor per path, bounded independent worker caches; not native game startup/FPS"},new JsonSerializerOptions{WriteIndented=true}));
            if(unequal!=0)throw new Exception("Parallel fitting changed "+unequal+" complete graph datum bits");
            foreach(int source in new[]{143549,52127,136593})
            {
                TerrainQueryTiming.SerialRiverFitting=true;var reach=literal.Amplification.RiverReach(source);if(reach.Length==0)throw new Exception("Missing audit river "+source);
                V at=(reach[reach.Length/2].A+reach[reach.Length/2].B).Unit;
                var frame=new LocalTerrainFrame(at,literal.RadiusM,0,.1);
                var first=literal.Patch(at,40000);var second=cursor.Patch(at,40000);
                for(int z=0;z<24;z++)for(int x=0;x<24;x++)
                {
                    V p=frame.Position((x-12)*37.271,(z-12)*31.713);
                    TerrainQueryTiming.SerialRiverFitting=true;var expected=first.Sample(p);
                    TerrainQueryTiming.SerialRiverFitting=false;var actual=second.Sample(p);
                    if(!expected.Equals(actual))throw new Exception($"Parallel fitting changed full surface sample at {source}:{x},{z}");samples++;
                }
                // Reverse the path so neither test relies on one lucky leaf hint.
                for(int i=127;i>=0;i--)
                {
                    V p=frame.Position(Math.Sin(i*.53)*500,Math.Cos(i*.37)*500);
                    TerrainQueryTiming.SerialRiverFitting=true;var expected=first.Geometry(p);
                    TerrainQueryTiming.SerialRiverFitting=false;var actual=second.Geometry(p);
                    if(!expected.Equals(actual))throw new Exception("Reverse parallel changed canonical geometry at "+source);samples++;
                }
                foreach(var shape in new[]{(size:102.4,n:32),(size:409.6,n:16),(size:819.2,n:32)})
                {
                    TerrainQueryTiming.SerialRiverFitting=true;var old=LocalTerrainMesh.Build(literal,frame,-shape.size/2,-shape.size/2,shape.size,shape.n);
                    TerrainQueryTiming.SerialRiverFitting=false;var next=LocalTerrainMesh.Build(cursor,frame,-shape.size/2,-shape.size/2,shape.size,shape.n);
                    string oldHash=Hash(old),newHash=Hash(next);bool exact=oldHash==newHash;
                    rows.Add(new{Source=source,shape.size,shape.n,LiteralHash=oldHash,ParallelHash=newHash,Exact=exact,LiteralBuildMs=old.BuildMilliseconds,ParallelBuildMs=next.BuildMilliseconds});
                    File.WriteAllText(Path.Combine(output,"packets.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
                    if(!exact)throw new Exception("Parallel fitting changed complete terrain/water/material/cover packet at "+source);packets++;
                }
            }
            File.WriteAllText(Path.Combine(output,"checks.txt"),$"PASS every bit in {a.Length} graph datums, {samples} complete surface/geometry samples and {packets} complete terrain/water/material/cover packets. Normalize only BuildMilliseconds.\n");
            Console.WriteLine($"PASS {a.Length} graph datums, {samples} surface/geometry samples, {packets} complete wire packets EXACT");
        }
        finally{TerrainQueryTiming.SerialRiverFitting=false;}
    }
}
