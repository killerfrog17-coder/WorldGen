using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using WorldGen.Core;

public static class HistoryInternationalCanonicalTests
{
    static readonly MethodInfo WriteMethod=typeof(StateHash).GetMethod("Write",BindingFlags.Static|BindingFlags.NonPublic)!;
    // Separate literal encoder, not the typed writer or its scalar templates.
    static byte[] Literal(object value)
    {
        using var memory=new MemoryStream();using var writer=new BinaryWriter(memory,Encoding.UTF8,true);
        void Write(object item)
        {
            if(item==null){writer.Write((byte)0);return;}writer.Write((byte)1);var type=item.GetType();
            writer.Write(item is HistoricalRelationCampaigns?typeof(int[]).FullName!:type.FullName!);
            if(item is string text){writer.Write(text);return;}if(item is int number){writer.Write(number);return;}
            if(item is long wide){writer.Write(wide);return;}if(type.IsEnum){writer.Write(Convert.ToInt32(item));return;}
            if(item is HistoricalCellTree tree){writer.Write(tree.Fingerprint());return;}
            if(item is IEnumerable list){foreach(var entry in list){writer.Write((byte)1);Write(entry);}writer.Write((byte)0);return;}
            foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public).OrderBy(f=>f.Name,StringComparer.Ordinal))
            {
                if(item is HistoricalInternationalSystem system&&field.Name==nameof(HistoricalInternationalSystem.Finances)&&system.Finances==null)continue;
                writer.Write(field.Name);Write(field.GetValue(item));
            }
        }
        Write(value);writer.Flush();return memory.ToArray();
    }
    static byte[] Actual(object value)
    {using var memory=new MemoryStream();using var writer=new BinaryWriter(memory,Encoding.UTF8,true);WriteMethod.Invoke(null,new[]{writer,value});writer.Flush();return memory.ToArray();}
    static void Matches(object value,Action<bool,string> check)
    {
        var bytes=Literal(value);check(Actual(value).SequenceEqual(bytes),"International/calendar typed bytes exactly match independent literal field order and framing.");
        string hash=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();check(StateHash.Compute(value)==hash&&StateHash.ComputeHistoricalReference(value)==hash,"Typed, original reflection and independent SHA256 agree.");
    }
    public static void Run(Action<bool,string> check)
    {
        object[] records={new HistoricalDiplomaticAccount(),new HistoricalInternationalOrganization(),new HistoricalCoalitionEntry(),new HistoricalInternationalFinanceAccount(),new HistoricalInternationalOperatingBudget(),new SimulationEvent()};
        int serial=0;
        foreach(var row in records)
        {
            Matches(row,check);
            foreach(var field in row.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public).OrderBy(f=>f.Name,StringComparer.Ordinal))
            {
                string old=StateHash.Compute(row);serial++;
                if(field.FieldType==typeof(int))field.SetValue(row,serial%2==0?int.MinValue+serial:int.MaxValue-serial);
                else if(field.FieldType==typeof(long))field.SetValue(row,serial%2==0?long.MinValue+serial:long.MaxValue-serial);
                else if(field.FieldType.IsEnum)field.SetValue(row,Enum.ToObject(field.FieldType,int.MinValue+serial));
                else if(field.FieldType==typeof(string))field.SetValue(row,"Live testimony \"é水\n"+serial);
                else if(field.FieldType==typeof(HistoricalLocalBag)){var bag=new HistoricalLocalBag();bag.Add(17);bag.Add(7919);field.SetValue(row,bag);}
                else throw new Exception("Add explicit test input for "+field.Name);
                Matches(row,check);check(StateHash.Compute(row)!=old,"Public edits to every scalar/string/bag field are read live without value memoization.");
            }
            var array=Array.CreateInstance(row.GetType(),3);array.SetValue(row,0);array.SetValue(Activator.CreateInstance(row.GetType()),2);Matches(array,check);
            Matches(Array.CreateInstance(row.GetType(),0),check);
        }
        foreach(string text in new[]{null,"","Unicode é水",new string('水',4097)})
        {Matches(new HistoricalInternationalOrganization{Name=text,Members=null},check);Matches(new SimulationTimeline{Version=text,Events=null},check);}
        var system=new HistoricalInternationalSystem{Accounts=new[]{new HistoricalDiplomaticAccount{Id=7},null},Organizations=new[]{new HistoricalInternationalOrganization{Name="A funded league",Kind=HistoricalOrganizationKind.PeaceCongress},null},CoalitionEntries=new[]{new HistoricalCoalitionEntry{AllyId=7},null},RelationCampaigns=new[]{-1,int.MinValue,13,int.MaxValue}};
        Matches(system,check);system.Finances=new HistoricalInternationalFinances{Accounts=new[]{new HistoricalInternationalFinanceAccount{TradeAssessment=long.MaxValue},null},Organizations=new[]{new HistoricalInternationalOperatingBudget{UnfundedWork=long.MinValue},null}};Matches(system,check);
        // Copy contracts expect real non-null rows; remove deliberate wire-edge nulls first.
        system.Accounts=system.Accounts.Where(x=>x!=null).ToArray();system.Organizations=system.Organizations.Where(x=>x!=null).ToArray();system.CoalitionEntries=system.CoalitionEntries.Where(x=>x!=null).ToArray();
        system.Finances.Accounts=system.Finances.Accounts.Where(x=>x!=null).ToArray();system.Finances.Organizations=system.Finances.Organizations.Where(x=>x!=null).ToArray();
        var copy=system.Copy();string fixedCopy=StateHash.Compute(copy);system.Accounts[0].Relations.Add(37);system.Finances.Accounts[0].PeaceAssessment++;system.RelationCampaigns[0]=29;
        check(StateHash.Compute(copy)==fixedCopy&&StateHash.Compute(system)!=fixedCopy,"Live typed writers preserve retained-source/copy isolation including persistent bags and relation pages.");
        var timeline=new SimulationTimeline{Events=new[]{new SimulationEvent{Due=long.MaxValue,Entity=int.MinValue,Process=int.MaxValue,Ticket=long.MinValue},null},Seconds=long.MinValue,NextTicket=long.MaxValue,ProcessedEvents=-17};Matches(timeline,check);
        foreach(var field in typeof(SimulationTimeline).GetFields())
        {if(field.FieldType==typeof(long)){field.SetValue(timeline,~(long)field.GetValue(timeline));Matches(timeline,check);}}
        var parallel=new bool[32];Parallel.For(0,parallel.Length,i=>{var row=new HistoricalInternationalOrganization{Id=i,Name="Parallel "+i,Contributions=long.MaxValue-i,Kind=(HistoricalOrganizationKind)(i%2)};parallel[i]=Actual(row).SequenceEqual(Literal(row));});
        foreach(bool ok in parallel)check(ok,"Concurrent international writers use independent scratch buffers.");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();bool cancelled=false;try{StateHash.Compute(system,cancellation.Token);}catch(OperationCanceledException){cancelled=true;}check(cancelled,"Typed international hashing retains cancellation checks.");
        var events=Enumerable.Range(0,12000).Select(i=>new SimulationEvent{Due=i*7919L,Entity=i,Process=i%31,Ticket=i*23L}).ToArray();
        StateHash.Compute(events);StateHash.ComputeHistoricalReference(events);long before=GC.GetAllocatedBytesForCurrentThread();StateHash.Compute(events);long typed=GC.GetAllocatedBytesForCurrentThread()-before;
        before=GC.GetAllocatedBytesForCurrentThread();StateHash.ComputeHistoricalReference(events);long original=GC.GetAllocatedBytesForCurrentThread()-before;
        check(typed<original/4,"Typed12000-event hashing avoids per-field reflection/boxing allocations.");
        Console.WriteLine("12000 events: typed "+typed+"B vs literal "+original+"B.");
    }
}
