using WorldGen.Core;
using WorldGen.Serialization;
static class ForagerClimateTests
{
    public static void Run(string file)
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var d=new ForagerDistrict{Center=new V(.94,0,.342),AnnualRainMm=0};
        var e=new ForagerEnvironment(0,10,30,20,0,.8,500,.4,.6,.1,0,1,new double[5],new double[5],1,600);
        var dry=new WeatherClimate(new[]{20d,20,20,20},new double[4],new[]{.2,.2,.2,.2});
        var wet=ForagerClimate.Apply(dry,d,e,-4);
        Check(Math.Abs(wet.MonthlyPrecipitationMm.Sum()*3-600)<1e-10&&wet.TemperatureC.All(t=>t==16),"An absolute rain addition is conserved across four seasonal inputs and does not multiply a dry zero forever");
        Check(wet.MonthlyPrecipitationMm[1]>wet.MonthlyPrecipitationMm[3]*3,"Low-latitude northern monsoon additions peak in northern summer");
        var south=d.Copy();south.Center=new V(.94,0,-.342);var southern=ForagerClimate.Apply(dry,south,e,-4);
        Check(southern.MonthlyPrecipitationMm[3]>southern.MonthlyPrecipitationMm[1]*3,"Southern rainy seasons are not six months out of phase");
        var weather=new ProceduralWeather(42,d.Center.Unit,wet);var annual=weather.Between(0,365);var halves=weather.Between(0,180)+weather.Between(180,365);
        Check(annual.TotalMm>100&&Math.Abs(annual.TotalMm-halves.TotalMm)<1e-7,"New rainfall appears in analytical water windows with additive conservation");
        var frozen=ForagerClimate.Apply(dry,d,e,-35);var snow=new ProceduralWeather(42,d.Center.Unit,frozen).Between(0,365);
        Check(snow.SnowWaterMm>100&&snow.RainMm<1e-8,"The same new precipitation becomes snow when the historical temperature demands it");
        var world=WorldFile.ReadFile(file);var original=world.Prehistory;var engine=new Prehistory(world);var terrain=new LocalTerrain(world);var ww=new WorldWeather(world,terrain);
        int district=original.Foragers.Districts.Where(x=>x.Center.Z>.35).OrderByDescending(x=>x.Center.Z).First().Id;var at=original.Foragers.Districts[district].Center;
        var provider=ww.AtPosition(at);var before=provider.At(140);var beforeWater=provider.Between(100,140);
        var future=original.Copy();future.Foragers.Seconds=179000*SimulationTime.Year;world.Prehistory=future;
        var after=provider.At(140);var fresh=ww.AtPosition(at).At(140);var futureWater=provider.Between(100,140);var freshWater=ww.AtPosition(at).Between(100,140);
        Check(Math.Abs(after.TemperatureC-fresh.TemperatureC)<1e-10&&Math.Abs(after.PrecipitationMmPerHour-fresh.PrecipitationMmPerHour)<1e-10,"A stationary weather provider refreshes after a historical date change just like a newly visiting player");
        Check(Math.Abs(after.TemperatureC-before.TemperatureC)>.2,"Glacial climate reaches actual lattice weather, not only demographic calculations");
        Check(Math.Abs(futureWater.TotalMm-freshWater.TotalMm)<1e-9,"Accumulated water and current weather share the same refreshed historical inputs");
        Check(original.Hash==original.ComputeHash(),"Weather observation cannot rewrite the simulation");world.Prehistory=original;
        Console.WriteLine($"PASS {checks} historical climate/weather/water checks; frozen delta={after.TemperatureC-before.TemperatureC:0.00} C; rain={annual.TotalMm:0.0} mm");
    }
}
