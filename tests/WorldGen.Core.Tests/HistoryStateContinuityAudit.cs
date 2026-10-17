using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Pure saved-state diagnostics. Actor traversals never run in model turns.
public static class HistoryStateContinuityAudit
{
    public static void Run(string source,string output,string reviewPath,int focus=726)
        =>RunLoaded(WorldFile.Read(File.ReadAllBytes(source)),source,output,reviewPath,focus);

    public static void RunLoaded(World world,string source,string output,string reviewPath,int focus=726)
    {
        if(File.Exists(output))throw new IOException("Use a new continuity audit output.");
        var s=world.Prehistory;string before=s.ComputeHash();var h=s.History;var a=h.Aggregates;
        using var review=JsonDocument.Parse(File.ReadAllText(reviewPath));
        object Place(int cell)=>cell<0?null:new{Cell=cell,Latitude=Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI,
            Longitude=Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI};
        var liveTowns=s.Society.Settlements.Where(t=>t.Population>0&&a.Living.Cities[t.Id].AbandonedYear<0)
            .GroupBy(t=>a.Resolve(t.PolityId,h)).ToDictionary(g=>g.Key,g=>g.ToArray());
        var continuityPredecessors=Enumerable.Range(0,h.Polities.Length).Where(i=>h.International.Accounts[i].ContinuitySuccessor>=0)
            .GroupBy(i=>h.International.Accounts[i].ContinuitySuccessor).ToDictionary(g=>g.Key,g=>g.ToArray());
        object Distribution(int year)
        {
            var ages=h.Polities.Where(p=>p.FoundedYear<=year&&(p.EndedYear<0||p.EndedYear>year)).Select(p=>year-p.FoundedYear).OrderBy(age=>age).ToArray();
            int n=ages.Length;double Median()=>n==0?0:n%2==1?ages[n/2]:(ages[n/2-1]+ages[n/2])/2.0;
            return new{Year=year,LivingStates=n,Median=Median(),Maximum=n==0?0:ages[n-1],
                FoundationBoundaryRecords=h.Polities.Count(p=>p.FoundedYear==year),EndBoundaryRecords=h.Polities.Count(p=>p.EndedYear==year),
                Thresholds=new[]{500,1000,2000,4000}.Select(threshold=>new{OverYears=threshold,Count=ages.Count(age=>age>threshold),Share=n==0?0:ages.Count(age=>age>threshold)/(double)n}).ToArray()};
        }
        var states=h.Polities.Select(p=>
        {
            var r=a.Empires.Connections.Realms.Governments[p.Id];var size=a.Polities[p.Id];var imperial=a.Empires.Governments[p.Id];
            var dynamics=h.Dynamics.States[p.Id];var policy=a.Communities.Governments[p.Id];
            var towns=liveTowns.TryGetValue(p.Id,out var found)?found:Array.Empty<Settlement>();var capital=s.Society.Settlements[p.CapitalId];
            return new{p.Id,p.Name,p.CultureId,CultureName=s.Society.Cultures[p.CultureId].Name,p.ParentId,p.FoundedYear,p.EndedYear,
                StateAge=(p.EndedYear>=0?p.EndedYear:s.Years)-p.FoundedYear,RealmFoundedYear=r.FoundedYear,RealmAge=(p.EndedYear>=0?p.EndedYear:s.Years)-r.FoundedYear,
                r.RegimeSince,r.RegimeChanges,r.RegionalSeparations,r.RecognizedAreaKm2,r.RecognitionUpkeep,p.Government,p.Population,p.AreaKm2,p.Treasury,p.Army,p.Cohesion,p.Legitimacy,p.Exhaustion,
                dynamics.Strain,dynamics.PublicWorks,dynamics.LastSuccession,
                CurrentCycle=dynamics.Cycle,CurrentPolicy=new{policy.RepresentationMilli,policy.CentralizationMilli,policy.LastReformYear},
                CurrentTechnology=new[]{HistoricalTechnology.Writing,HistoricalTechnology.Roads,HistoricalTechnology.Printing,HistoricalTechnology.Telegraph,HistoricalTechnology.Radio,HistoricalTechnology.Electronics,HistoricalTechnology.Telecommunications}
                    .Select(technology=>new{Name=technology.ToString(),Deployment=HistoricalModernEffects.Adoption(h.Cultures[p.CultureId],technology,s.Years)}).ToArray(),
                AccountCityCount=size.CityCount,ActiveTownCount=towns.Length,size.Urban,size.Suburban,size.Rural,size.Remote,size.RemoteAreaKm2,size.Receipts,size.ServiceCost,
                imperial.Patron,imperial.SubjectCount,imperial.SubjectPopulation,imperial.SubjectAreaKm2,imperial.Imposed,
                CurrentSovereign=p.EndedYear<0?HistoricalImperialHierarchy.Sovereign(s,p.Id):-1,
                ContinuitySuccessor=h.International.Accounts[p.Id].ContinuitySuccessor,
                ContinuityPredecessors=continuityPredecessors.TryGetValue(p.Id,out var previous)?previous:Array.Empty<int>(),
                Capital=new{capital.Id,capital.Name,capital.FoundedYear,Age=s.Years-capital.FoundedYear,capital.Population,capital.CultureId,Place=Place(capital.Cell),Supported=a.Cities[capital.Id].Supported},
                ActiveTowns=towns.Select(t=>new{t.Id,t.Name,t.FoundedYear,t.Population,t.CultureId,RawPolity=t.PolityId,ResolvedPolity=a.Resolve(t.PolityId,h),CurrentSovereign=HistoricalImperialHierarchy.Sovereign(s,t.PolityId),Place=Place(t.Cell),Supported=a.Cities[t.Id].Supported,
                    a.Cities[t.Id].Rural,a.Cities[t.Id].Suburban,a.Living.Cities[t.Id].DeclineYears,
                    a.Communities.Cities[t.Id].InclusionMilli,a.Communities.Cities[t.Id].SinceYear,h.Towns[t.Id].AutonomyPressure,
                    CurrentClaimant=HistoricalRealmClaimantHistory.At(s,t.Id)}).ToArray()};
        }).ToArray();
        var sovereigns=h.Polities.Where(p=>p.EndedYear<0).GroupBy(p=>HistoricalImperialHierarchy.Sovereign(s,p.Id))
            .Select(group=>new{Id=group.Key,Name=h.Polities[group.Key].Name,Courts=group.Select(p=>p.Id).OrderBy(id=>id).ToArray(),
                TownCount=group.Sum(p=>liveTowns.TryGetValue(p.Id,out var towns)?towns.Length:0),
                TownResidents=group.Sum(p=>liveTowns.TryGetValue(p.Id,out var towns)?towns.Sum(t=>t.Population):0),
                ExclusiveCourtPopulations=group.Sum(p=>p.Population),ExclusiveCourtAreaKm2=group.Sum(p=>p.AreaKm2)})
            .OrderByDescending(group=>group.TownCount).ThenBy(group=>group.Id).ToArray();
        var events=h.Archive.Where(e=>e.PolityId==focus||e.OtherPolityId==focus).OrderBy(e=>e.Year).ThenBy(e=>e.Id).ToArray();
        var campaigns=h.Dynamics.Campaigns.Where(c=>c.A==focus||c.B==focus).Select(c=>
        {
            var op=a.Empires.Connections.Military.Campaigns[c.Id];
            return new{c.Id,c.Name,c.A,c.B,c.AttackerId,c.StartedYear,c.EndedYear,Aim=c.Aim.ToString(),c.Conquests,c.Repulses,c.CapturedSettlements,
                op.OriginalCapitalA,op.OriginalCapitalB,op.VictoriesA,op.VictoriesB,op.CorePressureA,op.CorePressureB,op.Closed};
        }).ToArray();
        var leaders=h.Dynamics.Leaders.Where(l=>l.PolityId==focus).Select(l=>new{l.Id,l.Name,l.StartedYear,l.EndedYear,l.Victories,l.Defeats,l.Legacy}).ToArray();
        var captures=events.Where(e=>e.Kind=="conquest").Select(e=>new{e.Id,e.Year,e.PolityId,e.OtherPolityId,e.SettlementId,e.Text,Place=Place(e.Cell),
            FocusWon=e.PolityId==focus,FocusLost=e.OtherPolityId==focus}).ToArray();
        if(s.ComputeHash()!=before)throw new Exception("Saved continuity audit changed its source.");
        string assembly=typeof(Prehistory).Assembly.Location;
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=Path.GetFullPath(source),Year=s.Years,Hash=before,CoreAssembly=assembly,
            CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))),Realm=a.Empires.Connections.Realms.Version,
            FinalDistribution=Distribution(s.Years),HistoricalDistributions=review.RootElement.GetProperty("Rows").EnumerateArray().Where(row=>row.GetProperty("Year").GetInt32()<=s.Years)
                .Select(row=>new{Era=row.GetProperty("EraName").GetString(),Distribution=Distribution(row.GetProperty("Year").GetInt32())}).ToArray(),States=states,CurrentSovereignGroups=sovereigns,
            Focus=new{Id=focus,LeaderRecords=leaders,MilitaryGovernment=a.Empires.Connections.Military.Governments[focus],Campaigns=campaigns,Captures=captures,
                Events=events.Select(e=>new{e.Id,e.Year,e.Kind,e.CultureId,e.PolityId,e.OtherPolityId,e.SettlementId,e.Text,Place=Place(e.Cell)}).ToArray()},
            Notes=new[]{"Pure saved-state inspection: no replay, rules upgrade, simulation mutation or save write.",
                "Historical age distributions reconstruct recorded existence intervals from immutable foundation/end dates. Same-year foundation/end events can occur after a saved row within that year: historical boundary counts/medians are approximate unless independently joined to that snapshot's actual owners. Final distribution uses the actual saved living-state records. Historical territory, city counts and constitutions are not reconstructed from final accounts.",
                "StateAge uses the polity record, matching review.json; RealmAge separately exposes explicit constitutional continuity dates. ParentId does not automatically imply constitutional continuity.",
                "All present-day town/account/army/territory fields are measured at the final saved year; dead-state aggregate accounts may retain historical caches and are not live holdings. CurrentSovereignGroups resolves actual final bounded imperial allegiance, not similar culture colors; its area/population sums use exclusive court caches rather than double-counting SubjectArea/SubjectPopulation. No past sovereign hierarchy is reconstructed from these final values.",
                "Full focused archive is exported, including state/secession/regime/conquest/capital records omitted by the political-summary filter. No absent capital-moved event is treated as proof of no relocation.",
                "A declared war alone is not a credible military threat. Supplied battles, actual captured towns, original-capital pressure and military stocks are exported separately."}},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine($"State continuity {s.Years}: {states.Count(p=>p.EndedYear<0)} living of {states.Length}; focus{focus}/{events.Length} events; preserved {before}");
    }
}
