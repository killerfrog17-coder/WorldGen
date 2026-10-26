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
    public static bool LegacyTreeResident,LegacyTreeShader,LegacyTreeWind;
    public static bool Measuring;
    public static readonly double[] Milliseconds=new double[4];
    public static readonly bool TraceEnabled=Enabled&&Environment.GetCommandLineArgs().Contains("--worldgen-stutter-trace");
    public static readonly double[] TraceMilliseconds=new double[17];static int traceFrame=-1;
    public static bool SuppressIdleScatter,SkipMapUploads,ForceFreshCoverCull,NoLocalTrees,ForceFreshForestOwnership,TreeLegacyAlpha,SuppressFarForest,ForceIndividualTreeAdmission,DrainEachFrame,LegacyTreeQueues;
    public static long TraceBegin()
    {
        if(!TraceEnabled)return 0;
        if(traceFrame!=Time.frameCount){traceFrame=Time.frameCount;Array.Clear(TraceMilliseconds,0,TraceMilliseconds.Length);}
        return System.Diagnostics.Stopwatch.GetTimestamp();
    }
    public static void TraceEnd(int slot,long started){if(started!=0)TraceMilliseconds[slot]+=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000d/System.Diagnostics.Stopwatch.Frequency;}
    public static long Begin()=>Enabled?System.Diagnostics.Stopwatch.GetTimestamp():0;
    public static void End(int slot,long start){if(start!=0)Milliseconds[slot]=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;}
}

