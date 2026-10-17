using System.Reflection;
using System.Runtime.CompilerServices;
using WorldGen.Core;
public static class HistoryProtoContinuityTests
{
    static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    public static void Run(Action<bool,string> check)
    {
        var engine=(Prehistory)RuntimeHelpers.GetUninitializedObject(typeof(Prehistory));
        var rules=new HistoricalRegionalTraditions();rules.Qualified=rules.Qualified.With(0,1);
        var state=new PrehistoryState{Years=1000,Society=new SocietyState{Settlements=new[]{new Settlement{Id=0,Cell=0,CultureId=0,PolityId=-1,Population=3719,FoodReserve=11157}}},
            History=new HistoryState{Cultures=new[]{new HistoricalCulture{Id=0}},Towns=new[]{new HistoricalTown{Id=0}},RegionalTraditions=rules,
                Aggregates=new HistoricalAggregateState{Cities=new[]{new HistoricalCityAggregate{Id=0}},Living=new HistoricalLivingHistory{Cities=new[]{new HistoricalLivingCity{AbandonedYear=-1}}},
                    Empires=new HistoricalImperialSystem{Connections=new HistoricalColonialConnections{Realms=new HistoricalRealmLifecycles{Version=20}}}}}};
        object Call(string name,params object[] values)=>typeof(Prehistory).GetMethod(name,Private)!.Invoke(engine,values)!;
        check(!(bool)Call("LivingInstitutionalContinuity",state,0),"Literal20 does not recognize its own already-qualified proto-urban institution in the early-village continuity test.");
        state.History.Aggregates.Empires.Connections.Realms.Version=21;
        // This deliberately tiny fixture is not a complete generated world;
        // inspect its literal fields rather than aggregate integrity roots.
        string before=StateHash.Compute(state);
        check((bool)Call("LivingInstitutionalContinuity",state,0),"Revision21 recognizes an actual living qualified proto-center without granting writing, a teacher or a state.");
        check(!(bool)Call("LivingProtocentreDispersal",state,0,100),"A qualified proto-center no longer undergoes the independent unorganized-village dispersal lottery.");
        check((long)Call("LivingProtocentreCapacity",state,0,10000L)==10000L,"The existing physical food-capacity calculation is no longer capped by the unorganized-village category after real qualification.");
        check(StateHash.Compute(state)==before&&state.Society.Settlements[0].Population==3719&&state.Society.Settlements[0].FoodReserve==11157,"Continuity queries create no people, provisions, court, technology or saved mutation.");
        rules.Qualified=rules.Qualified.With(0,0);
        check(!(bool)Call("LivingInstitutionalContinuity",state,0),"A dequalified or ordinary village keeps its existing ephemeral-village treatment.");
        rules.Qualified=rules.Qualified.With(0,1);state.History.Aggregates.Living.Cities[0].AbandonedYear=999;
        check(!(bool)Call("LivingInstitutionalContinuity",state,0),"An abandoned center cannot claim continuing organization through a stale qualification flag.");
        check(HistoricalUrbanSurvivalEffects.Retire(0,0,0,100,.1),"The independent actual-food/distress retirement rule still retires an untenable center; proto continuity does not waive famine.");
    }
}
