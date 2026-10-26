using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public sealed partial class LocalTerrainExplorer
{
    [StructLayout(LayoutKind.Sequential)]
    struct TreeWindRoot {public Vector4 PositionFlex,HeightAxis;}
    readonly List<TreeWindRoot> treeWindRoots=new List<TreeWindRoot>();
    GraphicsBuffer treeWindInputs,treeWindResults;ComputeShader treeWindCompute;
    int treeWindCapacity,treeWindFrame=-1,treeShaderRevision=-1;bool treeWindDirty=true,treeShaderEnabled;
    static readonly string[] TreeWindVectors={"_GrassWindDirection","_VegetationDirection","_VegetationControls","_VegetationPhases","_VegetationNoiseU","_VegetationNoiseV","_VegetationAnchor","_VegetationSphere"};
    bool UseSharedTreeWind=>TerrainVideoOptions.EfficientTreeRendering&&!WorldGenPerformance.LegacyTreeWind&&SystemInfo.supportsComputeShaders&&SystemInfo.graphicsShaderLevel>=45;
    static bool EfficientTreeShader=>TerrainVideoOptions.EfficientTreeRendering&&!WorldGenPerformance.LegacyTreeShader&&SystemInfo.graphicsShaderLevel>=45;
    void ConfigureEfficientTreeMaterial(Material material)
    {
        if(material.shader.name!="WorldGen/TreeSurface2"&&material.shader.name!="WorldGen/ForestPackets")return;
        if(EfficientTreeShader)material.EnableKeyword("WORLDGEN_TREE_EFFICIENT");else material.DisableKeyword("WORLDGEN_TREE_EFFICIENT");
    }
    void UpdateEfficientTreeMaterials()
    {
        bool enabled=EfficientTreeShader;
        if(treeShaderRevision==ownedScatterMaterials.Count&&enabled==treeShaderEnabled)return;
        treeShaderRevision=ownedScatterMaterials.Count;treeShaderEnabled=enabled;
        foreach(var material in ownedScatterMaterials)if(material!=null)ConfigureEfficientTreeMaterial(material);
    }
    void PrepareSharedTreeWind()
    {
        if(!UseSharedTreeWind||treeWindRoots.Count==0)return;
        if(treeWindCompute==null)treeWindCompute=Resources.Load<ComputeShader>("WorldGenTreeWind");
        if(treeWindCompute==null)return;
        if(treeWindCapacity<treeWindRoots.Count)
        {
            treeWindInputs?.Dispose();treeWindResults?.Dispose();treeWindCapacity=Mathf.NextPowerOfTwo(Mathf.Max(64,treeWindRoots.Count));
            treeWindInputs=new GraphicsBuffer(GraphicsBuffer.Target.Structured,treeWindCapacity,32);treeWindResults=new GraphicsBuffer(GraphicsBuffer.Target.Structured,treeWindCapacity,16);treeWindDirty=true;
        }
        if(!treeWindDirty&&treeWindFrame==Time.frameCount)return;
        if(treeWindDirty)treeWindInputs.SetData(treeWindRoots,0,0,treeWindRoots.Count);
        foreach(string name in TreeWindVectors)treeWindCompute.SetVector(name,Shader.GetGlobalVector(name));
        treeWindCompute.SetFloat("_WeatherDrivenVegetationWind",Shader.GetGlobalFloat("_WeatherDrivenVegetationWind"));
        float seconds=Time.time;treeWindCompute.SetVector("_Time",new Vector4(seconds/20,seconds,seconds*2,seconds*3));
        treeWindCompute.SetInt("_WindCount",treeWindRoots.Count);treeWindCompute.SetBuffer(0,"_WindRoots",treeWindInputs);treeWindCompute.SetBuffer(0,"_WindResults",treeWindResults);
        treeWindCompute.Dispatch(0,(treeWindRoots.Count+63)/64,1,1);treeWindFrame=Time.frameCount;treeWindDirty=false;
    }
    void BindSharedTreeWind(Camera camera)
    {
        bool enabled=camera==view&&UseSharedTreeWind&&treeWindResults!=null&&treeWindRoots.Count>0&&treeWindFrame==Time.frameCount;
        Shader.SetGlobalFloat("_WorldGenTreeWindCacheEnabled",enabled?1:0);
        if(enabled)Shader.SetGlobalBuffer("_WorldGenTreeWindData",treeWindResults);
    }
    void DisposeTreeEfficiency()
    {
        Shader.SetGlobalFloat("_WorldGenTreeWindCacheEnabled",0);treeWindInputs?.Dispose();treeWindResults?.Dispose();treeWindInputs=treeWindResults=null;treeWindRoots.Clear();
    }
}
