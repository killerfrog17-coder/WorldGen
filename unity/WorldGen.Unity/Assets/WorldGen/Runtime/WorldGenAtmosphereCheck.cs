using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckAtmosphere()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);AudioListener.volume=0;var report=new List<string>();var errors=new List<string>();
        Application.LogCallback listener=(message,trace,kind)=>{if(kind==LogType.Error||kind==LogType.Exception)errors.Add(message);};Application.logMessageReceived+=listener;
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("ATMOSPHERE CHECK: "+message);report.Add("PASS "+message);Debug.Log("ATMOSPHERE PASS "+message);}
        IEnumerator Until(Func<bool> ready,string label){double end=Time.realtimeSinceStartupAsDouble+180;while(!ready()&&Time.realtimeSinceStartupAsDouble<end)yield return null;Check(ready(),label);}
        recipe=Recipe.Preset("Earth");recipe.Detail=20000;setup.From(recipe);Generate();yield return Until(()=>worldReady&&!Busy&&humans!=null,"Earth world ready");StartHumanWorld();
        Check(world.Prehistory.History.Sky!=null&&world.Prehistory.History.Sky.Settings.Moons.Length==1,"fresh game saves the actual Earthlike celestial context for future cultures");
        ExploreTerrain(TerrainMapViewport.FromDegrees(35,-4));yield return Until(()=>terrainExplorer!=null&&terrainExplorer.Ready&&terrainExplorer.AllReady,"terrain and atmosphere resident");terrainExplorer.Pause();while(humanStep!=null)yield return null;
        string history=world.Prehistory.Hash,geography=StateHash.Compute(world);var originalSky=play.Settings.Sky.Copy();
        IEnumerator Configure(CelestialSettings sky,double day,double hour)
        {
            // Isolated renderer fixtures only. Production UI fixes sky definitions after history starts.
            play.Settings.Sky=sky;play.CalendarOffsetSeconds=day*SimulationTime.Day-SimulationTime.Seconds(world.Prehistory)-play.PendingHistorySeconds;play.SetLocalSolarHour(hour,-4);
            yield return new WaitForSecondsRealtime(.6f);
        }
        void Capture(string name){terrainExplorer.Capture(Path.Combine(output,name+".png"));}
        void Aim(V direction,float fov=30){terrainExplorer.TestFieldOfView(fov);terrainExplorer.TestView(40,(float)(Math.Atan2(direction.X,direction.Z)*180/Math.PI),(float)(-Math.Asin(direction.Y)*180/Math.PI));}
        var earth=CelestialSettings.Preset("Earth-like");
        var traditions=Enumerable.Range(0,32).Select(i=>HistoricalSkyContext.Root(earth,i,k=>new Rng(82,"calendar-test-"+k,i).Unit(),i%2==0)).ToArray();File.WriteAllText(Path.Combine(output,"celestial-traditions.json"),WorldJson.Write(traditions));
        double full=CelestialMechanics.NextPhase(earth.Moons[0],0,.5),newMoon=CelestialMechanics.NextPhase(earth.Moons[0],0,0);
        yield return Configure(earth,full,0);var fullBody=terrainExplorer.SkyFrame.Bodies.Single(b=>b.MoonIndex==0);Aim(fullBody.Direction);yield return new WaitForEndOfFrame();Capture("earth-full");
        Check(fullBody.IlluminatedFraction>.97&&fullBody.Direction.Y>0&&terrainExplorer.SkyFrame.NightIlluminanceLux>.1,"visible full moon actually supplies night illumination");
        string debug=terrainExplorer.TestDebugText();Check(debug.Contains("Northern ")&&(debug.Contains("solstice")||debug.Contains("equinox")),"F3 reports seasonal turning points rather than requiring Earth month names");File.WriteAllText(Path.Combine(output,"debug.txt"),debug);
        var moonCenter=terrainExplorer.LastCapturePixels[400*1280+640];Check(moonCenter.r>80,"the full moon disk actually renders, not only its metadata");
        var charcoal=earth.Copy();charcoal.Moons[0].Albedo=.03;yield return Configure(charcoal,full,0);Aim(fullBody.Direction);yield return new WaitForEndOfFrame();Capture("dark-albedo-moon");Check(terrainExplorer.LastCapturePixels[400*1280+640].r<moonCenter.r*.75,"lower reflectivity darkens the actual lunar surface as well as terrain illumination");yield return Configure(earth,full,0);
        terrainExplorer.TestFieldOfView(68);terrainExplorer.TestView(40,180,12);yield return new WaitForEndOfFrame();Capture("full-moon-landscape");var lit=terrainExplorer.LastCapturePixels;
        yield return Configure(earth,newMoon,0);terrainExplorer.TestView(40,180,12);yield return new WaitForEndOfFrame();Capture("new-moon-landscape");var dark=terrainExplorer.LastCapturePixels;
        double Mean(Color32[] pixels)=>pixels.Take(1280*200).Average(p=>(p.r+p.g+p.b)/3d);
        Check(Mean(lit)>Mean(dark)*1.3+.1,"real terrain capture becomes brighter under a visible full moon than a new moon");report.Add("Lower terrain full/new "+Mean(lit).ToString("F3")+" / "+Mean(dark).ToString("F3"));
        double quarter=CelestialMechanics.NextPhase(earth.Moons[0],0,.25);yield return Configure(earth,quarter,18);var half=terrainExplorer.SkyFrame.Bodies.Single(b=>b.MoonIndex==0);Aim(half.Direction,12);yield return new WaitForEndOfFrame();Capture("earth-quarter-detail");Check(half.IlluminatedFraction>.43&&half.IlluminatedFraction<.57,"quarter-moon phase agrees with the actual sun direction");
        foreach(string preset in new[]{"Rust moon","Ice moon"})
        {
            var sky=CelestialSettings.Preset(preset);double d=CelestialMechanics.NextPhase(sky.Moons[0],0,.5);yield return Configure(sky,d,0);var b=terrainExplorer.SkyFrame.Bodies.Single(m=>m.MoonIndex==0);Aim(b.Direction,22);yield return new WaitForEndOfFrame();Capture(preset=="Rust moon"?"rust-moon":"ice-moon");
            var pixel=terrainExplorer.LastCapturePixels[400*1280+640];Check(preset=="Rust moon"?pixel.r>pixel.g:pixel.b>pixel.r,"actual rendered surface retains its configured color: "+preset);
        }
        var twins=CelestialSettings.Preset("Twin moons");double paired=-1;
        for(double day=0;day<100;day+=.2){var f=PlayableSky.Sample(35,0,day/365,twins);var a=f.Bodies[1];var b=f.Bodies[2];if(a.Direction.Y>.15&&b.Direction.Y>.15&&a.IlluminatedFraction>.25&&b.IlluminatedFraction>.25&&V.Dot(a.Direction,b.Direction)>.75){paired=day;break;}}
        Check(paired>=0,"a genuine orbital configuration puts both large moons above the horizon");yield return Configure(twins,paired,0);Aim((terrainExplorer.SkyFrame.Bodies[1].Direction+terrainExplorer.SkyFrame.Bodies[2].Direction).Unit,68);yield return new WaitForEndOfFrame();Capture("twin-moons");Check(terrainExplorer.SkyFrame.Bodies.Length==3,"both independently orbiting moons are submitted to the sky renderer");
        foreach(var environment in new[]{StellarEnvironment.SolarNeighborhood,StellarEnvironment.DenseGalacticField,StellarEnvironment.StarCluster})
        {
            var sky=CelestialSettings.Preset("No moons");sky.Stars=environment;yield return Configure(sky,full,0);var f=terrainExplorer.SkyFrame;
            int stellarSeed=StellarField.Seed(world.Recipe.Seed,sky.Seed);var direction=environment==StellarEnvironment.StarCluster?StellarField.ClusterAxis(stellarSeed):V.Cross(StellarField.Pole(stellarSeed),new V(0,1,0)).Unit;var local=f.StarRight*direction.X+f.StarUp*direction.Y+f.StarForward*direction.Z;
            // Renderer fixture only: rotate the time until the selected field is above the horizon.
            for(int hour=0;local.Y<.2&&hour<24;hour++){yield return Configure(sky,full,hour);f=terrainExplorer.SkyFrame;local=f.StarRight*direction.X+f.StarUp*direction.Y+f.StarForward*direction.Z;}if(local.Y<0)local=local*(-1);
            Aim(local,68);yield return new WaitForEndOfFrame();Capture("stars-"+environment);Check(f.Bodies.Length==1,"moonless stellar environment renders without hidden moons: "+environment);
        }
        var air=CelestialSettings.Preset("No moons");air.Haze=0;yield return Configure(air,100,12);terrainExplorer.TestFieldOfView(68);terrainExplorer.TestView(600,60,4);yield return Until(()=>terrainExplorer.AllReady,"long-distance atmospheric terrain resident");yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();Capture("air-clear");var clear=terrainExplorer.LastCapturePixels;
        air.Haze=3;yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();Capture("air-hazy");var hazy=terrainExplorer.LastCapturePixels;
        double difference=clear.Zip(hazy,(a,b)=>(Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b))/3d).Average();Check(difference>.1,"height-dependent atmospheric scattering actually changes distant terrain pixels");report.Add("Clear/haze mean absolute pixel difference "+difference.ToString("F3"));
        play.Settings.Sky=originalSky;yield return new WaitForSecondsRealtime(.3f);world.Prehistory.History.Sky.Validate(world.Prehistory.History,world.Prehistory.Years);
        var saved=WorldJson.Read<PlaySession>(WorldJson.Write(play));saved.Validate();Check(saved.Settings.Sky.Moons.Length==1&&saved.Settings.Sky.Moons[0].OrbitRadiusKm==384400,"configured moon physics cold-roundtrips in game saves");
        Check(world.Prehistory.Hash==history&&StateHash.Compute(world)==geography,"visual sky/phase tests do not rewrite recorded history or terrain");Check(errors.Count==0,"no Unity atmosphere shader/runtime errors");
        Application.logMessageReceived-=listener;File.WriteAllLines(Path.Combine(output,"result.txt"),report);Debug.Log("ATMOSPHERE CHECK PASS "+report.Count(x=>x.StartsWith("PASS ",StringComparison.Ordinal)));Application.Quit(0);
    }
}
