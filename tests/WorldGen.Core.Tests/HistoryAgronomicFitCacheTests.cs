using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;
public static class HistoryAgronomicFitCacheTests
{
    static double Call(Prehistory engine,string name,params object[] args)=>(double)typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args)!;
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;string hash=source.ComputeHash();
        var cached=new Prehistory(world);var literal=new Prehistory(world){UseAgronomicFitCache=false};cached.Validate(source);literal.Validate(source);
        check(source.History.Version=="history-32","This diagnostic exercises the current agronomic fit cache rather than a legacy branch");
        var sites=Enumerable.Range(0,24).Select(k=>k*(world.Surface.Cells.Length-1)/23).ToArray();
        foreach(int cell in sites)foreach(bool managed in new[]{false,true})
        foreach(int crop in Enumerable.Range(0,HistoricalCropCatalog.Crops.Length))
        {
            var definition=HistoricalCropCatalog.Crops[crop];
            var cultivar=new HistoricalCultivar{CropId=crop,MaturityDays=definition.MaturityDays,DaylengthLatitudeTenths=240,WaterRequirementPermille=definition.MinimumWaterPermille,MinimumWarmTenthsC=definition.MinimumWarmC*10,SelectedYears=1000};
            void Compare()
            {
                double a=Call(cached,"CommunityTransferFit",source,cell,cultivar,managed),b=Call(literal,"CommunityTransferFit",source,cell,cultivar,managed);
                check(BitConverter.DoubleToInt64Bits(a)==BitConverter.DoubleToInt64Bits(b),"Cold/warm fits preserve the literal quantized result for current cultivar traits and management");
                check(Call(cached,"CommunityTransferFit",source,cell,cultivar,managed)==a,"A repeated exact key preserves its original computed value");
            }
            Compare();cultivar.MaturityDays-=20;Compare();cultivar.DaylengthLatitudeTenths+=250;Compare();
            cultivar.WaterRequirementPermille+=200;Compare();cultivar.MinimumWarmTenthsC-=35;Compare();cultivar.SelectedYears+=3000;Compare();
            cultivar.CropId=(crop+1)%HistoricalCropCatalog.Crops.Length;Compare();
        }
        foreach(int culture in Enumerable.Range(0,Math.Min(5,source.History.Cultures.Length)))foreach(int cell in sites)foreach(bool storage in new[]{false,true})
            check(Call(cached,"CommunityCropLandscape",source,culture,cell,storage)==Call(literal,"CommunityCropLandscape",source,culture,cell,storage),"Full field-area and preservation arithmetic is identical with cached versus literal agronomic physics");
        var aFuture=cached.AdvanceYears(source,20);var bFuture=literal.AdvanceYears(source,20);
        check(aFuture.Hash==bFuture.Hash,"Twenty years of dated history, crops, population, wars and accounts match with fit caches enabled versus disabled");
        check(source.ComputeHash()==hash,"Agronomic cache diagnostics do not change the loaded world");
    }
}
