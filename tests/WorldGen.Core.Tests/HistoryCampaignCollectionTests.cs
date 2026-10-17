using System.Reflection;
using System.Runtime.Serialization;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryCampaignCollectionTests
{
    static T ReadAs<T>(object source,Type type)
    {using var bytes=new MemoryStream();new DataContractSerializer(type).WriteObject(bytes,source);bytes.Position=0;return (T)new DataContractSerializer(typeof(T)).ReadObject(bytes)!;}
    static int Exposed(object collection)
    {
        object rows=collection.GetType().GetField("rows",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(collection)!;
        return ((System.Collections.IDictionary)rows.GetType().GetField("materialized",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(rows))?.Count??0;
    }
    static void Wire<T,TCollection>(T[] array,Func<T[],TCollection> wrap,Action<bool,string> check) where T:class where TCollection:class
    {
        var rows=wrap(array);string literal=StateHash.ComputeHistoricalReference(array);
        check(StateHash.Compute(rows)==literal&&StateHash.ComputeHistoricalReference(rows)==literal,"Paged "+typeof(T).Name+" matches every independently reflected legacy array byte.");
        check(WorldJson.Write(rows)==WorldJson.Write(array),"Public "+typeof(T).Name+" JSON remains an ordinary array.");
        check(StateHash.Compute(WorldJson.Read<TCollection>(WorldJson.Write(array)))==literal&&StateHash.Compute(WorldJson.Read<T[]>(WorldJson.Write(rows)))==literal,"Old and new "+typeof(T).Name+" JSON readers interoperate in both directions.");
        check(StateHash.Compute(ReadAs<TCollection>(array,typeof(T[])))==literal,"New "+typeof(T).Name+" storage reads the preceding array contract.");
        check(StateHash.Compute(ReadAs<T[]>(rows,typeof(TCollection)))==literal,"The preceding "+typeof(T).Name+" array reader accepts the paged writer.");
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int count in new[]{0,1,255,256,257,513})
        {
            Wire(Enumerable.Range(0,count).Select(i=>i%17==0?null:new HistoricalCampaign{Id=i,A=i*2,B=i*2+1,Aim=(HistoricalWarAim)(i%6),EndedYear=i%2==0?-1:i,Context=i%3==0?null:i%3==1?"":"Local campaign Ω 水",CapturedSettlements=i%2==0?Array.Empty<int>():new[]{int.MinValue+i,int.MaxValue-i},CasualtiesA=long.MaxValue-i,Spoils=long.MinValue+i,Name="War "+i}).ToArray(),a=>new HistoricalCampaignCollection(a),check);
            Wire(Enumerable.Range(0,count).Select(i=>i%17==0?null:new HistoricalCampaignTheater{Cell=i,SupplyA=int.MinValue+i,SupplyB=int.MaxValue-i}).ToArray(),a=>new HistoricalCampaignTheaterCollection(a),check);
            Wire(Enumerable.Range(0,count).Select(i=>i%17==0?null:new HistoricalCampaignAdvance{AttackerAreaKm2=long.MinValue+i,DefenderAreaKm2=long.MaxValue-i}).ToArray(),a=>new HistoricalCampaignAdvanceCollection(a),check);
            Wire(Enumerable.Range(0,count).Select(i=>i%17==0?null:new HistoricalMilitaryOperation{Closed=i%2==0,PartyA=i,PartyB=i+1,CommittedA=int.MaxValue-i,OriginalCapitalA=int.MinValue+i,SiegeTarget=i%3-1,SiegeDays=i*7919,EpochA=i+17}).ToArray(),a=>new HistoricalMilitaryOperationCollection(a),check);
        }
        var retained=new HistoricalCampaign{Id=0,EndedYear=100,Outcome="Recorded",CapturedSettlements=new[]{17,19}};
        var source=new HistoricalCampaignCollection(new[]{retained});var first=source.Copy();retained.Outcome="Edited input";retained.CapturedSettlements[0]=23;
        check(first[0].Outcome=="Recorded"&&first[0].CapturedSettlements[0]==17&&source[0].CapturedSettlements[0]==23,"A retained initial record and nested captured-town array cannot mutate a campaign fork.");
        first[0].CapturedSettlements[1]=29;check(source[0].CapturedSettlements[1]==19,"First mutable campaign access detaches nested captured-town arrays on both branches.");
        var held=first[0];int[] heldTowns=held.CapturedSettlements;var second=first.Copy();heldTowns[0]=31;held.Outcome="Later edit";
        check(second[0].CapturedSettlements[0]==17&&second[0].Outcome=="Recorded","A nested array reference retained before a later fork cannot alter that fork.");
        var assigned=new HistoricalCampaign{CapturedSettlements=new[]{41}};first[0]=assigned;var assignment=first.Copy();assigned.CapturedSettlements[0]=43;
        check(assignment[0].CapturedSettlements[0]==41,"Index replacement also freezes a retained campaign's nested arrays.");
        var theater=new HistoricalCampaignTheaterCollection(new[]{new HistoricalCampaignTheater{Cell=5}});var theaterFork=theater.Copy();theater[0].SupplyA=9;theaterFork[0].SupplyB=11;
        check(theater[0].SupplyB==-1&&theaterFork[0].SupplyA==-1,"Theater sources detach independently, not just the main campaign row.");
        var advance=new HistoricalCampaignAdvanceCollection(new[]{new HistoricalCampaignAdvance{AttackerAreaKm2=17}});var advanceHeld=advance[0];var advanceFork=advance.Copy();advanceHeld.AttackerAreaKm2=19;
        check(advanceFork[0].AttackerAreaKm2==17,"A retained community-area sidecar cannot change a fork.");
        var operations=new HistoricalMilitaryOperationCollection(new[]{new HistoricalMilitaryOperation{CommittedA=23}});var opHeld=operations[0];var opFork=operations.Copy();opHeld.CommittedA=29;opFork[0].CommittedB=31;
        check(opFork[0].CommittedA==23&&operations[0].CommittedB==0,"Actual commitment sidecars detach on both publication and transaction branches.");
        string Owned(HistoricalCampaignCollection rows)=>(string)typeof(StateHash).GetMethod("ComputeOwnedHistorical",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{rows,new HistoryState(),default(CancellationToken)})!;
        string before=StateHash.Compute(second);Owned(second);var edit=second[0];Owned(second);edit.CapturedSettlements[0]=47;
        check(StateHash.Compute(second)!=before&&StateHash.Compute(second)==StateHash.ComputeHistoricalReference(second),"Public hashing detects nested edits even after warming closed-campaign canonical bytes.");
        var detached=second.Copy();check(Owned(detached)==StateHash.Compute(second),"A fork invalidates stale private canonical bytes on externally exposed campaign records.");
        edit.CapturedSettlements[0]=53;check(Owned(detached)!=StateHash.Compute(second),"A retained nested-array edit cannot affect the fork's private cached campaign bytes.");
        for(int i=1;i<257;i++)source.Add(new HistoricalCampaign{Id=i});var sibling=source.Copy();source.Add(new HistoricalCampaign{Name="Left"});sibling.Add(new HistoricalCampaign{Name="Right"});
        check(source.Length==258&&sibling.Length==258&&source[257].Name=="Left"&&sibling[257].Name=="Right","Campaign appends after a shared partial page preserve both prefixes and stable identities.");
        using(var iterator=sibling.GetEnumerator()){int count=sibling.Length;sibling.Add(new HistoricalCampaign());int seen=0;while(iterator.MoveNext())seen++;check(seen==count,"A pre-append campaign iterator retains its old array prefix.");}
        var large=new HistoricalCampaignCollection();var theaters=new HistoricalCampaignTheaterCollection();var advances=new HistoricalCampaignAdvanceCollection();var military=new HistoricalMilitaryOperationCollection();
        for(int i=0;i<100000;i++){large.Add(new HistoricalCampaign{Id=i,EndedYear=100});theaters.Add(new HistoricalCampaignTheater{Cell=i});advances.Add(new HistoricalCampaignAdvance());military.Add(new HistoricalMilitaryOperation{Closed=true});}
        large=large.Copy();theaters=theaters.Copy();advances=advances.Copy();military=military.Copy();
        long bytes=GC.GetAllocatedBytesForCurrentThread();var c=large.Copy();var t=theaters.Copy();var a=advances.Copy();var m=military.Copy();long fork=GC.GetAllocatedBytesForCurrentThread()-bytes;
        check(fork<65536&&c.Length==100000&&t.Length==c.Length&&a.Length==c.Length&&m.Length==c.Length,"Publishing four untouched100k-campaign histories copies page references, not all archived rows ("+fork+"B).");
        StateHash.Compute(c);StateHash.ComputeHistoricalReference(c);StateHash.Compute(t);StateHash.Compute(a);StateHash.Compute(m);
        check(Exposed(c)==0&&Exposed(t)==0&&Exposed(a)==0&&Exposed(m)==0,"Typed and reference canonical hashing do not materialize any of the four archived campaign prefixes.");
        var readers=new HistoricalCampaign[32];Parallel.For(0,readers.Length,i=>readers[i]=c[50000]);check(readers.All(row=>ReferenceEquals(row,readers[0])),"Concurrent readers materialize one isolated campaign record.");
        bytes=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++){c.Add(new HistoricalCampaign{Id=100000+i});t.Add(new HistoricalCampaignTheater());a.Add(new HistoricalCampaignAdvance());m.Add(new HistoricalMilitaryOperation());}long append=GC.GetAllocatedBytesForCurrentThread()-bytes;
        check(append<2*1024*1024&&c.Length==t.Length&&t.Length==a.Length&&a.Length==m.Length,"A thousand aligned campaign appends allocate bounded pages instead of four lifetime arrays ("+append+"B).");
        var cancelled=c.Copy();cancelled[0].CapturedSettlements=new[]{71};using(var token=new CancellationTokenSource()){token.Cancel();bool caught=false;try{StateHash.Compute(cancelled,token.Token);}catch(OperationCanceledException){caught=true;}check(caught&&c[0].CapturedSettlements.Length==0,"Cancelled hashing cannot leak a nested mutable campaign edit into its source.");}
        Console.WriteLine("Campaign family: fork4x100k="+fork+"B; append4x1000="+append+"B.");
    }
    public static void World(string path,Action<bool,string> check,string savedRoundtrip=null)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var original=w.Prehistory;string hash=original.Hash;var e=new Prehistory(w);e.Validate(original);
        check(original.ComputeHash()==hash&&original.History.Dynamics.Campaigns.Length>1000,"An actual old dense world retains its original campaign integrity bytes.");
        var privateRows=original.History.Dynamics.Campaigns.Copy().Copy();var publication=original.Copy();publication.History.Dynamics.Campaigns=privateRows.Copy();
        var ownedMethod=typeof(StateHash).GetMethod("ComputeOwnedHistorical",BindingFlags.NonPublic|BindingFlags.Static)!;string publicHash="";
        Parallel.Invoke(()=>{for(int i=0;i<8;i++)ownedMethod.Invoke(null,new object[]{privateRows,new HistoryState(),default(CancellationToken)});},()=>publicHash=publication.ComputeHash());
        check(publicHash==hash,"A public full integrity check can run concurrently with owned hashing of shared immutable campaign pages.");
        var changed=original.Copy();changed.History.Dynamics.Campaigns[0].Outcome+=" Edited";
        check(changed.ComputeHash()!=hash&&original.ComputeHash()==hash,"A public edit to an actual archived campaign is detected and source-isolated.");
        changed=original.Copy();changed.History.Aggregates.Provinces.Theaters[0].SupplyA++;
        check(changed.ComputeHash()!=hash&&original.ComputeHash()==hash,"A public edit to an actual historical theater remains detectable.");
        changed=original.Copy();changed.History.Aggregates.Communities.Campaigns[0].AttackerAreaKm2++;
        check(changed.ComputeHash()!=hash&&original.ComputeHash()==hash,"A public edit to an actual historical community sidecar remains detectable.");
        if(original.History.Aggregates.Empires.Connections.Military!=null)
        {
            changed=original.Copy();changed.History.Aggregates.Empires.Connections.Military.Campaigns[0].CommittedA++;
            check(changed.ComputeHash()!=hash&&original.ComputeHash()==hash,"A public edit to an actual closed military operation remains detectable.");
        }
        var saved=WorldFile.Write(w);
        if(savedRoundtrip!=null){if(File.Exists(savedRoundtrip))throw new IOException("Refusing to overwrite a campaign-wire fixture.");File.WriteAllBytes(savedRoundtrip,saved);}
        var restoredWorld=WorldFile.Read(saved);var restored=restoredWorld.Prehistory;
        check(restored.Hash==hash&&restored.ComputeHash()==hash,"The old full world round-trips through all four preceding array wire contracts exactly.");
        var whole=e.AdvanceYears(original,12);var cold=new Prehistory(restoredWorld);var parts=cold.AdvanceYears(cold.AdvanceYears(restored,5),7);e.Validate(whole);cold.Validate(parts);
        check(whole.Hash==parts.Hash,"Actual campaigns and all sidecars replay identically across cold12 versus5+7years.");
        using(var token=new CancellationTokenSource()){token.Cancel();bool caught=false;try{e.AdvanceYears(original,1,token.Token);}catch(OperationCanceledException){caught=true;}check(caught&&original.ComputeHash()==hash,"Cancelled real playback cannot corrupt any of the caller's campaign collections.");}
        check(original.ComputeHash()==hash,"All four old campaign collections preserve the caller across copy/save/continuation checks.");
        Console.WriteLine("Campaign pages world12="+whole.Hash+"; campaigns="+original.History.Dynamics.Campaigns.Length);
    }
}
