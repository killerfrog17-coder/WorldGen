using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    // These joins close only the actual difference between two resident land
    // profiles. They are temporary render geometry, never a second heightfield
    // or a fixed-depth skirt. The sampled/contact terrain remains unchanged.
    sealed class ResidentSeam
    {
        public GameObject Root;public Mesh Mesh;public Material Material;public int Triangles;
    }
    sealed class ResidentLine
    {
        public TerrainVertex[] Points;
        public TerrainVertex At(double along,bool vertical)
        {
            int lo=0,hi=Points.Length-1;
            while(lo+1<hi){int mid=(lo+hi)/2;double at=vertical?Points[mid].Position.z:Points[mid].Position.x;if(at<=along)lo=mid;else hi=mid;}
            var a=Points[lo];var b=Points[hi];double first=vertical?a.Position.z:a.Position.x,last=vertical?b.Position.z:b.Position.x;
            return SeamBetween(a,b,(float)Math.Max(0,Math.Min(1,(along-first)/Math.Max(1e-12,last-first))));
        }
    }
    sealed class ResidentProfiles
    {
        public readonly ResidentLine[] Outer=new ResidentLine[4];
        public readonly Dictionary<(bool,long),ResidentLine> Interior=new Dictionary<(bool,long),ResidentLine>();
    }
    readonly Dictionary<TerrainTileKey,Tile> seamObservedTiles=new Dictionary<TerrainTileKey,Tile>();
    readonly Dictionary<Tile,ResidentProfiles> residentProfiles=new Dictionary<Tile,ResidentProfiles>();
    readonly Dictionary<Tile,ResidentSeam> residentSeams=new Dictionary<Tile,ResidentSeam>();
    readonly List<TerrainTileKey> seamChanged=new List<TerrainTileKey>(),seamAffected=new List<TerrainTileKey>();
    readonly HashSet<TerrainTileKey> seamPending=new HashSet<TerrainTileKey>();
    readonly List<TerrainVertex> seamVertices=new List<TerrainVertex>();
    readonly List<int> seamIndices=new List<int>();
    readonly List<double> seamCuts=new List<double>(),seamIntervalCuts=new List<double>();
    LocalTerrainFrame seamFrame;long seamRevision=-1;
    int residentSeamTriangles;double residentSeamLastMs,residentSeamMaxMs;
    string ResidentSeamReceipt=>"resident boundary joins="+residentSeams.Count+" triangles="+residentSeamTriangles+" queued="+seamAffected.Count+" last/max ms="+residentSeamLastMs.ToString("F2")+"/"+residentSeamMaxMs.ToString("F2");

    static TerrainVertex SeamBetween(TerrainVertex a,TerrainVertex b,float t)=>new TerrainVertex{
        Position=Vector3.LerpUnclamped(a.Position,b.Position,t),Normal=Vector3.LerpUnclamped(a.Normal,b.Normal,t),
        Color=Color.LerpUnclamped(a.Color,b.Color,t),Uv=Vector2.LerpUnclamped(a.Uv,b.Uv,t),
        GroundA=Vector4.LerpUnclamped(a.GroundA,b.GroundA,t),GroundB=Vector4.LerpUnclamped(a.GroundB,b.GroundB,t),
        RockA=Vector4.LerpUnclamped(a.RockA,b.RockA,t),RockB=Vector2.LerpUnclamped(a.RockB,b.RockB,t)};

    ResidentLine ResidentProfile(TerrainTileKey key,Tile tile,bool vertical,double worldFixed)
    {
        if(!residentProfiles.TryGetValue(tile,out var profiles)){profiles=new ResidentProfiles();residentProfiles.Add(tile,profiles);}
        double origin=(vertical?key.X:key.Z)*key.SizeM,local=worldFixed-origin;
        // The actual submitted outer coordinate is a float, just like all
        // other mesh vertices. Interior cuts also use that same stored domain.
        float fixedAt=(float)Math.Max(0,Math.Min(key.SizeM,local));long address=(long)Math.Round(local*10000);
        double epsilon=Math.Max(.00001,key.SizeM*1e-8);
        int outer=Math.Abs(local)<=epsilon?(vertical?0:2):Math.Abs(local-key.SizeM)<=epsilon?(vertical?1:3):-1;
        if(outer>=0&&profiles.Outer[outer]!=null)return profiles.Outer[outer];
        if(outer<0&&profiles.Interior.TryGetValue((vertical,address),out var found))return found;
        var filter=tile.Root.transform.Find("Land and seabed").GetComponent<MeshFilter>();
        using var read=Mesh.AcquireReadOnlyMeshData(filter.sharedMesh);var data=read[0];
        var vertices=data.GetVertexData<TerrainVertex>();var part=data.GetSubMesh(0);
        var small=data.indexFormat==IndexFormat.UInt16?data.GetIndexData<ushort>():default;
        var large=data.indexFormat==IndexFormat.UInt32?data.GetIndexData<int>():default;
        int Index(int at)=>part.baseVertex+(data.indexFormat==IndexFormat.UInt16?small[at]:large[at]);
        // All four outer profiles share one immutable mesh read and index scan.
        // Refined meshes retain unused original grid vertices, so only vertices
        // referenced by the submitted submesh can define its drawn boundary.
        if(outer>=0)
        {
            var borders=new[]{new List<TerrainVertex>(),new List<TerrainVertex>(),new List<TerrainVertex>(),new List<TerrainVertex>()};
            var seen=new bool[vertices.Length];
            for(int at=part.indexStart;at<part.indexStart+part.indexCount;at++)
            {
                int index=Index(at);if(seen[index])continue;seen[index]=true;var v=vertices[index];
                if(Math.Abs(v.Position.x)<=epsilon)borders[0].Add(v);
                if(Math.Abs(v.Position.x-(float)key.SizeM)<=epsilon)borders[1].Add(v);
                if(Math.Abs(v.Position.z)<=epsilon)borders[2].Add(v);
                if(Math.Abs(v.Position.z-(float)key.SizeM)<=epsilon)borders[3].Add(v);
            }
            for(int edge=0;edge<4;edge++)profiles.Outer[edge]=FinishResidentLine(borders[edge],key,edge<2,epsilon);
            return profiles.Outer[outer];
        }
        var points=new List<TerrainVertex>();
        void Edge(TerrainVertex a,TerrainVertex b)
        {
            double da=(vertical?a.Position.x:a.Position.z)-fixedAt,db=(vertical?b.Position.x:b.Position.z)-fixedAt;
            if(Math.Abs(da)<=epsilon)points.Add(a);
            if(da*db<0)points.Add(SeamBetween(a,b,(float)(da/(da-db))));
        }
        for(int at=part.indexStart;at<part.indexStart+part.indexCount;at+=3)
        {
            var a=vertices[Index(at)];var b=vertices[Index(at+1)];var c=vertices[Index(at+2)];
            double aa=vertical?a.Position.x:a.Position.z,bb=vertical?b.Position.x:b.Position.z,cc=vertical?c.Position.x:c.Position.z;
            if(fixedAt<Math.Min(aa,Math.Min(bb,cc))-epsilon||fixedAt>Math.Max(aa,Math.Max(bb,cc))+epsilon)continue;
            Edge(a,b);Edge(b,c);Edge(c,a);
        }
        var line=FinishResidentLine(points,key,vertical,epsilon);
        if(profiles.Interior.Count>=64)profiles.Interior.Clear();
        profiles.Interior.Add((vertical,address),line);return line;
    }
    static ResidentLine FinishResidentLine(List<TerrainVertex> points,TerrainTileKey key,bool vertical,double epsilon)
    {
        points.Sort((a,b)=>(vertical?a.Position.z:a.Position.x).CompareTo(vertical?b.Position.z:b.Position.x));
        int count=0;
        for(int point=0;point<points.Count;point++)
        {
            var value=points[point];
            if(count>0&&Math.Abs((vertical?value.Position.z:value.Position.x)-(vertical?points[count-1].Position.z:points[count-1].Position.x))<=epsilon)
            {if(value.Position.y>points[count-1].Position.y)points[count-1]=value;}
            else points[count++]=value;
        }
        if(count<2)throw new InvalidOperationException("Resident terrain has no complete boundary profile: "+key.X+","+key.Z+","+key.Level);
        if(count<points.Count)points.RemoveRange(count,points.Count-count);
        return new ResidentLine{Points=points.ToArray()};
    }

    static bool SeamTouches(TerrainTileKey a,TerrainTileKey b)
    {
        double epsilon=Math.Max(a.SizeM,b.SizeM)*1e-9;
        return Math.Max(a.X*a.SizeM,b.X*b.SizeM)<=Math.Min((a.X+1)*a.SizeM,(b.X+1)*b.SizeM)+epsilon&&
            Math.Max(a.Z*a.SizeM,b.Z*b.SizeM)<=Math.Min((a.Z+1)*a.SizeM,(b.Z+1)*b.SizeM)+epsilon;
    }
    static int SeamOrder(TerrainTileKey a,TerrainTileKey b)=>a.Level!=b.Level?a.Level.CompareTo(b.Level):a.X!=b.X?a.X.CompareTo(b.X):a.Z.CompareTo(b.Z);

    void UpdateResidentSeams()
    {
        if(closed||frame==null)return;
        if(!ReferenceEquals(seamFrame,frame)){ClearResidentSeams();seamFrame=frame;}
        if(seamRevision==residentRevision&&seamAffected.Count==0)return;
        var watch=System.Diagnostics.Stopwatch.StartNew();
        if(seamRevision!=residentRevision)
        {
            seamChanged.Clear();
            foreach(var pair in seamObservedTiles)if(!tiles.TryGetValue(pair.Key,out var next)||!ReferenceEquals(next,pair.Value))seamChanged.Add(pair.Key);
            foreach(var pair in tiles)if(!seamObservedTiles.TryGetValue(pair.Key,out var old)||!ReferenceEquals(old,pair.Value))seamChanged.Add(pair.Key);
            foreach(var pair in tiles)
            {
                bool changed=seamObservedTiles.Count==0;
                if(!changed)foreach(var region in seamChanged)if(SeamTouches(pair.Key,region)){changed=true;break;}
                if(changed&&residentSeams.TryGetValue(pair.Value,out var stale))stale.Root.SetActive(false);
                if(changed&&seamPending.Add(pair.Key))seamAffected.Add(pair.Key);
            }
            seamAffected.Sort((a,b)=>{int d=TerrainLod.DistanceSquared(b,motion.X,motion.Z).CompareTo(TerrainLod.DistanceSquared(a,motion.X,motion.Z));return d!=0?d:SeamOrder(b,a);});
            seamObservedTiles.Clear();foreach(var pair in tiles)seamObservedTiles.Add(pair.Key,pair.Value);
            seamRevision=residentRevision;
        }
        // Bounded work at a tile boundary: do not turn a chart replacement into
        // hundreds of mesh reads in one frame. One expensive first profile can
        // exceed the target, so native timings still report its actual maximum.
        int rebuilt=0;
        while(seamAffected.Count>0&&(rebuilt==0||watch.Elapsed.TotalMilliseconds<1))
        {
            int index=seamAffected.Count-1;var key=seamAffected[index];seamAffected.RemoveAt(index);seamPending.Remove(key);
            if(!tiles.TryGetValue(key,out var tile))continue;
            RebuildResidentSeam(key,tile);rebuilt++;
        }
        residentSeamLastMs=watch.Elapsed.TotalMilliseconds;residentSeamMaxMs=Math.Max(residentSeamMaxMs,residentSeamLastMs);
    }

    void RebuildResidentSeam(TerrainTileKey key,Tile tile)
    {
        seamVertices.Clear();seamIndices.Clear();double size=key.SizeM,ox=key.X*size,oz=key.Z*size;
        double ownStep=size/tile.Segments,epsilon=Math.Max(.002,size*1e-7);
        for(int edge=0;edge<4;edge++)
        {
            bool vertical=edge<2;double fixedAt=(vertical?ox:oz)+(edge==1||edge==3?size:0),begin=vertical?oz:ox,end=begin+size;
            seamCuts.Clear();seamCuts.Add(begin);seamCuts.Add(end);
            foreach(var pair in tiles)
            {
                var k=pair.Key;double across=(vertical?k.X:k.Z)*k.SizeM,along=(vertical?k.Z:k.X)*k.SizeM;
                if(fixedAt<across-epsilon||fixedAt>across+k.SizeM+epsilon||along>=end||along+k.SizeM<=begin)continue;
                seamCuts.Add(Math.Max(begin,along));seamCuts.Add(Math.Min(end,along+k.SizeM));
            }
            seamCuts.Sort();ResidentLine own=null;
            for(int interval=1;interval<seamCuts.Count;interval++)
            {
                double first=seamCuts[interval-1],last=seamCuts[interval];if(last-first<.0001)continue;
                double middle=(first+last)*.5,inside=fixedAt+(edge==0||edge==2?epsilon:-epsilon),outside=fixedAt+(edge==0||edge==2?-epsilon:epsilon);
                if(!ReferenceEquals(VisibleGroundOwner(vertical?inside:middle,vertical?middle:inside,out _),tile))continue;
                var other=VisibleGroundOwner(vertical?outside:middle,vertical?middle:outside,out var otherKey);if(other==null||ReferenceEquals(other,tile))continue;
                double otherStep=otherKey.SizeM/other.Segments;
                if(ownStep>otherStep*1.000001||Math.Abs(ownStep-otherStep)<.00001&&SeamOrder(key,otherKey)>0)continue;
                own??=ResidentProfile(key,tile,vertical,fixedAt);var neighbour=ResidentProfile(otherKey,other,vertical,fixedAt);
                double otherBegin=(vertical?otherKey.Z:otherKey.X)*otherKey.SizeM;
                seamIntervalCuts.Clear();seamIntervalCuts.Add(first);seamIntervalCuts.Add(last);
                foreach(var point in own.Points){double at=begin+(vertical?point.Position.z:point.Position.x);if(at>first&&at<last)seamIntervalCuts.Add(at);}
                foreach(var point in neighbour.Points){double at=otherBegin+(vertical?point.Position.z:point.Position.x);if(at>first&&at<last)seamIntervalCuts.Add(at);}
                seamIntervalCuts.Sort();
                for(int cut=1;cut<seamIntervalCuts.Count;cut++)
                {
                    double a=seamIntervalCuts[cut-1],b=seamIntervalCuts[cut];if(b-a<.0001)continue;
                    var sa=own.At(a-begin,vertical);var sb=own.At(b-begin,vertical);
                    var na=neighbour.At(a-otherBegin,vertical);var nb=neighbour.At(b-otherBegin,vertical);
                    // Express both actual submitted profiles in the owner's
                    // local render frame, including float root translations.
                    var delta=other.Root.transform.localPosition-tile.Root.transform.localPosition;na.Position+=delta;nb.Position+=delta;
                    if((sa.Position-na.Position).sqrMagnitude<.000001f&&(sb.Position-nb.Position).sqrMagnitude<.000001f)continue;
                    var normal=vertical?new Vector3(edge==0?-1:1,0,0):new Vector3(0,0,edge==2?-1:1);
                    sa.Normal=sb.Normal=normal;na.Normal=nb.Normal=normal;
                    // Use the real neighbouring height/position with the same
                    // surface attributes; there is no invented downward depth.
                    na.Color=sa.Color;na.GroundA=sa.GroundA;na.GroundB=sa.GroundB;na.RockA=sa.RockA;na.RockB=sa.RockB;
                    nb.Color=sb.Color;nb.GroundA=sb.GroundA;nb.GroundB=sb.GroundB;nb.RockA=sb.RockA;nb.RockB=sb.RockB;
                    int index=seamVertices.Count;seamVertices.Add(sa);seamVertices.Add(sb);seamVertices.Add(na);seamVertices.Add(nb);
                    seamIndices.Add(index);seamIndices.Add(index+2);seamIndices.Add(index+1);seamIndices.Add(index+1);seamIndices.Add(index+2);seamIndices.Add(index+3);
                }
            }
        }
        if(seamVertices.Count==0){RemoveResidentSeam(tile);return;}
        if(!residentSeams.TryGetValue(tile,out var seam))
        {
            seam=new ResidentSeam{Root=new GameObject("Transient resident boundary joins",typeof(MeshFilter),typeof(MeshRenderer)),Mesh=new Mesh{name="Actual resident boundary joins"},Material=new Material(tile.Materials[0])};
            seam.Root.layer=30;seam.Root.transform.SetParent(tile.Root.transform,false);seam.Root.GetComponent<MeshFilter>().sharedMesh=seam.Mesh;
            // A bridge already selects its two visible owners. Applying either
            // owner's coverage mask here would reopen the exact same slit.
            seam.Material.SetFloat("_CoverageEnabled",0);seam.Material.SetFloat("_TransitionEnabled",0);seam.Material.SetFloat("_TerrainCull",0);
            seam.Root.GetComponent<MeshRenderer>().sharedMaterial=seam.Material;residentSeams.Add(tile,seam);
        }
        residentSeamTriangles-=seam.Triangles;seam.Triangles=seamIndices.Count/3;residentSeamTriangles+=seam.Triangles;
        var mesh=seam.Mesh;mesh.Clear();mesh.indexFormat=seamVertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
        mesh.SetVertexBufferParams(seamVertices.Count,TerrainUpload.Layout);mesh.SetVertexBufferData(seamVertices.ToArray(),0,0,seamVertices.Count);
        mesh.SetIndexBufferParams(seamIndices.Count,mesh.indexFormat);
        if(mesh.indexFormat==IndexFormat.UInt32)mesh.SetIndexBufferData(seamIndices.ToArray(),0,0,seamIndices.Count);
        else{var indices=new ushort[seamIndices.Count];for(int i=0;i<indices.Length;i++)indices[i]=(ushort)seamIndices[i];mesh.SetIndexBufferData(indices,0,0,indices.Length);}
        var low=seamVertices[0].Position;var high=low;foreach(var vertex in seamVertices){low=Vector3.Min(low,vertex.Position);high=Vector3.Max(high,vertex.Position);}
        var bounds=new Bounds((low+high)*.5f,high-low);bounds.Expand(.01f);mesh.bounds=bounds;mesh.subMeshCount=1;
        mesh.SetSubMesh(0,new SubMeshDescriptor(0,seamIndices.Count){vertexCount=seamVertices.Count,bounds=bounds},MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);
        seam.Root.SetActive(true);
    }
    void RemoveResidentSeam(Tile tile)
    {
        if(!residentSeams.TryGetValue(tile,out var seam))return;residentSeams.Remove(tile);residentSeamTriangles-=seam.Triangles;
        seam.Root.SetActive(false);seam.Root.GetComponent<MeshFilter>().sharedMesh=null;Destroy(seam.Mesh);Destroy(seam.Material);Destroy(seam.Root);
    }
    void ReleaseResidentSeam(Tile tile){RemoveResidentSeam(tile);residentProfiles.Remove(tile);}
    void ClearResidentSeams()
    {
        foreach(var seam in residentSeams.Values){seam.Root.SetActive(false);seam.Root.GetComponent<MeshFilter>().sharedMesh=null;Destroy(seam.Mesh);Destroy(seam.Material);Destroy(seam.Root);}
        residentSeams.Clear();residentProfiles.Clear();seamObservedTiles.Clear();seamPending.Clear();seamAffected.Clear();seamChanged.Clear();seamRevision=-1;seamFrame=null;residentSeamTriangles=0;
    }
}
