using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Serialization;

public static class WorldGenBatch
{
    static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
    public static void Validate()
    {
        try
        {
            string output=Path.Combine(Root,"artifacts/unity-editor");Directory.CreateDirectory(output);
            foreach(var entry in GoldenCorpus.Recipes())
            {
                var pipe=new WorldPipeline();var w=pipe.Generate(entry.Recipe);var repeat=pipe.Reapply(WorldJson.ReadRecipe(WorldJson.Write(entry.Recipe)));
                if(!w.Stages.Select(s=>s.Hash).SequenceEqual(repeat.Stages.Select(s=>s.Hash)))throw new Exception("Unity reapply mismatch: "+entry.Name);
                if(StateHash.Compute(WorldJson.ReadWorld(WorldJson.Write(w)))!=StateHash.Compute(w))throw new Exception("Unity serialization mismatch");
                string expected=Path.Combine(Root,"tests/golden",entry.Name+".hashes.json");
                if(File.Exists(expected)&&!WorldJson.Read<System.Collections.Generic.List<StageRecord>>(File.ReadAllText(expected)).Select(s=>s.Hash).SequenceEqual(w.Stages.Select(s=>s.Hash)))throw new Exception("CLI/Unity stage hash mismatch: "+entry.Name);
                File.WriteAllText(Path.Combine(output,entry.Name+".hashes.json"),WorldJson.Write(w.Stages));Debug.Log("WORLDGEN PASS "+entry.Name+" "+w.Stages.Last().Hash);
            }
            Debug.Log("WORLDGEN UNITY VALIDATION PASSED");
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void BuildPlayer()
    {
        var output=Path.Combine(Root,"artifacts/player/WorldGen.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
        // Runtime-created cameras are invisible to scene-only postprocess stripping analysis.
        var stripping=UnityEngine.Rendering.GraphicsSettings.GetRenderPipelineSettings<UnityEngine.Rendering.Universal.URPShaderStrippingSetting>();
        bool prior=stripping!=null&&stripping.stripUnusedPostProcessingVariants;
        try
        {
            if(stripping!=null)stripping.stripUnusedPostProcessingVariants=false;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Player build failed: "+report.summary.result);
        }
        finally{if(stripping!=null)stripping.stripUnusedPostProcessingVariants=prior;}
    }
}
