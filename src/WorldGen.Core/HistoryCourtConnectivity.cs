using System;
using System.Collections.Generic;
namespace WorldGen.Core
{
    public enum HistoricalCourtConnection { Unknown, Connected, Blocked }
    public static class HistoricalCourtConnectivity
    {
        // A bounded probe is not an exhaustive component census. Budget exhaustion
        // means unknown, never proof that a country is disconnected.
        public static HistoricalCourtConnection Probe(int from,int to,Func<int,int[]> neighbors,Func<int,bool> passable,Func<int,double> priority,int budget=96)
        {
            if(from==to)return HistoricalCourtConnection.Connected;
            var open=new SortedSet<(double,int)>();var seen=new HashSet<int>{from};open.Add((priority(from),from));bool truncated=false;
            for(int count=0;count<budget&&open.Count>0;count++)
            {
                var item=open.Min;open.Remove(item);
                foreach(int next in neighbors(item.Item2))
                {
                    if(seen.Contains(next)||!passable(next))continue;
                    if(next==to)return HistoricalCourtConnection.Connected;
                    if(seen.Count>=budget*4){truncated=true;continue;}
                    seen.Add(next);open.Add((priority(next),next));
                }
            }
            return open.Count>0||truncated?HistoricalCourtConnection.Unknown:HistoricalCourtConnection.Blocked;
        }
        public static double CoreRadius(double localService,double writing,double roads,double rail)
            =>localService*(1+.75*D.Clamp(writing)+1.5*D.Clamp(roads)+3*D.Clamp(rail));
        public static double IndependenceHazard(double serviceRatio,HistoricalCourtConnection connection,double bureaucracy,double localStrength,double legitimacy)
        {
            double pressure=(connection==HistoricalCourtConnection.Blocked?1:0)+.65*D.Clamp(serviceRatio-1);
            return D.Q(.025*pressure*(.25+.75*D.Clamp(localStrength))*(1-.8*D.Clamp(bureaucracy))*(1.2-.4*D.Clamp(legitimacy)),1000000000);
        }
    }
    public sealed partial class Prehistory
    {
        static bool CourtConnectivityHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=30;
        double CourtCoreRadius(PrehistoryState s,int owner)
        {
            var hc=s.History.Cultures[s.History.Polities[owner].CultureId];
            return HistoricalCourtConnectivity.CoreRadius(HistoricalProvincialEffects.LocalServiceKm(hc,s.Years,s.History.Dynamics.States[owner].PublicWorks),
                HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Writing,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Roads,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Railways,s.Years));
        }
        bool CourtFrontierWithinCore(PrehistoryState s,int owner,double effort)
        {
            if(!CourtConnectivityHistory(s))return true;var hc=s.History.Cultures[s.History.Polities[owner].CultureId];
            // Mature physical transport can support delegated overseas/frontier
            // administration. Do not reimpose a capital-centered ancient radius
            // on the existing paid colonial and railway supply systems.
            return HistoricalModernEffects.Adoption(hc,HistoricalTechnology.OceanNavigation,s.Years)>=.2
                ||HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Railways,s.Years)>=.2||effort<=CourtCoreRadius(s,owner);
        }
        HistoricalCourtConnection CourtLandConnection(PrehistoryState s,int owner,int from,int to)
        {
            int sovereign=HistoricalImperialHierarchy.Sovereign(s,owner);
            bool Passable(int cell)
            {
                if(World.Hydrology.OceanConnected[cell]||World.Hydrology.LakeCoverageFraction[cell]>=.9)return false;
                int actual=HistoricalCells.Polity(s,cell);
                // Unclaimed countryside can carry a courier, but a hostile or
                // unrelated ruler's sovereignty is not an automatic transit right.
                return cell==from||cell==to||actual<0||HistoricalImperialHierarchy.Sovereign(s,actual)==sovereign;
            }
            var destination=World.Surface.Cells[to].Position;
            return HistoricalCourtConnectivity.Probe(from,to,c=>World.Surface.Cells[c].Neighbors,Passable,c=>D.Q(D.Angle(World.Surface.Cells[c].Position,destination),1000000000));
        }
        int CourtHarbor(PrehistoryState s,int owner,int cell)
        {
            if(DiffusionShore(cell)&&RevisedMaritimeDeparture(s,owner,cell))return cell;
            int best=-1,checkedCities=0;double distance=180;var a=s.History.Aggregates;
            foreach(int city in NearbyLivingTowns(s,cell,180))
            {
                if(++checkedCities>12)break;var town=s.Society.Settlements[city];
                if(!DiffusionShore(town.Cell)||a.Resolve(town.PolityId,s.History)!=owner||!a.Cities[city].Supported||a.Living.Cities[city].AbandonedYear>=0)continue;
                double effort=LivingTravelKm(s,cell,town.Cell,s.History.Polities[owner].CultureId);
                if(effort<distance){best=town.Cell;distance=effort;}
            }
            return best>=0&&CourtLandConnection(s,owner,cell,best)==HistoricalCourtConnection.Connected&&RevisedMaritimeDeparture(s,owner,best)?best:-1;
        }
        double CourtSeaEffort(PrehistoryState s,int owner,int from,int to)
        {
            if(!RevisedMaritimeAdministration(s,owner)||s.History.Polities[owner].Treasury<=0)return double.MaxValue;
            int departure=CourtHarbor(s,owner,from),arrival=CourtHarbor(s,owner,to);if(departure<0||arrival<0)return double.MaxValue;
            int culture=s.History.Polities[owner].CultureId;
            if(D.Angle(World.Surface.Cells[departure].Position,World.Surface.Cells[arrival].Position)*World.Recipe.RadiusKm>StateMaritimeNetworkKm(s,owner)
                ||!InstitutionalRouteAllowed(s,departure,arrival,culture))return double.MaxValue;
            double sea=DiffusionSeaEffort(s,departure,arrival,culture);if(sea==double.MaxValue)return sea;
            return sea+LivingTravelKm(s,from,departure,culture)+LivingTravelKm(s,arrival,to,culture);
        }
        bool ReviewDisconnectedCourt(PrehistoryState s,int owner,int city,int elapsed)
        {
            if(!CourtConnectivityHistory(s))return false;var h=s.History;var a=h.Aggregates;var town=s.Society.Settlements[city];var p=h.Polities[owner];
            if(city==p.CapitalId||town.Population<300||s.Years-town.FoundedYear<30||s.Years-a.Communities.Cities[city].SinceYear<15)return false;
            int capital=s.Society.Settlements[p.CapitalId].Cell;double sea=CourtSeaEffort(s,owner,capital,town.Cell);
            var connection=sea<double.MaxValue?HistoricalCourtConnection.Connected:CourtLandConnection(s,owner,capital,town.Cell);
            double ratio=Math.Min(sea,LivingTravelKm(s,capital,town.Cell,p.CultureId))/Math.Max(1,CourtCoreRadius(s,owner));
            double strength=D.Clamp(h.Cultures[town.CultureId].Institution/500.0)*D.Clamp(town.Population/1200.0);
            double hazard=HistoricalCourtConnectivity.IndependenceHazard(ratio,connection,RealmBureaucracy(s,owner),strength,p.Legitimacy/1000.0);
            if(hazard<=0||HistoryRoll("court-disconnection-30",city,s.Years)>=1-Math.Exp(-hazard*elapsed))return false;
            int rival=-1;double best=double.MaxValue;
            foreach(int peer in a.Cities[city].Neighbors)
            {
                var nearby=s.Society.Settlements[peer];int state=a.Resolve(nearby.PolityId,h);
                if(state<0||state==owner||HistoricalImperialHierarchy.Sovereign(s,state)==HistoricalImperialHierarchy.Sovereign(s,owner)
                    ||a.Living.Cities[peer].AbandonedYear>=0||!a.Cities[peer].Supported||h.Polities[state].Legitimacy<400)continue;
                double km=LivingTravelKm(s,nearby.Cell,town.Cell,h.Polities[state].CultureId);
                double central=LivingTravelKm(s,s.Society.Settlements[h.Polities[state].CapitalId].Cell,town.Cell,h.Polities[state].CultureId);
                int rivalCapital=s.Society.Settlements[h.Polities[state].CapitalId].Cell;
                if(km>=best||central>CourtCoreRadius(s,state)||CourtLandConnection(s,state,rivalCapital,town.Cell)!=HistoricalCourtConnection.Connected)continue;
                // Existing neighboring successor courts are preferred to another
                // flag, while incompatible local identities retain a choice.
                if(town.CultureId!=h.Polities[state].CultureId&&HistoryRoll("court-local-protection-30-"+state,city,s.Years)>.25)continue;
                best=km;rival=state;
            }
            if(rival>=0)
            {
                ClaimAggregatePolity(s,town.Cell,rival);ClaimDevelopmentProvince(s,city,owner);
                HistoryEvent(s,"regional-realignment",town.Name+" turns to nearby "+h.Polities[rival].Name+" for protection after losing effective connection to "+p.Name+".",town.Cell,town.CultureId,rival,owner,city,category:ChronicleCategory.Politics);
            }
            else if(town.Population>=1200&&h.Cultures[town.CultureId].Institution>=350&&a.Cities[city].Supported)
            {FoundAggregateState(s,city,owner);ClaimDevelopmentProvince(s,city,owner);}
            else
            {
                ClaimAggregatePolity(s,town.Cell,-1);QueueTerritorialServiceRecovery(s,city);
                HistoryEvent(s,"local-autonomy",town.Name+" ceases to answer to the inaccessible court of "+p.Name+"; its inhabitants remain a local settlement, not a new state.",town.Cell,town.CultureId,town:city,category:ChronicleCategory.Politics);
            }
            return true;
        }
    }
}
