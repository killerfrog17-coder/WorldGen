using WorldGen.Core;
public static class HistoryFarmFrontierTests
{
    public static void Run(Action<bool,string> check)
    {
        check(HistoricalFarmFrontier.Room(0,30000,100,0)==500,"A full regional foraging pool cannot occupy thirty thousand people worth of genuinely viable new fields by rounding.");
        check(HistoricalFarmFrontier.Room(0,0,0,0)==0,"No viable fields grant no additional receiving room.");
        check(HistoricalFarmFrontier.Room(0,1000,1200,600)==0,"Already occupied farm plots cannot be counted twice.");
        check(HistoricalFarmFrontier.Room(0,1000,1000,300)==500,"Existing mixed farmers reserve their actual estimated share of local fields.");
        check(HistoricalFarmFrontier.Room(0,40,0,0)==40,"A tiny field opportunity does not round up to a full household wave.");
        check(HistoricalFarmFrontier.Room(10000,0,1000,600)==10000,"Normal available receiving capacity is preserved rather than capped by the small frontier allowance.");
        check(HistoricalFarmFrontier.Room(-100,100,0,0)==100,"Overfull native carrying capacity does not erase separate viable cultivated plots.");
    }
}