public sealed partial class LocalTerrainExplorer
{
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
        var materials=new List<Material>(ownedScatterMaterials);if(groundCoverMaterial!=null)materials.Add(groundCoverMaterial);if(groundCanopyMaterial!=null)materials.Add(groundCanopyMaterial);
        foreach(var material in materials)if(material!=null&&material.HasProperty("_VegetationSceneEnabled"))
        {
            bool grass=material==groundCoverMaterial||material==groundCanopyMaterial;
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
                    var cached=new uint[5][];var counts=new GraphicsBuffer.IndirectDrawArgs[5];gpu.Args.GetData(counts);
                    for(int lod=0;lod<5;lod++){cached[lod]=new uint[counts[lod].instanceCount];if(cached[lod].Length>0)gpu.Visible[lod].GetData(cached[lod],0,0,cached[lod].Length);Array.Sort(cached[lod]);}
                    long before=GrassCullDispatches;CullGroundCover(gpu,origin,view,pair.Value,(float)pair.Key.SizeM);if(GrassCullDispatches!=before)throw new InvalidOperationException("Identical visibility inputs did not reuse grass lists");
                    WorldGenPerformance.ForceFreshCoverCull=true;CullGroundCover(gpu,origin,view,pair.Value,(float)pair.Key.SizeM);gpu.Args.GetData(counts);
                    for(int lod=0;lod<5;lod++)
                    {var fresh=new uint[counts[lod].instanceCount];if(fresh.Length>0)gpu.Visible[lod].GetData(fresh,0,0,fresh.Length);Array.Sort(fresh);if(!fresh.SequenceEqual(cached[lod]))throw new InvalidOperationException("Cached grass visibility changed admitted roots");compared++;}
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
                    if(prepared.Matrices[i][j]!=reference.Matrices[i][j])throw new InvalidOperationException("Prepared dense tree matrix differs from ordinary projection");
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
            LastCapturePixels=colors;if(path!=null)File.WriteAllBytes(path,pixels.EncodeToPNG());
        }
        finally{RenderTexture.active=prior;UnityEngine.Object.Destroy(pixels);}
    }
    public bool PerformanceSettled=>Ready&&NearReady&&AllReady&&ScatterReady&&!DetailPlanning&&jobs.Count==0&&scatterJobs.Count==0&&coverRequests.Count==0&&previousTerrain.Count==0&&retiringFrames.Count==0&&tiles.Values.All(t=>FadeComplete(t))&&(!TerrainVideoOptions.GeneratedTrees||!TerrainVideoOptions.FarForest||!TerrainVideoOptions.TreeRebuild||!TerrainVideoOptions.TreeImpostors||!ForestPending)&&(!TerrainVideoOptions.GeneratedTrees||!TerrainVideoOptions.TreeImpostors||PendingTreeModels==0);
    public string PerformanceForestState=>$"fields {ForestFieldCount}/{forestPriority.Length}, roots {ForestPacketCount}, pending {ForestPending}, baked local {BakedTreeModels}, baked forest {BakedForestModels}, pending models {PendingTreeModels}, texture bytes {TreeBakeBytes}";
    public void PerformanceInvalidate(){scatterDirty=true;}
    public void PerformanceShadows(bool enabled){if(localSun!=null)localSun.shadows=enabled?LightShadows.Soft:LightShadows.None;}
    public void PerformanceFreezeDaylight(){TerrainDiagnostics.FreezeSun=false;sunPoseWritten=false;SetDaylight(12,skyYear);TerrainDiagnostics.FreezeSun=true;}
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
        var prior=scatterGroundChanges.ToArray();int checkedRoots=0,repairedRoots=0,checkedChunks=0,coarseChecks=0;
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
                    scatterGroundChanges.Clear();scatterGroundChanges.Add(tile);
                    if(++checkedChunks>=4)break;
                }
                if(checkedChunks>=4)break;
            }
        }
        finally{scatterGroundChanges.Clear();foreach(var tile in prior)scatterGroundChanges.Add(tile);scatterDirty=true;}
        if(repairedRoots==0)throw new InvalidOperationException("Selective projection fixture did not touch actual roots");
        return "Selective/full ground projection: "+checkedRoots+" exact matrices, "+repairedRoots+" displaced roots repaired across "+checkedChunks+" chunks; "+coarseChecks+" coarse-parent ownership guards";
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
        public double Time,Frame,Update,Map,Scatter,Weather,Ground,Fog,Coverage,Sky,Batches,Bakes,Projection,Forest,Shadows,CoverDraw,Canopy,ForestDraw;public int Gc;public long Heap,MainAllocated;
        public override string ToString()=>string.Join(",",Perf(Time),Perf(Frame),Gc,Heap,MainAllocated,Perf(Update),Perf(Map),Perf(Scatter),Perf(Weather),Perf(Ground),Perf(Fog),Perf(Coverage),Perf(Sky),Perf(Batches),Perf(Bakes),Perf(Projection),Perf(Forest),Perf(Shadows),Perf(CoverDraw),Perf(Canopy),Perf(ForestDraw));
    }
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
        var sites=new[]{("plains",Pick(h=>h.Biome==Biome.Grassland||h.Biome==Biome.Savanna)),("savanna",Pick(h=>h.Biome==Biome.Savanna)),("forest-flat",Pick(h=>h.Biome==Biome.TemperateForest||h.Biome==Biome.TropicalRainforest)),("forest-hills",Pick(h=>h.Biome==Biome.TemperateForest||h.Biome==Biome.TropicalRainforest,true)),("mountains",Pick(h=>h.Biome==Biome.Alpine||h.Biome==Biome.BorealForest,true)),("desert",Pick(h=>h.Biome==Biome.Desert)),("tundra",Pick(h=>h.Biome==Biome.Tundra))};
        string filter=Argument("--worldgen-perf-sites","");
        string caseFilter=Argument("--worldgen-perf-cases","");
        double seconds=double.Parse(Argument("--worldgen-perf-seconds","6"),CultureInfo.InvariantCulture);
        double profileView=double.Parse(Argument("--worldgen-perf-view","60000"),CultureInfo.InvariantCulture);
        int profilePlants=int.Parse(Argument("--worldgen-perf-plant-quality","2"));
        var cases=new List<PerfCase>{
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
            new PerfCase("turning-depth-off",()=>TerrainVideoOptions.FoliageDepthPrepass=false),
            new PerfCase("turning-bands-off",()=>TerrainVideoOptions.FrontToBackTrees=false),
            new PerfCase("turning-depth-on",()=>TerrainVideoOptions.FoliageDepthPrepass=true),
            new PerfCase("travel-depth-off",()=>TerrainVideoOptions.FoliageDepthPrepass=false),
            new PerfCase("overlap-verify",()=>{}),
            new PerfCase("turning-uncached-ownership",()=>WorldGenPerformance.ForceFreshForestOwnership=true),
            new PerfCase("travel",()=>{}),
            new PerfCase("weather-off",()=>{var w=WeatherVideoOptions.Current;w.Clouds=0;w.SunGlare=w.SunRays=w.Adaptation=w.Visibility=0;})
        };
        if(!WorldGenPerformance.TraceEnabled)cases.RemoveAll(c=>c.Name.StartsWith("stutter-",StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(output,"machine.txt"),$"{SystemInfo.processorType}\n{SystemInfo.graphicsDeviceName}\n{SystemInfo.graphicsDeviceVersion}\nRAM {SystemInfo.systemMemorySize} MB VRAM {SystemInfo.graphicsMemorySize} MB\n{Screen.width}x{Screen.height}, {QualitySettings.activeColorSpace}, VSync {QualitySettings.vSyncCount}, target {Application.targetFrameRate}\nUnity {Application.unityVersion}\nUTC {DateTime.UtcNow:o}\nSeed {world.Recipe.Seed}; scale .1; clear noon; High terrain; view {profileView}m; fine radius153.6m; grass30/300m; default non-grass.\nCPU sections are submission/update times, NOT GPU times. PresentMon is the independent GPU/presentation measurement.\n");
        File.AppendAllText(Path.Combine(output,"machine.txt"),$"Forced single offscreen URP render {width}x{height}; automatic camera disabled. Frame timing stats enabled: {FrameTimingManager.IsFeatureEnabled()}. Original CPU paths: {WorldGenPerformance.Original}.\n");
        File.WriteAllText(Path.Combine(output,"summary.csv"),"site,cell,biome,relief,case,frames,seconds,fps,p50_ms,p95_ms,p99_ms,max_ms,over10ms,over16ms,scatter_update_ms,scatter_submit_ms,grass_submit_ms,weather_ms,tiles,wanted,objects,specialists,mid,trees,scatter_draws,grass_draws,grass_candidates,grass_bytes,frustum_rejected,terrain_rejected,settled,start_qpc,end_qpc,gpu_ms,cpu_ms,timing_samples,gpu_drain_ms\n");
        foreach(var site in sites)
        {
            if(site.Item2<0||filter.Length>0&&!filter.Split(',').Contains(site.Item1))continue;
            void Defaults(){var w=WeatherVideoOptions.Current;w.Clouds=1;w.SunGlare=.35f;w.SunRays=.12f;w.Adaptation=.5f;w.Visibility=1;TerrainVideoOptions.ResetVegetation();TerrainVideoOptions.GroundCover=true;TerrainVideoOptions.GrassDensity=1;TerrainVideoOptions.GrassDetail=2;TerrainVideoOptions.GrassBladeDistance=30;TerrainVideoOptions.GrassDistance=300;TerrainVideoOptions.GrassCanopy=true;TerrainVideoOptions.Current.Quality=2;TerrainVideoOptions.Current.Effort=1;TerrainVideoOptions.Current.ViewDistanceM=profileView;TerrainVideoOptions.Current.FullDetailTiles=1.5;TerrainVideoOptions.ApplyAllPlantQuality(profilePlants);}
            Defaults();TerrainVideoOptions.Revision++;selectedCell=site.Item2;ExploreTerrain(world.Surface.Cells[site.Item2].Position);
            deadline=Time.realtimeSinceStartupAsDouble+240;while(terrainExplorer!=null&&!terrainExplorer.Ready&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(terrainExplorer==null||!terrainExplorer.Ready){Debug.LogError("PERF terrain unavailable");Application.Quit(1);yield break;}
            terrainExplorer.PerformanceDrySite();terrainExplorer.TestView(1.65,20,3);terrainExplorer.PerformanceRender(width,height);
            TerrainMapViewport.Degrees(terrainExplorer.Position,out double lat,out double lon);play.SetLocalSolarHour(12,lon);
            PlayCommands.Execute(play,0,"/weather clear",weather:terrainExplorer.WeatherControl);
            File.AppendAllText(Path.Combine(output,"sites.txt"),$"{site.Item1} cell {site.Item2} {lat:R},{lon:R}; biome {world.Ecology.Habitat[site.Item2].Biome}; relief {world.Ecology.Habitat[site.Item2].ReliefM}\n");
            if(WorldGenPerformance.TraceEnabled)File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyMapMaterials()+"\n");
            bool previousTerrain=false;
            foreach(var item in cases)
            {
                if(caseFilter.Length>0&&!caseFilter.Split(',').Contains(item.Name))continue;
                terrainExplorer.PerformanceMap(false);WorldGenPerformance.SuppressIdleScatter=WorldGenPerformance.SkipMapUploads=WorldGenPerformance.ForceFreshCoverCull=WorldGenPerformance.NoLocalTrees=WorldGenPerformance.ForceFreshForestOwnership=WorldGenPerformance.TreeLegacyAlpha=WorldGenPerformance.SuppressFarForest=WorldGenPerformance.ForceIndividualTreeAdmission=WorldGenPerformance.DrainEachFrame=WorldGenPerformance.LegacyTreeQueues=false;
                WorldGenPerformance.LegacyTreeResident=WorldGenPerformance.LegacyTreeShader=WorldGenPerformance.LegacyTreeWind=false;Defaults();WorldGenPerformance.OriginalCase=false;terrainExplorer.PerformancePose();terrainExplorer.ScatterEnabled=true;terrainExplorer.PerformanceShadows(true);item.Apply();TerrainVideoOptions.VegetationRevision++;terrainExplorer.PerformanceInvalidate();
                terrainExplorer.PerformanceTreeAlpha(!WorldGenPerformance.TreeLegacyAlpha);
                terrainExplorer.PerformanceTreeQueues(!WorldGenPerformance.LegacyTreeQueues);
                if(item.Terrain||previousTerrain)TerrainVideoOptions.Revision++;previousTerrain=item.Terrain;
                // Readiness is explicit; partial scenes are labelled, never silently accepted.
                yield return null;deadline=Time.realtimeSinceStartupAsDouble+(item.Name=="forest-ready"?900:item.Name=="view-200km"||item.Name=="full"||item.Terrain&&terrainExplorer.WantedCount>1000?420:150);
                double progressAt=0;
                while(!terrainExplorer.PerformanceSettled&&terrainExplorer.Error==""&&Time.realtimeSinceStartupAsDouble<deadline)
                {
                    if(Time.realtimeSinceStartupAsDouble>=progressAt)
                    {progressAt=Time.realtimeSinceStartupAsDouble+5;File.AppendAllText(Path.Combine(output,"loading-progress.txt"),site.Item1+" "+item.Name+" t="+Time.realtimeSinceStartupAsDouble.ToString("F1")+" tiles "+terrainExplorer.TileCount+"/"+terrainExplorer.WantedCount+"; "+terrainExplorer.PerformanceForestState+"\n");}
                    yield return null;
                }
                if(terrainExplorer.Error!=""){Debug.LogError("PERF "+terrainExplorer.Error);Application.Quit(1);yield break;}
                play.SetLocalSolarHour(12,lon);terrainExplorer.PerformanceFreezeDaylight();yield return new WaitForSecondsRealtime(4);
                if(item.Name=="stutter-idle")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyRenderCleanup()+"\n"+terrainExplorer.PerformanceVerifyVegetationScene()+"\n"+terrainExplorer.PerformanceVerifyGrassCullCache()+"\n");
                if(item.Name=="forest-ready"){File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyForestIdentity()+"\n"+terrainExplorer.PerformanceVerifyTreeAlpha()+"\n"+terrainExplorer.PerformanceVerifyTreeGroupAdmission()+"\n"+terrainExplorer.PerformanceVerifyTreeQueues(Path.Combine(output,site.Item1+"-queues"))+"\n");terrainExplorer.PerformanceCapture(Path.Combine(output,site.Item1+"-forest.png"));}
                if(item.Name=="trees-efficient-verify")File.AppendAllText(Path.Combine(output,"equivalence.txt"),terrainExplorer.PerformanceVerifyTreeEfficiency(output)+"\n");
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
                    yield return new WaitForSecondsRealtime(2);terrainExplorer.PerformanceCapture(Path.Combine(output,site.Item1+".png"));yield return new WaitForSecondsRealtime(1);
                }
                if(item.Name=="flying-60m"){terrainExplorer.PerformanceCapture(Path.Combine(output,site.Item1+"-flying.png"));yield return new WaitForSecondsRealtime(1);}
                var frames=new List<double>(10000);var sections=new double[4];int gc=GC.CollectionCount(0);
                var stutter=WorldGenPerformance.TraceEnabled?new List<StutterRow>(30000):null;
                long heap=0;double heapAt=0;var endFrame=stutter==null?null:new WaitForEndOfFrame();
                var timings=new FrameTiming[1];double gpu=0,cpu=0;int timingCount=0;ulong lastTiming=0;
                terrainExplorer.WeatherCheckGpuBarrier(); // Drain pre-window work before counting renders.
                WorldGenPerformance.Measuring=true;
                long qpcStart=System.Diagnostics.Stopwatch.GetTimestamp();double start=Time.realtimeSinceStartupAsDouble,previous=start;
                Debug.Log("PERF BEGIN "+site.Item1+" "+item.Name+" qpc="+qpcStart);
                while(Time.realtimeSinceStartupAsDouble-start<seconds)
                {
                    yield return endFrame;
                    // DIAGNOSTIC ONLY: distinguish unbounded offscreen queueing
                    // from work that remains slow when every render completes.
                    // This is intentionally synchronous and never normal play.
                    if(WorldGenPerformance.DrainEachFrame)terrainExplorer.WeatherCheckGpuBarrier();
                    double now=Time.realtimeSinceStartupAsDouble;double frameMs=(now-previous)*1000;frames.Add(frameMs);previous=now;
                    if(stutter!=null)
                    {
                        if(now>=heapAt){heap=GC.GetTotalMemory(false);heapAt=now+.25;}
                        var t=WorldGenPerformance.TraceMilliseconds;
                        stutter.Add(new StutterRow{Time=now-start,Frame=frameMs,Gc=GC.CollectionCount(0),Heap=heap,MainAllocated=GC.GetAllocatedBytesForCurrentThread(),Update=t[0],Map=t[1],Scatter=t[2],Weather=t[3],Ground=t[4],Fog=t[5],Coverage=t[6],Sky=t[7],Batches=t[9],Bakes=t[10],Projection=t[11],Forest=t[12],Shadows=t[13],CoverDraw=t[14],Canopy=t[15],ForestDraw=t[16]});
                    }
                    if(item.Name.StartsWith("turning",StringComparison.Ordinal))terrainExplorer.PerformanceTurn(20+(float)((now-start)*60));
                    if(item.Name.StartsWith("travel",StringComparison.Ordinal))terrainExplorer.PerformanceTravel((now-start)*8);
                    for(int j=0;j<4;j++)sections[j]+=WorldGenPerformance.Milliseconds[j];
                    FrameTimingManager.CaptureFrameTimings();
                    if(FrameTimingManager.GetLatestTimings(1,timings)>0&&timings[0].frameStartTimestamp!=lastTiming&&timings[0].gpuFrameTime>0)
                    {lastTiming=timings[0].frameStartTimestamp;gpu+=timings[0].gpuFrameTime;cpu+=timings[0].cpuFrameTime;timingCount++;}
                }
                double drainStart=Time.realtimeSinceStartupAsDouble;
                terrainExplorer.WeatherCheckGpuBarrier(); // Include completion, not just CPU queueing, in mean FPS.
                double gpuDrain=(Time.realtimeSinceStartupAsDouble-drainStart)*1000;
                WorldGenPerformance.Measuring=false;
                if(terrainExplorer.Error!="")throw new InvalidOperationException("Performance scene: "+terrainExplorer.Error);
                if(WorldGenPerformance.TraceEnabled&&item.Name.StartsWith("stutter-map",StringComparison.Ordinal))File.AppendAllText(Path.Combine(output,"map-progress.txt"),item.Name+": "+terrainExplorer.PerformanceMapDiagnostics+"\n");
                long qpcEnd=System.Diagnostics.Stopwatch.GetTimestamp();double elapsed=Time.realtimeSinceStartupAsDouble-start;var sorted=frames.OrderBy(v=>v).ToArray();
                double P(double p)=>sorted[Math.Min(sorted.Length-1,(int)(sorted.Length*p))];
                var h=world.Ecology.Habitat[site.Item2];
                string row=string.Join(",",site.Item1,site.Item2,h.Biome,h.ReliefM,item.Name,frames.Count,Perf(elapsed),Perf(frames.Count/elapsed),Perf(P(.5)),Perf(P(.95)),Perf(P(.99)),Perf(sorted.Last()),frames.Count(v=>v>10),frames.Count(v=>v>16.667),Perf(sections[0]/frames.Count),Perf(sections[1]/frames.Count),Perf(sections[2]/frames.Count),Perf(sections[3]/frames.Count),terrainExplorer.PerformanceCounts,terrainExplorer.PerformanceSettled,qpcStart,qpcEnd);
                row+=","+Perf(gpu/Math.Max(1,timingCount))+","+Perf(cpu/Math.Max(1,timingCount))+","+timingCount+","+Perf(gpuDrain);
                File.AppendAllText(Path.Combine(output,"summary.csv"),row+"\n");
                File.AppendAllText(Path.Combine(output,"forest-state.txt"),site.Item1+" "+item.Name+": "+terrainExplorer.PerformanceForestState+"\n");
                File.WriteAllLines(Path.Combine(output,site.Item1+"-"+item.Name+"-frames.csv"),new[]{"ms"}.Concat(frames.Select(Perf)));
                if(stutter!=null)File.WriteAllLines(Path.Combine(output,site.Item1+"-"+item.Name+"-trace.csv"),new[]{"t_s,frame_ms,gc0,heap_bytes,allocated_main_total,explorer_ms,map_ms,scatter_ms,weather_ms,ground_ms,fog_ms,coverage_ms,sky_ms,batches_ms,bakes_ms,projection_ms,forest_ms,shadow_batches_ms,cover_draw_ms,canopy_ms,forest_draw_ms"}.Concat(stutter.Select(s=>s.ToString())));
                Debug.Log("PERF END "+row+" gc0="+(GC.CollectionCount(0)-gc));
            }
            File.AppendAllText(Path.Combine(output,"equivalence.txt"),site.Item1+": "+terrainExplorer.PerformancePendingComparisons+" loading/idle readiness comparisons passed.\n");
            terrainExplorer.Close();yield return null;yield return null;
        }
        File.WriteAllText(Path.Combine(output,"complete.txt"),DateTime.UtcNow.ToString("o"));Application.Quit(0);
    }
}
