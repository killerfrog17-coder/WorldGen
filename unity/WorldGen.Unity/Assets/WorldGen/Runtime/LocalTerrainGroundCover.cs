using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    readonly struct CoverPageKey:IEquatable<CoverPageKey>
    {
        public readonly int X,Z;public readonly bool Far;
        public CoverPageKey(int x,int z,bool far){X=x;Z=z;Far=far;}
        public double Size=>Far?192:48;
        public bool Equals(CoverPageKey other)=>X==other.X&&Z==other.Z&&Far==other.Far;
        public override bool Equals(object other)=>other is CoverPageKey key&&Equals(key);
        public override int GetHashCode()=>unchecked(X*73856093^Z*19349663^(Far?1073741824:0));
    }
    struct CoverRequest {public TerrainTileKey Key;public Tile Tile;public CoverPageKey Page;public double Priority;}
    readonly List<CoverRequest> coverRequests=new List<CoverRequest>();
    readonly List<CoverPageKey> expiredCoverPages=new List<CoverPageKey>();
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
    Texture2D grassFieldTexture,mossFieldTexture,lichenFieldTexture;
    readonly Plane[] coverFrustum=new Plane[6];
    readonly Vector4[] coverCullPlanes=new Vector4[6];
    public bool GroundCoverEnabled=true;
    public int GroundCoverDraws {get;private set;}
    public long GroundCoverBytes {get;private set;}
    public int GroundCoverCandidates {get;private set;}
    static void ReleaseGroundCover(Tile tile)
    {
        ReleaseCoverGpu(tile.Grass);ReleaseCoverGpu(tile.GrassSprites);tile.Grass=null;tile.GrassSprites=null;
        foreach(var page in tile.CoverPages.Values)ReleaseCoverGpu(page);tile.CoverPages.Clear();tile.CoverGrid?.Dispose();tile.CoverGrid=null;
    }
    static void ReleaseCoverGpu(CoverGpu gpu)
    {if(gpu==null)return;gpu.Roots?.Dispose();gpu.Args?.Dispose();gpu.Count?.Dispose();foreach(var visible in gpu.Visible)visible?.Dispose();}
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
        InitializeGroundFloraMaterials();
    }
    void InitializeGroundFloraMaterials()
    {
        if(landMaterial==null)return;
        if(grassFieldTexture==null)grassFieldTexture=CreateGrassFieldTexture();
        if(mossFieldTexture==null)mossFieldTexture=CreateGroundMatTexture(false);
        if(lichenFieldTexture==null)lichenFieldTexture=CreateGroundMatTexture(true);
        landMaterial.SetTexture("_GrassField",grassFieldTexture);landMaterial.SetTexture("_MossField",mossFieldTexture);landMaterial.SetTexture("_LichenField",lichenFieldTexture);
    }
    // Unity-main-thread staging only. Every consumer overwrites every pixel and
    // SetPixels/Apply copies it before the next consumer. Four normal grid sizes
    // retain ~89KiB instead of three fresh Color arrays for every terrain upload.
    static readonly Dictionary<int,Color[]> surfacePixelStaging=new Dictionary<int,Color[]>();
    static Color[] SurfacePixelStaging(int count)
    {
        if(WorldGenPerformance.Original||count>65*65)return new Color[count];
        if(!surfacePixelStaging.TryGetValue(count,out var pixels))
        {pixels=new Color[count];if(surfacePixelStaging.Count<8)surfacePixelStaging.Add(count,pixels);}
        return pixels;
    }
    static Texture2D CreateFloraSurface(Tile tile)
    {
        if(tile.Cover==null||tile.Cover.Length==0)return null;
        int side=tile.Segments+1;var pixels=SurfacePixelStaging(side*side);
        for(int i=0;i<pixels.Length;i++)
        {
            int at=i*16;float total=tile.Cover[at+3],moss=tile.Cover[at+10],lichen=tile.Cover[at+11];
            pixels[i]=new Color(total,moss,lichen,Math.Max(0,total-moss-lichen));
        }
        var texture=new Texture2D(side,side,TextureFormat.RGBAHalf,false,true){name="Ground flora material weights",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        texture.SetPixels(pixels);texture.Apply(false,true);return texture;
    }
    static void BindFloraSurface(Material properties,Tile tile)
    {
        properties.SetFloat("_FloraSurfaceEnabled",tile.FloraSurface==null?0:1);
        if(tile.FloraSurface!=null)properties.SetTexture("_FloraSurface",tile.FloraSurface);
        properties.SetFloat("_MidSurfaceEnabled",tile.MidSurface==null?0:1);
        if(tile.MidSurface!=null)properties.SetTexture("_MidSurface",tile.MidSurface);
        properties.SetFloat("_ForestSurfaceEnabled",tile.ForestSurface==null?0:1);
        if(tile.ForestSurface!=null)properties.SetTexture("_ForestSurface",tile.ForestSurface);
    }
    static Texture2D CreateGroundMatTexture(bool lichen)
    {
        const int size=256;var heights=new float[size*size];
        float Hash(int x,int z){uint h=unchecked((uint)x*73856093u^(uint)z*19349663u^(lichen?0x239ad2u:0x51937u));h^=h>>16;h*=0x7feb352du;h^=h>>15;return (h&65535)/65535f;}
        float Noise(int x,int z,int period)
        {
            float px=x*period/(float)size,pz=z*period/(float)size;int ix=Mathf.FloorToInt(px),iz=Mathf.FloorToInt(pz);
            float a=px-ix,b=pz-iz;a=a*a*(3-2*a);b=b*b*(3-2*b);int mask=period-1;
            return Mathf.Lerp(Mathf.Lerp(Hash(ix&mask,iz&mask),Hash((ix+1)&mask,iz&mask),a),Mathf.Lerp(Hash(ix&mask,(iz+1)&mask),Hash((ix+1)&mask,(iz+1)&mask),a),b);
        }
        float mean=0;
        for(int z=0;z<size;z++)for(int x=0;x<size;x++)
        {
            float clump=Noise(x,z,8),grain=Noise(x,z,64);
            float h=lichen?Mathf.SmoothStep(.12f,.85f,clump)*.65f+grain*.16f:clump*.42f+Noise(x,z,32)*.36f+grain*.22f;
            heights[z*size+x]=h;mean+=h;
        }
        mean/=heights.Length;var pixels=new Color32[heights.Length];
        for(int z=0;z<size;z++)for(int x=0;x<size;x++)
        {
            float sx=heights[z*size+((x+1)&255)]-heights[z*size+((x-1)&255)],sz=heights[((z+1)&255)*size+x]-heights[((z-1)&255)*size+x];
            pixels[z*size+x]=new Color(.5f+(heights[z*size+x]-mean)*.65f,.5f-sx*1.8f,.5f-sz*1.8f,heights[z*size+x]);
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name=lichen?"Stationary lichen crust":"Stationary moss cushions",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Repeat,anisoLevel=8};
        texture.SetPixels32(pixels);texture.Apply(true,true);return texture;
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
        double spacing=sprites?1.2:.60,pageSize=sprites?192:48;
        // Stable, world-addressed pages. Movement never invalidates retained
        // pages just because floor(player/64) changed.
        double minX=Math.Max(key.X*key.SizeM,wx*pageSize),maxX=Math.Min((key.X+1)*key.SizeM,(wx+1)*pageSize);
        double minZ=Math.Max(key.Z*key.SizeM,wz*pageSize),maxZ=Math.Min((key.Z+1)*key.SizeM,(wz+1)*pageSize);
        int x0=(int)Math.Ceiling(minX/spacing-.5),x1=(int)Math.Ceiling(maxX/spacing-.5);
        int z0=(int)Math.Ceiling(minZ/spacing-.5),z1=(int)Math.Ceiling(maxZ/spacing-.5);
        int width=Math.Max(0,x1-x0),height=Math.Max(0,z1-z0),count=width*height;
        if(count==0||count>(sprites?1000000:450000))return null;
        var gpu=new CoverGpu{WindowX=wx,WindowZ=wz,Candidates=count,Sprites=sprites,
            LocalBounds=tile.Root==null?new Bounds():tile.Root.GetComponentInChildren<MeshFilter>().sharedMesh.bounds};
        try
        {
            if(tile.CoverGrid==null){tile.CoverGrid=new GraphicsBuffer(GraphicsBuffer.Target.Structured,tile.Cover.Length,4);tile.CoverGrid.SetData(tile.Cover);}
            gpu.Grid=tile.CoverGrid;
            var bounds=gpu.LocalBounds;bounds.center=new Vector3((float)((minX+maxX)*.5-key.X*key.SizeM),bounds.center.y,(float)((minZ+maxZ)*.5-key.Z*key.SizeM));
            bounds.size=new Vector3((float)(maxX-minX),bounds.size.y,(float)(maxZ-minZ));gpu.LocalBounds=bounds;
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
            gpu.Bytes=(long)count*116+4+5*GraphicsBuffer.IndirectDrawArgs.size;
            return gpu;
        }
        catch{ReleaseCoverGpu(gpu);throw;}
    }
    void CullGroundCover(CoverGpu gpu,Vector4 origin,Camera camera,Tile tile=null,float tileSize=1,bool frustumReady=false)
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
        if(!frustumReady)
        {
            GeometryUtility.CalculateFrustumPlanes(camera,coverFrustum);
            for(int k=0;k<6;k++)coverCullPlanes[k]=new Vector4(coverFrustum[k].normal.x,coverFrustum[k].normal.y,coverFrustum[k].normal.z,coverFrustum[k].distance);
        }
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
        GroundCoverDraws=0;GroundCoverBytes=0;GroundCoverCandidates=0;GrassPagesOccluded=0;
        if(!GroundCoverEnabled||!TerrainVideoOptions.GroundCover||!ScatterEnabled||view==null||terrain==null)
        {foreach(var entry in tiles)ReleaseGroundCover(entry.Value);return;}
        InitializeGroundCover();if(groundCoverMaterial==null)return;
        coverRequests.Clear();
        GeometryUtility.CalculateFrustumPlanes(view,coverFrustum);
        for(int k=0;k<6;k++)coverCullPlanes[k]=new Vector4(coverFrustum[k].normal.x,coverFrustum[k].normal.y,coverFrustum[k].normal.z,coverFrustum[k].distance);
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
        // Discover missing pages first, then install near-first. Existing pages
        // keep drawing; only pages well outside the draw radius are evicted.
        foreach(var pair in tiles)
        {
            var key=pair.Key;var tile=pair.Value;
            if(key.SizeM>Math.Max(2048,TerrainLod.BaseSizeM*HorizontalScale)*1.001||tile.Cover==null||tile.Cover.Length==0||CoverFullyHidden(tile.CoverageNodes)||!WorldGenPerformance.Original&&!HasMovingCover(tile)){ReleaseGroundCover(tile);continue;}
            expiredCoverPages.Clear();
            foreach(var page in tile.CoverPages.Keys)
            {
                double keep=(page.Far?grassRange:bladeRange)+page.Size;
                if(page.Far&&!TerrainVideoOptions.GrassCanopy||PageDistanceSquared(key,page)>keep*keep)expiredCoverPages.Add(page);
            }
            foreach(var page in expiredCoverPages){ReleaseCoverGpu(tile.CoverPages[page]);tile.CoverPages.Remove(page);}
            for(int pass=0;pass<2;pass++)
            {
                bool far=pass==1;if(far?!TerrainVideoOptions.GrassCanopy:key.Level!=0)continue;
                double size=far?192:48,range=(far?grassRange:bladeRange)+size*.5;
                if(TerrainLod.DistanceSquared(key,motion.X,motion.Z)>range*range)continue;
                int x0=(int)Math.Floor(Math.Max(key.X*key.SizeM,motion.X-range)/size),x1=(int)Math.Floor(Math.Min((key.X+1)*key.SizeM-.001,motion.X+range)/size);
                int z0=(int)Math.Floor(Math.Max(key.Z*key.SizeM,motion.Z-range)/size),z1=(int)Math.Floor(Math.Min((key.Z+1)*key.SizeM-.001,motion.Z+range)/size);
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
                {
                    var page=new CoverPageKey(x,z,far);double distance=PageDistanceSquared(key,page);
                    if(distance<=range*range&&!tile.CoverPages.ContainsKey(page))coverRequests.Add(new CoverRequest{Key=key,Tile=tile,Page=page,Priority=distance+(far?1e8:0)});
                }
            }
        }
        coverRequests.Sort((a,b)=>a.Priority.CompareTo(b.Priority));
        if(CenterReady)for(int i=0;i<Math.Min(2,coverRequests.Count);i++)
        {
            var request=coverRequests[i];var added=PrepareGroundCover(request.Key,request.Tile,request.Page.X,request.Page.Z,request.Page.Far);
            if(added!=null)request.Tile.CoverPages.Add(request.Page,added);
        }
        foreach(var pair in tiles)
        {
            var key=pair.Key;var tile=pair.Value;
            if(tile.CoverGrid!=null)GroundCoverBytes+=(long)tile.Cover.Length*4;
            foreach(var page in tile.CoverPages)
            {
            var gpu=page.Value;bool sprites=page.Key.Far;double distance2=PageDistanceSquared(key,page.Key);
            GroundCoverBytes+=gpu.Bytes;GroundCoverCandidates+=gpu.Candidates;
            float range=sprites?grassRange:bladeRange;if(distance2>range*range)continue;
            var bounds=gpu.LocalBounds;bounds.center+=tile.Root.transform.position;bounds.Expand(10);
            if(!GeometryUtility.TestPlanesAABB(coverFrustum,bounds))continue;
            var localBounds=bounds;localBounds.center=transform.InverseTransformPoint(bounds.center);
            if(TerrainOccluded(localBounds,view.transform.localPosition)){GrassPagesOccluded++;continue;}
            CullGroundCover(gpu,new Vector4((float)(key.X*key.SizeM-originX),0,(float)(key.Z*key.SizeM-originZ),0),view,tile,(float)key.SizeM,true);
            for(int lod=0;lod<5;lod++)
            {
                if(sprites!=(lod>=2))continue;
                if(lod==0&&distance2>.36*bladeRange*bladeRange||lod==1&&distance2>bladeRange*bladeRange)continue;
                if(lod==2&&distance2>=4*bladeRange*bladeRange||lod==3&&distance2>=16*bladeRange*bladeRange)continue;
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
    double PageDistanceSquared(TerrainTileKey tile,CoverPageKey page)
    {
        double size=page.Size,minX=Math.Max(tile.X*tile.SizeM,page.X*size),maxX=Math.Min((tile.X+1)*tile.SizeM,(page.X+1)*size);
        double minZ=Math.Max(tile.Z*tile.SizeM,page.Z*size),maxZ=Math.Min((tile.Z+1)*tile.SizeM,(page.Z+1)*size);
        double dx=Math.Max(0,Math.Max(minX-motion.X,motion.X-maxX)),dz=Math.Max(0,Math.Max(minZ-motion.Z,motion.Z-maxZ));return dx*dx+dz*dz;
    }
    static bool HasMovingCover(Tile tile)
    {
        if(tile.MovingCover>=0)return tile.MovingCover!=0;
        var values=tile.Cover;
        if(values==null||values.Length%16!=0)return true; // Unknown is not empty.
        for(int at=0;at<values.Length;at+=16)
        {
            float moving=values[at+3]-values[at+10]-values[at+11];
            if(float.IsNaN(moving)||float.IsInfinity(moving)||moving>0){tile.MovingCover=1;return true;}
        }
        // The placement kernel interpolates these same channels linearly before
        // subtracting stationary mats. All-zero/nonpositive vertex support cannot
        // produce its >=.002 moving-cover admission anywhere inside a triangle.
        tile.MovingCover=0;return false;
    }
}
