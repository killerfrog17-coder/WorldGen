using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Audible effort, separate from the physiological effort model.
    /// Fresh exercise stays quiet; sustained depletion raises it continuously.</summary>
    public static class BreathingFeedback
    {
        public static double Intensity(ExertionState? state)
        {
            if(state==null)return 0;
            double breath=Math.Max(0,Math.Min(1,(state.Breath-.04)/.96));
            double strain=Math.Max(1-state.Burst,Math.Min(1,state.Fatigue*1.25));
            return breath*(.025+.975*Math.Pow(Math.Max(0,Math.Min(1,strain)),1.6));
        }
    }
}
