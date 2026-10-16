using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    public readonly struct HistoricalClimateAnomaly
    {
        public readonly int TemperatureTenthsC,PrecipitationPermille;
        public HistoricalClimateAnomaly(int temperature,int precipitation){TemperatureTenthsC=temperature;PrecipitationPermille=precipitation;}
    }
    // History-only anomalies around the saved four-season climatology. This is
    // a climate-variability prior, not a GCM, glacial reconstruction or edit to
    // accepted terrain/ecology. Neighbouring places share regional forcing.
    public static class HistoricalClimateVariability
    {
        static double Smooth(int seed,string stream,int region,int year,int period)
        {
            int epoch=year/period;double t=year%period/(double)period;t=t*t*(3-2*t);
            double a=Rng.Value(seed,stream,unchecked(region*104729+epoch*7919))*2-1;
            double b=Rng.Value(seed,stream,unchecked(region*104729+(epoch+1)*7919))*2-1;
            return a+(b-a)*t;
        }
        public static HistoricalClimateAnomaly At(int seed,int region,int year)
        {
            if(year<0)throw new ArgumentOutOfRangeException(nameof(year));
            double global=Smooth(seed,"history-climate-global",0,year,1200);
            double regional=Smooth(seed,"history-climate-regional",region,year,180);
            double pluvial=Smooth(seed,"history-climate-pluvial",region,year,2400);
            double decadal=Smooth(seed,"history-climate-decadal",region,year,35);
            return new HistoricalClimateAnomaly((int)Math.Round(12*global+8*regional+3*decadal),
                (int)Math.Round(Math.Max(350,Math.Min(1650,1000+350*pluvial+220*regional+120*decadal))));
        }
        public static double FoodResponse(HabitatCell h,HistoricalClimateAnomaly anomaly,double freshwater,bool farming,double mitigation)
        {
            double delta=anomaly.TemperatureTenthsC/10.0,rain=anomaly.PrecipitationPermille/1000.0;
            double buffer=D.Clamp(freshwater*5),water=D.Clamp(h.AridityMilli/1000.0);
            // Rainfall matters most near the dry cultivation boundary; excess
            // wetness can also harm humid farming, not uniformly improve yield.
            double sensitivity=farming?.90:.45;
            double moisture=1+(rain-1)*sensitivity*(.35+.65*(1-water))*(1-.65*buffer);
            if(h.AridityMilli>1400&&rain>1)moisture-=Math.Min(.22,(rain-1)*.35);
            double thermal=1;
            if(h.MeanTenthsC<130)thermal+=delta*.055;
            if(h.MeanTenthsC>240)thermal-=delta*.035;
            double response=Math.Max(.25,Math.Min(1.35,moisture*thermal));
            double resilience=.40*D.Clamp(mitigation);
            return D.Q(response<1?response+(1-response)*resilience:response,1000000);
        }
    }
    public static class HistoricalProtocentreEffects
    {
        public static double MaximumResidents(double environment,double stored,double institution,int links)
        {
            double provisioning=D.Clamp(stored/.20),network=.5+.5*D.Clamp(links/3.0);
            // State-emergence already charges the severe ecological hazard.
            // Do not cap every rainforest village below its formation threshold
            // forever: sustained locally stored produce can support an exception.
            return D.Q(250+5000*(.40+.60*Math.Sqrt(D.Clamp(environment)))*provisioning*(.35+.65*D.Clamp(institution/600.0))*network,1000);
        }
        public static double AnnualDispersalHazard(double environment,double stored,double shock,bool institutionalContinuity)
        {
            if(institutionalContinuity)return 0;
            // Stored support already contains crop, soil and seasonal costs;
            // multiplying it by the full establishment penalty again made
            // viable Neolithic villages disperse before their farms matured.
            // Poor camps remain transient; genuinely provisioned communities
            // may persist for centuries without thereby becoming a state.
            double reliability=Math.Sqrt(D.Clamp(environment))*D.Clamp(stored/.18);
            return D.Q(.00015+.035*Math.Pow(1-reliability,4)+.012*D.Clamp(shock),1000000000);
        }
    }
    public sealed partial class Prehistory
    {
        readonly Dictionary<(int,int),HistoricalClimateAnomaly> livingClimateCache=new Dictionary<(int,int),HistoricalClimateAnomaly>();
        readonly Queue<(int,int)> livingClimateOrder=new Queue<(int,int)>();
        HistoricalClimateAnomaly LivingClimate(PrehistoryState s,int cell)
        {
            // Decadal published forcing shares a bounded cache across ordinary
            // actor events; cache lifetime does not enter authoritative state.
            int region=World.Ecology.Province[cell],year=s.Years/10*10;var key=(region,year);
            if(livingClimateCache.TryGetValue(key,out var value))return value;
            value=HistoricalClimateVariability.At(World.Recipe.Seed,region,year);
            if(livingClimateCache.Count>=16384)livingClimateCache.Remove(livingClimateOrder.Dequeue());
            livingClimateCache.Add(key,value);livingClimateOrder.Enqueue(key);return value;
        }
        double LivingClimateFood(PrehistoryState s,int cell,int culture)
        {
            var h=CropHabitat(s,cell);bool farming=culture>=0&&s.Society.Cultures[culture].Farming>=180;
            return HistoricalClimateVariability.FoodResponse(h,LivingClimate(s,cell),riverAccess[cell],farming,culture<0?0:EnvironmentalManagement(s,culture));
        }
        bool LivingInstitutionalContinuity(PrehistoryState s,int city)
        {
            var t=s.Society.Settlements[city];var record=s.History.Towns[city];var hc=s.History.Cultures[t.CultureId];
            return s.History.Aggregates.Resolve(t.PolityId,s.History)>=0||record.CivicTeacherPolityId>=0&&record.CivicExampleExposure>=600&&record.CivicPreparationYears>=120||HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Writing,s.Years)>=.35&&s.History.Aggregates.Cities[city].Neighbors.Length>=2;
        }
        long LivingProtocentreCapacity(PrehistoryState s,int city,long potential)
        {
            if(LivingInstitutionalContinuity(s,city))return potential;
            var t=s.Society.Settlements[city];var hc=s.History.Cultures[t.CultureId];
            double maximum=HistoricalProtocentreEffects.MaximumResidents(EnvironmentalEstablishment(s,t.Cell,t.CultureId),LivingCropLandscape(s,t.CultureId,t.Cell,true),hc.Institution,s.History.Aggregates.Cities[city].Neighbors.Length);
            return Math.Min(potential,(long)maximum);
        }
        bool LivingProtocentreDispersal(PrehistoryState s,int city,int elapsed)
        {
            if(LivingInstitutionalContinuity(s,city))return false;var t=s.Society.Settlements[city];
            double shock=D.Clamp((1-LivingClimateFood(s,t.Cell,t.CultureId))*3);
            double hazard=HistoricalProtocentreEffects.AnnualDispersalHazard(EnvironmentalEstablishment(s,t.Cell,t.CultureId),LivingCropLandscape(s,t.CultureId,t.Cell,true),shock,false);
            if(HistoryRoll("living-protocentre-dispersal",city,s.Years)>=1-Math.Exp(-hazard*elapsed))return false;
            AbandonLivingTown(s,city,"The early village loses its concentrated surplus; households move between fields, seasonal camps and neighboring communities.");return true;
        }
    }
}
