using WorldGen.Core;
using System.Collections;
using System.Reflection;
public static class HistoryContactCacheTests
{
    public static void Run(Action<bool,string> check)
    {
        const int width=12,n=width*width;
        var world=new World{Recipe=new Recipe(),Surface=new Surface(),Ecology=new EcologyResult(),ElevationM=new int[n]};
        world.Surface.Cells=Enumerable.Range(0,n).Select(i=>
        {
            int x=i%width,y=i/width;
            var neighbors=new[]{x>0?i-1:-1,x<width-1?i+1:-1,y>0?i-width:-1,y<width-1?i+width:-1}.Where(j=>j>=0).ToArray();
            return new Cell{Id=i,Position=new V(1,(x-6)*.01,(y-6)*.01).Unit,AreaM2=10000000000L,Neighbors=neighbors,DistanceM=neighbors.Select(j=>70000+(i+j)%13*1000).ToArray()};
        }).ToArray();
        world.Surface.Edges=world.Surface.Cells.SelectMany(c=>c.Neighbors.Where(j=>j>c.Id).Select(j=>new Edge{A=c.Id,B=j,LengthM=70000})).ToArray();
        world.Hydrology.OceanConnected=new bool[n];world.Hydrology.LakeCoverageFraction=new double[n];
        world.Ecology.Region=new int[n];world.Ecology.Regions=new[]{new Ecoregion{Id=0,Biome=Biome.Savanna}};
        world.Ecology.Habitat=Enumerable.Range(0,n).Select(i=>new HabitatCell{Biome=i%7==0?Biome.TropicalRainforest:Biome.Savanna,ReliefM=i%9*150,AridityMilli=i%3==0?150:800,GrowingDays=150+i%100,ProductivityGCM2Year=1000,MeanTenthsC=200}).ToArray();
        world.Stages.Add(new StageRecord{Name="complete",Hash="contact-grid-test"});
        var state=new PrehistoryState{Years=1000,History=new HistoryState{Version="history-5",Cultures=new[]{new HistoricalCulture{Technology=new int[HistoricalTechnologies.Names.Length]}}},Society=new SocietyState{Cultures=new[]{new CultureIdentity{OriginCell=0}}}};
        var indexed=new Prehistory(world);var literal=new Prehistory(world){UseHistoryIndexes=false};
        bool same=true;
        for(int from=n-1;from>=0;from--)for(int to=0;to<n;to++)
            same&=indexed.CanHistoricalContact(state,from,to,0)==literal.CanHistoricalContact(state,from,to,0);
        check(same,"Resumed source searches match independent directed Dijkstra for every grid pair, including unreachable targets");
        var pairs=(IDictionary)typeof(Prehistory).GetField("civicContactCache",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(indexed)!;
        check(pairs.Count==n*n&&pairs.Count>8192,"Contact cache does not wipe out all prior answers at the old8192-entry boundary");
        foreach(string version in new[]{"history-15","history-16","history-5"})
        {
            state.History.Version=version;
            foreach(bool roads in new[]{true,false})
            {
                state.History.Cultures[0].Technology[(int)HistoricalTechnology.Roads]=roads?1:0;
                foreach(int origin in new[]{0,1})
                {
                    state.Society.Cultures[0].OriginCell=origin;same=true;
                    for(int from=0;from<n;from+=11)for(int to=n-1;to>=0;to-=7)
                        same&=indexed.CanHistoricalContact(state,from,to,0)==literal.CanHistoricalContact(state,from,to,0);
                    check(same,version+" contact keys preserve roads, directed ecological barriers and origin aridity");
                }
            }
        }
        state.History.Version="history-16";var culture=state.History.Cultures[0];culture.Technology[(int)HistoricalTechnology.Railways]=950;
        foreach(int year in new[]{950,975,1030,975})
        {
            state.Years=year;same=true;
            for(int from=0;from<n;from+=13)for(int to=0;to<n;to+=5)same&=indexed.CanHistoricalContact(state,from,to,0)==literal.CanHistoricalContact(state,from,to,0);
            check(same,"Transport adoption and revisiting an earlier branch cannot reuse the wrong movement costs");
        }
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();bool stopped=false;
        try{indexed.CanHistoricalContact(state,0,1,0,cancelled.Token);}catch(OperationCanceledException){stopped=true;}
        check(stopped,"Physical-cache hits honor a cancelled token");
        check(indexed.CanHistoricalContact(state,0,n-1,0)==literal.CanHistoricalContact(state,0,n-1,0),"Cancellation cannot poison a later complete physical query");
    }
}
