using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;
public static class HistoryMarineForagerTests
{
    static object Call(Prehistory engine,string method,params object[] values)=>typeof(Prehistory).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,values);
    public static void Run(Action<bool,string> check)
    {
        double rich=HistoricalMarineForagerEffects.FoodSupport(true,true,true,.8,1,160,false);
        check(rich>.8&&rich<=1,"Actual fish and shellfish with accessible shelf and drinking water can support specialized forager aggregation");
        check(HistoricalMarineForagerEffects.FoodSupport(false,true,true,1,1,160,false)==0,"An inland cell cannot acquire marine subsistence from an ocean-only regional label");
        check(HistoricalMarineForagerEffects.FoodSupport(true,false,false,1,1,160,false)==0,"A coastline without actual feeding resources supplies no imaginary fish");
        check(HistoricalMarineForagerEffects.FoodSupport(true,true,true,1,0,160,false)==0&&HistoricalMarineForagerEffects.FoodSupport(true,true,true,1,1,160,true)==0,"Seafood does not remove drinking-water or permanent-glacier constraints");
        check(HistoricalMarineForagerEffects.FoodSupport(true,false,true,1,1,160,false)>0,"Shore gathering can matter without inventing a cereal progenitor or a fishing fleet");
        check(HistoricalMarineForagerEffects.FoodSupport(true,true,true,.1,1,160,false)<rich&&HistoricalMarineForagerEffects.FoodSupport(true,true,true,.8,1,-40,false)<rich,"Shelf access and difficult seasonal access vary aquatic opportunity rather than making every coast identical");
        double chance=HistoricalMarineForagerEffects.IndependentCultureOpportunity(rich,.12,1);
        check(chance>0&&chance<.0001&&HistoricalMarineForagerEffects.IndependentCultureOpportunity(rich,0,1)==0,"Marine opportunities remain rare occupied-tradition hazards, not an immediate cultural unlock");
        check(HistoricalMarineForagerEffects.IndependentCultureOpportunity(rich,.12,.00001)<chance/50000,"The new livelihood does not bypass the owner's anti-human-homeland origin prior");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);var source=world.Prehistory;string hash=source.ComputeHash();var s=source.Copy();engine.Validate(s);
        int cell=Enumerable.Range(0,world.Surface.Cells.Length).Where(c=>engine.CellPatch[c]>=0&&s.Population[engine.CellPatch[c]]>=400&&s.History.Aggregates.Regions[engine.CellPatch[c]].Culture<0)
            .Where(c=>(double)Call(engine,"MarineForagerSupport",s,c)>.2&&(int)Call(engine,"CommunityNativeCropMask",s,c)==0).DefaultIfEmpty(-1).First();
        if(cell<0)throw new InvalidOperationException("Marine-forager fixture needs an actual inhabited productive coast with no local crop progenitors.");
        int region=engine.CellPatch[cell];double support=(double)Call(engine,"MarineForagerSupport",s,cell);var legacy=s.Copy();legacy.History.Version=Prehistory.EnvironmentalHistoryVersion;
        check(support>.2&&(double)Call(engine,"MarineForagerSupport",legacy,cell)==0,"An actual crop-free occupied coast supports the32-only resource path, while31 retains its old rules");
        var queue=new SimulationEventQueue(s.Timeline.Events);var field=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.NonPublic|BindingFlags.Instance)!;field.SetValue(engine,queue);
        long population=s.History.Aggregates.TotalPopulation;int cultures=s.Society.Cultures.Length,states=s.History.Polities.Length,cities=s.Society.Settlements.Length;
        Call(engine,"FoundAggregateCulture",s,region,cell,-1);field.SetValue(engine,null);s.Timeline.Events=queue.Ordered();
        var culture=s.Society.Cultures[cultures];
        check(s.Society.Cultures.Length==cultures+1&&culture.Fishing>=350&&culture.Fishing+culture.Hunting+culture.Gathering==1000,"Actual culture creation initializes a plausible, conserved marine livelihood instead of the inland Woodland default");
        check(culture.Farming==0&&s.History.Polities.Length==states&&s.Society.Settlements.Length==cities&&s.History.Aggregates.TotalPopulation==population,"A specialized marine culture creates neither agriculture, states, cities nor people");
        check(s.History.Agriculture[cultures].NativeMask==0&&s.History.Agriculture[cultures].AdoptedYear.All(y=>y==0),"Marine recognition never invents or domesticates unavailable crops");
        s.Seal();engine.Validate(s);world.Prehistory=s;var saved=WorldFile.Read(WorldFile.Write(world));
        check(saved.Prehistory.Hash==s.Hash&&source.ComputeHash()==hash,"The real marine-forager actor saves with integrity without changing its source world");
    }
}
