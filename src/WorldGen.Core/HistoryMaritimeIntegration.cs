using System;
namespace WorldGen.Core
{
    // Funded port networks, not a blanket age bonus or free colonial people.
    // Numerical coefficients are game priors; neither consent nor conquest is
    // inferred from a society being unrepresented by a state actor.
    public static class HistoricalMaritimeIntegrationEffects
    {
        public static bool AdministrativeShipping(double unsupportedKm,double writing,double navigation,int institution)
            =>unsupportedKm<=800||writing>=.2&&navigation>=.2&&institution>=350;
        public static bool CivilianTerritory(int growingDays,bool permanentIce,long residents,double strategicValue)
            =>!permanentIce&&(growingDays>=60||residents>=200||growingDays>=20&&strategicValue>=15);
        public static int ChartBudget(bool oceanGoing,bool globalShipping)=>globalShipping?256:oceanGoing?128:48;
        public static int PostBudget(long urban,double administrativeCapacity,double communications)
            =>(int)Math.Round(Math.Max(8,Math.Min(2048,16+Math.Sqrt(Math.Max(0,urban)/2500.0)*12*Math.Max(1,administrativeCapacity)*Math.Sqrt(Math.Max(1,communications)))));
        public static long CouncilInvestment(long residents,int distanceM)
            =>Math.Max(2500,Math.Min(2000000,residents/3+2500+Math.Max(0,distanceM)/1500));
        public static double ConsentChance(int elapsed,int diplomacy,int legitimacy,int representation,bool familiar,double localVulnerability)
        {
            double trust=(familiar?1:.15)*(.2+.8*D.Clamp(diplomacy/1000.0))*(.2+.8*D.Clamp(legitimacy/1000.0));
            double terms=.2+.8*D.Clamp(representation/1000.0);
            return D.Q(1-Math.Exp(-Math.Max(0,elapsed)*.015*trust*terms*(.2+.8*D.Clamp(localVulnerability))),1000000000);
        }
        public static double ExpeditionChance(int elapsed,double militaryRatio,int exhaustion,int ambition)
        {
            if(militaryRatio<2||exhaustion>=500||ambition<650)return 0;
            double advantage=D.Clamp((militaryRatio-1)/5),readiness=D.Clamp(1-exhaustion/600.0);
            return D.Q(1-Math.Exp(-Math.Max(0,elapsed)*.02*advantage*readiness*D.Clamp(ambition/1000.0)),1000000000);
        }
    }
    public sealed partial class Prehistory
    {
        bool LivingTerritorialInterest(PrehistoryState s,int polity,int cell)
        {
            var habitat=World.Ecology.Habitat[cell];int town=s.History.Aggregates.TownAt[cell];
            long residents=town>=0?s.Society.Settlements[town].Population:AggregateRuralCellPopulation(s,cell);
            return HistoricalMaritimeIntegrationEffects.CivilianTerritory(habitat.GrowingDays,habitat.Biome==Biome.Ice,residents,LivingStrategicResourceValue(s,polity,cell));
        }
        bool MaritimeCharterLogistics(PrehistoryState s,int polity,Route route,int preferred,out int port,out long provisions)
            =>MaritimeVoyageLogistics(s,polity,route,preferred,250,out port,out provisions);
        bool MaritimeVoyageLogistics(PrehistoryState s,int polity,Route route,int preferred,int crew,out int port,out long provisions)
        {
            port=-1;provisions=0;var h=s.History;var a=h.Aggregates;var hc=h.Cultures[h.Polities[polity].CultureId];
            double unsupported=InstitutionalUnsupportedWaterKm(route.CellA,route.CellB,out _);
            // Ocean-going oral navigation is possible; an overseas fiscal state
            // and a reliable protected shipping venture need additional deployed
            // administration. This does not restrict ordinary human voyaging.
            if(!HistoricalMaritimeIntegrationEffects.AdministrativeShipping(unsupported,HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Writing,s.Years),
                HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Navigation,s.Years),hc.Institution))return false;
            var cities=a.Polities[polity].Cities;int offset=cities.Count==0?0:(int)(HistoryRoll("maritime-provisioning-base",polity,s.Years)*cities.Count);
            bool Consider(int city)
            {
                if(city<0||city>=a.Cities.Length)return false;var town=s.Society.Settlements[city];
                if(a.Resolve(town.PolityId,h)!=polity||town.Population<500||!a.Cities[city].Supported||a.Living.Cities[city].AbandonedYear>=0)return false;
                double supplyKm=D.Angle(World.Surface.Cells[town.Cell].Position,World.Surface.Cells[route.CellA].Position)*World.Recipe.RadiusKm;
                if(town.Cell!=route.CellA)
                {
                    // A coarse inland city can provision its real serviced port.
                    // It still pays actual food and inland transport; merely
                    // colouring an unserviced shore does not create shipping.
                    bool coastal=DiffusionShore(town.Cell)&&supplyKm*1000<=HistoricalTechnicalMigration.SeaRangeM(hc,s.Years,true)&&DiffusionSeaLane(town.Cell,route.CellA)&&InstitutionalRouteAllowed(s,town.Cell,route.CellA,h.Polities[polity].CultureId);
                    double inland=LivingTravelKm(s,town.Cell,route.CellA,h.Polities[polity].CultureId);
                    int office=a.Empires.Territories.Sources[route.CellA];
                    bool serviced=HistoricalCells.Polity(s,route.CellA)==polity&&DiffusionShore(route.CellA)&&TerritorialNodeLive(s,polity,office)
                        &&World.Landmass[town.Cell]>=0&&World.Landmass[town.Cell]==World.Landmass[route.CellA]
                        &&inland<=TerritorialServiceRadius(s,polity,route.CellA)*1.5;
                    if(!coastal&&!serviced)return false;
                }
                long food=HistoricalVoyageEffects.Provisioning(crew,(int)Math.Min(int.MaxValue,route.DistanceM+supplyKm*1000));
                if(town.FoodReserve<food+town.Population*.2)return false;
                return true;
            }
            if(preferred>=0&&Consider(preferred))port=preferred;
            if(port<0&&RealmHistory(s))
            {
                int source=a.Empires.Territories.Sources[route.CellA];
                for(int depth=0;source!= -1&&depth<8;depth++)
                {
                    if(source>=0){if(Consider(source))port=source;break;}
                    source=a.Empires.Territories.Posts[-source-2].ParentSource;
                }
            }
            for(int k=0;port<0&&k<Math.Min(8,cities.Count);k++){int candidate=cities.At((offset+k)%cities.Count);if(Consider(candidate))port=candidate;}
            if(port<0)return false;
            double leg=D.Angle(World.Surface.Cells[s.Society.Settlements[port].Cell].Position,World.Surface.Cells[route.CellA].Position)*World.Recipe.RadiusKm;
            provisions=HistoricalVoyageEffects.Provisioning(crew,(int)Math.Min(int.MaxValue,route.DistanceM+leg*1000));return true;
        }
        bool TryMaritimeStateExpedition(PrehistoryState s,int polity,int defender,Route route,int elapsed)
            =>CommitMaritimeStateExpedition(s,polity,defender,route,elapsed,false);
        bool CommitMaritimeStateExpedition(PrehistoryState s,int polity,int defender,Route route,int elapsed,bool organized)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var other=h.Polities[defender];var hc=h.Cultures[p.CultureId];
            if(p.Cohesion<600||p.Exhaustion>=500||p.Army<500||!HistoricalTechnologies.Has(hc,HistoricalTechnology.OceanNavigation))return false;
            if(HistoricalImperialHierarchy.Sovereign(s,polity)==HistoricalImperialHierarchy.Sovereign(s,defender))return false;
            var relation=AggregateRelation(s,polity,defender);if(relation.WarSince>=0||relation.TruceUntil>=s.Years||relation.Alliance)return false;
            var leader=h.Dynamics.Leaders[h.Dynamics.States[polity].LeaderId];
            double expedition=p.Army*.20*HistoricalModernEffects.Military(hc,s.Years);
            double resistance=Math.Max(50,other.Army+a.Empires.Governments[defender].SubjectArmy*.2)*HistoricalModernEffects.Military(h.Cultures[other.CultureId],s.Years);
            long investment=Math.Max(5000,p.Army/2+route.DistanceM/500);
            if(p.Treasury<investment*3||a.Polities[polity].Receipts<=a.Polities[polity].ServiceCost+investment/100)return false;
            double chance=HistoricalMaritimeIntegrationEffects.ExpeditionChance(elapsed,expedition/resistance,p.Exhaustion,leader.Ambition);
            if(chance<=0||!organized&&HistoryRoll("maritime-state-expedition-"+defender,polity,s.Years)>=chance)return false;
            // Use the ordinary supplied campaign, casualties, local resistance,
            // conquests and peace machinery. A chart does not annex a country.
            int before=h.Dynamics.Campaigns.Length;BeginAggregateCampaign(s,polity,defender,relation,route.CellB);
            if(h.Dynamics.Campaigns.Length==before)return false;
            p.Treasury-=investment;p.Exhaustion=Math.Min(1000,p.Exhaustion+35);
            HistoryEvent(s,"overseas-expedition",p.Name+" funds a naval expedition against "+other.Name+" through its charted port route. The result depends on supplied armies, local resistance and the campaign, not an automatic colonial claim.",route.CellB,p.CultureId,polity,defender,category:ChronicleCategory.Politics);
            return true;
        }
        Route ColonialTargetRoute(PrehistoryState s,int source,int region,int polity)
            =>ColonialLandingRoute(s,source,region,polity,-1);
        Route ColonialLandingRoute(PrehistoryState s,int source,int region,int polity,int actualShore)
        {
            int representative=actualShore>=0?actualShore:developmentCoast[region];if(representative<0)return null;
            int best=-1;double bestScore=double.NegativeInfinity;
            void Consider(int cell)
            {
                if(CellPatch[cell]<0||!DiffusionShore(cell))return;
                int owner=HistoricalCells.Polity(s,cell);
                if(owner>=0&&HistoricalImperialHierarchy.Sovereign(s,owner)==HistoricalImperialHierarchy.Sovereign(s,polity))return;
                if(RespectRecognizedClaim(s,polity,cell))return;
                int town=s.History.Aggregates.TownAt[cell];
                if(owner<0&&s.History.Aggregates.Empires.Territories.PostAt[cell]>=0)return;
                double km=D.Angle(World.Surface.Cells[source].Position,World.Surface.Cells[cell].Position)*World.Recipe.RadiusKm;
                var hc=s.History.Cultures[s.History.Polities[polity].CultureId];
                if(km*1000>HistoricalTechnicalMigration.SeaRangeM(hc,s.Years,true)||!DiffusionSeaLane(source,cell)||!InstitutionalRouteAllowed(s,source,cell,s.History.Polities[polity].CultureId))return;
                double score=ProvincialPhysicalYield(s,s.History.Polities[polity].CultureId,cell)*3-km/20000.0;
                if(town>=0&&s.History.Aggregates.Cities[town].Supported&&s.Society.Settlements[town].Population>=500)score+=.5;
                if(owner>=0)
                {
                    var p=s.History.Polities[polity];var opponent=s.History.Polities[owner];
                    double strength=Math.Max(1,p.Army)*HistoricalModernEffects.Military(hc,s.Years);
                    double defense=Math.Max(1,opponent.Army)*HistoricalModernEffects.Military(s.History.Cultures[opponent.CultureId],s.Years);
                    score-=.4+Math.Min(3,defense/strength);
                }
                if(score>bestScore||score==bestScore&&(best<0||cell<best)){best=cell;bestScore=score;}
            }
            Consider(representative);
            var neighbors=World.Surface.Cells[representative].Neighbors;
            for(int k=0;k<Math.Min(8,neighbors.Length);k++)Consider(neighbors[k]);
            // The existing spatial town index returns a bounded local portfolio;
            // do not walk every port, settlement or coastline on this continent.
            int sampled=0;foreach(int town in NearbyLivingTowns(s,representative,250))
            {Consider(s.Society.Settlements[town].Cell);if(++sampled>=8)break;}
            if(best<0)return null;
            return new Route{A=CellPatch[source],B=CellPatch[best],CellA=source,CellB=best,Sea=true,
                DistanceM=(int)Math.Round(D.Angle(World.Surface.Cells[source].Position,World.Surface.Cells[best].Position)*World.Recipe.RadiusKm*1000)};
        }
        bool TryMaritimeCouncil(PrehistoryState s,int polity,int city,Route route,int elapsed)
            =>CommitMaritimeCouncil(s,polity,city,route,elapsed,false);
        bool CommitMaritimeCouncil(PrehistoryState s,int polity,int city,Route route,int elapsed,bool organized)
        {
            if(!LivingWorldHistory(s))return false;
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var town=s.Society.Settlements[city];var hc=h.Cultures[p.CultureId];
            if(a.Resolve(town.PolityId,h)>=0||town.Population<500||!a.Cities[city].Supported||a.Living.Cities[city].AbandonedYear>=0||p.Cohesion<550||p.Exhaustion>=500)return false;
            var account=a.Polities[polity];long investment=HistoricalMaritimeIntegrationEffects.CouncilInvestment(town.Population,route.DistanceM);
            if(p.Treasury<investment*2||account.Receipts<=account.ServiceCost+investment/100)return false;
            int ancestor=polity;
            for(int depth=0;depth<HistoricalImperialHierarchy.MaximumDepth;depth++)
            {
                int parent=HistoricalImperialHierarchy.Patron(s,ancestor);
                if(parent<0)break;
                // Reserve one bounded allegiance level for the new local court;
                // never create an accidental independent state after a failed pact.
                if(depth>=HistoricalImperialHierarchy.MaximumDepth-2)return false;
                ancestor=parent;
            }
            var leader=h.Dynamics.Leaders[h.Dynamics.States[polity].LeaderId];
            int lastContact=EnvironmentalContactYear(s,town.CultureId,p.CultureId);
            bool familiar=town.CultureId==p.CultureId||lastContact>=0&&lastContact>=s.Years-100;
            double consent=HistoricalMaritimeIntegrationEffects.ConsentChance(elapsed,leader.Diplomacy,p.Legitimacy,a.Communities.Governments[polity].RepresentationMilli,familiar,1-town.Stability/1000.0);
            bool imposed=false;
            if(HistoryRoll("maritime-local-council-"+city,polity,s.Years)>=consent)
            {
                double expedition=p.Army*.15*HistoricalModernEffects.Military(hc,s.Years);
                double resistance=Math.Max(20,(town.Population+a.Cities[city].Rural*.15)*.025)*HistoricalModernEffects.Military(h.Cultures[town.CultureId],s.Years);
                double chance=HistoricalMaritimeIntegrationEffects.ExpeditionChance(elapsed,expedition/resistance,p.Exhaustion,leader.Ambition);
                // An explicitly funded expedition has already passed the
                // dispatch decision. Resolve local resistance, rather than
                // applying another decades-long decision lottery at landfall.
                double outcome=organized&&chance>0?D.Clamp(.5+.35*(1-resistance/Math.Max(1,expedition))):chance;
                if(HistoryRoll("maritime-coercive-expedition-"+city,polity,s.Years)>=outcome)return false;
                imposed=true;p.Exhaustion=Math.Min(1000,p.Exhaustion+80);p.Army=Math.Max(0,p.Army-(int)Math.Min(p.Army/10,resistance*.1));
            }
            p.Treasury-=investment;
            h.Towns[city].CivicTeacherPolityId=polity;h.Towns[city].CivicExampleExposure=Math.Max(700,h.Towns[city].CivicExampleExposure);
            var continuity=a.Empires.Connections.Cultures[town.CultureId];
            if(continuity.CivilizationRoot<0)continuity.CivilizationRoot=HistoricalRecognizedClaims.Civilization(s,p.CultureId);
            FoundAggregateStateKind(s,city,polity,"chartered-institutions");int client=town.PolityId;
            h.Polities[client].Treasury=Math.Min(1000000000,h.Polities[client].Treasury+investment/2);
            h.Polities[client].Government=imposed?"A local council under a coercive overseas protectorate":"A local council under a negotiated overseas charter";
            a.Empires.Connections.Governments[client].Sponsor=polity;
            SetImperialPatron(s,client,polity,imposed,imposed?"A funded naval expedition compels tribute while retaining the settled port's people and local administration.":"A funded protection and trading agreement supports existing local administration; no colonists or population are created.");
            LivingCultureContactAt(s,p.CultureId,town.CultureId,route.CellA,route.CellB);
            HistoryEvent(s,imposed?"colonial-protectorate":"colonial-council",p.Name+(imposed?" imposes a protectorate over ":" funds a dependent port council at ")+town.Name+". Existing residents and their local culture remain; the maritime sponsor pays "+investment+" account units.",town.Cell,town.CultureId,polity,client,town:city,category:ChronicleCategory.Politics);
            return true;
        }
    }
}
