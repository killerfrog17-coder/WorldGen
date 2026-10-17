using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using System.IO.Compression;
using WorldGen.Core;
using WorldGen.Serialization;

// Exact legacy/current binary and JSON compatibility, not a physics test.
public static class HistoryTreeReferenceTests
{
    const string Contract="http://schemas.datacontract.org/2004/07/WorldGen.Core";
    [DataContract(Name="HistoricalCellTree",Namespace=Contract)] public sealed class LegacyTree
    {
        [DataMember(Name="root")] public LegacyNode Root;
        [DataMember(Name="length")] public int Length;
    }
    [DataContract(Name="HistoricalCellTree.Node",Namespace=Contract)] public sealed class LegacyNode
    {
        [DataMember] public LegacyNode[] Children;
        [DataMember] public int[] Values;
    }
    [DataContract] public sealed class CancelFixture
    {
        [DataMember] public HistoricalCellTree Tree;
        [OnDeserializing] void CancelRead(StreamingContext context){cancelProbe?.Cancel();}
        [OnSerializing] void CancelWrite(StreamingContext context){cancelWriteProbe?.Cancel();}
    }
    [ThreadStatic] static CancellationTokenSource cancelProbe;
    [ThreadStatic] static CancellationTokenSource cancelWriteProbe;
    // Direct malformed wire is independent of the production write guard.
    [DataContract(Name="HistoricalCellTree",Namespace=Contract)] public sealed class WireTree
    {
        [DataMember(Name="root")] public WireNode Root;
        [DataMember(Name="length")] public int Length;
    }
    [DataContract(Name="HistoricalCellTree.Node",Namespace=Contract,IsReference=true)] public sealed class WireNode
    {
        [DataMember] public WireNode[] Children;
        [DataMember] public int[] Values;
    }
    static readonly FieldInfo Root=typeof(HistoricalCellTree).GetField("root",BindingFlags.Instance|BindingFlags.NonPublic)!;
    static readonly Type Node=typeof(HistoricalCellTree).GetNestedType("Node",BindingFlags.NonPublic)!;
    static readonly FieldInfo Children=Node.GetField("Children",BindingFlags.Instance|BindingFlags.NonPublic)!;
    static readonly FieldInfo Values=Node.GetField("Values",BindingFlags.Instance|BindingFlags.NonPublic)!;
    static byte[] Write(object value,Type type,CancellationToken token=default)
    {using var stream=new MemoryStream();using var transport=HistoricalCellTree.CreateBinaryTransport(token);var serializer=new DataContractSerializer(type);serializer.SetSerializationSurrogateProvider(transport);using(var writer=XmlDictionaryWriter.CreateBinaryWriter(stream,null,null,false))serializer.WriteObject(writer,value);return stream.ToArray();}
    static int Compressed(byte[] bytes)
    {using var stream=new MemoryStream();using(var gzip=new GZipStream(stream,CompressionLevel.Optimal,true))gzip.Write(bytes);return checked((int)stream.Length);}
    static object Read(byte[] bytes,Type type,CancellationToken token=default)
    {using var scope=HistoricalCellTree.BeginReadInterning(token);using var transport=HistoricalCellTree.CreateBinaryTransport(token);var serializer=new DataContractSerializer(type);serializer.SetSerializationSurrogateProvider(transport);using var reader=XmlDictionaryReader.CreateBinaryReader(bytes,XmlDictionaryReaderQuotas.Max);return serializer.ReadObject(reader)!;}
    static LegacyNode CloneNode(object source,Dictionary<object,LegacyNode> seen)
    {
        if(source==null)return null;if(seen.TryGetValue(source,out var result))return result;
        result=new LegacyNode();seen.Add(source,result);var values=(int[])Values.GetValue(source);if(values!=null)result.Values=(int[])values.Clone();
        var children=(Array)Children.GetValue(source);if(children!=null){result.Children=new LegacyNode[children.Length];for(int k=0;k<children.Length;k++)result.Children[k]=CloneNode(children.GetValue(k),seen);}return result;
    }
    static LegacyTree Legacy(HistoricalCellTree tree,Dictionary<object,LegacyNode> seen)=>new LegacyTree{Length=tree.Length,Root=CloneNode(Root.GetValue(tree),seen)};
    static WireNode IdentityWire(object source,Dictionary<object,WireNode> seen)
    {
        if(source==null)return null;if(seen.TryGetValue(source,out var prior))return prior;
        var result=new WireNode{Values=(int[])Values.GetValue(source)};seen.Add(source,result);
        var children=(Array)Children.GetValue(source);if(children!=null){result.Children=new WireNode[children.Length];for(int i=0;i<children.Length;i++)result.Children[i]=IdentityWire(children.GetValue(i),seen);}return result;
    }
    static int At(LegacyTree tree,int index)
    {var node=tree.Root;for(int shift=20;shift>0;shift-=5)node=node?.Children[(index>>shift)&31];return node?.Values[index&31]??-1;}
    public static void Run(Action<bool,string> check)
    {
        var tree=new HistoricalCellTree(100000);for(int i=0;i<1000;i++)tree=tree.With(i*97,i);
        var variants=Enumerable.Range(0,128).Select(i=>tree.With(99999-i,i+17)).ToArray();var seen=new Dictionary<object,LegacyNode>();var legacy=variants.Select(t=>Legacy(t,seen)).ToArray();
        var oldBytes=Write(legacy,typeof(LegacyTree[]));var newBytes=Write(variants,typeof(HistoricalCellTree[]));
        Console.WriteLine($"Tree wire old={oldBytes.Length}, selective-reference={newBytes.Length}, ratio={(double)newBytes.Length/oldBytes.Length:F4}");
        check(newBytes.Length<oldBytes.Length/5,"Shared immutable subtrees are encoded once rather than expanded per government.");
        var current=(HistoricalCellTree[])Read(oldBytes,typeof(HistoricalCellTree[]));
        check(current.Length==variants.Length,"Reference-enabled reader accepts preceding by-value tree wire.");
        check(Write(current,typeof(HistoricalCellTree[])).Length<oldBytes.Length/5,"Read-time content interning recovers old by-value DAG sharing, not just existing references.");
        for(int k=0;k<variants.Length;k++)check(current[k].Fingerprint(true)==variants[k].Fingerprint(true),"Legacy tree values and exact canonical fingerprint survive new reader.");
        var roundtrip=(HistoricalCellTree[])Read(newBytes,typeof(HistoricalCellTree[]));
        for(int k=0;k<variants.Length;k++)check(roundtrip[k].Fingerprint(true)==variants[k].Fingerprint(true),"Selective-reference roundtrip preserves exact tree values/fingerprint.");
        try
        {
            var oldReader=(LegacyTree[])Read(newBytes,typeof(LegacyTree[]));bool correct=oldReader.Length==variants.Length;
            for(int k=0;k<oldReader.Length&&correct;k++)for(int i=0;i<1000;i++)correct&=At(oldReader[k],i*97)==i;
            Console.WriteLine("Old by-value reader of new selective-reference wire: "+(correct?"EXACT":"INCORRECT"));
            check(correct,"Old tree reader must retain every shared-node value from new wire.");
        }
        catch(Exception error){Console.WriteLine("OLD READER FAILED: "+error.GetType().Name+": "+error.Message);throw;}
        string original=tree.Fingerprint(true);var changed=roundtrip[0].With(0,42);
        check(changed[0]==42&&roundtrip[0][0]==0&&tree.Fingerprint(true)==original,"A loaded shared tree remains immutable under With and source forks.");
        using var cancel=new CancellationTokenSource();cancel.Cancel();bool canceled=false;try{WorldFile.Write(null,cancel.Token);}catch(OperationCanceledException){canceled=true;}
        check(canceled,"World save cancellation is honored before reading source state.");
        var bytesAgain=Write(roundtrip,typeof(HistoricalCellTree[]));Console.WriteLine($"Content-interned wire={bytesAgain.Length}");check(bytesAgain.Length<=newBytes.Length,"Sharing survives selective-reference save/load/save, with equal-content fresh nodes also deduplicated.");
        var roots=typeof(HistoricalCellTree).GetField("readScope",BindingFlags.Static|BindingFlags.NonPublic)!;
        check(roots.GetValue(null)==null,"The initial read scope is disposed without a process-wide pool.");
        using(var outer=HistoricalCellTree.BeginReadInterning())
        {
            var held=roots.GetValue(null);Read(oldBytes,typeof(HistoricalCellTree[]));
            check(ReferenceEquals(held,roots.GetValue(null)),"A nested synchronous read restores its preceding thread-local scope.");
            bool malformed=false;try{Read(new byte[]{0,1,2},typeof(HistoricalCellTree[]));}catch(Exception){malformed=true;}
            check(malformed&&ReferenceEquals(held,roots.GetValue(null)),"A malformed nested read restores the outer scope in finally.");
        }
        check(roots.GetValue(null)==null,"Disposing the outer scope leaves no thread-local world references.");
        var collisionA=new HistoricalCellTree(64).With(0,0).With(1,31);
        var collisionB=new HistoricalCellTree(64).With(0,1).With(1,0);
        var collisionLegacy=new[]{Legacy(collisionA,new Dictionary<object,LegacyNode>()),Legacy(collisionB,new Dictionary<object,LegacyNode>())};
        var collisions=(HistoricalCellTree[])Read(Write(collisionLegacy,typeof(LegacyTree[])),typeof(HistoricalCellTree[]));
        check(collisions[0][0]==0&&collisions[0][1]==31&&collisions[1][0]==1&&collisions[1][1]==0,"Equal structural hash codes never merge unequal leaf payloads.");
        check(collisions[0].Fingerprint(true)==collisionA.Fingerprint(true)&&collisions[1].Fingerprint(true)==collisionB.Fingerprint(true),"Structural hash collisions preserve both exact canonical fingerprints.");
        var tinyBytes=Write(new[]{Legacy(tree,new Dictionary<object,LegacyNode>())},typeof(LegacyTree[]));var concurrent=new object[8];
        Parallel.For(0,concurrent.Length,k=>
        {
            for(int retry=0;retry<3;retry++)
            {
                var copy=(HistoricalCellTree[])Read(tinyBytes,typeof(HistoricalCellTree[]));
                if(copy[0].Fingerprint(true)!=original||roots.GetValue(null)!=null)throw new Exception("A concurrent read leaked a scope or changed values.");
                concurrent[k]=Root.GetValue(copy[0]);
            }
        });
        check(concurrent.Distinct(ReferenceEqualityComparer.Instance).Count()==concurrent.Length,"Concurrent and repeated reads do not share process-global node pools.");
        using(var during=new CancellationTokenSource())
        {
            var fixture=Write(new CancelFixture{Tree=tree},typeof(CancelFixture));bool stopped=false;
            try{cancelProbe=during;Read(fixture,typeof(CancelFixture),during.Token);}catch(Exception error){for(var e=error;e!=null;e=e.InnerException)if(e is OperationCanceledException)stopped=true;}finally{cancelProbe=null;}
            check(stopped&&roots.GetValue(null)==null,"Cancellation during a node callback releases the entire scope and pool.");
        }
        var afterCancel=(HistoricalCellTree[])Read(tinyBytes,typeof(HistoricalCellTree[]));check(afterCancel[0].Fingerprint(true)==original,"A canceled prior read cannot contaminate a later successful read.");
        var json=WorldJson.Write(tree);var jsonTree=WorldJson.Read<HistoricalCellTree>(json);check(jsonTree.Fingerprint(true)==original,"Existing JSON tree transport remains compatible.");
        var legacyJson=WorldJson.Write(Legacy(tree,new Dictionary<object,LegacyNode>()));
        check(json==legacyJson,"JSON keeps the exact preceding by-value field layout and bytes.");
        var oldJsonReader=WorldJson.Read<LegacyTree>(json);check(At(oldJsonReader,97)==1&&At(oldJsonReader,99999)==-1,"The preceding JSON reader accepts current JSON without reference extensions.");
        var newJsonReader=WorldJson.Read<HistoricalCellTree>(legacyJson);check(newJsonReader.Fingerprint(true)==original,"The current JSON reader accepts preceding tree JSON unchanged.");
        check(Node.GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Select(f=>f.Name).OrderBy(x=>x).SequenceEqual(new[]{"Children","Digest","Values"}),"Logical simulation nodes keep their exact preceding allocation layout; temporary metadata belongs only to transport DTOs.");
        var cycle=Activator.CreateInstance(Node,true)!;var cycleChildren=Array.CreateInstance(Node,32);cycleChildren.SetValue(cycle,0);Children.SetValue(cycle,cycleChildren);var cycleTree=new HistoricalCellTree(32);Root.SetValue(cycleTree,cycle);
        bool cyclic=false;try{Write(cycleTree,typeof(HistoricalCellTree));}catch(Exception){cyclic=true;}
        check(cyclic,"Invalid cyclic logical source graphs are rejected by the bounded writer.");
        var wireCycle=new WireNode{Children=new WireNode[32]};wireCycle.Children[0]=wireCycle;
        cyclic=false;try{Read(Write(new WireTree{Length=32,Root=wireCycle},typeof(WireTree)),typeof(HistoricalCellTree));}catch(Exception){cyclic=true;}
        check(cyclic&&roots.GetValue(null)==null,"Direct cyclic wire is independently rejected by the reader without leaking its scope.");
        var wireDeep=new WireNode{Values=Enumerable.Repeat(-1,32).ToArray()};for(int i=0;i<5;i++){var next=new WireNode{Children=new WireNode[32]};next.Children[0]=wireDeep;wireDeep=next;}
        bool deep=false;try{Read(Write(new WireTree{Length=32,Root=wireDeep},typeof(WireTree)),typeof(HistoricalCellTree));}catch(Exception){deep=true;}
        check(deep&&roots.GetValue(null)==null,"Excessive radix depth is rejected from directly authored malformed wire, not merely blocked by the writer.");
        FreshWriteInterning(check);
        using(var transport=HistoricalCellTree.CreateBinaryTransport()){transport.Dispose();bool stopped=false;try{transport.GetObjectToSerialize(tree,typeof(HistoricalCellTree));}catch(ObjectDisposedException){stopped=true;}check(stopped,"A disposed binary provider cannot retain or reuse source graph state.");}
    }
    static void FreshWriteInterning(Action<bool,string> check)
    {
        // From allocates independent equal pages. Never read/intern these trees
        // before testing the first save, unlike an old-world rewrite fixture.
        var fresh=Enumerable.Range(0,32).Select(_=>HistoricalCellTree.From(32768,i=>unchecked(i*7919)^0x13579)).ToArray();
        check(fresh.Select(t=>Root.GetValue(t)).Distinct(ReferenceEqualityComparer.Instance).Count()==fresh.Length,"The first-save fixture starts with independent roots, not already interned or inherited pages.");
        var prior=fresh.Select(t=>t.Fingerprint(true)).ToArray();var identitySeen=new Dictionary<object,WireNode>();
        var identityOnly=fresh.Select(t=>new WireTree{Length=t.Length,Root=IdentityWire(Root.GetValue(t),identitySeen)}).ToArray();
        var oldBytes=Write(identityOnly,typeof(WireTree[]));var compact=Write(fresh,typeof(HistoricalCellTree[]));
        int oldZip=Compressed(oldBytes),newZip=Compressed(compact);
        Console.WriteLine($"Fresh duplicate first-save decoded {oldBytes.Length}->{compact.Length}; gzip {oldZip}->{newZip}.");
        check(compact.Length<oldBytes.Length/8&&newZip<oldZip,"Independent equal first-save pages collapse before serialization, reducing decoded and compressed payload without a prior read.");
        var current=(HistoricalCellTree[])Read(compact,typeof(HistoricalCellTree[]));var preceding=(LegacyTree[])Read(compact,typeof(LegacyTree[]));
        check(current.Select(t=>t.Fingerprint(true)).SequenceEqual(prior)&&fresh.Select(t=>t.Fingerprint(true)).SequenceEqual(prior),"Fresh first-save content interning preserves every exact tree fingerprint and leaves source pages unchanged.");
        check(preceding.Length==fresh.Length&&preceding.All(t=>Enumerable.Range(0,32768).All(i=>At(t,i)==fresh[0][i])),"The preceding by-value reader retains every value from fully equal shared-root wire.");
        var collisionA=new HistoricalCellTree(64).With(0,0).With(1,31);var collisionB=new HistoricalCellTree(64).With(0,1).With(1,0);
        string a=collisionA.Fingerprint(true),b=collisionB.Fingerprint(true);var digest=Node.GetField("Digest",BindingFlags.Instance|BindingFlags.NonPublic)!;
        digest.SetValue(Root.GetValue(collisionA),new byte[32]);digest.SetValue(Root.GetValue(collisionB),new byte[32]);
        var collided=(HistoricalCellTree[])Read(Write(new[]{collisionA,collisionB},typeof(HistoricalCellTree[])),typeof(HistoricalCellTree[]));
        check(collided[0].Fingerprint(true)==a&&collided[1].Fingerprint(true)==b&&a!=b,"Write-side structural collisions and identical stale digest caches never merge different literal leaf values.");
        var parallel=new byte[8][];Parallel.For(0,parallel.Length,i=>parallel[i]=Write(fresh,typeof(HistoricalCellTree[])));
        check(parallel.All(bytes=>bytes.SequenceEqual(compact)),"Concurrent scope-local write pools produce identical wire without a process-global intern table.");
        using(var cancel=new CancellationTokenSource())
        {
            bool stopped=false;try{cancelWriteProbe=cancel;Write(new CancelFixture{Tree=fresh[0]},typeof(CancelFixture),cancel.Token);}catch(Exception error){for(var inner=error;inner!=null;inner=inner.InnerException)if(inner is OperationCanceledException)stopped=true;}finally{cancelWriteProbe=null;}
            check(stopped,"Cancellation after serialization begins interrupts write-side node conversion.");
        }
        check(Write(fresh,typeof(HistoricalCellTree[])).SequenceEqual(compact),"A failed or canceled write cannot contaminate the next independent save.");
        using(var transport=HistoricalCellTree.CreateBinaryTransport())
        {
            transport.GetObjectToSerialize(Root.GetValue(fresh[0]),Node);transport.Dispose();
            int Count(string name){var pool=transport.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(transport)!;return (int)pool.GetType().GetProperty("Count")!.GetValue(pool)!;}
            check(Count("written")==0&&Count("writePool")==0,"Disposal releases both source-identity and content-pool references after a save.");
        }
    }
    public static void World(string source,string destination,int coldYears=0)
    {
        string expected=SaveAndContinue(source,destination,coldYears);
        if(coldYears>0)
        {
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            var restored=WorldFile.Read(File.ReadAllBytes(destination));string actual=Continue(restored,coldYears);
            if(actual!=expected)throw new Exception("Cold continuation changed under selective immutable-node transport: "+actual+" != "+expected);
            File.WriteAllText(destination+".cold-"+coldYears+".hash",actual);Console.WriteLine("COLD EXACT "+actual);
        }
    }
    static string Continue(World world,int years)=>new Prehistory(world).AdvanceYears(world.Prehistory,years).Hash;
    static string SaveAndContinue(string source,string destination,int coldYears)
    {
        if(File.Exists(destination))throw new IOException("Do not overwrite a save experiment.");var timer=Stopwatch.StartNew();var world=WorldFile.Read(File.ReadAllBytes(source));string hash=world.Prehistory.Hash;
        Console.WriteLine($"Old-world read/validated year={world.Prehistory.Years}, hash={hash}, seconds={timer.Elapsed.TotalSeconds:F3}");
        using(var cancel=new CancellationTokenSource())
        {
            cancel.CancelAfter(10);bool canceled=false;
            try{WorldFile.Write(world,cancel.Token);}catch(Exception error){for(var e=error;e!=null;e=e.InnerException)if(e is OperationCanceledException)canceled=true;}
            if(!canceled)throw new Exception("An actual canceled world write unexpectedly completed.");Console.WriteLine("MID-WRITE CANCELLATION PASS");
        }
        timer.Restart();var bytes=WorldFile.Write(world);File.WriteAllBytes(destination,bytes);
        Console.WriteLine($"Selective-reference write bytes={bytes.Length}, decoded={BitConverter.ToUInt32(bytes,bytes.Length-4)}, seconds={timer.Elapsed.TotalSeconds:F3}");
        if(world.Prehistory.ComputeHash()!=hash)throw new Exception("Serialization changed source history integrity.");
        Console.WriteLine("SOURCE EXACT "+hash);
        return coldYears>0?Continue(world,coldYears):hash;
    }
}
