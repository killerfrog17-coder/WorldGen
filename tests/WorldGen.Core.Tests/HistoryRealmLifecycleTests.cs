using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HistoryRealmLifecycleTests
{
    public static void Run(Action<bool,string> check)
    {
        double fragile=HistoricalRealmEffects.SuccessionHazard(900,500,500,3,1,.2,false);
        double integrated=HistoricalRealmEffects.SuccessionHazard(0,200,0,.5,0,.9,false);
        check(fragile>.5&&integrated<.02,"Personal conquest and poor service can trigger crises; integrated peaceful realms have no expiry timer.");
        check(HistoricalRealmEffects.SuccessionHazard(900,500,500,3,1,.2,true)<fragile/3,"Representative transfers are not personal imperial inheritance.");
        check(HistoricalRealmEffects.ProvincialRupture(.9,3,.1,true,.1)>HistoricalRealmEffects.ProvincialRupture(.9,.2,.9,false,.8)*5,"Remote excluded centers face greater rupture risk than integrated local institutions.");
        check(HistoricalRealmEffects.ProvincialRupture(0,10,0,true,0)==0,"No succession rupture outside a crisis.");
        var rules=new HistoricalRealmLifecycles{Governments=new[]{new HistoricalRealmAccount{FoundedYear=10,RegimeSince=20}}};var copy=rules.Copy();copy.Governments[0].RegimeSince=30;
        check(rules.Governments[0].RegimeSince==20,"Published realm records detach.");
        check(StateHash.Compute(new HistoricalColonialConnections())=="3e6f409c4cdc77e11d2119ec182e566258a65f5eaa5265d0ebc2bd8ea6d094ea","Absent realm extension preserves old canonical bytes.");
        check(HistoricalRealmEffects.LegitimacyTarget(.8,1,100,0,0,750)>HistoricalRealmEffects.LegitimacyTarget(.1,.2,800,800,600,100),"Fiscal support, inclusion and institutions matter more than merely retaining some cash.");
        check(HistoricalRealmPoliticalEffects.WarHazard(.9,.7,.5,true,false,true,1,1)>HistoricalRealmPoliticalEffects.WarHazard(.2,.1,0,true,true,false,0,1)*10,"Ambitious supplied conquerors can campaign more readily than allied peaceful peers.");
        check(HistoricalRealmPoliticalEffects.SelfDetermination(1,1,true,true,.8,.2)>HistoricalRealmPoliticalEffects.SelfDetermination(1,0,true,true,.8,.2)*5,"Mass communication increases colonial self-determination pressure independently of army reach.");
        check(HistoricalRealmPoliticalEffects.SelfDetermination(1,1,false,true,.8,.2)==0,"Cultural integration is not automatically colonial exclusion.");
        check(HistoricalRealmPoliticalEffects.IdeologicalDistance(100,900,900,200,1)>.7&&HistoricalRealmPoliticalEffects.IdeologicalDistance(100,900,900,200,0)==0,"Mass political ideological rivalry is not projected backward onto pre-literate courts.");
        check(HistoricalRealmEffects.CohesionTarget(1,350,100,0)>HistoricalRealmEffects.CohesionTarget(0,350,100,0)+150,"Modern administrative capacity can maintain cohesion without preventing colonial opposition.");
        check(HistoricalRealmEffects.RulerTenure(0)==3&&HistoricalRealmEffects.RulerTenure(.5)<16&&HistoricalRealmEffects.RulerTenure(1)==50,"Court tenures include short reigns and exceptional longer ones, rather than requiring every ruler to last18–50years.");
        double settler=HistoricalRealmPoliticalEffects.SettlerSelfGovernment(300,5000,850,1,.15,700,400,false);
        check(settler>.004&&HistoricalRealmPoliticalEffects.SettlerSelfGovernment(30,5000,850,1,.15,700,400,false)==0,"Long-resident overseas settler institutions can seek self-government without needing a different initial ethnic label.");
        check(HistoricalRealmPoliticalEffects.SettlerSelfGovernment(300,5000,850,1,.15,700,400,true)<settler/2,"An external military threat can make continued imperial protection more valuable.");
        check(HistoricalRealmEffects.CourtFailureHazard(.9)<HistoricalRealmEffects.CourtFailureHazard(.1)/40,"Functioning impersonal institutions sharply reduce whole-court failure without erasing local opposition or war.");
        var routes=new HistoricalRealmClaimRoutes{IndexCount=new HistoricalRealmIndexCount{Value=3}};var routesCopy=routes.Copy();routesCopy.IndexCount.Value=4;
        check(routes.IndexCount.Value==3,"Published bounded title indexes detach their mutable allocation counter.");
        check(HistoricalRealmPoliticalEffects.FleetCohesionFloor(0)==500&&HistoricalRealmPoliticalEffects.FleetCohesionFloor(1)==320,"Modern funded naval administration can operate through ordinary cabinet instability; early courts retain the stricter unity requirement.");
        var legacyRoutes=new HistoricalRealmClaimRoutes{Sources=new HistoricalCellTree(10).With(2,5),EffortM=new HistoricalCellTree(10).With(2,1234)};
        using(var bytes=new MemoryStream())
        {
            using(var writer=new BinaryWriter(bytes,System.Text.Encoding.UTF8,true))
            {
                writer.Write((byte)1);writer.Write("WorldGen.Core.HistoricalRealmClaimRoutes");
                var write=typeof(StateHash).GetMethod("Write",BindingFlags.Static|BindingFlags.NonPublic);
                writer.Write("EffortM");write.Invoke(null,new object[]{writer,legacyRoutes.EffortM});writer.Write("Sources");write.Invoke(null,new object[]{writer,legacyRoutes.Sources});
            }
            string preceding=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes.ToArray())).ToLowerInvariant();
            check(StateHash.Compute(legacyRoutes)==preceding,"Absent title indexes preserve the exact two-field prototype canonical representation.");
        }
    }
    public static void World(string path)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var e=new Prehistory(w);var source=w.Prehistory;string original=source.Hash;
        var s=e.UpgradeRealmLifecycles(source);e.Validate(s);int count=0;
        void Check(bool ok,string message){count++;if(!ok)throw new Exception(message);}
        Check((source.History.Aggregates.Empires.Connections.Realms?.Version??0)<new HistoricalRealmLifecycles().Version,"Source retains its preceding realm rules.");
        Check(s.History.Aggregates.Empires.Connections.Realms.Version==new HistoricalRealmLifecycles().Version&&s.History.Aggregates.Empires.Connections.Realms.Claimants!=null&&s.History.Aggregates.Empires.Connections.SeaService!=null&&s.History.Aggregates.Empires.Connections.Religion?.Version==2&&s.History.Aggregates.Empires.Connections.Military!=null&&s.History.Aggregates.Empires.Connections.ServiceRecovery!=null,"Explicit upgrade installs the current reviewed realm rules without rewriting the past.");
        Check(s.History.Aggregates.TotalPopulation==source.History.Aggregates.TotalPopulation,"Upgrade changes no population.");
        Check(ReferenceEquals(e.UpgradeRealmLifecycles(s),s),"Upgrade is idempotent.");
        Check(Prehistory.CanUpgradeHistory(source)&&!Prehistory.CanUpgradeHistory(s),"Existing32 worlds expose the explicit update-rules action; current realm worlds do not auto-upgrade.");
        int p=Array.FindIndex(s.History.Polities,x=>x.EndedYear<0);int founded=s.History.Polities[p].FoundedYear;
        var method=typeof(Prehistory).GetMethod("TryLivingRegimeChange",BindingFlags.Instance|BindingFlags.NonPublic);
        Check(!(bool)method.Invoke(e,new object[]{s,p})&&s.History.Polities[p].FoundedYear==founded&&s.History.Polities.Length==source.History.Polities.Length,"Government changes do not refound countries.");
        w.Prehistory=s;var loaded=WorldFile.Read(WorldFile.Write(w)).Prehistory;
        var full=e.AdvanceYears(s,30);var pieces=e.AdvanceYears(e.AdvanceYears(loaded,13),17);e.Validate(full);
        Check(full.Hash==pieces.Hash,"Crises replay across saves and partitioned advances.");
        Check(new Prehistory(w).AdvanceYears(loaded,30).Hash==full.Hash,"Cold geographical/bearing caches produce identical outcomes to a reused engine.");
        Check(source.ComputeHash()==original,"Original checkpoint remains unchanged.");
        Check(HistoryAtlas.Read(s,"state",p,w,e).Facts[0].Label=="Country founded","The actual state profile distinguishes country foundation from regime and allegiance dates.");
        var supply=typeof(Prehistory).GetMethod("RealmSupplierChainKm",BindingFlags.Instance|BindingFlags.NonPublic);
        bool testedSupply=false;
        foreach(var office in s.History.Aggregates.Empires.Territories.Posts)
        {
            int owner=s.History.Aggregates.Resolve(office.PolityId,s.History);if(owner<0||office.ClosedYear>=0)continue;
            int node=s.History.Aggregates.Empires.Territories.Sources[office.Cell],supplier=node;
            // A transferred office's old record can outlive reassignment of its
            // cell to a different real service node. Test the current chain,
            // never compare that new chain with the superseded office's cost.
            if(node!= -2-office.Id||s.History.Aggregates.Empires.Territories.PostAt[office.Cell]!=office.Id)continue;
            for(int depth=0;depth<8&&supplier< -1;depth++)supplier=s.History.Aggregates.Empires.Territories.Posts[-supplier-2].ParentSource;
            if(supplier<0)continue;double distance=(double)supply.Invoke(e,new object[]{s,owner,office.Cell,supplier});if(distance<0)continue;
            Check(distance>=office.MessageEffortKm,"An actual recorded administrative supply chain retains its full travel effort rather than a single local service radius.");
            var interrupted=s.Copy();interrupted.History.Aggregates.Empires.Territories.Posts[office.Id].ClosedYear=interrupted.Years;
            Check((double)supply.Invoke(e,new object[]{interrupted,owner,office.Cell,supplier})<0,"Closing a real link interrupts supply; an owned coastline alone never becomes a food supplier.");
            testedSupply=true;
            break;
        }
        Check(testedSupply||!s.History.Aggregates.Empires.Territories.Posts.Any(x=>x.ClosedYear<0),"A world with recorded live offices exercises one actual currently supplied administrative chain.");
        var country=s.History.Polities.Where(x=>x.EndedYear<0&&s.History.Aggregates.Polities[x.Id].Cities.Count>=2)
            .OrderByDescending(x=>{int r=e.CellPatch[s.Society.Settlements[x.CapitalId].Cell],t=s.History.Aggregates.Empires.Connections.TitleAt[r];return t>=0&&s.History.Aggregates.Resolve(s.History.Aggregates.Empires.Connections.Titles[t].Holder,s.History)==x.Id;}).FirstOrDefault();
        if(country!=null)
        {
            var probe=s.Copy();var a=probe.History.Aggregates;int city=country.CapitalId;
            Check((bool)typeof(Prehistory).GetMethod("ContestFailingRealmCourt",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(e,new object[]{probe,country.Id})&&probe.History.Polities[country.Id].EndedYear<0&&a.Empires.Connections.Realms.Governments[country.Id].CrisisUntil>probe.Years,"Failure of a multi-center court starts an actual regional contest rather than instantly deleting every local government.");
            int region=e.CellPatch[probe.Society.Settlements[city].Cell],title=probe.History.Aggregates.Empires.Connections.TitleAt[region];
            bool localTitle=title>=0&&a.Resolve(probe.History.Aggregates.Empires.Connections.Titles[title].Holder,probe.History)==country.Id&&probe.Society.Settlements[city].Population>=1200&&a.Cities[city].Supported;
            if(localTitle)
            {
                var routes=probe.History.Aggregates.Empires.Connections.Realms.ClaimRoutes;
                routes.Sources=routes.Sources.With(region,probe.Society.Settlements[city].Cell);routes.EffortM=routes.EffortM.With(region,0);
                probe.Seal();e.Validate(probe);
            }
            long funds=probe.History.Polities.Sum(x=>x.Treasury),people=a.TotalPopulation;int states=probe.History.Polities.Length;
            var queue=new SimulationEventQueue(probe.Timeline.Events);var queueField=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic);queueField.SetValue(e,queue);
            try{typeof(Prehistory).GetMethod("FoundAggregateState",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(e,new object[]{probe,city,country.Id});}
            finally{queueField.SetValue(e,null);}
            probe.Timeline.Events=queue.Ordered();probe.Seal();e.Validate(probe);
            Check(probe.History.Polities.Length==states+1&&probe.History.Polities.Sum(x=>x.Treasury)==funds&&a.TotalPopulation==people,"A controlled real-center separation divides existing public funds and preserves every person.");
            if(localTitle)Check(HistoricalRecognizedClaims.Holder(probe,probe.Society.Settlements[city].Cell)==states,"A district's recorded jurisdiction follows its actual separating administrative center, not the forever-unchanged old imperial color.");
        }
        Console.WriteLine("PASS "+count+" realm actual-world assertions");
    }
    public static void Review(string path,string folder,int years,bool candidate)
    {
        if(Directory.Exists(folder)||years<1||years>30000)throw new ArgumentException("Use a new folder and 1..30000 years.");
        Directory.CreateDirectory(folder);var w=WorldFile.Read(File.ReadAllBytes(path));string inputHash=w.Prehistory.Hash;var e=new Prehistory(w);var initial=candidate?e.UpgradeRealmLifecycles(e.UpgradeGeographicKnowledge(w.Prehistory)):w.Prehistory;
        var session=new SimulationSession(e,initial);var rows=new List<object>();int start=initial.Years;var watch=System.Diagnostics.Stopwatch.StartNew();int captures=0;
        int archive=initial.History.Archive.Count,lastArchive=archive,lastEra=WorldEraView.Of(initial);var previous=initial;var photographed=new HashSet<int>();
        while(session.LastSnapshot.Years<start+years)
        {
            session.AdvanceSeconds(Math.Min(25,start+years-session.LastSnapshot.Years)*SimulationTime.Year,int.MaxValue);var s=session.LastSnapshot;var h=s.History;var a=h.Aggregates;
            var states=h.Polities.Where(p=>p.EndedYear<0).ToArray();var roots=states.Where(p=>HistoricalImperialHierarchy.Patron(s,p.Id)<0).ToArray();
            var leaders=roots.OrderByDescending(p=>HistoricalImperialHierarchy.AreaKm2(s,p.Id)).Take(8).Select(p=>new{p.Id,p.Name,Age=s.Years-HistoricalRealmLifecycles.Founded(s,p.Id),Area=HistoricalImperialHierarchy.AreaKm2(s,p.Id),Population=HistoricalImperialHierarchy.Population(s,p.Id),Subjects=a.Empires.Governments[p.Id].SubjectCount,Debt=h.Dynamics.States[p.Id].Cycle.ConquestDebt,p.Cohesion,Strain=h.Dynamics.States[p.Id].Strain,Reach=a.Polities[p.Id].ReachKm,Representation=a.Communities.Governments[p.Id].RepresentationMilli}).ToArray();
            var events=h.Archive.Skip(archive).ToArray();
            var row=new{Year=s.Years,Era=WorldEraView.Name(WorldEraView.Of(s)),MaximumTechnologyYear=a.FirstMaximumTechnologyYear,Seconds=watch.Elapsed.TotalSeconds,Population=a.TotalPopulation,States=states.Length,Sovereigns=roots.Length,Crises=a.Empires.Connections.Realms?.Governments.Count(g=>g.CrisisUntil>s.Years),Events=events.GroupBy(x=>x.Kind).OrderBy(g=>g.Key).ToDictionary(g=>g.Key,g=>g.Count()),Leaders=leaders};
            rows.Add(row);if((s.Years-start)%100==0||s.Years==start+years)Console.WriteLine(JsonSerializer.Serialize(row));
            var recent=h.Archive.Skip(lastArchive).ToArray();
            int fragment=previous.History.Polities.Where(p=>p.EndedYear<0&&HistoricalImperialHierarchy.Patron(previous,p.Id)<0&&HistoricalImperialHierarchy.AreaKm2(previous,p.Id)>=1000000&&!photographed.Contains(p.Id))
                .Where(p=>(h.Polities[p.Id].EndedYear>=0?0:HistoricalImperialHierarchy.AreaKm2(s,p.Id))<HistoricalImperialHierarchy.AreaKm2(previous,p.Id)*.6&&recent.Count(x=>x.OtherPolityId==p.Id&&(x.Kind=="imperial-independence"||x.Kind=="secession"))>=2).OrderByDescending(p=>HistoricalImperialHierarchy.AreaKm2(previous,p.Id)).Select(p=>p.Id).DefaultIfEmpty(-1).First();
            bool finished=s.Years==start+years||initial.History.Aggregates.FirstMaximumTechnologyYear<0&&a.FirstMaximumTechnologyYear>=0&&s.Years>=a.FirstMaximumTechnologyYear+1000;
            if(finished||captures<8&&(s.Years==start+25||WorldEraView.Of(s)!=lastEra||fragment>=0))
            {
                if(fragment>=0){photographed.Add(fragment);string before=Path.Combine(folder,previous.Years.ToString());Directory.CreateDirectory(before);HistoryAudit.Snapshot(w,e,previous,before,new[]{HistoryOverlay.States},compressed:true,backgroundOpacity:0,overlayOpacity:.85,includeLabels:true,includeSettlements:false);}
                string output=Path.Combine(folder,s.Years.ToString());Directory.CreateDirectory(output);HistoryAudit.Snapshot(w,e,s,output,new[]{HistoryOverlay.States,HistoryOverlay.Technology},compressed:true,backgroundOpacity:0,overlayOpacity:.85,includeLabels:true,includeSettlements:false);captures++;
            }
            previous=s;lastArchive=h.Archive.Count;lastEra=WorldEraView.Of(s);
            if(finished)break;
        }
        w.Prehistory=session.Snapshot();e.Validate(w.Prehistory);File.WriteAllBytes(Path.Combine(folder,"final.world"),WorldFile.Write(w));
        double land=0,controlled=0,recognized=0;var ending=w.Prehistory;
        // Offline review only: never a recurring simulation process.
        for(int cell=0;cell<w.Surface.Cells.Length;cell++)
        {
            if(w.Hydrology.OceanConnected[cell]||w.Hydrology.LakeCoverageFraction[cell]>=.9||w.Ecology.Habitat[cell].Biome==Biome.Ice)continue;
            double area=w.Surface.Cells[cell].AreaM2/1000000.0*(1-w.Hydrology.LakeCoverageFraction[cell]);land+=area;
            if(HistoricalCells.Polity(ending,cell)>=0){controlled+=area;recognized+=area;}
            else if(HistoricalRecognizedClaims.Holder(ending,cell)>=0)recognized+=area;
        }
        File.WriteAllText(Path.Combine(folder,"coverage.json"),JsonSerializer.Serialize(new{Year=ending.Years,LandKm2=land,ControlledFraction=controlled/land,SovereigntyFraction=recognized/land,Definition="Non-ocean, non-whole-lake, non-ice-biome area; administration and recognized external sovereignty are distinct."},new JsonSerializerOptions{WriteIndented=true}));
        string coreHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(Prehistory).Assembly.Location))).ToLowerInvariant();
        File.WriteAllText(Path.Combine(folder,"review.json"),JsonSerializer.Serialize(new{Input=path,InputHash=inputHash,StartHash=initial.Hash,CoreAssemblySha256=coreHash,Candidate=candidate,Rows=rows},new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(folder,"events.json"),JsonSerializer.Serialize(w.Prehistory.History.Archive.Skip(archive),new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
    }
    public static void Diagnostic(string path,string output)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var e=new Prehistory(w);var initial=w.Prehistory;var reasons=new SortedDictionary<string,int>();
        e.SeaVentureObserver=(polity,reason)=>{string key=reason;reasons[key]=reasons.TryGetValue(key,out int n)?n+1:1;};
        e.RealmRecognitionObserver=(polity,region,reason)=>{string key="recognition-"+reason;reasons[key]=reasons.TryGetValue(key,out int n)?n+1:1;};
        var session=new SimulationSession(e,initial);session.AdvanceSeconds(100*SimulationTime.Year,int.MaxValue);var s=session.Snapshot();e.Validate(s);
        var h=s.History;var a=h.Aggregates;
        var departure=typeof(Prehistory).GetMethod("LivingColonialDepartures",BindingFlags.Instance|BindingFlags.NonPublic);
        var states=h.Polities.Where(p=>p.EndedYear<0).OrderByDescending(p=>p.Population).Select(p=>new{p.Id,p.Name,p.Cohesion,p.Exhaustion,p.Treasury,p.Population,Capital=s.Society.Settlements[p.CapitalId].Cell,
            a.Polities[p.Id].Receipts,a.Polities[p.Id].ServiceCost,a.Polities[p.Id].Urban,Cities=a.Polities[p.Id].Cities.Count,Posts=a.Empires.Governments[p.Id].AdministrativePosts.Count,Departures=(int[])departure.Invoke(e,new object[]{s,p.Id}),
            Navigation=HistoricalModernEffects.Adoption(h.Cultures[p.CultureId],HistoricalTechnology.Navigation,s.Years),Ocean=HistoricalModernEffects.Adoption(h.Cultures[p.CultureId],HistoricalTechnology.OceanNavigation,s.Years),Steam=HistoricalModernEffects.Adoption(h.Cultures[p.CultureId],HistoricalTechnology.SteamPower,s.Years),
            Voyages=a.Empires.Connections.Geography?.Governments[p.Id].Voyages,Surveys=a.Empires.Connections.Geography?.Governments[p.Id].Surveys,Landfalls=a.Empires.Connections.Geography?.Governments[p.Id].Landfalls,
            Know=a.Empires.Connections.Geography?.Governments[p.Id].Places.Count}).ToArray();
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=path,Reasons=reasons,States=states},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(new{Source=path,Reasons=reasons}));
    }
    public static void CoverageAudit(string path,string output)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var e=new Prehistory(w);var s=w.Prehistory;var a=s.History.Aggregates;var c=a.Empires.Connections;
        var rows=new List<object>();
        for(int region=0;region<e.Patches.Length;region++)
        {
            int cell=a.Regions[region].BestCell;double area=0,missing=0;
            foreach(int v in e.Patches[region].Cells)
            {
                if(w.Hydrology.OceanConnected[v]||w.Hydrology.LakeCoverageFraction[v]>=.9||w.Ecology.Habitat[v].Biome==Biome.Ice)continue;
                double km2=w.Surface.Cells[v].AreaM2/1e6;area+=km2;
                if(HistoricalCells.Polity(s,v)<0&&HistoricalRecognizedClaims.Holder(s,v)<0)missing+=km2;
            }
            if(missing<10000)continue;
            var neighbors=a.Regions[region].Routes.Select(id=>e.Routes[id]).Select(r=>new{Region=r.A==region?r.B:r.A,r.Sea,r.DistanceM})
                .Select(r=>new{r.Region,r.Sea,r.DistanceM,Title=c.TitleAt[r.Region],Holder=c.TitleAt[r.Region]>=0?a.Resolve(c.Titles[c.TitleAt[r.Region]].Holder,s.History):-1,
                    Source=c.Realms.ClaimRoutes.Sources[r.Region],EffortM=c.Realms.ClaimRoutes.EffortM[r.Region]}).ToArray();
            var habitat=w.Ecology.Habitat[cell];
            rows.Add(new{Region=region,Area=area,Missing=missing,Biome=habitat.Biome.ToString(),habitat.GrowingDays,Landmass=w.Landmass[cell],Cell=cell,
                Title=c.TitleAt[region],Polity=HistoricalCells.Polity(s,cell),Population=s.Population[region],Neighbors=neighbors});
        }
        File.WriteAllText(output,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Missing-region audit: "+rows.Count);
    }
    public static void Frames(string path,string folder,int[] years)
    {
        if(Directory.Exists(folder)||years.Length<1||years.Length>8)throw new ArgumentException("Use a new folder and at most eight explicit dates.");
        var w=WorldFile.Read(File.ReadAllBytes(path));var e=new Prehistory(w);var start=e.UpgradeRealmLifecycles(e.UpgradeGeographicKnowledge(w.Prehistory));var session=new SimulationSession(e,start);
        Directory.CreateDirectory(folder);var rows=new List<object>();
        foreach(int year in years.OrderBy(x=>x).Distinct())
        {
            if(year<session.LastSnapshot.Years||year>start.Years+30000)throw new ArgumentOutOfRangeException(nameof(years));
            while(session.LastSnapshot.Years<year){int step=Math.Min(250,year-session.LastSnapshot.Years);session.AdvanceSeconds(step*SimulationTime.Year,int.MaxValue);}
            var s=session.Snapshot();e.Validate(s);string output=Path.Combine(folder,year.ToString());Directory.CreateDirectory(output);
            HistoryAudit.Snapshot(w,e,s,output,new[]{HistoryOverlay.States},compressed:true,backgroundOpacity:0,overlayOpacity:.85,includeLabels:true,includeSettlements:false);
            var leaders=s.History.Polities.Where(p=>p.EndedYear<0&&HistoricalImperialHierarchy.Patron(s,p.Id)<0).OrderByDescending(p=>HistoricalImperialHierarchy.AreaKm2(s,p.Id)).Take(8).Select(p=>new{p.Id,p.Name,Area=HistoricalImperialHierarchy.AreaKm2(s,p.Id),Subjects=s.History.Aggregates.Empires.Governments[p.Id].SubjectCount}).ToArray();
            rows.Add(new{Year=year,s.Hash,Leaders=leaders});Console.WriteLine("Replayed screenshot "+year+" "+s.Hash);
        }
        File.WriteAllText(Path.Combine(folder,"frames.json"),JsonSerializer.Serialize(new{Source=path,StartHash=start.Hash,Rows=rows},new JsonSerializerOptions{WriteIndented=true}));
    }
    public static void Crisis(string path,string folder)
    {
        if(Directory.Exists(folder))throw new IOException("Use a new controlled-test output directory.");Directory.CreateDirectory(folder);
        var w=WorldFile.Read(File.ReadAllBytes(path));var e=new Prehistory(w);var baseState=e.UpgradeRealmLifecycles(e.UpgradeGeographicKnowledge(w.Prehistory));
        var h=baseState.History;var a=h.Aggregates;var roots=h.Polities.Where(p=>p.EndedYear<0&&HistoricalImperialHierarchy.Patron(baseState,p.Id)<0).OrderByDescending(p=>HistoricalImperialHierarchy.AreaKm2(baseState,p.Id)).ToArray();
        if(roots.Length<7)throw new ArgumentException("The controlled scenario needs seven real sovereign courts.");
        int parent=roots[0].Id,capital=baseState.Society.Settlements[roots[0].CapitalId].Cell;
        var clients=roots.Skip(1).OrderBy(p=>D.Angle(w.Surface.Cells[capital].Position,w.Surface.Cells[baseState.Society.Settlements[p.CapitalId].Cell].Position)).ThenBy(p=>p.Id).Take(6).Select(p=>p.Id).ToArray();
        var setter=typeof(Prehistory).GetMethod("SetImperialPatron",BindingFlags.Instance|BindingFlags.NonPublic);
        typeof(Prehistory).GetMethod("PrepareAggregateRelations",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(e,new object[]{baseState});
        long people=a.TotalPopulation;
        foreach(int client in clients)if(!(bool)setter.Invoke(e,new object[]{baseState,client,parent,true,"Controlled succession-mechanism test; these are existing courts, not generated new inhabitants."}))throw new Exception("Unable to prepare the controlled hierarchy.");
        baseState.Seal();e.Validate(baseState);if(a.TotalPopulation!=people)throw new Exception("Allegiance created people.");
        var rows=new List<object>();
        foreach(bool crisis in new[]{false,true})
        {
            var initial=baseState.Copy();var record=initial.History.Aggregates.Empires.Connections.Realms.Governments[parent];
            initial.History.Polities[parent].Cohesion=crisis?180:900;
            record.CrisisSeverity=crisis?950:0;record.CrisisUntil=crisis?initial.Years+30:0;initial.History.Dynamics.States[parent].DisputedUntil=record.CrisisUntil;
            initial.Seal();e.Validate(initial);var session=new SimulationSession(e,initial);
            for(int elapsed=0;elapsed<=30;elapsed+=5)
            {
                var s=session.LastSnapshot;int attached=clients.Count(id=>HistoricalImperialHierarchy.Sovereign(s,id)==parent);
                var row=new{Scenario=crisis?"contested":"continuing",Year=s.Years,Elapsed=elapsed,Parent=parent,ParentSurvives=s.History.Polities[parent].EndedYear<0,AttachedCourts=attached,EmpireArea=s.History.Polities[parent].EndedYear<0?HistoricalImperialHierarchy.AreaKm2(s,parent):0,Population=s.History.Aggregates.TotalPopulation};rows.Add(row);Console.WriteLine(JsonSerializer.Serialize(row));
                if(elapsed==0||elapsed==10||elapsed==30){string output=Path.Combine(folder,crisis?"contested":"continuing",s.Years.ToString());Directory.CreateDirectory(output);HistoryAudit.Snapshot(w,e,s,output,new[]{HistoryOverlay.States},compressed:true,backgroundOpacity:0,overlayOpacity:.85,includeLabels:true,includeSettlements:false);}
                if(elapsed==30){e.Validate(s);break;}session.AdvanceSeconds(5*SimulationTime.Year,int.MaxValue);
            }
        }
        File.WriteAllText(Path.Combine(folder,"controlled-crisis.json"),JsonSerializer.Serialize(new{Input=path,Parent=parent,Clients=clients,Explanation="Controlled stress case, not evidence of natural frequency: real existing courts share a recent imperial allegiance; one branch receives a disputed low-cohesion succession, the other does not.",Rows=rows},new JsonSerializerOptions{WriteIndented=true}));
    }
}
