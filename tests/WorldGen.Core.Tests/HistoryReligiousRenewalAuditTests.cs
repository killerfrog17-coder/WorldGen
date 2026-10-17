using System.Reflection;
using System.Runtime.CompilerServices;
using WorldGen.Core;

// Controlled diagnosis of the unchanged <=18 actor path. No world generation,
// map census, threshold adjustment or promised count of future sects.
public static class HistoryReligiousRenewalAuditTests
{
    sealed class Fixture
    {
        public readonly Prehistory Engine=(Prehistory)RuntimeHelpers.GetUninitializedObject(typeof(Prehistory));
        public readonly PrehistoryState State;
        public HistoricalReligiousNetworks Network=>State.History.Aggregates.Empires.Connections.Religion;
        public Fixture(int household=1)
        {
            var religion=new HistoricalReligiousNetworks{Version=2,SinceYear=0,Communities=new HistoricalCellTree(13)};
            religion.Write(0,3,0);religion.Write(0,5,household);religion.Write(0,9,0);
            State=new PrehistoryState{Years=1000,Population=new long[]{0},Society=new SocietyState{Settlements=new[]{new Settlement{Id=0,Cell=0,Name="Local actor",PolityId=0,Population=10000,FoodReserve=50000,Stability=700}}},History=new HistoryState
            {
                Polities=new[]{new HistoricalPolity{Id=0,ReligionId=0,Treasury=20000}},
                Religions=new[]{new HistoricalReligion{Id=0,FoundedYear=0},new HistoricalReligion{Id=1,FoundedYear=0}},
                Dynamics=new HistoricalDynamics{States=new[]{new HistoricalStateDynamics{Cycle=new HistoricalPoliticalCycle()}}},
                Aggregates=new HistoricalAggregateState{TotalPopulation=10000,Cities=new[]{new HistoricalCityAggregate{Id=0,Supported=true}},Empires=new HistoricalImperialSystem{Connections=new HistoricalColonialConnections{Realms=new HistoricalRealmLifecycles{Version=18},Religion=religion}}}
            }};
        }
        public void Tick(int institution,bool excluded,int elapsed=10)
        {
            State.Years+=elapsed;State.History.Polities[0].ReligionId=excluded?1:0;
            typeof(Prehistory).GetMethod("TryReligiousRenewal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(Engine,new object[]{State,0,0,0,0,institution,elapsed,excluded});
        }
        public bool MaterialUnchanged=>State.History.Aggregates.TotalPopulation==10000&&State.Society.Settlements[0].Population==10000&&State.Society.Settlements[0].FoodReserve==50000&&State.History.Polities[0].Treasury==20000&&State.History.Religions.Length==2;
    }
    public static void Run(Action<bool,string> check)
    {
        var crisis=new Fixture();for(int year=0;year<60;year+=10)crisis.Tick(900,true);
        check(crisis.Network.Read(0,9)==60,"The literal <=18 renewal path records sixty continuously organized exclusion years as60 dissent, not its required600.");
        for(int year=0;year<120;year+=10)crisis.Tick(900,false);
        check(crisis.Network.Read(0,9)==0,"One hundred twenty calm years erase that entire sixty-year grievance before a renewal is eligible.");

        var weak=new Fixture();weak.Network.Write(0,9,500);for(int year=0;year<100;year+=10)weak.Tick(599,true);
        check(weak.Network.Read(0,9)==500,"Below support600 the actual early return freezes grievance even when exclusion and retained households are supplied to the local actor.");

        var declining=new Fixture();int support=900;
        for(int year=0;year<100;year+=10){support=HistoricalReligiousNetworkEffects.Institution(support,10,false,false);declining.Tick(support,true);}
        check(support==331&&declining.Network.Read(0,9)==40,"Loss of provision drops an old900-support institution below the organizing gate after forty recorded grievance years; elapsed crisis alone cannot create a sect.");

        var developing=new Fixture();support=0;
        for(int year=0;year<600;year+=10){support=HistoricalReligiousNetworkEffects.Institution(support,10,true,true);developing.Tick(support,true);}
        check(support==868&&developing.Network.Read(0,9)==410,"Even six hundred continuously provisioned exclusion years from an initially unorganized community yield only410 dissent because its early organization period is gated out.");

        var same=new Fixture(0);var minority=new Fixture(1);same.Network.Write(0,9,500);minority.Network.Write(0,9,500);
        for(int year=0;year<100;year+=10){same.Tick(900,false);minority.Tick(900,false);}
        check(same.Network.Read(0,9)==450&&minority.Network.Read(0,9)==450,"Changing only the retained household identity cannot select a minority organizing actor in the current public-faith renewal method.");

        var unsupported=new Fixture();unsupported.State.History.Aggregates.Cities[0].Supported=false;unsupported.Network.Write(0,9,500);unsupported.Tick(900,true);
        check(unsupported.Network.Read(0,9)==500,"The current local renewal mechanism additionally requires a supported town; the institution index alone is not permission to found a lineage.");
        check(new[]{crisis,weak,declining,developing,same,minority,unsupported}.All(f=>f.MaterialUnchanged),"These actual private-method counterfactuals create no people, warehouse food, treasury or religious actors.");
        Console.WriteLine("Literal renewal counterfactuals: crisis60->dissent60->calm0; weak500 frozen; declining support331/dissent40; developing support868/dissent410; household identity does not select a second actor.");
    }
}
