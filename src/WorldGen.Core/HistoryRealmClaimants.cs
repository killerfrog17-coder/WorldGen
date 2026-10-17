using System;
using System.Linq;
namespace WorldGen.Core
{
    // One latest/recent claimant record per participating city, not a record
    // for every city, province or terrain cell. Earlier episodes stay in the
    // ordinary named event archive. This is a political coalition, not another
    // population, treasury, military unit or automatically independent state.
    [Serializable] public sealed class HistoricalRealmLocalClaimant
    {
        public int City,Owner,DeclaredYear,LastYear,CrisisSinceYear,SupportMilli;
        public int ResolvedYear=-1,SuccessorPolity=-1,LeaderSinceYear,LeaderUntilYear;
        public string Name="",Outcome="";
        public HistoricalRealmLocalClaimant Copy()=>(HistoricalRealmLocalClaimant)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalRealmClaimants
    {
        public int Version=1,SinceYear;
        public HistoricalCellTree At=new HistoricalCellTree(33554432);
        public HistoricalRealmLocalClaimant[] Records=Array.Empty<HistoricalRealmLocalClaimant>();
        public HistoricalRealmClaimants Copy()=>new HistoricalRealmClaimants{Version=Version,SinceYear=SinceYear,At=At,Records=Records.Select(x=>x.Copy()).ToArray()};
    }
    public static class HistoricalRealmClaimantHistory
    {
        public static bool Enabled(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=6&&s.History.Aggregates.Empires.Connections.Realms.Claimants!=null;
        public static HistoricalRealmLocalClaimant At(PrehistoryState s,int city)
        {
            if(!Enabled(s)||city<0||city>=s.Society.Settlements.Length)return null;
            var r=s.History.Aggregates.Empires.Connections.Realms.Claimants;int index=r.At[city];return index>=0&&index<r.Records.Length?r.Records[index]:null;
        }
        public static bool Active(PrehistoryState s,HistoricalRealmLocalClaimant c)
            =>c!=null&&Enabled(s)&&c.City>=0&&c.City<s.Society.Settlements.Length&&c.ResolvedYear<0&&c.SupportMilli>=180&&c.Owner>=0&&c.Owner<s.History.Polities.Length&&s.History.Polities[c.Owner].EndedYear<0
                &&s.History.Aggregates.Living.Cities[c.City].AbandonedYear<0&&s.Society.Settlements[c.City].Population>0&&HistoricalCells.Polity(s,s.Society.Settlements[c.City].Cell)==c.Owner;
    }
    public static class HistoricalRealmClaimantEffects
    {
        public static double EmergenceHazard(double crisis,double inclusion,double centralization,double recentConquest)
            =>D.Q(.025*D.Clamp(crisis)*(.3+.7*(1-D.Clamp(inclusion)))*(.5+.5*D.Clamp(centralization))*(.5+.5*D.Clamp(recentConquest)),1000000000);
        public static int Support(int previous,int elapsed,double crisis,double inclusion,double recentConquest,double deficit,double exhaustion,double representation,double reliableService,bool viable)
        {
            if(elapsed<=0)return Math.Max(0,Math.Min(1000,previous));
            double target=viable?D.Clamp(.05+.65*D.Clamp(crisis)+.5*(1-D.Clamp(inclusion))+.3*D.Clamp(recentConquest)+.25*D.Clamp(deficit)+.2*D.Clamp(exhaustion)
                -.30*D.Clamp(reliableService)-.25*D.Clamp(inclusion)-.1*D.Clamp(representation)):0;
            return (int)Math.Round(Math.Max(0,Math.Min(1000,previous+(1000*target-previous)*(1-Math.Exp(-elapsed/(viable?30.0:12.0))))));
        }
        public static double LocalShock(int support)=>D.Q(.8*D.Clamp((support-200)/650.0),1000000000);
        public static int CaptureRenown(long residents)=>residents<=0?0:residents<600?5:residents<5000?15:25;
    }
    public sealed partial class Prehistory
    {
        void InitializeRealmClaimants(PrehistoryState s)
        {
            var r=s.History.Aggregates.Empires.Connections.Realms;if(r.Claimants==null)r.Claimants=new HistoricalRealmClaimants{SinceYear=s.Years};
        }
        double RealmClaimantShock(PrehistoryState s,int owner,int city)
        {
            var c=HistoricalRealmClaimantHistory.At(s,city);return c!=null&&c.Owner==owner&&HistoricalRealmClaimantHistory.Active(s,c)?HistoricalRealmClaimantEffects.LocalShock(c.SupportMilli):0;
        }
        void NameRealmClaimant(PrehistoryState s,HistoricalRealmLocalClaimant c)
        {
            int culture=s.Society.Settlements[c.City].CultureId;int salt=unchecked(c.City*7919+s.Years);
            c.Name=LanguageHistory(s)?LanguageName(s,culture,LanguageNameKind.Person,salt):HistoricalName("realm-local-claimant",salt);
            c.LeaderSinceYear=s.Years;c.LeaderUntilYear=s.Years+HistoricalRealmEffects.RulerTenure(HistoryRoll("realm-local-claimant-tenure",c.City,s.Years));
        }
        void ResolveRealmClaimant(PrehistoryState s,HistoricalRealmLocalClaimant c,string outcome,string kind)
        {
            if(c.ResolvedYear>=0)return;c.ResolvedYear=s.Years;c.LastYear=s.Years;c.Outcome=outcome;
            var town=s.Society.Settlements[c.City];HistoryEvent(s,kind,c.Name+"'s local coalition at "+town.Name+" "+outcome+".",town.Cell,town.CultureId,c.Owner,town:c.City,category:ChronicleCategory.Politics);
        }
        void TransferRealmClaimantCity(PrehistoryState s,int city,int old,int polity)
        {
            if(old==polity)return;var c=HistoricalRealmClaimantHistory.At(s,city);
            if(c!=null&&c.ResolvedYear<0&&c.Owner!=polity)ResolveRealmClaimant(s,c,polity<0?"loses its inhabited governing center":"loses control of its center to another government","realm-claimant-displaced");
        }
        void AdvanceRealmClaimant(PrehistoryState s,int owner,int city,int elapsed)
        {
            if(!HistoricalRealmClaimantHistory.Enabled(s))return;
            var h=s.History;var a=h.Aggregates;var town=s.Society.Settlements[city];var realm=a.Empires.Connections.Realms;var r=realm.Claimants;var c=HistoricalRealmClaimantHistory.At(s,city);
            if(c!=null&&c.ResolvedYear<0&&c.Owner!=owner)ResolveRealmClaimant(s,c,"loses control of its center to another government","realm-claimant-displaced");
            var p=h.Polities[owner];var local=a.Communities.Cities[city];double crisis=RealmCrisis(s,owner);
            bool viable=a.Cities[city].Supported&&town.Population>=600&&a.Living.Cities[city].AbandonedYear<0&&a.Living.Cities[city].DeclineYears==0&&town.Stability>=200;
            bool capital=city==p.CapitalId;int cause=realm.Governments[owner].RegimeSince;
            double debt=(h.Dynamics.States[owner].Cycle?.ConquestDebt??0)/1000.0*(s.Years-local.SinceYear<75?1:.35);
            double inclusion=local.InclusionMilli/1000.0;var policy=a.Communities.Governments[owner];
            if((c==null||c.ResolvedYear>=0)&&!capital&&viable&&s.Years-town.FoundedYear>=50&&crisis>0
                &&(c==null||c.Owner!=owner||c.CrisisSinceYear!=cause))
            {
                double hazard=HistoricalRealmClaimantEffects.EmergenceHazard(crisis,inclusion,policy.CentralizationMilli/1000.0,debt);
                int exposed=Math.Min(elapsed,Math.Max(1,s.Years-cause+1));
                if(HistoryRoll("realm-local-claimant-emergence",city,s.Years)<1-Math.Exp(-hazard*exposed))
                {
                    if(c==null)
                    {
                        c=new HistoricalRealmLocalClaimant{City=city};int index=r.Records.Length;r.Records=r.Records.Concat(new[]{c}).ToArray();r.At=r.At.With(city,index);
                    }
                    c.Owner=owner;c.DeclaredYear=c.LastYear=s.Years;c.CrisisSinceYear=cause;c.SupportMilli=(int)Math.Round(350+350*crisis+150*(1-inclusion));c.ResolvedYear=c.SuccessorPolity=-1;c.Outcome="";NameRealmClaimant(s,c);
                    HistoryEvent(s,"realm-local-claimant",c.Name+" gathers a local coalition at "+town.Name+" during the disputed succession of "+p.Name+". Its supporters dispute the center's authority and negotiate local obligations.",town.Cell,town.CultureId,owner,town:city,category:ChronicleCategory.Politics);
                }
            }
            if(c==null||c.ResolvedYear>=0||c.Owner!=owner)return;
            // A relocated capital does not automatically buy a local faction's
            // loyalty. Existing support still needs actual reconciliation;
            // the ordinary regional process separately excludes the capital.
            int years=s.Years-c.LastYear;if(years<=0)return;c.LastYear=s.Years;
            var account=a.Polities[owner];var ds=h.Dynamics.States[owner];double deficit=D.Clamp((account.ServiceCost-account.Receipts)/(double)Math.Max(1,account.ServiceCost));
            double distance=LivingTravelKm(s,s.Society.Settlements[p.CapitalId].Cell,town.Cell,town.CultureId)/Math.Max(50,HistoricalProvincialEffects.LocalServiceKm(h.Cultures[p.CultureId],s.Years,ds.PublicWorks));
            double reliable=D.Clamp(account.Receipts/(double)Math.Max(1,account.ServiceCost))*(.5+.5*ds.PublicWorks/1000.0)/(1+distance*distance);
            c.SupportMilli=HistoricalRealmClaimantEffects.Support(c.SupportMilli,years,crisis,inclusion,debt,deficit,p.Exhaustion/1000.0,policy.RepresentationMilli/1000.0,reliable,viable);
            if(c.SupportMilli<180){ResolveRealmClaimant(s,c,viable?"reconciles as inclusion and reliable service restore the center's authority":"disperses after losing the resources for a sustained local challenge","realm-claimant-reconciled");return;}
            if(s.Years>=c.LeaderUntilYear)
            {
                NameRealmClaimant(s,c);HistoryEvent(s,"realm-claimant-succession",c.Name+" takes up the continuing local claim at "+town.Name+". The dispute continues under new leadership.",town.Cell,town.CultureId,owner,town:city,category:ChronicleCategory.Politics);
            }
        }
        void InheritRealmClaimantGovernment(PrehistoryState s,int parent,int city,int successor)
        {
            var c=HistoricalRealmClaimantHistory.At(s,city);if(c==null||c.Owner!=parent||c.SupportMilli<180||(c.ResolvedYear>=0&&c.ResolvedYear!=s.Years))return;
            var h=s.History;var state=h.Polities[successor];if(state.FoundedYear!=s.Years||state.CapitalId!=city)return;
            c.ResolvedYear=c.LastYear=s.Years;c.SuccessorPolity=successor;c.Outcome="establishes an independent successor court";
            var leader=h.Dynamics.Leaders[h.Dynamics.States[successor].LeaderId];
            if(s.Years>=c.LeaderUntilYear)NameRealmClaimant(s,c);
            leader.Name=c.Name;leader.Selection="Acclamation by the existing local coalition after the contested central succession";state.Ruler=leader.Name;SealAggregateLeader(s,leader.Id);
            var town=s.Society.Settlements[city];HistoryEvent(s,"realm-claimant-government",c.Name+"'s coalition at "+town.Name+" establishes the successor court of "+state.Name+". Existing local institutions and a share of the former court's resources support the new government.",town.Cell,town.CultureId,successor,parent,city,category:ChronicleCategory.Politics);
        }
        void JoinRealmClaimantGovernment(PrehistoryState s,int parent,int city,int successor)
        {
            var c=HistoricalRealmClaimantHistory.At(s,city);if(c==null||c.Owner!=parent||c.SupportMilli<180||(c.ResolvedYear>=0&&c.ResolvedYear!=s.Years))return;
            c.ResolvedYear=c.LastYear=s.Years;c.SuccessorPolity=successor;c.Outcome="joins a neighboring successor court";
            var town=s.Society.Settlements[city];HistoryEvent(s,"realm-claimant-government",c.Name+"'s coalition at "+town.Name+" joins the neighboring court of "+s.History.Polities[successor].Name+" amid the disputed succession of "+s.History.Polities[parent].Name+".",town.Cell,town.CultureId,successor,parent,city,category:ChronicleCategory.Politics);
        }
        void CreditRealmTownCapture(PrehistoryState s,int winner,int city)
        {
            if(!HistoricalRealmClaimantHistory.Enabled(s)||city<0||city>=s.Society.Settlements.Length)return;
            var h=s.History;var town=s.Society.Settlements[city];if(town.Population<=0||h.Aggregates.Living.Cities[city].AbandonedYear>=0||HistoricalCells.Polity(s,town.Cell)!=winner)return;
            var leader=h.Dynamics.Leaders[h.Dynamics.States[winner].LeaderId];int gain=HistoricalRealmClaimantEffects.CaptureRenown(town.Population);
            if(leader.Victories<int.MaxValue)leader.Victories++;if(leader.Renown<1000)leader.Renown=Math.Min(1000,leader.Renown+gain);SealAggregateLeader(s,leader.Id);
        }
        void ValidateRealmClaimants(PrehistoryState s)
        {
            var r=s.History.Aggregates.Empires.Connections.Realms?.Claimants;if(r==null)return;
            if(r.Version!=1||r.SinceYear<0||r.SinceYear>s.Years||r.At==null||r.Records==null||r.At.Length!=33554432)throw new ArgumentException("Invalid local realm claimants.");
            for(int index=0;index<r.Records.Length;index++)
            {
                var c=r.Records[index];if(c==null||c.City<0||c.City>=s.Society.Settlements.Length||r.At[c.City]!=index||c.Owner<0||c.Owner>=s.History.Polities.Length||c.DeclaredYear<r.SinceYear||c.DeclaredYear>s.Years||c.LastYear<c.DeclaredYear||c.LastYear>s.Years||c.CrisisSinceYear<0||c.CrisisSinceYear>c.DeclaredYear||c.SupportMilli<0||c.SupportMilli>1000||c.ResolvedYear< -1||c.ResolvedYear>s.Years||c.ResolvedYear>=0&&c.ResolvedYear<c.DeclaredYear||c.SuccessorPolity< -1||c.SuccessorPolity>=s.History.Polities.Length||c.LeaderSinceYear<c.DeclaredYear||c.LeaderSinceYear>c.LastYear||c.LeaderUntilYear<c.LeaderSinceYear||c.LeaderUntilYear>c.LeaderSinceYear+50||string.IsNullOrWhiteSpace(c.Name)||c.Outcome==null)throw new ArgumentException("Invalid saved local claimant record.");
            }
            if(!r.At.AllPresentValues((city,index)=>index>=0&&index<r.Records.Length&&r.Records[index].City==city))throw new ArgumentException("Invalid local claimant sparse index.");
        }
    }
}
