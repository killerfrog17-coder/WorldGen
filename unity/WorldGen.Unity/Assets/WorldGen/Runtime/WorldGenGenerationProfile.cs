using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class WorldGenRuntime
{
    readonly List<GenerationStageTiming> generationMeasurements=new List<GenerationStageTiming>();
    [Serializable] sealed class GenerationTimingRow{public string Stage;public double ComputeMs,HashMs,PreviewMs,TotalMs,Percent;}
    [Serializable] sealed class GenerationTimingReport{public string Runtime,WorldHash,RecipeFile;public int Cells,Seed;public double EngineMs,ReadyMs,ComputeMs,HashMs,PreviewMs;public GenerationTimingRow[] Stages;}
    IEnumerator CheckGenerationProfile()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);
        recipe=setup.Preset("Continents plus",new Recipe{Detail=200000});recipe.Seed=int.Parse(Argument("--worldgen-seed","73421"));recipe.Detail=int.Parse(Argument("--worldgen-detail","200000"));preferredLayer=layer=Layer.Terrain;generationMeasurements.Clear();
        double start=Time.realtimeSinceStartupAsDouble;StartGeneration();
        while((Generating||Busy||!worldReady||displayedWorld!=world)&&error.Length==0&&Time.realtimeSinceStartupAsDouble-start<1800)yield return null;
        if(error.Length>0||!worldReady||Busy){Debug.LogError("GENERATION PROFILE failed "+error);Application.Quit(1);yield break;}
        var report=new GenerationTimingReport{Runtime="Unity Mono "+Application.unityVersion+(Environment.GetCommandLineArgs().Contains("--worldgen-generation-inprocess")?" (in-process generation)":" (CoreCLR generation worker)"),Cells=world.Surface.Cells.Length,Seed=world.Recipe.Seed,ReadyMs=(Time.realtimeSinceStartupAsDouble-start)*1000,RecipeFile="recipe.json",WorldHash=world.Stages[world.Stages.Count-1].Hash};
        lock(generationMeasurements)
        {
            foreach(var row in generationMeasurements){report.EngineMs+=row.TotalMs;report.ComputeMs+=row.ComputeMs;report.HashMs+=row.HashMs;report.PreviewMs+=row.PreviewMs;}
            report.Stages=generationMeasurements.ConvertAll(row=>new GenerationTimingRow{Stage=row.Stage,ComputeMs=row.ComputeMs,HashMs=row.HashMs,PreviewMs=row.PreviewMs,TotalMs=row.TotalMs,Percent=100*row.TotalMs/report.EngineMs}).ToArray();
        }
        File.WriteAllText(Path.Combine(output,"recipe.json"),WorldJson.Write(world.Recipe));File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(report,true));Debug.Log("GENERATION PROFILE PASS "+report.ReadyMs+"ms");Application.Quit(0);
    }
}
