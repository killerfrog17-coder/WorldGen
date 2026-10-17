using WorldGen.Core;

public static class HistoryReturningFleetTests
{
    public static void Run(Action<bool,string> check)
    {
        double Fleet(double writing,double tools,double mills,double accounts,double study)=>HistoricalMaritimeControlEffects.ReturningFleet(1,writing,1,1,tools,tools,mills,accounts,study,0);
        check(Fleet(0,0,0,0,0)==0,"Expert oral ocean wayfinding is not itself an imperial convoy administration.");
        check(Fleet(1,0,0,1,0)==0,"Writing and accounts alone do not conjure repair workshops and bulk shipping equipment.");
        double early=Fleet(.4,.4,0,.2,0),regional=Fleet(.8,.8,.6,.7,.3),ocean=Fleet(.9,.9,.9,.9,.9);
        check(early>0&&early<.03&&HistoricalMaritimeControlEffects.ReturningLegKm(early,0,0)<900,"An early literate craft center keeps limited regional service despite inherited ocean knowledge.");
        check(regional>early&&HistoricalMaritimeControlEffects.ReturningLegKm(regional,0,0)>1500,"Supported preindustrial workshops can extend real regional sea administration.");
        check(HistoricalMaritimeControlEffects.ReturningLegKm(ocean,0,0)>5000&&HistoricalMaritimeControlEffects.ReturningNetworkKm(ocean,0,0)>9000,"Advanced preindustrial returning fleets can cross and support transoceanic ventures without an industrial or gunpowder date gate.");
        check(HistoricalMaritimeControlEffects.ReturningLegKm(0,0,0)==700&&HistoricalMaritimeControlEffects.ReturningNetworkKm(0,0,0)==1800,"Regional ancient service is retained independently of later ocean convoy specialization.");
        check(HistoricalMaritimeControlEffects.ReturningLegKm(1,1,1)>20000&&HistoricalMaritimeControlEffects.ReturningNetworkKm(1,1,1)>35000,"Powered fleets can sustain genuinely planetary control networks.");
        for(int i=0;i<=100;i++)
        {
            double amount=i/100.0;
            double lower=HistoricalMaritimeControlEffects.ReturningFleet(amount,.6,.8,.7,.4,.6,.5,.7,.5,0),higher=HistoricalMaritimeControlEffects.ReturningFleet(amount,.9,.8,.7,.4,.6,.8,.7,.8,0);
            check(lower>=0&&higher<=1&&higher>=lower,"Better supported maritime institutions never reduce the service capability; no era label enters the function.");
            if(i>0)check(HistoricalMaritimeControlEffects.ReturningLegKm(amount,0,0)>=HistoricalMaritimeControlEffects.ReturningLegKm((i-1)/100.0,0,0),"Fleet reach grows continuously, without a sudden threshold jump to global sea reach.");
        }
    }
}
