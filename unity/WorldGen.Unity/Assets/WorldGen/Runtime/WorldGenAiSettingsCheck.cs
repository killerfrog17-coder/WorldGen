using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckAiSettingsRuntime()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);var report=new List<string>();
        void Check(bool ok,string text){if(!ok){UnityEngine.Debug.LogError("AI SETTINGS "+text);File.WriteAllLines(Path.Combine(output,"failed-checks.txt"),report);Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);UnityEngine.Debug.Log("AI SETTINGS PASS "+text);}
        bool Alive(int pid){try{return !Process.GetProcessById(pid).HasExited;}catch(ArgumentException){return false;}}
        IEnumerator Await(Task task)
        {double deadline=Time.realtimeSinceStartupAsDouble+75;while(!task.IsCompleted&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;Check(task.IsCompleted&&!task.IsFaulted&&!task.IsCanceled,task.IsFaulted?task.Exception.GetBaseException().Message:"asynchronous AI operation completed");}
        string originalPath=NpcModelClient.ConfigPath;byte[] original=File.ReadAllBytes(originalPath);var settings=NpcModelClient.LoadSettings();
        NpcModelClient.TestSettingsPath=Path.Combine(output,"settings.json");NpcModelClient.SaveSettings(settings);
        try
        {
            screen="Options";settingsCategory=8;ReloadAiDraft();Check(SettingsTabs[8]=="AI","AI tab is available in shared settings");
            aiDraft.Preset(2,Environment.ProcessorCount);ApplyAiSettings();var saved=NpcModelClient.LoadSettings();
            Check(saved.GpuLayers==37&&saved.BriefReplies&&saved.ModelPath==settings.ModelPath,"Apply persists GPU preset without changing the model");
            var client=EnsureSettingsAiClient();var watch=Stopwatch.StartNew();yield return Await(client.WarmUp());double boot=watch.Elapsed.TotalSeconds;
            int gpuHost=client.OwnedProcessId;Check(gpuHost>0&&client.Config.GpuLayers==37,"GPU host starts from saved settings");
            var first=TestAiForSettings();var duplicate=client.Ask("duplicate","[\"player\"]");
            yield return Await(first);Check(duplicate.IsFaulted&&duplicate.Exception.GetBaseException() is InvalidOperationException,"parallel requests are rejected without another host");
            Check(client.LastSeconds>0&&client.LastGeneratedTokens>0&&first.Result.Contains("Reply:"),"menu test reports actual response time and a validated reply");
            Check(client.BackendInfo.Contains("37/37"),"GPU backend confirms all 37 layers actually offloaded: "+client.BackendInfo);
            aiNotice=first.Result;settingsScroll=Vector2.zero;yield return new WaitForEndOfFrame();
            var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,"ai-settings-top.png"),image.EncodeToPNG());Destroy(image);
            settingsScroll=new Vector2(0,10000);yield return null;yield return new WaitForEndOfFrame();image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,"ai-settings-status.png"),image.EncodeToPNG());Destroy(image);
            File.WriteAllText(Path.Combine(output,"gpu-receipt.txt"),"Boot seconds: "+boot+"\nReply seconds: "+client.LastSeconds+"\nInput tokens: "+client.LastPromptTokens+"\nOutput tokens: "+client.LastGeneratedTokens+"\nBackend: "+client.BackendInfo+"\n"+first.Result);
            client.Config.IdleMinutes=1;typeof(NpcModelClient).GetField("lastUse",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(client,Stopwatch.GetTimestamp()-(long)(61d*Stopwatch.Frequency));client.ReleaseIfIdle();
            yield return new WaitForSecondsRealtime(.3f);Check(!Alive(gpuHost),"idle unload releases the GPU model process");
            aiDraft.Preset(0,Environment.ProcessorCount);ApplyAiSettings();Check(NpcModelClient.LoadSettings().GpuLayers==0,"CPU preset persists after restart");
            client=EnsureSettingsAiClient();yield return Await(client.WarmUp());int cpuHost=client.OwnedProcessId;DisposeAiPreview();yield return new WaitForSecondsRealtime(.3f);
            Check(!Alive(cpuHost),"manual unload closes the CPU model process");
            Check(Convert.ToBase64String(File.ReadAllBytes(originalPath))==Convert.ToBase64String(original),"test leaves the owner's saved AI settings untouched");
            File.WriteAllLines(Path.Combine(output,"checks.txt"),report);
        }
        finally{DisposeAiPreview();NpcModelClient.TestSettingsPath=null;}
        Application.Quit(0);
    }
}
