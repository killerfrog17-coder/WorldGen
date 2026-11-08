using System;

namespace WorldGen.Core
{
    // Static finite dune forms over the existing terrain heightfield.
    // Wind is the downwind coordinate; across is transverse.
    public static class DuneForms
    {
        static double Smooth(double a,double b,double x)
        {double t=Math.Max(0,Math.Min(1,(x-a)/(b-a)));return t*t*t*(10+t*(-15+6*t));}
        static uint Key(int x,int z,int seed)=>Rng.Mix(unchecked((uint)x*0x9e3779b9u^(uint)z*0x85ebca6bu^(uint)seed));
        static double Unit(uint key,uint salt)=>(Rng.Mix(key^salt)&0xffffffu)/16777216d;

        public static double WindVariability(double e0,double n0,double e1,double n1,double e2,double n2,double e3,double n3)
        {
            double east=0,north=0,axisX=0,axisY=0,total=0;
            void Add(double e,double n)
            {
                double speed=Math.Sqrt(e*e+n*n);if(speed<.000001)return;
                east+=e;north+=n;axisX+=(e*e-n*n)/speed;axisY+=2*e*n/speed;total+=speed;
            }
            Add(e0,n0);Add(e1,n1);Add(e2,n2);Add(e3,n3);if(total<.000001)return .5;
            double directed=Math.Min(1,Math.Sqrt(east*east+north*north)/total);
            double axial=Math.Min(1,Math.Sqrt(axisX*axisX+axisY*axisY)/total);
            return Math.Max(0,Math.Min(1,1-(directed+axial)*.5));
        }
        public static double SeasonalVariability(ClimateResult climate,int cell)
        {
            if(climate==null||cell<0)return .5;
            var a=climate.NorthernSpring;var b=climate.NorthernSummer;var c=climate.NorthernAutumn;var d=climate.NorthernWinter;
            if(a.WindEastMilli.Length<=cell||a.WindNorthMilli.Length<=cell||b.WindEastMilli.Length<=cell||b.WindNorthMilli.Length<=cell||c.WindEastMilli.Length<=cell||c.WindNorthMilli.Length<=cell||d.WindEastMilli.Length<=cell||d.WindNorthMilli.Length<=cell)return .5;
            return WindVariability(a.WindEastMilli[cell],a.WindNorthMilli[cell],b.WindEastMilli[cell],b.WindNorthMilli[cell],c.WindEastMilli[cell],c.WindNorthMilli[cell],d.WindEastMilli[cell],d.WindNorthMilli[cell]);
        }
        public static double Coordinates(V metres,out double wind,out double across)
        {
            double first=metres.X*.73+metres.Y*.41+metres.Z*.54;
            double alignment=first/Math.Max(.000001,metres.Length);
            double blend=Smooth(.2,.6,1-alignment*alignment);
            across=metres.X*-.6805+metres.Y*.4507+metres.Z*.5777;
            wind=blend>=.5?first:metres.X*.13+metres.Y*.85-metres.Z*.51;
            // Fade finite bodies to the existing ridge family in the overlap
            // between charts, rather than blending unrelated finite summits.
            return blend>=.5?Smooth(.5,.95,blend):1-Smooth(.05,.5,blend);
        }
        public static double TerrainVariability(double seasonal,double relief,V metres,int seed)
        {
            Coordinates(metres,out double wind,out double across);
            // Quarter means omit local mountain-valley deflection. Admit that
            // extra diversity only where relief and a coherent subgrid field
            // support it, not as random star dunes in every sandy desert.
            double scattering=Deflection(relief)*Smooth(.42,.82,Regime(wind,across,seed^0x631b7));
            return 1-(1-seasonal)*(1-scattering);
        }
        public static double Deflection(double relief)=>.95*Smooth(250,1600,relief);
        public static double LocalVariability(V metres,LandscapeControls controls,int seed)
        {
            Coordinates(metres,out double wind,out double across);
            return LocalVariability(wind,across,controls,seed);
        }
        static double LocalVariability(double wind,double across,LandscapeControls controls,int seed)
        {
            // Interpolate the regional susceptibility, then evaluate subgrid
            // deflection HERE. Sampling its phase at coarse cell centres first
            // aliases the field and erases local multidirectional provinces.
            double scattering=controls.DuneWindDeflection>0?controls.DuneWindDeflection*Smooth(.42,.82,Regime(wind,across,seed^0x631b7)):0;
            return 1-(1-controls.DuneWindVariability)*(1-scattering);
        }

