using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

// Opt-in, release-player benchmark. No PlayerPrefs writes, saves, UI automation,
// screenshots or per-frame file IO in measured windows. One explicit URP render
// per frame replaces automatic rendering; GPU drains bracket each timing window.
public static class WorldGenPerformance
{
    public static readonly bool Enabled=Environment.GetCommandLineArgs().Contains("--worldgen-performance");
    static readonly bool original=Enabled&&Environment.GetCommandLineArgs().Contains("--worldgen-perf-original");
    public static bool Original=>Enabled&&(original||OriginalCase);
    public static bool OriginalCase;
    public static bool Measuring;
    public static readonly double[] Milliseconds=new double[4];
    public static long Begin()=>Enabled?System.Diagnostics.Stopwatch.GetTimestamp():0;
    public static void End(int slot,long start){if(start!=0)Milliseconds[slot]=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;}
}

public sealed partial class LocalTerrainExplorer
{
    public bool PerformanceCoverPending=>coverRequests.Count>0;
    public int PerformanceVerifyEmptyCover()
    {
        int pages=0;var count=new uint[1];
        foreach(var tile in tiles.Values)
        {
            if(tile.Cover==null||tile.Cover.Length==0||HasMovingCover(tile))continue;
            foreach(var page in tile.CoverPages.Values)
            {
                page.Count.GetData(count);
                if(count[0]!=0)throw new InvalidOperationException("Empty-cover optimisation would remove admitted GPU roots");
                pages++;
            }
        }
        return pages;
    }
    public int PerformancePendingComparisons {get;private set;}
    void PerformanceVerifyPending()
    {
        if(!Ready||Error!="")return;
        if(NearTerrainPending()!=NearTerrainPendingFresh())throw new InvalidOperationException("Near-terrain readiness memo changed scheduling state");
        PerformancePendingComparisons++;
    }
    string[] PerformanceInstances()
    {
        var values=new List<string>();
        foreach(var b in drawBatches)for(int i=0;i<b.Count;i++)
        {
            var s=new System.Text.StringBuilder();s.Append(b.Key).Append(':').Append((int)b.Shadows);
            for(int n=0;n<16;n++)s.Append(':').Append(BitConverter.SingleToInt32Bits(b.Matrices[i][n]));
            for(int n=0;n<4;n++)s.Append(':').Append(BitConverter.SingleToInt32Bits(b.Colors[i][n])).Append(':').Append(BitConverter.SingleToInt32Bits(b.Fades[i][n]));
            values.Add(s.ToString());
        }
        values.Sort(StringComparer.Ordinal);return values.ToArray();
    }
    public string PerformanceVerifyVisibility()
    {
        if(WorldGenPerformance.Original)return "Original-only run: equivalence check skipped.";
        for(int yaw=0;yaw<360;yaw+=45)
        {
            PerformanceTurn(yaw);WorldGenPerformance.OriginalCase=true;BuildScatterBatches();var before=PerformanceInstances();
            WorldGenPerformance.OriginalCase=false;BuildScatterBatches();var after=PerformanceInstances();
            if(!before.SequenceEqual(after))throw new InvalidOperationException("Visibility optimisation changed submitted instances at yaw "+yaw+" ("+before.Length+" / "+after.Length+")");
            foreach(var b in drawBatches)
            {
                if(!b.Properties.GetVectorArray("_Tint").SequenceEqual(b.Colors.Take(b.Count))||!b.Properties.GetVectorArray("_LodTransition").SequenceEqual(b.Fades.Take(b.Count)))throw new InvalidOperationException("Instanced property upload truncated or changed live entries");
            }
        }
        PerformanceTurn(20);WorldGenPerformance.OriginalCase=false;BuildScatterBatches();
        foreach(var tile in tiles.Values)if(tile.OcclusionMin!=null&&tile.OcclusionMin.Max()!=tile.OcclusionCeiling)throw new InvalidOperationException("Occlusion ceiling cache mismatch");
        foreach(double x in new[]{0d,1d,23d,78d})
        {
            WorldGenPerformance.OriginalCase=true;double exact=Ground(x,5);
            WorldGenPerformance.OriginalCase=false;if(BitConverter.DoubleToInt64Bits(exact)!=BitConverter.DoubleToInt64Bits(Ground(x,5)))throw new InvalidOperationException("Ground memo mismatch");
        }
        return "PASS identical model/transform/tint/LOD/shadow instance multisets at eight headings; exact ground memo and occlusion ceilings.";
    }
    public void PerformancePose(double height=1.65)
    {
        bool changed=motion.X!=0||motion.Z!=0||Math.Abs(HeightAboveGround-height)>.01;
        motion.X=motion.Z=0;motion.Y=Ground(0,0)+height;motion.Flying=true;heading=20;pitch=height>10?12:3;
        if(changed)RefreshWanted();UpdateCamera();
    }
    public void PerformanceTurn(float yaw){heading=yaw;UpdateCamera();}
    public void PerformanceTravel(double east){motion.X=east;motion.Y=Ground(motion.X,motion.Z)+1.65;UpdateCamera();}
    public void PerformanceRender(int width,int height)
    {
        // Exactly one full URP camera render per LateUpdate, even on a noninteractive
        // desktop. Disabled automatic camera prevents double rendering if shown.
        weatherCheckTarget=new RenderTexture(width,height,24);weatherCheckTarget.Create();
        view.targetTexture=weatherCheckTarget;view.aspect=width/(float)height;view.enabled=false;
    }
    public void PerformanceCapture(string path)
    {
        var prior=RenderTexture.active;var pixels=new Texture2D(weatherCheckTarget.width,weatherCheckTarget.height,TextureFormat.RGB24,false);
        try
        {
            RenderTexture.active=weatherCheckTarget;pixels.ReadPixels(new Rect(0,0,pixels.width,pixels.height),0,0);pixels.Apply();
            var colors=pixels.GetPixels32();var first=colors[0];int changed=0;foreach(var c in colors)if(c.r!=first.r||c.g!=first.g||c.b!=first.b)changed++;
            if(changed<1000)throw new InvalidOperationException("Benchmark render is blank; reject measurements.");
            File.WriteAllBytes(path,pixels.EncodeToPNG());
        }
        finally{RenderTexture.active=prior;UnityEngine.Object.Destroy(pixels);}
    }
    public bool PerformanceSettled=>Ready&&NearReady&&AllReady&&ScatterReady&&!DetailPlanning&&jobs.Count==0&&scatterJobs.Count==0&&coverRequests.Count==0&&previousTerrain.Count==0&&retiringFrames.Count==0&&tiles.Values.All(t=>FadeComplete(t));
    public void PerformanceInvalidate(){scatterDirty=true;}
    public void PerformanceShadows(bool enabled){if(localSun!=null)localSun.shadows=enabled?LightShadows.Soft:LightShadows.None;}
    public string PerformanceCounts=>string.Join(",",TileCount,WantedCount,ScatterObjects,SpecialistObjects,MidPlantObjects,GeneratedTreeObjects,ScatterDrawCalls,GroundCoverDraws,GroundCoverCandidates,GroundCoverBytes,ScatterFrustumRejected,ScatterTerrainRejected);
    public void PerformanceDrySite()
    {
        // Pick the closest dry point, not the flattest: retain the nominated relief.
        var patch=terrain.Patch(Position,30000);
        for(int ring=0;ring<12;ring++)for(int z=-ring;z<=ring;z++)for(int x=-ring;x<=ring;x++)
        {
            if(Math.Max(Math.Abs(x),Math.Abs(z))!=ring)continue;
            double dx=x*80/HorizontalScale,dz=z*80/HorizontalScale;var sample=patch.Sample(dx,dz);
            if(!sample.Underwater&&sample.ElevationM>5){if(ring>0)Teleport(patch.Position(dx,dz));return;}
        }
        throw new InvalidOperationException("No dry benchmark site near selected cell.");
    }
}

