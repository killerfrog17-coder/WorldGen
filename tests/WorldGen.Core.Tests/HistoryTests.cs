using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;
public static class HistoryTests
{
    static void TributeScenario(Prehistory engine,PrehistoryState source,Action<bool,string> check)
    {
        var state=source.Copy();var h=state.History;
        foreach(var known in h.Cultures)for(int k=0;k<known.Technology.Length;k++)known.Technology[k]=1;
        var cities=state.Society.Settlements.Where(t=>t.Population>1500&&t.PolityId>=0&&h.Polities[t.PolityId].EndedYear<0).ToArray();
        var possible=(from x in cities from y in cities where x.PolityId<y.PolityId select (x,y)).OrderBy(p=>D.Angle(engine.World.Surface.Cells[p.x.Cell].Position,engine.World.Surface.Cells[p.y.Cell].Position)).ThenBy(p=>p.x.Id).ThenBy(p=>p.y.Id);
        var sites=possible.FirstOrDefault(p=>engine.CanHistoricalContact(state,p.x.Cell,p.y.Cell,p.x.CultureId));
        check(sites.x!=null,"Historical control contains a physically connected independent-state campaign scenario");
        var relation=h.Relations.FirstOrDefault(r=>r.A==sites.x.PolityId&&r.B==sites.y.PolityId);
        if(relation==null){relation=new HistoricalRelation{A=sites.x.PolityId,B=sites.y.PolityId};h.Relations=h.Relations.Concat(new[]{relation}).ToArray();}
        var attacker=h.Polities[relation.A];var defender=h.Polities[relation.B];var culture=h.Cultures[attacker.CultureId];
        foreach(var other in h.Relations){other.WarSince=-1;other.Alliance=false;other.Trade=false;}
        foreach(var old in h.Dynamics.Campaigns.Where(c=>c.EndedYear<0)){old.EndedYear=state.Years;old.Outcome="Closed for an isolated mechanism test.";}
        foreach(var old in h.Dynamics.Dependencies.Where(d=>d.EndedYear<0)){old.EndedYear=state.Years;old.Ending="Closed for an isolated mechanism test.";}
        foreach(int k in Enumerable.Range(0,culture.Technology.Length))culture.Technology[k]=1;
        var destinations=state.Society.Settlements.Where(t=>t.PolityId==defender.Id&&t.Population>=100).ToArray();
        bool reachable=state.Society.Settlements.Where(t=>t.PolityId==attacker.Id&&t.Population>=100).Any(t=>destinations.Any(d=>engine.CanHistoricalContact(state,t.Cell,d.Cell,attacker.CultureId)));
        check(reachable,"Tribute fixture has a physical route, not an assumed teleport");
        var leader=h.Dynamics.Leaders[h.Dynamics.States[attacker.Id].LeaderId];leader.Military=999;leader.Administration=999;leader.ExpectedEndYear=state.Years+10;
        attacker.Army=500000;attacker.Cohesion=1000;attacker.Treasury=0;defender.Army=40;defender.Treasury=1000000;
        relation.WarSince=state.Years;relation.Trade=false;relation.Alliance=false;
        int id=h.Dynamics.Campaigns.Length;h.Dynamics.Campaigns=h.Dynamics.Campaigns.Concat(new[]{new HistoricalCampaign{Id=id,A=relation.A,B=relation.B,AttackerId=attacker.Id,LeaderId=leader.Id,StartedYear=state.Years,LastActionYear=state.Years,Aim=HistoricalWarAim.Tribute,Name="Controlled tribute test",Objective="Tribute and fiscal relief"}}).ToArray();
        state.Seal();var next=engine.AdvanceYears(state,2);engine.Validate(next);var campaign=next.History.Dynamics.Campaigns[id];
        Console.WriteLine($"Tribute scenario: captures {campaign.Conquests}, repulses {campaign.Repulses}, payment {campaign.TributePaid}, ended {campaign.EndedYear}, last action {campaign.LastActionYear}; {campaign.Outcome}");
        check(campaign.TributePaid>0&&campaign.TributeRecipientId==attacker.Id&&campaign.EndedYear>=0,"Successful tribute battle records a paid settlement and ends its campaign");
        check(campaign.Conquests==0&&next.History.Polities[defender.Id].EndedYear<0&&destinations.All(t=>next.Society.Settlements[t.Id].PolityId==defender.Id),"Tribute victory preserves the defeated court and its cities rather than mandatory annexation");
        check(source.ComputeHash()==source.Hash,"Controlled war fixture and continuation do not mutate the original checkpoint");
    }
    public static void Run(Action<bool,string> check,string version="history-5")
    {
        string folder=Path.GetFullPath(Environment.GetEnvironmentVariable("HISTORY_TEST_OUTPUT")??"artifacts/history-5/tests");Directory.CreateDirectory(folder);
        string legacy4="artifacts/history-4/final-earth/final.world";
        if(File.Exists(legacy4)){var old=WorldFile.Read(File.ReadAllBytes(legacy4));check(new Prehistory(old).AdvanceYears(old.Prehistory,37).Hash==File.ReadAllText("artifacts/history-4/final-earth/resume-hash.txt").Trim(),"History-4 retains exact pre-change continuation");}
        string legacy3="artifacts/history-3/founders-earth/final.world";
        if(File.Exists(legacy3)){var old=WorldFile.Read(File.ReadAllBytes(legacy3));check(new Prehistory(old).AdvanceYears(old.Prehistory,37).Hash==File.ReadAllText("artifacts/history-3/founders-earth/resume-hash.txt").Trim(),"History-3 checkpoint retains exact continuation");}
        check(Prehistory.FrontierGrowthPerCentury(50,10000)>Prehistory.FrontierGrowthPerCentury(5000,10000),"Available habitat supports faster frontier growth");
        check(Prehistory.FrontierGrowthPerCentury(10000,10000)==.048&&Prehistory.FrontierGrowthPerCentury(12000,10000)==.048,"Crowded and overcapacity populations retain the original rate");
        check(Prehistory.FrontierGrowthPerCentury(0,10000)<=.208,"Frontier response is bounded rather than unlimited exponential growth");
        check(Prehistory.RegionalEmergenceExposure(500)==1&&Prehistory.RegionalEmergenceExposure(8000)==2&&Prehistory.RegionalEmergenceExposure(1000000)==4,"Regional opportunity scales sublinearly with people and remains bounded");
        check(Prehistory.MarketBorderCost(true,false,false)>1&&Prehistory.MarketBorderCost(true,false,true)==1,"Peaceful informal markets incur friction rather than an automatic embargo");
        check(Prehistory.MarketBorderCost(true,true,true)<0&&Prehistory.MarketBorderCost(false,false,false)==1,"War blocks foreign provisioning and domestic supply remains unchanged");
        string legacy="artifacts/history-1/Earth.world";
        string legacy2="artifacts/history-3/legacy-history-2.world";
        if(File.Exists(legacy2)){var old=WorldFile.Read(File.ReadAllBytes(legacy2));check(new Prehistory(old).AdvanceYears(old.Prehistory,37).Hash==File.ReadAllText("artifacts/history-3/legacy-history-2-hash.txt").Trim(),"History-2 checkpoint retains exact continuation");}
        if(File.Exists(legacy)){var old=WorldFile.Read(File.ReadAllBytes(legacy));var oldEngine=new Prehistory(old);check(oldEngine.AdvanceYears(old.Prehistory,37).Hash==File.ReadAllText("artifacts/history-1/Earth-resume-hash.txt").Trim(),"History-1 checkpoint retains its exact continuation");}
        foreach(string preset in new[]{"Earth","Continents"})
        {
            string previous="artifacts/societies-1/"+preset+".world";
            World w;if(File.Exists(previous)){w=WorldFile.Read(File.ReadAllBytes(previous));w.Prehistory=null;}else{var r=Recipe.Preset(preset);r.Detail=5000;w=new WorldPipeline().Generate(r);}
            string terrain=StateHash.Compute(w);w.Prehistory=new PrehistoryState{Version="prehistory-6",History=new HistoryState{Version=version}};
            var engine=new Prehistory(w);var start=engine.Start(engine.Prepare());
            check(start.Version=="prehistory-6"&&start.History?.Version==version,"Independent history version");
            var watch=System.Diagnostics.Stopwatch.StartNew();var s=start;
            while(s.Years<120000)
            {
                int ceiling=(version=="history-7"||(version=="history-8"||version=="history-9"))?HistoryAudit.MedievalCeilingYear(s.History):-1;
                int limit=ceiling>=0?Math.Min(120000,ceiling+750):120000;if(s.Years>=limit)break;
                int interval=(version=="history-7"||(version=="history-8"||version=="history-9"))?(s.Society.Cultures.Length>0?50:1000):10000;
                s=engine.AdvanceYears(s,Math.Min(interval,limit-s.Years));engine.Validate(s);
                if(s.Years==100000){w.Prehistory=s;File.WriteAllBytes(Path.Combine(folder,preset+"-preview.world"),WorldFile.Write(w));w.Prehistory=null;}
                if(s.Years%50000==0)Console.WriteLine($"{preset} {s.Years}: {s.Society.Cultures.Length} cultures, {s.Society.Settlements.Length} towns, {s.History.Polities.Count(p=>p.EndedYear<0)} active/{s.History.Polities.Length} states, {s.History.Archive.Count} events; {watch.Elapsed.TotalSeconds:F1}s");
            }
            if((version=="history-7"||(version=="history-8"||version=="history-9"))){w.Prehistory=s;File.WriteAllBytes(Path.Combine(folder,preset+"-preview.world"),WorldFile.Write(w));w.Prehistory=null;Console.WriteLine(preset+" era review ends at "+s.Years);}
            check(s.Society.Cultures.Length>0,"Cultures emerge in suitable conditions");
            var ruralTotals=new long[engine.Patches.Length];for(int cell=0;cell<engine.CellPatch.Length;cell++)if(engine.CellPatch[cell]>=0)ruralTotals[engine.CellPatch[cell]]+=engine.RuralCellPopulation(s,cell);
            check(ruralTotals.SequenceEqual(s.Population),"Resource-weighted cells sum exactly to every patch population");
            check(s.History.CellStage.Count(x=>x>=2)<=s.History.CellPolity.Count(x=>x>=0)+s.Society.Settlements.Length,"Urban stages are local to towns and political territory");
            check(s.History.CellStage.Count(x=>x>=3)<s.Society.CellCulture.Count(x=>x>=0),"Civilization is not a culture-wide paint operation");
            var full=engine.AdvanceYears(s,100);var split=engine.AdvanceYears(engine.AdvanceYears(s,37),63);check(full.Hash==split.Hash,"Irregular historical batching stable");
            if((version=="history-7"||(version=="history-8"||version=="history-9")))
            {
                var renamed=s.Copy();foreach(var p in renamed.History.Polities)p.Name="Court "+p.Id;renamed.Seal();var namedResult=engine.AdvanceYears(renamed,100);
                check(namedResult.Population.SequenceEqual(full.Population)&&namedResult.History.CellPolity.SequenceEqual(full.History.CellPolity)&&namedResult.History.Polities.Length==full.History.Polities.Length&&namedResult.Society.Settlements.Select(t=>t.Population).SequenceEqual(full.Society.Settlements.Select(t=>t.Population)),"Court labels do not determine demographic or political outcomes");
            }
            var annual=s;for(int i=0;i<10;i++)annual=engine.AdvanceYears(annual,1);check(engine.AdvanceYears(s,10).Hash==annual.Hash,"Annual/century historical replay stable");
            check(s.ComputeHash()==s.Hash,"Prior checkpoint immutable");
            if(version=="history-9")
            {
                check(s.Society.Settlements.Select(t=>t.Cell).Distinct().Count()==s.Society.Settlements.Length,"Abandonment and reoccupation retain one persistent settlement record per cell");
                check(s.History.RegionalLearning!=null&&s.History.RegionalLearning.FarmingExposure.Any(x=>x>0),"Ordinary farming creates persistent regional exposure");
                var regionalEdit=s.Copy();regionalEdit.History.RegionalLearning.FarmingExposure[0]=(regionalEdit.History.RegionalLearning.FarmingExposure[0]+1)%1001;
                check(regionalEdit.ComputeHash()!=s.Hash&&s.ComputeHash()==s.Hash,"Regional exposure is hashed without aliasing published checkpoints");
                var invalidRegional=s.Copy();invalidRegional.History.RegionalLearning.FarmingSource[0]=s.History.Cultures.Length;invalidRegional.Seal();bool rejectedRegional=false;
                try{engine.Validate(invalidRegional);}catch(ArgumentException){rejectedRegional=true;}check(rejectedRegional,"Unknown regional crop source is rejected");
                var studyEdit=s.Copy();studyEdit.History.Agriculture[0].PracticeCell[0]=(studyEdit.History.Agriculture[0].PracticeCell[0]+1)%w.Surface.Cells.Length;
                check(studyEdit.ComputeHash()!=s.Hash&&s.ComputeHash()==s.Hash,"Crop-study locations are persistent independent integrity fields");
                var invalidStudy=s.Copy();invalidStudy.History.Agriculture[0].PracticeCell[0]=-1;invalidStudy.Seal();bool rejectedStudy=false;
                try{engine.Validate(invalidStudy);}catch(ArgumentException){rejectedStudy=true;}check(rejectedStudy,"Invalid agricultural practice locations are rejected");
                var diagnostic=engine.AssessHistoricalFoundation(s,0);check(diagnostic.Preparation==s.History.Towns[0].CivicPreparationYears&&s.ComputeHash()==s.Hash,"Foundation diagnostics expose actual state without mutating it");
            }
            if((version=="history-8"||version=="history-9"))
            {
                check(s.History.Towns.Any(t=>t.CivicPreparationYears>=150),"Ordinary states have accumulated sustained local preparation");
                var invalidPreparation=s.Copy();invalidPreparation.History.Towns[0].CivicPreparationYears=1001;invalidPreparation.Seal();bool rejectedPreparation=false;
                try{engine.Validate(invalidPreparation);}catch(ArgumentException){rejectedPreparation=true;}check(rejectedPreparation,"Invalid saved preparation is rejected before simulation");
            }
            using(var cancellation=new System.Threading.CancellationTokenSource())
            {
                cancellation.Cancel();bool cancelled=false;
                try{engine.AdvanceYears(s,1000,cancellation.Token);}catch(OperationCanceledException){cancelled=true;}
                check(cancelled,"Historical stepping honors cancellation");
                check(s.ComputeHash()==s.Hash,"Cancelled historical step leaves prior checkpoint intact");
            }
            check(PrehistoryView.Codes(w,null,s).Length==w.Surface.Cells.Length,"Saved overlay safe before engine preparation");
            var islandPatch=engine.Patches.FirstOrDefault(p=>p.Island);
            if(islandPatch!=null&&s.History.Polities.Any(p=>p.EndedYear<0))
            {
                int cell=islandPatch.Cells[0];var island=s.Copy();var owner=s.History.Polities.First(p=>p.EndedYear<0);island.Society.CellCulture[cell]=owner.CultureId;island.History.CellPolity[cell]=owner.Id;island.Seal();
                check(PrehistoryView.Codes(w,engine,island,-1,HistoryOverlay.States)[cell]==50,"Island state tint retains the water-cell mask");
                check(PrehistoryView.Codes(w,engine,island,cell,HistoryOverlay.States)[cell]==60,"Selected island state has a distinct color code");
                var group=SubcellGeography.Find(w,cell);var largest=w.Subcells.Islands.Skip(group.FirstIsland).Take(group.IslandCount).OrderByDescending(i=>i.AreaM2).First();
                check(D.Angle(SocietyView.Location(w,cell),largest.Center)<.000001,"Island settlement marker uses its actual island center");
            }
            var alteredStage=s.Copy();alteredStage.History.CellStage[0]=(alteredStage.History.CellStage[0]+1)%4;check(alteredStage.ComputeHash()!=s.Hash,"Local development stage participates in integrity hash");
            w.Prehistory=s;var round=WorldFile.Read(WorldFile.Write(w));check(round.Prehistory.Hash==s.Hash,"History serialized intact");
            check(new Prehistory(round).AdvanceYears(round.Prehistory,100).Hash==full.Hash,"Saved reconstruction continuation");
            check(StateHash.Compute(w)==terrain,"History leaves geography unchanged");
            foreach(HistoryOverlay overlay in Enum.GetValues<HistoryOverlay>()){var codes=PrehistoryView.Codes(w,engine,s,-1,overlay);check(codes.Length==w.Surface.Cells.Length,"Overlay "+overlay);}
            check(s.History.Polities.Where(p=>p.EndedYear<0).All(p=>s.Society.Settlements[p.CapitalId].PolityId==p.Id),"Active capitals remain owned after conquest");
            if((version=="history-7"||(version=="history-8"||version=="history-9")))check(s.History.Polities.Where(p=>p.EndedYear<0).Select(p=>p.Name).Distinct().Count()==s.History.Polities.Count(p=>p.EndedYear<0),"Simultaneous courts have distinct names even when their towns share a name");
            check(s.Society.Settlements.All(t=>t.PolityId==s.History.CellPolity[t.Cell]),"Town administration and political map ownership agree after peaceful expansion and local foundation");
            if(s.History.Polities.Length>0){var regression=s.Copy();foreach(var culture in regression.Society.Cultures)culture.Stage=SocietyStage.AdvancedCulture;regression.Seal();check(engine.SetStepYears(regression,100,true).Society.SelectedStepYears==1,"Historical calendar does not return to prehistory after institutional regression");}
            var faiths=SocietyView.Legend(s,HistoryOverlay.Religions,engine:engine);
            long faithful=Enumerable.Range(0,s.History.CellReligion.Length).Where(i=>s.History.CellReligion[i]>=0).Sum(i=>engine.RuralCellPopulation(s,i))+s.Society.Settlements.Where(t=>s.History.CellReligion[t.Cell]>=0).Sum(t=>t.Population);
            check(faiths.Sum(f=>f.Population)==faithful,"Religious ranking accounts for modeled dominant-faith population exactly once");
            check(faiths.All(f=>s.History.CellReligion[f.Cell]==f.ColorId),"Faith legend selects current followers");
            check(SocietyView.Legend(s,HistoryOverlay.Civilizations).All(c=>SocietyView.OverlayId(s,c.Cell,HistoryOverlay.Civilizations)==c.ColorId),"Civilization legend selects actual territory of its ruling culture, not conquered local identity");
            var practices=SocietyView.Legend(s,HistoryOverlay.Technology).Select(e=>e.ColorId).ToArray();check(practices.SequenceEqual(practices.OrderByDescending(i=>i)),"Technology legend ordered by adopted practice count");
            check(s.History.Cultures.All(c=>c.Technology.Length==15),"Technology ends at defined medieval practices");
            if(version=="history-6"||(version=="history-7"||(version=="history-8"||version=="history-9")))
            {
                check(s.History.Towns.Sum(t=>t.ExportedFood)==s.History.Towns.Sum(t=>t.ImportedFood+t.FoodTransportLoss),"Inter-town food exports equal deliveries plus transit loss");
                var contacts=engine.HistoricalTownContacts(s);
                check(contacts.Length==s.Society.Settlements.Length&&contacts.All(row=>row.SequenceEqual(row.Distinct().OrderBy(x=>x))),"Town contacts use stable, duplicate-free IDs");
                check(Enumerable.Range(0,contacts.Length).All(i=>contacts[i].All(j=>j>=0&&j<contacts.Length&&j!=i&&contacts[j].Contains(i))),"Regular town contact is reciprocal and has no phantom neighbors");
                int originalFirst=contacts.FirstOrDefault(row=>row.Length>0)?[0]??-1;
                if(originalFirst>=0){int index=Array.FindIndex(contacts,row=>row.Length>0);contacts[index][0]=-1;check(engine.HistoricalTownContacts(s)[index][0]==originalFirst,"Diagnostic callers cannot mutate the derived contact cache");}
                var immuneEdit=s.Copy();immuneEdit.History.Towns[0].KindImmunity[0]++;check(immuneEdit.ComputeHash()!=s.Hash&&s.ComputeHash()==s.Hash,"Family-specific recovery participates in immutable checkpoint integrity");
                var badWave=s.Copy();badWave.History.Towns[0].DiseaseWaveId=badWave.History.Archive.Count+1;badWave.Seal();bool waveRejected=false;
                try{engine.Validate(badWave);}catch(ArgumentException){waveRejected=true;}check(waveRejected,"Unknown epidemic lineage references are rejected");
                var faithEdit=s.Copy();faithEdit.History.Faith.Influence[0]=(faithEdit.History.Faith.Influence[0]+1)%1001;
                check(faithEdit.ComputeHash()!=s.Hash&&s.ComputeHash()==s.Hash,"Religious adoption pressure is hashed without mutating the source checkpoint");
                var invalidFaith=s.Copy();invalidFaith.History.Faith.Candidate[0]=invalidFaith.History.Religions.Length;invalidFaith.Seal();bool faithRejected=false;
                try{engine.Validate(invalidFaith);}catch(ArgumentException){faithRejected=true;}check(faithRejected,"Nonexistent religious competition references are rejected");
                var invalidAncestor=s.Copy();invalidAncestor.History.Cultures[0].ParentCultureId=0;invalidAncestor.Seal();bool ancestorRejected=false;
                try{engine.Validate(invalidAncestor);}catch(ArgumentException){ancestorRejected=true;}check(ancestorRejected,"A community cannot be its own ancestor");
            }
            TributeScenario(engine,s,check);
            var dynamics=s.History.Dynamics;
            check(dynamics.States.Length==s.History.Polities.Length&&dynamics.Leaders.Length>0,"Every political state has persistent institutional and leadership records");
            check(dynamics.Leaders.Select(l=>(l.Military,l.Administration,l.Diplomacy,l.Ambition)).Distinct().Count()>1,"Leaders differ in modeled capabilities rather than names alone");
            var rulerEdit=s.Copy();rulerEdit.History.Dynamics.Leaders[0].Military=(rulerEdit.History.Dynamics.Leaders[0].Military+1)%1001;
            check(rulerEdit.ComputeHash()!=s.Hash&&s.ComputeHash()==s.Hash,"Leadership is integrity-hashed and checkpoint copies are independent");
            if(dynamics.Campaigns.Length>0){var campaignEdit=s.Copy();campaignEdit.History.Dynamics.Campaigns[0].Outcome+=" changed";check(campaignEdit.ComputeHash()!=s.Hash,"Persistent campaign outcome participates in integrity hashing");}
            check(dynamics.Campaigns.All(c=>c.TributePaid==0||c.EndedYear>=0&&c.TributeRecipientId>=0),"Peace payments have a named recipient and a completed campaign");
            var invalidLeader=s.Copy();invalidLeader.History.Dynamics.Leaders[0].PolityId=s.History.Polities.Length;invalidLeader.Seal();bool leaderRejected=false;try{engine.Validate(invalidLeader);}catch(ArgumentException){leaderRejected=true;}check(leaderRejected,"Invalid leader-state linkage is rejected");
            check(s.History.Agriculture.Length==s.Society.Cultures.Length,"Every culture has aligned persistent crop records");
            check(s.History.Agriculture.Any(a=>a.AdoptedYear.Any(y=>y>0)),"Cultivation has explicit adopted crop systems");
            var cropCulture=s.History.Agriculture.First(a=>a.AdoptedYear.Any(y=>y>0));int cropIndex=Array.FindIndex(cropCulture.AdoptedYear,y=>y>0);
            var cropEdit=s.Copy();cropEdit.History.Agriculture[cropCulture.CultureId].Practice[cropIndex]=(cropEdit.History.Agriculture[cropCulture.CultureId].Practice[cropIndex]+1)%1001;
            check(cropEdit.ComputeHash()!=s.Hash,"Crop practice participates in integrity hashing");
            check(s.History.Agriculture[cropCulture.CultureId].Practice[cropIndex]==cropCulture.Practice[cropIndex],"Crop mutations leave the source checkpoint untouched");
            var badCrop=s.Copy();badCrop.History.Agriculture[cropCulture.CultureId].SourceCulture[cropIndex]=cropCulture.CultureId;badCrop.Seal();
            bool cropRejected=false;try{engine.Validate(badCrop);}catch(ArgumentException){cropRejected=true;}
            check(cropRejected,"Crop adoption cannot cite itself as a prior source");
            var badLanguage=s.Copy();badLanguage.History.Cultures[0].LanguageId=badLanguage.History.Cultures.Length;badLanguage.Seal();
            bool languageRejected=false;try{engine.Validate(badLanguage);}catch(ArgumentException){languageRejected=true;}
            check(languageRejected,"Out-of-range language-family reference is rejected before inspection/export");
            check(SocietyView.HistoricalDescription(s,s.Society.Cultures[cropCulture.CultureId].OriginCell,HistoryOverlay.Technology,"").Contains("ADOPTION TIMELINE"),"Technology inspection exposes dated adoption rather than an unexplained level");
            check(s.History.Archive.Select(e=>e.Id).SequenceEqual(Enumerable.Range(0,s.History.Archive.Count)),"Lifetime archive stable identities");
            string lore=SocietyView.CampaignLore(s,w);
            check(lore.Contains("## Settlement gazetteer")&&lore.Contains("## Religious traditions"),"Campaign export has city and religious reference directories");
            check(lore.Contains("## Notable and current leaders")&&lore.Contains("## Military campaigns"),"Campaign export includes recorded leadership and war narratives");
            check(s.Society.Settlements.All(t=>lore.Contains("[site "+t.Id+"]")),"Campaign directory includes every recorded settlement, including declined sites");
            check(s.History.Polities.All(p=>lore.Contains("[state "+p.Id+"]")),"Campaign directory preserves fallen as well as living states");
            check(lore.Contains("Coordinates locate coarse settlement sites")&&lore.Contains("Complete event archive"),"Gazetteer states coordinate limits and retains the full event archive");
            // Legacy archives intentionally preserve their original livelihood
            // wording; current crop-aware presentation is not a save migration.
            if(s.History.Version==Prehistory.LivingWorldHistoryVersion)
                check(!lore.Contains("Seasonal grain cultivation with fishing"),"Current crop-system inspection does not label every farming tradition as grain cultivation");
            var corrupt=s.Copy();if(corrupt.History.Cultures.Length>0){corrupt.History.Cultures[0].Technology[14]=1;corrupt.History.Cultures[0].Technology[6]=0;corrupt.Seal();bool rejected=false;try{engine.Validate(corrupt);}catch(ArgumentException){rejected=true;}check(rejected,"Invalid technology prerequisite rejected");}
            File.WriteAllText(Path.Combine(folder,preset+"-resume-hash.txt"),engine.AdvanceYears(s,37).Hash);File.WriteAllBytes(Path.Combine(folder,preset+".world"),WorldFile.Write(w));File.WriteAllText(Path.Combine(folder,preset+".json"),WorldJson.Write(s));File.WriteAllText(Path.Combine(folder,preset+"-chronicle.txt"),SocietyView.CampaignLore(s));
            Console.WriteLine($"{preset}: {SocietyView.Population(s):N0} people; urban cells {s.History.CellStage.Count(x=>x>=2)}; cultural cells {s.Society.CellCulture.Count(x=>x>=0)}; technologies {s.History.Cultures.Select(c=>c.Technology.Count(t=>t>0)).DefaultIfEmpty().Max()}; wars {s.History.Archive.Count(e=>e.Kind=="war")}; conquests {s.History.Archive.Count(e=>e.Kind=="conquest")}");
        }
        // Negative control: elapsed time cannot substitute for productive habitat.
        // This is a qualitative invariant, not a golden population or fitted historical date.
        var setup=new WorldSetup();var desertRecipe=setup.Resolve(setup.Preset("Desert world",new Recipe{Seed=17,Detail=20000}));
        var desert=new WorldPipeline().Generate(desertRecipe);string desertTerrain=StateHash.Compute(desert);
        if((version=="history-7"||(version=="history-8"||version=="history-9")))desert.Prehistory=new PrehistoryState{Version="prehistory-6",History=new HistoryState{Version=version}};
        var desertEngine=new Prehistory(desert);var desertState=desertEngine.AdvanceYears(desertEngine.Start(desertEngine.Prepare()),200000);
        desertEngine.Validate(desertState);
        check(desertState.Society.Cultures.Length==0,"Desert negative control does not force regional institutions with time");
        check(desertState.Society.Settlements.Length==0&&desertState.History.Polities.Length==0,"Unproductive desert does not create automatic urban states");
        check(SocietyView.Population(desertState)>0,"Sparse desert population can persist without urbanization");
        check(Prehistory.YearsPerStep(desertState)==100,"Nonurban negative control retains prehistoric automatic steps");
        check(StateHash.Compute(desert)==desertTerrain,"Negative-control history leaves terrain unchanged");
    }
}
