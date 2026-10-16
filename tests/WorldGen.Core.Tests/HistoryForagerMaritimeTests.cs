using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryForagerMaritimeTests
{
    static object Call(Prehistory e,string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(e,values)!;
    public static void Run(Action<bool,string> check)
    {
        double state=HistoricalEnvironmentalEffects.HomelandMultiplier(0,true);
        check(state==.00001&&HistoricalForagerMaritime.RecognitionHomeland(state)==1,"Recognizable ancestral forager traditions have no geographical exclusion while the pristine-state prior remains exact");
        check(HistoricalForagerMaritime.RecognitionHomeland(1)==1&&HistoricalForagerMaritime.RecognitionHomeland(.4)==1,"Forager recognition is independent of state-origin distance, not of actual ecology or resource constraints");
        long coarse=HistoricalForagerMaritime.CatchmentResidents(5000,30000000000L,20000),fine=HistoricalForagerMaritime.CatchmentResidents(500,3000000000L,20000);
        check(coarse==fine&&coarse>5000,"A fixed physical coastal catchment preserves support across coarse and fine equal-density cells");
        check(HistoricalForagerMaritime.CatchmentResidents(5000,30000000000L,1200)==1200,"A tiny actual region cannot supply hypothetical extra specialist households");
        check(HistoricalForagerMaritime.CatchmentResidents(0,3000000000L,20000)==0&&HistoricalForagerMaritime.CatchmentResidents(500,3000000000L,0)==0,"Neither an empty cell nor an empty regional actor ledger supplies imagined catchment residents");
        double food=HistoricalMarineForagerEffects.FoodSupport(true,true,true,1,1,150,false);
        double support=HistoricalForagerMaritime.Support(food,650,5000,true);
        check(support==1,"Real productive marine food and a local fishing community can support portable boatbuilding");
        check(HistoricalForagerMaritime.Support(food,650,0,true)==0&&HistoricalForagerMaritime.Support(food,650,199,true)==0,"Culture-wide population cannot substitute for a populated local shore");
        check(HistoricalForagerMaritime.Support(food,200,5000,true)==0&&HistoricalForagerMaritime.Support(food,650,5000,false)==0,"Actual maritime practice and specialized livelihoods remain necessary");
        foreach(var args in new (bool coast,bool fish,bool shell,double water,bool ice)[]{(false,true,true,1,false),(true,false,false,1,false),(true,true,true,0,false),(true,true,true,1,true)})
        {
            double missing=HistoricalMarineForagerEffects.FoodSupport(args.coast,args.fish,args.shell,1,args.water,150,args.ice);
            check(HistoricalForagerMaritime.Support(missing,650,5000,true)==0,"Inland, resource-empty, waterless and permanent-ice coasts grant no portable marine capacity");
        }
        double coldFood=HistoricalMarineForagerEffects.FoodSupport(true,true,true,1,1,-40,false);
        check(HistoricalForagerMaritime.Support(coldFood,650,5000,true)>0&&HistoricalForagerMaritime.Support(coldFood,650,5000,true)<support,"A real cold-water fishing tradition is possible without equating it to a warm productive shelf");
        double extreme=HistoricalMarineForagerEffects.FoodSupport(true,true,true,1,1,-200,false);
        check(HistoricalForagerMaritime.Support(extreme,650,5000,true)==0,"Negligibly accessible hypercold marine food does not create local specialist support");
        double capital=HistoricalForagerMaritime.DeploymentCapital((int)HistoricalTechnology.Sailing,.4,support);
        double storage=HistoricalForagerMaritime.DeploymentCapital((int)HistoricalTechnology.Storage,.4,support);
        double sailing=Math.Min(1,storage/.7)*capital;
        double navigation=Math.Min(1,Math.Min(sailing/.7,storage/.7))*HistoricalForagerMaritime.DeploymentCapital((int)HistoricalTechnology.Navigation,.4,support);
        double ocean=Math.Min(1,Math.Min(navigation/.7,Math.Min(sailing/.7,storage/.7)))*HistoricalForagerMaritime.DeploymentCapital((int)HistoricalTechnology.OceanNavigation,.4,support);
        check(.4/.7*.4<.25&&sailing>=.25&&navigation>=.25&&ocean>=.25,"A genuinely supported nonurban craft chain can cross the practical250 deployment threshold while preserving prerequisite scaling");
        check(sailing*.3<.25,"Material scarcity still constrains the same boatbuilding deployment");
        foreach(var technology in new[]{HistoricalTechnology.Copper,HistoricalTechnology.Writing,HistoricalTechnology.Irrigation,HistoricalTechnology.Railways,HistoricalTechnology.NuclearWeapons})
            check(HistoricalForagerMaritime.DeploymentCapital((int)technology,.4,1)==.4,"Marine support grants no unrelated urban or industrial capacity: "+technology);
        check(HistoricalForagerMaritime.DeploymentCapital((int)HistoricalTechnology.Sailing,.9,1)==.9&&HistoricalForagerMaritime.DeploymentCapital(3,.4,0)==.4,"The portable path neither reduces an existing funded specialist network nor boosts an unsupported coast");
        check(HistoricalForagerMaritime.Provisioning(100,1)==300&&HistoricalForagerMaritime.Provisioning(600,1)==600&&HistoricalForagerMaritime.Provisioning(100,0)==100,"Maritime-only provisions are bounded and preserve existing actual agricultural surplus");
        check(!HistoricalTechnicalGraph.Prerequisites((int)HistoricalTechnology.Navigation,new int[HistoricalTechnicalGraph.Count]),"Portable support does not remove unfamiliar navigation's research prerequisites");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string hash=original.ComputeHash();var engine=new Prehistory(world);engine.Validate(original);var s=original.Copy();
        int cell=Enumerable.Range(0,world.Surface.Cells.Length).Where(c=>engine.CellPatch[c]>=0&&s.History.Aggregates.Regions[engine.CellPatch[c]].Culture<0)
            .Where(c=>(double)Call(engine,"MarineForagerSupport",s,c)>.6&&HistoricalForagerMaritime.CatchmentResidents((long)Call(engine,"AggregateRuralCellPopulation",s,c),world.Surface.Cells[c].AreaM2,
                (long)Call(engine,"ProvincialContactResidents",s,engine.CellPatch[c],false))>=2500).DefaultIfEmpty(-1).First();
        if(cell<0)throw new Exception("Use an actual inhabited productive shore such as the N Aborea73 prestate world; this fixture does not invent marine resources or residents.");
        int region=engine.CellPatch[cell],id=s.Society.Cultures.Length;double statePrior=(double)Call(engine,"HomelandOriginFactor",s,cell);
        var queue=new SimulationEventQueue(s.Timeline.Events);var field=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.NonPublic|BindingFlags.Instance)!;field.SetValue(engine,queue);
        long population=s.History.Aggregates.TotalPopulation;int towns=s.Society.Settlements.Length,polities=s.History.Polities.Length;
        Call(engine,"FoundAggregateCulture",s,region,cell,-1);field.SetValue(engine,null);s.Timeline.Events=queue.Ordered();
        var c=s.Society.Cultures[id];var hc=s.History.Cultures[id];
        double support=(double)Call(engine,"ForagerMaritimeSupport",s,id,true);
        check(support>0&&c.Fishing>=350,"An actual inhabited productive Aborea shore provides nonurban marine specialist support");
        check(s.Society.Settlements.Length==towns&&s.History.Polities.Length==polities&&s.History.Aggregates.TotalPopulation==population&&c.Farming==0,"The actual marine-capacity path grants no town, state, farm or person");
        check(hc.Technical.Levels[(int)HistoricalTechnology.Sailing]==0&&hc.Technical.Deployment[(int)HistoricalTechnology.Sailing]==0,"Querying capacity never grants unknown sailing or installed boats");
        check(hc.Surplus==0&&(double)Call(engine,"HomelandOriginFactor",s,cell)==statePrior,"Maritime-specific provisioning changes neither global fiscal surplus nor state-origin geography");
        long actualLocal=HistoricalForagerMaritime.CatchmentResidents((long)Call(engine,"AggregateRuralCellPopulation",s,cell),world.Surface.Cells[cell].AreaM2,(long)Call(engine,"ProvincialContactResidents",s,region,false));
        long priorPopulation=s.Population[region];var priorLayers=s.History.Aggregates.Cells;s.Population[region]=0;
        // A late-world fixture may also have a real market/state rural cohort.
        // The controlled empty-shore contrast removes its local attribution,
        // not merely the unallocated pool while leaving real rural people.
        s.History.Aggregates.Cells=priorLayers.With(cell*5+1,-1).With(cell*5+3,-1);
        check((double)Call(engine,"ForagerMaritimeSupport",s,id,true)==0,"An empty actual shore remains unsupported even when its culture population ledger is nonzero");
        s.Population[region]=priorPopulation;s.History.Aggregates.Cells=priorLayers;
        string version=s.History.Version;s.History.Version=Prehistory.EnvironmentalHistoryVersion;
        check((double)Call(engine,"ForagerMaritimeSupport",s,id,true)==0,"History31 retains its exact old portable capacity behavior");s.History.Version=version;
        check((double)Call(engine,"ForagerMaritimeSupport",s,id,true)==support&&actualLocal>=2500,"Restoring the same actual local resident cohort restores the same capacity");
        s.Seal();engine.Validate(s);world.Prehistory=s;var saved=WorldFile.Read(WorldFile.Write(world));
        check(saved.Prehistory.Hash==s.Hash&&original.ComputeHash()==hash,"The ordinary newly founded culture and its existing ledgers save with integrity while the source world remains unchanged");
    }
}
