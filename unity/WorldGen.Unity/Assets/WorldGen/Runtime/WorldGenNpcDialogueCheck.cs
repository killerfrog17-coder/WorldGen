using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckNpcGroundedDialogue(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok){File.WriteAllLines(Path.Combine(output,"failed-checks.txt"),report.Concat(new[]{"FAIL "+text}));Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);Debug.Log("NPC DIALOGUE PASS "+text);}
        PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();npcModel??=new NpcModelClient();npcModel.Config.Enabled=true;npcModel.Config.BackgroundReactions=false;npcEvents.Clear();npcPlans.Clear();
        double until=Time.unscaledTimeAsDouble+90;while(!livingPeople.Values.Any(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18)&&Time.unscaledTimeAsDouble<until)yield return null;
        var actor=livingPeople.Values.FirstOrDefault(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18);Check(actor!=null,"An actual adult resident is available for the real-model dialogue review");
        var group=livingGroups[actor.GroupId];long coordinator=NpcCoordinator(group);string leader=group.Residents.Members.FirstOrDefault(p=>p.Id==coordinator)?.Name??"";
        File.WriteAllText(Path.Combine(output,"expected-community.json"),JsonUtility.ToJson(group.Residents,true));
        string[] questions={"Who helps lead this band, and what does that person do?","What time of day is it now, and what is the weather like?","What kinds of food do people here normally gather or hunt?","What do your people do when someone in the band dies?"};
        var transcript=new List<string>();
        for(int i=0;i<questions.Length;i++)
        {
            motion.Place(actor.Motor.X-1.8,actor.Motor.Z,CampFloor(actor.Motor.X-1.8,actor.Motor.Z));UpdateHumans();UpdateCamera();actor.Threat=null;npcTalkTarget=actor;int spoken=npcUtterances;npcLastError="";
            Check(SpeakToNpc(questions[i]),"The real player dialogue queue admits question "+(i+1));
            until=Time.unscaledTimeAsDouble+210;while(npcUtterances==spoken&&npcLastError.Length==0&&Time.unscaledTimeAsDouble<until)yield return null;
            Check(npcUtterances>spoken,"Qwen delivers the streamed reply to question "+(i+1)+": "+npcLastError);
            string answer=NpcMemoryFor(actor.ResidentId).Conversation.Last(t=>t.FromNpc).Text;transcript.Add("PLAYER: "+questions[i]+"\n"+actor.Resident.Name+": "+answer);
            File.WriteAllText(Path.Combine(output,"request-"+(i+1)+".txt"),string.Join("\n",npcModel.LastMessages.Select(m=>m.role+": "+m.content)));
            File.WriteAllLines(Path.Combine(output,"dialogue-review.txt"),transcript);
            if(i==0)File.WriteAllText(Path.Combine(output,"leadership-semantic-check.txt"),"Expected coordinator: "+leader+"\nExact-name present: "+answer.Contains(leader)+"\nReply: "+answer);
        }
        // A controlled visitor situation exercises the actual expanded schema,
        // rather than passing a manually-created decision to the executor.
        until=Time.unscaledTimeAsDouble+20;while((npcModel.IsBusy||npcThinking!=null)&&Time.unscaledTimeAsDouble<until)yield return null;
        var identity=PlayerRules.Identity.Copy();var local=NpcCommunity(group);local.Coordinator=actor.ResidentId;local.Visitors=.85;group.PolicyCulture=null;PlayerRules.Identity=identity.Copy();PlayerRules.Identity.Group=0;
        motion.Place(actor.Motor.X-1.8,actor.Motor.Z,CampFloor(actor.Motor.X-1.8,actor.Motor.Z));UpdateHumans();actor.Threat=null;
        var incident=new NpcEventQueue.Event{Person=actor.ResidentId,Speaker=0,Words="May I stay here as a guest?",Trigger="A peaceful visitor asks for your hospitality."};
        var targets=NpcTargets(actor,0).ToArray();var actions=NpcSocialActions(actor,incident);Check(actions.Contains("invite"),"The controlled welcoming host has an actual invitation action");
        int before=npcUtterances;npcLastError="";npcTalkTarget=actor;Check(SpeakToNpc(incident.Words),"The visitor asks to stay through the real player interaction queue");
        until=Time.unscaledTimeAsDouble+210;while((npcUtterances==before||npcThinking!=null||npcModel.IsBusy)&&npcLastError.Length==0&&Time.unscaledTimeAsDouble<until)yield return null;
        Check(npcUtterances>before&&npcLastError.Length==0,"The real model responds through the expanded community-action schema: "+npcLastError);
        string invitation=NpcMemoryFor(actor.ResidentId).Conversation.Last(t=>t.FromNpc).Text;bool invited=local.Guests.Contains(PlayerPropertyKey);
        if(npcPlans.TryGetValue(actor.ResidentId,out var finalPlan))File.WriteAllText(Path.Combine(output,"invitation-decision.json"),JsonUtility.ToJson(finalPlan.Decision,true));
        File.WriteAllText(Path.Combine(output,"invitation-semantic-check.txt"),"Actual generated invite executed: "+invited+"\n"+invitation);
        File.WriteAllText(Path.Combine(output,"invitation-request.txt"),string.Join("\n",npcModel.LastMessages.Select(m=>m.role+": "+m.content)));
        Check(invited,"The generated invitation actually establishes guest permission before the conversation ends");
        PlayerRules.Identity=identity;humanPlayer.GroupId=identity.Group;
        LivingState.Copy().Validate();Check(true,"Actual model conversations and their local state validate for saving");
        npcThoughts=true;npcTalkTarget=actor;OpenChat();yield return NpcLifeImage(output,"grounded-local-model-dialogue");CloseChat();DisposeNpcMinds();
    }
}
