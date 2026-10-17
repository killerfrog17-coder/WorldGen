using System.Reflection;
using System.Runtime.Serialization;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryLeaderCollectionTests
{
    static T ReadAs<T>(object source,Type type)
    {using var bytes=new MemoryStream();new DataContractSerializer(type).WriteObject(bytes,source);bytes.Position=0;return (T)new DataContractSerializer(typeof(T)).ReadObject(bytes)!;}
    public static void Run(Action<bool,string> check)
    {
        foreach(int count in new[]{0,1,255,256,257,1000})
        {
            var array=Enumerable.Range(0,count).Select(i=>i%17==0?null:new HistoricalLeader{Id=i,PolityId=i*3,PredecessorId=i%2==0?-1:i-1,Name="水 Ω "+i,Legacy=i%3==0?null:"Recorded reign",Selection=i%2==0?"":"Provincial council",Military=843,EndedYear=i%2==0?-1:i+100,StartedYear=i,Victories=i*7}).ToArray();
            HistoricalLeaderCollection rows=array;
            check(StateHash.Compute(rows)==StateHash.Compute(array)&&StateHash.ComputeHistoricalReference(rows)==StateHash.ComputeHistoricalReference(array),"Paged leader records retain every typed and literal array-framing byte.");
            check(WorldJson.Write(rows)==WorldJson.Write(array),"Leader JSON stays an ordinary chronological array.");
            check(StateHash.Compute(ReadAs<HistoricalLeaderCollection>(array,typeof(HistoricalLeader[])))==StateHash.Compute(array),"The new paged leader reader accepts the preceding saved-array contract.");
            check(StateHash.Compute(ReadAs<HistoricalLeader[]>(rows,typeof(HistoricalLeaderCollection)))==StateHash.Compute(array),"The preceding array reader accepts the new paged writer.");
        }
        var retained=new HistoricalLeader{Id=0,Name="First ruler",Renown=17};var source=new HistoricalLeaderCollection(new[]{retained});var first=source.Copy();retained.Renown=19;
        check(first[0].Renown==17&&source[0].Renown==19,"A retained original leader cannot mutate a published copy.");
        var second=first.Copy();first[0].Name="Left";second[0].Name="Right";check(first[0].Name=="Left"&&second[0].Name=="Right","Leader records detach independently on both branches.");
        var held=first[0];var heldFork=first.Copy();held.Name="Later edit";check(heldFork[0].Name=="Left","A reference obtained before a fork cannot later alter the fork's frozen leader.");
        var assigned=new HistoricalLeader{Name="Assigned"};first[0]=assigned;var assignment=first.Copy();assigned.Name="Changed";check(assignment[0].Name=="Assigned","Index assignment is isolated from a retained outside record.");
        for(int i=1;i<257;i++)source.Add(new HistoricalLeader{Id=i});var shared=source.Copy();source.Add(new HistoricalLeader{Name="Left append"});shared.Add(new HistoricalLeader{Name="Right append"});
        check(source.Length==258&&shared.Length==258&&source[257].Name=="Left append"&&shared[257].Name=="Right append","Partial-page appends preserve stable IDs on both branches.");
        using(var iterator=shared.GetEnumerator()){int old=shared.Length;shared.Add(new HistoricalLeader());int seen=0;while(iterator.MoveNext())seen++;check(seen==old,"An existing enumerator retains its pre-append prefix.");}
        var large=new HistoricalLeaderCollection();for(int i=0;i<100000;i++)large.Add(new HistoricalLeader{Id=i,EndedYear=100,Name="Recorded ruler"});var frozen=large.Copy();
        long start=GC.GetAllocatedBytesForCurrentThread();var publication=frozen.Copy();long copy=GC.GetAllocatedBytesForCurrentThread()-start;
        check(copy<32768&&publication.Length==100000,"An untouched100k-ruler publication copies page references rather than every dead ruler ("+copy+"B).");
        start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)publication.Add(new HistoricalLeader{Id=100000+i});long append=GC.GetAllocatedBytesForCurrentThread()-start;
        check(append<1024*1024,"A thousand appointments allocate bounded pages rather than a thousand lifetime-history arrays ("+append+"B).");
        var field=typeof(HistoricalLeaderCollection).GetField("materialized",BindingFlags.NonPublic|BindingFlags.Instance)!;
        var quiet=frozen.Copy();StateHash.Compute(quiet);StateHash.ComputeHistoricalReference(quiet);
        check(field.GetValue(quiet)==null,"Both canonical writers read leader records without materializing the archived prefix.");
        string hash=StateHash.Compute(second);var edit=second[0];edit.Victories++;check(StateHash.Compute(second)!=hash,"Public edits to recorded reigns remain detectable.");
        using(var token=new CancellationTokenSource()){token.Cancel();bool cancelled=false;try{StateHash.Compute(publication,token.Token);}catch(OperationCanceledException){cancelled=true;}check(cancelled,"Large paged historical hashing remains cancellable.");}
        Console.WriteLine($"Leader pages: fork100k={copy}B; append1000={append}B.");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string hash=original.Hash;var engine=new Prehistory(world);engine.Validate(original);
        check(original.ComputeHash()==hash&&original.History.Dynamics.Leaders.Length>1000,"An actual old dense world loads with its complete original leader integrity hash.");
        var changed=original.Copy();var leader=changed.History.Dynamics.Leaders[0];leader.Name+=" Edited";
        bool detected=false;try{changed.ComputeHash();}catch(ArgumentException){detected=true;}
        check(detected&&original.ComputeHash()==hash,"A publicly edited archived ruler is detected without corrupting the original history.");
        var restoredWorld=WorldFile.Read(WorldFile.Write(world));var restored=restoredWorld.Prehistory;
        check(restored.Hash==hash&&restored.ComputeHash()==hash,"Array-compatible leader pages preserve the actual full saved world.");
        var whole=engine.AdvanceYears(original,12);var cold=new Prehistory(restoredWorld);var parts=cold.AdvanceYears(cold.AdvanceYears(restored,5),7);
        engine.Validate(whole);cold.Validate(parts);check(whole.Hash==parts.Hash,"Real appointments and deaths replay exactly across12 versus5+7 cold saved years.");
        using(var token=new CancellationTokenSource()){token.Cancel();bool caught=false;try{engine.AdvanceYears(original,1,token.Token);}catch(OperationCanceledException){caught=true;}check(caught&&original.ComputeHash()==hash,"Cancelled actual playback cannot leak leader edits into the source.");}
        check(original.ComputeHash()==hash,"Copying, saving, continuing and cancellation leave the actual input untouched.");
        Console.WriteLine("Leader-page world12 "+whole.Hash+"; leaders="+original.History.Dynamics.Leaders.Length);
    }
}
