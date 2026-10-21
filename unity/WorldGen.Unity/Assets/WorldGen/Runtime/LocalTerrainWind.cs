using System;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public V CurrentWindMps {get;private set;}
    Vector3 WindToChart(V value)
    {
        var at=Position;V east=V.Cross(Math.Abs(at.Z)>.999999?new V(0,1,0):new V(0,0,1),at).Unit,north=V.Cross(at,east).Unit;
        var p=east*value.X+north*value.Y;
        return new Vector3((float)V.Dot(p,frame.East),(float)V.Dot(p,frame.Origin),(float)V.Dot(p,frame.North));
    }
    void UpdateWindPresentation()
    {
        // Weather/rain keep their existing instantaneous sample. Vegetation
        // reads ONLY the broad heading; its original shader owns all animation.
        double seconds=playState==null?weatherDays*86400:PlayCalendar.Seconds(playState,SimulationTime.Seconds(sourceWorld.Prehistory));
        CurrentWindMps=WeatherWind.Sample(CurrentWeather,sourceWorld.Recipe.Seed,Position,frame.RadiusM,seconds);
        var direction=WindToChart(new V(CurrentWeather.WindEastMps,CurrentWeather.WindNorthMps,0));direction.y=0;
        direction=direction.sqrMagnitude>.000001f?direction.normalized*Mathf.Sqrt(.8f):new Vector3(.8f,0,.4f);
        Shader.SetGlobalVector("_GrassWindDirection",new Vector4(direction.x,0,direction.z,0));
        Shader.SetGlobalFloat("_WorldWindPlantsEnabled",TerrainVideoOptions.GrassWind?1:0);
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
