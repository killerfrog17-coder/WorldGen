using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class MindPlan
    {
        public NpcDecision Decision;public NpcEventQueue.Event Event;public double Until;public bool Spoke,Listening;
        public string Stage="Approaching";public readonly HumanActivitySample Routine=new HumanActivitySample();
    }
    readonly Dictionary<long,MindPlan> npcPlans=new Dictionary<long,MindPlan>();
    readonly HashSet<long> npcSeenVisitors=new HashSet<long>();
    readonly NpcEventQueue npcEvents=new NpcEventQueue();
    NpcModelClient npcModel;Task<string> npcThinking;NpcEventQueue.Event npcPending;
    string[] npcPendingTargets;HumanActor npcTalkTarget,npcAimed;double npcObserveAt,npcAimAt,npcNextBackground;
    bool npcThoughts;string npcLastError="";int npcDecisions,npcUtterances;
    bool CommandChatStopped=>chatOpen&&(npcTalkTarget==null||chatText.StartsWith("/"));
    long NpcSpeakerKey(long speaker)=>speaker==0?PlayerRules.Identity?.Person?.Id??0:speaker;
    ForagerPersonSample NpcSpeaker(HumanActor listener,long speaker,out bool known)
    {
        known=speaker==0?PlayerRules.Identity?.Group==listener.GroupId:livingPeople.TryGetValue(speaker,out var peer)&&peer.GroupId==listener.GroupId;
        return speaker==0?PlayerRules.Identity?.Person:livingPeople.TryGetValue(speaker,out var actor)?actor.Resident:null;
    }
    static string NpcVisibleHair(string variant)=>variant=="A"?"black, tightly curled hair":variant=="B"?"short dark brown hair":variant=="C"?"brown braided hair":variant=="D"?"long black hair":variant=="E"?"black braided hair":variant=="F"?"short grey hair":"hair appearance unavailable";
    NpcMemory NpcMemoryFor(long person)
    {
        LivingState.Minds??=new List<NpcMemory>();var memory=LivingState.Minds.FirstOrDefault(m=>m.Person==person);
        if(memory==null){memory=new NpcMemory{Person=person};LivingState.Minds.Add(memory);}return memory;
    }
    bool QueueNpcEvent(HumanActor actor,string trigger,string words="",int priority=1,long speaker=0,int depth=0)
    {
        if(actor?.Resident==null||actor.Health<=0||actor.Resident.AgeYears<4||closed)return false;
        npcModel??=new NpcModelClient();if(!npcModel.Config.Enabled)return false;
        var item=new NpcEventQueue.Event{Person=actor.ResidentId,Trigger=trigger,Words=NpcDecision.Clean(words,256),Priority=priority,Speaker=speaker,Depth=depth};
        if(!npcEvents.Add(item,Time.unscaledTimeAsDouble))return false;
        if(item.Words.Length>0){var person=NpcSpeaker(actor,speaker,out bool known);item.DialogueSequence=NpcMemoryFor(actor.ResidentId).Hear(NpcSpeakerKey(speaker),known?person?.Name??"Visitor":"Visitor",item.Words).Sequence;}
        return true;
    }
    void UpdateNpcMinds(bool stopped)
    {
        if(closed)return;
        npcModel?.ReleaseIfIdle();
        if(npcTalkTarget!=null&&(npcTalkTarget.Health<=0||!livingPeople.ContainsKey(npcTalkTarget.ResidentId)||HumanDistance(npcTalkTarget.Planet,Position)>24))npcTalkTarget=null;
        if(stopped)return;
        double now=Time.unscaledTimeAsDouble;
        if(npcThinking!=null&&npcThinking.IsCompleted)
        {
            var work=npcThinking;var item=npcPending;npcThinking=null;npcPending=null;
            try
            {
                if(work.IsFaulted)throw work.Exception.GetBaseException();if(work.IsCanceled)throw new OperationCanceledException();
                bool direct=item.Priority>=3&&npcTalkTarget?.ResidentId==item.Person;
                if(livingPeople.TryGetValue(item.Person,out var actor)&&actor.Health>0&&HumanDistance(actor.Planet,Position)<50&&now-item.At<(direct?240:45))
                {
                    var decision=JsonUtility.FromJson<NpcDecision>(work.Result);
                    if(decision==null||!decision.Validate(npcPendingTargets))throw new InvalidOperationException("Invalid NPC decision");
                    var memory=NpcMemoryFor(actor.ResidentId);memory.Thought=decision.thought;memory.Trigger=NpcDecision.Clean(item.Trigger,160);
                    memory.Remember("Intention: "+decision.action+" "+decision.target+". "+decision.thought);
                    npcDecisions++;npcLastError="";
                    if(decision.action=="resume"){npcPlans.Remove(actor.ResidentId);if(decision.speech.Length>0&&NpcGoal(actor,decision.target,out var listener)&&HumanDistance(actor.Planet,listener)<6)NpcSay(actor,new MindPlan{Decision=decision,Event=item});}
                    else npcPlans[actor.ResidentId]=new MindPlan{Decision=decision,Event=item,Until=now+Math.Max(15,decision.seconds+15)};
                }
            }
            catch(Exception ex){npcPlans.Remove(item.Person);npcLastError=ex.Message;if(item.Priority>=3)PostExplorerChat("NPC response unavailable: "+npcLastError,true);}
        }
        if(npcThinking==null&&npcEvents.Count>0&&npcModel?.IsBusy!=true)
        {
            var item=npcEvents.Take(now);
            if(item!=null&&livingPeople.TryGetValue(item.Person,out var actor)&&actor.Health>0&&HumanDistance(actor.Planet,Position)<40)
            {
                npcModel??=new NpcModelClient();if(npcModel.Config.Enabled)
                {
                    npcPending=item;npcPendingTargets=NpcTargets(actor,item.Speaker).ToArray();
                    var list="["+string.Join(",",npcPendingTargets.Select(t=>"\""+t+"\""))+"]";
                    var memory=NpcMemoryFor(actor.ResidentId);long speaker=NpcSpeakerKey(item.Speaker);
                    var current=memory.Conversation?.FirstOrDefault(t=>t.Sequence==item.DialogueSequence);
                    npcThinking=npcModel.Ask(NpcContext(actor,item,npcPendingTargets),list,memory.DialogueFor(speaker,item.DialogueSequence,npcModel.Config.ConversationTurns),current,memory.Recall(speaker,item.Words,item.DialogueSequence>0?item.DialogueSequence-1:long.MaxValue));
                }
            }
        }
        if(now<npcObserveAt)return;npcObserveAt=now+1.5;
        npcPlans.Keys.Where(id=>!livingPeople.ContainsKey(id)).ToList().ForEach(id=>npcPlans.Remove(id));
        if(now<npcNextBackground)return;
        npcModel??=new NpcModelClient();if(!npcModel.Config.Enabled||!npcModel.Config.BackgroundReactions)return;
        foreach(var actor in humanNpcs)
        {
            if(actor.Resident==null||actor.Health<=0||actor.CurrentActivity==HumanActivity.Sleep||PlayerRules.Identity?.Group==actor.GroupId||npcSeenVisitors.Contains(actor.GroupId))continue;
            double range=HumanDistance(actor.Planet,Position);if(range>12||!LivingNoticesVisitor(actor,range))continue;
            npcSeenVisitors.Add(actor.GroupId);QueueNpcEvent(actor,"An unfamiliar visitor is visibly near the camp.");npcNextBackground=now+(npcModel?.Config.VisitorCooldownSeconds??20);break;
        }
    }
    IEnumerable<string> NpcTargets(HumanActor actor,long speaker)
    {
        yield return "none";yield return "player";yield return "camp";
        if(speaker>0&&livingPeople.TryGetValue(speaker,out var heard)&&heard.Health>0&&HumanDistance(actor.Planet,heard.Planet)<12)yield return "person:"+speaker;
        foreach(var other in livingPeople.Values.Where(p=>p!=actor&&p.ResidentId!=speaker&&p.Health>0&&HumanDistance(p.Planet,actor.Planet)<12).OrderBy(p=>p.ResidentId).Take(4))yield return "person:"+other.ResidentId;
    }
    string NpcContext(HumanActor actor,NpcEventQueue.Event item,string[] targets)
    {
        var group=livingGroups[actor.GroupId];var band=group.Residents;var f=sourceWorld.Prehistory.Foragers;
        var s=new StringBuilder(NpcPersonContext.Describe("YOU (NPC)",actor.Resident,band,true,NpcVisibleHair(actor.Definition?.Variant)));s.Append(" Band: ").Append(band.Name);
        s.Append(". Culture: ").Append(f.Cultures[band.Culture].Name).Append(". Language: ").Append(f.Languages[band.Language].Name).Append(" (render speech in English translation).");
        s.Append(" Current activity: ").Append(NpcActivityText(actor.CurrentActivity)).Append(". Camp phase: ").Append(group.Situation.Phase).Append(". Health: ").Append(actor.Health.ToString("0")).Append("/100. Breath strain: ").Append(actor.Exertion.Breath.ToString("0.00"));
        if(actor.Routine!=null)s.Append(". Assigned routine before this conversation: ").Append(NpcActivityText(actor.Routine.Activity));
        s.Append(". Temperature: ").Append(CurrentWeather.TemperatureC.ToString("0")).Append(" C. Known tools/techniques: ");
        if(band.Techniques!=null)for(int i=0;i<Math.Min(band.Techniques.Length,ForagerTechnology.All.Length);i++)if(band.Techniques[i])s.Append(ForagerTechnology.All[i].Name).Append(", ");
        s.Append(". Available targets: ").Append(string.Join(", ",targets)).Append('.');
        foreach(var id in targets.Where(t=>t.StartsWith("person:")))if(long.TryParse(id.Substring(7),out long person)&&livingPeople.TryGetValue(person,out var other))s.Append(' ').Append(id).Append(" is ").Append(other.Resident.Name).Append(other.GroupId==actor.GroupId?" (nearby band member).":" (nearby visitor).");
        var speakerPerson=NpcSpeaker(actor,item.Speaker,out bool sameBand);
        long speakerId=speakerPerson?.Id??0;
        string relation=speakerId<=0?"no confirmed family relationship":actor.Resident.Partner==speakerId?"your partner":actor.Resident.Mother==speakerId?"your mother":actor.Resident.Father==speakerId?"your father":speakerPerson.Mother==actor.ResidentId||speakerPerson.Father==actor.ResidentId?"your child":actor.Resident.Mother>0&&actor.Resident.Mother==speakerPerson.Mother||actor.Resident.Father>0&&actor.Resident.Father==speakerPerson.Father?"your sibling":"no confirmed family relationship";
        string appearance=item.Speaker==0?NpcVisibleHair(humanPlayer?.Definition?.Variant):livingPeople.TryGetValue(item.Speaker,out var visible)?NpcVisibleHair(visible.Definition?.Variant):"not available";
        s.Append(' ').Append(NpcPersonContext.Describe("SPEAKER",speakerPerson,band,sameBand,appearance));
        s.Append(" SPEAKER relationship to YOU: ").Append(sameBand?"member of your band; ":"not a member of your band; ").Append(relation).Append("; speaker target ID=").Append(item.Speaker==0?"player":"person:"+item.Speaker).Append('.');
        s.Append(" Current perceived event: ").Append(item.Trigger).Append('.');
        return s.ToString();
    }
    static string NpcActivityText(HumanActivity activity)=>activity==HumanActivity.GatherPlants?"gathering wild plants for food and materials":activity==HumanActivity.GatherFuel?"collecting fallen branches for the campfire":activity==HumanActivity.PrepareFood?"preparing wild gathered or hunted food":activity.ToString();
    bool NpcGoal(HumanActor actor,string target,out V point)
    {
        point=actor.Planet;
        if(target=="player"){point=Position;return HumanDistance(actor.Planet,point)<40;}
        if(target=="camp"&&livingGroups.TryGetValue(actor.GroupId,out var group)){point=group.Grounded?group.Anchor:group.Situation.Camp.Position;return HumanDistance(actor.Planet,point)<100;}
        if(target.StartsWith("person:")&&long.TryParse(target.Substring(7),out var id)&&livingPeople.TryGetValue(id,out var other)&&other.Health>0){point=other.Planet;return HumanDistance(actor.Planet,point)<40;}
        return target=="none";
    }
    bool ApplyNpcPlan(HumanActor actor,ref HumanActivitySample routine,ref V target)
    {
        if(!npcPlans.TryGetValue(actor.ResidentId,out var plan))return false;
        double now=Time.unscaledTimeAsDouble;var alarm=LivingState.Groups.FirstOrDefault(g=>g.Group==actor.GroupId);
        if(plan.Listening&&npcTalkTarget==actor&&HumanDistance(actor.Planet,Position)<24&&
            ((npcPending?.Person==actor.ResidentId&&npcPending.Priority>=3&&npcThinking!=null&&!npcThinking.IsCompleted)||npcEvents.HasDirect(actor.ResidentId)))plan.Until=now+2;
        if(now>plan.Until||alarm!=null&&alarm.AlarmUntilSolarDay>PlayerRules.SolarDays||!NpcGoal(actor,plan.Decision.target,out var goal))
        {npcPlans.Remove(actor.ResidentId);return false;}
        var d=plan.Decision;double distance=HumanDistance(actor.Planet,goal);target=actor.Planet;
        bool move=(d.action=="approach"||d.action=="speak")&&distance>2.2;
        if(move){target=goal;plan.Stage="Approaching "+d.target;}
        else
        {
            if(d.action=="flee")
            {
                if(!plan.Spoke){plan.Spoke=true;if(d.speech.Length>0&&distance<6)NpcSay(actor,plan);}TerrainHandoff.Coordinates(frame,goal,out var gx,out var gz);double dx=actor.Motor.X-gx,dz=actor.Motor.Z-gz,n=Math.Max(.01,Math.Sqrt(dx*dx+dz*dz));target=frame.Position(actor.Motor.X+dx/n*15,actor.Motor.Z+dz/n*15);plan.Stage="Withdrawing";}
            else
            {
                plan.Stage=plan.Listening?"Listening":d.action=="rest"?"Resting":d.speech.Length>0?"Conversing":"Waiting";
                TerrainHandoff.Coordinates(frame,goal,out var gx,out var gz);if(distance>.3)actor.TargetYaw=(float)(Math.Atan2(gx-actor.Motor.X,gz-actor.Motor.Z)*180/Math.PI);
                if(!plan.Spoke)
                {
                    plan.Spoke=true;plan.Until=now+d.seconds;
                    if(d.speech.Length>0)NpcSay(actor,plan);
                }
            }
        }
        routine=plan.Routine;routine.Activity=d.action=="rest"?HumanActivity.Rest:d.action=="flee"?HumanActivity.Retreat:move?HumanActivity.Watch:HumanActivity.Socialise;routine.Person=actor.ResidentId;routine.Group=actor.GroupId;routine.Position=target;
        actor.CurrentActivity=routine.Activity;return true;
    }
    void NpcSay(HumanActor actor,MindPlan plan)
    {
        string speech=plan.Decision.speech;var memory=NpcMemoryFor(actor.ResidentId);memory.Remember("Said: "+speech);
        long listener=plan.Decision.target.StartsWith("person:")&&long.TryParse(plan.Decision.target.Substring(7),out var recipient)?recipient:NpcSpeakerKey(plan.Event.Speaker);
        memory.Say(listener,actor.Resident.Name,speech,plan.Event.DialogueSequence);npcUtterances++;
        if(HumanDistance(actor.Planet,Position)<20)PostExplorerChat("<"+actor.Resident.Name+"> "+speech);
        if(plan.Event.Depth<1&&plan.Decision.target.StartsWith("person:")&&long.TryParse(plan.Decision.target.Substring(7),out var id)&&livingPeople.TryGetValue(id,out var other)&&HumanDistance(actor.Planet,other.Planet)<5)
        {NpcMemoryFor(id).Remember(actor.Resident.Name+" said: "+speech);QueueNpcEvent(other,"A nearby person addressed you.",speech,2,actor.ResidentId,plan.Event.Depth+1);}
    }
    bool TryNpcInteract(float itemDistance)
    {
        var ray=new Ray(view.transform.position,view.transform.forward);var hit=TraceLiving(ray,4.5f);
        if(hit?.Human?.Resident==null||hit.Distance>=itemDistance||TraceCombatObstacle(ray,Math.Max(0,hit.Distance-.1f),checkGround:false)!=null)return false;
        npcTalkTarget=hit.Human;NpcListen(npcTalkTarget);OpenChat();return true;
    }
    bool SpeakToNpc(string message)
    {
        var actor=npcTalkTarget;if(actor==null||actor.Health<=0||HumanDistance(actor.Planet,Position)>20)return false;
        npcModel??=new NpcModelClient();if(!npcModel.Config.Enabled){PostExplorerChat("Local NPC model disabled or not installed. /npcai status",true);return true;}
        if(QueueNpcEvent(actor,"You were directly addressed.",message,3))NpcListen(actor);else PostExplorerChat(actor.Resident.Name+" is still responding.");return true;
    }
    void NpcListen(HumanActor actor)
    {
        // Listening is an immediate scripted reaction, not a model request.
        npcPlans[actor.ResidentId]=new MindPlan{Decision=new NpcDecision{action="wait",target="player",seconds=30},Event=new NpcEventQueue.Event{Person=actor.ResidentId},Until=Time.unscaledTimeAsDouble+35,Spoke=true,Listening=true,Stage="Listening"};
    }
    void EndNpcListening()
    {
        if(npcTalkTarget!=null&&npcPlans.TryGetValue(npcTalkTarget.ResidentId,out var plan)&&plan.Listening&&npcPending?.Person!=npcTalkTarget.ResidentId)npcPlans.Remove(npcTalkTarget.ResidentId);
    }
    PlayCommandResult ExecuteNpcMindCommand(string[] words)
    {
        bool debug=words[0].Equals("/npcthoughts",StringComparison.OrdinalIgnoreCase);string option=words.Length>1?words[1].ToLowerInvariant():"status";
        if(words.Length>2||!new[]{"on","off","status"}.Contains(option))return new PlayCommandResult(false,"Usage: "+words[0]+" on | off | status");
        if(debug){if(option!="status")npcThoughts=option=="on";return new PlayCommandResult(true,"NPC thoughts: "+(npcThoughts?"on":"off")+". Shows intentions, actual actions and conditions.");}
        npcModel??=new NpcModelClient();if(option=="off"){npcModel.Dispose();npcModel=null;npcEvents.Clear();npcPlans.Clear();npcThinking=null;npcPending=null;npcModel=new NpcModelClient();npcModel.Config.Enabled=false;}
        if(option=="on"){DisposeNpcMinds();npcPending=null;npcModel=new NpcModelClient();npcModel.Config.Enabled=true;npcLastError="";}
        if(option!="status")NpcModelClient.SaveSettings(npcModel.Config);
        return new PlayCommandResult(true,"NPC model: "+(npcModel.Config.Enabled?npcModel.Config.ModelName:"disabled")+" · "+npcModel.Status+" · "+npcModel.Requests+" requests · last "+npcModel.LastSeconds.ToString("0.0")+"s",NpcModelClient.ConfigPath,npcLastError.Length>0?npcLastError:"Thinking occurs only on special events; schedules run independently.");
    }
    internal NpcModelClient NpcAiRuntime=>npcModel;
    internal NpcModelClient EnsureNpcAiRuntime()=>npcModel??(npcModel=new NpcModelClient());
    internal void ReloadNpcAiSettings(){DisposeNpcMinds();npcPending=null;npcLastError="";npcModel=new NpcModelClient();}
    void DrawNpcMind()
    {
        if(paused||mapOpen||ecologyOpen||DeathVisible)return;
        if(Time.unscaledTimeAsDouble>=npcAimAt)
        {
            npcAimAt=Time.unscaledTimeAsDouble+.15;npcAimed=null;float best=4.5f;
            var ray=new Ray(view.transform.position,view.transform.forward);
            foreach(var actor in humanNpcs)
            {
                if(actor.Resident==null||actor.Health<=0||HumanDistance(Position,actor.Planet)>6)continue;
                var skins=actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>();if(skins.Length==0)continue;var bounds=skins[0].bounds;for(int i=1;i<skins.Length;i++)bounds.Encapsulate(skins[i].bounds);
                if(bounds.IntersectRay(ray,out var range)&&range<best&&TraceCombatObstacle(ray,Math.Max(0,range-.1f),checkGround:false)==null){best=range;npcAimed=actor;}
            }
        }
        if(npcAimed!=null&&!chatOpen)GUI.Label(new Rect(Screen.width*.5f-150,Screen.height*.58f,300,26),npcAimed.Resident.Name+"  [E]",new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter});
        if(!npcThoughts)return;
        var focus=npcAimed??npcTalkTarget??humanNpcs.Where(p=>p.Resident!=null&&p.Health>0&&HumanDistance(p.Planet,Position)<30).OrderBy(p=>HumanDistance(p.Planet,Position)).FirstOrDefault();
        if(focus==null)return;var memory=NpcMemoryFor(focus.ResidentId);npcPlans.TryGetValue(focus.ResidentId,out var plan);
        var text=focus.Resident.Name+" · "+focus.Resident.AgeYears.ToString("0")+" years\nActual: "+focus.CurrentActivity+" · "+(focus.Speed>.1?"moving "+focus.Speed.ToString("0.0")+" m/s":"stationary")+
            "\nHealth "+focus.Health.ToString("0")+" · breath "+focus.Exertion.Breath.ToString("0.00")+" · fatigue "+focus.Exertion.Fatigue.ToString("0.00")+
            "\nRoutine: "+focus.Routine?.Activity+" · camp "+(livingGroups.TryGetValue(focus.GroupId,out var group)?group.Situation.Phase.ToString():"unloaded")+
            "\nMind: "+(npcPending?.Person==focus.ResidentId?"thinking":plan?.Stage??"following schedule")+" · queue "+npcEvents.Count+
            "\nTrigger: "+memory.Trigger+"\nThought: "+memory.Thought+(plan==null?"":"\nIntent: "+plan.Decision.action+" → "+plan.Decision.target);
        var style=new GUIStyle(GUI.skin.label){wordWrap=true,richText=false,fontSize=14};float height=style.CalcHeight(new GUIContent(text),360)+20;
        GUI.Box(new Rect(10,Screen.height*.22f,380,height),GUIContent.none);GUI.Label(new Rect(20,Screen.height*.22f+8,360,height-12),text,style);
    }
    void DisposeNpcMinds(){npcModel?.Dispose();npcModel=null;npcEvents.Clear();npcPlans.Clear();if(npcThinking!=null)npcThinking.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);npcThinking=null;}
}
