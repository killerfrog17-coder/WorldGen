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
        public double X=double.NaN,Z,Altitude;
    }
    sealed class RetiringFrame {public GameObject Root;public Queue<Tile> Tiles;}
    PreparedFrame preparedFrame;
    readonly Queue<RetiringFrame> retiringFrames=new Queue<RetiringFrame>();
    double nextHandoffCheck;
    public int ReanchorCount {get;private set;}
    public double LargestHandoffMs {get;private set;}
    public double LargestUploadMs {get;private set;}
    public int PreparedTileCount=>preparedFrame?.Tiles.Count??0;
    public double ReanchorDistance=>Math.Max(Math.Abs(motion.X),Math.Abs(motion.Z));
    public double ReanchorLimit=>frame==null?0:TerrainHandoff.Limit(frame);
    public double RenderedFootClearance=>motion.Y-VisibleGround(motion.X,motion.Z,Ground(motion.X,motion.Z));
    public bool TerrainVisible=>terrainRoot!=null&&terrainRoot.activeInHierarchy&&tiles.Count>0;
    public void TestAdvanceEast(double metres){motion.X+=metres;}
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
        if(terrain==null)return;
        double limit=TerrainHandoff.Limit(frame),distance=ReanchorDistance;
        if(preparedFrame==null)
        {
            if(distance<limit*.5||retiringFrames.Count>0)return;
            // Aim the next chart toward the approaching boundary, not behind
            // the traveller. No change to the current frame or loaded products.
            var point=frame.Position(motion.X*2,motion.Z*2);
            var next=new LocalTerrainFrame(point,terrain.RadiusM,terrain.Patch(point).Sample(0,0).ElevationM,HorizontalScale);
            preparedFrame=new PreparedFrame{Frame=next,Root=NewTerrainRoot(false)};
        }
        var pending=preparedFrame;
        if(V.Dot(Position,pending.Frame.Origin)<=0){CancelPreparedFrame();return;}
        TerrainHandoff.Coordinates(pending.Frame,Position,out double x,out double z);
        double altitude=HeightAboveGround;
        double preparationStep=motion.Flying?1024:Math.Min(1024,512*HorizontalScale);
        if(double.IsNaN(pending.X)||Math.Abs(x-pending.X)>preparationStep||Math.Abs(z-pending.Z)>preparationStep||
            Math.Abs(altitude-pending.Altitude)>Math.Max(150,pending.Altitude*.3))
        {
            pending.X=x;pending.Z=z;pending.Altitude=altitude;
            pending.Wanted=TerrainLod.Plan(x,z,altitude,viewDistance,HorizontalScale);
            pending.Priority.Clear();pending.Priority.AddRange(pending.Wanted.Keys);pending.Edges.Clear();
            foreach(var key in pending.Priority)pending.Edges[key]=TerrainLod.EdgeSteps(key,pending.Wanted);
            double Distance(TerrainTileKey k){double dx=(k.X+.5)*k.SizeM-x,dz=(k.Z+.5)*k.SizeM-z;return dx*dx+dz*dz;}
            pending.Priority.Sort((a,b)=>Distance(a).CompareTo(Distance(b)));
            foreach(var key in pending.Tiles.Keys.Where(k=>!pending.Wanted.ContainsKey(k)&&!pending.Wanted.Keys.Any(w=>Overlaps(k,w))).ToArray())
            {DestroyTile(pending.Tiles[key]);pending.Tiles.Remove(key);}
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
        CommitPreparedFrame(pending,x,z);
    }
    void SchedulePreparedWork(bool reserveCurrentSlot)
    {
        var pending=preparedFrame;if(pending?.Wanted==null)return;
        if(reserveCurrentSlot&&jobs.Any(j=>j.Prepared!=null))return;
        foreach(var key in pending.Priority)
        {
            if(jobs.Count+scatterJobs.Count>=2)return;
            int detail=pending.Wanted[key];var edges=pending.Edges[key];
            if(pending.Tiles.TryGetValue(key,out var tile)&&tile.Segments==detail&&tile.Edges.SequenceEqual(edges)||
                jobs.Any(j=>ReferenceEquals(j.Prepared,pending)&&j.Key.Equals(key)))continue;
            var source=terrain;var buildFrame=pending.Frame;var token=cancellation.Token;
            jobs.Add(new Work{Key=key,Edges=edges,Segments=detail,Generation=generation,Prepared=pending,
                Task=Task.Run(()=>LocalTerrainMesh.Build(source,buildFrame,key.X*key.SizeM,key.Z*key.SizeM,key.SizeM,detail,token,edges),token)});
            if(reserveCurrentSlot)return;
        }
    }
    void CommitPreparedFrame(PreparedFrame pending,double x,double z)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        double above=HeightAboveGround;var oldFrame=frame;double yaw=heading*Math.PI/180,tilt=pitch*Math.PI/180;
        V direction=(oldFrame.East*Math.Sin(yaw)+oldFrame.North*Math.Cos(yaw))*Math.Cos(tilt)-oldFrame.Origin*Math.Sin(tilt);
        var oldRoot=terrainRoot;var oldTiles=tiles;
        frame=pending.Frame;tiles=pending.Tiles;terrainRoot=pending.Root;preparedFrame=null;
        generation++;walkingPatch=null;originX=Math.Floor(x/2048)*2048;originZ=Math.Floor(z/2048)*2048;
        motion.X=x;motion.Z=z;motion.Y=Ground(x,z)+above; // Preserve gait, jump velocity and fly state.
        stepX=motion.X;stepY=motion.Y;stepZ=motion.Z; // Coordinate change is not a travelled footstep.
        heading=(float)(Math.Atan2(V.Dot(direction,frame.East),V.Dot(direction,frame.North))*180/Math.PI);
        pitch=(float)(-Math.Asin(Math.Max(-1,Math.Min(1,V.Dot(direction,frame.Origin))))*180/Math.PI);
        foreach(var pair in tiles)
            pair.Value.Root.transform.localPosition=new Vector3((float)(pair.Key.X*pair.Key.SizeM-originX),0,(float)(pair.Key.Z*pair.Key.SizeM-originZ));
        landMaterial.SetVector("_LocalOrigin",new Vector4((float)originX,(float)originZ,0,0));
        plannedX=double.NaN;RefreshWanted();UpdateCamera();ResetScatterFrame();ResetWeatherFrame();
        SetDaylight(LocalSolarHour,skyYear);
        // Both roots switch in the same Update. Never render an empty frame.
        terrainRoot.SetActive(true);RetireFrame(oldRoot,oldTiles);ReanchorCount++;
        LargestHandoffMs=Math.Max(LargestHandoffMs,timer.Elapsed.TotalMilliseconds);
    }
}
