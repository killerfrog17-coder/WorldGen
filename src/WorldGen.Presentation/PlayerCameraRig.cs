using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Player-owned camera distance; collision never changes its requested zoom.</summary>
    public sealed class PlayerCameraRig
    {
        public const double MaximumDistance=12;
        public double Distance {get;private set;}
        public bool Locked {get;private set;}
        public bool FirstPerson=>Distance<.15;
        public void Restore(double distance,bool locked){Distance=Math.Max(0,Math.Min(MaximumDistance,distance));Locked=locked;}
        public void ToggleLock(){Locked=!Locked;}
        public void Zoom(double notches)
        {
            if(Locked||notches==0||double.IsNaN(notches)||double.IsInfinity(notches))return;
            double distance=Distance;
            for(int i=0;i<Math.Min(8,Math.Ceiling(Math.Abs(notches)));i++)
                distance=notches<0?(distance<.15?2.4:Math.Min(MaximumDistance,distance*1.24)):(distance<=2.41?0:distance/1.24);
            Distance=distance;
        }
        public static double ClearDistance(V pivot,V backwards,double requested,Func<double,double,double> ground)
        {
            if(requested<=0)return 0;
            // Bounded height-field probe; no terrain colliders or mesh rebuilds.
            double prior=0;
            for(int i=1;i<=12;i++)
            {
                double d=requested*i/12;var p=pivot+backwards*d;
                if(p.Y<ground(p.X,p.Z)+.18)
                {
                    double low=prior,high=d;
                    for(int j=0;j<4;j++){double middle=(low+high)*.5;var q=pivot+backwards*middle;if(q.Y<ground(q.X,q.Z)+.18)high=middle;else low=middle;}
                    return Math.Max(0,low-.08);
                }
                prior=d;
            }
            return requested;
        }
    }
}
