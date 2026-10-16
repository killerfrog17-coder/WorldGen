using System;
using System.Linq;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalInternationalFinanceAccount
    {
        public long TradeAssessment,PeaceAssessment,TradeReserveDue,PeaceReserveDue;
        public HistoricalInternationalFinanceAccount Copy()=>(HistoricalInternationalFinanceAccount)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalInternationalOperatingBudget
    {
        public long AnnualAssessments,UnfundedWork,LastOperatingCharge;
        public int AuthorityWindowYears,AuthorityApplied;
        public HistoricalInternationalOperatingBudget Copy()=>(HistoricalInternationalOperatingBudget)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalInternationalFinances
    {
        public HistoricalInternationalFinanceAccount[] Accounts=Array.Empty<HistoricalInternationalFinanceAccount>();
        public HistoricalInternationalOperatingBudget[] Organizations=Array.Empty<HistoricalInternationalOperatingBudget>();
        public HistoricalInternationalFinances Copy()=>new HistoricalInternationalFinances{Accounts=Accounts.Select(x=>x.Copy()).ToArray(),Organizations=Organizations.Select(x=>x.Copy()).ToArray()};
    }
    public static class HistoricalInternationalBudgetEffects
    {
        public const long Maximum=1000000000000L;
        // An abstract capacity-to-pay proxy, not a literal UN tax rate or GDP.
        public static long Assessment(long receipts)=>receipts<=0?0:Math.Max(1,Math.Min(1000000000L,receipts/1000));
        public static long OperatingCost(long assessments,int members)=>Math.Max(1+Math.Max(0,members)/4,assessments);
        public static long ReserveTarget(long annual)=>(long)Math.Min(Maximum,(decimal)Math.Max(0,annual)*3);
        public static long Contribution(long assessment,int elapsed,long treasury,long reserve,long annual,long unfunded)
        {
            decimal needed=Math.Max(0,(decimal)ReserveTarget(annual)+Math.Max(0,unfunded)-Math.Max(0,reserve));
            return (long)Math.Min(Maximum,Math.Min(needed,Math.Min(Math.Max(0,treasury)/20,(decimal)Math.Max(0,assessment)*Math.Max(0,elapsed))));
        }
        public static double Coverage(long unfunded,long lastCharge,long annual)
            =>D.Clamp(1-Math.Max(0,unfunded)/(double)Math.Max(1,Math.Max(lastCharge,annual)));
    }
    public sealed partial class Prehistory
    {
        public PrehistoryState UpgradeInternationalOperatingBudgets(PrehistoryState source)
        {
            Validate(source);if(!LivingWorldHistory(source))throw new ArgumentException("Operating-budget upgrade requires history32.");
            if(source.History.International?.Finances!=null)return source;
            var s=source.Copy();InitializeInternationalFinances(s);s.Seal();return s;
        }
        void InitializeInternationalFinances(PrehistoryState s)
        {
            var system=s.History.International;if(system==null||system.Finances!=null)return;
            var finances=new HistoricalInternationalFinances{Accounts=system.Accounts.Select(_=>new HistoricalInternationalFinanceAccount()).ToArray(),Organizations=system.Organizations.Select(_=>new HistoricalInternationalOperatingBudget()).ToArray()};system.Finances=finances;
            // One-time explicit upgrade only; ordinary updates change two cached
            // member assessments and at most two organizational sums in O(1).
            for(int id=0;id<system.Accounts.Length;id++)foreach(int body in new[]{system.Accounts[id].TradeOrganization,system.Accounts[id].PeaceOrganization})
                if(body>=0&&system.Organizations[body].EndedYear<0){RefreshInternationalAssessment(s,id,body);if(system.Organizations[body].Kind==HistoricalOrganizationKind.TradeLeague)system.Accounts[id].LastTradeDuesYear=s.Years;else system.Accounts[id].LastPeaceDuesYear=s.Years;}
            foreach(var body in system.Organizations)if(body.EndedYear<0)body.LastActionYear=s.Years;
        }
        void RefreshInternationalAssessment(PrehistoryState s,int polity,int organization)
        {
            var system=s.History.International;var f=system.Finances;if(f==null)return;bool trade=system.Organizations[organization].Kind==HistoricalOrganizationKind.TradeLeague;
            var account=f.Accounts[polity];long old=trade?account.TradeAssessment:account.PeaceAssessment,next=HistoricalInternationalBudgetEffects.Assessment(s.History.Aggregates.Polities[polity].Receipts);
            long priorCapital=trade?account.TradeReserveDue:account.PeaceReserveDue,capital=next<old?Math.Min(priorCapital,HistoricalInternationalBudgetEffects.ReserveTarget(next)):Math.Min(HistoricalInternationalBudgetEffects.Maximum,priorCapital+HistoricalInternationalBudgetEffects.ReserveTarget(next-old));
            f.Organizations[organization].AnnualAssessments+=next-old;if(trade){account.TradeAssessment=next;account.TradeReserveDue=capital;}else{account.PeaceAssessment=next;account.PeaceReserveDue=capital;}
        }
        void RemoveInternationalAssessment(PrehistoryState s,int polity,int organization)
        {
            var system=s.History.International;var f=system.Finances;if(f==null)return;bool trade=system.Organizations[organization].Kind==HistoricalOrganizationKind.TradeLeague;
            var account=f.Accounts[polity];f.Organizations[organization].AnnualAssessments-=trade?account.TradeAssessment:account.PeaceAssessment;if(trade)account.TradeAssessment=account.TradeReserveDue=0;else account.PeaceAssessment=account.PeaceReserveDue=0;
        }
        void CreditInternationalReservePledge(PrehistoryState s,int polity,int organization,long paid)
        {var system=s.History.International;if(system.Finances==null)return;var account=system.Finances.Accounts[polity];bool trade=system.Organizations[organization].Kind==HistoricalOrganizationKind.TradeLeague;if(trade)account.TradeReserveDue=Math.Max(0,account.TradeReserveDue-paid);else account.PeaceReserveDue=Math.Max(0,account.PeaceReserveDue-paid);}
        long InternationalAnnualBudget(PrehistoryState s,int organization)
        {var system=s.History.International;return HistoricalInternationalBudgetEffects.OperatingCost(system.Finances.Organizations[organization].AnnualAssessments,system.Organizations[organization].Members.Count);}
        long InternationalAccessionDeposit(PrehistoryState s,int polity,int organization)
        {
            var system=s.History.International;long own=HistoricalInternationalBudgetEffects.Assessment(s.History.Aggregates.Polities[polity].Receipts);
            long annual=HistoricalInternationalBudgetEffects.OperatingCost(system.Finances.Organizations[organization].AnnualAssessments+own,system.Organizations[organization].Members.Count+1);
            long missing=Math.Max(0,HistoricalInternationalBudgetEffects.ReserveTarget(annual)-system.Organizations[organization].Reserve);
            return Math.Min(HistoricalInternationalBudgetEffects.ReserveTarget(own),missing);
        }
        void ValidateInternationalFinances(PrehistoryState s)
        {
            var system=s.History.International;var f=system.Finances;if(f==null)return;
            void Require(bool condition){if(!condition)throw new ArgumentException("Invalid international operating budget.");}
            Require(f.Accounts.Length==system.Accounts.Length&&f.Organizations.Length==system.Organizations.Length);
            var sums=new long[f.Organizations.Length];
            // Explicit integrity validation may inspect all actors; events do not.
            for(int id=0;id<f.Accounts.Length;id++)
            {
                var a=f.Accounts[id];Require(a!=null&&a.TradeAssessment>=0&&a.TradeAssessment<=1000000000L&&a.PeaceAssessment>=0&&a.PeaceAssessment<=1000000000L&&a.TradeReserveDue>=0&&a.TradeReserveDue<=HistoricalInternationalBudgetEffects.Maximum&&a.PeaceReserveDue>=0&&a.PeaceReserveDue<=HistoricalInternationalBudgetEffects.Maximum);
                var member=system.Accounts[id];foreach(int body in new[]{member.TradeOrganization,member.PeaceOrganization})if(body>=0&&system.Organizations[body].EndedYear<0)sums[body]+=system.Organizations[body].Kind==HistoricalOrganizationKind.TradeLeague?a.TradeAssessment:a.PeaceAssessment;
            }
            for(int id=0;id<f.Organizations.Length;id++){var b=f.Organizations[id];Require(b!=null&&b.AnnualAssessments>=0&&b.UnfundedWork>=0&&b.UnfundedWork<=HistoricalInternationalBudgetEffects.Maximum&&b.LastOperatingCharge>=0&&b.LastOperatingCharge<=HistoricalInternationalBudgetEffects.Maximum&&b.AuthorityWindowYears>=0&&b.AuthorityApplied>=-b.AuthorityWindowYears&&b.AuthorityApplied<=5);if(system.Organizations[id].EndedYear<0)Require(b.AnnualAssessments==sums[id]);}
        }
        void SettleInternationalOperatingWork(PrehistoryState s,int organization)
        {
            var system=s.History.International;var body=system.Organizations[organization];var budget=system.Finances.Organizations[organization];int years=s.Years-body.LastActionYear;if(years<=0)return;
            body.LastActionYear=s.Years;long charge=(long)Math.Min(HistoricalInternationalBudgetEffects.Maximum,(decimal)InternationalAnnualBudget(s,organization)*years);
            budget.LastOperatingCharge=charge;budget.UnfundedWork=Math.Min(HistoricalInternationalBudgetEffects.Maximum,budget.UnfundedWork+charge);budget.AuthorityWindowYears=years;budget.AuthorityApplied=0;
        }
        void FundInternationalOperatingWork(PrehistoryState s,int organization)
        {
            var system=s.History.International;var body=system.Organizations[organization];var budget=system.Finances.Organizations[organization];long paid=Math.Min(body.Reserve,budget.UnfundedWork);
            body.Reserve-=paid;body.ServiceSpending+=paid;budget.UnfundedWork-=paid;
        }
        void ReconcileInternationalOperatingAuthority(PrehistoryState s,int organization)
        {
            var system=s.History.International;var body=system.Organizations[organization];var budget=system.Finances.Organizations[organization];int years=budget.AuthorityWindowYears;if(years<=0)return;
            double coverage=HistoricalInternationalBudgetEffects.Coverage(budget.UnfundedWork,budget.LastOperatingCharge,InternationalAnnualBudget(s,organization));
            int target=(int)Math.Round(coverage*(Math.Min(5,years)+years)-years),before=body.Authority;
            body.Authority=Math.Max(0,Math.Min(1000,body.Authority+target-budget.AuthorityApplied));budget.AuthorityApplied+=body.Authority-before;
        }
        double InternationalFunding(PrehistoryState s,int organization)
        {
            var system=s.History.International;if(system.Finances==null)return system.Organizations[organization].Reserve>0?1:0;
            var body=system.Organizations[organization];var budget=system.Finances.Organizations[organization];long annual=InternationalAnnualBudget(s,organization);
            // Already-paid current work remains real service even if it used
            // the final cash unit. As time passes, reserve must back otherwise
            // unfunded new work; no stale authority score pays future employees.
            if(budget.LastOperatingCharge==0)return D.Q(D.Clamp(body.Reserve/(double)Math.Max(1,annual)),1000000);
            double pending=(double)annual*Math.Max(0,s.Years-body.LastActionYear),unfunded=budget.UnfundedWork+Math.Max(0,pending-body.Reserve);
            return D.Q(D.Clamp(1-unfunded/Math.Max(1,budget.LastOperatingCharge+pending)),1000000);
        }
        void PayInternationalOperatingDues(PrehistoryState s,int polity,int organization)
        {
            var h=s.History;var system=h.International;var body=system.Organizations[organization];var account=system.Accounts[polity];var finance=system.Finances.Accounts[polity];bool trade=body.Kind==HistoricalOrganizationKind.TradeLeague;
            int elapsed=s.Years-(trade?account.LastTradeDuesYear:account.LastPeaceDuesYear);if(elapsed<=0)return;
            long prior=trade?finance.TradeAssessment:finance.PeaceAssessment;SettleInternationalOperatingWork(s,organization);FundInternationalOperatingWork(s,organization);RefreshInternationalAssessment(s,polity,organization);
            var budget=system.Finances.Organizations[organization];long annual=InternationalAnnualBudget(s,organization);
            long exposure=Math.Max(0,h.Polities[polity].Treasury)/20,expected=HistoricalInternationalBudgetEffects.Contribution(prior,elapsed,long.MaxValue,body.Reserve,annual,budget.UnfundedWork);
            long due=HistoricalInternationalBudgetEffects.Contribution(prior,elapsed,h.Polities[polity].Treasury,body.Reserve,annual,budget.UnfundedWork);
            h.Polities[polity].Treasury-=due;body.Reserve+=due;body.Contributions+=due;FundInternationalOperatingWork(s,organization);
            long pledge=trade?finance.TradeReserveDue:finance.PeaceReserveDue,missing=Math.Max(0,HistoricalInternationalBudgetEffects.ReserveTarget(annual)+budget.UnfundedWork-body.Reserve);
            long capital=Math.Min(pledge,Math.Min(Math.Max(0,exposure-due),missing));h.Polities[polity].Treasury-=capital;body.Reserve+=capital;body.Contributions+=capital;CreditInternationalReservePledge(s,polity,organization,capital);FundInternationalOperatingWork(s,organization);
            if(body.Reserve>=HistoricalInternationalBudgetEffects.ReserveTarget(annual)&&budget.UnfundedWork==0)CreditInternationalReservePledge(s,polity,organization,long.MaxValue);
            bool arrears=due<expected/2;
            if(trade){account.LastTradeDuesYear=s.Years;account.TradeArrearsYears=arrears?account.TradeArrearsYears+elapsed:Math.Max(0,account.TradeArrearsYears-elapsed);}
            else{account.LastPeaceDuesYear=s.Years;account.PeaceArrearsYears=arrears?account.PeaceArrearsYears+elapsed:Math.Max(0,account.PeaceArrearsYears-elapsed);}
            if((trade?account.TradeArrearsYears:account.PeaceArrearsYears)>25){LeaveInternationalOrganization(s,polity,organization,"Persistent unpaid dues suspend its treaty privileges.");return;}
            ReconcileInternationalOperatingAuthority(s,organization);
        }
    }
}
