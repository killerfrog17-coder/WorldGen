using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
static class PaleolithicTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
        var recipe=Recipe.Preset("Continents");recipe.Detail=5000;var world=new WorldPipeline().Generate(recipe);var terrainHash=StateHash.Compute(world);var engine=new Prehistory(world);
        var old=engine.Prepare();engine.Validate(old);var legacyHash=old.Hash;
        var ready=engine.PreparePaleolithic();engine.Validate(ready);Check(!ready.Started&&ready.Population.Sum()==0,"Preparation does not advance or populate the world");
        var start=engine.Start(ready);engine.Validate(start);Check(start.Version=="prehistory-7"&&start.History==null&&start.Population.Sum()>=25,"New version begins with viable established sapiens foragers");
        var colored=PrehistoryView.Codes(world,engine,start,overlay:HistoryOverlay.Cultures);Check(engine.Patches[start.Origin].Cells.All(i=>colored[i]==235),"Paleolithic culture layer uses actual identity colours");
        Check(SimulationClockView.Date(start,2*SimulationTime.Year)=="Year 0"&&SimulationClockView.Date(start,1).Contains("Jan 1")&&WorldEraView.Name(2)=="Paleolithic","Existing in-world year/calendar convention and requested era label are retained");
        foreach(var overlay in new[]{HistoryOverlay.States,HistoryOverlay.Territories,HistoryOverlay.Provinces,HistoryOverlay.Civilizations,HistoryOverlay.Religions,HistoryOverlay.ReligiousInstitutions,HistoryOverlay.Ethnicities})
        {Check(SocietyView.Legend(start,overlay).Length==0&&Enumerable.Range(0,world.Surface.Cells.Length).All(i=>SocietyView.OverlayId(start,i,overlay)==-1)&&PrehistoryView.Codes(world,engine,start,overlay:overlay).All(code=>code==0||code==85),overlay+" cannot fabricate institutions/identities from culture IDs");}
        Check(start.HumanRanges!=null&&start.HumanRanges.Lineages.Length==2&&HumanRangeState.Mask(start,engine.Patches[start.Origin].Cell)==1,"Separate archaic ranges exist without overlapping the initial sapiens region");
        Check(start.HumanRanges.Lineages.All(l=>l.Residents[start.Origin]==0&&l.Residents.Sum()>0),"Each archaic lineage has a real independent regional population ledger");
        var one=engine.AdvanceSeconds(start,60);Check(SimulationTime.Seconds(one)==60&&one.Years==0,"Minute-scale background history is not rounded away");
        var fifty=engine.AdvanceYears(start,50);var split=engine.AdvanceYears(engine.AdvanceYears(start,17),33);Check(fifty.Hash==split.Hash,"One large interval equals split playback across a decision boundary");
        var session=new SimulationSession(engine,start);while(session.Seconds<250*SimulationTime.Year)session.AdvanceSeconds(250*SimulationTime.Year-session.Seconds,1);
        Check(session.Hash==engine.AdvanceYears(start,250).Hash,"Budgeted session playback is independent of event budget");
        var published=session.Snapshot();published.Paleolithic.Communities[0].Knowledge[0]=0;published.Paleolithic.Communities[0].Language.Onsets[0]=24;
        Check(session.Hash==session.Snapshot().ComputeHash(),"Published knowledge and language arrays cannot mutate private session state");
        var c=start.Paleolithic.Communities[0];int cell=engine.Patches[c.Patch].Cell;
        var band=engine.PaleolithicBandAt(start,cell,10);var bandAgain=engine.PaleolithicBandAt(start,cell,10);Check(StateHash.Compute(band)==StateHash.Compute(bandAgain),"Band identity, knowledge, language and appearance are stable across revisits");
        Check(band.Appearance.Available&&SapientSpecies.Find(band.Species)==SapientSpecies.Human,"Bands share the human appearance system and explicit sapient registry");
        int expressive=Enumerable.Range(0,2000).Count(i=>PaleolithicRules.CanExpress(engine.PaleolithicBandAt(start,cell,i),PaleolithicSpeech.ClauseCombination));Check(expressive>0&&expressive<1800,"Speech capabilities spread through stable local cohorts instead of all bands crossing a shared threshold on one day");
        Check(!PaleolithicRules.CanName(band,"iron")&&!PaleolithicRules.CanName(band,"writing"),"Primitive speech cannot silently name unimplemented later-age concepts");
        var probe=start.Copy();probe.Paleolithic.Communities[0].Knowledge[(int)PaleolithicTechnique.HideWraps]=.9;probe.Paleolithic.Communities[0].Targets[(int)PaleolithicTechnique.HideWraps]=.9;
        int knowing=0;for(int i=0;i<2000;i++)if(engine.PaleolithicBandAt(probe,cell,i).Knows(PaleolithicTechnique.HideWraps))knowing++;
        Check(knowing>1700&&knowing<1880,"90% adoption produces roughly90% stable band samples, not a fresh per-encounter roll");
        var warm=PaleolithicRules.Climate(73421,75000,.8);var ice=PaleolithicRules.Climate(73421,179000,.8);
        Check(ice.Ice>warm.Ice&&ice.TemperatureDeltaC<warm.TemperatureDeltaC&&ice.SeaFallM>warm.SeaFallM,"Dated glacial forcing changes northern cold exposure and sea level");
        var a=PaleolithicRules.Climate(73421,50000,.4);var b=PaleolithicRules.Climate(73421,50000+1/365.0,.4);Check(Math.Abs(a.TemperatureDeltaC-b.TemperatureDeltaC)<.001,"Climate cannot jump on an ordinary game day");
        Check(Math.Abs(PaleolithicRules.Climate(73421,200000-1/365.0,.4).TemperatureDeltaC-PaleolithicRules.Climate(73421,200000+1/365.0,.4).TemperatureDeltaC)<.001,"Continuing the Paleolithic sandbox past the reference present cannot abruptly reset the glacial forcing");
        var shelf=new Prehistory.Route{Sea=true,Shallow=true,SeaDropM=70,DistanceM=200000};var ocean=new Prehistory.Route{Sea=true,DistanceM=100000};
        Check(!PaleolithicRules.RouteOpen(shelf,warm,0)&&PaleolithicRules.RouteOpen(shelf,ice,0),"Actual shallow shelf threshold controls a glacial land corridor");
        Check(!PaleolithicRules.RouteOpen(ocean,ice,1),"Ice and raft knowledge never unlock a100km deep-water crossing");
        var noClothes=new WeatherFrame(5,.5,2,0,.5,.2,1000,500,4000,2,2,3,0);var weather=PaleolithicRules.Weather(noClothes,ice);Check(weather.TemperatureC<5&&weather.SnowWaterMmPerHour>0,"Glacial forcing reaches real thermal exposure and rain/snow partition");
        var before=engine.AdvanceSeconds(start,50*SimulationTime.Year-1);var after=engine.AdvanceSeconds(before,1);Check(Math.Abs(PaleolithicRules.Adoption(before.Paleolithic,before.Paleolithic.Communities[0],8)-PaleolithicRules.Adoption(after.Paleolithic,after.Paleolithic.Communities[0],8))<.00001,"Knowledge has no discontinuity at simulation decision boundaries");
        int changeYear=((c.NextSpeechChange+49)/50)*50;var speechBefore=engine.AdvanceSeconds(start,changeYear*SimulationTime.Year-1);var speechAfter=engine.AdvanceSeconds(speechBefore,1);
        Check(speechAfter.Paleolithic.Communities[0].PreviousLanguage!=null&&Enumerable.Range(0,20).All(i=>StateHash.Compute(engine.PaleolithicBandAt(speechBefore,cell,i).Language)==StateHash.Compute(engine.PaleolithicBandAt(speechAfter,cell,i).Language)),"A sound change begins gradual local adoption rather than changing every band's language overnight");
        Check(PrehistoryState.SameDomain(one,start),"Minute updates preserve the map domain instead of scheduling constant border rebuilds in gameplay");
        var watch=Stopwatch.StartNew();var late=engine.AdvanceYears(start,188300);watch.Stop();engine.Validate(late);
        Check(late.Population.Count(n=>n>0)>1,"Long-run population expands by connected migration rather than a fixed origin bubble");
        Check(late.History==null&&late.Society.Settlements.Length==0&&late.Society.Cultures.All(x=>x.Farming==0&&x.Stage==SocietyStage.Foragers),"Whole Paleolithic simulation cannot unlock farming, cities or later ages");
        Check(late.Paleolithic.Communities.Any(x=>x.Language.Revision>0&&x.Speech[3]>c.Speech[3]),"Language sound changes and expressive development occur from the beginning");
        world.Prehistory=late;var copy=WorldJson.Read<PrehistoryState>(WorldJson.Write(late));engine.Validate(copy);Check(engine.AdvanceYears(copy,100).Hash==engine.AdvanceYears(late,100).Hash,"JSON save/reload continues with identical knowledge and migration");
        var bytes=WorldFile.Write(world);var restored=WorldFile.Read(bytes);new Prehistory(restored).Validate(restored.Prehistory);Check(restored.Prehistory.Hash==late.Hash,"Real world binary serialization retains the new state");
        Check(PrehistoryView.Describe(engine,late,cell,HistoryOverlay.Technology).Contains("LOCAL KNOWLEDGE")&&SocietyView.Legend(late,HistoryOverlay.Cultures,engine:engine).Length>0,"Map inspection and culture legend show actual Paleolithic communities");
        var techCard=HistoryAtlas.Card(late,cell,HistoryOverlay.Technology,engine);Check(techCard.Bars.Length==24&&techCard.Bars.Any(b=>b.Value>0)&&HistoryAtlas.Card(start,cell,HistoryOverlay.Technology,engine).Bars.Any(b=>b.Value>0&&b.Value<1000),"Visible technology inspection shows all techniques with gradual adoption shares");
        int cultureId=SocietyView.Culture(late,cell).Id;
        Check(HistoryAtlas.Read(late,"technology",cultureId,world,engine).Sections.Any(s=>s.Links.Length==24&&s.Bars)&&HistoryAtlas.Directory(late,"technology").Length>0,"Existing technology atlas/directory are connected to the new state");
        var dialects=late.Copy();var living=dialects.Paleolithic.Communities.Where(c=>dialects.Population[c.Patch]>0).Take(2).ToArray();living[1].Language=living[0].Language.Copy();living[0].PreviousLanguage=living[1].PreviousLanguage=null;Array.Clear(living[1].Knowledge);Array.Clear(living[1].Targets);Array.Clear(living[1].Rates);dialects.Seal();
        int aCell=engine.Patches[living[0].Patch].Cell,bCell=engine.Patches[living[1].Patch].Cell;
        Check(SocietyView.OverlayId(dialects,aCell,HistoryOverlay.Cultures)!=SocietyView.OverlayId(dialects,bCell,HistoryOverlay.Cultures)&&SocietyView.OverlayId(dialects,aCell,HistoryOverlay.Languages)==SocietyView.OverlayId(dialects,bCell,HistoryOverlay.Languages),"Distinct cultures with identical speech share a language map identity");
        Check(SocietyView.OverlayId(dialects,aCell,HistoryOverlay.Technology)!=SocietyView.OverlayId(dialects,bCell,HistoryOverlay.Technology),"Technology map keys follow actual local knowledge independently of culture and shared language");
        int language=SocietyView.OverlayId(late,cell,HistoryOverlay.Languages);Check(HistoryAtlas.Read(late,"language",language,world,engine).Sections.Any(s=>s.Title=="Sounds and reusable roots")&&HistoryAtlas.Card(late,cell,HistoryOverlay.Languages,engine).Bars.Length==4,"Language inspection exposes actual vocabulary, grammar and gradual speech capacities");
        var coexist=start.Copy();coexist.HumanRanges.Lineages[0].Residents[start.Origin]=80;coexist.Seal();coexist=engine.AdvanceYears(coexist,50);engine.Validate(coexist);
        Check(HumanRangeState.Mask(coexist,cell)==3&&coexist.Population[start.Origin]>0,"Sapiens and archaic populations coexist through a real demographic update without one overwriting the other");
        Check(SocietyView.Legend(coexist,HistoryOverlay.HumanSpecies).Any(e=>e.Name.Contains(" + "))&&HistoryAtlas.Directory(coexist,"human-species").Length>0,"Overlapping species have a visible mixed-range legend and inspection entry");
        var detached=late.Copy();detached.HumanRanges.Lineages[0].Residents[0]+=1;Check(late.Hash==late.ComputeHash(),"Published copies cannot mutate another checkpoint's human species populations");
        world.Prehistory=null;Check(StateHash.Compute(world)==terrainHash,"Simulation never rewrites terrain, ecology boundaries or the fauna pack");engine.Validate(old);Check(old.Hash==legacyHash,"Legacy historical checkpoint remains valid and unchanged");
        var cancellation=new CancellationTokenSource();cancellation.Cancel();bool cancelled=false;try{engine.AdvanceYears(start,100,cancellation.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled&&start.Hash==start.ComputeHash(),"Cancellation preserves the source checkpoint");
        Console.WriteLine("Peak practice: "+string.Join(", ",Enumerable.Range(8,16).Select(k=>PaleolithicRules.Names[k]+"="+late.Paleolithic.Communities.Max(c=>c.Knowledge[k]).ToString("0.00"))));
        Console.WriteLine($"PASS {checks} Paleolithic simulation checks;188300yr={watch.Elapsed.TotalSeconds:0.00}s; patches={engine.Patches.Length}; occupied={late.Population.Count(n=>n>0)}; population={late.Population.Sum()}; communities={late.Paleolithic.Communities.Length}");
    }
}
