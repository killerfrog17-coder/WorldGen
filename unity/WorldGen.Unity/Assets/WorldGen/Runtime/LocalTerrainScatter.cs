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
    sealed class ScatterGroup {public Bounds Bounds;public int[] Indices;}
    sealed class ScatterChunk { public ScatterPatch Source;public Matrix4x4[] Matrices;public Vector3[] Roots;public Bounds[] Bounds;public Bounds AllBounds;public ScatterGroup[] Groups;public LocalTerrainFrame GroupsFrame; public float Born; }
    sealed class ArtPart { public Mesh Mesh;public Material Material;public int Submesh;public Matrix4x4 Local; }
    sealed class ArtModel { public ArtPart[] Parts;public Matrix4x4 Scale; public bool IdentityScale,Fades;public double LastUsed;public bool Generated; }
    sealed class DrawBatch
    {
        // Nonuniform scale needs both transform and inverse; stay below the platform's 511-instance limit.
        public readonly Matrix4x4[] Matrices=new Matrix4x4[256];public readonly Vector4[] Colors=new Vector4[256];
        public readonly Vector4[] Fades=new Vector4[256];
        public readonly List<Vector4> UsedColors=new List<Vector4>(256),UsedFades=new List<Vector4>(256);
        public readonly MaterialPropertyBlock Properties=new MaterialPropertyBlock();public int Count;public long Key;public bool CompactProperties;public ArtModel Model;public ShadowCastingMode Shadows;public Bounds Bounds;
        public Matrix4x4[][] PartMatrices;
    }
    LocalScatter scatter;LocalTerrainArtSet artSet;Texture2DArray[] generatedGround;
    Texture2DArray treeLeaves;
    readonly Dictionary<long,Material> treeMaterials=new Dictionary<long,Material>();
    readonly HashSet<TerrainTileKey> scatterGroundChanges=new HashSet<TerrainTileKey>();
    readonly Dictionary<ScatterKey,ScatterChunk> scatterChunks=new Dictionary<ScatterKey,ScatterChunk>();
    readonly List<ScatterWork> scatterJobs=new List<ScatterWork>();readonly HashSet<ScatterKey> scatterWanted=new HashSet<ScatterKey>();readonly List<ScatterKey> scatterPriority=new List<ScatterKey>();
    readonly Dictionary<long,ArtModel> scatterArt=new Dictionary<long,ArtModel>();readonly List<Mesh> ownedScatterMeshes=new List<Mesh>();readonly List<Material> ownedScatterMaterials=new List<Material>();
    readonly List<DrawBatch> drawBatches=new List<DrawBatch>();readonly List<DrawBatch> batchPool=new List<DrawBatch>();
    readonly Dictionary<long,DrawBatch> activeScatterBatches=new Dictionary<long,DrawBatch>();
    readonly HashSet<ScatterKind> prefabScatterKinds=new HashSet<ScatterKind>();
    double scatterX=double.NaN,scatterZ,scatterAltitude,lastBatchTime,lastGroundProject;bool scatterDirty=true;int nearGroundRevision,projectedGroundRevision;
    Light precedingSun,localSun;AmbientMode precedingAmbient;Color precedingSky,precedingEquator,precedingGround;bool lightingSaved;double nextDaylightAt;
    public int ScatterObjects { get;private set; }
    public int ScatterDrawCalls { get;private set; }
    public int ScatterChunks=>scatterChunks.Count;
    public int SpecialistObjects {get;private set;}
    public int MidPlantObjects {get;private set;}
    public int GeneratedTreeObjects {get;private set;}
    static bool IsSpecialist(ScatterKind kind)=>kind==ScatterKind.Herb||kind==ScatterKind.Fungus;
    static long ScatterArtKey(ScatterKind kind,int variant,int lod)=>((long)(uint)variant<<8)|((long)(int)kind<<2)|(uint)lod;
    public bool ScatterReady=>scatter!=null&&scatterWanted.All(k=>scatterChunks.ContainsKey(k));
    public bool ScatterEnabled=true;
    void InitializeScatter()
    {
        scatter=new LocalScatter(terrain,HorizontalScale);artSet=Resources.Load<LocalTerrainArtSet>("LocalTerrainArtSet");
        if(artSet!=null&&artSet.Scatter!=null)foreach(var binding in artSet.Scatter)
            if(binding!=null&&(binding.Near!=null||binding.Middle!=null||binding.Far!=null))prefabScatterKinds.Add(binding.Kind);
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
        RefreshScatterWanted();foreach(var chunk in scatterChunks.Values)ProjectScatter(chunk);scatterGroundChanges.Clear();scatterDirty=true;
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
        {
            foreach(var chunk in scatterChunks.Values)
            {
                bool affected=scatterGroundChanges.Count==0||chunk.Bounds==null;
                foreach(var key in scatterGroundChanges)
                {
                    var b=chunk.AllBounds;double x0=key.X*key.SizeM-originX,z0=key.Z*key.SizeM-originZ;
                    if(b.max.x>=x0&&b.min.x<=x0+key.SizeM&&b.max.z>=z0&&b.min.z<=z0+key.SizeM){affected=true;break;}
                }
                if(affected)ProjectScatter(chunk);
            }
            scatterGroundChanges.Clear();projectedGroundRevision=nearGroundRevision;lastGroundProject=Time.realtimeSinceStartupAsDouble;scatterDirty=true;
        }
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
        if(scatterDirty||ScatterViewChanged||Time.realtimeSinceStartupAsDouble-lastBatchTime>1)BuildScatterBatches();
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
        var source=chunk.Source.Instances;
        if(chunk.Matrices==null||chunk.Matrices.Length!=source.Length){chunk.Matrices=new Matrix4x4[source.Length];chunk.Roots=new Vector3[source.Length];}
        for(int i=0;i<source.Length;i++)
        {
            var p=source[i];double dot=Math.Max(.1,V.Dot(p.Position,frame.Origin));double x=V.Dot(p.Position,frame.East)*frame.RadiusM/dot,z=V.Dot(p.Position,frame.North)*frame.RadiusM/dot;
            double y=VisibleGround(x,z,frame.SurfaceY(p.Position,p.GroundM));
            bool herb=IsSpecialist(p.Kind),vertical=herb||p.Kind==ScatterKind.GeneratedTree||p.Kind==ScatterKind.MidPlant||p.Kind==ScatterKind.Broadleaf||p.Kind==ScatterKind.Conifer||p.Kind==ScatterKind.SavannaTree;
            V up=vertical?p.Position:p.Normal;var normal=new Vector3((float)V.Dot(up,frame.East),(float)V.Dot(up,frame.Origin),(float)V.Dot(up,frame.North)).normalized;
            // An ellipsoidal mesh touching at its bottom point looks levitating.
            // Embed its broad lower body; outcrops are bedrock, not balanced eggs.
            var position=new Vector3((float)(x-originX),(float)y-(herb?.003f:vertical?.045f:p.Kind==ScatterKind.Outcrop?(float)p.HeightM*.55f:p.Kind==ScatterKind.Boulder?(float)p.HeightM*.35f:.015f),(float)(z-originZ));chunk.Roots[i]=position;
            var rotation=Quaternion.FromToRotation(Vector3.up,normal)*Quaternion.Euler(0,(float)p.YawDegrees,0);
            chunk.Matrices[i]=Matrix4x4.TRS(position,rotation,new Vector3((float)p.WidthM,(float)p.HeightM,(float)p.WidthM));
        }
        PrepareScatterBounds(chunk);
    }
    ArtModel Art(ScatterKind kind,int variant,int lod)
    {
        long key=ScatterArtKey(kind,variant,lod);if(scatterArt.TryGetValue(key,out var found)){found.LastUsed=Time.realtimeSinceStartupAsDouble;return found;}
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
                {var material=new Material(renderer.sharedMaterials[sub]){enableInstancing=true};if(material.HasProperty("_FogDistance"))material.SetFloat("_FogDistance",landMaterial.GetFloat("_FogDistance"));ownedScatterMaterials.Add(material);parts.Add(new ArtPart{Mesh=filter.sharedMesh,Material=material,Submesh=sub,Local=local});}
            }
        }
        if(parts.Count==0)
        {
            var mesh=LocalScatterMeshes.Build(kind,lod,variant);ownedScatterMeshes.Add(mesh);bounds=mesh.bounds;
            var shader=Resources.Load<Shader>("WorldGenLocalScatter");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Scatter shader missing or unsupported.");
            // A Resources material advertises runtime instancing to the player shader stripper.
            var template=Resources.Load<Material>("LocalScatterMaterial");if(template==null)throw new InvalidOperationException("Scatter instancing material missing.");
            bool rock=kind==ScatterKind.Boulder||kind==ScatterKind.Stone||kind==ScatterKind.Outcrop;
            long materialKey=ScatterArtKey(kind,variant&~7,lod<2?0:2);
            Material sharedMaterial=null;bool shared=kind==ScatterKind.GeneratedTree&&treeMaterials.TryGetValue(materialKey,out sharedMaterial);
            var material=shared?sharedMaterial:new Material(template){enableInstancing=true};
            material.SetFloat("_Wind",kind==ScatterKind.Grass?.045f:rock||kind==ScatterKind.Fungus?0:kind==ScatterKind.Herb||kind==ScatterKind.MidPlant?.85f:.018f);material.SetFloat("_HerbWind",kind==ScatterKind.Herb||kind==ScatterKind.MidPlant?1:0);material.SetFloat("_FogDistance",landMaterial.GetFloat("_FogDistance"));
            if(kind==ScatterKind.Herb)
            {
                var motion=PlantMotion.Herb(variant);
                material.SetVector("_PlantTraits",new Vector4((float)motion.Flexibility,(float)motion.Response,(float)motion.Damping,Math.Max(.001f,bounds.max.y)));
            }
            if(kind==ScatterKind.MidPlant)
            {
                var morphology=new MidMorphology(variant);material.SetFloat("_Wind",(float)morphology.Flexibility);
                if(lod==2)material.SetFloat("_Wind",0); // Sub-pixel motion is not worth far vertex work.
                material.SetVector("_PlantTraits",new Vector4((float)morphology.Flexibility,.5f,.7f,1));
            }
            if(kind==ScatterKind.GeneratedTree)
            {
                var morphology=new TreeMorphology(variant);material.SetFloat("_TreeSurface",1);
                if(treeLeaves==null)treeLeaves=LocalTreeLeaves.Create();material.SetTexture("_TreeLeaves",treeLeaves);
                material.SetFloat("_TreeLeafShape",(int)morphology.LeafShape);material.SetFloat("_TreeBarkPattern",(int)morphology.BarkPattern);
                material.SetFloat("_Wind",lod<2?(float)morphology.Flexibility:0);
                material.SetVector("_PlantTraits",new Vector4((float)morphology.Flexibility,.5f,.7f,1));
            }
            if(rock)
            {
                material.SetFloat("_RockLayer",8+variant/3);material.SetTexture("_RockAlbedo",landMaterial.GetTexture("_GroundAlbedo"));
                material.SetTexture("_RockNormals",landMaterial.GetTexture("_GroundNormals"));material.SetFloat("_RockLayers",landMaterial.GetFloat("_RockLayers"));
            }
            if(!shared){ownedScatterMaterials.Add(material);if(kind==ScatterKind.GeneratedTree)treeMaterials[materialKey]=material;}
            parts.Add(new ArtPart{Mesh=mesh,Material=material,Submesh=0,Local=Matrix4x4.identity});
        }
        float height=kind==ScatterKind.MidPlant||kind==ScatterKind.GeneratedTree?1:binding!=null&&binding.ReferenceHeightM>0?binding.ReferenceHeightM:Math.Max(.001f,bounds.size.y),width=kind==ScatterKind.MidPlant||kind==ScatterKind.GeneratedTree?1:binding!=null&&binding.ReferenceWidthM>0?binding.ReferenceWidthM:Math.Max(.001f,Math.Max(bounds.size.x,bounds.size.z));
        var model=new ArtModel{Parts=parts.ToArray(),Scale=Matrix4x4.Scale(new Vector3(1/width,1/height,1/width)),IdentityScale=width==1&&height==1,Fades=parts.All(part=>part.Material.HasProperty("_LodTransition")),Generated=prefab==null,LastUsed=Time.realtimeSinceStartupAsDouble};scatterArt[key]=model;return model;
    }
    void BuildScatterBatches()
    {
        lastBatchTime=Time.realtimeSinceStartupAsDouble;scatterDirty=false;
        foreach(var batch in drawBatches){batch.Count=0;batchPool.Add(batch);}drawBatches.Clear();activeScatterBatches.Clear();
        var eye=view.transform.localPosition;scatterEye=eye;scatterRotation=view.transform.localRotation;scatterProjection=view.projectionMatrix;scatterOptions=ScatterOptionsStamp;
        GeometryUtility.CalculateFrustumPlanes(view,scatterFrustum);
        PrepareVegetationShadowVisibility();
        ScatterObjects=0;SpecialistObjects=0;MidPlantObjects=0;GeneratedTreeObjects=0;ScatterFrustumRejected=ScatterTerrainRejected=ScatterShadowOnly=0;
        foreach(var pair in scatterChunks)
        {
            var chunk=pair.Value;
            if(chunk.Bounds==null)PrepareScatterBounds(chunk);
            var chunkBounds=chunk.AllBounds;chunkBounds.Expand(10+(chunkBounds.center-eye).magnitude*.04f);
            if(TerrainVideoOptions.ScatterFrustum&&!GeometryUtility.TestPlanesAABB(scatterFrustum,chunkBounds)&&!MayCastVisibleShadow(chunkBounds,eye))
            {ScatterFrustumRejected+=chunk.Source.Instances.Length;continue;}
            bool grouped=!WorldGenPerformance.Original&&chunk.Groups!=null&&chunk.Groups.Length>1;
            for(int region=0;region<(grouped?chunk.Groups.Length:1);region++)
            {
            var group=grouped?chunk.Groups[region]:null;
            if(grouped&&TerrainVideoOptions.ScatterFrustum)
            {
                var groupBounds=group.Bounds;
                // Include the largest individual visibility margin anywhere in
                // this bin. Reject only if NONE can be visible or cast a shadow.
                groupBounds.Expand(10+((groupBounds.center-eye).magnitude+groupBounds.extents.magnitude)*.04f);
                if(!GeometryUtility.TestPlanesAABB(scatterFrustum,groupBounds)&&!MayCastVisibleShadow(groupBounds,eye))
                {ScatterFrustumRejected+=group.Indices.Length;continue;}
            }
            for(int member=0;member<(grouped?group.Indices.Length:chunk.Source.Instances.Length);member++)
            {
                int i=grouped?group.Indices[member]:member;
                var p=chunk.Source.Instances[i];double distance=(chunk.Roots[i]-eye).magnitude;
                if(IsSpecialist(p.Kind)&&!TerrainVideoOptions.SpecialistPlants)continue;
                if(p.Kind==ScatterKind.MidPlant&&!TerrainVideoOptions.MidPlants)continue;
                if(p.Kind==ScatterKind.GeneratedTree&&!TerrainVideoOptions.GeneratedTrees)continue;
                if(!TerrainVideoOptions.KeepVegetation(p.Kind,p.Id))continue;
                var blend=TerrainVideoOptions.VegetationBlend(p.Kind,p.WidthM,distance);if(blend.Visibility<=0)continue;
                var bounds=chunk.Bounds[i];bounds.Expand(6+(float)distance*.04f);
                bool frustum=TerrainVideoOptions.ScatterFrustum&&!GeometryUtility.TestPlanesAABB(scatterFrustum,bounds);
                bool occluded=!frustum&&TerrainOccluded(bounds,eye),shadowOnly=frustum||occluded;
                if(frustum)ScatterFrustumRejected++;if(occluded)ScatterTerrainRejected++;
                if(shadowOnly&&(p.Kind==ScatterKind.Grass||IsSpecialist(p.Kind)||!MayCastVisibleShadow(bounds,eye)))continue;
                if(shadowOnly&&(p.Kind==ScatterKind.MidPlant&&!TerrainVideoOptions.MidPlantShadows||p.Kind==ScatterKind.GeneratedTree&&!TerrainVideoOptions.TreeShadows))continue;
                int minimum=p.Kind==ScatterKind.MidPlant?2-TerrainVideoOptions.MidPlantDetail:p.Kind==ScatterKind.GeneratedTree?2-TerrainVideoOptions.TreeDetail:IsSpecialist(p.Kind)?1-TerrainVideoOptions.SpecialistDetail:0;
                int nearLod=Math.Max(minimum,blend.Near),farLod=Math.Max(minimum,blend.Far);
                if(shadowOnly&&p.Kind==ScatterKind.GeneratedTree){nearLod=Math.Max(1,nearLod);farLod=Math.Max(1,farLod);}
                // Built-in generated art always supports distance intervals.
                bool compatible=!prefabScatterKinds.Contains(p.Kind)||
                    Art(p.Kind,p.Variant,nearLod).Fades&&Art(p.Kind,p.Variant,farLod).Fades;
                if(nearLod==farLod)AddScatterInstance(chunk,i,p,nearLod,0,(float)blend.Visibility,shadowOnly);
                else if(!compatible)AddScatterInstance(chunk,i,p,blend.Blend<.5?nearLod:farLod,0,1,shadowOnly);
                else {float split=(float)(blend.Visibility*(1-blend.Blend));AddScatterInstance(chunk,i,p,nearLod,0,split,shadowOnly);AddScatterInstance(chunk,i,p,farLod,split,(float)blend.Visibility,shadowOnly);}
                if(shadowOnly){ScatterShadowOnly++;continue;}ScatterObjects++;
                if(IsSpecialist(p.Kind))SpecialistObjects++;
                if(p.Kind==ScatterKind.MidPlant)MidPlantObjects++;
                if(p.Kind==ScatterKind.GeneratedTree)GeneratedTreeObjects++;
            }
            }
        }
        foreach(var batch in drawBatches)
        {
            // Most species/LOD batches are much smaller than the safety capacity.
            // Upload only live property entries, not 256 colours + fades per draw.
            // Clear resets Unity's fixed-on-first-set array lengths when reusing a block.
            if(WorldGenPerformance.Original)
            {if(batch.CompactProperties)batch.Properties.Clear();batch.CompactProperties=false;batch.Properties.SetVectorArray("_Tint",batch.Colors);batch.Properties.SetVectorArray("_LodTransition",batch.Fades);}
            else
            {
                batch.Properties.Clear();batch.CompactProperties=true;
                batch.UsedColors.Clear();batch.UsedFades.Clear();
                for(int i=0;i<batch.Count;i++){batch.UsedColors.Add(batch.Colors[i]);batch.UsedFades.Add(batch.Fades[i]);}
                batch.Properties.SetVectorArray("_Tint",batch.UsedColors);batch.Properties.SetVectorArray("_LodTransition",batch.UsedFades);
            }
            if(batch.PartMatrices==null||batch.PartMatrices.Length!=batch.Model.Parts.Length)batch.PartMatrices=new Matrix4x4[batch.Model.Parts.Length][];
            for(int j=0;j<batch.Model.Parts.Length;j++)if(batch.Model.Parts[j].Local!=Matrix4x4.identity)
            {if(batch.PartMatrices[j]==null)batch.PartMatrices[j]=new Matrix4x4[batch.Matrices.Length];for(int i=0;i<batch.Count;i++)batch.PartMatrices[j][i]=batch.Matrices[i]*batch.Model.Parts[j].Local;}
        }
        // Generated regional art is a working set, not a permanent world-wide mesh archive.
        if(scatterArt.Count>384)
        {
            var used=new HashSet<ArtModel>(drawBatches.Select(b=>b.Model));
            foreach(var item in scatterArt.Where(p=>p.Value.Generated&&!used.Contains(p.Value)).OrderBy(p=>p.Value.LastUsed).Take(Math.Min(8,scatterArt.Count-384)).ToArray())
            {
                foreach(var part in item.Value.Parts)
                {
                    ownedScatterMeshes.Remove(part.Mesh);Destroy(part.Mesh);
                    if(scatterArt.Any(other=>other.Key!=item.Key&&other.Value.Parts.Any(p=>p.Material==part.Material)))continue;
                    ownedScatterMaterials.Remove(part.Material);Destroy(part.Material);
                    foreach(var key in treeMaterials.Where(p=>p.Value==part.Material).Select(p=>p.Key).ToArray())treeMaterials.Remove(key);
                }
                scatterArt.Remove(item.Key);
            }
        }
    }
    void AddScatterInstance(ScatterChunk chunk,int index,ScatterInstance p,int lod,float low,float high,bool shadowOnly)
    {
        if(high-low<.0001f)return;
        bool casts=TerrainVideoOptions.VegetationShadows&&p.Kind!=ScatterKind.Grass&&!IsSpecialist(p.Kind)&&lod<(p.Kind==ScatterKind.MidPlant?1:2)&&
            (p.Kind!=ScatterKind.MidPlant||TerrainVideoOptions.MidPlantShadows)&&(p.Kind!=ScatterKind.GeneratedTree||TerrainVideoOptions.TreeShadows);
        if(shadowOnly&&!casts)return;
        long key=ScatterArtKey(p.Kind,p.Variant,lod)|(shadowOnly?1L<<60:0);
        if(!activeScatterBatches.TryGetValue(key,out var batch)||batch.Count==batch.Matrices.Length)
        {
            if(batchPool.Count>0){int last=batchPool.Count-1;batch=batchPool[last];batchPool.RemoveAt(last);}else batch=new DrawBatch();
            batch.Key=key;batch.Model=Art(p.Kind,p.Variant,lod);batch.Shadows=shadowOnly?ShadowCastingMode.ShadowsOnly:casts?ShadowCastingMode.On:ShadowCastingMode.Off;
            activeScatterBatches[key]=batch;drawBatches.Add(batch);
        }
        // Explicit wind-inclusive bounds also protect the engine's group culler.
        if(batch.Count==0)batch.Bounds=chunk.Bounds[index];else batch.Bounds.Encapsulate(chunk.Bounds[index]);
        // Tree/mid models are already unit-normalized. Do not perform thousands
        // of native matrix multiplications by identity whenever the view changes.
        batch.Matrices[batch.Count]=batch.Model.IdentityScale&&!WorldGenPerformance.Original?chunk.Matrices[index]:chunk.Matrices[index]*batch.Model.Scale;
        batch.Colors[batch.Count]=new Vector4((float)p.Color.X,(float)p.Color.Y,(float)p.Color.Z,1);
        batch.Fades[batch.Count]=new Vector4(low,high,0,0);batch.Count++;
    }
    void LateUpdate(){if(!closed){long started=WorldGenPerformance.Begin();DrawScatter();WorldGenPerformance.End(1,started);started=WorldGenPerformance.Begin();DrawGroundCover();WorldGenPerformance.End(2,started);if(weatherCheckTarget!=null&&view!=null)view.Render();}}
    void DrawScatter()
    {
        if(landMaterial!=null)landMaterial.SetFloat("_ForestFarEnabled",TerrainVideoOptions.FarForest?1:0);
        ScatterDrawCalls=0;if(!ScatterEnabled||view==null)return;
        foreach(var batch in drawBatches)for(int j=0;j<batch.Model.Parts.Length;j++)
        {
            var part=batch.Model.Parts[j];
            // Prefab child transforms are art only; default placeholders have an identity part transform.
            Matrix4x4[] matrices=batch.Matrices;
            if(part.Local!=Matrix4x4.identity)matrices=batch.PartMatrices[j];
            var parameters=new RenderParams(part.Material){camera=view,layer=30,worldBounds=batch.Bounds,matProps=batch.Properties,shadowCastingMode=batch.Shadows,receiveShadows=true};
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
        if(treeLeaves!=null)Destroy(treeLeaves);
        foreach(var mesh in ownedScatterMeshes)Destroy(mesh);foreach(var material in ownedScatterMaterials)Destroy(material);
        if(lightingSaved){RenderSettings.sun=precedingSun;RenderSettings.ambientMode=precedingAmbient;RenderSettings.ambientSkyColor=precedingSky;RenderSettings.ambientEquatorColor=precedingEquator;RenderSettings.ambientGroundColor=precedingGround;}
    }
}
