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
        Check(SimulationClockView.Date(start,SimulationTime.Year).Contains("200,000"),"Paleolithic calendar has an explicit200ka starting epoch");
        var one=engine.AdvanceSeconds(start,60);Check(SimulationTime.Seconds(one)==60&&one.Years==0,"Minute-scale background history is not rounded away");
        var fifty=engine.AdvanceYears(start,50);var split=engine.AdvanceYears(engine.AdvanceYears(start,17),33);Check(fifty.Hash==split.Hash,"One large interval equals split playback across a decision boundary");
        var session=new SimulationSession(engine,start);while(session.Seconds<250*SimulationTime.Year)session.AdvanceSeconds(250*SimulationTime.Year-session.Seconds,1);
        Check(session.Hash==engine.AdvanceYears(start,250).Hash,"Budgeted session playback is independent of event budget");
        var published=session.Snapshot();published.Paleolithic.Communities[0].Knowledge[0]=0;published.Paleolithic.Communities[0].Language.Onsets[0]=24;
        Check(session.Hash==session.Snapshot().ComputeHash(),"Published knowledge and language arrays cannot mutate private session state");
        var c=start.Paleolithic.Communities[0];int cell=engine.Patches[c.Patch].Cell;
        var band=engine.PaleolithicBandAt(start,cell,10);var bandAgain=engine.PaleolithicBandAt(start,cell,10);Check(StateHash.Compute(band)==StateHash.Compute(bandAgain),"Band identity, knowledge, language and appearance are stable across revisits");
        Check(band.Appearance.Available&&SapientSpecies.Find(band.Species)==SapientSpecies.Human,"Bands share the human appearance system and explicit sapient registry");
        Check(!PaleolithicRules.CanName(band,"iron")&&!PaleolithicRules.CanName(band,"writing"),"Primitive speech cannot silently name unimplemented later-age concepts");
        var probe=start.Copy();probe.Paleolithic.Communities[0].Knowledge[(int)PaleolithicTechnique.HideWraps]=.9;probe.Paleolithic.Communities[0].Targets[(int)PaleolithicTechnique.HideWraps]=.9;
        int knowing=0;for(int i=0;i<2000;i++)if(engine.PaleolithicBandAt(probe,cell,i).Knows(PaleolithicTechnique.HideWraps))knowing++;
        Check(knowing>1700&&knowing<1880,"90% adoption produces roughly90% stable band samples, not a fresh per-encounter roll");
        var warm=PaleolithicRules.Climate(73421,75000,.8);var ice=PaleolithicRules.Climate(73421,179000,.8);
        Check(ice.Ice>warm.Ice&&ice.TemperatureDeltaC<warm.TemperatureDeltaC&&ice.SeaFallM>warm.SeaFallM,"Dated glacial forcing changes northern cold exposure and sea level");
        var a=PaleolithicRules.Climate(73421,50000,.4);var b=PaleolithicRules.Climate(73421,50000+1/365.0,.4);Check(Math.Abs(a.TemperatureDeltaC-b.TemperatureDeltaC)<.001,"Climate cannot jump on an ordinary game day");
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
        Check(PrehistoryView.Describe(engine,late,cell).Contains("LOCAL KNOWLEDGE")&&SocietyView.Legend(late,HistoryOverlay.States,engine:engine).Length>0,"Map inspection and its default legend show Paleolithic communities");
        world.Prehistory=null;Check(StateHash.Compute(world)==terrainHash,"Simulation never rewrites terrain, ecology boundaries or the fauna pack");engine.Validate(old);Check(old.Hash==legacyHash,"Legacy historical checkpoint remains valid and unchanged");
        var cancellation=new CancellationTokenSource();cancellation.Cancel();bool cancelled=false;try{engine.AdvanceYears(start,100,cancellation.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled&&start.Hash==start.ComputeHash(),"Cancellation preserves the source checkpoint");
        Console.WriteLine("Peak practice: "+string.Join(", ",Enumerable.Range(8,16).Select(k=>PaleolithicRules.Names[k]+"="+late.Paleolithic.Communities.Max(c=>c.Knowledge[k]).ToString("0.00"))));
        Console.WriteLine($"PASS {checks} Paleolithic simulation checks;188300yr={watch.Elapsed.TotalSeconds:0.00}s; patches={engine.Patches.Length}; occupied={late.Population.Count(n=>n>0)}; population={late.Population.Sum()}; communities={late.Paleolithic.Communities.Length}");
    }
}
