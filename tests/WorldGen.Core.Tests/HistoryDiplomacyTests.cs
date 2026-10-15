using WorldGen.Core;
public static class HistoryDiplomacyTests
{
    public static void Run(Action<bool,string> check)
    {
        // Two shores with a genuinely open-water middle, no alternative land path.
        var world=new World{Recipe=new Recipe(),ElevationM=new[]{100,-3000,-4000,-3000,100},Surface=new Surface(),Ecology=new EcologyResult()};
        world.Surface.Cells=Enumerable.Range(0,5).Select(i=>new Cell{Id=i,Position=new V(Math.Cos(i*.031392246),Math.Sin(i*.031392246),0),AreaM2=10000000000,Neighbors=new[]{i-1,i+1}.Where(j=>j>=0&&j<5).ToArray(),DistanceM=new[]{i-1,i+1}.Where(j=>j>=0&&j<5).Select(_=>200000).ToArray()}).ToArray();
        world.Surface.Edges=Enumerable.Range(0,4).Select(i=>new Edge{A=i,B=i+1,LengthM=200000}).ToArray();
        world.Hydrology.OceanConnected=new[]{false,true,true,true,false};world.Hydrology.LakeCoverageFraction=new double[5];
        world.Ecology.Region=new[]{0,-1,-1,-1,1};world.Ecology.Regions=Enumerable.Range(0,2).Select(i=>new Ecoregion{Id=i,Biome=Biome.Savanna}).ToArray();
        world.Ecology.Habitat=Enumerable.Range(0,5).Select(_=>new HabitatCell{Biome=Biome.Savanna,ProductivityGCM2Year=1000,GrowingDays=200,MeanTenthsC=200,AridityMilli=800}).ToArray();
        world.Stages.Add(new StageRecord{Name="complete",Hash="contact-test"});
        var engine=new Prehistory(world);var culture=new HistoricalCulture();var state=new PrehistoryState{History=new HistoryState{Version="history-5",Cultures=new[]{culture}}};
        check(!engine.CanHistoricalContact(state,0,4,0),"Political contact does not invent sea travel without sailing");
        culture.Technology[(int)HistoricalTechnology.Sailing]=1;
        check(!engine.CanHistoricalContact(state,0,4,0),"Coastal sailing alone cannot traverse the open-water middle");
        culture.Technology[(int)HistoricalTechnology.Navigation]=1;
        check(engine.CanHistoricalContact(state,0,4,0),"Navigation enables a physically bounded sea contact");
        check(engine.CanHistoricalContact(state,0,4,0)==new Prehistory(world).CanHistoricalContact(state,0,4,0),"Cached contact agrees with a fresh geography search");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();bool cancelled=false;
        try{engine.CanHistoricalContact(state,0,4,0,cancellation.Token);}catch(OperationCanceledException){cancelled=true;}
        check(cancelled,"Cached geography contacts retain cancellation");
        state.History.Version="history-4";
        check(!engine.CanHistoricalContact(state,0,4,0),"Historical contact cache cannot leak new reach into legacy histories");
        state.History.Version="history-5";
        check(engine.CanHistoricalContact(state,0,4,0),"Returning to the new model preserves its own physical reach");
    }
}
