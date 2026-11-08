using System;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    NpcAiSettings aiDraft;int aiDraftRevision=-1;bool aiAdvanced,aiDirty;string aiNotice="";
    NpcModelClient aiPreview;Task<string> aiSettingsTest;
    NpcModelClient SettingsAiClient=>terrainExplorer!=null?terrainExplorer.NpcAiRuntime:aiPreview;
    NpcModelClient EnsureSettingsAiClient()=>terrainExplorer!=null?terrainExplorer.EnsureNpcAiRuntime():aiPreview??(aiPreview=new NpcModelClient());
    void DisposeAiPreview(){aiPreview?.Dispose();aiPreview=null;if(aiSettingsTest!=null)aiSettingsTest.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);aiSettingsTest=null;}
    void ReloadAiDraft(){aiDraft=NpcModelClient.LoadSettings();aiDraftRevision=NpcModelClient.SettingsRevision;aiDirty=false;}
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
        if(aiDraft==null||aiDraftRevision!=NpcModelClient.SettingsRevision)ReloadAiDraft();
        if(aiSettingsTest?.IsCompleted==true)
        {
            var task=aiSettingsTest;aiSettingsTest=null;
            aiNotice=task.IsFaulted?task.Exception.GetBaseException().Message:task.IsCanceled?"AI test cancelled.":task.Result;
        }
        GUILayout.Label("NPC AI",SectionStyle());
        GUILayout.Label("Model: "+aiDraft.ModelName);GUILayout.Label("Local, free inference. Schedules continue independently; thinking is prompted by events.",MutedStyle());
        bool prior=GUI.changed;GUI.changed=false;
        aiDraft.Enabled=GUILayout.Toggle(aiDraft.Enabled,"Enable NPC thoughts and dialogue");
        GUILayout.Space(8);GUILayout.Label("Hardware starting points");
        GUILayout.BeginHorizontal();
        foreach(var preset in new[]{"CPU","Hybrid","GPU"})if(GUILayout.Button(preset)){aiDraft.Preset(preset=="CPU"?0:preset=="Hybrid"?1:2,Environment.ProcessorCount);aiDirty=true;}
        GUILayout.EndHorizontal();GUILayout.Label("CPU: protects GPU resources. Hybrid: shares work. GPU: prioritizes response speed. All use the same model.",MutedStyle());
        aiDraft.GpuLayers=AiSlider("GPU layers",aiDraft.GpuLayers,0,37,"0 = CPU only. 37 = full offload. More layers use more graphics memory and can compete with rendering.");
        GUILayout.Label(SystemInfo.graphicsDeviceName+" · "+(SystemInfo.graphicsMemorySize/1024f).ToString("0.0")+" GB total VRAM",MutedStyle());
        aiDraft.Threads=AiSlider("CPU threads",aiDraft.Threads,1,Environment.ProcessorCount,"More CPU workers may speed up inference, but can compete with the game. More is not always faster.");
        aiDraft.BriefReplies=GUILayout.Toggle(aiDraft.BriefReplies,"Brief replies");GUILayout.Label("Shorter speech and thoughts reduce the amount of text the model must generate.",MutedStyle());
        bool dirtyBeforeFold=GUI.changed;GUI.changed=false;aiAdvanced=GUILayout.Toggle(aiAdvanced,"Advanced AI settings");GUI.changed=dirtyBeforeFold;
        if(aiAdvanced)
        {
            GUILayout.Space(8);GUILayout.Label("CPU priority");aiDraft.Priority=GUILayout.SelectionGrid(aiDraft.Priority<0?0:1,new[]{"Low","Normal"},2)==0?-1:0;
            GUILayout.Label("Low gives the game preference when CPU work competes. Normal can improve AI responsiveness at the expense of gameplay.",MutedStyle());
            GUILayout.Label("Context window");int window=aiDraft.ContextTokens<=2048?0:aiDraft.ContextTokens<=4096?1:2;
            window=GUILayout.SelectionGrid(window,new[]{"2,048 tokens","4,096 tokens","8,192 tokens"},3);aiDraft.ContextTokens=window==0?2048:window==1?4096:8192;
            GUILayout.Label("Maximum input/output capacity, not intelligence. Larger windows reserve more memory. Keep 2,048 unless you need more history.",MutedStyle());
            aiDraft.HistoryRecords=AiSlider("Recent memory entries",aiDraft.HistoryRecords,0,12,"Recent conversation and intentions included with each request. More history gives context but adds processing work.");
            aiDraft.MaxTokens=AiSlider("Output token limit",aiDraft.MaxTokens,128,384,"A token is roughly a word fragment. This is a hard ceiling, not a requested reply length; too low can cut off a valid response.");
            GUILayout.Label("Creativity  "+aiDraft.Temperature.ToString("0.00"));aiDraft.Temperature=Mathf.Round(GUILayout.HorizontalSlider(aiDraft.Temperature,0,1.2f)*20)/20;
            GUILayout.Label("Higher values increase variation and the chance of mistakes. This does not make the model smarter.",MutedStyle());
            aiDraft.BackgroundReactions=GUILayout.Toggle(aiDraft.BackgroundReactions,"Allow unsolicited reactions to unfamiliar visitors");
            aiDraft.VisitorCooldownSeconds=AiSlider("Background reaction spacing (seconds)",aiDraft.VisitorCooldownSeconds,20,300,"Minimum spacing between new visitor thoughts across nearby communities. Direct dialogue and injury reactions keep priority.");
            aiDraft.IdleMinutes=AiSlider("Unload after inactivity (minutes)",aiDraft.IdleMinutes,0,30,"0 = keep loaded. Unloading releases model memory/VRAM; the next interaction must load it again.");
        }
        aiDirty|=GUI.changed;GUI.changed=prior;
        GUILayout.Space(10);GUILayout.Label(aiDirty?"Unsaved changes. Apply restarts the AI host and cancels pending replies.":"Settings saved per PC. Applying requires no world reload.",MutedStyle());
        GUILayout.BeginHorizontal();if(GUILayout.Button("Apply & restart AI"))ApplyAiSettings();if(GUILayout.Button("Discard changes")){ReloadAiDraft();aiNotice="";}GUILayout.EndHorizontal();
        var client=SettingsAiClient;GUILayout.Space(12);GUILayout.Label("AI STATUS",SectionStyle());
        GUILayout.Label(client?.Status??"Not loaded");
        if(client?.LastSeconds>0)GUILayout.Label("Last request: "+client.LastSeconds.ToString("0.00")+" s · "+client.LastPromptTokens+" input / "+client.LastGeneratedTokens+" output tokens");
        bool enabled=GUI.enabled;GUI.enabled=enabled&&!aiDirty&&aiSettingsTest==null&&client?.IsBusy!=true&&aiDraft.Enabled;
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Load model"))aiSettingsTest=WarmAiForSettings();
        if(GUILayout.Button("Test saved settings"))aiSettingsTest=TestAiForSettings();
        GUILayout.EndHorizontal();GUI.enabled=enabled;
        if(GUILayout.Button("Unload AI now")){DisposeAiPreview();terrainExplorer?.ReloadNpcAiSettings();aiNotice="AI unloaded. It will load again when needed.";}
        GUILayout.Label("The short synthetic test measures reply time, not game FPS. Loading ahead avoids model startup work but does not remove prompt processing.",MutedStyle());
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
