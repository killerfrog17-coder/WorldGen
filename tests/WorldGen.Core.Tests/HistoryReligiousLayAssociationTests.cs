using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryReligiousLayAssociationTests
{
    static object Call(Prehistory e,string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,values)!;
    public static void World(string path,Action<bool,string> check,bool cold=true)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var e=new Prehistory(w);var source=w.Prehistory;e.Validate(source);string original=source.Hash;
        var s=e.UpgradeRealmLifecycles(source).Copy();var a=s.History.Aggregates;var r=a.Empires.Connections.Religion;
        a.Empires.Connections.Realms.Version=20;Call(e,"InitializeLayReligiousAssociations",s);Call(e,"PrepareAggregateRelations",s);
        check(r.LayAssociations.Records.Fingerprint()==new HistoricalCellTree(e.CellPatch.Length*HistoricalLayAssociations.Fields).Fingerprint(),"Explicit local-organization initialization starts empty rather than inventing historical associations or invoices.");
        check(a.TotalPopulation==source.History.Aggregates.TotalPopulation&&s.Society.Settlements.Select(x=>x.FoodReserve).SequenceEqual(source.Society.Settlements.Select(x=>x.FoodReserve))&&s.History.Polities.Select(x=>x.Treasury).SequenceEqual(source.History.Polities.Select(x=>x.Treasury)),"Prospective organization upgrade conserves every actual town stock, treasury and person.");
        int donor=-1,target=-1;
        foreach(var city in a.Cities)
        {
            if(a.Living.Cities[city.Id].AbandonedYear>=0)continue;
            foreach(int peer in city.Neighbors.Take(12))
            {
                var d=s.Society.Settlements[peer];var t=s.Society.Settlements[city.Id];
                if(peer==city.Id||d.Population<600||t.Population<100||d.FoodReserve<=d.Population||!a.Cities[peer].Supported||a.Living.Cities[peer].AbandonedYear>=0)continue;
                if(!(bool)Call(e,"InstitutionalRouteAllowed",s,d.Cell,t.Cell,d.CultureId)||!(bool)Call(e,"ReligiousTownCarrierAllowed",s,peer,city.Id))continue;
                donor=peer;target=city.Id;break;
            }
            if(target>=0)break;
        }
        check(target>=0,"The saved geography supplies a currently viable actual town-to-town religious carrier route.");
        if(target>=0)
        {
            // Controlled affiliations on real actors/terrain, using their actual
            // stocks, not an invented endowment or a claim about adherent numbers.
            var test=s.Copy();var ta=test.History.Aggregates;var tr=ta.Empires.Connections.Religion;var d=test.Society.Settlements[donor];var t=test.Society.Settlements[target];
            int incumbent=HistoricalCells.Religion(test,t.Cell),minority=Enumerable.Range(0,test.History.Religions.Length).First(x=>x!=incumbent);
            ta.Cells=ta.Cells.With(d.Cell*5+2,incumbent);tr.Write(d.Cell,5,minority);tr.Write(t.Cell,5,minority);tr.Write(d.Cell,4,900);
            Call(e,"SeedLayReligiousCommunity",test,d.Cell,minority);
            string publicBefore=ta.Cells.Fingerprint();long foodBefore=d.FoodReserve,people=ta.TotalPopulation,cash=test.History.Polities.Sum(x=>x.Treasury);
            for(int i=0;i<5;i++)
            {
                test.Years+=10;tr.Write(d.Cell,2,test.Years);tr.Write(t.Cell,2,test.Years);
                Call(e,"AdvanceLayReligiousCommunity",test,d.Cell,donor,-1,incumbent,900,0,false);
            }
            int support=Prehistory.LayAssociationAt(test,d.Cell)!.Support;
            long gifts=HistoricalLayAssociationEffects.Gift(d.Population,10)*5;
            check(support>=400&&d.FoodReserve==foodBefore-gifts,"Five actual local reviews build supported delivery only after debiting the exact real town gifts.");
            string untouched=tr.Communities.Fingerprint();long preVisit=d.FoodReserve;
            Call(e,"RecordReligiousContact",test,d.CultureId,t.CultureId,-1,t.Cell);
            check(tr.Communities.Fingerprint()==untouched&&d.FoodReserve==preVisit,"A global culture identity without a real departure endpoint cannot dispatch or bill an association.");
            Call(e,"RecordReligiousContact",test,d.CultureId,t.CultureId,d.Cell,t.Cell);
            check(tr.Read(t.Cell,6)==minority&&tr.Read(t.Cell,12)==support&&d.FoodReserve==preVisit-Math.Max(1,d.Population/4000),"The actual reachable route delivers its funded minority carrier, exact support and real source provisions even when public religions agree.");
            var received=Prehistory.LayAssociationAt(test,t.Cell);
            check(received!=null&&received.Faith==minority&&received.Support==0&&ta.Cells.Fingerprint()==publicBefore,"Reception creates only an unendowed local potential: no public conversion, territory repaint or copied institution.");
            check(ta.TotalPopulation==people&&test.History.Polities.Sum(x=>x.Treasury)==cash,"Actual paid household organization and delivery create neither inhabitants nor court money.");
            int disrupted=ta.Living.Cities[donor].DisruptedUntil;ta.Living.Cities[donor].DisruptedUntil=test.Years+100;
            check(!(bool)Call(e,"ReligiousTownCarrierAllowed",test,donor,target),"A previously real route cannot use its disrupted source as a live religious carrier.");ta.Living.Cities[donor].DisruptedUntil=disrupted;
            Console.WriteLine($"Actual route town{donor}/cell{d.Cell} -> town{target}/cell{t.Cell}; population{d.Population}; gifts{gifts}; carrier{Math.Max(1,d.Population/4000)}; support{support}; public{incumbent}, private{minority}.");
        }
        check(source.ComputeHash()==original,"All controlled organization experiments leave the original saved checkpoint untouched.");
        if(!cold)return;
        s.Seal();e.Validate(s);w.Prehistory=s;byte[] bytes=WorldFile.Write(w);var read=WorldFile.Read(bytes);var re=new Prehistory(read);re.Validate(read.Prehistory);
        check(read.Prehistory.Hash==s.Hash&&read.Prehistory.History.Aggregates.Empires.Connections.Religion.LayAssociations!=null,"Genuine full-world serialization retains the explicit revision20 organization ledger and canonical checkpoint.");
        var full=e.AdvanceYears(s,12);var five=re.AdvanceYears(read.Prehistory,5);read.Prehistory=five;
        var middle=WorldFile.Read(WorldFile.Write(read));var coldEngine=new Prehistory(middle);var split=coldEngine.AdvanceYears(middle.Prehistory,7);e.Validate(full);coldEngine.Validate(split);
        check(middle.Prehistory.Hash==five.Hash&&full.Hash==split.Hash,"Revision20 twelve-year continuation exactly equals five years, a genuine intermediate cold save, and seven further years.");
        var fresh=e.Start(e.Prepare(source.Origin));var three=e.AdvanceYears(fresh,3);var oneTwo=e.AdvanceYears(e.AdvanceYears(fresh,1),2);
        check(fresh.History.Aggregates.Empires.Connections.Realms.Version==new HistoricalRealmLifecycles().Version&&fresh.History.Aggregates.Empires.Connections.Religion.LayAssociations!=null&&three.Hash==oneTwo.Hash,"Fresh current rules start with their prospective lay-association ledger and deterministic split advancement.");
        Console.WriteLine("Cold revision20 continuation "+full.Hash);
    }
    sealed class Fixture
    {
        public Prehistory Engine=(Prehistory)RuntimeHelpers.GetUninitializedObject(typeof(Prehistory));
        public PrehistoryState State;
        public HistoricalReligiousNetworks Network=>State.History.Aggregates.Empires.Connections.Religion;
        public HistoricalLayAssociations Lay=>Network.LayAssociations;
        public Settlement Town=>State.Society.Settlements[0];
        public Fixture(int revision=20)
        {
            typeof(Prehistory).GetField("CellPatch")!.SetValue(Engine,new[]{0,0});
            typeof(Prehistory).GetField("World")!.SetValue(Engine,new World{Recipe=new Recipe{Seed=123}});
            Engine.UseProvincialCaches=false;
            var r=new HistoricalReligiousNetworks{Version=2,SinceYear=0,Communities=new HistoricalCellTree(26)};
            r.Write(0,2,1000);r.Write(0,3,0);r.Write(0,4,900);r.Write(0,5,1);r.Write(1,2,1000);r.Write(1,3,0);r.Write(1,5,1);
            State=new PrehistoryState{Years=1000,Population=new long[]{0},Society=new SocietyState{Cultures=new[]{new CultureIdentity{Id=0,Name="Local people"}},Settlements=new[]{new Settlement{Id=0,Cell=0,Name="First town",CultureId=0,PolityId=0,Population=10000,FoodReserve=50000,Stability=700},new Settlement{Id=1,Cell=0,Name="Later town",CultureId=0,PolityId=0,Population=10000,FoodReserve=50000,Stability=700}}},History=new HistoryState
            {
                Version="history-1",Cultures=new[]{new HistoricalCulture{Id=0,ReligionId=0}},
                Polities=new[]{new HistoricalPolity{Id=0,ReligionId=0,Treasury=20000}},
                Religions=new[]{new HistoricalReligion{Id=0,FoundedYear=0,Name="Public lineage",Ritual="Shared inherited rites"},new HistoricalReligion{Id=1,FoundedYear=0,Name="Household lineage",Ritual="Household calendar"}},
                Dynamics=new HistoricalDynamics{States=new[]{new HistoricalStateDynamics{Cycle=new HistoricalPoliticalCycle()}}},
                Aggregates=new HistoricalAggregateState{TotalPopulation=20000,Cells=new HistoricalCellTree(10).With(0,0).With(1,0).With(2,0).With(5,0).With(7,0),TownAt=new HistoricalCellTree(2).With(0,0),Cities=new[]{new HistoricalCityAggregate{Id=0,Supported=true},new HistoricalCityAggregate{Id=1,Supported=true}},Polities=new[]{new HistoricalPolityAggregate{Id=0}},Living=new HistoricalLivingHistory{Cities=new[]{new HistoricalLivingCity(),new HistoricalLivingCity()}},Empires=new HistoricalImperialSystem{Connections=new HistoricalColonialConnections{Realms=new HistoricalRealmLifecycles{Version=revision},Religion=r}}}
            }};
            if(revision>=20)Call(Engine,"InitializeLayReligiousAssociations",State);
        }
        public void Seed(int faith=1)=>Call(Engine,"SeedLayReligiousCommunity",State,0,faith);
        public void Tick(int policy=0,bool paidOfficials=false)
        {
            State.Years+=10;Network.Write(0,2,State.Years);Network.Write(1,2,State.Years);
            Call(Engine,"AdvanceLayReligiousCommunity",State,0,0,0,0,900,policy,paidOfficials);
        }
    }
    public static void Run(Action<bool,string> check)
    {
        var f=new Fixture();string empty=f.Lay.Records.Fingerprint();long food=f.Town.FoodReserve;
        Call(f.Engine,"AdvanceLayReligiousCommunity",f.State,0,0,0,0,900,0,false);
        check(f.Lay.Records.Fingerprint()==empty&&f.Town.FoodReserve==food,"A bare household memory creates neither a working association nor an invoice.");
        Call(f.Engine,"SeedLayFromDeliveredContact",f.State,0,1,399);
        check(f.Lay.Records.Fingerprint()==empty,"A remembered or unsupported teacher cannot endow a parallel organization.");
        Call(f.Engine,"SeedLayFromDeliveredContact",f.State,0,1,400);
        check(f.Lay.Records[0]==1&&f.Lay.Records[1]==0&&f.Town.FoodReserve==food&&HistoricalCells.Religion(f.State,0)==0,"Actual matching supported contact seeds only a zero-support potential; public predominance and food remain unchanged.");
        f.Tick();long gift=HistoricalLayAssociationEffects.Gift(10000,10);
        check(f.Town.FoodReserve==food-gift&&f.Lay.Records[1]>0&&f.State.History.Aggregates.TotalPopulation==20000,"The first real local gift is exactly debited and builds organizing support without creating people.");
        long after=f.Town.FoodReserve;Call(f.Engine,"AdvanceLayReligiousCommunity",f.State,0,0,0,0,900,0,false);
        check(f.Town.FoodReserve==after,"The local association clock prevents duplicate same-year gifts from city/contact callers.");

        var permitted=new Fixture();permitted.Seed();permitted.Lay.Records=permitted.Lay.Records.With(1,600);
        var suppressed=new Fixture();suppressed.Seed();suppressed.Lay.Records=suppressed.Lay.Records.With(1,600);
        long cash=suppressed.State.History.Polities[0].Treasury;suppressed.Tick(2);permitted.Tick(0);
        check(suppressed.Lay.Records[1]<600&&suppressed.Lay.Records[1]<permitted.Lay.Records[1]&&suppressed.State.History.Polities[0].Treasury==cash-25,"Real paid restrictions reduce activity and consume actual court funds rather than becoming a free dissent multiplier.");
        check(suppressed.Lay.Records[0]==1&&suppressed.Lay.Records[5]==0&&suppressed.State.History.Religions.Length==2&&HistoricalCells.Religion(suppressed.State,0)==0,"External repression preserves the existing household faith: no child lineage, grievance manufacture or public conversion.");
        suppressed.Tick(0);
        check(suppressed.Lay.Records[0]==1&&suppressed.State.History.Archive.Any(e=>e.Kind=="religious-reconciliation")&&HistoricalCells.Religion(suppressed.State,0)==0,"Relaxed funded restrictions permit resumed meetings without changing either household identity or public predominance.");
        var paper=new Fixture();paper.Seed();paper.Lay.Records=paper.Lay.Records.With(1,600);paper.State.History.Polities[0].Treasury=0;paper.Tick(2);
        check(paper.Lay.Records[1]==permitted.Lay.Records[1]&&paper.State.History.Polities[0].Treasury==0,"An unaffordable prohibition cannot claim the organizational effects of funded enforcement.");
        var poor=new Fixture();poor.Seed();poor.Lay.Records=poor.Lay.Records.With(1,600);poor.Town.FoodReserve=5000;poor.Tick();
        check(poor.Town.FoodReserve==5000&&poor.Lay.Records[1]<600&&poor.Lay.Records[7]<0,"Food needed for household survival cannot be spent to create ritual organization; unsupported activity declines.");

        var copy=f.Network.Copy();string prior=f.Lay.Records.Fingerprint();copy.LayAssociations.Records=copy.LayAssociations.Records.With(1,999);
        check(!ReferenceEquals(copy.LayAssociations,f.Lay)&&f.Lay.Records.Fingerprint()==prior&&copy.LayAssociations.Records[1]==999,"Publication shares immutable nodes but detaches the mutable optional association wrapper.");
        var serializer=new DataContractSerializer(typeof(HistoricalReligiousNetworks));using var bytes=new MemoryStream();serializer.WriteObject(bytes,f.Network);bytes.Position=0;var read=(HistoricalReligiousNetworks)serializer.ReadObject(bytes)!;
        check(StateHash.Compute(read)==StateHash.Compute(f.Network),"The optional organization, funding date and commitment serialize with exact canonical continuity.");
        var maximum=new HistoricalCellTree(2560000*8).With(2559999*8+7,123);
        check(maximum.Length<33554432&&maximum[2559999*8+7]==123,"Fixed eight-slot records fit maximum terrain detail without another per-world population array.");
        check(HistoricalLayAssociationEffects.OrganizingCapacity(300,900,false)==300&&HistoricalLayAssociationEffects.OrganizingCapacity(300,900,true)==700,"A minority cannot borrow another faith's public institutional capacity as its own clergy.");

        var carrier=new Fixture();carrier.Seed();carrier.Lay.Records=carrier.Lay.Records.With(1,500).With(7,carrier.State.Years);object[] args={carrier.State,0,1,0};long provisions=carrier.Town.FoodReserve;
        int faith=(int)typeof(Prehistory).GetMethod("LayReligiousCarrier",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(carrier.Engine,args)!;
        check(faith==1&&(int)args[3]==500&&carrier.Town.FoodReserve==provisions-2,"A real local carrier uses its recently funded association and pays exact extra source provisions, not the distant public institution's900 support.");
        Call(carrier.Engine,"RecordReligiousVisitorWithSupport",carrier.State,1,0,faith,(int)args[3]);
        check(carrier.Network.Read(1,12)==500&&HistoricalCells.Religion(carrier.State,1)==0,"A delivered association preserves actual carrier support and does not instantly convert the destination.");
        carrier.Lay.Records=carrier.Lay.Records.With(7,carrier.State.Years-21);args=new object[]{carrier.State,0,1,0};provisions=carrier.Town.FoodReserve;
        int stale=(int)typeof(Prehistory).GetMethod("LayReligiousCarrier",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(carrier.Engine,args)!;
        check(stale==0&&carrier.Town.FoodReserve==provisions,"Stale household memory cannot impersonate a currently provisioned traveling association.");

        var successor=new Fixture();successor.Seed();successor.Lay.Records=successor.Lay.Records.With(1,700);successor.State.History.Aggregates.Living.Cities[0].AbandonedYear=1000;successor.State.History.Aggregates.TownAt=successor.State.History.Aggregates.TownAt.With(0,1);
        check(!Prehistory.LayAssociationAt(successor.State,0)!.Active&&Prehistory.LayAssociationAt(successor.State,0)!.Support==0,"Reusing a physical cell does not resurrect an abandoned holder's active organization.");
        successor.Seed();check(successor.Lay.Records[4]==1&&successor.Lay.Records[1]==0,"An actual newly seeded community in the reused cell starts its own potential without inherited endowed support.");

        var reform=new Fixture();reform.Seed(0);reform.Town.Stability=300;reform.State.History.Dynamics.States[0].Cycle.EliteCapture=850;
        int generations=0;long beforeFood=reform.Town.FoodReserve,beforeCash=reform.State.History.Polities[0].Treasury;
        while(reform.State.History.Religions.Length==2&&generations++<60)reform.Tick(1);
        check(reform.State.History.Religions.Length==3&&reform.State.History.Religions[2].ParentId==0&&reform.Lay.Records[0]==2,"A controlled actually provisioned internal patronage dispute can form an autonomous parent-linked association through the seeded local process.");
        check(HistoricalCells.Religion(reform.State,0)==0&&reform.Network.Read(0,5)==1&&reform.State.History.Aggregates.TotalPopulation==20000&&reform.Town.FoodReserve<beforeFood&&reform.State.History.Polities[0].Treasury<beforeCash,"The genuine new-lineage control pays real gifts, assemblies and patronage, retaining public identity, household memory and every inhabitant.");
        var negative=new Fixture();negative.Seed(0);negative.Town.Stability=300;negative.State.History.Dynamics.States[0].Cycle.EliteCapture=850;negative.Town.FoodReserve=5000;
        for(int k=0;k<60;k++)negative.Tick(1);
        check(negative.State.History.Religions.Length==2&&negative.Lay.Records[5]==0,"The same long internal dispute cannot found a new lineage without actual local assembly provisions.");
        var old=new Fixture(19);old.Network.LayAssociations=f.Lay.Copy();string oldRoot=old.Lay.Records.Fingerprint();food=old.Town.FoodReserve;old.Seed();old.Tick();
        check(old.Lay.Records.Fingerprint()==oldRoot&&old.Town.FoodReserve==food,"Revision19 and earlier do not opt in even when a diagnostic optional record is attached.");
        Console.WriteLine("Controlled internal renewal formed after "+generations+" funded ten-year actor reviews; public faith unchanged.");
    }
}
