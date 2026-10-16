using System;
namespace WorldGen.Core
{
    // Transparent ecological/institutional priors. These are not fitted Earth
    // country counts, expiry dates or a global technological timetable.
    public static class HistoricalProvincialEffects
    {
        public static double FarmingDensity(double localYield,int farming)
            =>D.Q((2+65*D.Clamp(localYield))*(.2+.8*D.Clamp(farming/600.0)),1000000);
        public static double RuralProductivity(int farming,double localYield,HistoricalCulture c,int year)
            =>D.Q(1+30*D.Clamp(farming/600.0)*D.Clamp(localYield)*HistoricalModernEffects.FoodMultiplier(c,year),1000000);
        public static double UrbanShare(HistoricalCulture c,int year)
            =>D.Q(.08+.04*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Roads,year)+.04*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Printing,year)
                +.15*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Railways,year)+.10*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Sanitation,year)
                +.12*HistoricalModernEffects.Adoption(c,HistoricalTechnology.MechanizedAgriculture,year),1000000);
        public static double ProvincialPressure(double serviceDistance,double localStrength,bool foreign,double mobilization,
            double legitimacy,double exhaustion,double conquest,double deficit,double publicWorks)
        {
            double exclusion=foreign?(.22+.22*mobilization)*localStrength:.08*localStrength;
            return D.Q(D.Clamp(.12+.30*D.Clamp(serviceDistance/2)+exclusion+.25*exhaustion+.20*conquest
                +.30*deficit-.20*legitimacy-.10*publicWorks),1000000);
        }
        public static double IndependenceHazard(double pressure,double cohesion)
            =>D.Q(.035*Math.Pow(Math.Max(0,pressure-.40),2)*(.35+.65*D.Clamp(1-cohesion)),1000000000);
        public static double LocalServiceKm(HistoricalCulture c,int year,int works)
            =>D.Q((90+.07*works+35*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Writing,year)+65*HistoricalModernEffects.Adoption(c,HistoricalTechnology.Roads,year))
                *Math.Sqrt(HistoricalModernEffects.LandTransport(c,year)*HistoricalModernEffects.AdministrativeCapacity(c,year)),1000000);
    }
    public sealed partial class Prehistory
    {
        public const string RegionalHistoryVersion="history-20";
        static bool ProvincialHistory(PrehistoryState s)=>LivingWorldHistory(s)||s?.History?.Version==RegionalHistoryVersion||CommunityHistory(s);
        public PrehistoryState UpgradeToRegionalHistory(PrehistoryState source)
        {
            Validate(source);if(ProvincialHistory(source))return source;
            if(!ConnectedHistory(source))source=UpgradeToConnectedHistory(source);
            var s=source.Copy();s.History.Version=RegionalHistoryVersion;
            PrepareProvincialSupport(s);
            // No reassignment of residents, dates, ownership or past events.
            // Productivity will respond gradually through the ordinary accounts.
            s.Seal();return s;
        }
        void AdvanceRegionalCityPolitics(PrehistoryState s,int city)
        {
            if(CommunityHistory(s)){AdvanceCommunityPolitics(s,city);return;}
            var h=s.History;var a=h.Aggregates;var t=s.Society.Settlements[city];var record=h.Towns[city];
            int owner=a.Resolve(t.PolityId,h);if(owner<0)return;
            if(record.UnrestPolityId!=owner){record.UnrestPolityId=owner;record.AutonomyPressure=0;record.LastAutonomyActionYear=s.Years;return;}
            int elapsed=s.Years-record.LastAutonomyActionYear;if(elapsed<20)return;record.LastAutonomyActionYear=s.Years;
            var p=h.Polities[owner];if(city==p.CapitalId||t.Population<1800||a.Living.Cities[city].DeclineYears>0)return;
            var account=a.Polities[owner];var hc=h.Cultures[t.CultureId];var cycle=h.Dynamics.States[owner].Cycle;
            double distance=LivingTravelKm(s,s.Society.Settlements[p.CapitalId].Cell,t.Cell,t.CultureId)/Math.Max(50,HistoricalProvincialEffects.LocalServiceKm(h.Cultures[p.CultureId],s.Years,h.Dynamics.States[owner].PublicWorks));
            double localStrength=D.Clamp(hc.Institution/650.0)*D.Clamp(Math.Sqrt(t.Population/6000.0));
            double mobilization=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Printing,s.Years);
            double deficit=D.Clamp((account.ServiceCost-account.Receipts)/(double)Math.Max(1,account.ServiceCost));
            double target=HistoricalProvincialEffects.ProvincialPressure(distance,localStrength,t.CultureId!=p.CultureId,mobilization,
                p.Legitimacy/1000.0,p.Exhaustion/1000.0,(cycle?.ConquestDebt??0)/1000.0,deficit,h.Dynamics.States[owner].PublicWorks/1000.0);
            // A local political process belongs to this city, not one randomly
            // selected province per entire empire. Each check is bounded.
            double previous=record.AutonomyPressure/1000.0;
            double pressure=previous+(target-previous)*(1-Math.Exp(-elapsed/100.0));
            record.AutonomyPressure=(int)Math.Round(pressure*1000);
            double risk=HistoricalProvincialEffects.IndependenceHazard(pressure,p.Cohesion/1000.0);
            bool succession=s.Years-h.Dynamics.States[owner].LastSuccession<25;
            if(succession)risk*=2;
            if(s.Years-t.FoundedYear>=120&&HistoryRoll("regional-independence",city,s.Years)<1-Math.Exp(-risk*elapsed))
            {
                FoundAggregateState(s,city,owner);ClaimDevelopmentProvince(s,city,owner);
                record.UnrestPolityId=t.PolityId;record.AutonomyPressure=100;
            }
        }
    }
}
