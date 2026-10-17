using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryHashCostAudit
{
    public static void Run(string path,string output)
    {
        if(File.Exists(output))throw new ArgumentException("Refusing to overwrite a hash audit.");
        var world=WorldFile.Read(File.ReadAllBytes(path));var s=world.Prehistory;string original=s.Hash;var h=s.History;var a=h.Aggregates;
        var owned=typeof(StateHash).GetMethod("ComputeOwnedHistorical",BindingFlags.Static|BindingFlags.NonPublic)!;
        var measurements=new List<object>();
        void Measure(string name,object value)
        {
            long before=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();string hash=(string)owned.Invoke(null,new object[]{value,h,default(CancellationToken)})!;watch.Stop();long first=GC.GetAllocatedBytesForCurrentThread()-before;
            before=GC.GetAllocatedBytesForCurrentThread();var warm=Stopwatch.StartNew();string repeated=(string)owned.Invoke(null,new object[]{value,h,default(CancellationToken)})!;warm.Stop();
            if(hash!=repeated)throw new Exception("Hash audit changed canonical result.");measurements.Add(new{Name=name,FirstMilliseconds=watch.Elapsed.TotalMilliseconds,WarmMilliseconds=warm.Elapsed.TotalMilliseconds,FirstAllocatedBytes=first,WarmAllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-before});
        }
        Measure("Society",s.Society);Measure("HistoryCultures",h.Cultures);Measure("HistoricalTowns",h.Towns);Measure("Agriculture",h.Agriculture);Measure("AggregateCities",a.Cities);Measure("AggregateRegions",a.Regions);Measure("ImperialSystem",a.Empires);Measure("ColonialConnections",a.Empires.Connections);
        foreach(var field in typeof(HistoricalColonialConnections).GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.Name,StringComparer.Ordinal))Measure("Connections."+field.Name,field.GetValue(a.Empires.Connections)!);
        foreach(var field in typeof(HistoricalImperialSystem).GetFields(BindingFlags.Public|BindingFlags.Instance).Where(f=>f.Name!="Connections").OrderBy(f=>f.Name,StringComparer.Ordinal))Measure("Empire."+field.Name,field.GetValue(a.Empires)!);
        foreach(var field in typeof(HistoricalTerritorialAdministration).GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.Name,StringComparer.Ordinal))Measure("Territories."+field.Name,field.GetValue(a.Empires.Territories)!);
        var dirty=a.Cells;for(int k=0;k<1024;k++){int at=(int)((long)k*7919%dirty.Length);dirty=dirty.With(at,dirty[at]+1);}Measure("SyntheticDirtyCellTree1024Edits",dirty);
        var seal=typeof(PrehistoryState).GetMethod("SealAggregateOwned",BindingFlags.NonPublic|BindingFlags.Instance)!;
        for(int i=0;i<3;i++){long before=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();seal.Invoke(s,new object[]{default(CancellationToken)});watch.Stop();measurements.Add(new{Name="WholeOwnedHash"+i,WarmMilliseconds=watch.Elapsed.TotalMilliseconds,WarmAllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-before});if(s.Hash!=original)throw new Exception("Owned hash differs from validated input.");}
        if(s.ComputeHash()!=original)throw new Exception("Hash audit modified the checkpoint.");
        File.WriteAllText(output,JsonSerializer.Serialize(new{Year=s.Years,Hash=original,Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,Warning="Offline.NETsubtree attribution, not native Mono timings. Parent/subtree measurements overlap; allocation counters apply only to this thread.",RecordedCities=a.Cities.Length,LivingCities=a.Living.Cities.Count(c=>c.AbandonedYear<0),Cultures=h.Cultures.Length,Polities=h.Polities.Length,Campaigns=h.Dynamics.Campaigns.Length,Measurements=measurements},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));Console.WriteLine(output);
    }
}
