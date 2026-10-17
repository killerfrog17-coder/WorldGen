using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Explicit read-only diagnostics: timings and byte counts never enter history.
public static class HistoryPrivateHashCostAudit
{
    static readonly MethodInfo WriteMethod=typeof(StateHash).GetMethod("Write",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(BinaryWriter),typeof(object)},null)!;
    static readonly MethodInfo OwnedMethod=typeof(StateHash).GetMethod("ComputeOwnedHistorical",BindingFlags.Static|BindingFlags.NonPublic)!;
    static readonly FieldInfo OwnedContext=typeof(StateHash).GetField("ownedHistoryContext",BindingFlags.Static|BindingFlags.NonPublic)!;
    static readonly MethodInfo ShaFactory=typeof(StateHash).Assembly.GetType("WorldGen.Core.CanonicalSha256")!.GetMethod("Create",BindingFlags.Static|BindingFlags.Public)!;
    static readonly byte[] RawBlock=Enumerable.Range(0,65536).Select(i=>(byte)(i*73+19)).ToArray();
    sealed class Counter:Stream
    {
        public long Bytes;
        public override void Write(byte[] b,int offset,int count){Bytes+=count;}
        public override void WriteByte(byte value){Bytes++;}
        public override void Flush(){}public override bool CanWrite=>true;public override bool CanRead=>false;public override bool CanSeek=>false;
        public override long Length=>Bytes;public override long Position{get=>Bytes;set=>throw new NotSupportedException();}
        public override int Read(byte[] b,int o,int n)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();
    }
    static long Serialize(object value,HistoryState history)
    {
        object previous=OwnedContext.GetValue(null);OwnedContext.SetValue(null,history);
        try
        {
            using var counter=new Counter();using(var buffer=new BufferedStream(counter,65536))using(var writer=new BinaryWriter(buffer,Encoding.UTF8,true))WriteMethod.Invoke(null,new[]{writer,value});
            return counter.Bytes;
        }
        finally{OwnedContext.SetValue(null,previous);}
    }
    static string Owned(object value,HistoryState history)=>(string)OwnedMethod.Invoke(null,new object[]{value,history,default(CancellationToken)})!;
    public static long WarmSerializationAllocated(object value,HistoryState history)
    {Serialize(value,history);long before=GC.GetAllocatedBytesForCurrentThread();Serialize(value,history);return GC.GetAllocatedBytesForCurrentThread()-before;}
    static void RawSha(long bytes)
    {
        using var sha=(SHA256)ShaFactory.Invoke(null,null)!;
        for(long remaining=bytes;remaining>0;){int count=(int)Math.Min(RawBlock.Length,remaining);sha.TransformBlock(RawBlock,0,count,null,0);remaining-=count;}
        sha.TransformFinalBlock(Array.Empty<byte>(),0,0);
    }
    public sealed record Row(string Name,long Bytes,double SerializeMs,long SerializeAllocated,double OwnedHashMs,long OwnedHashAllocated,double RawShaMs,long RawShaAllocated);
    static Row Measure(string name,object value,HistoryState history,int iterations)
    {
        long bytes=Serialize(value,history);string expected=Owned(value,history);
        (double Ms,long Allocated) Time(Action action)
        {long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();for(int i=0;i<iterations;i++)action();return(watch.Elapsed.TotalMilliseconds/iterations,(GC.GetAllocatedBytesForCurrentThread()-allocated)/iterations);}
        var encode=Time(()=>{if(Serialize(value,history)!=bytes)throw new Exception("Canonical length changed during read-only measurement.");});
        var hash=Time(()=>{if(Owned(value,history)!=expected)throw new Exception("Canonical hash changed during read-only measurement.");});
        var raw=Time(()=>RawSha(bytes));return new Row(name,bytes,encode.Ms,encode.Allocated,hash.Ms,hash.Allocated,raw.Ms,raw.Allocated);
    }
    static object[] RootValues(PrehistoryState s)
    {
        var h=s.History;var a=h.Aggregates;
        var values=new List<object>{s.Version,s.Species,s.WorldHash,s.Origin,s.Years,s.Started,s.Population,s.ArrivalYear,s.Ancestry,s.Society,h.Version,a,h.Cultures,h.Religions,h.Polities,h.Relations,h.Towns,h.Agriculture,h.Dynamics.States,h.Dynamics.Campaigns,h.Dynamics.Dependencies,h.Modern,h.OceanSettlements,h.Administration,s.Timeline};
        foreach(var extra in new object[]{h.Languages,h.OriginReach,h.Nuclear,h.International,h.ReceiverFrontier,h.RegionalTraditions})if(extra!=null)values.Add(extra);
        return values.ToArray();
    }
    public static void World(string source,string output)
    {
        if(File.Exists(output))throw new IOException("Do not overwrite a private-hash profile.");
        var world=WorldFile.Read(File.ReadAllBytes(source));var s=world.Prehistory.Copy();var h=s.History;var a=h.Aggregates;var c=a.Empires.Connections;
        var root=RootValues(s);if(Owned(root,h)!=s.Hash)throw new Exception("Diagnostic root layout differs from actual aggregate sealing; update diagnostics, not history.");
        var parts=new List<(string Name,object Value)>{("complete private seal",root),("society",s.Society),("aggregates (inclusive)",a),("cultures",h.Cultures),("religions",h.Religions),("polities",h.Polities),("relations",h.Relations),("towns",h.Towns),("agriculture",h.Agriculture),("state dynamics",h.Dynamics.States),("campaigns",h.Dynamics.Campaigns),("modern",h.Modern),("administration",h.Administration),("international",h.International),("timeline",s.Timeline),("aggregate cities",a.Cities),("aggregate governments",a.Polities),("aggregate regions",a.Regions),("imperial connections (inclusive)",c),("geographic governments",c.Geography.Governments),("territorial posts",a.Empires.Territories.Posts),("campaign theaters",a.Provinces.Theaters),("campaign progress",a.Communities.Campaigns),("military operations",c.Military?.Campaigns)};
        foreach(var part in new (string Name,object Value)[]{("population",s.Population),("arrival years",s.ArrivalYear),("ancestry",s.Ancestry),("dependencies",h.Dynamics.Dependencies),("ocean settlements",h.OceanSettlements),("languages",h.Languages),("origin reach",h.OriginReach),("nuclear",h.Nuclear),("receiver frontier",h.ReceiverFrontier),("regional traditions",h.RegionalTraditions)})if(part.Value!=null)parts.Add(part);
        foreach(var field in c.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.Name))if(field.GetValue(c)!=null)parts.Add(("connections."+field.Name,field.GetValue(c)!));
        var rows=new List<Row>();foreach(var part in parts){var row=Measure(part.Name,part.Value,h,3);rows.Add(row);Console.WriteLine(JsonSerializer.Serialize(row));}
        var parents=h.Dynamics.Campaigns;var peek=parents.GetType().GetMethod("Peek",BindingFlags.NonPublic|BindingFlags.Instance)!;int closed=0,mixed=0;
        for(int first=0;first+256<=parents.Length;first+=256){bool all=true;for(int i=first;i<first+256;i++)if(((HistoricalCampaign)peek.Invoke(parents,new object[]{i})!).EndedYear<0){all=false;break;}if(all)closed++;else mixed++;}
        var report=new{Year=s.Years,Hash=s.Hash,PrivateHash=Owned(root,h),Campaigns=parents.Length,FullyClosed256RowPages=closed,Mixed256RowPages=mixed,Rows=rows};
        File.WriteAllText(output,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));if(s.ComputeHash()!=s.Hash)throw new Exception("Read-only profiling changed authoritative state.");
    }
    public static void Synthetic()
    {
        const int count=12000;var context=new HistoryState{Dynamics=new HistoricalDynamics{Campaigns=Enumerable.Range(0,count).Select(i=>new HistoricalCampaign{Id=i,EndedYear=100,Name="Campaign "+i,Outcome="Recorded outcome",CapturedSettlements=new[]{i%3000}}).ToArray()}};
        var parts=new (string Name,object Value)[]{("campaigns",context.Dynamics.Campaigns.Copy()),("theaters",new HistoricalCampaignTheaterCollection(Enumerable.Range(0,count).Select(i=>new HistoricalCampaignTheater{Cell=i,SupplyA=i+1})).Copy()),("progress",new HistoricalCampaignAdvanceCollection(Enumerable.Range(0,count).Select(i=>new HistoricalCampaignAdvance{AttackerAreaKm2=i,DefenderAreaKm2=i+1})).Copy()),("military",new HistoricalMilitaryOperationCollection(Enumerable.Range(0,count).Select(i=>new HistoricalMilitaryOperation{Closed=true,PartyA=i,VictoriesA=i%7})).Copy()),("relations",new HistoricalRelationCollection(Enumerable.Range(0,count).Select(i=>new HistoricalRelation{A=i,B=i+1,Trade=true})).Copy())};
        foreach(var part in parts)Console.WriteLine(JsonSerializer.Serialize(Measure(part.Name,part.Value,context,10)));
    }
}
