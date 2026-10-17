using WorldGen.Core;
using System.Reflection;

public static class HistoryNuclearCanonicalTests
{
    public static void Run(Action<bool,string> check)
    {
        for(int sample=0;sample<40;sample++)
        {
            var record=new HistoricalNuclearState();int index=0;
            foreach(var field in typeof(HistoricalNuclearState).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                if(field.FieldType==typeof(long))field.SetValue(record,sample%2==0?long.MinValue+sample+index:long.MaxValue-sample-index);
                else field.SetValue(record,sample%2==0?int.MinValue+sample+index:int.MaxValue-sample-index);
                index++;
            }
            check(StateHash.Compute(record)==StateHash.ComputeHistoricalReference(record),"Every signed nuclear ledger field matches literal canonical serialization.");
            check(StateHash.Compute(new[]{record,null,record.Copy()})==StateHash.ComputeHistoricalReference(new[]{record,null,record.Copy()}),"Nuclear arrays preserve member/null/copy framing.");
            foreach(var field in typeof(HistoricalNuclearState).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                string before=StateHash.Compute(record);
                if(field.FieldType==typeof(long))field.SetValue(record,unchecked((long)field.GetValue(record)!+1));else field.SetValue(record,unchecked((int)field.GetValue(record)!+1));
                check(StateHash.Compute(record)!=before&&StateHash.Compute(record)==StateHash.ComputeHistoricalReference(record),"A changed nuclear field is read live rather than cached.");
            }
        }
        check(StateHash.Compute(new HistoricalNuclearHistory())==StateHash.ComputeHistoricalReference(new HistoricalNuclearHistory()),"The default empty nuclear container retains its exact format.");
    }
}
