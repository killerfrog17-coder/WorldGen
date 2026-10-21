using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    // One hidden replacement, not a cache or a second visible LOD hierarchy.
    sealed class PreparedFrame
    {
        public LocalTerrainFrame Frame;public GameObject Root;
        public readonly Dictionary<TerrainTileKey,Tile> Tiles=new Dictionary<TerrainTileKey,Tile>();
        public Dictionary<TerrainTileKey,int> Wanted;
        public readonly Dictionary<TerrainTileKey,double[]> Edges=new Dictionary<TerrainTileKey,double[]>();
        public readonly List<TerrainTileKey> Priority=new List<TerrainTileKey>();
        public readonly List<TerrainTileKey> CoveragePriority=new List<TerrainTileKey>();
        public double X=double.NaN,Z,Altitude,SortAt,Range;
        public int Revision,ScatterRevision=-1;
        public double ScatterOriginX,ScatterOriginZ;
        public readonly Dictionary<ScatterKey,ScatterChunk> Scatter=new Dictionary<ScatterKey,ScatterChunk>();
    }
    sealed class RetiringFrame {public GameObject Root;public Queue<Tile> Tiles;}
    PreparedFrame preparedFrame;
    readonly Queue<RetiringFrame> retiringFrames=new Queue<RetiringFrame>();
    double nextHandoffCheck;
    public int ReanchorCount {get;private set;}
    public double LargestHandoffMs {get;private set;}
    public double LargestUploadMs {get;private set;}
    public string LargestHandoffProfile {get;private set;}="";
    public int PreparedTileCount=>preparedFrame?.Tiles.Count??0;
    public double ReanchorDistance=>Math.Max(Math.Abs(motion.X),Math.Abs(motion.Z));
    public double ReanchorLimit=>frame==null?0:TerrainHandoff.Limit(frame);
    public double RenderedFootClearance
    {
        get
        {
            double highest=double.NegativeInfinity,x=motion.X,z=motion.Z;
            // Inspect every overlapping rendered land surface, not merely the
            // finest cached mesh: an erroneously retained parent could hide it.
            foreach(var pair in tiles)
            {
                var key=pair.Key;var tile=pair.Value;double size=key.SizeM;
                if(!tile.Root.activeInHierarchy||x<key.X*size||x>(key.X+1)*size||z<key.Z*size||z>(key.Z+1)*size)continue;
                int n=tile.Segments,side=n+1;double gx=(x/size-key.X)*n,gz=(z/size-key.Z)*n;
                int a=Math.Min(n-1,Math.Max(0,(int)gx)),b=Math.Min(n-1,Math.Max(0,(int)gz));double u=gx-a,v=gz-b;
                double h00=tile.Heights[b*side+a],h10=tile.Heights[b*side+a+1],h01=tile.Heights[(b+1)*side+a],h11=tile.Heights[(b+1)*side+a+1];
                highest=Math.Max(highest,u+v<=1?h00+(h10-h00)*u+(h01-h00)*v:h11+(h01-h11)*(1-u)+(h10-h11)*(1-v));
            }
            return motion.Y-(double.IsNegativeInfinity(highest)?Ground(x,z):highest);
        }
    }
    public bool TerrainVisible=>terrainRoot!=null&&terrainRoot.activeInHierarchy&&tiles.Count>0;
    public bool TestHoldHandoff,TestHandoffWaiting;
    public string TestHandoffDiagnostics()
    {
        var p=preparedFrame;if(p?.Wanted==null)return "no preparation; active jobs "+jobs.Count;
        TerrainHandoff.Coordinates(p.Frame,Position,out double x,out double z);
        var resident=p.Tiles.ToDictionary(pair=>pair.Key,pair=>pair.Value.Segments);
        return "tiles "+p.Tiles.Count+"/"+p.Wanted.Count+"; pending jobs "+jobs.Count(j=>j.Prepared!=null)+
            "; coverage "+TerrainHandoff.Ready(p.Wanted,resident,1e20,1e20,0)+
            "; near ready "+TerrainHandoff.Ready(p.Wanted,resident,x,z,motion.Flying?0:2048*HorizontalScale);
    }
    public void TestAdvanceEast(double metres){motion.X+=metres;motion.Y=Math.Max(motion.Y,Ground(motion.X,motion.Z)+450);}
    public void TestBoundaryGround()
    {
        double x=ReanchorLimit-6;
        for(int j=0;j<101;j++)
        {
            double z=(j%2==0?1:-1)*((j+1)/2)*80;
            var at=frame.Position(x,z);var patch=terrain.Patch(at,500/HorizontalScale);
            var sample=patch.Sample(at);if(sample.Underwater||sample.ElevationM<10)continue;
            bool gentle=true;
            foreach(double d in new[]{-30d,30d})
                if(Math.Abs(patch.Sample(frame.Position(x+d,z)).ElevationM-sample.ElevationM)>.6)gentle=false;
            if(!gentle)continue;motion.X=x;motion.Z=z;walkingPatch=null;motion.Y=Ground(x,z)+1.65;return;
        }
        throw new InvalidOperationException("No gentle boundary review location.");
    }
    GameObject NewTerrainRoot(bool visible)
    {
        var root=new GameObject(visible?"Visible terrain frame":"Preparing terrain frame");
        root.transform.SetParent(transform,false);root.SetActive(visible);return root;
    }
    void CancelPreparedFrame()
    {
        if(preparedFrame==null)return;
        RetireFrame(preparedFrame.Root,preparedFrame.Tiles);preparedFrame=null;
    }
    void RetireFrame(GameObject root,Dictionary<TerrainTileKey,Tile> products)
    {
        if(root!=null)root.SetActive(false);
        retiringFrames.Enqueue(new RetiringFrame{Root=root,Tiles=new Queue<Tile>(products.Values)});
    }
    void RetireTerrainGradually()
    {
        if(retiringFrames.Count==0)return;
        var old=retiringFrames.Peek();
        // Unity object destruction and driver resource release are spread out.
        for(int i=0;i<2&&old.Tiles.Count>0;i++)DestroyTile(old.Tiles.Dequeue());
        if(old.Tiles.Count==0){if(old.Root!=null)Destroy(old.Root);retiringFrames.Dequeue();}
    }
    void DisposeRetiringTerrain()
    {
        while(retiringFrames.Count>0)
        {
            var old=retiringFrames.Dequeue();foreach(var tile in old.Tiles)DestroyTile(tile);
            if(old.Root!=null)Destroy(old.Root);
        }
    }
    void UpdatePreparedFrame()
    {
        TestHandoffWaiting=false;
        if(terrain==null)return;
        double limit=TerrainHandoff.Limit(frame),distance=ReanchorDistance;
        if(preparedFrame==null)
        {
            if(distance<limit*.5||retiringFrames.Count>0)return;
            // Aim the next chart toward the approaching boundary, not behind
            // the traveller. No change to the current frame or loaded products.
            double lead=distance<limit?limit/distance:1;
            var point=frame.Position(motion.X*lead,motion.Z*lead);
            var next=new LocalTerrainFrame(point,terrain.RadiusM,terrain.Patch(point).Sample(0,0).ElevationM,HorizontalScale);
            preparedFrame=new PreparedFrame{Frame=next,Root=NewTerrainRoot(false)};
        }
        var pending=preparedFrame;
        if(V.Dot(Position,pending.Frame.Origin)<=0){CancelPreparedFrame();return;}
        TerrainHandoff.Coordinates(pending.Frame,Position,out double x,out double z);
        double altitude=HeightAboveGround;
        double preparationStep=TerrainLod.ReplanDistance(HorizontalScale);
        if(double.IsNaN(pending.X)||pending.Range!=viewDistance||Math.Abs(x-pending.X)>preparationStep||Math.Abs(z-pending.Z)>preparationStep||
            Math.Abs(altitude-pending.Altitude)>Math.Max(150,pending.Altitude*.3))
        {
            pending.X=x;pending.Z=z;pending.Altitude=altitude;pending.Range=viewDistance;
            pending.Wanted=TerrainLod.Plan(x,z,altitude,viewDistance,HorizontalScale);
            pending.Priority.Clear();pending.Priority.AddRange(pending.Wanted.Keys);pending.Edges.Clear();
            foreach(var key in pending.Priority)pending.Edges[key]=TerrainLod.EdgeSteps(key,pending.Wanted);
            pending.CoveragePriority.Clear();pending.CoveragePriority.AddRange(pending.Priority);
            pending.CoveragePriority.Sort((a,b)=>b.Level.CompareTo(a.Level));pending.SortAt=0;
            foreach(var key in pending.Tiles.Keys.Where(k=>!pending.Wanted.ContainsKey(k)&&!pending.Wanted.Keys.Any(w=>Overlaps(k,w))).ToArray())
            {DestroyTile(pending.Tiles[key]);pending.Tiles.Remove(key);pending.Revision++;}
            // Same replacement rule as the active renderer: do not accumulate
            // a trail of obsolete fine products in the hidden frame.
            foreach(var key in pending.Tiles.Keys.Where(k=>!pending.Wanted.ContainsKey(k)).ToArray())
                if(pending.Wanted.Where(w=>Overlaps(key,w.Key)).All(w=>pending.Tiles.TryGetValue(w.Key,out var replacement)&&replacement.Segments==w.Value))
                {DestroyTile(pending.Tiles[key]);pending.Tiles.Remove(key);pending.Revision++;}
        }
        if(Time.realtimeSinceStartupAsDouble>=pending.SortAt)
        {
            pending.SortAt=Time.realtimeSinceStartupAsDouble+.1;
            double Distance(TerrainTileKey k)=>TerrainLod.DistanceSquared(k,x,z);
            pending.Priority.Sort((a,b)=>Distance(a).CompareTo(Distance(b)));
        }
        if(distance<limit||Time.realtimeSinceStartupAsDouble<nextHandoffCheck)return;
        nextHandoffCheck=Time.realtimeSinceStartupAsDouble+.1;
        // Turning back or travelling in a completely different direction must
        // not hand over into a worse chart, nor accumulate hidden charts.
        if(Math.Max(Math.Abs(x),Math.Abs(z))>=distance*.85){CancelPreparedFrame();return;}
        var resident=new Dictionary<TerrainTileKey,int>(pending.Tiles.Count);
        foreach(var pair in pending.Tiles)resident.Add(pair.Key,pair.Value.Segments);
        double near=motion.Flying?0:2048*HorizontalScale;
        foreach(var pair in pending.Wanted)
        {
            var key=pair.Key;
            double dx=Math.Max(Math.Max(key.X*key.SizeM-x,0),x-(key.X+1)*key.SizeM);
            double dz=Math.Max(Math.Max(key.Z*key.SizeM-z,0),z-(key.Z+1)*key.SizeM);
            if(dx*dx+dz*dz<=near*near&&
                (!pending.Tiles.TryGetValue(key,out var tile)||!tile.Edges.SequenceEqual(pending.Edges[key])))return;
        }
        if(!TerrainHandoff.Ready(pending.Wanted,resident,x,z,near))return;
        if(!motion.Flying&&!PrepareGroundScatter(pending,x,z))return;
        if(TestHoldHandoff){TestHandoffWaiting=true;return;}
        CommitPreparedFrame(pending,x,z);
    }
    void SchedulePreparedWork(bool reserveCurrentSlot)
    {
        var pending=preparedFrame;if(pending?.Wanted==null||NearTerrainPending())return;
        if(reserveCurrentSlot&&jobs.Any(j=>j.Prepared!=null))return;
        // First fill the actual requested coarse distance leaves (not extra
        // roots). Refinement churn near a fast traveller must not starve them.
        var order=pending.CoveragePriority.Where(k=>!pending.Tiles.ContainsKey(k)&&k.Level>=4).Concat(pending.Priority);
        foreach(var key in order)
        {
            if(jobs.Count+scatterJobs.Count>=2)return;
            int detail=pending.Wanted[key];var edges=pending.Edges[key];
            if(pending.Tiles.TryGetValue(key,out var tile)&&tile.Segments==detail&&tile.Edges.SequenceEqual(edges)||
                jobs.Any(j=>ReferenceEquals(j.Prepared,pending)&&j.Key.Equals(key)))continue;
            var source=terrain;var buildFrame=pending.Frame;var jobCancellation=System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);var token=jobCancellation.Token;
            var producer=meshProcess;
            jobs.Add(new Work{Key=key,Edges=edges,Segments=detail,Generation=generation,Prepared=pending,Cancellation=jobCancellation,
                Task=Task.Run(()=>TerrainUpload.Prepare(producer==null?LocalTerrainMesh.Build(source,buildFrame,key.X*key.SizeM,key.Z*key.SizeM,key.SizeM,detail,token,edges):producer.Build(buildFrame,key.X*key.SizeM,key.Z*key.SizeM,key.SizeM,detail,token,edges),token),token)});
            if(reserveCurrentSlot)return;
        }
    }
    void CommitPreparedFrame(PreparedFrame pending,double x,double z)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        double stamp=0;var profile=new System.Text.StringBuilder();
        void Mark(string name){double now=timer.Elapsed.TotalMilliseconds;profile.Append(name+" "+(now-stamp).ToString("F2")+"ms; ");stamp=now;}
        double above=HeightAboveGround;var oldFrame=frame;double yaw=heading*Math.PI/180,tilt=pitch*Math.PI/180;
        V direction=(oldFrame.East*Math.Sin(yaw)+oldFrame.North*Math.Cos(yaw))*Math.Cos(tilt)-oldFrame.Origin*Math.Sin(tilt);
        V? scatterPlan=double.IsNaN(scatterX)?(V?)null:oldFrame.Position(scatterX,scatterZ);
        var oldRoot=terrainRoot;var oldTiles=tiles;
        frame=pending.Frame;tiles=pending.Tiles;terrainRoot=pending.Root;preparedFrame=null;
        generation++;walkingPatch=null;originX=Math.Floor(x/2048)*2048;originZ=Math.Floor(z/2048)*2048;
        motion.X=x;motion.Z=z;motion.Y=Ground(x,z)+above; // Preserve gait, jump velocity and fly state.
        stepX=motion.X;stepY=motion.Y;stepZ=motion.Z; // Coordinate change is not a travelled footstep.
        heading=(float)(Math.Atan2(V.Dot(direction,frame.East),V.Dot(direction,frame.North))*180/Math.PI);
        pitch=(float)(-Math.Asin(Math.Max(-1,Math.Min(1,V.Dot(direction,frame.Origin))))*180/Math.PI);
        // The geographic scatter region did not move. Retain its plan and
        // express its last refresh position in the new chart, rather than
        // querying/sorting the same region again at the boundary.
        if(scatterPlan.HasValue)TerrainHandoff.Coordinates(frame,scatterPlan.Value,out scatterX,out scatterZ);
        foreach(var pair in tiles)
            pair.Value.Root.transform.localPosition=new Vector3((float)(pair.Key.X*pair.Key.SizeM-originX),0,(float)(pair.Key.Z*pair.Key.SizeM-originZ));
        landMaterial.SetVector("_LocalOrigin",new Vector4((float)originX,(float)originZ,0,0));
        // The replacement was already planned. Do not repeat its quadratic edge
        // stitching/LOD planning on the handoff frame.
        wanted=pending.Wanted;wantedEdges.Clear();foreach(var pair in pending.Edges)wantedEdges.Add(pair.Key,pair.Value);
        priority.Clear();priority.AddRange(pending.Priority);plannedX=pending.X;plannedZ=pending.Z;plannedAltitude=pending.Altitude;
        UpdateCamera();Mark("frame/plan");
        AdoptScatterFrame(pending,!motion.Flying);
        Mark("scatter");
        ResetWeatherFrame();SetDaylight(LocalSolarHour,skyYear);Mark("sky/weather");
        // Both roots switch in the same Update. Never render an empty frame.
        terrainRoot.SetActive(true);coverageDirty=true;RetireFrame(oldRoot,oldTiles);ReanchorCount++;
        Mark("activate");
        if(timer.Elapsed.TotalMilliseconds>LargestHandoffMs){LargestHandoffMs=timer.Elapsed.TotalMilliseconds;LargestHandoffProfile=profile.ToString();}
    }
}
