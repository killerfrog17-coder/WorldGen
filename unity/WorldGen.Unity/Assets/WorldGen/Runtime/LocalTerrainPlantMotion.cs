using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    ComputeShader plantWindResponse;RenderTexture plantWindPrevious,plantWindNext;
    readonly Vector4[] plantWindWeights=new Vector4[PlantMotion.Taps],plantWindMeans=new Vector4[10],plantWindStats=new Vector4[10];
    readonly Vector4[] boundWindWaves=new Vector4[4];readonly int[] boundWindSlots=new int[2];
    Vector4 boundWindMean,boundWindStats,boundWindAnchor,boundWindSphere;
    long plantWindTick=long.MinValue;double plantWindX=double.NaN,plantWindZ,lastPlantWindSeconds=double.NaN;int plantWindSize,plantWindCell;
    sealed class WindChange {public double At;public string Mode;public WeatherFrame? Test;}
    readonly List<WindChange> windChanges=new List<WindChange>();
    void UpdatePlantWind(double seconds)
    {
        bool enabled=TerrainVideoOptions.GrassWind&&(TerrainVideoOptions.GroundCover||TerrainVideoOptions.SpecialistPlants);
        if(!enabled||!SystemInfo.supportsComputeShaders){Shader.SetGlobalFloat("_PlantWindReady",0);return;}
        if(plantWindResponse==null)
        {
            plantWindResponse=Resources.Load<ComputeShader>("PlantWindResponse");if(plantWindResponse==null)return;
            for(int i=0;i<PlantMotion.Taps;i++)plantWindWeights[i]=new Vector4((float)PlantMotion.Weight(i,0),(float)PlantMotion.Weight(i,1),(float)PlantMotion.Weight(i,2),(float)PlantMotion.Weight(i,3));
            plantWindResponse.SetVectorArray("_ResponseWeights",plantWindWeights);
        }
        if(seconds<lastPlantWindSeconds-.01){windChanges.Clear();plantWindTick=long.MinValue;}
        lastPlantWindSeconds=seconds;
        var last=windChanges.Count==0?null:windChanges[windChanges.Count-1];
        if(last==null||last.Mode!=WeatherControl.Mode||!Nullable.Equals(last.Test,TestWeatherOverride))
        {
            windChanges.Add(new WindChange{At=last==null?double.NegativeInfinity:seconds,Mode=WeatherControl.Mode,Test=TestWeatherOverride});
            plantWindTick=long.MinValue;
        }
        while(windChanges.Count>1&&windChanges[1].At<seconds-PlantMotion.HistorySeconds-2)windChanges.RemoveAt(0);
        float range=Mathf.Max(TerrainVideoOptions.GroundCover?TerrainVideoOptions.GrassDistance:0,TerrainVideoOptions.SpecialistPlants?150:0);
        int size=range<=200?128:256,cell=Mathf.Max(2,Mathf.CeilToInt((2*range+64)/size));
        double x=Math.Floor((motion.X-range-32)/cell)*cell,z=Math.Floor((motion.Z-range-32)/cell)*cell;
        if(plantWindPrevious==null||size!=plantWindSize)
        {
            if(plantWindPrevious!=null){plantWindPrevious.Release();Destroy(plantWindPrevious);plantWindNext.Release();Destroy(plantWindNext);}
            RenderTexture Create(string label)
            {
                var texture=new RenderTexture(size,size,0,RenderTextureFormat.RGHalf){name=label,dimension=TextureDimension.Tex2DArray,volumeDepth=4,enableRandomWrite=true,
                    filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,useMipMap=false};texture.Create();return texture;
            }
            plantWindPrevious=Create("Plant spring response: previous");plantWindNext=Create("Plant spring response: next");plantWindSize=size;plantWindTick=long.MinValue;
        }
        long tick=(long)Math.Floor(seconds*10);
        bool moved=x!=plantWindX||z!=plantWindZ||cell!=plantWindCell;
        var map=new Vector4((float)(x-originX),(float)(z-originZ),cell,size);
        if(moved||tick!=plantWindTick)
        {
            plantWindResponse.SetVector("_WorldWindAnchor",boundWindAnchor);plantWindResponse.SetVector("_WorldWindSphere",boundWindSphere);
            plantWindResponse.SetInt("_WorldWindSeed",sourceWorld.Recipe.Seed);
            plantWindResponse.SetInt("_WorldWindSlot0",boundWindSlots[0]);plantWindResponse.SetInt("_WorldWindSlot1",boundWindSlots[1]);
            for(int lane=0;lane<4;lane++)plantWindResponse.SetVector(WindWaveIds[lane],boundWindWaves[lane]);
            plantWindResponse.SetVector("_ResponseMap",map);
            void Fill(RenderTexture output,double at)
            {
                for(int i=0;i<plantWindMeans.Length;i++)
                {
                    double time=at-i;var change=windChanges[0];
                    for(int c=windChanges.Count-1;c>=0;c--)if(windChanges[c].At<=time){change=windChanges[c];break;}
                    var f=change.Test??WorldGen.Presentation.WeatherControl.ApplyMode(change.Mode,
                        weatherProvider.At(time/86400,((LocalSolarHour+(time-seconds)/3600)%24+24)%24,includeFog:false),profileElevation);
                    var mean=WindToChart(new V(f.WindEastMps,f.WindNorthMps,0));
                    plantWindMeans[i]=new Vector4(mean.x,mean.y,mean.z,(float)f.GustMps);
                    plantWindStats[i]=new Vector4((float)f.WindGustChance,(float)f.WindGustDurationS,(float)f.WindTurbulence,0);
                }
                plantWindResponse.SetVectorArray("_ResponseMeans",plantWindMeans);plantWindResponse.SetVectorArray("_ResponseStats",plantWindStats);
                plantWindResponse.SetFloat("_ResponseAge",(float)(seconds-at));plantWindResponse.SetTexture(0,"_ResponseOutput",output);
                plantWindResponse.Dispatch(0,size/8,size/8,1);
            }
            if(!moved&&tick==plantWindTick+1)
            {var swap=plantWindPrevious;plantWindPrevious=plantWindNext;plantWindNext=swap;}
            else Fill(plantWindPrevious,tick*.1);
            Fill(plantWindNext,(tick+1)*.1);
            plantWindTick=tick;plantWindX=x;plantWindZ=z;plantWindCell=cell;
        }
        Shader.SetGlobalTexture("_PlantWindPrevious",plantWindPrevious);Shader.SetGlobalTexture("_PlantWindNext",plantWindNext);
        Shader.SetGlobalVector("_PlantWindMap",new Vector4(map.x,map.y,1f/(cell*size),size));
        Shader.SetGlobalFloat("_PlantWindBlend",(float)(seconds*10-tick));Shader.SetGlobalFloat("_PlantWindReady",1);
    }
    void ResetPlantWind(){plantWindTick=long.MinValue;plantWindX=plantWindZ=double.NaN;Shader.SetGlobalFloat("_PlantWindReady",0);}
    void DisposePlantWind()
    {
        Shader.SetGlobalFloat("_PlantWindReady",0);
        if(plantWindPrevious!=null){plantWindPrevious.Release();Destroy(plantWindPrevious);}
        if(plantWindNext!=null){plantWindNext.Release();Destroy(plantWindNext);}
    }
}
