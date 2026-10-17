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
    }
    public static void World(string path)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var e=new Prehistory(w);var source=w.Prehistory;string original=source.Hash;
        var s=e.UpgradeRealmLifecycles(source);e.Validate(s);int count=0;
        void Check(bool ok,string message){count++;if(!ok)throw new Exception(message);}
        Check(source.History.Aggregates.Empires.Connections.Realms==null,"Source retains old rules.");
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
        var country=s.History.Polities.FirstOrDefault(x=>x.EndedYear<0&&s.History.Aggregates.Polities[x.Id].Cities.Count>=2);
        if(country!=null)
        {
            var probe=s.Copy();var a=probe.History.Aggregates;int city=a.Polities[country.Id].Cities.At(0);if(city==country.CapitalId)city=a.Polities[country.Id].Cities.At(1);
            long funds=probe.History.Polities.Sum(x=>x.Treasury),people=a.TotalPopulation;int states=probe.History.Polities.Length;
            var queue=new SimulationEventQueue(probe.Timeline.Events);var queueField=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic);queueField.SetValue(e,queue);
            try{typeof(Prehistory).GetMethod("FoundAggregateState",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(e,new object[]{probe,city,country.Id});}
            finally{queueField.SetValue(e,null);}
            probe.Timeline.Events=queue.Ordered();probe.Seal();e.Validate(probe);
            Check(probe.History.Polities.Length==states+1&&probe.History.Polities.Sum(x=>x.Treasury)==funds&&a.TotalPopulation==people,"A controlled real-center separation divides existing public funds and preserves every person.");
        }
        Console.WriteLine("PASS "+count+" realm actual-world assertions");
    }
    public static void Review(string path,string folder,int years,bool candidate)
    {
        if(Directory.Exists(folder)||years<1||years>30000)throw new ArgumentException("Use a new folder and 1..30000 years.");
        Directory.CreateDirectory(folder);var w=WorldFile.Read(File.ReadAllBytes(path));var e=new Prehistory(w);var initial=candidate?e.UpgradeRealmLifecycles(e.UpgradeGeographicKnowledge(w.Prehistory)):w.Prehistory;
        var session=new SimulationSession(e,initial);var rows=new List<object>();int start=initial.Years;var watch=System.Diagnostics.Stopwatch.StartNew();int captures=0;
        var firstArea=initial.History.Polities.Where(p=>p.EndedYear<0).ToDictionary(p=>p.Id,p=>HistoricalImperialHierarchy.AreaKm2(initial,p.Id));
        int archive=initial.History.Archive.Count;
        while(session.LastSnapshot.Years<start+years)
        {
            session.AdvanceSeconds(Math.Min(25,start+years-session.LastSnapshot.Years)*SimulationTime.Year,int.MaxValue);var s=session.LastSnapshot;var h=s.History;var a=h.Aggregates;
            var states=h.Polities.Where(p=>p.EndedYear<0).ToArray();var roots=states.Where(p=>HistoricalImperialHierarchy.Patron(s,p.Id)<0).ToArray();
            var leaders=roots.OrderByDescending(p=>HistoricalImperialHierarchy.AreaKm2(s,p.Id)).Take(8).Select(p=>new{p.Id,p.Name,Age=s.Years-HistoricalRealmLifecycles.Founded(s,p.Id),Area=HistoricalImperialHierarchy.AreaKm2(s,p.Id),Population=HistoricalImperialHierarchy.Population(s,p.Id),Subjects=a.Empires.Governments[p.Id].SubjectCount,Debt=h.Dynamics.States[p.Id].Cycle.ConquestDebt,p.Cohesion,Strain=h.Dynamics.States[p.Id].Strain,Reach=a.Polities[p.Id].ReachKm,Representation=a.Communities.Governments[p.Id].RepresentationMilli}).ToArray();
            var events=h.Archive.Skip(archive).ToArray();
            var row=new{Year=s.Years,Era=WorldEraView.Name(WorldEraView.Of(s)),MaximumTechnologyYear=a.FirstMaximumTechnologyYear,Seconds=watch.Elapsed.TotalSeconds,Population=a.TotalPopulation,States=states.Length,Sovereigns=roots.Length,Crises=a.Empires.Connections.Realms?.Governments.Count(g=>g.CrisisUntil>s.Years),Events=events.GroupBy(x=>x.Kind).OrderBy(g=>g.Key).ToDictionary(g=>g.Key,g=>g.Count()),Leaders=leaders};
            rows.Add(row);if((s.Years-start)%100==0||s.Years==start+years)Console.WriteLine(JsonSerializer.Serialize(row));
            bool contraction=roots.Any(p=>firstArea.TryGetValue(p.Id,out long prior)&&prior>1000000&&HistoricalImperialHierarchy.AreaKm2(s,p.Id)<prior*.5);
            bool finished=s.Years==start+years||initial.History.Aggregates.FirstMaximumTechnologyYear<0&&a.FirstMaximumTechnologyYear>=0&&s.Years>=a.FirstMaximumTechnologyYear+1000;
            if(finished||captures<8&&(s.Years==start+25||(s.Years-start)%Math.Max(100,years/5/25*25)==0||contraction))
            {string output=Path.Combine(folder,s.Years.ToString());Directory.CreateDirectory(output);HistoryAudit.Snapshot(w,e,s,output,new[]{HistoryOverlay.States,HistoryOverlay.Technology},compressed:true,backgroundOpacity:1,overlayOpacity:.75,includeLabels:false);captures++;}
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
        File.WriteAllText(Path.Combine(folder,"review.json"),JsonSerializer.Serialize(new{Input=path,InputHash=initial.Hash,Candidate=candidate,Rows=rows},new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(folder,"events.json"),JsonSerializer.Serialize(w.Prehistory.History.Archive.Skip(archive),new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
    }
    public static void Diagnostic(string path,string output)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var e=new Prehistory(w);var initial=w.Prehistory;var reasons=new SortedDictionary<string,int>();
        e.SeaVentureObserver=(polity,reason)=>{string key=reason;reasons[key]=reasons.TryGetValue(key,out int n)?n+1:1;};
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
}
