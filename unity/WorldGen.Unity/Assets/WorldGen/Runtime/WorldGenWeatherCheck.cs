using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckWeather()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);AudioListener.volume=0;var report=new List<string>();var errors=new List<string>();
        Application.LogCallback listener=(m,t,k)=>{if(k==LogType.Error||k==LogType.Exception)errors.Add(m);};Application.logMessageReceived+=listener;
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("WEATHER CHECK: "+text);report.Add("PASS "+text);Debug.Log("WEATHER PASS "+text);}
        var numeric=new List<string>();var numericClimate=new WeatherClimate(new[]{-12d,-8,-15,-20},new[]{80d,110,90,60},new[]{.6,.7,.6,.5});var numericWeather=new ProceduralWeather(817,new V(.7,.4,.5).Unit,numericClimate);
        foreach(double date in new[]{-32.01,0,120.125,73000000.2}){var f=numericWeather.At(date);var s=numericWeather.SurfaceAt(date);numeric.Add(string.Join(",",new[]{date,f.TemperatureC,f.RainMmPerHour,f.SnowWaterMmPerHour,numericWeather.Between(date-1,date).TotalMm,numericWeather.Between(date-30,date).TotalMm,s.SnowWaterMm}.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).ToArray()));}
        File.WriteAllLines(Path.Combine(output,"weather-numeric.csv"),numeric);
        IEnumerator Until(Func<bool> ready,string label){double end=Time.realtimeSinceStartupAsDouble+180;while(!ready()&&Time.realtimeSinceStartupAsDouble<end)yield return null;Check(ready(),label);}
        recipe=Recipe.Preset("Earth");recipe.Detail=20000;setup.From(recipe);Generate();yield return Until(()=>worldReady&&!Busy&&humans!=null,"Earth ready");StartHumanWorld();
        ExploreTerrain(TerrainMapViewport.FromDegrees(35,-4));yield return Until(()=>terrainExplorer!=null&&terrainExplorer.Ready&&terrainExplorer.AllReady,"terrain ready");terrainExplorer.Pause();while(humanStep!=null)yield return null;
        yield return Until(()=>terrainExplorer.WeatherSoundsReady,"SA3 sound library loaded asynchronously");
        Check(terrainExplorer.WeatherRecordingCount==17,"all seventeen generated wind/rain/thunder files decode in the native player");
        Check(terrainExplorer.WeatherRecordingSignalsValid,"native PCM readback of every generated recording is finite nonempty and unclipped");
        string history=world.Prehistory.Hash,geography=StateHash.Compute(world);var o=WeatherVideoOptions.Current;o.Clouds=1;terrainExplorer.BeginWeatherRenderCheck();
        play.CalendarOffsetSeconds=100*SimulationTime.Day-SimulationTime.Seconds(world.Prehistory)-play.PendingHistorySeconds;play.SetLocalSolarHour(10,-4);
        terrainExplorer.TestView(80,170,-18);terrainExplorer.TestFieldOfView(68);yield return Until(()=>terrainExplorer.AllReady,"sky-facing terrain ready");yield return new WaitForSecondsRealtime(.7f);
        Check(terrainExplorer.WeatherScreen!=null&&terrainExplorer.WeatherScreen.DrawCount>0,"URP volume and composite passes actually execute");
        Check(terrainExplorer.WeatherRecent(30).TotalMm>=terrainExplorer.WeatherRecent(1).TotalMm,"native retrospective rainfall queries remain consistent");
        void Capture(string name)=>terrainExplorer.Capture(Path.Combine(output,name+".png"));
        double Difference(Color32[] a,Color32[] b)=>a.Zip(b,(x,y)=>(Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b))/3d).Average();
        var weatherCamera=terrainExplorer.WeatherScreen.GetComponent<Camera>();double layerBase=Math.Max(1200,(weatherCamera.transform.position-terrainExplorer.WeatherScreen.PlanetCenter).magnitude-terrainExplorer.WeatherScreen.RadiusM+1200);
        WeatherFrame Fixture(double cover,double cirrus=0,double rain=0,double snow=0,double storm=0)=>new WeatherFrame(snow>0?-8:18,.8,rain,snow,cover,cirrus,layerBase,1700,8500,7,2,20,storm);
        o.Adaptation=0;o.SunGlare=0;o.SunRays=0;o.Clouds=1;o.Particles=1;
        terrainExplorer.TestWeatherOverride=Fixture(0);yield return new WaitForSecondsRealtime(.3f);Capture("clear");var clear=terrainExplorer.LastCapturePixels;
        terrainExplorer.WeatherScreen.DebugDepth=1;Capture("depth");terrainExplorer.WeatherScreen.DebugDepth=0;
        terrainExplorer.TestWeatherOverride=Fixture(.65);yield return new WaitForSecondsRealtime(.3f);Capture("cumulus-low");var cumulus=terrainExplorer.LastCapturePixels;
        Check(Difference(clear,cumulus)>.2,"low-quality volume visibly adds cloud bodies, not just metadata");
        o.Clouds=3;yield return new WaitForSecondsRealtime(.2f);Capture("cumulus-high");
        terrainExplorer.TestWeatherOverride=Fixture(.35);yield return new WaitForSecondsRealtime(.2f);Capture("scattered-cumulus");
        terrainExplorer.TestWeatherOverride=null;
        Check(PlayCommands.Execute(play,0,"/weather overcast",weather:terrainExplorer.WeatherControl).Success,"user weather command accepts overcast");
        yield return new WaitForSecondsRealtime(.3f);Capture("command-overcast");
        Check(terrainExplorer.CurrentWeather.CloudCover==1,"weather command drives the actual explorer, not just a chat acknowledgement");
        PlayCommands.Execute(play,0,"/weather rain",weather:terrainExplorer.WeatherControl);terrainExplorer.TestAnimateWeather=true;
        yield return new WaitForSecondsRealtime(4);Capture("command-rain");
        Check(terrainExplorer.CurrentWeather.RainMmPerHour==4&&terrainExplorer.WeatherParticleCount>500,"command rain produces a dense bounded near-observer shower");
        Check(terrainExplorer.RainRecordingGain>.1f&&terrainExplorer.ActiveWeatherBedVoices>0,"rain actually plays recorded audio sources, not a synthesizer");
        PlayCommands.Execute(play,0,"/weather downpour",weather:terrainExplorer.WeatherControl);yield return new WaitForSecondsRealtime(4);Capture("command-downpour");
        Check(terrainExplorer.CurrentWeather.VisibilityM<1000&&terrainExplorer.WeatherParticleCount>3000,"heavy downpour scales particle density and reduces visibility below1km");
        PlayCommands.Execute(play,0,"/weather fog",weather:terrainExplorer.WeatherControl);yield return new WaitForSecondsRealtime(2);Capture("command-fog");
        Check(terrainExplorer.CurrentWeather.Fog==1&&terrainExplorer.CurrentWeather.VisibilityM<300&&Difference(clear,terrainExplorer.LastCapturePixels)>3,"ground fog actually obscures the view, not just a reported value");
        PlayCommands.Execute(play,0,"/weather storm",weather:terrainExplorer.WeatherControl);yield return new WaitForSecondsRealtime(.3f);Capture("command-storm");
        Check(terrainExplorer.CurrentWeather.Thunderstorm==1,"storm command activates storm weather");
        // Isolate the actual cloud volume: rain haze must not make this test pass.
        float oldVisibility=o.Visibility;o.Visibility=0;o.Particles=0;
        yield return new WaitForSecondsRealtime(2);Capture("storm-volume-no-mist");
        terrainExplorer.TestView(80,170,-85);yield return new WaitForSecondsRealtime(.3f);Capture("storm-overhead-no-mist");
        var texturedRoof=terrainExplorer.LastCapturePixels;double roofMean=texturedRoof.Average(p=>(p.r+p.g+p.b)/3d);
        double roofContrast=Math.Sqrt(texturedRoof.Average(p=>Math.Pow((p.r+p.g+p.b)/3d-roofMean,2)));
        report.Add("Overhead cloud luminance standard deviation "+roofContrast.ToString("F2"));
        Check(roofContrast>1.5,"storm underside retains visible internal shading with haze and particles disabled");
        double textureContrast=0;int textureSamples=0;
        double Luma(int x,int y){var p=texturedRoof[y*1280+x];return(p.r+p.g+p.b)/3d;}
        for(int y=16;y<784;y+=4)for(int x=16;x<1264;x+=4){textureContrast+=Math.Abs(Luma(x,y)-(Luma(x-12,y)+Luma(x+12,y)+Luma(x,y-12)+Luma(x,y+12))*.25);textureSamples++;}
        textureContrast/=textureSamples;report.Add("Overhead cloud local detail contrast "+textureContrast.ToString("F2"));
        Check(textureContrast>.4,"cloud underside has local surface detail, not only a broad smooth gray gradient");
        terrainExplorer.WeatherScreen.DebugDepth=3;Capture("storm-overhead-flat-lighting");
        Check(Difference(texturedRoof,terrainExplorer.LastCapturePixels)>1,"bounded cloud lighting changes the actual cloud interior, not just its silhouette");
        terrainExplorer.WeatherScreen.DebugDepth=2;Capture("storm-overhead-opacity");
        Check(terrainExplorer.LastCapturePixels.Average(p=>(double)p.r)>225,"rain-bearing volume actually closes the overhead sky without rain haze or particles");
        terrainExplorer.WeatherScreen.DebugDepth=0;terrainExplorer.TestView(80,170,-18);
        terrainExplorer.TestWeatherOverride=Fixture(.65);play.SetLocalSolarHour(17.5,-4);yield return new WaitForSecondsRealtime(.3f);Capture("cumulus-evening");
        terrainExplorer.TestWeatherOverride=null;play.SetLocalSolarHour(10,-4);o.Visibility=oldVisibility;o.Particles=1;
        PlayCommands.Execute(play,0,"/weather auto",weather:terrainExplorer.WeatherControl);yield return new WaitForSecondsRealtime(.6f);
        Check(terrainExplorer.WeatherControl.Mode=="auto"&&terrainExplorer.CurrentWeather.RainMmPerHour!=25,"auto command restores the natural provider");
        double rainDate=0,best=0;for(int k=0;k<2920;k++){double d=k*.125,rate=terrainExplorer.TestNaturalWeather(d).RainMmPerHour;if(rate>best){rainDate=d;best=rate;}if(rate>=1&&rate<4){rainDate=d;break;}}
        play.CalendarOffsetSeconds=rainDate*SimulationTime.Day-SimulationTime.Seconds(world.Prehistory)-play.PendingHistorySeconds;
        yield return new WaitForSecondsRealtime(4);Capture("natural-rain");
        Check(terrainExplorer.CurrentWeather.RainMmPerHour>.1&&terrainExplorer.WeatherParticleCount>100,"a naturally dated real-world profile produces visible rain without a test override");
        yield return Until(()=>terrainExplorer.WeatherFieldReady,"spatial cloud field ready");
        Check(terrainExplorer.WeatherScreen.UseField&&terrainExplorer.CurrentWeather.CloudCover>.9,"natural rain and visible clouds share the spatial field");
        var beforeStep=terrainExplorer.CurrentWeather;terrainExplorer.TestWeatherMove(2);yield return new WaitForSecondsRealtime(.2f);Capture("natural-two-metre-step");
        Check(Math.Abs(beforeStep.CloudCover-terrainExplorer.CurrentWeather.CloudCover)<.01&&Math.Abs(beforeStep.PrecipitationMmPerHour-terrainExplorer.CurrentWeather.PrecipitationMmPerHour)<.03,"two-metre movement cannot flip the weather regime");terrainExplorer.TestWeatherMove(-2);
        terrainExplorer.TestCloudMap(true);yield return Until(()=>terrainExplorer.CloudMapReady&&terrainExplorer.MapReady,"natural cloud overlay and cached base map ready");terrainExplorer.CaptureCloudMap(Path.Combine(output,"map-clouds-natural.png"),true);terrainExplorer.CaptureMap(Path.Combine(output,"map-base.png"));int mapBuilds=terrainExplorer.MapBuildCount;
        PlayCommands.Execute(play,0,"/weather clear",weather:terrainExplorer.WeatherControl);yield return new WaitForSecondsRealtime(.3f);yield return Until(()=>terrainExplorer.CloudMapReady,"clear command refreshes optional map overlay");terrainExplorer.CaptureCloudMap(Path.Combine(output,"map-clouds-clear.png"));
        PlayCommands.Execute(play,0,"/weather storm",weather:terrainExplorer.WeatherControl);yield return new WaitForSecondsRealtime(.3f);yield return Until(()=>terrainExplorer.CloudMapReady,"storm command refreshes optional map overlay");terrainExplorer.CaptureCloudMap(Path.Combine(output,"map-clouds-storm.png"));
        Check(terrainExplorer.MapBuildCount==mapBuilds,"cloud options and weather overrides do not rebuild the cached base map");
        terrainExplorer.TestCloudMap(false);terrainExplorer.Pause();PlayCommands.Execute(play,0,"/weather auto",weather:terrainExplorer.WeatherControl);
        // Restore the original solar date and let earlier command-test drops leave the volume
        // before the independent foreground-occlusion comparison below.
        play.CalendarOffsetSeconds=100*SimulationTime.Day-SimulationTime.Seconds(world.Prehistory)-play.PendingHistorySeconds;
        terrainExplorer.TestWeatherOverride=Fixture(0);yield return new WaitForSecondsRealtime(2);
        terrainExplorer.TestWeatherOverride=Fixture(1);yield return new WaitForSecondsRealtime(.2f);Capture("overcast");Check(Difference(clear,terrainExplorer.LastCapturePixels)>1,"overcast changes the actual rendered sky");
        terrainExplorer.TestWeatherOverride=Fixture(0,.9);yield return new WaitForSecondsRealtime(.2f);Capture("cirrus");Check(Difference(clear,terrainExplorer.LastCapturePixels)>.1,"independent high thin layer renders");
        var cirrusPixels=terrainExplorer.LastCapturePixels;double groundChange=0;int groundCount=0;
        for(int y=0;y<320;y++)for(int x=0;x<1280;x++){var a=clear[y*1280+x];if(a.b<a.r*1.15&&a.g<a.r*1.2){var b=cirrusPixels[y*1280+x];groundChange+=(Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b))/3d;groundCount++;}}
        Check(groundCount>1000&&groundChange/groundCount<.5,"high clouds do not paint across opaque foreground terrain");
        terrainExplorer.TestAnimateWeather=true;terrainExplorer.TestWeatherOverride=Fixture(1,0,8);yield return new WaitForSecondsRealtime(4);Capture("rain");Check(terrainExplorer.WeatherParticleCount>50,"rain emits real bounded particles");
        terrainExplorer.TestWeatherOverride=Fixture(1,0,0,3);yield return new WaitForSecondsRealtime(5);Capture("snow");Check(terrainExplorer.WeatherParticleCount>50,"snow emits real bounded particles");
        terrainExplorer.TestWeatherOverride=Fixture(1,0,8,0,1);o.Lightning=.25f;terrainExplorer.TestLightning();yield return new WaitForEndOfFrame();Capture("lightning");Check(terrainExplorer.WeatherScreen.Flash>0,"lightning produces a controlled native flash");
        Check(terrainExplorer.ThunderVariantCount==4&&terrainExplorer.ThunderSignalsValid,"four distinct SA3 mono thunder recordings have finite unclipped samples and silent boundaries");
        Check(terrainExplorer.LastThunderDistanceM>1000&&Math.Abs(terrainExplorer.LastThunderDelaySeconds-terrainExplorer.LastThunderDistanceM/343)<.001&&terrainExplorer.LastThunderLowPassHz<3000,"actual bolt distance controls thunder delay and high-frequency loss");
        int firstVariant=terrainExplorer.LastThunderVariant;terrainExplorer.TestLightning();
        Check(firstVariant!=terrainExplorer.LastThunderVariant,"repeated strike seeds still avoid consecutive identical thunder recordings");
        terrainExplorer.TestLightning(1703u);
        Check(terrainExplorer.PendingThunderCount>=3&&terrainExplorer.PendingThunderCount<=4,"nearby lightning retains bounded overlapping thunder arrivals");
        terrainExplorer.TestAnimateWeather=false;yield return new WaitForSecondsRealtime(terrainExplorer.LastThunderDelaySeconds+1);
        Check(terrainExplorer.PendingThunderCount>=3,"paused exploration does not consume queued thunder arrivals");
        Check(terrainExplorer.ActiveWeatherBedVoices==0,"pausing also pauses every recorded wind and rain voice");
        terrainExplorer.TestAnimateWeather=true;yield return new WaitForSecondsRealtime(terrainExplorer.LastThunderDelaySeconds+1);
        Check(terrainExplorer.PendingThunderCount==0&&terrainExplorer.ActiveThunderCount>0,"queued thunder arrivals actually play after their physical delays");
        Check(terrainExplorer.ActiveWeatherBedVoices>0,"recorded ambience resumes with the explorer");
        Check(terrainExplorer.WeatherBedTransitions>0,"recorded beds transition across overlapping variants rather than hard-looping");
        terrainExplorer.TestWeatherOverride=Fixture(0);o.Adaptation=.8f;play.SetLocalSolarHour(0,-4);yield return new WaitForSecondsRealtime(4);
        play.SetLocalSolarHour(12,-4);yield return new WaitForSecondsRealtime(.15f);Capture("day-dazzle");float dazzle=terrainExplorer.WeatherScreen.Exposure;
        yield return new WaitForSecondsRealtime(3);Capture("day-adapted");Check(dazzle>terrainExplorer.WeatherScreen.Exposure+.1f,"night-to-day teleport produces transient, recovering eye adaptation");o.Adaptation=0;
        var sun=terrainExplorer.SkyFrame.Bodies[0].Direction;terrainExplorer.TestView(80,(float)(Math.Atan2(sun.X,sun.Z)*180/Math.PI),(float)(-Math.Asin(sun.Y)*180/Math.PI));
        o.SunGlare=0;o.SunRays=0;yield return new WaitForSecondsRealtime(.3f);Capture("sun-effects-off");var plainSun=terrainExplorer.LastCapturePixels;
        o.SunGlare=1;o.SunRays=1;yield return new WaitForSecondsRealtime(.3f);Capture("sun-effects-on");Check(Difference(plainSun,terrainExplorer.LastCapturePixels)>.05,"sun glare and rays toggle changes rendered pixels");o.SunGlare=0;o.SunRays=0;
        play.SetLocalSolarHour(10,-4);
        terrainExplorer.TestWeatherOverride=Fixture(.75);terrainExplorer.TestView(5500,170,10);yield return Until(()=>terrainExplorer.AllReady,"above-cloud terrain ready");yield return new WaitForSecondsRealtime(.4f);Capture("above-clouds");
        terrainExplorer.TestView(80,170,-18);yield return Until(()=>terrainExplorer.AllReady,"performance terrain resident");yield return new WaitForSecondsRealtime(1);
        bool oldVSync=displayVSync;int oldFps=displayMaxFps;SetDisplayOptions(false,0,false);
        foreach(int quality in new[]{0,1,3})
        {
            o.Clouds=quality;yield return new WaitForSecondsRealtime(.3f);var frames=new List<double>();double previous=Time.realtimeSinceStartupAsDouble;
            for(int i=0;i<120;i++){yield return null;terrainExplorer.WeatherCheckGpuBarrier();double now=Time.realtimeSinceStartupAsDouble;frames.Add((now-previous)*1000);previous=now;}
            frames.Sort();report.Add("GPU-synchronized frame ms clouds "+quality+": median "+frames[60].ToString("F2")+", p95 "+frames[114].ToString("F2"));
        }
        o.Clouds=1;terrainExplorer.TestWeatherOverride=null;yield return Until(()=>terrainExplorer.WeatherFieldReady,"spatial benchmark field ready");yield return new WaitForSecondsRealtime(.4f);Capture("spatial-distant-clouds");
        var spatialFrames=new List<double>();double previousSpatial=Time.realtimeSinceStartupAsDouble;
        for(int i=0;i<120;i++){yield return null;terrainExplorer.WeatherCheckGpuBarrier();double now=Time.realtimeSinceStartupAsDouble;spatialFrames.Add((now-previousSpatial)*1000);previousSpatial=now;}
        spatialFrames.Sort();report.Add("GPU-synchronized spatial-weather Low frame ms: median "+spatialFrames[60].ToString("F2")+", p95 "+spatialFrames[114].ToString("F2"));
        SetDisplayOptions(oldVSync,oldFps,false);o.Clouds=1;o.SunGlare=.35f;o.SunRays=.12f;o.Adaptation=.5f;
        terrainExplorer.TestWeatherOverride=null;yield return new WaitForSecondsRealtime(.6f);Capture("natural-weather");
        double actualBase=terrainExplorer.CurrentWeather.CloudBaseM;terrainExplorer.TestView(1500,170,-18);yield return new WaitForSecondsRealtime(.4f);
        Check(Math.Abs(actualBase-terrainExplorer.CurrentWeather.CloudBaseM)<.01,"flying vertically does not drag the physical cloud layer upward");
        // Test a genuinely cold stored climate, not an assumed modern-Earth climate.
        // The accepted simulator's65N/25E early-winter input is comparatively mild/mixed.
        terrainExplorer.Teleport(TerrainMapViewport.FromDegrees(-70,50));terrainExplorer.TestView(30,170,-15);
        play.CalendarOffsetSeconds=350*SimulationTime.Day-SimulationTime.Seconds(world.Prehistory)-play.PendingHistorySeconds;play.SetLocalSolarHour(12,50);
        yield return Until(()=>terrainExplorer.AllReady,"natural cold-polar terrain ready");yield return new WaitForSecondsRealtime(.6f);Capture("natural-polar-snow");
        Check(terrainExplorer.WeatherRecent(30).SnowWaterMm>terrainExplorer.WeatherRecent(30).RainMm&&terrainExplorer.WeatherSurface.SnowWaterMm>0,"actual cold-world climate yields snowfall history and retained snow-water data without ground visuals");
        Check(world.Prehistory.Hash==history&&StateHash.Compute(world)==geography,"weather does not mutate terrain or historical records");
        Check(errors.Count==0,"no Unity shader/runtime errors");Application.logMessageReceived-=listener;File.WriteAllLines(Path.Combine(output,"result.txt"),report);Debug.Log("WEATHER CHECK PASS");Application.Quit(0);
    }
}
