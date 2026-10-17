using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Explicit diagnostic only: copies an already warmed private checkpoint,
// never a proposed actor/cell census in an ordinary turn.
public static class HistoryCopyCostAudit
{
    static MethodInfo CopyMethod(Type type,string name="Copy")=>type.GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,Type.EmptyTypes,null);
    static Func<object> Bind(object target,MethodInfo method)=>Expression.Lambda<Func<object>>(Expression.Convert(Expression.Call(Expression.Constant(target),method),typeof(object))).Compile();
    static Func<object> Cloner(object value)
    {
        if(value is Array array)
        {
            var element=array.GetType().GetElementType()!;var method=CopyMethod(element);
            if(method==null||array.Rank!=1)return element.IsValueType||element==typeof(string)?()=>array.Clone():null;
            var item=Expression.Parameter(typeof(object));var clone=Expression.Lambda<Func<object,object>>(Expression.Convert(Expression.Call(Expression.Convert(item,element),method),typeof(object)),item).Compile();
            return ()=>{var result=Array.CreateInstance(element,array.Length);for(int i=0;i<array.Length;i++){object row=array.GetValue(i);result.SetValue(row==null?null:clone(row),i);}return result;};
        }
        var copy=CopyMethod(value.GetType());return copy==null?null:Bind(value,copy);
    }
    public sealed record Row(string Name,int Slots,double Milliseconds,long AllocatedBytes);
    static Row Measure(string name,Func<object> clone,int slots)
    {
        GC.KeepAlive(clone());const int iterations=3;var watch=new Stopwatch();long before=GC.GetAllocatedBytesForCurrentThread();object latest=null;watch.Start();
        for(int i=0;i<iterations;i++)latest=clone();watch.Stop();long allocated=(GC.GetAllocatedBytesForCurrentThread()-before)/iterations;GC.KeepAlive(latest);
        return new Row(name,slots,watch.Elapsed.TotalMilliseconds/iterations,allocated);
    }
    public static void World(string source,string output)
    {
        if(File.Exists(output))throw new IOException("Do not overwrite copy evidence.");
        var world=WorldFile.Read(File.ReadAllBytes(source));var engine=new Prehistory(world);var session=new SimulationSession(engine,world.Prehistory);
        session.AdvanceSeconds(5*SimulationTime.Year,int.MaxValue);
        var state=(PrehistoryState)typeof(SimulationSession).GetField("current",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(session)!;var h=state.History;var a=h.Aggregates;string hash=state.Hash;
        var parts=new List<(string Name,Func<object> Clone,int Slots)>{("whole private checkpoint",Bind(state,CopyMethod(typeof(PrehistoryState),"CopyAggregateOwned")),0),("whole public publication",()=>session.Snapshot(),0)};
        void Add(string name,object value)
        {if(value==null)return;var clone=Cloner(value);if(clone!=null)parts.Add((name,clone,value is Array rows?rows.Length:0));}
        void Fields(string prefix,object value)
        {if(value==null)return;foreach(var field in value.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.Name,StringComparer.Ordinal))Add(prefix+field.Name,field.GetValue(value));}
        Add("Society inclusive",state.Society);Add("History inclusive",h);Add("Aggregates inclusive",a);Add("Dynamics inclusive",h.Dynamics);Add("Empires inclusive",a.Empires);Add("Connections inclusive",a.Empires.Connections);
        Fields("state.",state);Fields("history.",h);Fields("aggregate.",a);Fields("dynamics.",h.Dynamics);Fields("connections.",a.Empires.Connections);
        var rows=new List<Row>();foreach(var part in parts){var row=Measure(part.Name,part.Clone,part.Slots);rows.Add(row);Console.WriteLine(JsonSerializer.Serialize(row));}
        if(state.ComputeHash()!=hash||session.Hash!=hash)throw new Exception("Copy diagnosis changed actual private checkpoint values.");
        File.WriteAllText(output,JsonSerializer.Serialize(new{Year=state.Years,Hash=hash,Qualification="Shared CPU; allocations measured on this thread; inclusive components overlap. Array iteration adapter adds fixed overhead, not per-record reflection argument arrays.",Rows=rows},new JsonSerializerOptions{WriteIndented=true}));
    }
}
