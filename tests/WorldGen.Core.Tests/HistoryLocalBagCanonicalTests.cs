using System.Collections;
using System.Security.Cryptography;
using WorldGen.Core;

public static class HistoryLocalBagCanonicalTests
{
    static void Integer(BinaryWriter writer,int value){writer.Write((byte)1);writer.Write("System.Int32");writer.Write(value);}
    static void Tree(BinaryWriter writer,HistoricalCellTree tree){writer.Write((byte)(tree==null?0:1));if(tree!=null){writer.Write("WorldGen.Core.HistoricalCellTree");writer.Write(tree.Fingerprint());}}
    static void Bag(BinaryWriter writer,HistoricalLocalBag bag)
    {
        writer.Write((byte)(bag==null?0:1));if(bag==null)return;writer.Write("WorldGen.Core.HistoricalLocalBag");
        writer.Write("Count");Integer(writer,bag.Count);writer.Write("Items");Tree(writer,bag.Items);writer.Write("Positions");Tree(writer,bag.Positions);
    }
    static void Port(BinaryWriter writer,HistoricalPendingPortGovernment record)
    {writer.Write((byte)(record==null?0:1));if(record==null)return;writer.Write("WorldGen.Core.HistoricalPendingPortGovernment");writer.Write("Cursor");Integer(writer,record.Cursor);writer.Write("Pending");Bag(writer,record.Pending);}
    static string Literal(Action<BinaryWriter> action){using var bytes=new MemoryStream();using(var writer=new BinaryWriter(bytes,System.Text.Encoding.UTF8,true))action(writer);return Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLowerInvariant();}
    static string ArrayLiteral(HistoricalPendingPortGovernment[] rows)=>Literal(writer=>{writer.Write((byte)1);writer.Write("WorldGen.Core.HistoricalPendingPortGovernment[]");foreach(var row in rows){writer.Write((byte)1);Port(writer,row);}writer.Write((byte)0);});
    sealed class Reentrant:IEnumerable<HistoricalLocalBag>
    {public HistoricalLocalBag First,Last;public Action Between;public IEnumerator<HistoricalLocalBag> GetEnumerator(){yield return First;Between?.Invoke();yield return Last;}IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();}
    public static void Run(Action<bool,string> check)
    {
        foreach(int value in new[]{int.MinValue,-7,-1,0,1,17,int.MaxValue})
        {
            var bag=new HistoricalLocalBag{Count=value,Items=new HistoricalCellTree(73).With(17,-9),Positions=new HistoricalCellTree(73).With(45,31)};
            check(StateHash.Compute(bag)==Literal(w=>Bag(w,bag)),"Bag fast path retains independently emitted flags, field order, signed scalar and tree bytes.");
            var row=new HistoricalPendingPortGovernment{Cursor=value,Pending=bag};
            check(StateHash.Compute(row)==Literal(w=>Port(w,row)),"Pending-port scalar/record framing is byte-identical to independent literal output.");
            var array=new[]{row,null,new HistoricalPendingPortGovernment{Cursor=~value,Pending=null},row};
            check(StateHash.Compute(array)==ArrayLiteral(array)&&StateHash.Compute(array)==StateHash.ComputeHistoricalReference(array),"Ordered duplicate/null pending-port array entries retain literal/reflection framing.");
            string old=StateHash.Compute(array);row.Cursor=unchecked(value+1);check(StateHash.Compute(array)!=old,"Public cursor changes remain visible; no saved record bytes are memoized.");
            old=StateHash.Compute(array);bag.Count=unchecked(value+1);check(StateHash.Compute(array)!=old,"Public bag count mutations remain visible through retained array references.");
            old=StateHash.Compute(array);bag.Items=bag.Items.With(17,97);check(StateHash.Compute(array)!=old,"Persistent item-tree replacement remains visible through a retained bag reference.");
        }
        foreach(var bag in new[]{new HistoricalLocalBag(),new HistoricalLocalBag{Items=null},new HistoricalLocalBag{Positions=null},null})check(StateHash.Compute(bag)==Literal(w=>Bag(w,bag)),"Empty/null bags and null child trees keep exact canonical markers.");
        check(StateHash.Compute(Array.Empty<HistoricalPendingPortGovernment>())==ArrayLiteral(Array.Empty<HistoricalPendingPortGovernment>()),"Empty pending-port arrays retain the terminal marker.");
        var source=new HistoricalLocalBag();source.Add(17);var copy=source.Copy();string copied=StateHash.Compute(copy);source.Add(45);source.Count=97;check(StateHash.Compute(copy)==copied&&StateHash.Compute(source)!=copied,"Retained public edits cannot change an independently copied bag.");
        var sequence=new Reentrant{First=copy,Last=new HistoricalLocalBag{Count=397}};string expected=StateHash.ComputeHistoricalReference(sequence);sequence.Between=()=>check(StateHash.Compute(new HistoricalPendingPortGovernment{Cursor=-97,Pending=new HistoricalLocalBag{Count=int.MinValue}})==Literal(w=>Port(w,new HistoricalPendingPortGovernment{Cursor=-97,Pending=new HistoricalLocalBag{Count=int.MinValue}})),"Nested root hashing emits independent pending-port bytes.");
        check(StateHash.Compute(sequence)==expected,"Reentrant root hashing cannot overwrite already emitted outer scalar scratch.");
        var matches=new bool[32];Parallel.For(0,matches.Length,i=>{var rows=new[]{new HistoricalPendingPortGovernment{Cursor=i,Pending=new HistoricalLocalBag{Count=-i}},null};matches[i]=StateHash.Compute(rows)==ArrayLiteral(rows);});foreach(bool match in matches)check(match,"Concurrent hashes own separate scalar scratch and preserve all rows.");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();bool caught=false;try{StateHash.Compute(new[]{new HistoricalPendingPortGovernment()},cancellation.Token);}catch(OperationCanceledException){caught=true;}check(caught&&StateHash.Compute(copy)==copied,"Cancellation cannot poison thread-local scalar scratch or alter its source.");}
    }
}
