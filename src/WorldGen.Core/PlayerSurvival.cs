using System;

namespace WorldGen.Core
{
    public enum GameMode { Creative=0,Survival=1,Spectator=2 }
    public enum PlayerDeathCause { None=0,Cold=1,Heat=2,Dehydration=3,Starvation=4,Fall=5,Combat=6,Poison=7,Drowning=8,Fire=9 }

    public static class GameModeRules
    {
        public static bool CanFly(GameMode mode)=>mode!=GameMode.Survival;
        public static bool NoClip(GameMode mode)=>mode==GameMode.Spectator;
        public static bool HasBody(GameMode mode)=>mode!=GameMode.Spectator;
        public static bool UnlimitedResources(GameMode mode)=>mode==GameMode.Creative;
        public static bool HasSurvivalNeeds(GameMode mode)=>mode==GameMode.Survival;
    }

    /// <summary>Player state, independent of generated world history and presentation.</summary>
    [Serializable] public sealed class PlayerBloodLayer
    {public string Renderer="";public byte[] Mask;public PlayerBloodLayer Copy()=>new PlayerBloodLayer{Renderer=Renderer,Mask=Mask==null?null:(byte[])Mask.Clone()};}
    [Serializable]
    public sealed class PlayerSurvival
    {
        public double Health=100,Stamina=100,Energy=100,Hunger,Thirst,CoreTemperatureC=37,Wetness;
        [System.Runtime.Serialization.OptionalField] public double Tiredness;
        [System.Runtime.Serialization.OptionalField] public System.Collections.Generic.List<PlayerBloodLayer> Blood;
        public bool Exhausted;
        [System.Runtime.Serialization.OptionalField] public bool Unconscious;
        [System.Runtime.Serialization.OptionalField] public System.Collections.Generic.List<FoodExposure> FoodExposures;
        [System.Runtime.Serialization.OptionalField] public double WaterSicknessDelaySeconds,WaterSicknessSeconds,WaterSicknessSeverity;
        [System.Runtime.Serialization.OptionalField] public double BreathDebtSeconds;
        public PlayerDeathCause DeathCause;
        public bool Dead=>Health<=0;
        public bool Incapacitated=>Dead||Unconscious;
        public double MaximumStamina=>Energy;
        public bool CanSprint=>!Incapacitated&&!Exhausted&&Stamina>0&&Energy>5&&CoreTemperatureC>33&&CoreTemperatureC<40.5;
        public double MovementScale=>Incapacitated?0:Math.Max(.25,Math.Min(1,Math.Min(.55+Energy*.045,Math.Min(1-(35-CoreTemperatureC)*.16,1-(CoreTemperatureC-39)*.15))))*(1-.2*Math.Max(0,(Tiredness-70)/30));
        public PlayerSurvival Copy(){var p=(PlayerSurvival)MemberwiseClone();p.Blood=Blood?.ConvertAll(b=>b.Copy());p.FoodExposures=FoodExposures?.ConvertAll(e=>e.Copy());return p;}
        public void Restore()
        {Health=Stamina=Energy=100;Hunger=Thirst=Wetness=Tiredness=0;CoreTemperatureC=37;Exhausted=Unconscious=false;DeathCause=PlayerDeathCause.None;Blood?.Clear();FoodExposures?.Clear();WaterSicknessDelaySeconds=WaterSicknessSeconds=WaterSicknessSeverity=BreathDebtSeconds=0;}
        public void Validate()
        {
            void Range(double v,double lo,double hi){if(double.IsNaN(v)||double.IsInfinity(v)||v<lo||v>hi)throw new ArgumentException("Invalid survival state.");}
            Range(Health,0,100);Range(Stamina,0,100);Range(Energy,0,100);Range(Hunger,0,100);Range(Thirst,0,100);Range(Wetness,0,1);Range(CoreTemperatureC,25,45);
            Range(Tiredness,0,100);
            Range(WaterSicknessDelaySeconds,0,1e8);Range(WaterSicknessSeconds,0,1e8);Range(WaterSicknessSeverity,0,1);Range(BreathDebtSeconds,0,3600);
            if(FoodExposures!=null)foreach(var e in FoodExposures){if(e==null)throw new ArgumentException("Missing food exposure.");e.Validate();}
            if(Blood!=null&&Blood.Exists(b=>b==null||string.IsNullOrEmpty(b.Renderer)||b.Renderer.Length>160||b.Mask==null||b.Mask.Length!=256*256))throw new ArgumentException("Invalid player blood layer.");
            if(!Enum.IsDefined(typeof(PlayerDeathCause),DeathCause))throw new ArgumentException("Invalid player death cause.");
        }
    }
}
