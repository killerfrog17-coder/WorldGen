using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class WorldGenRuntime
{
    bool skyPresetMenu,skyAdvanced;
    void DrawSkySettings()
    {
        if(play.Settings.Sky==null)play.Settings.Sky=CelestialSettings.Preset("No moons");var sky=play.Settings.Sky;
        bool locked=world?.Prehistory?.Started==true&&screen!="Setup";bool enabled=GUI.enabled;
        GUILayout.Label("SKY & ATMOSPHERE",SectionStyle());GUI.enabled=enabled&&!locked;
        GUILayout.Label("Stellar environment");
        sky.Stars=(StellarEnvironment)GUILayout.SelectionGrid((int)sky.Stars,new[]{"Solar neighborhood","Dense galactic field","Star cluster"},1);
        if(GUILayout.Button("Moon arrangement  ▾"))skyPresetMenu=!skyPresetMenu;
        if(skyPresetMenu){foreach(string name in CelestialSettings.Presets)if(GUILayout.Button(name)){var next=CelestialSettings.Preset(name);next.Stars=sky.Stars;next.Seed=sky.Seed;next.Haze=sky.Haze;play.Settings.Sky=sky=next;skyPresetMenu=false;}}
        double Number(string key,string label,double value,double min,double max)
        {
            key="sky-"+key;GUILayout.BeginHorizontal();GUILayout.Label(label);GUI.SetNextControlName(key);
            if(!inputText.TryGetValue(key,out var text)||GUI.GetNameOfFocusedControl()!=key)text=value.ToString("G6",CultureInfo.InvariantCulture);
            string next=GUILayout.TextField(text,GUILayout.Width(100));inputText[key]=next;GUILayout.EndHorizontal();
            if(next!=text&&double.TryParse(next,NumberStyles.Float,CultureInfo.InvariantCulture,out double v)&&!double.IsNaN(v)&&!double.IsInfinity(v)&&v>=min&&v<=max)return v;return value;
        }
        for(int i=0;i<sky.Moons.Length;i++)
        {
            var moon=sky.Moons[i];GUILayout.Space(12);GUILayout.BeginHorizontal();moon.Name=GUILayout.TextField(moon.Name,48);if(GUILayout.Button("×",GUILayout.Width(28))){sky.Moons=sky.Moons.Where((m,k)=>k!=i).ToArray();GUILayout.EndHorizontal();break;}GUILayout.EndHorizontal();
            if(GUILayout.Button("Surface: "+moon.Surface))moon.Surface=(MoonSurface)(((int)moon.Surface+1)%4);
            moon.RadiusKm=Number(i+"-radius","Radius · km",moon.RadiusKm,2,3500);moon.OrbitRadiusKm=Number(i+"-orbit","Orbit · km from center",moon.OrbitRadiusKm,30000,700000);
            GUILayout.BeginHorizontal();if(GUILayout.Button("White"))moon.Tint=new V(.94,.95,1);if(GUILayout.Button("Rust"))moon.Tint=new V(1,.35,.18);if(GUILayout.Button("Ice blue"))moon.Tint=new V(.7,.87,1);GUILayout.EndHorizontal();
            GUILayout.Label("Orbit "+moon.OrbitalPeriodDays.ToString("F2")+" days · phases "+moon.SynodicPeriodDays.ToString("F2")+" days",MutedStyle());
            if(skyAdvanced)
            {
                moon.Albedo=Number(i+"-albedo","Reflectivity",moon.Albedo,.01,.95);moon.DensityKgM3=Number(i+"-density","Density · kg/m³",moon.DensityKgM3,1500,6000);
                moon.InclinationDegrees=Number(i+"-incline","Inclination · °",moon.InclinationDegrees,0,60);moon.NodeDegrees=Number(i+"-node","Ascending node · °",moon.NodeDegrees,0,360);moon.LongitudeAtEpochDegrees=Number(i+"-phase","Orbital epoch · °",moon.LongitudeAtEpochDegrees,0,360);
                double r=Number(i+"-red","Red reflectance",moon.Tint.X,.02,1),g=Number(i+"-green","Green reflectance",moon.Tint.Y,.02,1),b=Number(i+"-blue","Blue reflectance",moon.Tint.Z,.02,1);moon.Tint=new V(r,g,b);
            }
        }
        if(sky.Moons.Length<4&&GUILayout.Button("Add moon"))sky.Moons=sky.Moons.Concat(new[]{new MoonSettings{Name="Moon "+(sky.Moons.Length+1),RadiusKm=700,OrbitRadiusKm=100000+sky.Moons.Length*150000}}).ToArray();
        skyAdvanced=GUILayout.Toggle(skyAdvanced,"Orbital and color details");if(skyAdvanced)sky.Seed=(int)Number("seed","Stellar seed",sky.Seed,0,16777215);
        GUI.enabled=enabled;if(locked)GUILayout.Label("Moons and stars are fixed once history starts. Choose another sky in a new game.",MutedStyle());
        GUILayout.Space(12);GUILayout.Label("Aerosol haze  "+sky.Haze.ToString("F2")+"×");sky.Haze=GUILayout.HorizontalSlider((float)sky.Haze,0,5);
        try{sky.Validate();}catch(ArgumentException e){GUILayout.Label(e.Message,MutedStyle());}
    }
}
