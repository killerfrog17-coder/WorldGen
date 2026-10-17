using System.Diagnostics;
using System.Reflection;
using WorldGen.Core;

public static class HistoryCampaignSidecarCacheTests
{
    static readonly MethodInfo OwnedMethod=typeof(StateHash).GetMethod("ComputeOwnedHistorical",BindingFlags.Static|BindingFlags.NonPublic)!;
    static string Owned(object value,HistoryState context)=>(string)OwnedMethod.Invoke(null,new object[]{value,context,default(CancellationToken)})!;
    static object Peek(object rows,int index)=>rows.GetType().GetMethod("Peek",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(rows,new object[]{index})!;
    static byte[] Memo(object row)=>(byte[])row.GetType().GetField("OwnedCanonicalRecord",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(row)!;
    static HistoryState Context(int count)=>new HistoryState{Dynamics=new HistoricalDynamics{Campaigns=Enumerable.Range(0,count).Select(i=>new HistoricalCampaign{Id=i,EndedYear=i==1?-1:100}).ToArray()}};
    static void Family<C,R>(C initial,Func<C,C> copy,Action<R,int> mutate,Action<C,R> replace,Func<R,R> clone,Action<bool,string> check) where C:class,IReadOnlyList<R> where R:class
    {
        string name=typeof(R).Name;var context=Context(initial.Count);var rows=copy(initial);string before=StateHash.Compute(rows);
        check(Owned(rows,context)==before&&before==StateHash.ComputeHistoricalReference(rows),name+": first private bytes equal public and literal bytes.");
        check(Memo(Peek(rows,0))!=null&&Memo(Peek(rows,1))==null,name+": only a matching closed parent is cached.");
        check(Owned(rows,context)==before,name+": warm private reuse preserves identical bytes.");
        var held=rows[0];check(Memo(held)==null,name+": every mutable public access invalidates bytes.");
        Owned(rows,context);mutate(held,7919);
        string changed=StateHash.Compute(rows);check(changed!=before&&changed==StateHash.ComputeHistoricalReference(rows),name+": public hashing reads changes through a reference retained before a cache refill.");
        var branch=copy(rows);string isolated=Owned(branch,context);
        check(isolated==changed,name+": Fork detaches and invalidates externally exposed records.");
        mutate(held,7927);check(Owned(branch,context)==isolated&&StateHash.Compute(rows)!=isolated,name+": a retained mutable row cannot change a fork's private bytes.");
        var assigned=clone(held);replace(rows,assigned);check(Memo(assigned)==null,name+": index replacement invalidates memo bytes.");
        var assignment=copy(rows);string fixedAssignment=Owned(assignment,context);mutate(assigned,7933);
        check(Owned(assignment,context)==fixedAssignment,name+": replacement records retained by callers are detached at Fork.");
        var active=rows[1];string oldActive=Owned(rows,context);mutate(active,7949);check(Owned(rows,context)!=oldActive&&Memo(active)==null,name+": an active parent's row always reads current fields.");
        var openContext=Context(rows.Count);openContext.Dynamics.Campaigns[0].EndedYear=-1;
        check(Owned(rows,openContext)==StateHash.Compute(rows),name+": cached data from another context is not trusted for an active parent.");
        var frozen=copy(rows);string expected=Owned(frozen,context);var publication=copy(frozen);
        var concurrent=Task.Run(()=>{for(int i=0;i<16;i++)if(StateHash.Compute(publication)!=StateHash.ComputeHistoricalReference(publication))throw new Exception("Public sidecar bytes differ.");});
        for(int i=0;i<16;i++)check(Owned(frozen,context)==expected,name+": concurrent public hashing cannot clear shared frozen-row cache.");concurrent.GetAwaiter().GetResult();
        check(Owned(frozen,context)==expected,name+": private bytes remain exact after concurrent public integrity checks.");
    }
    public static void Run(Action<bool,string> check)
    {
        Family<HistoricalCampaignTheaterCollection,HistoricalCampaignTheater>(new HistoricalCampaignTheaterCollection(new[]{new HistoricalCampaignTheater{Cell=17},new HistoricalCampaignTheater{SupplyA=23},new HistoricalCampaignTheater{SupplyB=29}}),x=>x.Copy(),(x,n)=>x.Cell=n,(x,row)=>x[0]=row,x=>x.Copy(),check);
        Family<HistoricalCampaignAdvanceCollection,HistoricalCampaignAdvance>(new HistoricalCampaignAdvanceCollection(new[]{new HistoricalCampaignAdvance{AttackerAreaKm2=long.MaxValue},new HistoricalCampaignAdvance{DefenderAreaKm2=long.MinValue},new HistoricalCampaignAdvance{AttackerAreaKm2=17}}),x=>x.Copy(),(x,n)=>x.AttackerAreaKm2=n,(x,row)=>x[0]=row,x=>x.Copy(),check);
        Family<HistoricalMilitaryOperationCollection,HistoricalMilitaryOperation>(new HistoricalMilitaryOperationCollection(new[]{new HistoricalMilitaryOperation{Closed=true,PartyA=17,MarchDaysA=int.MaxValue},new HistoricalMilitaryOperation{PartyB=23},new HistoricalMilitaryOperation{Closed=true,SiegeDays=29}}),x=>x.Copy(),(x,n)=>x.PartyA=n,(x,row)=>x[0]=row,x=>x.Copy(),check);
        check(HistoricalTreeTransportDiagnostics.Verify().StartsWith("PASS 12 "),"The actual Core native binary/legacy/JSON/cancellation probe passes.");
        var context=Context(12000);context.Dynamics.Campaigns[1].EndedYear=100;var campaigns=context.Dynamics.Campaigns.Copy();
        var relations=new HistoricalRelationCollection(Enumerable.Range(0,12000).Select(i=>new HistoricalRelation{A=i,B=i+1})).Copy();
        long campaignBytes=HistoryPrivateHashCostAudit.WarmSerializationAllocated(campaigns,context),relationBytes=HistoryPrivateHashCostAudit.WarmSerializationAllocated(relations,context);
        check(campaignBytes<100000,"Warm12000-campaign serialization does not allocate per-record capture closures ("+campaignBytes+"B).");
        check(relationBytes<100000,"Warm12000-relation serialization does not allocate per-record capture closures ("+relationBytes+"B).");
        check(Owned(campaigns,context)==StateHash.ComputeHistoricalReference(campaigns)&&Owned(relations,context)==StateHash.ComputeHistoricalReference(relations),"Closure-free warm loops retain literal canonical bytes.");
        var publicCopy=campaigns.Copy();var heldCampaign=publicCopy[0];string old=StateHash.Compute(publicCopy);Owned(publicCopy,context);heldCampaign.Outcome="Retained mutation";
        check(StateHash.Compute(publicCopy)!=old&&StateHash.Compute(publicCopy)==StateHash.ComputeHistoricalReference(publicCopy),"Closure-free cached campaigns still detect retained-reference public edits.");
    }
    public static void Benchmark()
    {
        const int count=12000,iterations=30;var context=Context(count);context.Dynamics.Campaigns[1].EndedYear=100;
        var theaters=new HistoricalCampaignTheaterCollection(Enumerable.Range(0,count).Select(i=>new HistoricalCampaignTheater{Cell=i,SupplyA=i+1})).Copy();
        var progress=new HistoricalCampaignAdvanceCollection(Enumerable.Range(0,count).Select(i=>new HistoricalCampaignAdvance{AttackerAreaKm2=i,DefenderAreaKm2=i+1})).Copy();
        var military=new HistoricalMilitaryOperationCollection(Enumerable.Range(0,count).Select(i=>new HistoricalMilitaryOperation{Closed=true,PartyA=i,EpochA=i+1,VictoriesA=i%7})).Copy();
        object[] values={theaters,progress,military};string literal=StateHash.Compute(values),warm=Owned(values,context);if(literal!=warm)throw new Exception("Benchmark canonical bytes differ.");
        foreach(bool owned in new[]{false,true})
        {
            long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
            for(int i=0;i<iterations;i++){string hash=owned?Owned(values,context):StateHash.Compute(values);if(hash!=literal)throw new Exception("Warm canonical bytes differ.");}
            Console.WriteLine($"Sidecars owned={owned}, iterations={iterations}, milliseconds={watch.Elapsed.TotalMilliseconds:F3}, allocated={GC.GetAllocatedBytesForCurrentThread()-allocated}");
        }
    }
}
