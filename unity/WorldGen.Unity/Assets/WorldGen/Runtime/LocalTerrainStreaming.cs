using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    TerrainLru<TerrainProductKey,Tile> terrainProducts;
    readonly TerrainSampleCache localSampleCache=new TerrainSampleCache();
    Dictionary<TerrainTileKey,int> prefetchWanted=new Dictionary<TerrainTileKey,int>();
    Dictionary<TerrainTileKey,double[]> prefetchEdges=new Dictionary<TerrainTileKey,double[]>();
    readonly List<TerrainTileKey> prefetchPriority=new List<TerrainTileKey>();
    double streamingVX,streamingVZ,predictedX=double.NaN,predictedZ,predictionDue;
    int installationsThisFrame;
    public long TerrainCacheHits {get;private set;}public long TerrainBuildRequests {get;private set;}public long TerrainPrefetchRequests {get;private set;}
    public int CachedTerrainTiles=>terrainProducts?.Count??0;
    public long CachedTerrainBytes=>terrainProducts?.Bytes??0;
    public void TestStreamingVelocity(double x,double z){streamingVX=x;streamingVZ=z;predictedX=double.NaN;predictionDue=0;RefreshStreamingPrediction();}
    TerrainLru<TerrainProductKey,Tile> TerrainProducts=>terrainProducts??(terrainProducts=new TerrainLru<TerrainProductKey,Tile>(96L*1024*1024,128,RetireCachedTile));
    void RetireCachedTile(Tile tile)
    {
        if(closed){DestroyTile(tile);return;}
        // Detach disabled cache products so teleport/chart-root destruction
        // cannot destroy their objects before we explicitly release meshes.
        if(tile.Root!=null){tile.Root.SetActive(false);tile.Root.transform.SetParent(null,false);}
        retiringFrames.Enqueue(new RetiringFrame{Tiles=new Queue<Tile>(new[]{tile})});
    }
    void CacheTerrain(TerrainTileKey key,Tile tile)
    {
        ReleaseGroundCover(tile);
        tile.Root.SetActive(false);tile.CoverageNodes=null;
        if(tile.Coverage!=null){Destroy(tile.Coverage);tile.Coverage=null;}
        if(tile.CoverageTimes!=null){Destroy(tile.CoverageTimes);tile.CoverageTimes=null;}tile.CoverageTimingNodes=null;tile.FadeIn=tile.FadeOut=0;
        TerrainProducts.Put(new TerrainProductKey(key,tile.Segments,tile.Edges),tile,tile.BufferBytes);coverageDirty=true;
    }
    bool RestoreTerrain(TerrainTileKey key,int segments,double[] edges)
    {
        if(!CanInstall||!TerrainProducts.Take(new TerrainProductKey(key,segments,edges,false),out var tile))return false;
        tiles.TryGetValue(key,out var old);BeginTerrainReplacement(key,tile,old);
        tile.Root.transform.SetParent(terrainRoot.transform,false);
        tile.Root.transform.localPosition=new Vector3((float)(key.X*key.SizeM-originX),0,(float)(key.Z*key.SizeM-originZ));
        tiles[key]=tile;residentRevision++;tile.Root.SetActive(true);coverageDirty=true;installationsThisFrame++;TerrainCacheHits++;
        scatterGroundRevision++;scatterGroundChanges.Add(key);return true;
    }
    void ResetStreamingReuse(bool cancelJobs)
    {
        ClearTerrainTransitions();
        terrainProducts?.Clear();streamingVX=streamingVZ=0;predictedX=double.NaN;
        prefetchWanted.Clear();prefetchEdges.Clear();prefetchPriority.Clear();
        if(cancelJobs)foreach(var job in jobs)job.Cancellation?.Cancel();
    }
    void TrackStreamingMotion(double dx,double dz,double dt)
    {
        if(dt<=0)return;double blend=1-Math.Exp(-Math.Min(.25,dt)*8);
        streamingVX+=(dx/dt-streamingVX)*blend;streamingVZ+=(dz/dt-streamingVZ)*blend;
    }
    double StreamingPriority(TerrainTileKey key)
    {
        if(ImmediateTerrain(key))return TerrainLod.DistanceSquared(key,motion.X,motion.Z);
        if(backdrops.ContainsKey(key)&&!tiles.ContainsKey(key))return 1e10+TerrainLod.DistanceSquared(key,motion.X,motion.Z)*.001;
        double baseSize=TerrainLod.BaseSizeM*HorizontalScale;
        double d=TerrainLod.DistanceSquared(key,motion.X,motion.Z),future=double.IsNaN(predictedX)?d:TerrainLod.DistanceSquared(key,predictedX,predictedZ);
        return baseSize*baseSize*100+Math.Min(d,future*1.2);
    }
    void RefreshStreamingPrediction()
    {
        if(Time.realtimeSinceStartupAsDouble<predictionDue)return;predictionDue=Time.realtimeSinceStartupAsDouble+.2;
        double speed=Math.Sqrt(streamingVX*streamingVX+streamingVZ*streamingVZ),size=TerrainLod.BaseSizeM*HorizontalScale;
        if(speed<.5)
        {
            if(prefetchWanted.Count>0||!double.IsNaN(predictedX))
            {
                predictedX=double.NaN;prefetchWanted.Clear();prefetchPriority.Clear();foreach(var j in jobs)if(j.Prefetch)j.Cancellation?.Cancel();
                priority.Sort((a,b)=>StreamingPriority(a).CompareTo(StreamingPriority(b)));
            }
            return;
        }
        double lead=Math.Min(size*6,Math.Max(size*.6,speed*.75));double x=motion.X+streamingVX/speed*lead,z=motion.Z+streamingVZ/speed*lead;
        if(double.IsNaN(predictedX)||Math.Abs(x-predictedX)>size*.25||Math.Abs(z-predictedZ)>size*.25)
        {
            predictedX=x;predictedZ=z;
            var plan=TerrainLod.Plan(x,z,HeightAboveGround,size*4,HorizontalScale);prefetchEdges=TerrainEdges.Build(plan);
            prefetchWanted=plan.Where(p=>TerrainLod.Immediate(p.Key,x,z)).OrderBy(p=>TerrainLod.DistanceSquared(p.Key,x,z)).Take(12).ToDictionary(p=>p.Key,p=>p.Value);
            prefetchPriority.Clear();prefetchPriority.AddRange(prefetchWanted.Keys);
            foreach(var j in jobs)if(j.Prefetch&&(!prefetchWanted.TryGetValue(j.Key,out int detail)||detail!=j.Segments||!TerrainEdges.Equal(j.Edges,prefetchEdges[j.Key])))j.Cancellation?.Cancel();
        }
        priority.Sort((a,b)=>{int d=StreamingPriority(a).CompareTo(StreamingPriority(b));return d!=0?d:a.Level!=b.Level?a.Level.CompareTo(b.Level):a.X!=b.X?a.X.CompareTo(b.X):a.Z.CompareTo(b.Z);});
    }
    bool WorkMatchesCurrent(Work job)=>job.Prepared==null&&job.Generation==generation&&
        (job.Backdrop?backdrops.ContainsKey(job.Key)&&!tiles.ContainsKey(job.Key)&&BackdropNeeded(job.Key):wanted.TryGetValue(job.Key,out int detail)&&detail==job.Segments&&TerrainEdges.Equal(job.Edges,wantedEdges[job.Key]));
    void RequestTerrain(TerrainTileKey key,int detail,double[] edges,bool prefetch=false,bool backdrop=false)
    {
        var buildFrame=frame;var buildTerrain=terrain;var producer=meshProcess;
        var stop=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);var token=stop.Token;
        TerrainBuildRequests++;if(prefetch)TerrainPrefetchRequests++;nextRequestAt=Time.realtimeSinceStartupAsDouble+detailOptions.RequestInterval;
        jobs.Add(new Work{Key=key,Edges=edges,Segments=detail,Generation=generation,Prefetch=prefetch,Backdrop=backdrop,Cancellation=stop,
            Task=Task.Run(()=>TerrainUpload.Prepare(producer==null?LocalTerrainMesh.Build(buildTerrain,buildFrame,key.X*key.SizeM,key.Z*key.SizeM,key.SizeM,detail,token,edges,localSampleCache):producer.Build(buildFrame,key.X*key.SizeM,key.Z*key.SizeM,key.SizeM,detail,token,edges),token),token)});
    }
    void SchedulePredictiveTerrain()
    {
        if(!CanRequest||NearTerrainPending()||ActiveWorkerCount>=WorkerLimit||jobs.Any(j=>j.Prefetch))return;
        foreach(var key in prefetchPriority)
        {
            int detail=prefetchWanted[key];var edges=prefetchEdges[key];
            if(wanted.ContainsKey(key)||tiles.ContainsKey(key)||TerrainProducts.Contains(new TerrainProductKey(key,detail,edges,false))||jobs.Any(j=>j.Prepared==null&&j.Key.Equals(key)))continue;
            RequestTerrain(key,detail,edges,true);break;
        }
    }
}
