using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryMilitaryOperationTests
{
    public static void Run(Action<bool,string> check)
    {
        check(HistoricalMilitaryOperationEffects.Reinforcements(0,10000,1,100000,100000)==800,"An empty trained army cannot instantly recover its full demographic target.");
        check(HistoricalMilitaryOperationEffects.Reinforcements(9000,10000,10,100000,100000)==1000,"Training cannot create soldiers beyond the actual eligible manpower target.");
        check(HistoricalMilitaryOperationEffects.Reinforcements(0,10000,5,0,10000)==0&&HistoricalMilitaryOperationEffects.Reinforcements(0,10000,5,100000,0)==0,"Recruitment needs actual equipment money and food.");
        check(HistoricalMilitaryOperationEffects.Reinforcements(0,10000,1,100,10000)==25,"Recruitment spends a finite real equipment account.");
        int first=HistoricalMilitaryOperationEffects.Reserve(10000,0,7000),second=HistoricalMilitaryOperationEffects.Reserve(10000,first,7000),third=HistoricalMilitaryOperationEffects.Reserve(10000,first+second,7000);
        check(first==7000&&second==3000&&third==0,"Simultaneous operations cannot reuse the same trained soldiers.");
        check(!HistoricalMilitaryOperationEffects.Submission(false,0,20,1000000,.01),"Even overwhelming strategic superiority and distant skirmishes do not submit an unthreatened country.");
        check(!HistoricalMilitaryOperationEffects.Submission(true,1000,20,0,0),"A ghost battle cannot impose external political allegiance.");
        check(HistoricalMilitaryOperationEffects.Submission(true,0,1,2000,1000)&&HistoricalMilitaryOperationEffects.Submission(false,400,2,2000,1000),"Actual supplied victory against the capital or sustained core pressure can produce a consequential political settlement.");
        check(HistoricalMilitaryOperationEffects.SiegeDays(1,0,1)>365&&HistoricalMilitaryOperationEffects.SiegeDays(1,1,1)<HistoricalMilitaryOperationEffects.SiegeDays(1,0,1),"An organized fortified center takes real siege time; engineering shortens the operation.");
        check(HistoricalMilitaryOperationEffects.MarchDays(1000,1,1,0,0,900)<HistoricalMilitaryOperationEffects.MarchDays(1000,0,1,0,0,500),"Actual mounted practice and capable command support rapid overland operations.");
        check(HistoricalMilitaryOperationEffects.MarchDays(1000,1,0,0,0,900)>HistoricalMilitaryOperationEffects.MarchDays(1000,1,1,0,0,900),"Bad mounted terrain still slows an exceptional mounted commander.");
        check(HistoricalMilitaryOperationEffects.MarchDays(1000,0,1,1,1,600)<HistoricalMilitaryOperationEffects.MarchDays(1000,0,1,0,0,600),"Deployed motorization and rail support faster modern operational movement.");
        check(HistoricalCommunityCampaignEffects.AnnualAdvanceKm2(50000,1,950,950)>HistoricalCommunityCampaignEffects.AnnualAdvanceKm2(50000,1,600,600)*3,"The bounded physical area model retains exceptional rapid conquest instead of slowing every campaign uniformly.");
        check(HistoricalMilitaryOperationEffects.Losses(1000,false,4,1)>HistoricalMilitaryOperationEffects.Losses(1000,true,4,1)&&HistoricalMilitaryOperationEffects.Losses(0,false,100,1)==0,"Defeat causes consequential bounded real losses, while absent soldiers cannot be killed.");
        var model=new HistoricalMilitaryOperations{Governments=new[]{new HistoricalMilitaryGovernment{Reserved=7}},Campaigns=new[]{new HistoricalMilitaryOperation{CommittedA=7}}};var copy=model.Copy();copy.Governments[0].Reserved++;copy.Campaigns[0].CommittedA++;
        check(model.Governments[0].Reserved==7&&model.Campaigns[0].CommittedA==7,"Military commitments detach across public checkpoints.");
        check(StateHash.Compute(new HistoricalColonialConnections())=="3e6f409c4cdc77e11d2119ec182e566258a65f5eaa5265d0ebc2bd8ea6d094ea","Absent military policy preserves the released colonial record's canonical bytes.");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var original=w.Prehistory;string hash=original.ComputeHash();var e=new Prehistory(w);var normal=e.UpgradeRealmLifecycles(original);e.Validate(normal);var s=normal.Copy();var h=s.History;var a=h.Aggregates;
        object Call(string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,values)!;
        var states=h.Polities.Where(p=>p.EndedYear<0&&a.Cities[p.CapitalId].Supported&&a.Living.Cities[p.CapitalId].AbandonedYear<0).Take(3).ToArray();if(states.Length<3)throw new Exception("Military fixture needs three functioning actual courts.");
        int p=states[0].Id,q=states[1].Id,r=states[2].Id;Call("ReleaseMilitaryGovernment",s,p);Call("ReleaseMilitaryGovernment",s,q);h.Polities[p].Army=10000;h.Polities[q].Army=10000;h.Polities[p].Treasury=h.Polities[q].Treasury=1000000;
        int Add(int firstParty,int secondParty)
        {
            int id=h.Dynamics.Campaigns.Length;h.Dynamics.Campaigns=h.Dynamics.Campaigns.Concat(new[]{new HistoricalCampaign{Id=id,A=Math.Min(firstParty,secondParty),B=Math.Max(firstParty,secondParty),AttackerId=firstParty,LeaderId=h.Dynamics.States[firstParty].LeaderId,StartedYear=s.Years-1,LastActionYear=s.Years-1,Name="Controlled military contrast"}}).ToArray();
            a.Provinces.Theaters=a.Provinces.Theaters.Concat(new[]{new HistoricalCampaignTheater{Cell=s.Society.Settlements[h.Polities[secondParty].CapitalId].Cell}}).ToArray();a.Communities.Campaigns=a.Communities.Campaigns.Concat(new[]{new HistoricalCampaignAdvance()}).ToArray();Call("RegisterMilitaryOperation",s,id);return id;
        }
        int war=Add(p,q);int oldAllegiance=HistoricalImperialHierarchy.Patron(s,q);
        foreach(int owner in new[]{p,q}){var cities=a.Polities[owner].Cities;for(int k=0;k<cities.Count;k++)s.Society.Settlements[cities.At(k)].FoodReserve=0;}
        // Directly test the active supplied-force preparation and global shortcut;
        // existing hierarchical ties need not be removed to manufacture a war.
        object[] field={s,war,p,a.Provinces.Theaters[war].Cell,1,-1,0};
        check((double)Call("PrepareMilitaryField",field)==0&&(int)field[6]==0,"An actual court with no food supplies fields zero men rather than a .01 ghost army.");
        check(!(bool)Call("TryImperialSubmission",s,p,q,true,1000,a.Provinces.Theaters[war].Cell)&&HistoricalImperialHierarchy.Patron(s,q)==oldAllegiance,"A nonnegative distant theater cannot bypass political reach through the old imposed-submission shortcut.");
        int first=(int)Call("ReserveMilitaryContingent",s,war,p,8000),other=Add(p,r),second=(int)Call("ReserveMilitaryContingent",s,other,p,8000);
        var military=a.Empires.Connections.Military;check(first==8000&&second==2000&&military.Governments[p].Reserved==10000,"Two actual campaign records share one conserved military manpower pool.");
        Call("ReleaseMilitaryGovernment",s,p);h.Polities[p].Army=1000;int next=(int)Call("ReserveMilitaryContingent",s,war,p,8000);
        check(next==1000&&military.Governments[p].Reserved==1000,"After an actual governmental manpower split, stale field commitments cannot survive their mobilization epoch.");
        Call("ReleaseMilitaryOperation",s,other);check(military.Governments[p].Reserved==1000,"Closing an old-epoch front cannot subtract soldiers from a newly assigned contingent.");
        int capital=h.Polities[p].CapitalId;long before=a.TotalPopulation;int army=h.Polities[p].Army;long deaths=military.Governments[p].Deaths;
        Call("MilitaryOperationLosses",s,war,p,capital,200);
        check(h.Polities[p].Army==army-200&&military.Governments[p].Reserved==800&&a.TotalPopulation==before-200&&military.Governments[p].Deaths==deaths+200,"A battlefield death decrements trained troops, reserved field force and exactly one real population cohort.");
        var home=s.Society.Settlements[capital];home.FoodReserve=Math.Max(100000,home.Population);h.Polities[p].Treasury=1000000;before=a.TotalPopulation;long cash=h.Polities[p].Treasury;
        Call("AdvanceRevisedMilitaryReadiness",s,p,1,10000);
        check(h.Polities[p].Army==1600&&a.TotalPopulation==before&&h.Polities[p].Treasury==cash-3200,"One year of paid recovery raises800 living recruits without recreating dead soldiers or removing the same people from the census again.");
        int source=-1;object[] paid={s,war,p,home.Cell,1,source,0};cash=h.Polities[p].Treasury;long food=home.FoodReserve;
        check((double)Call("PrepareMilitaryField",paid)>0&&(int)paid[6]>=20&&h.Polities[p].Treasury<cash&&home.FoodReserve<food,"The same real supported depot can finance a positive supplied operation when its food and equipment accounts exist.");
        int protectedArmy=military.Governments[p].Reserved;before=a.TotalPopulation;army=h.Polities[p].Army;
        Call("MilitaryUncommittedLosses",s,p,capital,1000000);
        check(h.Polities[p].Army==protectedArmy&&military.Governments[p].Reserved==protectedArmy&&a.TotalPopulation==before-(army-protectedArmy),"A separate coercive port action can lose only its available soldiers and cannot kill or duplicate troops already committed to other fronts.");
        check(original.ComputeHash()==hash,"Controlled military contrasts preserve the loaded checkpoint.");
        w.Prehistory=normal;var restored=WorldFile.Read(WorldFile.Write(w)).Prehistory;check(restored.Hash==normal.Hash,"Military policies and epoch ledgers survive save/load integrity.");
        var whole=e.AdvanceYears(normal,12);var pieces=e.AdvanceYears(e.AdvanceYears(restored,5),7);e.Validate(whole);
        check(whole.Hash==pieces.Hash,"Actual military operations and reserves replay identically across save/load and12 versus5+7-year partitions.");
    }
}
