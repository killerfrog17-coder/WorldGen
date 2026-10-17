using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        // Optional review instrumentation; never an input to dates or outcomes.
        public Action<int,string> SeaVentureObserver;
        void AdvanceGeographicExpedition(PrehistoryState s,int polity)
        {
            var map=s.History.Aggregates.Empires.Connections.Geography.Governments[polity];
            if(map.Places.Count==0)BootstrapGeographicMap(s,polity);
            ShareGeographicReports(s,polity);
            if(map.ScheduledDue>0||SimulationTime.Seconds(s)<map.NextPlan)return;
            PlanSeaVentures(s,polity);ScheduleSeaMissions(s,polity);
        }
        void ScheduleSeaMissions(PrehistoryState s,int polity)
        {
            var map=s.History.Aggregates.Empires.Connections.Geography.Governments[polity];
            if(map.ScheduledDue>0||map.Missions.Length==0)return;
            map.ScheduledDue=map.Missions.Min(m=>m.Due);ScheduleAggregate(s,SeaMissionProcess,polity,map.ScheduledDue);
        }
        void PlanSeaVentures(PrehistoryState s,int polity)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var hc=h.Cultures[p.CultureId];var map=a.Empires.Connections.Geography.Governments[polity];long now=SimulationTime.Seconds(s);
            double ocean=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.OceanNavigation,s.Years),nav=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Navigation,s.Years);
            if(p.EndedYear>=0)return;
            if(p.Cohesion<(RealmHistory(s)?HistoricalRealmPoliticalEffects.FleetCohesionFloor(RealmBureaucracy(s,polity)):500)){SeaVentureObserver?.Invoke(polity,"court-cohesion");return;}
            if(nav<.2){SeaVentureObserver?.Invoke(polity,"navigation-not-deployed");return;}
            if(p.Treasury<2500||a.Polities[polity].Receipts<=a.Polities[polity].ServiceCost){SeaVentureObserver?.Invoke(polity,"court-finances");return;}
            int capacity=HistoricalSeaVentureEffects.Fleet(a.Polities[polity].Urban,a.Polities[polity].Receipts-a.Polities[polity].ServiceCost,p.Exhaustion);
            if(capacity<=map.Missions.Count(m=>m.Kind!=HistoricalSeaMission.Report)){SeaVentureObserver?.Invoke(polity,"fleet-capacity");return;}
            var departures=LivingColonialDepartures(s,polity);if(departures.Length==0){SeaVentureObserver?.Invoke(polity,"no-departure");return;}
            foreach(int source in departures)ObserveGeographicCoast(s,map,source,3,polity);
            // Try a fixed portfolio, ranked on the state's dated reports. The
            // engine's potential sea routes are search bearings, not knowledge
            // of attractive undiscovered farms, armies, populations or minerals.
            var proposals=new List<(int source,int region,int cell,double score)>();
            for(int slot=0;slot<Math.Min(2,departures.Length);slot++)
            {
                int departureOffset=RealmHistory(s)?(int)(HistoryRoll("realm-departure-rotation",polity,s.Years)*departures.Length):map.Voyages;
                int source=departures[(departureOffset+slot)%departures.Length],region=CellPatch[source];var potential=communityVoyages[region];
                for(int k=0;k<8;k++)
                {
                    int bearingShore=-1;
                    if(RealmHistory(s)&&k>=4)bearingShore=RealmBearingShore(source,(int)(HistoryRoll("realm-ocean-bearing-"+slot+"-"+k,polity,unchecked(s.Years+map.Voyages*7919))*64));
                    int to=k<4&&map.Places.Count>0?map.Places.At((int)(HistoryRoll("sea-known-"+slot+"-"+k,polity,unchecked(s.Years+map.Voyages*7919))*map.Places.Count)):
                        bearingShore>=0?CellPatch[bearingShore]:potential.Length>0?potential[(int)(HistoryRoll("sea-bearing-"+slot+"-"+k,polity,unchecked(s.Years+map.Voyages*7919))*potential.Length)].B:-1;
                    if(to<0||to==region||map.ReviewAfter[to]>s.Years||map.Missions.Any(m=>m.Region==to&&m.Kind!=HistoricalSeaMission.Report))continue;
                    int grade=map.Grade[to],cell=bearingShore>=0?bearingShore:grade>=2?map.Shore[to]:developmentCoast[to];if(cell<0)continue;
                    if(grade>=2&&map.Host[to]==polity&&a.TownAt[cell]>=0)continue;
                    double km=D.Angle(World.Surface.Cells[source].Position,World.Surface.Cells[cell].Position)*World.Recipe.RadiusKm;
                    if(km*1000>HistoricalTechnicalMigration.SeaRangeM(hc,s.Years,true)||ocean<.2&&km>700)continue;
                    double interest=grade>=2?.5+map.Opportunity[to]/1000.0:.7;
                    // A known, still-unsettled owned office gets funded retries,
                    // rather than being excluded from all subsequent planning.
                    if(grade>=2&&map.Host[to]==polity)interest+=.7;
                    if(grade>=2&&map.Host[to]!=polity&&s.Years-map.ReportedYear[to]<15)interest*=1.5;
                    if(grade>=2&&TerritorialAmbitionContains(s,polity,cell))interest*=1.4;
                    double score=interest*(.45+HistoryRoll("sea-interest-"+slot+"-"+k,polity,unchecked(s.Years+map.Voyages*7919)))/(1+km/10000);
                    if(!proposals.Any(x=>x.source==source&&x.region==to))proposals.Add((source,to,cell,score));
                }
            }
            // The office's own service ledger is already first-hand knowledge.
            // Address two fair pending entries, not every holding of an empire.
            var retries=a.Empires.Connections.PortSettlements?.Governments[polity];var pending=retries?.Pending;
            for(int k=0;pending!=null&&k<Math.Min(2,pending.Count);k++)
            {
                int postId=a.Empires.Connections.Geography.Version>=2?retries.TakeOne():pending.At((map.Voyages+k)%pending.Count);var post=a.Empires.Territories.Posts[postId];int region=CellPatch[post.Cell];
                if(!PendingColonialPort(s,polity,post)||map.ReviewAfter[region]>s.Years||map.Missions.Any(m=>m.Region==region&&m.Kind!=HistoricalSeaMission.Report))continue;
                int source=departures[(map.Voyages+k)%departures.Length];if(source==post.Cell)continue;
                if(a.Empires.Connections.Geography.Version>=2)
                {
                    // A funded office has an actual recorded parent service
                    // route. Prefer that owned shore over an unrelated random
                    // port on the other side of the empire/ocean basin.
                    int original=TerritorialNodeCell(s,post.ParentSource);
                    if(original>=0&&DiffusionShore(original)&&HistoricalCells.Polity(s,original)==polity)source=original;
                }
                ObserveGeographicCoast(s,map,post.Cell,3,polity);
                if(!proposals.Any(x=>x.source==source&&x.region==region))proposals.Add((source,region,post.Cell,3));
            }
            int checkedRoutes=0,dispatched=0;
            // Reserve exploration opportunities. Otherwise known nearby offices
            // always outscore unknown seas and discovery stalls indefinitely.
            bool explore=HistoryRoll("sea-exploration-policy",polity,unchecked(s.Years+map.Voyages*7919))<.35;
            foreach(var proposal in proposals.OrderByDescending(x=>(map.Grade[x.region]<2)==explore).ThenByDescending(x=>x.score).ThenBy(x=>x.region).ThenBy(x=>x.source))
            {
                if(checkedRoutes>=(RealmHistory(s)?12:3)||dispatched>=3||map.Missions.Count(m=>m.Kind!=HistoricalSeaMission.Report)>=capacity||map.Missions.Length>=8)break;
                if(map.Missions.Any(m=>m.Region==proposal.region&&m.Kind!=HistoricalSeaMission.Report))continue;
                checkedRoutes++;
                var route=new Route{A=CellPatch[proposal.source],B=proposal.region,CellA=proposal.source,CellB=proposal.cell,Sea=true,
                    DistanceM=(int)Math.Round(D.Angle(World.Surface.Cells[proposal.source].Position,World.Surface.Cells[proposal.cell].Position)*World.Recipe.RadiusKm*1000)};
                if(route.DistanceM>HistoricalTechnicalMigration.SeaRangeM(hc,s.Years,true)||ocean<.2&&route.DistanceM>700000)continue;
                // Validation uses real geometry/suppliers only after choosing a
                // search or destination. No target resource optimization here.
                if(!DiffusionSeaLane(route.CellA,route.CellB)||!InstitutionalRouteAllowed(s,route.CellA,route.CellB,p.CultureId)){SeaVentureObserver?.Invoke(polity,"physical-route");continue;}
                bool convoy=HistoricalSeaVentureEffects.CanColonize(map.Grade[proposal.region],ocean>=.2,route.DistanceM);
                int supplier;
                bool supplied=RealmHistory(s)?MaritimeVoyageLogistics(s,polity,route,a.TownAt[route.CellA],convoy?250:30,out supplier,out _):MaritimeCharterLogistics(s,polity,route,a.TownAt[route.CellA],out supplier,out _);
                if(!supplied){SeaVentureObserver?.Invoke(polity,"no-actual-supplier");continue;}
                long cost=(convoy?750:500)+route.DistanceM/4000;
                long food=HistoricalVoyageEffects.Provisioning(convoy?80:30,route.DistanceM*(convoy?1:2));
                var port=s.Society.Settlements[supplier];
                if(p.Treasury<cost*4||port.FoodReserve<food+port.Population/2){SeaVentureObserver?.Invoke(polity,"voyage-stock-or-cash");continue;}
                // Actual paid crew provisions and fitting-out, not free attempts.
                // Settler and army accounts remain at home until their separate
                // validated transaction; this is an expedition, not invented people.
                double steam=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.SteamPower,s.Years),modern=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.CombustionEngines,s.Years);
                long duration=HistoricalSeaVentureEffects.Duration(route.DistanceM,!convoy,ocean,steam,modern,HistoryRoll("sea-voyage-weather",polity,map.Voyages+1));
                if(now>SimulationTime.Maximum-duration)continue;
                p.Treasury-=cost;port.FoodReserve-=food;map.Voyages++;dispatched++;
                var mission=new HistoricalSeaMission{Kind=convoy?HistoricalSeaMission.Convoy:HistoricalSeaMission.Survey,Source=route.CellA,Target=route.CellB,Region=route.B,DistanceM=route.DistanceM,Departed=now,Due=now+duration,Investment=cost,ReportedYear=s.Years};
                map.Missions=map.Missions.Concat(new[]{mission}).ToArray();
                SeaVentureObserver?.Invoke(polity,convoy?"convoy-departed":"survey-departed");
                a.Empires.Connections.Governments[polity].LastExpeditionYear=s.Years;
            }
            map.NextPlan=now+(long)((map.Missions.Length>0?.5:5+15*HistoryRoll("sea-idle-review",polity,s.Years))*SimulationTime.Year);
            if(proposals.Count==0)SeaVentureObserver?.Invoke(polity,"no-proposals");
        }
        void AdvanceSeaMissions(PrehistoryState s,int polity)
        {
            var h=s.History;var a=h.Aggregates;var map=a.Empires.Connections.Geography.Governments[polity];map.ScheduledDue=0;
            var completed=map.Missions.Where(m=>m.Due<=SimulationTime.Seconds(s)).ToArray();map.Missions=map.Missions.Where(m=>m.Due>SimulationTime.Seconds(s)).ToArray();
            if(h.Polities[polity].EndedYear>=0){map.Missions=Array.Empty<HistoricalSeaMission>();return;}
            foreach(var mission in completed)
            {
                if(mission.Kind==HistoricalSeaMission.Report)
                {map.Record(mission.Region,mission.Grade,mission.ReportedYear,mission.Opportunity,mission.Host,mission.Target);continue;}
                var p=h.Polities[polity];var hc=h.Cultures[p.CultureId];
                if(HistoricalCells.Polity(s,mission.Source)!=polity){map.FailedVoyages++;continue;}
                // Better practical navigation reduces failed searches, not the
                // travel clock or the need for food, access and local adaptation.
                double skill=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.OceanNavigation,s.Years),steam=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.SteamPower,s.Years);
                double failure=(mission.Kind==HistoricalSeaMission.Survey?.18:.06)*(1-.7*skill)*(1-.6*steam);
                if(HistoryRoll("sea-return-outcome-"+mission.Region,polity,unchecked((int)(mission.Departed/SimulationTime.Day)))<failure){map.FailedVoyages++;continue;}
                if(mission.Kind==HistoricalSeaMission.Survey)
                {
                    // On-site reconnaissance may find a usable shore within a
                    // bounded local neighborhood. This is not a pre-departure
                    // oracle selecting ideal resources on an undiscovered coast.
                    var landing=RealmHistory(s)?ColonialLandingRoute(s,mission.Source,mission.Region,polity,mission.Target):ColonialTargetRoute(s,mission.Source,mission.Region,polity);
                    if(landing!=null){mission.Region=landing.B;mission.Target=landing.CellB;}
                    RegisterPaidSeaContact(s,polity,mission.Source,mission.Target);
                    int previous=map.Grade[mission.Region];ObserveGeographicCoast(s,map,mission.Target,3,polity);
                    map.Surveys++;RecordCoastChart(s,p.CultureId,mission.Region,p.CultureId);
                    if(previous<2)HistoryEvent(s,"survey-return",p.Name+" receives a returning expedition's account of "+ColonialRegionName(mission.Region)+". Its coast is now navigable on the state's working map; sovereignty and settlement are separate questions.",mission.Target,p.CultureId,polity,category:ChronicleCategory.Discoveries);
                    continue;
                }
                RegisterPaidSeaContact(s,polity,mission.Source,mission.Target);
                ObserveGeographicCoast(s,map,mission.Target,3,polity);
                var route=new Route{A=CellPatch[mission.Source],B=mission.Region,CellA=mission.Source,CellB=mission.Target,DistanceM=mission.DistanceM,Sea=true};
                int before=a.Empires.Territories.PostAt[route.CellB],townBefore=a.TownAt[route.CellB];
                CommitTerritorialSeaCharter(s,route,p.CultureId,1,true,polity);
                bool founded=false;
                if(HistoricalCells.Polity(s,route.CellB)==polity)
                {
                    founded=CommitLivingColonialPort(s,polity,route,1,true);ObserveGeographicCoast(s,map,route.CellB,3,polity);
                    if(before<0&&a.Empires.Territories.PostAt[route.CellB]>=0||townBefore<0&&a.TownAt[route.CellB]>=0)map.Landfalls++;
                }
                SeaVentureObserver?.Invoke(polity,founded?"port-founded":HistoricalCells.Polity(s,route.CellB)==polity?"office-without-port":"no-political-access");
                if(!founded)map.ReviewAfter=map.ReviewAfter.With(mission.Region,s.Years+3+(int)(7*HistoryRoll("sea-unready-review",polity,map.Voyages)));
            }
            // Active routes can support several subsequent voyages within years.
            // No additional 12/40-year founding lottery after a paid arrival.
            PlanSeaVentures(s,polity);ScheduleSeaMissions(s,polity);
        }
        void RegisterPaidSeaContact(PrehistoryState s,int polity,int source,int target)
        {
            if(s.History.Aggregates.Empires.Connections.Geography.Version<2)return;
            int culture=s.History.Polities[polity].CultureId,local=HistoricalCells.Culture(s,target);
            // An actual successful voyage creates witnessed local contact. It
            // offers knowledge through the existing physical/adoption machinery;
            // it neither installs a civilization nor grants usable technology.
            if(local>=0&&local!=culture)LivingCultureContactAt(s,culture,local,source,target);
        }
        void ShareGeographicReports(PrehistoryState s,int polity)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var map=a.Empires.Connections.Geography.Governments[polity];
            if(map.Places.Count==0)return;var contacts=s.Society.Cultures[p.CultureId].Contacts;if(contacts.Length==0)return;
            int start=(int)(HistoryRoll("geographic-trade-partner",polity,s.Years)*contacts.Length);
            for(int k=0;k<Math.Min(2,contacts.Length);k++)
            {
                int culture=contacts[(start+k)%contacts.Length],peer=a.Resolve(a.Living.CivicOrigins[culture],h);
                if(peer<0){var cities=a.Cultures[culture].Cities;for(int j=0;j<Math.Min(2,cities.Count);j++){peer=a.Resolve(s.Society.Settlements[cities.At(j)].PolityId,h);if(peer>=0)break;}}
                if(peer<0||peer==polity)continue;var relation=AggregateRelation(s,polity,peer);if(!relation.Trade||relation.WarSince>=0||relation.Trust<0)continue;
                var receiver=a.Empires.Connections.Geography.Governments[peer];if(receiver.Missions.Length>=8||receiver.Missions.Count(m=>m.Kind==HistoricalSeaMission.Report)>=4)continue;
                int region=map.Places.At((int)(HistoryRoll("geographic-trade-report-"+peer,polity,s.Years)*map.Places.Count));
                int grade=map.Grade[region]>=2&&relation.Trust>=40?2:1;
                if(receiver.Grade[region]>=grade||receiver.Missions.Any(m=>m.Kind==HistoricalSeaMission.Report&&m.Region==region))continue;
                // Reports cross an actual trading relationship. Copied directions
                // never become the receiver's own verified voyage. Arrival is
                // dated, and forwards retain the observation's original date.
                int from=s.Society.Settlements[p.CapitalId].Cell,to=s.Society.Settlements[h.Polities[peer].CapitalId].Cell;
                double distance=D.Angle(World.Surface.Cells[from].Position,World.Surface.Cells[to].Position)*World.Recipe.RadiusKm;
                long now=SimulationTime.Seconds(s),due=now+(long)((30+distance/30)*SimulationTime.Day);
                due=Math.Max(due,receiver.ScheduledDue);
                receiver.Missions=receiver.Missions.Concat(new[]{new HistoricalSeaMission{Kind=HistoricalSeaMission.Report,Source=from,Target=map.Shore[region],Region=region,Reporter=polity,
                    Grade=grade,Opportunity=grade>=2?map.Opportunity[region]:0,Host=grade>=2?map.Host[region]:-1,ReportedYear=map.ReportedYear[region],Departed=now,Due=due}}).ToArray();
                ScheduleSeaMissions(s,peer);
            }
        }
    }
}
