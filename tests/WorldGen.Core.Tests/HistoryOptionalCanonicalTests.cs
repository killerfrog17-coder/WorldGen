using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryOptionalCanonicalTests
{
    static readonly Type[] Types={typeof(HistoricalAdministrativePost),typeof(HistoricalImperialGovernment),typeof(HistoricalMilitaryGovernment),typeof(HistoricalMilitaryOperation),typeof(HistoricalRecognizedTitle),typeof(HistoricalRealmAccount)};
    public static void Run(Action<bool,string> check)
    {
        HistoryTreeCanonicalBufferTests.Run(check);
        foreach(var type in Types)
        {
            var record=Activator.CreateInstance(type)!;
            check(StateHash.Compute(record)==StateHash.ComputeHistoricalReference(record),"An optional record's default live fields preserve literal canonical bytes.");
            int index=0;
            foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.Name,StringComparer.Ordinal))
            {
                object value=field.FieldType==typeof(int)?unchecked(int.MinValue+index*7919):field.FieldType==typeof(long)?long.MaxValue-index*104729L:field.FieldType==typeof(bool)?true:field.FieldType==typeof(double)?123456789.123456789:field.FieldType==typeof(string)?"Quoted \" testimony / é水\n"+index:field.FieldType==typeof(int[])?new[]{int.MinValue,-1,0,1,int.MaxValue}:field.FieldType==typeof(HistoricalLocalBag)?new HistoricalLocalBag():throw new Exception("Update optional canonical fixture for "+field.Name);
                if(value is HistoricalLocalBag bag){bag.Add(7);bag.Add(128);bag.Remove(7);}
                field.SetValue(record,value);index++;
                check(StateHash.Compute(record)==StateHash.ComputeHistoricalReference(record),"Every typed optional field, including signed extremes and quantized doubles, matches literal canonical order.");
            }
            var records=Array.CreateInstance(type,3);records.SetValue(record,0);records.SetValue(null,1);records.SetValue(Activator.CreateInstance(type),2);
            check(StateHash.Compute(records)==StateHash.ComputeHistoricalReference(records),"Typed arrays preserve null entries, heterogeneous values and exact sequence framing.");
            string before=StateHash.Compute(records);var scalar=type.GetFields().First(f=>f.FieldType==typeof(int));scalar.SetValue(record,(int)scalar.GetValue(record)!+1);
            check(StateHash.Compute(records)!=before&&StateHash.Compute(records)==StateHash.ComputeHistoricalReference(records),"Previously hashed records are read live; public mutation cannot hide behind a frozen-record cache.");
            check(StateHash.Compute(Array.CreateInstance(type,0))==StateHash.ComputeHistoricalReference(Array.CreateInstance(type,0)),"Empty optional arrays retain their exact terminator.");
        }
        Parallel.For(0,32,k=>{var values=new[]{new HistoricalAdministrativePost{Id=k,AnnualCost=long.MaxValue-k,MessageEffortKm=k/7.0,Name="Concurrent"+k},null};check(StateHash.Compute(values)==StateHash.ComputeHistoricalReference(values),"Concurrent optional writers have isolated per-call scratch.");});
        check(StateHash.Compute(new HistoricalColonialConnections())=="3e6f409c4cdc77e11d2119ec182e566258a65f5eaa5265d0ebc2bd8ea6d094ea","Nullable legacy extensions preserve the released colonial canonical hash.");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var source=w.Prehistory;string original=source.Hash;var e=new Prehistory(w);e.Validate(source);
        check(source.ComputeHash()==original,"Existing saved rules retain the exact stored integrity hash with typed optional writers.");
        var literal=typeof(StateHash).GetField("literalAggregateRecords",BindingFlags.NonPublic|BindingFlags.Static)!;
        string Reference(PrehistoryState value){literal.SetValue(null,true);try{return value.ComputeHash();}finally{literal.SetValue(null,false);}}
        check(Reference(source)==original,"The entire saved aggregate checkpoint matches the literal canonical writer, not just isolated records.");
        var copy=source.Copy();var t=copy.History.Aggregates.Empires.Territories;string before=copy.ComputeHash();t.Posts[0].AnnualCost++;
        check(copy.ComputeHash()!=before&&copy.ComputeHash()==Reference(copy),"Public edits to existing administrative offices remain detectable after a whole-checkpoint hash.");
        if(source.History.Aggregates.Empires.Connections.Military?.Campaigns.Length>0)
        {
            copy=source.Copy();before=copy.ComputeHash();copy.History.Aggregates.Empires.Connections.Military.Campaigns[0].CommittedA++;
            check(copy.ComputeHash()!=before&&copy.ComputeHash()==Reference(copy),"Public edits to closed military operation ledgers remain detectable.");
        }
        var restored=WorldFile.Read(WorldFile.Write(w)).Prehistory;check(restored.Hash==original,"Serialization/load preserves historical optional record values and canonical bytes.");
        var whole=e.AdvanceYears(source,12);var pieces=new Prehistory(w).AdvanceYears(new Prehistory(w).AdvanceYears(restored,5),7);e.Validate(whole);e.Validate(pieces);
        check(whole.Hash==pieces.Hash&&whole.Hash==Reference(whole),"Actual saved continuation agrees across cold restore, partitioned advances and literal canonical hashing.");
        check(source.ComputeHash()==original,"Typed hashing and continuation leave the caller's original saved checkpoint unchanged.");
    }
}
