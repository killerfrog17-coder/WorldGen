using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using Unity.Profiling;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

// Opt-in, release-player benchmark. No PlayerPrefs writes, saves, UI automation,
// screenshots or per-frame file IO in measured windows. One explicit URP render
// per frame replaces automatic rendering; GPU drains bracket each timing window.
public static class WorldGenPerformance
{
    public static readonly bool Enabled=Environment.GetCommandLineArgs().Contains("--worldgen-performance");
    public static readonly bool Presented=Enabled&&Environment.GetCommandLineArgs().Contains("--worldgen-perf-presented");
    public static readonly FullScreenMode PresentationMode=ReadPresentationMode();
    static FullScreenMode ReadPresentationMode()
    {
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--worldgen-perf-window-mode");
        if(!Presented||index<0)return FullScreenMode.Windowed;
        if(index+1>=args.Length)throw new ArgumentException("Missing performance window mode");
        switch(args[index+1])
        {case "Windowed":return FullScreenMode.Windowed;case "Borderless":return FullScreenMode.FullScreenWindow;case "Exclusive":return FullScreenMode.ExclusiveFullScreen;default:throw new ArgumentException("Unknown performance window mode");}
    }
    static readonly bool original=Enabled&&Environment.GetCommandLineArgs().Contains("--worldgen-perf-original");
    public static bool Original=>Enabled&&(original||OriginalCase);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool QueryPerformanceCounter(out long value);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool QueryPerformanceFrequency(out long value);
    // Mono Stopwatch and engine/ETW counters can have equal frequencies but
    // different origins. A once-per-window sandwich relates their timelines.
    public static (long Before,long Native,long After,long Frequency) CaptureNativeClock()
    {
        long before=System.Diagnostics.Stopwatch.GetTimestamp(),native=0,frequency=0;
        if(Application.platform==RuntimePlatform.WindowsPlayer||Application.platform==RuntimePlatform.WindowsEditor)
        {if(!QueryPerformanceCounter(out native))native=0;if(!QueryPerformanceFrequency(out frequency))frequency=0;}
        return(before,native,System.Diagnostics.Stopwatch.GetTimestamp(),frequency);
    }
    public static bool OriginalCase;
    public static bool DisableScatterViewGuard;
    public static bool ForceFreshVisibleGround;
    public static bool DirectionalScatter;
    public static bool GpuNearOcclusion;
    public static bool NonIndexedClumps;
    public static bool ForceFreshWaterSamples;
    public static bool FullWaterAim;
    public static bool LegacyGroupVisibility;
    public static bool ParallelScatter=true;
    public static bool BurstScatter=true;
    public static bool ScatterRangeCache;
    public static bool MemoHumanInputs;
    public static bool RepeatHumanOutputBinding;
    public static bool NoTerrainBufferReuse;
    public static bool ResidentCoverReuse=true;
    public static bool IndexedWalkingRivers=true;
    public static bool NoBuilderBufferReuse;
    public static bool StagedGroundPublication=true;
    public static bool KeepBackdrops=true;
    public static bool PacedTerrainRetirement=true;
    public static bool StagedTerrainRetirement=true;
    public static bool MemoryTrace;
    public static bool TracePresentedCamera;
    public static bool PartialForestUploads=true;
    public static bool DetailedFoliageComparison;
    // Diagnostic gross managed-heap growth, inclusive of nested work and any
    // background allocations. This is not a per-thread allocation counter.
    public static readonly long[] HeapGrowth=new long[48];
    public static readonly int[] HeapCollectionIntervals=new int[48];
    public static int HeapIntervalCollections{get{int total=0;foreach(int value in HeapCollectionIntervals)total+=value;return total;}}
    static readonly Dictionary<long,(long bytes,int collection)> heapStarts=new Dictionary<long,(long,int)>(64);
    public static bool NarrowScatterGuard;
    public static bool LegacyTreeResident,LegacyTreeShader,LegacyTreeWind;
    public static int FoliageShadowTaps;
    public static bool NoTrunkCasters,NoFoliageShadowReceivers;
    public static float TreeTrunkShadowRange;
    public static bool Measuring,ProfileRenderCompletion;
    public static double CompletedRenderMs;
    public static readonly double[] Milliseconds=new double[4];
    public static readonly bool TraceEnabled=Enabled&&Environment.GetCommandLineArgs().Contains("--worldgen-stutter-trace");
    public static readonly bool FineCullTrace=TraceEnabled&&Environment.GetCommandLineArgs().Contains("--worldgen-fine-cull-trace");
    static readonly List<(long Time,string Animal,string Stage)> faunaStages=TraceEnabled?new List<(long,string,string)>(256):null;
    public static void TraceFauna(string animal,string stage)
    {if(TraceEnabled&&Measuring&&faunaStages.Count<4096)faunaStages.Add((System.Diagnostics.Stopwatch.GetTimestamp(),animal,stage));}
    public static void ClearFaunaTrace(){faunaStages?.Clear();}
    public static void WriteFaunaTrace(string path,long start)
    {
        if(!TraceEnabled)return;
        File.WriteAllLines(path,new[]{"t_s,animal,stage"}.Concat(faunaStages.Select(e=>
            ((e.Time-start)/(double)System.Diagnostics.Stopwatch.Frequency).ToString("R",CultureInfo.InvariantCulture)+","+e.Animal+","+e.Stage)));
    }
    public static readonly double[] TraceMilliseconds=new double[48];static int traceFrame=-1;
    public static bool SuppressIdleScatter,SkipMapUploads,ForceFreshCoverCull,NoLocalTrees,ForceFreshForestOwnership,TreeLegacyAlpha,SuppressFarForest,ForceIndividualTreeAdmission,DrainEachFrame,LegacyTreeQueues;
    public static long TraceBegin()
    {
        if(!TraceEnabled)return 0;
        if(traceFrame!=Time.frameCount){traceFrame=Time.frameCount;Array.Clear(TraceMilliseconds,0,TraceMilliseconds.Length);TerrainQueryTiming.Clear();if(MemoryTrace){Array.Clear(HeapGrowth,0,HeapGrowth.Length);Array.Clear(HeapCollectionIntervals,0,HeapCollectionIntervals.Length);heapStarts.Clear();}}
        long bytes=MemoryTrace?GC.GetTotalMemory(false):0;int collection=MemoryTrace?GC.CollectionCount(0):0;
        long start=System.Diagnostics.Stopwatch.GetTimestamp();if(MemoryTrace)heapStarts[start]=(bytes,collection);return start;
    }
    public static void TraceEnd(int slot,long started)
    {
        if(started==0)return;TraceMilliseconds[slot]+=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000d/System.Diagnostics.Stopwatch.Frequency;
        if(MemoryTrace&&heapStarts.TryGetValue(started,out var sample))
        {heapStarts.Remove(started);if(GC.CollectionCount(0)!=sample.collection)HeapCollectionIntervals[slot]++;else HeapGrowth[slot]+=Math.Max(0,GC.GetTotalMemory(false)-sample.bytes);}
    }
    public static long Begin()=>Enabled?System.Diagnostics.Stopwatch.GetTimestamp():0;
    public static void End(int slot,long start){if(start!=0)Milliseconds[slot]=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;}
}

