using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public struct VegetationWindState
    {
        public V Velocity,Drift;
        public double GustExtra,GustChance,GustDuration,PhaseA,PhaseB;
    }
    /// <summary>SpeedTree-style desired/effective controls, not per-blade physics.
    /// A multiplayer host can publish this small shared state, never plant poses.</summary>
    public sealed class VegetationWindController
    {
        public V Velocity {get;private set;}
        public V Drift {get;private set;}
        public double GustExtra {get;private set;}
        public double GustChance {get;private set;}
        public double GustDuration {get;private set;}
        public double PhaseA {get;private set;}
        public double PhaseB {get;private set;}
        public bool Ready {get;private set;}
        public double Speed=>Velocity.Length;
        public VegetationWindState Capture()=>new VegetationWindState{Velocity=Velocity,Drift=Drift,GustExtra=GustExtra,GustChance=GustChance,GustDuration=GustDuration,PhaseA=PhaseA,PhaseB=PhaseB};
        public void Restore(VegetationWindState state)
        {Velocity=state.Velocity;Drift=state.Drift;GustExtra=state.GustExtra;GustChance=state.GustChance;GustDuration=state.GustDuration;PhaseA=state.PhaseA;PhaseB=state.PhaseB;Ready=true;}
        static double Wrap(double value)=>value-Math.Floor(value/(2*Math.PI))*(2*Math.PI);
        public void Advance(V desired,double peak,double chance,double duration,double seconds,double elapsed,double responseSeconds)
        {
            double extra=Math.Max(0,peak-desired.Length);
            if(!Ready)
            {
                Ready=true;Velocity=desired;GustExtra=extra;GustChance=chance;GustDuration=duration;
                PhaseA=Wrap(seconds*1.6);PhaseB=Wrap(seconds*2.7);
                return;
            }
            elapsed=Math.Max(0,Math.Min(.25,elapsed));
            // Approximately 95% of a step reaches the target over responseSeconds.
            double a=1-Math.Exp(-3*elapsed/Math.Max(.25,responseSeconds));
            Velocity+=(desired-Velocity)*a;GustExtra+=(extra-GustExtra)*a;
            GustChance+=(chance-GustChance)*a;GustDuration+=(duration-GustDuration)*a;
            double speed=Speed,rate=.8+.2*Math.Min(3,speed/6);
            PhaseA=Wrap(PhaseA+elapsed*1.6*rate);PhaseB=Wrap(PhaseB+elapsed*2.7*rate);
            // Integrated translation, never time * a changing direction/speed.
            // Duration changes the travel rate rather than rescaling world UVs.
            if(speed>.001)Drift+=Velocity/speed*(elapsed*Math.Max(1.2,speed)*12/Math.Max(6,GustDuration));
        }
    }
}