        // Coherent subgrid scattering potential, used with seasonal winds and
        // terrain relief. It does not independently choose a desert's dune type.
        public static double Regime(double wind,double across,int seed)
        {
            double x=wind/11000,z=across/11000;int ix=(int)Math.Floor(x),iz=(int)Math.Floor(z);
            double a=Smooth(0,1,x-ix),b=Smooth(0,1,z-iz);
            double h00=Unit(Key(ix,iz,seed),0x17bd39u),h10=Unit(Key(ix+1,iz,seed),0x17bd39u);
            double h01=Unit(Key(ix,iz+1,seed),0x17bd39u),h11=Unit(Key(ix+1,iz+1,seed),0x17bd39u);
            return (h00*(1-a)+h10*a)*(1-b)+(h01*(1-a)+h11*a)*b;
        }
        public static void Weights(double regime,out double barchan,out double transverse,out double star)
        {barchan=1-Smooth(.28,.4,regime);star=Smooth(.62,.76,regime);transverse=1-barchan-star;}
        public static void FieldWeights(V metres,LandscapeControls controls,double sandSea,out double barchan,out double transverse,out double star,out double wind,out double across,int seed=0)
        {
            double confidence=Coordinates(metres,out wind,out across);
            Weights(LocalVariability(wind,across,controls,seed),out barchan,out _,out star);
            // Stable winds with abundant sand make connected ridges; isolated
            // crescents belong to lower-supply parts of the same sand province.
            barchan*=confidence*(1-Smooth(.65,.95,sandSea));
            star*=confidence*Smooth(.3,.55,sandSea);
            double active=(1-Smooth(.2,.6,controls.Frozen))*(1-Smooth(.1,.6,controls.Glacial));
            barchan*=active;star*=active;
            transverse=1-barchan-star;
        }

        public static double Barchan(double wind,double across,double height,double halfSpan,double advance,double stoss)
        {
            double q=across/halfSpan;if(Math.Abs(q)>=1)return 0;
            double remaining=1-q*q,crest=advance*q*q;
            double h=height*Math.Pow(remaining,1.25);
            double run=stoss*Math.Sqrt(remaining),offset=wind-crest;
            if(offset<=0)
            {
                double t=1+offset/Math.Max(.0001,run);
                return t<=0?0:h*Math.Pow(t,1.35);
            }
            // The curved crest's changing direction lengthens the lee run.
            // A restrained 27-degree baseline leaves room for its lateral slope.
            double crestSlope=2*advance*q/halfSpan;
            return Math.Max(0,h-offset*Math.Tan(27*Math.PI/180)/Math.Sqrt(1+crestSlope*crestSlope));
        }
        public static double Barchans(double wind,double across,int seed,double scale=1)
        {
            if(scale<=0)return 0;
            const double spacing=520;int ix=(int)Math.Floor(wind/spacing),iz=(int)Math.Floor(across/spacing);double result=0;
            for(int z=iz-1;z<=iz+1;z++)for(int x=ix-1;x<=ix+1;x++)
            {
                uint key=Key(x,z,seed^0x497b1);if(Unit(key,0x823f1u)>.82)continue;
                double u=wind-(x+.2+.6*Unit(key,0x4173u))*spacing,v=across-(z+.2+.6*Unit(key,0x7123u))*spacing;
                double h=(12+25*Unit(key,0x8a719u))*scale,span=h*(4.2+2.4*Unit(key,0x371b7u));
                if(Math.Abs(v)>=span||u<-h*6.5||u>span*.65+h*2)continue;
                result=Math.Max(result,Barchan(u,v,h,span,span*.65,h*6.5));
            }
            return result;
        }
        public static double Star(double wind,double across,double height,double radius,double phaseX,double phaseZ)
        {
            double r=Math.Sqrt(wind*wind+across*across);if(r>=radius)return 0;
            if(r<.000001)return height;
            double t=r/radius,c=wind/r,s=across/r;
            double cos3=4*c*c*c-3*c,sin3=3*s-4*s*s*s;
            double arm=.5+.5*(cos3*phaseX+sin3*phaseZ);arm=arm*arm*arm;
            // The angular relief vanishes at the summit, retaining one height
            // rather than a direction-dependent discontinuity at the centre.
            return height*(1-t)*(1-.22*Smooth(.05,.6,t)*(1-arm));
        }
        public static double Stars(double wind,double across,int seed,double scale=1)
        {
            if(scale<=0)return 0;
            const double spacing=2400;int ix=(int)Math.Floor(wind/spacing),iz=(int)Math.Floor(across/spacing);double result=0;
            for(int z=iz-1;z<=iz+1;z++)for(int x=ix-1;x<=ix+1;x++)
            {
                uint key=Key(x,z,seed^0x714d3);if(Unit(key,0x8b721u)>.78)continue;
                double u=wind-(x+.32+.36*Unit(key,0x2173u))*spacing,v=across-(z+.32+.36*Unit(key,0x9183u))*spacing;
                double radius=(720+330*Unit(key,0x513b7u))*scale;if(u*u+v*v>=radius*radius)continue;
                double height=Math.Min(radius*.25,(75+110*Unit(key,0x4b915u))*scale);
                double px=Unit(key,0x51375u)*2-1,pz=Unit(key,0x2a913u)*2-1,n=Math.Sqrt(px*px+pz*pz);
                if(n<.00001){px=1;pz=0;n=1;}
                result=Math.Max(result,Star(u,v,height,radius,px/n,pz/n));
            }
            return result;
        }
    }
}
