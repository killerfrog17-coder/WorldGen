using System;
using System.Linq;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HomininSimulationTests
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string message){n++;if(!ok)throw new Exception(message);}
        var recipe=Recipe.Preset("Earth");recipe.Detail=5000;var world=new WorldPipeline().Generate(recipe);var engine=new Prehistory(world);
        string immutable=StateHash.Compute(world);var prepared=engine.PrepareHominins();engine.Validate(prepared);
        Check(SimulationClockView.LocalHistoryEventDue(prepared,1),"Direct exploration of an unstarted early world still initializes its founding population");
        Check(prepared.Species=="Homo"&&prepared.DeepPast.Lineages.Length==1&&!prepared.DeepPast.Lineages[0].SapiensLike,"Early Homo start, not a renamed sapiens origin");
        var s=engine.Start(prepared);Check(s.DeepPast.Populations.Length==1&&s.Society.Cultures.Length==1,"Tradition and ancestry exist from the founding population");
        Check(s.DeepPast.Version==2&&s.DeepPast.Languages.Length==1&&s.DeepPast.Languages[0].Parent==-1,"Founding communication identity is recorded independently of biological ancestry");
        var watch=System.Diagnostics.Stopwatch.StartNew();var a=engine.AdvanceYears(s,20000);engine.Validate(a);
        var b=engine.AdvanceYears(engine.AdvanceYears(s,7500),12500);Check(a.Hash==b.Hash,"Evolution and climate independent of request partition");
        var budget=engine.AdvanceSeconds(s,20000*SimulationTime.Year,1);while(SimulationTime.Seconds(budget)<20000*SimulationTime.Year)budget=engine.AdvanceSeconds(budget,20000*SimulationTime.Year-SimulationTime.Seconds(budget),1);
        Check(a.Hash==budget.Hash,"Event budget does not change evolution");
        long untilBoundary=a.DeepPast.NextBoundaryYear*SimulationTime.Year-SimulationTime.Seconds(a);
        Check(!SimulationClockView.LocalHistoryEventDue(a,untilBoundary-1)&&SimulationClockView.LocalHistoryEventDue(a,untilBoundary),"Local early-history publication waits for its actual scheduled event");
        var partialFlush=SimulationClockView.LocalPublication(a,3*SimulationTime.Year);
        Check(partialFlush.Seconds==3*SimulationTime.Year&&partialFlush.MaximumEvents==1,"Leaving play flushes empty years in one publication rather than daily copies");
        var boundarySlice=SimulationClockView.LocalPublication(a,untilBoundary+SimulationTime.Year);
        Check(boundarySlice.Seconds==untilBoundary&&boundarySlice.MaximumEvents==1,"Local credit is consumed through the next event without skipping it");
        var legacySlice=SimulationClockView.LocalPublication(new PrehistoryState(),SimulationTime.Year);
        Check(legacySlice.Seconds==SimulationTime.Day&&legacySlice.MaximumEvents==1024,"Legacy local publication budgets are preserved");
        Check(SimulationClockView.Year(a,0)!=SimulationClockView.Year(a,a.Years)&&SimulationClockView.Year(a,a.Years)==HomininView.Date(a),"Historical event labels use their event year instead of the current date");
        var localPlay=new PlaySession{CalendarOffsetSeconds=1234};for(int i=0;i<3000;i++)localPlay.Accumulate(.1);
        Check(!SimulationClockView.LocalHistoryEventDue(a,(long)localPlay.PendingHistorySeconds),"Five minutes of normal local play do not trigger empty early-history publications");
        double visibleTime=PlayCalendar.Seconds(localPlay,SimulationTime.Seconds(a));Check(visibleTime>SimulationTime.Seconds(a)+SimulationTime.Day,"Fine calendar and seasons advance despite retaining the demographic checkpoint");
        var savedCredit=WorldJson.Read<PlaySession>(WorldJson.Write(localPlay));savedCredit.Validate();var exitSlice=SimulationClockView.LocalPublication(a,(long)savedCredit.PendingHistorySeconds);
        var flushed=engine.AdvanceSeconds(a,exitSlice.Seconds,exitSlice.MaximumEvents);savedCredit.Consume(exitSlice.Seconds);
        Check(Math.Abs(PlayCalendar.Seconds(savedCredit,SimulationTime.Seconds(flushed))-visibleTime)<.001&&savedCredit.PendingHistorySeconds<1,"Save/reload and exit flush conserve fine clock credit and calendar offset");
        Check(flushed.DeepPast.CanonicalHash==a.DeepPast.CanonicalHash&&flushed.Timeline.ProcessedEvents==a.Timeline.ProcessedEvents,"Empty-time flush does not invent a demographic event or mutate its domain");
        var actorSource=a.Copy();var sampler=new HomininActorSampler(engine,actorSource);int birthplace=engine.Patches[a.Origin].Cell;
        Check(sampler.TrySample(birthplace,123,false,out var person)&&person.Population>=0&&person.Appearance.Available,"Future NPC profile comes from a recorded local population");
        Check(person.WorldHash==a.WorldHash&&person.HistoryHash==a.Hash&&person.Ancestry.Sum(x=>x.Share)>.999999&&person.TraditionName.Length>0&&person.LanguageName.Length>0,"NPC profile retains ancestry, culture, communication and checkpoint provenance");
        string personJson=WorldJson.Write(person);Check(sampler.TrySample(birthplace,123,false,out var repeated)&&WorldJson.Write(repeated)==personJson,"Individual sampling is deterministic for the same demographic checkpoint");
        actorSource.DeepPast.Populations.First(p=>p.Id==person.Population).Appearance[0]=0;person.Ancestry[0].Share=0;person.Appearance.SkinPigmentation=0;
        Check(sampler.TrySample(birthplace,123,false,out var isolated)&&WorldJson.Write(isolated)==personJson,"Sampler and returned actor profiles do not alias mutable source/output records");
        Check(!sampler.TrySample(-1,0,false,out _)&&!sampler.TrySample(Array.FindIndex(engine.CellPatch,p=>p<0),0,false,out _),"Sampler does not invent a population in invalid or unoccupied water locations");
        Check(!sampler.TrySample(birthplace,123,true,out _),"Sapiens-only creation does not silently substitute an early archaic lineage");
        var mixed=s.Copy();var first=mixed.DeepPast.Populations[0];first.People=100;
        var descendant=mixed.DeepPast.Lineages[0].Copy();descendant.Id=1;descendant.Parent=0;descendant.Name="Homo sampling-fixture";descendant.SapiensLike=true;
        mixed.DeepPast.Lineages=mixed.DeepPast.Lineages.Concat(new[]{descendant}).ToArray();
        var more=first.Copy();more.Id=mixed.DeepPast.NextPopulation++;more.People=300;more.Lineage=1;more.Ancestry=new[]{new HomininAncestry{Lineage=1,Share=1}};
        mixed.DeepPast.Populations=new[]{first,more};mixed.Population[first.Region]=400;mixed.DeepPast.SealDomain();mixed.Seal();
        var weighted=new HomininActorSampler(engine,mixed);int descendantSamples=0;bool allSampled=true;
        for(int i=0;i<1000;i++){bool found=weighted.TrySample(birthplace,i,false,out var sample);allSampled&=found;if(found&&sample.Lineage==1)descendantSamples++;}
        Check(allSampled,"Occupied cohort sampling succeeds across individual seeds");
        Check(descendantSamples>690&&descendantSamples<810,"Local actor selection weights people rather than choosing each cohort equally");
        Check(weighted.TrySample(birthplace,123,true,out var modern)&&modern.SapiensLike&&modern.Lineage==1&&modern.Ancestry.Single().Lineage==1,"Explicit sapiens-only filtering retains the selected population's actual ancestry");
        Check(WorldJson.Write(WorldJson.Read<HomininActorProfile>(personJson))==personJson,"Actor provenance and appearance can persist independently of later demographic checkpoints");
        Check(a.DeepPast.Populations.All(p=>p.People>=0&&Math.Abs(p.Ancestry.Sum(x=>x.Share)-1)<1e-6),"Population and admixture bounds");
        Check(a.DeepPast.Populations.Sum(p=>Math.Round(p.People))==a.Population.Sum(),"Published headcounts reconcile");
        var restored=WorldJson.Read<PrehistoryState>(WorldJson.Write(a));engine.Validate(restored);Check(engine.AdvanceYears(restored,1000).Hash==engine.AdvanceYears(a,1000).Hash,"Saved early history resumes identically");
        Check(a.DeepPast.Languages.Length==a.DeepPast.NextLanguage&&a.DeepPast.Languages.Skip(1).All(l=>l.Parent>=0&&l.Parent<l.Id),"Communication branches retain earlier parent identities");
        var detached=a.Copy();detached.DeepPast.Languages[0].Name="Changed only in the copy";Check(detached.DeepPast.Languages[0].Name!=a.DeepPast.Languages[0].Name,"Communication records detach with published snapshots");
        var legacy=a.Copy();legacy.DeepPast.Version=1;legacy.DeepPast.RulesVersion=1;legacy.DeepPast.Languages=Array.Empty<HomininLanguage>();legacy.DeepPast.SealDomain();legacy.Seal();engine.Validate(legacy);string legacyHash=legacy.Hash;
        var upgraded=engine.UpgradeHomininLanguages(legacy);engine.Validate(upgraded);
        Check(legacy.Hash==legacyHash&&legacy.DeepPast.Version==1&&upgraded.DeepPast.Version==2,"Language-history upgrade leaves the previous checkpoint unchanged");
        Check(upgraded.DeepPast.Populations.Select(p=>p.Language).SequenceEqual(legacy.DeepPast.Populations.Select(p=>p.Language))&&upgraded.DeepPast.Languages.All(l=>l.LegacyOriginUnknown&&l.Parent==-1),"Old language IDs survive upgrade without invented parentage");
        Check(upgraded.DeepPast.RulesVersion==1&&a.DeepPast.RulesVersion==4,"Rule revisions remain explicit across identity-only save upgrades");
        var returning=a.Copy();var ancestral=returning.DeepPast.Populations.First(p=>!returning.DeepPast.Lineages[p.Lineage].SapiensLike);
        ancestral.Communication=.9;ancestral.Learning=.9;
        int extinctChild=returning.DeepPast.Lineages.Length;var lost=returning.DeepPast.Lineages[ancestral.Lineage].Copy();
        lost.Id=extinctChild;lost.Parent=ancestral.Lineage;lost.Name="Previously lost descendant";lost.SapiensLike=true;lost.FoundedYear=10000;lost.ExtinctYear=15000;
        returning.DeepPast.Lineages=returning.DeepPast.Lineages.Concat(new[]{lost}).ToArray();returning.DeepPast.SealDomain();returning.Seal();engine.Validate(returning);
        var legacyReturn=returning.Copy();legacyReturn.DeepPast.RulesVersion=3;legacyReturn.DeepPast.SealDomain();legacyReturn.Seal();
        var oldReturn=engine.AdvanceYears(legacyReturn,1000);Check(oldReturn.DeepPast.Populations.Any(p=>p.Lineage==extinctChild),"Recorded rules 3 retain their original descendant-reuse behaviour");
        var newReturn=engine.AdvanceYears(returning,1000);engine.Validate(newReturn);
        Check(newReturn.DeepPast.Lineages[extinctChild].ExtinctYear==15000&&!newReturn.DeepPast.Populations.Any(p=>p.Lineage==extinctChild),"A new descendant cannot silently resurrect an extinct lineage");
        Check(newReturn.DeepPast.Populations.Where(p=>p.People>0).All(p=>newReturn.DeepPast.Lineages[p.Lineage].ExtinctYear<0),"Living populations reference extant lineages under current rules");
        var invalidLiving=newReturn.Copy();invalidLiving.DeepPast.Lineages[invalidLiving.DeepPast.Populations[0].Lineage].ExtinctYear=invalidLiving.Years;invalidLiving.DeepPast.SealDomain();invalidLiving.Seal();
        bool rejectedLiving=false;try{engine.Validate(invalidLiving);}catch(ArgumentException){rejectedLiving=true;}Check(rejectedLiving,"Validation rejects living cohorts attached to extinct lineages under new rules");
        string beforeAtlas=WorldJson.Write(a);var earlyCategories=AtlasNavigation.CategoriesFor(a);
        Check(earlyCategories.Any(c=>c.Kind=="population")&&earlyCategories.Any(c=>c.Kind=="lineage")&&!earlyCategories.Any(c=>c.Kind=="ethnicity"||c.Kind=="state"),"Early atlas keeps biological lineages distinct from ethnicities and later states");
        Check(AtlasNavigation.CategoriesFor(true).SequenceEqual(earlyCategories)&&earlyCategories.All(c=>AtlasTables.Columns(c.Kind,true).SequenceEqual(AtlasTables.Columns(c.Kind,a))),"Browser atlas layout needs only the published scenario flag, not a second demographic snapshot");
        foreach(var category in earlyCategories)
        {
            var rows=HistoryAtlas.Directory(a,category.Kind);Check(rows.Length>0,"Early atlas exposes its recorded "+category.Kind+" identities");
            Check(rows.All(row=>AtlasTables.Columns(category.Kind,a).All(column=>AtlasTables.Cell(row,column).Text.Length>0)),"Early table columns have corresponding measures");
        }
        Check(HistoryAtlas.Directory(a,"population").Sum(row=>row.Value)==a.Population.Sum()&&HistoryAtlas.Directory(a,"people").Sum(row=>row.Value)==a.Population.Sum()&&HistoryAtlas.Directory(a,"language").Sum(row=>row.Value)==a.Population.Sum(),"Atlas headcounts reconcile across population, tradition and communication views");
        var earlyWorld=HistoryAtlas.Read(a,"world",-1,world,engine);Check(earlyWorld.Facts.Any(f=>f.Label=="Living lineages")&&earlyWorld.Sections.SelectMany(section=>section.Links).Any(link=>link.Kind=="population"),"World overview presents early history instead of claiming no cultures are recorded");
        var firstPopulation=a.DeepPast.Populations[0];var populationPage=HistoryAtlas.Read(a,"population",firstPopulation.Id,world,engine);
        Check(populationPage.Cell==engine.Patches[firstPopulation.Region].Cell&&new[]{"lineage","people","language"}.All(kind=>populationPage.Facts.SelectMany(f=>f.Related).Any(link=>link.Kind==kind)),"Population article links its actual separate identities and geographic region");
        Check(HistoryAtlas.Selection(a,birthplace,HistoryOverlay.Cultures)?.Id==a.Society.CellCulture[birthplace]&&HistoryAtlas.ProfilesAt(a,birthplace).Any(link=>link.Kind=="technology"),"Map inspection opens the recorded local tradition and its practices");
        Check(HistoryAtlas.Selection(a,birthplace,HistoryOverlay.Languages)==null,"A local tradition is not falsely attributed as the selected language identity");
        Check(!AtlasTables.Columns("language",a).Contains("Script")&&!AtlasTables.Columns("language",a).Contains("Word order"),"Early communication tables do not fabricate unmodeled grammar or scripts");
        var timePage=HistoryAtlas.Timeline(a,offset:999999);Check(timePage.Total==a.DeepPast.Events.Length&&timePage.Records.All(f=>f.Label.Contains("before reference")),"Early timeline retains recorded events and uses their historical dates");
        Check(earlyWorld.Sections.Single(section=>section.Title=="Recorded history").Timeline.Length>0&&HistoryAtlas.Read(a,"people",0).Sections.Any(section=>section.Title=="Recorded history"),"Early timeline records are reachable from actual article navigation");
        Check(AtlasNavigation.SnapshotLabel(earlyWorld)==HomininView.Date(a)&&HistoryAtlas.PlainText(earlyWorld).Contains("Snapshot: "+HomininView.Date(a)),"Early article headers and copied text retain their real before-reference date");
        var localArticle=AtlasNavigation.Location(new SelectionCard(),birthplace,a.Years,-1,HomininView.Date(a));Check(AtlasNavigation.SnapshotLabel(localArticle)==HomininView.Date(a),"Location articles retain the published early-history date without reconstructing the domain");
        var latestNews=a.Society.Events.Last();Check(HistoryAtlas.NewsDetail(a,latestNews.Year,latestNews.CultureId,latestNews.Cell,latestNews.Category).Value==latestNews.Text,"Chronicle inspection resolves the actual early event");
        Check(!HistoryAtlas.Directory(returning,"lineage").Any(row=>row.Id==extinctChild)&&HistoryAtlas.Directory(returning,"lineage",past:true).Any(row=>row.Id==extinctChild),"Historical-record toggle exposes extinct lineages without treating them as living");
        Check(HistoryAtlas.Directory(oldReturn,"lineage").Any(row=>row.Id==extinctChild)&&HistoryAtlas.Read(oldReturn,"lineage",extinctChild).Facts.Any(f=>f.Label=="Status"&&f.Value.Contains("still represented")),"Legacy inconsistent extinction records remain inspectable without silently rewriting their dates");
        var extinctionContext=returning.Copy();extinctionContext.DeepPast.Events=extinctionContext.DeepPast.Events.Concat(new[]{new HomininEvent{Year=15000,Region=lost.OriginRegion,Lineage=extinctChild,Kind="extinction",Text="Extinction context fixture"}}).OrderBy(e=>e.Year).ToArray();extinctionContext.DeepPast.SealDomain();extinctionContext.Seal();engine.Validate(extinctionContext);
        Check(HistoryAtlas.Timeline(extinctionContext,"people",0).Records.All(f=>f.Value!="Extinction context fixture")&&HistoryAtlas.Timeline(extinctionContext,"lineage",extinctChild).Records.Single(f=>f.Value=="Extinction context fixture").Related.All(link=>link.Kind=="lineage"),"Biological extinction is not fabricated as an event of default tradition zero");
        var oldLanguage=HistoryAtlas.Read(legacy,"language",0);Check(oldLanguage.Facts.Any(f=>f.Label=="Recorded"&&f.Value=="Unrecorded"),"Legacy early communication records do not invent founding dates");
        var sparse=a.Copy();int sparseId=sparse.DeepPast.NextPopulation+123;sparse.DeepPast.Populations[0].Id=sparseId;sparse.DeepPast.NextPopulation=sparseId+1;sparse.DeepPast.SealDomain();sparse.Seal();engine.Validate(sparse);
        Check(HistoryAtlas.Read(sparse,"population",sparseId,world,engine).Facts.Any(f=>f.Label=="People"),"Population articles resolve stable sparse IDs rather than array positions");
        earlyWorld.Facts[0].Value="Only this view changed";Check(WorldJson.Write(a)==beforeAtlas,"Browsing and editing returned atlas documents cannot mutate simulation records");
        var laterPrepared=engine.PrepareSapiens();Check(ReferenceEquals(AtlasNavigation.CategoriesFor(laterPrepared),AtlasNavigation.Categories)&&AtlasTables.Columns("people",laterPrepared).SequenceEqual(AtlasTables.Columns("people")),"Later-history categories and columns retain their existing interface");
        Check(HistoryAtlas.Read(laterPrepared).Facts.Any(f=>f.Label=="Surviving states"),"The established later-history overview still uses its original adapter");
        var smallBand=new HomininPopulation{People=100,Tradition=3,Language=2,Communication=.4,Learning=.5,Appearance=Enumerable.Repeat(2000,8).ToArray(),Ancestry=new[]{new HomininAncestry{Lineage=0,Share=1}}};
        var largeBand=new HomininPopulation{People=300,Tradition=8,Language=5,Communication=.8,Learning=.9,Appearance=Enumerable.Repeat(8000,8).ToArray(),Ancestry=new[]{new HomininAncestry{Lineage=1,Share=1}}};
        double beforeCommunication=smallBand.People*smallBand.Communication+largeBand.People*largeBand.Communication;
        Prehistory.ExchangeHomininAncestry(smallBand,largeBand,.15);
        Check(Math.Abs(smallBand.People*smallBand.Ancestry.First(x=>x.Lineage==0).Share+largeBand.People*largeBand.Ancestry.First(x=>x.Lineage==0).Share-100)<1e-8,"Unequal-size reciprocal admixture conserves represented ancestry");
        Check(Math.Abs(smallBand.People*smallBand.Communication+largeBand.People*largeBand.Communication-beforeCommunication)<1e-8,"Reciprocal exchange does not create inherited trait advantage through update order");
        Check(smallBand.Tradition==3&&largeBand.Tradition==8&&smallBand.Language==2&&largeBand.Language==5,"Biological exchange does not silently replace learned culture or language");
        Check(StateHash.Compute(world)==immutable,"Early history does not rewrite generation ecology or terrain");
        world.Prehistory=a;var binary=WorldFile.Write(world);var reloaded=WorldFile.Read(binary);var freshEngine=new Prehistory(reloaded);
        Check(freshEngine.Patches.Length==engine.Patches.Length&&freshEngine.Routes.Length==engine.Routes.Length,"Saved early world retains geographic route domain");
        Check(freshEngine.AdvanceYears(reloaded.Prehistory,2000).Hash==engine.AdvanceYears(a,2000).Hash,"Full compact world reload resumes identically with a fresh engine");
        world.Prehistory=null;
        Check(PleistoceneClimate.GlacialFraction(42,20000)>PleistoceneClimate.GlacialFraction(42,0)+.5,"Late glacial maximum precedes rapid forward-time deglaciation");
        var cold=PleistoceneClimate.At(42,20000,65,0,300);var tropical=PleistoceneClimate.At(42,20000,0,0,300);Check(cold.TemperatureDeltaC<tropical.TemperatureDeltaC,"Regional glacial cooling differs with latitude");
        foreach(int year in Enumerable.Range(0,300).Select(i=>i*10000)){var c=PleistoceneClimate.At(42,year,60,30,400);Check(c.Glacial>=0&&c.Glacial<=1&&c.PrecipitationFactor>0&&c.SeaLevelM<=0&&c.IceSuitability>=0&&c.IceSuitability<=1,"Climate bounds");}
        Check(HomininHistory.Stage(new HomininTradition())==LithicStage.LowerPaleolithic,"Initial tool tradition lower Paleolithic");
        Check(HomininHistory.Stage(new HomininTradition{Knowledge=LithicKnowledge.PreparedCores})==LithicStage.MiddlePaleolithic,"Regional technical stage");
        Check(HomininHistory.Stage(new HomininTradition{Knowledge=LithicKnowledge.Cultivation,Cultivation=.6})==LithicStage.Neolithic,"Farming matters independently of date");
        Check(HomininHistory.Stage(new HomininTradition{Knowledge=LithicKnowledge.Cultivation|LithicKnowledge.Copper,Cultivation=.6})==LithicStage.Neolithic,"Native copper ornaments alone do not imply extractive metallurgy");
        Check(HomininHistory.Stage(new HomininTradition{Knowledge=LithicKnowledge.Copper|LithicKnowledge.MetalSmelting})==LithicStage.Chalcolithic,"Extractive metalworking has a distinct technical stage");
        Check(HomininHistory.Stage(new HomininTradition{Knowledge=LithicKnowledge.Copper},true)==LithicStage.Chalcolithic,"Legacy stage descriptions remain available for legacy rules");
        foreach(var skill in new[]{LithicKnowledge.Pottery,LithicKnowledge.Needles,LithicKnowledge.Cultivation,LithicKnowledge.Herding,LithicKnowledge.MetalSmelting,LithicKnowledge.Bronze})
        {
            Check(HomininChronology.Weight(skill,700000,true)<1e-10,"Earth-like chronology suppresses severely anachronistic "+skill);
            Check(HomininChronology.Weight(skill,0,true)>HomininChronology.Weight(skill,50000,true)&&HomininChronology.Weight(skill,50000,true)>0,"Plausibility is a soft time prior, not an exact date unlock");
            Check(HomininChronology.Weight(skill,700000,false)==1,"Open trajectory omits historical date priors");
        }
        Check(Rng.Value(42,"lithic-innovation-256",3516219)==0,"Recorded legacy rare-event fixture reproduces 24-bit zero quantization");
        Check(Rng.OpenValue(42,"lithic-innovation-256",3516219)>1e-11,"New policy does not accept an infinitesimal hazard merely because the old roll rounded to zero");
        Check(Enumerable.Range(0,10000).All(i=>Rng.OpenValue(42,"open-value-check",i)>0&&Rng.OpenValue(42,"open-value-check",i)<1),"Rare-event random values exclude both endpoints");
        Console.WriteLine("PASS "+n+" hominin/climate assertions; "+a.DeepPast.Populations.Length+" cohorts / "+a.DeepPast.Traditions.Length+" traditions in "+watch.Elapsed.TotalSeconds.ToString("F2")+"s");
    }
}
