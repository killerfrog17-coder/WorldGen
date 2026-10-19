using System;
using System.Collections.Generic;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    // Small UI state: the browser never deserializes the authoritative mesh.
    public sealed class WorldSummary
    {
        public Recipe Recipe = new Recipe(); public int CellCount;
        public double PrimeMeridianLongitude;
        public List<StageRecord> Stages = new List<StageRecord>(); public List<Metric> Metrics = new List<Metric>(); public List<string> Warnings = new List<string>();
        public static WorldSummary From(World world)
        {
            var summary=new WorldSummary { Recipe=world.Recipe, CellCount=world.Surface.Cells.Length, Stages=world.Stages, Metrics=world.Metrics, Warnings=world.Warnings };
            if(summary.CellCount>0&&world.ElevationM.Length==summary.CellCount&&world.Hydrology.OceanConnected.Length==summary.CellCount)summary.PrimeMeridianLongitude=WorldMeridian.For(world).PrimeLongitude;
            List<string>? omitted=null;
            foreach(var metric in world.Metrics)
                if(double.IsNaN(metric.Value)||double.IsInfinity(metric.Value))
                {
                    if(omitted==null){omitted=new List<string>();summary.Metrics=new List<Metric>();}
                    omitted.Add(metric.Name);
                }
            if(omitted!=null)
            {
                // An all-ocean world has no denominator for some land ratios.
                // Leave its accepted data/hash untouched, but never emit NaN/INF
                // into the browser's strict JSON summary or pretend it is zero.
                foreach(var metric in world.Metrics)if(!double.IsNaN(metric.Value)&&!double.IsInfinity(metric.Value))summary.Metrics.Add(metric);
                summary.Warnings=new List<string>(world.Warnings){"Undefined diagnostics omitted from this display: "+string.Join(", ",omitted)+"."};
            }
            return summary;
        }
    }
}
