using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static class StellarField
    {
        public static int Seed(int world,int sky)=>(int)(Rng.Mix(unchecked((uint)(world^sky)))&0x7fffffff);
        public static int Count(StellarEnvironment environment)=>environment==StellarEnvironment.DenseGalacticField?24000:environment==StellarEnvironment.StarCluster?18000:5800;
        public static V Pole(int seed)=>new V(.37+new Rng(seed,"galaxy-axis-1",0).Unit(),.4,.63).Unit;
        public static V ClusterAxis(int seed)=>(Pole(seed)+new V(-1,.5,-.2)).Unit;
        public static V Direction(int seed,int index,StellarEnvironment environment)
        {
            var r=new Rng(seed,"stellar-position-1",index);double y=r.Unit()*2-1,a=r.Unit()*2*Math.PI;
            V uniform=new V(Math.Sqrt(1-y*y)*Math.Sin(a),y,Math.Sqrt(1-y*y)*Math.Cos(a));
            if(environment==StellarEnvironment.DenseGalacticField&&r.Unit()<.7)
            {
                V pole=Pole(seed),right=V.Cross(pole,new V(0,1,0)).Unit,forward=V.Cross(right,pole).Unit;double latitude=(r.Unit()+r.Unit()+r.Unit()+r.Unit()-2)*.14;
                return ((right*Math.Sin(a)+forward*Math.Cos(a))*Math.Cos(latitude)+pole*Math.Sin(latitude)).Unit;
            }
            if(environment==StellarEnvironment.StarCluster&&r.Unit()<.28)
            {
                // A spherical exponential density has a bright core and a soft
                // tail, unlike normalizing points from a bounded offset sphere.
                V axis=ClusterAxis(seed),right=V.Cross(axis,new V(0,1,0)).Unit,up=V.Cross(right,axis).Unit;
                const double concentration=24;double u=Math.Max(1e-12,r.Unit()),cosine=1+Math.Log(u+(1-u)*Math.Exp(-2*concentration))/concentration;
                double sine=Math.Sqrt(Math.Max(0,1-cosine*cosine));return axis*cosine+(right*Math.Sin(a)+up*Math.Cos(a))*sine;
            }
            return uniform;
        }
    }
    public static class AirPerspective
    {
        public static double Column(double distanceM,double observerM,double targetM,double scaleHeight)
        {
            double a=Math.Max(0,observerM)/scaleHeight,b=Math.Max(0,targetM)/scaleHeight,t=b-a;
            return distanceM*(Math.Abs(t)<.001?Math.Exp(-a)*(1-t*.5+t*t/6):(Math.Exp(-a)-Math.Exp(-b))/t);
        }
        public static V Transmission(double distanceM,double observerM,double targetM,double haze=1)
        {
            double r=Column(distanceM,observerM,targetM,8000),m=Column(distanceM,observerM,targetM,1200)*haze;
            return new V(Math.Exp(-r*.0000058-m*.000012),Math.Exp(-r*.0000135-m*.000012),Math.Exp(-r*.0000331-m*.000012));
        }
    }
}
