using UnityEngine;

public sealed class TerrainVideoOptions
{
    public static readonly TerrainVideoOptions Current=new TerrainVideoOptions();
    public float DistanceKm=200,DetailRange=1,TransitionSeconds=.8f;
    public int Quality=1;
    public void Load()
    {
        DistanceKm=Mathf.Clamp(PlayerPrefs.GetFloat("terrain-distance-km",200),20,500);
        DetailRange=Mathf.Clamp(PlayerPrefs.GetFloat("terrain-detail-range",1),.5f,2);
        TransitionSeconds=Mathf.Clamp(PlayerPrefs.GetFloat("terrain-transition",.8f),.2f,2);
        Quality=Mathf.Clamp(PlayerPrefs.GetInt("terrain-quality",1),0,2);
    }
    public void Save()
    {
        PlayerPrefs.SetFloat("terrain-distance-km",DistanceKm);PlayerPrefs.SetFloat("terrain-detail-range",DetailRange);
        PlayerPrefs.SetFloat("terrain-transition",TransitionSeconds);PlayerPrefs.SetInt("terrain-quality",Quality);
    }
}
public sealed partial class WorldGenRuntime
{
    void DrawTerrainVideoOptions()
    {
        var o=TerrainVideoOptions.Current;GUILayout.Space(12);GUILayout.Label("TERRAIN",SectionStyle());bool prior=GUI.changed;GUI.changed=false;
        GUILayout.Label("Distant landscape  "+o.DistanceKm.ToString("F0")+" km");o.DistanceKm=GUILayout.HorizontalSlider(o.DistanceKm,20,500);
        GUILayout.Label("Detail distance  "+o.DetailRange.ToString("F1")+"x");o.DetailRange=GUILayout.HorizontalSlider(o.DetailRange,.5f,2);
        GUILayout.Label("Mesh quality");o.Quality=GUILayout.SelectionGrid(o.Quality,new[]{"Low","Balanced","High"},3);
        GUILayout.Label("LOD transition  "+o.TransitionSeconds.ToString("F1")+" s");o.TransitionSeconds=GUILayout.HorizontalSlider(o.TransitionSeconds,.2f,2);
        if(GUI.changed)o.Save();GUI.changed|=prior;
    }
}
