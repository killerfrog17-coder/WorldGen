using System;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    static readonly int[] WindWaveIds={Shader.PropertyToID("_WorldWindWave0"),Shader.PropertyToID("_WorldWindWave1"),Shader.PropertyToID("_WorldWindWave2"),Shader.PropertyToID("_WorldWindWave3")};
    static readonly int[] PlantWaveAIds={Shader.PropertyToID("_PlantWaveA0"),Shader.PropertyToID("_PlantWaveA1"),Shader.PropertyToID("_PlantWaveA2")},
        PlantWaveBIds={Shader.PropertyToID("_PlantWaveB0"),Shader.PropertyToID("_PlantWaveB1"),Shader.PropertyToID("_PlantWaveB2")};
    public V CurrentWindMps {get;private set;}
    Vector3 WindToChart(V value)
    {
        var at=Position;V east=V.Cross(Math.Abs(at.Z)>.999999?new V(0,1,0):new V(0,0,1),at).Unit,north=V.Cross(at,east).Unit;
        var p=east*value.X+north*value.Y;
        return new Vector3((float)V.Dot(p,frame.East),(float)V.Dot(p,frame.Origin),(float)V.Dot(p,frame.North));
    }
    void UpdateWindPresentation()
    {
        // The saved/play calendar is the shared authority. Pauses freeze it;
        // frame rate, process start time and chunk births never enter the field.
        double seconds=playState==null?weatherDays*86400:PlayCalendar.Seconds(playState,SimulationTime.Seconds(sourceWorld.Prehistory));
        int seed=sourceWorld.Recipe.Seed;var at=Position;
        CurrentWindMps=WeatherWind.Sample(CurrentWeather,seed,at,frame.RadiusM,seconds);
        Vector3 mean=WindToChart(new V(CurrentWeather.WindEastMps,CurrentWeather.WindNorthMps,0));
        boundWindMean=new Vector4(mean.x,mean.y,mean.z,(float)CurrentWeather.GustMps);
        boundWindStats=new Vector4((float)CurrentWeather.WindGustChance,(float)CurrentWeather.WindGustDurationS,(float)CurrentWeather.WindTurbulence,0);
        Shader.SetGlobalVector("_WorldWindMean",boundWindMean);Shader.SetGlobalVector("_WorldWindStats",boundWindStats);
        var up=new Vector3((float)V.Dot(at,frame.East),(float)V.Dot(at,frame.Origin),(float)V.Dot(at,frame.North));
        double length=Math.Sqrt(frame.RadiusM*frame.RadiusM+motion.X*motion.X+motion.Z*motion.Z);
        boundWindAnchor=new Vector4((float)(motion.X-originX),0,(float)(motion.Z-originZ),(float)length);
        boundWindSphere=new Vector4(up.x,up.y,up.z,(float)frame.RadiusM);
        Shader.SetGlobalVector("_WorldWindAnchor",boundWindAnchor);Shader.SetGlobalVector("_WorldWindSphere",boundWindSphere);
        Shader.SetGlobalInteger("_WorldWindSeed",seed);
        Shader.SetGlobalFloat("_WorldWindPlantsEnabled",TerrainVideoOptions.GrassWind?1:0);
        for(int lane=0;lane<4;lane++)
        {
            V axis=WeatherWind.Axis(seed,lane)/WeatherWind.Wavelength(lane);
            double phase=WeatherWind.Phase(seed,lane,at,frame.RadiusM,seconds),cell=Math.Floor(phase);
            boundWindWaves[lane]=new Vector4((float)V.Dot(axis,frame.East),(float)V.Dot(axis,frame.Origin),(float)V.Dot(axis,frame.North),(float)(phase-cell));
            Shader.SetGlobalVector(WindWaveIds[lane],boundWindWaves[lane]);
            if(lane<2){boundWindSlots[lane]=unchecked((int)(long)cell);Shader.SetGlobalInteger(lane==0?"_WorldWindSlot0":"_WorldWindSlot1",boundWindSlots[lane]);}
        }
        // Phase-continuous loop bands retain the pre-weather 1.6/2.7rad/s
        // character. Slow envelopes mix speeds without restarting an oscillator.
        for(int band=0;band<3;band++)for(int lane=0;lane<2;lane++)
        {
            double rate=band==0?.65:band==1?1:1.45,frequency=(lane==0?1.6:2.7)*rate;
            V axis=WeatherWind.Axis(seed,lane+2)*(2*Math.PI/(lane==0?45:29));
            double phase=V.Dot(at,axis)*frame.RadiusM+seconds*frequency;
            phase-=Math.Floor(phase/(2*Math.PI))*(2*Math.PI);
            Shader.SetGlobalVector(lane==0?PlantWaveAIds[band]:PlantWaveBIds[band],
                new Vector4((float)V.Dot(axis,frame.East),(float)V.Dot(axis,frame.Origin),(float)V.Dot(axis,frame.North),(float)phase));
        }
        UpdatePlantWind(seconds);
    }
    string WindDebugText
    {
        get
        {
            double mean=Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps);
            double from=(Math.Atan2(-CurrentWindMps.X,-CurrentWindMps.Y)*180/Math.PI+360)%360;
            return "Wind "+CurrentWindMps.Length.ToString("F1")+" m/s now · mean "+mean.ToString("F1")+" · peak envelope "+CurrentWeather.GustMps.ToString("F1")+" · from "+from.ToString("F0")+"° · gust chance "+(CurrentWeather.WindGustChance*100).ToString("F0")+"% /64s · duration ~"+CurrentWeather.WindGustDurationS.ToString("F0")+"s";
        }
    }
}
