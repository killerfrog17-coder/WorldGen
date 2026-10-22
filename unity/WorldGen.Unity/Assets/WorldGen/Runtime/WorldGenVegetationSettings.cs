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
    static void LoadVegetation()
    {
        bool Flag(string name,bool fallback=true)=>PlayerPrefs.GetInt("vegetation-"+name,fallback?1:0)!=0;
        float Value(string name,float fallback,float min,float max)
        {float v=PlayerPrefs.GetFloat("vegetation-"+name,fallback);return float.IsNaN(v)||float.IsInfinity(v)?fallback:Mathf.Clamp(v,min,max);}
        SpecialistPlants=Flag("specialists");MidPlants=Flag("mid");GeneratedTrees=Flag("trees");FarForest=Flag("far-forest");
        SpecialistDetail=Mathf.Clamp(PlayerPrefs.GetInt("vegetation-specialist-detail",1),0,1);
        MidPlantDetail=Mathf.Clamp(PlayerPrefs.GetInt("vegetation-mid-detail",2),0,2);TreeDetail=Mathf.Clamp(PlayerPrefs.GetInt("vegetation-tree-detail",2),0,2);
        SpecialistDensity=Value("specialist-density",1,0,1);MidPlantDensity=Value("mid-density",1,0,1);TreeDensity=Value("tree-density",1,0,1);
        SpecialistNearLod=Value("specialist-near",22,5,60);SpecialistDistance=Value("specialist-range",140,10,140);FungusDistance=Value("fungus-range",65,5,65);
        MidNearLod=Value("mid-near",55,10,100);MidFarLod=Value("mid-far",200,MidNearLod*2,500);MidMaxDistance=Value("mid-range",1400,50,1400);
        TreeNearLod=Value("tree-near",120,20,240);TreeFarLod=Value("tree-far",450,TreeNearLod*2,1000);TreeMaxDistance=Value("tree-range",1400,100,1400);
        MidPlantShadows=Flag("mid-shadows");TreeShadows=Flag("tree-shadows");VegetationShadows=Flag("shadows");
        ScatterFrustum=Flag("frustum");VegetationOcclusion=Flag("occlusion");VegetationRevision++;
    }
    public static void SaveVegetation()
    {
        void Flag(string name,bool value)=>PlayerPrefs.SetInt("vegetation-"+name,value?1:0);
        void Value(string name,float value)=>PlayerPrefs.SetFloat("vegetation-"+name,value);
        Flag("specialists",SpecialistPlants);Flag("mid",MidPlants);Flag("trees",GeneratedTrees);Flag("far-forest",FarForest);
        PlayerPrefs.SetInt("vegetation-specialist-detail",SpecialistDetail);PlayerPrefs.SetInt("vegetation-mid-detail",MidPlantDetail);PlayerPrefs.SetInt("vegetation-tree-detail",TreeDetail);
        Value("specialist-density",SpecialistDensity);Value("mid-density",MidPlantDensity);Value("tree-density",TreeDensity);
        Value("specialist-near",SpecialistNearLod);Value("specialist-range",SpecialistDistance);Value("fungus-range",FungusDistance);
        Value("mid-near",MidNearLod);Value("mid-far",MidFarLod);Value("mid-range",MidMaxDistance);
        Value("tree-near",TreeNearLod);Value("tree-far",TreeFarLod);Value("tree-range",TreeMaxDistance);
        Flag("mid-shadows",MidPlantShadows);Flag("tree-shadows",TreeShadows);Flag("shadows",VegetationShadows);
        Flag("frustum",ScatterFrustum);Flag("occlusion",VegetationOcclusion);
        // No terrain-plan revision or world regeneration for render-only changes.
        VegetationRevision++;PlayerPrefs.Save();
    }
    public static void ResetVegetation()
    {
        SpecialistPlants=MidPlants=GeneratedTrees=FarForest=true;SpecialistDetail=1;MidPlantDetail=TreeDetail=2;
        SpecialistDensity=MidPlantDensity=TreeDensity=1;SpecialistNearLod=22;SpecialistDistance=140;FungusDistance=65;
        MidNearLod=55;MidFarLod=200;MidMaxDistance=1400;TreeNearLod=120;TreeFarLod=450;TreeMaxDistance=1400;
        MidPlantShadows=TreeShadows=VegetationShadows=ScatterFrustum=VegetationOcclusion=true;
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
        if(kind==ScatterKind.GeneratedTree)return ScatterLodBlend.At(kind,width,distance,TreeNearLod,TreeFarLod,TreeMaxDistance);
        if(kind==ScatterKind.MidPlant)return ScatterLodBlend.At(kind,width,distance,MidNearLod,MidFarLod,MidMaxDistance);
        if(kind==ScatterKind.Herb||kind==ScatterKind.Fungus)return ScatterLodBlend.At(kind,width,distance,SpecialistNearLod,0,kind==ScatterKind.Fungus?FungusDistance:SpecialistDistance);
        return ScatterLodBlend.At(kind,width,distance);
    }
}

