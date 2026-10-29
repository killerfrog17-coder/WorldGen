using UnityEngine;
using WorldGen.Core;

public sealed class FaunaAudioOptions
{
    public static readonly FaunaAudioOptions Current=new FaunaAudioOptions();
    public static readonly string[] Groups=FaunaAmbience.Names;
    public bool Enabled=true,AnimalsEnabled=true;public float Volume=.65f,CreatureVolume=.8f;
    public readonly bool[] GroupsEnabled=new bool[FaunaAmbience.KindCount];
    bool loaded;
    public void Load()
    {
        if(loaded)return;loaded=true;
        Enabled=PlayerPrefs.GetInt("fauna-audio-enabled",1)!=0;Volume=Mathf.Clamp01(PlayerPrefs.GetFloat("fauna-audio-volume",.65f));
        AnimalsEnabled=PlayerPrefs.GetInt("fauna-animal-calls",1)!=0;CreatureVolume=Mathf.Clamp01(PlayerPrefs.GetFloat("fauna-animal-volume",.8f));
        for(int i=0;i<GroupsEnabled.Length;i++)GroupsEnabled[i]=true;
    }
    public void Save()
    {
        PlayerPrefs.SetInt("fauna-audio-enabled",Enabled?1:0);PlayerPrefs.SetFloat("fauna-audio-volume",Volume);
        PlayerPrefs.SetInt("fauna-animal-calls",AnimalsEnabled?1:0);PlayerPrefs.SetFloat("fauna-animal-volume",CreatureVolume);
        for(int i=0;i<GroupsEnabled.Length;i++)PlayerPrefs.SetInt("fauna-audio-group-"+i,GroupsEnabled[i]?1:0);
        PlayerPrefs.Save();
    }
}
public sealed partial class WorldGenRuntime
{
    void DrawFaunaAudioOptions()
    {
        var options=FaunaAudioOptions.Current;options.Load();GUILayout.Space(12);GUILayout.Label("WILDLIFE AUDIO",SectionStyle());
        bool previous=GUI.changed;GUI.changed=false;
        options.Enabled=GUILayout.Toggle(options.Enabled,"Habitat-based wildlife sounds");
        GUILayout.Label("Ambient wildlife volume  "+Mathf.RoundToInt(options.Volume*100)+"%");options.Volume=GUILayout.HorizontalSlider(options.Volume,0,1);
        options.AnimalsEnabled=GUILayout.Toggle(options.AnimalsEnabled,"Animal calls and reactions");
        GUILayout.Label("Animal volume  "+Mathf.RoundToInt(options.CreatureVolume*100)+"%");options.CreatureVolume=GUILayout.HorizontalSlider(options.CreatureVolume,0,1);
        if(GUI.changed)options.Save();GUI.changed|=previous;
    }
}
