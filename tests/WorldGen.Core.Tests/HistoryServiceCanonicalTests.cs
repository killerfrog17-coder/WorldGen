using WorldGen.Core;

public static class HistoryServiceCanonicalTests
{
    public static void Run(Action<bool,string> check)
    {
        void Equal(object value,string label)=>check(StateHash.Compute(value)==StateHash.ComputeHistoricalReference(value),label);
        for(int i=0;i<50;i++)
        {
            var aim=new HistoricalTerritorialAmbition{AnchorCell=i*7,Landmass=~i,Region=i-10,Scope=(HistoricalAmbitionScope)(i%4-1),SinceYear=int.MaxValue-i};
            var port=new HistoricalSeaServicePort{Capacity=long.MaxValue-i,CargoDayCredit=long.MinValue+i,Cell=~i,CharterSpending=i*17L,ConstructionSpending=~(i*29L),CrewFood=i*123456789L,ExpansionCredit=long.MaxValue/2-i,Key=i,LastSeconds=long.MinValue/2+i,OwnerId=i-20,TownId=~(i*3),WorkSpent=-i};
            var government=new HistoricalColonialGovernment{LastExpeditionYear=~i,Sponsor=i};if(i%2==0)government.Titles.Add(i);
            Equal(aim,"Ambition enum and signed scalar bytes match independent reflection.");Equal(new[]{aim,null,aim.Copy()},"Ambition arrays retain null/item/terminator framing.");
            Equal(port,"Port credit and time preserve full signed64-bit values.");Equal(new[]{port,null,port.Copy()},"Port arrays use identical canonical framing.");
            Equal(government,"Government title bags preserve nested canonical values.");Equal(new[]{government,null,government.Copy()},"Government arrays preserve independent copied bags.");
            string before=StateHash.Compute(port);port.WorkSpent++;
            check(StateHash.Compute(port)!=before,"Port hashes read changed current fields, not stale cached values.");
            Equal(port,"Mutated port still matches literal serialization.");
        }
        Equal(Array.Empty<HistoricalSeaServicePort>(),"Empty port arrays preserve the canonical terminator.");
        Equal(new HistoricalSeaService(),"A service container dispatches its exact nested port and tree values.");
        Equal(new HistoricalTerritorialAmbitions{Governments=new HistoricalTerritorialAmbition[]{null}},"An ambition container retains a null member.");
    }
}
