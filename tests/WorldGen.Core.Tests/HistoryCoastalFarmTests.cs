using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;
public static class HistoryCoastalFarmTests
{
    static object Call(Prehistory e,string name,params object[] args)=>typeof(Prehistory).GetMethods(BindingFlags.NonPublic|BindingFlags.Instance).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(e,args)!;
    static readonly FieldInfo Queue=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.NonPublic|BindingFlags.Instance)!;
    static long Number(object plan,string field)=>Convert.ToInt64(plan.GetType().GetField(field)!.GetValue(plan));
    public static void Run(Action<bool,string> check)
    {
        check(StateHash.Compute(new HistoricalColonialConnections())=="3e6f409c4cdc77e11d2119ec182e566258a65f5eaa5265d0ebc2bd8ea6d094ea","Absent coastal-farming policy preserves historical canonical fields exactly");
        check(HistoricalCoastalFarmEffects.Cohort(1799)==199&&HistoricalCoastalFarmEffects.Cohort(1800)==200&&HistoricalCoastalFarmEffects.Cohort(9000)==1000,"A small coastal farm needs real200 people above an urban retention floor and never exports more than1000 residents");
        for(int cohort=200;cohort<=1000;cohort+=13)
        {
            int urban=HistoricalCoastalFarmEffects.Urban(cohort);check(urban>=40&&urban+cohort-urban==cohort,"Cohort-sized urban/rural allocation conserves every actual emigrant");
            long area=HistoricalCoastalFarmEffects.AreaKm2(cohort,.15);check(area>=50&&area<=2000,"Real cleared catchment is bounded and proportional to the actual funded cohort");
        }
        check(HistoricalCoastalFarmEffects.AnnualOpportunity(8000,1000,.9,4,true,200)==HistoricalSettlementNetworks.AnnualOpportunity(8000,1600,.9,4,true,200),"Eligible port opportunities retain the existing annual hazard; only the actual minimum transferable cohort changes");
        check(HistoricalCoastalFarmEffects.AnnualOpportunity(1800,199,1,4,true,200)==0&&HistoricalCoastalFarmEffects.AnnualOpportunity(1800,200,1,4,true,29)==0,"Insufficient real households and immature ports cannot found a countryside");
        var x=new HistoricalCoastalFarming{SinceYear=50};x.Families=x.Families.With(10,5);var y=x.Copy();y.Families=y.Families.With(11,10);
        check(x.Families[11]<0&&y.Families[11]==10,"Local-family policy copies through immutable sparse structure without mutating its origin");
    }
    sealed class Candidate{public int Parent,Target;public object Plan;}
    static Candidate Find(Prehistory e,PrehistoryState s)
    {
        Call(e,"PrepareAggregateRelations",s);
        foreach(var p in s.History.Aggregates.Empires.Connections.ProvisionedPorts.Ports)
        {
            if(s.History.Aggregates.Living.Cities[p.TownId].AbandonedYear>=0)continue;
            for(int trial=0;trial<3;trial++)
            {
                int target=(int)Call(e,"LivingLocalTownCandidate",s,p.TownId,trial);object[] args={s,p.TownId,target,null};
                if((bool)Call(e,"PrepareCoastalFarm",args))return new Candidate{Parent=p.TownId,Target=target,Plan=args[3]};
            }
        }
        return null;
    }
    static bool Found(Prehistory e,PrehistoryState s,Candidate c)
    {
        Call(e,"PrepareAggregateRelations",s);var q=new SimulationEventQueue(s.Timeline.Events);Queue.SetValue(e,q);
        bool result=(bool)Call(e,"TryFoundLivingLocalTown",s,c.Parent,c.Target);
        s.Timeline.Events=q.Ordered();Queue.SetValue(e,null);return result;
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string hash=original.Hash;var e=new Prehistory(world);
        var s=e.UpgradeCoastalFarming(original);e.Validate(s);var a=s.History.Aggregates;
        check(original.ComputeHash()==hash&&s.History.Aggregates.Empires.Connections.CoastalFarming!=null,"Explicit coastal farming upgrade does not silently mutate the frozen V world");
        check(ReferenceEquals(e.UpgradeCoastalFarming(s),s),"Already enabled local families are not rebuilt or re-dated");
        var c=Find(e,s);check(c!=null,"Frozen V contains a real provisioned harbor and physically reachable, funded carried-crop-compatible hinterland candidate");if(c==null)return;
        if(world.Recipe.Seed==261015&&s.Years==171800)
        {
            var nw=e.UpgradeCoastalFarming(original).Copy();Call(e,"PrepareAggregateRelations",nw);object[] candidate={nw,1952,125733,null};
            check((bool)Call(e,"PrepareCoastalFarm",candidate),"The actual sparse northwestern continent has a viable carried-crop hinterland at cell125733 from its real3515-person harbor1952");
            long oldPeople=nw.History.Aggregates.TotalPopulation,oldSource=nw.Society.Settlements[1952].Population,northwestRemote=nw.History.Aggregates.Polities[nw.Society.Settlements[1952].PolityId].Remote;
            var target=new Candidate{Parent=1952,Target=125733,Plan=candidate[3]};int added=nw.History.Aggregates.Cities.Length;
            check(Found(e,nw,target)&&nw.Society.Settlements[1952].Population==oldSource-628&&nw.Society.Settlements[added].Population+nw.History.Aggregates.Cities[added].Rural==628&&nw.History.Aggregates.TotalPopulation==oldPeople,"Northwestern foothold transfers its real628 urban emigrants rather than the former2250-person national-remote fiction");
            check(nw.History.Aggregates.Polities[nw.Society.Settlements[1952].PolityId].Remote>=northwestRemote,"Northwestern local farming has no hidden remote-country migrant debit");nw.Seal();e.Validate(nw);
        }
        var parent=s.Society.Settlements[c.Parent];int owner=a.Resolve(parent.PolityId,s.History),region=e.CellPatch[c.Target],id=a.Cities.Length;
        long people=Number(c.Plan,"People"),urban=Number(c.Plan,"Urban"),area=Number(c.Plan,"Area"),provisions=Number(c.Plan,"Provisions"),investment=Number(c.Plan,"Investment");
        long population=a.TotalPopulation,worldUrban=a.TotalUrban,parentUrban=parent.Population,food=parent.FoodReserve,treasury=s.History.Polities[owner].Treasury,remote=a.Polities[owner].Remote,committed=a.Living.Regions[region].CommittedAreaKm2;
        check(people>=200&&people<=1000&&people<=Math.Max(0,parent.Population-1000)/4,"The exact candidate has only a real locally transferable urban cohort, not a2250-person decree");
        check(Found(e,s,c)&&a.Cities.Length==id+1,"A real paid source urban cohort can establish crop-compatible local farming without a national remote population draw");
        var child=s.Society.Settlements[id];var city=a.Cities[id];
        check(a.TotalPopulation==population&&parent.Population==parentUrban-people&&child.Population==urban&&city.Rural==people-urban&&a.TotalUrban==worldUrban-(people-urban),"Exact source urban loss becomes actual child urban/rural households and conserves global population");
        check(a.Polities[owner].Remote>=remote,"No national remote residents are consumed; incorporation may add only the actual destination's existing local residents");
        check(parent.FoodReserve==food-people-provisions&&child.FoodReserve==urban&&s.History.Polities[owner].Treasury==treasury-investment,"Startup food, additional real travel rations and clearing/construction charges are paid exactly once");
        check(city.AreaKm2==area&&area>=50&&area<=2000&&a.Living.Regions[region].CommittedAreaKm2==committed+area,"The actual smaller farming catchment is reserved in the regional land ledger");
        check(a.Empires.Connections.CoastalFarming.Families[id]==c.Parent&&a.Empires.Connections.ProvisionedPorts.ByTown[id]<0,"The child is an ordinary farming community with an O(1) inherited local-household policy, not another virtual supply port");
        check(child.Population<=200&&child.PolityId==owner,"Small paid village does not spontaneously create a new civilization");
        check(s.History.Towns[id].Cropping!=null&&s.History.Towns[id].Cropping.CropIds.Length>0&&s.History.Towns[id].Cropping.CultureId==parent.CultureId,"The founding community records its actual source's locally viable carried crops immediately, not an unexplained empty or newly invented crop package");
        s.Seal();e.Validate(s);
        int barren=world.Surface.Cells[parent.Cell].Neighbors.FirstOrDefault(n=>e.CellPatch[n]>=0&&a.TownAt[n]<0&&(double)Call(e,"LivingCropLandscape",s,parent.CultureId,n,false)<.02,-1);
        if(barren>=0){object[] bad={s,c.Parent,barren,null};check(!(bool)Call(e,"PrepareCoastalFarm",bad),"A real adjacent coast with incompatible carried crops remains unsuitable; urban migrants and treasury do not waive the field gate");}
        long actualCapacity=(long)Call(e,"CommunityUrbanCapacity",s,id);var remotePantry=s.Copy();
        remotePantry.History.Aggregates.Polities[owner].Remote=1000000000000000;remotePantry.History.Aggregates.Provinces.FarmPotential[owner]=1000000000000000;
        check((long)Call(e,"CommunityUrbanCapacity",remotePantry,id)==actualCapacity,"A huge distant national farming/remote ledger cannot inflate this local farm's food capacity or indirectly fabricate warehouse exports back to its harbor");
        long available=(long)Call(e,"LivingFounderAvailability",s,id),local=HistoricalSettlementMigration.Available(child.Population,city.Suburban,city.Rural,0,a.Polities[owner].CityCount);
        check(available==local,"A coastal village descendant can recruit actual local rural/suburban/urban households, not the empire's global remote pool");
        var tooMany=s.Copy();long before=tooMany.History.Aggregates.TotalPopulation;
        bool rejected=false;try{Call(e,"TransferLivingFounders",tooMany,id,local+1,child.CultureId);}catch(TargetInvocationException ex)when(ex.InnerException is InvalidOperationException){rejected=true;}
        check(rejected&&tooMany.History.Aggregates.TotalPopulation==before&&tooMany.History.Aggregates.Polities[owner].Remote==a.Polities[owner].Remote,"Even direct descendant transfer refuses to supplement insufficient local households from a huge distant remote account");
        var cityEvent=s.Copy();cityEvent.Years++;long oldRemote=cityEvent.History.Aggregates.Polities[owner].Remote;Call(e,"AdvanceAggregateCity",cityEvent,id);
        check(cityEvent.History.Aggregates.Polities[owner].Remote==oldRemote,"Modern ordinary city upkeep cannot teleport remote empire residents into the new overseas village");
        foreach(string failure in new[]{"food","treasury","households","delivery","works","health","ownership"})
        {
            var no=e.UpgradeCoastalFarming(original).Copy();Call(e,"PrepareAggregateRelations",no);
            if(failure=="food")no.Society.Settlements[c.Parent].FoodReserve=0;
            if(failure=="treasury")no.History.Polities[owner].Treasury=0;
            if(failure=="households")no.Society.Settlements[c.Parent].Population=1799;
            if(failure=="delivery"){int p=no.History.Aggregates.Empires.Connections.ProvisionedPorts.ByTown[c.Parent];no.History.Aggregates.Empires.Connections.ProvisionedPorts.Ports[p].LastDeliveryYear=no.Years-6;}
            if(failure=="works")no.History.Dynamics.States[owner].PublicWorks=0;
            if(failure=="health")no.History.Aggregates.Living.Cities[c.Parent].DeclineYears=1;
            if(failure=="ownership")no.Society.Settlements[c.Parent].PolityId=-1;
            long nf=no.Society.Settlements[c.Parent].FoodReserve,nc=no.History.Polities[owner].Treasury,np=no.History.Aggregates.TotalPopulation;int count=no.Society.Settlements.Length;
            check(!Found(e,no,c)&&no.Society.Settlements.Length==count&&no.Society.Settlements[c.Parent].FoodReserve==nf&&no.History.Polities[owner].Treasury==nc&&no.History.Aggregates.TotalPopulation==np,"Missing "+failure+" rejects the local farm without a food/population/fiscal bypass");
        }
        var future=e.AdvanceYears(s,3);var split=e.AdvanceYears(e.AdvanceYears(s,1),2);check(future.Hash==split.Hash,"Saved local-household families remain publication-partition deterministic");
        world.Prehistory=future;var restored=WorldFile.Read(WorldFile.Write(world));check(restored.Prehistory.Hash==future.Hash&&restored.Prehistory.History.Aggregates.Empires.Connections.CoastalFarming.Families[id]==c.Parent,"Family restriction and actual small farm ledgers survive a world-file roundtrip");
        check(new Prehistory(restored).AdvanceYears(restored.Prehistory,2).Hash==e.AdvanceYears(future,2).Hash,"Reloaded small-farm actors continue identically without hidden global population help");
        check(original.ComputeHash()==hash,"All controlled farm transactions leave the original V snapshot unchanged");
        Console.WriteLine($"Coastal farming actual fixture: parent={c.Parent}, target={c.Target}, people={people}, urban={urban}, rural={people-urban}, area={area}, provisions={provisions}, investment={investment}.");
    }
}
