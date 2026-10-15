using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
public static class SimulationCalendarTests
{
    public static void Run(Action<bool,string> check)
    {
        var q=new SimulationEventQueue(new[]{new SimulationEvent{Due=30,Process=2,Ticket=3},new SimulationEvent{Due=10,Process=1,Ticket=2},new SimulationEvent{Due=10,Process=0,Ticket=1}});
        check(q.Pop().Ticket==1&&q.Pop().Ticket==2&&q.Pop().Ticket==3,"Queue ties are stable");
        long w=SimulationEventQueue.Window(17,"test-window",3,20,1000,100);check(w>=900&&w<=1100&&w==SimulationEventQueue.Window(17,"test-window",3,20,1000,100),"Timing windows are bounded and seeded");
        check(SimulationClockView.Date(0,100*SimulationTime.Year)=="Year 0","Fast playback shows years only");
        check(SimulationClockView.Date(0,SimulationTime.Year/2.0)=="Year 0 · Jan","Half-year/second shows months");
        check(SimulationClockView.Date(0,14*SimulationTime.Day)=="Year 0 · Jan 1","Two weeks/second includes days");
        check(SimulationClockView.Date(0,SimulationTime.Day/2.0)=="Year 0 · Jan 1 · 00h","Half-day/second includes hours, not minutes");
        check(SimulationClockView.Date(366*SimulationTime.Day+3661,1)=="Year 1 · Jan 2 · 01:01","Real-time shows minutes but no seconds");
        check(SimulationClockView.Date(59*SimulationTime.Day,1).Contains("Mar 1"),"Non-leap month lengths are explicit");
        var epochState=new PrehistoryState{Years=73849,History=new HistoryState{Polities=new[]{new HistoricalPolity{FoundedYear=73000,EndedYear=73200},new HistoricalPolity{FoundedYear=73500}}}};
        check(SimulationClockView.EpochYear(epochState)==73000&&SimulationClockView.Date(epochState,100*SimulationTime.Year)=="Year 849","First civilization establishes displayed Year 0, including on existing saves");
        check(SimulationClockView.Date(73000*SimulationTime.Year,100*SimulationTime.Year,73000)=="Year 0","Epoch starts at zero, not one");
        check(SimulationClockView.Year(epochState,72995)=="Year 5 before civilization","Older historical records use the same epoch");
        check(SimulationClockView.Date(73001*SimulationTime.Year+SimulationTime.Day+3661,1,73000)=="Year 1 · Jan 2 · 01:01","Epoch changes years without losing precise month/day/time");
        check(epochState.Years==73849&&epochState.History.Polities[0].FoundedYear==73000,"Display epoch never rewrites underlying simulation dates");
        var epochJson=WorldJson.Read<PrehistoryState>(WorldJson.Write(epochState));check(SimulationClockView.EpochYear(epochJson)==73000,"Epoch remains stable after save/reload and collapse");
        check(SimulationClockView.EpochYear(new PrehistoryState{History=new HistoryState()})== -1,"No reset before civilization actually forms");
        var play=new SimulationPlayback();play.SetRate(100);play.Accumulate(1,true);check(play.Pending==25,"Hitch debt is bounded");play.Consume(10);check(play.Pending==15,"Unprocessed credit survives a work budget");play.Accumulate(1,false);check(play.Pending==0,"Pause clears credit");play.Phase(1);check(play.Rate==100,"Manual rates survive phase changes");play.Auto(1);check(play.Rate==5*SimulationTime.Year,"Automatic phase rates do not set solver frequency");
        var fastPlay=new SimulationPlayback();fastPlay.SetRate(1000*SimulationTime.Year);long fastSeconds=123;
        for(int frame=0;frame<60;frame++)
        {
            fastPlay.Accumulate(1.0/60,true);
            var slice=SimulationClockView.Publication(fastSeconds,fastPlay.Pending,fastPlay.Rate);
            fastSeconds+=slice.Seconds;fastPlay.Consume(slice.Seconds);
        }
        check(Math.Abs(fastSeconds-123-1000*SimulationTime.Year)<=1,"A 60Hz playback loop can request 1000 years per second, not one year per frame");
        string fixture=Environment.GetEnvironmentVariable("CALENDAR_HISTORY_FIXTURE")??"artifacts/history-5/tests/Earth.world";
        var world=File.Exists(fixture)?WorldFile.Read(File.ReadAllBytes(fixture)):new WorldPipeline().Generate(new Recipe{Detail=5000});world.Prehistory=new PrehistoryState{History=new HistoryState{Version="history-16"}}; // Calendar-2 compatibility, not the new actor calendar.
        var engine=new Prehistory(world);var start=engine.Start(engine.Prepare());string original=start.Hash;
        var partial=engine.AdvanceSeconds(start,37);engine.Validate(partial);check(partial.Years==0&&partial.Timeline.Seconds==37&&partial.Population.SequenceEqual(start.Population),"Clock-only seconds do not run century demographics");
        var full=engine.AdvanceSeconds(start,1000*SimulationTime.Year);var split=engine.AdvanceSeconds(partial,1000*SimulationTime.Year-37);engine.Validate(full);check(full.Hash==split.Hash,"Arbitrary-second partitions preserve hashes");
        var fastSession=new SimulationSession(engine,start);long fastTarget=1000*SimulationTime.Year;int publications=0;
        while(fastSession.Seconds<fastTarget)
        {
            var slice=SimulationClockView.Publication(fastSession.Seconds,fastTarget-fastSession.Seconds,1000*SimulationTime.Year);
            fastSession.AdvanceSeconds(slice.Seconds,slice.MaximumEvents,maximumGlobalWork:slice.MaximumGlobalWork);publications++;
        }
        check(fastSession.Hash==full.Hash&&publications<40,"Sparse prehistory crosses 1000 years in work-bounded rather than year-bounded publications with the exact direct-run hash");
        PrehistoryState FixedClock(PrehistoryState state)
        {
            var result=state.Copy();var events=new List<SimulationEvent>();long ticket=0;
            void Add(int process,int year)=>events.Add(new SimulationEvent{Process=process,Due=year*SimulationTime.Year,Ticket=ticket++});
            Add(0,(state.Years/100+1)*100);Add(1,(state.Years/50+1)*50);if(state.Society.Settlements.Length>0)Add(2,state.Years+1);
            result.Timeline=new SimulationTimeline{Seconds=state.Years*SimulationTime.Year,NextTicket=ticket,Events=events.OrderBy(e=>e,Comparer<SimulationEvent>.Create(SimulationEventQueue.Compare)).ToArray()};result.Seal();return result;
        }
        var originalRules=start.Copy();originalRules.History.Version="history-12";originalRules.History.Modern=null;originalRules.Seal();
        var legacy=engine.AdvanceSeconds(FixedClock(originalRules),1000*SimulationTime.Year);var domain=legacy.Copy();domain.Timeline=null;domain.Seal();check(domain.Hash==engine.AdvanceYears(originalRules,1000).Hash,"Calendar-1 preserves historical outcomes");
        check(engine.AdvanceYears(start,1000).Hash==full.Hash,"Current history has one stochastic calendar regardless of the stepping API");
        check(partial.Timeline.Version=="event-calendar-2"&&partial.Timeline.Events.Single(e=>e.Process==1).Due>=30*SimulationTime.Year&&partial.Timeline.Events.Single(e=>e.Process==1).Due<=60*SimulationTime.Year,"New calendars schedule real cultural events in a 30–60 year window");
        var next=partial;var intervals=new List<long>();long previousDue=0;
        for(int i=0;i<10;i++){long due=engine.NextSimulationEvent(next);next=engine.AdvanceSeconds(next,due-SimulationTime.Seconds(next));if(next.Timeline.Events.Single(e=>e.Process==1).Due!=previousDue){previousDue=next.Timeline.Events.Single(e=>e.Process==1).Due;intervals.Add(previousDue-SimulationTime.Seconds(next));}}
        check(intervals.Count>2&&intervals.Distinct().Count()>2&&intervals.Any(x=>x%SimulationTime.Year!=0),"Actual requeued updates vary at second precision rather than year multiples");
        var budget=start;while(SimulationTime.Seconds(budget)<1000*SimulationTime.Year)budget=engine.AdvanceSeconds(budget,1000*SimulationTime.Year-SimulationTime.Seconds(budget),1);engine.Validate(budget);check(budget.Hash==full.Hash,"Work budgets never drop events or expose partial coincident phases");
        check(start.ComputeHash()==original&&start.Timeline==null,"Input and legacy integrity remain immutable");world.Prehistory=partial;
        var restored=WorldFile.Read(WorldFile.Write(world));check(restored.Prehistory.Hash==partial.Hash&&new Prehistory(restored).AdvanceSeconds(restored.Prehistory,63).Hash==engine.AdvanceSeconds(partial,63).Hash,"Binary save/reload preserves queue and seconds");
        var json=WorldJson.Read<PrehistoryState>(WorldJson.Write(partial));check(engine.AdvanceSeconds(json,63).Hash==engine.AdvanceSeconds(partial,63).Hash,"JSON continuation is exact");
        var corrupted=partial.Copy();corrupted.Timeline.Events[0].Due+=1000*SimulationTime.Year;corrupted.Seal();check(partial.ComputeHash()==partial.Hash,"Queue copies do not alias");bool rejected=false;try{engine.Validate(corrupted);}catch(ArgumentException){rejected=true;}check(rejected,"Out-of-window schedule rejected");
        using var cancel=new CancellationTokenSource();cancel.Cancel();bool stopped=false;try{engine.AdvanceSeconds(start,1000*SimulationTime.Year,10,cancel.Token);}catch(OperationCanceledException){stopped=true;}check(stopped&&start.ComputeHash()==original,"Cancelled scheduled work is immutable");
        if(File.Exists(fixture))
        {
            var old=WorldFile.Read(File.ReadAllBytes(fixture));var civ=new Prehistory(old);var s=old.Prehistory;var annual=civ.AdvanceYears(s,100);var fixedStart=FixedClock(s);var legacyScheduled=civ.AdvanceSeconds(fixedStart,100*SimulationTime.Year);civ.Validate(legacyScheduled);var stripped=legacyScheduled.Copy();stripped.Timeline=null;stripped.Seal();check(stripped.Hash==annual.Hash,"Legacy economy/campaign/culture ordering is unchanged");
            var atlas=HistoryAtlas.Read(s,"state",0);check(atlas.Facts.Single(f=>f.Label=="Founded").Value==SimulationClockView.Year(s,s.History.Polities[0].FoundedYear),"Atlas founding dates use the same civilization epoch as the HUD");
            var cityAtlas=HistoryAtlas.Read(s,"city",0);check(cityAtlas.EpochYear==SimulationClockView.EpochYear(s)&&cityAtlas.Facts.Single(f=>f.Label=="Founded").Value==SimulationClockView.Year(s,s.Society.Settlements[0].FoundedYear),"Pre-civilization city dates and atlas exports retain one consistent epoch");
            var scheduled=civ.AdvanceSeconds(s,100*SimulationTime.Year);civ.Validate(scheduled);
            var day=civ.AdvanceSeconds(s,SimulationTime.Day);check(ReferenceEquals(day.History,s.History)&&ReferenceEquals(day.Society,s.Society),"Clock-only updates reuse immutable map buffers");
            check(civ.AdvanceSeconds(civ.AdvanceSeconds(s,37*SimulationTime.Day),100*SimulationTime.Year-37*SimulationTime.Day).Hash==scheduled.Hash,"Civilized history respects sub-year batching");
            var upgraded=civ.UpgradeScheduling(fixedStart);civ.Validate(upgraded);check(upgraded.Timeline.Version=="event-calendar-2"&&upgraded.Years==s.Years&&ReferenceEquals(upgraded.History,fixedStart.History)&&fixedStart.Timeline.Version=="event-calendar-1","Explicit schedule upgrade preserves recorded history and source integrity");
            check(upgraded.Timeline.Events.Count(e=>e.Entity>=0)==s.Society.Cultures.Length&&upgraded.Timeline.Events.Where(e=>e.Entity>=0).Select(e=>e.Due).Distinct().Count()>1,"Each culture has an independent meaningful settlement update");
            var corruptActor=upgraded.Copy();corruptActor.Timeline.Events.First(e=>e.Entity>=0).Entity=999999;corruptActor.Seal();rejected=false;try{civ.Validate(corruptActor);}catch(ArgumentException){rejected=true;}check(rejected,"Invalid actor scheduling is rejected");
            var bounded=upgraded;long target=SimulationTime.Seconds(upgraded)+100*SimulationTime.Year;
            while(SimulationTime.Seconds(bounded)<target)bounded=civ.AdvanceSeconds(bounded,target-SimulationTime.Seconds(bounded),1);
            check(bounded.Hash==civ.AdvanceSeconds(upgraded,100*SimulationTime.Year).Hash,"Actor calendars retain budget-independent outcomes");
            old.Prehistory=upgraded;var saved=WorldFile.Read(WorldFile.Write(old));check(new Prehistory(saved).AdvanceSeconds(saved.Prehistory,100*SimulationTime.Year).Hash==bounded.Hash,"Actor windows survive binary save/load exactly");
            // The general history suite rewrites its diagnostic save at a much
            // later date, when all nearby sites may already be occupied. That
            // is valid history, but not a test of *new* settlement scheduling.
            // Rebuild the early reference from the same saved geography/rules.
            var growthWorld=new World();foreach(var field in typeof(World).GetFields())field.SetValue(growthWorld,field.GetValue(old));
            growthWorld.Prehistory=new PrehistoryState{Version="prehistory-6",History=new HistoryState{Version="history-5"}};
            var growthEngine=new Prehistory(growthWorld);var growthSource=growthEngine.AdvanceYears(growthEngine.Start(growthEngine.Prepare()),70000);
            var growing=growthEngine.UpgradeScheduling(FixedClock(growthSource));
            var developed=growthEngine.AdvanceSeconds(growing,2000*SimulationTime.Year);growthEngine.Validate(developed);
            var checkpoint=growthEngine.AdvanceSeconds(growing,777*SimulationTime.Year+13*SimulationTime.Day);var checkpointJson=WorldJson.Read<PrehistoryState>(WorldJson.Write(checkpoint));
            check(growthEngine.AdvanceSeconds(checkpointJson,1223*SimulationTime.Year-13*SimulationTime.Day).Hash==developed.Hash,"JSON reload and irregular partitions preserve real founding events and newly added actors");
            var founded=developed.Society.Settlements.Skip(growthSource.Society.Settlements.Length).ToArray();
            Console.WriteLine("New settlement founding years: "+string.Join(", ",founded.Select(t=>t.FoundedYear)));
            check(founded.Length>=3&&founded.Count(t=>t.FoundedYear%50!=0)>=founded.Length*.75,"Real settlement creation is no longer locked to 50-year boundaries");
            check(founded.Select(t=>t.FoundedYear%50).Distinct().Count()>=3,"Settlement founding years are dispersed, not cosmetically offset as a cohort");
        }
    }
}
