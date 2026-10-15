using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
namespace WorldGen.Core
{
    // Read-only diagnostic estimates. No timing data enters simulation state.
    public static class SimulationHashProfile
    {
        public static SimulationWorkMetric[] Capture(PrehistoryState source)
        {
            var results=new List<SimulationWorkMetric>();
            void Measure(string name,Func<string> hash){var watch=Stopwatch.StartNew();hash();results.Add(new SimulationWorkMetric{Stage=name,Milliseconds=watch.Elapsed.TotalMilliseconds});}
            Measure("complete checkpoint",source.ComputeHash);
            if(source.History!=null)
            {
                Measure("historical state",()=>StateHash.Compute(source.History));
                var reduced=source.History.Copy();reduced.Archive=new List<HistoricalEvent>();
                Measure("historical state without archive",()=>StateHash.Compute(reduced));
                foreach(var field in typeof(HistoryState).GetFields(BindingFlags.Instance|BindingFlags.Public))if(field.Name!=nameof(HistoryState.Archive))Measure("history."+field.Name,()=>StateHash.Compute(field.GetValue(source.History)));
            }
            if(source.Society!=null)foreach(var field in typeof(SocietyState).GetFields(BindingFlags.Instance|BindingFlags.Public))Measure("society."+field.Name,()=>StateHash.Compute(field.GetValue(source.Society)));
            return results.ToArray();
        }
    }
}
