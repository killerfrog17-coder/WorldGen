using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    [Serializable] sealed class DialogueQualityReply
    {
        public string Label,Question,Speech,Action,Error;public bool Graded,Passed;public double Seconds,TokensPerSecond;public int InputTokens,OutputTokens;
        public NpcChatMessage[] Messages;
    }
    [Serializable] sealed class DialogueQualityModel
    {public string Model,Family,Backend,Error;public double LoadSeconds;public int Passed,Graded;public List<DialogueQualityReply> Replies=new List<DialogueQualityReply>();}
    [Serializable] sealed class DialogueQualitySuite
    {public string Passkey,Conditions;public List<DialogueQualityModel> Models=new List<DialogueQualityModel>();}
    IEnumerator CheckNpcDialogueRuntime()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);AudioListener.volume=0;
        string originalPath=NpcModelClient.ConfigPath;byte[] original=File.ReadAllBytes(originalPath);var originalConfig=NpcModelClient.LoadSettings();
        NpcModelClient.TestSettingsPath=Path.Combine(output,"settings.json");
        var suite=new DialogueQualitySuite{Passkey="talun"+new System.Random(Environment.TickCount).Next(1000,9999),Conditions="Same16turns/model, CUDA,4096context,2recentexchanges,durableattributedstatements,temperature0.2,Maxspeed15FPSwhilebusy. Synthetic facts through the production client and prompt builder."};
        void Save()=>File.WriteAllText(Path.Combine(output,"dialogue-quality.json"),JsonUtility.ToJson(suite,true));
        IEnumerator Wait(Task task)
        {double deadline=Time.realtimeSinceStartupAsDouble+240;while(!task.IsCompleted&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;}
        bool Match(DialogueQualityReply r,string expected)
        {
            if(!string.IsNullOrEmpty(r.Error))return false;string text=(r.Speech??"").ToLowerInvariant();
            if(expected=="greeting")return !text.Contains("spirit")&&!text.Contains("blessing")&&!text.Contains("many days");
            if(expected=="unknown")return !text.Contains("lena")&&new[]{"don't know","do not know","not know","don't remember","do not remember","not sure","unsure","unknown","not told","haven't told","cannot recall","can't recall","forgot","not provided","no information","not been told"}.Any(text.Contains);
            if(expected=="approach")return r.Action=="approach"||r.Action=="speak";
            if(expected=="27")return (text.Contains("27")||text.Contains("twenty-seven")||text.Contains("twenty seven"))&&!text.Contains("61")&&!text.Contains("not 27");
            if(expected=="2")return (text.Contains("two")||text.Contains("2"))&&!text.Contains("not two")&&!text.Contains("three");
            if(expected=="3")return (text.Contains("three")||text.Contains("3"))&&!text.Contains("not three");
            return text.Contains(expected.ToLowerInvariant())&&!text.Contains("not "+expected.ToLowerInvariant());
        }
        string scene="YOU (NPC): name=Doyomu; age in years=61; partner identity=Mira; mother's name=Lena; visible appearance=grey hair. SPEAKER: name=Wufihi; age in years=27; partner identity=Tava; children=unknown; mother's name=unknown; visible appearance=black hair. SPEAKER relationship to YOU: bandmate, not your partner; speaker target ID=player. Current activity: collecting fallen branches for the campfire. Available targets: player, camp, none. Speaker is6m away; no threat.";
        var cases=new[]{
            ("greeting","Sup bro","greeting"),("speaker_age","How old am I?","27"),("speaker_partner","Who is my mate?","Tava"),("visible_hair","What color is my hair?","black"),
            ("remember_word","I want you to remember this secret passkey: "+suite.Passkey+". OK?",""),("immediate_passkey","What word is the secret passkey?",suite.Passkey),
            ("children_statement","I have two children.",""),("children_recall","How many children did I say I have?","2"),("distraction","Nice!",""),("ordinary_activity","What are you doing?",""),
            ("children_after_distraction","How many children did I say I have?","2"),("old_passkey","What was the secret passkey I asked you to remember?",suite.Passkey),
            ("unknown_mother","What is my mother's name?","unknown"),("approach","Come over here, please.","approach"),
            ("correct_children","I now have three children, not two.",""),("updated_claim","How many children did I most recently say I have?","3")};
        try
        {
            foreach(var model in NpcModelClient.InstalledModels(originalConfig))
            {
                if(Argument("--worldgen-ai-model","").Length>0&&model.Family!=Argument("--worldgen-ai-model",""))continue;
                var config=originalConfig.Copy();config.SelectModel(model);config.Preset(3,Environment.ProcessorCount);config.Enabled=true;config.ConversationTurns=2;config.Temperature=.2f;
                var row=new DialogueQualityModel{Model=model.Name,Family=model.Family,Backend=config.GpuBackend};suite.Models.Add(row);
                NpcModelClient.SaveSettings(config);screen="Options";settingsCategory=8;ReloadAiDraft();aiPreview=new NpcModelClient(config);
                var loadWatch=Stopwatch.StartNew();var load=aiPreview.WarmUp();yield return Wait(load);row.LoadSeconds=loadWatch.Elapsed.TotalSeconds;
                if(!load.IsCompleted||load.IsFaulted||load.IsCanceled){row.Error=load.Exception?.GetBaseException().Message??"Model load timed out";Save();DisposeAiPreview();continue;}
                var memory=new WorldGen.Core.NpcMemory{Person=100};
                foreach(var item in cases)
                {
                    var heard=memory.Hear(200,"Wufihi",item.Item2);var reply=new DialogueQualityReply{Label=item.Item1,Question=item.Item2,Graded=item.Item3.Length>0};row.Replies.Add(reply);
                    var task=aiPreview.Ask(scene,"[\"player\",\"camp\",\"none\"]",memory.DialogueFor(200,heard.Sequence,2),heard,memory.Recall(200,item.Item2,heard.Sequence));yield return Wait(task);
                    if(!task.IsCompleted||task.IsFaulted||task.IsCanceled)reply.Error=task.Exception?.GetBaseException().Message??"Reply timed out";
                    else
                    {
                        var decision=JsonUtility.FromJson<WorldGen.Core.NpcDecision>(task.Result);
                        if(decision==null||!decision.Validate(new[]{"player","camp","none"}))reply.Error="Invalid decision";
                        else{reply.Speech=decision.speech;reply.Action=decision.action;memory.Say(200,"Doyomu",decision.speech,heard.Sequence);}
                    }
                    reply.Seconds=aiPreview.LastSeconds;reply.TokensPerSecond=aiPreview.LastTokensPerSecond;reply.InputTokens=aiPreview.LastPromptTokens;reply.OutputTokens=aiPreview.LastGeneratedTokens;reply.Messages=aiPreview.LastMessages;
                    if(reply.Graded){row.Graded++;reply.Passed=Match(reply,item.Item3);if(reply.Passed)row.Passed++;}
                    aiNotice=item.Item2+"\n"+(reply.Error??reply.Speech);Save();UnityEngine.Debug.Log("DIALOGUE QUALITY "+model.Name+" "+reply.Label+" "+(reply.Graded?(reply.Passed?"PASS":"FAIL"):"setup")+": "+(reply.Error??reply.Speech));
                    if(item.Item1=="ordinary_activity"){memory=JsonUtility.FromJson<WorldGen.Core.NpcMemory>(JsonUtility.ToJson(memory));memory.ValidateDialogue();}
                }
                aiNotice=model.Name+": "+row.Passed+" / "+row.Graded+" factual/dialogue checks.";yield return new WaitForEndOfFrame();
                var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,"quality-"+model.Family+".png"),image.EncodeToPNG());Destroy(image);
                DisposeAiPreview();Save();
            }
            if(Convert.ToBase64String(File.ReadAllBytes(originalPath))!=Convert.ToBase64String(original))throw new InvalidOperationException("Owner AI profile changed during isolated comparison.");
            File.WriteAllText(Path.Combine(output,"checks.txt"),"PASS comparison uses production memory/prompt/client\nPASS owner AI profile unchanged\n");
        }
        finally{DisposeAiPreview();NpcModelClient.TestSettingsPath=null;}
        Application.Quit(0);
    }
    IEnumerator CheckAiSettingsRuntime()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);var report=new List<string>();
        void Check(bool ok,string text){if(!ok){UnityEngine.Debug.LogError("AI SETTINGS "+text);File.WriteAllLines(Path.Combine(output,"failed-checks.txt"),report);Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);UnityEngine.Debug.Log("AI SETTINGS PASS "+text);}
        bool Alive(int pid){try{return !Process.GetProcessById(pid).HasExited;}catch(ArgumentException){return false;}}
        IEnumerator Await(Task task)
        {double deadline=Time.realtimeSinceStartupAsDouble+200;while(!task.IsCompleted&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;Check(task.IsCompleted&&!task.IsFaulted&&!task.IsCanceled,task.IsFaulted?task.Exception.GetBaseException().Message:"asynchronous AI operation completed");}
        string originalPath=NpcModelClient.ConfigPath;byte[] original=File.ReadAllBytes(originalPath);var settings=NpcModelClient.LoadSettings();
        int normalFps=Application.targetFrameRate,normalVsync=QualitySettings.vSyncCount;
        NpcModelClient.TestSettingsPath=Path.Combine(output,"settings.json");settings.Enabled=true;NpcModelClient.SaveSettings(settings);
        try
        {
            screen="Options";settingsCategory=8;ReloadAiDraft();Check(SettingsTabs[8]=="AI","AI tab is available in shared settings");
            aiDraft.Preset(2,Environment.ProcessorCount);ApplyAiSettings();var saved=NpcModelClient.LoadSettings();
            Check(saved.GpuLayers==999&&saved.BriefReplies&&saved.ModelPath==settings.ModelPath,"Apply persists automatic GPU preset without changing the model");
            var client=EnsureSettingsAiClient();var watch=Stopwatch.StartNew();yield return Await(client.WarmUp());double boot=watch.Elapsed.TotalSeconds;
            int gpuHost=client.OwnedProcessId;Check(gpuHost>0&&client.Config.GpuLayers==999,"GPU host starts from saved settings");
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
            Check(aiModels.Length==4,"the current model and all three installed candidates appear in the selector");
            var trials=new List<string>();
            foreach(var model in aiModels)
            foreach(var backend in new[]{"cuda","vulkan"})
            {
                if(Argument("--worldgen-ai-model","").Length>0&&model.Family!=Argument("--worldgen-ai-model",""))continue;
                if(Argument("--worldgen-ai-backend","").Length>0&&backend!=Argument("--worldgen-ai-backend",""))continue;
                aiDraft.SelectModel(model);aiDraft.Preset(3,Environment.ProcessorCount);aiDraft.GpuBackend=backend;ApplyAiSettings();saved=NpcModelClient.LoadSettings();
                Check(saved.ModelPath==model.Path&&saved.GpuBackend==backend&&saved.Priority==2&&saved.GpuReserveMiB==512&&saved.Polling==100,"model selection and maximum-speed controls persist: "+model.Name+" / "+backend);
                var test=TestAiForSettings();yield return null;Check(Application.targetFrameRate<=15&&QualitySettings.vSyncCount==0,"AI-priority request temporarily limits rendering: "+model.Name);yield return Await(test);client=SettingsAiClient;int trialHost=client.OwnedProcessId;
                Check(test.Result.Contains("Reply:")&&client.LastGeneratedTokens>0,"selected model returns a validated menu decision: "+model.Name);
                var repeated=TestAiForSettings();yield return Await(repeated);test=repeated;
                trials.Add(model.Name+" | "+client.BackendInfo+" | "+client.LastSeconds.ToString("0.000")+"s total / "+client.LastLoadSeconds.ToString("0.000")+"s load / "+client.LastTokensPerSecond.ToString("0.0")+" tokens/s | "+client.LastPromptTokens+" input / "+client.LastGeneratedTokens+" output | "+test.Result);
                File.WriteAllLines(Path.Combine(output,"model-trials.txt"),trials);
                aiNotice=test.Result;settingsScroll=Vector2.zero;aiAdvanced=true;yield return new WaitForEndOfFrame();
                image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,"model-"+model.Family+"-"+backend+".png"),image.EncodeToPNG());Destroy(image);
                settingsScroll=new Vector2(0,10000);yield return null;yield return new WaitForEndOfFrame();
                image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,"status-"+model.Family+"-"+backend+".png"),image.EncodeToPNG());Destroy(image);
                DisposeAiPreview();yield return new WaitForSecondsRealtime(.3f);Check(!Alive(trialHost),"model switch releases the previous host: "+model.Name);
                Check(Application.targetFrameRate==normalFps&&QualitySettings.vSyncCount==normalVsync,"normal display settings return after inference: "+model.Name);
            }
            File.WriteAllLines(Path.Combine(output,"model-trials.txt"),trials);
            Check(Convert.ToBase64String(File.ReadAllBytes(originalPath))==Convert.ToBase64String(original),"test leaves the owner's saved AI settings untouched");
            File.WriteAllLines(Path.Combine(output,"checks.txt"),report);
        }
        finally{DisposeAiPreview();NpcModelClient.TestSettingsPath=null;}
        Application.Quit(0);
    }
}
