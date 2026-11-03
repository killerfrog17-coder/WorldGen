using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    public sealed class NpcPerformanceRow
    {
        public string Mode;public int Residents,Frames,Tiles,ScatterChunks;public bool TerrainReady;
        public double MeanNpcMs,P95NpcMs,MaxNpcMs,MeanFrameMs,P95FrameMs,BackgroundQueryMs;
    }
    static IEnumerator MeasureLivingHumans(LocalTerrainExplorer owner,string output)
    {
        double deadline=Time.realtimeSinceStartupAsDouble+180;
        while((!owner.AllReady||owner.scatterJobs.Count>0||owner.installingScatter!=null||owner.scatterChunks.Count<Math.Min(4,owner.scatterWanted.Count))&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        int cap=Application.targetFrameRate;Application.targetFrameRate=-1;var report=new List<NpcPerformanceRow>();
        foreach(string mode in new[]{"normal","render-only","no-residents","normal-repeat"})
        {
            owner.livingBenchmarkFreeze=mode=="render-only"||mode=="no-residents";
            foreach(var actor in owner.livingPeople.Values)actor.Root.SetActive(mode!="no-residents");
            for(int i=0;i<30;i++)yield return null;
            var ticks=new List<double>();var frames=new List<double>();double previous=Time.realtimeSinceStartupAsDouble;
            for(int i=0;i<240;i++){yield return null;double now=Time.realtimeSinceStartupAsDouble;ticks.Add(owner.livingTickMs);frames.Add((now-previous)*1000);previous=now;}
            ticks.Sort();frames.Sort();report.Add(new NpcPerformanceRow{Mode=mode,Residents=owner.livingPeople.Count,Frames=ticks.Count,Tiles=owner.tiles.Count,ScatterChunks=owner.scatterChunks.Count,TerrainReady=owner.AllReady,
                MeanNpcMs=ticks.Average(),P95NpcMs=ticks[(int)(ticks.Count*.95)],MaxNpcMs=ticks.Last(),MeanFrameMs=frames.Average(),P95FrameMs=frames[(int)(frames.Count*.95)],BackgroundQueryMs=owner.livingLastMs});
        }
        owner.livingBenchmarkFreeze=false;foreach(var actor in owner.livingPeople.Values)actor.Root.SetActive(true);Application.targetFrameRate=cap;
        File.WriteAllText(Path.Combine(output,"performance.json"),WorldJson.Write(report.ToArray()));
        Debug.Log("NPC measured main-thread normal "+report[0].MeanNpcMs.ToString("F3")+"ms / repeat "+report[3].MeanNpcMs.ToString("F3")+"ms; "+owner.livingPeople.Count+" residents");
    }
}
