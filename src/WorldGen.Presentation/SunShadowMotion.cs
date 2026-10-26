using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>A presentation-only light pose. Holds daylight while the
    /// observer is still, then eases toward the current astronomical direction.
    /// No shadow-map images, gameplay clock or weather state are interpolated.</summary>
    public sealed class SunShadowMotion
    {
        public V Direction {get;private set;}
        public bool Holding=>ready&&!catching;
        bool ready,catching;V start,previousTarget;double transitionAt,lastTime;
        static double Angle(V a,V b)=>Math.Acos(Math.Max(-1,Math.Min(1,V.Dot(a,b))))*180/Math.PI;
        public V Step(V target,double time,bool observerMoving,bool holdAllowed,bool reset=false)
        {
            target=target.Unit;
            if(!ready||reset||time<lastTime||Angle(previousTarget,target)>20)
            {ready=true;catching=false;Direction=start=previousTarget=target;lastTime=transitionAt=time;return Direction;}
            previousTarget=target;lastTime=time;
            bool follow=observerMoving||!holdAllowed;
            if(!catching&&(follow||Angle(Direction,target)>=5))
            {catching=true;start=Direction;transitionAt=time;}
            if(catching)
            {
                double t=Math.Max(0,Math.Min(1,(time-transitionAt)/5));t=t*t*(3-2*t);
                // Small bounded pose differences use a normalized blend, avoiding
                // Euler wrap/zenith singularities and repeated frame-rate lerps.
                var blended=start*(1-t)+target*t;
                Direction=blended.Length>1e-9?blended.Unit:target;
                if(t>=1&&!follow)catching=false;
            }
            return Direction;
        }
    }
}
