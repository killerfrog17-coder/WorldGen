using System;
namespace WorldGen.Core
{
    public static class ForagerClimate
    {
        // Transform the weather generator's seasonal inputs, not just a rendered
        // rain rate. Analytical water windows, snow and visible weather then
        // share the same changed climate, including rain in formerly dry cells.
        public static WeatherClimate Apply(WeatherClimate original,ForagerDistrict district,ForagerEnvironment environment,double temperatureDelta)
        {
            var temperature=(double[])original.TemperatureC.Clone();var rain=new double[4];var humidity=new double[4];
            double added=Math.Max(0,environment.RainMm-district.AnnualRainMm*environment.Moisture);
            double latitude=Math.Abs(Math.Asin(D.Clamp(district.Center.Z,-1,1))*180/Math.PI);
            double winterShare=D.Clamp((latitude-23)/15d)*.7;
            // Extra summer-monsoon and winter-storm rain have separate seasonal
            // concentration. Arrays use northern seasons even south of the equator.
            int summer=district.Center.Z<0?3:1,winter=(summer+2)%4;
            for(int s=0;s<4;s++)
            {
                temperature[s]+=temperatureDelta;
                double seasonal=.08+(s==summer?.68*(1-winterShare):0)+(s==winter?.68*winterShare:0);
                rain[s]=Math.Max(0,original.MonthlyPrecipitationMm[s]*environment.Moisture+added*seasonal/3d);
                humidity[s]=D.Clamp(original.Humidity[s]+.12*Math.Tanh((rain[s]-original.MonthlyPrecipitationMm[s])/60d));
            }
            return new WeatherClimate(temperature,rain,humidity,original.ElevationM,original.EastWind,original.NorthWind);
        }
    }
}
