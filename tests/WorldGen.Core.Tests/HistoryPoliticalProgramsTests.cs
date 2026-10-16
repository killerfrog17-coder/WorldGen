using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;
public static class HistoryPoliticalProgramsTests
{
    public static void Run(Action<bool,string> check)
    {
        var court=HistoricalPoliticalPrograms.At(100,850,.1,.1,.8,1,0,.7);
        var port=HistoricalPoliticalPrograms.At(450,500,.8,.9,.1,.6,.1,.3);
        var assembly=HistoricalPoliticalPrograms.At(800,300,.7,.7,.1,.5,1,.2);
        check(court.Form=="Military court"&&port.Form=="Merchant and civic oligarchy"&&assembly.Form=="Federated representative government","Institutions reflect participation, centralization and real interests, not a single global government template");
        check(court.InfluencePermille.Sum()==1000&&port.InfluencePermille.Sum()==1000&&assembly.InfluencePermille.Sum()==1000,"Five derived political interest weights are normalized, not counted as fictional party members");
        check(court.DefensePermille>assembly.DefensePermille&&port.LearningPermille>court.LearningPermille&&assembly.ReliefPermille>court.ReliefPermille,"War, commerce and participation create different finite spending priorities");
        check(court.WorksPermille+court.DefensePermille+court.ReliefPermille+court.LearningPermille==1000,"Political spending priorities cannot all be increased without a tradeoff");
        var budget=HistoricalPoliticalPrograms.Allocate(1234567,port);
        check(budget.Total==1234567&&budget.Works>0&&budget.Learning>0,"Rounding conserves the complete treasury-funded public budget");
        check(HistoricalPoliticalPrograms.Allocate(long.MaxValue,assembly).Total==long.MaxValue&&HistoricalPoliticalPrograms.Allocate(-10,court).Total==0,"Budget splitting is bounded and overflow-safe even for extreme manual fixtures");
        check(HistoricalPoliticalPrograms.LearningMultiplier(port,0)<HistoricalPoliticalPrograms.LearningMultiplier(port,800),"Nominal learning priorities without funded public capacity do not provide free research");
        check(HistoricalPoliticalPrograms.RecruitmentMultiplier(court)>HistoricalPoliticalPrograms.RecruitmentMultiplier(assembly),"Defense allocation actually changes field mobilization rather than only descriptive text");
        check(HistoricalPoliticalPrograms.FundedRecruitmentMultiplier(court,0)<HistoricalPoliticalPrograms.FundedRecruitmentMultiplier(court,1),"Military policy receives no free mobilization bonus when its treasury cannot fund defense");
        check(HistoricalPoliticalPrograms.TaxMultiplier(800,900)>HistoricalPoliticalPrograms.TaxMultiplier(100,100),"Administrative reach and negotiated taxation affect receipts with bounded amplitude");
        check(assembly.Participation.Contains("contingent")&&port.Participation.Contains("privileged"),"Ancient political voice is not misrepresented as automatic universal modern suffrage");
        double Chance(HistoricalPoliticalProfile profile,int representation,int capture=200,int fatigue=0,double literacy=0,bool war=false)
            =>HistoricalPoliticalPrograms.ConcessionChance(profile,representation,550,550,500,500,capture,fatigue,literacy,war);
        check(Chance(port,150)>Chance(port,900),"Extending formal voice is not a self-reinforcing one-way move to maximum representation");
        check(Chance(court,400,800)<Chance(assembly,400,100),"Organized court/elite power differs from a civilian bargaining coalition");
        check(Chance(port,400,200,0,1)<.96&&Chance(court,900,800,0,1)<.5,"Printing does not guarantee representative outcomes or erase entrenched elite interests");
        check(Chance(port,400,200,0,0,true)<Chance(port,400),"An active supplied war can strengthen coercive bargaining leverage independently of exhaustion");
        check(Chance(port,400,200,900,0,true)>Chance(port,400,200,0,0,true),"Severe war fatigue is not automatically treated as stronger military authority");
        double funded=HistoricalPoliticalPrograms.AdministrationTarget(assembly,1,.1,700,0,false);
        double overstretched=HistoricalPoliticalPrograms.AdministrationTarget(assembly,0,3,700,0,false);
        check(funded>overstretched+.3,"Real fiscal capacity and actual reach distinguish sustainable administration from unfunded distant holdings");
        check(funded>.3,"A representative bargain can coexist with stronger central administration rather than mandatory federalization");
        check(HistoricalPoliticalPrograms.AdministrationTarget(port,1,.3,600,0,true)>HistoricalPoliticalPrograms.AdministrationTarget(port,1,.3,600,0,false),"Funded wartime mobilization can consolidate administration");
        check(HistoricalPoliticalPrograms.AdministrationTarget(port,0,2,600,900,true)<HistoricalPoliticalPrograms.AdministrationTarget(port,1,.3,600,0,true),"Exhaustion and fiscal breakdown can undo wartime consolidation");
        check(Chance(port,400)==Chance(port,400),"Bargaining probability is a deterministic constant-size query");
        for(int representation=0;representation<=1000;representation+=100)
        {double chance=Chance(port,representation);check(chance>=.04&&chance<=.96,"Neither political direction is an impossible or guaranteed universal destination");}
        for(int r=0;r<=1000;r+=100)for(int c=0;c<=1000;c+=100)
        {var p=HistoricalPoliticalPrograms.At(r,c,1,1,1,1,1,1);check(p.WorksPermille>=0&&p.DefensePermille+p.WorksPermille+p.ReliefPermille+p.LearningPermille==1000,"All policy extremes retain nonnegative conserved shares");}
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);var source=world.Prehistory;var s=engine.UpgradeToLivingWorldHistory(source);string original=source.ComputeHash();
        int state=Array.FindIndex(s.History.Polities,p=>p.EndedYear<0&&s.History.Aggregates.Polities[p.Id].Cities.Count>0);
        if(state<0)throw new InvalidOperationException("Political integration needs a live state.");
        var p=s.History.Polities[state];var a=s.History.Aggregates.Polities[state];var ds=s.History.Dynamics.States[state];
        p.Treasury=1000000;a.Receipts=100000;long before=p.Treasury;int works=ds.PublicWorks;
        typeof(Prehistory).GetMethod("FundPoliticalProgram",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,new object[]{s,state,5});
        var budget=HistoricalPoliticalPrograms.Allocate(Math.Min(before/8,40000),engine.PoliticalProfile(s,state)!);
        check(p.Treasury==before-budget.Total&&p.Treasury>=0,"Actual state event spending debits the exact four-way budget and cannot mint public funds");
        check(ds.PublicWorks>=Math.Max(0,works-5)&&ds.PublicWorks<=Math.Min(1000,works+10),"Funded policy effects are bounded by elapsed years rather than arbitrary treasury size");
        check(source.ComputeHash()==original,"Political diagnostic and new-rule upgrade do not mutate the old saved world");
        s.Seal();engine.Validate(s);world.Prehistory=s;var loaded=WorldFile.Read(WorldFile.Write(world));
        check(loaded.Prehistory.Hash==s.Hash&&engine.PoliticalProfile(loaded.Prehistory,state)!.Form==engine.PoliticalProfile(s,state)!.Form,"Constitutional policy views survive serialization without new canonical fields");
        var future=engine.AdvanceYears(s,50);var partitioned=engine.AdvanceYears(engine.AdvanceYears(s,17),33);
        check(future.Hash==partitioned.Hash,"Political coalition draws and independent administration response are publication-partition deterministic");
        check(new Prehistory(loaded).AdvanceYears(loaded.Prehistory,50).Hash==future.Hash,"The actual political continuation survives full save/load with no new persisted policy fields");
        check(source.ComputeHash()==original,"Political evolution and all diagnostic branches preserve the old rules checkpoint");
    }
}