public sealed partial class WorldGenRuntime
{
    sealed class PerfCase
    {
        public string Name;public Action Apply;public bool Terrain;
        public PerfCase(string name,Action apply,bool terrain=false){Name=name;Apply=apply;Terrain=terrain;}
    }
    static string Perf(double value)=>value.ToString("F5",CultureInfo.InvariantCulture);
    IEnumerator CheckPerformance()
    {
        Application.logMessageReceived+=(message,trace,kind)=>{if(kind==LogType.Exception||kind==LogType.Error)Application.Quit(1);};
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);
        double deadline=Time.realtimeSinceStartupAsDouble+300;
        while((!CanExploreTerrain||humanStep!=null||humanPaletteJob!=null||humanPaletteReady!=null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(!CanExploreTerrain){Debug.LogError("PERF world unavailable "+error);Application.Quit(1);yield break;}
        humanPlaying=false;play.HasPlayer=false;play.Settings.HorizontalScale=.1;
        int width=int.Parse(Argument("--worldgen-perf-width","1920")),height=int.Parse(Argument("--worldgen-perf-height","1080"));
        var cells=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>world.ElevationM[i]>30&&world.ElevationM[i]<3200).ToArray();
        int Pick(Func<HabitatCell,bool> where,bool steep=false)=>cells.Where(i=>where(world.Ecology.Habitat[i])).OrderBy(i=>steep?-world.Ecology.Habitat[i].ReliefM:world.Ecology.Habitat[i].ReliefM).DefaultIfEmpty(-1).First();
        var sites=new[]{("plains",Pick(h=>h.Biome==Biome.Grassland||h.Biome==Biome.Savanna)),("forest-flat",Pick(h=>h.Biome==Biome.TemperateForest||h.Biome==Biome.TropicalRainforest)),("forest-hills",Pick(h=>h.Biome==Biome.TemperateForest||h.Biome==Biome.TropicalRainforest,true)),("mountains",Pick(h=>h.Biome==Biome.Alpine||h.Biome==Biome.BorealForest,true)),("desert",Pick(h=>h.Biome==Biome.Desert)),("tundra",Pick(h=>h.Biome==Biome.Tundra))};
        string filter=Argument("--worldgen-perf-sites","");
        string caseFilter=Argument("--worldgen-perf-cases","");
        double seconds=double.Parse(Argument("--worldgen-perf-seconds","6"),CultureInfo.InvariantCulture);
        var cases=new List<PerfCase>{
            new PerfCase("full",()=>{}),
            new PerfCase("original-cpu",()=>WorldGenPerformance.OriginalCase=true),
            new PerfCase("no-grass",()=>TerrainVideoOptions.GroundCover=false),
            new PerfCase("no-specialists",()=>TerrainVideoOptions.SpecialistPlants=false),
            new PerfCase("no-mid",()=>TerrainVideoOptions.MidPlants=false),
            new PerfCase("no-trees",()=>TerrainVideoOptions.GeneratedTrees=false),
            new PerfCase("terrain-only",()=>{TerrainVideoOptions.GroundCover=false;TerrainVideoOptions.SpecialistPlants=TerrainVideoOptions.MidPlants=TerrainVideoOptions.GeneratedTrees=TerrainVideoOptions.FarForest=false;terrainExplorer.ScatterEnabled=false;}),
            new PerfCase("grass-only",()=>TerrainVideoOptions.SpecialistPlants=TerrainVideoOptions.MidPlants=TerrainVideoOptions.GeneratedTrees=false),
            new PerfCase("no-vegetation-shadows",()=>TerrainVideoOptions.VegetationShadows=false),
            new PerfCase("no-shadows",()=>terrainExplorer.PerformanceShadows(false)),
            new PerfCase("trees-low",()=>TerrainVideoOptions.TreeDetail=0),
            new PerfCase("trees-700m",()=>TerrainVideoOptions.TreeIdentityRange=700),
            new PerfCase("tuned-lods",()=>{TerrainVideoOptions.TreeNearLod=80;TerrainVideoOptions.TreeFarLod=300;TerrainVideoOptions.MidNearLod=35;TerrainVideoOptions.MidFarLod=140;TerrainVideoOptions.GrassBladeDistance=15;TerrainVideoOptions.GrassDistance=200;}),
            new PerfCase("tuned-lods-no-veg-shadows",()=>{TerrainVideoOptions.TreeNearLod=80;TerrainVideoOptions.TreeFarLod=300;TerrainVideoOptions.MidNearLod=35;TerrainVideoOptions.MidFarLod=140;TerrainVideoOptions.GrassBladeDistance=15;TerrainVideoOptions.GrassDistance=200;TerrainVideoOptions.VegetationShadows=false;}),
            new PerfCase("mid-350m",()=>TerrainVideoOptions.MidMaxDistance=350),
            new PerfCase("specialists-60m",()=>{TerrainVideoOptions.SpecialistDistance=60;TerrainVideoOptions.FungusDistance=40;}),
            new PerfCase("grass-15-150m",()=>{TerrainVideoOptions.GrassBladeDistance=15;TerrainVideoOptions.GrassDistance=150;}),
            new PerfCase("occlusion-off",()=>TerrainVideoOptions.VegetationOcclusion=false),
            new PerfCase("frustum-off",()=>TerrainVideoOptions.ScatterFrustum=false),
            new PerfCase("terrain-low",()=>TerrainVideoOptions.Current.Quality=0,true),
            new PerfCase("terrain-ultra",()=>TerrainVideoOptions.Current.Quality=4,true),
            new PerfCase("view-10km",()=>TerrainVideoOptions.Current.ViewDistanceM=10000,true),
            new PerfCase("view-200km",()=>TerrainVideoOptions.Current.ViewDistanceM=200000,true),
            new PerfCase("fine-307m",()=>TerrainVideoOptions.Current.FullDetailTiles=3,true),
            new PerfCase("full-repeat",()=>{},true),
            new PerfCase("flying-60m",()=>terrainExplorer.PerformancePose(60),true),
            new PerfCase("flying-no-grass",()=>{terrainExplorer.PerformancePose(60);TerrainVideoOptions.GroundCover=false;}),
            new PerfCase("flying-no-trees",()=>{terrainExplorer.PerformancePose(60);TerrainVideoOptions.GeneratedTrees=false;}),
            new PerfCase("flying-no-mid",()=>{terrainExplorer.PerformancePose(60);TerrainVideoOptions.MidPlants=false;}),
            new PerfCase("flying-terrain-only",()=>{terrainExplorer.PerformancePose(60);TerrainVideoOptions.GroundCover=false;terrainExplorer.ScatterEnabled=false;TerrainVideoOptions.FarForest=false;}),
            new PerfCase("turning-original",()=>WorldGenPerformance.OriginalCase=true),
            new PerfCase("turning",()=>{}),
            new PerfCase("travel",()=>{}),
            new PerfCase("weather-off",()=>{var w=WeatherVideoOptions.Current;w.Clouds=0;w.SunGlare=w.SunRays=w.Adaptation=w.Visibility=0;})
        };
        File.WriteAllText(Path.Combine(output,"machine.txt"),$"{SystemInfo.processorType}\n{SystemInfo.graphicsDeviceName}\n{SystemInfo.graphicsDeviceVersion}\nRAM {SystemInfo.systemMemorySize} MB VRAM {SystemInfo.graphicsMemorySize} MB\n{Screen.width}x{Screen.height}, {QualitySettings.activeColorSpace}, VSync {QualitySettings.vSyncCount}, target {Application.targetFrameRate}\nUnity {Application.unityVersion}\nUTC {DateTime.UtcNow:o}\nSeed {world.Recipe.Seed}; scale .1; clear noon; High terrain; view 60km; fine radius153.6m; grass30/300m; default non-grass.\nCPU sections are submission/update times, NOT GPU times. PresentMon is the independent GPU/presentation measurement.\n");
        File.AppendAllText(Path.Combine(output,"machine.txt"),$"Forced single offscreen URP render {width}x{height}; automatic camera disabled. Frame timing stats enabled: {FrameTimingManager.IsFeatureEnabled()}. Original CPU paths: {WorldGenPerformance.Original}.\n");
        File.WriteAllText(Path.Combine(output,"summary.csv"),"site,cell,biome,relief,case,frames,seconds,fps,p50_ms,p95_ms,p99_ms,max_ms,over10ms,over16ms,scatter_update_ms,scatter_submit_ms,grass_submit_ms,weather_ms,tiles,wanted,objects,specialists,mid,trees,scatter_draws,grass_draws,grass_candidates,grass_bytes,frustum_rejected,terrain_rejected,settled,start_qpc,end_qpc,gpu_ms,cpu_ms,timing_samples,gpu_drain_ms\n");
        foreach(var site in sites)
        {
            if(site.Item2<0||filter.Length>0&&!filter.Split(',').Contains(site.Item1))continue;
            void Defaults(){var w=WeatherVideoOptions.Current;w.Clouds=1;w.SunGlare=.35f;w.SunRays=.12f;w.Adaptation=.5f;w.Visibility=1;TerrainVideoOptions.ResetVegetation();TerrainVideoOptions.GroundCover=true;TerrainVideoOptions.GrassDensity=1;TerrainVideoOptions.GrassDetail=2;TerrainVideoOptions.GrassBladeDistance=30;TerrainVideoOptions.GrassDistance=300;TerrainVideoOptions.GrassCanopy=true;TerrainVideoOptions.Current.Quality=2;TerrainVideoOptions.Current.Effort=1;TerrainVideoOptions.Current.ViewDistanceM=60000;TerrainVideoOptions.Current.FullDetailTiles=1.5;}
            Defaults();TerrainVideoOptions.Revision++;selectedCell=site.Item2;ExploreTerrain(world.Surface.Cells[site.Item2].Position);
            deadline=Time.realtimeSinceStartupAsDouble+240;while(terrainExplorer!=null&&!terrainExplorer.Ready&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(terrainExplorer==null||!terrainExplorer.Ready){Debug.LogError("PERF terrain unavailable");Application.Quit(1);yield break;}
            terrainExplorer.PerformanceDrySite();terrainExplorer.TestView(1.65,20,3);terrainExplorer.PerformanceRender(width,height);
            TerrainMapViewport.Degrees(terrainExplorer.Position,out double lat,out double lon);play.SetLocalSolarHour(12,lon);
            PlayCommands.Execute(play,0,"/weather clear",weather:terrainExplorer.WeatherControl);
            File.AppendAllText(Path.Combine(output,"sites.txt"),$"{site.Item1} cell {site.Item2} {lat:R},{lon:R}; biome {world.Ecology.Habitat[site.Item2].Biome}; relief {world.Ecology.Habitat[site.Item2].ReliefM}\n");
            bool previousTerrain=false;
            foreach(var item in cases)
            {
                if(caseFilter.Length>0&&!caseFilter.Split(',').Contains(item.Name))continue;
                Defaults();WorldGenPerformance.OriginalCase=false;terrainExplorer.PerformancePose();terrainExplorer.ScatterEnabled=true;terrainExplorer.PerformanceShadows(true);item.Apply();TerrainVideoOptions.VegetationRevision++;terrainExplorer.PerformanceInvalidate();
                if(item.Terrain||previousTerrain)TerrainVideoOptions.Revision++;previousTerrain=item.Terrain;
                // Readiness is explicit; partial scenes are labelled, never silently accepted.
                yield return null;deadline=Time.realtimeSinceStartupAsDouble+(item.Name=="view-200km"||item.Name=="full"||item.Terrain&&terrainExplorer.WantedCount>1000?420:150);
                while(!terrainExplorer.PerformanceSettled&&terrainExplorer.Error==""&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                if(terrainExplorer.Error!=""){Debug.LogError("PERF "+terrainExplorer.Error);Application.Quit(1);yield break;}
                yield return new WaitForSecondsRealtime(4);
                if(item.Name=="full")
                {
                    // Explicitly let the old path allocate/place its empty pages,
                    // then verify their real GPU root counts before pruning them.
                    WorldGenPerformance.OriginalCase=true;terrainExplorer.PerformanceInvalidate();
                    yield return new WaitForSecondsRealtime(1);
                    double emptyDeadline=Time.realtimeSinceStartupAsDouble+20;
                    while(terrainExplorer.PerformanceCoverPending&&Time.realtimeSinceStartupAsDouble<emptyDeadline)yield return null;
                    if(terrainExplorer.PerformanceCoverPending)throw new InvalidOperationException("Legacy cover verification did not finish placement");
                    File.AppendAllText(Path.Combine(output,"equivalence.txt"),site.Item1+": "+terrainExplorer.PerformanceVerifyEmptyCover()+" prunable pages verified with zero actual GPU roots.\n");
                    WorldGenPerformance.OriginalCase=false;terrainExplorer.PerformanceInvalidate();yield return null;
                    File.AppendAllText(Path.Combine(output,"equivalence.txt"),site.Item1+": "+terrainExplorer.PerformanceVerifyVisibility()+"\n");
                    yield return new WaitForSecondsRealtime(2);terrainExplorer.PerformanceCapture(Path.Combine(output,site.Item1+".png"));yield return new WaitForSecondsRealtime(1);
                }
                if(item.Name=="flying-60m"){terrainExplorer.PerformanceCapture(Path.Combine(output,site.Item1+"-flying.png"));yield return new WaitForSecondsRealtime(1);}
                var frames=new List<double>(10000);var sections=new double[4];int gc=GC.CollectionCount(0);
                var timings=new FrameTiming[1];double gpu=0,cpu=0;int timingCount=0;ulong lastTiming=0;
                terrainExplorer.WeatherCheckGpuBarrier(); // Drain pre-window work before counting renders.
                WorldGenPerformance.Measuring=true;
                long qpcStart=System.Diagnostics.Stopwatch.GetTimestamp();double start=Time.realtimeSinceStartupAsDouble,previous=start;
                Debug.Log("PERF BEGIN "+site.Item1+" "+item.Name+" qpc="+qpcStart);
                while(Time.realtimeSinceStartupAsDouble-start<seconds)
                {
                    yield return null;double now=Time.realtimeSinceStartupAsDouble;frames.Add((now-previous)*1000);previous=now;
                    if(item.Name.StartsWith("turning",StringComparison.Ordinal))terrainExplorer.PerformanceTurn(20+(float)((now-start)*60));
                    if(item.Name=="travel")terrainExplorer.PerformanceTravel((now-start)*8);
                    for(int j=0;j<4;j++)sections[j]+=WorldGenPerformance.Milliseconds[j];
                    FrameTimingManager.CaptureFrameTimings();
                    if(FrameTimingManager.GetLatestTimings(1,timings)>0&&timings[0].frameStartTimestamp!=lastTiming&&timings[0].gpuFrameTime>0)
                    {lastTiming=timings[0].frameStartTimestamp;gpu+=timings[0].gpuFrameTime;cpu+=timings[0].cpuFrameTime;timingCount++;}
                }
                double drainStart=Time.realtimeSinceStartupAsDouble;
                terrainExplorer.WeatherCheckGpuBarrier(); // Include completion, not just CPU queueing, in mean FPS.
                double gpuDrain=(Time.realtimeSinceStartupAsDouble-drainStart)*1000;
                WorldGenPerformance.Measuring=false;
                long qpcEnd=System.Diagnostics.Stopwatch.GetTimestamp();double elapsed=Time.realtimeSinceStartupAsDouble-start;var sorted=frames.OrderBy(v=>v).ToArray();
                double P(double p)=>sorted[Math.Min(sorted.Length-1,(int)(sorted.Length*p))];
                var h=world.Ecology.Habitat[site.Item2];
                string row=string.Join(",",site.Item1,site.Item2,h.Biome,h.ReliefM,item.Name,frames.Count,Perf(elapsed),Perf(frames.Count/elapsed),Perf(P(.5)),Perf(P(.95)),Perf(P(.99)),Perf(sorted.Last()),frames.Count(v=>v>10),frames.Count(v=>v>16.667),Perf(sections[0]/frames.Count),Perf(sections[1]/frames.Count),Perf(sections[2]/frames.Count),Perf(sections[3]/frames.Count),terrainExplorer.PerformanceCounts,terrainExplorer.PerformanceSettled,qpcStart,qpcEnd);
                row+=","+Perf(gpu/Math.Max(1,timingCount))+","+Perf(cpu/Math.Max(1,timingCount))+","+timingCount+","+Perf(gpuDrain);
                File.AppendAllText(Path.Combine(output,"summary.csv"),row+"\n");
                File.WriteAllLines(Path.Combine(output,site.Item1+"-"+item.Name+"-frames.csv"),new[]{"ms"}.Concat(frames.Select(Perf)));
                Debug.Log("PERF END "+row+" gc0="+(GC.CollectionCount(0)-gc));
            }
            File.AppendAllText(Path.Combine(output,"equivalence.txt"),site.Item1+": "+terrainExplorer.PerformancePendingComparisons+" loading/idle readiness comparisons passed.\n");
            terrainExplorer.Close();yield return null;yield return null;
        }
        File.WriteAllText(Path.Combine(output,"complete.txt"),DateTime.UtcNow.ToString("o"));Application.Quit(0);
    }
}
