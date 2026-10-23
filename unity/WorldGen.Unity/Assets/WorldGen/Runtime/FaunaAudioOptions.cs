using UnityEngine;

public sealed class FaunaAudioOptions
{
    public static readonly FaunaAudioOptions Current=new FaunaAudioOptions();
    public static readonly string[] Methods={"Stable Audio 3","Procedural","Field recordings"};
    public static readonly string[] Groups={"Crickets","Cicadas","Katydids","Native frogs","Understory birds"};
    public bool Enabled=true;public float Volume=.65f;
    public readonly bool[] GroupsEnabled={true,true,true,true,true};
    public readonly int[] Source=new int[5];
    bool loaded;
    public void Load()
    {
        if(loaded)return;loaded=true;
        Enabled=PlayerPrefs.GetInt("fauna-audio-enabled",1)!=0;Volume=Mathf.Clamp01(PlayerPrefs.GetFloat("fauna-audio-volume",.65f));
        for(int i=0;i<5;i++){GroupsEnabled[i]=PlayerPrefs.GetInt("fauna-audio-group-"+i,1)!=0;Source[i]=Mathf.Clamp(PlayerPrefs.GetInt("fauna-audio-source-"+i,0),0,2);}
    }
    public void Save()
    {
        PlayerPrefs.SetInt("fauna-audio-enabled",Enabled?1:0);PlayerPrefs.SetFloat("fauna-audio-volume",Volume);
        for(int i=0;i<5;i++){PlayerPrefs.SetInt("fauna-audio-group-"+i,GroupsEnabled[i]?1:0);PlayerPrefs.SetInt("fauna-audio-source-"+i,Source[i]);}
        PlayerPrefs.Save();
    }
}
public sealed partial class WorldGenRuntime
{
    void DrawFaunaAudioOptions()
    {
        var o=FaunaAudioOptions.Current;o.Load();GUILayout.Space(12);GUILayout.Label("WILDLIFE AMBIENCE",SectionStyle());
        bool previous=GUI.changed;GUI.changed=false;
        o.Enabled=GUILayout.Toggle(o.Enabled,"Habitat-based wildlife sounds");
        GUILayout.Label("Wildlife volume  "+Mathf.RoundToInt(o.Volume*100)+"%");o.Volume=GUILayout.HorizontalSlider(o.Volume,0,1);
        for(int i=0;i<5;i++)
        {
            GUILayout.BeginHorizontal();o.GroupsEnabled[i]=GUILayout.Toggle(o.GroupsEnabled[i],FaunaAudioOptions.Groups[i]);
            bool enabled=GUI.enabled;GUI.enabled=terrainExplorer!=null&&terrainExplorer.FaunaClipCount(i,o.Source[i])>0;
            if(GUILayout.Button("Preview",GUILayout.Width(80)))terrainExplorer.PreviewFauna(i,o.Source[i]);GUI.enabled=enabled;GUILayout.EndHorizontal();
            o.Source[i]=GUILayout.SelectionGrid(o.Source[i],FaunaAudioOptions.Methods,3);
        }
        if(GUI.changed)o.Save();GUI.changed|=previous;
        if(terrainExplorer!=null)
        {
            GUILayout.Label(terrainExplorer.FaunaAudioDebug,new GUIStyle(GUI.skin.label){wordWrap=true});
            if(GUILayout.Button("Stop preview"))terrainExplorer.StopFaunaPreview();
        }
        GUILayout.Label("Preview ignores habitat/time, but respects master and wildlife volume. Natural callers depend on season, time, weather and nearby habitat.",new GUIStyle(GUI.skin.label){wordWrap=true});
    }
}
