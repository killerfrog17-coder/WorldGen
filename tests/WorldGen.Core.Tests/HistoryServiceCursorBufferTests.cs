using WorldGen.Core;

// Differential fixtures for the exact local cursor transformation. Production
// retains its full territorial body; actual saved-world continuations exercise
// that body independently against the frozen pre-buffer executable.
public static class HistoryServiceCursorBufferTests
{
    sealed class Fixture
    {
        public HistoricalTerritorialServiceRecovery Recovery=new HistoricalTerritorialServiceRecovery();
        public HistoricalLocalBag Bag=new HistoricalLocalBag();
        public List<int> Visited=new List<int>();
        public int Writes;
        public bool Threw;
    }
    static Fixture Run(bool buffered,int initial,int count,int visits,int scenario,int throwAt)
    {
        const int key=42;var f=new Fixture();var r=f.Recovery;
        r.Cursor=r.Cursor.With(key,initial).With(99,731);
        r.PendingSources.Add(key);r.Reviewed=r.Reviewed.With(key,0);
        for(int i=0;i<count;i++)f.Bag.Add(i);
        int value=r.Cursor[key];bool touched=false;
        void Store(int cursor){r.Cursor=r.Cursor.With(key,cursor);f.Writes++;}
        void Queue(int queued)
        {
            r.Reviewed=r.Reviewed.With(queued,0);
            if(r.PendingSources.Positions[queued]>=0)return;
            r.PendingSources.Add(queued);r.Cursor=r.Cursor.With(queued,0);
            r.LastReviewYear=r.LastReviewYear.With(queued,12);r.CreditKm2=r.CreditKm2.With(queued,0);
        }
        try
        {
            try
            {
                for(int k=0;k<visits&&f.Bag.Count>0;k++)
                {
                    int cursor=Math.Max(0,buffered?value:r.Cursor[key])%f.Bag.Count,cell=f.Bag.At(cursor);
                    if(buffered){value=cursor+1;touched=true;}else Store(cursor+1);
                    f.Visited.Add(cell);
                    // Queueing the same pending source must NOT reset its
                    // cursor; a distinct source may mutate the same tree.
                    if((scenario&1)!=0){Queue(key);Queue(key+1);}
                    if(k==throwAt)throw new InvalidOperationException("Fixture cell body failure");
                    if((scenario&2)!=0&&(cell%2==0||(scenario&4)!=0))
                    {f.Bag.Remove(cell);r.Reviewed=r.Reviewed.With(key,0);continue;}
                    r.Reviewed=r.Reviewed.With(key,Math.Max(0,r.Reviewed[key])+1);
                }
            }
            finally {if(buffered&&touched)Store(value);}
            if(f.Bag.Count==0||r.Reviewed[key]>=f.Bag.Count)
            {r.PendingSources.Remove(key);r.Reviewed=r.Reviewed.With(key,0);}
            else r.PendingCursor++;
        }
        catch(InvalidOperationException){f.Threw=true;}
        return f;
    }
    public static void Run(Action<bool,string> check)
    {
        long eagerBytes=0,bufferedBytes=0;int reductions=0;
        foreach(int initial in new[]{-1,0,1,7,int.MaxValue})
        foreach(int count in new[]{0,1,2,3,8,13})
        foreach(int visits in new[]{0,2,8})
        foreach(int scenario in new[]{0,1,2,3,6,7})
        foreach(int throwAt in new[]{-1,0,3})
        {
            var eager=Run(false,initial,count,visits,scenario,throwAt);
            var buffered=Run(true,initial,count,visits,scenario,throwAt);
            check(eager.Visited.SequenceEqual(buffered.Visited)&&eager.Threw==buffered.Threw,
                "Buffered cursor preserves cell order and exception prefix with dynamic boundaries and nested queues.");
            check(StateHash.Compute(eager.Recovery)==StateHash.Compute(buffered.Recovery)&&StateHash.Compute(eager.Bag)==StateHash.Compute(buffered.Bag),
                "Buffered cursor preserves every canonical tree, queue, reviewed value and drained-source removal.");
            check(eager.Recovery.Cursor[99]==731&&buffered.Recovery.Cursor[99]==731,
                "Publishing one cursor retains unrelated public and nested tree mutations.");
            check(buffered.Writes<=1&&buffered.Writes<=eager.Writes,
                "A source review publishes at most one cursor tree path, including failure and empty-boundary cases.");
            if(eager.Writes>buffered.Writes)reductions++;
        }
        // Allocation comparison excludes canonical hashing and fixture setup.
        HistoricalCellTree tree=new HistoricalCellTree(33554432);const int key=42;
        long start=GC.GetAllocatedBytesForCurrentThread();
        for(int n=0;n<1000;n++)for(int k=0;k<8;k++)tree=tree.With(key,k+1);
        eagerBytes=GC.GetAllocatedBytesForCurrentThread()-start;
        tree=new HistoricalCellTree(33554432);start=GC.GetAllocatedBytesForCurrentThread();
        for(int n=0;n<1000;n++)tree=tree.With(key,8);
        bufferedBytes=GC.GetAllocatedBytesForCurrentThread()-start;
        check(reductions>0&&bufferedBytes<eagerBytes/4,"Cursor buffering substantially reduces immutable path allocations without changing the final value.");
        var copy=tree;string before=StateHash.Compute(copy);tree=tree.With(key,9);
        check(StateHash.Compute(copy)==before&&StateHash.Compute(tree)!=before,"The optimized publication keeps immutable public mutation detection and checkpoint detachment.");
        Console.WriteLine("Cursor differential cases="+(5*6*3*6*3)+"; 1000 eight-cell paths bytes="+eagerBytes+" -> "+bufferedBytes);
    }
}