public sealed partial class LocalTerrainExplorer
{
    public string PerformanceVerifyHotQueries()
    {
        int shaders=0,bindings=0,behaviours=0;
        foreach(var model in scatterArt.Values)foreach(var part in model.Parts)
        {
            var shader=part.Material.shader;bool expected=shader.name=="WorldGen/TreeSurface2";
            if(IsTreeSurfaceShader(shader)!=expected||IsTreeSurfaceShader(shader)!=expected)
                throw new InvalidOperationException("Cached tree shader classification differs from the native shader name");
            shaders++;
        }
        foreach(ScatterKind kind in Enum.GetValues(typeof(ScatterKind)))
        {
            var expected=artSet==null||artSet.Scatter==null?null:Array.Find(artSet.Scatter,b=>b!=null&&b.Kind==kind);
            if(!ReferenceEquals(FindScatterBinding(kind),expected))throw new InvalidOperationException("Scatter binding identity changed: "+kind);
            bindings++;
        }
        foreach(var animal in animals.Values)
        {
            var profile=animal.Spawn.Profile;int taxon=animal.Spawn.Taxon;
            var first=AnimalBehaviour(profile,taxon);
            if(!ReferenceEquals(first,animal.Behaviour)||!ReferenceEquals(first,AnimalBehaviour(profile,taxon)))
                throw new InvalidOperationException("Animal behaviour cache changed an admitted animal's profile");
            behaviours++;
        }
        if(behaviours==0)throw new InvalidOperationException("Hot-query proof requires actual admitted animals");
        return $"PASS hot queries: {shaders} actual shader classifications, {bindings} literal binding identities, {behaviours} admitted animal profile identities; no byte claim (use the allocation profiler).";
    }
    public string PerformanceVerifyRenderCleanup()
    {
        // Consecutive manual renders within the same Update keep shader time,
        // poses, light/weather samples and populations identical.
        ConfigureExplorationRenderer(true);DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(null);var original=LastCapturePixels;
        ConfigureExplorationRenderer(false);DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(null);var clean=LastCapturePixels;
        int delta=0;for(int i=0;i<original.Length;i++)delta=Math.Max(delta,Math.Max(Math.Abs(original[i].r-clean[i].r),Math.Max(Math.Abs(original[i].g-clean[i].g),Math.Abs(original[i].b-clean[i].b))));
        if(delta>1)throw new InvalidOperationException("Exploration render-pass removal changed pixels: "+delta);
        return "Original renderer/AO/copy vs exploration renderer: max RGB delta "+delta;
    }
    public string PerformanceVerifyVegetationScene()
    {
        var materials=new List<Material>(ownedScatterMaterials);if(groundCoverMaterial!=null)materials.Add(groundCoverMaterial);if(groundCanopyMaterial!=null)materials.Add(groundCanopyMaterial);if(groundClumpMaterial!=null)materials.Add(groundClumpMaterial);
        foreach(var material in materials)if(material!=null&&material.HasProperty("_VegetationSceneEnabled"))
        {
            bool grass=material==groundCoverMaterial||material==groundCanopyMaterial||material==groundClumpMaterial;
            if(grass){material.SetColor("_FogColor",landMaterial.GetColor("_FogColor"));material.SetColor("_AmbientTint",landMaterial.GetColor("_AmbientTint"));}
            else{material.SetVector("_FogColor",landMaterial.GetColor("_FogColor"));material.SetVector("_AmbientTint",landMaterial.GetColor("_AmbientTint"));}
            material.SetFloat("_FogDistance",landMaterial.GetFloat("_FogDistance"));if(material.HasProperty("_Daylight"))material.SetFloat("_Daylight",landMaterial.GetFloat("_Daylight"));
            material.SetVector("_AirGeometry",landMaterial.GetVector("_AirGeometry"));material.SetFloat("_AirObserverM",landMaterial.GetFloat("_AirObserverM"));material.SetFloat("_AirHaze",landMaterial.GetFloat("_AirHaze"));material.SetInt("_SecondaryLightCount",terrainSecondaryCount);material.SetVectorArray("_SecondaryDirections",secondaryDirections);material.SetVectorArray("_SecondaryColors",secondaryColors);
            material.SetFloat("_VegetationSceneEnabled",0);
        }
        DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(null);var before=LastCapturePixels;
        foreach(var material in materials)if(material!=null&&material.HasProperty("_VegetationSceneEnabled"))material.SetFloat("_VegetationSceneEnabled",1);
        DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(null);var after=LastCapturePixels;
        int delta=0;for(int i=0;i<before.Length;i++)delta=Math.Max(delta,Math.Max(Math.Abs(before[i].r-after[i].r),Math.Max(Math.Abs(before[i].g-after[i].g),Math.Abs(before[i].b-after[i].b))));
        if(delta>1)throw new InvalidOperationException("Shared vegetation scene changed pixels: "+delta);
        return "Legacy material vs shared vegetation scene: max RGB delta "+delta;
    }
    static uint[] ReadCoverDrawCounts(CoverGpu gpu)
    {
        // A resident page outside the view can own an allocated argument buffer
        // which has never been initialized by CullGroundCover. Reading it gave
        // meaningless billion-instance totals in the approach-review CSV.
        if(gpu.Topology==0)return new uint[6];
        uint[] counts;
        if(gpu.Indexed){var data=new GraphicsBuffer.IndirectDrawIndexedArgs[6];gpu.Args.GetData(data);counts=data.Select(a=>a.instanceCount).ToArray();}
        else {var data=new GraphicsBuffer.IndirectDrawArgs[6];gpu.Args.GetData(data);counts=data.Select(a=>a.instanceCount).ToArray();}
        foreach(uint count in counts)if(count>gpu.Candidates)throw new InvalidOperationException("Grass draw count exceeds the page's root capacity");
        return counts;
    }
    public string PerformanceVerifyGrassCullCache()
    {
        int compared=0;var pose=view.transform.localRotation;
        foreach(var pair in tiles)
        {
            if(pair.Value.CoverPages.Count==0)continue;
            foreach(var gpu in pair.Value.CoverPages.Values)
            {
                var origin=new Vector4((float)(pair.Key.X*pair.Key.SizeM-originX),0,(float)(pair.Key.Z*pair.Key.SizeM-originZ),0);
                for(int turn=0;turn<2;turn++)
                {
                    view.transform.localRotation=pose*Quaternion.Euler(0,turn*21,0);
                    WorldGenPerformance.ForceFreshCoverCull=false;CullGroundCover(gpu,origin,view,pair.Value,(float)pair.Key.SizeM);
                    var cached=new uint[5][];var counts=ReadCoverDrawCounts(gpu);
                    for(int lod=0;lod<5;lod++){cached[lod]=new uint[counts[lod]];if(cached[lod].Length>0)gpu.Visible[lod].GetData(cached[lod],0,0,cached[lod].Length);Array.Sort(cached[lod]);}
                    long before=GrassCullDispatches;CullGroundCover(gpu,origin,view,pair.Value,(float)pair.Key.SizeM);if(GrassCullDispatches!=before)throw new InvalidOperationException("Identical visibility inputs did not reuse grass lists");
                    WorldGenPerformance.ForceFreshCoverCull=true;CullGroundCover(gpu,origin,view,pair.Value,(float)pair.Key.SizeM);counts=ReadCoverDrawCounts(gpu);
                    for(int lod=0;lod<5;lod++)
                    {var fresh=new uint[counts[lod]];if(fresh.Length>0)gpu.Visible[lod].GetData(fresh,0,0,fresh.Length);Array.Sort(fresh);if(!fresh.SequenceEqual(cached[lod]))throw new InvalidOperationException("Cached grass visibility changed admitted roots");compared++;}
                }
                if(compared>=40)break;
            }
            if(compared>=40)break;
        }
        view.transform.localRotation=pose;WorldGenPerformance.ForceFreshCoverCull=false;
        return compared+" cached/fresh grass visible-root set comparisons, with camera-turn invalidation";
    }
    public string PerformanceVerifyMapMaterials()
    {
        var seed=ExecuteExplorerCommand("/seed");
        if(seed==null||!seed.Success||seed.Lines.Length!=1||seed.Lines[0]!="World seed: "+sourceWorld.Recipe.Seed.ToString(CultureInfo.InvariantCulture)||ExecuteExplorerCommand("/seed extra").Success)throw new InvalidOperationException("Seed command mismatch");
        var edgeCases=new[]{Array.Empty<double>(),new[]{1d,2,3,4},new[]{1d,2,3,5},new[]{double.NaN,-0d,double.PositiveInfinity,4},new[]{double.NaN,0d,double.PositiveInfinity,4}};
        foreach(var a in edgeCases)foreach(var b in edgeCases)if(TerrainEdges.Equal(a,b)!=a.SequenceEqual(b))throw new InvalidOperationException("Edge comparison mismatch");
        var cursor=terrain.CreateMapHeightCursor();var scratch=new List<int>();int checkedPoints=0;
        for(int i=0;i<48;i++)
        {
            int cell=(i*7919)%sourceWorld.Surface.Cells.Length;var p=sourceWorld.Surface.Cells[cell].Position;
            p=(p+V.Cross(p,new V(.31,.57,.79))*.000013).Unit;
            var expected=terrain.MapPatch(p,2048/HorizontalScale).SampleMap(p,512);var actual=cursor.Sample(p,512);terrain.FillMapMaterials(p,ref actual,scratch);
            bool Same(double a,double b)=>a.Equals(b)||Math.Abs(a-b)<1e-9;
            if(!expected.Ground.Equals(actual.Ground)||!expected.Cover.Equals(actual.Cover)||!expected.MidCover.Equals(actual.MidCover)||!expected.Forest.Equals(actual.Forest)||expected.Cell!=actual.Cell||expected.Biome!=actual.Biome||!Same(expected.ElevationM,actual.ElevationM)||!Same(expected.WaterSurfaceM,actual.WaterSurfaceM)||!Same(expected.WaterMaskM,actual.WaterMaskM)||!Same(expected.HillHeightM,actual.HillHeightM)||!Same(expected.MountainHeightM,actual.MountainHeightM)||!Same(expected.OceanInfluence,actual.OceanInfluence))throw new InvalidOperationException("Map kernel equivalence failed at cell "+cell);
            checkedPoints++;
        }
        return checkedPoints+" global map probes: identical blended ground/cover/mid/forest profiles, source identities, and terrain/water values.";
    }
    public bool PerformanceCoverPending=>coverRequests.Count>0;
    public void PerformanceTreeAlpha(bool optimized)
    {
        foreach(var material in ownedScatterMaterials)if(material!=null&&material.HasProperty("_TreeEarlyAlpha"))
        {
            material.SetFloat("_TreeEarlyAlpha",optimized?1:0);
            if(material.shader.name=="WorldGen/TreeSurface2")
            {if(optimized&&SystemInfo.graphicsShaderLevel>=45)material.EnableKeyword("WORLDGEN_TREE_QUAD_ALPHA");else material.DisableKeyword("WORLDGEN_TREE_QUAD_ALPHA");}
        }
    }
    public string PerformanceVerifyTreeAlpha()
    {
        PerformanceTreeAlpha(false);DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(null);var before=LastCapturePixels;
        PerformanceTreeAlpha(true);DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(null);var after=LastCapturePixels;int delta=0;
        for(int i=0;i<before.Length;i++)delta=Math.Max(delta,Math.Max(Math.Abs(before[i].r-after[i].r),Math.Max(Math.Abs(before[i].g-after[i].g),Math.Abs(before[i].b-after[i].b))));
        if(delta>1)throw new InvalidOperationException("Conservative tree alpha optimization changed pixels: "+delta);return "Tree initial-alpha early reject vs literal 12-sample views: max RGB delta "+delta;
    }
    public void PerformanceTreeQueues(bool optimized)
    {
        foreach(var material in ownedScatterMaterials)
        {
            if(material==null)continue;
            if(material.shader.name=="WorldGen/ForestPackets")material.renderQueue=optimized?2002:2000;
            else if(material.shader.name=="WorldGen/TreeSurface2"&&material.GetFloat("_Impostor")>.5)material.renderQueue=optimized?2001:2000;
        }
    }
    public string PerformanceVerifyTreeQueues(string output)
    {
        Directory.CreateDirectory(output);var pose=view.transform.localRotation;int delta=0;
        for(int turn=0;turn<4;turn++)
        {
            view.transform.localRotation=pose*Quaternion.Euler(0,turn*90,0);BuildScatterBatches();
            PerformanceTreeQueues(false);DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(Path.Combine(output,turn+"-original.png"));var before=LastCapturePixels;
            PerformanceTreeQueues(true);DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(Path.Combine(output,turn+"-optimized.png"));var after=LastCapturePixels;
            for(int i=0;i<before.Length;i++)delta=Math.Max(delta,Math.Max(Math.Abs(before[i].r-after[i].r),Math.Max(Math.Abs(before[i].g-after[i].g),Math.Abs(before[i].b-after[i].b))));
        }
        view.transform.localRotation=pose;BuildScatterBatches();DrawScatter();DrawGroundCover();view.Render();
        if(delta>1)throw new InvalidOperationException("Opaque impostor queue tiers changed pixels: "+delta);
        return "4 headings: original vs terrain/individual/field opaque queue tiers, max RGB delta "+delta;
    }
    public string PerformanceVerifyTreeGroupAdmission()
    {
        var pose=view.transform.localRotation;int checks=0,maximumDelta=0;
        for(int turn=0;turn<4;turn++)
        {
            view.transform.localRotation=pose*Quaternion.Euler(0,turn*90,0);
            WorldGenPerformance.ForceIndividualTreeAdmission=true;scatterDirty=true;BuildScatterBatches();int count=ScatterObjects;DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(null);var literal=LastCapturePixels;
            WorldGenPerformance.ForceIndividualTreeAdmission=false;scatterDirty=true;BuildScatterBatches();if(count!=ScatterObjects)throw new InvalidOperationException("Whole-bin tree admission changed visible instance count");DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(null);var grouped=LastCapturePixels;
            for(int i=0;i<literal.Length;i++)maximumDelta=Math.Max(maximumDelta,Math.Max(Math.Abs(literal[i].r-grouped[i].r),Math.Max(Math.Abs(literal[i].g-grouped[i].g),Math.Abs(literal[i].b-grouped[i].b))));checks++;
        }
        view.transform.localRotation=pose;scatterDirty=true;BuildScatterBatches();DrawScatter();DrawGroundCover();view.Render();
        if(maximumDelta>1)throw new InvalidOperationException("Whole-bin tree admission changed pixels: "+maximumDelta);
        return checks+" headings: tree-only bin vs literal individual admission, same visible counts; max RGB delta "+maximumDelta;
    }
    string PerformanceVerifyPreparedTreeBins()
    {
        var chunk=scatterChunks.Values.FirstOrDefault(c=>c.Source.Instances.Count(p=>p.Kind==ScatterKind.GeneratedTree)>=100);
        if(chunk==null)return "No dense tree source for prepared-bin fixture";
        int trees=chunk.Source.Instances.Count(p=>p.Kind==ScatterKind.GeneratedTree);
        var savedFrame=frame;var savedTiles=tiles;double savedX=originX,savedZ=originZ;
        try
        {
            foreach(var offset in new[]{new Vector2(128,64),new Vector2((float)ReanchorLimit,0),new Vector2(0,(float)ReanchorLimit)})
            {
                var next=new LocalTerrainFrame(savedFrame.Position(offset.x,offset.y),terrain.RadiusM,savedFrame.AltitudeM,HorizontalScale);
                var pending=new PreparedFrame{Frame=next};var prepared=new ScatterChunk{Source=chunk.Source};
                ProjectPreparedScatter(prepared,pending,0,0);
                // No resident-grid substitute: compare the two production
                // projection paths against the same canonical root heights.
                // All temporary explorer state is restored before rendering.
                frame=next;tiles=pending.Tiles;originX=originZ=0;
                var reference=new ScatterChunk{Source=chunk.Source};ProjectScatter(reference);
                if(!ReferenceEquals(prepared.GroupsFrame,next)||prepared.Groups.Length!=reference.Groups.Length)throw new InvalidOperationException("Prepared dense tree bins use the old chart");
                for(int i=0;i<prepared.Matrices.Length;i++)for(int j=0;j<16;j++)
                    if(prepared.Matrices[i][j]!=reference.Matrices[i][j])throw new InvalidOperationException("Prepared scatter matrix differs from ordinary projection: "+chunk.Source.Instances[i].Kind+" index "+i+", component "+j+", prepared "+prepared.Matrices[i][j]+", ordinary "+reference.Matrices[i][j]);
                for(int i=0;i<prepared.Groups.Length;i++)
                {
                    var a=prepared.Groups[i];var b=reference.Groups[i];
                    if(a.Trees!=b.Trees||!a.Indices.SequenceEqual(b.Indices)||a.RootBounds!=b.RootBounds||a.Bounds!=b.Bounds)throw new InvalidOperationException("Prepared dense tree spatial bins differ from ordinary projection");
                }
            }
        }
        finally{frame=savedFrame;tiles=savedTiles;originX=savedX;originZ=savedZ;}
        return trees+" trees: 3 alternate-chart prepared/ordinary matrices and spatial bins exact";
    }
    public string PerformanceVerifyForestIdentity()
    {
        string projection=PerformanceVerifyPreparedTreeBins();
        // Isolate permit accounting from rendering: an in-flight forest job
        // consumes capacity; a completed/cancelled payload must not block
        // terrain when tree rendering is disabled or installation is deferred.
        int beforeWorkers=ActiveWorkerCount;var promise=new System.Threading.Tasks.TaskCompletionSource<ForestPage>();
        var simulated=new ForestWork{Task=promise.Task,Cancellation=new System.Threading.CancellationTokenSource()};forestJobs.Add(simulated);
        try
        {
            if(ActiveWorkerCount!=beforeWorkers+1)throw new InvalidOperationException("In-flight forest work escaped shared permit accounting");
            promise.SetCanceled();if(ActiveWorkerCount!=beforeWorkers)throw new InvalidOperationException("Completed forest work kept a CPU permit");
        }
        finally{forestJobs.Remove(simulated);simulated.Cancellation.Dispose();}
        if(meshProcess!=null&&forestPriority.Length>0)
        {
            using(var stop=new System.Threading.CancellationTokenSource())
            {
                bool cancelled=false;
                try{meshProcess.Forest(forestPriority[0],stop.Token,()=>stop.Cancel());}catch(OperationCanceledException){cancelled=true;}
                if(!cancelled)throw new InvalidOperationException("Cancelled forest socket request was not surfaced as cancellation");
            }
        }
        int ownershipChecks=0;
        foreach(var chunk in scatterChunks.Values)
        {foreach(var tree in chunk.Source.Instances){if(tree.Kind!=ScatterKind.GeneratedTree)continue;WorldGenPerformance.ForceFreshForestOwnership=false;bool cached=ForestOwnsTree(tree);WorldGenPerformance.ForceFreshForestOwnership=true;bool fresh=ForestOwnsTree(tree);WorldGenPerformance.ForceFreshForestOwnership=false;if(cached!=fresh)throw new InvalidOperationException("Forest ownership memo changed CPU/GPU handoff");if(++ownershipChecks>=512)break;}if(ownershipChecks>=512)break;}
        var compact=new ForestBatch();var testKey=new ForestPageKey(5,1000000,1000000,0);var testField=new ForestField{Source=new ForestPage{Key=testKey}};var references=new List<int>();
        for(int i=0;i<1000;i++){compact.Data.Add(i<600?default:new ForestRoot{Height=1,Position=new Vector3(i,0,0)});compact.GroundLevels.Add(0);if(i<600)compact.Free.Push(i);else references.Add(i);}
        testField.Roots.Add(compact,references);forestFields.Add(testKey,testField);
        try{CompactForest(compact);if(compact.Data.Count!=400||compact.Free.Count!=0)throw new InvalidOperationException("Forest historical holes not compacted");for(int i=0;i<references.Count;i++)if(references[i]!=i||compact.Data[i].Position.x!=i+600)throw new InvalidOperationException("Forest compaction lost field/root references");}
        finally{forestFields.Remove(testKey);}
        int checkedRoots=0,empty=0;var snapshot=new Dictionary<ForestPageKey,ForestPage>();
        foreach(var pair in forestFields)
        {
            var source=pair.Value.Source;snapshot.Add(pair.Key,source);if(source.Roots.Length==0)empty++;
            if(checkedRoots>=128)continue;
            foreach(var root in source.Roots)
            {
                var tree=root.Tree;int slot=(int)(tree.Id&65535);var key=new ScatterKey((int)(tree.Id>>61),(int)((tree.Id>>40)&0xfffff),(int)((tree.Id>>20)&0xfffff));var patch=terrain.Patch(scatter.Position(key),840/HorizontalScale);
                if(!scatter.TryTree(key,slot%LocalScatter.TreeCandidateSide,slot/LocalScatter.TreeCandidateSide,patch,out var exact)||!exact.Equals(tree))throw new InvalidOperationException("Packaged worker far tree differs from canonical near placement");
                checkedRoots++;if(checkedRoots>=128)break;
            }
        }
        // Immutable payloads survive a geographic chart projection. Do not
        // mutate the live camera/terrain just to test that pure transformation.
        var alternate=new LocalTerrainFrame((frame.Origin+frame.East*(120/frame.RadiusM)).Unit,terrain.RadiusM,frame.AltitudeM,HorizontalScale);
        double worst=0;
        foreach(var pair in snapshot)foreach(var root in pair.Value.Roots.Take(2))
        {var at=root.Tree.Position;double dot=V.Dot(at,alternate.Origin),x=V.Dot(at,alternate.East)*alternate.RadiusM/dot,z=V.Dot(at,alternate.North)*alternate.RadiusM/dot;double error=(alternate.Position(x,z)-at).Length*alternate.RadiusM;worst=Math.Max(worst,error);if(error>.02)throw new InvalidOperationException("Forest reanchor roundtrip exceeds quantized basis tolerance: "+error+"m");}
        return projection+"; shared forest permit and sent-socket cancellation checks pass; "+checkedRoots+" IL2CPP/worker exact tree identities; "+ownershipChecks+" exact ownership cache checks; "+empty+" empty ownership pages retained; alternate-chart max roundtrip "+worst+"m (Core V uses 1e-9 quantization)";
    }
    public int PerformanceVerifyEmptyCover()
    {
        int pages=0;var count=new uint[1];
        foreach(var tile in tiles.Values)
        {
            if(!tile.Cover.IsCreated||tile.Cover.Length==0||HasMovingCover(tile))continue;
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
    string[] PerformanceInstances(bool visibleOnly=false,bool castersOnly=false,bool preserveOrder=false)
    {
        var values=new List<string>();
        foreach(var b in drawBatches)for(int i=0;i<b.Count;i++)
        {
            if(visibleOnly&&b.Shadows==UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)continue;
            if(castersOnly&&b.Shadows==UnityEngine.Rendering.ShadowCastingMode.Off)continue;
            var s=new System.Text.StringBuilder();s.Append(visibleOnly||castersOnly?b.Key&~(1L<<60):b.Key).Append(':').Append(castersOnly?1:(int)b.Shadows);
            for(int n=0;n<16;n++)s.Append(':').Append(BitConverter.SingleToInt32Bits(b.Matrices[i][n]));
            for(int n=0;n<4;n++)s.Append(':').Append(BitConverter.SingleToInt32Bits(b.Colors[i][n])).Append(':').Append(BitConverter.SingleToInt32Bits(b.Fades[i][n]));
            values.Add(s.ToString());
        }
        if(!preserveOrder)values.Sort(StringComparer.Ordinal);return values.ToArray();
    }
    public string PerformanceVerifyParallelScatter(bool burst=false)
    {
        bool before=WorldGenPerformance.ParallelScatter,beforeBurst=WorldGenPerformance.BurstScatter,directional=WorldGenPerformance.DirectionalScatter;int start=burst?BurstScatterBuilds:ParallelScatterBuilds;
        WorldGenPerformance.DirectionalScatter=true;
        try
        {
            for(int yaw=0;yaw<360;yaw+=45)
            {
                PerformanceTurn(yaw);WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=false;BuildScatterBatches();var sequential=PerformanceInstances();
                WorldGenPerformance.ParallelScatter=!burst;WorldGenPerformance.BurstScatter=burst;BuildScatterBatches();var parallel=PerformanceInstances();
                if(!sequential.SequenceEqual(parallel))throw new InvalidOperationException("Scatter backend changed model/transform/tint/fade/shadow multiset at "+yaw+", Burst="+burst+", counts="+sequential.Length+"/"+parallel.Length+"\nReference only: "+string.Join("\n",sequential.Except(parallel).Take(3))+"\nWorker only: "+string.Join("\n",parallel.Except(sequential).Take(3)));
            }
            if((burst?BurstScatterBuilds:ParallelScatterBuilds)-start!=8)throw new InvalidOperationException("Requested worker backend was not actually exercised at eight headings: Burst="+burst);
            return "PASS eight exact sequential/"+(burst?"Burst-compiled":"managed parallel")+" model, matrix, tint, LOD-fade and shadow instance multisets; engine objects/draw submission remain on main thread";
        }
        finally{WorldGenPerformance.ParallelScatter=before;WorldGenPerformance.BurstScatter=beforeBurst;WorldGenPerformance.DirectionalScatter=directional;PerformanceTurn(20);BuildScatterBatches();}
    }
    public string PerformanceVerifyHumanInputs(bool outputBinding=false)
    {
        bool old=WorldGenPerformance.MemoHumanInputs;int samples=0;float maximum=0;
        try
        {
            foreach(int variant in new[]{0,3})
            {
                HumanActor reference=null,cached=null;
                try
                {
                    reference=CreateHuman(variant,1);cached=CreateHuman(variant,1);
                    foreach(var actor in new[]{reference,cached})
                    {if(actor.Shared==null)throw new InvalidOperationException("Authored human motion unavailable for weight comparison");actor.Output.SetSourcePlayable(actor.Shared.Output);foreach(var renderer in actor.Root.GetComponentsInChildren<Renderer>())renderer.enabled=false;}
                    var a=reference.Root.GetComponentsInChildren<Transform>(true);var b=cached.Root.GetComponentsInChildren<Transform>(true);var roles=reference.Shared.ActionRoles.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
                    if(a.Length!=b.Length||roles.Length==0)throw new InvalidOperationException("Human weight fixture hierarchy/roles differ");
                    for(int step=0;step<Math.Max(360,roles.Length*12);step++)
                    {
                        string role=roles[(step/12)%roles.Length],other=step%48<24?roles[(step/24+1)%roles.Length]:null;
                        float speed=step%180<30?0:step%180<60?1.3f:step%180<90?3:step%180<120?5.8f:1;
                        bool airborne=step%180>=150,crouch=step%180>=120&&step%180<150,swim=step%240>=210;
                        void Apply(HumanActor actor,bool memo)
                        {
                            WorldGenPerformance.MemoHumanInputs=!outputBinding&&memo;
                            if(outputBinding){if(memo)BindHumanOutput(actor,actor.Shared.Output);else actor.Output.SetSourcePlayable(actor.Shared.Output);}
                            actor.Shared.Locomotion(speed,airborne,crouch,swim,1f/60,new Vector2(step%80<40?0:-1,1));
                            actor.Shared.Action(role,step/60d,step%60>=12,step%36>=18,other,step/70d,step%32<16,1f/60,step%50<25,step%30<15,step%40<20);actor.Graph.Evaluate(1f/60);
                        }
                        Apply(reference,false);Apply(cached,true);
                        for(int i=0;i<a.Length;i++)
                        {
                            var pa=a[i].localPosition;var pb=b[i].localPosition;var qa=a[i].localRotation;var qb=b[i].localRotation;var sa=a[i].localScale;var sb=b[i].localScale;
                            float delta=Mathf.Max(Mathf.Abs(pa.x-pb.x),Mathf.Abs(pa.y-pb.y),Mathf.Abs(pa.z-pb.z),Mathf.Abs(qa.x-qb.x),Mathf.Abs(qa.y-qb.y),Mathf.Abs(qa.z-qb.z),Mathf.Abs(qa.w-qb.w),Mathf.Abs(sa.x-sb.x),Mathf.Abs(sa.y-sb.y),Mathf.Abs(sa.z-sb.z));
                            maximum=Math.Max(maximum,delta);samples++;
                            if(delta>1e-6f)throw new InvalidOperationException("Memoized native mixer inputs changed actual authored human pose at "+variant+" / "+step+" / "+a[i].name+": "+delta);
                        }
                    }
                }
                finally{if(reference!=null)DestroyHuman(reference);if(cached!=null)DestroyHuman(cached);}
            }
            return "PASS "+samples+" actual human bone-pose comparisons ("+(outputBinding?"unchanged output binding":"memoized weights")+") across two authored bodies, locomotion/swim/air/crouch and every action layer role; max component error "+maximum;
        }
        finally{WorldGenPerformance.MemoHumanInputs=old;}
    }
    public string PerformanceVerifyVisibility()
    {
        bool old=WorldGenPerformance.DisableScatterViewGuard;WorldGenPerformance.DisableScatterViewGuard=true;
        try{return PerformanceVerifyLiteralVisibility();}
        finally{WorldGenPerformance.DisableScatterViewGuard=old;scatterDirty=true;}
    }
    public string PerformanceVerifyParallelRenders(string output,bool burst=false)
    {
        Directory.CreateDirectory(output);bool oldParallel=WorldGenPerformance.ParallelScatter,oldBurst=WorldGenPerformance.BurstScatter,oldDirectional=WorldGenPerformance.DirectionalScatter;
        float oldAdaptation=WeatherVideoOptions.Current.Adaptation,oldHeading=heading,oldPitch=pitch;var oldEye=view.transform.localPosition;var oldRotation=view.transform.localRotation;
        WeatherVideoOptions.Current.Adaptation=0;WorldGenPerformance.DirectionalScatter=true;int maximum=0,comparisons=0,start=burst?BurstScatterBuilds:ParallelScatterBuilds;
        try
        {
            for(int yaw=0;yaw<360;yaw+=45)foreach(var offset in new[]{new Vector3(0,0,0),new Vector3(8,12,0),new Vector3(0,-12,8),new Vector3(-8,20,-8)})
            {
                var at=oldEye+new Vector3(offset.x,0,offset.z);at.y=(float)SurfaceGround(at.x+originX,at.z+originZ)+1.8f;
                view.transform.localPosition=at;view.transform.localRotation=Quaternion.Euler(oldPitch+offset.y,yaw,0);
                WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=false;BuildScatterBatches();var expected=PerformanceInstances(preserveOrder:true);DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(null);var sequential=LastCapturePixels;
                WorldGenPerformance.ParallelScatter=!burst;WorldGenPerformance.BurstScatter=burst;BuildScatterBatches();
                if(!expected.SequenceEqual(PerformanceInstances(preserveOrder:true)))throw new InvalidOperationException("Parallel scatter changed exact submitted instance order/data at "+yaw+" / "+offset);
                DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(null);var parallel=LastCapturePixels;int delta=0;
                for(int i=0;i<sequential.Length;i++)delta=Math.Max(delta,Math.Max(Math.Abs(sequential[i].r-parallel[i].r),Math.Max(Math.Abs(sequential[i].g-parallel[i].g),Math.Abs(sequential[i].b-parallel[i].b))));
                maximum=Math.Max(maximum,delta);comparisons++;
                if(delta>2)
                {
                    var image=new Texture2D(weatherCheckTarget.width,weatherCheckTarget.height,TextureFormat.RGB24,false);
                    try{image.SetPixels32(sequential);image.Apply();File.WriteAllBytes(Path.Combine(output,"failed-sequential.png"),image.EncodeToPNG());image.SetPixels32(parallel);image.Apply();File.WriteAllBytes(Path.Combine(output,"failed-parallel.png"),image.EncodeToPNG());}finally{Destroy(image);}
                    WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=false;BuildScatterBatches();DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(Path.Combine(output,"failed-reference-repeat.png"));int repeat=0;
                    for(int i=0;i<sequential.Length;i++)repeat=Math.Max(repeat,Math.Max(Math.Abs(sequential[i].r-LastCapturePixels[i].r),Math.Max(Math.Abs(sequential[i].g-LastCapturePixels[i].g),Math.Abs(sequential[i].b-LastCapturePixels[i].b))));
                    throw new InvalidOperationException("Parallel versus sequential identical-camera render changed pixels: "+yaw+" / "+offset+", max RGB "+delta+"; sequential/reference repeat max RGB "+repeat);
                }
            }
            if((burst?BurstScatterBuilds:ParallelScatterBuilds)-start!=comparisons)throw new InvalidOperationException("Renderer comparisons did not exercise requested workers: Burst="+burst);
            return "PASS "+comparisons+" identical-camera sequential/"+(burst?"Burst-compiled":"managed parallel")+" renders with exact model/matrix/tint/fade/caster multisets; fixed exposure, all actors retained; max RGB delta "+maximum;
        }
        finally{WorldGenPerformance.ParallelScatter=oldParallel;WorldGenPerformance.BurstScatter=oldBurst;WorldGenPerformance.DirectionalScatter=oldDirectional;WeatherVideoOptions.Current.Adaptation=oldAdaptation;heading=oldHeading;pitch=oldPitch;view.transform.localPosition=oldEye;view.transform.localRotation=oldRotation;BuildScatterBatches();}
    }
    public string PerformanceVerifyScatterRange()
    {
        bool old=WorldGenPerformance.ScatterRangeCache,oldDirectional=WorldGenPerformance.DirectionalScatter;var eye=view.transform.localPosition;var rotation=view.transform.localRotation;int comparisons=0;
        WorldGenPerformance.DirectionalScatter=true;
        try
        {
            foreach(var offset in new[]{Vector3.zero,new Vector3(15.9f,0,0),new Vector3(-15.9f,0,0),new Vector3(11.2f,0,11.2f),new Vector3(16.1f,0,0),new Vector3(-35,2,-17)})
            for(int yaw=0;yaw<360;yaw+=45)
            {
                view.transform.localPosition=eye+offset;view.transform.localRotation=Quaternion.Euler(3,yaw,0);
                WorldGenPerformance.ScatterRangeCache=false;BuildScatterBatches();var expected=PerformanceInstances();
                WorldGenPerformance.ScatterRangeCache=true;BuildScatterBatches();
                if(!expected.SequenceEqual(PerformanceInstances()))throw new InvalidOperationException("Cached range changed exact instances at "+offset+" / "+yaw);
                comparisons++;
            }
            int retained=0,total=0;foreach(var chunk in scatterChunks.Values)if(chunk.RangeValid){retained+=chunk.RangeGroups.Count;total+=chunk.Groups.Length;}
            return "PASS "+comparisons+" exact cached/full-range instance/caster comparisons across headings, movement-margin boundaries and cache rebuilds; candidate bins "+retained+" / "+total;
        }
        finally{WorldGenPerformance.ScatterRangeCache=old;WorldGenPerformance.DirectionalScatter=oldDirectional;view.transform.localPosition=eye;view.transform.localRotation=rotation;BuildScatterBatches();}
    }
    public string PerformanceVerifyForestUploads()
    {
        var batch=forestBatches.Values.Where(b=>b.Roots!=null&&b.Data.Count>2048&&b.Capacity>=b.Data.Count).OrderByDescending(b=>b.Data.Count).FirstOrDefault();
        if(batch==null)throw new InvalidOperationException("Partial forest upload proof needs a real resident GPU batch above2048 roots");
        bool old=WorldGenPerformance.PartialForestUploads;int[] slots={1,517,batch.Data.Count-2};var saved=slots.Select(i=>batch.Data[i]).ToArray();
        int bytes=System.Runtime.InteropServices.Marshal.SizeOf<ForestRoot>();if(bytes!=64)throw new InvalidOperationException("Unexpected forest GPU record stride");
        void CheckGpu()
        {
            var actual=new ForestRoot[batch.Data.Count];batch.Roots.GetData(actual,0,0,actual.Length);var expected=batch.Data.ToArray();
            if(!System.Runtime.InteropServices.MemoryMarshal.AsBytes(actual.AsSpan()).SequenceEqual(System.Runtime.InteropServices.MemoryMarshal.AsBytes(expected.AsSpan())))throw new InvalidOperationException("Actual forest GPU bytes differ from CPU roots after partial/full upload");
        }
        try
        {
            DirtyAllForestRoots(batch);UploadForestRoots(batch,true);CheckGpu();WorldGenPerformance.PartialForestUploads=true;
            batch.Data[slots[0]]=default;DirtyForestRoot(batch,slots[0]);
            for(int n=1;n<slots.Length;n++){int i=slots[n];var root=batch.Data[i];root.Position.y+=.125f*n;root.Tint.x*=.81f;batch.Data[i]=root;DirtyForestRoot(batch,i);}
            long before=ForestRootUploadBytes;UploadForestRoots(batch);long partial=ForestRootUploadBytes-before;CheckGpu();
            if(partial<=0||partial>slots.Length*ForestUploadBlock*64L||partial>=batch.Data.Count*64L)throw new InvalidOperationException("Partial upload did not reduce actual transfer size: "+partial);
            DirtyAllForestRoots(batch);UploadForestRoots(batch,true);CheckGpu();
            return "PASS actual GPU root bytes equal all "+batch.Data.Count+" CPU records after clear/move/tint in three disjoint blocks and full reupload; partial "+partial+"bytes vs full "+batch.Data.Count*64L+"bytes";
        }
        finally{for(int n=0;n<slots.Length;n++)batch.Data[slots[n]]=saved[n];DirtyAllForestRoots(batch);UploadForestRoots(batch,true);WorldGenPerformance.PartialForestUploads=old;}
    }
    string PerformanceVerifyLiteralVisibility()
    {
        if(WorldGenPerformance.Original)return "Original-only run: equivalence check skipped.";
        for(int yaw=0;yaw<360;yaw+=45)
        {
            PerformanceTurn(yaw);WorldGenPerformance.OriginalCase=true;BuildScatterBatches();var before=PerformanceInstances();
            WorldGenPerformance.OriginalCase=false;BuildScatterBatches();var after=PerformanceInstances();
            if(!before.SequenceEqual(after))throw new InvalidOperationException("Visibility optimisation changed submitted instances at yaw "+yaw+" ("+before.Length+" / "+after.Length+")\nOriginal only: "+string.Join("\n",before.Except(after).Take(2))+"\nGrouped only: "+string.Join("\n",after.Except(before).Take(2)));
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
    public string PerformanceVerifyRotationGuard(string output,bool moving=false)
    {
        Directory.CreateDirectory(output);var oldRotation=view.transform.localRotation;var oldEye=view.transform.localPosition;float oldHeading=heading,oldPitch=pitch;
        bool oldOriginal=WorldGenPerformance.OriginalCase,oldDisabled=WorldGenPerformance.DisableScatterViewGuard,oldGpuOcclusion=WorldGenPerformance.GpuNearOcclusion,oldDirectional=WorldGenPerformance.DirectionalScatter;
        // Prepare() advances adaptation on every manual camera render, even in
        // the same frame. Hold exposure while comparing only admission/culling.
        float oldAdaptation=WeatherVideoOptions.Current.Adaptation;WeatherVideoOptions.Current.Adaptation=0;
        if(moving)WorldGenPerformance.DirectionalScatter=true;
        int comparisons=0,maxDelta=0,maxChanged=0;
        try
        {
            for(int yaw=0;yaw<360;yaw+=45)
            foreach(var offset in moving?new[]{new Vector2(17,0),new Vector2(-17,0),new Vector2(12,12),new Vector2(-12,-12)}:new[]{new Vector2(170,0),new Vector2(-95,0),new Vector2(0,74),new Vector2(45,-74)})
            {
                PerformanceTurn(yaw);var center=view.transform.localRotation;
                WorldGenPerformance.GpuNearOcclusion=oldGpuOcclusion;
                WorldGenPerformance.OriginalCase=false;WorldGenPerformance.DisableScatterViewGuard=false;BuildScatterBatches();
                if(scatterCachedAllHeadings==moving)throw new InvalidOperationException("Wrong moving/stationary scatter verification context");
                var guarded=PerformanceInstances(true).GroupBy(s=>s).ToDictionary(g=>g.Key,g=>g.Count(),StringComparer.Ordinal);
                var guardedCasters=PerformanceInstances(castersOnly:true);
                view.transform.localRotation=center*Quaternion.Euler(offset.y,offset.x,0);
                if(ScatterViewChanged)throw new InvalidOperationException("Within-guard camera rotation unexpectedly invalidated scatter");
                DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(null);var cached=LastCapturePixels;
                WorldGenPerformance.DisableScatterViewGuard=true;WorldGenPerformance.GpuNearOcclusion=false;BuildScatterBatches();
                if(!guardedCasters.SequenceEqual(PerformanceInstances(castersOnly:true)))throw new InvalidOperationException("Rotation guard changed physical model/transform/tint/LOD shadow casters at yaw "+yaw+", offset "+offset);
                foreach(var instance in PerformanceInstances(true))
                {
                    if(!guarded.TryGetValue(instance,out int count)||count==0)throw new InvalidOperationException("Rotation guard omitted a literally visible instance at yaw "+yaw+", offset "+offset);
                    guarded[instance]=count-1;
                }
                DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(null);var literal=LastCapturePixels;
                int delta=0,changed=0;bool edgeNoise=true;
                for(int i=0;i<cached.Length;i++)
                {
                    int d=Math.Max(Math.Abs(cached[i].r-literal[i].r),Math.Max(Math.Abs(cached[i].g-literal[i].g),Math.Abs(cached[i].b-literal[i].b)));
                    delta=Math.Max(delta,d);if(d>2)
                    {
                        changed++;int x=i%weatherCheckTarget.width,y=i/weatherCheckTarget.width;
                        int minR=255,minG=255,minB=255,maxR=0,maxG=0,maxB=0;
                        for(int yy=Math.Max(0,y-1);yy<=Math.Min(weatherCheckTarget.height-1,y+1);yy++)
                        for(int xx=Math.Max(0,x-1);xx<=Math.Min(weatherCheckTarget.width-1,x+1);xx++)
                        {
                            var c=literal[yy*weatherCheckTarget.width+xx];minR=Math.Min(minR,c.r);maxR=Math.Max(maxR,c.r);minG=Math.Min(minG,c.g);maxG=Math.Max(maxG,c.g);minB=Math.Min(minB,c.b);maxB=Math.Max(maxB,c.b);
                        }
                        if(Math.Max(maxR-minR,Math.Max(maxG-minG,maxB-minB))+2<d)edgeNoise=false;
                    }
                }
                maxDelta=Math.Max(maxDelta,delta);maxChanged=Math.Max(maxChanged,changed);comparisons++;
                // Repacking identical opaque/alpha-tested geometry can change
                // isolated depth-tie pixels. Visible membership and the entire
                // physical caster multiset above remain exact requirements.
                if(changed>cached.Length/100000||!edgeNoise)
                {
                    var image=new Texture2D(weatherCheckTarget.width,weatherCheckTarget.height,TextureFormat.RGB24,false);
                    try
                    {
                        image.SetPixels32(cached);image.Apply();File.WriteAllBytes(Path.Combine(output,"failed-guard.png"),image.EncodeToPNG());
                        image.SetPixels32(literal);image.Apply();File.WriteAllBytes(Path.Combine(output,"failed-literal.png"),image.EncodeToPNG());
                    }
                    finally{Destroy(image);}
                    throw new InvalidOperationException("Rotation guard render differs from literal camera: yaw "+yaw+", offset "+offset+", max RGB delta "+delta+", changed pixels "+changed);
                }
                view.transform.localRotation=center;WorldGenPerformance.OriginalCase=false;WorldGenPerformance.DisableScatterViewGuard=false;WorldGenPerformance.GpuNearOcclusion=oldGpuOcclusion;BuildScatterBatches();
                view.transform.localPosition+=Vector3.right*.6f;
                if(!ScatterViewChanged)throw new InvalidOperationException("Eye movement failed to invalidate scatter");
                view.transform.localPosition=oldEye;
            }
            return "PASS "+comparisons+" actual-camera renders at fixed exposure, visible-instance containment and exact physical caster multisets; max RGB delta "+maxDelta+", at most "+maxChanged+" changed edge pixels (budget 0.001%); eye movement invalidates.";
        }
        finally
        {
            WeatherVideoOptions.Current.Adaptation=oldAdaptation;
            heading=oldHeading;pitch=oldPitch;view.transform.localRotation=oldRotation;view.transform.localPosition=oldEye;WorldGenPerformance.OriginalCase=oldOriginal;WorldGenPerformance.DisableScatterViewGuard=oldDisabled;WorldGenPerformance.GpuNearOcclusion=oldGpuOcclusion;WorldGenPerformance.DirectionalScatter=oldDirectional;BuildScatterBatches();
        }
    }
    public string PerformanceVerifyIndexedGround(string output)
    {
        if(!TerrainVideoOptions.GrassClumps||groundClumpMaterial==null||groundClumpIndices==null)throw new InvalidOperationException("Indexed grass verification needs the real clump renderer");
        var oldRotation=view.transform.localRotation;bool old=WorldGenPerformance.NonIndexedClumps;int comparisons=0,maxDelta=0,maxChanged=0;
        try
        {
            for(int yaw=0;yaw<360;yaw+=30)foreach(int tilt in new[]{-25,0,25})
            {
                view.transform.localRotation=Quaternion.Euler(tilt,yaw,0);
                WorldGenPerformance.NonIndexedClumps=true;DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(null);var literal=LastCapturePixels;
                WorldGenPerformance.NonIndexedClumps=false;DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(null);var indexed=LastCapturePixels;
                int changed=0,delta=0;
                for(int i=0;i<literal.Length;i++)
                {
                    int d=Math.Max(Math.Abs(literal[i].r-indexed[i].r),Math.Max(Math.Abs(literal[i].g-indexed[i].g),Math.Abs(literal[i].b-indexed[i].b)));
                    delta=Math.Max(delta,d);if(d>2)changed++;
                }
                maxDelta=Math.Max(maxDelta,delta);maxChanged=Math.Max(maxChanged,changed);comparisons++;
                if(changed>literal.Length/100000)
                {
                    var image=new Texture2D(weatherCheckTarget.width,weatherCheckTarget.height,TextureFormat.RGB24,false);
                    try
                    {
                        image.SetPixels32(literal);image.Apply();File.WriteAllBytes(Path.Combine(output,"failed-grass-nonindexed.png"),image.EncodeToPNG());
                        image.SetPixels32(indexed);image.Apply();File.WriteAllBytes(Path.Combine(output,"failed-grass-indexed.png"),image.EncodeToPNG());
                    }
                    finally{Destroy(image);}
                    throw new InvalidOperationException("Indexed grass render differs at yaw "+yaw+", tilt "+tilt+": "+changed+" changed pixels; maximum RGB delta "+delta);
                }
            }
            return "PASS "+comparisons+" actual indexed/nonindexed grass image comparisons; max RGB delta "+maxDelta+", changed pixels "+maxChanged+" (budget 0.001%); identical shader, roots, density, triangles and literal vertex IDs.";
        }
        finally{WorldGenPerformance.NonIndexedClumps=old;view.transform.localRotation=oldRotation;}
    }
    public void PerformancePose(double height=1.65)
    {
        bool changed=motion.X!=0||motion.Z!=0||Math.Abs(HeightAboveGround-height)>.01;
        motion.X=motion.Z=0;motion.Y=Ground(0,0)+height;motion.Flying=true;heading=20;pitch=height>10?12:3;
        if(changed)RefreshWanted();UpdateCamera();
    }
    public void PerformanceWalkingQueryMode()
    {
        // Recreate both comparison cases, so an indexed first case cannot
        // silently leave its index enabled for the following baseline.
        walkingPatch=terrain.Patch(frame.Position(motion.X,motion.Z),2048/HorizontalScale);groundMemoFrame=null;
        if(WorldGenPerformance.IndexedWalkingRivers)walkingPatch.RiverSpans();
    }
    public void PerformanceTurn(float yaw){heading=yaw;UpdateCamera();}
    public string PerformanceVerifyAnimalPoseMath()
    {
        int compared=0,models=0;
        foreach(var model in animalModels.Values)
        {
            var asset=model.Asset;models++;
            foreach(var clip in asset.Clips.Take(4))foreach(double time in new[]{0d,.017,.37,1.001,42.17})
            {
                double f=(time/clip.duration%1)*clip.frames;int first=(int)f%clip.frames,next=(first+1)%clip.frames;float blend=(float)(f-Math.Floor(f));
                var frame=AnimalPoseFrame(asset,clip,time);
                for(int bone=0;bone<asset.BoneCount;bone++)
                {
                    var a=asset.Poses[(clip.start+first)*asset.BoneCount+bone];var b=asset.Poses[(clip.start+next)*asset.BoneCount+bone];
                    var actual=AnimalFramePose(asset,frame,bone);
                    for(int i=0;i<16;i++)
                    {float expected=a[i]+(b[i]-a[i])*blend;if(BitConverter.SingleToInt32Bits(expected)!=BitConverter.SingleToInt32Bits(actual[i]))throw new InvalidOperationException("Animal pose math changed: "+asset.Slug+" / "+bone);compared++;}
                }
            }
        }
        if(models==0)throw new InvalidOperationException("No actual animal motions loaded for pose comparison");
        return "PASS "+compared+" bit-exact authored bone matrix components across "+models+" loaded species; unchanged clips, sample times and blend math";
    }
    public void PerformanceTravel(double east){motion.X=east;motion.Y=Ground(motion.X,motion.Z)+1.65;UpdateCamera();}
    public void PerformanceRender(int width,int height)
    {
        if(WorldGenPerformance.Presented)
        {
            // Real player loop/presentation for throughput evidence. Pixel
            // verification continues to use the separate offscreen path.
            Screen.SetResolution(width,height,WorldGenPerformance.PresentationMode);view.targetTexture=null;view.enabled=true;return;
        }
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
            LastCapturePixels=colors;if(path!=null)File.WriteAllBytes(path,pixels.EncodeToPNG());
        }
        finally{RenderTexture.active=prior;UnityEngine.Object.Destroy(pixels);}
    }
    public bool PerformanceGroundSettled=>Ready&&NearReady&&AllReady&&!DetailPlanning&&jobs.Count==0&&previousTerrain.Count==0&&retiringFrames.Count==0&&tiles.Values.All(t=>FadeComplete(t));
    public bool PerformanceSettled=>!GroundPublicationPending&&PerformanceGroundSettled&&ScatterReady&&scatterJobs.Count==0&&coverRequests.Count==0&&(!TerrainVideoOptions.GeneratedTrees||!TerrainVideoOptions.FarForest||!TerrainVideoOptions.TreeRebuild||!TerrainVideoOptions.TreeImpostors||!ForestPending)&&(!TerrainVideoOptions.GeneratedTrees||!TerrainVideoOptions.TreeImpostors||PendingTreeModels==0);
    public string PerformanceForestState=>$"fields {ForestFieldCount}/{forestPriority.Length}, roots {ForestPacketCount}, pending {ForestPending}, baked local {BakedTreeModels}, baked forest {BakedForestModels}, pending models {PendingTreeModels}, texture bytes {TreeBakeBytes}; shadow proxies {UseTreeShadowProxies}, soft {SoftCanopyShadows}, casters {TreeShadowObjects}, coarse {TreeShadowCoarse}, shadow draws {TreeShadowDraws}, triangles {TreeShadowTriangles}; readiness near={NearReady} all={AllReady} detail={DetailPlanning} jobs={jobs.Count} previous={previousTerrain.Count} retiring={retiringFrames.Count} fades={tiles.Values.Count(t=>!FadeComplete(t))} scatter={ScatterReady} scatterJobs={scatterJobs.Count} coverRequests={coverRequests.Count}";
    public void PerformanceInvalidate(){scatterDirty=true;}
    public void PerformanceShadows(bool enabled){if(localSun!=null)localSun.shadows=enabled?LightShadows.Soft:LightShadows.None;}
    public void PerformanceFreezeDaylight(bool summer=false){TerrainDiagnostics.FreezeSun=false;sunPoseWritten=false;SetDaylight(12,summer?190000+(Position.Z<0?0:.5):skyYear);TerrainDiagnostics.FreezeSun=true;}
    public string PerformanceReviewGroundSeams(string output,string site)
    {
        var rows=new List<string>{"x,z,level,segments,axis,ground_delta,cover_delta,mid_delta,forest_delta"};int pairs=0,bindingMismatch=0;double coverMaximum=0,forestMaximum=0;
        var floorRows=new List<string>{"x,z,level,distance_m,samples,mean_stem_occupancy,mean_crown_width_m,mean_floor_alpha,max_floor_alpha"};
        foreach(var pair in tiles)
        {
            var key=pair.Key;var a=pair.Value;if(a.Root==null||!a.Root.activeInHierarchy||TerrainLod.DistanceSquared(key,motion.X,motion.Z)>512*512)continue;
            if(a.Heights==null||a.Cover.Length!=a.Heights.Length*16||a.MidCover.Length!=a.Heights.Length*6||a.ForestCover.Length!=a.Heights.Length*8)continue;
            double occupancy=0,widthSum=0,alphaSum=0,alphaMax=0;
            for(int sample=0;sample<a.Heights.Length;sample++)
            {
                int index=sample*8,code=BitConverter.SingleToInt32Bits(a.ForestCover[index+7]);var strategy=(TreeCrownStrategy)((code>>22)&3);
                double width=a.ForestCover[index+5],stock=a.ForestCover[index+3],open=a.ForestCover[index+6];
                double alpha=1-Math.Exp(-TreeFlora.PlacementPerM2*Math.PI*.25*width*width*(strategy==TreeCrownStrategy.TropicalCanopy?.31:.64)*stock*(1-.55*Math.Clamp(open,0,1)));
                occupancy+=stock;widthSum+=width;alphaSum+=alpha;alphaMax=Math.Max(alphaMax,alpha);
            }
            floorRows.Add(FormattableString.Invariant($"{key.X},{key.Z},{key.Level},{Math.Sqrt(TerrainLod.DistanceSquared(key,motion.X,motion.Z))},{a.Heights.Length},{occupancy/a.Heights.Length},{widthSum/a.Heights.Length},{alphaSum/a.Heights.Length},{alphaMax}"));
            if(a.Materials!=null)foreach(var material in a.Materials)
                if(material.shader.name=="WorldGen/LocalTerrain"&&(material.GetTexture("_GrassField")!=grassFieldTexture||material.GetTexture("_ForestFloor")!=a.ForestFloor||material.GetFloat("_ForestFloorEnabled")!=(a.ForestFloor==null?0:1)||material.GetTexture("_FloraSurface")!=a.FloraSurface))bindingMismatch++;
            for(int axis=0;axis<2;axis++)
            {
                var next=new TerrainTileKey(key.X+(axis==0?1:0),key.Z+(axis==1?1:0),key.Level,key.Scale);
                if(!tiles.TryGetValue(next,out var b)||b.Segments!=a.Segments||b.Root==null||!b.Root.activeInHierarchy)continue;
                if(b.Heights==null||b.Cover.Length!=b.Heights.Length*16||b.MidCover.Length!=b.Heights.Length*6||b.ForestCover.Length!=b.Heights.Length*8)continue;
                int n=a.Segments,side=n+1;double h=0,c=0,m=0,f=0;
                for(int i=0;i<=n;i++)
                {
                    int ia=axis==0?i*side+n:n*side+i,ib=axis==0?i*side:i;
                    h=Math.Max(h,Math.Abs(a.Heights[ia]-b.Heights[ib]));
                    for(int channel=0;channel<13;channel++)c=Math.Max(c,Math.Abs(a.Cover[ia*16+channel]-b.Cover[ib*16+channel]));
                    for(int channel=0;channel<6;channel++)m=Math.Max(m,Math.Abs(a.MidCover[ia*6+channel]-b.MidCover[ib*6+channel]));
                    for(int channel=0;channel<7;channel++)f=Math.Max(f,Math.Abs(a.ForestCover[ia*8+channel]-b.ForestCover[ib*8+channel]));
                }
                rows.Add(string.Join(",",key.X,key.Z,key.Level,n,axis,h.ToString("R",CultureInfo.InvariantCulture),c.ToString("R",CultureInfo.InvariantCulture),m.ToString("R",CultureInfo.InvariantCulture),f.ToString("R",CultureInfo.InvariantCulture)));pairs++;coverMaximum=Math.Max(coverMaximum,c);forestMaximum=Math.Max(forestMaximum,f);
            }
        }
        File.WriteAllLines(Path.Combine(output,site+"-ground-edges.csv"),rows);
        File.WriteAllLines(Path.Combine(output,site+"-forest-floor.csv"),floorRows);
        var oldFlags=TerrainDiagnostics.Flags;var rotation=view.transform.localRotation;float adaptation=WeatherVideoOptions.Current.Adaptation;
        try
        {
            WeatherVideoOptions.Current.Adaptation=0;view.transform.localRotation=Quaternion.Euler(12,110,0);
            foreach(var flags in new[]{(TerrainDiagnosticFlags)0,TerrainDiagnosticFlags.FlatColour|TerrainDiagnosticFlags.Unlit,TerrainDiagnosticFlags.NoForestTint,TerrainDiagnosticFlags.NoGrassMat,TerrainDiagnosticFlags.NoMoss,TerrainDiagnosticFlags.NoLichen,TerrainDiagnosticFlags.Unlit})
            {
                TerrainDiagnostics.Flags=flags;TerrainDiagnostics.Changed();ApplyTerrainDiagnostics();DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(Path.Combine(output,site+"-ground-flags-"+(int)flags+".png"));
            }
        }
        finally{TerrainDiagnostics.Flags=oldFlags;TerrainDiagnostics.Changed();ApplyTerrainDiagnostics();WeatherVideoOptions.Current.Adaptation=adaptation;view.transform.localRotation=rotation;}
        return site+": surface diagnostic "+pairs+" equal-grid adjacent pairs, max cover delta "+coverMaximum+", forest delta "+forestMaximum+", material binding mismatches "+bindingMismatch+"; actual component-isolation renders recorded";
    }
    public IEnumerator PerformanceReviewGrassApproach(string output,string site)
    {
        string table=Path.Combine(output,site+"-grass-approach.csv");File.WriteAllText(table,"east_m,near_pages,lod0_instances,lod1_instances\n");
        try
        {
            for(int step=0;step<=6;step++)
            {
                PerformanceTravel(step*8);
                // Let the literal production discovery/install path publish its
                // near-first pages while walking across the resident grid.
                for(int frame=0;frame<20;frame++)yield return null;
                DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(Path.Combine(output,site+"-grass-approach-"+(step*8)+".png"));
                long a=0,b=0;int pages=0;
                foreach(var tile in tiles.Values)foreach(var page in tile.CoverPages)
                {
                    if(page.Key.Far)continue;pages++;var counts=ReadCoverDrawCounts(page.Value);a+=counts[0];b+=counts[1];
                }
                File.AppendAllText(table,(step*8)+","+pages+","+a+","+b+"\n");
            }
        }
        finally{PerformancePose();}
    }
    public string PerformanceReviewTreeDepth(string output,string site)
    {
        float prior=Shader.GetGlobalFloat("_WorldGenTreeStableDepth"),adaptation=WeatherVideoOptions.Current.Adaptation;bool priorFlat=Shader.IsKeywordEnabled("WORLDGEN_FLAT_TREE_DEPTH");var rotation=view.transform.localRotation;var eye=view.transform.localPosition;
        try
        {
            WeatherVideoOptions.Current.Adaptation=0;
            for(int pose=0;pose<3;pose++)
            {
                view.transform.localPosition=eye+(pose==2?Vector3.up*60:Vector3.zero);
                view.transform.localRotation=Quaternion.Euler(pose==2?35:3,pose==1?200:20,0);BuildScatterBatches();
                Shader.DisableKeyword("WORLDGEN_FLAT_TREE_DEPTH");Shader.SetGlobalFloat("_WorldGenTreeStableDepth",0);DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(Path.Combine(output,site+"-tree-depth-original-"+pose+".png"));
                Shader.SetGlobalFloat("_WorldGenTreeStableDepth",1);DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(Path.Combine(output,site+"-tree-depth-bounded-"+pose+".png"));
                Shader.EnableKeyword("WORLDGEN_FLAT_TREE_DEPTH");DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(Path.Combine(output,site+"-tree-depth-flat-"+pose+".png"));
            }
        }
        finally{Shader.SetGlobalFloat("_WorldGenTreeStableDepth",prior);if(priorFlat)Shader.EnableKeyword("WORLDGEN_FLAT_TREE_DEPTH");else Shader.DisableKeyword("WORLDGEN_FLAT_TREE_DEPTH");WeatherVideoOptions.Current.Adaptation=adaptation;view.transform.localRotation=rotation;view.transform.localPosition=eye;BuildScatterBatches();}
        return site+": recorded original/bounded/ordinary multi-view impostors at three literal camera poses and fixed exposure; requires visual inspection";
    }
    public string PerformanceReviewPlantLighting(string output,string site)
    {
        float prior=Shader.GetGlobalFloat("_WorldGenFullPlantPbr"),adaptation=WeatherVideoOptions.Current.Adaptation;var rotation=view.transform.localRotation;
        try
        {
            WeatherVideoOptions.Current.Adaptation=0;
            foreach(int yaw in new[]{20,200})
            {
                view.transform.localRotation=Quaternion.Euler(3,yaw,0);BuildScatterBatches();
                Shader.SetGlobalFloat("_WorldGenFullPlantPbr",1);DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(Path.Combine(output,site+"-plant-pbr-"+yaw+".png"));
                Shader.SetGlobalFloat("_WorldGenFullPlantPbr",0);DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(Path.Combine(output,site+"-plant-diffuse-"+yaw+".png"));
            }
        }
        finally{Shader.SetGlobalFloat("_WorldGenFullPlantPbr",prior);WeatherVideoOptions.Current.Adaptation=adaptation;view.transform.localRotation=rotation;BuildScatterBatches();}
        return site+": recorded full-PBR/diffuse small-plant lighting at two literal camera poses and fixed exposure; requires visual inspection, not pixel equality";
    }
    public string PerformanceReviewVegetation(string output,string site)
    {
        if(!SoftCanopyShadows)throw new InvalidOperationException("Cached canopy shade is not ready; reject the foliage review");
        var position=view.transform.localPosition;var rotation=view.transform.localRotation;
        try
        {
            foreach(int yaw in new[]{20,110,200,290})
            {
                view.transform.localRotation=Quaternion.Euler(3,yaw,0);BuildScatterBatches();DrawScatter();DrawGroundCover();view.Render();
                PerformanceCapture(Path.Combine(output,site+"-ground-"+yaw+".png"));
            }
            view.transform.localPosition=position+Vector3.up*60;view.transform.localRotation=Quaternion.Euler(35,20,0);BuildScatterBatches();DrawScatter();DrawGroundCover();view.Render();
            PerformanceCapture(Path.Combine(output,site+"-overview.png"));
        }
        finally{view.transform.localPosition=position;view.transform.localRotation=rotation;BuildScatterBatches();}
        // Real compute-kernel check: interpolated cover must not put blades on
        // a cliff triangle even when all four input vertices claim full grass.
        var placement=Resources.Load<ComputeShader>("GroundCoverPlacement");
        using(var grid=new GraphicsBuffer(GraphicsBuffer.Target.Structured,64,4))
        using(var roots=new GraphicsBuffer(GraphicsBuffer.Target.Append,64,96))
        using(var count=new GraphicsBuffer(GraphicsBuffer.Target.Raw,1,4))
        {
            foreach(int fixture in new[]{0,1,2,3,4})
            {
                float grade=fixture==1?2:0;int weight=fixture==2?9:fixture==3?12:4;
                var data=new float[64];for(int i=0;i<4;i++){int at=i*16;data[at+3]=data[at+weight]=.8f;data[at+13]=fixture==4?0:1;data[at+14]=1;data[at+15]=(i%2)*8*grade;}
                grid.SetData(data);roots.SetCounterValue(0);placement.SetBuffer(0,"_Grid",grid);placement.SetBuffer(0,"_Roots",roots);
                placement.SetInts("_Candidate",0,0,8,8);placement.SetVector("_Tile",new Vector4(0,0,8,1));placement.SetFloat("_Spacing",.6f);placement.SetInt("_Seed",731);placement.Dispatch(0,1,1,1);
                GraphicsBuffer.CopyCount(roots,count,0);var result=new uint[1];count.GetData(result);
                if(fixture==0&&result[0]!=64||fixture!=0&&result[0]!=0)throw new InvalidOperationException("Grass habitat gate failed: fixture "+fixture+", roots "+result[0]);
            }
        }
        return site+": GPU habitat gate passed (64 grass / 0 cliff / 0 duplicate fern / 0 succulent ribbons / 0 wet roots); cached canopy ready; solid-trunk casters "+TreeShadowCoarse+" / "+TreeShadowObjects+", alpha tree cards "+TreeShadowCards;
    }
    public string PerformanceVerifyTreeEfficiency(string output)
    {
        if(!residentForestReady)throw new System.InvalidOperationException("Nearby exact GPU pages are not ready; reject a vacuous resident-tree comparison");
        bool oldResident=WorldGenPerformance.LegacyTreeResident,oldShader=WorldGenPerformance.LegacyTreeShader,oldWind=WorldGenPerformance.LegacyTreeWind;
        string report="";
        try
        {
            foreach(string part in new[]{"shader","wind","resident"})
            {
                void Mode(bool legacy){WorldGenPerformance.LegacyTreeShader=part=="shader"&&legacy;WorldGenPerformance.LegacyTreeWind=part=="wind"&&legacy;WorldGenPerformance.LegacyTreeResident=part=="resident"&&legacy;BuildScatterBatches();}
                Mode(true);DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(System.IO.Path.Combine(output,"trees-"+part+"-legacy.png"));var before=LastCapturePixels;int oldCpu=GeneratedTreeObjects;
                Mode(false);DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(System.IO.Path.Combine(output,"trees-"+part+"-efficient.png"));var after=LastCapturePixels;
                if(part=="wind"&&(treeWindRoots.Count==0||treeWindFrame!=Time.frameCount))throw new System.InvalidOperationException("Shared tree wind did not execute for this comparison");
                int changed=0,maximum=0;long total=0;
                for(int i=0;i<before.Length;i++)
                {
                    int d=System.Math.Max(System.Math.Abs(before[i].r-after[i].r),System.Math.Max(System.Math.Abs(before[i].g-after[i].g),System.Math.Abs(before[i].b-after[i].b)));
                    if(d>2)changed++;maximum=System.Math.Max(maximum,d);total+=d;
                }
                report+=part+": pixels>2="+changed+"/"+before.Length+", max="+maximum+", mean="+(total/(double)before.Length).ToString("F5")+", CPU trees="+oldCpu+"->"+GeneratedTreeObjects+", handoff="+ForestHandoffReach.ToString("F0")+", cached wind roots="+treeWindRoots.Count+"\n";
                if(changed>before.Length*.005)throw new System.InvalidOperationException("Tree "+part+" changed too many visible pixels: "+report);
            }
        }
        finally{WorldGenPerformance.LegacyTreeResident=oldResident;WorldGenPerformance.LegacyTreeShader=oldShader;WorldGenPerformance.LegacyTreeWind=oldWind;BuildScatterBatches();}
        return report;
    }
    public string PerformanceVerifyFoliageDepth(string output)
    {
        bool prior=TerrainVideoOptions.FoliageDepthPrepass;
        TerrainVideoOptions.FoliageDepthPrepass=false;DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(Path.Combine(output,"overlap-off.png"));var before=LastCapturePixels;
        TerrainVideoOptions.FoliageDepthPrepass=true;DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(Path.Combine(output,"overlap-on.png"));var after=LastCapturePixels;
        int delta=0,changed=0;for(int i=0;i<before.Length;i++){int d=Math.Max(Math.Abs(before[i].r-after[i].r),Math.Max(Math.Abs(before[i].g-after[i].g),Math.Abs(before[i].b-after[i].b)));delta=Math.Max(delta,d);if(d>1)changed++;}
        TerrainVideoOptions.FoliageDepthPrepass=prior;
        if(changed>before.Length*.001)throw new InvalidOperationException("Tree depth prepass changed visible surfaces: "+changed+" pixels, max delta "+delta);
        return "Tree silhouette prepass on/off: "+changed+" pixels >1 RGB difference; max delta "+delta;
    }
    public string PerformanceVerifySelectiveProjection()
    {
        int ownerChecks=0;bool oldFresh=WorldGenPerformance.ForceFreshVisibleGround;
        try
        {
            void Compare(double x,double z)
            {
                WorldGenPerformance.ForceFreshVisibleGround=true;double literal=VisibleGround(x,z,-12345,out int a);
                WorldGenPerformance.ForceFreshVisibleGround=false;double cached=VisibleGround(x,z,-12345,out int b);
                if(a!=b||BitConverter.DoubleToInt64Bits(literal)!=BitConverter.DoubleToInt64Bits(cached))throw new InvalidOperationException("Resident ground owner cache changed a triangle or exact height");
                ownerChecks++;
            }
            foreach(var key in tiles.Keys)foreach(double x in new[]{0d,.31,.999999,1})foreach(double z in new[]{0d,.57,.999999,1})
                Compare((key.X+x)*key.SizeM,(key.Z+z)*key.SizeM);
            foreach(var chunk in scatterChunks.Values)foreach(var root in chunk.Roots.Take(8))Compare(root.x+originX,root.z+originZ);
            var resident=tiles.First(p=>p.Key.Level==0&&p.Value.Heights!=null);var k=resident.Key;
            double cx=(k.X+.37)*k.SizeM,cz=(k.Z+.61)*k.SizeM;
            WorldGenPerformance.ForceFreshVisibleGround=false;double before=VisibleGround(cx,cz,-12345);
            try
            {
                var raised=(float[])resident.Value.Heights.Clone();for(int i=0;i<raised.Length;i++)raised[i]+=.25f;
                tiles[k]=new Tile{Heights=raised,Segments=resident.Value.Segments};residentRevision++;
                Compare(cx,cz);
                if(Math.Abs(VisibleGround(cx,cz,-12345)-before)<.20)throw new InvalidOperationException("Resident owner cache retained a replaced same-address surface");
                tiles.Remove(k);residentRevision++;Compare(cx,cz);
            }
            finally{tiles[k]=resident.Value;residentRevision++;}
            Compare(cx,cz);
            if(BitConverter.DoubleToInt64Bits(before)!=BitConverter.DoubleToInt64Bits(VisibleGround(cx,cz,-12345)))throw new InvalidOperationException("Resident owner cache failed to restore the current surface");
        }
        finally{WorldGenPerformance.ForceFreshVisibleGround=oldFresh;}
        var prior=scatterGroundChanges.ToArray();int checkedRoots=0,repairedRoots=0,checkedChunks=0,coarseChecks=0,partialChecks=0,partialRoots=0;
        try
        {
            foreach(var tile in tiles.Keys)
            {
                if(tile.Level!=0)continue;
                scatterGroundChanges.Clear();scatterGroundChanges.Add(tile);
                foreach(var chunk in scatterChunks.Values)
                {
                    if(chunk.Groups==null||!chunk.Source.Instances.Any(p=>p.Kind==ScatterKind.GeneratedTree))continue;
                    bool affected=false;foreach(var root in chunk.Roots)if(ChangedGroundAt(root.x+originX,root.z+originZ,.01)){affected=true;break;}
                    if(!affected)continue;
                    ProjectScatter(chunk);
                    for(int i=0;i<chunk.Roots.Length;i++)
                    {
                        var root=chunk.Roots[i];if(!ChangedGroundAt(root.x+originX,root.z+originZ,.01))continue;
                        root.y+=.25f;chunk.Roots[i]=root;var matrix=chunk.Matrices[i];matrix.m13=root.y;chunk.Matrices[i]=matrix;
                        var bounds=chunk.Bounds[i];bounds.center+=Vector3.up*.25f;chunk.Bounds[i]=bounds;repairedRoots++;
                    }
                    ProjectScatterGround(chunk);var selective=(Matrix4x4[])chunk.Matrices.Clone();ProjectScatter(chunk);
                    for(int i=0;i<selective.Length;i++)
                    {
                        if(selective[i]!=chunk.Matrices[i])throw new InvalidOperationException("Selective ground projection differs from full projection at root "+i);
                        checkedRoots++;
                    }
                    scatterGroundChanges.Clear();
                    foreach(var root in chunk.Roots)
                    {
                        double size=TerrainLod.BaseSizeM*HorizontalScale*(1<<16);
                        scatterGroundChanges.Add(new TerrainTileKey((int)Math.Floor((root.x+originX)/size),(int)Math.Floor((root.z+originZ)/size),16,HorizontalScale));
                    }
                    for(int i=0;i<chunk.Roots.Length;i++)if(chunk.GroundLevels[i]<16)
                    {
                        var root=chunk.Roots[i];if(ChangedGroundAt(root.x+originX,root.z+originZ,.01,chunk.GroundLevels[i]))throw new InvalidOperationException("Coarse replacement invalidated a finer ground owner");coarseChecks++;
                    }
                    // A real same-address mesh replacement often changes only
                    // boundary heights. Compare selective projection with a
                    // complete projection against those actual resident grids.
                    var surface=tiles[tile];var originalHeights=surface.Heights;var edgeHeights=(float[])originalHeights.Clone();int segments=surface.Segments,side=segments+1;
                    for(int iz=0;iz<=segments;iz++)for(int ix=0;ix<=segments;ix++)if(ix==0||iz==0||ix==segments||iz==segments)edgeHeights[iz*side+ix]+=.25f;
                    try
                    {
                        scatterGroundChanges.Clear();scatterGroundChanges.Add(tile,surface,new Tile{Heights=edgeHeights,Segments=segments});
                        surface.Heights=edgeHeights;
                        foreach(var root in chunk.Roots)if(ChangedGroundAt(root.x+originX,root.z+originZ,.01))partialRoots++;
                        ProjectScatterGround(chunk);var partial=(Matrix4x4[])chunk.Matrices.Clone();ProjectScatter(chunk);
                        for(int i=0;i<partial.Length;i++)
                        {
                            if(partial[i]!=chunk.Matrices[i])throw new InvalidOperationException("Changed-cell projection differs from full resident mesh replacement at root "+i);
                            partialChecks++;
                        }
                    }
                    finally{surface.Heights=originalHeights;ProjectScatter(chunk);}
                    scatterGroundChanges.Clear();scatterGroundChanges.Add(tile);
                    if(++checkedChunks>=4)break;
                }
                if(checkedChunks>=4)break;
            }
        }
        finally{scatterGroundChanges.Clear();foreach(var tile in prior)scatterGroundChanges.Add(tile);scatterDirty=true;}
        if(repairedRoots==0)throw new InvalidOperationException("Selective projection fixture did not touch actual roots");
        if(partialRoots==0||partialRoots>=partialChecks)throw new InvalidOperationException("Changed-cell projection did not exercise both changed and unchanged actual roots");
        return "Selective/full ground projection: "+checkedRoots+" exact matrices, "+repairedRoots+" displaced roots repaired across "+checkedChunks+" chunks; "+coarseChecks+" coarse-parent ownership guards; "+ownerChecks+" exact cached/fresh resident triangle queries including tile edges; partial mesh replacement "+partialChecks+" exact matrices with only "+partialRoots+" affected roots";
    }
    public string PerformanceVerifyTreeBands(string output)
    {
        bool prior=TerrainVideoOptions.FrontToBackTrees;
        TerrainVideoOptions.FrontToBackTrees=false;BuildScatterBatches();int count=ScatterObjects;DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(Path.Combine(output,"tree-bands-off.png"));var before=LastCapturePixels;
        TerrainVideoOptions.FrontToBackTrees=true;BuildScatterBatches();if(ScatterObjects!=count)throw new InvalidOperationException("Tree distance bands changed admitted instance count");DrawScatter();DrawGroundCover();view.Render();PerformanceCapture(Path.Combine(output,"tree-bands-on.png"));var after=LastCapturePixels;
        int changed=0,delta=0;for(int i=0;i<before.Length;i++){int d=Math.Max(Math.Abs(before[i].r-after[i].r),Math.Max(Math.Abs(before[i].g-after[i].g),Math.Abs(before[i].b-after[i].b)));if(d>1)changed++;delta=Math.Max(delta,d);}
        TerrainVideoOptions.FrontToBackTrees=prior;BuildScatterBatches();
        if(changed>before.Length*.001)throw new InvalidOperationException("Tree distance batches changed visible surfaces: "+changed+" pixels");
        return "Tree distance batches: same "+count+" admitted objects; "+changed+" pixels >1 RGB difference, max delta "+delta;
    }
    public string PerformanceCounts=>string.Join(",",TileCount,WantedCount,ScatterObjects,SpecialistObjects,MidPlantObjects,GeneratedTreeObjects,ScatterDrawCalls,GroundCoverDraws,GroundCoverCandidates,GroundCoverBytes,ScatterFrustumRejected,ScatterTerrainRejected);
    public string PerformanceVerifyTrunkBudget()
    {
        if(!SoftCanopyShadows)return "Soft canopy inactive; trunk budget does not replace the detailed fallback.";
        foreach(var batch in drawBatches)
            if((ScatterKind)((batch.Key>>2)&63)==ScatterKind.GeneratedTree&&batch.Shadows!=UnityEngine.Rendering.ShadowCastingMode.Off)
                throw new InvalidOperationException("Soft canopy admitted a full foliage shadow caster");
        float range=TerrainVideoOptions.ForestTrunkShadowDistance;
        foreach(var batch in treeShadowBatches)for(int i=0;i<batch.Count;i++)
        {
            var matrix=batch.Matrices[i];var root=new Vector3(matrix.m03,matrix.m13,matrix.m23);
            if(Vector3.Distance(root,scatterEye)>range+.01f)throw new InvalidOperationException("Trunk shadow caster escaped its close-range budget");
        }
        return "PASS all tree crowns use shared canopy shelter; "+TreeShadowObjects+" opaque trunks inside "+range+"m; no full-foliage fallback casters";
    }
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
    struct StutterRow
    {
        public double Time,Frame,Update,Map,Scatter,Weather,Ground,Fog,Coverage,Sky,Batches,Bakes,Projection,Forest,Shadows,CoverDraw,Canopy,ForestDraw,Survival,Prepared,Humans,Player,Fauna,AnimalMove,AnimalPose,BatchCollect,BatchUpload,TerrainOcclusion,FrustumTest,InstanceAppend,ScatterClassify,ScatterAppend,CameraRender,ForestGround,GroundPublication,Inventory,Planning,Install,Retirement,Scheduling,Readout,Solids,PlayerCamera,PlayerWater,PlayerCombat,PlayerBlood,PlayerFootsteps,PlayerFeedback,PlayerHud,PlayerAltitude,QueryLocate,QueryProfiles,QueryShape,QueryRivers,QueryDetail,QueryPonds,Driver;public int Gc,HeapIntervalsWithCollection;public long Heap,MainAllocated,FrameAllocated,HeapUpdate,HeapScatter,HeapHumans,HeapPlayer,HeapFauna,HeapCamera;
        public override string ToString()=>string.Join(",",Perf(Time),Perf(Frame),Gc,Heap,MainAllocated,Perf(Update),Perf(Map),Perf(Scatter),Perf(Weather),Perf(Ground),Perf(Fog),Perf(Coverage),Perf(Sky),Perf(Batches),Perf(Bakes),Perf(Projection),Perf(Forest),Perf(Shadows),Perf(CoverDraw),Perf(Canopy),Perf(ForestDraw),Perf(Survival),Perf(Prepared),Perf(Humans),Perf(Player),Perf(Fauna),Perf(AnimalMove),Perf(AnimalPose),Perf(BatchCollect),Perf(BatchUpload),Perf(TerrainOcclusion),Perf(FrustumTest),Perf(InstanceAppend),FrameAllocated,Perf(ScatterClassify),Perf(ScatterAppend),Perf(CameraRender),HeapUpdate,HeapScatter,HeapHumans,HeapPlayer,HeapFauna,HeapCamera,HeapIntervalsWithCollection,Perf(ForestGround),Perf(GroundPublication),Perf(Inventory),Perf(Planning),Perf(Install),Perf(Retirement),Perf(Scheduling),Perf(Readout),Perf(Solids),Perf(PlayerCamera),Perf(PlayerWater),Perf(PlayerCombat),Perf(PlayerBlood),Perf(PlayerFootsteps),Perf(PlayerFeedback),Perf(PlayerHud),Perf(PlayerAltitude),Perf(QueryLocate),Perf(QueryProfiles),Perf(QueryShape),Perf(QueryRivers),Perf(QueryDetail),Perf(QueryPonds),Perf(Driver));
    }
    IEnumerator CheckPerformance()
    {
        TerrainQueryTiming.Enabled=WorldGenPerformance.TraceEnabled;
        ulong originalGcSlice=UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds;
        Application.logMessageReceived+=(message,trace,kind)=>{if(kind==LogType.Exception||kind==LogType.Error)Application.Quit(1);};
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);
        // Numerical smoke check needs no terrain/forest warmup. This uses the
        // actual packaged Burst library and rejects managed fallback execution.
        if(Argument("--worldgen-perf-cases","")=="burst-math-verify")
        {
            File.WriteAllText(Path.Combine(output,"equivalence.txt"),LocalTerrainExplorer.PerformanceVerifyBurstDistances()+"\n");
            File.WriteAllText(Path.Combine(output,"complete.txt"),"Numerical correctness only; no throughput measurement.\n");
            Application.Quit(0);yield break;
        }
        double deadline=Time.realtimeSinceStartupAsDouble+300;
        while((!CanExploreTerrain||humanStep!=null||humanPaletteJob!=null||humanPaletteReady!=null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(!CanExploreTerrain){Debug.LogError("PERF world unavailable "+error);Application.Quit(1);yield break;}
        humanPlaying=false;play.HasPlayer=false;play.Settings.HorizontalScale=.1;
        int width=int.Parse(Argument("--worldgen-perf-width","1920")),height=int.Parse(Argument("--worldgen-perf-height","1080"));
        var cells=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>world.ElevationM[i]>30&&world.ElevationM[i]<3200).ToArray();
        int Pick(Func<HabitatCell,bool> where,bool steep=false)=>cells.Where(i=>where(world.Ecology.Habitat[i])).OrderBy(i=>steep?-world.Ecology.Habitat[i].ReliefM:world.Ecology.Habitat[i].ReliefM).DefaultIfEmpty(-1).First();
        var sites=new[]{("plains",Pick(h=>h.Biome==Biome.Grassland||h.Biome==Biome.Savanna)),("savanna",Pick(h=>h.Biome==Biome.Savanna)),("forest-flat",Pick(h=>h.Biome==Biome.TemperateForest||h.Biome==Biome.TropicalRainforest)),("forest-temperate",Pick(h=>h.Biome==Biome.TemperateForest)),("forest-hills",Pick(h=>h.Biome==Biome.TemperateForest||h.Biome==Biome.TropicalRainforest,true)),("mountains",Pick(h=>h.Biome==Biome.Alpine||h.Biome==Biome.BorealForest,true)),("desert",Pick(h=>h.Biome==Biome.Desert)),("tundra",Pick(h=>h.Biome==Biome.Tundra))};
        string filter=Argument("--worldgen-perf-sites","");
        var exactSites=new Dictionary<string,V>();string sitesPath=Argument("--worldgen-perf-sites-file","");
        if(sitesPath.Length>0)
        {
            var set=WorldJson.Read<VegetationBenchmarkSiteSet>(File.ReadAllText(sitesPath));
            if(set.HistoryHash!=world.Prehistory.Hash)throw new InvalidOperationException("Performance sites belong to another world history");
            var locator=new CellLocator(world.Surface.Cells);
            foreach(var s in set.Sites)
            {if(exactSites.ContainsKey(s.Name)||s.Position.Length<.99)throw new InvalidOperationException("Invalid performance site");exactSites.Add(s.Name,s.Position);}
            sites=sites.Concat(set.Sites.Select(s=>(s.Name,locator.Nearest(s.Position)))).ToArray();
        }
        string caseFilter=Argument("--worldgen-perf-cases","");
        double seconds=double.Parse(Argument("--worldgen-perf-seconds","6"),CultureInfo.InvariantCulture);
        double profileView=double.Parse(Argument("--worldgen-perf-view","60000"),CultureInfo.InvariantCulture);
        int profilePlants=int.Parse(Argument("--worldgen-perf-plant-quality","2"));
        bool savedSettings=Environment.GetCommandLineArgs().Contains("--worldgen-perf-saved-settings");
        var cases=new List<PerfCase>{
            new PerfCase("grass-clumps",()=>TerrainVideoOptions.GrassClumps=true),
            new PerfCase("grass-approach-review",()=>{}),
            new PerfCase("ground-seam-review",()=>{}),
            new PerfCase("hot-query-verify",()=>{}),
            new PerfCase("camp-light-selection-verify",()=>{}),
            new PerfCase("small-plant-lighting-review",()=>{}),
            new PerfCase("tree-depth-review",()=>{}),
            new PerfCase("tree-bounded-depth",()=>Shader.SetGlobalFloat("_WorldGenTreeStableDepth",1)),
            new PerfCase("plants-full-pbr",()=>Shader.SetGlobalFloat("_WorldGenFullPlantPbr",1)),
            new PerfCase("water-query-verify",()=>{}),
            new PerfCase("water-aim-verify",()=>{}),
            new PerfCase("human-output-verify",()=>{}),
            new PerfCase("water-query-fresh",()=>WorldGenPerformance.ForceFreshWaterSamples=true),
            new PerfCase("travel-whip-full-water-aim",()=>WorldGenPerformance.FullWaterAim=true),
            new PerfCase("animal-pose-verify",()=>{}),
            new PerfCase("grass-blades",()=>TerrainVideoOptions.GrassClumps=false),
            new PerfCase("scatter-range-verify",()=>{}),
            new PerfCase("scatter-parallel-verify",()=>{}),
            new PerfCase("scatter-guard-verify",()=>{}),
            new PerfCase("moving-guard-verify",()=>{}),
            new PerfCase("ground-owner-verify",()=>{}),
            new PerfCase("scatter-gpu-depth-verify",()=>WorldGenPerformance.GpuNearOcclusion=true),
            new PerfCase("grass-indexed-verify",()=>{}),
            new PerfCase("foliage-one-tap",()=>WorldGenPerformance.FoliageShadowTaps=1),
            new PerfCase("foliage-four-tap",()=>WorldGenPerformance.FoliageShadowTaps=4),
            new PerfCase("no-mid-shadows",()=>TerrainVideoOptions.MidPlantShadows=false),
            new PerfCase("no-tree-shadows",()=>TerrainVideoOptions.TreeShadows=false),
            new PerfCase("no-trunk-casters",()=>WorldGenPerformance.NoTrunkCasters=true),
            new PerfCase("no-foliage-receivers",()=>WorldGenPerformance.NoFoliageShadowReceivers=true),
            new PerfCase("trunk-range45",()=>WorldGenPerformance.TreeTrunkShadowRange=45),
            new PerfCase("trunk-range180",()=>WorldGenPerformance.TreeTrunkShadowRange=180),
            new PerfCase("human-review",()=>{}),
            new PerfCase("forest-community-review",()=>{}),
            new PerfCase("foliage-proxy-compare",()=>{TerrainVideoOptions.DistantTreeShadowCards=false;WorldGenPerformance.DetailedFoliageComparison=true;}),
            new PerfCase("trees-efficient-verify",()=>{}),
            new PerfCase("trees-steady-legacy",()=>WorldGenPerformance.LegacyTreeResident=WorldGenPerformance.LegacyTreeShader=WorldGenPerformance.LegacyTreeWind=true),
            new PerfCase("trees-steady-efficient",()=>{}),
            new PerfCase("trees-steady-no-wind-cache",()=>WorldGenPerformance.LegacyTreeWind=true),
            new PerfCase("trees-steady-legacy-shader",()=>WorldGenPerformance.LegacyTreeShader=true),
            new PerfCase("turning-trees-legacy",()=>WorldGenPerformance.LegacyTreeResident=WorldGenPerformance.LegacyTreeShader=WorldGenPerformance.LegacyTreeWind=true),
            new PerfCase("turning-trees-efficient",()=>{}),
            new PerfCase("stutter-idle",()=>{}),
            new PerfCase("stutter-no-idle-scatter",()=>WorldGenPerformance.SuppressIdleScatter=true),
            new PerfCase("stutter-map",()=>terrainExplorer.PerformanceMap(true,360)),
            new PerfCase("stutter-map-local",()=>terrainExplorer.PerformanceMap(true,.05)),
            new PerfCase("stutter-map-no-upload",()=>{WorldGenPerformance.SkipMapUploads=true;terrainExplorer.PerformanceMap(true,.05);}),
            new PerfCase("grass-cull-fresh",()=>WorldGenPerformance.ForceFreshCoverCull=true),
            new PerfCase("grass-cull-cached",()=>WorldGenPerformance.ForceFreshCoverCull=false),
            new PerfCase("full",()=>{}),
            new PerfCase("grass-clumps-repeat",()=>TerrainVideoOptions.GrassClumps=true),
            new PerfCase("forest-ready",()=>{}),
            new PerfCase("trees-legacy-alpha",()=>WorldGenPerformance.TreeLegacyAlpha=true),
            new PerfCase("trees-alpha-repeat",()=>{}),
            new PerfCase("trees-legacy-alpha-repeat",()=>WorldGenPerformance.TreeLegacyAlpha=true),
            new PerfCase("trees-legacy-queues",()=>WorldGenPerformance.LegacyTreeQueues=true),
            new PerfCase("trees-queue-repeat",()=>{}),
            new PerfCase("trees-legacy-queues-repeat",()=>WorldGenPerformance.LegacyTreeQueues=true),
            new PerfCase("turning-individual-admission",()=>WorldGenPerformance.ForceIndividualTreeAdmission=true),
            new PerfCase("turning-legacy-alpha",()=>WorldGenPerformance.TreeLegacyAlpha=true),
            new PerfCase("turning-synchronous",()=>WorldGenPerformance.DrainEachFrame=true),
            new PerfCase("turning-legacy-synchronous",()=>{WorldGenPerformance.DrainEachFrame=WorldGenPerformance.TreeLegacyAlpha=true;}),
            new PerfCase("turning-individual-synchronous",()=>{WorldGenPerformance.DrainEachFrame=WorldGenPerformance.ForceIndividualTreeAdmission=true;}),
            new PerfCase("original-cpu",()=>WorldGenPerformance.OriginalCase=true),
            new PerfCase("no-grass",()=>TerrainVideoOptions.GroundCover=false),
            new PerfCase("no-specialists",()=>TerrainVideoOptions.SpecialistPlants=false),
            new PerfCase("no-mid",()=>TerrainVideoOptions.MidPlants=false),
            new PerfCase("no-trees",()=>TerrainVideoOptions.GeneratedTrees=TerrainVideoOptions.FarForest=false),
            new PerfCase("no-local-trees",()=>WorldGenPerformance.NoLocalTrees=true),
            new PerfCase("no-far-forest",()=>WorldGenPerformance.SuppressFarForest=true),
            new PerfCase("terrain-only",()=>{TerrainVideoOptions.GroundCover=false;TerrainVideoOptions.SpecialistPlants=TerrainVideoOptions.MidPlants=TerrainVideoOptions.GeneratedTrees=TerrainVideoOptions.FarForest=false;terrainExplorer.ScatterEnabled=false;}),
            new PerfCase("grass-only",()=>TerrainVideoOptions.SpecialistPlants=TerrainVideoOptions.MidPlants=TerrainVideoOptions.GeneratedTrees=TerrainVideoOptions.FarForest=false),
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
            new PerfCase("flying-no-trees",()=>{terrainExplorer.PerformancePose(60);TerrainVideoOptions.GeneratedTrees=TerrainVideoOptions.FarForest=false;}),
            new PerfCase("flying-no-mid",()=>{terrainExplorer.PerformancePose(60);TerrainVideoOptions.MidPlants=false;}),
            new PerfCase("flying-terrain-only",()=>{terrainExplorer.PerformancePose(60);TerrainVideoOptions.GroundCover=false;terrainExplorer.ScatterEnabled=false;TerrainVideoOptions.FarForest=false;}),
            new PerfCase("turning-original",()=>WorldGenPerformance.OriginalCase=true),
            new PerfCase("turning",()=>{}),
            new PerfCase("turning-whip",()=>{}),
            new PerfCase("turning-whip-current",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=true),
            new PerfCase("turning-gpu-depth",()=>WorldGenPerformance.GpuNearOcclusion=true),
            new PerfCase("turning-nonindexed",()=>WorldGenPerformance.NonIndexedClumps=true),
            new PerfCase("turning-depth-off",()=>TerrainVideoOptions.FoliageDepthPrepass=false),
            new PerfCase("turning-bands-off",()=>TerrainVideoOptions.FrontToBackTrees=false),
            new PerfCase("turning-depth-on",()=>TerrainVideoOptions.FoliageDepthPrepass=true),
            new PerfCase("travel-depth-off",()=>TerrainVideoOptions.FoliageDepthPrepass=false),
            new PerfCase("overlap-verify",()=>{}),
            new PerfCase("turning-uncached-ownership",()=>WorldGenPerformance.ForceFreshForestOwnership=true),
            new PerfCase("travel",()=>{}),
            new PerfCase("travel-whip",()=>{}),
            new PerfCase("travel-whip-current",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=WorldGenPerformance.KeepBackdrops=WorldGenPerformance.StagedGroundPublication=WorldGenPerformance.PacedTerrainRetirement=WorldGenPerformance.StagedTerrainRetirement=true),
            new PerfCase("travel-whip-immediate-ground",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=true),
            new PerfCase("travel-whip-unindexed-ground",()=>{WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=true;WorldGenPerformance.IndexedWalkingRivers=false;}),
            new PerfCase("travel-whip-staged-ground",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=WorldGenPerformance.StagedGroundPublication=true),
            new PerfCase("travel-whip-retained-backdrops",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=WorldGenPerformance.KeepBackdrops=true),
            new PerfCase("travel-whip-retained-staged",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=WorldGenPerformance.KeepBackdrops=WorldGenPerformance.StagedGroundPublication=true),
            new PerfCase("travel-whip-paced-retirement",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=WorldGenPerformance.KeepBackdrops=WorldGenPerformance.StagedGroundPublication=WorldGenPerformance.PacedTerrainRetirement=true),
            new PerfCase("travel-whip-staged-retirement",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=WorldGenPerformance.KeepBackdrops=WorldGenPerformance.StagedGroundPublication=WorldGenPerformance.PacedTerrainRetirement=WorldGenPerformance.StagedTerrainRetirement=true),
            new PerfCase("travel-whip-cover-pool",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=WorldGenPerformance.ResidentCoverReuse=true),
            new PerfCase("travel-whip-unpooled-cover",()=>{WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=true;WorldGenPerformance.ResidentCoverReuse=false;}),
            new PerfCase("travel-whip-indexed-ground",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=WorldGenPerformance.IndexedWalkingRivers=true),
            new PerfCase("travel-whip-staged-gc1",()=>{WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=WorldGenPerformance.StagedGroundPublication=true;UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds=1000000;}),
            new PerfCase("staged-ground-verify",()=>WorldGenPerformance.StagedGroundPublication=true),
            new PerfCase("cached-ground-verify",()=>WorldGenPerformance.StagedGroundPublication=true),
            new PerfCase("travel-whip-unpooled-terrain",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=WorldGenPerformance.NoTerrainBufferReuse=true),
            new PerfCase("travel-whip-unpooled-models",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=WorldGenPerformance.NoBuilderBufferReuse=true),
            new PerfCase("travel-whip-parallel",()=>WorldGenPerformance.ParallelScatter=true),
            new PerfCase("travel-whip-range-cache",()=>WorldGenPerformance.ScatterRangeCache=true),
            new PerfCase("travel-whip-parallel-range",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.ScatterRangeCache=true),
            new PerfCase("travel-whip-human-memo",()=>WorldGenPerformance.MemoHumanInputs=true),
            new PerfCase("travel-whip-optimized",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.MemoHumanInputs=true),
            new PerfCase("travel-whip-optimized-depth",()=>{WorldGenPerformance.ParallelScatter=WorldGenPerformance.MemoHumanInputs=true;TerrainVideoOptions.FoliageDepthPrepass=true;}),
            new PerfCase("travel-whip-flat-tree",()=>{WorldGenPerformance.ParallelScatter=WorldGenPerformance.MemoHumanInputs=true;Shader.EnableKeyword("WORLDGEN_FLAT_TREE_DEPTH");}),
            new PerfCase("travel-whip-partial-upload",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.PartialForestUploads=true),
            new PerfCase("travel-whip-burst",()=>WorldGenPerformance.BurstScatter=true),
            new PerfCase("travel-whip-burst-partial",()=>WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=true),
            new PerfCase("travel-whip-repeat-human-binding",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=WorldGenPerformance.RepeatHumanOutputBinding=true),
            new PerfCase("travel-whip-camera-trace",()=>WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=WorldGenPerformance.TracePresentedCamera=true),
            new PerfCase("travel-whip-coarse-shadows",()=>{WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=true;TerrainVideoOptions.DistantTreeShadowCards=true;}),
            new PerfCase("travel-whip-coarse-sorted",()=>{WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=true;TerrainVideoOptions.DistantTreeShadowCards=TerrainVideoOptions.FrontToBackTrees=true;}),
            new PerfCase("travel-whip-coarse-no-depth",()=>{WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=true;TerrainVideoOptions.DistantTreeShadowCards=true;TerrainVideoOptions.FoliageDepthPrepass=false;}),
            new PerfCase("travel-whip-detailed-foliage",()=>{WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=WorldGenPerformance.DetailedFoliageComparison=true;TerrainVideoOptions.DistantTreeShadowCards=false;}),
            new PerfCase("travel-whip-memory",()=>WorldGenPerformance.MemoryTrace=true),
            new PerfCase("travel-whip-allocation-profile",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=WorldGenPerformance.KeepBackdrops=WorldGenPerformance.StagedGroundPublication=WorldGenPerformance.PacedTerrainRetirement=WorldGenPerformance.StagedTerrainRetirement=true),
            new PerfCase("travel-whip-cpu-profile",()=>WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.PartialForestUploads=WorldGenPerformance.KeepBackdrops=WorldGenPerformance.StagedGroundPublication=WorldGenPerformance.PacedTerrainRetirement=WorldGenPerformance.StagedTerrainRetirement=true),
            new PerfCase("moving-parallel-verify",()=>WorldGenPerformance.ParallelScatter=true),
            new PerfCase("scatter-range-cache-verify",()=>{}),
            new PerfCase("human-weights-verify",()=>{}),
            new PerfCase("forest-upload-verify",()=>{}),
            new PerfCase("burst-scatter-verify",()=>{}),
            new PerfCase("burst-render-verify",()=>WorldGenPerformance.BurstScatter=true),
            new PerfCase("travel-whip-full-pbr",()=>Shader.SetGlobalFloat("_WorldGenFullPlantPbr",1)),
            new PerfCase("travel-whip-legacy-groups",()=>WorldGenPerformance.LegacyGroupVisibility=true),
            new PerfCase("travel-whip-narrow",()=>WorldGenPerformance.NarrowScatterGuard=true),
            new PerfCase("travel-whip-original-culling",()=>WorldGenPerformance.LegacyGroupVisibility=WorldGenPerformance.NarrowScatterGuard=true),
            new PerfCase("travel-gpu-depth",()=>WorldGenPerformance.GpuNearOcclusion=true),
            new PerfCase("travel-nonindexed",()=>WorldGenPerformance.NonIndexedClumps=true),
            new PerfCase("weather-off",()=>{var w=WeatherVideoOptions.Current;w.Clouds=0;w.SunGlare=w.SunRays=w.Adaptation=w.Visibility=0;})
        };
        if(caseFilter.Length==0)cases.RemoveAll(c=>c.Name=="travel-whip-allocation-profile"||c.Name=="travel-whip-cpu-profile");
        if(!WorldGenPerformance.TraceEnabled)cases.RemoveAll(c=>c.Name.StartsWith("stutter-",StringComparison.Ordinal));
        if(WorldGenPerformance.Presented&&(caseFilter.Length==0||caseFilter.Split(',').Any(name=>!name.StartsWith("travel",StringComparison.Ordinal)&&!name.StartsWith("turning",StringComparison.Ordinal))))
            throw new InvalidOperationException("Presented throughput requires explicit travel/turning cases; image/pose verifiers use offscreen mode");
        File.WriteAllText(Path.Combine(output,"machine.txt"),$"{SystemInfo.processorType}\n{SystemInfo.graphicsDeviceName}\n{SystemInfo.graphicsDeviceVersion}\nRAM {SystemInfo.systemMemorySize} MB VRAM {SystemInfo.graphicsMemorySize} MB\n{Screen.width}x{Screen.height}, {QualitySettings.activeColorSpace}, VSync {QualitySettings.vSyncCount}, target {Application.targetFrameRate}\nUnity {Application.unityVersion}\nUTC {DateTime.UtcNow:o}\nSeed {world.Recipe.Seed}; scale .1; clear noon; terrain quality2; view {profileView}m; vegetation quality{profilePlants}; fine radius153.6m.\nCPU sections are submission/update times, NOT GPU times. PresentMon is the independent GPU/presentation measurement.\n");
        File.AppendAllText(Path.Combine(output,"machine.txt"),$"Render mode: {(WorldGenPerformance.Presented?"normal automatic camera + presented window":"forced single offscreen URP render; automatic camera disabled")} {width}x{height}. Frame timing stats enabled: {FrameTimingManager.IsFeatureEnabled()}. Original CPU paths: {WorldGenPerformance.Original}.\nExplicit dense-site file: {sitesPath}; these sites use local summer noon.\n");
        File.WriteAllText(Path.Combine(output,"summary.csv"),"site,cell,biome,relief,case,frames,seconds,fps,p50_ms,p95_ms,p99_ms,max_ms,over10ms,over16ms,scatter_update_ms,scatter_submit_ms,grass_submit_ms,weather_ms,tiles,wanted,objects,specialists,mid,trees,scatter_draws,grass_draws,grass_candidates,grass_bytes,frustum_rejected,terrain_rejected,settled,start_qpc,end_qpc,gpu_ms,cpu_ms,timing_samples,gpu_drain_ms,start_utc,end_utc\n");
        File.WriteAllText(Path.Combine(output,"forest-root-uploads.csv"),"site,case,root_bytes,root_calls,full_root_uploads\n");
        File.WriteAllText(Path.Combine(output,"gc-settings.txt"),"incremental="+UnityEngine.Scripting.GarbageCollector.isIncremental+" mode="+UnityEngine.Scripting.GarbageCollector.GCMode+" slice_ns="+UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds+" fine_cull_trace="+WorldGenPerformance.FineCullTrace+"\n");
        foreach(var site in sites)
        {
            if(site.Item2<0||filter.Length>0&&!filter.Split(',').Contains(site.Item1))continue;
            void Defaults(){var w=WeatherVideoOptions.Current;w.Clouds=1;w.SunGlare=.35f;w.SunRays=.12f;w.Adaptation=.5f;w.Visibility=1;TerrainVideoOptions.ResetVegetation();TerrainVideoOptions.GroundCover=true;TerrainVideoOptions.GrassDensity=1;TerrainVideoOptions.GrassDetail=2;TerrainVideoOptions.GrassBladeDistance=30;TerrainVideoOptions.GrassDistance=300;TerrainVideoOptions.GrassCanopy=true;TerrainVideoOptions.Current.Quality=2;TerrainVideoOptions.Current.Effort=1;TerrainVideoOptions.Current.ViewDistanceM=profileView;TerrainVideoOptions.Current.FullDetailTiles=1.5;TerrainVideoOptions.ApplyAllPlantQuality(profilePlants);if(savedSettings)TerrainVideoOptions.Load();}
            // Read saved preferences only on explicit request; never write them.
            // Record the distinction from all-Medium instead of presenting a
            // different configuration as the owner's actual gameplay baseline.
            Defaults();
            File.AppendAllText(Path.Combine(output,"settings.txt"),site.Item1+" canopy="+TerrainVideoOptions.CanopyShade+" proxies="+TerrainVideoOptions.SimplifiedForestShadows+" coarseCrown="+TerrainVideoOptions.DistantTreeShadowCards+" treeShadows="+TerrainVideoOptions.TreeShadows+" frontToBack="+TerrainVideoOptions.FrontToBackTrees+" grassNear="+TerrainVideoOptions.GrassBladeDistance+" grassFar="+TerrainVideoOptions.GrassDistance+"\n");
            File.AppendAllText(Path.Combine(output,"settings.txt"),site.Item1+" saved="+savedSettings+" geometry="+TerrainVideoOptions.Current.Quality+" surface="+TerrainVideoOptions.SurfaceQuality+" grass="+TerrainVideoOptions.GrassQuality+" specialists="+TerrainVideoOptions.SpecialistQuality+" mid="+TerrainVideoOptions.MidQuality+" trees="+TerrainVideoOptions.TreeQuality+" view="+TerrainVideoOptions.Current.ViewDistanceM+" depthPrepass="+TerrainVideoOptions.FoliageDepthPrepass+"\n");
            Defaults();TerrainVideoOptions.Revision++;selectedCell=site.Item2;ExploreTerrain(exactSites.TryGetValue(site.Item1,out var exactSite)?exactSite:world.Surface.Cells[site.Item2].Position);
            deadline=Time.realtimeSinceStartupAsDouble+240;while(terrainExplorer!=null&&!terrainExplorer.Ready&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(terrainExplorer==null||!terrainExplorer.Ready){Debug.LogError("PERF terrain unavailable");Application.Quit(1);yield break;}
            terrainExplorer.PerformanceDrySite();terrainExplorer.TestView(1.65,20,3);terrainExplorer.PerformanceRender(width,height);
            TerrainMapViewport.Degrees(terrainExplorer.Position,out double lat,out double lon);play.SetLocalSolarHour(12,lon);
            PlayCommands.Execute(play,0,"/weather clear",weather:terrainExplorer.WeatherControl);
            File.AppendAllText(Path.Combine(output,"sites.txt"),$"{site.Item1} cell {site.Item2} {lat:R},{lon:R}; biome {world.Ecology.Habitat[site.Item2].Biome}; relief {world.Ecology.Habitat[site.Item2].ReliefM}\n");
            if(WorldGenPerformance.TraceEnabled)File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyMapMaterials()+"\n");
            bool previousTerrain=false,firstCase=true;
            foreach(var item in cases)
            {
                if(caseFilter.Length>0&&!caseFilter.Split(',').Contains(item.Name))continue;
                terrainExplorer.PerformanceMap(false);WorldGenPerformance.SuppressIdleScatter=WorldGenPerformance.SkipMapUploads=WorldGenPerformance.ForceFreshCoverCull=WorldGenPerformance.NoLocalTrees=WorldGenPerformance.ForceFreshForestOwnership=WorldGenPerformance.TreeLegacyAlpha=WorldGenPerformance.SuppressFarForest=WorldGenPerformance.ForceIndividualTreeAdmission=WorldGenPerformance.DrainEachFrame=WorldGenPerformance.LegacyTreeQueues=false;
                WorldGenPerformance.FoliageShadowTaps=0;WorldGenPerformance.FullWaterAim=false;WorldGenPerformance.ForceFreshWaterSamples=WorldGenPerformance.LegacyGroupVisibility=WorldGenPerformance.NarrowScatterGuard=false;
                WorldGenPerformance.GpuNearOcclusion=WorldGenPerformance.ParallelScatter=WorldGenPerformance.BurstScatter=WorldGenPerformance.ScatterRangeCache=false;Shader.SetGlobalFloat("_WorldGenFullPlantPbr",0);Shader.SetGlobalFloat("_WorldGenTreeStableDepth",0);Shader.DisableKeyword("WORLDGEN_FLAT_TREE_DEPTH");
                WorldGenPerformance.MemoHumanInputs=WorldGenPerformance.MemoryTrace=WorldGenPerformance.PartialForestUploads=false;
                WorldGenPerformance.TracePresentedCamera=false;
                WorldGenPerformance.RepeatHumanOutputBinding=false;
                WorldGenPerformance.NoTerrainBufferReuse=false;
                WorldGenPerformance.ResidentCoverReuse=true;
                WorldGenPerformance.IndexedWalkingRivers=true;
                WorldGenPerformance.NoBuilderBufferReuse=false;
                WorldGenPerformance.StagedGroundPublication=false;
                WorldGenPerformance.KeepBackdrops=false;
                WorldGenPerformance.PacedTerrainRetirement=false;
                WorldGenPerformance.StagedTerrainRetirement=false;
                UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds=originalGcSlice;
                WorldGenPerformance.DetailedFoliageComparison=false;
                WorldGenPerformance.NonIndexedClumps=false;
                WorldGenPerformance.NoTrunkCasters=WorldGenPerformance.NoFoliageShadowReceivers=false;
                WorldGenPerformance.TreeTrunkShadowRange=0;
                WorldGenPerformance.LegacyTreeResident=WorldGenPerformance.LegacyTreeShader=WorldGenPerformance.LegacyTreeWind=false;Defaults();WorldGenPerformance.OriginalCase=false;terrainExplorer.PerformancePose();terrainExplorer.ScatterEnabled=true;terrainExplorer.PerformanceShadows(true);item.Apply();terrainExplorer.PerformanceWalkingQueryMode();TerrainVideoOptions.VegetationRevision++;terrainExplorer.PerformanceInvalidate();
                File.AppendAllText(Path.Combine(output,"case-settings.txt"),site.Item1+" "+item.Name+" parallel="+WorldGenPerformance.ParallelScatter+" burst="+WorldGenPerformance.BurstScatter+" partialUploads="+WorldGenPerformance.PartialForestUploads+" flatTree="+Shader.IsKeywordEnabled("WORLDGEN_FLAT_TREE_DEPTH")+" rangeCache="+WorldGenPerformance.ScatterRangeCache+" humanMemo="+WorldGenPerformance.MemoHumanInputs+" repeatHumanBinding="+WorldGenPerformance.RepeatHumanOutputBinding+" fullWaterAim="+WorldGenPerformance.FullWaterAim+" memoryTrace="+WorldGenPerformance.MemoryTrace+" depthPrepass="+TerrainVideoOptions.FoliageDepthPrepass+"\n");
                terrainExplorer.PerformanceTreeAlpha(!WorldGenPerformance.TreeLegacyAlpha);
                terrainExplorer.PerformanceTreeQueues(!WorldGenPerformance.LegacyTreeQueues);
                if(item.Terrain||previousTerrain)TerrainVideoOptions.Revision++;previousTerrain=item.Terrain;
                // Readiness is explicit; partial scenes are labelled, never silently accepted.
                yield return null;
                double warmup=item.Name=="forest-ready"?900:item.Name=="forest-community-review"?300:firstCase||item.Name=="view-200km"||item.Name=="full"||item.Terrain&&terrainExplorer.WantedCount>1000?420:150;
                // Short diagnostic captures identify allocation call sites, not
                // release throughput. Do not spend seven minutes waiting for
                // distant readiness when that is unrelated to the question.
                if(item.Name=="travel-whip-allocation-profile"||item.Name=="travel-whip-cpu-profile")
                {
                    double requested=double.Parse(Argument("--worldgen-profile-warmup-seconds","-1"),CultureInfo.InvariantCulture);
                    if(requested>=0)warmup=Math.Min(warmup,Math.Max(10,requested));
                }
                File.AppendAllText(Path.Combine(output,"case-settings.txt"),"warmup_limit_seconds="+warmup.ToString(CultureInfo.InvariantCulture)+"\n");
                deadline=Time.realtimeSinceStartupAsDouble+warmup;
                double progressAt=0;
                while(!terrainExplorer.PerformanceSettled&&terrainExplorer.Error==""&&Time.realtimeSinceStartupAsDouble<deadline)
                {
                    if(Time.realtimeSinceStartupAsDouble>=progressAt)
                    {progressAt=Time.realtimeSinceStartupAsDouble+5;File.AppendAllText(Path.Combine(output,"loading-progress.txt"),site.Item1+" "+item.Name+" t="+Time.realtimeSinceStartupAsDouble.ToString("F1")+" tiles "+terrainExplorer.TileCount+"/"+terrainExplorer.WantedCount+"; "+terrainExplorer.PerformanceForestState+"; "+terrainExplorer.PerformanceGroundPublication+"\n");}
                    yield return null;
                }
                if(terrainExplorer.Error!=""){Debug.LogError("PERF "+terrainExplorer.Error);Application.Quit(1);yield break;}
                firstCase=false;
                play.SetLocalSolarHour(12,lon);terrainExplorer.PerformanceFreezeDaylight(sitesPath.Length>0);yield return new WaitForSecondsRealtime(4);
                if(item.Name=="scatter-parallel-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyParallelScatter()+"\n");
                if(item.Name=="moving-parallel-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyParallelRenders(Path.Combine(output,"parallel-renders"))+"\n");
                if(item.Name=="scatter-range-cache-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyScatterRange()+"\n");
                if(item.Name=="human-weights-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyHumanInputs()+"\n");
                if(item.Name=="forest-upload-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyForestUploads()+"\n");
                if(item.Name=="burst-scatter-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyParallelScatter(true)+"\n");
                if(item.Name=="burst-render-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyParallelRenders(Path.Combine(output,"burst-renders"),true)+"\n");
                if(item.Name=="ground-seam-review")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceReviewGroundSeams(output,site.Item1)+"\n");
                if(item.Name=="hot-query-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyHotQueries()+"\n");
                if(item.Name=="camp-light-selection-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyCampLightSelection()+"\n");
                if(item.Name=="grass-approach-review")yield return terrainExplorer.PerformanceReviewGrassApproach(output,site.Item1);
                if(item.Name=="tree-depth-review")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceReviewTreeDepth(output,site.Item1)+"\n");
                if(item.Name=="small-plant-lighting-review")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceReviewPlantLighting(output,site.Item1)+"\n");
                if(item.Name=="forest-community-review")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceReviewVegetation(output,site.Item1)+"\n");
                if(item.Name=="water-query-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyWaterSamples()+"\n");
                if(item.Name=="water-aim-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyWaterAim()+"\n");
                if(item.Name=="human-output-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyHumanInputs(true)+"\n");
                if(item.Name=="animal-pose-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyAnimalPoseMath()+"\n");
                if(item.Name=="stutter-idle")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyRenderCleanup()+"\n"+terrainExplorer.PerformanceVerifyVegetationScene()+"\n"+terrainExplorer.PerformanceVerifyGrassCullCache()+"\n");
                if(item.Name=="forest-ready"){File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyForestIdentity()+"\n"+terrainExplorer.PerformanceVerifyTreeAlpha()+"\n"+terrainExplorer.PerformanceVerifyTreeGroupAdmission()+"\n"+terrainExplorer.PerformanceVerifyTreeQueues(Path.Combine(output,site.Item1+"-queues"))+"\n");terrainExplorer.PerformanceCapture(Path.Combine(output,site.Item1+"-forest.png"));}
                if(item.Name=="trees-efficient-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyTreeEfficiency(output)+"\n");
                if(item.Name=="scatter-range-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyVisibility()+"\n");
                if(item.Name=="scatter-guard-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyRotationGuard(Path.Combine(output,"rotation-guard"))+"\n");
                if(item.Name=="moving-guard-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyRotationGuard(Path.Combine(output,"moving-rotation-guard"),true)+"\n");
                if(item.Name=="ground-owner-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifySelectiveProjection()+"\n");
                if(item.Name=="staged-ground-verify")yield return terrainExplorer.PerformanceVerifyGroundPublication(output);
                if(item.Name=="cached-ground-verify")
                {
                    bool heldTerrain=TerrainDiagnostics.HoldTerrain,heldPlants=TerrainDiagnostics.HoldPlants;TerrainDiagnostics.HoldTerrain=TerrainDiagnostics.HoldPlants=true;
                    try{yield return terrainExplorer.PerformanceVerifyCachedGroundPublication(output);}
                    finally{TerrainDiagnostics.HoldTerrain=heldTerrain;TerrainDiagnostics.HoldPlants=heldPlants;}
                }
                if(item.Name=="scatter-gpu-depth-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),"Near GPU depth: "+terrainExplorer.PerformanceVerifyRotationGuard(Path.Combine(output,"near-gpu-depth"))+"\n");
                if(item.Name=="grass-indexed-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyIndexedGround(output)+"\n");
                if(item.Name=="overlap-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifySelectiveProjection()+"\n"+terrainExplorer.PerformanceVerifyTreeBands(output)+"\n"+terrainExplorer.PerformanceVerifyFoliageDepth(output)+"\n");
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
                    File.AppendAllText(Path.Combine(output,"equivalence.txt"),site.Item1+": "+terrainExplorer.PerformanceVerifyTrunkBudget()+"\n");
                    yield return new WaitForSecondsRealtime(2);terrainExplorer.PerformanceCapture(Path.Combine(output,site.Item1+".png"));yield return new WaitForSecondsRealtime(1);
                }
                if(item.Name=="flying-60m"){terrainExplorer.PerformanceCapture(Path.Combine(output,site.Item1+"-flying.png"));yield return new WaitForSecondsRealtime(1);}
                if(item.Name=="human-review")yield return terrainExplorer.PerformanceHumanReview(output);
                WorldGenPerformance.ProfileRenderCompletion=item.Name=="forest-community-review"||item.Name=="foliage-proxy-compare";
                var renderCosts=new List<double>();var frames=new List<double>(10000);var sections=new double[4];int gc=GC.CollectionCount(0);
                var stutter=WorldGenPerformance.TraceEnabled?new List<StutterRow>(30000):null;
                var engineTimings=WorldGenPerformance.TraceEnabled?new List<(double Observed,FrameTiming Timing)>(20000):null;
                ulong lastRawTiming=0;
                double driverMs=0,driverGroundMs=0;var driverQueries=new double[6];var driverBefore=new double[6];
                long heap=0;double heapAt=0;var endFrame=stutter==null&&!WorldGenPerformance.Presented?null:new WaitForEndOfFrame();
                var timings=new FrameTiming[1];double gpu=0,cpu=0,cpuMain=0,cpuRender=0,cpuPresentWait=0,largestRejectedGpu=0;int timingCount=0,rejectedGpuTimings=0;ulong lastTiming=0;
                using var allocationRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
                File.AppendAllText(Path.Combine(output,"gc-settings.txt"),site.Item1+" "+item.Name+" allocation_counter_valid="+allocationRecorder.Valid+" automatic_slice_ns="+UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds+"\n");
                if(WorldGenPerformance.Presented)yield return endFrame;
                terrainExplorer.WeatherCheckGpuBarrier(); // Drain pre-window work before counting renders.
                terrainExplorer.PerformanceBeginGroundTrace();
                WorldGenPerformance.Measuring=true;
                var windowUtc=DateTime.UtcNow;
                bool allocationProfile=item.Name=="travel-whip-allocation-profile",cpuProfile=item.Name=="travel-whip-cpu-profile";
                if(allocationProfile||cpuProfile)
                {
                    if(!Debug.isDebugBuild)throw new InvalidOperationException("Profiler capture requires an explicit diagnostic DevelopmentPlayer; never interpret its FPS as release throughput.");
                    UnityEngine.Profiling.Profiler.logFile=Path.Combine(output,site.Item1+(allocationProfile?"-allocations.raw":"-cpu.raw"));
                    UnityEngine.Profiling.Profiler.enableBinaryLog=true;
                    UnityEngine.Profiling.Profiler.enableAllocationCallstacks=allocationProfile;
                    UnityEngine.Profiling.Profiler.enabled=true;
                }
                long uploadBytesBefore=terrainExplorer.ForestRootUploadBytes,uploadCallsBefore=terrainExplorer.ForestRootUploadCalls,uploadFullBefore=terrainExplorer.ForestRootFullUploads;
                File.AppendAllText(Path.Combine(output,"case-settings.txt"),site.Item1+" "+item.Name+" presentation="+Screen.fullScreenMode+" resolution="+Screen.width+"x"+Screen.height+" vSync="+QualitySettings.vSyncCount+" targetFps="+Application.targetFrameRate+"\n");
                var clockBridge=WorldGenPerformance.TraceEnabled?WorldGenPerformance.CaptureNativeClock():default;
                long qpcStart=System.Diagnostics.Stopwatch.GetTimestamp();double start=Time.realtimeSinceStartupAsDouble,previous=start;
                WorldGenPerformance.ClearFaunaTrace();
                Debug.Log("PERF BEGIN "+site.Item1+" "+item.Name+" qpc="+qpcStart);
                while(Time.realtimeSinceStartupAsDouble-start<seconds)
                {
                    yield return endFrame;
                    // DIAGNOSTIC ONLY: distinguish unbounded offscreen queueing
                    // from work that remains slow when every render completes.
                    // This is intentionally synchronous and never normal play.
                    if(WorldGenPerformance.DrainEachFrame)terrainExplorer.WeatherCheckGpuBarrier();
                    double now=Time.realtimeSinceStartupAsDouble;double frameMs=(now-previous)*1000;frames.Add(frameMs);previous=now;
                    if(WorldGenPerformance.ProfileRenderCompletion&&frames.Count>2&&WorldGenPerformance.CompletedRenderMs>0)renderCosts.Add(WorldGenPerformance.CompletedRenderMs);
                    if(stutter!=null)
                    {
                        if(now>=heapAt){heap=GC.GetTotalMemory(false);heapAt=now+.25;}
                        var t=WorldGenPerformance.TraceMilliseconds;
                        stutter.Add(new StutterRow{Time=now-start,Frame=frameMs,Gc=GC.CollectionCount(0),Heap=heap,MainAllocated=GC.GetAllocatedBytesForCurrentThread(),Update=t[0],Map=t[1],Scatter=t[2],Weather=t[3],Ground=t[4]+driverGroundMs,Fog=t[5],Coverage=t[6],Sky=t[7],Batches=t[9],Bakes=t[10],Projection=t[11],Forest=t[12],Shadows=t[13],CoverDraw=t[14],Canopy=t[15],ForestDraw=t[16],Survival=t[17],Prepared=t[18],Humans=t[19],Player=t[20],Fauna=t[21],AnimalMove=t[22],AnimalPose=t[23],BatchCollect=t[24],BatchUpload=t[25],TerrainOcclusion=t[26],FrustumTest=t[27],InstanceAppend=t[28],FrameAllocated=allocationRecorder.Valid?allocationRecorder.LastValue:-1,ScatterClassify=t[29],ScatterAppend=t[30],CameraRender=t[8],ForestGround=t[31],GroundPublication=t[32],Inventory=t[33],Planning=t[34],Install=t[35],Retirement=t[36],Scheduling=t[37],Readout=t[38],Solids=t[39],PlayerCamera=t[40],PlayerWater=t[41],PlayerCombat=t[42],PlayerBlood=t[43],PlayerFootsteps=t[44],PlayerFeedback=t[45],PlayerHud=t[46],PlayerAltitude=t[47],QueryLocate=TerrainQueryTiming.Milliseconds(0)+driverQueries[0],QueryProfiles=TerrainQueryTiming.Milliseconds(1)+driverQueries[1],QueryShape=TerrainQueryTiming.Milliseconds(2)+driverQueries[2],QueryRivers=TerrainQueryTiming.Milliseconds(3)+driverQueries[3],QueryDetail=TerrainQueryTiming.Milliseconds(4)+driverQueries[4],QueryPonds=TerrainQueryTiming.Milliseconds(5)+driverQueries[5],Driver=driverMs,HeapUpdate=WorldGenPerformance.MemoryTrace?WorldGenPerformance.HeapGrowth[0]:0,HeapScatter=WorldGenPerformance.MemoryTrace?WorldGenPerformance.HeapGrowth[2]:0,HeapHumans=WorldGenPerformance.MemoryTrace?WorldGenPerformance.HeapGrowth[19]:0,HeapPlayer=WorldGenPerformance.MemoryTrace?WorldGenPerformance.HeapGrowth[20]:0,HeapFauna=WorldGenPerformance.MemoryTrace?WorldGenPerformance.HeapGrowth[21]:0,HeapCamera=WorldGenPerformance.MemoryTrace?WorldGenPerformance.HeapGrowth[8]:0,HeapIntervalsWithCollection=WorldGenPerformance.MemoryTrace?WorldGenPerformance.HeapIntervalCollections:0});
                    }
                    // The driver runs after this frame's row. Its ground query
                    // belongs to the NEXT measured frame interval; carry it
                    // explicitly rather than losing it when Update clears scopes.
                    long driverStart=stutter==null?0:System.Diagnostics.Stopwatch.GetTimestamp();
                    double priorGround=WorldGenPerformance.TraceMilliseconds[4];
                    if(stutter!=null)for(int phase=0;phase<6;phase++)driverBefore[phase]=TerrainQueryTiming.Milliseconds(phase);
                    if(item.Name.StartsWith("turning",StringComparison.Ordinal))terrainExplorer.PerformanceTurn(item.Name.StartsWith("turning-whip",StringComparison.Ordinal)?20+150*Mathf.Sin((float)(now-start)*4):20+(float)((now-start)*60));
                    if(item.Name.StartsWith("travel",StringComparison.Ordinal))
                    {terrainExplorer.PerformanceTravel((now-start)*8);if(item.Name.StartsWith("travel-whip",StringComparison.Ordinal))terrainExplorer.PerformanceTurn(20+150*Mathf.Sin((float)(now-start)*4));}
                    if(stutter!=null)
                    {
                        driverMs=(System.Diagnostics.Stopwatch.GetTimestamp()-driverStart)*1000d/System.Diagnostics.Stopwatch.Frequency;
                        driverGroundMs=WorldGenPerformance.TraceMilliseconds[4]-priorGround;
                        for(int phase=0;phase<6;phase++)driverQueries[phase]=TerrainQueryTiming.Milliseconds(phase)-driverBefore[phase];
                    }
                    for(int j=0;j<4;j++)sections[j]+=WorldGenPerformance.Milliseconds[j];
                    FrameTimingManager.CaptureFrameTimings();
                    uint availableTimings=FrameTimingManager.GetLatestTimings(1,timings);
                    if(engineTimings!=null&&availableTimings>0&&timings[0].frameStartTimestamp!=lastRawTiming)
                    {
                        lastRawTiming=timings[0].frameStartTimestamp;
                        engineTimings.Add((now-start,timings[0]));
                    }
                    if(availableTimings>0&&timings[0].frameStartTimestamp!=lastTiming&&timings[0].gpuFrameTime>0)
                    {
                        lastTiming=timings[0].frameStartTimestamp;double measuredGpu=timings[0].gpuFrameTime;
                        // The GPU was drained immediately before this window.
                        // A reported GPU frame longer than the entire elapsed
                        // window (+5s grace) is a counter error, not throughput.
                        // Keep every actual frame duration/FPS sample unchanged.
                        if(double.IsNaN(measuredGpu)||double.IsInfinity(measuredGpu)||measuredGpu>(now-start+5)*1000)
                        {rejectedGpuTimings++;largestRejectedGpu=Math.Max(largestRejectedGpu,measuredGpu);}
                        else{gpu+=measuredGpu;cpu+=timings[0].cpuFrameTime;cpuMain+=timings[0].cpuMainThreadFrameTime;cpuRender+=timings[0].cpuRenderThreadFrameTime;cpuPresentWait+=timings[0].cpuMainThreadPresentWaitTime;timingCount++;}
                    }
                }
                double drainStart=Time.realtimeSinceStartupAsDouble;
                terrainExplorer.WeatherCheckGpuBarrier(); // Include completion, not just CPU queueing, in mean FPS.
                double gpuDrain=(Time.realtimeSinceStartupAsDouble-drainStart)*1000;
                string componentFile=Path.Combine(output,"cpu-frame-components.csv");
                if(!File.Exists(componentFile))File.WriteAllText(componentFile,"site,case,samples,main_ms,render_ms,present_wait_ms\n");
                File.AppendAllText(componentFile,site.Item1+","+item.Name+","+timingCount+","+(timingCount>0?cpuMain/timingCount:0).ToString("F5",CultureInfo.InvariantCulture)+","+(timingCount>0?cpuRender/timingCount:0).ToString("F5",CultureInfo.InvariantCulture)+","+(timingCount>0?cpuPresentWait/timingCount:0).ToString("F5",CultureInfo.InvariantCulture)+"\n");
                if(allocationProfile||cpuProfile)
                {
                    UnityEngine.Profiling.Profiler.enabled=false;
                    UnityEngine.Profiling.Profiler.enableAllocationCallstacks=false;
                    UnityEngine.Profiling.Profiler.enableBinaryLog=false;
                    File.AppendAllText(Path.Combine(output,"allocation-profile.txt"),site.Item1+": diagnostic DevelopmentPlayer CPU capture; allocation callstacks="+allocationProfile+"; displayed FPS is NOT release throughput.\n");
                }
                WorldGenPerformance.Measuring=false;WorldGenPerformance.ProfileRenderCompletion=false;
                if(renderCosts.Count>0){renderCosts.Sort();File.AppendAllText(Path.Combine(output,"completed-renders.txt"),site.Item1+" "+item.Name+": "+renderCosts.Count+" samples; mean "+renderCosts.Average().ToString("F3")+"ms, median "+renderCosts[renderCosts.Count/2].ToString("F3")+"ms; submission + actual GPU completion, excluding Update/presentation/frame-loop waits.\n");}
                if(terrainExplorer.Error!="")throw new InvalidOperationException("Performance scene: "+terrainExplorer.Error);
                if(WorldGenPerformance.TraceEnabled&&item.Name.StartsWith("stutter-map",StringComparison.Ordinal))File.AppendAllText(Path.Combine(output,"map-progress.txt"),item.Name+": "+terrainExplorer.PerformanceMapDiagnostics+"\n");
                long qpcEnd=System.Diagnostics.Stopwatch.GetTimestamp();double elapsed=Time.realtimeSinceStartupAsDouble-start;var sorted=frames.OrderBy(v=>v).ToArray();
                double P(double p)=>sorted[Math.Min(sorted.Length-1,(int)(sorted.Length*p))];
                var h=world.Ecology.Habitat[site.Item2];
                string row=string.Join(",",site.Item1,site.Item2,h.Biome,h.ReliefM,item.Name,frames.Count,Perf(elapsed),Perf(frames.Count/elapsed),Perf(P(.5)),Perf(P(.95)),Perf(P(.99)),Perf(sorted.Last()),frames.Count(v=>v>10),frames.Count(v=>v>16.667),Perf(sections[0]/frames.Count),Perf(sections[1]/frames.Count),Perf(sections[2]/frames.Count),Perf(sections[3]/frames.Count),terrainExplorer.PerformanceCounts,terrainExplorer.PerformanceSettled,qpcStart,qpcEnd);
                row+=","+Perf(gpu/Math.Max(1,timingCount))+","+Perf(cpu/Math.Max(1,timingCount))+","+timingCount+","+Perf(gpuDrain)+","+windowUtc.ToString("O",CultureInfo.InvariantCulture)+","+DateTime.UtcNow.ToString("O",CultureInfo.InvariantCulture);
                File.AppendAllText(Path.Combine(output,"summary.csv"),row+"\n");
                File.AppendAllText(Path.Combine(output,"forest-root-uploads.csv"),site.Item1+","+item.Name+","+(terrainExplorer.ForestRootUploadBytes-uploadBytesBefore)+","+(terrainExplorer.ForestRootUploadCalls-uploadCallsBefore)+","+(terrainExplorer.ForestRootFullUploads-uploadFullBefore)+"\n");
                File.AppendAllText(Path.Combine(output,"forest-state.txt"),site.Item1+" "+item.Name+": "+terrainExplorer.PerformanceForestState+"\n");
                File.AppendAllText(Path.Combine(output,"gpu-counter-validation.txt"),site.Item1+" "+item.Name+": valid="+timingCount+" rejected="+rejectedGpuTimings+" largest_rejected_ms="+largestRejectedGpu.ToString("R",CultureInfo.InvariantCulture)+"; actual frame/FPS samples never filtered\n");
                File.AppendAllText(Path.Combine(output,"ground-publication.txt"),site.Item1+" "+item.Name+": "+terrainExplorer.PerformanceGroundPublication+"\n");
                if(WorldGenPerformance.TraceEnabled)terrainExplorer.PerformanceWriteGroundTrace(Path.Combine(output,site.Item1+"-"+item.Name+"-ground-corrections.csv"));
                WorldGenPerformance.WriteFaunaTrace(Path.Combine(output,site.Item1+"-"+item.Name+"-fauna-stages.csv"),qpcStart);
                File.AppendAllText(Path.Combine(output,"terrain-upload-pool.txt"),site.Item1+" "+item.Name+": "+terrainExplorer.PerformanceUploadPool+"\n");
                File.WriteAllLines(Path.Combine(output,site.Item1+"-"+item.Name+"-frames.csv"),new[]{"ms"}.Concat(frames.Select(Perf)));
                if(stutter!=null)File.WriteAllLines(Path.Combine(output,site.Item1+"-"+item.Name+"-trace.csv"),new[]{"t_s,frame_ms,gc0,heap_bytes,allocated_main_total,explorer_ms,map_ms,scatter_ms,weather_ms,ground_ms,fog_ms,coverage_ms,sky_ms,batches_ms,bakes_ms,projection_ms,forest_ms,shadow_batches_ms,cover_draw_ms,canopy_ms,forest_draw_ms,survival_ms,prepared_frame_ms,humans_ms,player_ms,fauna_ms,animal_move_ms,animal_pose_ms,batch_collect_ms,batch_upload_ms,terrain_occlusion_ms,frustum_test_ms,instance_append_ms,gc_allocated_in_frame_bytes,scatter_classify_ms,scatter_append_ms,camera_render_ms,heap_growth_update_bytes,heap_growth_scatter_bytes,heap_growth_humans_bytes,heap_growth_player_bytes,heap_growth_fauna_bytes,heap_growth_camera_bytes,heap_intervals_with_collection,forest_ground_ms,ground_publication_ms,inventory_ms,planning_ms,terrain_install_ms,terrain_retirement_ms,scheduling_ms,readout_ms,solids_ms,player_camera_ms,player_water_ms,player_combat_ms,player_blood_ms,player_footsteps_ms,player_feedback_ms,player_hud_ms,player_altitude_ms,query_locate_ms,query_profiles_ms,query_shape_ms,query_rivers_ms,query_detail_ms,query_ponds_ms,driver_ms"}.Concat(stutter.Select(s=>s.ToString())));
                Debug.Log("PERF END "+row+" gc0="+(GC.CollectionCount(0)-gc));
                if(engineTimings!=null)
                {
                    // Engine samples arrive asynchronously; retain their own
                    // source timestamps instead of assigning delayed GPU data
                    // to the current gameplay frame. Include invalid counters
                    // in this raw diagnostic file; aggregate validation above
                    // never removes measured wall-clock frame durations.
                    File.WriteAllText(Path.Combine(output,site.Item1+"-"+item.Name+"-clock-bridge.csv"),
                        "managed_before,system_qpc,managed_after,system_hz,managed_hz,engine_hz\n"+
                        string.Join(",",clockBridge.Before,clockBridge.Native,clockBridge.After,clockBridge.Frequency,System.Diagnostics.Stopwatch.Frequency,FrameTimingManager.GetCpuTimerFrequency())+"\n");
                    File.WriteAllText(Path.Combine(output,"engine-timing-clock.txt"),"engine_cpu_hz="+FrameTimingManager.GetCpuTimerFrequency()+" stopwatch_hz="+System.Diagnostics.Stopwatch.Frequency+"\n");
                    File.WriteAllLines(Path.Combine(output,site.Item1+"-"+item.Name+"-engine-timings.csv"),
                        new[]{"observed_t_s,frame_start,first_submit,present_called,frame_complete,cpu_ms,main_ms,render_ms,present_wait_ms,gpu_ms"}.Concat(engineTimings.Select(s=>
                            string.Join(",",Perf(s.Observed),s.Timing.frameStartTimestamp,s.Timing.firstSubmitTimestamp,s.Timing.cpuTimePresentCalled,s.Timing.cpuTimeFrameComplete,
                                Perf(s.Timing.cpuFrameTime),Perf(s.Timing.cpuMainThreadFrameTime),Perf(s.Timing.cpuRenderThreadFrameTime),Perf(s.Timing.cpuMainThreadPresentWaitTime),Perf(s.Timing.gpuFrameTime)))));
                }
                if(Environment.GetCommandLineArgs().Contains("--worldgen-perf-capture-after"))
                {
                    // Capture only after all timing samples and counters are
                    // finalized. Readback/PNG encoding never enters the run.
                    string imagePath=Path.Combine(output,site.Item1+"-"+item.Name+"-after.png");
                    if(WorldGenPerformance.Presented)
                    {
                        yield return new WaitForEndOfFrame();
                        var image=ScreenCapture.CaptureScreenshotAsTexture();
                        try{File.WriteAllBytes(imagePath,image.EncodeToPNG());}finally{Destroy(image);}
                    }
                    else terrainExplorer.PerformanceCapture(imagePath);
                }
                // Explicit diagnostic capture AFTER the measurement has ended.
                // Snapshot pauses and file IO must never enter an FPS window.
                if(Environment.GetCommandLineArgs().Contains("--worldgen-memory-snapshot"))
                {
                    if(!Debug.isDebugBuild)throw new InvalidOperationException("Heap snapshots require a diagnostic DevelopmentPlayer, not the playtest build.");
                    string snapshotPath=Path.Combine(output,site.Item1+"-"+item.Name+".snap");
                    if(File.Exists(snapshotPath))throw new InvalidOperationException("Refusing to overwrite an existing heap snapshot.");
                    bool finished=false,succeeded=false;
                    Unity.Profiling.Memory.MemoryProfiler.TakeSnapshot(snapshotPath,(path,success)=>{succeeded=success;finished=true;},
                        Unity.Profiling.Memory.CaptureFlags.ManagedObjects|Unity.Profiling.Memory.CaptureFlags.NativeObjects);
                    double snapshotDeadline=Time.realtimeSinceStartupAsDouble+120;
                    while(!finished&&Time.realtimeSinceStartupAsDouble<snapshotDeadline)yield return null;
                    if(!finished||!succeeded)throw new InvalidOperationException("Managed heap snapshot did not complete: "+snapshotPath);
                    File.AppendAllText(Path.Combine(output,"memory-snapshots.txt"),snapshotPath+"\n");
                }
            }
            File.AppendAllText(Path.Combine(output,"equivalence.txt"),site.Item1+": "+terrainExplorer.PerformancePendingComparisons+" loading/idle readiness comparisons passed.\n");
            terrainExplorer.Close();yield return null;yield return null;
        }
        UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds=originalGcSlice;
        File.WriteAllText(Path.Combine(output,"complete.txt"),DateTime.UtcNow.ToString("o"));Application.Quit(0);
    }
}
