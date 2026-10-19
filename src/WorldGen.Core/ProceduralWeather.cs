using System;

namespace WorldGen.Core
{
        /// <summary>Copied local climate inputs. Seasons are northern spring, summer, autumn, winter.</summary>
    public sealed class WeatherClimate
    {
        public readonly double[] TemperatureC,MonthlyPrecipitationMm,Humidity,EastWind,NorthWind;
        public readonly double ElevationM;
        public WeatherClimate(double[] temperatureC,double[] monthlyMm,double[] humidity,double elevationM=0,double[] east=null,double[] north=null)
        {
            if(temperatureC==null||monthlyMm==null||humidity==null||temperatureC.Length!=4||monthlyMm.Length!=4||humidity.Length!=4)throw new ArgumentException("Four seasonal weather inputs required.");
            TemperatureC=(double[])temperatureC.Clone();MonthlyPrecipitationMm=(double[])monthlyMm.Clone();Humidity=(double[])humidity.Clone();ElevationM=elevationM;
            EastWind=east==null?new double[4]:(double[])east.Clone();NorthWind=north==null?new double[4]:(double[])north.Clone();
            if(EastWind.Length!=4||NorthWind.Length!=4)throw new ArgumentException("Four wind seasons required.");
            for(int k=0;k<4;k++)if(!Finite(TemperatureC[k])||!Finite(MonthlyPrecipitationMm[k])||MonthlyPrecipitationMm[k]<0||!Finite(Humidity[k])||Humidity[k]<0||Humidity[k]>1||!Finite(EastWind[k])||!Finite(NorthWind[k]))throw new ArgumentException("Invalid weather climate.");
            if(!Finite(elevationM))throw new ArgumentException("Invalid weather elevation.");
        }
        static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        public double At(double[] values,double days)
        {
            double q=(days/365-.218)*4-.5;q-=Math.Floor(q/4)*4;int a=(int)q,b=(a+1)%4;return values[a]+(values[b]-values[a])*(q-a);
        }
        public static WeatherClimate FromWorld(World world,int cell,V position,double elevationM)
        {
            var t=new double[4];var p=new double[4];var h=new double[4];var e=new double[4];var n=new double[4];
            var seasons=new[]{world.Climate.NorthernSpring,world.Climate.NorthernSummer,world.Climate.NorthernAutumn,world.Climate.NorthernWinter};
            var neighbors=world.Surface.Cells[cell].Neighbors;double total=0,baseElevation=0;
            for(int k=-1;k<neighbors.Length;k++)
            {
                int id=k<0?cell:neighbors[k];var delta=world.Surface.Cells[id].Position-position;double weight=1/Math.Max(1e-10,V.Dot(delta,delta));total+=weight;baseElevation+=Math.Max(0,world.ElevationM[id])*weight;
                for(int s=0;s<4;s++){var c=seasons[s];t[s]+=c.TemperatureTenthsC[id]*.1*weight;p[s]+=c.PrecipitationMm[id]/3d*weight;h[s]+=(c.MoisturePermille.Length>id?c.MoisturePermille[id]/1000d:.5)*weight;e[s]+=(c.WindEastMilli.Length>id?c.WindEastMilli[id]/1000d:0)*weight;n[s]+=(c.WindNorthMilli.Length>id?c.WindNorthMilli[id]/1000d:0)*weight;}
            }
            for(int s=0;s<4;s++){t[s]=t[s]/total-(Math.Max(0,elevationM)-baseElevation/total)*.006;p[s]/=total;h[s]=D.Clamp(h[s]/total);e[s]/=total;n[s]/=total;}
            return new WeatherClimate(t,p,h,elevationM,e,n);
        }
    }
    public readonly struct WeatherWater
    {
        public readonly double RainMm,SnowWaterMm;
        public double TotalMm=>RainMm+SnowWaterMm;
        public WeatherWater(double rain,double snow){RainMm=rain;SnowWaterMm=snow;}
        public static WeatherWater operator +(WeatherWater a,WeatherWater b)=>new WeatherWater(a.RainMm+b.RainMm,a.SnowWaterMm+b.SnowWaterMm);
        public static WeatherWater operator -(WeatherWater a,WeatherWater b)=>new WeatherWater(a.RainMm-b.RainMm,a.SnowWaterMm-b.SnowWaterMm);
        public static WeatherWater operator *(WeatherWater a,double b)=>new WeatherWater(a.RainMm*b,a.SnowWaterMm*b);
    }
    public readonly struct WeatherFrame
    {
        public readonly double TemperatureC,Humidity,RainMmPerHour,SnowWaterMmPerHour,CloudCover,CirrusCover,CloudBaseM,CloudThicknessM,CirrusBaseM,WindEastMps,WindNorthMps,GustMps,Thunderstorm;
        public double PrecipitationMmPerHour=>RainMmPerHour+SnowWaterMmPerHour;
        public WeatherFrame(double temperature,double humidity,double rain,double snow,double clouds,double cirrus,double cloudBase,double thickness,double cirrusBase,double east,double north,double gust,double storm)
        {TemperatureC=temperature;Humidity=humidity;RainMmPerHour=rain;SnowWaterMmPerHour=snow;CloudCover=clouds;CirrusCover=cirrus;CloudBaseM=cloudBase;CloudThicknessM=thickness;CirrusBaseM=cirrusBase;WindEastMps=east;WindNorthMps=north;GustMps=gust;Thunderstorm=storm;}
    }
    public readonly struct WeatherSurfaceEstimate
    {
        public readonly double SnowWaterMm,RecentLiquidMm,SoilWetness,RunoffPotentialMm;
        public WeatherSurfaceEstimate(double snow,double liquid,double wetness,double runoff){SnowWaterMm=snow;RecentLiquidMm=liquid;SoilWetness=wetness;RunoffPotentialMm=runoff;}
    }
    /// <summary>Random-access weather-preview-1. No actor/cell ticks, stateful RNG or visitation history.
    /// 32-day conservative binary cascades, 3-hour packets convolved with a cubic B-spline.
    /// Rate and interval water use the same packets. Surface retention is an explicit bounded estimate.</summary>
    public sealed class ProceduralWeather
    {
        public const string Version="weather-preview-1";
        public const double PacketDays=.125,BlockDays=32,MaximumWindowDays=366;
        readonly int seed;readonly V position;readonly WeatherClimate climate;
        public ProceduralWeather(int seed,V position,WeatherClimate climate)
        {if(climate==null||double.IsNaN(position.Length)||Math.Abs(position.Length-1)>.00001)throw new ArgumentException("Weather needs a unit position and climate.");this.seed=seed;this.position=position;this.climate=climate;}
        static int Key(long value,int stream)=>unchecked((int)Rng.Mix((uint)value^Rng.Mix((uint)(value>>32))^(uint)stream*0x9e3779b9u));
        double Field(long id,int level,int stream)
        {return D.Clamp(.5+SphereNoise.Sample(position,seed^Key(id,stream),6+level*9)*.8);}
        double Synoptic(double days,int stream)
        {long k=(long)Math.Floor(days/6);double f=days/6-k;f=f*f*(3-2*f);return Field(k,2,stream)*(1-f)+Field(k+1,2,stream)*f;}
        double Temperature(double days)=>climate.At(climate.TemperatureC,days)+(Synoptic(days,18)-.5)*10;
        double WindPhase=>Rng.Mix(unchecked((uint)seed)^0x5216b5u)/4294967295d*2*D.Pi;
        double WindAngle(double days)=>((days/6)%1)*2*D.Pi+WindPhase;
        double MeanWind(double[] values)=> (values[0]+values[1]+values[2]+values[3])*.75;
        /// <summary>Analytic bulk-cloud flow, in metres. Surface climate winds are separate.
        /// The drift must not depend on the visitor's local climate: integrating a changed
        /// local mean over an ancient absolute date would make clouds jump on walking.</summary>
        public V WindDisplacement(double days)
        {
            double a=WindAngle(days),scale=4*6*86400/(2*D.Pi);
            return new V(3*days*86400-scale*D.Sin(a+D.Pi/2),days*86400+scale*D.Sin(a),0);
        }
        WeatherWater Root(long block)
        {
            double day=(block+.5)*BlockDays,mean=climate.At(climate.MonthlyPrecipitationMm,day)*BlockDays/(365d/12),factor=.35+1.3*Field(block,0,11);
            double amount=Math.Max(0,mean*factor),snow=D.Clamp((2-Temperature(day))/4);
            return new WeatherWater(amount*(1-snow),amount*snow);
        }
        double Split(long block,int node,int level,bool snow)
        {
            long key=unchecked(block*1024+node);double u=Field(key,level,7),wet=climate.At(climate.MonthlyPrecipitationMm,(block+.5)*BlockDays),edge=.38-.19*D.Clamp(wet/160);
            double f=D.Clamp((u-edge)/(1-2*edge));f=f*f*(3-2*f);
            // Rain/snow retain their own conserved budgets but share passing systems.
            if(snow){double cold=Field(key,level,19);f=D.Clamp(f+(cold-.5)*.25);}
            return f;
        }
        WeatherWater Packet(long index)
        {
            long block=(long)Math.Floor(index/256d);int local=(int)(index-block*256),node=1;var water=Root(block);
            for(int level=0;level<8;level++){bool right=(local&(128>>level))!=0;double r=Split(block,node,level,false),s=Split(block,node,level,true);water=new WeatherWater(water.RainMm*(right?1-r:r),water.SnowWaterMm*(right?1-s:s));node=node*2+(right?1:0);}
            return water;
        }
        WeatherWater Prefix(long block,int count)
        {
            if(count<=0)return default;if(count>=256)return Root(block);var nodeWater=Root(block);WeatherWater total=default;int node=1;
            for(int level=0;level<8;level++){double r=Split(block,node,level,false),s=Split(block,node,level,true);var left=new WeatherWater(nodeWater.RainMm*r,nodeWater.SnowWaterMm*s);bool right=(count&(128>>level))!=0;if(right){total+=left;nodeWater-=left;}else nodeWater=left;node=node*2+(right?1:0);}
            return total;
        }
        static double Cube(double x)=>x*x*x;
        public static double Kernel(double x)
        {double a=Math.Abs(x);return a>=2?0:(Cube(2-a)-4*Cube(Math.Max(0,1-a)))/6;}
        public static double KernelIntegral(double x)
        {if(x<=-2)return 0;if(x>=2)return 1;double P(double v){v=Math.Max(0,v);return v*v*v*v;}return (P(x+2)-4*P(x+1)+6*P(x)-4*P(x-1))/24;}
        WeatherWater Edge(double days)
        {
            double x=days/PacketDays-.5;long boundary=(long)Math.Floor(x)-2;WeatherWater sum=default;
            for(long i=boundary;i<=boundary+5;i++)sum+=Packet(i)*KernelIntegral(x-i);
            return sum;
        }
        WeatherWater FullPackets(long start,long end)
        {
            WeatherWater sum=default;while(start<end){long block=(long)Math.Floor(start/256d);int first=(int)(start-block*256),last=(int)Math.Min(256,end-block*256);sum+=Prefix(block,last)-Prefix(block,first);start=block*256+last;}return sum;
        }
        public WeatherWater Between(double startDays,double endDays)
        {
            if(double.IsNaN(startDays)||double.IsNaN(endDays)||double.IsInfinity(startDays)||double.IsInfinity(endDays)||endDays<startDays||endDays-startDays>MaximumWindowDays||Math.Abs(startDays)>365e7||Math.Abs(endDays)>365e7)throw new ArgumentOutOfRangeException("Weather windows must be ordered and at most366days.");
            // Full packets before a moving boundary cancel. Only two boundary
            // neighborhoods and complete cascade subtrees are needed.
            long a=(long)Math.Floor(startDays/PacketDays-.5)-2,b=(long)Math.Floor(endDays/PacketDays-.5)-2;
            var total=FullPackets(a,b)+Edge(endDays)-Edge(startDays);
            return new WeatherWater(Math.Max(0,total.RainMm),Math.Max(0,total.SnowWaterMm));
        }
        public WeatherFrame At(double days)
        {
            if(double.IsNaN(days)||double.IsInfinity(days)||Math.Abs(days)>365e7)throw new ArgumentOutOfRangeException(nameof(days));
            double x=days/PacketDays-.5;long at=(long)Math.Floor(x);WeatherWater rate=default;
            for(long i=at-1;i<=at+2;i++)rate+=Packet(i)*(Kernel(x-i)/(PacketDays*24));
            double t=Temperature(days),h=D.Clamp(climate.At(climate.Humidity,days)),wet=rate.TotalMm;
            // Mixed precipitation is generated from conserved cloud-water phases;
            // evaporative cooling keeps a snow-dominated shower near freezing.
            if(wet>.02&&rate.SnowWaterMm/wet>.5)t=Math.Min(t,1);if(wet>.02&&rate.RainMm/wet>.8)t=Math.Max(t,-1);
            double cloud=D.Clamp(h*.45+Synoptic(days,4)*.65-.15+Math.Min(.55,wet*.7));if(wet>.1)cloud=Math.Max(.86,cloud);
            double cirrus=D.Clamp((Synoptic(days,41)-.35)*1.3),storm=D.Clamp((wet-.8)/5)*D.Clamp((t+2)/20);
            double angle=WindAngle(days),wind=4+Synoptic(days,30)*4+Math.Max(storm,D.Clamp(wet/4))*12;
            double east=3+MeanWind(climate.EastWind)+4*D.Sin(angle),north=1+MeanWind(climate.NorthWind)+4*D.Sin(angle+D.Pi/2);
            double baseM=600+Math.Min(1800,Math.Max(0,climate.ElevationM)*.35)+(1-h)*1800;
            double thickness=700+cloud*1300+storm*4000;if(wet>.02)thickness=Math.Max(thickness,climate.ElevationM+600-baseM);
            return new WeatherFrame(D.Q(t,100),h,rate.RainMm,rate.SnowWaterMm,cloud,cirrus,baseM,thickness,8000+Math.Abs(position.Z)*-1800,east,north,wind*(1.1+.5*Field((long)Math.Floor(days*48),4,32)),storm);
        }
        public WeatherSurfaceEstimate SurfaceAt(double days)
        {
            // Five fixed age bands, not a replay from the world's origin. All
            // incoming water comes from Between, never unrelated "snow noise".
            double[] ages={0,1,7,30,90,365};double snow=0,liquid=0;
            for(int i=1;i<ages.Length;i++)
            {
                double age=(ages[i]+ages[i-1])*.5;var input=Between(days-ages[i],days-ages[i-1]);
                double heat=0;for(int k=0;k<8;k++)heat+=Math.Max(0,Temperature(days-age*(k+.5)/8));heat/=8;
                snow+=input.SnowWaterMm*Math.Exp(-age*(.002+heat*.12));liquid+=input.RainMm*Math.Exp(-age*(.12+Math.Max(0,Temperature(days))*.015));
            }
            double wetness=1-Math.Exp(-liquid/65),recent=Between(days-1,days).RainMm;
            return new WeatherSurfaceEstimate(snow,liquid,wetness,Math.Max(0,recent-12*(1-wetness)));
        }
    }
}
