using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HistoryGeographicKnowledgeTests
{
    public static void Run(Action<bool,string> check)
    {
        var map=new HistoricalGeographicMap(20);
        check(map.Grade[7]<0&&map.Places.Count==0,"An empty state map knows no foreign geography.");
        check(map.Record(7,1,100,0,-1,42)&&map.Places.Count==1,"Hearsay is a persistent indexed report, not a working sailing route.");
        check(!HistoricalSeaVentureEffects.CanColonize(map.Grade[7],true,5000000),"Even advanced ships cannot found a targeted colony on a mere rumor.");
        check(map.Record(7,2,110,1400,3,42),"A dated coastal account improves the report without declaring ownership.");
        check(HistoricalSeaVentureEffects.CanColonize(map.Grade[7],true,5000000),"Copied observed sailing directions can support a later separately funded venture.");
        check(!HistoricalSeaVentureEffects.CanColonize(3,false,701000)&&HistoricalSeaVentureEffects.CanColonize(3,false,700000),"Verified geography does not give an early coastal fleet blue-water reach.");
        check(map.Record(7,3,120,1600,3,42)&&map.Places.Count==1,"A verified returning survey upgrades the same durable region entry.");
        check(!map.Record(7,1,130,0,-1,4)&&map.Grade[7]==3&&map.ReportedYear[7]==120&&map.Shore[7]==42,"Later hearsay cannot erase a better first-hand navigational report.");
        check(!map.Record(7,3,110,2000,4,6)&&map.Opportunity[7]==1600,"Stale reports do not change current dated observations.");
        map.Missions=new[]{new HistoricalSeaMission{Kind=0,Source=1,Target=42,Region=7,Due=123456}};
        var copy=map.Copy();copy.Record(8,1,120,0,-1,55);copy.Missions[0].Due++;
        check(map.Grade[8]<0&&map.Places.Count==1&&map.Missions[0].Due==123456,"Published geographical trees and in-flight missions are copy-on-write/deep-copied.");
        long sail=HistoricalSeaVentureEffects.Duration(6000000,true,1,0,0,.5),steam=HistoricalSeaVentureEffects.Duration(6000000,true,1,1,0,.5);
        check(sail>SimulationTime.Day*150&&sail<SimulationTime.Year&&steam<sail,"A 6,000km survey takes realistic-order months, not independently stacked decades; engines shorten the route.");
        long convoy=HistoricalSeaVentureEffects.Duration(6000000,false,1,0,0,.5);
        check(convoy>=SimulationTime.Day*150&&convoy<SimulationTime.Year,"A paid convoy has a preparation/travel arrival date, not an instant teleport or 40-year founding lottery.");
        check(HistoricalSeaVentureEffects.Fleet(1000000,10000,0)==4&&HistoricalSeaVentureEffects.Fleet(1000000,0,0)==0&&HistoricalSeaVentureEffects.Fleet(1000000,10000,800)==0,"Fiscal surplus, urban scale and exhaustion constrain the bounded expedition fleet.");
        check(StateHash.Compute(new HistoricalColonialConnections())=="3e6f409c4cdc77e11d2119ec182e566258a65f5eaa5265d0ebc2bd8ea6d094ea","Null geography preserves the exact existing colonial record's canonical bytes.");
        var pending=new HistoricalPendingPortGovernment();for(int k=0;k<89;k++)pending.Pending.Add(k);
        var visited=new HashSet<int>();for(int k=0;k<89;k++)visited.Add(pending.TakeOne());
        check(visited.Count==89&&pending.Cursor==0,"Every vacant office gets reconsidered even when voyage counts never advance; sampling cannot permanently starve 87 of 89 offices.");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var original=w.Prehistory;var engine=new Prehistory(w);engine.Validate(original);string hash=original.Hash;
        var s=engine.UpgradeGeographicKnowledge(original);engine.Validate(s);var a=s.History.Aggregates;
        check(original.History.Aggregates.Empires.Connections.Geography==null,"The source fixture keeps its original null-policy rules.");
        check(s.History.Aggregates.TotalPopulation==original.History.Aggregates.TotalPopulation,"Map initialization creates no residents, crops, ports or technology.");
        check(s.History.Aggregates.Empires.Connections.Geography.Governments.All(m=>m.Missions.Length==0&&m.Places.Count<=21),"Explicit upgrade seeds only bounded actual owned coastal nodes, never global cultural charts.");
        check(ReferenceEquals(engine.UpgradeGeographicKnowledge(s),s),"The explicit map upgrade is idempotent.");
        var v1=s.Copy();v1.History.Aggregates.Empires.Connections.Geography.Version=1;v1.Seal();engine.Validate(v1);
        var updated=engine.UpgradeGeographicKnowledge(v1);
        check(v1.History.Aggregates.Empires.Connections.Geography.Version==1&&updated.History.Aggregates.Empires.Connections.Geography.Version==2&&updated.History.Aggregates.TotalPopulation==v1.History.Aggregates.TotalPopulation,"Version1 remains loadable and its fair-cursor revision is an explicit non-mutating upgrade, not a silent rules change.");
        w.Prehistory=s;var restored=WorldFile.Read(WorldFile.Write(w)).Prehistory;engine.Validate(restored);check(restored.Hash==s.Hash,"Geographical maps survive validated save/load with exact integrity.");
        var one=engine.AdvanceYears(s,20);var pieces=engine.AdvanceYears(engine.AdvanceYears(s,7),13);engine.Validate(one);
        check(one.Hash==pieces.Hash&&one.Hash==engine.AdvanceYears(restored,20).Hash,"Exact mission dates and outcomes are invariant under reload and 20 vs 7+13-year publication partitions.");
        var maps=one.History.Aggregates.Empires.Connections.Geography.Governments;
        check(maps.Sum(m=>m.Voyages)>0,"Actual supported states fund at least one real voyage in this world.");
        check(maps.Sum(m=>m.Surveys)>0,"At least one actual returning voyage completes and improves its state's map.");
        check(one.Timeline.Events.Where(e=>e.Process==27).All(e=>e.Due>one.Timeline.Seconds),"Outstanding sea events wake at future exact dates, not whole-year tick boundaries.");
        check(maps.All(m=>m.Missions.Count(x=>x.Kind!=HistoricalSeaMission.Report)<=4&&m.Missions.Length<=8),"Each actor's sea portfolio has a fixed computational bound regardless of world/country size.");
        var invalid=one.Copy();invalid.History.Aggregates.Empires.Connections.Geography.Version=99;invalid.Seal();bool rejected=false;try{engine.Validate(invalid);}catch(ArgumentException){rejected=true;}
        check(rejected&&one.History.Aggregates.Empires.Connections.Geography.Version==2,"Unknown rules fail validation and do not alias prior publications.");
        check(original.ComputeHash()==hash,"The original actual-world checkpoint remains unchanged.");
    }
    public static void Compare(string path,string folder,int years)
    {
        if(years<1||years>1000||Directory.Exists(folder))throw new ArgumentException("Use 1..1000 years and a new output folder.");
        var w=WorldFile.Read(File.ReadAllBytes(path));var source=w.Prehistory;var e=new Prehistory(w);e.Validate(source);
        if(source.History.Aggregates.Empires.Connections.Geography!=null)throw new ArgumentException("Comparison requires a null-geography control.");
        Directory.CreateDirectory(folder);int firstTown=source.Society.Settlements.Length,firstPost=source.History.Aggregates.Empires.Territories.Posts.Length,start=source.Years;
        object Summary(PrehistoryState s)
        {
            var h=s.History;var a=h.Aggregates;var maps=a.Empires.Connections.Geography?.Governments;
            // A real port may populate an already-paid old office. Restricting
            // the count to newly created posts hid the retry fix's actual towns.
            var newPorts=s.Society.Settlements.Where(t=>t.Id>=firstTown&&a.Empires.Territories.PostAt[t.Cell]>=0&&a.Empires.Territories.Posts[a.Empires.Territories.PostAt[t.Cell]].Maritime).ToArray();
            return new{Year=s.Years,Era=WorldEraView.Name(WorldEraView.Of(s)),Population=a.TotalPopulation,States=h.Polities.Count(p=>p.EndedYear<0),
                LiveTowns=s.Society.Settlements.Count(t=>a.Living.Cities[t.Id].AbandonedYear<0),NewPorts=newPorts.Length,LiveNewPorts=newPorts.Count(t=>a.Living.Cities[t.Id].AbandonedYear<0),NewPosts=a.Empires.Territories.Posts.Length-firstPost,
                LiveNewPosts=a.Empires.Territories.Posts.Count(p=>p.Id>=firstPost&&p.ClosedYear<0),
                Voyages=maps?.Sum(m=>m.Voyages),Surveys=maps?.Sum(m=>m.Surveys),Landfalls=maps?.Sum(m=>m.Landfalls),FailedVoyages=maps?.Sum(m=>m.FailedVoyages),KnownRegions=maps?.Sum(m=>m.Places.Count),
                NewPortResidents=newPorts.Sum(t=>t.Population+a.Cities[t.Id].Rural+a.Cities[t.Id].Suburban),Hash=s.Hash};
        }
        var rows=new List<object>();
        foreach(var item in new[]{("baseline",source),("candidate",e.UpgradeGeographicKnowledge(source))})
        {
            var reasons=new SortedDictionary<string,int>();e.SeaVentureObserver=(polity,reason)=>{string key=polity+":"+reason;reasons[key]=reasons.TryGetValue(key,out int n)?n+1:1;};
            var session=new SimulationSession(e,item.Item2);var watch=System.Diagnostics.Stopwatch.StartNew();
            for(int year=0;year<years;year+=10)
            {
                session.AdvanceSeconds(Math.Min(10,years-year)*SimulationTime.Year,int.MaxValue);var s=session.LastSnapshot;
                if((s.Years-start)%50==0||s.Years==start+years){var row=new{Run=item.Item1,ElapsedSeconds=watch.Elapsed.TotalSeconds,Summary=Summary(s)};rows.Add(row);Console.WriteLine(JsonSerializer.Serialize(row));}
                if(s.Years==start+50||s.Years==start+years)
                {string output=Path.Combine(folder,item.Item1,s.Years.ToString());Directory.CreateDirectory(output);HistoryAudit.Snapshot(w,e,s,output,new[]{HistoryOverlay.States,HistoryOverlay.Technology},compressed:true,backgroundOpacity:1,overlayOpacity:.65,includeLabels:false);}
            }
            w.Prehistory=session.Snapshot();e.Validate(w.Prehistory);File.WriteAllBytes(Path.Combine(folder,item.Item1+".world"),WorldFile.Write(w));
            File.WriteAllText(Path.Combine(folder,item.Item1+"-ventures.json"),JsonSerializer.Serialize(reasons,new JsonSerializerOptions{WriteIndented=true}));
            e.SeaVentureObserver=null;
        }
        File.WriteAllText(Path.Combine(folder,"comparison.json"),JsonSerializer.Serialize(new{Input=path,InputHash=source.Hash,Years=years,Rows=rows},new JsonSerializerOptions{WriteIndented=true}));
    }
    public static void Eras(string path,string folder,int maximumYears)
    {
        if(maximumYears<1000||maximumYears>60000||Directory.Exists(folder))throw new ArgumentException("Use a fresh output directory and 1000..60000-year safety bound.");
        Directory.CreateDirectory(folder);var w=WorldFile.Read(File.ReadAllBytes(path));var e=new Prehistory(w);var initial=e.UpgradeGeographicKnowledge(w.Prehistory);
        var session=new SimulationSession(e,initial);var captures=new HashSet<string>();var rows=new List<object>();var watch=System.Diagnostics.Stopwatch.StartNew();int early=-1;
        while(session.LastSnapshot.Years<initial.Years+maximumYears)
        {
            session.AdvanceSeconds(100*SimulationTime.Year,int.MaxValue);var s=session.LastSnapshot;var a=s.History.Aggregates;int era=WorldEraView.Of(s);
            if(era>=7&&early<0)early=s.Years;
            if(s.Years%500==initial.Years%500||s.Years==early||a.FirstMaximumTechnologyYear>=0&&s.Years>=a.FirstMaximumTechnologyYear+1000)
            {
                var maps=a.Empires.Connections.Geography.Governments;var row=new{Year=s.Years,SinceFirstState=s.Years-s.History.Polities.Min(p=>p.FoundedYear),Era=WorldEraView.Name(era),Population=a.TotalPopulation,
                    States=s.History.Polities.Count(p=>p.EndedYear<0),LiveTowns=a.Living.Cities.Count(t=>t.AbandonedYear<0),LivePorts=a.Empires.Connections.ProvisionedPorts.Ports.Count(p=>a.Living.Cities[p.TownId].AbandonedYear<0),
                    Voyages=maps.Sum(m=>m.Voyages),Surveys=maps.Sum(m=>m.Surveys),Landfalls=maps.Sum(m=>m.Landfalls),ActualMaxTechYear=a.FirstMaximumTechnologyYear,WallSeconds=watch.Elapsed.TotalSeconds};rows.Add(row);Console.WriteLine(JsonSerializer.Serialize(row));
            }
            string capture=s.Years==early?"early-modern":early>=0&&s.Years>=early+200&&!captures.Contains("early-modern+200")?"early-modern+200":era>=9&&!captures.Contains("modern")?"modern":
                a.FirstMaximumTechnologyYear>=0&&s.Years>=a.FirstMaximumTechnologyYear+1000?"maxtech+1000":"";
            if(capture!=""&&captures.Add(capture))
            {
                string output=Path.Combine(folder,capture,s.Years.ToString());Directory.CreateDirectory(output);
                HistoryAudit.Snapshot(w,e,s,output,new[]{HistoryOverlay.States,HistoryOverlay.Technology},compressed:true,backgroundOpacity:1,overlayOpacity:.65,includeLabels:false);
            }
            if(a.FirstMaximumTechnologyYear>=0&&s.Years>=a.FirstMaximumTechnologyYear+1000)break;
        }
        w.Prehistory=session.Snapshot();e.Validate(w.Prehistory);File.WriteAllBytes(Path.Combine(folder,"final.world"),WorldFile.Write(w));
        File.WriteAllText(Path.Combine(folder,"review.json"),JsonSerializer.Serialize(new{Input=path,InputHash=initial.Hash,Rows=rows},new JsonSerializerOptions{WriteIndented=true}));
    }
    public static void AuditPorts(string path,string output)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var s=w.Prehistory;var e=new Prehistory(w);e.Validate(s);var a=s.History.Aggregates;var rows=new List<object>();
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args)!;
        foreach(var p in s.History.Polities.Where(p=>p.EndedYear<0))
        {
            var departures=(int[])Call("LivingColonialDepartures",s,p.Id);var pending=a.Empires.Connections.PortSettlements.Governments[p.Id].Pending;
            for(int k=0;k<pending.Count;k++)
            {
                var post=a.Empires.Territories.Posts[pending.At(k)];
                foreach(int source in departures)
                {
                    if(source==post.Cell)continue;
                    var route=new Prehistory.Route{A=e.CellPatch[source],B=e.CellPatch[post.Cell],CellA=source,CellB=post.Cell,Sea=true,DistanceM=(int)Math.Round(D.Angle(w.Surface.Cells[source].Position,w.Surface.Cells[post.Cell].Position)*w.Recipe.RadiusKm*1000)};
                    object[] supply={s,p.Id,route,a.TownAt[source],-1,0L};if(!(bool)Call("MaritimeCharterLogistics",supply))continue;
                    int port=(int)supply[4];var town=s.Society.Settlements[port];object[] import={s,p.Id,route,port,null!};
                    double yield=(double)Call("LivingCropLandscape",s,town.CultureId,post.Cell,false);bool viable=(bool)Call("CanFoundLivingTown",s,town.CultureId,post.Cell,port),provisioned=(bool)Call("CanPrepareProvisionedPort",import);
                    rows.Add(new{Polity=p.Id,p.Name,Post=post.Id,Target=post.Cell,DistanceKm=route.DistanceM/1000,Source=port,SourceUrban=town.Population,Available=(long)Call("LivingFounderAvailability",s,port),Yield=yield,Normal=viable,Import=provisioned,Stock=town.FoodReserve,p.Treasury,Net=a.Polities[p.Id].Receipts-a.Polities[p.Id].ServiceCost,Works=s.History.Dynamics.States[p.Id].PublicWorks,
                        NeighborTowns=((int[])Call("NearbyLivingTowns",s,post.Cell,100d)).Length,Habitat=w.Ecology.Habitat[post.Cell]});break;
                }
            }
        }
        File.WriteAllText(output,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));Console.WriteLine("Audited "+rows.Count+" actually supplied vacant offices.");
    }
}
