using System.Reflection;
using System.Runtime.CompilerServices;
using WorldGen.Core;

public static class HistoryTerritorialContinuityTests
{
    public static void Run(Action<bool,string> check)
    {
        HistoryCourtConnectivityTests.Run(check);
        double P(double bureaucracy=.8,int inclusion=700,int representation=650,long receipts=1000,long cost=800,double distance=1,bool served=true)
            =>HistoricalTerritorialContinuityEffects.SecessionPressure(.7,bureaucracy,inclusion,representation,receipts,cost,distance,served);
        check(P()<.45,"Actually included, financed, connected communities under installed modern administration need not turn ordinary autonomy demands into secession.");
        check(P(0)==.7,"Ancient non-bureaucratic empires retain the exact preceding territorial risk.");
        check(P(served:false)==.7&&P(receipts:0)==.7,"An unserved district or insolvent government receives no continuity protection.");
        check(P(inclusion:0)==.7,"The new rule does not protect an excluded colonial population merely because its ruler has radio.");
        check(P(distance:double.PositiveInfinity)==.7&&P(distance:double.NaN)==.7,"An absent or invalid physical route cannot manufacture institutional reach.");
        check(P(distance:6)>P(distance:2)&&P(distance:2)>P(distance:1),"Actual physical travel burden weakens the same government's territorial integration.");
        check(P(receipts:100)>P(receipts:800)&&P(receipts:100000)==P(receipts:800),"Service funding is necessary and surplus beyond fully financed costs does not mint unlimited cohesion.");
        check(P(inclusion:100)>P(inclusion:700)&&P(representation:50)>P(representation:800),"Exclusion and restricted participation preserve separation pressure.");
        foreach(int inclusion in new[]{0,100,400,700,1000})foreach(int representation in new[]{0,500,1000})foreach(double bureaucracy in new[]{0,.2,.7,1})
        {double value=P(bureaucracy,inclusion,representation);check(value>=0&&value<=.7,"The rule bounds territorial pressure without erasing or increasing the underlying demands.");}
        var engine=(Prehistory)RuntimeHelpers.GetUninitializedObject(typeof(Prehistory));
        var guard=typeof(Prehistory).GetMethod("CommunityTerritorialSecessionPressure",BindingFlags.Instance|BindingFlags.NonPublic)!;
        check((double)guard.Invoke(engine,new object[]{new PrehistoryState(),0,0,.7,1.0})! ==.7,"Legacy histories without any realm extension preserve the literal rule without touching missing world data.");
        var legacy=new PrehistoryState{History=new HistoryState{Aggregates=new HistoricalAggregateState{Empires=new HistoricalImperialSystem{Connections=new HistoricalColonialConnections{Realms=new HistoricalRealmLifecycles{Version=22}}}}}};
        check((double)guard.Invoke(engine,new object[]{legacy,0,0,.7,1.0})! ==.7,"Revision22 remains unchanged even under the new source.");
    }
}
