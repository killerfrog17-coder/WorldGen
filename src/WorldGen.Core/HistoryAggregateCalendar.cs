using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        // Diagnostic only: observers cannot change authoritative dates or work.
        public Action<int,double> YearWorkObserver;
        const int RegionProcess=20,CityProcess=21,CultureProcess=22,StateProcess=23,CampaignProcess=24,RemoteProcess=26;
        long AggregateInterval(int process,int entity,long ticket,PrehistoryState state=null)
        {
            double cadence=process==RegionProcess||process==RemoteProcess?100:process==CityProcess||process==CampaignProcess?1:process==CultureProcess?40:5;
            if(ProvincialHistory(state)&&(process==CultureProcess||process==RegionProcess))
            {
                long mask=process==CultureProcess?ProvincialKnowledge(state.History.Cultures[entity]):state.History.Aggregates.Provinces.RegionalKnowledge[entity];
                if(process==RegionProcess){int c=state.History.Aggregates.Regions[entity].Culture;if(c>=0)mask|=ProvincialKnowledge(state.History.Cultures[c]);}
                if((mask&(1L<<(int)HistoricalTechnology.Telecommunications))!=0)cadence=2;
                else if((mask&(1L<<(int)HistoricalTechnology.Telegraph))!=0)cadence=5;
                else if((mask&(1L<<(int)HistoricalTechnology.Printing))!=0)cadence=process==CultureProcess?15:20;
            }
            if(TechnicalHistory(state)&&process==CultureProcess)
            {
                var known=state.History.Cultures[entity].Technical;
                if(known.Offers[(int)HistoricalTechnology.Telecommunications]>0&&state.Years-known.OfferYear[(int)HistoricalTechnology.Telecommunications]<=100)cadence=Math.Min(cadence,2);
                else if(known.Offers[(int)HistoricalTechnology.Telegraph]>0&&state.Years-known.OfferYear[(int)HistoricalTechnology.Telegraph]<=100)cadence=Math.Min(cadence,5);
                else if(known.Offers[(int)HistoricalTechnology.Printing]>0&&state.Years-known.OfferYear[(int)HistoricalTechnology.Printing]<=250)cadence=Math.Min(cadence,15);
            }
            // Fast communications must not turn every empty/remote habitat into
            // a biennial census. Analytic population and adoption use the actual
            // elapsed time; city, conflict and political calendars stay separate.
            if(ColonialHistory(state)){if(process==RegionProcess)cadence=Math.Max(10,cadence);if(process==CultureProcess)cadence=Math.Max(5,cadence);}
            if(process==CultureProcess&&state?.History?.RegionalTraditions?.Retired[entity]>=0)cadence=10000;
            return SimulationEventQueue.Window(World.Recipe.Seed,"aggregate-1-"+process,entity,unchecked((int)ticket),(long)(cadence*SimulationTime.Year),(long)(cadence*.3*SimulationTime.Year));
        }
        SimulationTimeline InitialAggregateTimeline(PrehistoryState s)
        {
            var t=new SimulationTimeline{Version="event-calendar-3",Seconds=SimulationTime.Seconds(s)};var events=new List<SimulationEvent>();
            void Add(int process,int id){long ticket=t.NextTicket++;long interval=AggregateInterval(process,id,ticket);double phase=Rng.Value(World.Recipe.Seed,"aggregate-initial-phase-"+process,id);events.Add(new SimulationEvent{Process=process,Entity=id,Ticket=ticket,Due=t.Seconds+Math.Max(1,(long)(interval*(.05+.95*phase)))});}
            foreach(var r in s.History.Aggregates.Regions)Add(RegionProcess,r.Id);
            foreach(var c in s.History.Aggregates.Cities)if(!LivingHistory(s)||s.History.Aggregates.Living.Cities[c.Id].AbandonedYear<0)Add(CityProcess,c.Id);
            foreach(var c in s.History.Aggregates.Cultures)Add(CultureProcess,c.Id);
            foreach(var p in s.History.Polities)if(p.EndedYear<0)Add(StateProcess,p.Id);
            foreach(var c in s.History.Dynamics.Campaigns)if(c.EndedYear<0)Add(CampaignProcess,c.Id);
            foreach(var p in s.History.Polities)if(p.EndedYear>=0&&s.History.Aggregates.Polities[p.Id].Remote>0)Add(RemoteProcess,p.Id);
            t.Events=events.OrderBy(e=>e,Comparer<SimulationEvent>.Create(SimulationEventQueue.Compare)).ToArray();return t;
        }
        // Only in an active private transaction; never survives cancellation.
        SimulationEventQueue aggregateQueue;
        void ScheduleAggregate(PrehistoryState s,int process,int id,long due=0)
        {long ticket=s.Timeline.NextTicket++;aggregateQueue.Push(new SimulationEvent{Process=process,Entity=id,Ticket=ticket,Due=due>0?due:s.Timeline.Seconds+AggregateInterval(process,id,ticket,s)});}
        PrehistoryState AdvanceAggregateCalendar(PrehistoryState source,long seconds,int maximumEvents,CancellationToken token,bool validate,int workBudget)
        {
            if(validate)Validate(source);if(!source.Started)throw new InvalidOperationException("Start the world first.");
            long now=SimulationTime.Seconds(source);if(seconds<1||seconds>SimulationTime.Maximum-now||maximumEvents<1||workBudget<1)throw new ArgumentOutOfRangeException(nameof(seconds));
            token.ThrowIfCancellationRequested();var overhead=Stopwatch.StartNew();PrehistoryState s;using(ProfileWork("aggregate checkpoint copy"))s=validate?source.Copy():source.CopyAggregateOwned();var a=s.History.Aggregates;long target=now+seconds;int count=0;
            var queue=new SimulationEventQueue(s.Timeline.Events);aggregateQueue=queue;PrepareAggregateRelations(source);double eventMilliseconds=0;bool completed=false;
            try
            {
                // A budget limits local actor events, not a world-sized annual
                // pass. Coincident timestamps remain atomic, preserving saves.
                int limit=Math.Min(maximumEvents,workBudget==int.MaxValue?int.MaxValue:Math.Max(64,workBudget*8));
                while(queue.Count>0&&queue.Peek.Due<=target&&(count<limit||queue.Peek.Due==s.Timeline.Seconds))
                {
                    token.ThrowIfCancellationRequested();var e=queue.Pop();s.Timeline.Seconds=e.Due;s.Years=(int)(e.Due/SimulationTime.Year);long started=Stopwatch.GetTimestamp(),reads=a.CellReads;
                    // Optional diagnostic only, never a calendar/work-budget
                    // input. Count this worker's allocations, not total heap
                    // movement caused by unrelated rendering/other threads.
                    long allocated=WorkObserver==null?0:GC.GetAllocatedBytesForCurrentThread();
                    switch(e.Process)
                    {
                        case RegionProcess:AdvanceAggregateRegion(s,e.Entity);ScheduleAggregate(s,e.Process,e.Entity);break;
                        case CityProcess:if(!LivingHistory(s)||a.Living.Cities[e.Entity].AbandonedYear<0){AdvanceAggregateCity(s,e.Entity);if(!LivingHistory(s)||a.Living.Cities[e.Entity].AbandonedYear<0)ScheduleAggregate(s,e.Process,e.Entity);}break;
                        case CultureProcess:AdvanceAggregateCulture(s,e.Entity);ScheduleAggregate(s,e.Process,e.Entity);break;
                        case StateProcess:if(s.History.Polities[e.Entity].EndedYear<0){AdvanceAggregateState(s,e.Entity);if(s.History.Polities[e.Entity].EndedYear<0)ScheduleAggregate(s,e.Process,e.Entity);}break;
                        case CampaignProcess:if(s.History.Dynamics.Campaigns[e.Entity].EndedYear<0){AdvanceAggregateCampaign(s,e.Entity);if(s.History.Dynamics.Campaigns[e.Entity].EndedYear<0)ScheduleAggregate(s,e.Process,e.Entity);}break;
                        case RemoteProcess:if(a.Polities[e.Entity].Remote>0){AdvanceAggregateRemote(s,e.Entity);if(a.Polities[e.Entity].Remote>0)ScheduleAggregate(s,e.Process,e.Entity);}break;
                        case SeaMissionProcess:AdvanceSeaMissions(s,e.Entity);break;
                        default:throw new ArgumentException("Unknown aggregate process.");
                    }
                    double elapsed=(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;eventMilliseconds+=elapsed;YearWorkObserver?.Invoke(s.Years,elapsed);WorkObserver?.Invoke(new SimulationWorkMetric{Stage=AggregateProcessLabel(e.Process),Items=(int)(a.CellReads-reads),Milliseconds=elapsed,AllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-allocated});a.ProcessedLocalEvents++;count++;s.Timeline.ProcessedEvents++;
                }
                if(queue.Count==0||queue.Peek.Due>target)s.Timeline.Seconds=target;
                s.Years=(int)(s.Timeline.Seconds/SimulationTime.Year);s.Timeline.Events=queue.Ordered();s.DomainToken=new object();using(ProfileWork("aggregate checkpoint hash"))s.SealAggregateOwned(token);
                YearWorkObserver?.Invoke(s.Years,Math.Max(0,overhead.Elapsed.TotalMilliseconds-eventMilliseconds));aggregateRelationCheckpoint=s.Hash;completed=true;return s;
            }
            finally {aggregateQueue=null;if(!completed)aggregateRelationCheckpoint=null;}
        }
        static string AggregateProcessLabel(int process)=>"aggregate event "+process;
        void ValidateAggregateTimeline(PrehistoryState s)
        {
            var t=s.Timeline;var a=s.History.Aggregates;
            if(t==null||t.Version!="event-calendar-3"||t.Seconds<0||s.Years!=t.Seconds/SimulationTime.Year||t.Events==null)throw new ArgumentException("Invalid aggregate calendar.");
            var identities=new HashSet<(int,int)>();var tickets=new HashSet<long>();
            for(int i=0;i<t.Events.Length;i++)
            {
                var e=t.Events[i];int length=e.Process==RegionProcess?a.Regions.Length:e.Process==CityProcess?a.Cities.Length:e.Process==CultureProcess?a.Cultures.Length:e.Process==StateProcess||e.Process==RemoteProcess?a.Polities.Length:e.Process==CampaignProcess?s.History.Dynamics.Campaigns.Length:e.Process==SeaMissionProcess&&GeographicHistory(s)?a.Polities.Length:0;
                if(e.Entity<0||e.Entity>=length||e.Due<=t.Seconds||e.Ticket<0||e.Ticket>=t.NextTicket||!identities.Add((e.Process,e.Entity))||!tickets.Add(e.Ticket)||i>0&&SimulationEventQueue.Compare(t.Events[i-1],e)>=0)throw new ArgumentException("Invalid aggregate actor schedule.");
            }
            if(!a.Regions.All(r=>identities.Contains((RegionProcess,r.Id)))||!a.Cities.Where(c=>!LivingHistory(s)||a.Living.Cities[c.Id].AbandonedYear<0).All(c=>identities.Contains((CityProcess,c.Id)))||!a.Cultures.All(c=>identities.Contains((CultureProcess,c.Id)))||!s.History.Polities.Where(p=>p.EndedYear<0).All(p=>identities.Contains((StateProcess,p.Id))))throw new ArgumentException("Missing aggregate actor schedule.");
        }
    }
}
