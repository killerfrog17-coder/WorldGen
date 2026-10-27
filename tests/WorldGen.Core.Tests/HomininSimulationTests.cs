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
        var watch=System.Diagnostics.Stopwatch.StartNew();var a=engine.AdvanceYears(s,20000);engine.Validate(a);
        var b=engine.AdvanceYears(engine.AdvanceYears(s,7500),12500);Check(a.Hash==b.Hash,"Evolution and climate independent of request partition");
        var budget=engine.AdvanceSeconds(s,20000*SimulationTime.Year,1);while(SimulationTime.Seconds(budget)<20000*SimulationTime.Year)budget=engine.AdvanceSeconds(budget,20000*SimulationTime.Year-SimulationTime.Seconds(budget),1);
        Check(a.Hash==budget.Hash,"Event budget does not change evolution");
        Check(a.DeepPast.Populations.All(p=>p.People>=0&&Math.Abs(p.Ancestry.Sum(x=>x.Share)-1)<1e-6),"Population and admixture bounds");
        Check(a.DeepPast.Populations.Sum(p=>Math.Round(p.People))==a.Population.Sum(),"Published headcounts reconcile");
        var restored=WorldJson.Read<PrehistoryState>(WorldJson.Write(a));engine.Validate(restored);Check(engine.AdvanceYears(restored,1000).Hash==engine.AdvanceYears(a,1000).Hash,"Saved early history resumes identically");
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
