using System;

namespace WorldGen.Core
{
    public readonly struct WaterQuality
    {
        public readonly LocalWaterKind Kind;public readonly double SalinityPpt,Contamination,Turbidity;
        public WaterQuality(LocalWaterKind kind,double salinity,double contamination,double turbidity)
        {Kind=kind;SalinityPpt=Math.Max(0,salinity);Contamination=D.Clamp(contamination);Turbidity=D.Clamp(turbidity);}
        public bool Fresh=>SalinityPpt<.5;
        public string Name=>SalinityPpt>=30?"Salt water":!Fresh?"Brackish / saline water":Kind==LocalWaterKind.River?"Fresh river water":"Fresh lake water";
        public string Description=>Fresh?"Untreated; safety is uncertain":SalinityPpt>=7?"Too salty to hydrate you":"Salty, untreated water";
        public static WaterQuality From(LocalTerrainSample sample,HabitatCell habitat=null,double lakeSalinity=double.NaN)
        {
            double salinity=sample.WaterKind==LocalWaterKind.Ocean?35:.08;
            if(sample.WaterKind==LocalWaterKind.Lake)salinity=double.IsFinite(lakeSalinity)?lakeSalinity:(habitat?.SalinityMilliPpt??80)*.001;
            if(sample.WaterKind==LocalWaterKind.River&&habitat?.Flooding==FloodRegime.Tidal&&sample.WaterSurfaceM<2)
                salinity=Math.Max(salinity,habitat.SalinityMilliPpt*.001*D.Clamp(1-Math.Max(0,sample.WaterSurfaceM)/2));
            double mud=D.Clamp(sample.Sediment*.75+sample.WaterTurbulence*.1+(habitat?.FloodFractionPermille??0)*.00025);
            // Appearance and salinity do not establish whether infectious material
            // is present. The source model supplies that separately.
            return new WaterQuality(sample.WaterKind,salinity,0,mud);
        }
        public WaterQuality WithContamination(double hazardPerLitre)=>new WaterQuality(Kind,SalinityPpt,hazardPerLitre,Turbidity);
    }
    public static class WaterDrinking
    {
        public static void Drink(PlayerSurvival state,WaterQuality water,double litres,double chance,GameMode mode)
        {
            if(state==null||state.Incapacitated||water.Kind==LocalWaterKind.None||!double.IsFinite(litres)||litres<=0)return;
            if(mode!=GameMode.Survival)return;
            double net=litres*Math.Max(-1.8,1-Math.Pow(water.SalinityPpt/7,1.2));
            PlayerMetabolism.Hydrate(state,net);
            if(water.Contamination<=0||state.WaterSicknessSeconds>0)return;
            if(state.WaterExposureThreshold<=0)
            {
                // One saved susceptibility draw per 24-hour exposure window,
                // not per keypress. Subdividing the same volume cannot reroll it.
                double u=double.IsFinite(chance)?D.Clamp(chance):.5;
                state.WaterExposureThreshold=-Math.Log(Math.Max(1e-12,1-Math.Min(1-1e-12,Math.Max(1e-12,u))));
                state.WaterExposureResponse=Rng.Value(unchecked((int)(u*int.MaxValue)),"water-illness-response",state.WaterExposureSequence);
                state.WaterExposureSequence=state.WaterExposureSequence==int.MaxValue?0:state.WaterExposureSequence+1;
                state.WaterExposureAgeSeconds=state.WaterExposureHazard=0;
            }
            state.WaterExposureHazard+=water.Contamination*litres;
            if(state.WaterExposureHazard<state.WaterExposureThreshold)return;
            double response=state.WaterExposureResponse;
            // Deliberate gameplay distribution, not measured prehistoric rates.
            // Most infections are unnoticed or mild; a small tail is serious.
            double severity=response<.55?0:response<.85?.06+(response-.55)/.3*.12:
                response<.97?.25+(response-.85)/.12*.3:.65+(response-.97)/.03*.3;
            double variation=Rng.Value(state.WaterExposureSequence,"water-illness-course",unchecked((int)(response*int.MaxValue)));
            state.WaterSicknessDelaySeconds=(12+36*variation)*3600;
            state.WaterSicknessSeconds=state.WaterSicknessTotalSeconds=(severity==0?12+24*variation:24+48*variation)*3600;
            state.WaterSicknessSeverity=severity;
            ClearExposure(state);
        }
        static void ClearExposure(PlayerSurvival s)
        {s.WaterExposureHazard=s.WaterExposureThreshold=s.WaterExposureResponse=s.WaterExposureAgeSeconds=0;}
        public static double SymptomSeverity(PlayerSurvival s)
        {
            if(s.WaterSicknessSeconds<=0||s.WaterSicknessDelaySeconds>0)return 0;
            if(s.WaterSicknessTotalSeconds<=0)return s.WaterSicknessSeverity; // Existing saves.
            double age=Math.Max(0,s.WaterSicknessTotalSeconds-s.WaterSicknessSeconds);
            return s.WaterSicknessSeverity*Math.Min(1,Math.Min(age/(3*3600),s.WaterSicknessSeconds/(6*3600)));
        }
        // Integral of the squared symptom envelope: exact even when a sleep
        // step spans onset, the entire illness and recovery in one update.
        static double SymptomIntegral(double age,double total)
        {
            age=Math.Max(0,Math.Min(total,age));const double onset=3*3600,fade=6*3600;
            if(age<onset)return age*age*age/(3*onset*onset);
            if(age<=total-fade)return onset/3+age-onset;
            double left=(total-age)/fade;
            return onset/3+total-fade-onset+fade/3*(1-left*left*left);
        }
        public static void AdvanceIllness(PlayerSurvival state,double seconds)
        {
            if(!double.IsFinite(seconds)||seconds<=0||state.Dead)return;
            if(state.WaterExposureThreshold>0)
            {state.WaterExposureAgeSeconds+=seconds;if(state.WaterExposureAgeSeconds>=86400)ClearExposure(state);}
            if(state.WaterSicknessSeconds<=0)return;
            double active=Math.Max(0,seconds-state.WaterSicknessDelaySeconds);state.WaterSicknessDelaySeconds=Math.Max(0,state.WaterSicknessDelaySeconds-seconds);
            active=Math.Min(active,state.WaterSicknessSeconds);if(active<=0)return;
            double age=state.WaterSicknessTotalSeconds-state.WaterSicknessSeconds;
            double burden=state.WaterSicknessTotalSeconds>0?SymptomIntegral(age+active,state.WaterSicknessTotalSeconds)-SymptomIntegral(age,state.WaterSicknessTotalSeconds):active;
            double symptomaticDays=Math.Max(0,burden)/86400*state.WaterSicknessSeverity*state.WaterSicknessSeverity;
            state.WaterSicknessSeconds=Math.Max(0,state.WaterSicknessSeconds-active);
            PlayerMetabolism.Hydrate(state,-symptomaticDays*6);state.Energy=Math.Max(0,state.Energy-symptomaticDays*36);
            if(state.WaterSicknessSeconds<=0){state.WaterSicknessSeverity=state.WaterSicknessTotalSeconds=0;ClearExposure(state);}
        }
    }
}
