using WorldGen.Core;
using System.Reflection;
public static class HistoricalCanonicalTests
{
    public static void Run(Action<bool,string> check)
    {
        var tree=HistoricalCellTree.From(129,i=>i%7==0?i:-1);string root=tree.Fingerprint();
        check(root==tree.Fingerprint(true)&&root==tree.Fingerprint(),"Immutable root caching preserves the explicitly recomputed Merkle digest");
        var changed=tree.With(17,123);check(changed.Fingerprint()!=root&&tree.Fingerprint()==root,"A spatial write gets its own fingerprint without invalidating a published root");
        var rootField=typeof(HistoricalCellTree).GetField("root",BindingFlags.Instance|BindingFlags.NonPublic)!;
        object node=rootField.GetValue(changed)!;var nodeType=node.GetType();var children=nodeType.GetField("Children",BindingFlags.Instance|BindingFlags.NonPublic)!;
        for(int shift=20;shift>0;shift-=5)node=((Array)children.GetValue(node)!).GetValue((17>>shift)&31)!;
        var values=(int[])nodeType.GetField("Values",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(node)!;
        string trusted=changed.Fingerprint();values[17&31]++;
        check(changed.Fingerprint(true)!=trusted,"Explicit verification detects tampered backing values even after a trusted fingerprint was cached");
        var types=new[]{typeof(HistoricalCityAggregate),typeof(HistoricalRegionAggregate),typeof(HistoricalCultureAggregate),typeof(HistoricalPolityAggregate),typeof(HistoricalLivingCity),typeof(HistoricalLivingRegion),typeof(HistoricalExchangeMemory),typeof(HistoricalCampaignTheater),typeof(HistoricalPolity),typeof(HistoricalRelation),typeof(HistoricalTown),typeof(HistoricalStateDynamics),typeof(HistoricalCampaign),typeof(HistoricalAgriculture),typeof(HistoricalCulture),typeof(HistoricalCultivar),typeof(Settlement),typeof(CultureIdentity),typeof(HistoricalAdministrativeAccount),typeof(HistoricalUrbanDemography),typeof(HistoricalLocalHarvest),typeof(HistoricalLocalCommunity),typeof(HistoricalGovernmentPolicy),typeof(HistoricalCampaignAdvance)};
        foreach(var type in types.Concat(new[]{typeof(HistoricalTechnicalPractice),typeof(HistoricalImperialGovernment),typeof(HistoricalAdministrativePost)}))
        {
            var records=Array.CreateInstance(type,19);var fields=type.GetFields(BindingFlags.Public|BindingFlags.Instance);
            for(int row=0;row<records.Length;row++)
            {
                if(row%5==0)continue;var record=Activator.CreateInstance(type)!;
                foreach(var field in fields)
                {
                    int index=row*97+Array.IndexOf(fields,field);var rng=new Rng(1927,"canonical-historical-"+type.Name,index);
                    if(field.FieldType==typeof(int))field.SetValue(record,unchecked((int)rng.Next()));
                    else if(field.FieldType==typeof(long))field.SetValue(record,unchecked(((long)rng.Next()<<32)|rng.Next()));
                    else if(field.FieldType==typeof(bool))field.SetValue(record,index%2==0);
                    else if(field.FieldType==typeof(double))field.SetValue(record,row%4==0?-0.0:((int)rng.Next())/1927.125789);
                    else if(field.FieldType.IsEnum)field.SetValue(record,Enum.ToObject(field.FieldType,unchecked((int)rng.Next())));
                    else if(field.FieldType==typeof(string))field.SetValue(record,row%3==0?null:row%4==0?"":"River café / ɯ — "+row);
                    else if(field.FieldType==typeof(int[]))field.SetValue(record,row%3==0?null:new[]{int.MinValue,row,-row,int.MaxValue});
                    else if(field.FieldType==typeof(long[]))field.SetValue(record,row%3==0?null:new[]{long.MinValue,(long)row,long.MaxValue});
                }
                records.SetValue(record,row);
            }
            string expected=StateHash.ComputeHistoricalReference(records),actual=StateHash.Compute(records);
            check(actual==expected,type.Name+" typed arrays preserve every canonical byte, including nulls, signs and double quantization");
            var item=records.GetValue(1)!;var integer=fields.FirstOrDefault(f=>f.FieldType==typeof(int));
            if(integer!=null){integer.SetValue(item,unchecked((int)integer.GetValue(item)!+1));check(StateHash.Compute(records)!=actual&&StateHash.Compute(records)==StateHash.ComputeHistoricalReference(records),type.Name+" hashing reads live fields rather than caching past values");}
            var concurrent=new string[12];Parallel.For(0,concurrent.Length,i=>concurrent[i]=i%2==0?StateHash.Compute(records):StateHash.ComputeHistoricalReference(records));
            check(concurrent.Distinct().Count()==1,type.Name+" canonical scratch/reference state is isolated across threads");
        }
    }
}
