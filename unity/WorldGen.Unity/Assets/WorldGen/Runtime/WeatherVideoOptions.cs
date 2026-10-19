using UnityEngine;

public sealed class WeatherVideoOptions
{
    public static readonly WeatherVideoOptions Current=new WeatherVideoOptions();
    public int Clouds=1,Particles=1;
    public float SunGlare=.35f,SunRays=.12f,Adaptation=.5f,Lightning=.25f,WindVolume=.35f,ThunderVolume=.4f;
    public void Load()
    {
        Clouds=Mathf.Clamp(PlayerPrefs.GetInt("weather-clouds",1),0,3);Particles=Mathf.Clamp(PlayerPrefs.GetInt("weather-particles",1),0,2);
        SunGlare=Read("glare",.35f);SunRays=Read("rays",.12f);Adaptation=Read("adaptation",.5f);Lightning=Read("lightning",.25f);WindVolume=Read("wind",.35f);ThunderVolume=Read("thunder",.4f);
    }
    static float Read(string key,float value)=>Mathf.Clamp01(PlayerPrefs.GetFloat("weather-"+key,value));
    public void Save()
    {
        PlayerPrefs.SetInt("weather-clouds",Clouds);PlayerPrefs.SetInt("weather-particles",Particles);
        PlayerPrefs.SetFloat("weather-glare",SunGlare);PlayerPrefs.SetFloat("weather-rays",SunRays);PlayerPrefs.SetFloat("weather-adaptation",Adaptation);PlayerPrefs.SetFloat("weather-lightning",Lightning);PlayerPrefs.SetFloat("weather-wind",WindVolume);PlayerPrefs.SetFloat("weather-thunder",ThunderVolume);
    }
}
public sealed partial class WorldGenRuntime
{
    void DrawWeatherVideoOptions()
    {
        var o=WeatherVideoOptions.Current;GUILayout.Space(12);GUILayout.Label("ATMOSPHERE",SectionStyle());bool changed=GUI.changed;GUI.changed=false;
        GUILayout.Label("Volumetric clouds");o.Clouds=GUILayout.SelectionGrid(o.Clouds,new[]{"Off","Low","Medium","High"},4);
        GUILayout.Label("Rain / snow particles");o.Particles=GUILayout.SelectionGrid(o.Particles,new[]{"Off","Low","High"},3);
        float Slider(string name,float value){GUILayout.Label(name+"  "+Mathf.RoundToInt(value*100)+"%");return GUILayout.HorizontalSlider(value,0,1);}
        o.SunGlare=Slider("Sun glare",o.SunGlare);o.SunRays=Slider("Sun rays",o.SunRays);o.Adaptation=Slider("Eye adaptation",o.Adaptation);o.Lightning=Slider("Lightning flashes · 0 = off",o.Lightning);
        o.WindVolume=Slider("Wind ambience",o.WindVolume);o.ThunderVolume=Slider("Thunder",o.ThunderVolume);
        if(GUI.changed)o.Save();GUI.changed|=changed;
    }
}
