using System;
using System.Linq;
namespace WorldGen.Core
{
    public enum HistoricalTechnologyCeiling { Medieval,EarlyModern,Industrial,Modern }
    [Serializable] public sealed class HistoricalModernSettings
    {
        public HistoricalTechnologyCeiling TechnologyCeiling=HistoricalTechnologyCeiling.Modern;
        [System.Runtime.Serialization.OptionalField] public HistoricalShippingLink[] Shipping=Array.Empty<HistoricalShippingLink>();
        [System.Runtime.Serialization.OptionalField] public HistoricalCivicRivalry[] CivicRivalries;
        [System.Runtime.Serialization.OnDeserializing] void InitializeShipping(System.Runtime.Serialization.StreamingContext context){Shipping=Array.Empty<HistoricalShippingLink>();}
        public HistoricalModernSettings Copy()=>new HistoricalModernSettings{TechnologyCeiling=TechnologyCeiling,Shipping=Shipping.Select(r=>r.Copy()).ToArray(),CivicRivalries=CivicRivalries?.Select(r=>r.Copy()).ToArray()};
    }
    [Serializable] public sealed class HistoricalShippingLink
    {
        public int Id,EstablishedYear,CultureA,CultureB,PortA,PortB,DistanceM;
        public HistoricalShippingLink Copy()=>(HistoricalShippingLink)MemberwiseClone();
    }
    // Effects are transparent game priors, not fitted economic forecasts. A
    // capability is useful only after sustained adoption, not on discovery day.
    public static class HistoricalModernEffects
    {
        public static HistoricalTechnologyCeiling Era(HistoricalTechnology technology)
        {
            int k=(int)technology;
            if(technology==HistoricalTechnology.Vaccination)return HistoricalTechnologyCeiling.Industrial;
            return k<15?HistoricalTechnologyCeiling.Medieval:k<20?HistoricalTechnologyCeiling.EarlyModern:k<27?HistoricalTechnologyCeiling.Industrial:HistoricalTechnologyCeiling.Modern;
        }
        public static double Adoption(HistoricalCulture c,HistoricalTechnology technology,int year)
        {
            int k=(int)technology;if(k>=c.Technology.Length||c.Technology[k]==0)return 0;
            return D.Q(D.Clamp((year-(c.Technology[k]-1)+1)/80.0),1000000);
        }
        public static double FoodMultiplier(HistoricalCulture c,int year)=>D.Q(1+.25*Adoption(c,HistoricalTechnology.CropRotation,year)+.5*Adoption(c,HistoricalTechnology.ArtificialFertilizers,year)+.65*Adoption(c,HistoricalTechnology.MechanizedAgriculture,year),1000000);
        public static double MortalityMultiplier(HistoricalCulture c,int year,HistoricalDiseaseKind kind)
        {
            double sanitation=Adoption(c,HistoricalTechnology.Sanitation,year),medicine=Adoption(c,HistoricalTechnology.Antibiotics,year),vaccines=Adoption(c,HistoricalTechnology.Vaccination,year);
            // Antibiotics do not cure every syndrome. Vector and respiratory
            // burdens need prevention too; no practice removes all mortality.
            double treatment=kind==HistoricalDiseaseKind.Enteric?.48:kind==HistoricalDiseaseKind.Respiratory?.28:.12;
            return D.Q(Math.Max(.12,(1-.55*sanitation)*(1-treatment*medicine)*(1-.45*vaccines)),1000000);
        }
        public static double ChronicMortalityMultiplier(HistoricalCulture c,int year)=>D.Q(Math.Max(.15,1-.35*Adoption(c,HistoricalTechnology.Sanitation,year)-.2*Adoption(c,HistoricalTechnology.Vaccination,year)-.25*Adoption(c,HistoricalTechnology.Antibiotics,year)),1000000);
        public static double LandTransport(HistoricalCulture c,int year)=>D.Q(1+2*Adoption(c,HistoricalTechnology.Railways,year)+1.5*Adoption(c,HistoricalTechnology.CombustionEngines,year),1000000);
        public static double Communication(HistoricalCulture c,int year)=>D.Q(1+2*Adoption(c,HistoricalTechnology.Telegraph,year)+4*Adoption(c,HistoricalTechnology.Radio,year)+8*Adoption(c,HistoricalTechnology.Telecommunications,year),1000000);
        public static double Military(HistoricalCulture c,int year)=>D.Q(1+.3*Adoption(c,HistoricalTechnology.Gunpowder,year)+.4*Adoption(c,HistoricalTechnology.IndustrialMetallurgy,year)+.6*Adoption(c,HistoricalTechnology.CombustionEngines,year)+.5*Adoption(c,HistoricalTechnology.Aviation,year),1000000);
        // Account units are not percentages of the harvest. Rural households
        // have lower market receipts than urban ones, but are not nearly free
        // of fiscal value in an agrarian state. Service costs still apply.
        public static double RuralReceiptRate(HistoricalCulture c,int year)=>D.Q(.025+.005*Adoption(c,HistoricalTechnology.Printing,year)+.01*Adoption(c,HistoricalTechnology.Telecommunications,year),1000000);
        public static double AdministrativeCapacity(HistoricalCulture c,int year)=>D.Q(1+.5*Adoption(c,HistoricalTechnology.Printing,year)+Adoption(c,HistoricalTechnology.Railways,year)+Adoption(c,HistoricalTechnology.Telegraph,year)+1.5*Adoption(c,HistoricalTechnology.Computing,year),1000000);
        public static bool CarriedAtFounding(HistoricalCulture parent,int technology,int foundedYear)
            =>technology>=0&&technology<parent.Technology.Length&&parent.Technology[technology]>0&&parent.Technology[technology]<=foundedYear;
    }
    public sealed partial class Prehistory
    {
        static bool ModernHistory(PrehistoryState s)=>IsAggregateHistoryVersion(s.History?.Version)||(s.History?.Version=="history-13"||(s.History?.Version=="history-14"||(s.History?.Version=="history-15"||s.History?.Version=="history-16")));
        public PrehistoryState UpgradeToModernHistory(PrehistoryState source)
        {
            Validate(source);if(ModernHistory(source))return source;
            if(!InstitutionalHistory(source))return UpgradeToCurrentHistory(source);
            var institutional=source;
            var s=institutional.Copy();s.History.Version="history-13";s.History.Modern=new HistoricalModernSettings();
            foreach(var c in s.History.Cultures){Array.Resize(ref c.Technology,HistoricalTechnologies.Names.Length);Array.Resize(ref c.Research,HistoricalTechnologies.Names.Length);}
            foreach(var crops in s.History.Agriculture)crops.Cultivars=new HistoricalCultivar[6];
            foreach(var entry in s.History.Agriculture.SelectMany(a=>Enumerable.Range(0,6).Where(k=>a.AdoptedYear[k]>0).Select(k=>(Agriculture:a,Crop:k))).OrderBy(e=>e.Agriculture.AdoptedYear[e.Crop]).ThenBy(e=>e.Agriculture.CultureId).ThenBy(e=>e.Crop))
            {
                var a=entry.Agriculture;int k=entry.Crop;var cultivar=MakeCultivar(s,a.CultureId,k,a.SourceCulture[k]);
                cultivar.SelectedYears=Math.Max(cultivar.SelectedYears,Math.Min(4000,Math.Max(0,s.Years-a.AdoptedYear[k]+1)));a.Cultivars[k]=cultivar;
            }
            s.Seal();return UpgradeScheduling(s);
        }
        public PrehistoryState SetTechnologyCeiling(PrehistoryState source,HistoricalTechnologyCeiling ceiling)
        {
            Validate(source);if(!ModernHistory(source)||!Enum.IsDefined(typeof(HistoricalTechnologyCeiling),ceiling))throw new ArgumentException("Technology ceilings require the current historical rules.");
            // Lowering a ceiling stops further development. It never erases
            // knowledge, cities, discoveries or their already recorded effects.
            var s=source.Copy();s.History.Modern.TechnologyCeiling=ceiling;s.Seal();return s;
        }
    }
}
