namespace WorldGen.Core
{
    // Recognition of an inhabited exchange community is not cultivation,
    // urban qualification or sovereignty. These thresholds are authored model
    // priors; old revisions and pristine opportunities retain their old rules.
    public static class HistoricalContactCommunityRecognition
    {
        public static bool Qualified(int revision,int teacher,int cultureCount,int exposure,int lastExchangeYear,int year,long rawResidents,bool storageDelivered,double localCivicExample)
            =>revision>=28&&teacher>=0&&teacher<cultureCount&&exposure>=600
                &&lastExchangeYear>0&&lastExchangeYear<=year&&(long)year-lastExchangeYear<=250
                &&rawResidents>=400&&storageDelivered&&localCivicExample>=600;

        public static double EcologicalFactor(int revision,double ecological,bool qualified)
            =>revision>=28&&qualified?1:.02+.98*ecological;
    }
}
