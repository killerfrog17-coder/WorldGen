using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Serialization;

public sealed partial class WorldGenRuntime
{
    string activeWorldId, activeSaveId, pendingWorldId, pendingSaveId, pendingWorldName, deleteId; bool deleteGroup, saveDialog, mapTypeMenu, gridMenu;
    readonly HashSet<string> expandedWorlds=new HashSet<string>();
    string saveEditName;
    void OpenSave(WorldLibraryEntry entry)
    {
        Try(()=>{pendingPlay=WorldLibrary.ReadPlaySession(entry.SnapshotPath);pendingWorldId=entry.GroupId;pendingSaveId=entry.Kind=="quick"?entry.ParentSaveId:entry.Id;pendingWorldName=entry.Kind=="quick"?entry.ManualName??"New save":entry.Name;libraryOpen=false;BeginWork(false,entry.SnapshotPath);});
    }
    void DrawWorldLibrary()
    {
        GUI.Box(new Rect(0,0,Screen.width,Screen.height),"");
        float width=Math.Min(820,Screen.width-24),height=Math.Min(760,Screen.height-24);
        var rect=new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);GUI.Box(rect,"");
        GUILayout.BeginArea(new Rect(rect.x+16,rect.y+12,width-32,height-24));
        GUILayout.Label("YOUR WORLDS",new GUIStyle(GUI.skin.label){fontSize=24,fontStyle=FontStyle.Bold});
        GUI.enabled=!Busy && exportJob==null && deleteId==null;
        libraryScroll=GUILayout.BeginScrollView(libraryScroll);
        foreach(var group in saves.Where(e=>e.Id!="earth").GroupBy(e=>e.GroupId))
        {
            var versions=group.OrderByDescending(e=>e.SavedUtc,StringComparer.Ordinal).ThenBy(e=>e.Id,StringComparer.Ordinal).ToArray();
            var latest=versions[0];bool expanded=expandedWorlds.Contains(group.Key);
            GUILayout.BeginHorizontal(GUI.skin.box);
            if(versions.Length>1){if(GUILayout.Button(expanded?"−":"+",GUILayout.Width(30))){if(expanded)expandedWorlds.Remove(group.Key);else expandedWorlds.Add(group.Key);}}
            else GUILayout.Space(34);
            GUILayout.Label(latest.GroupName??latest.Name,GUILayout.ExpandWidth(true));
            bool enabled=GUI.enabled;GUI.enabled=enabled&&latest.Error.Length==0;
            if(GUILayout.Button("Load last save",GUILayout.Width(125)))OpenSave(latest);
            GUI.enabled=enabled;
            if(GUILayout.Button("Delete",GUILayout.Width(75))){deleteId=group.Key;deleteGroup=true;}
            GUILayout.EndHorizontal();
            if(expanded && versions.Length>1)foreach(var save in versions)
            {
                GUILayout.BeginHorizontal();GUILayout.Space(40);
                string label=save.Name+" ? "+(DateTime.TryParse(save.SavedUtc,out var time)?time.ToLocalTime().ToString("g"):save.SavedUtc);
                GUILayout.Label(label+(save.Error.Length>0?" · "+save.Error:""));
                GUI.enabled=enabled&&save.Error.Length==0;
                if(GUILayout.Button("Load",GUILayout.Width(75)))OpenSave(save);
                GUI.enabled=enabled;
                if(GUILayout.Button("Delete",GUILayout.Width(75))){deleteId=save.Id;deleteGroup=false;}
                GUILayout.EndHorizontal();
            }
        }
        if(!saves.Any(e=>e.Id!="earth"))GUILayout.Label("No saved worlds yet.");
        GUILayout.EndScrollView();
        if(error.Length>0)GUILayout.Label(error);
        GUILayout.BeginHorizontal();GUILayout.FlexibleSpace();if(GUILayout.Button("Back",GUILayout.Width(100)))libraryOpen=false;GUILayout.EndHorizontal();
        GUI.enabled=true;GUILayout.EndArea();
        if(deleteId!=null)
        {
            var modal=new Rect(Screen.width/2-210,Screen.height/2-85,420,170);GUI.Box(modal,"");
            GUILayout.BeginArea(new Rect(modal.x+20,modal.y+20,380,130));
            GUILayout.Label("Are you sure?");
            GUILayout.Label(deleteGroup?"Delete this world and all its saves?":"Delete this save?");
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Cancel"))deleteId=null;
            if(GUILayout.Button("Delete"))
            {
                Try(()=>{if(deleteGroup)library.DeleteWorld(deleteId);else library.DeleteSave(deleteId);saves=library.Entries();if(activeWorldId!=null&&!saves.Any(e=>e.GroupId==activeWorldId)){activeWorldId=null;activeSaveId=null;}});
                deleteId=null;
            }
            GUILayout.EndHorizontal();GUILayout.EndArea();
        }
    }
    void DrawSaveDialog()
    {
        GUI.Box(new Rect(0,0,Screen.width,Screen.height),"");
        GUILayout.BeginArea(new Rect(Screen.width/2-220,Screen.height/2-110,440,220),GUI.skin.box);
        GUILayout.Label(activeWorldId==null?"SAVE WORLD":"SAVE PROGRESS",new GUIStyle(GUI.skin.label){fontSize=24});
        if(saveEditName==null)saveEditName=saveName;
        GUI.enabled=exportJob==null;saveEditName=GUILayout.TextField(saveEditName,80);
        GUILayout.Label("Use the same name to overwrite; a new name creates another save.");
        if(libraryNotice.Length>0)GUILayout.Label(libraryNotice);
        GUI.enabled=exportJob==null;GUILayout.BeginHorizontal();
        if(GUILayout.Button("Cancel")){saveDialog=false;saveEditName=null;}
        if(GUILayout.Button("Save"))SaveToLibrary(false,saveEditName);
        GUILayout.EndHorizontal();GUI.enabled=true;GUILayout.EndArea();
    }
}
