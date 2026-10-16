using WorldGen.Core;
public static class CanonicalIntegerArrayTests
{
    public static void Run(Action<bool,string> check)
    {
        foreach(int length in new[]{0,1,2,3,37,39,127,128,129,2047,2048,2049,4097,8193})
        {
            var values=Enumerable.Range(0,length).Select(i=>i%5==0?int.MinValue:i%5==1?int.MaxValue:unchecked(i*7919-15000)).ToArray();
            string expected=StateHash.ComputeHistoricalReference(values);
            check(StateHash.Compute(values)==expected,"Batched integer bytes equal literal enumerable/scalar bytes at every size boundary.");
            Array.Reverse(values);string reversed=StateHash.ComputeHistoricalReference(values);
            check(StateHash.Compute(values)==reversed,"Reused scratch never retains previous order or previous values.");
            check(StateHash.Compute(new[]{42})==StateHash.ComputeHistoricalReference(new[]{42}),"A short array after a long one does not hash unused scratch bytes.");
        }
        var sample=new[]{int.MinValue,-1,0,1,int.MaxValue};string hash=StateHash.ComputeHistoricalReference(sample);
        Parallel.For(0,64,_=>check(StateHash.Compute(sample)==hash,"Concurrent hash writers have isolated bounded integer scratch."));
    }
}
