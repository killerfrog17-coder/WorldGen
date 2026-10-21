using System;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class CoverGpu
    {
        public GraphicsBuffer Grid,Roots,Args,Count;
        public GraphicsBuffer[] Visible=new GraphicsBuffer[5];
        public int Topology;
        public MaterialPropertyBlock Properties=new MaterialPropertyBlock();
        public int WindowX,WindowZ,Candidates;
        public float Born;
        public long Bytes;
        public Bounds LocalBounds;
        public bool Sprites;
    }
    Material groundCoverMaterial,groundCanopyMaterial;ComputeShader groundCoverPlacement;
    Texture2D grassFieldTexture;
    readonly Plane[] coverFrustum=new Plane[6];
    readonly Vector4[] coverCullPlanes=new Vector4[6];
    public bool GroundCoverEnabled=true;
    public int GroundCoverDraws {get;private set;}
    public long GroundCoverBytes {get;private set;}
    public int GroundCoverCandidates {get;private set;}
    static void ReleaseGroundCover(Tile tile)
    {
        ReleaseCoverGpu(tile.Grass);ReleaseCoverGpu(tile.GrassSprites);tile.Grass=null;tile.GrassSprites=null;
    }
    static void ReleaseCoverGpu(CoverGpu gpu)
    {if(gpu==null)return;gpu.Grid?.Dispose();gpu.Roots?.Dispose();gpu.Args?.Dispose();gpu.Count?.Dispose();foreach(var visible in gpu.Visible)visible?.Dispose();}
    static bool CoverFullyHidden(float[] nodes,int node=0)
    {
        if(nodes==null||nodes.Length==0)return false;
        for(int i=0;i<4;i++){float next=nodes[node*4+i];if(next==0||next>0&&!CoverFullyHidden(nodes,(int)next-1))return false;}
        return true;
    }
    void InitializeGroundCover()
    {
        if(groundCoverMaterial!=null||!SystemInfo.supportsComputeShaders)return;
        var shader=Resources.Load<Shader>("WorldGenGroundCover");groundCoverPlacement=Resources.Load<ComputeShader>("GroundCoverPlacement");
        if(shader==null||!shader.isSupported||groundCoverPlacement==null)return;
        groundCoverMaterial=new Material(shader);
        // The middle distance now uses the same procedural leaves, not an
        // unrelated texture-bank shader. Kept separate for per-draw properties.
        groundCanopyMaterial=new Material(shader);
        grassFieldTexture=CreateGrassFieldTexture();
        if(landMaterial!=null)landMaterial.SetTexture("_GrassField",grassFieldTexture);
    }
    static Texture2D CreateGrassFieldTexture()
    {
        // Neutral mipped top-down bent ribbons, tinted by the actual world flora.
        const int size=256;var heights=new float[size*size];uint state=0x713ad5u;
        float Next(){state^=state<<13;state^=state>>17;state^=state<<5;return (state&0xffffffu)/16777216f;}
        for(int blade=0;blade<6000;blade++)
        {
            float x=Next()*size,z=Next()*size,angle=Next()*Mathf.PI*2,length=3+Next()*14,width=.45f+Next()*.65f;
            float dx=Mathf.Cos(angle),dz=Mathf.Sin(angle),bend=(Next()-.5f)*3;
            for(int step=0;step<=12;step++)
            {
                float t=step/12f,px=x+dx*length*t-dz*bend*t*t,pz=z+dz*length*t+dx*bend*t*t;
                int ix=Mathf.FloorToInt(px),iz=Mathf.FloorToInt(pz);float radius=width*(1-t)+.35f;
                for(int oz=-1;oz<=2;oz++)for(int ox=-1;ox<=2;ox++)
                {
                    float distance=Vector2.Distance(new Vector2(ix+ox+.5f,iz+oz+.5f),new Vector2(px,pz));
                    float h=Mathf.Clamp01(radius+.65f-distance)*Mathf.Lerp(.35f,1,t);
                    int at=((iz+oz)&255)*size+((ix+ox)&255);heights[at]=Mathf.Max(heights[at],h);
                }
            }
        }
        float mean=0;foreach(float h in heights)mean+=h;mean/=heights.Length;
        var pixels=new Color32[heights.Length];
        for(int z=0;z<size;z++)for(int x=0;x<size;x++)
        {
            int at=z*size+x;float sx=heights[z*size+((x+1)&255)]-heights[z*size+((x-1)&255)];
            float sz=heights[((z+1)&255)*size+x]-heights[((z-1)&255)*size+x];
            pixels[at]=new Color(.5f+(heights[at]-mean)*.27f,.5f-sx*.22f,.5f-sz*.22f,1);
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="Procedural grass mat",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=8};
        texture.SetPixels32(pixels);texture.Apply(true,true);return texture;
    }
    CoverGpu PrepareGroundCover(TerrainTileKey key,Tile tile,int wx,int wz,bool sprites=false)
    {
        double spacing=sprites?1.2:.60,window=sprites?576:96;
        // Separate sparse sprite fields from the expensive close blade field.
        // Both use physical metres, including on small planets.
        double cx=wx*64+32,cz=wz*64+32;
        int x0=(int)Math.Floor(Math.Max(key.X*key.SizeM,cx-window)/spacing),x1=(int)Math.Ceiling(Math.Min((key.X+1)*key.SizeM,cx+window)/spacing);
        int z0=(int)Math.Floor(Math.Max(key.Z*key.SizeM,cz-window)/spacing),z1=(int)Math.Ceiling(Math.Min((key.Z+1)*key.SizeM,cz+window)/spacing);
        int width=Math.Max(0,x1-x0),height=Math.Max(0,z1-z0),count=width*height;
        if(count==0||count>(sprites?1000000:450000))return null;
        var gpu=new CoverGpu{WindowX=wx,WindowZ=wz,Candidates=count,Born=Math.Max(.001f,Time.time),Sprites=sprites,
            LocalBounds=tile.Root==null?new Bounds():tile.Root.GetComponentInChildren<MeshFilter>().sharedMesh.bounds};
        try
        {
            gpu.Grid=new GraphicsBuffer(GraphicsBuffer.Target.Structured,tile.Cover.Length,4);gpu.Grid.SetData(tile.Cover);
            gpu.Roots=new GraphicsBuffer(GraphicsBuffer.Target.Append,count,96);gpu.Roots.SetCounterValue(0);
            gpu.Args=new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,5,GraphicsBuffer.IndirectDrawArgs.size);
            gpu.Count=new GraphicsBuffer(GraphicsBuffer.Target.Raw,1,4);
            for(int lod=0;lod<5;lod++)gpu.Visible[lod]=new GraphicsBuffer(GraphicsBuffer.Target.Append,count,4);
            groundCoverPlacement.SetBuffer(0,"_Grid",gpu.Grid);groundCoverPlacement.SetBuffer(0,"_Roots",gpu.Roots);
            groundCoverPlacement.SetInts("_Candidate",x0,z0,width,height);
            groundCoverPlacement.SetFloat("_Spacing",(float)spacing);
            groundCoverPlacement.SetVector("_Tile",new Vector4((float)(key.X*key.SizeM),(float)(key.Z*key.SizeM),(float)key.SizeM,tile.Segments));
            groundCoverPlacement.SetInt("_Seed",terrain==null?731:terrain.SourceSeed);groundCoverPlacement.Dispatch(0,(count+63)/64,1,1);
            GraphicsBuffer.CopyCount(gpu.Roots,gpu.Count,0);
            gpu.Properties.SetBuffer("_CoverRoots",gpu.Roots);
            gpu.Properties.SetVector("_CoverTileOrigin",new Vector4((float)(key.X*key.SizeM),(float)(key.Z*key.SizeM),0,0));
            gpu.Properties.SetInt("_CoverSeed",terrain==null?731:terrain.SourceSeed);
            gpu.Bytes=(long)tile.Cover.Length*4+(long)count*116+4+5*GraphicsBuffer.IndirectDrawArgs.size;
            return gpu;
        }
        catch{ReleaseCoverGpu(gpu);throw;}
    }
    void CullGroundCover(CoverGpu gpu,Vector4 origin,Camera camera,Tile tile=null,float tileSize=1)
    {
        int detail=Mathf.Clamp(TerrainVideoOptions.GrassDetail,0,3);
        int near=Mathf.Max(2,Mathf.RoundToInt((detail==0?16:detail==1?32:detail==2?64:96)*TerrainVideoOptions.GrassDensity));
        int baseNear=near;near*=2;
        int middle=Mathf.Max(2,Mathf.RoundToInt(baseNear/3f));
        int segments=detail==0?2:detail==3?4:3,topology=near|(middle<<10)|(segments<<24);
        if(gpu.Topology!=topology)
        {
            var args=new GraphicsBuffer.IndirectDrawArgs[5];
            args[0].vertexCountPerInstance=(uint)(near*segments*6);args[1].vertexCountPerInstance=(uint)(middle*2*6);
            for(int lod=2;lod<5;lod++)args[lod].vertexCountPerInstance=(uint)(Mathf.CeilToInt(middle*3f/(1<<(2*(lod-2))))*6);
            gpu.Args.SetData(args);gpu.Topology=topology;
        }
        GeometryUtility.CalculateFrustumPlanes(camera,coverFrustum);
        for(int k=0;k<6;k++)coverCullPlanes[k]=new Vector4(coverFrustum[k].normal.x,coverFrustum[k].normal.y,coverFrustum[k].normal.z,coverFrustum[k].distance);
        foreach(var visible in gpu.Visible)visible.SetCounterValue(0);
        groundCoverPlacement.SetBuffer(1,"_PlacedRoots",gpu.Roots);groundCoverPlacement.SetBuffer(1,"_RootCount",gpu.Count);
        groundCoverPlacement.SetBuffer(1,"_NearRoots",gpu.Visible[0]);groundCoverPlacement.SetBuffer(1,"_MiddleRoots",gpu.Visible[1]);groundCoverPlacement.SetBuffer(1,"_FarRoots",gpu.Visible[2]);
        groundCoverPlacement.SetBuffer(1,"_DistantRoots",gpu.Visible[3]);groundCoverPlacement.SetBuffer(1,"_HorizonRoots",gpu.Visible[4]);
        groundCoverPlacement.SetVector("_CullOrigin",origin);groundCoverPlacement.SetVector("_CullCamera",camera.transform.position);
        groundCoverPlacement.SetVectorArray("_CullPlanes",coverCullPlanes);groundCoverPlacement.SetFloat("_GrassRange",TerrainVideoOptions.GrassDistance);
        groundCoverPlacement.SetInt("_SpritesOnly",gpu.Sprites?1:0);
        groundCoverPlacement.SetInt("_CoverageEnabled",tile?.Coverage!=null?1:0);
        groundCoverPlacement.SetFloat("_CoverageSize",tileSize);
        groundCoverPlacement.SetTexture(1,"_TerrainCoverage",tile?.Coverage!=null?tile.Coverage:Texture2D.blackTexture);
        float bladeRange=Mathf.Min(TerrainVideoOptions.GrassBladeDistance,TerrainVideoOptions.GrassDistance*.75f);
        groundCoverPlacement.SetFloat("_GrassNear",bladeRange);groundCoverPlacement.SetInt("_CanopyEnabled",TerrainVideoOptions.GrassCanopy&&groundCanopyMaterial!=null?1:0);
        groundCoverPlacement.Dispatch(1,(gpu.Candidates+63)/64,1,1);
        for(int lod=0;lod<5;lod++)GraphicsBuffer.CopyCount(gpu.Visible[lod],gpu.Args,lod*GraphicsBuffer.IndirectDrawArgs.size+4);
        gpu.Properties.SetVector("_CoverOrigin",origin);gpu.Properties.SetVector("_BladeCounts",new Vector4(near,middle,baseNear,segments));
        gpu.Properties.SetFloat("_GrassNear",bladeRange);gpu.Properties.SetFloat("_GrassRange",TerrainVideoOptions.GrassDistance);
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
        if(groundCanopyMaterial!=null)
        {
            groundCanopyMaterial.SetColor("_FogColor",landMaterial.GetColor("_FogColor"));groundCanopyMaterial.SetColor("_AmbientTint",landMaterial.GetColor("_AmbientTint"));
            groundCanopyMaterial.SetFloat("_FogDistance",landMaterial.GetFloat("_FogDistance"));groundCanopyMaterial.SetFloat("_Daylight",landMaterial.GetFloat("_Daylight"));
            groundCanopyMaterial.SetFloat("_GrassWind",TerrainVideoOptions.GrassWind?1:0);
        }
        float grassRange=TerrainVideoOptions.GrassDistance,bladeRange=Mathf.Min(TerrainVideoOptions.GrassBladeDistance,grassRange*.75f);
        foreach(var pair in tiles)
        {
            var key=pair.Key;var tile=pair.Value;
            double distance2=TerrainLod.DistanceSquared(key,motion.X,motion.Z);
            for(int pass=0;pass<2;pass++)
            {
            bool sprites=pass==1;float range=sprites?grassRange:bladeRange;
            bool eligible=(sprites?TerrainVideoOptions.GrassCanopy:key.Level==0)&&tile.Cover!=null&&tile.Cover.Length>0&&distance2<range*range&&!CoverFullyHidden(tile.CoverageNodes);
            var gpu=sprites?tile.GrassSprites:tile.Grass;
            if(!eligible||gpu!=null&&key.SizeM>(sprites?512:64)&&(gpu.WindowX!=wx||gpu.WindowZ!=wz))
            {ReleaseCoverGpu(gpu);gpu=null;if(sprites)tile.GrassSprites=null;else tile.Grass=null;}
            if(!eligible)continue;
            if(gpu==null&&!installed&&CenterReady)
            {gpu=PrepareGroundCover(key,tile,wx,wz,sprites);if(sprites)tile.GrassSprites=gpu;else tile.Grass=gpu;installed=true;}
            if(gpu==null)continue;
            GroundCoverBytes+=gpu.Bytes;GroundCoverCandidates+=gpu.Candidates;
            var bounds=gpu.LocalBounds;bounds.center+=tile.Root.transform.position;bounds.Expand(10);
            if(!GeometryUtility.TestPlanesAABB(coverFrustum,bounds))continue;
            CullGroundCover(gpu,new Vector4((float)(key.X*key.SizeM-originX),0,(float)(key.Z*key.SizeM-originZ),gpu.Born),view,tile,(float)key.SizeM);
            for(int lod=0;lod<5;lod++)
            {
                if(sprites!=(lod>=2))continue;
                if(lod==0&&distance2>.36*bladeRange*bladeRange||lod==1&&distance2>bladeRange*bladeRange)continue;
                var material=lod>=2?groundCanopyMaterial:groundCoverMaterial;
                if(material==null||lod>=2&&!TerrainVideoOptions.GrassCanopy)continue;
                gpu.Properties.SetInt("_CoverLod",lod);
                gpu.Properties.SetBuffer("_VisibleRoots",gpu.Visible[lod]);
                var rp=new RenderParams(material){camera=view,layer=30,worldBounds=bounds,matProps=gpu.Properties,shadowCastingMode=ShadowCastingMode.Off,receiveShadows=true};
                Graphics.RenderPrimitivesIndirect(in rp,MeshTopology.Triangles,gpu.Args,1,lod);GroundCoverDraws++;
            }
            }
        }
    }
}
