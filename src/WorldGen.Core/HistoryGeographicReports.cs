using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void ShareRevisedGeographicReports(PrehistoryState s,int polity)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var map=a.Empires.Connections.Geography.Governments[polity];
            if(map.Places.Count==0||p.EndedYear>=0)return;
            var contacts=s.Society.Cultures[p.CultureId].Contacts;
            var cursor=map.Sharing??(map.Sharing=new HistoricalGeographicSharing());int sent=0;
            int start=cursor.ContactCursor%Math.Max(1,contacts.Length);cursor.ContactCursor=(start+1)%Math.Max(1,contacts.Length);
            var candidates=new List<int>(8);
            void Peer(int peer){if(peer>=0&&peer!=polity&&candidates.Count<8&&!candidates.Contains(peer))candidates.Add(peer);}
            // Actual city contacts include other governments of the SAME
            // culture; a foreign-culture list alone left these courts isolated.
            var ownCities=a.Polities[polity].Cities;
            int cityStart=cursor.CityCursor%Math.Max(1,ownCities.Count);cursor.CityCursor=(cityStart+2)%Math.Max(1,ownCities.Count);
            int peerStart=cursor.PeerCursor;cursor.PeerCursor=(peerStart+1)%12;
            for(int k=0;k<Math.Min(2,ownCities.Count);k++)
            {
                int city=ownCities.At((cityStart+k)%ownCities.Count);
                if(a.Living.Cities[city].AbandonedYear>=0||!a.Cities[city].Supported)continue;
                var neighbors=a.Cities[city].Neighbors;
                for(int j=0;j<neighbors.Length;j++){int neighbor=neighbors[(peerStart+j)%neighbors.Length];if(a.Living.Cities[neighbor].AbandonedYear<0&&a.Cities[neighbor].Supported)Peer(a.Resolve(s.Society.Settlements[neighbor].PolityId,h));}
            }
            for(int k=0;k<Math.Min(4,contacts.Length);k++)
            {
                int culture=contacts[(start+k)%contacts.Length];
                if(EnvironmentalContactYear(s,p.CultureId,culture)<s.Years-100)continue;
                int peer=a.Resolve(a.Living.CivicOrigins[culture],h);
                if(peer<0){var cities=a.Cultures[culture].Cities;for(int j=0;j<Math.Min(2,cities.Count);j++){peer=a.Resolve(s.Society.Settlements[cities.At(j)].PolityId,h);if(peer>=0)break;}}
                Peer(peer);
            }
            foreach(int peer in candidates)
            {
                if(sent>=4)break;
                var relation=AggregateRelation(s,polity,peer);if(!relation.Trade||relation.WarSince>=0||relation.Trust<0)continue;
                var receiver=a.Empires.Connections.Geography.Governments[peer];
                int from=s.Society.Settlements[p.CapitalId].Cell,to=s.Society.Settlements[h.Polities[peer].CapitalId].Cell;
                bool physical=InstitutionalRouteAllowed(s,from,to,p.CultureId);
                // Capitals need not be ports. A bounded witnessed city trading
                // connection can deliver directions into both governments.
                if(!physical)
                {
                    var cities=a.Polities[polity].Cities;
                    for(int j=0;j<Math.Min(4,cities.Count)&&!physical;j++)
                    {
                        int city=cities.At((cursor.ContactCursor+j)%cities.Count);
                        if(a.Living.Cities[city].AbandonedYear>=0||!a.Cities[city].Supported)continue;
                        foreach(int neighbor in a.Cities[city].Neighbors)
                        {
                            var other=s.Society.Settlements[neighbor];
                            if(a.Resolve(other.PolityId,h)!=peer||a.Living.Cities[neighbor].AbandonedYear>=0||!a.Cities[neighbor].Supported)continue;
                            int here=s.Society.Settlements[city].Cell;
                            if(InstitutionalRouteAllowed(s,here,other.Cell,p.CultureId)){from=here;to=other.Cell;physical=true;break;}
                        }
                    }
                }
                if(!physical)continue;
                // Four newest entries get a prompt opportunity; four fair
                // indexed entries cover the older archive without a map census.
                int peerSent=0;
                for(int trial=0;trial<9&&sent<4&&peerSent<2;trial++)
                {
                    if(receiver.Missions.Length>=8||receiver.Missions.Count(m=>m.Kind==HistoricalSeaMission.Report)>=4)break;
                    int at=trial<5?map.Places.Count-trial:(cursor.ReportCursor++%map.Places.Count);
                    if(at<0||trial==0&&cursor.RecentRegion<0)continue;int region=trial==0?cursor.RecentRegion:map.Places.At(at),grade=map.Grade[region]>=2&&relation.Trust>=40?2:1;
                    if((RevisedMaritimeMissionHistory(s)?!HistoricalMaritimeMissionEffects.ShareWitnessed(map,receiver,region,grade):receiver.Grade[region]>=grade)||receiver.Missions.Any(m=>m.Kind==HistoricalSeaMission.Report&&m.Region==region))continue;
                    double distance=D.Angle(World.Surface.Cells[from].Position,World.Surface.Cells[to].Position)*World.Recipe.RadiusKm;
                    var hc=h.Cultures[p.CultureId];double comm=HistoricalModernEffects.Communication(hc,s.Years);
                    long now=SimulationTime.Seconds(s),due=now+Math.Max(SimulationTime.Day,(long)((7+distance/(30*Math.Max(1,comm)))*SimulationTime.Day));
                    receiver.Missions=receiver.Missions.Concat(new[]{new HistoricalSeaMission{Kind=HistoricalSeaMission.Report,Source=from,Target=map.Shore[region],Region=region,Reporter=polity,
                        Grade=grade,Opportunity=grade>=2?map.Opportunity[region]:0,Host=grade>=2?map.Host[region]:-1,ReportedYear=map.ReportedYear[region],Departed=now,Due=due}}).ToArray();
                    sent++;peerSent++;ScheduleSeaMissions(s,peer);
                }
            }
            if(cursor.ReportCursor>=map.Places.Count)cursor.ReportCursor%=map.Places.Count;
        }
    }
}
