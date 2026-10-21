using System;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class CoverGpu
    {
        public GraphicsBuffer Grid,Roots,Args;
        public MaterialPropertyBlock Properties=new MaterialPropertyBlock();
        public int WindowX,WindowZ,Candidates;
        public float Born;
        public long Bytes;
        public Bounds LocalBounds;
    }
    Material groundCoverMaterial;ComputeShader groundCoverPlacement;
    readonly Plane[] coverFrustum=new Plane[6];
    public bool GroundCoverEnabled=true;
    public int GroundCoverDraws {get;private set;}
    public long GroundCoverBytes {get;private set;}
    public int GroundCoverCandidates {get;private set;}
    static void ReleaseGroundCover(Tile tile)
    {
        if(tile.Grass==null)return;tile.Grass.Grid?.Dispose();tile.Grass.Roots?.Dispose();tile.Grass.Args?.Dispose();tile.Grass=null;
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
        const double spacing=.85;
        // Clip large-scale1024m tiles to a bounded moving window. Small-scale
        // worlds still use physical-metre plant spacing, not scaled-up rivers/grass.
        double cx=wx*64+32,cz=wz*64+32;
        int x0=(int)Math.Floor(Math.Max(key.X*key.SizeM,cx-224)/spacing),x1=(int)Math.Ceiling(Math.Min((key.X+1)*key.SizeM,cx+224)/spacing);
        int z0=(int)Math.Floor(Math.Max(key.Z*key.SizeM,cz-224)/spacing),z1=(int)Math.Ceiling(Math.Min((key.Z+1)*key.SizeM,cz+224)/spacing);
        int width=Math.Max(0,x1-x0),height=Math.Max(0,z1-z0),count=width*height;
        if(count==0||count>300000)return null;
        var gpu=new CoverGpu{WindowX=wx,WindowZ=wz,Candidates=count,Born=Math.Max(.001f,Time.time),
            LocalBounds=tile.Root==null?new Bounds():tile.Root.GetComponentInChildren<MeshFilter>().sharedMesh.bounds};
        try
        {
            gpu.Grid=new GraphicsBuffer(GraphicsBuffer.Target.Structured,tile.Cover.Length,4);gpu.Grid.SetData(tile.Cover);
            gpu.Roots=new GraphicsBuffer(GraphicsBuffer.Target.Append,count,64);gpu.Roots.SetCounterValue(0);
            gpu.Args=new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,3,GraphicsBuffer.IndirectDrawArgs.size);
            var args=new GraphicsBuffer.IndirectDrawArgs[3];
            args[0].vertexCountPerInstance=126;args[1].vertexCountPerInstance=48;args[2].vertexCountPerInstance=12;gpu.Args.SetData(args);
            groundCoverPlacement.SetBuffer(0,"_Grid",gpu.Grid);groundCoverPlacement.SetBuffer(0,"_Roots",gpu.Roots);
            groundCoverPlacement.SetInts("_Candidate",x0,z0,width,height);
            groundCoverPlacement.SetVector("_Tile",new Vector4((float)(key.X*key.SizeM),(float)(key.Z*key.SizeM),(float)key.SizeM,tile.Segments));
            groundCoverPlacement.SetInt("_Seed",terrain==null?731:terrain.SourceSeed);groundCoverPlacement.Dispatch(0,(count+63)/64,1,1);
            for(int lod=0;lod<3;lod++)GraphicsBuffer.CopyCount(gpu.Roots,gpu.Args,lod*GraphicsBuffer.IndirectDrawArgs.size+4);
            gpu.Properties.SetBuffer("_CoverRoots",gpu.Roots);
            gpu.Bytes=(long)tile.Cover.Length*4+(long)count*64+3*GraphicsBuffer.IndirectDrawArgs.size;
            return gpu;
        }
        catch{gpu.Grid?.Dispose();gpu.Roots?.Dispose();gpu.Args?.Dispose();throw;}
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
        foreach(var pair in tiles)
        {
            var key=pair.Key;var tile=pair.Value;
            double distance2=TerrainLod.DistanceSquared(key,motion.X,motion.Z);
            bool eligible=key.Level==0&&tile.Cover!=null&&tile.Cover.Length>0&&distance2<160*160;
            if(!eligible){ReleaseGroundCover(tile);continue;}
            if(tile.Grass!=null&&key.SizeM>256&&(tile.Grass.WindowX!=wx||tile.Grass.WindowZ!=wz))ReleaseGroundCover(tile);
            if(tile.Grass==null&&!installed&&CenterReady)
            {tile.Grass=PrepareGroundCover(key,tile,wx,wz);installed=true;}
            var gpu=tile.Grass;if(gpu==null)continue;
            GroundCoverBytes+=gpu.Bytes;GroundCoverCandidates+=gpu.Candidates;
            var bounds=gpu.LocalBounds;bounds.center+=tile.Root.transform.position;bounds.Expand(4);
            if(!GeometryUtility.TestPlanesAABB(coverFrustum,bounds))continue;
            gpu.Properties.SetVector("_CoverOrigin",new Vector4((float)(key.X*key.SizeM-originX),0,(float)(key.Z*key.SizeM-originZ),gpu.Born));
            for(int lod=0;lod<3;lod++)
            {
                if(lod==0&&distance2>42*42||lod==1&&distance2>90*90)continue;
                gpu.Properties.SetInt("_CoverLod",lod);
                var rp=new RenderParams(groundCoverMaterial){camera=view,layer=30,worldBounds=bounds,matProps=gpu.Properties,shadowCastingMode=ShadowCastingMode.Off,receiveShadows=true};
                Graphics.RenderPrimitivesIndirect(in rp,MeshTopology.Triangles,gpu.Args,1,lod);GroundCoverDraws++;
            }
        }
    }
}
