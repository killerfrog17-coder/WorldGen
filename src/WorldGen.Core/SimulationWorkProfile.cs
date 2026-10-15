using System;
using System.Diagnostics;
namespace WorldGen.Core
{
    // Optional, read-only diagnostics. Elapsed time must never determine model
    // decisions, event dates, tie order or authoritative work budgets.
    public sealed class SimulationWorkMetric
    {
        public string Stage;
        public int Items;
        public double Milliseconds;
    }
    public sealed partial class Prehistory
    {
        public Action<SimulationWorkMetric> WorkObserver { get; set; }
        internal IDisposable ProfileWork(string stage,int items=0)
            =>WorkObserver==null?null:new WorkMeasurement(WorkObserver,stage,items);
        sealed class WorkMeasurement:IDisposable
        {
            readonly Action<SimulationWorkMetric> observer;readonly string stage;readonly int items;readonly long started;
            public WorkMeasurement(Action<SimulationWorkMetric> observer,string stage,int items)
            {this.observer=observer;this.stage=stage;this.items=items;started=Stopwatch.GetTimestamp();}
            public void Dispose()=>observer(new SimulationWorkMetric{Stage=stage,Items=items,Milliseconds=(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency});
        }
    }
}
