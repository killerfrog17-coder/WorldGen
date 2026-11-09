using System;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    NpcAiSettings aiDraft;int aiDraftRevision=-1;bool aiAdvanced,aiDirty;string aiNotice="";
    NpcAiModel[] aiModels=Array.Empty<NpcAiModel>();
    NpcModelClient aiPreview;Task<string> aiSettingsTest;
    NpcModelClient SettingsAiClient=>terrainExplorer!=null?terrainExplorer.NpcAiRuntime:aiPreview;
    NpcModelClient EnsureSettingsAiClient()=>terrainExplorer!=null?terrainExplorer.EnsureNpcAiRuntime():aiPreview??(aiPreview=new NpcModelClient());
    void DisposeAiPreview(){aiPreview?.Dispose();aiPreview=null;if(aiSettingsTest!=null)aiSettingsTest.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);aiSettingsTest=null;}
    void ReloadAiDraft(){aiDraft=NpcModelClient.LoadSettings();aiModels=NpcModelClient.InstalledModels(aiDraft);aiDraftRevision=NpcModelClient.SettingsRevision;aiDirty=false;}
    void ApplyAiSettings()
    {
        try{NpcModelClient.SaveSettings(aiDraft);DisposeAiPreview();terrainExplorer?.ReloadNpcAiSettings();ReloadAiDraft();aiNotice="Saved. The AI host will use these settings on its next load.";}
        catch(Exception ex){aiNotice=ex.Message;}
    }
    int AiSlider(string label,int value,int minimum,int maximum,string hint)
    {
        GUILayout.Label(label+"  "+value);int result=Mathf.RoundToInt(GUILayout.HorizontalSlider(value,minimum,maximum));
        if(!string.IsNullOrEmpty(hint))GUILayout.Label(hint,MutedStyle());return result;
    }
    void DrawAiSettings()
    {
        SettingsAiClient?.ReleaseIfIdle();
        if(aiDraft==null||aiDraftRevision!=NpcModelClient.SettingsRevision)ReloadAiDraft();
        if(aiSettingsTest?.IsCompleted==true)
        {
            var task=aiSettingsTest;aiSettingsTest=null;
            aiNotice=task.IsFaulted?task.Exception.GetBaseException().Message:task.IsCanceled?"AI test cancelled.":task.Result;
        }
        GUILayout.Label("NPC AI",SectionStyle());
        GUILayout.Label("Local, free inference. Schedules continue independently; thinking is prompted by events.",MutedStyle());
        bool prior=GUI.changed;GUI.changed=false;
        GUILayout.Label("Model");
        int selected=Array.FindIndex(aiModels,m=>string.Equals(m.Path,aiDraft.ModelPath,StringComparison.OrdinalIgnoreCase));
        string[] modelNames=Array.ConvertAll(aiModels,m=>m.Name+(m.Bytes>0?" ("+(m.Bytes/1073741824d).ToString("0.0")+" GiB)":""));
        if(aiModels.Length>0)
        {
            int next=GUILayout.SelectionGrid(selected,modelNames,2);
            if(next>=0&&next!=selected){aiDraft.SelectModel(aiModels[next]);aiDirty=true;}
        }
        GUILayout.Label(aiDraft.ModelPath,MutedStyle());
        aiDraft.Enabled=GUILayout.Toggle(aiDraft.Enabled,"Enable NPC thoughts and dialogue");
        GUILayout.Space(8);GUILayout.Label("Hardware starting points");
        GUILayout.BeginHorizontal();
        int presetIndex=0;foreach(var preset in new[]{"CPU","Hybrid","GPU","Max speed"}){if(GUILayout.Button(preset)){aiDraft.Preset(presetIndex,Environment.ProcessorCount,SystemInfo.graphicsMemorySize);aiDirty=true;}presetIndex++;}
        GUILayout.EndHorizontal();GUILayout.Label("Max speed prioritizes AI: high CPU priority, active polling and 15 FPS while generating. Your normal frame limit returns afterward. Thread tuning may still improve speed.",MutedStyle());
        if(System.IO.File.Exists(aiDraft.CudaServerPath)){GUILayout.Label("GPU backend");aiDraft.GpuBackend=GUILayout.SelectionGrid(aiDraft.GpuBackend=="cuda"?1:0,new[]{"Vulkan","CUDA (NVIDIA)"},2)==1?"cuda":"vulkan";}
        bool autoGpu=GUILayout.Toggle(aiDraft.GpuLayers==999,"Automatically fit as much as possible on the GPU");
        if(autoGpu)aiDraft.GpuLayers=999;
        else{if(aiDraft.GpuLayers==999)aiDraft.GpuLayers=Math.Min(18,aiDraft.ModelLayers);aiDraft.GpuLayers=AiSlider("GPU layers (requested)",aiDraft.GpuLayers,0,aiDraft.ModelLayers,"0 = CPU only. The maximum depends on the selected model. Status shows actual offload.");}
        GUILayout.Label(SystemInfo.graphicsDeviceName+" · "+(SystemInfo.graphicsMemorySize/1024f).ToString("0.0")+" GB total VRAM",MutedStyle());
        aiDraft.Threads=AiSlider("Generation CPU threads",aiDraft.Threads,1,Environment.ProcessorCount,"More CPU workers may speed up inference, but can compete with the game. More is not always faster.");
        aiDraft.BriefReplies=GUILayout.Toggle(aiDraft.BriefReplies,"Brief replies");GUILayout.Label("Shorter speech and thoughts reduce the amount of text the model must generate.",MutedStyle());
        aiDraft.MumbleSpeech=GUILayout.Toggle(aiDraft.MumbleSpeech,"Mumbled NPC speech");
        aiDraft.SpeechCharactersPerSecond=AiSlider("Dialogue reading pace",aiDraft.SpeechCharactersPerSecond,12,60,"Characters per second. Text arrives as generated and is revealed no faster than this pace.");
        bool dirtyBeforeFold=GUI.changed;GUI.changed=false;aiAdvanced=GUILayout.Toggle(aiAdvanced,"Advanced AI settings");GUI.changed=dirtyBeforeFold;
        if(aiAdvanced)
        {
            GUILayout.Space(8);GUILayout.Label("CPU priority");aiDraft.Priority=GUILayout.SelectionGrid(aiDraft.Priority+1,new[]{"Low","Normal","Above normal","High"},4)-1;
            GUILayout.Label("Higher priority lets AI compete more aggressively with the game. It does not guarantee higher token throughput.",MutedStyle());
            aiDraft.BatchThreads=AiSlider("Prompt-processing threads",aiDraft.BatchThreads,1,Environment.ProcessorCount,"Used while reading the prompt. Can benefit from more workers than generation does.");
            aiDraft.Polling=AiSlider("Active CPU polling (%)",aiDraft.Polling,0,100,"Keeps workers awake for lower scheduling latency; uses CPU even while waiting during inference.");
            aiDraft.GpuReserveMiB=AiSlider("Free VRAM reserve (MiB)",aiDraft.GpuReserveMiB,0,2048,"Automatic fitting leaves this margin. Max speed uses 512; ordinary GPU uses 1,024. You can try 0, but spilling into shared memory can be much slower.");
            aiDraft.InferenceFps=AiSlider("Game FPS limit while AI works",aiDraft.InferenceFps,0,120,"0 keeps your normal display settings. A lower limit frees rendering time for inference; normal FPS and VSync return when the request ends.");
            if(aiDraft.ModelExpertLayers>0)aiDraft.CpuExpertLayers=AiSlider("Expert layers kept on CPU",aiDraft.CpuExpertLayers,0,aiDraft.ModelExpertLayers,"For expert models: keep expert weights in RAM while attention runs on GPU. This helps larger models fit; compare values for speed.");
            aiDraft.PreloadModel=GUILayout.Toggle(aiDraft.PreloadModel,"Preload CPU model weights into RAM");GUILayout.Label("Uses explicit RAM buffers instead of mapped files. Recommended for CPU expert weights on a hard drive; leave off if memory is tight.",MutedStyle());
            GUILayout.Label("Context window");int window=aiDraft.ContextTokens<=2048?0:aiDraft.ContextTokens<=4096?1:2;
            window=GUILayout.SelectionGrid(window,new[]{"2,048 tokens","4,096 tokens","8,192 tokens"},3);aiDraft.ContextTokens=window==0?2048:window==1?4096:8192;
            GUILayout.Label("4,096 is recommended; Gemma presets use 2,048 on 8 GB GPUs to reduce memory pressure. You can override it. Older exchanges trim to fit; remembered statements are separate.",MutedStyle());
            aiDraft.ConversationTurns=AiSlider("Recent conversation turns",aiDraft.ConversationTurns,0,12,"Each turn includes what the speaker said and the NPC's spoken reply. Private intentions are excluded. Relevant remembered statements persist beyond this window.");
            aiDraft.MaxTokens=AiSlider("Output token limit",aiDraft.MaxTokens,128,384,"A token is roughly a word fragment. This is a hard ceiling, not a requested reply length; too low can cut off a valid response.");
            GUILayout.Label("Creativity  "+aiDraft.Temperature.ToString("0.00"));aiDraft.Temperature=Mathf.Round(GUILayout.HorizontalSlider(aiDraft.Temperature,0,1.2f)*20)/20;
            GUILayout.Label("Higher values increase variation and the chance of mistakes. This does not make the model smarter.",MutedStyle());
            aiDraft.BackgroundReactions=GUILayout.Toggle(aiDraft.BackgroundReactions,"Nearby conversations and unsolicited greetings");
            aiDraft.VisitorCooldownSeconds=AiSlider("Background reaction spacing (seconds)",aiDraft.VisitorCooldownSeconds,20,300,"Minimum spacing between new visitor thoughts across nearby communities. Direct dialogue and injury reactions keep priority.");
            aiDraft.IdleMinutes=AiSlider("Unload after inactivity (minutes)",aiDraft.IdleMinutes,0,30,"0 = keep loaded. Unloading releases model memory/VRAM; the next interaction must load it again.");
        }
        aiDirty|=GUI.changed;GUI.changed=prior;
        GUILayout.Space(10);GUILayout.Label(aiDirty?"Unsaved changes. Apply restarts the AI host and cancels pending replies.":"Settings saved per PC. Applying requires no world reload.",MutedStyle());
        GUILayout.BeginHorizontal();if(GUILayout.Button("Apply & restart AI"))ApplyAiSettings();if(GUILayout.Button("Discard changes")){ReloadAiDraft();aiNotice="";}GUILayout.EndHorizontal();
        var client=SettingsAiClient;GUILayout.Space(12);GUILayout.Label("AI STATUS",SectionStyle());
        GUILayout.Label(client?.Status??"Not loaded");if(client!=null)GUILayout.Label(client.BackendInfo,MutedStyle());
        if(client?.LastSeconds>0)GUILayout.Label("Last request: "+client.LastSeconds.ToString("0.00")+" s · "+client.LastPromptTokens+" input / "+client.LastGeneratedTokens+" output tokens");
        if(client?.LastFirstSpeechSeconds>0)GUILayout.Label("First speech text: "+client.LastFirstSpeechSeconds.ToString("0.00")+" s",MutedStyle());
        if(client?.LastTokensPerSecond>0)GUILayout.Label("Generation: "+client.LastTokensPerSecond.ToString("0.0")+" tokens/s; model load: "+client.LastLoadSeconds.ToString("0.00")+" s",MutedStyle());
        bool enabled=GUI.enabled;GUI.enabled=enabled&&!aiDirty&&aiSettingsTest==null&&client?.IsBusy!=true&&aiDraft.Enabled;
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Load model"))aiSettingsTest=WarmAiForSettings();
        if(GUILayout.Button("Test saved settings"))aiSettingsTest=TestAiForSettings();
        GUILayout.EndHorizontal();GUI.enabled=enabled;
        if(GUILayout.Button("Unload AI now")){DisposeAiPreview();terrainExplorer?.ReloadNpcAiSettings();aiNotice="AI unloaded. It will load again when needed.";}
        GUILayout.Label("The short test measures reply time, not FPS. A first GPU request can be slower while shaders prepare; repeat to measure warmed-up replies.",MutedStyle());
        if(aiSettingsTest!=null)GUILayout.Label("Working… The menu remains usable.");if(aiNotice.Length>0)GUILayout.Label(aiNotice,MutedStyle());
    }
    async Task<string> WarmAiForSettings(){await EnsureSettingsAiClient().WarmUp();return "Model loaded.";}
    async Task<string> TestAiForSettings()
    {
        var client=EnsureSettingsAiClient();string reply=await client.Ask("YOU: Aru. Current activity: collecting fallen branches for campfire. Speaker: a member of your band, target player, 2m away. No confirmed family relationship. They said: Hello, what are you doing?","[\"player\",\"camp\",\"none\"]");
        var decision=JsonUtility.FromJson<WorldGen.Core.NpcDecision>(reply);if(decision==null||!decision.Validate(new[]{"player","camp","none"}))throw new InvalidOperationException("The model did not return a valid decision.");
        return "Short test: "+client.LastSeconds.ToString("0.00")+" s. Reply: "+decision.speech;
    }
}
