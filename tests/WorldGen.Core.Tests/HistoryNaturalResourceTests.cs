using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryNaturalResourceTests
{
    static HabitatCell Habitat(bool forest=false)=>new HabitatCell{Habitat=forest?WorldGen.Core.Habitat.Forest:WorldGen.Core.Habitat.Open,Biome=forest?Biome.TemperateForest:Biome.Grassland,
        ProductivityGCM2Year=700,GrowingDays=240,SoilFertilityPermille=650,AridityMilli=700,WaterTableCm=500,MeanTenthsC=180,ReliefM=150};
    static World Fixture(int count=257)
    {
        var cells=new Cell[count];for(int i=0;i<count;i++){double angle=i*.0025;cells[i]=new Cell{Id=i,Position=new V(Math.Cos(angle),Math.Sin(angle),0),AreaM2=100000000,Neighbors=Array.Empty<int>()};}
        return new World{Recipe=new Recipe{Seed=617231},Surface=new Surface{Cells=cells},Ecology=new EcologyResult{Habitat=Enumerable.Range(0,count).Select(_=>Habitat()).ToArray()},
            Hydrology=new HydrologyResult{OceanConnected=new bool[count],NorthernSpringDischargeM3PerS=new double[count],NorthernSummerDischargeM3PerS=new double[count],NorthernAutumnDischargeM3PerS=new double[count],NorthernWinterDischargeM3PerS=new double[count]},
            ElevationM=Enumerable.Repeat(200,count).ToArray(),FoldPermille=new int[count],CratonPermille=Enumerable.Repeat(500,count).ToArray(),BasinPermille=Enumerable.Repeat(500,count).ToArray()};
    }
    static bool Equal(HistoricalResourcePotential a,HistoricalResourcePotential b)=>Enumerable.Range(0,HistoricalNaturalResources.Count).All(i=>a[(HistoricalNaturalResource)i]==b[(HistoricalNaturalResource)i])&&a.SyntheticEarthGeology==b.SyntheticEarthGeology;
    public static void Run(Action<bool,string> check)
    {
        var w=Fixture();var initial=Enumerable.Range(0,w.Surface.Cells.Length).Select(i=>HistoricalNaturalResources.At(w,i)).ToArray();
        check(initial.All(p=>Enumerable.Range(0,HistoricalNaturalResources.Count).All(i=>p[(HistoricalNaturalResource)i]>=0&&p[(HistoricalNaturalResource)i]<=1000)),"Every ordered resource quality is a finite0–1000 potential");
        check(Enumerable.Range(0,HistoricalNaturalResources.Count).Select(i=>HistoricalNaturalResources.Name((HistoricalNaturalResource)i)).Distinct().Count()==HistoricalNaturalResources.Count,"Stable resource IDs have distinct readable labels");
        check(Enumerable.Range(0,initial.Length).Reverse().All(i=>Equal(initial[i],HistoricalNaturalResources.At(w,i))),"Resource queries do not depend on visitation order");
        var cache=new HistoricalNaturalResourceCache(w,2);for(int i=0;i<initial.Length;i++)check(Equal(cache.At(i),initial[i]),"Cold bounded resource cache equals literal query");
        check(cache.Count==2&&Equal(cache.At(0),initial[0]),"FIFO eviction preserves exact recalculation and memory bound");
        cache.Clear();check(cache.Count==0&&Equal(cache.At(0),initial[0]),"Clearing derived resource data cannot change the world");
        double nearby=0,distant=0;for(int i=1;i<initial.Length;i++){nearby+=Math.Abs(initial[i].Copper-initial[i-1].Copper);distant+=Math.Abs(initial[i].Copper-initial[(i+initial.Length/2)%initial.Length].Copper);}
        check(distant>nearby*3&&initial.Select(p=>p.Copper).Distinct().Count()>12,"Deposits form spatially coherent provinces and varied patches rather than cellwise white noise");
        int copper=Enumerable.Range(0,initial.Length).OrderByDescending(i=>initial[i].Copper).First();int before=initial[copper].Copper;w.FoldPermille[copper]=1000;
        check(HistoricalNaturalResources.At(w,copper).Copper>before,"Magmatic-fold provenance raises copper prospectivity at an otherwise identical site");
        int fossil=Enumerable.Range(0,initial.Length).OrderByDescending(i=>initial[i].Coal+initial[i].Oil).First();
        foreach(var kind in new[]{HistoricalNaturalResource.Coal,HistoricalNaturalResource.Oil})
        {
            int cell=Enumerable.Range(0,initial.Length).OrderByDescending(i=>initial[i][kind]).First();
            w.BasinPermille[cell]=0;var noBasin=HistoricalNaturalResources.At(w,cell);w.BasinPermille[cell]=1000;var basin=HistoricalNaturalResources.At(w,cell);
            check(basin[kind]>noBasin[kind],"Buried sedimentary-basin history favors "+kind+" where its separate formation history permits a deposit");
        }
        w.Ecology.Habitat[fossil]=Habitat(true);var forest=HistoricalNaturalResources.At(w,fossil);w.Ecology.Habitat[fossil]=Habitat();w.Ecology.Habitat[fossil].Biome=Biome.Desert;w.Ecology.Habitat[fossil].AridityMilli=30;var desert=HistoricalNaturalResources.At(w,fossil);
        check(forest.Coal==desert.Coal&&forest.Oil==desert.Oil,"Present-day forest and desert do not determine ancient coal or petroleum formation");
        check(forest.Timber>desert.Timber&&forest.CharcoalPotential>desert.CharcoalPotential,"Local vegetation supplies explicit timber/charcoal alternatives rather than automatic coal");
        var dry=Habitat();dry.AridityMilli=20;dry.WaterTableCm=4000;w.Ecology.Habitat[0]=dry;var dryValue=HistoricalNaturalResources.At(w,0);w.Hydrology.NorthernSpringDischargeM3PerS[0]=w.Hydrology.NorthernSummerDischargeM3PerS[0]=w.Hydrology.NorthernAutumnDischargeM3PerS[0]=w.Hydrology.NorthernWinterDischargeM3PerS[0]=1000;
        check(HistoricalNaturalResources.At(w,0).Freshwater>dryValue.Freshwater+500,"A real perennial river supplies water in an otherwise dry region");
        w.Ecology.Habitat[0].SalinityMilliPpt=35000;var salty=HistoricalNaturalResources.At(w,0);
        check(salty.Salt==1000&&salty.Freshwater==0&&salty.Pasture==0,"Saline water is a salt resource, not free fresh water or pasture");
        w.Hydrology.OceanConnected[0]=true;w.ElevationM[0]=-5000;var ocean=HistoricalNaturalResources.At(w,0);
        check(ocean.Stone==0&&ocean.Timber==0&&ocean.Fertility==0&&ocean.Oil==0,"Deep ocean does not become a terrestrial mine, forest, farm or free offshore oil field");
        w.Recipe.TerrainSource="Earth";var earth=HistoricalNaturalResources.At(w,copper);w.FoldPermille[copper]=0;w.CratonPermille[copper]=0;w.BasinPermille[copper]=0;
        check(earth.SyntheticEarthGeology&&Equal(earth,HistoricalNaturalResources.At(w,copper)),"Measured Earth relief is not misrepresented as a measured geology inventory or its procedural plates as real ore evidence");
        check(w.Features.Length==0&&w.Surface.Cells.All(c=>c.Neighbors.Length==0),"Resource query needs neither feature membership scans nor a terrain-neighbor census");
        Console.WriteLine($"Natural-resource coherence: adjacent copper difference {nearby/(initial.Length-1):F2}, half-transect difference {distant/(initial.Length-1):F2} permille.");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));string stages=string.Join(";",w.Stages.Select(s=>s.Hash));var cache=new HistoricalNaturalResourceCache(w,128);var watch=Stopwatch.StartNew();long total=0;
        int stride=Math.Max(1,w.Surface.Cells.Length/1000),sampled=0;
        for(int cell=0;cell<w.Surface.Cells.Length;cell+=stride)
        {
            var literal=HistoricalNaturalResources.At(w,cell);check(Equal(literal,cache.At(cell)),"Saved world cached/literal potential equality");
            for(int k=0;k<HistoricalNaturalResources.Count;k++){int value=literal[(HistoricalNaturalResource)k];check(value>=0&&value<=1000,"Saved-world potential range");total+=value;}
            sampled++;
        }
        check(string.Join(";",w.Stages.Select(s=>s.Hash))==stages,"Resource inspection does not alter accepted generation stages");
        Console.WriteLine($"Natural resources: {sampled} sites, {watch.Elapsed.TotalMilliseconds:F1}ms, checksum {total}, version {HistoricalNaturalResources.Version}.");
    }
}
