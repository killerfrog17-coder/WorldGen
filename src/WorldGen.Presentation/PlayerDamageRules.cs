using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Forgiving prototype impact balance, not clinical injury prediction.</summary>
    public static class PlayerDamageRules
    {
        // Assume a fit character lands well. Injury risk is deliberately not
        // converted directly to lost health; fractures will be a separate system.
        public const double FreeFallMeters=4.5,FatalFallMeters=18;
        public static double EquivalentFallMeters(double downwardSpeed)
            =>double.IsFinite(downwardSpeed)&&downwardSpeed>0?downwardSpeed*downwardSpeed/(2*TerrainMotion.Gravity):0;
        public static double FallDamage(double downwardSpeed)
        {
            double height=EquivalentFallMeters(downwardSpeed);
            if(height>=FatalFallMeters-1e-8)return 100;
            return 100*Math.Pow(Math.Max(0,Math.Min(1,(height-FreeFallMeters)/(FatalFallMeters-FreeFallMeters))),1.5);
        }
        public static void Apply(PlayerSurvival state,GameMode mode,double damage,PlayerDeathCause cause)
        {
            if(!GameModeRules.HasSurvivalNeeds(mode)||state.Dead||!double.IsFinite(damage)||damage<=0)return;
            state.Health=Math.Max(0,state.Health-damage);
            if(state.Dead){state.DeathCause=cause;state.Unconscious=false;}
        }
    }
}
