using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryMaritimeIntegrationTests
{
    public static void Run(Action<bool,string> check)
    {
        check(HistoricalMaritimeIntegrationEffects.ChartBudget(false,false)==48&&HistoricalMaritimeIntegrationEffects.ChartBudget(true,false)==128&&HistoricalMaritimeIntegrationEffects.ChartBudget(true,true)==256,"Coastal directions expand with oceangoing and global-shipping knowledge while retaining a fixed256-entry maximum");
        check(!HistoricalMaritimeIntegrationEffects.CivilianTerritory(0,true,0,100)&&!HistoricalMaritimeIntegrationEffects.CivilianTerritory(0,true,500,100),"A funded Antarctic station or hypothetical icecap ore does not confer a civilian district title");
        check(HistoricalMaritimeIntegrationEffects.CivilianTerritory(30,false,500,0)&&HistoricalMaritimeIntegrationEffects.CivilianTerritory(35,false,0,20),"Actual Arctic communities and productive reachable cold resource districts remain possible");
        check(!HistoricalMaritimeIntegrationEffects.CivilianTerritory(0,false,0,0)&&HistoricalMaritimeIntegrationEffects.CivilianTerritory(80,false,0,0),"Empty unproductive polar ground is not an automatic engine-era frontier while habitable unoccupied shores remain viable");
        check(!HistoricalMaritimeIntegrationEffects.AdministrativeShipping(3000,0,1,900),"Oral ocean-going skill alone does not create a transoceanic fiscal shipping network");
        check(!HistoricalMaritimeIntegrationEffects.AdministrativeShipping(3000,1,.19,900)&&!HistoricalMaritimeIntegrationEffects.AdministrativeShipping(3000,1,1,349),"Blue-water state ventures require deployed navigation and real administrative institutions");
        check(HistoricalMaritimeIntegrationEffects.AdministrativeShipping(3000,.2,.2,350)&&HistoricalMaritimeIntegrationEffects.AdministrativeShipping(120,0,0,0),"Actual ocean institutions qualify while short coastal ventures need not be modern bureaucracies");
        int early=HistoricalMaritimeIntegrationEffects.PostBudget(10000,1,1),modern=HistoricalMaritimeIntegrationEffects.PostBudget(1000000,4,8);
        check(HistoricalMaritimeIntegrationEffects.PostBudget(1000000,1,1)<modern&&early<modern/4,"Urban scale alone does not grant the same office portfolio as deployed modern administration and communications");
        check(early<128&&modern>128,"Funded maritime-post capacity scales beyond the old128 ceiling with real institutions and communications");
        check(HistoricalMaritimeIntegrationEffects.PostBudget(long.MaxValue,1000,1000)<=2048,"Maritime post portfolio retains a finite actor budget");
        double familiar=HistoricalMaritimeIntegrationEffects.ConsentChance(10,800,800,700,true,.5),stranger=HistoricalMaritimeIntegrationEffects.ConsentChance(10,800,800,700,false,.5);
        check(familiar>stranger*5&&familiar<.2,"Prior contact supports negotiation; a rich stranger does not automatically buy local allegiance");
        check(HistoricalMaritimeIntegrationEffects.ConsentChance(10,800,800,700,true,1)>HistoricalMaritimeIntegrationEffects.ConsentChance(10,800,800,700,true,0),"Local insecurity can make a protection agreement more attractive");
        check(HistoricalMaritimeIntegrationEffects.ConsentChance(0,1000,1000,1000,true,1)==0,"A political agreement takes actual elapsed opportunity");
        check(HistoricalMaritimeIntegrationEffects.ExpeditionChance(10,1.9,0,1000)==0,"Coercing settled shores needs substantial expeditionary military superiority");
        check(HistoricalMaritimeIntegrationEffects.ExpeditionChance(10,10,500,1000)==0&&HistoricalMaritimeIntegrationEffects.ExpeditionChance(10,10,0,600)==0,"Exhaustion and non-expansionist leadership can prevent coercion despite technology");
        check(HistoricalMaritimeIntegrationEffects.ExpeditionChance(10,5,0,900)>0&&HistoricalMaritimeIntegrationEffects.ExpeditionChance(10,5,0,900)<.2,"An eligible military expedition remains probabilistic");
        check(HistoricalMaritimeIntegrationEffects.CouncilInvestment(5000,4000000)>HistoricalMaritimeIntegrationEffects.CouncilInvestment(500,500000),"Distant larger settled ports require real additional fiscal investment");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;string oldHash=source.ComputeHash();var engine=new Prehistory(world);
        var s=engine.UpgradeToLivingWorldHistory(source);var a=s.History.Aggregates;var h=s.History;
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args)!;
        var supplyTown=s.Society.Settlements.First(t=>a.Resolve(t.PolityId,h)>=0&&a.Cities[t.Id].Supported&&t.Population>=500&&(bool)Call("DiffusionShore",t.Cell));
        int supplier=a.Resolve(supplyTown.PolityId,h);
        int[] departures=(int[])Call("LivingColonialDepartures",s,supplier);
        check(departures.Length<=4&&departures.Distinct().Count()==departures.Length&&departures.All(cell=>HistoricalCells.Polity(s,cell)==supplier&&(bool)Call("DiffusionShore",cell)),"The colonial planning portfolio is at most four distinct actual owned coastal departures, not an empire-wide scan or foreign supply base");
        var suppliedCities=s.Society.Settlements.Where(t=>a.Resolve(t.PolityId,h)==supplier).ToArray();
        var reserves=suppliedCities.Select(t=>t.FoodReserve).ToArray();
        foreach(var t in suppliedCities)t.FoodReserve=100000;
        var supplyRoute=new Prehistory.Route{CellA=supplyTown.Cell,CellB=supplyTown.Cell,DistanceM=100000,Sea=true};
        object[] provisionArgs={s,supplier,supplyRoute,supplyTown.Id,-1,0L};
        check((bool)Call("MaritimeCharterLogistics",provisionArgs)&&(int)provisionArgs[4]==supplyTown.Id&&(long)provisionArgs[5]>0,"A charter's logistics name an actual supported populated coastal port and real expedition provisions");
        foreach(var t in suppliedCities)t.FoodReserve=0;
        check(!(bool)Call("MaritimeCharterLogistics",s,supplier,supplyRoute,supplyTown.Id,-1,0L),"A treasury cannot substitute for absent food throughout its actual port network");
        for(int i=0;i<suppliedCities.Length;i++)suppliedCities[i].FoodReserve=reserves[i];
        var queue=new SimulationEventQueue(s.Timeline.Events);
        var queueField=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;
        queueField.SetValue(engine,queue);
        var sponsor=h.Polities.First(p=>p.EndedYear<0);int polity=sponsor.Id;
        var fiscalAccount=a.Polities[polity];var stateRecord=h.Dynamics.States[polity];var stateCulture=h.Cultures[sponsor.CultureId];
        long receipts=fiscalAccount.Receipts,service=fiscalAccount.ServiceCost;int works=stateRecord.PublicWorks,cohesion=sponsor.Cohesion,writing=stateCulture.Technology[(int)HistoricalTechnology.Writing],writingDeployment=stateCulture.Technical?.Deployment[(int)HistoricalTechnology.Writing]??0,offices=a.Empires.Governments[polity].AdministrativePosts.Count;
        stateRecord.PublicWorks=900;sponsor.Cohesion=900;stateCulture.Technology[(int)HistoricalTechnology.Writing]=1;fiscalAccount.Receipts=0;fiscalAccount.ServiceCost=100;
        if(stateCulture.Technical!=null)stateCulture.Technical.Deployment[(int)HistoricalTechnology.Writing]=1000;
        check((int)Call("TryEstablishTerritorialPost",s,polity,s.Society.Settlements[sponsor.CapitalId].Cell)==-1&&a.Empires.Governments[polity].AdministrativePosts.Count==offices,"A larger inland-office allowance does not waive an actual fiscal surplus");
        fiscalAccount.Receipts=100000;stateCulture.Technology[(int)HistoricalTechnology.Writing]=0;
        check((int)Call("TryEstablishTerritorialPost",s,polity,s.Society.Settlements[sponsor.CapitalId].Cell)==-1&&a.Empires.Governments[polity].AdministrativePosts.Count==offices,"Even a rich state cannot establish funded inland administration without actual writing institutions");
        fiscalAccount.Receipts=receipts;fiscalAccount.ServiceCost=service;stateRecord.PublicWorks=works;sponsor.Cohesion=cohesion;stateCulture.Technology[(int)HistoricalTechnology.Writing]=writing;
        if(stateCulture.Technical!=null)stateCulture.Technical.Deployment[(int)HistoricalTechnology.Writing]=writingDeployment;
        // A retention fixture, not a simulation of discovering these shores.
        // Actual discovery remains gated by physical voyages in production.
        for(int region=0;region<Math.Min(32,engine.Patches.Length);region++)Call("RecordCoastChart",s,sponsor.CultureId,region,sponsor.CultureId);
        check(a.Empires.Connections.Cultures[sponsor.CultureId].Charts.Length>=32&&a.Empires.Connections.Cultures[sponsor.CultureId].Charts.Length<=256,"A32 shipping society retains more than24 actual direction records and saves them within the bounded chart portfolio");
        int city=s.Society.Settlements.First(t=>t.Id!=sponsor.CapitalId&&t.Population>=500&&a.Cities[t.Id].Supported&&a.Living.Cities[t.Id].AbandonedYear<0&&a.Resolve(t.PolityId,h)<0).Id;
        var town=s.Society.Settlements[city];
        sponsor.Treasury=100000000;sponsor.Cohesion=900;sponsor.Legitimacy=1000;sponsor.Exhaustion=0;
        a.Polities[polity].Receipts=100000;a.Polities[polity].ServiceCost=100;
        var leader=h.Dynamics.Leaders[h.Dynamics.States[polity].LeaderId];leader.Diplomacy=1000;leader.Ambition=0;
        Call("SealAggregateLeader",s,leader.Id);
        a.Communities.Governments[polity].RepresentationMilli=1000;
        // A controlled opportunity fixture, not a claim this elapsed interval is
        // an ordinary city tick. The helper is reached only after actual maritime
        // access and charter eligibility have been tested by its caller.
        var route=new Prehistory.Route{A=engine.CellPatch[s.Society.Settlements[sponsor.CapitalId].Cell],B=engine.CellPatch[town.Cell],CellA=s.Society.Settlements[sponsor.CapitalId].Cell,CellB=town.Cell,Sea=true,DistanceM=2000000};
        long total=a.TotalPopulation,urban=town.Population,rural=a.Cities[city].Rural,suburban=a.Cities[city].Suburban,treasury=sponsor.Treasury;
        int culture=town.CultureId,terrainCulture=HistoricalCells.Culture(s,town.Cell),states=h.Polities.Length;
        sponsor.Treasury=0;
        check(!(bool)Call("TryMaritimeCouncil",s,polity,city,route,1000000)&&h.Polities.Length==states&&a.TotalPopulation==total,"An unaffordable port agreement cannot create a government or residents");
        sponsor.Treasury=treasury;
        check((bool)Call("TryMaritimeCouncil",s,polity,city,route,1000000),"A funded real settled port can form a negotiated dependent council instead of blocking colonization forever");
        int client=town.PolityId;
        check(client>=states&&HistoricalImperialHierarchy.Patron(s,client)==polity,"A local council is an actual constituent government, not an independent pristine invention");
        check(a.TotalPopulation==total&&town.Population==urban&&a.Cities[city].Rural==rural&&a.Cities[city].Suburban==suburban,"A settled-port charter conserves every existing household account");
        check(town.CultureId==culture&&HistoricalCells.Culture(s,town.Cell)==terrainCulture,"Foreign political allegiance does not replace local inhabitants' culture");
        check(sponsor.Treasury==treasury-HistoricalMaritimeIntegrationEffects.CouncilInvestment(urban,route.DistanceM),"Charter negotiation spends the sponsoring court's real investment");
        check(h.Archive.Any(e=>e.Kind=="chartered-institutions"&&e.SettlementId==city)&&h.Archive.Any(e=>e.Kind=="colonial-council"&&e.SettlementId==city),"Chartered institutions and the protection compact are recorded separately from pristine origins and secession");
        int after=h.Polities.Length;check(!(bool)Call("TryMaritimeCouncil",s,polity,city,route,1000000)&&h.Polities.Length==after,"An already governed port cannot duplicate courts through repeated colonial attempts");
        check(source.ComputeHash()==oldHash,"Maritime fixture branches do not alter the loaded checkpoint");
        s.Timeline.Events=queue.Ordered();queueField.SetValue(engine,null);
        s.Seal();engine.Validate(s);
        world.Prehistory=s;var loaded=WorldFile.Read(WorldFile.Write(world));
        check(loaded.Prehistory.Hash==s.Hash&&HistoricalImperialHierarchy.Patron(loaded.Prehistory,client)==polity,"A dependent port's political accounts survive save/load integrity validation");
    }
}
