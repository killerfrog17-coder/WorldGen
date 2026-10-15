using System.Diagnostics;
using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;
public static class HistoryIndexTests
{
    static object Invoke(Prehistory engine,string method,params object[] values)
        =>typeof(Prehistory).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,values);
    public static void Prepare(string source,string destination,int years)
    {
        var world=WorldFile.Read(File.ReadAllBytes(source));var engine=new Prehistory(world);
        world.Prehistory=engine.AdvanceYears(world.Prehistory,years);engine.Validate(world.Prehistory);
        File.WriteAllBytes(destination,WorldFile.Write(world));
        Console.WriteLine($"Checkpoint {world.Prehistory.Years}: {world.Prehistory.Society.Cultures.Length} cultures, {world.Prehistory.Society.Settlements.Length} settlements, {world.Prehistory.History.Polities.Length} recorded states; {world.Prehistory.Hash}");
    }
    public static void Profile(string source,int years,string output,bool indexed)
    {
        var world=WorldFile.Read(File.ReadAllBytes(source));var engine=new Prehistory(world){UseHistoryIndexes=indexed};
        var metrics=new List<SimulationWorkMetric>();engine.WorkObserver=metrics.Add;var watch=Stopwatch.StartNew();
        var result=engine.AdvanceYears(world.Prehistory,years);double elapsed=watch.Elapsed.TotalSeconds;engine.Validate(result);
        var rows=new List<string>{"stage,calls,items,totalMilliseconds,maxMilliseconds"};
        foreach(var group in metrics.GroupBy(m=>m.Stage).OrderByDescending(g=>g.Sum(m=>m.Milliseconds)))
            rows.Add(FormattableString.Invariant($"{group.Key},{group.Count()},{group.Sum(m=>(long)m.Items)},{group.Sum(m=>m.Milliseconds):F3},{group.Max(m=>m.Milliseconds):F3}"));
        File.WriteAllLines(output,rows);File.WriteAllText(output+".hash",result.Hash);
        Console.WriteLine(FormattableString.Invariant($"Indexed={indexed}: {years} years in {elapsed:F3}s ({years/elapsed:F1} years/s); {result.Hash}"));
    }
    public static void Run(string source,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(source));var engine=new Prehistory(world);var literal=new Prehistory(world){UseHistoryIndexes=false};
        var basis=engine.Start(engine.Prepare());var state=basis.Copy();var metrics=new List<SimulationWorkMetric>();engine.WorkObserver=metrics.Add;
        Invoke(engine,"BeginHistoricalWork",basis,state);
        try
        {
            int patch=Enumerable.Range(0,engine.Patches.Length).Where(p=>engine.Patches[p].Cells.Length>=8).OrderByDescending(p=>engine.Patches[p].Cells.Length).First();
            var cells=engine.Patches[patch].Cells;
            var first=(CultureIdentity)Invoke(engine,"FoundCulture",state,patch,cells[0])!;Invoke(engine,"InitializeHistoricalCulture",state,first);
            var second=(CultureIdentity)Invoke(engine,"FoundCulture",state,patch,cells[1])!;Invoke(engine,"InitializeHistoricalCulture",state,second);
            for(int i=2;i<Math.Min(70,cells.Length);i++)Invoke(engine,"SetCultureOwner",state,cells[i],i%3==0?1:0);
            void Match(string message)
            {
                engine.RefreshSocietyTotals(state);var actual=state.Society.Cultures.Select(c=>(c.Population,c.AreaKm2)).ToArray();
                literal.RefreshSocietyTotals(state);check(actual.SequenceEqual(state.Society.Cultures.Select(c=>(c.Population,c.AreaKm2))),message);
            }
            foreach(long people in new long[]{0,1,7,101,10007,999999,1000000000000})
            {state.Population[patch]=people;Match("Regional spans exactly match per-cell integer allocations at population "+people);}
            for(int i=0;i<32;i++)
            {
                int cell=cells[(i*13)%Math.Min(70,cells.Length)];Invoke(engine,"SetCultureOwner",state,cell,i%3-1);
                state.Population[patch]=7919L*(i+1);Match("Ownership change updates only its region without rounding drift "+i);
            }
            Invoke(engine,"SetPoliticalOwner",state,cells[3],17);Invoke(engine,"SetPoliticalOwner",state,cells[5],8);
            state.History.Polities=Enumerable.Range(0,18).Select(i=>new HistoricalPolity{Id=i}).ToArray();
            void MatchStates(string message)
            {
                foreach(var p in state.History.Polities){p.Population=0;p.AreaKm2=0;}Invoke(engine,"AddIndexedPoliticalTotals",state);
                var expectedPopulation=new long[18];var expectedArea=new long[18];
                for(int cell=0;cell<state.History.CellPolity.Length;cell++)if(state.History.CellPolity[cell]>=0)
                {int owner=state.History.CellPolity[cell];expectedPopulation[owner]+=engine.RuralCellPopulation(state,cell);expectedArea[owner]+=(long)Invoke(engine,"CellCulturalArea",state,cell)!;}
                check(state.History.Polities.All(p=>p.Population==expectedPopulation[p.Id]&&p.AreaKm2==expectedArea[p.Id]),message);
            }
            MatchStates("Regional state census matches literal per-cell census");state.Population[patch]+=71;MatchStates("State census tracks changed regional population");
            check(((IEnumerable<int>)Invoke(engine,"PoliticalCells",state)!).SequenceEqual(new[]{cells[3],cells[5]}),"Political membership has stable cell order, not insertion or owner order");
            Invoke(engine,"SetPoliticalOwner",state,cells[3],8);
            MatchStates("Conquest transfers exact rural census and area between states");
            check(((IEnumerable<int>)Invoke(engine,"PolityCells",state,8)!).SequenceEqual(new[]{cells[3],cells[5]}),"Conquest transfers membership without a world scan");
            Invoke(engine,"SetPoliticalOwner",state,cells[3],-1);
            check(((IEnumerable<int>)Invoke(engine,"PolityCells",state,17)!).Count()==0,"Dissolved state's membership is removed");
            Invoke(engine,"SetPoliticalOwner",state,cells[5],-1);
            MatchStates("Territorial withdrawal removes its contribution without rounding drift");
            check(metrics.Where(m=>m.Stage=="regional population bookkeeping").All(m=>m.Items==engine.Patches.Length),"Population bookkeeping iterates ecological regions rather than the terrain mesh");
        }
        finally {Invoke(engine,"EndHistoricalWork",state,false);}
        var current=world.Prehistory;string unchanged=current.Hash;
        var indexed=engine.AdvanceSeconds(current,3*SimulationTime.Year+17*SimulationTime.Day);
        var reference=literal.AdvanceSeconds(current,3*SimulationTime.Year+17*SimulationTime.Day);
        if(indexed.Hash!=reference.Hash)
        {
            void Fields(object a,object b,string path)
            {foreach(var field in a.GetType().GetFields()){var left=field.GetValue(a);var right=field.GetValue(b);if(StateHash.Compute(left)!=StateHash.Compute(right))Console.WriteLine("Index mismatch "+path+"."+field.Name);}}
            Fields(indexed,reference,"state");Fields(indexed.History,reference.History,"history");Fields(indexed.Society,reference.Society,"society");
            for(int cell=0;cell<indexed.History.CellStage.Length;cell++)if(indexed.History.CellStage[cell]!=reference.History.CellStage[cell]){Console.WriteLine($"Stage {cell}: indexed {indexed.History.CellStage[cell]} literal {reference.History.CellStage[cell]}, polity {indexed.History.CellPolity[cell]}, culture {indexed.Society.CellCulture[cell]}");break;}
        }
        check(indexed.Hash==reference.Hash,"Indexed cultural/state history exactly matches the literal reference path");
        var session=new SimulationSession(engine,current);long target=SimulationTime.Seconds(current)+3*SimulationTime.Year+17*SimulationTime.Day;int publications=0;
        while(session.Seconds<target){var slice=SimulationClockView.Publication(session.Seconds,target-session.Seconds,1000*SimulationTime.Year);session.AdvanceSeconds(slice.Seconds,slice.MaximumEvents,maximumGlobalWork:slice.MaximumGlobalWork);publications++;}
        check(session.Hash==reference.Hash,"Separate actor/global playback budgets preserve exact history");
        if(current.Society.Settlements.Length>0&&current.Society.Settlements.Length+current.History.Polities.Length<=32&&current.Society.Cultures.Length<1000&&current.Timeline!=null&&current.Timeline.Events.All(e=>e.Process!=0&&e.Process!=1||e.Due>target))
            check(publications==1,"Small pre-state settlement systems process multiple annual updates per publication, not one year per frame");
        if(current.Society.Cultures.Length>0&&current.Society.Settlements.Length==0)
        {
            var idleSession=new SimulationSession(engine,current);var warm=idleSession.AdvanceSeconds(200*SimulationTime.Year,int.MaxValue);
            var earliest=warm.Timeline.Events[0];
            if(earliest.Process==3&&warm.Society.Settlements.Length==0)
            {
                var pristine=warm.Copy();long interval=earliest.Due-idleSession.Seconds;
                warm.Population[0]++;
                var idle=idleSession.AdvanceSeconds(interval,4096,maximumGlobalWork:1);
                check(idle.Hash==literal.AdvanceSeconds(pristine,interval).Hash,"Idle actor fast path still requeues exact tickets/times and ignores mutated public arrays");
                check(PrehistoryState.SameDomain(warm,idle),"Ineligible townless culture checks do not force a map recolor or domain copy");
            }
        }
        check(current.Hash==unchanged&&current.ComputeHash()==unchanged,"Index tests and continuation leave the public source untouched");
        check(HistoricalLocalEffects.RegionalTraditionOpportunity(0)==0&&HistoricalLocalEffects.RegionalTraditionOpportunity(1000)>0,"New traditions have no synchronized date gate");
        check(HistoricalLocalEffects.RegionalTraditionOpportunity(10000)<HistoricalLocalEffects.RegionalTraditionOpportunity(50000),"Persistent local occupation makes specialist networks more plausible, not less");
        check(HistoricalLocalEffects.RegionalTraditionOpportunity(1000000)<=.12,"Even long-established habitats retain a lower spontaneous emergence rate");
        var upgraded=engine.UpgradeToCurrentHistory(current);engine.Validate(upgraded);
        check(upgraded.History.Version==Prehistory.CurrentHistoryVersion&&upgraded.Years==current.Years&&upgraded.Society.Cultures.Select(c=>c.FoundedYear).SequenceEqual(current.Society.Cultures.Select(c=>c.FoundedYear)),"Explicit new pacing upgrade preserves recorded culture founding dates");
    }
}
