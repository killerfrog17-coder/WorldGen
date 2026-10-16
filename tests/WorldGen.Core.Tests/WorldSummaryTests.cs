using System;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class WorldSummaryTests
{
    public static void Run(Action<bool,string> check)
    {
        var world=new World{Surface=new Surface{Cells=Array.Empty<Cell>()}};
        world.Metrics.Add(new Metric{Name="finite",Value=.25});
        world.Metrics.Add(new Metric{Name="undefined land ratio",Value=double.NaN});
        world.Metrics.Add(new Metric{Name="positive infinity",Value=double.PositiveInfinity});
        world.Metrics.Add(new Metric{Name="negative infinity",Value=double.NegativeInfinity});
        world.Warnings.Add("Existing saved warning");
        string before=StateHash.Compute(world);
        var summary=WorldSummary.From(world);
        check(summary.Metrics.Count==1&&summary.Metrics[0].Name=="finite"&&summary.Metrics[0].Value==.25,"Display summary omits undefined diagnostics rather than replacing them with zero");
        check(summary.Warnings.Count==2&&summary.Warnings[1].Contains("undefined land ratio")&&summary.Warnings[1].Contains("positive infinity")&&summary.Warnings[1].Contains("negative infinity"),"Display warns explicitly which diagnostics are undefined");
        using(var document=JsonDocument.Parse(WorldJson.Write(summary)))check(document.RootElement.GetProperty("Metrics").GetArrayLength()==1,"Summary is valid strict JSON for browser JSON.parse");
        check(world.Metrics.Count==4&&double.IsNaN(world.Metrics[1].Value)&&world.Warnings.Count==1&&StateHash.Compute(world)==before,"Summary does not mutate accepted world diagnostics, warnings or canonical hash");
        world.Metrics.RemoveRange(1,3);
        var ordinary=WorldSummary.From(world);
        check(ReferenceEquals(ordinary.Metrics,world.Metrics)&&ReferenceEquals(ordinary.Warnings,world.Warnings),"Ordinary finite summaries retain existing compact behavior without defensive world copies");
    }
}
