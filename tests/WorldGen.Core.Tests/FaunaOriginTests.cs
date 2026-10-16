using System.Reflection;
using WorldGen.Core;

public static class FaunaOriginTests
{
    static readonly MethodInfo Penalty=typeof(Biogeography).GetMethod("OriginHabitatPenalty",BindingFlags.NonPublic|BindingFlags.Static)!;
    static readonly MethodInfo Find=typeof(Biogeography).GetMethod("FindOrigin",BindingFlags.NonPublic|BindingFlags.Static)!;
    static Taxon Animal()=>new Taxon{Id=0,Name="Large terrestrial test lineage",Rank=TaxonRank.Lineage,MassKg=100,Habitats=Habitat.Open,Occupies=Stratum.Ground,
        MinColdTenthsC=-600,MinWarmTenthsC=-600,MaxWarmTenthsC=600,Lineage=new AnimalLineage{Sources=new[]{new AnimalNativeSource{Realm="test",Latitude=0,Longitude=0}}}};
    static World Chain(int count,long area)
    {
        return new World{Recipe=new Recipe{EcologyProfile=EarthFaunaPack.Version,TerrainSource="Earth"},
            Surface=new Surface{Cells=Enumerable.Range(0,count).Select(i=>new Cell{Id=i,Position=new V(Math.Cos(i*.001),Math.Sin(i*.001),0),AreaM2=area,
                Neighbors=Enumerable.Range(0,count).Where(j=>Math.Abs(j-i)==1).ToArray(),DistanceM=Enumerable.Repeat(1000,i==0||i==count-1?1:2).ToArray()}).ToArray()},
            ElevationM=Enumerable.Repeat(100,count).ToArray(),Landmass=new int[count],Hydrology=new HydrologyResult{OceanConnected=new bool[count]},
            Ecology=new EcologyResult{Habitat=Enumerable.Range(0,count).Select(_=>new HabitatCell{Habitat=Habitat.Open,ColdTenthsC=100,WarmTenthsC=250,AridityMilli=700}).ToArray()}};
    }
    static long Cost(World w,Taxon t,CancellationToken token=default)=>(long)Penalty.Invoke(null,new object[]{w,t,0,token})!;
    static int Origin(World w,Taxon t)=>(int)Find.Invoke(null,new object[]{w,t,new Dictionary<string,int>(),new List<int>(),new Dictionary<int,int>{{0,w.Surface.Cells.Length}},w.Surface.Cells.Length,default(CancellationToken),t.Lineage!.Sources[0]})!;
    public static void Run(Action<bool,string> check)
    {
        var t=Animal();var coarse=Chain(1,2000000000L);var fine=Chain(32,62500000L);
        check(Cost(coarse,t)==Cost(fine,t)&&Cost(coarse,t)>0,"Origin breadth is physical area, not a small-component cell count");
        check(Cost(Chain(3,10000000000L),t)==0,"A measured broad resident patch has no provenance penalty");
        check(Cost(Chain(100,1000000L),t)==0,"A budget-censored open habitat frontier is unknown, not proven undersized");
        var knownSmall=Chain(64,1000000L);
        check(Cost(knownSmall,t)>99000,"An exhausted 64-cell tiny fragment remains distinguishable from an open frontier");
        var sea=Chain(3,15000000000L);sea.Hydrology.OceanConnected[1]=true;
        check(Cost(sea,t)==50000,"Source preference cannot count suitable land reached only across an ocean cell");
        var barrier=Chain(3,15000000000L);barrier.Ecology.Habitat[1].Habitat=Habitat.Forest;
        check(Cost(barrier,t)==50000,"Resident breadth does not invent usable habitat in an unsuitable transit barrier");
        using(var canceled=new CancellationTokenSource())
        {
            canceled.Cancel();bool stopped=false;try{Cost(fine,t,canceled.Token);}catch(TargetInvocationException ex)when(ex.InnerException is OperationCanceledException){stopped=true;}
            check(stopped,"Fixed source probes retain cooperative cancellation");
        }
        var choices=Chain(3,15000000000L);choices.Surface.Cells[0].AreaM2=1000000000L;choices.Surface.Cells[0].Neighbors=Array.Empty<int>();
        choices.Surface.Cells[1].Neighbors=new[]{2};choices.Surface.Cells[2].Neighbors=new[]{1};
        choices.Surface.Cells[1].Position=new V(Math.Cos(.08),Math.Sin(.08),0);choices.Surface.Cells[2].Position=new V(Math.Cos(.09),Math.Sin(.09),0);
        check(Origin(choices,t)==1,"Soft regional breadth can prefer a constrained nearby broad source over the nearest isolated stamp");
        choices.Recipe.EcologyProfile=NativeLifePack.Version;check(Origin(choices,t)==0,"Native-2 source selection keeps the exact nearest eligible origin");
        choices.Recipe.EcologyProfile=EarthFaunaPack.Version;
        foreach(string kind in new[]{"small","flying","coastal","aquatic","freshwater","producer"})
        {
            var untouched=Animal();switch(kind){case "small":untouched.MassKg=9;break;case "flying":untouched.Occupies|=Stratum.Air;break;
                case "coastal":untouched.Lineage.RequiresCoast=true;break;case "aquatic":untouched.Lineage.Aquatic=true;break;
                case "freshwater":untouched.Lineage.RequiresFreshwater=true;break;case "producer":untouched.Producer=true;untouched.Rank=TaxonRank.Species;break;}
            // Eligibility remains real: add the required habitat to both choices.
            foreach(var h in choices.Ecology.Habitat)h.Habitat=Habitat.Open|Habitat.Coastal|Habitat.Freshwater;
            check(Origin(choices,untouched)==0,"Source breadth leaves this excluded category unchanged: "+kind);
        }
        var rare=Chain(1,1000000000L);check(Origin(rare,t)==0,"A genuinely isolated source is allowed; no minimum range or extra seed is invented");
        rare.Ecology.Habitat[0].Habitat=Habitat.Forest;check(Origin(rare,t)==-1,"No eligible source remains a valid absence, not a guaranteed lineage");
        check(Origin(choices,t)==Origin(choices,t),"Source candidate ties and probes are deterministic");
    }
}
