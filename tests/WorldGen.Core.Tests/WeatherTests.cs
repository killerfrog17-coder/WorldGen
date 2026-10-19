using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class WeatherTests
{
    public static void World(Action<bool,string> check)
    {
        var recipe=Recipe.Preset("Earth");recipe.Detail=20000;var world=new WorldPipeline().Generate(recipe);string original=StateHash.Compute(world);var locator=new CellLocator(world.Surface.Cells);
        foreach(var site in new[]{(65d,25d),(75d,40d),(-70d,50d),(30d,15d),(45d,-4d),(-3d,-60d)})
        {
            var point=WorldGen.Presentation.TerrainMapViewport.FromDegrees(site.Item1,site.Item2);int id=locator.Nearest(point);var climate=WeatherClimate.FromWorld(world,id,point,Math.Max(0,world.ElevationM[id]));var weather=new ProceduralWeather(world.Recipe.Seed,point,climate);var month=weather.Between(320,350);var surface=weather.SurfaceAt(350);
            Console.WriteLine("Weather site "+site+" seasonal T "+string.Join(",",climate.TemperatureC.Select(x=>x.ToString("F1")))+" seasonal monthly P "+string.Join(",",climate.MonthlyPrecipitationMm.Select(x=>x.ToString("F1")))+" day350 T "+weather.At(350).TemperatureC+" rain/snow "+month.RainMm+"/"+month.SnowWaterMm+" SWE "+surface.SnowWaterMm);
            int rainy=0,overcast=0;for(int k=0;k<2920;k++){var f=weather.At(k*.125);if(f.RainMmPerHour>.1)rainy++;if(f.CloudCover>.85)overcast++;}
            Console.WriteLine($"  annual rainy samples {rainy/29.2:F1}%, overcast {overcast/29.2:F1}% (simulated climate, not observed)");
            check(month.TotalMm>=0&&double.IsFinite(surface.SnowWaterMm),"Real-world climate profiles yield finite bounded water estimates");
        }
        check(StateHash.Compute(world)==original,"Actual-world weather queries leave all authoritative stage hashes unchanged");
        var terrain=new LocalTerrain(world);var view=new TerrainMapViewport();var sw=Stopwatch.StartNew();
        var map=WeatherMapImage.Build(world,terrain,view,120,width:64,height:32);sw.Stop();
        Console.WriteLine("Cloud overlay64x32 "+sw.Elapsed.TotalMilliseconds.ToString("F1")+"ms");
        check(map.Rgba.Length==64*32*4&&Enumerable.Range(0,64*32).Select(i=>map.Rgba[i*4+3]).Distinct().Count()>30,"Optional cloud map shows spatially varied natural weather");
        var clear=WeatherMapImage.Build(world,terrain,view,120,"clear",8,4);var storm=WeatherMapImage.Build(world,terrain,view,120,"storm",8,4);
        check(Enumerable.Range(0,32).All(i=>clear.Rgba[i*4+3]==0&&storm.Rgba[i*4+3]>200),"Map honors the same clear/storm test controls as the explorer");
        check(map.Rgba.SequenceEqual(WeatherMapImage.Build(world,terrain,view,120,width:64,height:32).Rgba),"Cloud map repeat queries are exact");
        using var cancel=new CancellationTokenSource();cancel.Cancel();bool cancelled=false;try{WeatherMapImage.Build(world,terrain,view,120,token:cancel.Token);}catch(OperationCanceledException){cancelled=true;}
        check(cancelled&&StateHash.Compute(world)==original,"Cloud overlay cancels without altering any world stage");
        SpatialWeatherTests.Run(world,terrain,check);
    }
    public static void Native(string path,Action<bool,string> check)
    {
        var climate=new WeatherClimate(new[]{-12d,-8,-15,-20},new[]{80d,110,90,60},new[]{.6,.7,.6,.5});var weather=new ProceduralWeather(817,new V(.7,.4,.5).Unit,climate);
        foreach(var line in File.ReadAllLines(Path.Combine(path,"weather-numeric.csv")))
        {
            var values=line.Split(',').Select(v=>double.Parse(v,System.Globalization.CultureInfo.InvariantCulture)).ToArray();double date=values[0];var f=weather.At(date);var surface=weather.SurfaceAt(date);
            var expected=new[]{f.TemperatureC,f.RainMmPerHour,f.SnowWaterMmPerHour,weather.Between(date-1,date).TotalMm,weather.Between(date-30,date).TotalMm,surface.SnowWaterMm};
            for(int i=0;i<expected.Length;i++)check(Math.Abs(expected[i]-values[i+1])<1e-7,"Native and CLR weather agree at arbitrary dates, including ancient histories");
        }
    }
    public static void Run(Action<bool,string> check)
    {
        var originalRain=new RainSynth{MillimetresPerHour=12};var rainSamples=new float[44100];originalRain.Read(rainSamples);
        var rainRepeat=new float[44100];new RainSynth{MillimetresPerHour=12}.Read(rainRepeat);
        check(rainSamples.SequenceEqual(rainRepeat)&&rainSamples.All(x=>float.IsFinite(x)&&Math.Abs(x)<=.9f),"Restored original rain is deterministic finite and bounded");
        var silentRain=new float[44100];new RainSynth().Read(silentRain);
        check(silentRain.All(x=>x==0)&&rainSamples.Any(x=>Math.Abs(x)>.01),"Original rain is silent when dry and produces real wet samples");
        var wet=new WeatherClimate(new[]{25d,27,24,22},new[]{200d,350,180,120},new[]{.8,.9,.7,.7});
        var cold=new WeatherClimate(new[]{-12d,-8,-15,-20},new[]{80d,110,90,60},new[]{.6,.7,.6,.5});
        var desert=new WeatherClimate(new[]{25d,35,28,18},new[]{1d,0,1,2},new[]{.08,.05,.1,.1});
        var p=new V(.7,.4,.5).Unit;var weather=new ProceduralWeather(817,p,wet);var snow=new ProceduralWeather(817,p,cold);var dry=new ProceduralWeather(817,p,desert);
        var controls=new WeatherControl();var play=new PlaySession();string saved=WorldJson.Write(play);var natural=weather.At(120);
        foreach(string mode in WeatherControl.Modes)
        {
            check(PlayCommands.Execute(play,0,"/weather "+mode,weather:controls).Success&&controls.Mode==mode,"Weather command accepts "+mode);
            var f=controls.Apply(natural);check(f.CloudCover>=0&&f.CloudCover<=1&&f.PrecipitationMmPerHour>=0,"Weather test frame is bounded: "+mode);
            if(mode=="rain"||mode=="storm")check(f.RainMmPerHour>=4&&f.SnowWaterMmPerHour==0&&f.CloudCover==1,"Wet testing produces actual rain and full cover");
            if(mode=="snow"||mode=="blizzard")check(f.SnowWaterMmPerHour>0&&f.RainMmPerHour==0&&f.TemperatureC<0,"Snow testing is cold, not warm rain");
        }
        int revision=controls.Revision;check(!PlayCommands.Execute(play,0,"/weather nonsense",weather:controls).Success&&revision==controls.Revision,"Invalid weather command leaves controls unchanged");
        controls.Set("rain");check(controls.Apply(natural,6000).CloudBaseM>6000,"Test rain remains overhead even on high mountains");
        check(PlayCommands.Execute(play,0,"/weather auto",weather:controls).Success&&controls.Apply(natural).RainMmPerHour==natural.RainMmPerHour&&WorldJson.Write(play)==saved,"Auto restores exact natural weather without modifying the saved calendar");
        check(PlayCommands.Complete("/weather s").SequenceEqual(new[]{"/weather snow","/weather storm"}),"Weather autocomplete lists the implemented modes");
        var calmAudio=new WeatherAudioMix(0,0,0,false);var breezeAudio=new WeatherAudioMix(4,6,0,false);var galeAudio=new WeatherAudioMix(35,40,0,false);
        check(calmAudio.WindStrength==0,"Still air plays no recorded wind");
        check(breezeAudio.WindStrength>0&&breezeAudio.WindBand==0,"Light breeze selects a gentle gust, not simultaneous wind layers");
        check(galeAudio.WindStrength>.9&&galeAudio.WindBand==2,"Gales select one gale variant");
        check(breezeAudio.WindGapMinSeconds>15&&breezeAudio.WindGapMaxSeconds>30,"Gentle gusts leave substantial genuinely quiet intervals");
        foreach(double speed in Enumerable.Range(0,501).Select(i=>i*.1))
        {
            var a=new WeatherAudioMix(speed,speed,4,false);var b=new WeatherAudioMix(speed+.0001,speed+.0001,4,false);
            check(Math.Abs(a.WindStrength-b.WindStrength)<.01,"Gust intensity changes continuously with wind strength");
        }
        check(!new WeatherAudioMix(8,15,10,false,.2,1).RainAllowed&&!new WeatherAudioMix(8,15,10,false,.2,1).ThunderAllowed,"Clear local sky cannot play rain or thunder even if supplied contradictory rain data");
        check(!new WeatherAudioMix(8,15,.01,false,1,1).RainAllowed,"Negligible precipitation cannot produce an audible continuous rain bed");
        check(new WeatherAudioMix(8,15,25,false,1,1).ThunderAllowed&&!new WeatherAudioMix(8,15,25,true,1,1).ThunderAllowed,"Thunder requires local storm rain below its cloud layer");
        foreach(double date in new[]{0d,.001,31.999,32,120.125,200000*365d+.2})
        {
            var now=weather.At(date);check(now.RainMmPerHour>=0&&now.SnowWaterMmPerHour==0&&now.CloudCover>=0&&now.CloudCover<=1&&now.CloudBaseM>0,"Warm weather has nonnegative rain, no snow and valid clouds");
            var total=weather.Between(date-30,date);var split=weather.Between(date-30,date-7)+weather.Between(date-7,date);
            check(Math.Abs(total.TotalMm-split.TotalMm)<1e-6,"Arbitrary-date monthly totals are additive across interval boundaries");
            check(weather.Between(date,date).TotalMm==0,"Empty interval has no incoming water");
            check(snow.Between(date-30,date).RainMm==0&&snow.Between(date-30,date).SnowWaterMm>0,"Cold precipitation water is retained in the snow channel");
        }
        double integral=0,step=1d/1024,start=103.13;for(int i=0;i<1024;i++)integral+=weather.At(start+(i+.5)*step).PrecipitationMmPerHour*step*24;
        check(Math.Abs(integral-weather.Between(start,start+1).TotalMm)<.0001,"Visible instantaneous precipitation integrates to the same24-hour water total");
        double wetAnnual=weather.Between(1,366).TotalMm,dryAnnual=dry.Between(1,366).TotalMm;check(wetAnnual>dryAnnual*50&&dryAnnual<20,"The shared planetary pattern yields very different wet/desert water budgets");
        foreach(var sample in new[]{("wet",weather),("desert",dry)})
        {
            int wetHours=0,overcast=0;double peak=0;
            for(int k=0;k<2920;k++){var f=sample.Item2.At(k*.125);if(f.PrecipitationMmPerHour>.1)wetHours++;if(f.CloudCover>.85)overcast++;peak=Math.Max(peak,f.PrecipitationMmPerHour);}
            Console.WriteLine($"Weather annual {sample.Item1}: rain >0.1mm/h {wetHours/29.2:F1}%, overcast {overcast/29.2:F1}%, peak {peak:F2}mm/h");
            check(sample.Item1=="wet"?overcast>wetHours*2:overcast<400,"Extended dry overcast is possible in wet climates without making deserts permanently cloudy");
        }
        var first=snow.SurfaceAt(200000*365d+150);weather.At(900);weather.Between(-20,-1);var other=new ProceduralWeather(817,p,cold).SurfaceAt(200000*365d+150);
        check(first.SnowWaterMm>0&&first.SnowWaterMm==other.SnowWaterMm&&first.SoilWetness==other.SoilWetness,"Surface estimates at arbitrary dates are independent of prior visits or simulation ticks");
        check(weather.SurfaceAt(90).SnowWaterMm==0,"A warm climate cannot acquire unrelated decorative snow");
        var near=new ProceduralWeather(817,(p+new V(.000000001,0,0)).Unit,wet);check(Math.Abs(near.Between(80,100).TotalMm-weather.Between(80,100).TotalMm)<.001,"Nearby positions share weather rather than using independent cell RNG");
        foreach(double date in new[]{-100000d,-32.000001,-.000001,0,31.999999,32,640.71,73000000.51})
        {
            double half=.017;var total=weather.Between(date-half,date+half);var left=weather.Between(date-half,date)+weather.Between(date,date+half);
            check(Math.Abs(total.TotalMm-left.TotalMm)<1e-6,"Short windows remain additive across negative dates and root boundaries");
            var wind=weather.WindDisplacement(date+1d/86400)-weather.WindDisplacement(date);var now=weather.At(date);
            check(Math.Abs(wind.X-now.WindEastMps)<.01&&Math.Abs(wind.Y-now.WindNorthMps)<.01,"Cloud displacement differentiates to the same wind even at ancient dates");
        }
        bool rejected=false;try{weather.Between(0,367);}catch(ArgumentOutOfRangeException){rejected=true;}check(rejected,"Unbounded retrospective queries are explicitly rejected rather than replayed");
        var sw=Stopwatch.StartNew();double sink=0;for(int i=0;i<10000;i++)sink+=weather.At(i*.11).RainMmPerHour;sw.Stop();Console.WriteLine("Weather current mean "+(sw.Elapsed.TotalMilliseconds/10000).ToString("F4")+" ms; checksum "+sink.ToString("F3"));
        sw.Restart();for(int i=0;i<200;i++)sink+=weather.SurfaceAt(i*1.37).SnowWaterMm;sw.Stop();Console.WriteLine("Weather surface mean "+(sw.Elapsed.TotalMilliseconds/200).ToString("F4")+" ms");
        check(sw.Elapsed.TotalMilliseconds/200<10,"Bounded surface-history query stays below10ms on this host; no world-size loop");
    }
}
