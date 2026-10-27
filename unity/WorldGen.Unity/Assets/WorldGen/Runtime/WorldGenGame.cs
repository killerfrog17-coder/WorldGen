using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class WorldGenRuntime
{
    PlaySession play=new PlaySession(),pendingPlay;
    bool advancedGeneration,discardPrompt,continueLoadedGame;
    string setupCategory="World";
    string playPauseTab="",optionsReturn="Menu";Vector2 gameSettingsScroll;
    double playPublicationAt;bool calendarWorkIsPlay;
    void NewGame()
    {
        if(Busy||humanStep!=null)return;
        if(worldReady&&world!=null&&!discardPrompt){discardPrompt=true;return;}
        discardPrompt=false;humanPlaying=false;humanCancellation?.Cancel();world=null;worldReady=false;humanWorld=null;humans=null;activeWorldId=null;
        if(globe!=null)StartCoroutine(Release(globe));globe=null;displayedWorld=null;
        play=new PlaySession();saveName="New world";recipe=new Recipe{Detail=200000};recipe=setup.Preset("Continents plus",recipe);preset="Continents plus";RollSeed();
        panels=groups.Select(_=>false).ToArray();advancedGeneration=false;setupCategory="World";inputText.Clear();screen="Setup";error="";selectedCell=-1;
    }
    void CapturePlayer()
    {if(terrainExplorer!=null&&terrainExplorer.Ready)terrainExplorer.StorePlayer(play);}
    void ReturnFromPlay(string destination)
    {
        CapturePlayer();humanPlaying=false;playPauseTab="";
        terrainExplorer?.Close();simulationPaused=false;screen=destination;humanClockSample=Time.realtimeSinceStartupAsDouble;humanPlayback.Reset();
    }
    void DrawPlayPause()
    {
        if(saveDialog)return;
        GUI.skin=Theme();float width=Math.Min(540,Screen.width-32),height=Math.Min(620,Screen.height-40);
        var r=new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);MenuCard(r);
        GUILayout.BeginArea(new Rect(r.x+28,r.y+23,r.width-56,r.height-46));
        GUILayout.Label(playPauseTab.Length==0?"PAUSED":playPauseTab.ToUpperInvariant(),TitleStyle(26));GUILayout.Space(16);
        if(error.Length>0)GUILayout.Label(error);
        if(playPauseTab.Length==0)
        {
            if(MenuButton("Resume game"))terrainExplorer.Resume();
            if(MenuButton("World simulation"))ReturnFromPlay("Simulation");
            if(MenuButton("Save game")){CapturePlayer();libraryNotice="";saveDialog=true;}
            if(MenuButton("Game settings"))playPauseTab="Game settings";
            if(MenuButton("World settings"))playPauseTab="World settings";
            GUILayout.FlexibleSpace();if(MenuButton("Main menu"))ReturnFromPlay("Menu");
        }
        else
        {
            if(playPauseTab=="Game settings")
            DrawSettingsPage();
            else {gameSettingsScroll=GUILayout.BeginScrollView(gameSettingsScroll);DrawPlaySettings(true);DrawSkySettings();GUILayout.EndScrollView();}
            if(playPauseTab=="World settings"&&terrainExplorer.HorizontalScale!=play.Settings.HorizontalScale&&MenuButton("Apply scale and reload terrain"))
            {ReturnFromPlay("Simulation");RequestTerrain();}
            if(MenuButton("Back"))playPauseTab="";
        }
        GUILayout.EndArea();
    }
    void DrawPlaySettings(bool editing=false,string section=null)
    {
        var s=play.Settings;
        double Number(string key,string label,double value,double min,double max,string suffix)
        {
            GUILayout.BeginHorizontal();GUILayout.Label(label,GUILayout.MinWidth(130));GUI.SetNextControlName("play-"+key);
            if(!inputText.TryGetValue("play-"+key,out var text)||GUI.GetNameOfFocusedControl()!="play-"+key)text=value.ToString("G5",CultureInfo.InvariantCulture);
            string next=GUILayout.TextField(text,GUILayout.Width(85));inputText["play-"+key]=next;GUILayout.Label(suffix,GUILayout.Width(55));GUILayout.EndHorizontal();
            if(next!=text&&double.TryParse(next,NumberStyles.Float,CultureInfo.InvariantCulture,out double parsed)&&!double.IsNaN(parsed)&&!double.IsInfinity(parsed)&&parsed>=min&&parsed<=max)value=parsed;
            return value;
        }
        if(section==null||section=="Time"){
        GUILayout.Label("TIME & CALENDAR",SectionStyle());
        s.RealMinutesPerDay=Number("day","Day length",s.RealMinutesPerDay,1,10080,"min IRL");
        s.CalendarDaysPerDay=Number("calendar","Calendar per day",s.CalendarDaysPerDay,1,365,"days");
        GUILayout.BeginHorizontal();foreach(var choice in new[]{("Day",1d),("Week",7d),("Month",SimulationTime.Year/(12d*SimulationTime.Day))})
            if(GUILayout.Button(choice.Item1)){s.CalendarDaysPerDay=choice.Item2;inputText.Remove("play-calendar");GUI.FocusControl(null);}GUILayout.EndHorizontal();
        GUILayout.Label("One solar cycle: "+s.RealMinutesPerDay.ToString("G4")+" real minutes · one calendar year: "+(SimulationTime.Year/(double)SimulationTime.Day/s.CalendarDaysPerDay*s.RealMinutesPerDay/60).ToString("G4")+" real hours",MutedStyle());
        GUILayout.Space(18);}
        if(section==null||section=="Scale"){
        GUILayout.Label("WORLD SCALE",SectionStyle());
        s.HorizontalScale=Number("scale","Horizontal scale",s.HorizontalScale,.01,2,"×");
        GUILayout.Label("Heights stay unchanged. Smaller worlds have steeper slopes.",MutedStyle());
        if(editing)GUILayout.Label("A scale change takes effect when local terrain is reloaded.",MutedStyle());
        GUILayout.Space(18);GUILayout.Label("STRUCTURES",SectionStyle());
        s.StructureDensity=Number("density","Scattered density",s.StructureDensity,0,100,"×");
        GUILayout.Label("Reserved for future local settlements. 1× preserves structures per playable km²; recorded cities are not removed.",MutedStyle());
        }
    }
    void DrawSimulationPause()
    {
        float width=Math.Min(520,Screen.width-32),height=Math.Min(620,Screen.height-40);var r=new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);MenuCard(r);
        GUILayout.BeginArea(new Rect(r.x+26,r.y+24,r.width-52,r.height-48));GUILayout.Label(playPauseTab.Length==0?"WORLD SIMULATION":playPauseTab.ToUpperInvariant(),TitleStyle(25));GUILayout.Space(18);
        if(playPauseTab.Length==0)
        {
            GUI.enabled=CanRequestTerrain;if(MenuButton(play.HasPlayer?"Resume game":"Enter world")){simulationPaused=false;RequestTerrain();}GUI.enabled=true;
            if(MenuButton("Return to simulation"))ToggleSimulationPause();
            if(MenuButton("Game settings"))playPauseTab="Game settings";
            if(MenuButton("World settings"))playPauseTab="World settings";
            if(MenuButton("Save game")){libraryNotice="";saveDialog=true;}
            if(Prehistory.CanUpgradeHistory(world?.Prehistory)&&humanStep==null&&MenuButton("Update simulation rules")){var engine=humans;var source=world.Prehistory;calendarWorkStarted=SimulationTime.Seconds(source);humanStep=System.Threading.Tasks.Task.Run(()=>engine.UpgradeToCurrentHistory(source));}
            GUILayout.FlexibleSpace();if(MenuButton("Main menu")){humanPlaying=false;simulationPaused=false;screen="Menu";}
        }
        else
        {if(playPauseTab=="World settings"){gameSettingsScroll=GUILayout.BeginScrollView(gameSettingsScroll);DrawPlaySettings();DrawSkySettings();GUILayout.EndScrollView();}else DrawSettingsPage();if(MenuButton("Back"))playPauseTab="";}
        GUILayout.EndArea();
    }
}
