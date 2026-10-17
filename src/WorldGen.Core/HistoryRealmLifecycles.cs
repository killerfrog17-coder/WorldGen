using System;
using System.Linq;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalRealmAccount
    {
        public int FoundedYear,RegimeSince,CrisisUntil,CrisisSeverity,RegimeChanges,RegionalSeparations,RecognitionCursor;
        public long RecognizedAreaKm2,RecognitionUpkeep;
        public HistoricalRealmAccount Copy()=>(HistoricalRealmAccount)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalRealmLifecycles
    {
        internal const int CurrentRevision=31;
        public int Version=CurrentRevision,SinceYear;
        [System.Runtime.Serialization.OptionalField] public HistoricalRealmClaimRoutes ClaimRoutes;
        [System.Runtime.Serialization.OptionalField] public HistoricalRealmClaimants Claimants;
        public HistoricalRealmAccount[] Governments=Array.Empty<HistoricalRealmAccount>();
        public HistoricalRealmLifecycles Copy()=>new HistoricalRealmLifecycles{Version=Version,SinceYear=SinceYear,ClaimRoutes=ClaimRoutes?.Copy(),Claimants=Claimants?.Copy(),Governments=Governments.Select(g=>g.Copy()).ToArray()};
        public static int Founded(PrehistoryState s,int polity)=>s.History.Aggregates?.Empires?.Connections?.Realms?.Governments[polity].FoundedYear??s.History.Polities[polity].FoundedYear;
    }
    public static class HistoricalRealmEffects
    {
        public static int RulerTenure(double roll)=>3+(int)(47*D.Clamp(roll)*D.Clamp(roll));
        public static double CourtFailureHazard(double bureaucracy)=>.005*Math.Pow(1-.98*D.Clamp(bureaucracy),2);
        public static int LegitimacyTarget(double integration,double serviceRatio,int capture,int conquest,int exhaustion,int representation)
            =>(int)Math.Round(1000*Math.Max(.15,Math.Min(.85,.48+.18*D.Clamp(integration)+.12*D.Clamp(serviceRatio)+.12*D.Clamp(representation/1000.0)
                -.18*D.Clamp(capture/1000.0)-.15*D.Clamp(conquest/1000.0)-.25*D.Clamp(exhaustion/1000.0))));
        public static double CohesionTarget(double bureaucracy,int strain,int exhaustion,double crisis)
            =>Math.Max(50,Math.Min(950,650+200*D.Clamp(bureaucracy)-.6*strain-.2*exhaustion-200*D.Clamp(crisis)));
        // Scenario priors: personal conquest and weak communications matter,
        // not a country's birthday or an expiry timer for every empire.
        public static double SuccessionHazard(int debt,int capture,int exhaustion,double serviceLoad,double personal,double integration,bool representative)
        {
            double pressure=.55*D.Clamp(debt/800.0)+.25*D.Clamp(capture/800.0)+.3*D.Clamp(exhaustion/800.0)
                +.25*D.Clamp(serviceLoad-1)+.35*D.Clamp(personal);
            return D.Q(Math.Min(.85,Math.Max(0,pressure-.10)*(.85-.55*D.Clamp(integration))*(representative?.18:1)),1000000000);
        }
        public static double ProvincialRupture(double shock,double distance,double inclusion,bool foreign,double representation)
            =>D.Q(.22*D.Clamp(shock)*(.25+.75*D.Clamp(distance/2))*(.35+.65*(1-D.Clamp(inclusion)))*(foreign?1.25:1)*(1-.65*D.Clamp(representation)),1000000000);
    }
    public sealed partial class Prehistory
    {
        static bool RealmHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms!=null;
        static bool RevisedRealmHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=2;
        public PrehistoryState UpgradeRealmLifecycles(PrehistoryState source)
        {
            Validate(source);if(!LivingWorldHistory(source))throw new ArgumentException("Realm lifecycles require history32.");
            if(RevisedRealmHistory(source)&&source.History.Aggregates.Empires.Connections.Realms.Version>=HistoricalRealmLifecycles.CurrentRevision&&source.History.Aggregates.Empires.Connections.Realms.ClaimRoutes?.SourceHeads!=null&&source.History.Aggregates.Empires.Connections.Realms.Claimants!=null&&source.History.Aggregates.Empires.Connections.SeaService!=null&&source.History.Aggregates.Empires.Connections.Religion?.Version>=2&&source.History.Aggregates.Empires.Connections.Military!=null&&source.History.Aggregates.Empires.Connections.ServiceRecovery?.Version>=2&&source.History.Aggregates.Empires.Connections.LocalSeeds!=null&&source.History.Aggregates.Empires.Connections.PreparedPlots?.Version>=2&&source.History.Aggregates.Empires.Connections.Religion.LayAssociations!=null&&source.History.Aggregates.Empires.Connections.Religion.Traditions!=null)return source;
            int preceding=source.History.Aggregates.Empires.Connections.Realms?.Version??0;
            var s=source.Copy();InitializeRealmLifecycles(s);InitializeRealmClaimRoutes(s);s.History.Aggregates.Empires.Connections.Realms.Version=HistoricalRealmLifecycles.CurrentRevision;UpgradeReligiousNetworkRules(s);InitializeMilitaryOperations(s);UpgradeTerritorialServiceRecovery(s);InitializeRealmClaimants(s);InitializeSeaService(s);if(preceding<8)InitializeLocalReligiousCityClocks(s);InitializeLocalSeedlots(s);InitializePreparedPlots(s);InitializeLayReligiousAssociations(s);InitializeStructuredReligions(s);s.Seal();return s;
        }
        void InitializeRealmLifecycles(PrehistoryState s)
        {
            if(RealmHistory(s))return;var h=s.History;var records=new HistoricalRealmAccount[h.Polities.Length];
            // One-time actor migration. Recognized constitutional successors are
            // continuity, unlike conquest or an independent provincial court.
            for(int i=0;i<records.Length;i++)records[i]=new HistoricalRealmAccount{FoundedYear=h.Polities[i].FoundedYear,RegimeSince=h.Polities[i].FoundedYear};
            var diplomacy=h.International;
            if(diplomacy!=null)for(int i=0;i<records.Length;i++)
            {int next=diplomacy.Accounts[i].ContinuitySuccessor;if(next>i)records[next].FoundedYear=Math.Min(records[next].FoundedYear,records[i].FoundedYear);}
            h.Aggregates.Empires.Connections.Realms=new HistoricalRealmLifecycles{SinceYear=s.Years,Governments=records};
            InitializeRealmClaimRoutes(s);
            foreach(var title in h.Aggregates.Empires.Connections.Titles)
            {int owner=h.Aggregates.Resolve(title.Holder,h);if(owner>=0)ChangeRealmTitleAccount(s,owner,title.AreaKm2,1);}
            InitializeReligiousNetworks(s);
            InitializeMilitaryOperations(s);
            InitializeTerritorialServiceRecovery(s);
            if(h.Aggregates.Empires.Connections.Realms.Version>=6){InitializeRealmClaimants(s);InitializeSeaService(s);}
            if(h.Aggregates.Empires.Connections.Realms.Version>=14)InitializeLocalSeedlots(s);
            if(h.Aggregates.Empires.Connections.Realms.Version>=16)InitializePreparedPlots(s);
            InitializeLayReligiousAssociations(s);
            InitializeStructuredReligions(s);
        }
        void AppendRealmGovernment(PrehistoryState s,int predecessor)
        {
            if(!RealmHistory(s))return;var r=s.History.Aggregates.Empires.Connections.Realms;int id=r.Governments.Length;
            var next=predecessor>=0?r.Governments[predecessor].Copy():new HistoricalRealmAccount{FoundedYear=s.History.Polities[id].FoundedYear,RegimeSince=s.Years};
            r.Governments=r.Governments.Concat(new[]{next}).ToArray();
            AppendMilitaryGovernment(s);
        }
        double RealmIntegration(PrehistoryState s,int polity)
        {
            var h=s.History;var hc=h.Cultures[h.Polities[polity].CultureId];var ds=h.Dynamics.States[polity];var policy=h.Aggregates.Communities.Governments[polity];
            return D.Clamp(.20*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Writing,s.Years)+.20*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Roads,s.Years)
                +.20*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Telegraph,s.Years)+.20*policy.RepresentationMilli/1000.0+.20*ds.PublicWorks/1000.0);
        }
        double RealmBureaucracy(PrehistoryState s,int polity)
        {
            if(!RealmHistory(s))return 0;var h=s.History;var hc=h.Cultures[h.Polities[polity].CultureId];
            return D.Clamp(.5*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Telegraph,s.Years)+.3*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Radio,s.Years)
                +.2*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Electronics,s.Years))*(.5+.5*h.Dynamics.States[polity].PublicWorks/1000.0);
        }
        void RealmSuccession(PrehistoryState s,int polity,HistoricalLeader former,HistoricalLeader incoming)
        {
            if(!RealmHistory(s)||former==null)return;var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var ds=h.Dynamics.States[polity];var cycle=ds.Cycle;
            var record=a.Empires.Connections.Realms.Governments[polity];var policy=a.Communities.Governments[polity];
            bool representative=policy.RepresentationMilli>=650;double integration=RealmIntegration(s,polity);
            double extent=Math.Sqrt(Math.Max(1,HistoricalImperialHierarchy.AreaKm2(s,polity))/Math.PI)/Math.Max(50,a.Polities[polity].ReachKm);
            double personal=D.Clamp((former.Renown+Math.Max(0,former.Military-650)*.5)/500.0)*D.Clamp(cycle.ConquestDebt/300.0);
            double chance=HistoricalRealmEffects.SuccessionHazard(cycle.ConquestDebt,cycle.EliteCapture,p.Exhaustion,extent,personal,integration,representative);
            double urbanContest=UrbanRealmSuccessionRisk(s,polity,incoming);
            if(urbanContest>0)chance=1-(1-chance)*(1-urbanContest);
            // Impersonal fiscal/communication institutions survive the person
            // at the top. Modern territorial defeat or colonial revolt remains
            // possible; leadership mortality is no longer a general dissolution lottery.
            chance*=1-.92*RealmBureaucracy(s,polity)*(.5+.5*integration);
            bool disputed=HistoryRoll("realm-contested-succession-1",incoming.Id,s.Years)<chance;
            if(!disputed)return;
            record.RegimeSince=s.Years;record.RegimeChanges++;ds.HouseSerial++;
            p.Legitimacy=Math.Max(100,p.Legitimacy-100);p.Cohesion=Math.Max(100,p.Cohesion-60);
            bool regional=a.Polities[polity].Cities.Count>=3||a.Empires.Governments[polity].SubjectCount>0;
            if(regional)
            {
                record.CrisisSeverity=(int)Math.Round(1000*D.Clamp(.30+.55*chance+.25*personal+.20*extent-.30*integration));
                record.CrisisUntil=s.Years+8+(int)(18*HistoryRoll("realm-crisis-duration-1",incoming.Id,s.Years));
                ds.DisputedUntil=record.CrisisUntil;p.Cohesion=Math.Max(100,p.Cohesion-record.CrisisSeverity/3);
                string message=urbanContest>0?p.Name+" enters a contested succession after "+former.Name+". Substantial urban centers, provincial elites and the incoming court negotiate competing claims to authority; the country is not refounded by this change of ruler.":p.Name+" enters a contested imperial succession after "+former.Name+". Provincial commanders and constituent courts dispute the new center's authority; the country is not refounded by this change of ruler.";
                HistoryEvent(s,"realm-succession-crisis",message,s.Society.Settlements[p.CapitalId].Cell,p.CultureId,polity,category:ChronicleCategory.Politics);
            }
            else HistoryEvent(s,"regime-change",p.Name+" replaces its ruling court after a contested succession. Its state identity, founding date, territory and inhabitants continue.",s.Society.Settlements[p.CapitalId].Cell,p.CultureId,polity,category:ChronicleCategory.Politics);
        }
        double RealmCrisis(PrehistoryState s,int polity)
        {
            if(!RealmHistory(s)||polity<0)return 0;var r=s.History.Aggregates.Empires.Connections.Realms.Governments[polity];
            return r.CrisisUntil>s.Years?r.CrisisSeverity/1000.0:0;
        }
        bool ContestFailingRealmCourt(PrehistoryState s,int polity)
        {
            if(!RealmHistory(s)||s.History.Aggregates.Polities[polity].Cities.Count<2)return false;
            if(RealmCrisis(s,polity)>0)return true;
            var h=s.History;var p=h.Polities[polity];var ds=h.Dynamics.States[polity];var record=h.Aggregates.Empires.Connections.Realms.Governments[polity];
            record.RegimeSince=s.Years;record.RegimeChanges++;record.CrisisSeverity=900;
            record.CrisisUntil=s.Years+12+(int)(14*HistoryRoll("realm-fiscal-contest-duration",polity,s.Years));ds.DisputedUntil=record.CrisisUntil;
            HistoryEvent(s,"realm-succession-crisis",p.Name+"'s central court loses authority amid fiscal and elite conflict. Its existing regional centers contest control rather than all losing their governments at once.",s.Society.Settlements[p.CapitalId].Cell,p.CultureId,polity,category:ChronicleCategory.Politics);
            return true;
        }
        void AdvanceRealmLegitimacy(PrehistoryState s,int polity,int elapsed,int previous)
        {
            if(!RealmHistory(s))return;var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var account=a.Polities[polity];var cycle=h.Dynamics.States[polity].Cycle;
            int target=HistoricalRealmEffects.LegitimacyTarget(RealmIntegration(s,polity),account.Receipts/Math.Max(1.0,account.ServiceCost),cycle.EliteCapture,cycle.ConquestDebt,p.Exhaustion,a.Communities.Governments[polity].RepresentationMilli);
            target-=(int)(150*RealmCrisis(s,polity));p.Legitimacy=Math.Max(100,Math.Min(950,(int)Math.Round(previous+(target-previous)*(1-Math.Exp(-elapsed/45.0)))));
        }
        void AdvanceRealmCohesion(PrehistoryState s,int polity,int elapsed,int previous)
        {
            if(!RealmHistory(s))return;var h=s.History;var p=h.Polities[polity];
            double target=HistoricalRealmEffects.CohesionTarget(RealmBureaucracy(s,polity),h.Dynamics.States[polity].Strain,p.Exhaustion,RealmCrisis(s,polity));
            p.Cohesion=(int)Math.Round(previous+(target-previous)*Math.Min(.5,elapsed/50.0));
        }
        bool TryRealmRegionalSeparation(PrehistoryState s,int owner,int city,int elapsed,double distance)
        {
            double shock=Math.Max(RealmCrisis(s,owner),RealmClaimantShock(s,owner,city));if(shock<=0)return false;
            var h=s.History;var a=h.Aggregates;var p=h.Polities[owner];var town=s.Society.Settlements[city];var local=a.Communities.Cities[city];
            if(city==p.CapitalId||town.Population<600||!a.Cities[city].Supported||s.Years-town.FoundedYear<50)return false;
            double hazard=HistoricalRealmEffects.ProvincialRupture(shock,distance,local.InclusionMilli/1000.0,town.CultureId!=p.CultureId,a.Communities.Governments[owner].RepresentationMilli/1000.0);
            if(HistoryRoll("realm-provincial-contest-1",city,s.Years)>=1-Math.Exp(-hazard*elapsed))return false;
            int rival=-1;double best=double.MaxValue;
            // Existing physical city contacts only. Nearby compatible courts
            // coalesce, rather than minting one independent flag per city.
            foreach(int peer in a.Cities[city].Neighbors)
            {
                var candidate=s.Society.Settlements[peer];int state=a.Resolve(candidate.PolityId,h);
                if(state<0||state==owner||h.Polities[state].ParentId!=owner||h.Polities[state].CultureId!=town.CultureId||s.Years-h.Polities[state].FoundedYear>50)continue;
                double km=LivingTravelKm(s,candidate.Cell,town.Cell,town.CultureId);if(km<best&&km<Math.Max(180,a.Polities[state].ReachKm*2.0)){best=km;rival=state;}
            }
            if(rival<0){rival=h.Polities.Length;FoundAggregateState(s,city,owner);}
            else {ClaimAggregatePolity(s,town.Cell,rival);JoinRealmClaimantGovernment(s,owner,city,rival);HistoryEvent(s,"regional-realignment",town.Name+" joins the neighboring successor court of "+h.Polities[rival].Name+" amid the disputed succession of "+p.Name+".",town.Cell,town.CultureId,rival,owner,city,category:ChronicleCategory.Politics);}
            ClaimDevelopmentProvince(s,city,owner);a.Empires.Connections.Realms.Governments[owner].RegionalSeparations++;
            var relation=AggregateRelation(s,owner,rival);relation.TruceUntil=s.Years+2;relation.Trust=-60;
            return true;
        }
        void ValidateRealmLifecycles(PrehistoryState s)
        {
            var r=s.History.Aggregates.Empires.Connections.Realms;if(r==null)return;
            if(!LivingWorldHistory(s)||r.Version<1||r.Version>HistoricalRealmLifecycles.CurrentRevision||r.SinceYear<0||r.SinceYear>s.Years||r.Governments?.Length!=s.History.Polities.Length)throw new ArgumentException("Invalid realm lifecycle extension.");
            if(r.Version>=2&&s.History.Aggregates.Empires.Connections.Religion==null)throw new ArgumentException("Revised realm institutions require their saved religious communities.");
            ValidateReligiousNetworks(s);
            if(r.Version>=3&&s.History.Aggregates.Empires.Connections.Military==null)throw new ArgumentException("Revised field operations require their saved manpower accounts.");
            ValidateMilitaryOperations(s);
            if(r.Version>=4&&(s.History.Aggregates.Empires.Connections.Religion?.Version!=2||s.History.Aggregates.Empires.Connections.ServiceRecovery==null))throw new ArgumentException("Revised control requires local institutional religion and saved service recovery.");
            ValidateTerritorialServiceRecovery(s);
            if(r.Version>=6&&(r.Claimants==null||s.History.Aggregates.Empires.Connections.SeaService==null))throw new ArgumentException("Revised local claimants and maritime service require their saved ledgers.");
            ValidateRealmClaimants(s);ValidateSeaService(s);
            if(r.Version>=14&&s.History.Aggregates.Empires.Connections.LocalSeeds==null)throw new ArgumentException("Local cultivation requires saved physical seedlots.");
            ValidateLocalSeedlots(s);
            if(r.Version>=16&&s.History.Aggregates.Empires.Connections.PreparedPlots==null)throw new ArgumentException("Prepared cultivation requires saved local plot accounts.");
            ValidatePreparedPlots(s);
            ValidateLayReligiousAssociations(s);
            ValidateStructuredReligions(s);
            foreach(var g in r.Governments)if(g==null||g.FoundedYear<0||g.FoundedYear>s.Years||g.RegimeSince<g.FoundedYear||g.RegimeSince>s.Years||g.CrisisUntil<0||g.CrisisUntil>s.Years+100||g.CrisisSeverity<0||g.CrisisSeverity>1000||g.RegimeChanges<0||g.RegionalSeparations<0||g.RecognizedAreaKm2<0||g.RecognitionUpkeep<0||g.RecognitionCursor<0)throw new ArgumentException("Invalid realm continuity/crisis account.");
            var areas=new long[r.Governments.Length];var costs=new long[areas.Length];
            foreach(var title in s.History.Aggregates.Empires.Connections.Titles)
            {int owner=s.History.Aggregates.Resolve(title.Holder,s.History);if(owner>=0){areas[owner]+=title.AreaKm2;costs[owner]+=RealmTitleUpkeep(title.AreaKm2);}}
            for(int i=0;i<areas.Length;i++)if(s.History.Polities[i].EndedYear<0&&(areas[i]!=r.Governments[i].RecognizedAreaKm2||costs[i]!=r.Governments[i].RecognitionUpkeep))throw new ArgumentException("Recognized sovereignty service ledger.");
            ValidateRealmClaimRoutes(s);
        }
    }
}
