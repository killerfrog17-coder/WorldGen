using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryProvisionedSupplierTests
{
    static object Call(Prehistory e,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args)!;
    public static void Trace(string path,string output,int years)
    {
        if(File.Exists(output)||years<1||years>500)throw new ArgumentException("New trace output and1–500 years required.");
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string hash=original.ComputeHash();var e=new Prehistory(world);
        var rows=new List<HistoricalPortSupplierTrace>();e.SupplierRoutingObserver=rows.Add;
        var next=e.AdvanceYears(e.UpgradeProvisionedPortSuppliers(original),years);e.Validate(next);
        if(original.ComputeHash()!=hash)throw new InvalidOperationException("Supplier trace mutated input.");
        File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(new{Source=Path.GetFullPath(path),InputHash=hash,Start=original.Years,End=next.Years,OutputHash=next.Hash,Changes=rows.Count,
            Reasons=rows.GroupBy(x=>x.Reason).Select(g=>new{Reason=g.Key,Changes=g.Count(),Setup=g.Sum(x=>x.Setup)}).ToArray(),
            Ports=rows.GroupBy(x=>x.Port).Select(g=>new{Port=g.Key,Changes=g.Count(),UniqueSources=g.Select(x=>x.NewSupplier).Append(g.First().OldSupplier).Distinct().Count(),Setup=g.Sum(x=>x.Setup),
                FinallyAbandoned=next.History.Aggregates.Living.Cities[g.Key].AbandonedYear,FinalPeople=next.Society.Settlements[g.Key].Population}).ToArray(),Rows=rows},new System.Text.Json.JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine("Exact actor supplier trace saved "+output);
    }
    public static void Audit(string path,string output)
    {
        if(File.Exists(output))throw new IOException("Refusing to overwrite an existing supplier audit.");
        var world=WorldFile.Read(File.ReadAllBytes(path));var s=world.Prehistory;string hash=s.ComputeHash();var a=s.History.Aggregates;
        var rows=a.Empires.Connections.ProvisionedPorts.Ports.Select(p=>
        {
            var town=s.Society.Settlements[p.TownId];var source=s.Society.Settlements[p.SupplierTownId];int owner=a.Resolve(town.PolityId,s.History),supplierOwner=a.Resolve(source.PolityId,s.History);
            int original=p.SupplierHistory?.OriginalSupplierTownId??p.SupplierTownId;var old=s.Society.Settlements[original];
            return new{p.TownId,town.Name,town.Cell,town.Population,Abandoned=a.Living.Cities[p.TownId].AbandonedYear,Owner=owner,p.FoundedYear,p.MaximumResidents,p.LastDeliveryYear,p.SupplierTownId,
                SourceName=source.Name,SourcePeople=source.Population,SourceStock=source.FoodReserve,SourceRural=a.Cities[p.SupplierTownId].Rural,SourceCapacity=a.Cities[p.SupplierTownId].RuralCapacity,
                SourceSupported=a.Cities[p.SupplierTownId].Supported,SourceAbandoned=a.Living.Cities[p.SupplierTownId].AbandonedYear,SourceOwner=supplierOwner,SameOwner=owner>=0&&owner==supplierOwner,
                OriginalSupplier=original,OriginalSourcePopulation=old.Population,OriginalSourceAbandoned=a.Living.Cities[original].AbandonedYear,
                SourceDistanceKm=D.Angle(world.Surface.Cells[source.Cell].Position,world.Surface.Cells[town.Cell].Position)*world.Recipe.RadiusKm,
                p.SentFood,p.ReceivedFood,p.LostFood,p.ShippingPaid,p.SupplierHistory};
        }).ToArray();
        if(s.ComputeHash()!=hash)throw new InvalidOperationException("Read-only supplier audit mutated input.");
        File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(new{Source=Path.GetFullPath(path),Year=s.Years,InputHash=hash,Preserved=true,Rows=rows,
            Changes=s.History.Archive.Where(x=>x.Kind=="harbor-supplier").Select(x=>new{x.Year,x.SettlementId,x.PolityId,x.Text}).ToArray()},new System.Text.Json.JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine("Read-only supplier audit saved "+output);
    }
    public static void Review(string path,string folder,int years)
    {
        if(years<1||years>1000)throw new ArgumentOutOfRangeException(nameof(years));
        if(Directory.Exists(folder))throw new IOException("Refusing to overwrite a supplier comparison folder.");
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string hash=original.ComputeHash();var e=new Prehistory(world);
        if(original.History.Aggregates.Empires.Connections.ProvisionedPorts?.SupplierRouting!=null)throw new ArgumentException("The matched baseline must have V null supplier-routing rules.");
        int beforeCount=original.History.Aggregates.Empires.Connections.ProvisionedPorts.Ports.Length;
        object Summary(PrehistoryState s)
        {
            var a=s.History.Aggregates;var ports=a.Empires.Connections.ProvisionedPorts.Ports;
            return new{Year=s.Years,Population=a.TotalPopulation,RecordedTowns=a.Cities.Length,ActiveTowns=a.Living.Cities.Count(x=>x.AbandonedYear<0),
                Ports=ports.Length,Live=ports.Count(p=>a.Living.Cities[p.TownId].AbandonedYear<0),
                OriginalCohortLive=ports.Take(beforeCount).Count(p=>a.Living.Cities[p.TownId].AbandonedYear<0),
                Residents=ports.Sum(p=>s.Society.Settlements[p.TownId].Population),
                SupplierChanges=ports.Sum(p=>(p.SupplierHistory?.EarlierSegments??0)+(p.SupplierHistory?.Recent.Length??1)-1),
                ChangedPorts=ports.Count(p=>(p.SupplierHistory?.EarlierSegments??0)+(p.SupplierHistory?.Recent.Length??1)>1),
                Sent=ports.Sum(p=>p.SentFood),Received=ports.Sum(p=>p.ReceivedFood),Lost=ports.Sum(p=>p.LostFood),Freight=ports.Sum(p=>p.ShippingPaid),
                Setup=ports.Sum(p=>(p.SupplierHistory?.EarlierSetupPaid??0)+(p.SupplierHistory?.Recent.Sum(x=>x.SetupPaid)??0)),Hash=s.Hash};
        }
        var timer=System.Diagnostics.Stopwatch.StartNew();var baseline=e.AdvanceYears(original,years);double baselineSeconds=timer.Elapsed.TotalSeconds;
        timer.Restart();var routing=e.AdvanceYears(e.UpgradeProvisionedPortSuppliers(original),years);double routingSeconds=timer.Elapsed.TotalSeconds;
        e.Validate(baseline);e.Validate(routing);if(original.ComputeHash()!=hash)throw new InvalidOperationException("Supplier review mutated its input.");
        Directory.CreateDirectory(folder);world.Prehistory=baseline;File.WriteAllBytes(Path.Combine(folder,"baseline.world"),WorldFile.Write(world));world.Prehistory=routing;File.WriteAllBytes(Path.Combine(folder,"routing.world"),WorldFile.Write(world));
        File.WriteAllText(Path.Combine(folder,"comparison.json"),System.Text.Json.JsonSerializer.Serialize(new{Source=Path.GetFullPath(path),InputHash=hash,Years=years,OriginalPorts=beforeCount,
            BaselineSeconds=baselineSeconds,RoutingSeconds=routingSeconds,Before=Summary(original),Baseline=Summary(baseline),Routing=Summary(routing)},new System.Text.Json.JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine("Matched supplier comparison saved "+Path.GetFullPath(folder));
    }
    public static void Run(Action<bool,string> check)
    {
        var old=new HistoricalProvisionedPorts{Ports=new[]{new HistoricalProvisionedPort{TownId=10,SupplierTownId=2,FoundedYear=40,DepartureCell=7,SentFood=100,ReceivedFood=80,LostFood=20,ShippingPaid=4}}};
        string hash=StateHash.Compute(old);var copy=old.Copy();
        check(copy.SupplierRouting==null&&copy.Ports[0].SupplierHistory==null&&StateHash.Compute(copy)==hash,"Null supplier-routing copies preserve legacy port accounts and canonical bytes");
        var initialize=typeof(Prehistory).GetMethod("InitializePortSupplierHistory",BindingFlags.Static|BindingFlags.NonPublic)!;
        initialize.Invoke(null,new object[]{copy.Ports[0],90});copy.SupplierRouting=new HistoricalPortSupplierRouting();
        var history=copy.Ports[0].SupplierHistory;
        check(history.OriginalSupplierTownId==2&&history.ActivatedYear==90&&history.Recent.Single().SinceYear==40,"Explicit activation retains actual original supplier and founding date, not fabricated replacement history");
        check(history.Recent[0].SentFood==100&&history.Recent[0].ReceivedFood==80&&history.Recent[0].LostFood==20&&history.Recent[0].ShippingPaid==4,"Pre-activation legacy cargo is attributed to its unchanged real source without resetting wealth");
        initialize.Invoke(null,new object[]{copy.Ports[0],100});check(history.ActivatedYear==90&&history.Recent.Length==1,"Supplier-account initialization is idempotent");
        var detached=copy.Copy();detached.Ports[0].SupplierHistory.Recent[0].SentFood++;
        check(copy.Ports[0].SupplierHistory.Recent[0].SentFood==100&&old.Ports[0].SupplierHistory==null,"Port supplier segments and nullable activation deep-copy independently");
        check(StateHash.Compute(copy)!=hash&&HistoricalPortSupplierHistory.RecentLimit==16,"Enabled supplier routing is canonical and its recent account portfolio has a fixed bound");
        check(HistoricalProvisionedPortEffects.ReplacementSource(10,40,false,5000,6000)&&HistoricalProvisionedPortEffects.ReplacementSource(10,2,false,5000,6000),"A genuinely producing younger local farm town may replace a remote original warehouse without an arbitrary founding-ID barrier");
        check(!HistoricalProvisionedPortEffects.ReplacementSource(10,40,true,5000,6000)&&!HistoricalProvisionedPortEffects.ReplacementSource(10,10,false,5000,6000)&&!HistoricalProvisionedPortEffects.ReplacementSource(10,40,false,0,6000),"Dependent ports, self-supply and non-producing nominal communities cannot supply replacement farm rations");
        var append=typeof(Prehistory).GetMethod("AppendPortSupplierSegment",BindingFlags.Static|BindingFlags.NonPublic)!;
        for(int i=1;i<=2000;i++)append.Invoke(null,new object[]{history,new HistoricalPortSupplierSegment{SupplierTownId=3,DepartureCell=8,SinceYear=90+i,SentFood=10,ReceivedFood=8,LostFood=2,ShippingPaid=3,SetupPaid=5}});
        check(history.Recent.Length==16&&history.EarlierSegments==1985&&history.OriginalSupplierTownId==2,"Two thousand dated supplier changes retain a fixed16 recent account portfolio and original attribution");
        check(history.EarlierSentFood+history.Recent.Sum(x=>x.SentFood)==20100&&history.EarlierReceivedFood+history.Recent.Sum(x=>x.ReceivedFood)==16080&&
            history.EarlierLostFood+history.Recent.Sum(x=>x.LostFood)==4020&&history.EarlierShippingPaid+history.Recent.Sum(x=>x.ShippingPaid)==6004&&history.EarlierSetupPaid+history.Recent.Sum(x=>x.SetupPaid)==10000,
            "Rolling old supplier segments conserves every cargo, transport loss, freight and actual setup unit");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string hash=original.ComputeHash();var e=new Prehistory(world);
        check(original.History.Aggregates.Empires.Connections.ProvisionedPorts?.SupplierRouting==null,"The actual V fixture retains its original null routing contract on read");
        var initialized=original.Copy();initialized.History.Aggregates.Empires.Connections.ProvisionedPorts=null;
        Call(e,"InitializeProvisionedPorts",initialized);
        check(initialized.History.Aggregates.Empires.Connections.ProvisionedPorts.SupplierRouting==null,"Fresh default port collections do not enable the rejected supplier-routing experiment");
        var s=e.UpgradeProvisionedPortSuppliers(original);
        check(original.ComputeHash()==hash&&s!=original&&s.History.Aggregates.Empires.Connections.ProvisionedPorts.SupplierRouting!=null,"Explicit W upgrade copies saved accounts without silently changing V input or minting funds");
        check(ReferenceEquals(e.UpgradeProvisionedPortSuppliers(s),s),"Supplier routing upgrade does not rebuild an activated state");
        var records=s.History.Aggregates.Empires.Connections.ProvisionedPorts.Ports;
        check(records.Length>0&&records.All(p=>p.SupplierHistory.Recent.Single().SentFood==p.SentFood),"Every existing V port retains reconciled historical original-source cargo on explicit upgrade");
        var dated=e.AdvanceYears(s,1);
        PrehistoryState fixture=null!,silent=null!;int selected=-1,oldSupplier=-1;long setupDebit=0;
        var traces=new List<HistoricalPortSupplierTrace>();e.SupplierRoutingObserver=traces.Add;
        // Offline fixture discovery only. The production actor samples at most
        // four indexed towns, never this list of saved ports.
        foreach(var record in dated.History.Aggregates.Empires.Connections.ProvisionedPorts.Ports)
        {
            if(dated.History.Aggregates.Living.Cities[record.TownId].AbandonedYear>=0||record.LastSupplyYear>=dated.Years)continue;
            var candidate=dated.Copy();var p=candidate.History.Aggregates.Empires.Connections.ProvisionedPorts.Ports[candidate.History.Aggregates.Empires.Connections.ProvisionedPorts.ByTown[record.TownId]];
            candidate.Society.Settlements[p.SupplierTownId].FoodReserve=0;candidate.Society.Settlements[p.TownId].FoodReserve=0;
            int government=candidate.History.Aggregates.Resolve(candidate.Society.Settlements[p.TownId].PolityId,candidate.History);if(government<0)continue;
            long treasury=candidate.History.Polities[government].Treasury;
            var before=candidate.Copy();
            if(!(bool)Call(e,"ReplaceProvisionedSupplier",candidate,p.TownId,1,p))continue;
            fixture=candidate;silent=before;selected=p.TownId;oldSupplier=record.SupplierTownId;setupDebit=treasury-candidate.History.Polities[government].Treasury;break;
        }
        e.SupplierRoutingObserver=null;
        check(fixture!=null,"The real saved V world has an owned live alternative warehouse in the bounded four-town portfolio; no donor food/money/people was added");
        if(fixture==null)return;
        var silentPort=silent.History.Aggregates.Empires.Connections.ProvisionedPorts.Ports[silent.History.Aggregates.Empires.Connections.ProvisionedPorts.ByTown[selected]];
        Call(e,"ReplaceProvisionedSupplier",silent,selected,1,silentPort);
        check(silent.ComputeHash()==fixture.ComputeHash()&&traces.Count==1&&traces[0].Port==selected,"An enabled primitive diagnostic observer records the actual change without changing any authoritative bytes or dates");
        var a=fixture.History.Aggregates;var port=a.Empires.Connections.ProvisionedPorts.Ports[a.Empires.Connections.ProvisionedPorts.ByTown[selected]];
        var target=fixture.Society.Settlements[selected];var supplier=fixture.Society.Settlements[port.SupplierTownId];int owner=a.Resolve(target.PolityId,fixture.History);
        check(port.SupplierTownId!=oldSupplier&&port.SupplierTownId!=selected&&a.Empires.Connections.ProvisionedPorts.ByTown[port.SupplierTownId]<0&&a.Cities[port.SupplierTownId].Rural>0,"Replacement uses a real producing ordinary town, excluding self-supply and dependent-port cycles");
        if(world.Recipe.Seed==31415&&original.Years==143200)check(port.SupplierTownId>selected,"The actual Pangeae fixture can obtain real food from a younger ordinary farming town rather than remaining bound to the original founding order");
        check(port.SupplierHistory.Recent.Length==2&&port.SupplierHistory.Recent[0].UntilYear==fixture.Years&&port.SupplierHistory.Recent[1].SinceYear==fixture.Years&&port.SupplierHistory.Recent[1].SetupPaid>0,"A paid dated route change closes the old attribution and opens the actual new source account");
        check(setupDebit==port.SupplierHistory.Recent.Last().SetupPaid&&setupDebit>0,"The replacement route's exact recorded setup expense was deducted once from the real current owner's treasury");
        long oldFood=supplier.FoodReserve,oldCash=fixture.History.Polities[owner].Treasury,oldPeople=a.TotalPopulation;
        long receipt=(long)Call(e,"SupplyProvisionedPort",fixture,selected,1,port);
        var current=port.SupplierHistory.Recent.Last();
        check(receipt>0&&oldFood-supplier.FoodReserve==current.SentFood&&current.SentFood==current.ReceivedFood+current.LostFood&&receipt==current.ReceivedFood,"Replacement shipments consume only the selected warehouse's actual cargo and reconcile transport loss");
        check(oldCash-fixture.History.Polities[owner].Treasury==current.ShippingPaid&&a.TotalPopulation==oldPeople,"Replacement supply pays actual freight and creates no people");
        check(a.Cities[selected].Rural==0&&a.Cities[selected].Suburban==0&&target.CultivatedPermille==0,"Replacement leaves the harbor import-dependent without phantom crop fields or farmer households");
        long afterFood=supplier.FoodReserve,afterCash=fixture.History.Polities[owner].Treasury;
        check((long)Call(e,"SupplyProvisionedPort",fixture,selected,1,port)==0&&supplier.FoodReserve==afterFood&&fixture.History.Polities[owner].Treasury==afterCash,"Repeated same-date city publication cannot contract/debit a second shipment");
        object[] self={fixture,selected,selected,port.DepartureCell,1,port,true};
        check(Call(e,"ProvisionedSupplyOption",self)==null,"A harbor cannot become its own supplier");
        var foreign=fixture.Copy();foreign.Society.Settlements[port.SupplierTownId].PolityId=-1;
        check(Call(e,"ProvisionedSupplyOption",foreign,selected,port.SupplierTownId,port.DepartureCell,1,port,true)==null,"Foreign or ownerless warehouses do not become free trade suppliers");
        var broke=fixture.Copy();broke.History.Polities[owner].Treasury=0;
        check(Call(e,"ProvisionedSupplyOption",broke,selected,port.SupplierTownId,port.DepartureCell,1,port,true)==null,"A new route cannot be contracted without actual setup/freight money");
        var empty=fixture.Copy();empty.Society.Settlements[port.SupplierTownId].FoodReserve=0;
        check(Call(e,"ProvisionedSupplyOption",empty,selected,port.SupplierTownId,port.DepartureCell,1,port,true)==null,"A supported city with no warehouse stock is not a magical supply option");
        fixture.Seal();e.Validate(fixture);
        world.Prehistory=fixture;var loaded=WorldFile.Read(WorldFile.Write(world));
        check(loaded.Prehistory.Hash==fixture.Hash&&loaded.Prehistory.History.Aggregates.Empires.Connections.ProvisionedPorts.Ports[a.Empires.Connections.ProvisionedPorts.ByTown[selected]].SupplierHistory.Recent.Length==2,"Dated supplier history and reconciled cargo survive actual world save/load");
        check(e.AdvanceYears(fixture,2).Hash==e.AdvanceYears(e.AdvanceYears(fixture,1),1).Hash,"Replacement routes and future city events remain publication-partition deterministic");
        check(new Prehistory(loaded).AdvanceYears(loaded.Prehistory,2).Hash==e.AdvanceYears(fixture,2).Hash,"Saved replacement route continues identically after reload");
        check(original.ComputeHash()==hash,"All routing diagnosis/upgrade fixtures preserve the original V save");
        Console.WriteLine($"Actual routing fixture: port {selected}, supplier {oldSupplier}->{port.SupplierTownId}, received {receipt}, year {fixture.Years}.");
    }
}
