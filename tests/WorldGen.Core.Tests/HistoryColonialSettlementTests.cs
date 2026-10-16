#nullable enable
using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryColonialSettlementTests
{
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var originalState=world.Prehistory;string original=originalState.ComputeHash();var engine=new Prehistory(world);
        var s=engine.UpgradeToLivingWorldHistory(world.Prehistory).Copy();var h=s.History;var a=h.Aggregates;
        object Call(string method,params object[] args)=>typeof(Prehistory).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args)!;
        var queue=new SimulationEventQueue(s.Timeline.Events);var queueField=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;queueField.SetValue(engine,queue);
        int sponsor=-1,source=-1,target=-1;Prehistory.Route? route=null;
        // Explicit test fixture search only; production samples two offices and
        // four departures, never these full actor collections.
        foreach(var post in a.Empires.Territories.Posts)
        {
            int owner=a.Resolve(post.PolityId,h);
            if(owner<0||!post.Maritime||post.ClosedYear>=0||a.TownAt[post.Cell]>=0||HistoricalCells.Polity(s,post.Cell)!=owner||!(bool)Call("TerritorialNodeLive",s,owner,-2-post.Id))continue;
            h.Polities[owner].Cohesion=900;h.Polities[owner].Treasury=1000000000;
            foreach(var town in s.Society.Settlements)
            {
                if(a.Resolve(town.PolityId,h)!=owner||town.Population<1000||!a.Cities[town.Id].Supported||!(bool)Call("DiffusionShore",town.Cell)||engine.CellPatch[town.Cell]==engine.CellPatch[post.Cell])continue;
                town.FoodReserve=1000000000;
                if(!(bool)Call("CanFoundLivingTown",s,town.CultureId,post.Cell,town.Id)||!(bool)Call("LivingTerritorialInterest",s,owner,post.Cell)||!(bool)Call("InstitutionalRouteAllowed",s,town.Cell,post.Cell,h.Polities[owner].CultureId))continue;
                var candidate=new Prehistory.Route{A=engine.CellPatch[town.Cell],B=engine.CellPatch[post.Cell],CellA=town.Cell,CellB=post.Cell,Sea=true,DistanceM=(int)Math.Round(D.Angle(world.Surface.Cells[town.Cell].Position,world.Surface.Cells[post.Cell].Position)*world.Recipe.RadiusKm*1000)};
                object[] args={s,owner,candidate,town.Id,-1,0L};
                if(!(bool)Call("MaritimeCharterLogistics",args)||(int)args[4]!=town.Id)continue;
                sponsor=owner;source=town.Id;target=post.Cell;route=candidate;break;
            }
            if(route!=null)break;
        }
        if(route==null)
        {
            // Old fixtures can predate any overseas venture. Construct ONE
            // explicitly controlled, paid foothold on actual reachable terrain,
            // using the ordinary ownership/area ledgers. No coast, crops or
            // population are invented to make the migration helper succeed.
            foreach(var town in s.Society.Settlements)
            {
                int owner=a.Resolve(town.PolityId,h);
                if(owner<0||town.Population<1000||!a.Cities[town.Id].Supported||!(bool)Call("DiffusionShore",town.Cell))continue;
                h.Polities[owner].Cohesion=900;h.Polities[owner].Treasury=1000000000;town.FoodReserve=1000000000;
                foreach(var cell in world.Surface.Cells.OrderBy(cell=>D.Angle(world.Surface.Cells[town.Cell].Position,cell.Position)))
                {
                    int at=cell.Id,current=HistoricalCells.Polity(s,at);
                    if(engine.CellPatch[at]<0||engine.CellPatch[at]==engine.CellPatch[town.Cell]||a.TownAt[at]>=0||a.Empires.Territories.PostAt[at]>=0||current>=0&&current!=owner||!(bool)Call("DiffusionShore",at))continue;
                    if(!(bool)Call("CanFoundLivingTown",s,town.CultureId,at,town.Id)||!(bool)Call("LivingTerritorialInterest",s,owner,at)||!(bool)Call("InstitutionalRouteAllowed",s,town.Cell,at,h.Polities[owner].CultureId))continue;
                    if((double)Call("DiffusionSeaEffort",s,town.Cell,at,h.Polities[owner].CultureId)==double.MaxValue)continue;
                    var candidate=new Prehistory.Route{A=engine.CellPatch[town.Cell],B=engine.CellPatch[at],CellA=town.Cell,CellB=at,Sea=true,DistanceM=(int)Math.Round(D.Angle(world.Surface.Cells[town.Cell].Position,cell.Position)*world.Recipe.RadiusKm*1000)};
                    object[] args={s,owner,candidate,town.Id,-1,0L};if(!(bool)Call("MaritimeCharterLogistics",args)||(int)args[4]!=town.Id)continue;
                    var territory=a.Empires.Territories;int id=territory.Posts.Length;
                    var post=new HistoricalAdministrativePost{Id=id,PolityId=owner,Cell=at,ParentSource=town.Id,Depth=1,FoundedYear=s.Years,AnnualCost=100,Maritime=true,LegDistanceM=candidate.DistanceM,MessageEffortKm=candidate.DistanceM/1000.0,Name="Controlled coastal foothold fixture"};
                    territory.Posts=territory.Posts.Concat(new[]{post}).ToArray();territory.PostAt=territory.PostAt.With(at,id);
                    var government=a.Empires.Governments[owner];government.AdministrativePosts.Add(id);government.AdministrativePostCost+=100;h.Polities[owner].Treasury-=1000;
                    Call("AppendProvincialFrontier",s);Call("ClaimAggregatePolity",s,at,owner);Call("AssignTerritorialSource",s,at,-2-id);
                    sponsor=owner;source=town.Id;target=at;route=candidate;break;
                }
                if(route!=null)break;
            }
        }
        if(route==null)throw new Exception("Fixture has no real supported port with a physically reachable viable unoccupied coastline, even for the labeled controlled foothold.");
        var p=h.Polities[sponsor];var depot=s.Society.Settlements[source];int culture=depot.CultureId,count=a.Cities.Length;
        long total=a.TotalPopulation;int[] neighbors=world.Surface.Cells[target].Neighbors;int[] neighborsCulture=neighbors.Select(cell=>HistoricalCells.Culture(s,cell)).ToArray();
        p.Treasury=0;
        check(!(bool)Call("TryFoundLivingColonialPort",s,sponsor,route,1000000)&&a.Cities.Length==count,"A colonial port cannot appear without a paid sponsoring treasury");
        p.Treasury=1000000000;
        var providers=Enumerable.Range(0,a.Polities[sponsor].Cities.Count).Select(k=>s.Society.Settlements[a.Polities[sponsor].Cities.At(k)]).ToArray();var priorFood=providers.Select(t=>t.FoodReserve).ToArray();
        foreach(var provider in providers)provider.FoodReserve=0;
        check(!(bool)Call("TryFoundLivingColonialPort",s,sponsor,route,1000000)&&a.Cities.Length==count,"Money cannot replace missing rations for real migrating households");
        for(int k=0;k<providers.Length;k++)providers[k].FoodReserve=priorFood[k];
        depot.FoodReserve=1000000000;int farming=s.Society.Cultures[culture].Farming;s.Society.Cultures[culture].Farming=0;
        check(!(bool)Call("TryFoundLivingColonialPort",s,sponsor,route,1000000)&&a.Cities.Length==count,"Ships and a foreign ruler cannot turn unpracticed source foragers into farmers");
        s.Society.Cultures[culture].Farming=farming;
        long available=(long)Call("LivingFounderAvailability",s,source),founders=Math.Min(250,available/12),farms=Math.Min(available/3,Math.Max(800,founders*8)),people=founders+farms;
        var old=a.Cities[source];long oldUrban=depot.Population,oldRural=old.Rural,oldSuburban=old.Suburban,oldRemote=a.Polities[sponsor].Remote;
        var transfer=HistoricalSettlementMigration.Allocate(people,oldUrban,oldSuburban,oldRural,oldRemote,a.Polities[sponsor].CityCount);
        long food=depot.FoodReserve,money=p.Treasury,provisions=HistoricalVoyageEffects.Provisioning(people,route.DistanceM);
        check((bool)Call("TryFoundLivingColonialPort",s,sponsor,route,1000000),"A funded actual maritime office can receive a real small migrant farming port");
        int city=a.TownAt[target];var townNew=s.Society.Settlements[city];
        check(a.TotalPopulation==total&&townNew.Population==founders&&a.Cities[city].Rural==farms,"Colonial founding relocates exact existing households, not invented colonists or a fully grown city");
        check(depot.Population==oldUrban-transfer.Urban&&old.Rural==oldRural-transfer.Rural&&old.Suburban==oldSuburban-transfer.Suburban&&a.Polities[sponsor].Remote==oldRemote-transfer.Remote,"Urban, suburban, rural and remote source budgets are debited separately");
        check(depot.FoodReserve==food-people-provisions&&p.Treasury==money-HistoricalMaritimeIntegrationEffects.CouncilInvestment(people,route.DistanceM),"Startup food, extra voyage rations and construction money are each charged exactly once");
        check(townNew.CultureId==culture&&a.Resolve(townNew.PolityId,h)==sponsor,"The port retains its migrants' actual source culture and sponsor instead of inventing a pristine civilization");
        check(neighbors.Select((cell,k)=>HistoricalCells.Culture(s,cell)==neighborsCulture[k]).All(x=>x),"Founding a port does not replace the neighboring indigenous ecoregion's cultures");
        check(!(bool)Call("TryFoundLivingColonialPort",s,sponsor,route,1000000)&&a.Cities.Length==count+1,"A staffed port cannot repeatedly reuse archival founder counts to create more towns");
        s.Timeline.Events=queue.Ordered();queueField.SetValue(engine,null);s.Seal();engine.Validate(s);
        world.Prehistory=s;var roundtrip=WorldFile.Read(WorldFile.Write(world));
        check(roundtrip.Prehistory.Hash==s.Hash&&roundtrip.Prehistory.Society.Settlements[city].Population==founders,"Actual colonial accounts and scheduled town events survive validated save/load");
        check(originalState.ComputeHash()==original,"The original historical fixture remains unchanged");
    }
}
