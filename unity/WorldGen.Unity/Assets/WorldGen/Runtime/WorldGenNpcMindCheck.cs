using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckNpcMindRuntime()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);AudioListener.volume=0;var report=new List<string>();
        string profile=Argument("--worldgen-ai-profile","");
        if(profile.Length>0){var config=NpcModelClient.LoadSettings();string family=Argument("--worldgen-ai-model","");if(family.Length>0)config.SelectModel(NpcModelClient.InstalledModels(config).First(m=>m.Family==family));config.Preset(profile=="max"?3:profile=="gpu"?2:profile=="hybrid"?1:0,Environment.ProcessorCount);NpcModelClient.TestSettingsPath=Path.Combine(output,"ai-profile.json");NpcModelClient.SaveSettings(config);}
        double deadline=Time.realtimeSinceStartupAsDouble+240;
        while((Busy||!worldReady||humans==null||humanStep!=null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(!worldReady||Busy||humanStep!=null)throw new InvalidOperationException("NPC test world failed to load");
        humanPlaying=false;ChooseWorldEntry();StartWorldEntry(true);deadline=Time.realtimeSinceStartupAsDouble+300;
        while((worldEntryOpen||terrainExplorer==null||!terrainExplorer.Ready||!terrainExplorer.CenterReady)&&Time.realtimeSinceStartupAsDouble<deadline)
        {if(worldEntryError.Length>0)throw new InvalidOperationException(worldEntryError);yield return null;}
        if(terrainExplorer==null||!terrainExplorer.Ready||!terrainExplorer.CenterReady)throw new InvalidOperationException("NPC test ground failed to load");
        yield return terrainExplorer.CheckNpcMinds(output,report);
        CapturePlayer();play.Copy().Validate();report.Add("PASS captured session validates with conversation memories");
        ReturnFromPlay("Simulation");while(terrainUnloading!=null&&!terrainUnloading.isDone)yield return null;
        yield return new WaitForSecondsRealtime(1);File.WriteAllLines(Path.Combine(output,"checks.txt"),report);Application.Quit(0);
    }
}
public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckNpcMinds(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok){Debug.LogError("NPC MIND "+text);File.WriteAllLines(Path.Combine(output,"failed-checks.txt"),report.Concat(new[]{"FAIL "+text}));Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);Debug.Log("NPC MIND PASS "+text);}
        double deadline=Time.realtimeSinceStartupAsDouble+120;
        while(!humanNpcs.Any(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident?.AgeYears>=18&&p.Health>0)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        var actor=humanNpcs.Where(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident?.AgeYears>=18&&p.Health>0).OrderBy(p=>HumanDistance(p.Planet,Position)).FirstOrDefault();
        Check(actor!=null,"actual adult resident exists in the player's community");
        npcSeenVisitors.UnionWith(humanNpcs.Where(p=>p.Resident!=null).Select(p=>p.GroupId));npcEvents.Clear();
        PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();TestHumanView(0);paused=false;mapOpen=ecologyOpen=false;
        void Stand(double range)
        {
            for(int attempt=0;attempt<24;attempt++)
            {double angle=attempt*Math.PI/12,x=actor.Motor.X+Math.Cos(angle)*range,z=actor.Motor.Z+Math.Sin(angle)*range;double floor=VisibleGround(x,z,actor.Motor.Y-TerrainMotion.EyeHeightM);
            if(double.IsNaN(floor))continue;motion.Place(x,z,floor);UpdateCamera();var toward=actor.Root.transform.position+Vector3.up*1.05f-view.transform.position;
            if(TraceCombatObstacle(new Ray(view.transform.position,toward.normalized),toward.magnitude-.15f)==null)break;}
            motion.Flying=false;
            for(int aim=0;aim<8;aim++){UpdateHumans();UpdateCamera();var bone=actor.Root.GetComponentsInChildren<Transform>().First(t=>t.name=="spine_02");var worldDelta=transform.InverseTransformDirection(bone.position-view.transform.position).normalized;heading=Mathf.Atan2(worldDelta.x,worldDelta.z)*Mathf.Rad2Deg;pitch=-Mathf.Asin(worldDelta.y)*Mathf.Rad2Deg;UpdateCamera();}
        }
        Stand(2.8);yield return null;
        var diagnosticHit=TraceLiving(new Ray(view.transform.position,view.transform.forward),4.5f);File.WriteAllText(Path.Combine(output,"aim.txt"),"Activity="+actor.CurrentActivity+" / intended="+actor.ResidentId+" / hit="+diagnosticHit?.Human?.ResidentId+" / origin="+view.transform.position+" / forward="+view.transform.forward+" / actor="+actor.Root.transform.position);Capture(Path.Combine(output,"aim.png"));
        Check(TryNpcInteract(float.MaxValue),"aimed E interaction reaches the resident using the real hit surface");CloseChat();
        int initial=npcModel?.Requests??0;yield return new WaitForSecondsRealtime(3);
        Check((npcModel?.Requests??0)==initial,"ordinary schedule ticks do not invoke the model");
        npcTalkTarget=actor;Check(SpeakToNpc("Hello "+actor.Resident.Name+". What are you doing?"),"typed dialogue reaches selected resident");
        yield return new WaitForSecondsRealtime(1);Check(npcDecisions>0||actor.Speed<.15f,"addressed NPC listens immediately while the model runs");
        deadline=Time.realtimeSinceStartupAsDouble+70;while(npcUtterances<1&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(npcUtterances>=1,"actual local model replied and the executor delivered speech nearby: "+npcLastError);
        Check(npcDecisions>=1&&NpcMemoryFor(actor.ResidentId).Thought.Length>0,"validated fictional thought is available to debug");
        npcThoughts=true;yield return new WaitForEndOfFrame();var screen=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,"npc-conversation.png"),screen.EncodeToPNG());Destroy(screen);
        File.WriteAllText(Path.Combine(output,"first-memory.json"),JsonUtility.ToJson(NpcMemoryFor(actor.ResidentId),true));
        npcPlans.Remove(actor.ResidentId);Stand(6);yield return null;int utterances=npcUtterances;
        SpeakToNpc("Come here please. I want to speak with you.");
        deadline=Time.realtimeSinceStartupAsDouble+70;while(npcDecisions<2&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(npcDecisions>=2,"second real event produces a separate decision");
        Check(npcPlans.TryGetValue(actor.ResidentId,out var plan),"model intention becomes an executable temporary plan");
        File.WriteAllText(Path.Combine(output,"second-decision.json"),JsonUtility.ToJson(plan.Decision,true));
        if(plan.Decision.action=="approach"||plan.Decision.action=="speak")
        {
            Check(npcUtterances==utterances,"distant reply waits for physical approach");
            deadline=Time.realtimeSinceStartupAsDouble+35;while(npcUtterances==utterances&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Check(npcUtterances>utterances&&HumanDistance(actor.Planet,Position)<2.7,"NPC physically approaches before speaking");
        }
        npcPlans[actor.ResidentId]=new MindPlan{Decision=new NpcDecision{action="rest",target="none",thought="I need a moment.",seconds=2},Event=new NpcEventQueue.Event{Person=actor.ResidentId},Until=Time.unscaledTimeAsDouble+10};
        yield return new WaitForSecondsRealtime(.5f);Check(actor.CurrentActivity==HumanActivity.Rest,"rest intention changes actual activity");
        yield return new WaitForSecondsRealtime(3);Check(!npcPlans.ContainsKey(actor.ResidentId),"temporary action expires and resumes schedule");
        var saved=LivingState.Copy();saved.Validate();Check(saved.Minds.Any(m=>m.Person==actor.ResidentId&&m.Recent.Count>0),"short memories survive the session copy");
        npcTalkTarget=actor;OpenChat();Check(!CommandChatStopped,"conversation input keeps NPC simulation running");CloseChat();
        File.WriteAllText(Path.Combine(output,"final-memory.json"),JsonUtility.ToJson(NpcMemoryFor(actor.ResidentId),true));
        File.WriteAllText(Path.Combine(output,"model-receipt.txt"),npcModel.Config.ModelName+" / threads "+npcModel.Config.Threads+" / requests "+npcModel.Requests+" / last seconds "+npcModel.LastSeconds+" / prompt tokens "+npcModel.LastPromptTokens+" / generated tokens "+npcModel.LastGeneratedTokens);
        int host=npcModel.OwnedProcessId;DisposeNpcMinds();yield return new WaitForSecondsRealtime(.5f);
        bool alive=false;try{alive=!System.Diagnostics.Process.GetProcessById(host).HasExited;}catch(ArgumentException){}
        Check(host>0&&!alive,"leaving the explorer terminates its own local host");
    }
}
