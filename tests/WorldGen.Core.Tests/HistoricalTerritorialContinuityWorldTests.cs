using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Offline actor queries and cold continuation: no counterfactual politics
// handler, sovereignty write or RNG call is used by the query checks.
public static class HistoricalTerritorialContinuityWorldTests
{
    static object Call(Prehistory engine,string method,params object[] args)=>typeof(Prehistory).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args)!;
    public static void ColdUpgrade(string file,int previous,int revision,string expectedLegacy,string legacyOutput,Action<bool,string> check)
    {
        if(expectedLegacy!=null&&(expectedLegacy.Length!=64||expectedLegacy.Any(c=>!Uri.IsHexDigit(c))))throw new ArgumentException("Expected legacy history hash must be exactly64 hexadecimal characters.",nameof(expectedLegacy));
        var world=WorldFile.Read(File.ReadAllBytes(file));var source=world.Prehistory;var engine=new Prehistory(world);engine.Validate(source);string original=source.ComputeHash();
        check(source.History.Aggregates.Empires.Connections.Realms.Version==previous,"The cold-upgrade input retains its genuine preceding saved rule marker.");
        check(new HistoricalRealmLifecycles().Version==revision,"The driver uses the coherent activated target assembly, not a manually forced future marker.");
        if(File.Exists(legacyOutput)||File.Exists(legacyOutput+".cold.hash"))throw new IOException("Do not overwrite literal or cold continuation evidence.");
        if(expectedLegacy!=null)
        {
            var old=engine.AdvanceYears(source,12);engine.Validate(old);File.WriteAllText(legacyOutput,old.Hash);
            check(old.Hash==expectedLegacy,"The activated source exactly preserves the frozen preceding-rule twelve-year continuation on the identical actual checkpoint.");
            Console.WriteLine("Literal"+previous+" +12 "+old.Hash);
        }
        else Console.WriteLine("No duplicate preceding-rule control: this cold-only fixture uses a separately identified source, not another checkpoint's legacy hash.");
        var next=engine.UpgradeRealmLifecycles(source);engine.Validate(next);
        check(next.History.Aggregates.Empires.Connections.Realms.Version==revision,"The public explicit upgrade opts into the target revision without changing the source marker.");
        check(next.History.Aggregates.TotalPopulation==source.History.Aggregates.TotalPopulation&&next.Population.SequenceEqual(source.Population)&&next.Society.Settlements.Select(t=>t.Population).SequenceEqual(source.Society.Settlements.Select(t=>t.Population)),"The prospective upgrade conserves world population, every unallocated regional cohort and every town's existing residents.");
        check(next.Society.Settlements.Select(t=>t.FoodReserve).SequenceEqual(source.Society.Settlements.Select(t=>t.FoodReserve))&&next.History.Polities.Select(p=>p.Treasury).SequenceEqual(source.History.Polities.Select(p=>p.Treasury)),"The prospective upgrade grants no food or money: every existing town stock and court treasury is unchanged.");
        check(next.History.Cultures.Select(c=>StateHash.Compute(c.Technical)).SequenceEqual(source.History.Cultures.Select(c=>StateHash.Compute(c.Technical))),"The rule upgrade does not grant a technical rank, lesson, research credit, deployment or altered technical bank.");
        Console.WriteLine("Upgrade conservation passed "+previous+"->"+revision);
        world.Prehistory=next;var restored=WorldFile.Read(WorldFile.Write(world));var coldEngine=new Prehistory(restored);coldEngine.Validate(restored.Prehistory);
        check(restored.Prehistory.Hash==next.Hash,"A genuine full-world initial cold load retains the exact upgraded checkpoint.");
        var full=engine.AdvanceYears(next,12);var five=coldEngine.AdvanceYears(restored.Prehistory,5);restored.Prehistory=five;
        var middle=WorldFile.Read(WorldFile.Write(restored));var other=new Prehistory(middle);var split=other.AdvanceYears(middle.Prehistory,7);engine.Validate(full);other.Validate(split);
        check(middle.Prehistory.Hash==five.Hash&&split.Hash==full.Hash,"Twelve target-rule years exactly equal five years, a full intermediate cold world save/load, and seven further years; both final material accounts validate.");
        check(source.ComputeHash()==original&&source.History.Aggregates.Empires.Connections.Realms.Version==previous,"Every new-rule continuation and cold save preserves the original preceding-rule checkpoint and marker.");
        var fresh=engine.Start(engine.Prepare(source.Origin));check(fresh.History.Aggregates.Empires.Connections.Realms.Version==revision,"Fresh ordinary setup uses the activated target revision.");
        File.WriteAllText(legacyOutput+".cold.hash",full.Hash);Console.WriteLine("Genuine"+revision+" cold12==5+save/load+7 "+full.Hash);
    }
    public static void World(string file,string evidence,string legacyOutput,Action<bool,string> check,bool cold=true)
    {
        var world=WorldFile.Read(File.ReadAllBytes(file));var source=world.Prehistory;var engine=new Prehistory(world);engine.Validate(source);
        string original=source.ComputeHash();
        check(source.History.Aggregates.Empires.Connections.Realms.Version==22,"The compatibility source is a genuinely saved22 history, not a forced new-rule checkpoint.");
        using(var json=JsonDocument.Parse(File.ReadAllText(evidence)))
        {
            var root=json.RootElement;
            check(root.GetProperty("Hash").GetString()==original&&root.GetProperty("Year").GetInt32()==source.Years,"Independent actual-actor evidence names this precise saved world and date.");
            var probe=source.Copy();var a=probe.History.Aggregates;long reads=a.CellReads;
            Call(engine,"PrepareAggregateEnvironment",probe);Call(engine,"PrepareAggregateRelations",probe);
            foreach(int city in new[]{2864,4871,1495,2163,2877,3987,6181})
            {
                var row=root.GetProperty("Towns").EnumerateArray().First(r=>r.GetProperty("Town").GetInt32()==city);
                var town=probe.Society.Settlements[city];int owner=a.Resolve(town.PolityId,probe.History);var ruler=probe.History.Polities[owner];
                var account=a.Polities[owner];var policy=a.Communities.Governments[owner];var ds=probe.History.Dynamics.States[owner];
                int capital=probe.Society.Settlements[ruler.CapitalId].Cell,node=a.Empires.Territories.Sources[town.Cell];
                double bureaucracy=(double)Call(engine,"RealmBureaucracy",probe,owner);
                double distance=(double)Call(engine,"LivingTravelKm",probe,capital,town.Cell,ruler.CultureId)/Math.Max(50,HistoricalProvincialEffects.LocalServiceKm(probe.History.Cultures[ruler.CultureId],probe.Years,ds.PublicWorks));
                bool nodeLive=(bool)Call(engine,"TerritorialNodeLive",probe,owner,node),route=(bool)Call(engine,"InstitutionalRouteAllowed",probe,capital,town.Cell,ruler.CultureId);
                double demands=probe.History.Towns[city].AutonomyPressure/1000.0;
                check(owner==row.GetProperty("Owner").GetInt32()&&account.Receipts==row.GetProperty("Receipts").GetInt64()&&account.ServiceCost==row.GetProperty("ServiceCost").GetInt64()&&nodeLive==row.GetProperty("NodeLive").GetBoolean()&&route==row.GetProperty("PhysicalRulerRoute").GetBoolean()&&distance==row.GetProperty("CorrectRulerCultureDistance").GetDouble(),"Actual town"+city+" retains its independently observed government, financing, node and ruler-route burden.");
                a.Empires.Connections.Realms.Version=22;
                check((double)Call(engine,"CommunityTerritorialSecessionPressure",probe,owner,city,demands,distance)==demands,"The private caller leaves actual town"+city+" literally unchanged under22.");
                a.Empires.Connections.Realms.Version=23;
                double actual=(double)Call(engine,"CommunityTerritorialSecessionPressure",probe,owner,city,demands,distance);
                double expected=HistoricalTerritorialContinuityEffects.SecessionPressure(demands,bureaucracy,a.Communities.Cities[city].InclusionMilli,policy.RepresentationMilli,account.Receipts,account.ServiceCost,distance,nodeLive&&route);
                check(actual==expected&&Math.Abs(actual-row.GetProperty("Counterfactual23SecessionPressure").GetDouble())<1e-14,"The guarded23 private caller agrees with the independent current-pressure counterfactual at town"+city+".");
                if(city==6181)check(a.Communities.Cities[city].InclusionMilli==125&&actual>.9&&actual<demands,"Actual excluded town6181 retains high territorial pressure despite its ruler's modern administration; institutional capacity is not blanket immunity.");
                else check(city==2864||city==4871||city==1495?actual<.35&&demands>.35:actual==demands&&!(nodeLive&&route),"Actual financed served towns can retain autonomy demands without automatic border separation; actual unserved towns keep their preceding pressure.");
                Console.WriteLine(JsonSerializer.Serialize(new{Town=city,Owner=owner,Demands=demands,SecessionPressure=actual,account.Receipts,account.ServiceCost,Node=node,NodeLive=nodeLive,Route=route,Distance=distance,Bureaucracy=bureaucracy}));
            }
            a.Empires.Connections.Realms.Version=22;
            FirstTownProbe(engine,world,probe,check);
            SuccessorQuery(engine,probe,root,check);
            a.CellReads=reads;
            check(probe.ComputeHash()==original&&source.ComputeHash()==original,"Actual actor queries change no food, money, population, pressure, country, event or authoritative source/probe field; diagnostic reads are restored.");
        }
        if(File.Exists(legacyOutput))throw new IOException("Use a new literal continuation control path.");
        var twelveOld=engine.AdvanceYears(source,12);engine.Validate(twelveOld);File.WriteAllText(legacyOutput,twelveOld.Hash);
        check(twelveOld.Hash=="dc558fd2554a8e32a5edf68d25b64e0e8a593ca71fad93c209600daab8afe489","Current source preserves the literal frozen W22 twelve-year continuation on this named Earth fixture.");
        check(source.ComputeHash()==original,"The literal22 continuation preserves its original checkpoint.");
        Console.WriteLine("Literal22 +12 "+twelveOld.Hash);
        if(!cold)return;
        check(new HistoricalRealmLifecycles().Version==23,"Genuine23 cold acceptance requires the activated coherent23 assembly.");
        var next=engine.UpgradeRealmLifecycles(source);engine.Validate(next);
        check(next.History.Aggregates.Empires.Connections.Realms.Version==23&&next.History.Aggregates.TotalPopulation==source.History.Aggregates.TotalPopulation&&next.Society.Settlements.Select(t=>t.FoodReserve).SequenceEqual(source.Society.Settlements.Select(t=>t.FoodReserve))&&next.History.Polities.Select(p=>p.Treasury).SequenceEqual(source.History.Polities.Select(p=>p.Treasury)),"The explicit23 rule upgrade conserves every existing person, town food stock and treasury; no continuity subsidy is paid.");
        check(next.History.Cultures.Select(c=>StateHash.Compute(c.Technical)).SequenceEqual(source.History.Cultures.Select(c=>StateHash.Compute(c.Technical))),"The upgrade credits no experiments or lessons and grants no technical rank before a real subsequent review.");
        world.Prehistory=next;var restored=WorldFile.Read(WorldFile.Write(world));var coldEngine=new Prehistory(restored);coldEngine.Validate(restored.Prehistory);
        check(restored.Prehistory.Hash==next.Hash,"A genuine full-world cold load retains the exact activated23 checkpoint.");
        var full=engine.AdvanceYears(next,12);var five=coldEngine.AdvanceYears(restored.Prehistory,5);restored.Prehistory=five;
        var middle=WorldFile.Read(WorldFile.Write(restored));var other=new Prehistory(middle);var split=other.AdvanceYears(middle.Prehistory,7);engine.Validate(full);other.Validate(split);
        check(middle.Prehistory.Hash==five.Hash&&split.Hash==full.Hash,"Actual23 twelve years exactly equal five years, a genuine intermediate full-world cold save/reload, and seven further years; both states validate all material accounts.");
        check(source.ComputeHash()==original,"All23 continuations and full-world cold saves preserve the original22 checkpoint.");
        var fresh=engine.Start(engine.Prepare(source.Origin));check(fresh.History.Aggregates.Empires.Connections.Realms.Version==23,"Fresh current setup opts into23 without upgrading the preserved original save.");
        Console.WriteLine("Genuine23 cold12==5+save/load+7 "+full.Hash);
    }
    static void SuccessorQuery(Prehistory engine,PrehistoryState s,JsonElement evidence,Action<bool,string> check)
    {
        var a=s.History.Aggregates;int liveOptions=0;
        foreach(int city in new[]{740,918,959})
        {
            var town=s.Society.Settlements[city];int owner=a.Resolve(town.PolityId,s.History);var rows=new List<object>();
            var observed=evidence.GetProperty("Towns").EnumerateArray().First(r=>r.GetProperty("Town").GetInt32()==city);
            foreach(int peer in a.Cities[city].Neighbors)
            {
                var candidate=s.Society.Settlements[peer];int state=a.Resolve(candidate.PolityId,s.History);
                if(state<0||state==owner||s.History.Polities[state].ParentId!=owner||s.History.Polities[state].CultureId!=town.CultureId||s.Years-s.History.Polities[state].FoundedYear>50)continue;
                double km=(double)Call(engine,"LivingTravelKm",s,candidate.Cell,town.Cell,town.CultureId);
                bool route=(bool)Call(engine,"InstitutionalRouteAllowed",s,candidate.Cell,town.Cell,town.CultureId),live=candidate.Population>0&&a.Living.Cities[peer].AbandonedYear<0;
                bool eligible=km<Math.Max(180,a.Polities[state].ReachKm*2.0)&&route;
                if(eligible)
                {
                    check(observed.GetProperty("CurrentEligibleSiblingGovernments").EnumerateArray().Any(r=>r.GetProperty("Court").GetInt32()==state&&r.GetProperty("Town").GetInt32()==peer&&r.GetProperty("Km").GetDouble()==km),"Bounded actual sibling ownership, shared parent, age, culture and route agree with the independent snapshot at town"+city+".");
                    if(live)liveOptions++;
                }
                rows.Add(new{Town=peer,Court=state,LiveTown=live,Route=route,Km=km,Eligible=eligible&&live});
            }
            Console.WriteLine(JsonSerializer.Serialize(new{Probe="ActualBoundedSuccessorOptions",Town=city,Owner=owner,NeighborsExamined=a.Cities[city].Neighbors.Length,Candidates=rows}));
        }
        check(liveOptions>0,"Actual22 records contain geographically reachable living same-cultural recent successor governments; this query transfers no ownership and does not invoke the joining handler.");
    }
    static void FirstTownProbe(Prehistory engine,World world,PrehistoryState s,Action<bool,string> check)
    {
        const int culture=461;var c=s.Society.Cultures[culture];var a=s.History.Aggregates;var hc=s.History.Cultures[culture];
        int chosen=(int)Call(engine,"DevelopmentEconomicSite",s,culture,c.OriginCell);
        var candidates=new[]{c.OriginCell,chosen,139102}.Concat(s.History.Agriculture[culture].PracticeCell).Distinct().ToArray();
        var rows=new List<object>();
        foreach(int cell in candidates)
        {
            int region=engine.CellPatch[cell];if(region<0)continue;
            double yield=(double)Call(engine,"FoundingFieldLandscape",s,culture,cell,-1),stored=(double)Call(engine,"CarriedCropLandscape",s,culture,cell,true,-1,-1);
            int practice=(int)Call(engine,"CarriedCropPractice",s,culture,cell,-1);
            double environment=(double)Call(engine,"EnvironmentalEstablishment",s,cell,culture),rail=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Railways,s.Years);
            double spacing=(100-45*rail)*Math.Sqrt(Math.Min(4,Math.Max(1,.25/Math.Max(.02,yield))));
            int bucket=(int)typeof(Prehistory).GetMethod("LivingTownBucket",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{world.Surface.Cells[cell].Position})!;
            rows.Add(new{Cell=cell,Region=region,Latitude=Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI,Longitude=Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI,
                CanFound=(bool)Call(engine,"CanFoundLivingTown",s,culture,cell,-1),FieldYield=yield,Practice=practice,Stored=stored,Environment=environment,Institution=hc.Institution,
                ProtocentreMaximum=HistoricalProtocentreEffects.MaximumResidents(environment,stored,hc.Institution,0),RegionalRawPeople=s.Population[region],TownAt=a.TownAt[cell],Bucket=bucket,BucketCount=a.Living.BucketCounts[bucket],
                SpacingKm=spacing,Nearby=(int[])Call(engine,"NearbyLivingTowns",s,cell,spacing),CommittedArea=a.Living.Regions[region].CommittedAreaKm2,RegionArea=a.Regions[region].AreaKm2});
        }
        check(a.Cultures[culture].Cities.Count==0,"The actual culture461 first-town diagnostic observes a currently townless culture; it never invokes a founding or opportunity handler.");
        Console.WriteLine(JsonSerializer.Serialize(new{Probe="Culture461FirstTown",OriginCell=c.OriginCell,ChosenCell=chosen,c.Farming,ActualCities=a.Cultures[culture].Cities.Count,PracticeCells=s.History.Agriculture[culture].PracticeCell,Queries=rows}));
    }
}
