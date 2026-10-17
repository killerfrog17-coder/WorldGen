using System.Reflection;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HistoryInlandRevisionTests
{
    static PrehistoryState Rules(int version)=>new PrehistoryState{History=new HistoryState{Version="history-32",Aggregates=new HistoricalAggregateState{Empires=new HistoricalImperialSystem{Connections=new HistoricalColonialConnections{Realms=new HistoricalRealmLifecycles{Version=version}}}}}};
    public static void Run(Action<bool,string> check)
    {
        var old=Rules(4);var next=Rules(5);
        for(int k=0;k<HistoricalTechnicalGraph.Count;k++)for(int level=1;level<=8;level++)
        {
            long previous=HistoricalTechnicalGraph.Cost(k,level,"history-32");check(HistoricalTechnicalGraph.Cost(old,k,level)==previous,"Realm4 preserves every released research and learning node cost.");
            if(k!=(int)HistoricalTechnology.Iron)check(HistoricalTechnicalGraph.Cost(next,k,level)==previous,"Realm5 does not globally multiply research or cheapen unrelated discoveries.");
        }
        check(HistoricalTechnicalGraph.Cost(next,9,1)==1458000000L&&HistoricalTechnicalGraph.Cost(next,9,2)==1113750000L&&HistoricalTechnicalGraph.Cost(next,9,3)==1470150000L,"Iron's early experimental transition and repeated workshop levels use explicit270/125/100percent base costs.");
        check(HistoricalTechnicalGraph.Requirements(12).Any(r=>r.Technology==9&&r.Level==3)&&!HistoricalTechnicalGraph.Prerequisites(9,new int[39]),"The revised costs preserve actual copper/pottery prerequisites and steel's real Iron3 requirement.");
        for(int trial=0;trial<3;trial++)check(HistoricalInlandSettlement.LocalProposalKm(trial,1,90)>=100&&HistoricalInlandSettlement.LocalProposalKm(trial,1,90)<135,"All three early flatland proposals fit a real135km service envelope without violating100km settlement spacing by design.");
        check(HistoricalInlandSettlement.LocalProposalKm(2,4.5,1000)>HistoricalInlandSettlement.LocalProposalKm(2,1,90)*4,"Funded later transport retains rapid regional expansion rather than globally shortening migration.");
        var from=new V(1,0,0);var point=HistoricalSettlementNetworks.LocalProbeKm(from,6371,.25,120);
        check(Math.Abs(D.Angle(from,point)*6371-120)<.0001,"The new proposal geometry preserves its stated physical distance.");
        check(HistoricalInlandSettlement.Provisions(2000,120,1)>0&&HistoricalInlandSettlement.Investment(2000,120,1)>0,"Local proposals still require genuine carriage food and capital.");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var original=w.Prehistory;string hash=original.ComputeHash();var e=new Prehistory(w);var normal=e.UpgradeRealmLifecycles(original);
        if(normal.History.Aggregates.Empires.Connections.Realms.Version<5)throw new Exception("Realm5 upgrade is not yet wired.");e.Validate(normal);
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args)!;
        var s=normal.Copy();Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);var a=s.History.Aggregates;var h=s.History;
        int first=-1,second=-1;
        foreach(var town in s.Society.Settlements.Where(t=>t.Population>=1200&&a.Cities[t.Id].Supported&&a.Living.Cities[t.Id].AbandonedYear<0&&a.Living.Cities[t.Id].DeclineYears==0))
        {
            var neighbors=(int[])Call("NearbyLivingTowns",s,town.Cell,280.0);
            foreach(int peer in neighbors)if(peer!=town.Id&&s.Society.Settlements[peer].Population>=300&&a.Cities[peer].Supported&&w.Landmass[town.Cell]>=0&&w.Landmass[town.Cell]==w.Landmass[s.Society.Settlements[peer].Cell]&&(bool)Call("InstitutionalRouteAllowed",s,town.Cell,s.Society.Settlements[peer].Cell,town.CultureId)){first=town.Id;second=peer;break;}
            if(first>=0)break;
        }
        if(first<0)throw new Exception("Fixture needs nearby occupied overland actors.");
        var source=s.Society.Settlements[first];var target=s.Society.Settlements[second];int owner=a.Resolve(source.PolityId,h);source.FoodReserve=0;
        check(!(bool)Call("TryRevisedLocalTownRelay",s,first,second,40),"A real local teacher cannot send a food-free traveling staff.");
        source.FoodReserve=Math.Max(100000,source.Population*3);a.Cities[first].Supported=false;
        check(!(bool)Call("TryRevisedLocalTownRelay",s,first,second,40),"A rich but unsupported town is not a working relay school.");a.Cities[first].Supported=true;
        int host=a.Resolve(target.PolityId,h);
        if(owner>=0&&host>=0&&host!=owner){var relation=(HistoricalRelation)Call("AggregateRelation",s,owner,host);relation.Trade=true;relation.WarSince=-1;}
        if(owner>=0){h.Polities[owner].Treasury=10000000;a.Polities[owner].Receipts=a.Polities[owner].ServiceCost+100000;}
        h.Towns[first].CivicTeacherPolityId=owner>=0?owner:h.Polities.First(p=>p.EndedYear<0).Id;h.Towns[first].CivicExampleExposure=800;h.Towns[second].CivicExampleExposure=0;
        long food=source.FoodReserve,cash=owner>=0?h.Polities[owner].Treasury:0;bool delivered=false;int year=s.Years;
        for(int attempt=0;attempt<128&&!delivered;attempt++){s.Years=year+attempt;delivered=(bool)Call("TryRevisedLocalTownRelay",s,first,second,40);}s.Years=year;
        check(delivered&&source.FoodReserve<food&&(owner<0||h.Polities[owner].Treasury<cash)&&h.Towns[second].CivicExampleExposure>0,"An actual nearby staffed, funded town delivers its locally practiced institutional knowledge and pays the trip.");
        int provenance=h.Towns[first].CivicTeacherPolityId;source.PolityId=-1;h.Polities[provenance].EndedYear=s.Years;a.Successors=a.Successors.With(provenance,-1);h.Towns[first].CivicExampleExposure=800;h.Towns[second].CivicExampleExposure=0;food=source.FoodReserve;delivered=false;
        for(int attempt=0;attempt<128&&!delivered;attempt++){s.Years=year+attempt;delivered=(bool)Call("TryRevisedLocalTownRelay",s,first,second,40);}s.Years=year;
        check(delivered&&source.FoodReserve<food&&h.Towns[second].CivicExampleExposure>0,"An actual occupied trained local community can relay its own learned institutions after its originating court ends, without a distant capital hotline.");
        var distant=s.Society.Settlements.First(t=>a.Living.Cities[t.Id].AbandonedYear<0&&D.Angle(w.Surface.Cells[source.Cell].Position,w.Surface.Cells[t.Cell].Position)*w.Recipe.RadiusKm>1000);
        check(!(bool)Call("TryRevisedLocalTownRelay",s,first,distant.Id,40),"A wealthy local teaching actor cannot turn a remembered global contact into a thousand-kilometer direct relay.");
        var legacy=normal.Copy();legacy.History.Aggregates.Empires.Connections.Realms.Version=4;
        check(!(bool)Call("TryRevisedLocalTownRelay",legacy,first,second,40),"Realm4 does not silently acquire the new relay behavior.");
        var culture=h.Cultures[source.CultureId];culture.Technical.Levels[9]=0;culture.Technical.Offers[9]=0;culture.Technical.Allocation[9]=1000;culture.Technical.Progress[9]=145800000;
        check(TechnicalResearchView.Read(s,source.CultureId).First(p=>p.Id==9).ResearchPermille==100,"The public research progress view uses the exact revised cost context.");
        check(original.ComputeHash()==hash,"Controlled inland/technology experiments do not modify the loaded old-rules checkpoint.");
        w.Prehistory=normal;var restored=WorldFile.Read(WorldFile.Write(w)).Prehistory;var whole=e.AdvanceYears(normal,12);var pieces=new Prehistory(w).AdvanceYears(new Prehistory(w).AdvanceYears(restored,5),7);e.Validate(whole);e.Validate(pieces);
        check(whole.Hash==pieces.Hash,"Local relays, proposal geometry and contextual research costs replay across cold restore and twelve versus five-plus-seven-year partitions.");
    }
}
