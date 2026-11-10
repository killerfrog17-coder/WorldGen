using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    // Uses the landscape harness's canonical site, renderer and worker. No
    // human library or living-world planner is initialized for this check.
    IEnumerator CheckChunkLoading(string output,double preparationMs)
    {
        if(!animalDisabled||PlayerRules.Mode!=GameMode.Spectator)
            throw new InvalidOperationException("Chunk check requires --worldgen-no-animals and spectator");
        var rows=new List<string>{"seconds,terrainM,forestM,readyM,drawnFogM,tiles,terrainJobs,completedJobs,scatterJobs,forestJobs,activeWorkers"};
        var frames=new List<double>();var milestones=new Dictionary<string,double>();
        double start=Time.realtimeSinceStartupAsDouble,last=start,next=start,deadline=start+180;
        double[] radii={50,100,300,600};bool viewed=false;
        BeginLoadingProfile();
        while(Time.realtimeSinceStartupAsDouble<deadline)
        {
            double now=Time.realtimeSinceStartupAsDouble,elapsed=now-start;
            frames.Add((now-last)*1000);last=now;
            if(Error!="")throw new InvalidOperationException(Error);
            if(Ready&&walkingPatch!=null&&!viewed){TestView(3,20,5);viewed=true;}
            if(NearReady&&!milestones.ContainsKey("near"))milestones["near"]=elapsed;
            foreach(double radius in radii)
            {
                string name="radius"+radius;
                if(Math.Min(terrainRevealRadius,forestRevealRadius)>=radius&&!milestones.ContainsKey(name))milestones[name]=elapsed;
            }
            if(now>=next)
            {
                next=now+.25;
                rows.Add(string.Join(",",new[]{elapsed,terrainRevealRadius,forestRevealRadius,fogSafeRadius,renderedStreamingFog,tiles.Count,jobs.Count,jobs.Count(j=>j.Task.IsCompleted),scatterJobs.Count,forestJobs.Count,ActiveWorkerCount}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
            }
            if(AllReady&&!DetailPlanning&&jobs.Count==0&&!ForestPending&&ScatterReady&&milestones.ContainsKey("radius600"))
            {milestones["complete"]=elapsed;break;}
            yield return null;
        }
        if(humanPlayer!=null||livingPlanner!=null||humanNpcs.Count!=0||livingPeople.Count!=0||livingGroups.Count!=0)
            throw new InvalidOperationException("Chunk check contains a human or camp workload");
        File.WriteAllLines(Path.Combine(output,"chunk-loading.csv"),rows);
        frames.Sort();double Percentile(double p)=>frames.Count==0?0:frames[Math.Min(frames.Count-1,(int)(frames.Count*p))];
        var report=new List<string>{"Empty spectator; no humans, communities, camps, or fauna; no saved preferences changed.",
            "Shared PC: MapleLegends and YouTube. Frame figures include 60 FPS pacing, not an FPS benchmark.",
            "Preparation ms: "+preparationMs.ToString("F3"),"Detail: "+detailOptions.Quality+"; effort: "+detailOptions.Effort+"; view: "+viewDistance,
            "Legacy scheduler: "+Environment.GetCommandLineArgs().Contains("--worldgen-loader-legacy"),
            "Frame p99/max ms: "+Percentile(.99).ToString("F3")+" / "+Percentile(1).ToString("F3"),
            "Frames >50ms / >100ms: "+frames.Count(f=>f>50)+" / "+frames.Count(f=>f>100),EndLoadingProfile(),Diagnostics,PerformanceForestState};
        report.AddRange(milestones.Select(p=>p.Key+" seconds: "+p.Value.ToString("F3")));
        File.WriteAllLines(Path.Combine(output,"chunk-loading.txt"),report);
        if(!milestones.ContainsKey("radius600"))throw new InvalidOperationException("600m all-direction terrain/forest footprint not loaded in 180 seconds");
        Capture(Path.Combine(output,"chunk-loaded.png"));
        if(Environment.GetCommandLineArgs().Contains("--worldgen-chunk-travel"))
        {
            var travel=new List<string>{"seconds,east,nearReady,terrainM,forestM,tiles,activeWorkers"};
            start=Time.realtimeSinceStartupAsDouble;next=start;double lastEast=0;BeginLoadingProfile();
            while(Time.realtimeSinceStartupAsDouble-start<40)
            {
                double elapsed=Time.realtimeSinceStartupAsDouble-start,east=Math.Min(400,elapsed*20);
                TrackStreamingMotion(east-lastEast,0,Time.unscaledDeltaTime);lastEast=east;
                PerformanceTravel(east);
                if(Time.realtimeSinceStartupAsDouble>=next)
                {next=Time.realtimeSinceStartupAsDouble+.25;travel.Add(string.Join(",",new[]{elapsed,east,NearReady?1d:0,terrainRevealRadius,forestRevealRadius,tiles.Count,ActiveWorkerCount}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));}
                if(Error!="")throw new InvalidOperationException(Error);
                yield return null;
            }
            File.WriteAllLines(Path.Combine(output,"chunk-travel.csv"),travel);
            File.WriteAllText(Path.Combine(output,"chunk-travel.txt"),EndLoadingProfile()+"\n"+Diagnostics+"\n"+PerformanceGroundPublication);
            Capture(Path.Combine(output,"chunk-travel.png"));
        }
        File.WriteAllText(Path.Combine(output,"complete.txt"),"PASS empty spectator loading check\n");
        Close();Application.Quit(0);
    }
}
