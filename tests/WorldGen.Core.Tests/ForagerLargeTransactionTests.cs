using WorldGen.Core;
using WorldGen.Serialization;
static class ForagerLargeTransactionTests
{
    public static void Run(string file)
    {
        var w=WorldFile.ReadFile(file);var engine=new Prehistory(w);var start=w.Prehistory;int checks=0;
        void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        Check(start.Foragers.Cohorts.Count(c=>c.Population>=1)>=1024,"Fixture actually exercises the concurrent cohort path");
        string original=start.ComputeHash();var full=engine.AdvanceYears(start,25);var split=start;
        for(int i=0;i<5;i++)split=engine.AdvanceYears(split,5);
        Check(full.Hash==split.Hash,"Large concurrent learning gives the same full ledger across caller boundaries");
        var session=new SimulationSession(engine,start);long target=SimulationTime.Seconds(full);
        while(session.Seconds<target)session.AdvanceSeconds(target-session.Seconds,1,maximumGlobalWork:1);
        Check(session.Hash==full.Hash,"Event/work budgets preserve the concurrent learning result");
        var atomic=new SimulationSession(engine,start);
        using(var cancel=new CancellationTokenSource())
        {
            engine.WorkObserver=m=>{if(m.Stage=="forager practices")cancel.Cancel();};
            bool cancelled=false;try{atomic.AdvanceSeconds(25*SimulationTime.Year,int.MaxValue,cancel.Token);}catch(OperationCanceledException){cancelled=true;}
            engine.WorkObserver=null;
            Check(cancelled&&atomic.Seconds==SimulationTime.Seconds(start)&&atomic.Hash==original,"Cancellation after worker arithmetic cannot publish a partial private ledger");
        }
        Check(start.ComputeHash()==original,"Concurrent runs and cancellation never mutate the source checkpoint");
        Console.WriteLine($"PASS {checks} large-cohort concurrent transaction checks; {full.Hash}");
    }
}
