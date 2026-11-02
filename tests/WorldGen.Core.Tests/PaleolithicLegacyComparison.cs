using System.Reflection;
using System.Runtime.Loader;
using WorldGen.Core;
static class PaleolithicLegacyComparison
{
    public static void Run(string assemblyPath)
    {
        string folder=Path.GetDirectoryName(Path.GetFullPath(assemblyPath));var context=new AssemblyLoadContext("pre-paleolithic-comparison",true);
        context.Resolving+=(c,n)=>File.Exists(Path.Combine(folder,n.Name+".dll"))?c.LoadFromAssemblyPath(Path.Combine(folder,n.Name+".dll")):null;
        var core=context.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
        object Call(object target,string name,params object[] given)
        {
            var methods=(target as Type??target.GetType()).GetMethods().Where(m=>m.Name==name&&m.GetParameters().Length>=given.Length&&m.GetParameters().Skip(given.Length).All(p=>p.IsOptional));
            var method=methods.OrderBy(m=>m.GetParameters().Length).First();var ps=method.GetParameters();var values=new object[ps.Length];Array.Copy(given,values,given.Length);for(int i=given.Length;i<values.Length;i++)values[i]=ps[i].ParameterType==typeof(CancellationToken)?CancellationToken.None:ps[i].DefaultValue;
            return method.Invoke(target is Type?null:target,values);
        }
        var recipeType=core.GetType("WorldGen.Core.Recipe");var oldRecipe=Call(recipeType,"Preset","Continents");recipeType.GetField("Detail").SetValue(oldRecipe,5000);
        var oldWorld=Call(Activator.CreateInstance(core.GetType("WorldGen.Core.WorldPipeline")),"Generate",oldRecipe);
        var oldEngine=Activator.CreateInstance(core.GetType("WorldGen.Core.Prehistory"),oldWorld,CancellationToken.None);var oldStart=Call(oldEngine,"Start",Call(oldEngine,"Prepare"));var oldEnd=Call(oldEngine,"Advance",oldStart,2000);
        var recipe=Recipe.Preset("Continents");recipe.Detail=5000;var engine=new Prehistory(new WorldPipeline().Generate(recipe));var current=engine.Advance(engine.Start(engine.Prepare()),2000);
        string oldHash=(string)oldEnd.GetType().GetField("Hash").GetValue(oldEnd);if(oldHash!=current.Hash)throw new Exception("Legacy engine diverged from the untouched packaged baseline: "+oldHash+" / "+current.Hash);
        Console.WriteLine("PASS exact200000-year legacy continuation vs pre-overhaul packaged assembly: "+oldHash);context.Unload();
    }
}
