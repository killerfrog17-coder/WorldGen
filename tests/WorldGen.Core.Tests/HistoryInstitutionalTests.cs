using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;
public static class HistoryInstitutionalTests
{
    public static void Run(Action<bool,string> check)
    {
        int Council(int institutions,int diplomacy,int renown=0,bool contested=false)=>HistoricalInstitutionalEffects.SuccessionLegitimacyCost(false,institutions,diplomacy,renown,contested);
        check(Council(650,550)<28,"Orderly supported council handovers no longer lose more legitimacy than a typical fourteen-year term can recover");
        check(Council(900,800)<Council(200,200),"Institutional continuity and negotiation reduce routine succession disruption");
        check(Council(650,550,0,true)>Council(650,550),"Contested transfers retain a substantial legitimacy cost");
        check(HistoricalInstitutionalEffects.SuccessionLegitimacyCost(true,650,550,0,false)>Council(650,550),"Personal dynastic replacement remains distinct from appointment to a continuing council");
        check(Council(650,550,700)>Council(650,550),"Even institutional government can lose support after an unusually influential leader");
        check(Council(650,550,700)<HistoricalInstitutionalEffects.SuccessionLegitimacyCost(true,650,550,700,false),"A council's authority is less wholly dependent on one celebrated individual");
        check(HistoricalInstitutionalEffects.ServiceBurden(50000,100000,true)==0&&HistoricalInstitutionalEffects.ServiceBurden(50000,100000,false)==700,"A disconnected province remains locally severely strained even if its residents are a small part of a realm");
        check(HistoricalInstitutionalEffects.RealmStrain(700*1000,100000,1000,1)<20,"A one-percent disconnected fringe cannot impose whole-state communications collapse");
        check(HistoricalInstitutionalEffects.RealmStrain(700*90000,100000,90000,1)==700,"A genuinely predominantly disconnected realm retains severe integration strain");
        check(HistoricalInstitutionalEffects.RealmStrain(100*10000,10000,0,3)==HistoricalInstitutionalEffects.RealmStrain(100*100000,100000,0,3),"Equivalent resident service shares do not become less governable solely from population count or cell count");
        string fixture=Environment.GetEnvironmentVariable("INSTITUTIONAL_FIXTURE")??"D:/WorldGen-history11-20261013/release-earth20k/final.world";
        var w=WorldFile.Read(File.ReadAllBytes(fixture));var engine=new Prehistory(w);var old=w.Prehistory;
        check(old.History.Version=="history-11","Regression fixture uses the released history11 rules");
        string past=old.Hash,geo=StateHash.Compute(w);var start=engine.UpgradeToInstitutionalHistory(old);engine.Validate(start);
        check(start.History.Version=="history-12"&&old.Hash==past&&old.ComputeHash()==past,"Explicit upgrade preserves the old checkpoint and its integrity");
        check(start.History.Archive.Count==old.History.Archive.Count&&start.Society.Settlements.Select(t=>t.FoundedYear).SequenceEqual(old.Society.Settlements.Select(t=>t.FoundedYear)),"New succession rules do not rewrite past events or founding dates");
        check(SimulationTime.Seconds(start)==SimulationTime.Seconds(old)&&SimulationClockView.EpochYear(start)==SimulationClockView.EpochYear(old),"Precise time and observer Year0 survive the upgrade");
        var probe=old.Copy();var court=probe.History.Polities.First(p=>p.EndedYear<0&&p.Government=="Storage and irrigation council"&&!probe.History.Relations.Any(r=>r.WarSince>=0&&(r.A==p.Id||r.B==p.Id)));
        var ruler=probe.History.Dynamics.Leaders[probe.History.Dynamics.States[court.Id].LeaderId];ruler.ExpectedEndYear=probe.Years+1;ruler.Renown=0;court.Legitimacy=700;court.Cohesion=800;court.Exhaustion=0;probe.History.Cultures[court.CultureId].Institution=650;probe.Seal();engine.Validate(probe);
        var legacyNext=engine.AdvanceYears(probe,1);var revisedNext=engine.AdvanceYears(engine.UpgradeToInstitutionalHistory(probe),1);
        check(legacyNext.History.Dynamics.States[court.Id].LeaderId!=ruler.Id&&revisedNext.History.Dynamics.States[court.Id].LeaderId!=ruler.Id,"The mechanism check exercises a real scheduled council appointment");
        check(revisedNext.History.Polities[court.Id].Legitimacy>legacyNext.History.Polities[court.Id].Legitimacy,"The actual appointment applies the institutional rule, not just its isolated helper");
        var next=engine.AdvanceSeconds(start,137*SimulationTime.Year+17*SimulationTime.Day);engine.Validate(next);
        var split=engine.AdvanceSeconds(engine.AdvanceSeconds(start,51*SimulationTime.Year+3*SimulationTime.Hour),86*SimulationTime.Year+17*SimulationTime.Day-3*SimulationTime.Hour);
        check(next.Hash==split.Hash,"Institutional succession is independent of playback partitioning");
        check(new Prehistory(w).AdvanceSeconds(start,137*SimulationTime.Year+17*SimulationTime.Day).Hash==next.Hash,"Fresh and warmed connectivity caches give identical outcomes");
        var json=WorldJson.Read<PrehistoryState>(WorldJson.Write(next));engine.Validate(json);check(json.Hash==next.Hash,"Current history JSON retains exact integrity");
        w.Prehistory=next;var binary=WorldFile.Read(WorldFile.Write(w));check(new Prehistory(binary).AdvanceYears(binary.Prehistory,17).Hash==engine.AdvanceYears(next,17).Hash,"Binary saves continue the new rules identically");
        check(StateHash.Compute(w)==geo&&old.ComputeHash()==past,"No succession rule changes geography or the source historical snapshot");
        var cancel=new CancellationTokenSource();cancel.Cancel();bool rejected=false;try{engine.AdvanceYears(start,1,token:cancel.Token);}catch(OperationCanceledException){rejected=true;}check(rejected&&start.ComputeHash()==start.Hash,"Cancellation cannot partially alter an institutional checkpoint");
        string folder=Environment.GetEnvironmentVariable("INSTITUTIONAL_OUTPUT")??"D:/WorldGen-history12-20261014/mechanism-tests";Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,"Earth.world"),WorldFile.Write(w));
        Console.WriteLine($"Scheduled handover legitimacy: history11 {legacyNext.History.Polities[court.Id].Legitimacy}; history12 {revisedNext.History.Polities[court.Id].Legitimacy}. Continued hash {next.Hash}");
    }
}
