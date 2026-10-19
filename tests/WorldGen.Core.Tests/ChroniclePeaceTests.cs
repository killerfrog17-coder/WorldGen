using WorldGen.Core;
using WorldGen.Presentation;
public static class ChroniclePeaceTests
{
    public static void Run(Action<bool,string> check)
    {
        var s=new PrehistoryState{Years=200,Society=new SocietyState{Cultures=Array.Empty<CultureIdentity>(),Settlements=new[]{new Settlement{Id=0},new Settlement{Id=1}}},History=new HistoryState{Polities=new[]{new HistoricalPolity{Id=0,CapitalId=0,Name="Riverrealm",Population=10000},new HistoricalPolity{Id=1,CapitalId=1,Name="Coastrealm",Population=10000}}}};
        var e=new HistoricalEvent{Year=100,Kind="peace",PolityId=0,OtherPolityId=1,Category=ChronicleCategory.Politics};
        string Headline(string terms){e.Text="Campaign for the river ends. "+terms;return new ChronicleNews(s).Project(e)!.Text;}
        foreach(var example in new[]{
            ("A supplied campaign secures a limited frontier settlement; the defeated country keeps its government","limited frontier settlement"),
            ("The defeated court pays tribute after losses around its political core","tribute settlement"),
            ("A negotiated settlement reopens the contested trade route after a supplied campaign","reopening the contested trade route"),
            ("A military settlement retains the defeated country as a constituent government","retained rather than annexed"),
            ("The belligerents enter the same imperial settlement","same imperial settlement"),
            ("Neither side can sustain a supplied field force; the offensive is abandoned","neither side could keep its army supplied"),
            ("Losses, fatigue and stalled objectives bring a negotiated ceasefire","ceasefire"),
            ("Negotiated peace after exhaustion and frontier conflict","costly war"),
            ("A rare armistice preserves neutral access to a sacred site.","preserves neutral access")})
        {string text=Headline(example.Item1);check(text.Contains(example.Item2)&&text.Contains("Riverrealm")&&text.Contains("Coastrealm"),"Peace headline retains actual outcome: "+example.Item2);}
        e.Text="Riverrealm and Coastrealm agree to peace.";check(new ChronicleNews(s).Project(e)!.Text.Contains("no detailed terms were recorded"),"Old generic events do not invent treaty terms");
        s.History.Polities[0].EndedYear=100;check(Headline("A belligerent loses its remaining government")=="Riverrealm's government fell, ending its war with Coastrealm.","Government loss is not described as an equal negotiated peace or automatically as annexation");
        s.History.Aggregates=new HistoricalAggregateState{Empires=new HistoricalImperialSystem{Governments=new[]{new HistoricalImperialGovernment(),new HistoricalImperialGovernment()},Connections=new HistoricalColonialConnections{Realms=new HistoricalRealmLifecycles{Version=37,Sovereignty=new HistoricalSovereigntyHistory()}}}};
        var r=s.History.Aggregates.Empires.Connections.Realms.Sovereignty;
        r.Transfers=r.Transfers.WithBlock(0,new[]{0,1,100,(int)HistoricalSovereigntyCause.Conquest,0,0,-1,0});
        check(Headline("The defeated government loses its remaining administrative center")=="Coastrealm defeated Riverrealm, ending its independent government.","Dated actual conquest names the winner and defeated government");
        string before=StateHash.Compute(s);Headline("A belligerent loses its remaining government");check(StateHash.Compute(s)==before,"Headline projection never modifies historical records");
        r.Transfers=r.Transfers.With(2,150);check(!Headline("A belligerent loses its remaining government").Contains("defeated"),"A later conquest does not rewrite an earlier war's outcome");
        s.History.Polities[0].EndedYear=150;check(!Headline("A negotiated settlement ends the campaign").Contains("government fell"),"Later national collapse does not rewrite an earlier peace");
        s.History.Polities[0].EndedYear=-1;var client=s.History.Aggregates.Empires.Governments[0];client.Patron=1;client.SinceYear=100;
        check(Headline("A military settlement retains the defeated country as a constituent government")=="Riverrealm submitted to Coastrealm, retaining its local government.","Dated submission names both parties without claiming direct annexation");
        client.SinceYear=150;check(!Headline("A military settlement retains the defeated country as a constituent government").Contains("submitted to"),"A later patron is not falsely assigned to a historical settlement");
        s.History.Polities[0].EndedYear=s.History.Polities[1].EndedYear=100;check(Headline("One court ceased to exist").Contains("both governments falling"),"Mutual collapse does not assign an imaginary victor");
    }
}
