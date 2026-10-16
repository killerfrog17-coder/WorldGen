using System;
using System.Collections.Generic;
namespace WorldGen.Core
{
    public readonly struct AgronomicCacheStatistics
    {
        public readonly long FitHits,FitMisses,HabitatHits,HabitatMisses,AreaHits,AreaMisses;
        public AgronomicCacheStatistics(long fitHits,long fitMisses,long habitatHits,long habitatMisses,long areaHits,long areaMisses)
        {FitHits=fitHits;FitMisses=fitMisses;HabitatHits=habitatHits;HabitatMisses=habitatMisses;AreaHits=areaHits;AreaMisses=areaMisses;}
    }
    public sealed partial class Prehistory
    {
        // Literal-path switch for deterministic diagnostics, never saved. The
        // world climate/soil is immutable; historical weather is applied later
        // to food production and is deliberately NOT cached here. Every mutable
        // cultivar input and soil-management choice participates in the key.
        public bool UseAgronomicFitCache=true;
        long agronomicFitHits,agronomicFitMisses,agronomicHabitatHits,agronomicHabitatMisses,agronomicAreaHits,agronomicAreaMisses;
        public AgronomicCacheStatistics AgronomicCacheCounters=>new AgronomicCacheStatistics(agronomicFitHits,agronomicFitMisses,agronomicHabitatHits,agronomicHabitatMisses,agronomicAreaHits,agronomicAreaMisses);
        readonly Dictionary<AgronomicFitKey,double> agronomicFits=new Dictionary<AgronomicFitKey,double>();
        readonly Queue<AgronomicFitKey> agronomicFitOrder=new Queue<AgronomicFitKey>();
        readonly struct AgronomicFitKey:IEquatable<AgronomicFitKey>
        {
            readonly int cell,crop,maturity,latitude,water,warmth,selection,managed;
            public AgronomicFitKey(int cell,HistoricalCultivar c,bool managed)
            {this.cell=cell;crop=c.CropId;maturity=c.MaturityDays;latitude=c.DaylengthLatitudeTenths;water=c.WaterRequirementPermille;warmth=c.MinimumWarmTenthsC;selection=c.SelectedYears;this.managed=managed?1:0;}
            public bool Equals(AgronomicFitKey b)=>cell==b.cell&&crop==b.crop&&maturity==b.maturity&&latitude==b.latitude&&water==b.water&&warmth==b.warmth&&selection==b.selection&&managed==b.managed;
            public override bool Equals(object other)=>other is AgronomicFitKey b&&Equals(b);
            public override int GetHashCode(){unchecked{int h=cell;h=h*31+crop;h=h*31+maturity;h=h*31+latitude;h=h*31+water;h=h*31+warmth;h=h*31+selection;return h*31+managed;}}
        }
        double CachedCommunityTransferFit(PrehistoryState s,int cell,HistoricalCultivar cultivar,bool managed)
        {
            var key=new AgronomicFitKey(cell,cultivar,managed);
            if(agronomicFits.TryGetValue(key,out double fit)){agronomicFitHits++;return fit;}
            agronomicFitMisses++;
            fit=LiteralCommunityTransferFit(s,cell,cultivar,managed);
            if(agronomicFits.Count>=262144)agronomicFits.Remove(agronomicFitOrder.Dequeue());
            agronomicFits.Add(key,fit);agronomicFitOrder.Enqueue(key);return fit;
        }
        readonly Dictionary<int,double> agronomicAreaYields=new Dictionary<int,double>();
        readonly Queue<int> agronomicAreaOrder=new Queue<int>();
        double CommunityAreaYield(PrehistoryState s,int cell,int group,HabitatCell habitat,double rainfallSeason)
        {
            bool cached=UseAgronomicFitCache&&LivingWorldHistory(s);int key=cell*8+group;
            if(cached&&agronomicAreaYields.TryGetValue(key,out double fit)){agronomicAreaHits++;return fit;}
            if(cached)agronomicAreaMisses++;
            fit=HistoricalLandscapeEffects.CropAreaYield(habitat,riverAccess[cell],rainfallSeason)*HistoricalDevelopmentEffects.GrowingSeason(habitat);
            if(cached){if(agronomicAreaYields.Count>=262144)agronomicAreaYields.Remove(agronomicAreaOrder.Dequeue());agronomicAreaYields.Add(key,fit);agronomicAreaOrder.Enqueue(key);}
            return fit;
        }
    }
}
