using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
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
    }
    [ThreadStatic] static CancellationTokenSource cancelProbe;
    static readonly FieldInfo Root=typeof(HistoricalCellTree).GetField("root",BindingFlags.Instance|BindingFlags.NonPublic)!;
    static readonly Type Node=typeof(HistoricalCellTree).GetNestedType("Node",BindingFlags.NonPublic)!;
    static readonly FieldInfo Children=Node.GetField("Children",BindingFlags.Instance|BindingFlags.NonPublic)!;
    static readonly FieldInfo Values=Node.GetField("Values",BindingFlags.Instance|BindingFlags.NonPublic)!;
    static byte[] Write(object value,Type type)
    {using var stream=new MemoryStream();using var transport=HistoricalCellTree.CreateBinaryTransport();var serializer=new DataContractSerializer(type);serializer.SetSerializationSurrogateProvider(transport);using(var writer=XmlDictionaryWriter.CreateBinaryWriter(stream,null,null,false))serializer.WriteObject(writer,value);return stream.ToArray();}
    static object Read(byte[] bytes,Type type,CancellationToken token=default)
    {using var scope=HistoricalCellTree.BeginReadInterning(token);using var transport=HistoricalCellTree.CreateBinaryTransport(token);var serializer=new DataContractSerializer(type);serializer.SetSerializationSurrogateProvider(transport);using var reader=XmlDictionaryReader.CreateBinaryReader(bytes,XmlDictionaryReaderQuotas.Max);return serializer.ReadObject(reader)!;}
    static LegacyNode CloneNode(object source,Dictionary<object,LegacyNode> seen)
    {
        if(source==null)return null;if(seen.TryGetValue(source,out var result))return result;
        result=new LegacyNode();seen.Add(source,result);var values=(int[])Values.GetValue(source);if(values!=null)result.Values=(int[])values.Clone();
        var children=(Array)Children.GetValue(source);if(children!=null){result.Children=new LegacyNode[children.Length];for(int k=0;k<children.Length;k++)result.Children[k]=CloneNode(children.GetValue(k),seen);}return result;
    }
    static LegacyTree Legacy(HistoricalCellTree tree,Dictionary<object,LegacyNode> seen)=>new LegacyTree{Length=tree.Length,Root=CloneNode(Root.GetValue(tree),seen)};
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
        bool cyclic=false;try{Read(Write(cycleTree,typeof(HistoricalCellTree)),typeof(HistoricalCellTree));}catch(Exception){cyclic=true;}
        check(cyclic&&roots.GetValue(null)==null,"Invalid cyclic source graphs cannot escape a bounded read or leak a scope.");
        using(var transport=HistoricalCellTree.CreateBinaryTransport()){transport.Dispose();bool stopped=false;try{transport.GetObjectToSerialize(tree,typeof(HistoricalCellTree));}catch(ObjectDisposedException){stopped=true;}check(stopped,"A disposed binary provider cannot retain or reuse source graph state.");}
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
