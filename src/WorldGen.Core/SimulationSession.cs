using System;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    // Playback owns a private validated checkpoint. Published states are
    // detached copies: a UI/export caller cannot mutate the next input. This
    // avoids rehashing a trusted private input before every queued event while
    // keeping public AdvanceSeconds fully validating arbitrary callers/saves.
    public sealed class SimulationSession
    {
        readonly Prehistory engine;PrehistoryState current;
        public PrehistoryState LastSnapshot {get;private set;}
        public long Seconds=>SimulationTime.Seconds(current);
        public string Hash=>current.Hash;
        public long NextEventDue=>engine.NextOwnedSimulationEvent(current);
        public SimulationSession(Prehistory engine,PrehistoryState source)
        {
            this.engine=engine??throw new ArgumentNullException(nameof(engine));engine.Validate(source);
            current=DetachedCopy(engine.UpgradeScheduling(source));LastSnapshot=source;
        }
        public PrehistoryState AdvanceSeconds(long seconds,int maximumEvents=1,CancellationToken token=default,int maximumGlobalWork=int.MaxValue)
        {
            var next=engine.AdvanceOwnedSeconds(current,seconds,maximumEvents,token,maximumGlobalWork);
            // Commit only after all work and publication copying succeed.
            var publicationTimer=System.Diagnostics.Stopwatch.StartNew();PrehistoryState publication;using(engine.ProfileWork("publication copy"))publication=DetachedCopy(next);token.ThrowIfCancellationRequested();if(next.History?.Aggregates!=null)engine.YearWorkObserver?.Invoke(next.Years,publicationTimer.Elapsed.TotalMilliseconds);current=next;LastSnapshot=publication;return publication;
        }
        static PrehistoryState DetachedCopy(PrehistoryState source)
        {
            var copy=source.Copy();
            copy.DomainToken=source.DomainToken;
            // The archive fork isolates records on first public access. Frozen
            // pages can be shared without exposing mutable private records or
            // cloning the entire lifetime chronicle for each publication.
            if(copy.Society!=null)copy.Society.Events=source.Society.Events.Select(e=>new ChronicleEvent{Year=e.Year,Cell=e.Cell,CultureId=e.CultureId,Category=e.Category,Text=e.Text}).ToArray();
            return copy;
        }
        public PrehistoryState Snapshot()=>DetachedCopy(current);
    }
}
