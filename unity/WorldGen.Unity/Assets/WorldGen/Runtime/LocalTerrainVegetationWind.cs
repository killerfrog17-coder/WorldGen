using System;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    readonly VegetationWindController vegetationWind=new VegetationWindController();
    void UpdateVegetationWind(double seconds)
    {
        var raw=WindToChart(new V(CurrentWeather.WindEastMps,CurrentWeather.WindNorthMps,0));
        V desired=frame.East*raw.x+frame.Origin*raw.y+frame.North*raw.z;
        vegetationWind.Advance(desired,CurrentWeather.GustMps,CurrentWeather.WindGustChance,CurrentWeather.WindGustDurationS,
            seconds,Time.unscaledDeltaTime,TerrainVideoOptions.VegetationWindResponse);
        var velocity=vegetationWind.Velocity;
        var direction=new Vector3((float)V.Dot(velocity,frame.East),0,(float)V.Dot(velocity,frame.North));
        direction=direction.sqrMagnitude>.000001f?direction.normalized*Mathf.Sqrt(.8f):new Vector3(.8f,0,.4f);
        Shader.SetGlobalFloat("_WeatherDrivenVegetationWind",TerrainVideoOptions.WeatherDrivenVegetationWind?1:0);
        Shader.SetGlobalVector("_VegetationDirection",new Vector4(direction.x,0,direction.z,0));
        Shader.SetGlobalVector("_VegetationControls",new Vector4((float)(vegetationWind.Speed/6),(float)(vegetationWind.GustExtra/6),
            (float)vegetationWind.GustChance,(float)vegetationWind.GustDuration));
        Shader.SetGlobalVector("_VegetationPhases",new Vector4((float)vegetationWind.PhaseA,(float)vegetationWind.PhaseB,0,0));
        var at=Position;double radius=frame.RadiusM;
        var up=new Vector3((float)V.Dot(at,frame.East),(float)V.Dot(at,frame.Origin),(float)V.Dot(at,frame.North));
        Shader.SetGlobalVector("_VegetationAnchor",new Vector4((float)(motion.X-originX),0,(float)(motion.Z-originZ),
            (float)Math.Sqrt(radius*radius+motion.X*motion.X+motion.Z*motion.Z)));
        Shader.SetGlobalVector("_VegetationSphere",new Vector4(up.x,up.y,up.z,(float)radius));
        V position=at*radius-vegetationWind.Drift;
        for(int axisIndex=0;axisIndex<2;axisIndex++)
        {
            var axis=WeatherWind.Axis(sourceWorld.Recipe.Seed,axisIndex)/70;
            double phase=V.Dot(position,axis);phase-=Math.Floor(phase/128)*128;
            Shader.SetGlobalVector(axisIndex==0?"_VegetationNoiseU":"_VegetationNoiseV",new Vector4(
                (float)V.Dot(axis,frame.East),(float)V.Dot(axis,frame.Origin),(float)V.Dot(axis,frame.North),(float)phase));
        }
    }
    string VegetationWindDebugText=>"\nPlant wind · "+(TerrainVideoOptions.WeatherDrivenVegetationWind?"layered gusts · effective "+vegetationWind.Speed.ToString("F1")+" m/s · response "+TerrainVideoOptions.VegetationWindResponse.ToString("F0")+"s":"direction-only rollback");
}
