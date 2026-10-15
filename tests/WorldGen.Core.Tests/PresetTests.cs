using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class PresetTests
{
    public static void Run(Action<bool,string> check)
    {
        check(WorldPresets.Names.Length==21&&WorldPresets.Names.Distinct().Count()==21,"Twenty-one unique presets");
        check(WorldPresets.Names[0]=="Continents plus"&&WorldPresets.Names[2]=="Earthlike","Default and Earthlike preset order");
        var legacy=Recipe.Preset("Preview");legacy.Detail=5000;legacy.AlgorithmVersion=Recipe.SubcellAlgorithm;
        var current=legacy.Clone();current.AlgorithmVersion=Recipe.CurrentAlgorithm;
        var oldWorld=new WorldPipeline().Generate(legacy);var newWorld=new WorldPipeline().Generate(current);
        check(StateHash.Compute(oldWorld.ElevationM)==StateHash.Compute(newWorld.ElevationM),"Default relief unchanged from 3.31");
        check(StateHash.Compute(oldWorld.Climate)==StateHash.Compute(newWorld.Climate),"Default climate unchanged from 3.31");
        check(StateHash.Compute(oldWorld.Hydrology)==StateHash.Compute(newWorld.Hydrology),"Default hydrology unchanged from 3.31");
        check(StateHash.Compute(oldWorld.Subcells)==StateHash.Compute(newWorld.Subcells),"Default islands unchanged from 3.31");
        foreach(string name in WorldPresets.Names)
        {
            for(int seed=0;seed<30;seed++)
            {
                var source=new Recipe{Seed=seed,Detail=5000};var setup=new WorldSetup();var ui=setup.Resolve(setup.Preset(name,source));
                var core=Recipe.Preset(name);core.Seed=seed;core.Detail=5000;WorldPresets.Apply(name,core);core.Validate();
                check(StateHash.Compute(core)==StateHash.Compute(ui),"Preset UI/core parity: "+name);
                foreach(var window in WorldPresets.Windows(name)){double v=Convert.ToDouble(typeof(Recipe).GetField(window.Field)!.GetValue(core));check(v>=window.Min&&v<=window.Max,"Window bounds: "+name);}
            }
            var recipe=Recipe.Preset(name);recipe.Detail=5000;
            var pipeline=new WorldPipeline();var w=pipeline.Generate(recipe);
            check(w.ElevationM.Length==5000,"Completed "+name);
            check(w.ElevationM.All(x=>Math.Abs(x)<100000),"Finite sensible relief "+name);
            if(recipe.LandShape!=null)
            {
                check(Enumerable.Range(0,5000).All(i=>w.Hydrology.OceanConnected[i]==(MapShapes.SignedDegrees(recipe,w.Surface.Cells[i].Position,out _)<=0)),"Shape coast preserved "+name);
                check(w.Stages.Select(s=>s.Hash).SequenceEqual(pipeline.Reapply(recipe).Stages.Select(s=>s.Hash)),"Shape reapply "+name);
                check(w.Stages.Select(s=>s.Hash).SequenceEqual(new WorldPipeline().Generate(WorldJson.ReadRecipe(WorldJson.Write(recipe))).Stages.Select(s=>s.Hash)),"Shape serialized recipe "+name);
                check(w.Stages.Select(s=>s.Hash).SequenceEqual(pipeline.Reconstruct(w).Stages.Select(s=>s.Hash)),"Shape cached reconstruction "+name);
            }
            if(name=="Lakes"||name=="Desert world")check(!w.Hydrology.OceanConnected.Any(x=>x),"Oceanless "+name);
            Console.WriteLine($"{name}: land {w.Hydrology.OceanConnected.Count(x=>!x)/50.0:F1}%, continents {recipe.ContinentCount}, lake coverage {w.Hydrology.LakeCoverageFraction.Sum()/50:F2}%");
            Console.WriteLine("  "+string.Join(", ",w.Ecology.Habitat.Where((c,i)=>!w.Hydrology.OceanConnected[i]).GroupBy(c=>c.Biome).OrderByDescending(g=>g.Count()).Take(5).Select(g=>$"{g.Key} {g.Count()}")));
        }
    }
}
