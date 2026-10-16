using WorldGen.Core;

public static class HistoryResourceEconomyTests
{
    static HistoricalResourcePotential Resources(int copper=0,int tin=0,int iron=0,int coal=0,int oil=0,int timber=0,int uranium=0)
        =>new(copper,tin,iron,0,coal,oil,uranium,500,timber,100,500,500,500,false);
    public static void Run(Action<bool,string> check)
    {
        var empty=Resources();var metals=Resources(copper:800,tin:800,iron:800,coal:800,timber:800);var oil=Resources(iron:800,oil:800);
        check(HistoricalResourceEconomy.DeploymentSupply(metals,HistoricalTechnology.Bronze)>HistoricalResourceEconomy.DeploymentSupply(Resources(copper:800),HistoricalTechnology.Bronze),"Bronze production benefits from both copper and a tin supply, not copper alone");
        check(HistoricalResourceEconomy.DeploymentSupply(Resources(iron:800,timber:800),HistoricalTechnology.Iron)>HistoricalResourceEconomy.DeploymentSupply(empty,HistoricalTechnology.Iron),"Woodland charcoal is a real preindustrial substitute for coal");
        check(HistoricalResourceEconomy.DeploymentSupply(oil,HistoricalTechnology.CombustionEngines)>HistoricalResourceEconomy.DeploymentSupply(metals,HistoricalTechnology.CombustionEngines),"Oil availability affects engine deployment differently from coal-fed steam");
        check(HistoricalResourceEconomy.DeploymentSupply(Resources(uranium:800),HistoricalTechnology.NuclearWeapons)>HistoricalResourceEconomy.DeploymentSupply(empty,HistoricalTechnology.NuclearWeapons),"Knowing a nuclear design is not equivalent to easy fissile-material access");
        check(HistoricalResourceEconomy.DeploymentSupply(empty,HistoricalTechnology.Antibiotics)==1,"Mineral deposits do not nonsensically gate every medical discovery");
        foreach(var technology in Enum.GetValues<HistoricalTechnology>())foreach(var p in new[]{empty,metals,oil})
        {double value=HistoricalResourceEconomy.DeploymentSupply(p,technology);check(value>=.3&&value<=1,"Bounded material approximation retains costly substitutes/overlooked deposits: "+technology);}
        check(HistoricalResourceEconomy.ExtractiveReceipts(0,1,1,1,1,1,metals)==0,"Empty territorial claims do not generate mineral tax revenue");
        check(HistoricalResourceEconomy.ExtractiveReceipts(100000,1,1,0,0,0,metals)==0,"Unworked deposits do not generate free extraction income");
        long worked=HistoricalResourceEconomy.ExtractiveReceipts(100000,1,1,1,1,1,metals);
        check(worked>0&&worked<1000,"Worked mineral receipts are a small bounded specialist output, not a new unlimited economy");
        check(HistoricalResourceEconomy.StrategicValue(oil,0,0,0)==0&&HistoricalResourceEconomy.StrategicValue(oil,0,0,1)>10,"Petroleum provinces only become strategic industrial prizes after usable engine technology");
    }
}
