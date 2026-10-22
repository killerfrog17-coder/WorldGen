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
                var args=Environment.GetCommandLineArgs();int caseArgument=Array.IndexOf(args,"--worldgen-case");
                if(caseArgument>=0 && caseArgument+1<args.Length && entry.Name!=args[caseArgument+1])continue;
                var pipe=new WorldPipeline();var w=pipe.Generate(entry.Recipe);var repeat=pipe.Reapply(WorldJson.ReadRecipe(WorldJson.Write(entry.Recipe)));
                if(!w.Stages.Select(s=>s.Hash).SequenceEqual(repeat.Stages.Select(s=>s.Hash)))throw new Exception("Unity reapply mismatch: "+entry.Name);
                string serialized = WorldJson.Write(w);
                var restored = WorldJson.Read<World>(serialized);
                if(StateHash.Compute(restored)!=StateHash.Compute(w))
                {
                    File.WriteAllText(Path.Combine(output,entry.Name+".before.json"),serialized);
                    File.WriteAllText(Path.Combine(output,entry.Name+".after.json"),WorldJson.Write(restored));
                    Diagnose(w,restored,"world");
                    throw new Exception("Unity serialization mismatch: " + entry.Name);
                }
                WorldJson.ReadWorld(serialized);
                string expected=Path.Combine(Root,"tests/golden/v3",entry.Name+".hashes.json");
                if(File.Exists(expected)&&!WorldJson.Read<System.Collections.Generic.List<StageRecord>>(File.ReadAllText(expected)).Select(s=>s.Hash).SequenceEqual(w.Stages.Select(s=>s.Hash)))throw new Exception("CLI/Unity stage hash mismatch: "+entry.Name);
                File.WriteAllText(Path.Combine(output,entry.Name+".hashes.json"),WorldJson.Write(w.Stages));Debug.Log("WORLDGEN PASS "+entry.Name+" "+w.Stages.Last().Hash);
            }
            Debug.Log("WORLDGEN UNITY VALIDATION PASSED");
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Diagnose(object before,object after,string path)
    {
        if(before==null || after==null)return;
        if(before is double value){double restored=(double)after;if(BitConverter.DoubleToInt64Bits(D.Q(value,1e6))!=BitConverter.DoubleToInt64Bits(D.Q(restored,1e6)))Debug.LogError("HASH DOUBLE "+path+" "+value.ToString("R")+" -> "+restored.ToString("R")+" quantized "+D.Q(value,1e6).ToString("R")+" -> "+D.Q(restored,1e6).ToString("R"));return;}
        if(before is string || before.GetType().IsPrimitive || before.GetType().IsEnum)return;
        if(before is System.Collections.IEnumerable sequence){var other=((System.Collections.IEnumerable)after).GetEnumerator();int i=0;foreach(var item in sequence){if(!other.MoveNext())return;Diagnose(item,other.Current,path+"["+i+++"]");}return;}
        foreach(var field in before.GetType().GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))Diagnose(field.GetValue(before),field.GetValue(after),path+"."+field.Name);
    }
    public static void BuildPlayer()
    {
        var arguments=Environment.GetCommandLineArgs();int outputArgument=Array.IndexOf(arguments,"--worldgen-build-output");
        var output=outputArgument>=0&&outputArgument+1<arguments.Length?arguments[outputArgument+1]:Path.Combine(Root,"artifacts/player/WorldGen.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
        // Runtime-created cameras are invisible to scene-only postprocess stripping analysis.
        var stripping=UnityEngine.Rendering.GraphicsSettings.GetRenderPipelineSettings<UnityEngine.Rendering.Universal.URPShaderStrippingSetting>();
        bool prior=stripping!=null&&stripping.stripUnusedPostProcessingVariants;
        var namedTarget=UnityEditor.Build.NamedBuildTarget.Standalone;
        var priorBackend=PlayerSettings.GetScriptingBackend(namedTarget);
        var priorManagedStripping=PlayerSettings.GetManagedStrippingLevel(namedTarget);
        bool priorTiming=PlayerSettings.enableFrameTimingStats;
        string priorProduct=PlayerSettings.productName;
        try
        {
            if(stripping!=null)stripping.stripUnusedPostProcessingVariants=false;
            if(arguments.Contains("--worldgen-performance-build")){PlayerSettings.enableFrameTimingStats=true;PlayerSettings.productName=priorProduct+".Performance";}
            // Optional native-code experiment. Restore project preferences even
            // on failure; ordinary development builds retain their old backend.
            if(arguments.Contains("--worldgen-il2cpp-player"))
            {
                PlayerSettings.SetScriptingBackend(namedTarget,ScriptingImplementation.IL2CPP);
                PlayerSettings.SetManagedStrippingLevel(namedTarget,ManagedStrippingLevel.Minimal);
            }
            // Ordinary player builds should not silently carry the development
            // runtime/debug overlay. Diagnostic development builds are opt-in.
            var options=arguments.Contains("--worldgen-development-player")?BuildOptions.Development:BuildOptions.None;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=output,target=BuildTarget.StandaloneWindows64,options=options});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Player build failed: "+report.summary.result);
        }
        finally{PlayerSettings.productName=priorProduct;PlayerSettings.enableFrameTimingStats=priorTiming;if(stripping!=null)stripping.stripUnusedPostProcessingVariants=prior;PlayerSettings.SetScriptingBackend(namedTarget,priorBackend);PlayerSettings.SetManagedStrippingLevel(namedTarget,priorManagedStripping);}
    }
}
