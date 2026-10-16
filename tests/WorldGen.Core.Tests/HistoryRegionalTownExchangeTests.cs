using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryRegionalTownExchangeTests
{
    static object Call(Prehistory e,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args)!;
    public static void Run(Action<bool,string> check)
    {
        check(HistoricalRegionalTownExchange.Staff(999)==0&&HistoricalRegionalTownExchange.Staff(1000)==12,"A village or office cannot furnish a free specialist service");
        check(HistoricalRegionalTownExchange.Staff(1000000)==60,"Temporary traveling staff remain bounded by a fixed operational portfolio");
        check(HistoricalRegionalTownExchange.Food(12,2000,1)>HistoricalRegionalTownExchange.Food(12,20,1),"A long real overland service consumes additional rations");
        check(HistoricalRegionalTownExchange.Investment(60,2000,1)>HistoricalRegionalTownExchange.Investment(60,20,1),"Distance has an actual transport investment cost");
        check(HistoricalRegionalTownExchange.Investment(60,2000,4.5)<HistoricalRegionalTownExchange.Investment(60,2000,1),"Deployed transport reduces carriage costs without free investment");
        check(HistoricalRegionalTownExchange.Exposure(.2)<HistoricalRegionalTownExchange.Exposure(.9),"Jungle, latitude and other physical transmission penalties still slow delivery");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;string hash=source.ComputeHash();var e=new Prehistory(world);e.Validate(source);
        var probe=source.Copy();var a=probe.History.Aggregates;int city=-1,region=-1;
        // Offline fixture discovery only; the runtime has exactly three targets.
        foreach(var town in probe.Society.Settlements)
        {
            if(a.Living.Cities[town.Id].AbandonedYear>=0||town.Population<1000)continue;
            int home=e.CellPatch[town.Cell];var targets=new[]{home}.Concat(a.Regions[home].Routes.Select(i=>e.Routes[i]).Where(r=>!r.Sea).Select(r=>r.A==home?r.B:r.A));
            foreach(int target in targets)
                if((bool)Call(e,"TryLivingTownRegionalDelivery",probe,town.Id,target)){city=town.Id;region=target;break;}
            if(city>=0)break;
        }
        check(city>=0,"Actual saved world has a funded provisioned city and physically reachable inhabited rural anchor with a distinct recipient");
        var s=source.Copy();a=s.History.Aggregates;var h=s.History;var from=s.Society.Settlements[city];int cell=a.Regions[region].BestCell,donor=from.CultureId,owner=a.Resolve(from.PolityId,h),receiver=HistoricalCells.Culture(s,cell);if(receiver<0)receiver=a.Regions[region].Culture;
        double transport=HistoricalModernEffects.LandTransport(h.Cultures[donor],s.Years),effort=(double)Call(e,"LivingTravelKm",s,from.Cell,cell,donor);
        int staff=HistoricalRegionalTownExchange.Staff(from.Population);long food=from.FoodReserve,total=a.TotalPopulation,money=owner<0?0:h.Polities[owner].Treasury;
        var levels=h.Cultures.Select(c=>(int[])c.Technical.Levels.Clone()).ToArray();var crops=h.Agriculture.Select(c=>(int[])c.AdoptedYear.Clone()).ToArray();
        check((bool)Call(e,"TryLivingTownRegionalDelivery",s,city,region),"Real local specialist delivery succeeds with the same deterministic opportunity");
        check(from.FoodReserve==food-HistoricalRegionalTownExchange.Food(staff,effort,transport),"Actual roundtrip food is debited exactly once");
        check(owner<0||h.Polities[owner].Treasury==money-HistoricalRegionalTownExchange.Investment(staff,effort,transport),"A sponsoring court pays actual carriage investment exactly once");
        check(a.TotalPopulation==total&&h.Cultures.Select((c,i)=>c.Technical.Levels.SequenceEqual(levels[i])).All(v=>v)&&h.Agriculture.Select((c,i)=>c.AdoptedYear.SequenceEqual(crops[i])).All(v=>v),"Delivery grants no population, technology levels or domesticated crops");
        check(a.Living.Regions[region].Teacher==donor&&a.Living.Regions[region].LastExchangeYear==s.Years,"The physical local contributor, not its distant homeland or ancient founding court, records regional provenance");
        if(receiver>=0)check((bool)Call(e,"ProvincialRecentContact",s,receiver,donor,true),"The actual recipient retains a dated direct receipt without an origin-to-origin revalidation");
        s.Seal();e.Validate(s);world.Prehistory=s;var loaded=WorldFile.Read(WorldFile.Write(world));
        check(loaded.Prehistory.Hash==s.Hash,"Dated delivery and conservative account changes survive ordinary save/load integrity");
        var noFood=source.Copy();noFood.Society.Settlements[city].FoodReserve=0;long before=noFood.History.Aggregates.TotalPopulation,treasury=owner<0?0:noFood.History.Polities[owner].Treasury;
        check(!(bool)Call(e,"TryLivingTownRegionalDelivery",noFood,city,region)&&noFood.History.Aggregates.TotalPopulation==before&&(owner<0||noFood.History.Polities[owner].Treasury==treasury),"No actual provisions means no receipt or treasury debit");
        if(owner>=0)
        {
            var noFunds=source.Copy();noFunds.History.Polities[owner].Treasury=0;long stock=noFunds.Society.Settlements[city].FoodReserve;
            check(!(bool)Call(e,"TryLivingTownRegionalDelivery",noFunds,city,region)&&noFunds.Society.Settlements[city].FoodReserve==stock,"An unfunded court cannot invent free specialist staff service");
        }
        var ruins=source.Copy();ruins.History.Aggregates.Living.Cities[city].AbandonedYear=ruins.Years;
        check(!(bool)Call(e,"TryLivingTownRegionalDelivery",ruins,city,region),"An abandoned town cannot become a regional teacher merely through its persistent historical marker");
        var unsupported=source.Copy();unsupported.History.Aggregates.Cities[city].Supported=false;
        check(!(bool)Call(e,"TryLivingTownRegionalDelivery",unsupported,city,region),"A historical location without a currently supported workforce cannot dispatch teachers");
        int foreign=source.History.Polities.Where(p=>p.EndedYear<0&&p.Id!=owner).Select(p=>p.Id).DefaultIfEmpty(-1).First();
        if(foreign>=0)
        {
            var blocked=source.Copy();blocked.History.Aggregates.Cells=blocked.History.Aggregates.Cells.With(cell*5+1,foreign);
            var isolated=new Prehistory(world);isolated.Validate(source);
            check(!(bool)Call(isolated,"TryLivingTownRegionalDelivery",blocked,city,region)&&blocked.Society.Settlements[city].FoodReserve==food,"A foreign host without an actual peaceful trade permission does not receive a free sponsored mission");
        }
        var legacy=source.Copy();legacy.History.Version=Prehistory.EnvironmentalHistoryVersion;long legacyFood=legacy.Society.Settlements[city].FoodReserve;
        check(!(bool)Call(e,"TryLivingTownRegionalDelivery",legacy,city,region)&&legacy.Society.Settlements[city].FoodReserve==legacyFood,"The new path is absent from official history31 rules");
        var future=e.AdvanceYears(s,20);var partitioned=e.AdvanceYears(e.AdvanceYears(s,7),13);
        check(future.Hash==partitioned.Hash,"Existing dated actor scheduling remains publication-partition deterministic");
        check(new Prehistory(loaded).AdvanceYears(loaded.Prehistory,20).Hash==future.Hash,"Actual local delivery continues identically after save/load");
        check(source.ComputeHash()==hash,"The diagnostic discovery and all controlled continuations preserve the original source world");
    }
}
