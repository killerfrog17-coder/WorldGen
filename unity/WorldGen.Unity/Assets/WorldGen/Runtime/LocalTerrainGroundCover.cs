using System;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class CoverGpu
    {
        public GraphicsBuffer Grid,Roots,Args,Count;
        public GraphicsBuffer[] Visible=new GraphicsBuffer[3];
        public int Topology;
        public MaterialPropertyBlock Properties=new MaterialPropertyBlock();
        public int WindowX,WindowZ,Candidates;
        public float Born;
        public long Bytes;
        public Bounds LocalBounds;
    }
    Material groundCoverMaterial;ComputeShader groundCoverPlacement;
    readonly Plane[] coverFrustum=new Plane[6];
    readonly Vector4[] coverCullPlanes=new Vector4[6];
    public bool GroundCoverEnabled=true;
    public int GroundCoverDraws {get;private set;}
    public long GroundCoverBytes {get;private set;}
    public int GroundCoverCandidates {get;private set;}
    static void ReleaseGroundCover(Tile tile)
    {
        if(tile.Grass==null)return;
        var gpu=tile.Grass;gpu.Grid?.Dispose();gpu.Roots?.Dispose();gpu.Args?.Dispose();gpu.Count?.Dispose();
        foreach(var visible in gpu.Visible)visible?.Dispose();tile.Grass=null;
    }
    void InitializeGroundCover()
    {
        if(groundCoverMaterial!=null||!SystemInfo.supportsComputeShaders)return;
        var shader=Resources.Load<Shader>("WorldGenGroundCover");groundCoverPlacement=Resources.Load<ComputeShader>("GroundCoverPlacement");
        if(shader==null||!shader.isSupported||groundCoverPlacement==null)return;
        groundCoverMaterial=new Material(shader);
    }
    CoverGpu PrepareGroundCover(TerrainTileKey key,Tile tile,int wx,int wz)
    {
        const double spacing=.60;
        // Clip large-scale1024m tiles to a bounded moving window. Small-scale
        // worlds still use physical-metre plant spacing, not scaled-up rivers/grass.
        double cx=wx*64+32,cz=wz*64+32;
        int x0=(int)Math.Floor(Math.Max(key.X*key.SizeM,cx-192)/spacing),x1=(int)Math.Ceiling(Math.Min((key.X+1)*key.SizeM,cx+192)/spacing);
        int z0=(int)Math.Floor(Math.Max(key.Z*key.SizeM,cz-192)/spacing),z1=(int)Math.Ceiling(Math.Min((key.Z+1)*key.SizeM,cz+192)/spacing);
        int width=Math.Max(0,x1-x0),height=Math.Max(0,z1-z0),count=width*height;
        if(count==0||count>450000)return null;
        var gpu=new CoverGpu{WindowX=wx,WindowZ=wz,Candidates=count,Born=Math.Max(.001f,Time.time),
            LocalBounds=tile.Root==null?new Bounds():tile.Root.GetComponentInChildren<MeshFilter>().sharedMesh.bounds};
        try
        {
            gpu.Grid=new GraphicsBuffer(GraphicsBuffer.Target.Structured,tile.Cover.Length,4);gpu.Grid.SetData(tile.Cover);
            gpu.Roots=new GraphicsBuffer(GraphicsBuffer.Target.Append,count,64);gpu.Roots.SetCounterValue(0);
            gpu.Args=new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,3,GraphicsBuffer.IndirectDrawArgs.size);
            gpu.Count=new GraphicsBuffer(GraphicsBuffer.Target.Raw,1,4);
            for(int lod=0;lod<3;lod++)gpu.Visible[lod]=new GraphicsBuffer(GraphicsBuffer.Target.Append,count,4);
            groundCoverPlacement.SetBuffer(0,"_Grid",gpu.Grid);groundCoverPlacement.SetBuffer(0,"_Roots",gpu.Roots);
            groundCoverPlacement.SetInts("_Candidate",x0,z0,width,height);
            groundCoverPlacement.SetVector("_Tile",new Vector4((float)(key.X*key.SizeM),(float)(key.Z*key.SizeM),(float)key.SizeM,tile.Segments));
            groundCoverPlacement.SetInt("_Seed",terrain==null?731:terrain.SourceSeed);groundCoverPlacement.Dispatch(0,(count+63)/64,1,1);
            GraphicsBuffer.CopyCount(gpu.Roots,gpu.Count,0);
            gpu.Properties.SetBuffer("_CoverRoots",gpu.Roots);
            gpu.Bytes=(long)tile.Cover.Length*4+(long)count*76+4+3*GraphicsBuffer.IndirectDrawArgs.size;
            return gpu;
        }
        catch{tile.Grass=gpu;ReleaseGroundCover(tile);throw;}
    }
    void CullGroundCover(CoverGpu gpu,Vector4 origin,Camera camera)
    {
        int detail=Mathf.Clamp(TerrainVideoOptions.GrassDetail,0,3);
        int near=Mathf.Max(2,Mathf.RoundToInt((detail==0?8:detail==1?16:detail==2?24:32)*TerrainVideoOptions.GrassDensity));
        int middle=Mathf.Max(2,Mathf.RoundToInt(near/3f)),far=Mathf.Max(1,Mathf.RoundToInt(near/12f));
        int segments=detail==0?2:detail==3?4:3,topology=near+middle*128+far*16384+segments*1048576;
        if(gpu.Topology!=topology)
        {
            var args=new GraphicsBuffer.IndirectDrawArgs[3];
            args[0].vertexCountPerInstance=(uint)(near*segments*6);args[1].vertexCountPerInstance=(uint)(middle*2*6);args[2].vertexCountPerInstance=(uint)(far*6);gpu.Args.SetData(args);gpu.Topology=topology;
        }
        GeometryUtility.CalculateFrustumPlanes(camera,coverFrustum);
        for(int k=0;k<6;k++)coverCullPlanes[k]=new Vector4(coverFrustum[k].normal.x,coverFrustum[k].normal.y,coverFrustum[k].normal.z,coverFrustum[k].distance);
        foreach(var visible in gpu.Visible)visible.SetCounterValue(0);
        groundCoverPlacement.SetBuffer(1,"_PlacedRoots",gpu.Roots);groundCoverPlacement.SetBuffer(1,"_RootCount",gpu.Count);
        groundCoverPlacement.SetBuffer(1,"_NearRoots",gpu.Visible[0]);groundCoverPlacement.SetBuffer(1,"_MiddleRoots",gpu.Visible[1]);groundCoverPlacement.SetBuffer(1,"_FarRoots",gpu.Visible[2]);
        groundCoverPlacement.SetVector("_CullOrigin",origin);groundCoverPlacement.SetVector("_CullCamera",camera.transform.position);
        groundCoverPlacement.SetVectorArray("_CullPlanes",coverCullPlanes);groundCoverPlacement.SetFloat("_GrassRange",TerrainVideoOptions.GrassDistance);
        groundCoverPlacement.Dispatch(1,(gpu.Candidates+63)/64,1,1);
        for(int lod=0;lod<3;lod++)GraphicsBuffer.CopyCount(gpu.Visible[lod],gpu.Args,lod*GraphicsBuffer.IndirectDrawArgs.size+4);
        gpu.Properties.SetVector("_CoverOrigin",origin);gpu.Properties.SetVector("_BladeCounts",new Vector4(near,middle,far,segments));
    }
    void DrawGroundCover()
    {
        GroundCoverDraws=0;GroundCoverBytes=0;GroundCoverCandidates=0;
        if(!GroundCoverEnabled||!TerrainVideoOptions.GroundCover||!ScatterEnabled||view==null||terrain==null)
        {foreach(var entry in tiles)ReleaseGroundCover(entry.Value);return;}
        InitializeGroundCover();if(groundCoverMaterial==null)return;
        int wx=(int)Math.Floor(motion.X/64),wz=(int)Math.Floor(motion.Z/64);bool installed=false;
        GeometryUtility.CalculateFrustumPlanes(view,coverFrustum);
        groundCoverMaterial.SetColor("_FogColor",landMaterial.GetColor("_FogColor"));
        groundCoverMaterial.SetColor("_AmbientTint",landMaterial.GetColor("_AmbientTint"));
        groundCoverMaterial.SetFloat("_FogDistance",landMaterial.GetFloat("_FogDistance"));
        groundCoverMaterial.SetFloat("_Daylight",landMaterial.GetFloat("_Daylight"));
        groundCoverMaterial.SetFloat("_GrassRange",TerrainVideoOptions.GrassDistance);
        groundCoverMaterial.SetFloat("_GrassWind",TerrainVideoOptions.GrassWind?1:0);
        float grassRange=TerrainVideoOptions.GrassDistance,rangeScale=grassRange/160;
        foreach(var pair in tiles)
        {
            var key=pair.Key;var tile=pair.Value;
            double distance2=TerrainLod.DistanceSquared(key,motion.X,motion.Z);
            bool eligible=key.Level==0&&tile.Cover!=null&&tile.Cover.Length>0&&distance2<grassRange*grassRange;
            if(!eligible){ReleaseGroundCover(tile);continue;}
            if(tile.Grass!=null&&key.SizeM>256&&(tile.Grass.WindowX!=wx||tile.Grass.WindowZ!=wz))ReleaseGroundCover(tile);
            if(tile.Grass==null&&!installed&&CenterReady)
            {tile.Grass=PrepareGroundCover(key,tile,wx,wz);installed=true;}
            var gpu=tile.Grass;if(gpu==null)continue;
            GroundCoverBytes+=gpu.Bytes;GroundCoverCandidates+=gpu.Candidates;
            var bounds=gpu.LocalBounds;bounds.center+=tile.Root.transform.position;bounds.Expand(10);
            if(!GeometryUtility.TestPlanesAABB(coverFrustum,bounds))continue;
            CullGroundCover(gpu,new Vector4((float)(key.X*key.SizeM-originX),0,(float)(key.Z*key.SizeM-originZ),gpu.Born),view);
            for(int lod=0;lod<3;lod++)
            {
                if(lod==0&&distance2>42*42*rangeScale*rangeScale||lod==1&&distance2>90*90*rangeScale*rangeScale)continue;
                gpu.Properties.SetInt("_CoverLod",lod);
                gpu.Properties.SetBuffer("_VisibleRoots",gpu.Visible[lod]);
                var rp=new RenderParams(groundCoverMaterial){camera=view,layer=30,worldBounds=bounds,matProps=gpu.Properties,shadowCastingMode=ShadowCastingMode.Off,receiveShadows=true};
                Graphics.RenderPrimitivesIndirect(in rp,MeshTopology.Triangles,gpu.Args,1,lod);GroundCoverDraws++;
            }
        }
    }
}
