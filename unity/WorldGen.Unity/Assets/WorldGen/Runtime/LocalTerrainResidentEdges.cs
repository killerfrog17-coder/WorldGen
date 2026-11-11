using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    // A resident edge follows the terrain that is actually drawn next to it,
    // rather than the detail level requested for a future neighbour. Original
    // profiles are retained so temporary conformance never accumulates error.
    // Only the indexed outer vertices receive a small shader height override;
    // there are no skirts, extra draws, GPU readbacks or invented depths.
    sealed class ResidentEdgeData : IDisposable
    {
        public readonly struct Point
        {
            public readonly int Vertex,Mask;public readonly Vector3 Position;
            public Point(int vertex,Vector3 position,int mask){Vertex=vertex;Position=position;Mask=mask;}
        }
        public Point[] Points;
        public int[][] Sides;
        public float[] Heights,Published;
        public Mesh Mesh;
        public Texture2D Texture;
        public long Bytes;
        public bool Conformed;
        public static ResidentEdgeData Build(LocalTerrainMesh source,NativeArray<TerrainVertex> vertices)
        {
            double size=source.SizeM;
            if(size<=0)foreach(var vertex in vertices)size=Math.Max(size,Math.Max(vertex.Position.x,vertex.Position.z));
            double epsilon=Math.Max(.00001,size*1e-8);
            var used=new bool[vertices.Length];foreach(int index in source.Triangles)used[index]=true;
            var points=new List<Point>();var sides=new[]{new List<int>(),new List<int>(),new List<int>(),new List<int>()};
            for(int i=0;i<vertices.Length;i++)
            {
                if(!used[i])continue;var vertex=vertices[i];var p=vertex.Position;
                bool west=Math.Abs(p.x)<=epsilon,east=Math.Abs(p.x-(float)size)<=epsilon,south=Math.Abs(p.z)<=epsilon,north=Math.Abs(p.z-(float)size)<=epsilon;
                if(!west&&!east&&!south&&!north)continue;
                int index=points.Count;points.Add(new Point(i,p,(west?1:0)|(east?2:0)|(south?4:0)|(north?8:0)));vertex.EdgeIndex=index+1;vertices[i]=vertex;
                if(west)sides[0].Add(index);if(east)sides[1].Add(index);if(south)sides[2].Add(index);if(north)sides[3].Add(index);
            }
            var data=new ResidentEdgeData{Points=points.ToArray(),Sides=new int[4][],Heights=new float[points.Count],Published=new float[points.Count]};
            for(int side=0;side<4;side++)
            {
                bool vertical=side<2;sides[side].Sort((a,b)=>(vertical?data.Points[a].Position.z:data.Points[a].Position.x).CompareTo(vertical?data.Points[b].Position.z:data.Points[b].Position.x));
                data.Sides[side]=sides[side].ToArray();
            }
            for(int i=0;i<data.Points.Length;i++)data.Heights[i]=data.Published[i]=data.Points[i].Position.y;
            data.Bytes=(long)points.Count*28;foreach(var side in data.Sides)data.Bytes+=(long)side.Length*4;
            return data;
        }
        public bool TryBoundary(double x,double z,double size,out double height)
        {
            double epsilon=Math.Max(.00001,size*1e-7);int side=Math.Abs(x)<=epsilon?0:Math.Abs(x-size)<=epsilon?1:Math.Abs(z)<=epsilon?2:Math.Abs(z-size)<=epsilon?3:-1;
            height=0;if(side<0)return false;var line=Sides[side];if(line.Length<2)return false;
            bool vertical=side<2;double along=vertical?z:x;int lo=0,hi=line.Length-1;
            while(lo+1<hi){int mid=(lo+hi)/2;var p=Points[line[mid]].Position;if((vertical?p.z:p.x)<=along)lo=mid;else hi=mid;}
            int a=line[lo],b=line[hi];var pa=Points[a].Position;var pb=Points[b].Position;
            double first=vertical?pa.z:pa.x,last=vertical?pb.z:pb.x,t=Math.Max(0,Math.Min(1,(along-first)/Math.Max(1e-12,last-first)));
            height=Heights[a]+(Heights[b]-Heights[a])*t;return true;
        }
        public void Dispose(){if(Texture!=null)UnityEngine.Object.Destroy(Texture);Texture=null;}
    }

    readonly Dictionary<TerrainTileKey,Tile> edgeObservedTiles=new Dictionary<TerrainTileKey,Tile>();
    readonly Dictionary<Vector2Int,KeyValuePair<TerrainTileKey,Tile>> edgeOwners=new Dictionary<Vector2Int,KeyValuePair<TerrainTileKey,Tile>>();
    readonly List<TerrainTileKey> edgeChanged=new List<TerrainTileKey>();
    readonly List<KeyValuePair<TerrainTileKey,Tile>> edgeAffected=new List<KeyValuePair<TerrainTileKey,Tile>>();
    LocalTerrainFrame edgeFrame;long edgeRevision=-1;
    readonly bool checkResidentEdgeIntervals=Array.IndexOf(Environment.GetCommandLineArgs(),"--worldgen-chunk-loading-check")>=0;
    long residentEdgePublications,residentEdgeVertices;int residentEdgeResidualIntervals;double residentEdgeResidualM,residentEdgeLastMs,residentEdgeMaxMs;
    string ResidentEdgeReceipt=>"resident edge conformances="+residentEdgePublications+" vertices="+residentEdgeVertices+" unresolved refined intervals="+residentEdgeResidualIntervals+" residual m="+residentEdgeResidualM.ToString("F3")+" last/max ms="+residentEdgeLastMs.ToString("F2")+"/"+residentEdgeMaxMs.ToString("F2");

    static bool ResidentTouches(TerrainTileKey a,TerrainTileKey b)
    {
        double epsilon=Math.Max(a.SizeM,b.SizeM)*1e-9;
        return Math.Max(a.X*a.SizeM,b.X*b.SizeM)<=Math.Min((a.X+1)*a.SizeM,(b.X+1)*b.SizeM)+epsilon&&
            Math.Max(a.Z*a.SizeM,b.Z*b.SizeM)<=Math.Min((a.Z+1)*a.SizeM,(b.Z+1)*b.SizeM)+epsilon;
    }
    static int ResidentOrder(TerrainTileKey a,TerrainTileKey b)=>a.Level!=b.Level?b.Level.CompareTo(a.Level):a.X!=b.X?a.X.CompareTo(b.X):a.Z.CompareTo(b.Z);
    static double ResidentStep(TerrainTileKey key,Tile tile,double x,double z)
    {
        double step=key.SizeM/tile.Segments,epsilon=Math.Max(.00001,key.SizeM*1e-8);
        x-=key.X*key.SizeM;z-=key.Z*key.SizeM;
        if(tile.Edges==null||tile.Edges.Length<4)return step;
        if(Math.Abs(x)<=epsilon)step=Math.Max(step,TerrainEdges.SurfaceAt(tile.Edges,0,z));
        if(Math.Abs(x-key.SizeM)<=epsilon)step=Math.Max(step,TerrainEdges.SurfaceAt(tile.Edges,1,z));
        if(Math.Abs(z)<=epsilon)step=Math.Max(step,TerrainEdges.SurfaceAt(tile.Edges,2,x));
        if(Math.Abs(z-key.SizeM)<=epsilon)step=Math.Max(step,TerrainEdges.SurfaceAt(tile.Edges,3,x));
        return step;
    }
    static double ResidentMaximumStep(TerrainTileKey key,Tile tile)
    {
        double step=key.SizeM/tile.Segments;
        if(tile.Edges!=null)for(int i=0;i<4&&i<tile.Edges.Length;i++)step=Math.Max(step,tile.Edges[i]);return step;
    }
    static double ResidentGridHeight(TerrainTileKey key,Tile tile,double x,double z)
    {
        int n=tile.Segments;double gx=Math.Max(0,Math.Min(n,x/key.SizeM*n)),gz=Math.Max(0,Math.Min(n,z/key.SizeM*n));
        int a=Math.Min(n-1,(int)gx),b=Math.Min(n-1,(int)gz);double u=gx-a,v=gz-b;var heights=tile.Heights;
        int i=b*(n+1)+a;double h00=heights[i],h10=heights[i+1],h01=heights[i+n+1],h11=heights[i+n+2];
        return u+v<=1?h00+(h10-h00)*u+(h01-h00)*v:h11+(h01-h11)*(1-u)+(h10-h11)*(1-v);
    }
    static bool ResidentHeight(TerrainTileKey key,Tile tile,double x,double z,out double height)
    {
        x=Math.Max(0,Math.Min(key.SizeM,x-key.X*key.SizeM));z=Math.Max(0,Math.Min(key.SizeM,z-key.Z*key.SizeM));
        if(tile.ResidentEdges!=null&&tile.ResidentEdges.TryBoundary(x,z,key.SizeM,out height))return true;
        if(tile.Contact!=null&&tile.Contact.TryHeight(x,z,out height))return true;
        if(tile.Heights!=null&&tile.Heights.Length==(tile.Segments+1)*(tile.Segments+1)){height=ResidentGridHeight(key,tile,x,z);return true;}
        height=0;return false;
    }
    void RestoreResidentEdge(Tile tile)
    {
        var data=tile.ResidentEdges;
        for(int i=0;i<data.Points.Length;i++)
        {
            var point=data.Points[i];float height=point.Position.y;data.Heights[i]=height;
            if(point.Vertex<tile.Heights.Length)tile.Heights[point.Vertex]=height;
            if(tile.Cover.IsCreated&&point.Vertex<tile.Heights.Length&&point.Vertex*16+15<tile.Cover.Length)tile.Cover[point.Vertex*16+15]=height;
            tile.Contact?.SetHeight(point.Vertex,height);
        }
    }
    Tile ResidentOwner(double x,double z,out TerrainTileKey key)
    {
        double unit=TerrainLod.BaseSizeM*HorizontalScale;var address=new Vector2Int((int)Math.Floor(x/unit),(int)Math.Floor(z/unit));
        if(!edgeOwners.TryGetValue(address,out var owner))
        {var tile=VisibleGroundOwner(x,z,out key);owner=new KeyValuePair<TerrainTileKey,Tile>(key,tile);edgeOwners.Add(address,owner);}
        key=owner.Key;return owner.Value;
    }
    float ResidentTarget(TerrainTileKey key,Tile tile,int pointIndex)
    {
        var point=tile.ResidentEdges.Points[pointIndex];double x=key.X*key.SizeM+((point.Mask&1)!=0?0:(point.Mask&2)!=0?key.SizeM:point.Position.x),z=key.Z*key.SizeM+((point.Mask&4)!=0?0:(point.Mask&8)!=0?key.SizeM:point.Position.z);
        double epsilon=Math.Max(.002,key.SizeM*1e-8),best=-1;Tile authority=null;TerrainTileKey authorityKey=default;
        // Both sides of a shared point use the same incident owners and tie
        // break, including T-junctions and four-tile corners. Desired jobs and
        // hidden parent rectangles cannot become the authority accidentally.
        for(int iz=-1;iz<=1;iz+=2)for(int ix=-1;ix<=1;ix+=2)
        {
            var other=ResidentOwner(x+ix*epsilon,z+iz*epsilon,out var otherKey);if(other==null||other.ResidentEdges==null)continue;
            double step=ResidentStep(otherKey,other,x,z);
            if(authority==null||step>best*1.00000001||Math.Abs(step-best)<.000001&&ResidentOrder(otherKey,authorityKey)<0)
            {authority=other;authorityKey=otherKey;best=step;}
        }
        if(authority==null||ReferenceEquals(authority,tile))return point.Position.y;
        return ResidentHeight(authorityKey,authority,x,z,out double target)?(float)target:point.Position.y;
    }
    void UpdateResidentEdges()
    {
        if(closed||frame==null)return;
        if(!ReferenceEquals(edgeFrame,frame)){ClearResidentEdges();edgeFrame=frame;}
        if(edgeRevision==residentRevision)return;
        var watch=System.Diagnostics.Stopwatch.StartNew();edgeChanged.Clear();edgeAffected.Clear();edgeOwners.Clear();
        foreach(var pair in edgeObservedTiles)if(!tiles.TryGetValue(pair.Key,out var next)||!ReferenceEquals(next,pair.Value))edgeChanged.Add(pair.Key);
        foreach(var pair in tiles)if(!edgeObservedTiles.TryGetValue(pair.Key,out var old)||!ReferenceEquals(old,pair.Value))edgeChanged.Add(pair.Key);
        foreach(var pair in tiles)
        {
            if(pair.Value.ResidentEdges==null)continue;
            foreach(var changed in edgeChanged)if(ResidentTouches(pair.Key,changed)){edgeAffected.Add(pair);break;}
        }
        // Coarser resident profiles are established first. A fine edge can
        // then sample an ancestor's actual current triangles at an interior
        // coverage cut, instead of assuming that cut is its outer grid edge.
        edgeAffected.Sort((a,b)=>{int order=ResidentMaximumStep(b.Key,b.Value).CompareTo(ResidentMaximumStep(a.Key,a.Value));return order!=0?order:ResidentOrder(a.Key,b.Key);});
        foreach(var pair in edgeAffected)RestoreResidentEdge(pair.Value);
        foreach(var pair in edgeAffected)
        {
            var tile=pair.Value;var data=tile.ResidentEdges;long before=tile.Contact?.Bytes??0;
            for(int i=0;i<data.Points.Length;i++)
            {
                float height=ResidentTarget(pair.Key,tile,i);var point=data.Points[i];data.Heights[i]=height;
                if(point.Vertex<tile.Heights.Length)tile.Heights[point.Vertex]=height;
                if(tile.Cover.IsCreated&&point.Vertex<tile.Heights.Length&&point.Vertex*16+15<tile.Cover.Length)tile.Cover[point.Vertex*16+15]=height;
                tile.Contact?.SetHeight(point.Vertex,height);
            }
            tile.BufferBytes+=(tile.Contact?.Bytes??0)-before;
        }
        foreach(var pair in edgeAffected)PublishResidentEdge(pair.Key,pair.Value);
        residentEdgeResidualIntervals=0;residentEdgeResidualM=0;
        if(checkResidentEdgeIntervals)foreach(var pair in edgeAffected)CheckResidentEdgeIntervals(pair.Key,pair.Value);
        edgeObservedTiles.Clear();foreach(var pair in tiles)edgeObservedTiles.Add(pair.Key,pair.Value);
        edgeRevision=residentRevision;residentEdgeLastMs=watch.Elapsed.TotalMilliseconds;residentEdgeMaxMs=Math.Max(residentEdgeMaxMs,residentEdgeLastMs);
    }
    void CheckResidentEdgeIntervals(TerrainTileKey key,Tile tile)
    {
        var data=tile.ResidentEdges;double size=key.SizeM,ox=key.X*size,oz=key.Z*size,epsilon=Math.Max(.002,size*1e-8);
        for(int side=0;side<4;side++)
        {
            bool vertical=side<2;var line=data.Sides[side];double fixedAt=(vertical?ox:oz)+(side==1||side==3?size:0),direction=side==0||side==2?-1:1;
            for(int i=1;i<line.Length;i++)
            {
                int a=line[i-1],b=line[i];var pa=data.Points[a].Position;var pb=data.Points[b].Position;
                double first=vertical?pa.z:pa.x,last=vertical?pb.z:pb.x;if(last-first<.0001)continue;
                double along=(vertical?oz:ox)+(first+last)*.5;
                double x=vertical?fixedAt:along,z=vertical?along:fixedAt;
                if(!ReferenceEquals(ResidentOwner(x-(vertical?direction*epsilon:0),z-(vertical?0:direction*epsilon),out _),tile))continue;
                var other=ResidentOwner(x+(vertical?direction*epsilon:0),z+(vertical?0:direction*epsilon),out var otherKey);
                // Ordinary dyadic triangle knots already fall on the fine
                // perimeter grid. Refined river knots may not: detect that
                // concrete unsolved interval instead of declaring it closed.
                if(other==null||ReferenceEquals(other,tile)||other.Contact==null||ResidentStep(key,tile,x,z)>ResidentStep(otherKey,other,x,z)*1.000001)continue;
                if(!ResidentHeight(otherKey,other,x,z,out double drawn))continue;
                double error=Math.Abs((data.Heights[a]+data.Heights[b])*.5-drawn);
                if(error>.02){residentEdgeResidualIntervals++;residentEdgeResidualM=Math.Max(residentEdgeResidualM,error);}
            }
        }
    }
    void PublishResidentEdge(TerrainTileKey key,Tile tile)
    {
        var data=tile.ResidentEdges;bool changed=false,conformed=false;int changedVertices=0;
        for(int i=0;i<data.Points.Length;i++)
        {
            if(data.Heights[i]!=data.Published[i]){changed=true;changedVertices++;}
            if(data.Heights[i]!=data.Points[i].Position.y)conformed=true;
        }
        if(!changed)return;
        if(conformed)
        {
            if(data.Texture==null)
            {
                data.Texture=new Texture2D(data.Points.Length,1,TextureFormat.RFloat,false,true){name="Resident land edge heights",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                tile.BufferBytes+=(long)data.Points.Length*4;
            }
            data.Texture.SetPixelData(data.Heights,0);data.Texture.Apply(false,false);
            tile.Materials[0].SetTexture("_ResidentEdgeHeights",data.Texture);
        }
        tile.Materials[0].SetFloat("_ResidentEdgeCount",conformed?data.Points.Length:0);
        if(data.Mesh!=null)
        {
            var bounds=data.Mesh.bounds;var low=bounds.min;var high=bounds.max;
            foreach(float height in data.Heights){low.y=Math.Min(low.y,height);high.y=Math.Max(high.y,height);}
            data.Mesh.bounds=new Bounds((low+high)*.5f,high-low);
        }
        Array.Copy(data.Heights,data.Published,data.Heights.Length);data.Conformed=conformed;
        // Shader displacement changes the same contact/grid surface used by
        // movement and object roots. Surface/plant attributes are untouched.
        tile.OcclusionMin=null;tile.DiagnosticRevision=-1;
        ReleaseGroundCover(tile);
        scatterGroundChanges.Add(key);scatterGroundRevision++;residentRevision++;
        residentEdgePublications++;residentEdgeVertices+=changedVertices;
    }
    void ReleaseResidentEdges(Tile tile){tile.ResidentEdges?.Dispose();tile.ResidentEdges=null;}
    void ClearResidentEdges(){edgeObservedTiles.Clear();edgeOwners.Clear();edgeChanged.Clear();edgeAffected.Clear();edgeRevision=-1;edgeFrame=null;}
}
