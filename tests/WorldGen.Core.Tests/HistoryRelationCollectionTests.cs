using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryRelationCollectionTests
{
    static T Fork<T>(T value)=>(T)typeof(T).GetMethod("Fork",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(value,null)!;
    static byte[] Bytes(object value)
    {using var memory=new MemoryStream();using var writer=new BinaryWriter(memory,Encoding.UTF8,true);typeof(StateHash).GetMethod("Write",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new[]{writer,value});writer.Flush();return memory.ToArray();}
    static byte[] Literal(HistoricalRelation[] records)
    {
        using var memory=new MemoryStream();using var w=new BinaryWriter(memory,Encoding.UTF8,true);
        w.Write((byte)1);w.Write("WorldGen.Core.HistoricalRelation[]");
        foreach(var r in records)
        {
            w.Write((byte)1);w.Write((byte)(r==null?0:1));if(r==null)continue;w.Write("WorldGen.Core.HistoricalRelation");
            void I(string name,int v){w.Write(name);w.Write((byte)1);w.Write("System.Int32");w.Write(v);}
            void B(string name,bool v){w.Write(name);w.Write((byte)1);w.Write("System.Boolean");w.Write(v);}
            I("A",r.A);B("Alliance",r.Alliance);I("B",r.B);I("LastBattle",r.LastBattle);B("Trade",r.Trade);I("TruceUntil",r.TruceUntil);I("Trust",r.Trust);I("WarSince",r.WarSince);
        }
        w.Write((byte)0);w.Flush();return memory.ToArray();
    }
    static T ReadAs<T>(object value,Type original)
    {using var memory=new MemoryStream();new DataContractSerializer(original).WriteObject(memory,value);memory.Position=0;return (T)new DataContractSerializer(typeof(T)).ReadObject(memory)!;}
    public static void Run(Action<bool,string> check)
    {
        foreach(int count in new[]{0,1,255,256,257,1000})
        {
            var array=Enumerable.Range(0,count).Select(i=>i%13==0?null:new HistoricalRelation{A=i,B=i+1,Alliance=i%2==0,Trade=i%3==0,LastBattle=int.MinValue+i,Trust=int.MaxValue-i,WarSince=i-1,TruceUntil=i*7919}).ToArray();
            HistoricalRelationCollection records=array;
            check(Bytes(records).SequenceEqual(Literal(array)),"Paged relation canonical bytes independently match the complete legacy array framing and scalar field layout.");
            check(StateHash.Compute(records)==StateHash.ComputeHistoricalReference(array)&&StateHash.ComputeHistoricalReference(records)==StateHash.Compute(array),"Typed/reference relation hashes both retain every legacy byte.");
            check(WorldJson.Write(records)==WorldJson.Write(array),"Public JSON remains an ordinary relation array.");
            var restored=ReadAs<HistoricalRelationCollection>(array,typeof(HistoricalRelation[]));
            var oldReader=ReadAs<HistoricalRelation[]>(records,typeof(HistoricalRelationCollection));
            check(StateHash.Compute(restored)==StateHash.Compute(array)&&StateHash.Compute(oldReader)==StateHash.Compute(array),"Old array and new paged data-contract readers interoperate in both directions.");
            int[] ints=Enumerable.Range(0,count).Select(i=>i%2==0?int.MinValue+i:int.MaxValue-i).ToArray();HistoricalRelationCampaigns ids=ints;
            check(Bytes(ids).SequenceEqual(Bytes(ints))&&StateHash.ComputeHistoricalReference(ids)==StateHash.Compute(ints),"Aligned campaign IDs preserve the exact batched and reference int-array bytes.");
            check(WorldJson.Write(ids)==WorldJson.Write(ints)&&ReadAs<int[]>(ids,typeof(HistoricalRelationCampaigns)).SequenceEqual(ints)&&ReadAs<HistoricalRelationCampaigns>(ints,typeof(int[])).SequenceEqual(ints),"Campaign ID JSON and both old/new binary contracts retain ordinary integer arrays.");
        }
        var source=new HistoricalRelationCollection();var retained=new HistoricalRelation{A=1,B=2,Trust=17};source.Add(retained);
        var first=Fork(source);retained.Trust=19;check(first[0].Trust==17,"A retained input record cannot modify an already forked relation snapshot.");
        first[0].Trust=23;check(source[0].Trust==19,"A transaction getter materializes a persistent local record rather than leaking a shared mutable record.");
        var second=Fork(first);first[0].Trust=29;second[0].Trust=31;check(first[0].Trust==29&&second[0].Trust==31,"Both sides of repeated publication/transaction forks remain independently editable.");
        var assigned=new HistoricalRelation{A=3,B=4};first[0]=assigned;var assignedFork=Fork(first);assigned.Trust=43;check(assignedFork[0].Trust==0&&first[0].Trust==43,"Explicit index replacement also freezes retained external references across forks.");
        for(int i=1;i<257;i++)source.Add(new HistoricalRelation{A=i,B=i+1});var shared=Fork(source);source.Add(new HistoricalRelation{A=300,B=301});shared.Add(new HistoricalRelation{A=400,B=401});
        check(source.Length==258&&shared.Length==258&&source[257].A==300&&shared[257].A==400,"Appending independently to a shared partial page cannot overwrite or extend its sibling.");
        string before=StateHash.Compute(second);second[0].Trade=true;check(StateHash.Compute(second)!=before,"Public mutable relation edits remain visible to canonical integrity checks.");
        using(var iterator=shared.GetEnumerator()){int oldCount=shared.Length;shared.Add(new HistoricalRelation{A=500,B=501});int visited=0;while(iterator.MoveNext())visited++;check(visited==oldCount,"A pre-append enumerator retains the original array prefix.");}
        var large=new HistoricalRelationCollection();for(int i=0;i<100000;i++)large.Add(new HistoricalRelation{A=i,B=i+1});var frozen=Fork(large);
        long start=GC.GetAllocatedBytesForCurrentThread();var publication=Fork(frozen);long forkBytes=GC.GetAllocatedBytesForCurrentThread()-start;
        check(forkBytes<32768&&publication.Length==100000,"Publishing an untouched 100k-relation prefix copies only page references ("+forkBytes+" bytes).");
        start=GC.GetAllocatedBytesForCurrentThread();var last=publication[99999];long getterBytes=GC.GetAllocatedBytesForCurrentThread()-start;
        check(getterBytes<4096&&ReferenceEquals(last,publication[99999]),"First mutable access has constant allocation and retains one local record ("+getterBytes+" bytes).");
        var readers=new HistoricalRelation[32];Parallel.For(0,readers.Length,i=>readers[i]=publication[50000]);check(readers.All(r=>ReferenceEquals(r,readers[0])),"Concurrent public readers materialize exactly one local relation.");
        var local=typeof(HistoricalRelationCollection).GetField("materialized",BindingFlags.Instance|BindingFlags.NonPublic)!;int dirtyBefore=((System.Collections.IDictionary)local.GetValue(publication)!).Count;
        StateHash.Compute(publication);StateHash.ComputeHistoricalReference(publication);int dirtyAfter=((System.Collections.IDictionary)local.GetValue(publication)!).Count;
        check(dirtyBefore==dirtyAfter,"Trusted and reference canonical hashing never materialize the unaccessed relation prefix.");
        HistoricalRelationCampaigns campaigns=Enumerable.Repeat(-1,100000).ToArray();var cf=Fork(campaigns);campaigns[99999]=17;cf[0]=19;cf.Add(23);
        check(cf[99999]==-1&&campaigns[0]==-1&&campaigns.Length==100000&&cf.Length==100001,"Scalar page writes and appends detach in both directions after a campaign-ID fork.");
        start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++){frozen.Add(new HistoricalRelation{A=i,B=i+1});cf.Add(-1);}long appendBytes=GC.GetAllocatedBytesForCurrentThread()-start;
        check(appendBytes<1024*1024,"Appending 1000 pairs to a 100k prefix uses amortized page growth, not history-length array clones ("+appendBytes+" bytes).");
        var cancelled=Fork(frozen);cancelled[0].Trust=99;using(var token=new CancellationTokenSource()){token.Cancel();bool caught=false;try{StateHash.Compute(cancelled,token.Token);}catch(OperationCanceledException){caught=true;}check(caught&&frozen[0].Trust!=99,"A cancelled branch cannot leak mutable relation changes back to its source.");}
        string Owned(HistoricalRelationCollection rows)=>(string)typeof(StateHash).GetMethod("ComputeOwnedHistorical",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{rows,new HistoryState(),default(CancellationToken)})!;
        var cached=new HistoricalRelationCollection(new[]{new HistoricalRelation{A=1,B=2,Trust=17},new HistoricalRelation{A=3,B=4}});
        check(Owned(cached)==StateHash.Compute(cached)&&Owned(cached)==StateHash.ComputeHistoricalReference(cached),"Private relation-byte reuse preserves public and literal canonical hashes.");
        cached[0].Trust=27;check(Owned(cached)==StateHash.Compute(cached),"Every mutable record access invalidates private cached canonical bytes before the next owned checkpoint.");
        var held=cached[0];Owned(cached);held.Trust=31;
        check(StateHash.Compute(cached)==StateHash.ComputeHistoricalReference(cached),"Public integrity hashing observes edits through a retained reference even after private byte warming.");
        var detached=Fork(cached);check(Owned(detached)==StateHash.Compute(cached),"Forking re-reads and invalidates externally exposed records rather than carrying stale private bytes into simulation.");
        held.Trust=47;check(Owned(detached)!=StateHash.Compute(cached),"A later held-reference edit cannot mutate a fork's trusted record bytes.");
        var edit=detached[1];edit.Trade=true;check(Owned(detached)==StateHash.Compute(detached),"A fork's independently materialized edit invalidates only its own cached record.");
        Console.WriteLine("Relation pages: fork100k="+forkBytes+"B; first getter="+getterBytes+"B; append1000="+appendBytes+"B");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var original=w.Prehistory;var engine=new Prehistory(w);engine.Validate(original);string hash=original.ComputeHash();
        check(original.History.Relations.Length>1000,"The actual saved-world fixture exercises a substantial existing relation prefix.");
        var edited=original.Copy();var relation=edited.History.Relations[0];relation.Trust=relation.Trust==100?99:relation.Trust+1;
        check(edited.ComputeHash()!=hash&&original.ComputeHash()==hash,"An actual public relation edit is detected without corrupting its source checkpoint.");
        var bytes=WorldFile.Write(w);var restoredWorld=WorldFile.Read(bytes);var restored=restoredWorld.Prehistory;check(restored.Hash==hash&&restored.ComputeHash()==hash,"An old world reads and round-trips through array-compatible paged relations with exactly the same canonical hash.");
        var whole=engine.AdvanceYears(original,12);var cold=new Prehistory(restoredWorld);var parts=cold.AdvanceYears(cold.AdvanceYears(restored,5),7);engine.Validate(whole);cold.Validate(parts);
        check(whole.Hash==parts.Hash,"Actual relation pages replay identically after a cold save across12 versus5+7 years.");
        using(var token=new CancellationTokenSource()){token.Cancel();bool caught=false;try{engine.AdvanceYears(original,1,token.Token);}catch(OperationCanceledException){caught=true;}check(caught&&original.ComputeHash()==hash,"Cancelled actual playback leaves the loaded source and its lazy relation pages unchanged.");}
        check(original.ComputeHash()==hash,"Read, copy, serialization, continuation and cancellation preserve the original checkpoint.");
        Console.WriteLine("Paged world12 hash="+whole.Hash+"; relations="+original.History.Relations.Length+" -> "+whole.History.Relations.Length);
    }
}
