using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryMaritimePracticeTests
{
    public static void Run(Action<bool,string> check)
    {
        bool Port(bool alive=true,bool supported=true,bool shore=true,long people=500,long food=100,double sailing=.15)
            =>HistoricalMaritimePracticeEffects.WorkingPort(alive,supported,shore,people,food,sailing);
        check(Port(),"A supported provisioned shore population with actual craft practice can be a working port");
        check(!Port(alive:false)&&!Port(supported:false),"Ruins and unsupported cities are not active shipbuilding capacity");
        check(!Port(shore:false)&&!Port(people:499),"An inland village or unpopulated flag is not a port");
        check(!Port(food:99),"A starving port cannot support the court's active maritime service");
        check(!Port(sailing:.149),"Bare coastal geography does not substitute for actual boatbuilding practice");
        check(HistoricalMaritimePracticeEffects.FundedCourt(1000,101,100),"A funded current court may employ actual subject shipbuilders");
        check(!HistoricalMaritimePracticeEffects.FundedCourt(999,101,100)&&!HistoricalMaritimePracticeEffects.FundedCourt(1000,100,100),"Administrative exposure requires both real treasury and a service surplus");
        check(HistoricalMaritimePracticeEffects.RuralBoatCommunity(500,.15,0,false)&&HistoricalMaritimePracticeEffects.RuralBoatCommunity(500,0,200,false),"Actual marine resources or practiced local fishing can support rural boat communities before an urban harbor exists");
        check(!HistoricalMaritimePracticeEffects.RuralBoatCommunity(499,1,1000,false)&&!HistoricalMaritimePracticeEffects.RuralBoatCommunity(500,0,0,false)&&!HistoricalMaritimePracticeEffects.RuralBoatCommunity(1000,1,1000,true),"Empty claims, no aquatic livelihood and permanent ice cannot become rural shipyards");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string hash=original.ComputeHash();var engine=new Prehistory(world);engine.Validate(original);
        var s=original.Copy();var h=s.History;var a=h.Aggregates;
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args)!;
        int state=48,culture=11,port=103;
        if(s.Years!=130100||state>=h.Polities.Length||h.Polities[state].CultureId!=culture||h.Polities[state].CapitalId!=port)
            throw new Exception("Use the exact matched O Earth73421 early-modern130100 checkpoint; this regression does not manufacture its shipyard or political geography.");
        check(s.Society.Settlements[port].CultureId!=culture&&(bool)Call("LivingStateMaritimePort",s,state,port),"The actual current capital is a funded working shore town with a different local culture");
        string version=h.Version;h.Version=Prehistory.EnvironmentalHistoryVersion;bool legacy=(bool)Call("CommunityMaritimePractice",s,culture);h.Version=version;
        check(!legacy&&(bool)Call("CommunityMaritimePractice",s,culture),"The actual O48 foreign-cultural shipyard is recognized where the legacy sampled-cultural-town route misses it");
        var tech=h.Cultures[culture].Technical;var levels=(int[])tech.Levels.Clone();var deployment=(int[])tech.Deployment.Clone();long total=a.TotalPopulation;
        bool again=(bool)Call("CommunityMaritimePractice",s,culture);
        check(again&&levels.SequenceEqual(tech.Levels)&&deployment.SequenceEqual(tech.Deployment)&&a.TotalPopulation==total,"Practical maritime exposure repeats deterministically and grants no research, deployment or residents");
        int rural=-1;var coasts=a.Empires.Connections.Coasts.Governments[state];
        for(int k=0;k<coasts.Count;k++){int at=coasts.At(k);if(a.TownAt[at]<0&&(bool)Call("LivingStateRuralMaritime",s,state,at)){rural=at;break;}}
        check(rural>=0,"The actual O48 world contains a populated marine-supported serviced coast without a named urban port");
        long ruralResidents=(long)Call("AggregateRuralCellPopulation",s,rural);
        check(ruralResidents>=500&&(bool)Call("LivingStateRuralMaritime",s,state,rural),"Real rural boat workers and paid finite service support maritime practice before a city marker exists");
        int market=HistoricalCells.Market(s,rural),cohort=a.PopulationCohort(a.Cells[rural*5+1]),region=engine.CellPatch[rural];
        long savedRural=market>=0?a.Cities[market].Rural:0,savedSuburban=market>=0?a.Cities[market].Suburban:0,savedRemote=cohort>=0?a.Polities[cohort].Remote:0,savedPopulation=s.Population[region];
        if(market>=0&&a.Cities[market].AreaKm2>0){a.Cities[market].Rural=0;a.Cities[market].Suburban=0;}
        else if(cohort>=0&&a.Polities[cohort].RemoteAreaKm2>0)a.Polities[cohort].Remote=0;
        else s.Population[region]=0;
        check((long)Call("AggregateRuralCellPopulation",s,rural)==0&&!(bool)Call("LivingStateRuralMaritime",s,state,rural),"The identical serviced office or coastal claim cannot stand in for an empty resident cohort");
        if(market>=0){a.Cities[market].Rural=savedRural;a.Cities[market].Suburban=savedSuburban;}
        if(cohort>=0)a.Polities[cohort].Remote=savedRemote;s.Population[region]=savedPopulation;
        int ruralSource=a.Empires.Territories.Sources[rural];
        while(ruralSource<=-2)ruralSource=a.Empires.Territories.Posts[-ruralSource-2].ParentSource;
        long ruralFood=s.Society.Settlements[ruralSource].FoodReserve;s.Society.Settlements[ruralSource].FoodReserve=0;
        check(!(bool)Call("LivingStateRuralMaritime",s,state,rural),"Rural boat communities still need actual service-depot provisions, not only resident estimates");
        s.Society.Settlements[ruralSource].FoodReserve=ruralFood;
        var coastal=s.Society.Settlements.Where(t=>a.Resolve(t.PolityId,h)==state&&(bool)Call("DiffusionShore",t.Cell)).ToArray();
        var supported=coastal.Select(t=>a.Cities[t.Id].Supported).ToArray();foreach(var town in coastal)a.Cities[town.Id].Supported=false;
        check(!(bool)Call("CommunityMaritimePractice",s,culture),"Claimed shores and live offices without any supported populated port do not grant current-state maritime exposure");
        for(int k=0;k<coastal.Length;k++)a.Cities[coastal[k].Id].Supported=supported[k];
        var providers=Enumerable.Range(0,a.Polities[state].Cities.Count).Select(k=>s.Society.Settlements[a.Polities[state].Cities.At(k)]).ToArray();
        var food=providers.Select(t=>t.FoodReserve).ToArray();foreach(var town in providers)town.FoodReserve=0;
        check(!(bool)Call("CommunityMaritimePractice",s,culture),"The same actual port network loses its support when its real provisioning accounts are empty");
        for(int k=0;k<providers.Length;k++)providers[k].FoodReserve=food[k];
        var owners=coastal.Select(t=>t.PolityId).ToArray();foreach(var town in coastal)town.PolityId=-1;
        check(!(bool)Call("CommunityMaritimePractice",s,culture),"A stale state index cannot grant the practices of foreign or independent ports");
        for(int k=0;k<coastal.Length;k++)coastal[k].PolityId=owners[k];
        long treasury=h.Polities[state].Treasury;h.Polities[state].Treasury=0;
        check(!(bool)Call("CommunityMaritimePractice",s,culture),"A government with no actual service budget cannot employ the foreign-cultural port network");
        h.Polities[state].Treasury=treasury;
        check((bool)Call("CommunityMaritimePractice",s,culture),"Restoring the same real funded network restores its practical maritime support");
        check(original.ComputeHash()==hash,"All actual positive and negative checks leave the loaded world unchanged");
    }
}
