using WorldGen.Core;
using WorldGen.Serialization;
using System.Reflection;

public static class HistoryResourceEconomyTests
{
    static HistoricalResourcePotential Resources(int copper=0,int tin=0,int iron=0,int coal=0,int oil=0,int timber=0,int uranium=0)
        =>new(copper,tin,iron,0,coal,oil,uranium,500,timber,100,500,500,500,false);
    public static void Run(Action<bool,string> check)
    {
        var empty=Resources();var metals=Resources(copper:800,tin:800,iron:800,coal:800,timber:800);var oil=Resources(iron:800,oil:800);
        check(HistoricalResourceEconomy.DeploymentSupply(metals,HistoricalTechnology.Bronze)>HistoricalResourceEconomy.DeploymentSupply(Resources(copper:800),HistoricalTechnology.Bronze),"Bronze production benefits from both copper and a tin supply, not copper alone");
        check(HistoricalResourceEconomy.DeploymentSupply(Resources(iron:800,timber:800),HistoricalTechnology.Iron)>HistoricalResourceEconomy.DeploymentSupply(empty,HistoricalTechnology.Iron),"Woodland charcoal is a real preindustrial substitute for coal");
        check(HistoricalResourceEconomy.DeploymentSupply(oil,HistoricalTechnology.CombustionEngines)>HistoricalResourceEconomy.DeploymentSupply(metals,HistoricalTechnology.CombustionEngines),"Oil availability affects engine deployment differently from coal-fed steam");
        check(HistoricalResourceEconomy.DeploymentSupply(Resources(uranium:800),HistoricalTechnology.NuclearWeapons)>HistoricalResourceEconomy.DeploymentSupply(empty,HistoricalTechnology.NuclearWeapons),"Knowing a nuclear design is not equivalent to easy fissile-material access");
        check(HistoricalResourceEconomy.DeploymentSupply(empty,HistoricalTechnology.Antibiotics)==1,"Mineral deposits do not nonsensically gate every medical discovery");
        foreach(var technology in Enum.GetValues<HistoricalTechnology>())foreach(var p in new[]{empty,metals,oil})
        {double value=HistoricalResourceEconomy.DeploymentSupply(p,technology);check(value>=.3&&value<=1,"Bounded material approximation retains costly substitutes/overlooked deposits: "+technology);}
        check(HistoricalResourceEconomy.ExtractiveReceipts(0,1,1,1,1,1,metals)==0,"Empty territorial claims do not generate mineral tax revenue");
        check(HistoricalResourceEconomy.ExtractiveReceipts(100000,1,1,0,0,0,metals)==0,"Unworked deposits do not generate free extraction income");
        long worked=HistoricalResourceEconomy.ExtractiveReceipts(100000,1,1,1,1,1,metals);
        check(worked>0&&worked<1000,"Worked mineral receipts are a small bounded specialist output, not a new unlimited economy");
        check(HistoricalResourceEconomy.StrategicValue(oil,0,0,0)==0&&HistoricalResourceEconomy.StrategicValue(oil,0,0,1)>10,"Petroleum provinces only become strategic industrial prizes after usable engine technology");
        check(HistoricalResourceEconomy.StateSupplyCapacity(50000,100000,10000,600,1)>0,"Paid public works, an actual urban workforce and deployed transport support state material freight");
        check(HistoricalResourceEconomy.StateSupplyCapacity(50000,0,10000,600,1)==0&&HistoricalResourceEconomy.StateSupplyCapacity(50000,100000,10000,0,1)==0,"An unfunded state or absent paid public-works capacity cannot export a free material network");
        check(HistoricalResourceEconomy.StateSupplyCapacity(50000,100000,10000,600,0)==0&&HistoricalResourceEconomy.StateSupplyCapacity(0,100000,10000,600,1)==0,"Blueprints without installed transport or an actual urban workforce do not enable state supply");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string hash=original.ComputeHash();var engine=new Prehistory(world);var s=engine.UpgradeToLivingWorldHistory(original).Copy();var h=s.History;var a=h.Aggregates;
        object Call(string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,values)!;
        void Modern(HistoricalCulture c){for(int k=0;k<37;k++){c.Technology[k]=1;c.Technical.Levels[k]=8;c.Technical.LevelYear[k]=0;c.Technical.Deployment[k]=1000;}}
        int owner=-1,supplier=-1,receiver=-1;
        foreach(var state in h.Polities.Where(p=>p.EndedYear<0&&a.Polities[p.Id].Cities.Count>1))
        {
            int capital=state.CapitalId;if(!a.Cities[capital].Supported||s.Society.Settlements[capital].Population<500)continue;
            var raw=HistoricalNaturalResources.At(world,s.Society.Settlements[capital].Cell);if(raw.Copper+raw.Iron+raw.Coal+raw.Oil<50)continue;
            Modern(h.Cultures[state.CultureId]);
            var candidates=a.Polities[state.Id].Cities;
            for(int k=0;k<candidates.Count;k++)
            {
                int city=candidates.At(k);if(city==capital||!a.Cities[city].Supported)continue;
                if((bool)Call("InstitutionalRouteAllowed",s,s.Society.Settlements[city].Cell,s.Society.Settlements[capital].Cell,state.CultureId)){owner=state.Id;supplier=capital;receiver=city;break;}
            }
            if(owner>=0)break;
        }
        check(owner>=0,"Owned-state supply control has actual supported connected cities and a real geological resource site");
        var p=h.Polities[owner];var source=s.Society.Settlements[supplier];var target=s.Society.Settlements[receiver];var carrier=h.Cultures[p.CultureId];p.Treasury=100000000;a.Polities[owner].Receipts=100000;h.Dynamics.States[owner].PublicWorks=1000;
        var delivered=(HistoricalResourcePotential)Call("LivingStateMaterialSource",s,owner,target.Cell,supplier);
        check(delivered.Copper+delivered.Iron+delivered.Coal+delivered.Oil>0,"A worked supported co-owned city physically supplies raw materials rather than sharing only the ruler's technology string");
        string queryHash=s.ComputeHash();var market=(HistoricalResourcePotential)Call("LivingStateMaterialMarket",s,target.Cell,Resources());
        check(market.Copper+market.Iron+market.Coal+market.Oil>0&&engine.LastMaterialSupplierCandidates<=HistoricalResourceEconomy.StateSupplierCandidateLimit,"State material query includes actual capital/indexed-city/local-neighbor supply with a fixed16-candidate work bound");
        check(s.ComputeHash()==queryHash,"Reading funded material capacity never debits treasury or otherwise changes authoritative history");
        long treasury=p.Treasury;p.Treasury=0;var unfunded=(HistoricalResourcePotential)Call("LivingStateMaterialSource",s,owner,target.Cell,supplier);p.Treasury=treasury;
        check(unfunded.Copper+unfunded.Iron+unfunded.Coal+unfunded.Oil==0,"A broke government cannot use its old ownership colors as a free freight service");
        foreach(var technology in new[]{HistoricalTechnology.Roads,HistoricalTechnology.Sailing,HistoricalTechnology.Railways})carrier.Technical.Deployment[(int)technology]=0;
        var noTransport=(HistoricalResourcePotential)Call("LivingStateMaterialSource",s,owner,target.Cell,supplier);
        check(noTransport.Copper+noTransport.Iron+noTransport.Coal+noTransport.Oil==0,"Known state technology without installed transport is insufficient for material delivery");
        Modern(carrier);a.Cities[supplier].Supported=false;
        var unsupported=(HistoricalResourcePotential)Call("LivingStateMaterialSource",s,owner,target.Cell,supplier);a.Cities[supplier].Supported=true;
        check(unsupported.Copper+unsupported.Iron+unsupported.Coal+unsupported.Oil==0,"An unsupported emptying city is not a productive mine merely because it owns mineral potential");
        int foreign=h.Polities.First(state=>state.Id!=owner&&state.EndedYear<0).Id;
        int originalOwner=source.PolityId;source.PolityId=foreign;
        var refused=(HistoricalResourcePotential)Call("LivingStateMaterialSource",s,owner,target.Cell,supplier);source.PolityId=originalOwner;
        check(refused.Copper+refused.Iron+refused.Coal+refused.Oil==0,"A foreign city cannot be conscripted into an owned-state network without bilateral trade");
        int far=Enumerable.Range(0,world.Surface.Cells.Length).Where(cell=>engine.CellPatch[cell]>=0&&world.Landmass[cell]!=world.Landmass[target.Cell])
            .OrderByDescending(cell=>D.Angle(world.Surface.Cells[cell].Position,world.Surface.Cells[target.Cell].Position)).First();
        int originalCell=source.Cell,originalCulture=source.CultureId;source.Cell=far;source.CultureId=target.CultureId;
        a.Cells=a.Cells.With(far*5+1,owner);
        foreach(var technology in new[]{HistoricalTechnology.Navigation,HistoricalTechnology.OceanNavigation})carrier.Technical.Deployment[(int)technology]=0;
        var disconnected=(HistoricalResourcePotential)Call("LivingStateMaterialSource",s,owner,target.Cell,supplier);
        check(!(bool)Call("InstitutionalRouteAllowed",s,target.Cell,far,p.CultureId)&&disconnected.Copper+disconnected.Iron+disconnected.Coal+disconnected.Oil==0,"A same-culture co-owned city on the opposite side of an unsupported ocean cannot remotely lend its minerals");
        source.Cell=originalCell;source.CultureId=originalCulture;
        check(original.ComputeHash()==hash,"Material supply probe leaves the original historical checkpoint and integrity unchanged");
        Console.WriteLine($"Bounded state supply: {source.Name} -> {target.Name}; Cu {delivered.Copper}, Fe {delivered.Iron}, coal {delivered.Coal}, oil {delivered.Oil}; candidates <=16.");
    }
}
