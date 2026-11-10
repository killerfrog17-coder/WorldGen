using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

// Run each side in a fresh process. This deliberately bypasses the disposable
// datum cache: it measures cold derivation, not a previously warmed world entry.
static class RiverColdPreparationAudit
{
    sealed class ModelContext(string directory) : AssemblyLoadContext(isCollectible:true)
    {
        protected override Assembly Load(AssemblyName name)
        {
            if(name.Name?.StartsWith("WorldGen.",StringComparison.Ordinal)!=true)return null;
            string path=Path.Combine(directory,name.Name+".dll");
            return File.Exists(path)?LoadFromAssemblyPath(path):null;
        }
    }
    public static void Run(string worldPath,string directory,string output)
    {
        directory=Path.GetFullPath(directory);
        var context=new ModelContext(directory);
        var core=context.LoadFromAssemblyPath(Path.Combine(directory,"WorldGen.Core.dll"));
        var serialization=context.LoadFromAssemblyPath(Path.Combine(directory,"WorldGen.Serialization.dll"));
        var file=serialization.GetType("WorldGen.Serialization.WorldFile",true)!;
        var read=file.GetMethods(BindingFlags.Public|BindingFlags.Static).Single(m=>m.Name=="ReadFile"&&m.GetParameters().Length==3);
        var watch=Stopwatch.StartNew();
        var world=read.Invoke(null,new object[]{Path.GetFullPath(worldPath),CancellationToken.None,null!});
        double readSeconds=watch.Elapsed.TotalSeconds;
        var type=core.GetType("WorldGen.Core.LocalTerrain",true)!;
        Console.WriteLine("Cold river preparation: "+type.GetField("Version")!.GetRawConstantValue());
        watch.Restart();
        var terrain=Activator.CreateInstance(type,new object[]{world!,CancellationToken.None,.1,null!})!;
        double seconds=watch.Elapsed.TotalSeconds;
        var amplification=type.GetField("Amplification")!.GetValue(terrain)!;
        var amp=amplification.GetType();
        double fitting=(double)amp.GetProperty("RiverscapeFittingMilliseconds")!.GetValue(amplification)!;
        int reaches=(int)amp.GetProperty("RiverscapeReachCount")!.GetValue(amplification)!;
        var result=new{Assembly=Path.Combine(directory,"WorldGen.Core.dll"),AssemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(core.Location))),Version=type.GetField("Version")!.GetRawConstantValue(),ReadSeconds=readSeconds,PreparationSeconds=seconds,FittingMs=fitting,Reaches=reaches,Scope="Cold CoreCLR constructor component only; no game startup/FPS claim"};
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(result));
    }
    public static void Contact(string worldPath,string directory,string output)
    {
        var context=new ModelContext(Path.GetFullPath(directory));
        var assembly=context.LoadFromAssemblyPath(typeof(RiverColdPreparationAudit).Assembly.Location);
        assembly.GetType("RiverBankContactAudit",true)!.GetMethod("Run",BindingFlags.Public|BindingFlags.Static)!
            .Invoke(null,new object[]{worldPath,output});
    }
}
