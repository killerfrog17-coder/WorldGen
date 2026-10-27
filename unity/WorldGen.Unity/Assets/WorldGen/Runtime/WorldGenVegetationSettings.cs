using System;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public static partial class TerrainVideoOptions
{
    public static int VegetationRevision,SpecialistDetail=1;
    public static float SpecialistDensity=1,MidPlantDensity=1,TreeDensity=1;
    public static float SpecialistNearLod=22,SpecialistDistance=140,FungusDistance=65;
    public static float MidNearLod=55,MidFarLod=200,MidMaxDistance=1400;
    public static float TreeNearLod=120,TreeFarLod=450,TreeMaxDistance=1400;
    public static bool MidPlantShadows=true,TreeShadows=true;
    public static bool SimplifiedForestShadows=true;
    public static bool DistantTreeShadowCards=true,EfficientTreeViews=true;
    public static bool SteadyDaylightShadows=true;
    public static bool DawnDuskShadowBudget=true;
    public static bool EfficientTreeRendering=true;
    public static bool FoliageDepthPrepass=false,FrontToBackTrees=false;
    public static float ForestLightUp=1;
    public static int ForestShadowQuality=2;
    public static float ForestShadowSoftness=2;
    public static bool CanopyShade=true;
    public static float ForestShadowDistance=>DawnDuskShadowBudget?(float)VegetationShadowBudget.ShadowRange(ForestLightUp):180;
    public static int ForestShadowResolution=>Math.Min(ForestShadowQuality<2?1024:ForestShadowQuality==2?2048:4096,DawnDuskShadowBudget?VegetationShadowBudget.ResolutionCap(ForestLightUp):4096);
    public static int ForestShadowSamples=>DawnDuskShadowBudget&&ForestLightUp<.10f?2:Math.Min(ForestShadowQuality==0?2:ForestShadowQuality==4?9:4,DistantTreeShadowCards?4:DawnDuskShadowBudget&&ForestLightUp<.28f?4:9);
    public static int GrassQuality=2,SpecialistQuality=2,MidQuality=2,TreeQuality=2;
    public static float TreeIdentityRange= (float)VegetationQuality.TreeIdentityRange;
    public static void ApplyPlantQuality(int layer,int quality)
    {
        int q=Mathf.Clamp(quality,0,4);var p=VegetationQuality.At(q);
        if(layer==0){GrassQuality=q;GrassDensity=(float)p.GrassDensity;GrassBladeDistance=(float)p.GrassClose;GrassDistance=(float)p.GrassRange;GrassDetail=p.GrassDetail;}
        if(layer==1){SpecialistQuality=q;SpecialistDetail=1;SpecialistNearLod=(float)p.HerbNear;SpecialistDistance=(float)p.HerbRange;FungusDistance=(float)p.FungusRange;}
        if(layer==2){MidQuality=q;MidPlantDetail=2;MidNearLod=(float)p.MidNear;MidFarLod=(float)p.MidFar;MidMaxDistance=(float)p.MidRange;}
        if(layer==3){TreeQuality=q;TreeDetail=2;TreeNearLod=(float)p.TreeNear;TreeFarLod=(float)p.TreeFar;TreeMaxDistance=(float)p.TreeVariantRange;}
    }
    public static void ApplyAllPlantQuality(int quality){for(int i=0;i<4;i++)ApplyPlantQuality(i,quality);}
    static void LoadVegetation()
    {
        bool Flag(string name,bool fallback=true)=>PlayerPrefs.GetInt("vegetation-"+name,fallback?1:0)!=0;
        float Value(string name,float fallback,float min,float max)
        {float v=PlayerPrefs.GetFloat("vegetation-"+name,fallback);return float.IsNaN(v)||float.IsInfinity(v)?fallback:Mathf.Clamp(v,min,max);}
        SpecialistPlants=Flag("specialists");MidPlants=Flag("mid");GeneratedTrees=Flag("trees");FarForest=Flag("far-forest");
        GrassNearDensity=Value("grass-near-density",2,1,20);
        SimplifiedForestShadows=Flag("simple-forest-shadows");
        DistantTreeShadowCards=Flag("distant-shadow-cards");EfficientTreeViews=Flag("efficient-tree-views");
        SteadyDaylightShadows=Flag("steady-daylight-shadows");
        DawnDuskShadowBudget=Flag("dawn-dusk-shadow-budget");
        EfficientTreeRendering=Flag("efficient-tree-rendering");
        FoliageDepthPrepass=Flag("foliage-depth-prepass",false);FrontToBackTrees=Flag("front-to-back-trees",false);
        CanopyShade=Flag("canopy-shade");
        ForestShadowQuality=Mathf.Clamp(PlayerPrefs.GetInt("vegetation-forest-shadow-quality",2),0,4);
        ForestShadowSoftness=Value("forest-shadow-softness",2,.75f,4);
        SpecialistDetail=Mathf.Clamp(PlayerPrefs.GetInt("vegetation-specialist-detail",1),0,1);
        MidPlantDetail=Mathf.Clamp(PlayerPrefs.GetInt("vegetation-mid-detail",2),0,2);TreeDetail=Mathf.Clamp(PlayerPrefs.GetInt("vegetation-tree-detail",2),0,2);
        SpecialistDensity=Value("specialist-density",1,0,1);MidPlantDensity=Value("mid-density",1,0,1);TreeDensity=Value("tree-density",1,0,1);
        SpecialistNearLod=Value("specialist-near",22,5,60);SpecialistDistance=Value("specialist-range",140,10,140);FungusDistance=Value("fungus-range",65,5,65);
        MidNearLod=Value("mid-near",55,10,100);MidFarLod=Value("mid-far",200,MidNearLod*2,500);MidMaxDistance=Value("mid-range",1400,50,1400);
        TreeNearLod=Value("tree-near",120,20,240);TreeFarLod=Value("tree-far",450,TreeNearLod*2,1000);TreeMaxDistance=Value("tree-range",1400,100,1400);
        MidPlantShadows=Flag("mid-shadows");TreeShadows=Flag("tree-shadows");VegetationShadows=Flag("shadows");
        ScatterFrustum=Flag("frustum");VegetationOcclusion=Flag("occlusion");
        // One-time migration intentionally replaces the old metre sliders with
        // the owner's current budget as Medium; never rewrite ecology/placement.
        for(int i=0;i<4;i++)ApplyPlantQuality(i,PlayerPrefs.GetInt("vegetation-quality-v2-"+i,2));
        GrassCanopy=Flag("grass-distance");GrassWind=Flag("wind");VegetationRevision++;
    }
    public static void SaveVegetation()
    {
        void Flag(string name,bool value)=>PlayerPrefs.SetInt("vegetation-"+name,value?1:0);
        void Value(string name,float value)=>PlayerPrefs.SetFloat("vegetation-"+name,value);
        Flag("specialists",SpecialistPlants);Flag("mid",MidPlants);Flag("trees",GeneratedTrees);Flag("far-forest",FarForest);
        Value("grass-near-density",GrassNearDensity);Flag("simple-forest-shadows",SimplifiedForestShadows);
        Flag("distant-shadow-cards",DistantTreeShadowCards);Flag("efficient-tree-views",EfficientTreeViews);
        Flag("steady-daylight-shadows",SteadyDaylightShadows);
        Flag("dawn-dusk-shadow-budget",DawnDuskShadowBudget);
        Flag("efficient-tree-rendering",EfficientTreeRendering);
        Flag("foliage-depth-prepass",FoliageDepthPrepass);
        Flag("front-to-back-trees",FrontToBackTrees);
        Flag("canopy-shade",CanopyShade);
        PlayerPrefs.SetInt("vegetation-forest-shadow-quality",ForestShadowQuality);Value("forest-shadow-softness",ForestShadowSoftness);
        PlayerPrefs.SetInt("vegetation-specialist-detail",SpecialistDetail);PlayerPrefs.SetInt("vegetation-mid-detail",MidPlantDetail);PlayerPrefs.SetInt("vegetation-tree-detail",TreeDetail);
        Value("specialist-density",SpecialistDensity);Value("mid-density",MidPlantDensity);Value("tree-density",TreeDensity);
        Value("specialist-near",SpecialistNearLod);Value("specialist-range",SpecialistDistance);Value("fungus-range",FungusDistance);
        Value("mid-near",MidNearLod);Value("mid-far",MidFarLod);Value("mid-range",MidMaxDistance);
        Value("tree-near",TreeNearLod);Value("tree-far",TreeFarLod);Value("tree-range",TreeMaxDistance);
        Flag("mid-shadows",MidPlantShadows);Flag("tree-shadows",TreeShadows);Flag("shadows",VegetationShadows);
        Flag("frustum",ScatterFrustum);Flag("occlusion",VegetationOcclusion);
        int[] levels={GrassQuality,SpecialistQuality,MidQuality,TreeQuality};for(int i=0;i<4;i++)PlayerPrefs.SetInt("vegetation-quality-v2-"+i,levels[i]);
        Flag("grass-distance",GrassCanopy);Flag("wind",GrassWind);PlayerPrefs.SetInt("terrain-ground-cover",GroundCover?1:0);
        // No terrain-plan revision or world regeneration for render-only changes.
        VegetationRevision++;PlayerPrefs.Save();
    }
    public static void ResetVegetation()
    {
        SpecialistPlants=MidPlants=GeneratedTrees=FarForest=true;SpecialistDetail=1;MidPlantDetail=TreeDetail=2;
        SpecialistDensity=MidPlantDensity=TreeDensity=1;SpecialistNearLod=22;SpecialistDistance=140;FungusDistance=65;
        MidNearLod=55;MidFarLod=200;MidMaxDistance=1400;TreeNearLod=120;TreeFarLod=450;TreeMaxDistance=1400;
        MidPlantShadows=TreeShadows=VegetationShadows=ScatterFrustum=VegetationOcclusion=true;
        GroundCover=GrassCanopy=GrassWind=true;TreeIdentityRange=(float)VegetationQuality.TreeIdentityRange;ApplyAllPlantQuality(2);
        GrassNearDensity=2;SimplifiedForestShadows=CanopyShade=true;ForestShadowQuality=2;ForestShadowSoftness=2;
        DistantTreeShadowCards=EfficientTreeViews=true;
        SteadyDaylightShadows=true;
        DawnDuskShadowBudget=true;
        EfficientTreeRendering=true;
        FoliageDepthPrepass=false;FrontToBackTrees=false;
    }
    public static bool KeepVegetation(ScatterKind kind,ulong id)
    {
        float density=kind==ScatterKind.GeneratedTree?TreeDensity:kind==ScatterKind.MidPlant?MidPlantDensity:
            kind==ScatterKind.Herb||kind==ScatterKind.Fungus?SpecialistDensity:1;
        if(density>=1)return true;if(density<=0)return false;
        uint hash=Rng.Mix((uint)id^Rng.Mix((uint)(id>>32))^0x9a65c4u);
        return (hash&0xffffffu)/16777216f<density;
    }
    public static ScatterBlend VegetationBlend(ScatterKind kind,double width,double distance)
    {
        if(kind==ScatterKind.GeneratedTree)
        {
            // A common outer handoff avoids young/narrow trees disappearing
            // hundreds of metres before the same forest's distant layer.
            double reach=TreeRebuild&&TreeImpostors?TreeIdentityRange:TreeMaxDistance;
            double reachWidth=TreeRebuild?Math.Max(width,reach/150):width;
            double size=TreeRebuild?Math.Max(.72,Math.Min(1.35,width/10)):1;
            return ScatterLodBlend.At(kind,reachWidth,distance,TreeNearLod*size,TreeFarLod*size,reach);
        }
        if(kind==ScatterKind.MidPlant)return ScatterLodBlend.At(kind,width,distance,MidNearLod,MidFarLod,MidMaxDistance);
        if(kind==ScatterKind.Herb||kind==ScatterKind.Fungus)return ScatterLodBlend.At(kind,width,distance,SpecialistNearLod,0,kind==ScatterKind.Fungus?FungusDistance:SpecialistDistance);
        return ScatterLodBlend.At(kind,width,distance);
    }
}

