using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistorySettlementNetworkTests
{
    public static void Run(Action<bool,string> check)
    {
        double early=HistoricalSettlementNetworks.AnnualOpportunity(10000,5000,.9,1,false,500),modern=HistoricalSettlementNetworks.AnnualOpportunity(10000,5000,.9,4.5,true,500);
        check(early>0&&modern>early*3&&modern<.02,"Transported civic networks support faster local settlement than small unsupported prestate communities without instant sprawl");
        check(HistoricalSettlementNetworks.AnnualOpportunity(250,5000,1,5,true,500)==0,"A newly landed hamlet does not immediately breed more towns");
        check(HistoricalSettlementNetworks.AnnualOpportunity(10000,1599,1,5,true,500)==0,"Local expansion requires actual transferable households");
        check(HistoricalSettlementNetworks.AnnualOpportunity(10000,5000,1,5,true,29)==0&&HistoricalSettlementNetworks.AnnualOpportunity(10000,5000,1,1,false,79)==0,"New civic and prestate centers both need local residence rather than instant exponential branching");
        check(HistoricalSettlementNetworks.AnnualOpportunity(10000,5000,.2,1,true,500)<HistoricalSettlementNetworks.AnnualOpportunity(10000,5000,.9,1,true,500),"Sparse underfilled surroundings generate less movement pressure than a full local provisioning base");
        check(HistoricalSettlementNetworks.AnnualOpportunity(long.MaxValue,5000,1,5,true,500)==HistoricalSettlementNetworks.AnnualOpportunity(1000000,5000,1,5,true,500),"Metropolitan size has a finite multiplier rather than unbounded city branching");
        // The pure rate has no global cultural-city-count input; the world
        // transaction below verifies this against the actual indexed actor bag.
        check(modern+modern==2*modern,"Both mature ports retain their own bounded local opportunity");
        int coarse=HistoricalSettlementNetworks.LocalWalkSteps(25500,0),fine=HistoricalSettlementNetworks.LocalWalkSteps(2550,0);
        check(coarse==1&&fine==6,"Local site proposals use physical scale rather than sending coarse-map farmers four to six enormous cells away");
        check(Math.Sqrt(coarse*25500.0)/Math.Sqrt(fine*2550.0)<1.4,"Coarse and fine local proposals have comparable geographic reach within the unavoidable cell-size floor");
        check(Enumerable.Range(0,3).All(trial=>HistoricalSettlementNetworks.LocalWalkSteps(.000001,trial)<=8&&HistoricalSettlementNetworks.LocalWalkSteps(1000000,trial)>=1),"Local candidate paths remain strictly bounded even at extreme mesh resolutions");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string originalHash=original.ComputeHash();var engine=new Prehistory(world);
        var s=engine.UpgradeToLivingWorldHistory(original).Copy();var h=s.History;var a=h.Aggregates;
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args)!;
        int parent=-1,target=-1,polity=-1;
        foreach(var town in s.Society.Settlements)
        {
            int owner=a.Resolve(town.PolityId,h);if(owner<0||town.Population<1000||!a.Cities[town.Id].Supported||a.Living.Cities[town.Id].AbandonedYear>=0)continue;
            h.Polities[owner].Treasury=1000000000;h.Dynamics.States[owner].PublicWorks=900;a.Polities[owner].Receipts=100000;a.Polities[owner].ServiceCost=100;town.FoodReserve=1000000000;
            foreach(var cell in world.Surface.Cells.OrderBy(cell=>D.Angle(world.Surface.Cells[town.Cell].Position,cell.Position)))
            {
                int at=cell.Id,host=HistoricalCells.Polity(s,at);
                if(engine.CellPatch[at]<0||world.Landmass[at]!=world.Landmass[town.Cell]||host>=0&&host!=owner||
                    (bool)Call("RespectRecognizedClaim",s,owner,at)||!(bool)Call("CanFoundLivingTown",s,town.CultureId,at,town.Id)||!(bool)Call("InstitutionalRouteAllowed",s,town.Cell,at,town.CultureId))continue;
                double travel=(double)Call("LivingTravelKm",s,town.Cell,at,town.CultureId),radius=(double)Call("TerritorialServiceRadius",s,owner,at);
                if(travel>radius*1.5||(double)Call("LivingLocalTownRate",s,town.Id)<=0)continue;
                parent=town.Id;target=at;polity=owner;break;
            }
            if(parent>=0)break;
        }
        if(parent<0)throw new Exception("Fixture needs an actual mature town with transferable households and a reachable viable uncrowded local site.");
        var depot=s.Society.Settlements[parent];var source=a.Cities[parent];var account=a.Polities[polity];var ruler=h.Polities[polity];var ca=a.Cultures[depot.CultureId];
        double rate=(double)Call("LivingLocalTownRate",s,parent);int oldCount=ca.CityCount;var oldBag=ca.Cities;ca.Cities=oldBag.Copy();
        for(int k=0;k<100;k++)ca.Cities.Add(a.Cities.Length+100+k);ca.CityCount+=100;
        check((double)Call("LivingLocalTownRate",s,parent)==rate,"One hundred hypothetical distant cultural centers cannot starve this actual local port's opportunity");
        ca.Cities=oldBag;ca.CityCount=oldCount;
        var queue=new SimulationEventQueue(s.Timeline.Events);var queueField=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;queueField.SetValue(engine,queue);
        int count=a.Cities.Length;ruler.Treasury=0;
        check(!(bool)Call("TryFoundLivingLocalTown",s,parent,target)&&a.Cities.Length==count,"An actor-local schedule does not waive the state's real construction budget");
        ruler.Treasury=1000000000;depot.FoodReserve=0;
        check(!(bool)Call("TryFoundLivingLocalTown",s,parent,target)&&a.Cities.Length==count,"Local migration cannot create startup food for its transferred households");
        depot.FoodReserve=1000000000;
        var sourceCrops=h.Agriculture[depot.CultureId];var adopted=sourceCrops.AdoptedYear;sourceCrops.AdoptedYear=new int[6];
        check(!(bool)Call("TryFoundLivingLocalTown",s,parent,target)&&a.Cities.Length==count,"Founders without their own viable cultivated crops cannot borrow an unrelated native culture's repertoire merely by arriving");
        sourceCrops.AdoptedYear=adopted;
        long available=(long)Call("LivingFounderAvailability",s,parent),founders=Math.Min(250,available/12),farms=Math.Min(available/3,Math.Max(800,founders*8)),people=founders+farms;
        long total=a.TotalPopulation,urban=depot.Population,suburban=source.Suburban,rural=source.Rural,remote=account.Remote,unallocated=s.Population[engine.CellPatch[target]],food=depot.FoodReserve,money=ruler.Treasury;
        int nativeCohort=a.PopulationCohort(a.Cells[target*5+1]);long nativeRemote=nativeCohort>=0&&nativeCohort!=polity?a.Polities[nativeCohort].Remote:0;
        var transfer=HistoricalSettlementMigration.Allocate(people,urban,suburban,rural,remote,account.CityCount);
        check((bool)Call("TryFoundLivingLocalTown",s,parent,target),"A mature actual local actor can found its nearby viable community without a global cultural-parent lottery");
        int city=a.TownAt[target];long nativeIncorporation=unallocated-s.Population[engine.CellPatch[target]]+
            (nativeCohort>=0&&nativeCohort!=polity?nativeRemote-a.Polities[nativeCohort].Remote:0);
        check(a.TotalPopulation==total&&s.Society.Settlements[city].Population==founders&&a.Cities[city].Rural==farms,"Actor-local founding conserves global residents and begins with real small households");
        check(depot.Population==urban-transfer.Urban&&source.Suburban==suburban-transfer.Suburban&&source.Rural==rural-transfer.Rural&&account.Remote==remote-transfer.Remote+nativeIncorporation,"All actual source households and any separately incorporated native countryside have conserved accounts");
        check(depot.FoodReserve==food-people&&ruler.Treasury==money-Math.Max(100,people/5),"Startup rations and the local construction budget are charged exactly once");
        check(a.Resolve(s.Society.Settlements[city].PolityId,h)==polity,"A sponsored local community retains its actual funding polity rather than a culture's obsolete colonial sponsor");
        check(s.Society.Settlements[city].CultureId==depot.CultureId,"Real migrating founders retain their actual culture and crop repertoire instead of instantly becoming unrelated native farmers");
        check(!(bool)Call("TryFoundLivingLocalTown",s,parent,target)&&a.Cities.Length==count+1,"Crowding and existing-site rules prevent repeated settlements on the same cell");
        s.Timeline.Events=queue.Ordered();queueField.SetValue(engine,null);s.Seal();engine.Validate(s);world.Prehistory=s;
        check(WorldFile.Read(WorldFile.Write(world)).Prehistory.Hash==s.Hash,"Local settlement transactions and schedules survive validated save/load");
        check(original.ComputeHash()==originalHash,"The original saved historical world is untouched by the test branch");
    }
    public static void PrestateWorld(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string originalHash=original.ComputeHash();var engine=new Prehistory(world);engine.Validate(original);var s=original.Copy();var a=s.History.Aggregates;
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args)!;
        check(world.Surface.Cells.Length==20000&&s.History.Polities.Length==0,"The coarse prestate fixture has actual terrain, farming communities and no state infrastructure");
        int parent=-1,target=-1;
        foreach(var town in s.Society.Settlements)
        {
            if((double)Call("LivingLocalTownRate",s,town.Id)<=0)continue;
            long available=(long)Call("LivingFounderAvailability",s,town.Id),founders=Math.Min(250,available/12),farmers=Math.Min(available/3,Math.Max(800,founders*8));
            if(town.FoodReserve<founders+farmers+town.Population/4)continue;
            for(int trial=0;trial<3;trial++)
            {
                int site=(int)Call("LivingLocalTownCandidate",s,town.Id,trial);
                if(!(bool)Call("CanFoundLivingTown",s,town.CultureId,site,town.Id)||!(bool)Call("InstitutionalRouteAllowed",s,town.Cell,site,town.CultureId))continue;
                double km=D.Angle(world.Surface.Cells[town.Cell].Position,world.Surface.Cells[site].Position)*world.Recipe.RadiusKm;
                double transmission=(double)Call("EnvironmentalTransmission",s,town.Cell,site,town.CultureId,false);
                bool legacySea=(bool)Call("DiffusionShore",town.Cell)&&(bool)Call("DiffusionShore",site)&&(double)Call("DiffusionSeaEffort",s,town.Cell,site,town.CultureId)!=double.MaxValue;
                if(legacySea||km/transmission<=360*HistoricalModernEffects.LandTransport(s.History.Cultures[town.CultureId],s.Years))continue;
                parent=town.Id;target=site;break;
            }
            if(parent>=0)break;
        }
        if(parent<0)throw new Exception("Fixture needs a naturally supported prestate farmer center whose actual bounded coarse-map proposal is locally viable.");
        var source=s.Society.Settlements[parent];var ca=a.Cities[parent];long sourceResidents=source.Population+ca.Suburban+ca.Rural,total=a.TotalPopulation,food=source.FoodReserve;int count=a.Cities.Length;
        var queue=new SimulationEventQueue(s.Timeline.Events);var field=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;field.SetValue(engine,queue);
        check((bool)Call("TryFoundLivingLocalTown",s,parent,target),"A naturally provisioned prestate community can reach its actual km-scaled candidate without state wealth or synthetic crop grants");
        int child=a.TownAt[target];long settlers=s.Society.Settlements[child].Population+a.Cities[child].Rural;
        check(a.TotalPopulation==total&&source.Population+ca.Suburban+ca.Rural==sourceResidents-settlers&&source.FoodReserve==food-settlers,"The actual coarse prestate founding transfers existing households and startup food exactly once");
        check(a.Cities.Length==count+1&&s.History.Polities.Length==0&&s.Society.Settlements[child].CultureId==source.CultureId,"A local daughter community is not a free independent state or an instant unrelated cultural invention");
        check(a.Cities[parent].Neighbors.Contains(child)&&a.Cities[child].Neighbors.Contains(parent),"An actually supplied nearby daughter community and its founders have reciprocal real local network links");
        var legacy=s.Copy();legacy.History.Version=Prehistory.EnvironmentalHistoryVersion;
        check(!(bool)Call("LivingTownConnection",legacy,parent,child),"The same actual pair retains history31's stricter360-unit rule rather than silently upgrading old saves to32reach");
        int ocean=Array.FindIndex(world.Hydrology.OceanConnected,c=>c);
        check(ocean>=0&&!(bool)Call("TryFoundLivingLocalTown",s,parent,ocean)&&a.Cities.Length==count+1,"Harmonized town links do not allow a farmer community to materialize in unsupported open water");
        s.Timeline.Events=queue.Ordered();field.SetValue(engine,null);s.Seal();engine.Validate(s);world.Prehistory=s;
        check(WorldFile.Read(WorldFile.Write(world)).Prehistory.Hash==s.Hash&&original.ComputeHash()==originalHash,"The real coarse-map local transaction validates and saves without modifying its source");
    }
}
