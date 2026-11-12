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
        double start=Time.realtimeSinceStartupAsDouble,last=start,next=start,deadline=start+180,nextState=start+5;
        double[] radii={50,100,300,600};bool viewed=false;
        BeginLoadingProfile();
        while(Time.realtimeSinceStartupAsDouble<deadline)
        {
            double now=Time.realtimeSinceStartupAsDouble,elapsed=now-start;
            frames.Add((now-last)*1000);last=now;
            if(Error!="")throw new InvalidOperationException(Error);
            if(Ready&&walkingPatch!=null&&!viewed)
            {TestView(3,20,5);TestWeatherOverride=new WeatherFrame(22,.6,0,0,.15,.05,1800,500,Math.Max(7000,viewDistance*1.5),2,1,3,0);PlayerRules.SetLocalSolarHour(12,Math.Atan2(Position.Y,Position.X)*180/Math.PI);nextDaylightAt=0;SetDaylight(12,190000);viewed=true;}
            if(CenterReady&&!milestones.ContainsKey("firstGround"))
            {
                milestones["firstGround"]=elapsed;
                if(Environment.GetCommandLineArgs().Contains("--worldgen-chunk-forest-entry"))
                {
                    yield return new WaitForEndOfFrame();
                    Capture(Path.Combine(output,"prepared-first-chunk.png"));
                    int grounded=0,localFallback=0;uint submitted=0;
                    foreach(var field in forestFields.Values)foreach(var pair in field.Roots)foreach(int slot in pair.Value)
                    {
                        var root=pair.Key.Data[slot];
                        if(root.Padding<0||Vector3.Distance(root.Position,new Vector3((float)motion.X,(float)motion.Y,(float)motion.Z))>300)continue;
                        grounded++;if((forestFlags[field.Id]&2)!=0)localFallback++;
                    }
                    foreach(var batch in forestBatches.Values)if(batch.Args!=null&&batch.SubmittedFrame==Time.frameCount)
                    {var args=new uint[4];batch.Args.GetData(args);submitted+=args[1];}
                    File.WriteAllText(Path.Combine(output,"first-forest.txt"),"seconds="+elapsed+" hiddenDuringEntry="+!InitialViewReady+" groundedRootsWithin300m="+grounded+" localFallbackRoots="+localFallback+" submittedForestInstances="+submitted+" pendingModelBakes="+PendingTreeModels+"\n"+PerformanceForestState);
                    if(walkingPatch.ForestPotential(Position)>.2&&(grounded==0||submitted==0))throw new InvalidOperationException("First prepared forested chunk has no grounded, submitted tree records");
                }
            }
            if(NearReady&&!milestones.ContainsKey("near"))milestones["near"]=elapsed;
            foreach(double radius in radii)
            {
                string name="radius"+radius;
                if(Math.Min(terrainRevealRadius,forestRevealRadius)>=radius&&!milestones.ContainsKey(name))milestones[name]=elapsed;
            }
            if(Environment.GetCommandLineArgs().Contains("--worldgen-chunk-forest-entry")&&InitialViewReady&&HasInitialViewCoverage())
            {
                // Use the actual production entry gate: complete coarse view
                // and an exact local forest. Detail still streams during flight.
                Capture(Path.Combine(output,"playable-ground-and-forest.png"));
                File.WriteAllText(Path.Combine(output,"playable-start.txt"),"seconds="+elapsed+"\n"+PerformanceForestState);
                yield return CheckChunkFastFlight(output);
                File.WriteAllText(Path.Combine(output,"complete.txt"),"PASS first-ground forest submission and cold fast flight\n");
                Close();Application.Quit(0);yield break;
            }
            if(now>=next)
            {
                next=now+.25;
                rows.Add(string.Join(",",new[]{elapsed,terrainRevealRadius,forestRevealRadius,fogSafeRadius,renderedStreamingFog,tiles.Count,jobs.Count,jobs.Count(j=>j.Task.IsCompleted),scatterJobs.Count,forestJobs.Count,ActiveWorkerCount}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
            }
            if(now>=nextState)
            {nextState=now+5;File.AppendAllText(Path.Combine(output,"loading-work.txt"),elapsed.ToString("F2",CultureInfo.InvariantCulture)+"s: "+CheckStreamingState+"; "+PerformanceGroundPublication+"; install="+CanInstall+" scatter reserve="+reserveScatterFrame+" uploadMs="+frameUploadMs+"\n");}
            if(AllReady&&!DetailPlanning&&jobs.Count==0&&!ForestPending&&ScatterReady&&milestones.ContainsKey("radius600"))
            {milestones["complete"]=elapsed;break;}
            yield return null;
        }
        if(humanPlayer!=null||livingPlanner!=null||humanNpcs.Count!=0||livingPeople.Count!=0||livingGroups.Count!=0)
            throw new InvalidOperationException("Chunk check contains a human or camp workload");
        File.WriteAllLines(Path.Combine(output,"chunk-loading.csv"),rows);
        frames.Sort();double Percentile(double p)=>frames.Count==0?0:frames[Math.Min(frames.Count-1,(int)(frames.Count*p))];
        var report=new List<string>{"Empty spectator; no humans, communities, camps, or fauna; no saved preferences changed.",
            "Shared PC; concurrent owner workload recorded separately. Frame figures include 60 FPS pacing, not an FPS benchmark.",
            "Preparation ms: "+preparationMs.ToString("F3"),"Detail: "+detailOptions.Quality+"; effort: "+detailOptions.Effort+"; view: "+viewDistance,
            "Legacy scheduler: "+Environment.GetCommandLineArgs().Contains("--worldgen-loader-legacy"),
            "Hard chunk updates: "+TerrainVideoOptions.HardChunkUpdates,
            "Frame p99/max ms: "+Percentile(.99).ToString("F3")+" / "+Percentile(1).ToString("F3"),
            "Frames >50ms / >100ms: "+frames.Count(f=>f>50)+" / "+frames.Count(f=>f>100),EndLoadingProfile(),Diagnostics,PerformanceForestState};
        report.AddRange(milestones.Select(p=>p.Key+" seconds: "+p.Value.ToString("F3")));
        File.WriteAllLines(Path.Combine(output,"chunk-loading.txt"),report);
        if(Environment.GetCommandLineArgs().Contains("--worldgen-chunk-forest-entry"))
            throw new InvalidOperationException("Production terrain/forest entry did not become playable in 180 seconds");
        if(!milestones.ContainsKey("radius600"))throw new InvalidOperationException("600m all-direction terrain/forest footprint not loaded in 180 seconds");
        Capture(Path.Combine(output,"chunk-loaded.png"));
        if(Environment.GetCommandLineArgs().Contains("--worldgen-chunk-motion"))yield return CheckChunkPlanMotion(output);
        if(Environment.GetCommandLineArgs().Contains("--worldgen-chunk-fast-flight"))yield return CheckChunkFastFlight(output);
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
    IEnumerator CheckChunkPlanMotion(string output)
    {
        var before=new Dictionary<TerrainTileKey,int>(wanted);
        var probes=before.Select(p=>(X:(p.Key.X+.5)*p.Key.SizeM,Z:(p.Key.Z+.5)*p.Key.SizeM,Spacing:p.Key.SizeM/p.Value))
            .Where(p=>Math.Abs(p.X)<viewDistance*.7&&Math.Abs(p.Z)<viewDistance*.7).ToArray();
        double Spacing(IReadOnlyDictionary<TerrainTileKey,int> plan,double x,double z)
        {
            for(int level=0;level<24;level++)
            {
                double size=TerrainLod.BaseSizeM*HorizontalScale*(1<<level);
                var key=new TerrainTileKey((int)Math.Floor(x/size),(int)Math.Floor(z/size),level,HorizontalScale);
                if(plan.TryGetValue(key,out int segments))return key.SizeM/segments;
            }
            return double.PositiveInfinity;
        }
        var rows=new List<string>{"step,east,downgradedProbes,requestedTiles,residentTiles,buildRequests,cacheHits,previousSurfaces"};
        int worstDowngrades=0;long builds=TerrainBuildRequests,hits=TerrainCacheHits;BeginLoadingProfile();
        for(int step=1;step<=6;step++)
        {
            // Keep the full estimator pending for one second. This explicitly
            // exercises the in-flight interval, rather than hoping a fast CPU
            // catches the temporary zero-relief plan in a screenshot.
            double east=step*TerrainLod.ReplanDistance(HorizontalScale)*1.05;
            PerformanceTravel(east);RefreshWanted();detailDirty=false;
            int downgraded=probes.Count(p=>Spacing(wanted,p.X,p.Z)>p.Spacing*(1+1e-9));
            worstDowngrades=Math.Max(worstDowngrades,downgraded);
            rows.Add(string.Join(",",step,east.ToString("R",CultureInfo.InvariantCulture),downgraded,wanted.Count,tiles.Count,TerrainBuildRequests-builds,TerrainCacheHits-hits,previousTerrain.Count));
            if(!legacyPlanRefresh&&downgraded!=0)throw new InvalidOperationException("Movement replaced known relief with coarser temporary requests");
            double until=Time.realtimeSinceStartupAsDouble+1;
            while(Time.realtimeSinceStartupAsDouble<until){if(Error!="")throw new InvalidOperationException(Error);yield return null;}
            if(step==1||step==6)Capture(Path.Combine(output,"motion-step-"+step+".png"));
        }
        File.WriteAllLines(Path.Combine(output,"plan-motion.csv"),rows);
        File.WriteAllText(Path.Combine(output,"plan-motion.txt"),"Legacy bootstrap="+legacyPlanRefresh+"; worst downgraded probes="+worstDowngrades+"; builds="+(TerrainBuildRequests-builds)+"; cache hits="+(TerrainCacheHits-hits)+"\n"+EndLoadingProfile());
        detailDirty=true;
    }
    IEnumerator CheckChunkFastFlight(string output)
    {
        var origin=frame;var rows=new List<string>{"seconds,speed,east,altitude,viewM,terrainM,forestM,tiles,jobs,nearReady,groundAtPlayer,missingNearProbes,handoffs,buildRequests,meshHeightError,preparedTiles,groundPixelsMissing,missingForwardProbes"};
        var workRows=new List<string>();
        TestView(500,90,12);double start=Time.realtimeSinceStartupAsDouble,last=start,east=0,next=start;
        long initialBuilds=TerrainBuildRequests;int missingFrames=0,missingForwardFrames=0,maxMissingForward=0,lastPixelGaps=0,audits=0;double nextAudit=start+1;BeginLoadingProfile();
        var flightArguments=Environment.GetCommandLineArgs();bool longFlight=flightArguments.Contains("--worldgen-chunk-long-flight"),endurance=flightArguments.Contains("--worldgen-chunk-endurance-flight");double duration=endurance?260:longFlight?80:40,turnAt=endurance?190:longFlight?56:24;
        int speedIndex=Array.IndexOf(flightArguments,"--worldgen-chunk-flight-speed");double fixedSpeed=speedIndex>=0?double.Parse(flightArguments[speedIndex+1],CultureInfo.InvariantCulture):0;
        while(Time.realtimeSinceStartupAsDouble-start<duration)
        {
            double now=Time.realtimeSinceStartupAsDouble,t=now-start,dt=Math.Min(.25,now-last);last=now;
            double speed=fixedSpeed>0?fixedSpeed:t<8?200:600;if(t>=turnAt)speed=-Math.Abs(speed);
            east+=speed*dt;var at=origin.Position(east,0);TerrainHandoff.Coordinates(frame,at,out double x,out double z);
            TrackStreamingMotion(x-motion.X,z-motion.Z,Math.Max(.001,dt));motion.X=x;motion.Z=z;double floor=Ground(x,z);var water=SampleMotionWater(x,z);if(double.IsFinite(water.Surface))floor=Math.Max(floor,water.Surface);motion.Y=floor+500;motion.Flying=true;
            heading=(float)(Math.Atan2(V.Dot(origin.East,frame.East),V.Dot(origin.East,frame.North))*180/Math.PI)+(speed<0?180:0);UpdateCamera();
            int missing=0;foreach(double distance in new[]{0d,200,600})foreach(double angle in new[]{0d,Math.PI*.5,Math.PI,Math.PI*1.5})
                if(double.IsNaN(VisibleGround(x+Math.Cos(angle)*distance,z+Math.Sin(angle)*distance,double.NaN)))missing++;
            bool playerGround=!double.IsNaN(VisibleGround(x,z,double.NaN));if(!playerGround)missingFrames++;
            // Resident coverage ahead is independent of whether the camera
            // sees ocean or the land-only pixel audit can see its seabed.
            double forward=heading*Math.PI/180,fx=Math.Sin(forward),fz=Math.Cos(forward),limit=Math.Max(0,viewDistance*.9);
            double first=Math.Min(1000,limit),second=Math.Min(3000,limit);int missingForward=0;
            bool ForwardMissing(double distance)=>double.IsNaN(VisibleGround(x+fx*distance,z+fz*distance,double.NaN));
            if(first>0&&ForwardMissing(first))missingForward++;
            if(second>0&&second!=first&&ForwardMissing(second))missingForward++;
            if(limit>0&&limit!=first&&limit!=second&&ForwardMissing(limit))missingForward++;
            if(missingForward>0)missingForwardFrames++;maxMissingForward=Math.Max(maxMissingForward,missingForward);
            if(now>=nextAudit)
            {
                nextAudit=now+5;lastPixelGaps=CheckFlightGroundPixels(Path.Combine(output,"flight-ground-"+audits+".png"));
                Capture(Path.Combine(output,"flight-view-"+audits+".png"));audits++;
                int perspectiveGaps=CheckFlightGroundPixels(Path.Combine(output,"flight-perspective-ground-"+(audits-1)+".png"),overhead:false);
                workRows.Add(t.ToString("F2",CultureInfo.InvariantCulture)+"s: "+StreamingWorkReceipt);
                workRows.Add("perspective bottom-third ground gaps="+perspectiveGaps+"/3072");
                foreach(var level in forestFields.Values.GroupBy(f=>f.Source.Key.Level).OrderBy(g=>g.Key))
                {
                    int individual=0,stands=0,rejectedStands=0;double width=0;int anchors=0;
                    foreach(var field in level)foreach(var pair in field.Roots)foreach(int slot in pair.Value)
                    {
                        var root=pair.Key.Data[slot];if(pair.Key.Model.TreeCluster){if(root.Padding>0)stands++;else rejectedStands++;}
                        else{if(root.Padding>=0)individual++;width+=root.Width;anchors++;}
                    }
                    workRows.Add($"forest L{level.Key}: single={individual}, stand={stands}, rejectedStand={rejectedStands}, meanSingleWidth={width/Math.Max(1,anchors):F2}m, sourceTrees={level.Sum(f=>f.Source.Roots.Sum(r=>r.Population>0?1:r.CanopyTreeCount)):F0}");
                }
                File.WriteAllLines(Path.Combine(output,"flight-work.txt"),workRows);
                var snapshot=new List<string>{"scope,x,z,level,scale,segments,coverageOnly,hasWater,contact,waterBoundsKnown,waterMinWorldX,waterMinWorldZ,waterMaxWorldX,waterMaxWorldZ",$"observer,{motion.X:R},{motion.Z:R},0,{HorizontalScale:R},{viewDistance:R},,,,,,,,"};
                void Save(string scope,IEnumerable<KeyValuePair<TerrainTileKey,int>> entries,Dictionary<TerrainTileKey,Tile> residents=null)
                {
                    foreach(var p in entries)
                    {
                        var k=p.Key;string extra=",,,,,,,,";
                        if(residents!=null&&residents.TryGetValue(k,out var tile))
                        {
                            extra=","+string.Join(",",tile.CoverageOnly,tile.HasWater,tile.Contact!=null,tile.WaterBoundsKnown);
                            if(tile.HasWater&&tile.WaterBoundsKnown)
                            {var b=tile.WaterBounds;double x=k.X*k.SizeM,z=k.Z*k.SizeM;extra+=","+string.Join(",",new[]{x+b.min.x,z+b.min.z,x+b.max.x,z+b.max.z}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)));}
                            else extra+=",,,,";
                        }
                        snapshot.Add(string.Join(",",scope,k.X,k.Z,k.Level,k.Scale.ToString("R",CultureInfo.InvariantCulture),p.Value)+extra);
                    }
                }
                Save("wanted",wanted);Save("resident",tiles.Select(p=>new KeyValuePair<TerrainTileKey,int>(p.Key,p.Value.Segments)),tiles);
                foreach(var p in tiles){var k=p.Key;snapshot.Add(string.Join(",","fade",k.X,k.Z,k.Level,k.Scale.ToString("R",CultureInfo.InvariantCulture),p.Value.FadeIn.ToString("R",CultureInfo.InvariantCulture),p.Value.FadeOut.ToString("R",CultureInfo.InvariantCulture))+",,,,,,,");}
                if(preparedFrame!=null)
                {var p=preparedFrame;snapshot.Add($"prepared-observer,{p.X:R},{p.Z:R},0,{HorizontalScale:R},{viewDistance:R},,,,,,,,");Save("prepared-wanted",p.Wanted);Save("prepared-resident",p.Tiles.Select(t=>new KeyValuePair<TerrainTileKey,int>(t.Key,t.Value.Segments)),p.Tiles);}
                File.WriteAllLines(Path.Combine(output,"flight-snapshot-"+(audits-1)+".csv"),snapshot);
                if(audits%4==0)
                {
                    foreach(var tile in tiles.Values.Concat(previousTerrain.Values))foreach(var material in tile.Materials)material.SetFloat("_TransitionEnabled",0);
                    Capture(Path.Combine(output,"flight-no-transitions-"+(audits-1)+".png"));
                    foreach(var tile in tiles.Values.Concat(previousTerrain.Values))foreach(var material in tile.Materials)material.SetFloat("_TransitionEnabled",TerrainVideoOptions.HardChunkUpdates?0:1);
                    var flags=TerrainDiagnostics.Flags;TerrainDiagnostics.Flags|=TerrainDiagnosticFlags.NoCoverage;TerrainDiagnostics.Changed();ApplyTerrainDiagnostics();
                    Capture(Path.Combine(output,"flight-no-coverage-"+(audits-1)+".png"));
                    TerrainDiagnostics.Flags=flags;TerrainDiagnostics.Changed();ApplyTerrainDiagnostics();
                }
            }
            if(now>=next)
            {next=now+.2;rows.Add(string.Join(",",new[]{t,speed,east,HeightAboveGround,viewDistance,terrainRevealRadius,forestRevealRadius,tiles.Count,jobs.Count,NearReady?1d:0,playerGround?1d:0,missing,ReanchorCount,TerrainBuildRequests-initialBuilds,VisibleGround(x,z,double.NaN)-Ground(x,z),PreparedTileCount,lastPixelGaps,missingForward}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));}
            if(Error!="")throw new InvalidOperationException(Error);
            yield return null;
        }
        File.WriteAllLines(Path.Combine(output,"fast-flight.csv"),rows);
        File.WriteAllLines(Path.Combine(output,"flight-work.txt"),workRows);
        File.WriteAllText(Path.Combine(output,"fast-flight.txt"),(fixedSpeed>0?fixedSpeed+" m/s":"200 then 600 m/s")+" forward and reverse; "+duration+" seconds, 500m above land/water; frames with no resident ground at player="+missingFrames+"; frames missing forward resident ground="+missingForwardFrames+"; max missing forward probes="+maxMissingForward+"; builds="+(TerrainBuildRequests-initialBuilds)+"; handoffs="+ReanchorCount+"; max handoff ms="+LargestHandoffMs+" "+LargestHandoffProfile+"; pixel audits="+audits+" (extra GPU renders/readbacks included; not an FPS benchmark)\n"+EndLoadingProfile()+"\n"+Diagnostics+"\n"+PerformanceForestState);
        Capture(Path.Combine(output,"fast-flight.png"));
        if(flightArguments.Contains("--worldgen-chunk-forest-entry")&&missingFrames>0)
            throw new InvalidOperationException("Cold forest flight lost resident ground beneath the player in "+missingFrames+" frames");
    }
    int CheckFlightGroundPixels(string path,bool overhead=true)
    {
        // Positive-controlled actual shader audit. Only the production ground
        // renderers enter this camera; the original coverage/fade materials
        // remain bound, so a resident-but-clipped surface cannot pass.
        var moved=new List<(GameObject Object,int Layer)>();
        foreach(var tile in tiles.Values.Concat(previousTerrain.Values))
            foreach(var renderer in tile.Root.GetComponentsInChildren<MeshRenderer>())
                if(renderer.gameObject.name=="Land and seabed")
                {moved.Add((renderer.gameObject,renderer.gameObject.layer));renderer.gameObject.layer=29;}
        var oldPosition=view.transform.position;var oldRotation=view.transform.rotation;var oldTarget=view.targetTexture;
        var oldMask=view.cullingMask;var oldClear=view.clearFlags;var oldColour=view.backgroundColor;bool oldOrtho=view.orthographic;
        float oldSize=view.orthographicSize,oldFar=view.farClipPlane;var prior=RenderTexture.active;
        var target=new RenderTexture(96,96,24);var pixels=new Texture2D(96,96,TextureFormat.RGB24,false);
        try
        {
            if(overhead){view.transform.position=new Vector3((float)(motion.X-originX),(float)(Ground(motion.X,motion.Z)+10000),(float)(motion.Z-originZ));view.transform.rotation=Quaternion.Euler(90,0,0);view.orthographic=true;view.orthographicSize=40;view.farClipPlane=20000;}
            view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=Color.magenta;view.targetTexture=target;
            Color32[] Read(){view.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,96,96),0,0);pixels.Apply();return pixels.GetPixels32();}
            view.cullingMask=0;var empty=Read();var repeated=Read();
            bool Equal(Color32 a,Color32 b)=>Math.Abs(a.r-b.r)<4&&Math.Abs(a.g-b.g)<4&&Math.Abs(a.b-b.b)<4;
            int stable=0;for(int i=0;i<empty.Length;i++)if(Equal(empty[i],repeated[i]))stable++;
            // Atmospheric rendering may create a gradient over empty sky. A
            // repeated per-pixel control verifies it is stable without assuming
            // that every empty pixel has one uniform postprocessed colour.
            if(stable<empty.Length*.99){File.WriteAllBytes(path+"-unstable-control.png",pixels.EncodeToPNG());throw new InvalidOperationException("Flight ground audit has an unstable empty positive control");}
            view.cullingMask=1<<29;var actual=Read();int gaps=0;
            for(int i=0;i<(overhead?actual.Length:96*32);i++)if(Equal(actual[i],empty[i]))gaps++;
            File.WriteAllBytes(path,pixels.EncodeToPNG());return gaps;
        }
        finally
        {
            foreach(var p in moved)p.Object.layer=p.Layer;
            view.transform.SetPositionAndRotation(oldPosition,oldRotation);view.targetTexture=oldTarget;view.cullingMask=oldMask;view.clearFlags=oldClear;view.backgroundColor=oldColour;
            view.orthographic=oldOrtho;view.orthographicSize=oldSize;view.farClipPlane=oldFar;RenderTexture.active=prior;
            target.Release();Destroy(target);Destroy(pixels);
        }
    }
}
