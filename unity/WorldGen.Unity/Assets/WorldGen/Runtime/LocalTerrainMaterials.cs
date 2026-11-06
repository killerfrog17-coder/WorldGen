using System;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class TerrainMaterialBinding
    {
        public Texture Coverage,Times,Flora,Mid,Forest,Floor;public Vector4 Fade;
        public float Size,Safety;public bool Initialized;
    }
    const int TerrainSceneVectors=19;
    readonly Vector4[] terrainSceneData=new Vector4[TerrainSceneVectors],terrainSceneLast=new Vector4[TerrainSceneVectors];
    GraphicsBuffer terrainSceneBuffer;bool terrainSceneSubscribed;
    readonly System.Collections.Generic.List<Material> legacyVegetationScene=new System.Collections.Generic.List<Material>();
    void RegisterScatterMaterial(Material material)
    {
        ownedScatterMaterials.Add(material);
        ConfigureEfficientTreeMaterial(material);
        if(material.HasProperty("_VegetationSceneEnabled"))material.SetFloat("_VegetationSceneEnabled",1);
        else legacyVegetationScene.Add(material);
    }
    int terrainSecondaryCount;
    static readonly int TerrainSceneId=Shader.PropertyToID("WorldGenTerrainScene");
    public long TerrainMaterialBindingUpdates {get;private set;}
    public long TerrainSceneUploads {get;private set;}
    void EnsureTerrainMaterials(Tile tile)
    {
        if(tile.Materials!=null){ApplyTerrainDiagnosticTile(tile);return;}
        tile.Renderers=tile.Root.GetComponentsInChildren<MeshRenderer>();tile.Materials=new Material[tile.Renderers.Length];
        for(int i=0;i<tile.Renderers.Length;i++)
        {
            var renderer=tile.Renderers[i];var material=new Material(renderer.sharedMaterial){name="Persistent terrain tile material",enableInstancing=false};
            // Synthetic/legacy shader fixtures have no scene owner and keep
            // material-local lighting. Production tile draws use the scene CB.
            if(material.HasProperty("_TerrainSceneEnabled"))material.SetFloat("_TerrainSceneEnabled",landMaterial==null?0:1);
            renderer.sharedMaterial=material;renderer.SetPropertyBlock(null);tile.Materials[i]=material;
        }
        tile.MaterialBinding=new TerrainMaterialBinding();
        ApplyTerrainDiagnosticTile(tile);
    }
    void BindTerrainTile(Tile owner,Tile source,TerrainTileKey key,Vector4 fade)
    {
        EnsureTerrainMaterials(owner);var b=owner.MaterialBinding;
        float size=(float)key.SizeM,safety=view==null?0:(float)Math.Max(12,TerrainLod.BaseSizeM*HorizontalScale/64*3);
        if(b.Initialized&&ReferenceEquals(b.Coverage,source.Coverage)&&ReferenceEquals(b.Times,source.CoverageTimes)&&ReferenceEquals(b.Flora,source.FloraSurface)&&ReferenceEquals(b.Mid,source.MidSurface)&&ReferenceEquals(b.Forest,source.ForestSurface)&&ReferenceEquals(b.Floor,source.ForestFloor)&&b.Fade.Equals(fade)&&b.Size==size&&b.Safety==safety)return;
        foreach(var material in owner.Materials)
        {
            BindFloraSurface(material,source);
            material.SetFloat("_CoverageEnabled",source.CoverageNodes!=null&&source.CoverageNodes.Length>0?1:0);
            material.SetFloat("_CoverageTileSize",size);material.SetTexture("_TerrainCoverage",source.Coverage??Texture2D.blackTexture);material.SetTexture("_CoverageFadeTimes",source.CoverageTimes??Texture2D.blackTexture);
            material.SetFloat("_TransitionEnabled",1);material.SetFloat("_LodSafetyRadius",safety);material.SetVector("_LodFade",fade);
        }
        b.Initialized=true;b.Coverage=source.Coverage;b.Times=source.CoverageTimes;b.Flora=source.FloraSurface;b.Mid=source.MidSurface;b.Forest=source.ForestSurface;b.Floor=source.ForestFloor;b.Fade=fade;b.Size=size;b.Safety=safety;TerrainMaterialBindingUpdates++;
    }
    void PublishTerrainScene()
    {
        if(landMaterial==null||closed)return;
        // ShaderLab Color properties are converted on material upload in a
        // linear project. Raw constant-buffer writes must do that conversion.
        Vector4 Colour(string name){var c=landMaterial.GetColor(name);return QualitySettings.activeColorSpace==ColorSpace.Linear?(Vector4)c.linear:c;}
        terrainSceneData[0]=Colour("_FogColor");terrainSceneData[1]=Colour("_AmbientTint");
        terrainSceneData[2]=new Vector4(landMaterial.GetFloat("_FogDistance"),landMaterial.GetFloat("_Daylight"),TerrainVideoOptions.FarForest?1:0,0);
        terrainSceneData[3]=landMaterial.GetVector("_LocalOrigin");terrainSceneData[4]=waterMaterial==null?Vector4.zero:waterMaterial.GetVector("_LocalOrigin");
        terrainSceneData[5]=landMaterial.GetVector("_TextureAnchor");terrainSceneData[6]=landMaterial.GetVector("_PlanetEast");terrainSceneData[7]=landMaterial.GetVector("_PlanetNorth");terrainSceneData[8]=landMaterial.GetVector("_PlanetUp");
        terrainSceneData[9]=landMaterial.GetVector("_AirGeometry");terrainSceneData[10]=new Vector4(landMaterial.GetFloat("_AirObserverM"),landMaterial.GetFloat("_AirHaze"),terrainSecondaryCount,0);
        for(int i=0;i<4;i++){terrainSceneData[11+i]=secondaryDirections[i];terrainSceneData[15+i]=secondaryColors[i];}
        bool changed=terrainSceneBuffer==null;for(int i=0;i<TerrainSceneVectors&&!changed;i++)changed=!terrainSceneData[i].Equals(terrainSceneLast[i]);
        if(changed)
        {
            if(terrainSceneBuffer==null)terrainSceneBuffer=new GraphicsBuffer(GraphicsBuffer.Target.Constant,TerrainSceneVectors,16);
            terrainSceneBuffer.SetData(terrainSceneData);Array.Copy(terrainSceneData,terrainSceneLast,TerrainSceneVectors);TerrainSceneUploads++;
        }
        if(!terrainSceneSubscribed){RenderPipelineManager.beginCameraRendering+=BindTerrainSceneCamera;terrainSceneSubscribed=true;}
        Shader.SetGlobalConstantBuffer(TerrainSceneId,terrainSceneBuffer,0,TerrainSceneVectors*16);
    }
    void BindTerrainSceneCamera(ScriptableRenderContext context,Camera camera)
    {
        if(presentedTraceSubscribed&&camera==view)presentedCameraTrace=WorldGenPerformance.TraceBegin();
        BindCampLights(camera);
        Shader.SetGlobalVector("_WorldGenWaterWind",camera==view?new Vector4((float)CurrentWeather.WindEastMps,(float)CurrentWeather.WindNorthMps,0,0):Vector4.zero);
        BindSharedTreeWind(camera);
        float treeStart=(float)WorldGen.Presentation.TreeRenderingLod.ViewStart(TerrainVideoOptions.TreeQuality),treePixels=(float)WorldGen.Presentation.TreeRenderingLod.PixelStart(TerrainVideoOptions.TreeQuality);
        Shader.SetGlobalVector("_WorldGenTreeViewRanges",camera==view&&TerrainVideoOptions.EfficientTreeViews?new Vector4(treeStart,treeStart*1.5f,treePixels,treePixels*.5f):Vector4.zero);
        Shader.SetGlobalFloat("_WorldGenTreeViewProjection",camera==view?view.pixelHeight/Mathf.Tan(view.fieldOfView*Mathf.Deg2Rad*.5f):0);
        // The same sky gradient is the opaque endpoint for land, vegetation and
        // animals. Other cameras keep their own legacy/material fog settings.
        var fogZenith=fogSkyZenith;fogZenith.w=landMaterial==null?0:landMaterial.GetFloat("_FogDistance");
        Shader.SetGlobalVector("_WorldGenFogSkyZenith",camera==view?fogZenith:Vector4.zero);
        Shader.SetGlobalVector("_WorldGenFogSkyHorizon",camera==view?fogSkyHorizon:Vector4.zero);
        Shader.SetGlobalVector("_WorldGenFogSkyGlow",camera==view?fogSkyGlow:Vector4.zero);
        Shader.SetGlobalInteger("_TerrainDiagnosticFlags",camera==view?(int)TerrainDiagnostics.Flags:0);
        Shader.SetGlobalVector("_WorldGenFoliageShadowSettings",camera==view&&TerrainVideoOptions.SimplifiedForestShadows?new Vector4(1,TerrainVideoOptions.ForestShadowDistance,TerrainVideoOptions.ForestShadowSoftness,WorldGenPerformance.NoFoliageShadowReceivers?-1:TerrainVideoOptions.ForestShadowSamples):Vector4.zero);
        BindCanopyShade(camera);
        if(camera==view&&frame!=null&&terrain!=null)
        {
            Shader.SetGlobalVector("_TerrainSurfaceRanges",TerrainVideoOptions.SurfaceRanges);
            Shader.SetGlobalVector("_TerrainSurfaceControls",TerrainVideoOptions.SurfaceControls);
            Shader.SetGlobalVector("_TerrainGeologyDatum",new Vector4((float)frame.AltitudeM,(float)(.5/frame.RadiusM),(float)originX,(float)originZ));
            var bedding=frame.Origin*((frame.RadiusM+frame.AltitudeM)*.0001);
            Shader.SetGlobalVector("_TerrainBeddingOrigin",new Vector4((float)bedding.X,(float)bedding.Y,(float)bedding.Z,(terrain.SourceSeed&4095)*.001f));
            Shader.SetGlobalFloat("_TerrainDiagnosticRadius",(float)TerrainVideoOptions.Current.FullRadius(HorizontalScale));
            if(terrainSceneBuffer!=null)Shader.SetGlobalConstantBuffer(TerrainSceneId,terrainSceneBuffer,0,TerrainSceneVectors*16);
        }
    }
    long presentedCameraTrace;bool presentedTraceSubscribed;
    void UpdatePresentedCameraTrace()
    {
        bool wanted=WorldGenPerformance.Presented&&(WorldGenPerformance.MemoryTrace||WorldGenPerformance.TracePresentedCamera);
        if(wanted==presentedTraceSubscribed)return;
        if(wanted)RenderPipelineManager.endCameraRendering+=EndPresentedTerrainCamera;
        else RenderPipelineManager.endCameraRendering-=EndPresentedTerrainCamera;
        presentedTraceSubscribed=wanted;
    }
    void EndPresentedTerrainCamera(ScriptableRenderContext context,Camera camera)
    {if(camera==view){WorldGenPerformance.TraceEnd(8,presentedCameraTrace);presentedCameraTrace=0;}}
    void DisposeTerrainScene()
    {if(terrainSceneSubscribed)RenderPipelineManager.beginCameraRendering-=BindTerrainSceneCamera;if(presentedTraceSubscribed)RenderPipelineManager.endCameraRendering-=EndPresentedTerrainCamera;terrainSceneSubscribed=presentedTraceSubscribed=false;terrainSceneBuffer?.Dispose();terrainSceneBuffer=null;}
}
