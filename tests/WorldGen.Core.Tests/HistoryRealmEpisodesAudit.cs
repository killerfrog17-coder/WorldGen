using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Explicit OFFLINE actor/archive inspection, not an ordinary simulation census.
public static class HistoryRealmEpisodesAudit
{
    static readonly HashSet<string> TerritorialKinds=new(StringComparer.Ordinal){"conquest","great-conquest","secession","provincial-independence","imperial-allegiance","imperial-independence","collapse","colonial-independence","realm-regional-separation"};
    static readonly HashSet<string> EpisodeKinds=new(TerritorialKinds,StringComparer.Ordinal){"state","state-foundation","independent-institutions","institutional-diffusion","realm-succession-crisis","regime-change","dynasty-change","constitutional-reform","peace","war","capital-relocation","capital-moved","realm-local-claimant","realm-claimant-government","realm-claimant-reconciled","realm-claimant-displaced","realm-claimant-succession"};
    public static void Run(string path,string folder)
    {
        if(Directory.Exists(folder))throw new IOException("Use a new realm-episodes audit folder.");
        var world=WorldFile.Read(File.ReadAllBytes(path));var s=world.Prehistory;var engine=new Prehistory(world);engine.Validate(s);string original=s.ComputeHash();var h=s.History;var a=h.Aggregates;var r=a.Empires.Connections.Realms;
        var events=h.Archive.Where(e=>EpisodeKinds.Contains(e.Kind)).ToArray();
        var rulerCounts=h.Dynamics.Leaders.GroupBy(l=>l.PolityId).ToDictionary(g=>g.Key,g=>g.Count());
        var byParty=new Dictionary<int,List<HistoricalEvent>>();
        foreach(var e in events)foreach(int party in new[]{e.PolityId,e.OtherPolityId}.Where(id=>id>=0).Distinct()){if(!byParty.TryGetValue(party,out var list))byParty.Add(party,list=new List<HistoricalEvent>());list.Add(e);}
        object Event(HistoricalEvent e)=>new{e.Year,e.Kind,e.PolityId,e.OtherPolityId,e.SettlementId,e.Text};
        var rows=h.Polities.Select(p=>
        {
            bool alive=p.EndedYear<0;var g=a.Empires.Governments[p.Id];var ds=h.Dynamics.States[p.Id];var ra=r.Governments[p.Id];var town=s.Society.Settlements[p.CapitalId];var culture=s.Society.Cultures[p.CultureId];
            var chronology=byParty.TryGetValue(p.Id,out var list)?list.ToArray():Array.Empty<HistoricalEvent>();
            var last=chronology.LastOrDefault(e=>TerritorialKinds.Contains(e.Kind));int patron=alive?HistoricalImperialHierarchy.Patron(s,p.Id):-1,sovereign=alive?HistoricalImperialHierarchy.Sovereign(s,p.Id):-1;
            var account=a.Polities[p.Id];var policy=a.Communities.Governments[p.Id];
            var hc=h.Cultures[p.CultureId];double Adopt(HistoricalTechnology technology)=>HistoricalModernEffects.Adoption(hc,technology,s.Years);
            double integration=Math.Clamp(.20*Adopt(HistoricalTechnology.Writing)+.20*Adopt(HistoricalTechnology.Roads)+.20*Adopt(HistoricalTechnology.Telegraph)+.20*policy.RepresentationMilli/1000.0+.20*ds.PublicWorks/1000.0,0,1);
            double bureaucracy=Math.Clamp(.5*Adopt(HistoricalTechnology.Telegraph)+.3*Adopt(HistoricalTechnology.Radio)+.2*Adopt(HistoricalTechnology.Electronics),0,1)*(.5+.5*ds.PublicWorks/1000.0);
            var separations=chronology.Where(e=>(e.Kind=="secession"||e.Kind=="provincial-independence")&&e.OtherPolityId==p.Id).ToArray();
            long own=alive?p.AreaKm2:0,subjects=alive?g.SubjectAreaKm2:0,population=alive?p.Population:0,subjectPopulation=alive?g.SubjectPopulation:0;
            return new{p.Id,p.Name,p.CultureId,Culture=culture.Name,CultureFounded=culture.FoundedYear,p.ParentId,p.FoundedYear,p.EndedYear,ActorAge=(alive?s.Years:p.EndedYear)-p.FoundedYear,
                ContinuedCountryFounded=ra.FoundedYear,ContinuedCountryAge=(alive?s.Years:p.EndedYear)-ra.FoundedYear,ra.RegimeSince,RegimeAge=(alive?s.Years:p.EndedYear)-ra.RegimeSince,ra.RegimeChanges,ra.RegionalSeparations,ds.HouseSerial,
                Patron=patron,Sovereign=sovereign,StoredAllegianceSince=g.SinceYear,IndependentNow=alive&&patron<0,OwnAreaKm2=own,SubjectAreaKm2=subjects,CombinedAreaKm2=own+subjects,RecognizedAreaKm2=alive?ra.RecognizedAreaKm2:0,
                Population=population,SubjectPopulation=subjectPopulation,CombinedPopulation=population+subjectPopulation,SubjectCount=alive?g.SubjectCount:0,ds.PeakAreaKm2,ds.PeakPopulation,
                RecordedOwnAreaKm2=p.AreaKm2,RecordedPopulation=p.Population,RecordedSubjectAreaKm2=g.SubjectAreaKm2,
                CurrentToObservedPeakArea=ds.PeakAreaKm2<=0?0:(own+subjects)/(double)ds.PeakAreaKm2,
                Capital=new{town.Id,town.Name,town.Cell,town.Population,Abandoned=a.Living.Cities[town.Id].AbandonedYear,Owner=HistoricalCells.Polity(s,town.Cell)},
                Cities=alive?account.CityCount:0,p.Government,Policy=policy,LastRulerSuccession=ds.LastSuccession,StoredAggregateLastSuccession=account.LastSuccession,RecordedRulers=rulerCounts.TryGetValue(p.Id,out int count)?count:0,account.SuccessionDue,account.Receipts,account.ServiceCost,account.ReachKm,p.Legitimacy,p.Cohesion,p.Exhaustion,ds.PublicWorks,ds.Strain,ds.Cycle,
                ObservedFinalIntegration=integration,ObservedFinalBureaucracy=bureaucracy,
                CrisisCount=chronology.Count(e=>e.Kind=="realm-succession-crisis"&&e.PolityId==p.Id),RecordedConquestGains=chronology.Count(e=>(e.Kind=="conquest"||e.Kind=="great-conquest")&&e.PolityId==p.Id),RecordedConquestLosses=chronology.Count(e=>(e.Kind=="conquest"||e.Kind=="great-conquest")&&e.OtherPolityId==p.Id),
                RecordedSecessionsFrom=separations.Length,DistinctRecordedSeparatingCourts=separations.Select(e=>e.PolityId).Where(id=>id>=0&&id!=p.Id).Distinct().Count(),
                LastRecordedTerritorialEvent=last==null?null:Event(last),LastRecordedTerritorialYear=last?.Year??-1,
                Ruler=ds.LeaderId<0?null:h.Dynamics.Leaders[ds.LeaderId],
                EventCount=chronology.Length,Events=chronology.Length<=150?chronology.Select(Event).ToArray():chronology.Take(50).Concat(chronology.TakeLast(100)).Select(Event).ToArray()};
        }).ToArray();
        var aliveRows=rows.Where(p=>p.EndedYear<0).ToArray();var oldest=aliveRows.OrderByDescending(p=>p.ContinuedCountryAge).ThenBy(p=>p.Id).Take(15).ToArray();var largest=aliveRows.OrderByDescending(p=>p.CombinedAreaKm2).ThenBy(p=>p.Id).Take(15).ToArray();var peaks=rows.OrderByDescending(p=>p.PeakAreaKm2).ThenBy(p=>p.Id).Take(20).ToArray();
        var selected=oldest.Concat(largest).Concat(peaks).DistinctBy(p=>p.Id).OrderBy(p=>p.Id).ToArray();
        var claimants=r.Claimants?.Records??Array.Empty<HistoricalRealmLocalClaimant>();var activeClaims=claimants.Where(c=>HistoricalRealmClaimantHistory.Active(s,c)).ToArray();
        var summary=new{Source=path,Year=s.Years,Hash=s.Hash,Realms=r.Version,ReligiousRules=a.Empires.Connections.Religion?.Version,States=h.Polities.Length,Alive=aliveRows.Length,Independent=aliveRows.Count(p=>p.IndependentNow),
            LatestClaimantCenters=claimants.Length,CurrentActiveLocalClaims=activeClaims.Length,CurrentActiveAfterCentralDeadline=activeClaims.Count(c=>r.Governments[c.Owner].CrisisUntil<=s.Years),
            LatestClaimsOutlivingOriginCrisisMaximum=claimants.Count(c=>(c.ResolvedYear>=0?c.ResolvedYear:c.LastYear)>c.CrisisSinceYear+25),
            RecordedClaimantEvents=events.Where(e=>e.Kind.StartsWith("realm-claimant-",StringComparison.Ordinal)||e.Kind=="realm-local-claimant").GroupBy(e=>e.Kind).OrderBy(g=>g.Key,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Count()),
            CurrentLocalClaimExamples=activeClaims.OrderByDescending(c=>c.SupportMilli).ThenBy(c=>c.City).Take(12).Select(c=>new{c.City,Place=s.Society.Settlements[c.City].Name,c.Owner,Court=h.Polities[c.Owner].Name,c.Name,c.DeclaredYear,c.LeaderSinceYear,c.SupportMilli,CurrentCentralCrisisUntil=r.Governments[c.Owner].CrisisUntil}),
            OldestLivingCountries=oldest.Select(p=>new{p.Id,p.Name,p.ContinuedCountryAge,p.ActorAge,p.RegimeAge,p.IndependentNow,p.Patron,p.Sovereign,p.OwnAreaKm2,p.SubjectAreaKm2,p.PeakAreaKm2,p.CurrentToObservedPeakArea,p.CrisisCount,p.RegimeChanges,p.RecordedSecessionsFrom,p.LastRecordedTerritorialYear}),
            LargestCurrent=largest.Select(p=>new{p.Id,p.Name,p.ContinuedCountryAge,p.IndependentNow,p.CombinedAreaKm2,p.PeakAreaKm2,p.CurrentToObservedPeakArea,p.SubjectCount,p.RecordedSecessionsFrom}),
            LargestHistorical=peaks.Select(p=>new{p.Id,p.Name,p.FoundedYear,p.EndedYear,p.PeakAreaKm2,p.CombinedAreaKm2,p.CurrentToObservedPeakArea,p.CrisisCount,p.RecordedSecessionsFrom}),
            Notes="Culture continuity, state actor, recorded continued country, regime and present sovereign are distinct. RegimeSince records contested court changes, not every ordinary ruler succession. Own administrative area, reported subject area and recognized titles are separate ledgers. Ended actors have zero current holdings; Recorded fields preserve their frozen historical ledger, not current territory. Peaks are observed actor-event peaks without stored peak dates, not simultaneous empire censuses. Last recorded territorial event is an archive observation, not an assertion that every local cell transaction is narrated. AllegianceSince is the stored current relationship date, not proof of unbroken imperial sovereignty. RecordedSecessionsFrom counts events (secession/provincial independence can describe the same new court); DistinctRecordedSeparatingCourts counts counterpart IDs, not simultaneously independent successors. Selected episodes retain at most150 significant events per state, first50 plus last100 for larger records."};
        if(s.ComputeHash()!=original)throw new Exception("Realm-episode audit changed its source checkpoint.");Directory.CreateDirectory(folder);var options=new JsonSerializerOptions{WriteIndented=true,IncludeFields=true};File.WriteAllText(Path.Combine(folder,"summary.json"),JsonSerializer.Serialize(summary,options));File.WriteAllText(Path.Combine(folder,"selected-episodes.json"),JsonSerializer.Serialize(selected,options));File.WriteAllText(Path.Combine(folder,"states.json"),JsonSerializer.Serialize(rows.Select(p=>new{p.Id,p.Name,p.CultureId,p.ParentId,p.FoundedYear,p.EndedYear,p.ActorAge,p.ContinuedCountryAge,p.RegimeAge,p.IndependentNow,p.Patron,p.Sovereign,p.OwnAreaKm2,p.SubjectAreaKm2,p.PeakAreaKm2,p.PeakPopulation,p.CurrentToObservedPeakArea,p.CrisisCount,p.RegimeChanges,p.RecordedSecessionsFrom,p.LastRecordedTerritorialYear}),options));
        File.WriteAllText(Path.Combine(folder,"claimant-episodes.json"),JsonSerializer.Serialize(claimants.Select(c=>new{Record=c,Place=s.Society.Settlements[c.City].Name,Court=h.Polities[c.Owner].Name,OriginCrisisMaximumEnd=c.CrisisSinceYear+25,OutlivedOriginMaximum=(c.ResolvedYear>=0?c.ResolvedYear:c.LastYear)>c.CrisisSinceYear+25,Successor=c.SuccessorPolity>=0?h.Polities[c.SuccessorPolity].Name:null,Events=events.Where(e=>e.SettlementId==c.City&&e.Year>=c.DeclaredYear&&(e.PolityId==c.Owner||e.OtherPolityId==c.Owner)&&(e.Kind.StartsWith("realm-claimant-",StringComparison.Ordinal)||e.Kind=="realm-local-claimant")).TakeLast(40).Select(Event).ToArray()}),options));
        Console.WriteLine("Realm episodes "+path+": "+aliveRows.Length+" living states; oldest "+oldest.FirstOrDefault()?.Name+" age "+oldest.FirstOrDefault()?.ContinuedCountryAge+"; largest "+largest.FirstOrDefault()?.Name+" "+largest.FirstOrDefault()?.CombinedAreaKm2+" km2.");
    }
}
