using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    readonly Dictionary<string,Texture2D> habitatTextures=new Dictionary<string,Texture2D>();
    readonly HashSet<string> habitatRequested=new HashSet<string>();
    IEnumerator LoadHabitatTexture(string key)
    {
        string path=Path.Combine(Application.streamingAssetsPath,"atlas","biomes",key+".jpg");
        using(var request=UnityWebRequestTexture.GetTexture(new Uri(path).AbsoluteUri,true))
        {
            yield return request.SendWebRequest();
            if(request.result==UnityWebRequest.Result.Success)habitatTextures[key]=DownloadHandlerTexture.GetContent(request);
        }
    }
    void DrawHabitatPortrait(HabitatPortrait portrait,bool fauna=true)
    {
        if(portrait==null)return;
        if(habitatRequested.Add(portrait.Key))StartCoroutine(LoadHabitatTexture(portrait.Key));
        var image=GUILayoutUtility.GetRect(100,190,GUILayout.ExpandWidth(true));
        if(habitatTextures.TryGetValue(portrait.Key,out var texture))GUI.DrawTexture(image,texture,ScaleMode.ScaleAndCrop);
        else GUI.Box(image,"Habitat illustration");
        GUILayout.Label(portrait.Title,new GUIStyle(GUI.skin.label){fontSize=18,fontStyle=FontStyle.Bold});
        GUILayout.Label(portrait.Caption,new GUIStyle(GUI.skin.label){fontSize=10,wordWrap=true});
        GUILayout.Space(4);
        foreach(var fact in portrait.Conditions){GUILayout.Label(fact.Label+": "+fact.Value,new GUIStyle(GUI.skin.label){fontSize=12,wordWrap=true});}
        if(fauna&&portrait.Fauna.Length>0)
        {
            GUILayout.Space(10);GUILayout.Label("RECORDED NATIVE FAUNA",new GUIStyle(GUI.skin.label){fontSize=11,fontStyle=FontStyle.Bold});
            foreach(var animal in portrait.Fauna)
            {
                GUILayout.BeginVertical(GUI.skin.box);GUILayout.Label(animal.Name,new GUIStyle(GUI.skin.label){fontSize=13});
                GUILayout.Label(animal.Role+" · "+animal.BodyMass,new GUIStyle(GUI.skin.label){fontSize=11,wordWrap=true});
                if(animal.Variant.Length>0&&animal.Variant!=animal.Name)GUILayout.Label(animal.Variant,new GUIStyle(GUI.skin.label){fontSize=11,wordWrap=true});
                GUILayout.EndVertical();
            }
            GUILayout.Label("Representative pack members, not an animal census.",new GUIStyle(GUI.skin.label){fontSize=10,wordWrap=true});
        }
        GUILayout.Space(12);
    }
    void DisposeHabitatPortraits(){foreach(var texture in habitatTextures.Values)if(texture!=null)Destroy(texture);habitatTextures.Clear();}
}
