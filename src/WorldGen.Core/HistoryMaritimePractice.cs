using System;
namespace WorldGen.Core
{
    public static class HistoricalMaritimePracticeEffects
    {
        public static bool WorkingPort(bool alive,bool supported,bool shore,long residents,long food,double sailing)
            =>alive&&supported&&shore&&residents>=500&&food>=residents*.2&&sailing>=.15;
        public static bool FundedCourt(long treasury,long receipts,long service)=>treasury>=1000&&receipts>service;
        public static bool RuralBoatCommunity(long residents,double marineFood,int fishing,bool permanentIce)
            =>!permanentIce&&residents>=500&&(marineFood>=.15||fishing>=200);
    }
    public sealed partial class Prehistory
    {
        bool LivingStateMaritimePort(PrehistoryState s,int polity,int city)
        {
            var h=s.History;var a=h.Aggregates;if(polity<0||polity>=h.Polities.Length||city<0||city>=a.Cities.Length)return false;
            var p=h.Polities[polity];var town=s.Society.Settlements[city];var capital=s.Society.Settlements[p.CapitalId];
            if(p.EndedYear>=0||a.Resolve(town.PolityId,h)!=polity||a.Resolve(capital.PolityId,h)!=polity||
                !HistoricalMaritimePracticeEffects.FundedCourt(p.Treasury,a.Polities[polity].Receipts,a.Polities[polity].ServiceCost))return false;
            double craft=Math.Max(HistoricalModernEffects.Adoption(h.Cultures[town.CultureId],HistoricalTechnology.Sailing,s.Years),
                HistoricalModernEffects.Adoption(h.Cultures[p.CultureId],HistoricalTechnology.Sailing,s.Years));
            if(!HistoricalMaritimePracticeEffects.WorkingPort(a.Living.Cities[city].AbandonedYear<0,a.Cities[city].Supported,DiffusionShore(town.Cell),town.Population,town.FoodReserve,craft))return false;
            if(!TerritorialNodeLive(s,polity,city)||!InstitutionalRouteAllowed(s,capital.Cell,town.Cell,p.CultureId))return false;
            double effort=LivingTravelKm(s,capital.Cell,town.Cell,p.CultureId);
            return effort<=TerritorialServiceRadius(s,polity,town.Cell)*1.5;
        }
        bool LivingCourtMaritimePractice(PrehistoryState s,int polity)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];
            if(LivingStateMaritimePort(s,polity,p.CapitalId))return true;
            var cities=a.Polities[polity].Cities;
            int offset=cities.Count==0?0:(int)(HistoryRoll("living-maritime-state-cities",polity,s.Years)*cities.Count);
            for(int k=0;k<Math.Min(4,cities.Count);k++)if(LivingStateMaritimePort(s,polity,cities.At((offset+k)%cities.Count)))return true;
            var coasts=a.Empires.Connections.Coasts.Governments[polity];offset=coasts.Count==0?0:(int)(HistoryRoll("living-maritime-state-coasts",polity,s.Years)*coasts.Count);
            for(int k=0;k<Math.Min(4,coasts.Count);k++)
            {
                int cell=coasts.At((offset+k)%coasts.Count),city=a.TownAt[cell];
                if(LivingStateRuralMaritime(s,polity,cell))return true;
                if(city>=0&&LivingStateMaritimePort(s,polity,city))return true;
                int source=a.Empires.Territories.Sources[cell];
                if(!TerritorialNodeLive(s,polity,source))continue;
                for(int depth=0;source<=-2&&depth<8;depth++)source=a.Empires.Territories.Posts[-source-2].ParentSource;
                if(source>=0&&LivingStateMaritimePort(s,polity,source))return true;
            }
            return false;
        }
        bool LivingStateRuralMaritime(PrehistoryState s,int polity,int cell)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];
            if(p.EndedYear>=0||!DiffusionShore(cell)||HistoricalCells.Polity(s,cell)!=polity||
                !HistoricalMaritimePracticeEffects.FundedCourt(p.Treasury,a.Polities[polity].Receipts,a.Polities[polity].ServiceCost))return false;
            long residents=AggregateRuralCellPopulation(s,cell);int local=HistoricalCells.Culture(s,cell);
            if(!HistoricalMaritimePracticeEffects.RuralBoatCommunity(residents,MarineForagerSupport(s,cell),local<0?0:s.Society.Cultures[local].Fishing,World.Ecology.Habitat[cell].Biome==Biome.Ice))return false;
            int source=a.Empires.Territories.Sources[cell];if(!TerritorialNodeLive(s,polity,source))return false;
            for(int depth=0;source<=-2&&depth<8;depth++)source=a.Empires.Territories.Posts[-source-2].ParentSource;
            if(source<0||source>=a.Cities.Length)return false;
            var depot=s.Society.Settlements[source];var capital=s.Society.Settlements[p.CapitalId];
            if(a.Resolve(depot.PolityId,h)!=polity||!a.Cities[source].Supported||a.Living.Cities[source].AbandonedYear>=0||depot.Population<500||
                a.Resolve(capital.PolityId,h)!=polity||!a.Cities[p.CapitalId].Supported||a.Living.Cities[p.CapitalId].AbandonedYear>=0||capital.Population<500||
                depot.FoodReserve<depot.Population*.2+residents*.1)return false;
            // Rural coastal craft communities predate named urban harbors. They
            // still need real occupied shore, potable/food access and a funded
            // local service depot: a remote flag or empty office is not enough.
            if(!InstitutionalRouteAllowed(s,capital.Cell,cell,p.CultureId)||!InstitutionalRouteAllowed(s,depot.Cell,cell,p.CultureId))return false;
            double reach=TerritorialServiceRadius(s,polity,cell)*1.5;
            return LivingTravelKm(s,capital.Cell,cell,p.CultureId)<=reach&&LivingTravelKm(s,depot.Cell,cell,p.CultureId)<=reach;
        }
        bool LivingCommunityMaritimePractice(PrehistoryState s,int culture)
        {
            var h=s.History;var a=h.Aggregates;var c=s.Society.Cultures[culture];
            // A native coastal population can retain an oral boatbuilding
            // tradition without a literate treasury or a formal urban port.
            if(DiffusionShore(c.OriginCell)&&MarineForagerSupport(s,c.OriginCell)>.2&&ForagerMaritimeResidents(s,culture)>=200)return true;
            var cities=a.Cultures[culture].Cities;int offset=cities.Count==0?0:(int)(HistoryRoll("community-maritime-centers",culture,s.Years)*cities.Count);
            int first=-1,second=-1;
            for(int k=0;k<Math.Min(8,cities.Count);k++)
            {
                int id=cities.At((offset+k)%cities.Count);var town=s.Society.Settlements[id];
                if(a.Living.Cities[id].AbandonedYear>=0||!a.Cities[id].Supported||town.Population<300)continue;
                if(DiffusionShore(town.Cell)&&town.FoodReserve>=town.Population*.2)return true;
                int owner=a.Resolve(town.PolityId,h);
                if(owner<0||h.Polities[owner].CultureId!=culture||owner==first||owner==second)continue;
                if(first<0)first=owner;else if(second<0)second=owner;else continue;
                // The actual current court can employ culturally different
                // subject shipbuilders. This conveys practical demand/support,
                // not their technology levels or free research/deployment.
                if(LivingCourtMaritimePractice(s,owner))return true;
            }
            return false;
        }
    }
}
