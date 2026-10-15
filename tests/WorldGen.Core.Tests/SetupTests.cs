using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
public static class SetupTests
{
    public static void Run(Action<bool,string> check)
    {
        var recipe=Recipe.Preset("Preview");recipe.Detail=5000;
        var settings=new WorldSetup();var fixedRecipe=settings.Resolve(recipe);
        check(StateHash.Compute(recipe)==StateHash.Compute(fixedRecipe),"Fixed setup ranges preserve recipe");
        settings.PlatesMin=20;settings.PlatesMax=200;settings.ContinentsMin=1;settings.ContinentsMax=20;settings.ConnectionsMin=1;settings.ConnectionsMax=50;
        var selected=settings.Resolve(recipe);check(StateHash.Compute(selected)==StateHash.Compute(settings.Resolve(recipe)),"Seeded range selection repeats");
        var samples=new HashSet<int>();for(int seed=0;seed<200;seed++){recipe.Seed=seed;var r=settings.Resolve(recipe);samples.Add(r.ContinentCount);check(r.ContinentCount>=1&&r.ContinentCount<=20&&r.MicroplateCount>=20&&r.MicroplateCount<=200,"Range bounds");}
        check(samples.Count>12,"Ranges produce varied choices");settings.ContinentsMin=21;
        try{settings.Resolve(recipe);check(false,"Invalid range rejected");}catch(ArgumentException){check(true,"Invalid range rejected");}
        recipe.Seed=73421;var stages=new List<string>();var snapshots=new List<(World world,string hash)>();
        check(WorldSetup.Round(.012345,false)==.012 && WorldSetup.Round(12345,true)==12300,"Significant figure rounding");
        check(WorldSetup.Position(0,0,0)==0 && WorldSetup.Value(.5,0,0)==0,"Locked slider bounds are finite");
        var optional=new WorldSetup();optional.From(recipe);
        var temperature=typeof(Recipe).GetField(nameof(Recipe.TemperatureOffset))!;
        var range=optional.Get(temperature,recipe);range.Enabled=true;range.Min=-10;range.Max=10;
        foreach(var roll in Enum.GetValues<WorldSetup.Roll>())
        {
            range.Distribution=roll;double sum=0;
            for(int seed=0;seed<200;seed++){var source=recipe.Clone();source.Seed=seed;var picked=optional.Resolve(source);sum+=picked.TemperatureOffset;check(picked.TemperatureOffset>=-10&&picked.TemperatureOffset<=10,"Optional signed range bounds");check(StateHash.Compute(picked)==StateHash.Compute(optional.Resolve(source)),"Named range determinism");}
            check(roll==WorldSetup.Roll.Linear?Math.Abs(sum/200)<2:roll==WorldSetup.Roll.Low?sum/200<-3:sum/200>3,"Roll bias direction "+roll);
        }
        range.Enabled=false;check(optional.Resolve(recipe).TemperatureOffset==recipe.TemperatureOffset,"Unchecking range restores fixed value");
        var observed=new WorldPipeline().Generate(recipe,preview:(stage,w)=>{
            var p=GenerationPreview.Capture(stage,w);stages.Add(stage);snapshots.Add((p,StateHash.Compute(p)));
            Layer layer=stage=="coarse-plates"?Layer.Microplates:stage=="climate"?Layer.Climate:stage=="biogeography"?Layer.Ecoregions:Layer.Terrain;
            check(GenerationPreview.Available(p,layer),"Preview layer available "+stage);
            foreach(var projection in new[]{Projection.Globe,Projection.Map})check(SurfaceMesh.Chunks(p,new ViewOptions{Layer=layer,Projection=projection}).Any(),"Preview renders "+stage+projection);
            check(DisplayCache.Geometry(p,Projection.Globe,default).Count()>0,"Cached preview renders every overlay "+stage);
            check(CellInspection.Describe(p,0,layer).Length>0,"Preview inspection "+stage);
        });
        check(stages.SequenceEqual(new[]{"coarse-plates","erosion","climate","hydrology","biogeography"}),"Ordered completed previews");
        foreach(var p in snapshots)check(StateHash.Compute(p.world)==p.hash,"Preview remains immutable after generation");
        var normal=new WorldPipeline().Generate(recipe);check(observed.Stages.Select(s=>s.Hash).SequenceEqual(normal.Stages.Select(s=>s.Hash)),"Preview observer preserves every stage hash");
        check(observed.Stages.Select(s=>s.Hash).SequenceEqual(new WorldPipeline().Reapply(recipe).Stages.Select(s=>s.Hash)),"Reapply hashes unchanged");
        check(observed.Stages.Select(s=>s.Hash).SequenceEqual(new WorldPipeline().Generate(WorldJson.ReadRecipe(WorldJson.Write(recipe))).Stages.Select(s=>s.Hash)),"Serialized recipe hashes unchanged");
        check(observed.Stages.Select(s=>s.Hash).SequenceEqual(new WorldPipeline().Reconstruct(observed).Stages.Select(s=>s.Hash)),"Reconstructed hashes unchanged");
        foreach(var layer in GlobeView.VisibleLayers){check(!CellInspection.Describe(observed,0,layer).Contains("Cell 0"),"Compact inspection "+layer);}
        int ecologicalCell=Array.FindIndex(observed.Ecology.Region,id=>id>=0&&observed.Ecology.Regions[id].FoodWeb.Length>0);
        var web=FoodWebDiagram.Build(observed,ecologicalCell);var ecologicalRegion=observed.Ecology.Regions[observed.Ecology.Region[ecologicalCell]];
        check(web.Nodes.Length==ecologicalRegion.Species.Select(s=>s.TaxonId).Distinct().Count(),"Food web contains every regional taxon");
        check(web.Links.All(l=>ecologicalRegion.FoodWeb.Any(e=>e.ResourceId==l.From&&e.ConsumerId==l.To)),"Food web uses actual ecological links");
        check(FoodWebDiagram.Svg(observed,ecologicalCell).Contains("<svg"),"Shared food web SVG");
        var plain=SurfaceMesh.Chunks(normal,new ViewOptions{Layer=Layer.Terrain,GridSpacing=0}).First();var legacy=SurfaceMesh.Chunks(normal,new ViewOptions{Layer=Layer.Terrain,ShowPlates=true,GridSpacing=0}).First();
        check(plain.Colors.SequenceEqual(legacy.Colors)&&plain.Lines.SequenceEqual(legacy.Lines),"Plate toggle no longer paints terrain");
        foreach(var projection in new[]{Projection.Globe,Projection.Map})
        {
            var pieces=DisplayCache.Geometry(normal,projection,default).ToArray();
            foreach(string kind in new[]{"surface","wire","plates","ecology","grid5","grid10","grid15","grid30"})
                check(pieces.Any(p=>p.Kind==kind),"Prebuilt switchable overlay "+projection+kind);
            check(pieces.Where(p=>p.Kind=="surface").All(p=>p.Data.CellIds.Length==p.Data.Positions.Length/3),"Palette owner for every vertex "+projection);
            foreach(var main in DisplayCache.MainLayers)check(GlobeView.CellValues(normal,main).Colors.Length==normal.Surface.Cells.Length*3,"Cached palette size "+main+projection);
        }
        using var stop=new CancellationTokenSource();try{new WorldPipeline().Generate(recipe,cancellation:stop.Token,preview:(s,w)=>stop.Cancel());check(false,"Cancellation");}catch(OperationCanceledException){check(true,"Cancellation after preview");}
    }
}
