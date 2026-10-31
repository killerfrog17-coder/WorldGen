using System;

namespace WorldGen.Core
{
    [Serializable] public sealed class BleedingState
    {
        public double Remaining=1,Rate,ClotSeconds=45,LastUpdatedDays;
        public BleedingState Copy()=>(BleedingState)MemberwiseClone();
        public void Validate()
        {if(!ItemUseRules.Finite(Remaining)||Remaining<0||Remaining>1||!ItemUseRules.Finite(Rate)||Rate<0||Rate>1||!ItemUseRules.Finite(ClotSeconds)||ClotSeconds<1||ClotSeconds>3600||!ItemUseRules.Finite(LastUpdatedDays)||LastUpdatedDays<0)throw new ArgumentException("Invalid bleeding state.");}
    }
    [Serializable] public sealed class BloodSign
    {
        public V Position;public double SizeM,CreatedDays;public int Kind;
        public BloodSign Copy()=>(BloodSign)MemberwiseClone();
        public void Validate(){if(!ItemUseRules.Finite(Position.Length)||Math.Abs(Position.Length-1)>1e-5||!ItemUseRules.Finite(SizeM)||SizeM<=0||SizeM>1||!ItemUseRules.Finite(CreatedDays)||CreatedDays<0||Kind<0||Kind>2)throw new ArgumentException("Invalid blood sign.");}
    }
    /// <summary>Coarse wound/blood-loss gameplay, not an organ/injury simulation
    /// or clinical lethality model. Physical seconds match combat and exertion.</summary>
    public static class BleedingRules
    {
        static double Unit(double x)=>Math.Max(0,Math.Min(1,x));
        public static void Wound(BleedingState s,double damageFraction,double penetration,double incidence,double tissueDepthM,bool torso)
        {
            double cut=Unit(damageFraction)*Unit(penetration)*Unit(incidence);
            if(cut<.002||tissueDepthM<.005)return;
            bool deep=torso&&tissueDepthM>.12&&cut>.045;
            s.Rate=Math.Min(.04,s.Rate+cut*(deep?.035:.006));
            s.ClotSeconds=Math.Max(s.ClotSeconds,deep?900:30+180*cut);
        }
        public static double Advance(BleedingState s,double seconds,double activity=0)
        {
            if(!ItemUseRules.Finite(seconds)||seconds<0||seconds>1e9)throw new ArgumentException("Invalid bleeding interval.");
            if(s.Rate<=0||seconds==0)return 0;
            double decay=Math.Exp(-seconds/Math.Max(1,s.ClotSeconds));
            double lost=Math.Min(s.Remaining,s.Rate*s.ClotSeconds*(1-decay)*(1+Unit(activity)*.35));
            s.Remaining=Math.Max(0,s.Remaining-lost);s.Rate*=decay;if(s.Rate<1e-7)s.Rate=0;return lost;
        }
        public static double HealthLimit(BleedingState s,double maximum)=>maximum*Unit((s.Remaining-.35)/.5);
        public static double Mobility(BleedingState s)=>.25+.75*Unit((s.Remaining-.35)/.4);
    }
}
