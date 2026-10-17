using WorldGen.Core;
public static class HistoryUrbanSuccessionTests
{
    public static void Run(Action<bool,string> check)
    {
        double city=HistoricalUrbanSuccessionCompetition.Hazard(1,10000,10000,.7,.4,0,false,500);
        double network=HistoricalUrbanSuccessionCompetition.Hazard(30,100000,10000,.7,.4,0,false,500);
        check(city==0,"A lone center does not acquire fictitious urban rivals.");
        check(HistoricalUrbanSuccessionCompetition.Hazard(5,2000,1000,.7,.4,0,false,500)==0,"Tiny dependent settlements cannot finance substantial rival court coalitions.");
        check(network>.07&&network<.15,"A large premodern peer-center network has nonzero peacetime competition at succession.");
        check(HistoricalUrbanSuccessionCompetition.Hazard(30,100000,90000,.7,.4,0,false,500)<network/5,"Material concentration in the capital reduces independent local coalition power.");
        check(HistoricalUrbanSuccessionCompetition.Hazard(30,100000,10000,.7,.9,1,true,800)<network/50,"Modern impersonal representative transfers need not fracture functioning states.");
        check(HistoricalUrbanSuccessionCompetition.Hazard(30,100000,10000,.7,.8,0,false,500)<network,"Administrative integration lowers competition without making an ancient court immortal.");
        check(HistoricalUrbanSuccessionCompetition.Hazard(30,100000,10000,.7,.4,0,false,900)<network,"A more competent incoming ruler has better prospects for mediation.");
    }
}
