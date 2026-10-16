using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryProvisionedPortTests
{
    static object Call(Prehistory e,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args)!;
    static readonly FieldInfo Queue=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;
    public static void Run(Action<bool,string> check)
    {
        check(StateHash.Compute(new HistoricalColonialConnections())=="3e6f409c4cdc77e11d2119ec182e566258a65f5eaa5265d0ebc2bd8ea6d094ea","Absent provisioned-port data preserves frozen-O canonical bytes");
        check(HistoricalProvisionedPortEffects.UrbanFounders(3499)<100&&HistoricalProvisionedPortEffects.UrbanFounders(8000)==250,"Tiny communities cannot produce a paid urban overseas cohort; large communities transfer at most250 real residents");
        check(HistoricalProvisionedPortEffects.OpportunityYears(1433)==3&&HistoricalProvisionedPortEffects.OpportunityYears(-1)==0,"Current supplier stock cannot imply fourteen centuries of food deliveries");
        check(HistoricalProvisionedPortEffects.RetentionPermille(12000,1)<HistoricalProvisionedPortEffects.RetentionPermille(12000,4),"Longer and less effective shipping loses more food");
        check(HistoricalProvisionedPortEffects.HandlingCeiling(0,0,0,1000)==250&&HistoricalProvisionedPortEffects.HandlingCeiling(1,1,1,1000)==10250,"Paid harbor handling is bounded by actual maritime/engine deployment, not an arbitrary era label");
        check(HistoricalProvisionedPortEffects.ExpansionResidents(250,10000,100000)==63&&HistoricalProvisionedPortEffects.ExpansionResidents(250,250,100000)==0&&HistoricalProvisionedPortEffects.ExpansionResidents(250,10000,1500)==0,"A handling upgrade adds at most one quarter and requires both installed technology headroom and actual urban migrants");
        for(int km=0;km<=15000;km+=1000)for(int people=1;people<=1000;people+=41)
        {
            int retention=HistoricalProvisionedPortEffects.RetentionPermille(km,2);
            var shipment=HistoricalFoodEffects.Deliver(people*3,people,retention,people*2);
            check(shipment.Sent==shipment.Received+shipment.Lost&&shipment.Sent<=people*2&&shipment.Received<=people,"Every delivered ration comes from a finite debited cargo and explicit transport loss");
            check(HistoricalProvisionedPortEffects.ShippingCost(shipment.Sent,km)>0,"Nonempty cargo incurs real fiscal shipping cost");
        }
        var original=new HistoricalProvisionedPorts{Ports=new[]{new HistoricalProvisionedPort{TownId=9,SupplierTownId=2}}};original.ByTown=original.ByTown.With(9,0);
        var copy=original.Copy();copy.Ports[0].SentFood=30;copy.ByTown=copy.ByTown.With(10,1);
        check(original.Ports[0].SentFood==0&&original.ByTown[10]<0,"Port accounts deep-copy while their immutable sparse index safely shares structure");
    }
    sealed class Candidate{public int Polity,Source,Post;public Prehistory.Route Route;public object Plan;}
    static Candidate Find(Prehistory e,PrehistoryState s)
    {
        var a=s.History.Aggregates;
        // Explicit offline fixture discovery only: production tests a fixed
        // retry batch, never all governments/posts/departure combinations.
        foreach(var p in s.History.Polities.Where(p=>p.EndedYear<0))
        {
            int[] departures=(int[])Call(e,"LivingColonialDepartures",s,p.Id);
            foreach(var post in a.Empires.Territories.Posts.Where(x=>x.ClosedYear<0&&x.Maritime&&a.Resolve(x.PolityId,s.History)==p.Id&&a.TownAt[x.Cell]<0))foreach(int from in departures)
            {
                var route=Route(e,from,post.Cell);object[] logistics={s,p.Id,route,a.TownAt[from],-1,0L};if(!(bool)Call(e,"MaritimeCharterLogistics",logistics))continue;
                int source=(int)logistics[4];object[] prepare={s,p.Id,route,source,null};
                if((bool)Call(e,"CanPrepareProvisionedPort",prepare))return new Candidate{Polity=p.Id,Source=source,Post=post.Id,Route=route,Plan=prepare[4]};
            }
        }
        return null;
    }
    static Prehistory.Route Route(Prehistory e,int from,int target)=>new Prehistory.Route{CellA=from,CellB=target,A=e.CellPatch[from],B=e.CellPatch[target],Sea=true,DistanceM=(int)Math.Round(D.Angle(e.World.Surface.Cells[from].Position,e.World.Surface.Cells[target].Position)*e.World.Recipe.RadiusKm*1000)};
    static int Found(Prehistory e,PrehistoryState s,Candidate c)
    {
        int count=s.History.Aggregates.Cities.Length;var q=new SimulationEventQueue(s.Timeline.Events);Queue.SetValue(e,q);
        Call(e,"FoundLivingTownBasis",s,s.Society.Settlements[c.Source].CultureId,c.Route.CellB,c.Source,c.Polity,c.Plan);
        s.Timeline.Events=q.Ordered();Queue.SetValue(e,null);return s.History.Aggregates.Cities.Length>count?count:-1;
    }
    public static void Review(string source,string outputWorld)
    {
        if(File.Exists(outputWorld)||Path.GetFullPath(source).Equals(Path.GetFullPath(outputWorld),StringComparison.OrdinalIgnoreCase))throw new IOException("Refusing to overwrite any review world.");
        var world=WorldFile.Read(File.ReadAllBytes(source));string original=world.Prehistory.ComputeHash();var e=new Prehistory(world);
        var s=e.UpgradeProvisionedPorts(e.UpgradeColonialPortRetries(e.UpgradeToLivingWorldHistory(world.Prehistory)));
        var c=Find(e,s)??throw new InvalidOperationException("No genuinely funded crop-incompatible live harbor office in this snapshot; no political/resource gates were relaxed.");
        int id=Found(e,s,c);if(id<0)throw new InvalidOperationException("Commit-time supplied-harbor preflight failed.");s.Seal();e.Validate(s);
        // Continue through the real calendar. Do not forge delivery dates,
        // lower cohesion, add food/money, or run a second generation route.
        var future=e.AdvanceYears(s,5);e.Validate(future);
        if(world.Prehistory.ComputeHash()!=original)throw new InvalidOperationException("Controlled fixture mutated its input.");
        world.Prehistory=future;Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputWorld))!);File.WriteAllBytes(outputWorld,WorldFile.Write(world));
        var ports=future.History.Aggregates.Empires.Connections.ProvisionedPorts;var p=ports.Ports[ports.ByTown[id]];
        File.WriteAllText(outputWorld+".review.json",System.Text.Json.JsonSerializer.Serialize(new{ControlledFixture=true,Explanation="One eligible existing funded office is deliberately selected for a normal urban-only foundation transaction; following five years use the actual actor calendar. This is not a naturally arisen replay outcome.",Source=Path.GetFullPath(source),InputHash=original,OutputHash=future.Hash,Cells=world.Surface.Cells.Length,City=id,c.Polity,SupplierTown=c.Source,Target=c.Route.CellB,c.Post,Years=future.Years,Active=future.History.Aggregates.Living.Cities[id].AbandonedYear<0,Supply=p},new System.Text.Json.JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine("Controlled provisioned harbor "+id+" at "+c.Route.CellB+" saved "+outputWorld+"; real deliveredfood "+p.ReceivedFood+"; "+future.Hash);
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string hash=original.ComputeHash();var e=new Prehistory(world);
        var s=e.UpgradeProvisionedPorts(e.UpgradeColonialPortRetries(e.UpgradeToLivingWorldHistory(original)));
        check(original.ComputeHash()==hash&&s.History.Aggregates.Empires.Connections.ProvisionedPorts!=null,"An explicit32 upgrade creates empty supply ledgers without mutating or silently upgrading the loaded checkpoint");
        check(ReferenceEquals(e.UpgradeProvisionedPorts(s),s),"Existing sparse port supply records are not rebuilt on read");
        if(world.Recipe.Seed==73421&&s.Years==129700&&world.Surface.Cells.Length>190000)
        {
            var sri=Route(e,66324,115037);object[] args={s,103,sri,499,null};
            check((double)Call(e,"LivingCropLandscape",s,s.Society.Settlements[499].CultureId,115037,false)<.02,"The actual Sri Lankan candidate retains its incompatible carried wheat/olive fields; no rice is manufactured for a convenient positive test");
            check(!(bool)Call(e,"CanPrepareProvisionedPort",args)&&s.History.Aggregates.Empires.Territories.PostAt[115037]<0,"Known physical shipping to Sri Lanka does not itself grant an owned funded harbor office or bypass the charter's political requirements");
        }
        var c=Find(e,s);check(c!=null,"The actual saved world contains a funded, live, crop-incompatible overseas office with real urban settlers and warehouse food");
        if(c==null)return;
        var a=s.History.Aggregates;var source=s.Society.Settlements[c.Source];long population=a.TotalPopulation,urban=a.TotalUrban,food=source.FoodReserve,cash=s.History.Polities[c.Polity].Treasury;
        long people=HistoricalProvisionedPortEffects.UrbanFounders(source.Population);long rural=a.Cities[c.Source].Rural;
        long provisions=HistoricalVoyageEffects.Provisioning(people,(int)Math.Min(int.MaxValue,c.Route.DistanceM+D.Angle(e.World.Surface.Cells[source.Cell].Position,e.World.Surface.Cells[c.Route.CellA].Position)*e.World.Recipe.RadiusKm*1000));
        long investment=HistoricalMaritimeIntegrationEffects.CouncilInvestment(people,c.Route.DistanceM);
        int id=Found(e,s,c);check(id>=0,"A validated paid urban-only harbor can be founded despite unsuitable carried fields");
        if(id<0)return;
        var town=s.Society.Settlements[id];var city=a.Cities[id];var record=a.Empires.Connections.ProvisionedPorts.Ports.Last();
        check(a.TotalPopulation==population&&a.TotalUrban==urban&&source.Population+town.Population>0&&a.Cities[c.Source].Rural==rural,"Founding conserves world urban/total people and does not recruit fictitious rural/remote farmers");
        check(town.Population==people&&city.Rural==0&&city.Suburban==0&&city.RuralCapacity==0&&city.AreaKm2==10&&town.CultivatedPermille==0,"Small import-dependent harbor reserves only10km² and declares no fields or farming households");
        check(source.FoodReserve==food-people*3-provisions&&town.FoodReserve==people*3&&s.History.Polities[c.Polity].Treasury==cash-investment,"Startup stock transfers once, voyage food is consumed once and harbor construction is paid once");
        check(a.Empires.Connections.PortSettlements.Governments[c.Polity].Pending.Positions[c.Post]<0,"Founded port is removed from the fair vacant-office queue");
        s.Seal();e.Validate(s);
        foreach(string failure in new[]{"money","food","works","office","urban"})
        {
            var no=e.UpgradeProvisionedPorts(e.UpgradeColonialPortRetries(e.UpgradeToLivingWorldHistory(original))).Copy();
            if(failure=="money")no.History.Polities[c.Polity].Treasury=0;
            if(failure=="food")no.Society.Settlements[c.Source].FoodReserve=0;
            if(failure=="works")no.History.Dynamics.States[c.Polity].PublicWorks=0;
            if(failure=="office")no.History.Aggregates.Empires.Territories.Posts[c.Post].ClosedYear=no.Years;
            if(failure=="urban")no.Society.Settlements[c.Source].Population=2000;
            long nf=no.Society.Settlements[c.Source].FoodReserve,nc=no.History.Polities[c.Polity].Treasury,np=no.History.Aggregates.TotalPopulation;int length=no.Society.Settlements.Length;
            check(Found(e,no,c)<0&&no.Society.Settlements.Length==length&&no.Society.Settlements[c.Source].FoodReserve==nf&&no.History.Polities[c.Polity].Treasury==nc&&no.History.Aggregates.TotalPopulation==np,"Commit-time revalidation rejects stale "+failure+" prepared basis without transferring anyone, food or money");
        }
        var consumption=s.Copy();consumption.Years++;consumption.Society.Settlements[id].FoodReserve=0;
        long populationBefore=consumption.Society.Settlements[id].Population,stockBefore=consumption.Society.Settlements[c.Source].FoodReserve;
        Call(e,"AdvanceAggregateCity",consumption,id);
        var cp=consumption.History.Aggregates.Empires.Connections.ProvisionedPorts.Ports.Last();
        check(stockBefore-consumption.Society.Settlements[c.Source].FoodReserve==cp.SentFood&&consumption.Society.Settlements[id].FoodReserve==Math.Max(0,cp.ReceivedFood-populationBefore),"Actual city event debits donor cargo then consumes delivered food exactly once; no virtual local harvest duplicates the shipment");
        check(consumption.Society.Settlements[id].CultivatedPermille==0&&consumption.History.Towns[id].ImportedFood==cp.ReceivedFood&&consumption.History.Towns[id].Cropping==null,"City upkeep keeps imported rations distinct from nonexistent local crops");
        // Force a real next shipment by consuming only this harbor's existing
        // initial stores. No donor production, people or money is added.
        var shipment=s.Copy();shipment.Years++;shipment.Society.Settlements[id].FoodReserve=0;
        var sender=shipment.Society.Settlements[c.Source];long beforeFood=sender.FoodReserve,beforeCash=shipment.History.Polities[c.Polity].Treasury;
        var port=shipment.History.Aggregates.Empires.Connections.ProvisionedPorts.Ports.Last();
        long delivered=(long)Call(e,"SupplyProvisionedPort",shipment,id,1,port);
        check(delivered>0&&beforeFood-sender.FoodReserve==port.SentFood&&port.SentFood==port.ReceivedFood+port.LostFood&&port.ReceivedFood==delivered,"A live recurring route debits its actual named warehouse, with every received unit reconciled to sent food and loss");
        check(beforeCash-shipment.History.Polities[c.Polity].Treasury==port.ShippingPaid&&port.ShippingPaid>0,"Recurring food shipping consumes actual government money");
        check(shipment.Society.Settlements[id].FoodReserve==0,"Supply returns cargo to its consuming city transaction, not an additional duplicate reserve credit");
        long afterSender=sender.FoodReserve,afterCash=shipment.History.Polities[c.Polity].Treasury;
        check((long)Call(e,"SupplyProvisionedPort",shipment,id,1,port)==0&&sender.FoodReserve==afterSender&&shipment.History.Polities[c.Polity].Treasury==afterCash,"A repeated same-date supply request cannot debit or count a second shipment");
        var expansion=s.Copy();expansion.Years+=40;
        var ep=expansion.History.Aggregates.Empires.Connections.ProvisionedPorts.Ports.Last();expansion.Society.Settlements[id].FoodReserve=0;
        long actualReceipt=(long)Call(e,"SupplyProvisionedPort",expansion,id,1,ep);expansion.Society.Settlements[id].FoodReserve+=actualReceipt;
        check(actualReceipt>0&&ep.LastDeliveryYear==expansion.Years,"A mature-port expansion fixture first obtains an actual paid recent shipment rather than fabricating a supply receipt");
        long oldWorld=expansion.History.Aggregates.TotalPopulation,oldUrban=expansion.History.Aggregates.TotalUrban,oldSender=expansion.Society.Settlements[c.Source].Population;
        long oldReceiver=expansion.Society.Settlements[id].Population,oldStocks=expansion.Society.Settlements[c.Source].FoodReserve+expansion.Society.Settlements[id].FoodReserve,oldTreasury=expansion.History.Polities[c.Polity].Treasury;
        Call(e,"ExpandProvisionedPort",expansion,id,ep,1.0);
        check(ep.MaximumResidents>250&&ep.MaximumResidents<=313&&ep.AdditionalMigrants==ep.MaximumResidents-250,"A real funded mature harbor installs a modest actual shipping-handling expansion, not a permanent250-person ceiling");
        check(expansion.Society.Settlements[c.Source].Population==oldSender-ep.AdditionalMigrants&&expansion.Society.Settlements[id].Population==oldReceiver+ep.AdditionalMigrants&&expansion.History.Aggregates.TotalPopulation==oldWorld&&expansion.History.Aggregates.TotalUrban==oldUrban,"Expansion transfers only existing source urban residents and conserves world people exactly");
        long expandedVoyage=HistoricalVoyageEffects.Provisioning(ep.AdditionalMigrants,(int)Math.Min(int.MaxValue,c.Route.DistanceM+D.Angle(e.World.Surface.Cells[source.Cell].Position,e.World.Surface.Cells[c.Route.CellA].Position)*e.World.Recipe.RadiusKm*1000));
        check(oldStocks-expansion.Society.Settlements[c.Source].FoodReserve-expansion.Society.Settlements[id].FoodReserve==expandedVoyage&&oldTreasury-expansion.History.Polities[c.Polity].Treasury==ep.ExpansionPaid&&ep.ExpansionPaid>0,"Expansion shifts migrant stocks once, consumes only voyage rations, and debits actual harbor investment once");
        int installed=ep.MaximumResidents;Call(e,"ExpandProvisionedPort",expansion,id,ep,1.0);check(ep.MaximumResidents==installed,"Repeated same-date city publication cannot purchase another harbor expansion");
        expansion.Years+=2;expansion.History.Aggregates.Cities[id].LastYear=expansion.Years-2;Call(e,"AdvanceAggregateCity",expansion,id);
        check(expansion.History.Aggregates.Cities[id].Rural==0&&expansion.History.Aggregates.Cities[id].Suburban==0&&expansion.Society.Settlements[id].CultivatedPermille==0,"An expanded harbor's multi-year city event retains actual urban-only households instead of synthesizing rural/suburban residents");
        foreach(string failure in new[]{"food","money","works","office","recent","health","urban"})
        {
            var no=s.Copy();no.Years+=40;var pr=no.History.Aggregates.Empires.Connections.ProvisionedPorts.Ports.Last();pr.LastDeliveryYear=no.Years;
            if(failure=="food")no.Society.Settlements[c.Source].FoodReserve=0;
            if(failure=="money")no.History.Polities[c.Polity].Treasury=0;
            if(failure=="works")no.History.Dynamics.States[c.Polity].PublicWorks=0;
            if(failure=="office")no.History.Aggregates.Empires.Territories.Posts[c.Post].ClosedYear=no.Years;
            if(failure=="recent")pr.LastDeliveryYear=no.Years-6;
            if(failure=="health")no.History.Aggregates.Living.Cities[id].DeclineYears=1;
            if(failure=="urban")no.Society.Settlements[c.Source].Population=1500;
            long nf=no.Society.Settlements[c.Source].FoodReserve,nc=no.History.Polities[c.Polity].Treasury,np=no.History.Aggregates.TotalPopulation;
            Call(e,"ExpandProvisionedPort",no,id,pr,1.0);
            check(pr.MaximumResidents==250&&pr.AdditionalMigrants==0&&no.Society.Settlements[c.Source].FoodReserve==nf&&no.History.Polities[c.Polity].Treasury==nc&&no.History.Aggregates.TotalPopulation==np,"Missing "+failure+" prevents handling expansion and does not silently create people/food or debit money");
        }
        foreach(string failure in new[]{"food","money","source","office","works","foreign"})
        {
            var no=s.Copy();no.Years++;no.Society.Settlements[id].FoodReserve=0;
            if(failure=="food")no.Society.Settlements[c.Source].FoodReserve=0;
            if(failure=="money")no.History.Polities[c.Polity].Treasury=0;
            if(failure=="source")no.History.Aggregates.Living.Cities[c.Source].AbandonedYear=no.Years;
            if(failure=="office")no.History.Aggregates.Empires.Territories.Posts[c.Post].ClosedYear=no.Years;
            if(failure=="works")no.History.Dynamics.States[c.Polity].PublicWorks=0;
            if(failure=="foreign")no.Society.Settlements[c.Source].PolityId=-1;
            long nf=no.Society.Settlements[c.Source].FoodReserve,nc=no.History.Polities[c.Polity].Treasury,np=no.History.Aggregates.TotalPopulation;
            var pr=no.History.Aggregates.Empires.Connections.ProvisionedPorts.Ports.Last();
            check((long)Call(e,"SupplyProvisionedPort",no,id,1,pr)==0&&no.Society.Settlements[c.Source].FoodReserve==nf&&no.History.Polities[c.Polity].Treasury==nc&&no.History.Aggregates.TotalPopulation==np,"Missing "+failure+" prevents delivery without minting food/money/people or charging a failed shipment");
        }
        var stale=s.Copy();stale.Society.Settlements[c.Source].FoodReserve=0;
        for(int i=0;i<100&&stale.History.Aggregates.Living.Cities[id].AbandonedYear<0;i++){stale.Years++;Call(e,"AdvanceAggregateCity",stale,id);}
        check(stale.History.Aggregates.Living.Cities[id].AbandonedYear>=0&&stale.Society.Settlements[id].Population==0,"A harbor cut off from all source food declines and is abandoned rather than an immortal colored250-person marker");
        var abandoned=s.Copy();Call(e,"AbandonLivingTown",abandoned,id,"Controlled supply-base loss.");abandoned.Seal();e.Validate(abandoned);
        check(abandoned.History.Aggregates.Empires.Connections.PortSettlements.Governments[c.Polity].Pending.Positions[c.Post]>=0,"Abandonment re-enqueues the actual empty harbor office for fair future review");
        var future=e.AdvanceYears(s,3);var split=e.AdvanceYears(e.AdvanceYears(s,1),2);check(future.Hash==split.Hash,"Supply stock/fiscal accounts and source city actors are publication-partition deterministic");
        world.Prehistory=future;var loaded=WorldFile.Read(WorldFile.Write(world));check(loaded.Prehistory.Hash==future.Hash&&loaded.Prehistory.History.Aggregates.Empires.Connections.ProvisionedPorts.Ports.Length>0,"Sparse supply basis and reconciled receipts round-trip actual world serialization");
        check(new Prehistory(loaded).AdvanceYears(loaded.Prehistory,2).Hash==e.AdvanceYears(future,2).Hash,"Reloaded supply ports continue identically to the original saved actor queue");
        check(original.ComputeHash()==hash,"All paid-port fixtures leave the original snapshot unchanged");
        Console.WriteLine($"Provisioned port actual fixture: year={s.Years}, state={c.Polity}, source={c.Source}, target={c.Route.CellB}, post={c.Post}, settlers={people}, sent={port.SentFood}, received={port.ReceivedFood}, lost={port.LostFood}, shipping={port.ShippingPaid}.");
    }
}
