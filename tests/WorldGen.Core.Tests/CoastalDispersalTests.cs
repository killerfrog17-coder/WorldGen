using WorldGen.Core;
using WorldGen.Presentation;
public static class CoastalDispersalTests
{
    public static void Run(Action<bool,string> check)
    {
        int[][] neighbors={new[]{1,3},new[]{0,2,3,4},new[]{1,4},new[]{0,1,4},new[]{1,2,3}};
        var w=new World{Recipe=new Recipe(),ElevationM=new[]{100,6000,100,-3000,-3000},Surface=new Surface(),Ecology=new EcologyResult()};
        w.Surface.Cells=Enumerable.Range(0,5).Select(i=>new Cell{Id=i,Position=new V(Math.Cos(i*.03),Math.Sin(i*.03),0),AreaM2=10000000000,Neighbors=neighbors[i],DistanceM=neighbors[i].Select(_=>250000).ToArray()}).ToArray();
        w.Surface.Edges=Enumerable.Range(0,5).SelectMany(i=>neighbors[i].Where(j=>j>i).Select(j=>new Edge{A=i,B=j,LengthM=250000})).ToArray();
        w.Hydrology.OceanConnected=new[]{false,false,false,true,true};w.Hydrology.LakeCoverageFraction=new double[5];
        w.Ecology.Region=new[]{0,1,2,-1,-1};w.Ecology.Regions=Enumerable.Range(0,3).Select(i=>new Ecoregion{Id=i,Biome=Biome.Savanna,Name="Test "+i}).ToArray();
        w.Ecology.Habitat=Enumerable.Range(0,5).Select(i=>new HabitatCell{Biome=Biome.Savanna,ProductivityGCM2Year=1000,GrowingDays=200,MeanTenthsC=200,AridityMilli=800}).ToArray();w.Stages.Add(new StageRecord{Name="complete",Hash="coastal-fixture"});
        var engine=new Prehistory(w);check(engine.Patches[1].Capacity==0,"Mountain remains uninhabitable");
        check(engine.Routes.Any(r=>r.Coastal&&r.A==0&&r.B==2),"Coastal route bypasses uninhabitable barrier");
        var start=engine.Start(engine.Prepare(0));var next=engine.AdvanceYears(start,50000);
        check(next.Population[2]>0&&next.Population[1]==0,"Humans travel around mountain without settling it");
        w.Ecology.Province=new int[5];
        var learned=start.Copy();learned.Society.Knowledge[0]=7000;learned.Seal();var founders=learned;
        for(int year=0;year<50000&&founders.Population[2]==0;year+=100)founders=engine.AdvanceYears(founders,100);
        check(founders.Population[2]>0&&founders.Society.Knowledge[2]>=7000,"Successful founding group carries practical knowledge around an isolated barrier");
        check(learned.Society.Knowledge[2]==0&&learned.ComputeHash()==learned.Hash,"Founding knowledge transfer preserves the input checkpoint");
        var demographic=learned.Copy();demographic.Version="prehistory-3";demographic.Seal();var carried=engine.Advance(demographic,500);
        check(carried.Population[2]>0&&carried.Society.Knowledge[2]>=7000,"Demographic substeps transfer founder knowledge without requiring culture formation");
        check(demographic.ComputeHash()==demographic.Hash&&demographic.Society.Knowledge[2]==0,"Direct demographic continuation does not mutate shared source knowledge");
        w.Prehistory=new PrehistoryState{Version="prehistory-4"};var old=new Prehistory(w);check(!old.Routes.Any(r=>r.Coastal),"Previous route version retained");
        w.Prehistory=null;w.Hydrology.OceanConnected=new bool[5];check(!new Prehistory(w).Routes.Any(r=>r.Coastal),"No imaginary coastal detour through inland mountains");
        check(SocietyView.ColorHex(2)==SocietyView.ColorHex(2)&&SocietyView.ColorHex(2)!=SocietyView.ColorHex(3),"Stable distinct civilization colors");
        check(SocietyView.Short(new string('x',100),30).Length==30,"Bounded selection caption");
        var shelf=new Prehistory.Route{Sea=true,Shallow=true,SeaDropM=80,DistanceM=120000};
        check(Prehistory.RouteChance(shelf,0)==0&&Prehistory.MaritimeChance(shelf,1)>0,"Sailing crosses a submerged shelf without an ice-age landbridge");
        var deep=new Prehistory.Route{Sea=true,DistanceM=120000};
        check(Prehistory.MaritimeChance(shelf,2)==Prehistory.MaritimeChance(deep,2),"Equal sea distances do not penalize a shallow channel");
        check(Prehistory.MaritimeChance(deep,0)==0&&Prehistory.MaritimeChance(deep,2)>Prehistory.MaritimeChance(deep,1),"Learned navigation expands probabilistic sea reach");
    }
}
