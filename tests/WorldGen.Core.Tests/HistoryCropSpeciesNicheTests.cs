using WorldGen.Core;
using System.Text.Json;
public static class HistoryCropSpeciesNicheTests
{
    static HabitatCell Habitat(int warmth=146)=>new HabitatCell{WarmTenthsC=warmth,ColdTenthsC=36,GrowingDays=273,AridityMilli=950,SoilFertilityPermille=650,ReliefM=60,Habitat=WorldGen.Core.Habitat.Forest,Biome=Biome.TemperateForest};
    static HistoricalCultivar Potato()=>new HistoricalCultivar{CropId=10,MaturityDays=110,MinimumWarmTenthsC=110,WaterRequirementPermille=450,DaylengthLatitudeTenths=400,SelectedYears=100};
    public static void Run(Action<bool,string> check)
    {
        var h=Habitat();var potato=Potato();
        check(HistoricalCropCatalog.Fit(h,potato,40)>0&&HistoricalCropCatalog.TransferFit(h,potato,40)==0,"The old generic tropical-tuber system incorrectly erases otherwise positive cool-season potato physiology.");
        double corrected=HistoricalCropSpeciesNiches.TransferFit(h,potato,40);
        check(corrected>.12&&corrected<1,"The corrected cool-season potato niche admits a usable, bounded carried cultivar without changing the habitat.");
        check(HistoricalCropSpeciesNiches.TransferFit(h,potato,60)<corrected,"Imported potato photoperiod mismatch still reduces success at a distant latitude.");
        h.SalinityMilliPpt=2500;check(HistoricalCropSpeciesNiches.TransferFit(h,potato,40)==0,"Saline fields still cannot produce freshwater potatoes.");h.SalinityMilliPpt=0;
        h.PermafrostPermille=1000;check(HistoricalCropSpeciesNiches.TransferFit(h,potato,40)==0,"Permanent frozen ground still blocks potato cultivation.");h.PermafrostPermille=0;
        h.AridityMilli=50;check(HistoricalCropSpeciesNiches.TransferFit(h,potato,40)==0,"Unwatered hyper-arid ground remains unsuitable.");
        check(HistoricalCropSpeciesNiches.TransferFit(h,potato,40,1)>0,"Actual freshwater access, not a climate name, can support irrigated cropping.");h.AridityMilli=950;
        h.ReliefM=2200;check(HistoricalCropSpeciesNiches.TransferFit(h,potato,40)==0,"The species correction does not waive the slope constraint.");h.ReliefM=60;
        h.GrowingDays=10;check(HistoricalCropSpeciesNiches.TransferFit(h,potato,40)==0,"A short frost-free season cannot mature the carried potato.");h.GrowingDays=273;
        h.WarmTenthsC=320;check(HistoricalCropEcology.ReproductiveFit(h,potato)==0,"The retained reproductive heat limit prevents tropical lowland potato proliferation.");
        for(int crop=0;crop<HistoricalCropCatalog.Crops.Length;crop++)if(crop!=10)
        {
            var definition=HistoricalCropCatalog.Crops[crop];var cultivar=new HistoricalCultivar{CropId=crop,MaturityDays=definition.MaturityDays,MinimumWarmTenthsC=definition.MinimumWarmC*10,WaterRequirementPermille=definition.MinimumWaterPermille,DaylengthLatitudeTenths=400};
            foreach(int warmth in new[]{-100,100,146,220,320})foreach(bool managed in new[]{false,true})
            {var site=Habitat(warmth);check(HistoricalCropSpeciesNiches.TransferFit(site,cultivar,40,.3,managed)==HistoricalCropCatalog.TransferFit(site,cultivar,40,.3,managed),"Non-potato crop"+crop+" retains its exact preceding transfer arithmetic at"+warmth+"/"+managed);}
        }
        check(HistoricalCropSpeciesNiches.TransferFit(h,null,40)==0,"No carried seed still means no crop, regardless of the new niche.");
    }
    public static void Recorded(string metadata,Action<bool,string> check)
    {
        // Accepted static field observations, not a hypothetical fertility or
        // climate adjustment. Profile values come from frozenQ17 dated171000,
        // 173100 and178000 observations and are never backdated to other years.
        using var document=JsonDocument.Parse(File.ReadAllText(metadata));
        var field=document.RootElement.GetProperty("Fields")[0];
        var habitat=JsonSerializer.Deserialize<HabitatCell>(field.GetProperty("ActualHabitat").GetRawText(),new JsonSerializerOptions{IncludeFields=true})!;
        double latitude=field.GetProperty("Latitude").GetDouble(),fresh=field.GetProperty("RiverCropSites").GetDouble();
        check(field.GetProperty("TargetCell").GetInt32()==176034&&habitat.WarmTenthsC==157&&habitat.GrowingDays==273,"Recorded counterfactual uses the accepted French field, not a hand-retuned climate.");
        habitat.WarmTenthsC=HistoricalCropCalendarEffects.GrowingWindowWarmth(85,157,110,27,6);
        check(habitat.WarmTenthsC==133,"The unchanged potato calendar still chooses its actual13.3C six-month window.");
        foreach(var row in new[]{
            new[]{171000,14,613,114,354,443,772,124},new[]{171000,65,123,119,372,449,674,129},
            new[]{173100,14,1125,109,354,437,875,119},new[]{173100,65,500,115,372,444,750,125},
            new[]{178000,14,2417,96,354,423,1133,106},new[]{178000,65,1394,107,372,434,928,117}})
        {
            var profile=new HistoricalCultivar{CropId=10,OriginCulture=row[1],ParentCulture=-1,SelectedYears=row[2],MaturityDays=row[3],DaylengthLatitudeTenths=row[4],WaterRequirementPermille=row[5],YieldPermille=row[6],MinimumWarmTenthsC=row[7]};
            double physiological=HistoricalCropCatalog.Fit(habitat,profile,latitude,fresh);
            double reproductive=HistoricalCropEcology.ReproductiveFit(habitat,profile);
            double before=D.Q(HistoricalCropCatalog.TransferFit(habitat,profile,latitude,fresh)*reproductive,1000000);
            double after=D.Q(HistoricalCropSpeciesNiches.TransferFit(habitat,profile,latitude,fresh)*reproductive,1000000);
            check(physiological>0&&before==0,"Actual Q17 donor"+row[1]+" year"+row[0]+" has positive potato physiology erased by the generic tropical-tuber rule.");
            check(after>.12&&after<1,"The species-only repair makes the actual dated cultivar physiologically eligible without a free adoption, date, route or climate bonus.");
            Console.WriteLine(JsonSerializer.Serialize(new{Year=row[0],SourceCulture=row[1],Target=176034,Physiological=physiological,LegacyCommunity=before,SpeciesCommunity=after,WindowTenthsC=habitat.WarmTenthsC,Profile=profile},new JsonSerializerOptions{IncludeFields=true}));
        }
    }
}
