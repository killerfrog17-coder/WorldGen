using System;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    WeatherFieldTile weatherTile,previousWeatherTile;Task<WeatherFieldTile> weatherTileJob;Texture2D weatherConditions,weatherLayers,previousConditions,previousLayers;double fieldDue,fieldChangedAt;
    void UpdateWeatherField(double days)
    {
        if(weatherTileJob!=null&&weatherTileJob.IsCompleted)
        {
            if(weatherTileJob.IsFaulted){error=weatherTileJob.Exception.GetBaseException().Message;debugOpen=true;}
            else if(!weatherTileJob.IsCanceled)
            {
                var reusableConditions=previousConditions;var reusableLayers=previousLayers;
                previousWeatherTile=weatherTile;previousConditions=weatherConditions;previousLayers=weatherLayers;
                weatherTile=weatherTileJob.Result;weatherConditions=reusableConditions;weatherLayers=reusableLayers;fieldChangedAt=Time.realtimeSinceStartupAsDouble;fieldDue=Math.Max(fieldDue,fieldChangedAt+1.5);
                void Upload(ref Texture2D texture,float[] values)
                {
                    if(texture==null||texture.width!=weatherTile.Width||texture.height!=weatherTile.Height){if(texture!=null)Destroy(texture);texture=new Texture2D(weatherTile.Width,weatherTile.Height,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};}
                    texture.SetPixelData(values,0);texture.Apply(false,false);
                }
                Upload(ref weatherConditions,weatherTile.Conditions);Upload(ref weatherLayers,weatherTile.Layers);
            }
            weatherTileJob=null;
        }
        TerrainMapViewport.Degrees(Position,out double lat,out double lon);
        // Recenter well before the viewer reaches a cache edge: distant rays need a full
        // footprint around the observer, not merely a valid sample under their feet.
        if(weatherTileJob==null&&Time.realtimeSinceStartupAsDouble>=fieldDue&&(weatherTile==null||(Position-weatherTile.Center).Length>.02||!weatherTile.Contains(lat,lon)||Math.Abs(days-weatherTile.Days)>1d/288))
        {
            var weather=worldWeather;var position=Position;double radius=frame.RadiusM;var token=cancellation.Token;fieldDue=Time.realtimeSinceStartupAsDouble+1;
            weatherTileJob=Task.Run(()=>WeatherFieldTile.Build(weather,position,days,radius,token),token);
        }
        weatherScreen.FieldConditions=weatherConditions;weatherScreen.FieldLayers=weatherLayers;weatherScreen.Field=weatherTile;
        weatherScreen.PreviousConditions=previousConditions;weatherScreen.PreviousLayers=previousLayers;weatherScreen.PreviousField=previousWeatherTile;
        float blend=Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble-fieldChangedAt)/1.25f);blend=blend*blend*(3-2*blend);
        weatherScreen.FieldBlend=previousWeatherTile!=null&&previousWeatherTile.Contains(lat,lon,0)?blend:1;
        weatherScreen.UseField=weatherTile!=null&&weatherTile.Contains(lat,lon,0)&&TestWeatherOverride==null&&WeatherControl.Mode=="auto";
    }
    public bool WeatherFieldReady=>weatherTile!=null&&weatherTileJob==null&&Time.realtimeSinceStartupAsDouble-fieldChangedAt>=1.25;
    void DisposeWeatherField(){if(weatherConditions!=null)Destroy(weatherConditions);if(weatherLayers!=null)Destroy(weatherLayers);if(previousConditions!=null)Destroy(previousConditions);if(previousLayers!=null)Destroy(previousLayers);}
}
