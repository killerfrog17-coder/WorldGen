using System;
using System.Linq;
using System.Text;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    [Serializable] public sealed class HistoryLegendEntry
    {
        public string Name="",LabelName=""; public int Cell,ColorId,LabelCell=-1; public long Population,AreaKm2;public bool EraColor;
    }
    public static partial class SocietyView
    {
        // Color identity and label identity are different: one ruling culture
        // can govern several independent states with separate names/borders.
        public static HistoryLegendEntry[] MapLabels(World w,PrehistoryState? s,HistoryOverlay overlay,Prehistory? engine=null)
        {
            if(s?.History==null||s.Society==null)return Legend(s,overlay,engine:engine);
            if(overlay!=HistoryOverlay.Civilizations&&overlay!=HistoryOverlay.States&&overlay!=HistoryOverlay.Territories)return Legend(s,overlay,engine:engine);
            var labels=Legend(s,overlay==HistoryOverlay.Territories?overlay:HistoryOverlay.States,engine:engine).ToList();
            var groups=new System.Collections.Generic.Dictionary<int,System.Collections.Generic.List<int>>();
            for(int cell=0;cell<HistoricalCells.Count(s);cell++)
            {
                int culture=HistoricalCells.Culture(s,cell);if(culture<0||HistoricalCells.Polity(s,cell)>=0||HistoricalRecognizedClaims.Holder(s,cell)>=0||w.Hydrology.OceanConnected[cell])continue;
                if(!groups.TryGetValue(culture,out var cells))groups[culture]=cells=new System.Collections.Generic.List<int>();cells.Add(cell);
            }
            foreach(var group in groups.OrderBy(g=>g.Key))
            {
                V center=new V();foreach(int cell in group.Value)center+=w.Surface.Cells[cell].Position*(w.Surface.Cells[cell].AreaM2/1e6);
                var unit=center.Unit;int site=group.Value.OrderByDescending(cell=>V.Dot(w.Surface.Cells[cell].Position,unit)).ThenBy(cell=>cell).First();
                var c=s.Society.Cultures[group.Key];labels.Add(new HistoryLegendEntry{Name=c.Name,LabelName=c.Name,Cell=site,LabelCell=site,ColorId=c.Id,Population=c.Population,AreaKm2=c.AreaKm2});
            }
            return labels.ToArray();
        }
        public static HistoryLegendEntry[] Legend(PrehistoryState? s,HistoryOverlay overlay,string sort="Default",Prehistory? engine=null)
        {
            if(s?.Society==null)return Array.Empty<HistoryLegendEntry>();
            var h=s.History;System.Collections.Generic.IEnumerable<HistoryLegendEntry> entries;
            var faithSites=new System.Collections.Generic.Dictionary<int,int>();
            var faithPopulation=new long[h?.Religions.Length??0];var faithArea=new long[faithPopulation.Length];
            var civilizationSites=new System.Collections.Generic.Dictionary<int,int>();
            if(h!=null&&overlay==HistoryOverlay.Civilizations)for(int cell=0;cell<HistoricalCells.Count(s);cell++){int culture=OverlayId(s,cell,overlay);if(culture>=0&&!civilizationSites.ContainsKey(culture))civilizationSites.Add(culture,cell);}
            if(h!=null&&engine!=null&&overlay==HistoryOverlay.Religions)
            {
                var w=engine.World;
                for(int cell=0;cell<HistoricalCells.Count(s);cell++){int faith=HistoricalCells.Religion(s,cell);if(faith<0)continue;faithPopulation[faith]+=engine.RuralCellPopulation(s,cell);double area=w.Hydrology.OceanConnected[cell]?(SubcellGeography.Find(w,cell)?.LargestIslandAreaM2??0):w.Surface.Cells[cell].AreaM2*(1-w.Hydrology.LakeCoverageFraction[cell]);faithArea[faith]+=(long)Math.Round(area/1e6);}
                foreach(var town in s.Society.Settlements){int faith=HistoricalCells.Religion(s,town.Cell);if(faith>=0)faithPopulation[faith]+=town.Population;}
            }
            if(h!=null&&overlay==HistoryOverlay.Religions)for(int cell=0;cell<HistoricalCells.Count(s);cell++){int faith=HistoricalCells.Religion(s,cell);if(faith>=0&&!faithSites.ContainsKey(faith))faithSites.Add(faith,cell);}
            if(h?.Aggregates?.Empires?.Territories!=null&&overlay==HistoryOverlay.Provinces)
            {
                var administration=h.Aggregates.Empires.Territories;
                entries=Enumerable.Range(0,administration.DistrictAreas.Length).Where(i=>administration.DistrictAreas[i]>0&&h.Aggregates.Resolve(s.Society.Settlements[i].PolityId,h)>=0).Select(i=>new HistoryLegendEntry{Name=HistoricalProvinceIdentity.Name(s,i),Cell=s.Society.Settlements[i].Cell,ColorId=i,Population=s.Society.Settlements[i].Population,AreaKm2=administration.DistrictAreas[i]})
                    .Concat(administration.Posts.Where(p=>p.ClosedYear<0&&p.AreaKm2>0&&h.Aggregates.Resolve(p.PolityId,h)>=0).Select(p=>new HistoryLegendEntry{Name=p.Name,Cell=p.Cell,ColorId=HistoricalProvinceIdentity.PostIdentityOffset+p.Id,AreaKm2=p.AreaKm2}));
            }
            else if(h!=null&&(overlay==HistoryOverlay.States||overlay==HistoryOverlay.Territories))entries=h.Polities.Where(p=>p.EndedYear<0&&(overlay==HistoryOverlay.Territories||HistoricalImperialHierarchy.Patron(s,p.Id)<0)).Select(p=>new HistoryLegendEntry{Name=p.Name,Cell=s.Society.Settlements[p.CapitalId].Cell,ColorId=overlay==HistoryOverlay.Territories?p.Id:p.CultureId,Population=overlay==HistoryOverlay.Territories?p.Population:HistoricalImperialHierarchy.Population(s,p.Id),AreaKm2=overlay==HistoryOverlay.Territories?p.AreaKm2:HistoricalImperialHierarchy.AreaKm2(s,p.Id)});
            else if(h!=null&&overlay==HistoryOverlay.Religions)entries=h.Religions.Where(r=>faithSites.ContainsKey(r.Id)).Select(r=>new HistoryLegendEntry{Name=r.Name,Cell=HistoricalCells.Religion(s,r.HolyCell)==r.Id?r.HolyCell:faithSites[r.Id],ColorId=r.Id,Population=faithPopulation[r.Id],AreaKm2=faithArea[r.Id]});
            else if(h!=null&&overlay==HistoryOverlay.Technology){var display=HistoricalMapSnapshot.For(s);entries=h.Cultures.GroupBy(c=>c.Technical!=null?WorldEraView.KnowledgeEra(c):HistoricalImperialHierarchy.InstalledPractices(c)).Where(g=>display.TechnologySite(g.Key)>=0).OrderByDescending(g=>g.Key).Select(g=>new HistoryLegendEntry{Name=h.Aggregates?.Empires!=null?WorldEraView.Name(g.Key)+" knowledge":g.Key+" adopted practices",ColorId=g.Key,EraColor=h.Aggregates?.Empires!=null,Cell=display.TechnologySite(g.Key),Population=g.Sum(c=>s.Society.Cultures[c.Id].Population),AreaKm2=g.Sum(c=>s.Society.Cultures[c.Id].AreaKm2)});}
            else if(h?.Aggregates?.Empires?.Connections!=null&&overlay==HistoryOverlay.Civilizations)entries=h.Polities.Where(p=>p.EndedYear<0&&HistoricalImperialHierarchy.Patron(s,p.Id)<0).GroupBy(p=>HistoricalRecognizedClaims.Civilization(s,p.CultureId)).Where(g=>civilizationSites.ContainsKey(g.Key)).Select(g=>new HistoryLegendEntry{Name=s.Society.Cultures[g.Key].Name,Cell=civilizationSites[g.Key],ColorId=g.Key,Population=g.Sum(p=>HistoricalImperialHierarchy.Population(s,p.Id)),AreaKm2=g.Sum(p=>HistoricalImperialHierarchy.AreaKm2(s,p.Id))});
            else entries=s.Society.Cultures.Where(c=>overlay==HistoryOverlay.Cultures||(h==null?c.Stage==SocietyStage.Civilization:civilizationSites.ContainsKey(c.Id))).Select(c=>new HistoryLegendEntry{Name=c.Name,Cell=overlay==HistoryOverlay.Civilizations&&h!=null?(OverlayId(s,c.OriginCell,overlay)==c.Id?c.OriginCell:civilizationSites[c.Id]):c.OriginCell,ColorId=c.Id,Population=overlay==HistoryOverlay.Civilizations&&h!=null?h.Polities.Where(p=>p.EndedYear<0&&p.CultureId==c.Id&&HistoricalImperialHierarchy.Patron(s,p.Id)<0).Sum(p=>HistoricalImperialHierarchy.Population(s,p.Id)):c.Population,AreaKm2=overlay==HistoryOverlay.Civilizations&&h!=null?h.Polities.Where(p=>p.EndedYear<0&&p.CultureId==c.Id&&HistoricalImperialHierarchy.Patron(s,p.Id)<0).Sum(p=>HistoricalImperialHierarchy.AreaKm2(s,p.Id)):c.AreaKm2});
            var ranked=entries.OrderByDescending(e=>sort=="Population"?e.Population:sort=="Land area"?e.AreaKm2:overlay==HistoryOverlay.Technology?e.ColorId:e.Population+Math.Sqrt(e.AreaKm2)*100).ThenBy(e=>e.ColorId).ToArray();
            if(engine!=null&&h!=null&&(overlay==HistoryOverlay.States||overlay==HistoryOverlay.Civilizations||overlay==HistoryOverlay.Territories))
            {
                var sites=PoliticalLabelSites(engine.World,s,overlay);
                foreach(var entry in ranked){int owner=OverlayId(s,entry.Cell,overlay);if(sites.TryGetValue(owner,out int site))entry.LabelCell=site;entry.LabelName=entry.Name.EndsWith(" State",StringComparison.Ordinal)?entry.Name.Substring(0,entry.Name.Length-6):entry.Name;}
            }
            return ranked;
        }
        static System.Collections.Generic.Dictionary<int,int> PoliticalLabelSites(World w,PrehistoryState s,HistoryOverlay overlay)
        {
            var centers=new System.Collections.Generic.Dictionary<int,V>();
            for(int cell=0;cell<HistoricalCells.Count(s);cell++){int owner=OverlayId(s,cell,overlay);if(owner<0)continue;var p=w.Surface.Cells[cell];centers[owner]=(centers.TryGetValue(owner,out V old)?old:new V())+p.Position*(p.AreaM2/1e6);}
            var sites=new System.Collections.Generic.Dictionary<int,int>();var scores=new System.Collections.Generic.Dictionary<int,double>();
            for(int cell=0;cell<HistoricalCells.Count(s);cell++){int owner=OverlayId(s,cell,overlay);if(owner<0)continue;double score=V.Dot(w.Surface.Cells[cell].Position,centers[owner].Unit);if(!scores.TryGetValue(owner,out double best)||score>best){scores[owner]=score;sites[owner]=cell;}}
            return sites;
        }
        public static string HistoricalDescription(PrehistoryState s,int cell,HistoryOverlay overlay,string fallback)
        {
            var h=s.History;var culture=Culture(s,cell);if(h!=null&&culture==null&&cell>=0&&cell<HistoricalCells.Count(s)&&HistoricalCells.Polity(s,cell)>=0)culture=s.Society.Cultures[h.Polities[HistoricalCells.Polity(s,cell)].CultureId];if(h==null)return fallback;
            if(culture==null)
            {
                int localFaith=cell>=0&&cell<HistoricalCells.Count(s)?HistoricalCells.Religion(s,cell):-1;
                if(overlay!=HistoryOverlay.Religions||localFaith<0)return fallback;
                var faithRecord=h.Religions[localFaith];var faithText=new StringBuilder().AppendLine(faithRecord.Name).AppendLine(faithRecord.Tradition).AppendLine("Belief: "+faithRecord.Ethos).AppendLine("Observance: "+faithRecord.Ritual).AppendLine("Followers here need not belong to a named regional culture or state.").AppendLine().AppendLine("LOCAL CHRONICLE");
                foreach(var record in RecentEvents(h,e=>e.Cell==cell||e.ReligionId==localFaith).Reverse())faithText.AppendLine($"{SimulationClockView.Year(s,record.Year)}: {record.Text}");
                return faithText.ToString().TrimEnd();
            }
            if(overlay==HistoryOverlay.Civilizations&&HistoricalCells.Polity(s,cell)>=0)culture=s.Society.Cultures[h.Polities[HistoricalImperialHierarchy.Sovereign(s,HistoricalCells.Polity(s,cell))].CultureId];
            int id=HistoricalCells.Polity(s,cell),religion=HistoricalCells.Religion(s,cell);var hc=h.Cultures[culture.Id];
            int localId=id;if(id>=0&&(overlay==HistoryOverlay.States||overlay==HistoryOverlay.Civilizations))id=HistoricalImperialHierarchy.Sovereign(s,id);
            var b=new StringBuilder();
            if(overlay==HistoryOverlay.Provinces&&HistoricalProvinceIdentity.Of(s,cell)>=0)b.AppendLine(HistoricalProvinceIdentity.Name(s,HistoricalProvinceIdentity.Of(s,cell)));
            else if((overlay==HistoryOverlay.States||overlay==HistoryOverlay.Territories)&&id>=0)b.AppendLine(h.Polities[id].Name);
            else if(overlay==HistoryOverlay.Religions&&religion>=0)b.AppendLine(h.Religions[religion].Name);
            else b.AppendLine(culture.Name);
            if(localId>=0&&localId!=HistoricalImperialHierarchy.Sovereign(s,localId))b.AppendLine("Local government: "+h.Polities[localId].Name+" · Imperial allegiance: "+h.Polities[HistoricalImperialHierarchy.Sovereign(s,localId)].Name);
            b.AppendLine(HistoricalCells.Stage(s,cell)>=3?"Urban state hinterland":HistoricalCells.Stage(s,cell)>=2?"Regional settlement center":"Decentralized regional culture");
            string subsistence=(h.Version=="history-3"||h.Version=="history-4"||h.Version=="history-5"||h.Version=="history-6"||h.Version=="history-7"||h.Version=="history-8"||(h.Version=="history-9"||(h.Version=="history-10"||(h.Version=="history-11"||(h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||(h.Version=="history-16"||h.Aggregates!=null)))))))))&&culture.Farming>0?(culture.Farming>200?"Crop cultivation":"Early crop tending")+(culture.Fishing>=300?" with fishing":" with wild-food gathering"):culture.Subsistence;
            b.AppendLine(subsistence).AppendLine("Resources: "+culture.Resources);
            if((h.Version=="history-3"||h.Version=="history-4"||h.Version=="history-5"||h.Version=="history-6"||h.Version=="history-7"||h.Version=="history-8"||(h.Version=="history-9"||(h.Version=="history-10"||(h.Version=="history-11"||(h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||(h.Version=="history-16"||h.Aggregates!=null)))))))))&&h.Agriculture!=null)
            {
                var crops=h.Agriculture[culture.Id];b.AppendLine("CROP SYSTEMS");
                if(!crops.AdoptedYear.Any(y=>y>0))b.AppendLine("No established domestic crops");
                for(int k=0;k<HistoricalCrops.Names.Length;k++)if(crops.AdoptedYear[k]>0)
                    b.AppendLine(HistoricalCrops.Names[k]+" · year "+SimulationClockView.YearNumber(crops.AdoptedYear[k]-1,SimulationClockView.EpochYear(s))+" · "+(crops.SourceCulture[k]<0?"local domestication":"introduced through "+s.Society.Cultures[crops.SourceCulture[k]].Name));
                foreach(var animal in crops.Animals??Array.Empty<HistoricalAnimalHusbandry>())if(animal.AdoptedYear>0)b.AppendLine("Managed animals: "+animal.Name+" · year "+SimulationClockView.YearNumber(animal.AdoptedYear-1,SimulationClockView.EpochYear(s))+" · "+(animal.SourceCulture<0?"local breeding tradition":"introduced through "+s.Society.Cultures[animal.SourceCulture].Name));
            }
            b.AppendLine("Language family: "+s.Society.Cultures[hc.LanguageId].Name);
            if(hc.ParentCultureId>=0)b.AppendLine("Community ancestry: "+s.Society.Cultures[hc.ParentCultureId].Name);
            if(id>=0)
            {
                var p=h.Polities[id];b.AppendLine().AppendLine(p.Name+" · "+p.Government).AppendLine("Founded: "+SimulationClockView.Year(s,p.FoundedYear)).AppendLine("Capital: "+s.Society.Settlements[p.CapitalId].Name).AppendLine("Ruler: "+p.Ruler).AppendLine($"{p.Population:N0} people · {p.AreaKm2:N0} km²").AppendLine($"Cohesion {p.Cohesion/10}% · legitimacy {p.Legitimacy/10}%").AppendLine($"Army levy {p.Army:N0} · treasury {p.Treasury:N0}");
                if(h.Dynamics!=null)
                {
                    var ds=h.Dynamics.States[id];var leader=h.Dynamics.Leaders[ds.LeaderId];
                    b.AppendLine("Institutional tradition: "+ds.Tradition).AppendLine($"Administrative strain {ds.Strain/10}% · public works {ds.PublicWorks/10}%");
                    b.AppendLine($"Leadership: military {leader.Military/10}% · administration {leader.Administration/10}% · diplomacy {leader.Diplomacy/10}% · ambition {leader.Ambition/10}%");
                    b.AppendLine($"This reign: {leader.Victories} victories or conquests · {leader.Defeats} defeats · {leader.Reforms} public-work reforms");
                    if(!string.IsNullOrEmpty(leader.Selection))b.AppendLine("Selection: "+leader.Selection+(leader.PredecessorId>=0?" · predecessor "+h.Dynamics.Leaders[leader.PredecessorId].Name:""));
                    if(ds.DisputedUntil>s.Years)b.AppendLine("Contested transition · institutional recovery is incomplete");
                    if(ds.Cycle!=null)b.AppendLine("Political pressures: elite capture "+ds.Cycle.EliteCapture/10+"% · unconsolidated conquest "+ds.Cycle.ConquestDebt/10+"%. These are model indices, not probabilities of collapse.");
                    foreach(var campaign in h.Dynamics.Campaigns.Where(c=>c.EndedYear<0&&(c.A==id||c.B==id)))b.AppendLine(campaign.Name+" · "+campaign.Objective+" · "+campaign.Conquests+" conquests");
                    foreach(var dependency in h.Dynamics.Dependencies.Where(d=>d.EndedYear<0&&(d.PatronId==id||d.ClientId==id)))b.AppendLine(dependency.ClientId==id?"Tributary obligations to "+h.Polities[dependency.PatronId].Name+" · local court retained":"Protected or tributary court: "+h.Polities[dependency.ClientId].Name);
                }
                foreach(var r in h.Relations.Where(r=>r.A==id||r.B==id)){var other=h.Polities[r.A==id?r.B:r.A];if(other.EndedYear>=0)continue;b.AppendLine((r.WarSince>=0?"At war with ":r.Alliance?"Allied with ":r.Trade?"Trading with ":"In contact with ")+other.Name);}
            }
            else b.AppendLine("No centralized state controls this location.");
            if(religion>=0){var r=h.Religions[religion];b.AppendLine().AppendLine(r.Name).AppendLine(r.Tradition).AppendLine(r.Ethos).AppendLine(r.Ritual);if(h.Version=="history-6"||h.Version=="history-7"||h.Version=="history-8"||(h.Version=="history-9"||(h.Version=="history-10"||(h.Version=="history-11"||(h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||(h.Version=="history-16"||h.Aggregates!=null)))))))))b.AppendLine("Transmission: "+(r.Reach==ReligiousReach.Missionary?"traveling teachers and exchange institutions":r.Reach==ReligiousReach.Portable?"portable communal and ancestral practices":"local places and ancestral communities"));}
            b.AppendLine().AppendLine("Practices: "+string.Join(", ",hc.Technology.Select((year,k)=>(year,k)).Where(x=>x.year>0).Select(x=>HistoricalTechnologies.Name(h.Version,x.k))));
            if(overlay==HistoryOverlay.Technology)
            {
                b.AppendLine().AppendLine("ADOPTION TIMELINE");
                foreach(var item in hc.Technology.Select((year,k)=>(year,k)).Where(x=>x.year>0).OrderBy(x=>x.year).ThenBy(x=>x.k))
                    b.AppendLine("Year "+SimulationClockView.YearNumber(item.year-1,SimulationClockView.EpochYear(s))+": "+HistoricalTechnologies.Name(h.Version,item.k));
                b.AppendLine("Stored surplus "+hc.Surplus/10+"% · institutions "+hc.Institution/10+"%");
            }
            var town=s.Society.Settlements.FirstOrDefault(t=>t.Cell==cell);if(town!=null)b.AppendLine().AppendLine(town.Name+$" · {town.Population:N0} residents").AppendLine($"Food capacity {town.FoodCapacity:N0}; reserve {town.FoodReserve:N0} person-years").AppendLine($"Health {town.Health/10}% · stability {town.Stability/10}%");
            if(town!=null&&(h.Version=="history-6"||h.Version=="history-7"||h.Version=="history-8"||(h.Version=="history-9"||(h.Version=="history-10"||(h.Version=="history-11"||(h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||(h.Version=="history-16"||h.Aggregates!=null))))))))))
            {
                var record=h.Towns[town.Id];b.AppendLine($"Recorded provisioning: {record.ImportedFood:N0} imported, {record.ExportedFood:N0} exported, {record.FoodTransportLoss:N0} lost in transit (person-years)");
                if(record.LastHarvestHazard!=HarvestHazard.None)b.AppendLine($"Last recorded crop shock: {record.LastHarvestHazard} · year {SimulationClockView.YearNumber(record.LastHarvestHazardYear,SimulationClockView.EpochYear(s))}");
                if(record.DiseaseKind!=HistoricalDiseaseKind.None)b.AppendLine(HistoricalDiseaseEffects.Names[(int)record.DiseaseKind]+(record.DiseaseUntil>=s.Years?" · active outbreak":" · last recorded outbreak")+$" · {record.DiseaseDeaths:N0} lifetime recorded disease deaths");
            }
            b.AppendLine().AppendLine("LOCAL CHRONICLE");foreach(var e in RecentEvents(h,e=>e.Cell==cell||id>=0&&(e.PolityId==id||e.OtherPolityId==id)||overlay==HistoryOverlay.Religions&&religion>=0&&e.ReligionId==religion).Reverse())b.AppendLine($"{SimulationClockView.Year(s,e.Year)}: {e.Text}");
            return b.ToString().TrimEnd();
        }
        static System.Collections.Generic.IEnumerable<HistoricalEvent> RecentEvents(HistoryState h,Func<HistoricalEvent,bool> matches)
        {
            int found=0;for(int i=h.Archive.Count-1;i>=0&&found<12;i--)if(matches(h.Archive[i])){found++;yield return h.Archive[i];}
        }
        public static string CampaignLore(PrehistoryState s,World? world=null)
        {
            if(s?.History==null)return "This save predates the historical simulation.";
            int epoch=SimulationClockView.EpochYear(s);var b=new StringBuilder("# World chronicle\n\nYear "+SimulationClockView.YearNumber(s.Years,epoch)+"\n\n");var h=s.History;
            AppendCultureGazetteer(b,s,world,epoch);
            b.AppendLine("## States, including fallen states");foreach(var p in h.Polities)
            {
                b.AppendLine().AppendLine($"### {p.Name} [state {p.Id}]");
                b.AppendLine($"Founded: {SimulationClockView.Year(s,p.FoundedYear)}; "+(p.EndedYear<0?"extant":"ended in "+SimulationClockView.Year(s,p.EndedYear))+"; "+p.Government+".");
                b.AppendLine("Parent state: "+(p.ParentId<0?"none recorded":h.Polities[p.ParentId].Name+" [state "+p.ParentId+"]")+"; "+(p.EndedYear<0?"capital":"last capital")+": "+s.Society.Settlements[p.CapitalId].Name+".");
                b.AppendLine("Political tradition: "+s.Society.Cultures[p.CultureId].Name+"; "+(p.EndedYear<0?"current ruler":"last recorded ruler")+": "+p.Ruler+".");
                if(p.EndedYear<0)b.AppendLine($"Population: {p.Population:N0}; controlled land: {p.AreaKm2:N0} km².");
                if(h.Dynamics!=null){var ds=h.Dynamics.States[p.Id];b.AppendLine("Institutional tradition: "+ds.Tradition).AppendLine($"Recorded peak: {ds.PeakPopulation:N0} people; {ds.PeakAreaKm2:N0} km². Foundation readiness: {ds.FoundationPotential/10}%; a game-model context score, not archaeological probability.");}
            }
            AppendGazetteer(b,s,world);
            b.AppendLine("\n## Complete event archive");foreach(var e in h.Archive)b.AppendLine($"- Year {SimulationClockView.YearNumber(e.Year,epoch)}: {e.Text}");return b.ToString();
        }
    }
}
