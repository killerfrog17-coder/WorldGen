using System;
namespace WorldGen.Core
{
    // Capability and service priors, not dates or a restriction on community boats.
    public static class HistoricalMaritimeControlEffects
    {
        public static bool Administration(double writing,double navigation,int institution)
            =>writing>=.2&&navigation>=.2&&institution>=350;
        public static double LegKm(double ocean,double steam,double engines)
            =>ocean<.2?700:2500+7000*D.Clamp(ocean)+5000*D.Clamp(steam)+7000*D.Clamp(engines);
        public static double NetworkKm(double ocean,double steam,double engines)
            =>ocean<.2?1800:3500+10000*D.Clamp(ocean)+8000*D.Clamp(steam)+12000*D.Clamp(engines);
        public static bool Reach(double legKm,double chainKm,double ocean,double steam,double engines)
            =>legKm>=0&&chainKm>=0&&legKm<=LegKm(ocean,steam,engines)&&legKm+chainKm<=NetworkKm(ocean,steam,engines);
    }
    public sealed partial class Prehistory
    {
        bool RevisedMaritimeAdministration(PrehistoryState s,int polity)
        {
            var hc=s.History.Cultures[s.History.Polities[polity].CultureId];
            return HistoricalMaritimeControlEffects.Administration(HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Writing,s.Years),
                HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Navigation,s.Years),hc.Institution);
        }
        // A flag/empty payroll entry cannot launch another fleet. Occupied local
        // craft communities may use a supported depot without a named urban port.
        bool RevisedMaritimeDeparture(PrehistoryState s,int polity,int cell)
        {
            var a=s.History.Aggregates;
            bool Port(int city)
            {
                if(city<0||a.Resolve(s.Society.Settlements[city].PolityId,s.History)!=polity)return false;
                var town=s.Society.Settlements[city];
                double craft=Math.Max(HistoricalModernEffects.Adoption(s.History.Cultures[town.CultureId],HistoricalTechnology.Sailing,s.Years),
                    HistoricalModernEffects.Adoption(s.History.Cultures[s.History.Polities[polity].CultureId],HistoricalTechnology.Sailing,s.Years));
                return HistoricalMaritimePracticeEffects.WorkingPort(a.Living.Cities[city].AbandonedYear<0,a.Cities[city].Supported,DiffusionShore(town.Cell),town.Population,town.FoodReserve,craft);
            }
            if(Port(a.TownAt[cell]))return true;
            int checkedPorts=0;
            foreach(int city in NearbyLivingTowns(s,cell,180))
            {if(++checkedPorts>12)break;if(Port(city)&&InstitutionalRouteAllowed(s,s.Society.Settlements[city].Cell,cell,s.History.Polities[polity].CultureId))return true;}
            return LivingStateRuralMaritime(s,polity,cell);
        }
        double RevisedMaritimeChainKm(PrehistoryState s,int polity,int source)
        {
            var a=s.History.Aggregates;double distance=0;var hc=s.History.Cultures[s.History.Polities[polity].CultureId];
            for(int depth=0;depth<8;depth++)
            {
                if(source>=0)
                {
                    if(source>=a.Cities.Length||a.Resolve(s.Society.Settlements[source].PolityId,s.History)!=polity||a.Living.Cities[source].AbandonedYear>=0||!a.Cities[source].Supported)return double.MaxValue;
                    int capital=s.Society.Settlements[s.History.Polities[polity].CapitalId].Cell,cell=s.Society.Settlements[source].Cell;
                    // Populating one stepping stone does not reset the fiscal
                    // court's entire overseas reach to zero.
                    if(capital!=cell&&InstitutionalUnsupportedWaterKm(capital,cell,out _)>120)
                        distance+=D.Angle(World.Surface.Cells[capital].Position,World.Surface.Cells[cell].Position)*World.Recipe.RadiusKm;
                    return distance;
                }
                if(source== -1||-source-2>=a.Empires.Territories.Posts.Length)return double.MaxValue;
                var post=a.Empires.Territories.Posts[-source-2];
                if(post.ClosedYear>=0||a.Resolve(post.PolityId,s.History)!=polity||HistoricalCells.Polity(s,post.Cell)!=polity)return double.MaxValue;
                if(post.Maritime)
                {
                    if(!RevisedMaritimeAdministration(s,polity))return double.MaxValue;
                    double leg=post.LegDistanceM/1000.0;
                    if(leg>HistoricalMaritimeControlEffects.LegKm(HistoricalModernEffects.Adoption(hc,HistoricalTechnology.OceanNavigation,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.SteamPower,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.CombustionEngines,s.Years)))return double.MaxValue;
                    distance+=leg;
                }
                source=post.ParentSource;
            }
            return double.MaxValue;
        }
        bool RevisedMaritimeReach(PrehistoryState s,int polity,Route route,int source)
        {
            if(!RevisedRealmHistory(s))return true;
            if(!RevisedMaritimeAdministration(s,polity))return false;
            var hc=s.History.Cultures[s.History.Polities[polity].CultureId];
            double chain=RevisedMaritimeChainKm(s,polity,source);
            return HistoricalMaritimeControlEffects.Reach(route.DistanceM/1000.0,chain,
                HistoricalModernEffects.Adoption(hc,HistoricalTechnology.OceanNavigation,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.SteamPower,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.CombustionEngines,s.Years));
        }
    }
}
