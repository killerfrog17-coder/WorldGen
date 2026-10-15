using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    // Integer simulation seconds, never wall-clock timestamps. A non-leap,
    // 365-day calendar is intentional until world-specific calendars exist.
    public static class SimulationTime
    {
        public const long Minute=60,Hour=3600,Day=86400,Year=365*Day,Maximum=10000000*Year;
        public static long Seconds(PrehistoryState state)=>state.Timeline?.Seconds??state.Years*Year;
    }
    [Serializable] public sealed class SimulationEvent
    {
        public long Due,Ticket;
        public int Process,Entity=-1;
        public SimulationEvent Copy()=>(SimulationEvent)MemberwiseClone();
    }
    [Serializable] public sealed class SimulationTimeline
    {
        public string Version="event-calendar-1";
        public long Seconds,NextTicket,ProcessedEvents;
        public SimulationEvent[] Events=Array.Empty<SimulationEvent>();
        public SimulationTimeline Copy()=>new SimulationTimeline{Version=Version,Seconds=Seconds,NextTicket=NextTicket,ProcessedEvents=ProcessedEvents,Events=Events.Select(e=>e.Copy()).ToArray()};
    }
    // Reusable deterministic min-heap. Entity identity is part of the tie;
    // future assemblies/movement can use their own actors and cadences.
    public sealed class SimulationEventQueue
    {
        readonly List<SimulationEvent> heap=new List<SimulationEvent>();
        public static int Compare(SimulationEvent a,SimulationEvent b)
        {int c=a.Due.CompareTo(b.Due);if(c!=0)return c;c=a.Process.CompareTo(b.Process);if(c!=0)return c;c=a.Entity.CompareTo(b.Entity);return c!=0?c:a.Ticket.CompareTo(b.Ticket);}
        public SimulationEventQueue(IEnumerable<SimulationEvent> events){foreach(var e in events)Push(e.Copy());}
        public int Count=>heap.Count;
        public SimulationEvent Peek=>heap.Count==0?null:heap[0];
        public void Push(SimulationEvent e)
        {
            if(e==null||e.Due<0||e.Process<0||e.Entity< -1||e.Ticket<0)throw new ArgumentException("Invalid scheduled event.");
            e=e.Copy();int at=heap.Count;heap.Add(e);while(at>0){int parent=(at-1)/2;if(Compare(heap[parent],e)<=0)break;heap[at]=heap[parent];at=parent;}heap[at]=e;
        }
        public SimulationEvent Pop()
        {
            if(heap.Count==0)throw new InvalidOperationException("No scheduled event.");var result=heap[0];var last=heap[heap.Count-1];heap.RemoveAt(heap.Count-1);
            if(heap.Count>0){int at=0;while(at*2+1<heap.Count){int child=at*2+1;if(child+1<heap.Count&&Compare(heap[child+1],heap[child])<0)child++;if(Compare(last,heap[child])<=0)break;heap[at]=heap[child];at=child;}heap[at]=last;}return result;
        }
        public SimulationEvent[] Ordered()=>heap.OrderBy(e=>e,Comparer<SimulationEvent>.Create(Compare)).Select(e=>e.Copy()).ToArray();
        // Counter-derived timing windows: scheduling frequency does not change
        // when a host is slow, paused, reloaded, or requests smaller steps.
        public static long Window(int seed,string stream,int entity,int occurrence,long cadence,long halfWindow)
        {
            if(cadence<1||halfWindow<0||halfWindow>=cadence)throw new ArgumentOutOfRangeException(nameof(cadence));
            double unit=Rng.Value(seed,stream,unchecked(entity*7381+occurrence*193));
            return cadence+(long)Math.Round((2*unit-1)*halfWindow);
        }
    }
    public sealed partial class Prehistory
    {
        static bool StochasticCalendar(PrehistoryState s)=>s.Timeline?.Version=="event-calendar-2";
        static double ElapsedChance(double chance,double scale)=>scale==1?chance:D.Q(1-Math.Pow(1-D.Clamp(chance),scale),1000000000);
        long CalendarInterval(SimulationEvent e)
        {
            long years=e.Process==1?45:100,window=e.Process==1?15:20;
            if(e.Process==3){years=45;window=15;}
            if(e.Process==4){years=500;window=100;}
            return SimulationEventQueue.Window(World.Recipe.Seed,"calendar-2-process-"+e.Process,e.Entity,unchecked((int)e.Ticket),years*SimulationTime.Year,window*SimulationTime.Year);
        }
        SimulationTimeline InitialTimeline(PrehistoryState s,bool stochastic=true)
        {
            if(s.Version!="prehistory-4"&&s.Version!="prehistory-5"&&s.Version!="prehistory-6")throw new InvalidOperationException("This historical save uses its original fixed-step demographic model. Start a new simulation for calendar playback.");
            var timeline=new SimulationTimeline{Version=stochastic&&s.History!=null?"event-calendar-2":"event-calendar-1",Seconds=SimulationTime.Seconds(s)};var events=new List<SimulationEvent>();
            void Add(int process,int year)=>events.Add(new SimulationEvent{Due=year*SimulationTime.Year,Process=process,Ticket=timeline.NextTicket++});
            Add(0,(s.Years/100+1)*100);
            if(timeline.Version=="event-calendar-1")Add(1,(s.Years/50+1)*50);
            else
            {
                void RandomEvent(int process,int entity){var e=new SimulationEvent{Process=process,Entity=entity,Ticket=timeline.NextTicket++};e.Due=timeline.Seconds+CalendarInterval(e);events.Add(e);}
                RandomEvent(1,-1);
                foreach(var c in s.Society.Cultures)RandomEvent(s.Society.Settlements.Any(t=>t.CultureId==c.Id&&t.Population>=100)?4:3,c.Id);
            }
            if(s.Society.Settlements.Length>0)Add(2,s.Years+1);
            timeline.Events=events.OrderBy(e=>e,Comparer<SimulationEvent>.Create(SimulationEventQueue.Compare)).ToArray();return timeline;
        }
        // Reading an old save never rewrites it. Interactive hosts explicitly
        // opt into the new timing contract when its playback is resumed.
        public PrehistoryState UpgradeScheduling(PrehistoryState source)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            if(AggregateHistory(source))return source;
            if(source.Timeline?.Version!="event-calendar-1"||source.History==null)return source;
            Validate(source);
            var s=source.CopyClockOnly();s.Timeline=InitialTimeline(source);s.Seal();return s;
        }
        public long NextSimulationEvent(PrehistoryState source)
        {Validate(source);var timeline=source.Timeline??InitialTimeline(source);return timeline.Events[0].Due;}
        internal long NextOwnedSimulationEvent(PrehistoryState source)=>(source.Timeline??InitialTimeline(source)).Events[0].Due;
        public PrehistoryState AdvanceSeconds(PrehistoryState source,long seconds,int maximumEvents=int.MaxValue,CancellationToken token=default)
            =>AdvanceCalendar(source,seconds,maximumEvents,token,true);
        internal PrehistoryState AdvanceOwnedSeconds(PrehistoryState source,long seconds,int maximumEvents,CancellationToken token,int maximumGlobalWork=int.MaxValue)
            =>AdvanceCalendar(source,seconds,maximumEvents,token,false,maximumGlobalWork);
        PrehistoryState AdvanceCalendar(PrehistoryState source,long seconds,int maximumEvents,CancellationToken token,bool validateInput,int maximumGlobalWork=int.MaxValue)
        {
            if(AggregateHistory(source))return AdvanceAggregateCalendar(source,seconds,maximumEvents,token,validateInput,maximumGlobalWork);
            if(validateInput)using(ProfileWork("checkpoint validation"))Validate(source);if(!source.Started)throw new InvalidOperationException("Start the world first.");
            long now=SimulationTime.Seconds(source);if(seconds<1||seconds>SimulationTime.Maximum-now||maximumEvents<1||maximumGlobalWork<1)throw new ArgumentOutOfRangeException(nameof(seconds));
            token.ThrowIfCancellationRequested();long target=now+seconds;var timeline=source.Timeline?.Copy()??InitialTimeline(source);var queue=new SimulationEventQueue(timeline.Events);
            // A trusted, townless private checkpoint can requeue ineligible
            // local opportunities without copying/invalidating the terrain-sized
            // domain. Public inputs never get this shortcut: their aggregates
            // may have been edited and must be refreshed by the ordinary path.
            bool trusted=!validateInput&&UseHistoryIndexes&&ReferenceEquals(historyWorkIndex?.State,source)&&historyWorkIndex.Hash==source.Hash&&historyWorkIndex.TotalsVerified&&historyWorkIndex.StagesVerified;
            bool idleOpportunities=trusted&&source.Society.Settlements.Length==0&&source.History.Polities.Length==0&&timeline.Events.Where(e=>e.Due<=target).All(e=>e.Process==3&&(source.History.Cultures[e.Entity].Sedentism<=400||source.History.Cultures[e.Entity].Surplus<=230||source.Society.Cultures[e.Entity].Population<=1800));
            bool changes=queue.Peek.Due<=target&&!idleOpportunities;PrehistoryState s;using(ProfileWork("checkpoint copy"))s=changes?source.Copy():source.CopyClockOnly();s.Timeline=timeline;
            BeginHistoricalWork(source,s);bool completed=false;
            try {
            int processed=0,scheduledCultures=s.Society.Cultures.Length;long globalWork=0;bool annualScheduled=s.Society.Settlements.Length>0;
            void Schedule(int process,long due)=>queue.Push(new SimulationEvent{Process=process,Due=due,Ticket=timeline.NextTicket++});
            void ScheduleRandom(int process,int entity,long nowDue){var next=new SimulationEvent{Process=process,Entity=entity,Ticket=timeline.NextTicket++};next.Due=nowDue+CalendarInterval(next);queue.Push(next);}
            void EnsureTownSchedules(long due)
            {
                // Existing actors already own exactly one pending opportunity.
                // Only newly appended cultures need registration; sorting and
                // rescanning the entire queue at every actor event is quadratic.
                while(scheduledCultures<s.Society.Cultures.Length)
                {int id=scheduledCultures++;ScheduleRandom(s.Society.Settlements.Any(t=>t.CultureId==id&&t.Population>=100)?4:3,id,due);}
                if(s.Society.Settlements.Length>0&&!annualScheduled){Schedule(2,(due/SimulationTime.Year+1)*SimulationTime.Year);annualScheduled=true;}
            }
            while(queue.Count>0&&queue.Peek.Due<=target&&(processed<maximumEvents&&globalWork<maximumGlobalWork||queue.Peek.Due==timeline.Seconds))
            {
                token.ThrowIfCancellationRequested();var e=queue.Pop();timeline.Seconds=e.Due;s.Years=(int)(e.Due/SimulationTime.Year);
                using(ProfileWork("event "+e.Process)){
                if(idleOpportunities){using(ProfileWork("idle settlement opportunity",1))ScheduleRandom(3,e.Entity,e.Due);}
                else if(e.Process==0){AdvanceDemographicBoundary(s,token);Schedule(0,e.Due+100*SimulationTime.Year);}
                else if(e.Process==1)
                {
                    bool hadTowns=s.Society.Settlements.Length>0;
                    // Population totals are presentation aggregates refreshed at
                    // checkpoints. Recompute before authoritative readers so a
                    // host's request/budget boundaries cannot influence a trial.
                    if(StochasticCalendar(s))RefreshSocietyTotals(s);
                    if(s.History!=null)AdvanceHistoricalCultures(s,token,StochasticCalendar(s)?CalendarInterval(e)/(double)SimulationTime.Year:50);else AdvanceCultures(s,token);
                    if(StochasticCalendar(s)){ScheduleRandom(1,-1,e.Due);EnsureTownSchedules(e.Due);}else Schedule(1,e.Due+50*SimulationTime.Year);
                    // A newly founded town has its first budget at this boundary,
                    // after culture formation, exactly as in the annual model.
                    if(!StochasticCalendar(s)&&!hadTowns&&s.Society.Settlements.Length>0)Schedule(2,e.Due);
                }
                else if(e.Process==2){if(s.History!=null)AdvanceHistoricalYear(s,token);else AdvanceSettlements(s);Schedule(2,e.Due+SimulationTime.Year);}
                else if(StochasticCalendar(s)&&(e.Process==3||e.Process==4))
                {
                    RefreshSocietyTotals(s);
                    AdvanceSettlementOpportunity(s,e.Entity,token);
                    ScheduleRandom(s.Society.Settlements.Any(t=>t.CultureId==e.Entity&&t.Population>=100)?4:3,e.Entity,e.Due);
                    EnsureTownSchedules(e.Due);RefreshHistoricalStages(s);if(!s.Society.ManualStep)s.Society.SelectedStepYears=AutomaticYears(s);
                }
                else throw new ArgumentException("Unregistered simulation process.");
                // Execution-only work estimates, never calendar or historical
                // decisions. Small town systems can process many annual events
                // per frame; large global passes yield after one timestamp.
                processed++;
                if(e.Process<=2)globalWork+=e.Process==2?Math.Min(128,Math.Max(1,s.Society.Settlements.Length+(s.History?.Polities.Length??0))):e.Process==0?Math.Min(128,32+Patches.Length/128):Math.Min(128,32+s.Society.Cultures.Length/2+s.Society.Settlements.Length/4+(WorkIndex(s)?.CulturalFrontier.Count??0)/256);
                timeline.ProcessedEvents++;
                }
            }
            // Never skip work to pretend we achieved the requested speed. If a
            // budget expires, publish only fully processed timestamps. Do not
            // split coincident phases into a partially visible checkpoint.
            if(queue.Count==0||queue.Peek.Due>target)timeline.Seconds=target;
            s.Years=(int)(timeline.Seconds/SimulationTime.Year);timeline.Events=queue.Ordered();if(changes)using(ProfileWork("checkpoint totals"))RefreshSocietyTotals(s);using(ProfileWork("checkpoint hash"))s.Seal();completed=true;return s;
            } finally {EndHistoricalWork(s,completed);}
        }
        void ValidateTimeline(PrehistoryState s)
        {
            if(AggregateHistory(s)){ValidateAggregateTimeline(s);return;}
            var t=s.Timeline;if(t==null)return;
            bool stochastic=t.Version=="event-calendar-2";
            bool valid=(t.Version=="event-calendar-1"||stochastic&&s.History!=null)&&s.Society!=null&&t.Seconds>=0&&t.Seconds<=SimulationTime.Maximum&&s.Years==t.Seconds/SimulationTime.Year&&t.NextTicket>=0&&t.ProcessedEvents>=0&&t.Events!=null;
            if(!valid)throw new ArgumentException("Invalid simulation calendar.");
            var expected=s.Society.Settlements.Length>0?new[]{0,1,2}:new[]{0,1};
            var global=t.Events.Where(e=>e!=null&&e.Entity== -1).ToArray();
            if(global.Length!=expected.Length||!global.Select(e=>e.Process).OrderBy(x=>x).SequenceEqual(expected))throw new ArgumentException("Missing or duplicate simulation process.");
            var actors=t.Events.Where(e=>e!=null&&e.Entity>=0).ToArray();
            if(t.Events.Length!=expected.Length+actors.Length)throw new ArgumentException("Invalid scheduled entity.");
            if(!stochastic&&actors.Length>0||stochastic&&(actors.Length!=s.Society.Cultures.Length||!actors.Select(e=>e.Entity).OrderBy(x=>x).SequenceEqual(Enumerable.Range(0,s.Society.Cultures.Length))))throw new ArgumentException("Missing or duplicate actor process.");
            for(int i=0;i<t.Events.Length;i++)
            {var e=t.Events[i];if(e==null)throw new ArgumentException("Null scheduled process.");long interval=(e.Process==0?100:e.Process==1?50:1)*SimulationTime.Year;long due=(t.Seconds/interval+1)*interval;
                bool random=stochastic&&(e.Process==1||e.Process==3||e.Process==4);
                if(e.Ticket<0||e.Ticket>=t.NextTicket||e.Due<=t.Seconds||random&&(e.Due-t.Seconds>CalendarInterval(e)||(e.Process==1)!=(e.Entity== -1))||!random&&(e.Entity!= -1||e.Due!=due)||i>0&&SimulationEventQueue.Compare(t.Events[i-1],e)>=0)throw new ArgumentException("Invalid scheduled process or ordering.");}
            if(t.Events.Select(e=>e.Ticket).Distinct().Count()!=t.Events.Length)throw new ArgumentException("Duplicate schedule ticket.");
        }
    }
}
