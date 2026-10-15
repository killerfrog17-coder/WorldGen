using WorldGen.Core;
using WorldGen.Serialization;
using System.Reflection;
public static class HistoryRoutingPerformanceTests
{
    public static void Run(string file,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(file));var original=world.Prehistory;var indexed=new Prehistory(world);var literal=new Prehistory(world){UseHistoryIndexes=false};
        var source=original.Copy();if(source.History.Aggregates!=null)throw new ArgumentException("Use a legacy terrain-routing fixture for this compatibility test.");source.History.Version="history-16";source.Seal();indexed.Validate(source);var scratch=source.Copy();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        object Invoke(Prehistory engine,string method,params object[] values)=>typeof(Prehistory).GetMethod(method,flags)!.Invoke(engine,values)!;
        object Build(Prehistory engine)=>Invoke(engine,"BuildDelegatedAdministrativeNetwork",scratch,CancellationToken.None)!;
        void Compare(string label)
        {
            var actual=Build(indexed);var expected=Build(literal);var type=actual.GetType();
            foreach(string field in new[]{"Owner","Distance","CommunicationOwner"})
                check(((int[])type.GetField(field)!.GetValue(actual)!).SequenceEqual((int[])type.GetField(field)!.GetValue(expected)!),label+": "+field);
            check(StateHash.Compute(type.GetField("Accounts")!.GetValue(actual))==StateHash.Compute(type.GetField("Accounts")!.GetValue(expected)),label+": exact live accounts");
        }
        Invoke(indexed,"BeginHistoricalWork",source,scratch);
        try
        {
            Compare("Cold network");var live=scratch.History.Polities.Where(p=>p.EndedYear<0).ToArray();
            var publicWorks=live.Select(p=>scratch.History.Dynamics.States[p.Id].PublicWorks).ToArray();
            foreach(int delta in new[]{1,3,-2,50,-50,0})
            {
                for(int k=0;k<live.Length;k++)scratch.History.Dynamics.States[live[k].Id].PublicWorks=Math.Clamp(publicWorks[k]+delta,0,1000);
                Compare("Changed reach/public works "+delta);
            }
            var office=scratch.History.Administration.Offices.FirstOrDefault(o=>o.EndedYear<0&&scratch.History.Polities[o.PolityId].EndedYear<0);
            if(office!=null)
            {
                var ruler=scratch.History.Polities[office.PolityId];long money=ruler.Treasury;ruler.Treasury=0;
                Compare("Unfunded offices change origins inside a reused message network");ruler.Treasury=money;Compare("Office funding restored");
            }
            var held=Enumerable.Range(0,scratch.History.CellPolity.Length).First(i=>scratch.History.CellPolity[i]>=0);
            int former=scratch.History.CellPolity[held];Invoke(indexed,"SetPoliticalOwner",scratch,held,-1);Compare("Control barrier changes invalidate interval proofs");
            Invoke(indexed,"SetPoliticalOwner",scratch,held,former);Compare("Restored controller after an intervening branch");
            foreach(int rival in live.Where(p=>p.Id!=former).Take(2).Select(p=>p.Id))
            {Invoke(indexed,"SetPoliticalOwner",scratch,held,rival);Compare("A changed foreign controller preserves only equivalent blocked/allied transit");}
            Invoke(indexed,"SetPoliticalOwner",scratch,held,former);Compare("Original transit after successive conquests");
            int patch=indexed.CellPatch[held];long population=scratch.Population[patch];
            foreach(long changed in new[]{0L,1L,population+1000,population})
            {
                scratch.Population[patch]=changed;Compare("Live regional population "+changed);
                check(indexed.RuralCellPopulation(scratch,held)==literal.RuralCellPopulation(scratch,held),"Population-keyed allocations cannot reuse a different region count");
            }
            using var cancel=new CancellationTokenSource();cancel.Cancel();bool stopped=false;
            try{Invoke(indexed,"BuildDelegatedAdministrativeNetwork",scratch,cancel.Token);}catch(TargetInvocationException e) when(e.InnerException is OperationCanceledException){stopped=true;}
            check(stopped,"A warm interval cache honors cancellation");Compare("Resume after cancellation");
        }
        finally{Invoke(indexed,"EndHistoricalWork",scratch,false);}
        check(original.ComputeHash()==original.Hash&&source.ComputeHash()==source.Hash,"Routing probes do not alter the original or upgraded checkpoints");
    }
}
