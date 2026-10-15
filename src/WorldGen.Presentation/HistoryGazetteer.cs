using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static partial class SocietyView
    {
        static string GazetteerLocation(World? world,int cell)
        {
            if(world==null||cell<0||cell>=world.Surface.Cells.Length)return "Coordinates not included";
            var p=Location(world,cell);double lat=Math.Asin(p.Z)*180/Math.PI,lon=Math.Atan2(p.Y,p.X)*180/Math.PI;
            string coordinates=string.Format(CultureInfo.InvariantCulture,"{0:F2}°{1}, {2:F2}°{3}",Math.Abs(lat),lat<0?"S":"N",Math.Abs(lon),lon<0?"W":"E");
            int region=world.Ecology?.Region[cell]??-1;
            return coordinates+(region>=0?" · "+world.Ecology!.Regions[region].Name+" · "+world.Ecology.Habitat[cell].Biome:world.Hydrology.OceanConnected[cell]?" · island refuge":"");
        }
        static void AppendCultureGazetteer(StringBuilder b,PrehistoryState s,World? world,int epoch)
        {
            var h=s.History;b.AppendLine("## Peoples and changing traditions");
            foreach(var culture in s.Society.Cultures)
            {
                var knowledge=h.Cultures[culture.Id];b.AppendLine().AppendLine($"### {culture.Name} [people {culture.Id}]");
                b.AppendLine("Their recorded regional tradition dates to year "+SimulationClockView.YearNumber(culture.FoundedYear,epoch)+" at "+GazetteerLocation(world,culture.OriginCell)+". This is an ancestral location, not a claim that they still control it.");
                b.AppendLine($"{Stage(culture.Stage)}; {culture.Population:N0} recorded people and {culture.AreaKm2:N0} km² of mapped local identity.");b.AppendLine("Way of life: "+culture.Subsistence+". Local resources: "+culture.Resources+".");
                if(knowledge.ParentCultureId>=0)b.AppendLine("Early exchange and inherited practices: "+s.Society.Cultures[knowledge.ParentCultureId].Name+" [people "+knowledge.ParentCultureId+"].");
                b.AppendLine("Cultivated crops: "+HistoricalCrops.Description(h.Agriculture[culture.Id])+".");
                if(knowledge.ReligionId>=0)b.AppendLine("Recorded religious tradition: "+h.Religions[knowledge.ReligionId].Name+".");
                for(int k=0;k<knowledge.Technology.Length;k++)if(knowledge.Technology[k]>0)b.AppendLine("- Year "+SimulationClockView.YearNumber(knowledge.Technology[k]-1,epoch)+": knowledge of "+HistoricalTechnologies.Name(h.Version,k)+"; actual installed practice is tracked separately.");
                var continuity=h.Aggregates?.Empires?.Connections?.Cultures[culture.Id];
                if(continuity!=null)
                {
                    if(continuity.ColonialSponsor>=0)b.AppendLine("Founding overseas sponsor: "+h.Polities[continuity.ColonialSponsor].Name+".");
                    if(continuity.InstitutionalOriginCulture>=0)b.AppendLine("Recorded institutional origin: "+s.Society.Cultures[continuity.InstitutionalOriginCulture].Name+". This does not imply identical culture or permanent political unity.");
                    foreach(var episode in continuity.Episodes)b.AppendLine("- Years "+SimulationClockView.YearNumber(episode.Since,epoch)+"–"+SimulationClockView.YearNumber(episode.Until,epoch)+": "+episode.Name+"; "+episode.Reason+".");
                }
            }
        }
        static void AppendGazetteer(StringBuilder b,PrehistoryState s,World? world)
        {
            var h=s.History;var towns=s.Society.Settlements;
            if(h.Dynamics!=null)
            {
                b.AppendLine().AppendLine("## Notable and current leaders");
                b.AppendLine("Abilities are simulation priors; historical importance comes from recorded deeds, not a guaranteed great-person title. Routine reigns remain in the saved records and event archive.");
                foreach(var leader in h.Dynamics.Leaders.Where(l=>l.EndedYear<0||l.Renown>=150||l.Reforms>=3||l.Victories>=5).OrderByDescending(l=>l.Renown).ThenBy(l=>l.Id))
                {
                    b.AppendLine().AppendLine($"### {leader.Name} [leader {leader.Id}]");
                    b.AppendLine($"State: {h.Polities[leader.PolityId].Name}; reign began in year {SimulationClockView.YearNumber(leader.StartedYear,SimulationClockView.EpochYear(s))}; "+(leader.EndedYear<0?"still ruling.":$"ended in year {SimulationClockView.YearNumber(leader.EndedYear,SimulationClockView.EpochYear(s))}."));
                    b.AppendLine($"Military {leader.Military/10}%; administration {leader.Administration/10}%; diplomacy {leader.Diplomacy/10}%; ambition {leader.Ambition/10}%.");
                    b.AppendLine($"Recorded deeds: {leader.Victories} victories or conquests; {leader.Defeats} defeats; {leader.Reforms} public-work reforms. "+leader.Legacy);
                    if(!string.IsNullOrEmpty(leader.Selection))b.AppendLine("Selection: "+leader.Selection+(leader.PredecessorId>=0?". Predecessor: "+h.Dynamics.Leaders[leader.PredecessorId].Name+" [leader "+leader.PredecessorId+"]":""));
                }
                b.AppendLine().AppendLine("## Military campaigns");
                foreach(var c in h.Dynamics.Campaigns)
                {
                    b.AppendLine().AppendLine($"### {c.Name} [campaign {c.Id}]");
                    b.AppendLine($"Year {SimulationClockView.YearNumber(c.StartedYear,SimulationClockView.EpochYear(s))} — "+(c.EndedYear<0?"ongoing":$"year {SimulationClockView.YearNumber(c.EndedYear,SimulationClockView.EpochYear(s))}")+"; "+h.Polities[c.A].Name+" versus "+h.Polities[c.B].Name+".");
                    b.AppendLine("Initial commander: "+h.Dynamics.Leaders[c.LeaderId].Name+". Objective: "+c.Objective+". Outcome: "+c.Outcome);
                    if(!string.IsNullOrEmpty(c.Context))b.AppendLine("Recorded campaign context: "+c.Context);
                    b.AppendLine($"Recorded levy casualties: {c.CasualtiesA:N0} / {c.CasualtiesB:N0}. Settlement captures: {c.Conquests:N0}.");
                    b.AppendLine($"Repulsed assaults: {c.Repulses:N0}; captured state funds: {c.Spoils:N0} abstract account units.");
                    if(c.TributePaid>0)b.AppendLine($"One-time peace payment: {c.TributePaid:N0} account units to {h.Polities[c.TributeRecipientId].Name}. This is not an ongoing vassal obligation.");
                    if(c.CapturedSettlements.Length>0)b.AppendLine("Captured sites (in sequence): "+string.Join(", ",c.CapturedSettlements.Select(id=>towns[id].Name+" [site "+id+"]")));
                }
                if(h.Dynamics.Dependencies?.Length>0)
                {
                    b.AppendLine().AppendLine("## Client courts and tributary obligations");
                    b.AppendLine("Local administration and territory remain with the client; these are political obligations, not a merger of peoples or a complete commodity economy.");
                    foreach(var dependency in h.Dynamics.Dependencies)
                    {
                        b.AppendLine().AppendLine($"### {h.Polities[dependency.ClientId].Name} under {h.Polities[dependency.PatronId].Name} [dependency {dependency.Id}]");
                        b.AppendLine($"Began year {SimulationClockView.YearNumber(dependency.StartedYear,SimulationClockView.EpochYear(s))}; "+(dependency.EndedYear<0?"still in force.":$"ended year {SimulationClockView.YearNumber(dependency.EndedYear,SimulationClockView.EpochYear(s))}. "+dependency.Ending));
                        b.AppendLine($"Origin: {(dependency.Imposed?"negotiated after military defeat":"a protection agreement")}. Annual quota {dependency.AnnualQuota:N0}; recorded transfers {dependency.TotalPaid:N0} abstract account units; unpaid-demand years {dependency.ArrearsYears}.");
                    }
                }
            }
            var milestones=new List<HistoricalEvent>[towns.Length];
            foreach(var e in h.Archive)if(e.SettlementId>=0&&e.SettlementId<towns.Length&&e.Kind!="epidemic")
                (milestones[e.SettlementId]??(milestones[e.SettlementId]=new List<HistoricalEvent>())).Add(e);
            b.AppendLine().AppendLine("## Settlement gazetteer");
            b.AppendLine("Coordinates locate coarse settlement sites, not surveyed streets or finalized local terrain. These records describe the saved year; the archive retains prior events.");
            foreach(var t in towns.OrderByDescending(t=>t.Population).ThenBy(t=>t.Id))
            {
                int faith=HistoricalCells.Religion(s,t.Cell);
                b.AppendLine().AppendLine($"### {t.Name} [site {t.Id}]");
                b.AppendLine($"Founded: year {SimulationClockView.YearNumber(t.FoundedYear,SimulationClockView.EpochYear(s))}. Current residents: {t.Population:N0}"+(t.Population<100?" (small or declined settlement).":"."));
                b.AppendLine("Location: "+GazetteerLocation(world,t.Cell));
                b.AppendLine("People: "+s.Society.Cultures[t.CultureId].Name+". Political owner: "+(t.PolityId<0?"no centralized state":h.Polities[t.PolityId].Name+" [state "+t.PolityId+"]")+".");
                b.AppendLine("Local religious tradition: "+(faith<0?"not recorded":h.Religions[faith].Name)+".");
                b.AppendLine($"Modeled food capacity: {t.FoodCapacity:N0} people; reserve: {t.FoodReserve:N0} person-years. Health: {t.Health/10}%; stability: {t.Stability/10}%.");
                if(h.Version=="history-6"||h.Version=="history-7"||h.Version=="history-8"||h.Version=="history-9"||h.Version=="history-10"||(h.Version=="history-11"||(h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||h.Version=="history-16"))))))
                {
                    var record=h.Towns[t.Id];b.AppendLine($"Recorded inter-town provisioning: imports {record.ImportedFood:N0}, exports {record.ExportedFood:N0}, transit loss {record.FoodTransportLoss:N0} person-years; {record.ShortageYears} years of substantial shortfall.");
                    if(record.Demography!=null){var d=record.Demography;b.AppendLine($"Last annual demographic balance: natural change {d.NaturalChange:N0}, rural arrivals {d.RuralInflow:N0}, rural departures {d.RuralOutflow:N0}, chronic crowding losses {d.ChronicLoss:N0}. Lifetime arrivals {d.TotalRuralInflow:N0} and departures {d.TotalRuralOutflow:N0}. These are modeled flows, not census records.");}
                    if(record.LastHarvestHazard!=HarvestHazard.None)b.AppendLine($"Last recorded harvest shock: {record.LastHarvestHazard}, year {SimulationClockView.YearNumber(record.LastHarvestHazardYear,SimulationClockView.EpochYear(s))}.");
                    if(record.DiseaseKind!=HistoricalDiseaseKind.None)
                    {
                        b.AppendLine("Last recorded syndrome: "+HistoricalDiseaseEffects.Names[(int)record.DiseaseKind]+$"; {record.DiseaseDeaths:N0} cumulative modeled disease deaths across the site's history.");
                        if(record.DiseaseWaveId>0){var origin=h.Archive[record.DiseaseWaveId-1];b.AppendLine($"Outbreak lineage: year {SimulationClockView.YearNumber(origin.Year,SimulationClockView.EpochYear(s))}, "+(origin.SettlementId>=0?towns[origin.SettlementId].Name+" [site "+origin.SettlementId+"]":"unknown originating site")+".");}
                    }
                }
                if(milestones[t.Id]!=null)foreach(var e in milestones[t.Id].Skip(Math.Max(0,milestones[t.Id].Count-8)))b.AppendLine($"- Year {SimulationClockView.YearNumber(e.Year,SimulationClockView.EpochYear(s))}: {e.Text}");
            }
            var faithCells=new int[h.Religions.Length];foreach(int religion in Enumerable.Range(0,HistoricalCells.Count(s)).Select(cell=>HistoricalCells.Religion(s,cell)))if(religion>=0)faithCells[religion]++;
            b.AppendLine().AppendLine("## Religious traditions");
            b.AppendLine("The map stores the dominant local tradition, not a census of every believer or minority community.");
            foreach(var r in h.Religions.OrderByDescending(r=>faithCells[r.Id]).ThenBy(r=>r.Id))
            {
                b.AppendLine().AppendLine($"### {r.Name} [tradition {r.Id}]");
                b.AppendLine($"Founded: year {SimulationClockView.YearNumber(r.FoundedYear,SimulationClockView.EpochYear(s))}; originating people: {s.Society.Cultures[r.CultureId].Name}.");
                b.AppendLine("Lineage: "+(r.ParentId<0?"local ancestral tradition":h.Religions[r.ParentId].Name+" [tradition "+r.ParentId+"]"));
                b.AppendLine("Holy place: "+GazetteerLocation(world,r.HolyCell));
                b.AppendLine($"Currently dominant in {faithCells[r.Id]:N0} map cells.");
                b.AppendLine(r.Tradition).AppendLine("Ethos: "+r.Ethos).AppendLine("Practices: "+r.Ritual);
                if(h.Version=="history-6"||h.Version=="history-7"||h.Version=="history-8"||h.Version=="history-9"||h.Version=="history-10"||(h.Version=="history-11"||(h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||h.Version=="history-16"))))))b.AppendLine("Transmission tradition: "+r.Reach+". Dominance reflects sustained contact, not an automatic change after conquest.");
            }
        }
    }
}
