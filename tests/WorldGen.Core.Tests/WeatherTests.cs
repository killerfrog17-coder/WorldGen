using System.Diagnostics;
using WorldGen.Core;

static class WeatherTests
{
    public static void World(Action<bool,string> check)
    {
        var recipe=Recipe.Preset("Earth");recipe.Detail=20000;var world=new WorldPipeline().Generate(recipe);string original=StateHash.Compute(world);var locator=new CellLocator(world.Surface.Cells);
        foreach(var site in new[]{(65d,25d),(75d,40d),(-70d,50d),(30d,15d)})
        {
            var point=WorldGen.Presentation.TerrainMapViewport.FromDegrees(site.Item1,site.Item2);int id=locator.Nearest(point);var climate=WeatherClimate.FromWorld(world,id,point,Math.Max(0,world.ElevationM[id]));var weather=new ProceduralWeather(world.Recipe.Seed,point,climate);var month=weather.Between(320,350);var surface=weather.SurfaceAt(350);
            Console.WriteLine("Weather site "+site+" seasonal T "+string.Join(",",climate.TemperatureC.Select(x=>x.ToString("F1")))+" seasonal monthly P "+string.Join(",",climate.MonthlyPrecipitationMm.Select(x=>x.ToString("F1")))+" day350 T "+weather.At(350).TemperatureC+" rain/snow "+month.RainMm+"/"+month.SnowWaterMm+" SWE "+surface.SnowWaterMm);
            check(month.TotalMm>=0&&double.IsFinite(surface.SnowWaterMm),"Real-world climate profiles yield finite bounded water estimates");
        }
        check(StateHash.Compute(world)==original,"Actual-world weather queries leave all authoritative stage hashes unchanged");
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
        var wet=new WeatherClimate(new[]{25d,27,24,22},new[]{200d,350,180,120},new[]{.8,.9,.7,.7});
        var cold=new WeatherClimate(new[]{-12d,-8,-15,-20},new[]{80d,110,90,60},new[]{.6,.7,.6,.5});
        var desert=new WeatherClimate(new[]{25d,35,28,18},new[]{1d,0,1,2},new[]{.08,.05,.1,.1});
        var p=new V(.7,.4,.5).Unit;var weather=new ProceduralWeather(817,p,wet);var snow=new ProceduralWeather(817,p,cold);var dry=new ProceduralWeather(817,p,desert);
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
