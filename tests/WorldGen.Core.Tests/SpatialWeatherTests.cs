using WorldGen.Core;
using WorldGen.Presentation;

static class SpatialWeatherTests
{
    public static void Run(World world,LocalTerrain terrain,Action<bool,string> check)
    {
        var weather=new WorldWeather(world,terrain);const double date=120.14;
        foreach(var site in new[]{(35d,-4d),(0d,180d),(60d,10d),(-35d,20d),(89.999d,0d)})
        {
            var a=weather.AtPosition(TerrainMapViewport.FromDegrees(site.Item1,site.Item2-.000001)).At(date,includeFog:false);
            var b=weather.AtPosition(TerrainMapViewport.FromDegrees(site.Item1,site.Item2+.000001)).At(date,includeFog:false);
            check(Math.Abs(a.CloudCover-b.CloudCover)<.001&&Math.Abs(a.PrecipitationMmPerHour-b.PrecipitationMmPerHour)<.001,"Fixed global weather nodes remain continuous across weather-grid boundaries, meridian and poles");
        }
        for(int k=0;k<20;k++)
        {
            int id=k*world.Surface.Cells.Length/20,j=world.Surface.Cells[id].Neighbors[0];var center=(world.Surface.Cells[id].Position+world.Surface.Cells[j].Position).Unit;
            var delta=(world.Surface.Cells[j].Position-world.Surface.Cells[id].Position).Unit*.00000001;
            var a=weather.AtPosition((center-delta).Unit).At(date,includeFog:false);var b=weather.AtPosition((center+delta).Unit).At(date,includeFog:false);
            check(Math.Abs(a.CloudCover-b.CloudCover)<.001&&Math.Abs(a.PrecipitationMmPerHour-b.PrecipitationMmPerHour)<.001,"Crossing a source-cell edge cannot switch the sky");
        }
        var point=TerrainMapViewport.FromDegrees(45.2,-4.4);var sample=weather.AtPosition(point);var high=weather.AtPosition(point,6000);
        check(Math.Abs(sample.At(date,includeFog:false).PrecipitationMmPerHour-high.At(date,includeFog:false).PrecipitationMmPerHour)<1e-8,"Altitude repartitions rain/snow without changing the storm or total water");
        check(Math.Abs(sample.At(date,includeFog:false).CloudBaseM-high.At(date,includeFog:false).CloudBaseM)<1e-8,"The cloud layer is spatially anchored, not raised when the viewer climbs");
        double integral=0;for(int i=0;i<1024;i++)integral+=sample.At(date+(i+.5)/1024,includeFog:false).PrecipitationMmPerHour*24/1024;
        check(Math.Abs(integral-sample.Between(date,date+1).TotalMm)<.001,"Interpolated local weather and analytical water history use the same four nodes and weights");
        var watch=System.Diagnostics.Stopwatch.StartNew();var tile=WeatherFieldTile.Build(weather,point,date,terrain.RadiusM*.1);watch.Stop();Console.WriteLine($"Spatial cloud tile {tile.Width}x{tile.Height}: {watch.Elapsed.TotalMilliseconds:F1}ms");
        check(tile.Contains(45.2,-4.4)&&tile.MinBase>0&&tile.MaxTop>tile.MinBase&&tile.Width*tile.Height<15000,"Regional renderer cache is bounded and covers the viewer");
        double x=-4.4-tile.Longitude,y=45.2-tile.Latitude;int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y);double wet=0,cover=0;
        for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++){int i=((iy+dy)*tile.Width+ix+dx)*4;double w=(dx==0?1-(x-ix):x-ix)*(dy==0?1-(y-iy):y-iy);wet+=tile.Conditions[i+2]*w;cover+=tile.Conditions[i]*w;}
        var expected=sample.At(date,includeFog:false);cover=Math.Max(cover,ProceduralWeather.RainCover(wet));
        check(Math.Abs(wet-expected.PrecipitationMmPerHour)<1e-5&&Math.Abs(cover-expected.CloudCover)<1e-5,"GPU bilinear weather fields agree with the local shared Core query, including rain-forced cover");
        check(WeatherFieldTile.Build(weather,TerrainMapViewport.FromDegrees(89.5,179),date,terrain.RadiusM*.1).Contains(89.5,-179),"Polar weather window wraps all longitudes without perpetual rebuilding");
        var cold=new WeatherClimate(new[]{10d,10,10,10},new[]{100d,100,100,100},new[]{.95,.95,.95,.95});var fogProvider=new ProceduralWeather(812,point,cold);
        double maxFog=0,dayFog=0;
        for(int i=0;i<180;i++){maxFog=Math.Max(maxFog,fogProvider.At(i,5).Fog);dayFog=Math.Max(dayFog,fogProvider.At(i,14).Fog);}
        check(maxFog>.05&&dayFog==0,"Natural moist calm dawns can form fog that dissipates after morning");
        var controls=new WeatherControl();controls.Set("clear");var clear=controls.Apply(expected);controls.Set("downpour");var downpour=controls.Apply(expected);controls.Set("fog");var fog=controls.Apply(expected);
        check(downpour.RainMmPerHour>=30&&downpour.VisibilityM<1500&&downpour.CloudCover>.99&&downpour.CloudDarkness>.79,"Heavy rain has dense dark cloud and substantially reduced visibility");
        controls.Set("rain");var moderate=controls.Apply(expected);controls.Set("blizzard");var blizzard=controls.Apply(expected);
        check(moderate.VisibilityM<900&&downpour.VisibilityM<110&&blizzard.VisibilityM<300,"Moderate rain already strongly reduces visibility; downpour and blizzard obscure distant terrain");
        check(fog.VisibilityM<300&&fog.Fog==1&&clear.VisibilityM>50000,"Fog and clear modes have genuinely distinct visibility, not just labels");
        var calm=new RainSynth();var sound=new float[22050*2];calm.Read(sound);check(sound.All(x=>x==0),"No rain means no looping rain audio");
        var light=new RainSynth(){MillimetresPerHour=.3f};var heavy=new RainSynth(){MillimetresPerHour=40};var quiet=new float[22050*2*4];var loud=new float[quiet.Length];light.Read(quiet);heavy.Read(loud);
        check(loud.Sum(x=>(double)x*x)>quiet.Sum(x=>(double)x*x)*50&&loud.All(x=>float.IsFinite(x)&&Math.Abs(x)<.9),"Downpour sound is substantially stronger than drizzle without clipping");
        var stream=new RainSynth(){MillimetresPerHour=40};bool exact=true;for(int k=0;k<4;k++){stream.Read(sound);for(int i=0;i<sound.Length;i++)exact&=sound[i]==loud[k*sound.Length+i];}
        check(exact,"Rain audio has no buffer or recording-loop seams");
        var drought=new ProceduralWeather(8,point,new WeatherClimate(new[]{35d,35,35,35},new double[4],new double[4]));
        check(Enumerable.Range(0,100).All(i=>{var f=drought.At(i*3.65);return f.CloudCover==0&&f.CirrusCover==0&&f.PrecipitationMmPerHour==0&&f.Fog==0;}),"A moisture-free world cannot generate decorative wet clouds, fog or rain");
        var sites=new List<(Koppen Kind,double Annual,double Rainy,double Clouds)>();
        foreach(var kind in new[]{Koppen.BWh,Koppen.Af})
        {
            var candidates=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>world.Climate.Classification[i]==kind&&world.Surface.Cells[i].Neighbors.All(j=>world.Climate.Classification[j]==kind)).OrderBy(i=>world.Climate.NorthernSpring.PrecipitationMm[i]+world.Climate.NorthernSummer.PrecipitationMm[i]+world.Climate.NorthernAutumn.PrecipitationMm[i]+world.Climate.NorthernWinter.PrecipitationMm[i]).ToArray();
            if(candidates.Length==0)continue;int id=candidates[candidates.Length/2];var profile=weather.AtPosition(world.Surface.Cells[id].Position);int rainy=0,cloudy=0;
            for(int k=0;k<2920;k++){var f=profile.At(k*.125,includeFog:false);if(f.PrecipitationMmPerHour>.1)rainy++;if(f.CloudCover>.8)cloudy++;check(f.PrecipitationMmPerHour<.2||f.CloudCover>.995,"Rain-producing columns have dense cloud cover");}
            sites.Add((kind,profile.Between(0,365).TotalMm,rainy/29.2,cloudy/29.2));
        }
        foreach(var site in sites)Console.WriteLine($"Actual {site.Kind} biome: annual {site.Annual:F0}mm; wet {site.Rainy:F1}%; overcast {site.Clouds:F1}%");
        if(sites.Count==2)check(sites[1].Annual>sites[0].Annual*2&&sites[1].Rainy>sites[0].Rainy*2&&sites[1].Clouds>sites[0].Clouds,"Actual rainforest and desert climates produce different weather frequencies and water budgets");
    }
}
