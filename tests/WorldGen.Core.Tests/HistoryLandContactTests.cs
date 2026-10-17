using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryLandContactTests
{
    public static void Run(Action<bool,string> check)
    {
        double old=HistoricalSettlementNetworks.AnnualOpportunity(4000,1600,.9,1,false,100);
        check(HistoricalSettlementNetworks.AgrarianOpportunity(4000,1600,.9,1,false,100)==old,"One actual founder cohort does not mint extra settlement attempts");
        check(Math.Abs(HistoricalSettlementNetworks.AgrarianOpportunity(4000,160000,.9,1,false,100)-old*10)<.00000001,"Many actual rural households contribute bounded independent migration opportunities");
        check(HistoricalSettlementNetworks.AgrarianOpportunity(4000,1599,.9,1,false,100)==0,"No migrating households means no agrarian opportunity");
        check(HistoricalSettlementNetworks.AgrarianOpportunity(4000,160000,.9,1,false,10)==0,"A newly founded center is not an instantaneous source of another town");
        check(HistoricalSettlementNetworks.FarmingContinuity(.04,.9,180)==.04,"Foragers and early experiments retain livelihood adaptation constraints");
        check(HistoricalSettlementNetworks.FarmingContinuity(.04,0,500)==.04,"No carried viable crop grants no agrarian familiarity");
        check(HistoricalSettlementNetworks.FarmingContinuity(.04,.64,500)>.67,"Mature farmers need not relearn an unrelated forest foraging diet");
        check(HistoricalSettlementNetworks.FarmingContinuity(.9,.1,500)==.9,"Carried crops do not erase familiar local livelihoods");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;string hash=source.ComputeHash();var engine=new Prehistory(world);
        var legacy=source.Copy();legacy.History.Aggregates.Empires.Connections.Realms.Version=5;
        legacy.History.Aggregates.Empires.Connections.Realms.Claimants=null;legacy.History.Aggregates.Empires.Connections.SeaService=null;legacy.Seal();
        var current=engine.UpgradeRealmLifecycles(legacy);
        object Invoke(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,args)!;
        int Endpoint(PrehistoryState s,Prehistory.Route r,int region)=>(int)Invoke("RegionalContactCell",s,r,region);
        var land=engine.Routes.Where(r=>!r.Sea).ToArray();
        check(land.Length>0&&land.All(r=>r.LandCellA>=0&&r.LandCellB>=0&&engine.CellPatch[r.LandCellA]==r.A&&engine.CellPatch[r.LandCellB]==r.B),"Every land route retains its actual district-boundary endpoints");
        check(land.All(r=>world.Surface.Cells[r.LandCellA].Neighbors.Contains(r.LandCellB)),"New contact endpoints really are adjacent cells, not convenient faraway sites");
        check(land.All(r=>r.CellA<0&&r.CellB<0),"Legacy land endpoint representation and route indices remain unchanged");
        var sample=land.OrderByDescending(r=>D.Angle(world.Surface.Cells[current.History.Aggregates.Regions[r.A].BestCell].Position,world.Surface.Cells[current.History.Aggregates.Regions[r.B].BestCell].Position)).First();
        check(Endpoint(legacy,sample,sample.A)==legacy.History.Aggregates.Regions[sample.A].BestCell,"Revision5 continues using its recorded center-based rules");
        check(Endpoint(current,sample,sample.A)==sample.LandCellA&&Endpoint(current,sample,sample.B)==sample.LandCellB,"Revision6 explicitly uses the actual meeting border");
        var preceding=current.Copy();preceding.History.Aggregates.Empires.Connections.Realms.Version=6;
        check((int)Invoke("MigrantContactCell",preceding,sample,sample.B)==preceding.History.Aggregates.Regions[sample.B].BestCell,"Revision6 retains its old land-migrant center fallback");
        check((int)Invoke("MigrantContactCell",current,sample,sample.B)==sample.LandCellB,"Revision7 migrants retain the actual destination cell needed to carry their local crop tradition");
        int culture=source.History.Polities[0].CultureId;
        double effort=(double)Invoke("RegionalLandRelayDistance",current,sample,culture,sample.A,sample.B,sample.LandCellA,sample.LandCellB);
        double direct=D.Angle(world.Surface.Cells[current.History.Aggregates.Regions[sample.A].BestCell].Position,world.Surface.Cells[current.History.Aggregates.Regions[sample.B].BestCell].Position)*world.Recipe.RadiusKm;
        check(effort+.0001>=direct,"Border contact still accounts for travel and terrain through both district interiors");
        double oldDistance=(double)Invoke("RegionalLandRelayDistance",legacy,sample,culture,sample.A,sample.B,sample.LandCellA,sample.LandCellB);
        check(oldDistance==sample.DistanceM/1000.0+Math.Min(450,engine.Patches[sample.A].RadiusKm+engine.Patches[sample.B].RadiusKm),"Legacy relay arithmetic is exactly preserved");
        engine.Validate(current);check(source.ComputeHash()==hash,"Border-route diagnostics/explicit upgrade leave the saved source untouched");
        world.Prehistory=current;var loaded=WorldFile.Read(WorldFile.Write(world));
        check(loaded.Prehistory.Hash==current.Hash,"New border rules and optional ledgers survive compact persistence");
    }
}
