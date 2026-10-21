using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    /// <summary>Lazy fixed one-degree planetary weather lattice. Queries interpolate the same
    /// four nodes everywhere: crossing a cell, tile or tangent-chart boundary cannot replace the sky.
    /// No world-sized update. Rain rates AND analytical water windows share these exact weights.</summary>
    public sealed class WorldWeather
    {
        readonly World world;readonly CellLocator locator;readonly object gate=new object();readonly ProceduralWeather flow;
        readonly Dictionary<int,WeatherClimate> profiles=new Dictionary<int,WeatherClimate>();
        readonly Queue<int> eviction=new Queue<int>();const int CacheLimit=50000;
        public WorldWeather(World world,LocalTerrain terrain){this.world=world;locator=terrain.WeatherLocator;flow=new ProceduralWeather(world.Recipe.Seed,new V(1,0,0),new WeatherClimate(new double[4],new double[4],new double[4]));}
        public V WindDisplacement(double days)=>flow.WindDisplacement(days);
        public static V Position(int longitude,int latitude)
        {double a=latitude*D.Pi/180,b=longitude*D.Pi/180;return new V(Math.Cos(a)*Math.Cos(b),Math.Cos(a)*Math.Sin(b),Math.Sin(a));}
        public WeatherClimate Climate(int longitude,int latitude)
        {
            longitude=((longitude%360)+360)%360;latitude=Math.Max(-90,Math.Min(90,latitude));if(Math.Abs(latitude)==90)longitude=0;
            int key=(latitude+90)*360+longitude;lock(gate){if(profiles.TryGetValue(key,out var cached))return cached;}
            V point=Position(longitude,latitude);double support=3*Math.Sqrt(4*D.Pi/world.Surface.Cells.Length),support2=support*support;
            var cells=locator.Within(point,support2);if(cells.Length==0)cells=new[]{locator.Nearest(point)};
            var t=new double[4];var p=new double[4];var h=new double[4];var e=new double[4];var n=new double[4];
            var seasons=new[]{world.Climate.NorthernSpring,world.Climate.NorthernSummer,world.Climate.NorthernAutumn,world.Climate.NorthernWinter};double total=0,ground=0;
            foreach(int id in cells)
            {
                double r=Math.Sqrt(V.Dot(world.Surface.Cells[id].Position-point,world.Surface.Cells[id].Position-point)/support2),x=Math.Max(0,1-r),weight=x*x*x*x*(1+4*r);
                if(cells.Length==1)weight=1;total+=weight;ground+=Math.Max(0,world.ElevationM[id])*weight;
                for(int s=0;s<4;s++){var c=seasons[s];t[s]+=c.TemperatureTenthsC[id]*.1*weight;p[s]+=c.PrecipitationMm[id]/3d*weight;h[s]+=(c.MoisturePermille.Length>id?c.MoisturePermille[id]/1000d:.5)*weight;e[s]+=(c.WindEastMilli.Length>id?c.WindEastMilli[id]/1000d:0)*weight;n[s]+=(c.WindNorthMilli.Length>id?c.WindNorthMilli[id]/1000d:0)*weight;}
            }
            for(int s=0;s<4;s++){t[s]/=total;p[s]/=total;h[s]=D.Clamp(h[s]/total);e[s]/=total;n[s]/=total;}
            var climate=new WeatherClimate(t,p,h,ground/total,e,n);
            lock(gate){if(profiles.TryGetValue(key,out var cached))return cached;while(profiles.Count>=CacheLimit)profiles.Remove(eviction.Dequeue());profiles.Add(key,climate);eviction.Enqueue(key);}return climate;
        }
        public WeatherFrame Node(int longitude,int latitude,double days)
        {var p=Position(longitude,latitude);return new ProceduralWeather(world.Recipe.Seed,p,Climate(longitude,latitude)).At(days,includeFog:false);}
        public Sample AtPosition(V position,double? groundM=null)=>new Sample(this,position,groundM);
        public sealed class Sample
        {
            readonly ProceduralWeather[] nodes=new ProceduralWeather[4];readonly double[] weights=new double[4];readonly double ground,regionalGround;
            internal Sample(WorldWeather owner,V position,double? actualGround)
            {
                double lon=Math.Atan2(position.Y,position.X)*180/D.Pi,lat=Math.Asin(D.Clamp(position.Z,-1,1))*180/D.Pi;
                int x=(int)Math.Floor(lon),y=Math.Min(89,(int)Math.Floor(lat));double u=lon-x,v=lat-y;
                for(int i=0;i<4;i++)
                {
                    int dx=i%2,dy=i/2;weights[i]=(dx==0?1-u:u)*(dy==0?1-v:v);var climate=owner.Climate(x+dx,y+dy);regionalGround+=climate.ElevationM*weights[i];
                    if(actualGround.HasValue){var t=(double[])climate.TemperatureC.Clone();for(int s=0;s<4;s++)t[s]-=(Math.Max(0,actualGround.Value)-climate.ElevationM)*.006;climate=new WeatherClimate(t,climate.MonthlyPrecipitationMm,climate.Humidity,climate.ElevationM,climate.EastWind,climate.NorthWind);}
                    nodes[i]=new ProceduralWeather(owner.world.Recipe.Seed,Position(x+dx,y+dy),climate);
                }
                ground=actualGround??regionalGround;
            }
            public WeatherFrame At(double days,double? localSolarHour=null,bool includeFog=true)
            {
                double t=0,h=0,r=0,s=0,c=0,ci=0,b=0,th=0,cb=0,e=0,n=0,g=0,storm=0,fog=0,chance=0,duration=0,turbulence=0;
                for(int i=0;i<4;i++){var f=nodes[i].At(days,localSolarHour,includeFog);double w=weights[i];t+=f.TemperatureC*w;h+=f.Humidity*w;r+=f.RainMmPerHour*w;s+=f.SnowWaterMmPerHour*w;c+=f.CloudCover*w;ci+=f.CirrusCover*w;b+=f.CloudBaseM*w;th+=f.CloudThicknessM*w;cb+=f.CirrusBaseM*w;e+=f.WindEastMps*w;n+=f.WindNorthMps*w;g+=f.GustMps*w;storm+=f.Thunderstorm*w;fog+=f.Fog*w;chance+=f.WindGustChance*w;duration+=f.WindGustDurationS*w;turbulence+=f.WindTurbulence*w;}
                c=Math.Max(c,ProceduralWeather.RainCover(r+s));fog*=.35+.65*D.Clamp((regionalGround-ground+180)/360);
                return new WeatherFrame(t,h,r,s,c,ci,b,th,cb,e,n,g,storm,fog,chance,duration,turbulence);
            }
            public WeatherWater Between(double start,double end){WeatherWater value=default;for(int i=0;i<4;i++)value+=nodes[i].Between(start,end)*weights[i];return value;}
            public WeatherSurfaceEstimate SurfaceAt(double days)
            {double snow=0,liquid=0,wet=0,runoff=0;for(int i=0;i<4;i++){var f=nodes[i].SurfaceAt(days);snow+=f.SnowWaterMm*weights[i];liquid+=f.RecentLiquidMm*weights[i];wet+=f.SoilWetness*weights[i];runoff+=f.RunoffPotentialMm*weights[i];}return new WeatherSurfaceEstimate(snow,liquid,wet,runoff);}
        }
    }
}
