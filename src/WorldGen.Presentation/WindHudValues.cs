using System;
namespace WorldGen.Presentation
{
    public readonly struct WindHudValues
    {
        public readonly float Angle,Strength,Spread;
        public WindHudValues(float angle,float strength,float spread){Angle=angle;Strength=strength;Spread=spread;}
        public static WindHudValues From(double east,double north,double cameraHeading)
        {
            double speed=Math.Sqrt(east*east+north*north);
            double strength=Math.Max(0,Math.Min(1,(speed-.7)/3));strength=strength*strength*(3-2*strength);
            // Meteorological source bearing: airflow from behind occupies the bottom.
            double angle=Math.Atan2(-east,-north)-cameraHeading*Math.PI/180-Math.PI/2;
            angle=Math.Atan2(Math.Sin(angle),Math.Cos(angle));
            return new WindHudValues((float)angle,(float)strength,(float)(.75+Math.Max(0,2-speed)*.3));
        }
    }
}
