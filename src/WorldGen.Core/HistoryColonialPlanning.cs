using System;
using System.Collections.Generic;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        int[] LivingColonialDepartures(PrehistoryState s,int polity)
        {
            var a=s.History.Aggregates;var result=new List<int>(4);
            void Offer(int cell)
            {
                if(result.Count>=4||cell<0||CellPatch[cell]<0||!DiffusionShore(cell)||result.Contains(cell)||HistoricalCells.Polity(s,cell)!=polity)return;
                if(RevisedRealmHistory(s)&&!RevisedMaritimeDeparture(s,polity,cell))return;
                int town=a.TownAt[cell],source=a.Empires.Territories.Sources[cell];
                if(town>=0&&a.Cities[town].Supported&&a.Living.Cities[town].AbandonedYear<0&&s.Society.Settlements[town].Population>=500||TerritorialNodeLive(s,polity,source))result.Add(cell);
            }
            var cities=a.Polities[polity].Cities;int offset=cities.Count==0?0:(int)(HistoryRoll("living-colonial-city-ports",polity,s.Years)*cities.Count);
            // Actual urban ports get two slots; forwarded offices and serviced
            // provincial shores retain room to explore a different ocean basin.
            for(int k=0;k<Math.Min(12,cities.Count)&&result.Count<(RealmHistory(s)?1:2);k++)Offer(s.Society.Settlements[cities.At((offset+k)%cities.Count)].Cell);
            if(RealmHistory(s))
            {
                // Already-indexed overseas offices, not a lottery among every
                // inland governorship in a large country. Query stays read-only.
                var pending=a.Empires.Connections.PortSettlements?.Governments[polity].Pending;
                int start=pending==null||pending.Count==0?0:(int)(HistoryRoll("realm-forward-departures",polity,s.Years)*pending.Count);
                for(int k=0;pending!=null&&k<Math.Min(6,pending.Count)&&result.Count<3;k++)Offer(a.Empires.Territories.Posts[pending.At((start+k)%pending.Count)].Cell);
            }
            var posts=a.Empires.Governments[polity].AdministrativePosts;offset=posts.Count==0?0:(int)(HistoryRoll("living-colonial-forward-ports",polity,s.Years)*posts.Count);
            for(int k=0;k<Math.Min(6,posts.Count)&&result.Count<3;k++)Offer(a.Empires.Territories.Posts[posts.At((offset+k)%posts.Count)].Cell);
            var coasts=a.Empires.Connections.Coasts.Governments[polity];offset=coasts.Count==0?0:(int)(HistoryRoll("living-colonial-serviced-shores",polity,s.Years)*coasts.Count);
            for(int k=0;k<Math.Min(12,coasts.Count)&&result.Count<4;k++)Offer(coasts.At((offset+k)%coasts.Count));
            return result.ToArray();
        }
        void AdvanceLivingColonialExpedition(PrehistoryState s,int polity,int elapsed)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var hc=h.Cultures[p.CultureId];
            var charts=a.Empires.Connections.Cultures[p.CultureId].Charts;Route best=null;double bestScore=double.NegativeInfinity;
            var departures=LivingColonialDepartures(s,polity);RetryLivingColonialPorts(s,polity,elapsed,departures);
            foreach(int source in departures)
            {
                int region=CellPatch[source];var nearby=communityVoyages[region];
                for(int k=0;k<6;k++)
                {
                    int destination=charts.Length>0&&k<3?charts[(int)(HistoryRoll("living-colonial-chart-"+source+"-"+k,polity,s.Years)*charts.Length)].Region:
                        nearby.Length>0?nearby[(int)(HistoryRoll("living-colonial-explore-"+source+"-"+k,polity,s.Years)*nearby.Length)].B:-1;
                    if(destination<0||destination==region)continue;
                    var route=ColonialTargetRoute(s,source,destination,polity);if(route==null||!LivingTerritorialInterest(s,polity,route.CellB))continue;
                    int sourceTown=a.TownAt[source];
                    if(!MaritimeCharterLogistics(s,polity,route,sourceTown,out _,out _))continue;
                    int owner=HistoricalCells.Polity(s,route.CellB);double opportunity=.2+Math.Min(2,ProvincialPhysicalYield(s,p.CultureId,route.CellB)*4)+(World.Landmass[source]!=World.Landmass[route.CellB]?1:.25);
                    if(owner>=0)
                    {
                        double power=Math.Max(1,p.Army)*HistoricalModernEffects.Military(hc,s.Years);
                        var defender=h.Polities[owner];double resistance=Math.Max(1,defender.Army)*HistoricalModernEffects.Military(h.Cultures[defender.CultureId],s.Years);
                        opportunity*=D.Clamp(power/Math.Max(1,resistance*3));
                    }
                    opportunity*=.75+.5*HistoryRoll("living-colonial-preference-"+source+"-"+k,polity,s.Years);
                    if(TerritorialAmbitionContains(s,polity,route.CellB))opportunity*=1.75;
                    if(opportunity>bestScore){best=route;bestScore=opportunity;}
                }
            }
            if(best==null||HistoryRoll("colonial-expedition-departure",polity,s.Years)>1-Math.Exp(-elapsed/12.0))return;
            p.Treasury-=100;a.Empires.Connections.Governments[polity].LastExpeditionYear=s.Years;
            long previousReligiousResidents=MigrantReligiousHistory(s)?AggregateRuralCellPopulation(s,best.CellB):0;
            RecordCoastChart(s,p.CultureId,best.B,p.CultureId);TryTerritorialSeaCharter(s,best,p.CultureId,elapsed);
            int shore=best.CellB,destinationRegion=CellPatch[shore];
            if(HistoricalCells.Polity(s,shore)!=polity)return;
            TryFoundLivingColonialPort(s,polity,best,elapsed);
            var local=a.Living.Regions[destinationRegion];local.Teacher=p.CultureId;local.LastExchangeYear=s.Years;local.Exposure=Math.Max(700,local.Exposure);local.CivicTeacher=polity;local.CivicExposure=Math.Max(700,local.CivicExposure);
            a.Provinces.RegionalKnowledge[destinationRegion]|=ProvincialKnowledge(hc);
            int post=a.Empires.Territories.PostAt[shore];
            if(post<0||a.Empires.Territories.Posts[post].Founders<=0||a.Regions[destinationRegion].Culture>=0||HistoricalCells.Culture(s,shore)>=0)return;
            int child=FoundMigrantAggregateCulture(s,destinationRegion,shore,p.CultureId,best.CellA,-1,previousReligiousResidents);
            var continuity=a.Empires.Connections.Cultures[child];continuity.ColonialSponsor=polity;continuity.CivilizationRoot=HistoricalRecognizedClaims.Civilization(s,p.CultureId);
            HistoryEvent(s,"colonial-community",s.Society.Cultures[child].Name+" develops as an overseas branch sponsored by "+p.Name+", retaining its political connection and inherited practices.",shore,child,polity,category:ChronicleCategory.Society);
        }
    }
}
