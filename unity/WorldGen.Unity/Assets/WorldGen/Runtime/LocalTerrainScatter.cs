using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class ScatterWork { public ScatterKey Key;public Task<ScatterPatch> Task; }
    sealed class ScatterChunk { public ScatterPatch Source;public Matrix4x4[] Matrices;public Vector3[] Roots; public float Born; }
    sealed class ArtPart { public Mesh Mesh;public Material Material;public int Submesh;public Matrix4x4 Local; }
    sealed class ArtModel { public ArtPart[] Parts;public Matrix4x4 Scale; public bool Fades; }
    sealed class DrawBatch
    {
        // Nonuniform scale needs both transform and inverse; stay below the platform's 511-instance limit.
        public readonly Matrix4x4[] Matrices=new Matrix4x4[256];public readonly Vector4[] Colors=new Vector4[256];
        public readonly Vector4[] Fades=new Vector4[256];
        public readonly MaterialPropertyBlock Properties=new MaterialPropertyBlock();public int Count;public ArtModel Model;public bool Shadows;
        public Matrix4x4[][] PartMatrices;
    }
    LocalScatter scatter;LocalTerrainArtSet artSet;Texture2DArray[] generatedGround;
    readonly Dictionary<ScatterKey,ScatterChunk> scatterChunks=new Dictionary<ScatterKey,ScatterChunk>();
    readonly List<ScatterWork> scatterJobs=new List<ScatterWork>();readonly HashSet<ScatterKey> scatterWanted=new HashSet<ScatterKey>();readonly List<ScatterKey> scatterPriority=new List<ScatterKey>();
    readonly Dictionary<long,ArtModel> scatterArt=new Dictionary<long,ArtModel>();readonly List<Mesh> ownedScatterMeshes=new List<Mesh>();readonly List<Material> ownedScatterMaterials=new List<Material>();
    readonly List<DrawBatch> drawBatches=new List<DrawBatch>();readonly List<DrawBatch> batchPool=new List<DrawBatch>();
    double scatterX=double.NaN,scatterZ,scatterAltitude,lastBatchTime,lastGroundProject;bool scatterDirty=true;int nearGroundRevision,projectedGroundRevision;
    Light precedingSun,localSun;AmbientMode precedingAmbient;Color precedingSky,precedingEquator,precedingGround;bool lightingSaved;double nextDaylightAt;
    public int ScatterObjects { get;private set; }
    public int ScatterDrawCalls { get;private set; }
    public int ScatterChunks=>scatterChunks.Count;
    public int SpecialistObjects {get;private set;}
    static bool IsSpecialist(ScatterKind kind)=>kind==ScatterKind.Herb||kind==ScatterKind.Fungus;
    static long ScatterArtKey(ScatterKind kind,int variant,int lod)=>((long)(uint)variant<<8)|((long)(int)kind<<2)|(uint)lod;
    public bool ScatterReady=>scatter!=null&&scatterWanted.All(k=>scatterChunks.ContainsKey(k));
    public bool ScatterEnabled=true;
    void InitializeScatter()
    {
        scatter=new LocalScatter(terrain,HorizontalScale);artSet=Resources.Load<LocalTerrainArtSet>("LocalTerrainArtSet");
        if(artSet!=null&&artSet.GroundMaterial!=null){var replacement=new Material(artSet.GroundMaterial);Destroy(landMaterial);landMaterial=replacement;SetFog();}
        bool groundArt=artSet!=null&&artSet.Albedo!=null&&artSet.Normals!=null&&artSet.Masks!=null&&artSet.Albedo.depth>=8&&artSet.Normals.depth>=8&&artSet.Masks.depth>=8;
        if(!groundArt)generatedGround=LocalGroundTextures.Create();
        landMaterial.SetTexture("_GroundAlbedo",groundArt?artSet.Albedo:generatedGround[0]);
        landMaterial.SetTexture("_GroundNormals",groundArt?artSet.Normals:generatedGround[1]);
        landMaterial.SetTexture("_GroundMasks",groundArt?artSet.Masks:generatedGround[2]);
        landMaterial.SetFloat("_TextureMetres",artSet==null?2:Math.Max(.05f,artSet.TextureRepeatMetres));
        landMaterial.SetFloat("_RockLayers",groundArt&&artSet.Albedo.depth<14?0:1);
        InitializeGroundFloraMaterials();
        precedingSun=RenderSettings.sun;precedingAmbient=RenderSettings.ambientMode;precedingSky=RenderSettings.ambientSkyColor;precedingEquator=RenderSettings.ambientEquatorColor;precedingGround=RenderSettings.ambientGroundColor;lightingSaved=true;
        var lightRoot=new GameObject("Local sunlight",typeof(Light));lightRoot.transform.SetParent(transform,false);lightRoot.transform.rotation=Quaternion.Euler(48,-35,0);
        var sun=localSun=lightRoot.GetComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.95f,.85f);sun.intensity=2.3f;sun.shadows=LightShadows.Soft;sun.shadowBias=.035f;sun.shadowNormalBias=.2f;sun.cullingMask=1<<30;
        RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.43f,.52f,.64f);RenderSettings.ambientEquatorColor=new Color(.3f,.32f,.28f);RenderSettings.ambientGroundColor=new Color(.16f,.17f,.13f);
        InitializeSky();
    }
    void ResetScatterFrame()
    {
        if(scatter==null)return;scatterX=double.NaN;
        // Texture phase remains planet-addressed across floating origins and chart changes.
        double modulus=1024;V p=frame.Origin*(frame.RadiusM+frame.AltitudeM);
        landMaterial.SetVector("_TextureAnchor",new Vector4((float)(p.X%modulus),(float)(p.Y%modulus),(float)(p.Z%modulus),0));
        landMaterial.SetVector("_PlanetEast",new Vector4((float)frame.East.X,(float)frame.East.Y,(float)frame.East.Z,0));
        landMaterial.SetVector("_PlanetNorth",new Vector4((float)frame.North.X,(float)frame.North.Y,(float)frame.North.Z,0));
        landMaterial.SetVector("_PlanetUp",new Vector4((float)frame.Origin.X,(float)frame.Origin.Y,(float)frame.Origin.Z,0));
        RefreshScatterWanted();foreach(var chunk in scatterChunks.Values)ProjectScatter(chunk);scatterDirty=true;
    }
    void RefreshScatterWanted()
    {
        scatterX=motion.X;scatterZ=motion.Z;scatterAltitude=HeightAboveGround;scatterWanted.Clear();scatterPriority.Clear();
        if(scatterAltitude<2200)
        {
            foreach(var key in scatter.Around(Position,1800))scatterWanted.Add(key);
            if(scatterAltitude<160)foreach(var key in scatter.Around(Position,100,true))scatterWanted.Add(key);
        }
        scatterPriority.AddRange(scatterWanted);var at=Position;
        double Distance(ScatterKey k){V p=scatter.Position(k)-at;return p.X*p.X+p.Y*p.Y+p.Z*p.Z;}
        scatterPriority.Sort((a,b)=>{int d=Distance(a).CompareTo(Distance(b));return d!=0?d:a.CompareTo(b);});
        foreach(var key in scatterChunks.Keys.Where(k=>!scatterWanted.Contains(k)).ToArray())scatterChunks.Remove(key);scatterDirty=true;
    }
    void UpdateScatter()
    {
        if(scatter==null)return;
        if(nearGroundRevision!=projectedGroundRevision&&Time.realtimeSinceStartupAsDouble-lastGroundProject>.25)
        {foreach(var chunk in scatterChunks.Values)ProjectScatter(chunk);projectedGroundRevision=nearGroundRevision;lastGroundProject=Time.realtimeSinceStartupAsDouble;scatterDirty=true;}
        if(double.IsNaN(scatterX)||Math.Abs(motion.X-scatterX)>32||Math.Abs(motion.Z-scatterZ)>32||Math.Abs(HeightAboveGround-scatterAltitude)>100)RefreshScatterWanted();
        for(int i=scatterJobs.Count-1;i>=0;i--)
        {
            var work=scatterJobs[i];if(!work.Task.IsCompleted)continue;scatterJobs.RemoveAt(i);
            if(work.Task.IsFaulted){error=work.Task.Exception.GetBaseException().Message;debugOpen=true;CaptureMouse();continue;}
            if(!work.Task.IsCanceled&&scatterWanted.Contains(work.Key)){var chunk=new ScatterChunk{Source=work.Task.Result,Born=Math.Max(.001f,Time.time)};ProjectScatter(chunk);scatterChunks[work.Key]=chunk;scatterDirty=true;}break;
        }
        if(CenterReady&&!NearTerrainPending()&&jobs.Count+scatterJobs.Count<WorkerLimit&&scatterJobs.Count<1)
        {
            foreach(var key in scatterPriority)
            {if(scatterChunks.ContainsKey(key)||scatterJobs.Any(j=>j.Key.Equals(key)))continue;var source=scatter;var producer=meshProcess;var token=cancellation.Token;scatterJobs.Add(new ScatterWork{Key=key,Task=Task.Run(()=>producer==null?source.Build(key,token):producer.Scatter(key,token),token)});break;}
        }
        if(scatterDirty||Time.realtimeSinceStartupAsDouble-lastBatchTime>.10)BuildScatterBatches();
    }
    double VisibleGround(double x,double z,double fallback)
    {
        for(int level=0;level<=16;level++)
        {
            double size=WorldGen.Presentation.TerrainLod.BaseSizeM*HorizontalScale*(1<<level);var key=new WorldGen.Presentation.TerrainTileKey((int)Math.Floor(x/size),(int)Math.Floor(z/size),level,HorizontalScale);
            if(!tiles.TryGetValue(key,out var tile)||tile.Heights==null)continue;int n=tile.Segments,side=n+1;
            double gx=(x/size-key.X)*n,gz=(z/size-key.Z)*n;int a=Math.Min(n-1,Math.Max(0,(int)gx)),b=Math.Min(n-1,Math.Max(0,(int)gz));double u=gx-a,v=gz-b;
            double h00=tile.Heights[b*side+a],h10=tile.Heights[b*side+a+1],h01=tile.Heights[(b+1)*side+a],h11=tile.Heights[(b+1)*side+a+1];
            return u+v<=1?h00+(h10-h00)*u+(h01-h00)*v:h11+(h01-h11)*(1-u)+(h10-h11)*(1-v);
        }
        return fallback;
    }
    void ProjectScatter(ScatterChunk chunk)
    {
        var source=chunk.Source.Instances;chunk.Matrices=new Matrix4x4[source.Length];chunk.Roots=new Vector3[source.Length];
        for(int i=0;i<source.Length;i++)
        {
            var p=source[i];double dot=Math.Max(.1,V.Dot(p.Position,frame.Origin));double x=V.Dot(p.Position,frame.East)*frame.RadiusM/dot,z=V.Dot(p.Position,frame.North)*frame.RadiusM/dot;
            double y=VisibleGround(x,z,frame.SurfaceY(p.Position,p.GroundM));
            bool herb=IsSpecialist(p.Kind),vertical=herb||p.Kind==ScatterKind.Broadleaf||p.Kind==ScatterKind.Conifer||p.Kind==ScatterKind.SavannaTree;
            V up=vertical?p.Position:p.Normal;var normal=new Vector3((float)V.Dot(up,frame.East),(float)V.Dot(up,frame.Origin),(float)V.Dot(up,frame.North)).normalized;
            // An ellipsoidal mesh touching at its bottom point looks levitating.
            // Embed its broad lower body; outcrops are bedrock, not balanced eggs.
            var position=new Vector3((float)(x-originX),(float)y-(herb?.003f:vertical?.045f:p.Kind==ScatterKind.Outcrop?(float)p.HeightM*.55f:p.Kind==ScatterKind.Boulder?(float)p.HeightM*.35f:.015f),(float)(z-originZ));chunk.Roots[i]=position;
            var rotation=Quaternion.FromToRotation(Vector3.up,normal)*Quaternion.Euler(0,(float)p.YawDegrees,0);
            chunk.Matrices[i]=Matrix4x4.TRS(position,rotation,new Vector3((float)p.WidthM,(float)p.HeightM,(float)p.WidthM));
        }
    }
    ArtModel Art(ScatterKind kind,int variant,int lod)
    {
        long key=ScatterArtKey(kind,variant,lod);if(scatterArt.TryGetValue(key,out var found))return found;
        var binding=artSet==null||artSet.Scatter==null?null:Array.Find(artSet.Scatter,b=>b!=null&&b.Kind==kind);
        GameObject prefab=binding==null?null:lod==0?binding.Near:lod==1?(binding.Middle!=null?binding.Middle:binding.Near):(binding.Far!=null?binding.Far:binding.Middle!=null?binding.Middle:binding.Near);
        var parts=new List<ArtPart>();var bounds=new Bounds();bool first=true;
        if(prefab!=null)
        {
            var group=prefab.GetComponent<LODGroup>();Renderer[] selected=group==null||group.lodCount==0?null:group.GetLODs()[Math.Min(lod,group.lodCount-1)].renderers;
            foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||filter.sharedMesh==null||selected!=null&&!selected.Contains(renderer))continue;
                Matrix4x4 local=prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                Bounds b=filter.sharedMesh.bounds;for(int corner=0;corner<8;corner++){var p=local.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1)));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
                for(int sub=0;sub<filter.sharedMesh.subMeshCount&&sub<renderer.sharedMaterials.Length;sub++)
                {var material=new Material(renderer.sharedMaterials[sub]){enableInstancing=true};ownedScatterMaterials.Add(material);parts.Add(new ArtPart{Mesh=filter.sharedMesh,Material=material,Submesh=sub,Local=local});}
            }
        }
        if(parts.Count==0)
        {
            var mesh=LocalScatterMeshes.Build(kind,lod,variant);ownedScatterMeshes.Add(mesh);bounds=mesh.bounds;
            var shader=Resources.Load<Shader>("WorldGenLocalScatter");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Scatter shader missing or unsupported.");
            // A Resources material advertises runtime instancing to the player shader stripper.
            var template=Resources.Load<Material>("LocalScatterMaterial");if(template==null)throw new InvalidOperationException("Scatter instancing material missing.");
            bool rock=kind==ScatterKind.Boulder||kind==ScatterKind.Stone||kind==ScatterKind.Outcrop;
            var material=new Material(template){enableInstancing=true};material.SetFloat("_Wind",kind==ScatterKind.Grass?.045f:rock||kind==ScatterKind.Fungus?0:kind==ScatterKind.Herb?.85f:.018f);material.SetFloat("_HerbWind",kind==ScatterKind.Herb?1:0);material.SetFloat("_FogDistance",(float)viewDistance);
            if(rock)
            {
                material.SetFloat("_RockLayer",8+variant/3);material.SetTexture("_RockAlbedo",landMaterial.GetTexture("_GroundAlbedo"));
                material.SetTexture("_RockNormals",landMaterial.GetTexture("_GroundNormals"));material.SetFloat("_RockLayers",landMaterial.GetFloat("_RockLayers"));
            }
            ownedScatterMaterials.Add(material);parts.Add(new ArtPart{Mesh=mesh,Material=material,Submesh=0,Local=Matrix4x4.identity});
        }
        float height=binding!=null&&binding.ReferenceHeightM>0?binding.ReferenceHeightM:Math.Max(.001f,bounds.size.y),width=binding!=null&&binding.ReferenceWidthM>0?binding.ReferenceWidthM:Math.Max(.001f,Math.Max(bounds.size.x,bounds.size.z));
        var model=new ArtModel{Parts=parts.ToArray(),Scale=Matrix4x4.Scale(new Vector3(1/width,1/height,1/width)),Fades=parts.All(part=>part.Material.HasProperty("_LodTransition"))};scatterArt[key]=model;return model;
    }
    void BuildScatterBatches()
    {
        lastBatchTime=Time.realtimeSinceStartupAsDouble;scatterDirty=false;
        foreach(var batch in drawBatches){batch.Count=0;batchPool.Add(batch);}drawBatches.Clear();var active=new Dictionary<long,DrawBatch>();
        var eye=view.transform.localPosition;ScatterObjects=0;SpecialistObjects=0;
        foreach(var pair in scatterChunks)
        {
            var chunk=pair.Value;
            for(int i=0;i<chunk.Source.Instances.Length;i++)
            {
                var p=chunk.Source.Instances[i];double distance=(chunk.Roots[i]-eye).magnitude;
                if(IsSpecialist(p.Kind)&&!TerrainVideoOptions.SpecialistPlants)continue;
                var blend=ScatterLodBlend.At(p.Kind,p.WidthM,distance);if(blend.Visibility<=0)continue;
                void Add(int lod,float low,float high)
                {
                    if(high-low<.0001f)return;
                    long key=ScatterArtKey(p.Kind,p.Variant,lod);
                    if(!active.TryGetValue(key,out var batch)||batch.Count==batch.Matrices.Length)
                    {
                        if(batchPool.Count>0){int last=batchPool.Count-1;batch=batchPool[last];batchPool.RemoveAt(last);}else batch=new DrawBatch();
                        batch.Model=Art(p.Kind,p.Variant,lod);batch.Shadows=p.Kind!=ScatterKind.Grass&&!IsSpecialist(p.Kind)&&lod<2;active[key]=batch;drawBatches.Add(batch);
                    }
                    batch.Matrices[batch.Count]=chunk.Matrices[i]*batch.Model.Scale;
                    batch.Colors[batch.Count]=new Vector4((float)p.Color.X,(float)p.Color.Y,(float)p.Color.Z,1);
                    batch.Fades[batch.Count]=new Vector4(low,high,0,0);batch.Count++;
                }
                bool compatible=Art(p.Kind,p.Variant,blend.Near).Fades&&Art(p.Kind,p.Variant,blend.Far).Fades;
                if(!compatible)Add(blend.Blend<.5?blend.Near:blend.Far,0,1);
                else {float split=(float)(blend.Visibility*(1-blend.Blend));Add(blend.Near,0,split);Add(blend.Far,split,(float)blend.Visibility);}
                ScatterObjects++;
                if(IsSpecialist(p.Kind))SpecialistObjects++;
            }
        }
        foreach(var batch in drawBatches)
        {
            batch.Properties.SetVectorArray("_Tint",batch.Colors);
            batch.Properties.SetVectorArray("_LodTransition",batch.Fades);
            if(batch.PartMatrices==null||batch.PartMatrices.Length!=batch.Model.Parts.Length)batch.PartMatrices=new Matrix4x4[batch.Model.Parts.Length][];
            for(int j=0;j<batch.Model.Parts.Length;j++)if(batch.Model.Parts[j].Local!=Matrix4x4.identity)
            {if(batch.PartMatrices[j]==null)batch.PartMatrices[j]=new Matrix4x4[batch.Matrices.Length];for(int i=0;i<batch.Count;i++)batch.PartMatrices[j][i]=batch.Matrices[i]*batch.Model.Parts[j].Local;}
        }
    }
    void LateUpdate(){if(!closed){DrawScatter();DrawGroundCover();if(weatherCheckTarget!=null&&view!=null)view.Render();}}
    void DrawScatter()
    {
        ScatterDrawCalls=0;if(!ScatterEnabled||view==null)return;
        foreach(var batch in drawBatches)for(int j=0;j<batch.Model.Parts.Length;j++)
        {
            var part=batch.Model.Parts[j];
            // Prefab child transforms are art only; default placeholders have an identity part transform.
            Matrix4x4[] matrices=batch.Matrices;
            if(part.Local!=Matrix4x4.identity)matrices=batch.PartMatrices[j];
            var parameters=new RenderParams(part.Material){camera=view,layer=30,matProps=batch.Properties,shadowCastingMode=batch.Shadows?ShadowCastingMode.On:ShadowCastingMode.Off,receiveShadows=true};
            Graphics.RenderMeshInstanced(in parameters,part.Mesh,part.Submesh,matrices,batch.Count);ScatterDrawCalls++;
        }
    }
    void DisposeScatter()
    {
        if(groundCoverMaterial!=null)Destroy(groundCoverMaterial);
        if(groundCanopyMaterial!=null)Destroy(groundCanopyMaterial);
        if(grassFieldTexture!=null)Destroy(grassFieldTexture);
        if(mossFieldTexture!=null)Destroy(mossFieldTexture);if(lichenFieldTexture!=null)Destroy(lichenFieldTexture);
        if(generatedGround!=null)foreach(var texture in generatedGround)Destroy(texture);
        foreach(var mesh in ownedScatterMeshes)Destroy(mesh);foreach(var material in ownedScatterMaterials)Destroy(material);
        if(lightingSaved){RenderSettings.sun=precedingSun;RenderSettings.ambientMode=precedingAmbient;RenderSettings.ambientSkyColor=precedingSky;RenderSettings.ambientEquatorColor=precedingEquator;RenderSettings.ambientGroundColor=precedingGround;}
    }
}
