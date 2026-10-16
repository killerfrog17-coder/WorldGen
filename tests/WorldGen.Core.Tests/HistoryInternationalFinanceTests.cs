using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;
public static class HistoryInternationalFinanceTests
{
    static object Call(Prehistory e,string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(e,values);
    public static void Run(Action<bool,string> check)
    {
        check(HistoricalInternationalBudgetEffects.Assessment(10000)==10&&HistoricalInternationalBudgetEffects.Assessment(0)==0,"Operating assessments use member capacity to pay rather than a universal cash levy");
        check(HistoricalInternationalBudgetEffects.Assessment(long.MaxValue)==1000000000L,"Extreme manual receipts do not overflow the bounded assessment");
        check(HistoricalInternationalBudgetEffects.OperatingCost(2000,10)==2000&&HistoricalInternationalBudgetEffects.OperatingCost(0,8)==3,"A staffed common institution scales with its actual assessments but keeps modest office overhead");
        check(HistoricalInternationalBudgetEffects.ReserveTarget(2000)==6000,"Normal reserve funding targets three annual operating budgets, not a trillion-unit hoard");
        check(HistoricalInternationalBudgetEffects.Contribution(10,5,100000,60,20,0)==0,"A fully funded operating reserve waives new dues instead of charging or penalizing its member");
        check(HistoricalInternationalBudgetEffects.Contribution(10,5,100000,0,20,100)==50,"Accrued operating work permits actual catch-up funding without breaching the free reserve target");
        check(HistoricalInternationalBudgetEffects.Contribution(10,5,0,0,20,0)==0&&HistoricalInternationalBudgetEffects.Contribution(10,5,100,0,20,0)==5,"Actual treasury and fiscal exposure cap contributions");
        check(HistoricalInternationalBudgetEffects.Coverage(20,40,20)==.5&&HistoricalInternationalBudgetEffects.Coverage(100,40,20)==0,"Unfunded operating work reduces actual coverage, not merely a descriptive ledger number");
        check(HistoricalInternationalBudgetEffects.Contribution(long.MaxValue,int.MaxValue,long.MaxValue,0,long.MaxValue,long.MaxValue)<=HistoricalInternationalBudgetEffects.Maximum,"Extreme supplied contribution requests remain overflow-safe");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;string original=source.ComputeHash();var e=new Prehistory(world);var s=e.UpgradeInternationalOperatingBudgets(source);var h=s.History;var system=h.International!;
        check(source.ComputeHash()==original&&source.History.International!.Finances==null&&system.Finances!=null,"An old-null finance snapshot remains readable and unchanged; only explicit upgrade creates its new ledger");
        check(system.Organizations.Select(o=>o.Reserve).SequenceEqual(source.History.International.Organizations.Select(o=>o.Reserve)),"Explicit upgrade does not delete or refund existing accumulated common funds");
        e.Validate(s);var copy=s.Copy();copy.History.International!.Finances!.Accounts[0].TradeAssessment++;
        check(copy.History.International.Finances.Accounts[0].TradeAssessment!=system.Finances!.Accounts[0].TradeAssessment,"Saved operating-finance copies isolate mutable account records");
        int organization=Array.FindIndex(system.Organizations,b=>b.EndedYear<0&&b.Members.Count>=2);if(organization<0)throw new Exception("Finance fixture needs an actual existing multilateral organization.");
        int[] members=Enumerable.Range(0,system.Organizations[organization].Members.Count).Select(k=>system.Organizations[organization].Members.At(k)).ToArray();
        // Deliberate fiscal stress fixture. These fixed actor loops are tests,
        // never an ordinary state event or new simulation route.
        foreach(int id in members){h.Polities[id].Treasury=1000000000;h.Aggregates.Polities[id].Receipts=10000;Call(e,"RefreshInternationalAssessment",s,id,organization);}
        var body=system.Organizations[organization];var budget=system.Finances.Organizations[organization];long annual=(long)Call(e,"InternationalAnnualBudget",s,organization);
        body.Reserve=body.Contributions=annual*3;body.ServiceSpending=body.MediationSpending=0;body.LastActionYear=s.Years;body.Authority=500;budget.UnfundedWork=budget.LastOperatingCharge=0;
        foreach(int id in members)Call(e,"CreditInternationalReservePledge",s,id,organization,long.MaxValue);
        int start=s.Years;long cash=members.Sum(id=>h.Polities[id].Treasury)+body.Reserve;var cadence=s.Copy();
        for(int year=1;year<=2000;year++)
        {s.Years=start+year;foreach(int id in members)Call(e,"PayInternationalDues",s,id,organization);if(body.Reserve>annual*3)throw new Exception("Fresh normal contributions exceeded three years of operating reserve.");}
        check(body.ServiceSpending>=annual*1999&&body.Reserve<=annual*3&&budget.UnfundedWork==0,"Two thousand years of funded operation pay real staff/services and retain only a bounded reserve");
        check(members.Sum(id=>h.Polities[id].Treasury)+body.Reserve+body.ServiceSpending+body.MediationSpending==cash&&body.Contributions==body.Reserve+body.ServiceSpending+body.MediationSpending,"Long-lived organizations conserve every transferred and actually spent treasury unit");
        string repeated=StateHash.Compute(system);Call(e,"PayInternationalDues",s,members[0],organization);check(StateHash.Compute(system)==repeated,"Repeating a member payment at its saved date cannot add a bill, contribution or arrears");
        var batch=cadence.Copy();var reverse=cadence.Copy();batch.Years=reverse.Years=start+20;foreach(int id in members)Call(e,"PayInternationalDues",batch,id,organization);foreach(int id in members.Reverse())Call(e,"PayInternationalDues",reverse,id,organization);
        for(int year=1;year<=20;year++){cadence.Years=start+year;foreach(int id in members)Call(e,"PayInternationalDues",cadence,id,organization);}
        var bb=batch.History.International!.Organizations[organization];var cb=cadence.History.International!.Organizations[organization];
        check(bb.Reserve==cb.Reserve&&bb.Contributions==cb.Contributions&&bb.ServiceSpending==cb.ServiceSpending&&batch.History.International.Finances!.Organizations[organization].UnfundedWork==0,"A coarse twenty-year fiscal visit pays the same operating work as annual visits; batching cannot invent shortages or omit the missing bill");
        check(bb.Authority==505&&reverse.History.International!.Organizations[organization].Authority==bb.Authority,"Once all batched members fund the same interval, temporary first-payer shortages are reconciled once and reversed payment order cannot degrade authority");
        var growth=cadence.Copy();int contributor=members[0];var gb=growth.History.International!.Organizations[organization];long contributed=gb.Contributions,oldBudget=growth.History.International.Finances!.Organizations[organization].AnnualAssessments;
        growth.Years++;growth.History.Aggregates.Polities[contributor].Receipts=30000;Call(e,"PayInternationalDues",growth,contributor,organization);var gf=growth.History.International.Finances.Accounts[contributor];
        check(gb.Contributions-contributed==70&&growth.History.International.Finances.Organizations[organization].AnnualAssessments==oldBudget+20&&(gb.Kind==HistoricalOrganizationKind.TradeLeague?gf.TradeReserveDue:gf.PeaceReserveDue)==0,"Growing assessment finances only its own accrued ten-unit charge plus sixty-unit working-capital pledge, not the whole institution's bill");
        growth.Years++;growth.History.Aggregates.Polities[contributor].Receipts=0;long oldTreasury=growth.History.Polities[contributor].Treasury;Call(e,"PayInternationalDues",growth,contributor,organization);
        check(growth.History.International.Finances.Organizations[organization].AnnualAssessments==oldBudget-10&&(gb.Kind==HistoricalOrganizationKind.TradeLeague?gf.TradeAssessment+gf.TradeReserveDue:gf.PeaceAssessment+gf.PeaceReserveDue)==0&&growth.History.Polities[contributor].Treasury<=oldTreasury,"Declining capacity removes cached assessments and unneeded pledges without negative costs or fabricated cash refunds");
        var paid=cadence.Copy();var pb=paid.History.International!.Organizations[organization];pb.Reserve=0;
        check((double)Call(e,"InternationalFunding",paid,organization)==1,"Already-paid current operating work is still delivered even if that payment used the last advance reserve unit");
        paid.Years+=20;check((double)Call(e,"InternationalFunding",paid,organization)<.5,"Unpaid new elapsed work erodes delivery; a past paid window is not permanent free institutional support");
        var dry=cadence.Copy();var db=dry.History.International!.Organizations[organization];db.Reserve=db.Contributions=db.ServiceSpending=db.MediationSpending=0;dry.Years+=10;
        foreach(int id in members){dry.History.Polities[id].Treasury=0;Call(e,"PayInternationalDues",dry,id,organization);}
        check((double)Call(e,"InternationalFunding",dry,organization)==0&&dry.History.International.Finances!.Organizations[organization].UnfundedWork>0,"An unfunded office cannot grant full institutional effects from an old positive authority score");
        int departing=members[0];long assessment=body.Kind==HistoricalOrganizationKind.TradeLeague?system.Finances.Accounts[departing].TradeAssessment:system.Finances.Accounts[departing].PeaceAssessment,prior=budget.AnnualAssessments;
        var queue=new SimulationEventQueue(s.Timeline.Events);var q=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.NonPublic|BindingFlags.Instance)!;q.SetValue(e,queue);Call(e,"LeaveInternationalOrganization",s,departing,organization,"A diagnostic withdrawal tests the cached budget.");q.SetValue(e,null);
        check(budget.AnnualAssessments==prior-assessment,"Withdrawal removes only that member's cached assessment without scanning the remaining roster");
        var continuity=cadence.Copy();var cs=continuity.History.International!;int parent=members[0],next=continuity.History.Polities.Length;var successor=continuity.History.Polities[parent].Copy();successor.Id=next;continuity.History.Polities=continuity.History.Polities.Concat(new[]{successor}).ToArray();long before=cs.Finances!.Organizations[organization].AnnualAssessments;
        Call(e,"AppendInternationalState",continuity,parent,true);
        check(cs.Finances.Organizations[organization].AnnualAssessments==before&&cs.Finances.Accounts[parent].TradeAssessment==0&&cs.Finances.Accounts[parent].PeaceAssessment==0&&cs.Finances.Accounts[next].TradeAssessment+cs.Finances.Accounts[next].PeaceAssessment>0,"Constitutional succession transfers assessments once, without doubling or erasing the organization's budget");
        var valid=e.UpgradeInternationalOperatingBudgets(source);string beforeRead=valid.ComputeHash();e.InternationalProfile(valid,members[0]);
        check(valid.ComputeHash()==beforeRead,"Reading funded institutional profiles is genuinely read-only");
        world.Prehistory=valid;var loaded=WorldFile.Read(WorldFile.Write(world));check(loaded.Prehistory.Hash==valid.Hash&&loaded.Prehistory.History.International!.Finances!=null,"New operating budgets roundtrip with canonical integrity while old null snapshots retain their hashes");
        var one=e.AdvanceYears(valid,15);var two=e.AdvanceYears(e.AdvanceYears(valid,6),9);check(one.Hash==two.Hash,"Actual calendar events preserve playback partition determinism with operating budgets enabled");
        check(source.ComputeHash()==original,"All long fiscal stress fixtures leave their original saved world untouched");
    }
}
