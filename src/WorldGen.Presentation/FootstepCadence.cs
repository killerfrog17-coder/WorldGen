using System;
namespace WorldGen.Presentation
{
    public enum TravelGait { Walk,Jog,Sprint }
    /// <summary>Distance-driven contacts, never a repeating audio timer.</summary>
    public sealed class FootstepCadence
    {
        double phase=.5;
        public void Reset()=>phase=.5;
        public bool Advance(double metres,bool grounded,TravelGait gait)
        {
            if(double.IsNaN(metres)||metres<0||metres>4){Reset();return false;}
            // A brief loss of contact pauses a stride; it must not erase all
            // distance already walked. Never emit a contact while airborne.
            if(!grounded)return false;
            phase+=metres/(gait==TravelGait.Walk?.68:gait==TravelGait.Jog?1.08:1.42);
            if(phase<1)return false;phase=Math.Min(.95,phase-1);return true;
        }
        public static double Gain(TravelGait gait)=>gait==TravelGait.Walk?.35:gait==TravelGait.Jog?.6:1;
    }
}
