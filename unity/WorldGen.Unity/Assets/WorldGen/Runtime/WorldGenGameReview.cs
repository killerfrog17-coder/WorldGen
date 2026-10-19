using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckGameShell()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);AudioListener.volume=0;
        var report=new List<string>();var errors=new List<string>();Application.LogCallback listener=(message,trace,type)=>{if(type==LogType.Exception||type==LogType.Error)errors.Add(message);};Application.logMessageReceived+=listener;
        void Check(bool yes,string message){if(!yes)throw new InvalidOperationException("GAME REVIEW: "+message);report.Add("PASS "+message);}
        IEnumerator Show(string name)
        {
            double until=Time.realtimeSinceStartupAsDouble+5;while(Time.realtimeSinceStartupAsDouble<until)yield return null;
            yield return new WaitForEndOfFrame();var texture=ScreenCapture.CaptureScreenshotAsTexture();var pixels=texture.GetPixels32();
            if(pixels.All(p=>p.r==pixels[0].r&&p.g==pixels[0].g&&p.b==pixels[0].b))throw new InvalidOperationException("Blank visible UI capture: "+name);
            File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());Destroy(texture);Debug.Log("GAME REVIEW SCREEN "+name);
        }
        screen="Menu";yield return Show("01-main-menu");
        NewGame();int firstSeed=recipe.Seed;NewGame();Check(recipe.Seed!=firstSeed,"new-game seeds roll independently");
        Check(setupCategory=="World"&&!advancedGeneration&&panels.All(p=>!p)&&recipe.Detail==200000,"world-generation tab defaults to 200000 cells with advanced settings closed");
        Check(play.Settings.HorizontalScale==.1&&play.Settings.RealMinutesPerDay==60,"new-game defaults are tenth scale and a one-hour day");
        yield return Show("02-new-game");setupCategory="Time";yield return Show("02b-time-settings");setupCategory="World";advancedGeneration=true;yield return Show("03-world-generation");
        recipe=Recipe.Preset("Earth");recipe.Detail=5000;setup.From(recipe);preset="Earth";saveName="Game-flow review";Generate();
        double deadline=Time.realtimeSinceStartupAsDouble+240;while((!worldReady||Busy||humans==null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(worldReady&&!Busy&&humans!=null,"generation completes through the existing world pipeline");StartHumanWorld();
        yield return Show("04-world-simulation");Check(screen=="Simulation","new worlds begin in the simulation before entering play");
        RequestTerrain();deadline=Time.realtimeSinceStartupAsDouble+180;while((terrainExplorer==null||!terrainExplorer.Ready||!terrainExplorer.AllReady||!terrainExplorer.ScatterReady)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(terrainExplorer!=null&&terrainExplorer.Ready&&terrainExplorer.AllReady&&terrainExplorer.ScatterReady,"tenth-scale terrain and scatter stream successfully");
        Check(terrainExplorer.HorizontalScale==.1,"playable coordinates use the saved scale");
        long before=SimulationTime.Seconds(world.Prehistory);double solar=play.SolarDays;yield return Show("05-playing");
        Check(SimulationTime.Seconds(world.Prehistory)>before&&play.SolarDays>solar,"playing advances the actual historical event calendar and solar time");
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F3));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        Check(terrainExplorer.DebugVisible&&Cursor.lockState==CursorLockMode.Locked,"F3 opens text without releasing mouse-look");
        double travel=terrainExplorer.TravelM;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        Check(terrainExplorer.TravelM!=travel,"movement remains active while F3 is visible");yield return Show("06-f3-overlay");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.M));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        deadline=Time.realtimeSinceStartupAsDouble+60;while(!terrainExplorer.MapReady&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(terrainExplorer.MapVisible&&terrainExplorer.MapReady&&Cursor.lockState==CursorLockMode.None,"M independently opens the satellite world map");yield return Show("07-satellite-map");
        terrainExplorer.TestMapLayer(Layer.Climate);deadline=Time.realtimeSinceStartupAsDouble+60;while(!terrainExplorer.MapReady&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(terrainExplorer.MapReady,"the in-game map switches to cached climate data");yield return Show("08-climate-map");
        var previous=terrainExplorer.Position;terrainExplorer.TestMapZoom(.05);terrainExplorer.TestMapClick(.63,.43);Check(D.Angle(previous,terrainExplorer.Position)>.001,"the map teleports to exact planetary coordinates");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.M));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        Check(terrainExplorer.Paused,"Escape opens the game pause menu");
        while(humanStep!=null)yield return null;solar=play.SolarDays;long pausedTime=SimulationTime.Seconds(world.Prehistory);yield return Show("09-pause-menu");
        Check(solar==play.SolarDays&&pausedTime==SimulationTime.Seconds(world.Prehistory),"pause freezes both clocks without catch-up");
        playPauseTab="World settings";yield return Show("10-world-settings");playPauseTab="";
        CapturePlayer();var savedPosition=play.PlayerPosition;string history=world.Prehistory.Hash;
        SaveToLibrary();while(exportJob!=null)yield return null;Check(activeWorldId!=null,"game saves include the world and gameplay envelope");
        var entry=saves.First(e=>e.GroupId==activeWorldId);var cold=WorldLibrary.ReadPlaySession(entry.SnapshotPath);Check(cold.HasPlayer&&cold.Settings.HorizontalScale==.1&&cold.PlayerPosition.X==savedPosition.X,"cold save restores scale and exact player position");
        ReturnFromPlay("Simulation");yield return null;yield return null;Check(terrainExplorer==null&&world.Prehistory.Hash==history,"opening simulation preserves the player and pauses history");
        yield return Show("11-returned-simulation");humanPlaying=true;RequestTerrain();deadline=Time.realtimeSinceStartupAsDouble+120;while((terrainExplorer==null||!terrainExplorer.Ready)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(terrainExplorer!=null&&D.Angle(terrainExplorer.Position,savedPosition)<1e-6,"returning to play restores the same planetary location");
        ReturnFromPlay("Simulation");yield return null;yield return null;Check(!humanPlaying,"leaving play never restores an earlier fast-forward speed");
        OpenSave(entry);deadline=Time.realtimeSinceStartupAsDouble+180;
        while((Busy||continueLoadedGame||terrainExplorer==null||!terrainExplorer.Ready)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(terrainExplorer!=null&&terrainExplorer.Ready&&D.Angle(terrainExplorer.Position,savedPosition)<1e-6&&play.Settings.HorizontalScale==.1,"loading the saved world reopens gameplay with its exact location and scale");
        Check(world.Prehistory.Hash==history,"cold gameplay load preserves saved history before its next clock publication");
        terrainExplorer.Pause();yield return Show("12-loaded-game");
        InputSystem.RemoveDevice(keyboard);Application.logMessageReceived-=listener;Check(errors.Count==0,"no Unity errors or exceptions");
        File.WriteAllLines(Path.Combine(output,"result.txt"),report);Debug.Log("GAME REVIEW PASS "+report.Count+" checks");
        // Leave the interactive build open for owner inspection, rather than closing their review.
    }
}
