using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Explicit offline actor/contact-graph audit. No ordinary event calls this.
public static class HistoryCapitalTransitionsAudit
{
    static readonly HashSet<string> Relevant=new(StringComparer.Ordinal){"conquest","great-conquest","collapse","secession","provincial-independence","regional-realignment","abandonment","town-abandoned","imperial-allegiance","imperial-independence","realm-succession-crisis","capital-moved","realm-claimant-government","realm-local-claimant"};
    public static void Run(string path,int years,string folder)
    {
        if(years<1||years>100||Directory.Exists(folder))throw new ArgumentException("Use1..100 years and a new capital audit directory.");
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;var engine=new Prehistory(world);engine.Validate(source);string original=source.ComputeHash();var session=new SimulationSession(engine,source);var rows=new List<object>();
        object Call(string method,params object[] args)=>typeof(Prehistory).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,args);
        double Distance(int first,int second){var a=world.Surface.Cells[first].Position;var b=world.Surface.Cells[second].Position;return world.Recipe.RadiusKm*Math.Acos(Math.Clamp(a.X*b.X+a.Y*b.Y+a.Z*b.Z,-1,1));}
        object Event(HistoricalEvent e)=>new{e.Id,e.Year,e.Kind,e.Cell,e.PolityId,e.OtherPolityId,e.SettlementId,e.Text};
        object Finances(PrehistoryState s,int polity)
        {
            var p=s.History.Polities[polity];var a=s.History.Aggregates;var account=a.Polities[polity];var ds=s.History.Dynamics.States[polity];return new{p.EndedYear,p.CapitalId,p.Population,p.AreaKm2,p.Army,p.Treasury,p.Legitimacy,p.Cohesion,p.Exhaustion,ds.PublicWorks,account.Receipts,account.ServiceCost,Cities=account.Cities.Count,Patron=p.EndedYear<0?HistoricalImperialHierarchy.Patron(s,polity):-1,ds.Cycle,CentralCrisisUntil=a.Empires.Connections.Realms.Governments[polity].CrisisUntil,LocalClaimants=a.Empires.Connections.Realms.Claimants?.Records.Count(c=>c.Owner==polity&&HistoricalRealmClaimantHistory.Active(s,c))??0};
        }
        for(int step=0;step<years;step++)
        {
            var before=session.LastSnapshot;int startArchive=before.History.Archive.Count;session.AdvanceSeconds(SimulationTime.Year,int.MaxValue);var after=session.LastSnapshot;var a=after.History.Aggregates;
            var recent=after.History.Archive.Skip(startArchive).Where(e=>Relevant.Contains(e.Kind)).ToArray();
            foreach(var p in before.History.Polities)
            {
                if(p.EndedYear>=0)continue;var next=after.History.Polities[p.Id];bool ended=next.EndedYear>=0;if(!ended&&next.CapitalId==p.CapitalId)continue;
                var old=before.Society.Settlements[p.CapitalId];var current=after.Society.Settlements[next.CapitalId];
                var events=recent.Where(e=>e.PolityId==p.Id||e.OtherPolityId==p.Id||e.SettlementId==old.Id||e.SettlementId==current.Id).ToArray();
                bool abandoned=a.Living.Cities[old.Id].AbandonedYear>=before.Years&&a.Living.Cities[old.Id].AbandonedYear>=0;
                bool captured=events.Any(e=>(e.Kind=="conquest"||e.Kind=="great-conquest")&&(e.SettlementId==old.Id||e.Cell==old.Cell)&&e.OtherPolityId==p.Id);
                int oldOwner=HistoricalCells.Polity(after,old.Cell);bool separation=oldOwner>=0&&oldOwner!=p.Id&&after.History.Polities[oldOwner].ParentId==p.Id&&events.Any(e=>(e.Kind=="secession"||e.Kind=="provincial-independence"||e.Kind=="regional-realignment")&&e.OtherPolityId==p.Id);
                long withdrawn=(a.Empires.Connections.ServiceRecovery?.WithdrawnCells??0)-(before.History.Aggregates.Empires.Connections.ServiceRecovery?.WithdrawnCells??0);
                string reason=captured?"recorded military capture":abandoned?"actual town abandonment":separation?"local-separation-associated":oldOwner<0&&withdrawn>0?"withdrawal-or-unrecorded-transfer":"other/unrecorded capital loss";
                var reachable=new HashSet<int>{old.Id};var queue=new Queue<int>();queue.Enqueue(old.Id);
                // Diagnostic only: actual existing city links with owned living
                // intermediate centers. This is not proof of a secure corridor.
                while(queue.Count>0)
                {
                    int from=queue.Dequeue();foreach(int peer in a.Cities[from].Neighbors)
                    {
                        var town=after.Society.Settlements[peer];if(reachable.Contains(peer)||a.Living.Cities[peer].AbandonedYear>=0||town.Population<=0||a.Resolve(town.PolityId,after.History)!=p.Id)continue;
                        if(!(bool)Call("InstitutionalRouteAllowed",after,after.Society.Settlements[from].Cell,town.Cell,p.CultureId))continue;reachable.Add(peer);queue.Enqueue(peer);
                    }
                }
                var alternatives=Enumerable.Range(0,a.Polities[p.Id].Cities.Count).Select(index=>a.Polities[p.Id].Cities.At(index)).Select(id=>
                {
                    var town=after.Society.Settlements[id];var life=a.Living.Cities[id];var local=a.Communities.Cities[id];double km=Distance(old.Cell,town.Cell);
                    bool viable=a.Cities[id].Supported&&life.DeclineYears==0&&town.Population>=600&&town.Stability>=500&&(town.FoodReserve>=town.Population/4||town.FoodCapacity>=town.Population/2);
                    return new{town.Id,town.Name,town.Cell,town.Population,town.FoodReserve,town.FoodCapacity,town.Stability,a.Cities[id].Supported,life.DeclineYears,life.Productivity,local.InclusionMilli,local.SinceYear,GreatCircleKm=km,EffortKm=(double)Call("LivingTravelKm",after,old.Cell,town.Cell,p.CultureId),SameLandmass=world.Landmass[old.Cell]>=0&&world.Landmass[old.Cell]==world.Landmass[town.Cell],SingleInstitutionalLegAllowed=(bool)Call("InstitutionalRouteAllowed",after,old.Cell,town.Cell,p.CultureId),OwnCityLinkPath=reachable.Contains(id),LocalSourceLive=(bool)Call("TerritorialNodeLive",after,p.Id,id),ViableLoyalProxy=viable&&local.InclusionMilli>=600,BagIndex=a.Polities[p.Id].Cities.Positions[id]};
                }).OrderBy(t=>t.GreatCircleKm).ThenBy(t=>t.Id).ToArray();
                var chosen=alternatives.FirstOrDefault(t=>t.Id==next.CapitalId);var nearest=alternatives.FirstOrDefault();var strong=alternatives.FirstOrDefault(t=>t.ViableLoyalProxy&&t.OwnCityLinkPath);
                rows.Add(new{Year=after.Years,Polity=p.Id,p.Name,Ended=ended,Reason=reason,OldCapital=new{old.Id,old.Name,old.Cell,BeforePopulation=old.Population,AfterPopulation=after.Society.Settlements[old.Id].Population,AfterOwner=oldOwner,AbandonedYear=a.Living.Cities[old.Id].AbandonedYear},NewCapital=new{current.Id,current.Name,current.Cell},DistanceKm=Distance(old.Cell,current.Cell),Before=Finances(before,p.Id),After=Finances(after,p.Id),Chosen=chosen,Nearest=nearest,NearestViableLoyalLinked=strong,NearestAlternatives=alternatives.Take(12),CountryFundingProxy=next.Treasury>0&&a.Polities[p.Id].Receipts>=a.Polities[p.Id].ServiceCost,ObservedWithdrawnCellsWorldwide=withdrawn,Events=events.Select(Event)});
            }
        }
        var final=session.Snapshot();engine.Validate(final);if(source.ComputeHash()!=original)throw new Exception("Capital audit changed its input world.");Directory.CreateDirectory(folder);
        var options=new JsonSerializerOptions{WriteIndented=true};File.WriteAllText(Path.Combine(folder,"transitions.json"),JsonSerializer.Serialize(new{Source=path,Start=source.Years,End=final.Years,SourceHash=original,EndHash=final.Hash,Rules=source.History.Aggregates.Empires.Connections.Realms.Version,Rows=rows,Notes="Explicit offline annual observations, not exhaustive within-year transactions or a recurring simulation census. National annual financial/demographic deltas include ordinary war/economy and are not isolated relocation costs. Receipts/service and public works are national proxies, not stored local budgets. SingleInstitutionalLegAllowed tests only one leg; OwnCityLinkPath uses actual owned living city contacts and institutional leg gates, not a proven safe military escape corridor. ViableLoyalProxy is an audit screening heuristic, not an authoritative court-refuge rule. A frozen ended actor's ledgers are historical, not current holdings. Unrecorded transfer and service withdrawal are not distinguished merely by simultaneous worldwide withdrawn cells."},options));
        Console.WriteLine("Capital transitions "+source.Years+"→"+final.Years+": "+rows.Count+" annual capital-change/end-state observations; source unchanged.");
    }
}
