using System.Text.Json;
using WorldGen.Core;

public static class HistoryCropMaturityCalendarTests
{
    public static void Run(Action<bool,string> check)
    {
        int Window(int a,int b,int c,int d,int group,int days)=>HistoricalCropCalendarEffects.MaturityWindowWarmth(a,b,c,d,group,days);
        foreach(int group in Enumerable.Range(0,8))foreach(int days in new[]{1,30,90,91,92,110,120,121,122,160,220,270,365})
        {
            int value=Window(85,157,110,27,group,days);
            check(value>=27&&value<=157,"Window warmth stays within actual accepted seasonal observations.");
            check(value==Window(110,27,85,157,group,days),"Half-year hemisphere rotation preserves the same cyclic window opportunity.");
            check(Window(180,180,180,180,group,days)==180,"An aseasonal world does not gain heat from a different planting duration.");
        }
        check(HistoricalCropCalendarEffects.GrowingWindowWarmth(85,157,110,27,6)==133,"Literal preceding French six-month calendar remains unchanged.");
        check(Window(85,157,110,27,6,120)>133,"A120-day potato uses its real shorter season, not an imposed six-month average.");
        check(Window(0,0,200,200,0,120)>170,"A warm autumn-to-winter season can cross the calendar boundary.");
        check(Window(85,157,110,27,6,365)==94,"A year-long crop samples the complete annual thermal exposure.");
        foreach(int day in Enumerable.Range(80,90))check(Math.Abs(Window(85,157,110,27,6,day+1)-Window(85,157,110,27,6,day))<=2,"Fractional final-month weighting avoids artificial full-month jumps when a cultivar changes by one day.");
        check(Window(350,350,350,350,6,100)==350,"Selecting a short window cannot cool a uniformly hot lowland.");
        bool invalid=false;try{Window(0,0,0,0,0,0);}catch(ArgumentOutOfRangeException){invalid=true;}check(invalid,"A zero-length crop cannot manufacture a growing window.");
    }
    public static void Recorded(string path,Action<bool,string> check)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(path));var field=document.RootElement.GetProperty("Fields")[0];
        HabitatCell Read()=>JsonSerializer.Deserialize<HabitatCell>(field.GetProperty("ActualHabitat").GetRawText(),new JsonSerializerOptions{IncludeFields=true})!;
        var old=Read();var next=Read();double latitude=field.GetProperty("Latitude").GetDouble(),fresh=field.GetProperty("RiverCropSites").GetDouble();
        var cultivar=new HistoricalCultivar{CropId=10,OriginCulture=14,ParentCulture=-1,SelectedYears=613,MaturityDays=114,DaylengthLatitudeTenths=354,WaterRequirementPermille=443,YieldPermille=772,MinimumWarmTenthsC=124};
        string original=StateHash.Compute(old);old.WarmTenthsC=HistoricalCropCalendarEffects.GrowingWindowWarmth(85,157,110,27,6);
        next.WarmTenthsC=HistoricalCropCalendarEffects.MaturityWindowWarmth(85,157,110,27,6,cultivar.MaturityDays);
        double Fit(HabitatCell h)=>D.Q(HistoricalCropSpeciesNiches.TransferFit(h,cultivar,latitude,fresh)*HistoricalCropEcology.ReproductiveFit(h,cultivar),1000000);
        double before=Fit(old),after=Fit(next);check(before>.12&&after>before&&after<1,"The exact recorded French field and dated114-day cultivar gain a feasible shorter-season fit, without changing seed traits or other habitat values.");
        Console.WriteLine(JsonSerializer.Serialize(new{Field=field.GetProperty("TargetCell").GetInt32(),PriorSixMonthTenthsC=old.WarmTenthsC,MaturityDayWindowTenthsC=next.WarmTenthsC,Before=before,After=after,Note="DatedQ17crop-profile counterfactual, not an observed V21 delivery."}));
        check(StateHash.Compute(Read())==original,"The accepted climate and physical field observations are untouched.");
        next.AridityMilli=0;next.Flooding=FloodRegime.None;check(HistoricalCropSpeciesNiches.TransferFit(next,cultivar,latitude,0)==0,"A better thermal window does not water an unwatered desert.");
        next=Read();next.PermafrostPermille=1000;check(HistoricalCropSpeciesNiches.TransferFit(next,cultivar,latitude,fresh)==0,"The whole-year frozen-ground exclusion remains.");
        next=Read();next.GrowingDays=10;check(HistoricalCropSpeciesNiches.TransferFit(next,cultivar,latitude,fresh)==0,"A warm seasonal mean does not provide missing frost-free maturation days.");
    }
}
