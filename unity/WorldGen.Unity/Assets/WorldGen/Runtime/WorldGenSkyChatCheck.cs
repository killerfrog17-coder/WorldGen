using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Networking;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckSkyChat()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);AudioListener.volume=0;var report=new List<string>();var errors=new List<string>();
        Application.LogCallback listener=(message,trace,kind)=>{if(kind==LogType.Exception||kind==LogType.Error)errors.Add(message);};Application.logMessageReceived+=listener;
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("SKY CHAT CHECK: "+message);report.Add("PASS "+message);Debug.Log("SKY CHAT PASS "+message);}
        IEnumerator Until(Func<bool> condition,string label,double timeout=180){double end=Time.realtimeSinceStartupAsDouble+timeout;while(!condition()&&Time.realtimeSinceStartupAsDouble<end)yield return null;Check(condition(),label);}
        recipe=Recipe.Preset("Earth");recipe.Detail=20000;setup.From(recipe);Generate();yield return Until(()=>worldReady&&!Busy&&humans!=null,"Earth world ready");StartHumanWorld();
        PlayCommands.Execute(play,SimulationTime.Seconds(world.Prehistory),"/date set 0042-03-21");PlayCommands.Execute(play,0,"/time set noon");
        ExploreTerrain(TerrainMapViewport.FromDegrees(35,-4));yield return Until(()=>terrainExplorer!=null&&terrainExplorer.Ready&&terrainExplorer.AllReady,"scaled terrain and sky resident");
        terrainExplorer.Pause();while(humanStep!=null)yield return null;
        report.AddRange(terrainExplorer.Probes());
        IEnumerator Image(string name,string time,float yaw,float pitch)
        {
            Check(PlayCommands.Execute(play,SimulationTime.Seconds(world.Prehistory),"/time set "+time).Success,"time command "+time);
            terrainExplorer.TestView(40,yaw,pitch);yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();terrainExplorer.Capture(Path.Combine(output,name+".png"));
            Check(terrainExplorer.SkyFrame!=null&&terrainExplorer.SkyFrame.Bodies.Length==1,"one sun and no moon: "+name);
        }
        yield return Image("noon","noon",180,-35);var dayPixels=terrainExplorer.LastCapturePixels;
        yield return Image("sunrise","sunrise",90,-8);yield return Image("sunset","sunset",270,-8);
        yield return Image("midnight","midnight",180,-35);var nightPixels=terrainExplorer.LastCapturePixels;
        double Mean(Color32[] pixels)=>pixels.Take(pixels.Length/2).Average(p=>(p.r+p.g+p.b)/3d);
        Check(Mean(dayPixels)>Mean(nightPixels)*3,"day/night renders visibly differ");
        int bright=nightPixels.Skip(nightPixels.Length/2).Count(p=>Math.Max(p.r,Math.Max(p.g,p.b))>90);Check(bright>30,"star points actually render at night: "+bright);
        report.Add("Sky mean daylight "+Mean(dayPixels).ToString("F2")+", night "+Mean(nightPixels).ToString("F2"));
        string tagged=Directory.GetFiles(Path.Combine(musicFolder,"exploration")).First(p=>Path.GetFileName(p).Contains("+"));
        using(var audio=UnityWebRequestMultimedia.GetAudioClip(MusicFileUri(tagged),AudioType.OGGVORBIS))
        {
            yield return audio.SendWebRequest();Check(audio.result==UnityWebRequest.Result.Success,"original multi-biome filename decodes without renaming: "+Path.GetFileName(tagged));
            var clip=DownloadHandlerAudioClip.GetContent(audio);Check(clip!=null&&clip.length>=90,"tagged generated music has its full decoded duration");if(clip!=null)Destroy(clip);
        }
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();terrainExplorer.TestKeyboard(keyboard);terrainExplorer.Resume();
        yield return null;yield return null;
        IEnumerator KeyPress(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;}
        IEnumerator Type(string text){foreach(char c in text)InputSystem.QueueTextEvent(keyboard,c);yield return null;yield return null;}
        yield return KeyPress(Key.Enter);Check(terrainExplorer.ChatOpen&&terrainExplorer.ChatText=="","Enter opens an empty chat input");
        yield return Type("A quiet night");Check(terrainExplorer.ChatText=="A quiet night","actual keyboard text events enter chat");
        double travel=terrainExplorer.TravelM;yield return KeyPress(Key.W);Check(terrainExplorer.TravelM==travel,"typing keys cannot move the player while chat is open");
        yield return KeyPress(Key.Enter);Check(!terrainExplorer.ChatOpen,"Enter submits ordinary local chat");
        yield return KeyPress(Key.Slash);Check(terrainExplorer.ChatOpen&&terrainExplorer.ChatText=="/","slash opens chat already prefixed without duplication");
        yield return Type("help");yield return KeyPress(Key.Enter);Check(!terrainExplorer.ChatOpen,"slash help executes through the real chat input");
        yield return KeyPress(Key.Slash);yield return Type("time set noon");yield return KeyPress(Key.Enter);Check(Math.Abs(play.SolarHour-12)<.05,"chat time command changes the live clock");
        yield return KeyPress(Key.Slash);yield return Type("date set 0042-12-21");yield return KeyPress(Key.Enter);Check(PlayCalendar.Date(PlayCalendar.Seconds(play,SimulationTime.Seconds(world.Prehistory)))=="0042-12-21","chat date command changes the seasonal calendar");
        yield return KeyPress(Key.Enter);yield return KeyPress(Key.UpArrow);Check(terrainExplorer.ChatText=="/date set 0042-12-21","up arrow recalls the command history");
        yield return KeyPress(Key.Escape);Check(!terrainExplorer.ChatOpen&&!terrainExplorer.Paused,"Escape closes chat without opening pause");
        yield return KeyPress(Key.Slash);yield return Type("h");yield return KeyPress(Key.Tab);Check(terrainExplorer.ChatText=="/help ","Tab completes commands");yield return KeyPress(Key.Enter);
        yield return KeyPress(Key.Enter);yield return new WaitForEndOfFrame();var ui=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,"chat.png"),ui.EncodeToPNG());Destroy(ui);
        yield return KeyPress(Key.Escape);terrainExplorer.Pause();while(humanStep!=null)yield return null;
        // Commands themselves never enqueue simulation work. Normal unpaused gameplay above may legitimately advance it.
        string before=world.Prehistory.Hash;PlayCommands.Execute(play,SimulationTime.Seconds(world.Prehistory),"/date set 0042-06-21");PlayCommands.Execute(play,SimulationTime.Seconds(world.Prehistory),"/time set midnight");
        Check(world.Prehistory.Hash==before,"date/time edits do not rewrite recorded historical state");
        var saved=WorldJson.Read<PlaySession>(WorldJson.Write(play));saved.Validate();Check(saved.CalendarOffsetSeconds==play.CalendarOffsetSeconds&&saved.SolarDays==play.SolarDays,"calendar and solar phase cold roundtrip");
        Check(errors.Count==0,"no Unity shader/runtime errors");InputSystem.RemoveDevice(keyboard);Application.logMessageReceived-=listener;
        File.WriteAllLines(Path.Combine(output,"result.txt"),report);Debug.Log("SKY CHAT CHECK PASS "+report.Count(s=>s.StartsWith("PASS ",StringComparison.Ordinal)));Application.Quit(0);
    }
}
