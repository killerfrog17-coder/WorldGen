using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

// Explicit offline observation only. No preparation changes an existing save,
// and none of these censuses run inside an authoritative actor event.
public static class HistoryColonialThroughputAudit
{
    public static void Run(string path,string output,int replayStart=-1,int years=0,int realm=-1)
    {
        if(File.Exists(output)||years<0||years>300)throw new ArgumentException("Use a new audit file and a0..300year observation window.");
        var w=WorldFile.Read(File.ReadAllBytes(path));var original=w.Prehistory;string inputHash=original.Hash;long originalReads=original.History.Aggregates.CellReads;var e=new Prehistory(w);PrehistoryState s=original;
        if(replayStart>=0)
        {
            w.Prehistory=null;e=new Prehistory(w);s=e.Start(e.Prepare());
            // An explicit DIAGNOSTIC choice before the first elapsed second.
            // Never downgrade/upgrade the loaded historical world itself.
            if(realm>=0){s.History.Aggregates.Empires.Connections.Realms.Version=realm;s.Seal();e.Validate(s);}
            var replay=new SimulationSession(e,s);replay.AdvanceSeconds(replayStart*SimulationTime.Year,int.MaxValue);s=replay.Snapshot();
        }
        e.Validate(s);int startYear=s.Years;string startHash=s.Hash;
        var reasons=new SortedDictionary<string,int>();var polityReasons=new SortedDictionary<string,int>();var observed=new List<object>();var arrivals=new List<object>();var newEvents=new List<object>();
        var missions=new Dictionary<string,HistoricalSeaMission>();var missionPolity=new Dictionary<string,int>();var missing=new HashSet<string>();
        string Key(int polity,HistoricalSeaMission m)=>polity+":"+m.Kind+":"+m.Departed+":"+m.Due+":"+m.Source+":"+m.Target+":"+m.Region;
        void ObserveMissions(PrehistoryState state,bool initial)
        {
            var pending=new HashSet<string>();var maps=state.History.Aggregates.Empires.Connections.Geography.Governments;
            for(int polity=0;polity<maps.Length;polity++)foreach(var m in maps[polity].Missions)
            {
                string key=Key(polity,m);pending.Add(key);if(missions.ContainsKey(key))continue;missions[key]=m.Copy();missionPolity[key]=polity;
                if(!initial&&observed.Count<20000)
                {
                    var history=state.History;var aggregate=history.Aggregates;var ruler=history.Polities[polity];int owner=HistoricalCells.Polity(state,m.Target),sourceOwner=HistoricalCells.Polity(state,m.Source),town=aggregate.TownAt[m.Target],post=aggregate.Empires.Territories.PostAt[m.Target];
                    string gap=m.Kind!=HistoricalSeaMission.Convoy?"not-convoy":owner>=0&&owner!=polity&&ruler.Cohesion<600?"foreign-state-cohesion600":owner<0&&town>=0&&ruler.Cohesion<550?"independent-town-cohesion550":owner<0&&town<0&&ruler.Cohesion<550?"new-office-cohesion550":owner==polity&&town<0&&ruler.Cohesion<500?"own-port-cohesion500":"no-simple-cohesion-gap";
                    int local=aggregate.TownAt[m.Source];var actorArgs=new object[]{state,polity,m.Source,-1,-1,0L,0L};bool actor=(bool)Call("SeaServiceActor",actorArgs);int actorKey=(int)actorArgs[3],actorTown=(int)actorArgs[4],record=actor?aggregate.Empires.Connections.SeaService.BySource[actorKey]:-1;var craft=record>=0?aggregate.Empires.Connections.SeaService.Ports[record]:null;
                    observed.Add(new{Id=key,Polity=polity,Kind=m.Kind,Source=m.Source,Target=m.Target,m.Region,DistanceKm=m.DistanceM/1000.0,DepartedYear=m.Departed/(double)SimulationTime.Year,DueYear=m.Due/(double)SimulationTime.Year,Days=(m.Due-m.Departed)/(double)SimulationTime.Day,m.Investment,m.Reporter,m.Grade,m.ReportedYear,FirstObservedYear=SimulationTime.Seconds(state)/(double)SimulationTime.Year,SourceOwnerAtObservation=sourceOwner,TargetOwnerAtObservation=owner,TargetTownAtObservation=town,TargetPostAtObservation=post,RememberedHost=maps[polity].Host[m.Region],RememberedGrade=maps[polity].Grade[m.Region],ruler.Cohesion,ruler.Exhaustion,ruler.Army,OwnPoliticalGap=gap,DepartureTown=local,DepartureTownResidents=local>=0?state.Society.Settlements[local].Population:0,DepartureHouseholds=local>=0?(long)Call("LivingFounderAvailability",state,local):0,ActorKey=actorKey,ActorTown=actorTown,ActorCapacity=craft?.Capacity??0,ActorCargoDayCredit=craft?.CargoDayCredit??0,ActorLastYear=(craft?.LastSeconds??0)/(double)SimulationTime.Year,ActorWorkSpent=craft?.WorkSpent??0});
                }
            }
            foreach(var item in missions)
                if(!missing.Contains(item.Key)&&!pending.Contains(item.Key))
                {missing.Add(item.Key);if(arrivals.Count<20000){int polity=missionPolity[item.Key],target=item.Value.Target;arrivals.Add(new{Id=item.Key,Polity=polity,Kind=item.Value.Kind,Source=item.Value.Source,Target=target,item.Value.Region,DueYear=item.Value.Due/(double)SimulationTime.Year,ObservedGoneYear=SimulationTime.Seconds(state)/(double)SimulationTime.Year,DueReached=item.Value.Due<=SimulationTime.Seconds(state),SourceEndedYear=state.History.Polities[polity].EndedYear,TargetOwnerAtObservation=HistoricalCells.Polity(state,target),TargetTownAtObservation=state.History.Aggregates.TownAt[target],TargetPostAtObservation=state.History.Aggregates.Empires.Territories.PostAt[target]});}}
        }
        ObserveMissions(s,true);int archiveAt=s.History.Archive.Count;
        if(years>0)
        {
            e.SeaVentureObserver=(polity,reason)=>{reasons[reason]=reasons.TryGetValue(reason,out int count)?count+1:1;string key=polity+":"+reason;polityReasons[key]=polityReasons.TryGetValue(key,out int number)?number+1:1;};
            var session=new SimulationSession(e,s);long target=SimulationTime.Seconds(s)+years*SimulationTime.Year,step=SimulationTime.Year/4;
            while(session.Seconds<target)
            {
                session.AdvanceSeconds(Math.Min(step,target-session.Seconds),int.MaxValue);s=session.LastSnapshot;ObserveMissions(s,false);
                for(int i=archiveAt;i<s.History.Archive.Count;i++)
                {var item=s.History.Archive[i];if(newEvents.Count<20000&&(item.Kind.Contains("port")||item.Kind.Contains("charter")||item.Kind=="survey-return"||item.Kind.Contains("colonial")))newEvents.Add(new{item.Id,item.Year,item.Kind,item.PolityId,item.Cell,item.SettlementId,item.OtherPolityId,item.Text});}
                archiveAt=s.History.Archive.Count;
                if(s.Years%10==0&&SimulationTime.Seconds(s)%SimulationTime.Year==0)Console.WriteLine("Colonial observation year "+s.Years+"; dispatched seen "+observed.Count);
            }
            s=session.Snapshot();e.SeaVentureObserver=null;e.Validate(s);
        }
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(e,args)!;
        Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);var h=s.History;var a=h.Aggregates;long reads=a.CellReads;string before=s.ComputeHash();
        var events=h.Archive.ToArray();var eras=WorldEraView.FirstYears(s);int Era(int year){int result=0;for(int i=0;i<eras.Length;i++)if(eras[i]>=0&&eras[i]<=year)result=i;return result;}
        object Point(int cell){var p=w.Surface.Cells[cell].Position;return new{Cell=cell,Latitude=Math.Asin(p.Z)*180/Math.PI,Longitude=Math.Atan2(p.Y,p.X)*180/Math.PI,Landmass=w.Landmass[cell]};}
        var births=events.Where(x=>x.SettlementId>=0&&(x.Kind=="sponsored-port"||x.Kind=="provisioned-port")).GroupBy(x=>x.SettlementId).Select(x=>x.OrderBy(y=>y.Year).First()).ToArray();
        var portRows=births.Select(birth=>
        {
            var t=s.Society.Settlements[birth.SettlementId];var charter=events.Where(x=>x.Kind=="overseas-charter"&&x.Cell==t.Cell&&x.Year<=birth.Year&&x.PolityId==birth.PolityId).OrderByDescending(x=>x.Year).FirstOrDefault();
            int parent=a.Cities[t.Id].ParentCity;int supply=a.Empires.Connections.ProvisionedPorts.ByTown[t.Id];var provisioned=supply>=0?a.Empires.Connections.ProvisionedPorts.Ports[supply]:null;
            return new{t.Id,t.Name,t.FoundedYear,Era=WorldEraView.Name(Era(t.FoundedYear)),Kind=birth.Kind,Sponsor=birth.PolityId,SponsorCulture=h.Polities[birth.PolityId].CultureId,Destination=Point(t.Cell),ParentTown=parent,Parent=parent>=0?Point(s.Society.Settlements[parent].Cell):null,DirectKm=parent>=0?D.Angle(w.Surface.Cells[t.Cell].Position,w.Surface.Cells[s.Society.Settlements[parent].Cell].Position)*w.Recipe.RadiusKm:-1,CharterYear=charter?.Year??-1,CharterToTownYears=charter==null?-1:birth.Year-charter.Year,AbandonedYear=a.Living.Cities[t.Id].AbandonedYear,Provisioned=provisioned!=null,FoodSent=provisioned?.SentFood??0,FoodReceived=provisioned?.ReceivedFood??0,FreightPaid=provisioned?.ShippingPaid??0,ParentSourceNow=provisioned?.SupplierTownId??-1};
        }).ToArray();
        var stateRows=new List<object>();var pendingRows=new List<object>();var gates=new SortedDictionary<string,int>();
        foreach(var p in h.Polities.Where(p=>p.EndedYear<0))
        {
            var hc=h.Cultures[p.CultureId];var map=a.Empires.Connections.Geography.Governments[p.Id];
            bool admin=(bool)Call("RevisedMaritimeAdministration",s,p.Id);int[] departures=admin?(int[])Call("LivingColonialDepartures",s,p.Id):Array.Empty<int>();
            string gate=!admin?"shipping-administration":p.Treasury<2500||a.Polities[p.Id].Receipts<=a.Polities[p.Id].ServiceCost?"court-finances":departures.Length==0?"no-departure":"funded-departure";gates[gate]=gates.TryGetValue(gate,out int count)?count+1:1;
            if(departures.Length==0)continue;
            var retry=a.Empires.Connections.PortSettlements.Governments[p.Id].Pending;
            for(int trial=0;trial<Math.Min(8,retry.Count)&&pendingRows.Count<256;trial++)
            {
                var post=a.Empires.Territories.Posts[retry.At(trial)];if(!(bool)Call("PendingColonialPort",s,p.Id,post))continue;
                int departure=post.ParentSource== -1?-1:(int)Call("TerritorialNodeCell",s,post.ParentSource);
                if(departure<0||HistoricalCells.Polity(s,departure)!=p.Id)departure=departures[0];
                var route=new Prehistory.Route{A=e.CellPatch[departure],B=e.CellPatch[post.Cell],CellA=departure,CellB=post.Cell,Sea=true,DistanceM=(int)Math.Round(D.Angle(w.Surface.Cells[departure].Position,w.Surface.Cells[post.Cell].Position)*w.Recipe.RadiusKm*1000)};
                var supplyArgs=new object[]{s,p.Id,route,a.TownAt[departure],-1,0L};bool supplied=(bool)Call("MaritimeCharterLogistics",supplyArgs);int supplier=(int)supplyArgs[4];
                bool canFarm=supplied&&(bool)Call("CanFoundLivingTown",s,s.Society.Settlements[supplier].CultureId,post.Cell,supplier);
                var portArgs=new object[]{s,p.Id,route,supplier,null};bool canStation=supplied&&(bool)Call("CanPrepareProvisionedPort",portArgs);
                int alternative=-1;
                if(supplied&&!canFarm&&!canStation)
                foreach(int candidate in (int[])Call("NearbyLivingTowns",s,departure,280.0))
                {
                    if(candidate==supplier||a.Resolve(s.Society.Settlements[candidate].PolityId,h)!=p.Id)continue;
                    var checkArgs=new object[]{s,p.Id,route,candidate,-1,0L};
                    if((bool)Call("MaritimeCharterLogistics",checkArgs)&&(int)checkArgs[4]==candidate&&(bool)Call("CanFoundLivingTown",s,s.Society.Settlements[candidate].CultureId,post.Cell,candidate)){alternative=candidate;break;}
                }
                pendingRows.Add(new{Polity=p.Id,Post=post.Id,post.FoundedYear,Age=s.Years-post.FoundedYear,Target=Point(post.Cell),Departure=Point(departure),DistanceKm=route.DistanceM/1000.0,Supplied=supplied,Supplier=supplier,SupplierPopulation=supplied?s.Society.Settlements[supplier].Population:0,SupplierRural=supplied?a.Cities[supplier].Rural:0,SupplierFounderAvailability=supplied?(long)Call("LivingFounderAvailability",s,supplier):0,CropYield=supplied?(double)Call("LivingCropLandscape",s,s.Society.Settlements[supplier].CultureId,post.Cell,false):0,CanFoundFarm=canFarm,CanFoundStation=canStation,NearbyWorkingFarmerAlternative=alternative,p.Cohesion,ReviewAfter=a.Empires.Connections.Geography.Governments[p.Id].ReviewAfter[route.B]});
            }
            stateRows.Add(new{p.Id,p.Name,p.CultureId,p.FoundedYear,p.Treasury,p.Cohesion,p.Exhaustion,a.Polities[p.Id].Urban,a.Polities[p.Id].Receipts,a.Polities[p.Id].ServiceCost,Gate=gate,Departures=departures.Select(Point),ReturningFleet=Call("ReturningFleetCapacity",s,p.Id),LegKm=Call("StateMaritimeLegKm",s,p.Id),NetworkKm=Call("StateMaritimeNetworkKm",s,p.Id),KnownPlaces=map.Places.Count,map.Voyages,map.Surveys,map.Landfalls,map.FailedVoyages,PendingMissions=map.Missions.Length,PendingPhysical=map.Missions.Count(m=>m.Kind!=HistoricalSeaMission.Report),NextPlanYear=map.NextPlan/(double)SimulationTime.Year,ScheduledYear=map.ScheduledDue/(double)SimulationTime.Year,FleetBudget=HistoricalSeaVentureEffects.Fleet(a.Polities[p.Id].Urban,a.Polities[p.Id].Receipts-a.Polities[p.Id].ServiceCost,p.Exhaustion),Sailing=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Sailing,s.Years),Navigation=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Navigation,s.Years),Ocean=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.OceanNavigation,s.Years)});
        }
        a.CellReads=reads;original.History.Aggregates.CellReads=originalReads;if(s.ComputeHash()!=before||original.ComputeHash()!=inputHash)throw new Exception("A colonial diagnostic mutated an authoritative checkpoint.");
        var mapsFinal=a.Empires.Connections.Geography.Governments;
        var result=new{Input=path,InputHash=inputHash,Engine=typeof(Prehistory).Assembly.Location,ReplayStart=replayStart,WindowYears=years,StartYear=startYear,StartHash=startHash,Year=s.Years,s.Hash,Realm=a.Empires.Connections.Realms.Version,Eras=eras,
            Warning="Current source/fleet gates are final observations, not retroactive founding values. Quarter-year polling can miss short reports/surveys or a source that ends within the same interval; gone-by-due is not proof of successful arrival. Dispatch/arrival observations are interval-censored. Archive charter-to-port lags use the latest same-sponsor charter at that exact cell. Observer era is not a navigation or fleet capability.",
            Summary=new{Voyages=mapsFinal.Sum(m=>(long)m.Voyages),Surveys=mapsFinal.Sum(m=>(long)m.Surveys),Landfalls=mapsFinal.Sum(m=>(long)m.Landfalls),Failed=mapsFinal.Sum(m=>(long)m.FailedVoyages),KnownMapEntries=mapsFinal.Sum(m=>(long)m.Places.Count),Ports=portRows.Length,OfficeToPortLags=portRows.Where(p=>p.CharterToTownYears>=0).GroupBy(p=>p.CharterToTownYears).Select(g=>new{Years=g.Key,Count=g.Count()}),SourceGates=gates},
            EventCounts=events.Where(x=>x.Kind=="survey-return"||x.Kind=="overseas-charter"||x.Kind=="sponsored-port"||x.Kind=="provisioned-port"||x.Kind=="colonial-council"||x.Kind=="colonial-protectorate"||x.Kind=="overseas-community").GroupBy(x=>(Era:Era(x.Year),x.Kind)).OrderBy(g=>g.Key.Era).ThenBy(g=>g.Key.Kind).Select(g=>new{Era=WorldEraView.Name(g.Key.Era),g.Key.Kind,Count=g.Count(),First=g.Min(x=>x.Year),Last=g.Max(x=>x.Year)}),
            Ports=portRows,Governments=stateRows,PendingPortCases=pendingRows,WindowReasons=reasons,WindowPolityReasons=polityReasons,ObservedDispatches=observed,ObservedDisappearances=arrivals,NewArchiveEvents=newEvents};
        File.WriteAllText(output,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));Console.WriteLine(output+" year="+s.Years+" ports="+portRows.Length+" hash="+s.Hash);
    }
}
