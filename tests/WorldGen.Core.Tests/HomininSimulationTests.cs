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
        Check(prepared.Species=="Homo"&&prepared.DeepPast.Lineages.Length==1&&!prepared.DeepPast.Lineages[0].SapiensLike,"Early Homo start, not a renamed sapiens origin");
        var s=engine.Start(prepared);Check(s.DeepPast.Populations.Length==1&&s.Society.Cultures.Length==1,"Tradition and ancestry exist from the founding population");
        Check(s.DeepPast.Version==2&&s.DeepPast.Languages.Length==1&&s.DeepPast.Languages[0].Parent==-1,"Founding communication identity is recorded independently of biological ancestry");
        var watch=System.Diagnostics.Stopwatch.StartNew();var a=engine.AdvanceYears(s,20000);engine.Validate(a);
        var b=engine.AdvanceYears(engine.AdvanceYears(s,7500),12500);Check(a.Hash==b.Hash,"Evolution and climate independent of request partition");
        var budget=engine.AdvanceSeconds(s,20000*SimulationTime.Year,1);while(SimulationTime.Seconds(budget)<20000*SimulationTime.Year)budget=engine.AdvanceSeconds(budget,20000*SimulationTime.Year-SimulationTime.Seconds(budget),1);
        Check(a.Hash==budget.Hash,"Event budget does not change evolution");
        Check(a.DeepPast.Populations.All(p=>p.People>=0&&Math.Abs(p.Ancestry.Sum(x=>x.Share)-1)<1e-6),"Population and admixture bounds");
        Check(a.DeepPast.Populations.Sum(p=>Math.Round(p.People))==a.Population.Sum(),"Published headcounts reconcile");
        var restored=WorldJson.Read<PrehistoryState>(WorldJson.Write(a));engine.Validate(restored);Check(engine.AdvanceYears(restored,1000).Hash==engine.AdvanceYears(a,1000).Hash,"Saved early history resumes identically");
        Check(a.DeepPast.Languages.Length==a.DeepPast.NextLanguage&&a.DeepPast.Languages.Skip(1).All(l=>l.Parent>=0&&l.Parent<l.Id),"Communication branches retain earlier parent identities");
        var detached=a.Copy();detached.DeepPast.Languages[0].Name="Changed only in the copy";Check(detached.DeepPast.Languages[0].Name!=a.DeepPast.Languages[0].Name,"Communication records detach with published snapshots");
        var legacy=a.Copy();legacy.DeepPast.Version=1;legacy.DeepPast.Languages=Array.Empty<HomininLanguage>();legacy.DeepPast.SealDomain();legacy.Seal();engine.Validate(legacy);string legacyHash=legacy.Hash;
        var upgraded=engine.UpgradeHomininLanguages(legacy);engine.Validate(upgraded);
        Check(legacy.Hash==legacyHash&&legacy.DeepPast.Version==1&&upgraded.DeepPast.Version==2,"Language-history upgrade leaves the previous checkpoint unchanged");
        Check(upgraded.DeepPast.Populations.Select(p=>p.Language).SequenceEqual(legacy.DeepPast.Populations.Select(p=>p.Language))&&upgraded.DeepPast.Languages.All(l=>l.LegacyOriginUnknown&&l.Parent==-1),"Old language IDs survive upgrade without invented parentage");
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
        Console.WriteLine("PASS "+n+" hominin/climate assertions; "+a.DeepPast.Populations.Length+" cohorts / "+a.DeepPast.Traditions.Length+" traditions in "+watch.Elapsed.TotalSeconds.ToString("F2")+"s");
    }
}