public sealed partial class WorldGenRuntime
{
    bool vegetationDiagnostics;
    void PlantQualityRow(string title,int level,int layer)
    {
        GUILayout.Label(title);
        int next=GUILayout.SelectionGrid(level,VegetationQuality.Names,5);
        if(next!=level)TerrainVideoOptions.ApplyPlantQuality(layer,next);
    }
    void DrawVegetationOptions()
    {
        bool before=GUI.changed;GUI.changed=false;
        GUILayout.Label("VEGETATION",SectionStyle());
        GUILayout.Label("Quality changes detail, not generated populations. Medium uses your tested close-detail budgets. Ultra is bounded: it does not extend fine meshes to the horizon.",MutedStyle());
        int common=TerrainVideoOptions.GrassQuality;
        if(common!=TerrainVideoOptions.SpecialistQuality||common!=TerrainVideoOptions.MidQuality||common!=TerrainVideoOptions.TreeQuality)common=-1;
        GUILayout.Label("Overall quality"+(common<0?" (mixed)":""));
        int selected=GUILayout.SelectionGrid(common,VegetationQuality.Names,5);
        if(selected>=0&&selected!=common)TerrainVideoOptions.ApplyAllPlantQuality(selected);
        GUILayout.Space(12);
        bool enabled=GUI.enabled;
        TerrainVideoOptions.GroundCover=GUILayout.Toggle(TerrainVideoOptions.GroundCover,"Grass and ground-cover geometry");
        GUI.enabled=enabled&&TerrainVideoOptions.GroundCover;
        PlantQualityRow("Grass quality",TerrainVideoOptions.GrassQuality,0);
        GUILayout.Label("Near-foot grass density  "+TerrainVideoOptions.GrassNearDensity.ToString("F1")+"x");
        TerrainVideoOptions.GrassNearDensity=Mathf.Round(GUILayout.HorizontalSlider(TerrainVideoOptions.GrassNearDensity,1,20)*10)/10;
        GUILayout.Label("1x: no extra. 2x: previous boost. Up to20x (10 times previous). Full boost within5m, smoothly returns to normal by10m. Independent of grass quality; high values can be costly.",MutedStyle());
        GUI.enabled=enabled;
        TerrainVideoOptions.SpecialistPlants=GUILayout.Toggle(TerrainVideoOptions.SpecialistPlants,"Flowers, low herbs, ferns and fungi");
        GUI.enabled=enabled&&TerrainVideoOptions.SpecialistPlants;
        PlantQualityRow("Small-plant quality",TerrainVideoOptions.SpecialistQuality,1);GUI.enabled=enabled;
        TerrainVideoOptions.MidPlants=GUILayout.Toggle(TerrainVideoOptions.MidPlants,"Bushes and mid-height plants");
        GUI.enabled=enabled&&TerrainVideoOptions.MidPlants;
        PlantQualityRow("Mid-height quality",TerrainVideoOptions.MidQuality,2);GUI.enabled=enabled;
        TerrainVideoOptions.GeneratedTrees=GUILayout.Toggle(TerrainVideoOptions.GeneratedTrees,"Trees");
        GUI.enabled=enabled&&TerrainVideoOptions.GeneratedTrees;
        PlantQualityRow("Tree quality",TerrainVideoOptions.TreeQuality,3);GUI.enabled=enabled;
        TerrainVideoOptions.FarForest=GUILayout.Toggle(TerrainVideoOptions.FarForest,"Horizon forest coverage");
        TerrainVideoOptions.EfficientTreeViews=GUILayout.Toggle(TerrainVideoOptions.EfficientTreeViews,"Cheaper distant tree shading");
        GUILayout.Label("Close views keep depth and detailed normals. Distant crowns use simpler lighting; tiny crowns use one capture. Tree quality controls the transition.",MutedStyle());
        TerrainVideoOptions.GrassWind=GUILayout.Toggle(TerrainVideoOptions.GrassWind,"Vegetation wind animation");
        GUILayout.Space(12);GUILayout.Label("FOREST SHADOWS",SectionStyle());
        TerrainVideoOptions.SteadyDaylightShadows=GUILayout.Toggle(TerrainVideoOptions.SteadyDaylightShadows,"Steady daylight shadows");
        GUILayout.Label("Sunlight holds while you stand still, then catches up gently as you move or look around. Dawn and dusk keep following the sun.",MutedStyle());
        TerrainVideoOptions.DawnDuskShadowBudget=GUILayout.Toggle(TerrainVideoOptions.DawnDuskShadowBudget,"Reduce detailed shadows at dawn/dusk");
        TerrainVideoOptions.EfficientTreeRendering=GUILayout.Toggle(TerrainVideoOptions.EfficientTreeRendering,"Efficient tree rendering");
        GUILayout.Label("Retain tree instances on the GPU and share shading/wind work. Disable to compare performance.");
        TerrainVideoOptions.FrontToBackTrees=GUILayout.Toggle(TerrainVideoOptions.FrontToBackTrees,"Prioritize nearby trees when drawing");
        TerrainVideoOptions.FoliageDepthPrepass=GUILayout.Toggle(TerrainVideoOptions.FoliageDepthPrepass,"Tree depth prepass (experimental)");
        GUILayout.Label("Draw tree silhouettes first so hidden foliage can skip expensive lighting. Disable to compare on your GPU.");
        GUILayout.Label("Shorter, softer detailed shadows near the horizon. Direct sunlight fades into the existing ambient sky lighting as the sun rises or sets.",MutedStyle());
        TerrainVideoOptions.SimplifiedForestShadows=GUILayout.Toggle(TerrainVideoOptions.SimplifiedForestShadows,"Simplified forest shadows (off = previous rendering)");
        GUI.enabled=enabled&&TerrainVideoOptions.SimplifiedForestShadows;
        TerrainVideoOptions.DistantTreeShadowCards=GUILayout.Toggle(TerrainVideoOptions.DistantTreeShadowCards,"Soft coarse foliage shadows");
        GUILayout.Label("Cached soft canopy shade replaces foliage outlines; trunks and animals retain shadow-map geometry. Needs canopy shade below.",MutedStyle());
        GUILayout.Label("Forest shadow quality");
        TerrainVideoOptions.ForestShadowQuality=GUILayout.SelectionGrid(TerrainVideoOptions.ForestShadowQuality,VegetationQuality.Names,5);
        GUILayout.Label("Four cascades; current range "+TerrainVideoOptions.ForestShadowDistance.ToString("F0")+"m (180m in full daylight). Atlas "+TerrainVideoOptions.ForestShadowResolution+"px; ~"+(TerrainVideoOptions.ForestShadowResolution/2)+"px per cascade; "+TerrainVideoOptions.ForestShadowSamples+" foliage filter samples.");
        GUILayout.Label("Foliage shadow softness  "+TerrainVideoOptions.ForestShadowSoftness.ToString("F2"));
        TerrainVideoOptions.ForestShadowSoftness=Mathf.Round(GUILayout.HorizontalSlider(TerrainVideoOptions.ForestShadowSoftness,.75f,4)*20)/20;
        TerrainVideoOptions.CanopyShade=GUILayout.Toggle(TerrainVideoOptions.CanopyShade,"Canopy shade (height-aware placed-tree coverage)");
        GUILayout.Label("Quality controls resolution/filtering. The dawn/dusk budget can reduce range independently. Broad canopy shade blends in before detailed shadows end. Softness changes filter footprint.",MutedStyle());
        GUI.enabled=enabled;
        GUILayout.Space(10);
        vegetationDiagnostics=GUILayout.Toggle(vegetationDiagnostics,"Advanced diagnostics / A-B comparisons");
        if(vegetationDiagnostics)
        {
            GUILayout.Label("Population controls hide a stable local subset; they do not regenerate ecology or thin the horizon summary.",MutedStyle());
            float Density(string label,float value){GUILayout.Label(label+"  "+(value*100).ToString("F0")+"%");return Mathf.Round(GUILayout.HorizontalSlider(value,0,1)*20)/20;}
            TerrainVideoOptions.SpecialistDensity=Density("Small-plant population",TerrainVideoOptions.SpecialistDensity);
            TerrainVideoOptions.MidPlantDensity=Density("Mid-height population",TerrainVideoOptions.MidPlantDensity);
            TerrainVideoOptions.TreeDensity=Density("Local tree population",TerrainVideoOptions.TreeDensity);
            TerrainVideoOptions.GrassCanopy=GUILayout.Toggle(TerrainVideoOptions.GrassCanopy,"Middle-distance grass geometry");
            TerrainVideoOptions.TreeRebuild=GUILayout.Toggle(TerrainVideoOptions.TreeRebuild,"Rebuilt trees (off = previous models)");
            TerrainVideoOptions.TreeImpostors=GUILayout.Toggle(TerrainVideoOptions.TreeImpostors,"Baked tree views (off = mesh-only A-B)");
            TerrainVideoOptions.MidPlantShadows=GUILayout.Toggle(TerrainVideoOptions.MidPlantShadows,"Mid-height shadows");
            TerrainVideoOptions.TreeShadows=GUILayout.Toggle(TerrainVideoOptions.TreeShadows,"Tree shadows");
            TerrainVideoOptions.VegetationShadows=GUILayout.Toggle(TerrainVideoOptions.VegetationShadows,"All vegetation shadow casting");
            TerrainVideoOptions.ScatterFrustum=GUILayout.Toggle(TerrainVideoOptions.ScatterFrustum,"Frustum culling");
            TerrainVideoOptions.VegetationOcclusion=GUILayout.Toggle(TerrainVideoOptions.VegetationOcclusion,"Conservative terrain occlusion");
            TerrainVideoOptions.WeatherDrivenVegetationWind=GUILayout.Toggle(TerrainVideoOptions.WeatherDrivenVegetationWind,"Weather-driven wind (off = direction-only A-B)");
            GUILayout.Label("Tree mesh handoffs: "+TerrainVideoOptions.TreeNearLod.ToString("F0")+" / "+TerrainVideoOptions.TreeFarLod.ToString("F0")+" m. Cheaper views retain individual roots to the distant forest overlap.",MutedStyle());
            GUILayout.Label("F3 reports submitted plants, draws and pending bakes. A-B switches are diagnostic, not recommended quality presets.",MutedStyle());
        }
        if(GUILayout.Button("Reset vegetation to Medium")){TerrainVideoOptions.ResetVegetation();GUI.changed=true;}
        if(GUI.changed)TerrainVideoOptions.SaveVegetation();GUI.changed|=before;
    }
}