public sealed partial class WorldGenRuntime
{
    static float PlantSlider(string label,float value,float min,float max,float step=1,string units="m")
    {
        GUILayout.Label(label+"  "+value.ToString("0.##")+" "+units);
        return Mathf.Clamp(Mathf.Round(GUILayout.HorizontalSlider(value,min,max)/step)*step,min,max);
    }
    static float PlantDensity(string label,float value)=>PlantSlider(label,value*100,0,100,5,"%")/100;
    void DrawVegetationQuickToggles()
    {
        bool before=GUI.changed;GUI.changed=false;
        TerrainVideoOptions.SpecialistPlants=GUILayout.Toggle(TerrainVideoOptions.SpecialistPlants,"Generated flowers, low herbs, ferns and fungi");
        TerrainVideoOptions.MidPlants=GUILayout.Toggle(TerrainVideoOptions.MidPlants,"Generated mid-height plants");
        TerrainVideoOptions.GeneratedTrees=GUILayout.Toggle(TerrainVideoOptions.GeneratedTrees,"Generated trees · nearby instanced models");
        TerrainVideoOptions.FarForest=GUILayout.Toggle(TerrainVideoOptions.FarForest,"Distant forest canopy volumes and colour");
        if(GUI.changed)TerrainVideoOptions.SaveVegetation();GUI.changed|=before;
    }
    void DrawVegetationOptions()
    {
        GUILayout.Label("GENERATED VEGETATION · LIVE QUALITY CONTROLS",SectionStyle());
        GUILayout.Label("Saved on this PC. Changes affect rendering, not ecology or world generation. Smaller LOD distances switch to cheaper models sooner. F3 shows FPS and submitted counts.",MutedStyle());
        DrawVegetationQuickToggles();bool before=GUI.changed;GUI.changed=false;bool enabled=GUI.enabled;
        GUILayout.Space(12);GUILayout.Label("FLOWERS, LOW HERBS, FERNS & FUNGI",SectionStyle());
        GUI.enabled=enabled&&TerrainVideoOptions.SpecialistPlants;
        TerrainVideoOptions.SpecialistDensity=PlantDensity("Visible population",TerrainVideoOptions.SpecialistDensity);
        GUILayout.Label("Geometry quality");TerrainVideoOptions.SpecialistDetail=GUILayout.SelectionGrid(TerrainVideoOptions.SpecialistDetail,new[]{"Low","High"},2);
        GUI.enabled=enabled&&TerrainVideoOptions.SpecialistPlants&&TerrainVideoOptions.SpecialistDetail>0;
        TerrainVideoOptions.SpecialistNearLod=PlantSlider("High → low handoff ends",TerrainVideoOptions.SpecialistNearLod,5,60);
        GUILayout.Label("Handoff begins at "+(TerrainVideoOptions.SpecialistNearLod*12/22).ToString("F0")+" m. Low quality always uses the cheaper model.",MutedStyle());
        GUI.enabled=enabled&&TerrainVideoOptions.SpecialistPlants;
        TerrainVideoOptions.SpecialistDistance=PlantSlider("Herb / flower draw distance",TerrainVideoOptions.SpecialistDistance,10,140,5);
        TerrainVideoOptions.FungusDistance=PlantSlider("Mushroom draw distance",TerrainVideoOptions.FungusDistance,5,65,5);
        GUI.enabled=enabled;

        GUILayout.Space(16);GUILayout.Label("MID-HEIGHT PLANTS",SectionStyle());GUI.enabled=enabled&&TerrainVideoOptions.MidPlants;
        TerrainVideoOptions.MidPlantDensity=PlantDensity("Visible population",TerrainVideoOptions.MidPlantDensity);
        GUILayout.Label("Geometry quality");TerrainVideoOptions.MidPlantDetail=GUILayout.SelectionGrid(TerrainVideoOptions.MidPlantDetail,new[]{"Low","Medium","High"},3);
        GUI.enabled=enabled&&TerrainVideoOptions.MidPlants&&TerrainVideoOptions.MidPlantDetail==2;
        TerrainVideoOptions.MidNearLod=PlantSlider("High → middle handoff ends",TerrainVideoOptions.MidNearLod,10,100,5);
        GUI.enabled=enabled&&TerrainVideoOptions.MidPlants&&TerrainVideoOptions.MidPlantDetail>0;
        TerrainVideoOptions.MidFarLod=PlantSlider("Middle → low handoff ends",Math.Max(TerrainVideoOptions.MidNearLod*2,TerrainVideoOptions.MidFarLod),TerrainVideoOptions.MidNearLod*2,500,10);
        GUILayout.Label("Blends over "+(TerrainVideoOptions.MidNearLod*28/55).ToString("F0")+"–"+TerrainVideoOptions.MidNearLod.ToString("F0")+" m and "+(TerrainVideoOptions.MidFarLod*.55f).ToString("F0")+"–"+TerrainVideoOptions.MidFarLod.ToString("F0")+" m.",MutedStyle());
        GUI.enabled=enabled&&TerrainVideoOptions.MidPlants;
        TerrainVideoOptions.MidMaxDistance=PlantSlider("Maximum model draw distance",TerrainVideoOptions.MidMaxDistance,50,1400,25);
        TerrainVideoOptions.MidPlantShadows=GUILayout.Toggle(TerrainVideoOptions.MidPlantShadows,"Mid-height shadow casting");GUI.enabled=enabled;

        GUILayout.Space(16);GUILayout.Label("TREES",SectionStyle());GUI.enabled=enabled&&TerrainVideoOptions.GeneratedTrees;
        TerrainVideoOptions.TreeRebuild=GUILayout.Toggle(TerrainVideoOptions.TreeRebuild,"Rebuilt trees (session-only A/B; off = previous models)");
        TerrainVideoOptions.TreeImpostors=GUILayout.Toggle(TerrainVideoOptions.TreeImpostors,"Bake multi-angle distant trees (off = mesh-only comparison)");
        GUILayout.Label("Bakes run in the background; full middle meshes remain until ready. Atlas budget 48 models (~128 MiB).",MutedStyle());
        if(GUILayout.Button("Try rebuilt-tree LOD handoffs: 60 / 200 m")){TerrainVideoOptions.TreeNearLod=60;TerrainVideoOptions.TreeFarLod=200;GUI.changed=true;}
        TerrainVideoOptions.TreeDensity=PlantDensity("Visible population",TerrainVideoOptions.TreeDensity);
        GUILayout.Label("Geometry quality");TerrainVideoOptions.TreeDetail=GUILayout.SelectionGrid(TerrainVideoOptions.TreeDetail,new[]{"Low","Medium","High"},3);
        GUI.enabled=enabled&&TerrainVideoOptions.GeneratedTrees&&TerrainVideoOptions.TreeDetail==2;
        TerrainVideoOptions.TreeNearLod=PlantSlider("High → middle handoff ends",TerrainVideoOptions.TreeNearLod,20,240,5);
        GUI.enabled=enabled&&TerrainVideoOptions.GeneratedTrees&&TerrainVideoOptions.TreeDetail>0;
        TerrainVideoOptions.TreeFarLod=PlantSlider("Middle → low handoff ends",Math.Max(TerrainVideoOptions.TreeNearLod*2,TerrainVideoOptions.TreeFarLod),TerrainVideoOptions.TreeNearLod*2,1000,10);
        GUILayout.Label("Blends over "+(TerrainVideoOptions.TreeNearLod*.5f).ToString("F0")+"–"+TerrainVideoOptions.TreeNearLod.ToString("F0")+" m and "+(TerrainVideoOptions.TreeFarLod*260/450).ToString("F0")+"–"+TerrainVideoOptions.TreeFarLod.ToString("F0")+" m.",MutedStyle());
        GUI.enabled=enabled&&TerrainVideoOptions.GeneratedTrees;
        TerrainVideoOptions.TreeMaxDistance=PlantSlider("Maximum model draw distance",TerrainVideoOptions.TreeMaxDistance,100,1400,25);
        TerrainVideoOptions.TreeShadows=GUILayout.Toggle(TerrainVideoOptions.TreeShadows,"Tree shadow casting");GUI.enabled=enabled;

        GUILayout.Space(16);GUILayout.Label("VISIBILITY & SHADOWS",SectionStyle());
        TerrainVideoOptions.ScatterFrustum=GUILayout.Toggle(TerrainVideoOptions.ScatterFrustum,"Individual scatter frustum culling");
        TerrainVideoOptions.VegetationOcclusion=GUILayout.Toggle(TerrainVideoOptions.VegetationOcclusion,"Conservative hill occlusion (plants and grass pages)");
        TerrainVideoOptions.VegetationShadows=GUILayout.Toggle(TerrainVideoOptions.VegetationShadows,"Global scatter shadow casting (overrides layer switches)");
        GUILayout.Label("Draw distance is a cap: smaller plants can retire earlier. Density hides a stable subset of local models; distant ecological cover summaries stay unchanged. Reducing tree range may expose the gap before the distant canopy begins. Data/placement jobs and cached models remain; these controls measure drawing cost, not generation cost. Grass has its own tab.",MutedStyle());
        if(GUILayout.Button("Reset non-grass vegetation defaults")){TerrainVideoOptions.ResetVegetation();GUI.changed=true;}
        if(GUI.changed)TerrainVideoOptions.SaveVegetation();GUI.changed|=before;
    }
}
