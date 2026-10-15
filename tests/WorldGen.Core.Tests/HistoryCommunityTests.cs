using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;
public static class HistoryCommunityTests
{
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Call(Prehistory engine,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,Hidden)!.Invoke(engine,args)!;
    public static void Run(string path,Action<bool,string> check)
    {
        var early=new HistoricalCulture{Technology=new int[HistoricalTechnologies.Names.Length]};
        double old=HistoricalCommunityEffects.RuralGrowth(early,10000,1000),pioneer=HistoricalCommunityEffects.RuralGrowth(early,10000,0,true);
        check(old>0&&old<.001&&pioneer>old*5,"A local farming boom does not become permanent half-percent global growth");
        check(HistoricalCommunityEffects.RuralGrowth(early,10000,5000)==old,"The pioneer advantage expires without an arbitrary global deadline");
        early.Technology[(int)HistoricalTechnology.Sailing]=1;
        check(HistoricalCommunityEffects.MarketIntegration(early,10000,true)>HistoricalCommunityEffects.MarketIntegration(early,10000,false),"Premodern ships can provision ports without granting inland rail transport");
        early.Technology[(int)HistoricalTechnology.Sanitation]=1;
        check(HistoricalCommunityEffects.RuralGrowth(early,10000,1000)>old,"Sanitation changes the demographic regime rather than the passage of calendar years");
        long ordinary=HistoricalCommunityCampaignEffects.AnnualAdvanceKm2(10000,1,600,500),exceptional=HistoricalCommunityCampaignEffects.AnnualAdvanceKm2(10000,1,900,950);
        check(exceptional>ordinary*2,"Exceptional military leadership can produce unusually rapid campaigns");
        check(HistoricalCommunityCampaignEffects.AnnualAdvanceKm2(10000,9,600,500)>ordinary,"Transport affects physical campaign reach, independently of mesh resolution");
        int winterGrain=HistoricalCropCalendarEffects.GrowingWindowWarmth(280,400,300,140,0),coldGrain=HistoricalCropCalendarEffects.GrowingWindowWarmth(40,190,50,-160,0);
        check(winterGrain<300&&winterGrain>150,"Winter cereal opportunities are not rejected because the dry summer is hot");
        check(coldGrain<winterGrain,"A short cool continental season does not receive warm-year crop productivity");
        var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);string original=world.Prehistory.Hash;
        var state=engine.UpgradeToCurrentHistory(world.Prehistory);engine.Validate(state);var a=state.History.Aggregates;
        check(a.Communities!=null&&state.History.Version==Prehistory.CommunityHistoryVersion,"Community rules require an explicit versioned upgrade");
        var copy=state.Copy();copy.History.Aggregates.Communities.Governments[0].RepresentationMilli=800;
        check(a.Communities.Governments[0].RepresentationMilli!=800,"A policy edit cannot mutate the published source");
        int city=Enumerable.Range(0,a.Cities.Length).First(i=>a.Living.Cities[i].AbandonedYear<0&&a.Cities[i].Rural>100);
        var probe=state.Copy();probe.Society.Settlements[city].PolityId=-1;probe.History.Aggregates.Cities[city].Rural=0;
        check((long)Call(engine,"CommunityUrbanCapacity",probe,city)==0,"Unworked potential farmland cannot feed a town");
        probe.History.Aggregates.Cities[city].Rural=10000;
        check((long)Call(engine,"CommunityUrbanCapacity",probe,city)>0,"Actual farming households produce a bounded urban surplus");
        var policy=probe.History.Aggregates.Communities.Cities[city];policy.RulerId=-1;policy.InclusionMilli=900;
        int ruler=state.History.Polities.First(p=>p.EndedYear<0).Id;
        Call(engine,"TransferCommunityGovernment",probe,city,ruler);
        check(policy.RulerId==ruler&&policy.SinceYear==state.Years&&policy.InclusionMilli<900,"Conquest resets local integration without erasing the local culture");
        int culture=probe.Society.Settlements[city].CultureId;
        var routeField=typeof(Prehistory).GetField("communityVoyages",Hidden)!;var routes=(Prehistory.Route[][])routeField.GetValue(engine)!;
        check(routes.Length==engine.Patches.Length,"All coastal route data is prepared before recurring actor work");
        bool reciprocal=true,endpoints=true;
        for(int from=0;from<routes.Length;from++)foreach(var route in routes[from])
        {endpoints&=route.A==from&&engine.CellPatch[route.CellA]==from&&engine.CellPatch[route.CellB]==route.B&&route.DistanceM>0;reciprocal&=routes[route.B].Any(r=>r.B==from&&r.DistanceM==route.DistanceM&&r.CellA==route.CellB&&r.CellB==route.CellA);}
        check(reciprocal&&endpoints,"Ocean paths have real shore endpoints and exactly reciprocal lengths");
        int child=state.History.Cultures.Length-1,parent=0;var inherited=state.Copy();inherited.History.Cultures[parent].Technology[(int)HistoricalTechnology.Computing]=1;inherited.History.Cultures[child].Technology=new int[HistoricalTechnologies.Names.Length];
        Call(engine,"InheritDevelopmentCommunity",inherited,child,parent,inherited.Society.Cultures[child].OriginCell);
        check(inherited.History.Cultures[child].Technology[(int)HistoricalTechnology.Computing]==0,"A boatload of settlers does not instantiate a complete industrial economy");
        var fast=new Prehistory(world);var slow=new Prehistory(world){UseProvincialCaches=false};
        var advanced=fast.AdvanceYears(state,3);var literal=slow.AdvanceYears(state,3);
        check(advanced.Hash==literal.Hash,"Bounded derived caches preserve literal actor outcomes and accounting hashes");
        engine.Validate(advanced);check(world.Prehistory.ComputeHash()==original&&state.ComputeHash()==state.Hash,"New rules and diagnostics preserve the old checkpoint");
        int closed=Array.FindIndex(state.History.Polities,p=>p.EndedYear>=0);
        if(closed>=0)
        {
            var session=new SimulationSession(new Prehistory(world),state);var publication=session.AdvanceSeconds(SimulationTime.Year,int.MaxValue);
            publication.History.Polities[closed].Name="Edited public court";
            session.AdvanceSeconds(2*SimulationTime.Year,int.MaxValue);
            check(session.Hash==advanced.Hash,"Shared private terminal records do not expose mutable fields through publications");
            var edited=session.Snapshot();edited.History.Polities[closed].Name="An explicitly resealed court";edited.Seal();
            var editedSession=new SimulationSession(new Prehistory(world),edited);editedSession.AdvanceSeconds(SimulationTime.Year,int.MaxValue);var verified=editedSession.Snapshot();
            check(verified.Hash==verified.ComputeHash(),"Resealing a deliberate terminal-record edit invalidates trusted canonical bytes");
        }
        var cancelledEngine=new Prehistory(world);var rollback=new SimulationSession(cancelledEngine,state);using var cancellation=new CancellationTokenSource();
        cancelledEngine.WorkObserver=m=>{if(m.Stage=="aggregate checkpoint hash")cancellation.Cancel();};bool stopped=false;
        try{rollback.AdvanceSeconds(3*SimulationTime.Year,int.MaxValue,cancellation.Token);}catch(OperationCanceledException){stopped=true;}
        check(stopped&&rollback.Hash==state.Hash,"Cancellation at the publication boundary commits no partially completed history");
        cancelledEngine.WorkObserver=null;rollback.AdvanceSeconds(3*SimulationTime.Year,int.MaxValue);
        check(rollback.Hash==advanced.Hash,"Private frozen-record caches cannot change a resumed cancelled transaction");
    }
}
