using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Serialization;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public sealed class NpcPerformanceRow
    {
        public string Mode;public int Residents,Frames,Tiles,ScatterChunks;public bool TerrainReady;
        public double MeanNpcMs,P95NpcMs,P99NpcMs,MaxNpcMs,MeanFrameMs,P95FrameMs,P99FrameMs,MaxFrameMs,BackgroundQueryMs;
        public int UnfocusedFrames,Over10Ms,Over1667Ms,Width,Height,TreeQuality;public double Seconds,ViewDistanceM;public string Route;
        public double[] MeanStagesMs;
    }
    static IEnumerator MeasureLivingHumans(LocalTerrainExplorer owner,string output)
    {
        double deadline=Time.realtimeSinceStartupAsDouble+180;
        while((!owner.AllReady||owner.scatterJobs.Count>0||owner.installingScatter!=null||owner.scatterChunks.Count<Math.Min(4,owner.scatterWanted.Count))&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        int cap=Application.targetFrameRate;Application.targetFrameRate=-1;var report=new List<NpcPerformanceRow>();
        var arguments=Environment.GetCommandLineArgs();int option=Array.IndexOf(arguments,"--worldgen-npc-perf-seconds");double duration=30;
        if(option>=0&&option+1<arguments.Length)duration=Math.Max(5,Math.Min(180,double.Parse(arguments[option+1],System.Globalization.CultureInfo.InvariantCulture)));
        var group=owner.livingGroups.Values.Where(g=>g.CampLoaded&&!g.TraceOnly).OrderBy(g=>owner.HumanDistance(g.Anchor,owner.Position)).First();
        TerrainHandoff.Coordinates(owner.frame,group.Anchor,out double campX,out double campZ);float floor=(float)owner.VisibleGround(campX,campZ,0);
        var focus=new Vector3((float)(campX-owner.originX),floor+1,(float)(campZ-owner.originZ));
        // Repeat the same slow scenic orbit in every mode. No head whipping,
        // no invisible off-screen-only render target and no model inference.
        owner.npcModel??=new NpcModelClient();owner.npcModel.Config.Enabled=false;
        void CameraAt(double seconds){var offset=Quaternion.Euler(0,(float)(seconds/duration*360),0)*new Vector3(0,6,24);owner.view.transform.localPosition=focus+offset;owner.view.transform.LookAt(owner.transform.TransformPoint(focus));}
        owner.view.targetTexture=null;
        foreach(string mode in new[]{"normal","render-only","no-residents","normal-repeat"})
        {
            owner.livingBenchmarkFreeze=mode=="render-only"||mode=="no-residents";
            foreach(var actor in owner.livingPeople.Values){actor.Root.SetActive(mode!="no-residents");if(actor.InfantCarrier!=null)actor.InfantCarrier.SetActive(mode!="no-residents");}
            Debug.Log("NPC performance begins "+mode+" / "+owner.livingPeople.Count+" residents / "+owner.tiles.Count+" tiles; ready "+owner.AllReady);
            double warmUntil=Time.realtimeSinceStartupAsDouble+2;while(Time.realtimeSinceStartupAsDouble<warmUntil){CameraAt(0);yield return null;}
            var ticks=new List<double>(65536);var frames=new List<double>(65536);var stages=new double[5];double previous=Time.realtimeSinceStartupAsDouble,start=previous;int unfocused=0;
            while(Time.realtimeSinceStartupAsDouble-start<duration)
            {CameraAt(Time.realtimeSinceStartupAsDouble-start);yield return null;double now=Time.realtimeSinceStartupAsDouble;ticks.Add(owner.livingTickMs);frames.Add((now-previous)*1000);previous=now;if(!Application.isFocused)unfocused++;for(int k=0;k<stages.Length;k++)stages[k]+=owner.livingCosts[k];}
            for(int k=0;k<stages.Length;k++)stages[k]/=ticks.Count;
            ticks.Sort();frames.Sort();report.Add(new NpcPerformanceRow{Mode=mode,Residents=owner.livingPeople.Count,Frames=ticks.Count,Tiles=owner.tiles.Count,ScatterChunks=owner.scatterChunks.Count,TerrainReady=owner.AllReady,
                MeanNpcMs=ticks.Average(),P95NpcMs=ticks[(int)(ticks.Count*.95)],P99NpcMs=ticks[(int)(ticks.Count*.99)],MaxNpcMs=ticks.Last(),MeanFrameMs=frames.Average(),P95FrameMs=frames[(int)(frames.Count*.95)],P99FrameMs=frames[(int)(frames.Count*.99)],MaxFrameMs=frames.Last(),BackgroundQueryMs=owner.livingLastMs,MeanStagesMs=stages,UnfocusedFrames=unfocused,Over10Ms=frames.Count(v=>v>10),Over1667Ms=frames.Count(v=>v>1000d/60),Width=Screen.width,Height=Screen.height,TreeQuality=TerrainVideoOptions.TreeQuality,ViewDistanceM=owner.viewDistance,Seconds=previous-start,Route="Radius24m scenic camp orbit; same path and duration per mode"});
            File.WriteAllText(Path.Combine(output,"performance.json"),WorldJson.Write(report.ToArray()));
        }
        owner.livingBenchmarkFreeze=false;foreach(var actor in owner.livingPeople.Values)actor.Root.SetActive(true);Application.targetFrameRate=cap;
        File.WriteAllText(Path.Combine(output,"performance.json"),WorldJson.Write(report.ToArray()));
        Debug.Log("NPC measured main-thread normal "+report[0].MeanNpcMs.ToString("F3")+"ms / repeat "+report[3].MeanNpcMs.ToString("F3")+"ms; "+owner.livingPeople.Count+" residents");
    }
}
