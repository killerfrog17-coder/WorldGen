using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckNpcMindRuntime()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);AudioListener.volume=0;var report=new List<string>();
        Application.logMessageReceived+=(message,trace,type)=>{if(type==LogType.Exception){File.WriteAllLines(Path.Combine(output,"unhandled-test-exception.txt"),new[]{message,trace});Application.Quit(1);}};
        SetDisplayOptions(false,30,false); // Functional QA does not need uncapped rendering or persisted display changes.
        string profile=Argument("--worldgen-ai-profile","");
        if(profile.Length>0){var config=NpcModelClient.LoadSettings();string family=Argument("--worldgen-ai-model","");if(family.Length>0)config.SelectModel(NpcModelClient.InstalledModels(config).First(m=>m.Family==family));config.Preset(profile=="max"?3:profile=="gpu"?2:profile=="hybrid"?1:0,Environment.ProcessorCount,SystemInfo.graphicsMemorySize);
        if(Environment.GetCommandLineArgs().Contains("--worldgen-shared-gpu")){config.GpuReserveMiB=2048;config.Threads=Math.Max(1,Environment.ProcessorCount/2);config.BatchThreads=config.Threads;config.Priority=-1;config.Polling=0;config.InferenceFps=15;}
        config.BackgroundReactions=false;NpcModelClient.TestSettingsPath=Path.Combine(output,"ai-profile.json");NpcModelClient.SaveSettings(config);}
        double deadline=Time.realtimeSinceStartupAsDouble+240;
        while((Busy||!worldReady||humans==null||humanStep!=null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(!worldReady||Busy||humanStep!=null)throw new InvalidOperationException("NPC test world failed to load");
        humanPlaying=false;ChooseWorldEntry();StartWorldEntry(true);deadline=Time.realtimeSinceStartupAsDouble+300;
        while((worldEntryOpen||terrainExplorer==null||!terrainExplorer.Ready||!terrainExplorer.CenterReady)&&Time.realtimeSinceStartupAsDouble<deadline)
        {if(worldEntryError.Length>0)throw new InvalidOperationException(worldEntryError);yield return null;}
        if(terrainExplorer==null||!terrainExplorer.Ready||!terrainExplorer.CenterReady)throw new InvalidOperationException("NPC test ground failed to load");
        if(Environment.GetCommandLineArgs().Contains("--worldgen-npc-encounter-check"))yield return terrainExplorer.CheckNpcAnimalContact(output,report);
        else if(Environment.GetCommandLineArgs().Contains("--worldgen-npc-grounded-dialogue-check"))yield return terrainExplorer.CheckNpcGroundedDialogue(output,report);
        else if(Environment.GetCommandLineArgs().Contains("--worldgen-npc-camp-check"))yield return terrainExplorer.CheckNpcCampWork(output,report);
        else if(Environment.GetCommandLineArgs().Contains("--worldgen-npc-community-check"))yield return terrainExplorer.CheckNpcCommunity(output,report);
        else if(Environment.GetCommandLineArgs().Contains("--worldgen-npc-social-check"))yield return terrainExplorer.CheckNpcSocialActions(output,report);
        else if(Environment.GetCommandLineArgs().Contains("--worldgen-npc-daily-check"))yield return terrainExplorer.CheckNpcDailyLife(output,report);
        else if(Environment.GetCommandLineArgs().Contains("--worldgen-npc-material-check"))yield return terrainExplorer.CheckNpcMaterials(output,report);
        else if(Environment.GetCommandLineArgs().Contains("--worldgen-npc-safety-check"))yield return terrainExplorer.CheckNpcSafety(output,report);
        else if(Environment.GetCommandLineArgs().Contains("--worldgen-npc-belongings-check"))yield return terrainExplorer.CheckNpcBelongings(output,report);
        else if(Environment.GetCommandLineArgs().Contains("--worldgen-npc-world-check"))yield return terrainExplorer.CheckNpcWorld(output,report);
        else yield return terrainExplorer.CheckNpcMinds(output,report);
        CapturePlayer();play.Copy().Validate();report.Add("PASS captured session validates with conversation memories");
        ReturnFromPlay("Simulation");while(terrainUnloading!=null&&!terrainUnloading.isDone)yield return null;
        yield return new WaitForSecondsRealtime(1);File.WriteAllLines(Path.Combine(output,"checks.txt"),report);
        File.WriteAllText(Path.Combine(output,"npc-check-kind.txt"),Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("--worldgen-npc-",StringComparison.Ordinal)&&a.EndsWith("-check",StringComparison.Ordinal))??"");Application.Quit(0);
    }
}
public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckNpcMinds(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok){Debug.LogError("NPC MIND "+text);File.WriteAllLines(Path.Combine(output,"failed-checks.txt"),report.Concat(new[]{"FAIL "+text}));Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);Debug.Log("NPC MIND PASS "+text);}
        void ModelCheck(bool ok,string text){report.Add((ok?"MODEL PASS ":"MODEL FAIL ")+text);if(ok)Debug.Log("NPC MODEL PASS "+text);else Debug.LogWarning("NPC MODEL ACCURACY FAIL "+text);}
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
        if(npcThinking!=null&&!npcThinking.IsCompleted&&npcPending!=null)
        {
            npcPending.At=Time.unscaledTimeAsDouble-60;
            if(npcPlans.TryGetValue(actor.ResidentId,out var waiting))waiting.Until=Time.unscaledTimeAsDouble-1;
            yield return new WaitForSecondsRealtime(.3f);
            Check(npcPlans.TryGetValue(actor.ResidentId,out waiting)&&waiting.Listening,"pending direct dialogue keeps listening beyond the old timeout");
        }
        bool sawStream=false,sawSpatialVoice=false;
        deadline=Time.realtimeSinceStartupAsDouble+200;while(npcUtterances<1&&Time.realtimeSinceStartupAsDouble<deadline)
        {
            if(npcThinking!=null&&!npcThinking.IsCompleted&&npcSpeech.Values.Any(s=>s.Pace.Shown>0))sawStream=true;
            if(npcSpeech.Values.Any(s=>s.Voice!=null&&s.Voice.spatialBlend==1&&s.Voice.transform.parent==s.Actor.Root.transform))sawSpatialVoice=true;
            if(sawStream&&!File.Exists(Path.Combine(output,"stream-preview.png"))&&npcSpeech.Values.Any(s=>s.Pace.Shown>=12)){yield return NpcLifeImage(output,"stream-preview");}
            yield return null;
        }
        Check(npcUtterances>=1,"actual local model replied and the executor delivered speech nearby: "+npcLastError+" / "+npcModel?.Status+" / "+npcModel?.BackendInfo);
        Check(npcDecisions>=1&&NpcMemoryFor(actor.ResidentId).Thought.Length>0,"validated fictional thought is available to debug");
        Check(sawStream&&npcModel.LastFirstSpeechSeconds>0&&npcModel.LastFirstSpeechSeconds<npcModel.LastSeconds,"actual speech is displayed before the complete decision finishes generating");
        Check(!npcModel.Config.MumbleSpeech||sawSpatialVoice,"prototype mumble is attached to the actual speaker as positional audio");
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
        npcModel.Config.ConversationTurns=2;npcModel.Config.Temperature=.2f;
        string nonce="kelun"+new[]{"kavelo","miparu","nusami","vekani"}[UnityEngine.Random.Range(0,4)],lastSpeech="";
        var dialogueProof=new List<string>();
        IEnumerator Talk(string words)
        {
            npcPlans.Remove(actor.ResidentId);Stand(2);npcTalkTarget=actor;int before=npcUtterances;
            Check(SpeakToNpc(words),"actual resident accepts: "+words);
            double until=Time.realtimeSinceStartupAsDouble+200;while(npcUtterances==before&&Time.realtimeSinceStartupAsDouble<until&&npcLastError.Length==0)yield return null;
            Check(npcUtterances>before,"actual resident delivers the next reply: "+npcLastError);
            lastSpeech=NpcMemoryFor(actor.ResidentId).Conversation.Last(t=>t.FromNpc).Text;
            dialogueProof.Add("You: "+words+"\n"+actor.Resident.Name+": "+lastSpeech);
            File.WriteAllLines(Path.Combine(output,"memory-conversation.txt"),dialogueProof);
            File.WriteAllText(Path.Combine(output,"request-"+dialogueProof.Count+".txt"),string.Join("\n",npcModel.LastMessages.Select(m=>m.role+": "+m.content)));
        }
        yield return Talk("Please remember this secret passkey: "+nonce+".");
        yield return Talk("I have two children.");
        yield return Talk("What are you doing?");
        var copied=LivingState.Copy();copied.Validate();var copiedMind=copied.Minds.Single(m=>m.Person==actor.ResidentId);
        LivingState.Minds[LivingState.Minds.FindIndex(m=>m.Person==actor.ResidentId)]=copiedMind;
        Check(!copiedMind.DialogueFor(NpcSpeakerKey(0),long.MaxValue,2).Any(t=>t.Text.Contains(nonce)),"passkey has left the two-exchange live history before recall");
        yield return Talk("What is the secret passkey I asked you to remember?");
        ModelCheck(lastSpeech.IndexOf(nonce,StringComparison.OrdinalIgnoreCase)>=0,"actual resident recalls a random passkey after distraction and saved-memory restoration");
        yield return Talk("How many children did I say I have?");
        ModelCheck(lastSpeech.IndexOf("two",StringComparison.OrdinalIgnoreCase)>=0||lastSpeech.Contains("2"),"actual resident recalls the player's reported children separately from generated family records");
        yield return Talk("How old am I?");
        var context=string.Join("\n",npcModel.LastMessages.Select(m=>m.role+": "+m.content));
        Check(context.Contains("SPEAKER: name="+PlayerRules.Identity.Person.Name)&&context.Contains("age in years="+Math.Floor(PlayerRules.Identity.Person.AgeYears).ToString("0"))&&context.Contains(NpcVisibleHair(humanPlayer.Definition.Variant)),"actual request includes the controlled person's identity, age and rendered hair");
        File.WriteAllText(Path.Combine(output,"actual-request.txt"),context);
        if(Environment.GetCommandLineArgs().Contains("--worldgen-npc-life-check"))
        {
            yield return Talk("Roughly how long until our group plans to leave this camp?");
            File.WriteAllText(Path.Combine(output,"camp-plans.txt"),lastSpeech);
            Check(npcModel.LastMessages[0].content.Contains("Current plan is to leave")||npcModel.LastMessages[0].content.Contains("no departure is currently planned"),"NPC receives the actual camp itinerary rather than inventing a departure");
            yield return Talk("How do people decide things here, and who do people listen to?");
            File.WriteAllText(Path.Combine(output,"social-customs.txt"),lastSpeech);
            Check(npcModel.LastMessages[0].content.Contains("Camp customs:")&&npcModel.LastMessages[0].content.Contains("travel guide:"),"NPC knows the local decision tradition and actual named social roles");
            yield return CheckNpcLife(actor,output,Check);
        }
        npcThoughts=true;npcTalkTarget=actor;OpenChat();yield return new WaitForEndOfFrame();screen=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,"npc-memory.png"),screen.EncodeToPNG());Destroy(screen);CloseChat();
        var saved=LivingState.Copy();saved.Validate();Check(saved.Minds.Any(m=>m.Person==actor.ResidentId&&m.Recent.Count>0),"short memories survive the session copy");
        npcTalkTarget=actor;OpenChat();Check(!CommandChatStopped,"conversation input keeps NPC simulation running");CloseChat();
        File.WriteAllText(Path.Combine(output,"final-memory.json"),JsonUtility.ToJson(NpcMemoryFor(actor.ResidentId),true));
        File.WriteAllText(Path.Combine(output,"model-receipt.txt"),npcModel.Config.ModelName+" / threads "+npcModel.Config.Threads+" / requests "+npcModel.Requests+" / last seconds "+npcModel.LastSeconds+" / prompt tokens "+npcModel.LastPromptTokens+" / generated tokens "+npcModel.LastGeneratedTokens);
        int host=npcModel.OwnedProcessId;DisposeNpcMinds();yield return new WaitForSecondsRealtime(.5f);
        bool alive=false;try{alive=!System.Diagnostics.Process.GetProcessById(host).HasExited;}catch(ArgumentException){}
        Check(host>0&&!alive,"leaving the explorer terminates its own local host");
    }
    IEnumerator CheckNpcLife(HumanActor actor,string output,Action<bool,string> check)
    {
        npcModel.Config.BackgroundReactions=false;npcPlans.Clear();npcEvents.Clear();npcTalkTarget=null;CloseChat();paused=mapOpen=ecologyOpen=false;
        var animal=animals.Values.Where(a=>!a.Dead&&!a.Retired).OrderBy(a=>HumanDistance(a.Planet,Position)).FirstOrDefault();
        double solar=PlayerRules.SolarDays,humanAge=actor.SimulationAge,animalAge=animal?.Life??0;Quaternion pose=humanPlayer.Root.transform.localRotation;
        BeginCampSleep(0,true);check(campResting&&!campSleeping&&sleepingBed==null,"Waiting starts standing without bedding or sleep");
        double until=Time.realtimeSinceStartupAsDouble+2.5;while(campResting&&Time.realtimeSinceStartupAsDouble<until)yield return null;
        double advanced=(PlayerRules.SolarDays-solar)*PlayerRules.Settings.RealMinutesPerDay*60;
        check(campResting&&advanced>4,"Waiting advances the real solar/calendar clock faster than normal time");
        check(Quaternion.Angle(humanPlayer.Root.transform.localRotation,pose)<45,"Waiting preserves an upright player pose");
        check(Math.Abs(actor.SimulationAge-humanAge-advanced)<3.3,"NPC movement and avoidance use the same advanced scene time");
        check(animal==null||Math.Abs(animal.Life-animalAge-advanced)<3.3,"Visible wildlife advances with the waiting clock");
        yield return NpcLifeImage(output,"standing-wait");
        File.WriteAllText(Path.Combine(output,"wait-clock.txt"),"Advanced equivalent seconds="+advanced+"\nHuman="+(actor.SimulationAge-humanAge)+"\nAnimal="+(animal==null?"not present":(animal.Life-animalAge).ToString())+"\nSteps="+LocalSceneSteps);
        Vitals.Health-=1;CheckWaitInterrupt();check(!campResting&&CampTimeRate==1,"Damage immediately restores normal time");Vitals.Restore();
        BeginCampSleep(0,true);ulong threatId=ulong.MaxValue;while(animals.ContainsKey(threatId))threatId--;
        animals.Add(threatId,new Animal{Health=100,MassKg=50,Planet=Position,Behaviour=new WildlifeBehaviourProfile{Predator=true}});
        try{CheckWaitInterrupt();check(!campResting,"A controlled nearby predator threat interrupts waiting before another time step");}finally{animals.Remove(threatId);}
        var oldKeyboard=Keyboard.current;var oldBackground=InputSystem.settings.backgroundBehavior;var keyboard=InputSystem.AddDevice<Keyboard>();TestKeyboard(keyboard);keyboard.MakeCurrent();InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        IEnumerator KeyPress(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;}
        try
        {
            yield return KeyPress(Key.Z);check(campSleepMenu&&!paused,"Z opens rest choices through the actual keyboard path");
            yield return KeyPress(Key.Escape);check(!CampUiOpen&&!paused,"Closing rest choices does not open pause");
            BeginCampSleep(0,true);yield return KeyPress(Key.Space);check(!campResting&&!paused&&CampTimeRate==1,"Space cancels standing waiting through the actual keyboard path");
            BeginCampSleep(0,true);yield return KeyPress(Key.W);check(!campResting&&!paused,"Movement cancels waiting immediately");
        }
        finally{InputSystem.RemoveDevice(keyboard);TestKeyboard(null);if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();InputSystem.settings.backgroundBehavior=oldBackground;}
        BeginCampSleep(0,true);npcModel.Config.BackgroundReactions=true;npcWaitSocialDay=PlayerRules.SolarDays;WaitNpcApproach();
        check(!campResting&&npcPlans.Values.Any(p=>p.Decision.action=="approach"&&p.Decision.target=="player"),"A bandmate approaching to talk interrupts waiting");
        npcModel.Config.BackgroundReactions=false;npcPlans.Clear();npcEvents.Clear();npcTalkTarget=null;
        var people=livingPeople.Values.Where(p=>p.Health>40&&p.Resident.AgeYears>=7).ToArray();
        var pair=(from a in people from b in people where a.ResidentId<b.ResidentId&&a.GroupId==b.GroupId&&HumanDistance(a.Planet,b.Planet)<6&&HumanDistance(a.Planet,Position)<200 select new[]{a,b}).FirstOrDefault();
        check(pair!=null,"An actual nearby pair of camp residents exists for a social encounter");
        double x=(pair[0].Motor.X+pair[1].Motor.X)*.5,z=(pair[0].Motor.Z+pair[1].Motor.Z)*.5-2;motion.Place(x,z,Ground(x,z));UpdateHumans();UpdateCamera();
        var toward=transform.InverseTransformDirection(pair[0].Root.transform.position+Vector3.up*1.2f-view.transform.position).normalized;heading=Mathf.Atan2(toward.x,toward.z)*Mathf.Rad2Deg;pitch=-Mathf.Asin(toward.y)*Mathf.Rad2Deg;UpdateCamera();
        int before=npcUtterances;npcModel.Config.BackgroundReactions=true;npcSocialAt=0;npcObserveAt=0;
        until=Time.realtimeSinceStartupAsDouble+130;bool met=false;long one=0,two=0;
        while(npcUtterances<before+2&&Time.realtimeSinceStartupAsDouble<until)
        {if(npcConversation!=null){met=true;one=npcConversation.A;two=npcConversation.B;}yield return null;}
        check(met&&npcUtterances>=before+2,"Nearby residents actually exchange locally generated conversational turns");
        check(NpcMemoryFor(one).Conversation.Any(t=>t.Speaker==two)&&NpcMemoryFor(two).Conversation.Any(t=>t.Speaker==one),"Both participants retain what the other person actually said");
        npcTalkTarget=null;OpenChat();yield return NpcLifeImage(output,"npc-social-exchange");CloseChat();File.WriteAllText(Path.Combine(output,"social-one.json"),JsonUtility.ToJson(NpcMemoryFor(one),true));File.WriteAllText(Path.Combine(output,"social-two.json"),JsonUtility.ToJson(NpcMemoryFor(two),true));
        CancelNpcConversation();npcModel.Config.BackgroundReactions=false;
        until=Time.realtimeSinceStartupAsDouble+15;while(npcThinking!=null&&Time.realtimeSinceStartupAsDouble<until)yield return null;
        check(npcConversation==null&&!npcPlans.Values.Any(p=>p.Listening),"Ending the local exchange releases listening holds back to the ordinary schedule");
    }
    IEnumerator NpcLifeImage(string output,string name)
    {yield return new WaitForEndOfFrame();var shot=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,name+".png"),shot.EncodeToPNG());Destroy(shot);}
}
