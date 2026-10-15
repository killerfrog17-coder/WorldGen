using WorldGen.Core;
public static class HistoryCropEcologyTests
{
    public static void Run(Action<bool,string> check)
    {
        var warm=new HabitatCell{ColdTenthsC=250,WarmTenthsC=280,AridityMilli=1400,Habitat=Habitat.Forest};
        var seasonal=new HabitatCell{ColdTenthsC=80,WarmTenthsC=240,AridityMilli=650,WarmSeasonDryPermille=750,Habitat=Habitat.Open};
        check(HistoricalCropEcology.WildAnnualOpportunity(warm,.10,0)<.1&&HistoricalCropEcology.WildAnnualOpportunity(seasonal,.75,0)>.6,"Wild annual cereal progenitors favor seasonal open habitats over continuously warm closed forest");
        check(HistoricalCropEcology.WildAnnualOpportunity(warm,.10,0)>0,"Marginal grain origins retain a rare ecological tail rather than a climatic ban");
        check(HistoricalCropEcology.WildAnnualOpportunity(warm,.10,3)==1,"Tropical tuber gardening is not confused with temperate wild-grain ecology");
        var olive=new HistoricalCultivar{CropId=11};
        check(HistoricalCropEcology.ReproductiveFit(warm,olive)<HistoricalCropEcology.ReproductiveFit(seasonal,olive),"Olive fruiting requires appropriate winter conditions, not merely a minimum summer temperature");
        var dates=new HistoricalCultivar{CropId=12};
        check(HistoricalCropEcology.ReproductiveFit(warm,dates)<.1&&HistoricalCropEcology.ReproductiveFit(seasonal,dates)>.8,"Date ripening distinguishes rainy warm seasons from dry irrigable ones");
        var coconut=new HistoricalCultivar{CropId=13};
        check(HistoricalCropEcology.ReproductiveFit(warm,coconut)>HistoricalCropEcology.ReproductiveFit(seasonal,coconut),"Coconut and olive have distinct perennial climatic niches");
        var hot=new HabitatCell{WarmTenthsC=320};var cool=new HabitatCell{WarmTenthsC=180};var potato=new HistoricalCultivar{CropId=10};
        check(HistoricalCropEcology.ReproductiveFit(hot,potato)<HistoricalCropEcology.ReproductiveFit(cool,potato),"Potato tuberization does not improve indefinitely with heat");
        check(HistoricalCropCalendarEffects.GrowingWindowWarmth(200,400,300,150,6)<220,"A cool-season tuber can choose a different adjacent-season window from a tropical root crop");
    }
}
