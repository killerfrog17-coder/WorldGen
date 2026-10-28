using System;

namespace WorldGen.Core
{
    public enum GameMode { Creative=0,Survival=1,Spectator=2 }
    public enum PlayerDeathCause { None=0,Cold=1,Heat=2,Dehydration=3,Starvation=4,Fall=5,Combat=6 }

    public static class GameModeRules
    {
        public static bool CanFly(GameMode mode)=>mode!=GameMode.Survival;
        public static bool NoClip(GameMode mode)=>mode==GameMode.Spectator;
        public static bool HasBody(GameMode mode)=>mode!=GameMode.Spectator;
        public static bool UnlimitedResources(GameMode mode)=>mode==GameMode.Creative;
        public static bool HasSurvivalNeeds(GameMode mode)=>mode==GameMode.Survival;
    }

    /// <summary>Player state, independent of generated world history and presentation.</summary>
    [Serializable]
    public sealed class PlayerSurvival
    {
        public double Health=100,Stamina=100,Energy=100,Hunger,Thirst,CoreTemperatureC=37,Wetness;
        public bool Exhausted;
        [System.Runtime.Serialization.OptionalField] public bool Unconscious;
        public PlayerDeathCause DeathCause;
        public bool Dead=>Health<=0;
        public bool Incapacitated=>Dead||Unconscious;
        public double MaximumStamina=>Energy;
        public bool CanSprint=>!Incapacitated&&!Exhausted&&Stamina>0&&Energy>5&&CoreTemperatureC>33&&CoreTemperatureC<40.5;
        public double MovementScale=>Incapacitated?0:Math.Max(.25,Math.Min(1,Math.Min(.55+Energy*.045,Math.Min(1-(35-CoreTemperatureC)*.16,1-(CoreTemperatureC-39)*.15))));
        public PlayerSurvival Copy()=>(PlayerSurvival)MemberwiseClone();
        public void Restore()
        {Health=Stamina=Energy=100;Hunger=Thirst=Wetness=0;CoreTemperatureC=37;Exhausted=Unconscious=false;DeathCause=PlayerDeathCause.None;}
        public void Validate()
        {
            void Range(double v,double lo,double hi){if(double.IsNaN(v)||double.IsInfinity(v)||v<lo||v>hi)throw new ArgumentException("Invalid survival state.");}
            Range(Health,0,100);Range(Stamina,0,100);Range(Energy,0,100);Range(Hunger,0,100);Range(Thirst,0,100);Range(Wetness,0,1);Range(CoreTemperatureC,25,45);
            if(!Enum.IsDefined(typeof(PlayerDeathCause),DeathCause))throw new ArgumentException("Invalid player death cause.");
        }
    }
}
