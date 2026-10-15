using System.Globalization;
using System.Text;
using WorldGen.Core;
using WorldGen.Presentation;

// Observations for human review, not pass/fail archaeological expectations.
public static class HistoryAuditReview
{
    public static void TraceTown(World world,int townId,int years,string output)
    {
        if(world.Prehistory?.History==null||townId<0||townId>=world.Prehistory.Society.Settlements.Length||years<1||years>1000)throw new ArgumentException("Specify an existing historical town and 1–1000 years.");
        var engine=new Prehistory(world);var state=world.Prehistory;
        var rows=new List<string>{"year,population,foodCapacity,reserve,health,stability,polity,foodBase"};
        for(int year=0;year<=years;year++)
        {
            var town=state.Society.Settlements[townId];var history=state.History.Towns[townId];
            rows.Add(FormattableString.Invariant($"{state.Years},{town.Population},{town.FoodCapacity},{town.FoodReserve},{town.Health},{town.Stability},{town.PolityId},{history.FoodBase}"));
            if(year<years)state=engine.AdvanceYears(state,1);
        }
        engine.Validate(state);File.WriteAllLines(output,rows);
        Console.WriteLine("Town trace: "+output);
    }
    public static void Write(World world,Prehistory engine,PrehistoryState state,string folder)
    {
        if(state?.History==null||state.Society==null)throw new ArgumentException("This saved world has no historical checkpoint to review.");
        var towns=state.Society.Settlements.Where(t=>t.Population>=100).ToArray();
        var report=new StringBuilder("Historical plausibility review\n");
        report.AppendLine($"Year {state.Years}; {towns.Length} inhabited settlement sites; {state.History.Polities.Count(p=>p.EndedYear<0)} active states.");
        report.AppendLine("Flags below are investigation prompts, not proof of an error. Climate and drainage are simulated; town food comes from a catchment, not only its host cell.");
        long global=SocietyView.Population(state),settled=towns.Sum(t=>t.Population);
        report.AppendLine(FormattableString.Invariant($"Residents at named sites (>=100 people): {settled:N0} / {global:N0} ({100.0*settled/Math.Max(1,global):F1}%). This includes villages; it is not an archaeological urbanization rate."));
        var ended=state.History.Polities.Where(p=>p.EndedYear>=0).OrderBy(p=>p.EndedYear-p.FoundedYear).ThenBy(p=>p.Id).ToArray();
        long stateYears=state.History.Polities.Sum(p=>(long)Math.Max(1,(p.EndedYear<0?state.Years:p.EndedYear)-p.FoundedYear));
        int wars=state.History.Archive.Count(e=>e.Kind=="war");
        report.AppendLine($"Political turnover: {state.History.Polities.Length} recorded states; {ended.Length} ended; {ended.Count(p=>p.EndedYear-p.FoundedYear<50)} ended within 50 years.");
        if(state.History.Version=="history-15")report.AppendLine($"Institutional origins: {state.History.Archive.Count(e=>e.Kind=="independent-institutions")} independent foundations; {state.History.Archive.Count(e=>e.Kind=="institutional-diffusion")} locally transmitted foundations. Secessions are separate and not counted as pristine origins. No global origin quota is imposed.");
        if(ended.Length>0)report.AppendLine($"Median lifetime among ended states: {ended[ended.Length/2].EndedYear-ended[ended.Length/2].FoundedYear} years (excludes surviving states).");
        report.AppendLine(FormattableString.Invariant($"Recorded war declarations: {wars}; {1000.0*wars/Math.Max(1,stateYears):F2} per 1,000 recorded state-years. Descriptive only, not a calibrated conflict target."));
        report.AppendLine($"Recorded religious traditions: {state.History.Religions.Length}; current dominant faiths in land records: {state.History.CellReligion.Where(i=>i>=0).Distinct().Count()}.");
        long faithContacts=0,unlikeContacts=0,faithCells=0,isolatedFaithCells=0;
        // Before the first regional culture these maps are intentionally empty.
        // A no-state/no-culture control is a valid result, not a failed audit.
        for(int cell=0;cell<state.History.CellReligion.Length;cell++)
        {
            int faith=state.History.CellReligion[cell];if(faith<0||state.Society.CellCulture[cell]<0)continue;faithCells++;bool supported=false;
            foreach(int next in world.Surface.Cells[cell].Neighbors)
            {
                int other=state.History.CellReligion[next];if(other<0||state.Society.CellCulture[next]<0)continue;
                if(other==faith)supported=true;if(next>cell){faithContacts++;if(other!=faith)unlikeContacts++;}
            }
            if(!supported)isolatedFaithCells++;
        }
        report.AppendLine(FormattableString.Invariant($"Faith-map contact disagreement: {100.0*unlikeContacts/Math.Max(1,faithContacts):F1}% of inhabited adjacent pairs; locally isolated dominant faith: {100.0*isolatedFaithCells/Math.Max(1,faithCells):F1}% of inhabited faith cells. Spatial diagnostics, not a target religious homogeneity."));
        if(state.History.Version=="history-11"||state.History.Version=="history-12"||(state.History.Version=="history-13"||(state.History.Version=="history-14"||state.History.Version=="history-15")))
        {
            var demographic=state.History.Towns.Where(t=>t.Demography!=null).Select(t=>t.Demography).ToArray();
            report.AppendLine($"Lifetime local rural arrivals: {demographic.Sum(d=>d.TotalRuralInflow):N0}; departures: {demographic.Sum(d=>d.TotalRuralOutflow):N0}; chronic crowding losses: {demographic.Sum(d=>d.TotalChronicLoss):N0}. Natural balance is net modeled change, not births alone.");
            var ocean=state.History.OceanSettlements??Array.Empty<HistoricalOceanSettlement>();report.AppendLine($"Recorded remote ocean settlements: {ocean.Length}; founders transferred {ocean.Sum(v=>v.Migrants)}; longest passage {ocean.Select(v=>v.DistanceM/1000).DefaultIfEmpty(0).Max()} km.");
            report.AppendLine($"Inherited provincial offices: {state.History.Administration?.Offices.Count(o=>o.PredecessorPolityId>=0)??0}; ended states retaining a dated last-active estimate: {state.History.Polities.Count(p=>p.EndedYear>=0&&p.LastEstimateYear>0)}.");
        }
        if(state.History.Version=="history-6"||state.History.Version=="history-7"||state.History.Version=="history-8"||state.History.Version=="history-9"||state.History.Version=="history-10"||state.History.Version=="history-11"||state.History.Version=="history-12"||(state.History.Version=="history-13"||(state.History.Version=="history-14"||state.History.Version=="history-15")))
        {
            long exports=state.History.Towns.Sum(t=>t.ExportedFood),imports=state.History.Towns.Sum(t=>t.ImportedFood),loss=state.History.Towns.Sum(t=>t.FoodTransportLoss);
            report.AppendLine($"Provisioning: {exports:N0} sent, {imports:N0} delivered, {loss:N0} transit loss person-years; conserved={exports==imports+loss}.");
            report.AppendLine($"Recorded harvest shocks: {state.History.Archive.Count(e=>e.Kind=="harvest-shock")}; substantial shortfall site-years {state.History.Towns.Sum(t=>(long)t.ShortageYears):N0}; modeled disease deaths {state.History.Towns.Sum(t=>t.DiseaseDeaths):N0}; severe epidemic arrivals {state.History.Archive.Count(e=>e.Kind=="major-epidemic")}.");
            var network=engine.HistoricalTownContacts(state);var degrees=towns.Select(t=>network[t.Id].Length).OrderBy(x=>x).ToArray();
            report.AppendLine($"Regular town-contact degree: mean {(degrees.Length==0?0:degrees.Average()):F2}, maximum {degrees.DefaultIfEmpty(0).Max()}; possible travel is not a fully connected trade network.");
        }
        var dynamics=state.History.Dynamics;
        if(dynamics!=null)
        {
            report.AppendLine($"Leaders: {dynamics.Leaders.Length}; exceptional military ability (>=850): {dynamics.Leaders.Count(l=>l.Military>=850)}; recorded renown >=250: {dynamics.Leaders.Count(l=>l.Renown>=250)}.");
            report.AppendLine("Current governments and legitimacy (not a target government distribution):");
            foreach(var group in state.History.Polities.Where(p=>p.EndedYear<0).GroupBy(p=>p.Government).OrderBy(g=>g.Key,StringComparer.Ordinal))
                report.AppendLine(FormattableString.Invariant($"  {group.Key}: {group.Count()} states; mean legitimacy {group.Average(p=>p.Legitimacy):F0}/1000; {group.Count(p=>p.Legitimacy<350)} below 350."));
            report.AppendLine("Completed leadership tenures by actual appointment method:");
            foreach(var group in dynamics.Leaders.Where(l=>l.EndedYear>=0).GroupBy(l=>l.Selection).OrderBy(g=>g.Key,StringComparer.Ordinal))
                report.AppendLine(FormattableString.Invariant($"  {(string.IsNullOrEmpty(group.Key)?"Unrecorded":group.Key)}: {group.Count()} completed tenures; mean {group.Average(l=>l.EndedYear-l.StartedYear):F1} years."));
            var politics=new List<string>{"state,name,active,government,ageYears,population,areaKm2,cohesion,legitimacy,strain,treasury,army,offices,disconnectedRural,disconnectedUrban"};
            foreach(var p in state.History.Polities.OrderBy(p=>p.Id))
            {
                var account=state.History.Administration?.Accounts.FirstOrDefault(a=>a.PolityId==p.Id);bool active=p.EndedYear<0;
                politics.Add(FormattableString.Invariant($"{p.Id},{p.Name.Replace(',',';')},{active},{p.Government.Replace(',',';')},{(active?state.Years:p.EndedYear)-p.FoundedYear},{p.Population},{p.AreaKm2},{p.Cohesion},{p.Legitimacy},{dynamics.States[p.Id].Strain},{p.Treasury},{p.Army},{(active?state.History.Administration?.Offices.Count(o=>o.PolityId==p.Id&&o.EndedYear<0)??0:0)},{(active?account?.DisconnectedResidents??0:0)},{(active?account?.DisconnectedUrbanResidents??0:0)}"));
            }
            File.WriteAllLines(Path.Combine(folder,"state-pressures.csv"),politics);
            report.AppendLine($"Campaigns: {dynamics.Campaigns.Length}; with captures: {dynamics.Campaigns.Count(c=>c.Conquests>0)}; maximum captures in one campaign: {dynamics.Campaigns.Select(c=>c.Conquests).DefaultIfEmpty().Max()}; secessions: {state.History.Archive.Count(e=>e.Kind=="secession")}; contested successions: {state.History.Archive.Count(e=>e.Kind=="succession-crisis")}.");
            report.AppendLine($"One-time tribute settlements: {dynamics.Campaigns.Count(c=>c.TributePaid>0)}; with no annexed settlement: {dynamics.Campaigns.Count(c=>c.TributePaid>0&&c.Conquests==0)}.");
            if(dynamics.Dependencies!=null)
            {
                report.AppendLine($"Tributary dependencies: {dynamics.Dependencies.Length} recorded; {dynamics.Dependencies.Count(d=>d.EndedYear<0)} active; {state.History.Archive.Count(e=>e.Kind=="tributary-revolt")} breakaway attempts; {dynamics.Dependencies.Sum(d=>d.TotalPaid):N0} account units transferred.");
                var dependencies=new List<string>{"id,patron,client,start,end,annualQuota,totalPaid,arrearsYears,imposed,ending"};
                foreach(var d in dynamics.Dependencies)dependencies.Add($"{d.Id},{d.PatronId},{d.ClientId},{d.StartedYear},{d.EndedYear},{d.AnnualQuota},{d.TotalPaid},{d.ArrearsYears},{d.Imposed},{d.Ending.Replace(',',';')}");
                File.WriteAllLines(Path.Combine(folder,"dependencies.csv"),dependencies);
            }
            foreach(var group in dynamics.Campaigns.GroupBy(c=>c.Aim).OrderBy(g=>g.Key))report.AppendLine($"  Campaign aim {group.Key}: {group.Count()}");
            report.AppendLine("Institutional traditions (recorded states, not only survivors):");
            foreach(var group in dynamics.States.GroupBy(d=>d.Tradition).OrderBy(g=>g.Key,StringComparer.Ordinal))report.AppendLine($"  {group.Key}: {group.Count()}");
            report.AppendLine("Largest active states:");
            foreach(var p in state.History.Polities.Where(p=>p.EndedYear<0).OrderByDescending(p=>p.AreaKm2).ThenBy(p=>p.Id).Take(5))report.AppendLine($"  {p.Name} [state {p.Id}]: {p.Population:N0} people, {p.AreaKm2:N0} km², {towns.Count(t=>t.PolityId==p.Id)} towns; strain {dynamics.States[p.Id].Strain/10}%.");
            var foundations=new StringBuilder("state,year,cell,latitude,longitude,biome,riverFraction,storageOpportunity,foundationReadiness,tradition,kind,culture,parentState,nativeCropTraditionsAtFounding,introducedCropTraditionsAtFounding\n");
            foreach(var e in state.History.Archive.Where(e=>e.Kind=="state-foundation"||e.Kind=="secession"))
            {
                var p=world.Surface.Cells[e.Cell].Position;var site=engine.AssessHistoricalSite(state,e.Cell);var ds=dynamics.States[e.PolityId];
                var polity=state.History.Polities[e.PolityId];var agriculture=state.History.Agriculture?[polity.CultureId];
                int native=agriculture==null?0:Enumerable.Range(0,6).Count(k=>agriculture.AdoptedYear[k]>0&&agriculture.AdoptedYear[k]-1<=e.Year&&agriculture.SourceCulture[k]<0);
                int introduced=agriculture==null?0:Enumerable.Range(0,6).Count(k=>agriculture.AdoptedYear[k]>0&&agriculture.AdoptedYear[k]-1<=e.Year&&agriculture.SourceCulture[k]>=0);
                foundations.AppendLine(FormattableString.Invariant($"{e.PolityId},{e.Year},{e.Cell},{Math.Asin(p.Z)*180/Math.PI:F3},{Math.Atan2(p.Y,p.X)*180/Math.PI:F3},{world.Ecology.Habitat[e.Cell].Biome},{site.RiverFraction:F6},{site.StorageOpportunity:F6},{ds.FoundationPotential},{ds.Tradition},{e.Kind},{polity.CultureId},{polity.ParentId},{native},{introduced}"));
            }
            File.WriteAllText(Path.Combine(folder,"state-foundations.csv"),foundations.ToString());
        }
        report.AppendLine("\nSettlement habitats:");
        foreach(var group in towns.GroupBy(t=>world.Ecology.Habitat[t.Cell].Biome).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key))
            report.AppendLine($"  {group.Key}: {group.Count()} sites / {group.Sum(t=>t.Population):N0} residents");
        var sites=new StringBuilder("id,name,cell,population,latitude,longitude,biome,aridity,growingDays,riverFraction,coast,storageOpportunity,foodCapacity,reserveYears,polity\n");
        var dry=new List<string>();var cold=new List<string>();var strained=new List<string>();
        foreach(var town in towns.OrderBy(t=>t.Id))
        {
            int cell=town.Cell;var habitat=world.Ecology.Habitat[cell];var site=engine.AssessHistoricalSite(state,cell);var point=SocietyView.Location(world,cell);
            bool coast=world.Hydrology.OceanConnected[cell]||world.Surface.Cells[cell].Neighbors.Any(i=>world.Hydrology.OceanConnected[i]);
            double reserve=town.FoodReserve/(double)Math.Max(1,town.Population);
            sites.AppendLine(FormattableString.Invariant($"{town.Id},{town.Name.Replace(',',' ')},{cell},{town.Population},{Math.Asin(point.Z)*180/Math.PI:F3},{Math.Atan2(point.Y,point.X)*180/Math.PI:F3},{habitat.Biome},{habitat.AridityMilli},{habitat.GrowingDays},{site.RiverFraction:F6},{coast},{site.StorageOpportunity:F6},{town.FoodCapacity},{reserve:F3},{town.PolityId}"));
            string label=$"{town.Name} [site {town.Id}, cell {cell}]";
            if(habitat.AridityMilli<200&&site.RiverFraction<.001&&!coast)dry.Add(label);
            if(habitat.GrowingDays<90)cold.Add(label);
            if(town.Population>town.FoodCapacity*1.15&&reserve<.5)strained.Add(label);
        }
        void Flags(string label,List<string> values){report.AppendLine($"\n{label}: {values.Count}");foreach(string value in values.Take(12))report.AppendLine("  "+value);if(values.Count>12)report.AppendLine("  Additional sites are in settlement-review.csv.");}
        Flags("Very dry host cells without modeled local river/coastal access",dry);
        Flags("Short growing season (<90 days); inspect fishing, food catchment and trade",cold);
        Flags("Population >115% food capacity and <half-year reserve",strained);
        report.AppendLine("\nOccupied / empty sea opportunities (not guaranteed successful voyages):");
        int Navigation(int patch)=>engine.Patches[patch].Cells.Select(cell=>state.Society.CellCulture.Length==0?-1:state.Society.CellCulture[cell]).Where(c=>c>=0).Select(c=>HistoricalTechnologies.Has(state.History.Cultures[c],HistoricalTechnology.Navigation)?2:HistoricalTechnologies.Has(state.History.Cultures[c],HistoricalTechnology.Sailing)?1:0).DefaultIfEmpty(0).Max();
        var opportunities=engine.Routes.Where(r=>r.Sea&&((state.Population[r.A]>=1000&&state.Population[r.B]==0&&engine.Patches[r.B].Capacity>=200)||(state.Population[r.B]>=1000&&state.Population[r.A]==0&&engine.Patches[r.A].Capacity>=200))).OrderBy(r=>r.DistanceM).ThenBy(r=>r.A).ThenBy(r=>r.B).Take(12).ToArray();
        foreach(var route in opportunities)
        {
            int from=state.Population[route.A]>0?route.A:route.B,to=from==route.A?route.B:route.A,nav=Navigation(from);
            report.AppendLine(FormattableString.Invariant($"  Patch {from} -> {to}: {route.DistanceM/1000.0:F1} km, shallow={route.Shallow}, navigation={nav}, source people={state.Population[from]}, destination capacity={engine.Patches[to].Capacity}, source arrival={state.ArrivalYear[from]}."));
        }
        if(opportunities.Length==0)report.AppendLine("  None under the stated population/capacity filters.");
        report.AppendLine("\nTechnology distribution (cultures, not states):");
        foreach(var group in state.History.Cultures.GroupBy(c=>c.Technology.Count(y=>y>0)).OrderBy(g=>g.Key))report.AppendLine($"  {group.Key} adopted practices: {group.Count()} cultures");
        if(state.History.Modern!=null)
        {
            var crops=new List<string>{"culture,name,group,plant,adoptionYear,sourceCulture,originCulture,practiceCell,soil,growingDays,aridity,selectedYears,maturityDays,minimumWarmTenthsC,yieldPermille,husbandryYears"};
            foreach(var a in state.History.Agriculture)for(int k=0;k<6;k++)if(a.Cultivars[k]!=null)
            {
                var v=a.Cultivars[k];var h=world.Ecology.Habitat[a.PracticeCell[k]];
                crops.Add($"{a.CultureId},{state.Society.Cultures[a.CultureId].Name.Replace(',',';')},{k},{HistoricalCropCatalog.Crops[v.CropId].Name},{a.AdoptedYear[k]-1},{a.SourceCulture[k]},{v.OriginCulture},{a.PracticeCell[k]},{h.Soil},{h.GrowingDays},{h.AridityMilli},{v.SelectedYears},{v.MaturityDays},{v.MinimumWarmTenthsC},{v.YieldPermille},{a.HusbandryYears?[k]??0}");
            }
            File.WriteAllLines(Path.Combine(folder,"crop-lineages.csv"),crops);
            report.AppendLine($"\nNamed crop traditions: {crops.Count-1}; successful occupied-port shipping routes: {state.History.Modern.Shipping.Length}. Crop lineage and foundation CSVs distinguish local domestication, introductions and later political secessions.");
            var civic=new List<string>{"id,townA,townB,nameA,nameB,firstYear,lastConflictYear,firstCampaign,lastCampaign,intensity,sharedRuler"};
            int sharedRivals=0;
            foreach(var memory in state.History.Modern.CivicRivalries??Array.Empty<HistoricalCivicRivalry>())
            {
                var a=state.Society.Settlements[memory.TownA];var b=state.Society.Settlements[memory.TownB];bool shared=a.PolityId>=0&&a.PolityId==b.PolityId;if(shared)sharedRivals++;
                civic.Add($"{memory.Id},{a.Id},{b.Id},{a.Name.Replace(',',';')},{b.Name.Replace(',',';')},{memory.FirstYear},{memory.LastConflictYear},{memory.FirstCampaignId},{memory.LastCampaignId},{HistoricalCivicMemoryEffects.Intensity(memory,state.Years)},{shared}");
            }
            File.WriteAllLines(Path.Combine(folder,"civic-rivalries.csv"),civic);
            report.AppendLine($"Civic rivalry memories: {civic.Count-1}; {sharedRivals} pairs currently share a ruler. These are actual nearby campaign ties, not fabricated social relations.");
            report.AppendLine($"Overlapping-war civic memories renewed by an earlier campaign ID: {(state.History.Modern.CivicRivalries??Array.Empty<HistoricalCivicRivalry>()).Count(r=>r.LastCampaignId<r.FirstCampaignId)}. Campaign IDs are not a chronology of local contact.");
            report.AppendLine($"Successful settlement passages with recorded land-avoiding detours: {state.History.OceanSettlements?.Count(v=>v.SailingDistanceM>v.DistanceM)??0}. These sailing distances, not endpoint separation, determine voyage provisions and range.");
        }
        report.AppendLine("\nReview maps at cultivation, first state, iron and the post-ceiling endpoint. Check concentrated settlement, diffusion barriers and regional exceptions; do not tune toward a predetermined continental ranking.");
        File.WriteAllText(Path.Combine(folder,"review.txt"),report.ToString());
        File.WriteAllText(Path.Combine(folder,"settlement-review.csv"),sites.ToString());
    }
}
