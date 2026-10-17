using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Explicit read-only OFFLINE actor audit. Never called by production turns.
public static class HistoryCityLifecycleAudit
{
    public static void Run(string worldPath,string reviewFolder,int fromYear,int toYear,string outputFolder)
    {
        if(Directory.Exists(outputFolder))throw new IOException("Use a new city-lifecycle audit folder.");
        var world=WorldFile.Read(File.ReadAllBytes(worldPath));var s=world.Prehistory;var h=s.History;var a=h.Aggregates;var engine=new Prehistory(world);engine.Validate(s);string original=s.ComputeHash();
        using var first=JsonDocument.Parse(File.ReadAllText(Path.Combine(reviewFolder,"year-"+fromYear,"towns.json")));
        using var last=JsonDocument.Parse(File.ReadAllText(Path.Combine(reviewFolder,"year-"+toYear,"towns.json")));
        var before=first.RootElement.EnumerateArray().ToDictionary(t=>t.GetProperty("Id").GetInt32(),t=>t.Clone());
        var after=last.RootElement.EnumerateArray().Select(t=>t.GetProperty("Id").GetInt32()).ToHashSet();
        var lost=before.Keys.Where(id=>!after.Contains(id)).OrderBy(id=>id).ToArray();
        var histories=h.Archive.Where(e=>e.SettlementId>=0&&lost.Contains(e.SettlementId)).GroupBy(e=>e.SettlementId).ToDictionary(g=>g.Key,g=>g.ToArray());
        var reports=lost.Select(id=>
        {
            var t=s.Society.Settlements[id];var life=a.Living.Cities[id];var record=h.Towns[id];var city=a.Cities[id];var hc=h.Cultures[t.CultureId];
            var events=histories.TryGetValue(id,out var entries)?entries:Array.Empty<HistoricalEvent>();
            var abandonment=events.LastOrDefault(e=>e.Kind=="abandonment");var disruption=events.LastOrDefault(e=>e.Kind=="local-decline"&&e.Year<=life.AbandonedYear);
            int governor=abandonment?.PolityId??-1;var government=governor>=0?h.Polities[governor]:null;var governingCulture=government==null?null:h.Cultures[government.CultureId];
            bool early=abandonment?.Text.Contains("early village loses")==true;
            string terminal=early?"early-village-dispersal":life.DeclineYears<30?"urban-below-25":life.DeclineYears<80?"30-year-peak-relative-branch-or-urban-below-25":"at-least-80-year-branch";
            double Km(Settlement other)=>D.Angle(world.Surface.Cells[t.Cell].Position,world.Surface.Cells[other.Cell].Position)*world.Recipe.RadiusKm;
            var successors=s.Society.Settlements.Where(o=>o.Id!=id&&o.FoundedYear>=life.AbandonedYear&&o.FoundedYear<=toYear&&a.Living.Cities[o.Id].AbandonedYear<0).Select(o=>(Town:o,Km:Km(o))).Where(o=>o.Km<=100).OrderBy(o=>o.Km).ThenBy(o=>o.Town.Id).Take(3).ToArray();
            return new{Id=id,Before=before[id],Final=new{t.Name,t.Cell,t.FoundedYear,t.Population,t.FoodCapacity,t.FoodReserve,city.Rural,city.Suburban,city.Supported,life.AbandonedYear,life.DeclineYears,life.Productivity,life.DisruptedUntil,life.PeakPopulation,life.PeakYear,
                Climate=world.Climate.Classification[t.Cell].ToString(),Biome=world.Ecology.Habitat[t.Cell].Biome.ToString(),record.DiseaseDeaths,Demography=record.Demography,
                SanitationObservedFinal=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Sanitation,s.Years),RailwaysObservedFinal=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Railways,s.Years),MechanizedAgricultureObservedFinal=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.MechanizedAgriculture,s.Years)},
                TerminalPath=terminal,Governor=governor,Government=government==null?null:new{government.Name,government.CultureId,government.FoundedYear,government.EndedYear,
                    SanitationObservedFinal=HistoricalModernEffects.Adoption(governingCulture,HistoricalTechnology.Sanitation,s.Years),RailwaysObservedFinal=HistoricalModernEffects.Adoption(governingCulture,HistoricalTechnology.Railways,s.Years),MechanizedAgricultureObservedFinal=HistoricalModernEffects.Adoption(governingCulture,HistoricalTechnology.MechanizedAgriculture,s.Years)},
                AbandonmentEvent=abandonment==null?null:new{abandonment.Year,abandonment.Text},LastDisruption=disruption==null?null:new{disruption.Year,disruption.Text},
                RecordedEvents=events.Where(e=>e.Year>=fromYear-200&&e.Year<=toYear).Select(e=>new{e.Year,e.Kind,e.Text}).ToArray(),
                NearbyActiveFoundations=successors.Select(o=>new{o.Town.Id,o.Town.Name,o.Town.Cell,o.Town.FoundedYear,o.Town.Population,Km=o.Km}).ToArray()};
        }).ToArray();
        var summary=new{World=worldPath,FinalYear=s.Years,FromYear=fromYear,ToYear=toYear,Before=before.Count,After=after.Count,LostIds=lost.Length,PriorUrbanResidents=lost.Sum(id=>before[id].GetProperty("Population").GetInt64()),
            PhysicalAbandonment=reports.Count(r=>r.Final.AbandonedYear>=fromYear&&r.Final.AbandonedYear<=toYear),StillActiveLostIds=reports.Count(r=>r.Final.AbandonedYear<0),
            ActiveDisruptionAtAbandonment=reports.Count(r=>r.Final.DisruptedUntil>=r.Final.AbandonedYear&&r.Final.AbandonedYear>=0),LowProductivityAtAbandonment=reports.Count(r=>r.Final.Productivity<.4),
            AtLeast80DeclineYears=reports.Count(r=>r.Final.DeclineYears>=80),FinalLocalDeploymentAtLeastHalfSanitation=reports.Count(r=>r.Final.SanitationObservedFinal>=.5),
            TerminalPaths=reports.GroupBy(r=>r.TerminalPath).Select(g=>new{Path=g.Key,Count=g.Count(),PriorUrbanResidents=g.Sum(r=>r.Before.GetProperty("Population").GetInt64())}),
            StateOwnedAtAbandonment=reports.Count(r=>r.Governor>=0),FinalGovernorDeploymentAtLeastHalfSanitation=reports.Count(r=>r.Government?.SanitationObservedFinal>=.5),FinalGovernorSanitationGapAtLeastQuarter=reports.Count(r=>r.Government!=null&&r.Government.SanitationObservedFinal-r.Final.SanitationObservedFinal>=.25),
            NearbyActiveFoundationWithin25Km=reports.Count(r=>r.NearbyActiveFoundations.Any(o=>o.Km<=25)),SameCellActiveRefoundation=reports.Count(r=>r.NearbyActiveFoundations.Any(o=>o.Cell==r.Final.Cell)),
            LastDisruptionReasons=reports.Where(r=>r.LastDisruption!=null).GroupBy(r=>r.LastDisruption.Text.Contains("prolonged conflict")?"conflict":r.LastDisruption.Text.Contains("water and farmland")?"water-and-farmland":"land-use-and-exchange").Select(g=>new{Cause=g.Key,Count=g.Count()}),
            Climates=reports.GroupBy(r=>r.Final.Climate).Select(g=>new{Climate=g.Key,Count=g.Count()}),
            Notes="Lost prior residents are not recorded deaths. AbandonLivingTown transfers surviving urban, suburban and rural households to regional countryside and zeroes stocks/capacity. Final life fields freeze at abandonment, but last Demography precedes the terminal branch; exact terminal supply and resident counts were not archived. Technical.Deployment is final culture practice: Adoption ignores the supplied year for technical cultures, so these deployment observations cannot reconstruct capability at abandonment. Nearby foundations demonstrate possible site continuity, not proven identity or migrant descent."};
        if(s.ComputeHash()!=original)throw new Exception("Read-only city audit changed the source checkpoint.");
        Directory.CreateDirectory(outputFolder);var options=new JsonSerializerOptions{WriteIndented=true,IncludeFields=true};File.WriteAllText(Path.Combine(outputFolder,"summary.json"),JsonSerializer.Serialize(summary,options));File.WriteAllText(Path.Combine(outputFolder,"towns.json"),JsonSerializer.Serialize(reports,options));
        Console.WriteLine(JsonSerializer.Serialize(summary,options));
    }
}
