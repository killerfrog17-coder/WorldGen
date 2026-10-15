using System.Reflection;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
public static class HistoryAutonomyTests
{
    // A deliberately isolated mechanism fixture, not a calibrated historical run.
    public static void Run(string file,Action<bool,string> check)
    {
        var w=WorldFile.Read(File.ReadAllBytes(file));var engine=new Prehistory(w);var source=w.Prehistory;engine.Validate(source);
        var s=source.Copy();check(new[]{"history-7","history-8","history-9"}.Contains(s.History.Version),"Autonomy fixture uses the versioned territorial rules");
        var town=s.Society.Settlements.Where(t=>t.PolityId>=0&&t.Population>=20000&&t.CultureId!=s.History.Polities[t.PolityId].CultureId&&t.Id!=s.History.Polities[t.PolityId].CapitalId&&s.Society.Settlements.Count(x=>x.PolityId==t.PolityId&&x.Population>=100)>=3&&(decimal)t.Population/s.Society.Settlements.Where(x=>x.PolityId==t.PolityId).Sum(x=>x.Population)>=.08m).OrderBy(t=>t.Id).FirstOrDefault();
        check(town!=null,"Control history contains an organized conquered provincial center");
        var parent=s.History.Polities[town!.PolityId];var local=s.Society.Cultures[town.CultureId];local.Organization=Math.Max(600,local.Organization);town.FoodCapacity=Math.Max(town.FoodCapacity,town.Population);
        var record=s.History.Towns[town.Id];record.UnrestPolityId=parent.Id;record.AutonomyPressure=900;
        long people=SocietyView.Population(s),funds=s.History.Polities.Sum(p=>p.Treasury),army=s.History.Polities.Sum(p=>(long)p.Army);
        int oldStates=s.History.Polities.Length;var method=typeof(Prehistory).GetMethod("AdvanceProvincialAutonomy",BindingFlags.Instance|BindingFlags.NonPublic)!;
        // Hold the political crisis fixed while testing seeded opportunities. The
        // ordinary end-to-end audits, separately, let budgets and crises evolve.
        for(int attempt=0;attempt<300&&town.PolityId==parent.Id;attempt++)
        {
            s.Years=(s.Years/10+1)*10;parent.Legitimacy=200;parent.Exhaustion=600;s.History.Dynamics.States[parent.Id].Strain=650;
            method.Invoke(engine,new object[]{s,System.Threading.CancellationToken.None});
        }
        check(town.PolityId!=parent.Id,"A mature local movement can establish an autonomous court");
        check(s.History.Polities[town.PolityId].CultureId==town.CultureId&&s.History.Polities[town.PolityId].ParentId==parent.Id,"Local rulers retain the province's culture and political predecessor");
        check(SocietyView.Population(s)==people&&s.History.Polities.Sum(p=>p.Treasury)==funds&&s.History.Polities.Sum(p=>(long)p.Army)==army,"Revolt divides existing people, funds and levies without creating them");
        check(s.History.Archive.Any(e=>e.Kind=="provincial-revolt")&&s.History.Dynamics.Campaigns.Any(c=>c.EndedYear<0&&c.Aim==HistoricalWarAim.Reunification),"The movement and restoration campaign remain available for narrative lore");
        // This unit fixture did not run other courts' annual clocks while varying
        // the seeded opportunity year. Keep their held rulers valid for the later
        // serializer test; ordinary audits use normal succession throughout.
        foreach(var leader in s.History.Dynamics.Leaders.Where(l=>l.EndedYear<0))leader.ExpectedEndYear=Math.Max(leader.ExpectedEndYear,s.Years+1);
        s.Seal();engine.Validate(s);check(source.ComputeHash()==source.Hash,"Mechanism fixture does not mutate its original saved world");
        var altered=s.Copy();altered.History.Towns[town.Id].AutonomyPressure=999;check(altered.ComputeHash()!=s.Hash,"Provincial pressure participates in checkpoint integrity");
        w.Prehistory=s;var round=WorldFile.Read(WorldFile.Write(w));check(round.Prehistory.Hash==s.Hash,"Autonomy records survive binary serialization");
        check(new Prehistory(round).AdvanceYears(round.Prehistory,37).Hash==engine.AdvanceYears(s,37).Hash,"Autonomy records preserve serialized continuation");
        Console.WriteLine("Autonomy mechanism: "+(s.History.Polities.Length-oldStates)+" new courts; "+s.History.Archive.Count(e=>e.Kind=="provincial-revolt")+" recorded revolts.");
    }
}
