using System.Reflection;
using System.Runtime.CompilerServices;
using WorldGen.Core;

public static class HistoryMigrantReligiousContinuityTests
{
    sealed class Fixture
    {
        public readonly Prehistory Engine=(Prehistory)RuntimeHelpers.GetUninitializedObject(typeof(Prehistory));
        public readonly PrehistoryState State;
        public HistoricalReligiousNetworks Network=>State.History.Aggregates.Empires.Connections.Religion;
        public Fixture()
        {
            typeof(Prehistory).GetField("CellPatch")!.SetValue(Engine,new[]{0,0,0});
            State=new PrehistoryState{Years=1000,Population=new long[]{500},Society=new SocietyState{
                Cultures=new[]{new CultureIdentity{Id=0,Population=1500}},
                Settlements=new[]{new Settlement{Id=0,Cell=0,Population=1000,FoodReserve=2000},new Settlement{Id=1,Cell=1,Population=500,FoodReserve=1000}}},History=new HistoryState{
                Cultures=new[]{new HistoricalCulture{Id=0,ReligionId=2}},
                Religions=Enumerable.Range(0,6).Select(id=>new HistoricalReligion{Id=id,Reach=ReligiousReach.Local,ParentId=-1}).ToArray(),
                Polities=new[]{new HistoricalPolity{Id=0,Treasury=3000}},
                Aggregates=new HistoricalAggregateState{Cells=new HistoricalCellTree(15).With(2,0).With(7,4),TownAt=new HistoricalCellTree(3).With(0,0).With(1,1),TotalPopulation=2000,
                    Living=new HistoricalLivingHistory{Cities=new[]{new HistoricalLivingCity(),new HistoricalLivingCity()}},
                    Empires=new HistoricalImperialSystem{Connections=new HistoricalColonialConnections{Realms=new HistoricalRealmLifecycles{Version=29},Religion=new HistoricalReligiousNetworks{
                        Communities=new HistoricalCellTree(39),LayAssociations=new HistoricalLayAssociations{Records=new HistoricalCellTree(24).With(0,3).With(1,900)}}}}}}};
            Network.Write(0,5,1);Network.Write(0,4,900);Network.Write(1,5,5);Network.Write(1,4,800);Network.Write(1,2,900);Network.Write(1,9,600);
        }
        public HistoricalMigrantReligiousContinuity Read(int site=2,int source=0,int town=0,long previous=0)=>Prehistory.MigrantReligiousContinuityAt(State,0,site,source,town,previous);
        public void Apply(int site,HistoricalMigrantReligiousContinuity identity)=>typeof(Prehistory).GetMethod("ApplyMigrantReligiousContinuity",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(Engine,new object[]{State,site,identity});
    }
    public static void Run(Action<bool,string> check)
    {
        var f=new Fixture();var source=f.Read();
        check(source.Public==0&&source.Household==1&&!source.CultureFallback,"Observed source public0 and remembered household1 override culturedefault2 and organized lay3 independently.");
        check(source.FoundingPublic==0&&!source.Incumbent,"An empty destination inherits the same existing publicfaithID, not a newly minted sect.");
        f.Network.Write(2,4,700);f.Network.Write(2,9,600);f.Apply(2,source);
        check(HistoricalCells.Religion(f.State,2)==0&&f.Network.Read(2,5)==1,"Empty destination keeps actual public and household identities separately.");
        check(f.Network.Read(2,4)==0&&f.Network.Read(2,1)==0&&f.Network.Read(2,9)==0,"Migration brings no ready institution, conversion pressure or invented dispute.");
        check(f.Network.Read(2,6)==-1&&f.Network.Read(2,12)==0&&f.Network.Read(2,2)==1000,"Empty community starts its own review clock without a fabricated missionary delivery.");
        check(f.State.History.Religions.Length==6&&f.State.History.Religions[0].Reach==ReligiousReach.Local&&f.State.History.Religions.All(r=>r.ParentId==-1),"Carrying rites creates neither a new lineage nor automatic portable/missionary vocation.");
        check(f.Network.Read(0,4)==900&&f.Network.Read(0,5)==1&&f.State.History.Aggregates.Empires.Connections.Religion.LayAssociations.Records[0]==3,"Source institution, household and distinct lay association are unchanged.");
        check(f.State.Society.Settlements[0].FoodReserve==2000&&f.State.Society.Settlements[1].FoodReserve==1000&&f.State.History.Polities[0].Treasury==3000&&f.State.History.Aggregates.TotalPopulation==2000&&f.State.Population[0]==500,"Identity inheritance never creates or spends food, money or people; actual migration caller owns its paid transfer.");

        var native=new Fixture();var arrival=native.Read(site:1,previous:500);native.Apply(1,arrival);
        check(arrival.Incumbent&&arrival.FoundingPublic==4&&HistoricalCells.Religion(native.State,1)==4,"Existing named publicfaith4 remains the new local culture's public identity, rather than instantly converting natives.");
        check(native.Network.Read(1,5)==5&&native.Network.Read(1,4)==800&&native.Network.Read(1,9)==600&&native.Network.Read(1,2)==900,"Existing household5, paid institution, dispute and review clock survive migrant arrival.");
        check(native.Network.Read(1,6)==1&&native.Network.Read(1,12)==0&&native.Network.Read(1,7)==1000,"The limited visitor slot records incoming household1 as unfunded contact, not lay3 or a copied temple.");
        var strong=new Fixture();strong.State.History.Religions[3].Reach=ReligiousReach.Missionary;strong.Network.Write(1,6,3);strong.Network.Write(1,7,990);strong.Network.Write(1,10,850);strong.Network.Write(1,12,650);strong.Apply(1,strong.Read(site:1,previous:500));
        check(strong.Network.Read(1,6)==3&&strong.Network.Read(1,7)==990&&strong.Network.Read(1,10)==850&&strong.Network.Read(1,12)==650,"An unfunded arrival cannot erase a stronger recent actually supplied missionary visitor or counterfeit its recent delivery date.");
        var repeated=new Fixture();repeated.State.History.Religions[1].Reach=ReligiousReach.Missionary;repeated.Network.Write(1,6,1);repeated.Network.Write(1,7,901);repeated.Network.Write(1,10,850);repeated.Network.Write(1,12,650);repeated.Apply(1,repeated.Read(site:1,previous:500));
        check(repeated.Network.Read(1,6)==1&&repeated.Network.Read(1,7)==1000&&repeated.Network.Read(1,10)==850&&repeated.Network.Read(1,12)==0,"Arrival of the same remembered visitor preserves residence but cannot rejuvenate a stale paid missionary carrier through an unfunded family contact.");
        var foundedTown=new Fixture();foundedTown.State.History.Aggregates.Cells=foundedTown.State.History.Aggregates.Cells.With(7,2);
        typeof(Prehistory).GetMethod("InheritReligiousHouseholds",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(foundedTown.Engine,new object[]{foundedTown.State,1,0,4,500L});
        check(HistoricalCells.Religion(foundedTown.State,1)==4&&foundedTown.Network.Read(1,5)==5&&foundedTown.Network.Read(1,4)==800&&foundedTown.Network.Read(1,9)==600&&foundedTown.Network.Read(1,2)==900,"The actual same-culture town inheritance method restores PRE-overwrite native public4, preserving household5, funded institution, dispute and review clock after the founder wrote default2.");
        check(foundedTown.Network.Read(1,6)==1&&foundedTown.Network.Read(1,12)==0&&foundedTown.Network.Read(0,4)==900,"Actual same-culture town inheritance records only the arriving household contact; it cannot copy source institutions.");

        var abandoned=new Fixture();abandoned.State.History.Aggregates.Living.Cities[0].AbandonedYear=900;abandoned.State.History.Aggregates.Cells=abandoned.State.History.Aggregates.Cells.With(12,5);
        check(abandoned.Read(source:2).Public==5&&abandoned.Read(source:2).SourceCell==2,"An abandoned supplied town cannot override the actual live rural departure endpoint.");
        var invalid=new Fixture();invalid.State.Society.Settlements[0].Cell=99;invalid.State.History.Aggregates.Cells=invalid.State.History.Aggregates.Cells.With(12,5);
        check(invalid.Read(source:2).Public==5&&invalid.Read(source:2).SourceCell==2,"An invalid supplying-town cell falls back to the explicitly supplied rural endpoint.");
        check(invalid.Read(source:2,town:99).Public==5,"An out-of-range town ID also falls back safely to the actual departure endpoint.");
        var rural=new Fixture();rural.State.History.Aggregates.Cells=rural.State.History.Aggregates.Cells.With(2,-1);rural.Network.Write(0,5,-1);
        check(rural.Read(town:-1).Public==2&&rural.Read(town:-1).Household==2&&rural.Read(town:-1).CultureFallback,"Unknown rural public/household rites use the debited cohort's explicit culture proxy, clearly labeled rather than claimed observed.");
        rural.Network.Write(0,5,1);
        check(rural.Read(town:-1).Public==2&&rural.Read(town:-1).Household==1,"Missing rural public rites do not erase an independently recorded household identity.");
        var ghost=new Fixture();var vacated=ghost.Read(site:1,previous:0);ghost.Apply(1,vacated);
        check(!vacated.Incumbent&&HistoricalCells.Religion(ghost.State,1)==0&&ghost.Network.Read(1,5)==1,"A named but empty abandoned destination cannot masquerade as surviving resident households.");
        var old=new Fixture();old.State.History.Aggregates.Empires.Connections.Realms.Version=28;old.Apply(2,old.Read());
        check(HistoricalCells.Religion(old.State,2)==-1&&old.Network.Read(2,5)==-1,"The entire new mutation helper is inert for saved revision28.");
        check(typeof(Prehistory).GetMethods(BindingFlags.Instance|BindingFlags.NonPublic).Count(m=>m.Name=="FoundAggregateCulture")==1&&typeof(Prehistory).GetMethod("FoundAggregateCulture",BindingFlags.Instance|BindingFlags.NonPublic)!.GetParameters().Length==4,"The native/contact founder retains its unique original reflection signature.");
        check(typeof(Prehistory).GetMethod("InheritDevelopmentCommunityFrom",BindingFlags.Instance|BindingFlags.NonPublic)!.GetParameters().Length==6,"The existing development inheritance reflection signature also remains literal.");
    }
}
