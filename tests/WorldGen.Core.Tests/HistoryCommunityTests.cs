using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;
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
        check(a.Communities!=null&&state.History.Version==Prehistory.CurrentHistoryVersion,"Community rules require an explicit versioned upgrade");
        var coverage=HistoricalTechnologyCoverage.Measure(state);
        check(coverage.Length==HistoricalTechnologies.Names.Length&&coverage.All(p=>p>=0&&p<=a.TotalPopulation),"Capability coverage counts disjoint population accounts, not overlapping city/state totals");
        check(coverage[(int)HistoricalTechnology.Printing]<=coverage[(int)HistoricalTechnology.Writing],"Population-weighted diffusion respects actual prerequisite adoption");
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
        if(state.History.Version==Prehistory.DiffusionHistoryVersion||state.History.Version==Prehistory.AgronomicHistoryVersion)
        {
            var passages=(Prehistory.Route[][])typeof(Prehistory).GetField("diffusionPassages",Hidden)!.GetValue(engine)!;
            check(passages.Length==engine.Patches.Length&&passages.All(routes=>routes.Length<=12),"Sparse-habitat passages are prepared once with a bounded departure list");
            check(passages.SelectMany(routes=>routes).All(r=>!r.Sea&&r.DistanceM<=500000&&r.DistanceM>0&&engine.Patches[r.B].Capacity>=125),"Transit shortcuts do not cross arbitrary oceans or require colonies in each small resource patch");
            var inactive=state.Copy();inactive.Society.Cultures[culture].Farming=0;
            int group=Array.FindIndex(inactive.History.Agriculture[culture].Cultivars,c=>c!=null);
            if(group>=0)check((int)Call(engine,"ActiveCommunityCropYears",inactive,culture,group,100,inactive.Society.Settlements[city].Cell)==0,"Merely knowing a crop does not improve it during a century without cultivation");
            var invalid=state.Copy();invalid.History.Aggregates.Communities.Governments[0].RepresentationMilli=1001;invalid.Seal();bool refused=false;
            try{engine.Validate(invalid);}catch(ArgumentException){refused=true;}
            check(refused,"Loading validates local institutional ranges, not only array alignment");
            int interior=Enumerable.Range(0,engine.CellPatch.Length).First(cell=>engine.CellPatch[cell]>=0&&!(bool)Call(engine,"DiffusionShore",cell));
            int shore=Enumerable.Range(0,engine.CellPatch.Length).First(cell=>engine.CellPatch[cell]>=0&&(bool)Call(engine,"DiffusionShore",cell));
            check((double)Call(engine,"DiffusionSeaEffort",state,interior,shore,culture)==double.MaxValue,"Maritime logistics cannot lower the cost of an inland route merely because a culture can sail");
            check((double)Call(engine,"DiffusionSeaEffort",state,shore,shore,culture)==0,"A local port has no artificial loading-distance floor for same-location queries");
        }
        int child=state.History.Cultures.Length-1,parent=0;var inherited=state.Copy();inherited.History.Cultures[parent].Technology[(int)HistoricalTechnology.Computing]=1;inherited.History.Cultures[child].Technology=new int[HistoricalTechnologies.Names.Length];
        if(state.History.Version==Prehistory.AgronomicHistoryVersion)
        {
            var farm=state.Copy();int farmers=state.Society.Settlements.First(t=>t.Population>1000).CultureId;var farmersAccount=farm.History.Aggregates.Cultures[farmers];
            farm.Society.Cultures[farmers].Farming=200;farmersAccount.Rural=20000;farmersAccount.SupportedUrban=10000;
            for(int k=0;k<farmersAccount.Cities.Count;k++)
            {int center=farmersAccount.Cities.At(k);farm.History.Aggregates.Cities[center].Rural=6000;farm.History.Aggregates.Cities[center].Supported=true;farm.Society.Settlements[center].Population=1200;farm.History.Towns[center].Cropping=new HistoricalLocalHarvest{CropIds=new[]{0}};}
            check((bool)Call(engine,"AgronomicResearchSupported",farm,farmers),"A real supported farming community can research agronomy even when its wider culture is mostly fishers or foragers");
            var previousRules=farm.Copy();previousRules.History.Version=Prehistory.DiffusionHistoryVersion;
            check(!(bool)Call(engine,"AgronomicResearchSupported",previousRules,farmers),"History22 keeps its exact older agronomy threshold unless explicitly upgraded");
            for(int k=0;k<farmersAccount.Cities.Count;k++)farm.History.Towns[farmersAccount.Cities.At(k)].Cropping.CropIds=Array.Empty<int>();
            check(!(bool)Call(engine,"AgronomicResearchSupported",farm,farmers),"People and urban institutions alone cannot invent agronomy without actual cultivated crop records");
        }
        Call(engine,"InheritDevelopmentCommunity",inherited,child,parent,inherited.Society.Cultures[child].OriginCell);
        check(inherited.History.Version==Prehistory.TechnicalHistoryVersion?inherited.History.Cultures[child].Technical.Deployment[(int)HistoricalTechnology.Computing]==0:inherited.History.Cultures[child].Technology[(int)HistoricalTechnology.Computing]==0,"A boatload of settlers does not instantiate a complete industrial economy");
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
