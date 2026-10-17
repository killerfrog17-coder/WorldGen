using WorldGen.Core;
public static class HistoryFarmsteadEstablishmentTests
{
    public static void Run(Action<bool,string> check)
    {
        foreach(int revision in new[]{0,19,22,23})foreach(int parent in new[]{-1,0,20})
            foreach(double yield in new[]{0,.019999,.02,.030234,.098367,.099999,.10,.2,1})foreach(int practice in new[]{0,412,9999,10000,20000})
                check(HistoricalFarmsteadEstablishment.FieldAllowed(revision,parent,yield,practice)==!(yield<.10&&(yield<.02||practice<10000)),"Pre24 field gates retain their exact literal threshold behavior.");
        check(HistoricalFarmsteadEstablishment.FieldAllowed(24,-1,.098367,412),"The actual recorded marginal field can propose a small village, not an automatic proto-city or state.");
        check(!HistoricalFarmsteadEstablishment.FieldAllowed(24,0,.098367,412),"Secondary and overseas town proposals retain their existing field gate.");
        check(!HistoricalFarmsteadEstablishment.FieldAllowed(24,-1,.019999,20000),"Mature practice cannot manufacture an absent minimum crop yield.");
        check(HistoricalFarmsteadEstablishment.Opportunity(.098367,412)>.93&&HistoricalFarmsteadEstablishment.Opportunity(.098367,412)<.94,"The recorded field is close to, not discontinuously separated from, the comfortable threshold.");
        check(Math.Abs(HistoricalFarmsteadEstablishment.Opportunity(.03,0)-.0081)<1e-15,"Poor marginal fields remain exceptionally slow establishment candidates.");
        check(HistoricalFarmsteadEstablishment.Opportunity(.02,0)<.002,"At the physical floor, immature farmsteads are very rare.");
        check(HistoricalFarmsteadEstablishment.Opportunity(.01,20000)==0,"No mature seed history waives the production floor.");
        double previous=0;
        for(int i=0;i<=10000;i++)
        {
            double yield=i/10000.0,value=HistoricalFarmsteadEstablishment.Opportunity(yield,412);
            check(value>=previous&&value>=0&&value<=1,"Establishment is bounded and nondecreasing in actual field yield.");previous=value;
            if(yield>=.1)check(value==1,"Comfortable fields retain the preceding first-village opportunity rate.");
        }
        check(HistoricalFarmsteadEstablishment.Opportunity(.03,10000)==1,"The preceding mature-practice exception remains available.");
    }
}
