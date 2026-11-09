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
    public IEnumerator CheckNpcSocialActions(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok){File.WriteAllLines(Path.Combine(output,"failed-checks.txt"),report.Concat(new[]{"FAIL "+text}));Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);Debug.Log("NPC SOCIAL PASS "+text);}
        npcModel??=new NpcModelClient();npcModel.Config.Enabled=false;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();
        double until=Time.unscaledTimeAsDouble+90;while(livingPeople.Values.Count(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18)<2&&Time.unscaledTimeAsDouble<until)yield return null;
        var adults=livingPeople.Values.Where(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18).Take(2).ToArray();Check(adults.Length==2,"Two actual adult residents exist");paused=true;livingQueryAt=Time.unscaledTimeAsDouble+40;while(livingWork!=null)yield return null;
        var recipient=adults[0];var other=adults[1];
        void Place(HumanActor p,double x,double z,float yaw){p.Motor.Place(x,z,CampFloor(x,z));p.Planet=frame.Position(x,z);p.Root.transform.localPosition=new Vector3((float)(x-originX),(float)(p.Motor.Y-TerrainMotion.EyeHeightM),(float)(z-originZ));p.Root.transform.localRotation=Quaternion.Euler(0,yaw,0);p.Yaw=p.TargetYaw=yaw;p.Speed=0;p.CurrentActivity=HumanActivity.Socialise;p.Threat=null;}
        motion.Place(recipient.Motor.X-1.5,recipient.Motor.Z,CampFloor(recipient.Motor.X-1.5,recipient.Motor.Z));UpdateHumans();Place(recipient,motion.X+1.5,motion.Z,270);Place(other,motion.X+1.5,motion.Z+1.4,180);
        NpcBelongings.Equip(recipient.Belongings,Array.Empty<Possession>(),recipient.Planet);PublishNpcGroundGoods(recipient);
        foreach(var p in Inventory.Items.Where(p=>p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand).ToArray()){p.Place=PossessionPlace.Ground;p.Position=Position;}
        var gift=PortableTechnology.Create(PossessionKind.StoneBlade,Position);gift.Id="gift-probe-"+recipient.ResidentId;gift.Place=PossessionPlace.RightHand;ClaimPlayerItem(gift);Inventory.Items.Add(gift);
        Check(GiveNpcItem(recipient,gift),"A carried real item can be given to the nearby resident");
        Check(Inventory.Find(gift.Id)==null&&recipient.Belongings.Find(gift.Id)==gift&&recipient.Belongings.Carried(gift)&&gift.OwnerPerson==recipient.ResidentId,"The gift changes inventory and ownership exactly once");
        var state=NpcMemoryFor(recipient.ResidentId).Social;Check(state.Tie(PlayerPropertyKey,PlayerRules.SolarDays).Trust>0&&state.Incidents.Any(e=>e.Kind==NpcIncidentKind.Help&&e.Detail.Contains("Stone blade")),"The recipient remembers the specific actual gift and gains personal trust");
        var returned=PortableTechnology.Create(PossessionKind.StoneScraper,Position);returned.Id="return-probe-"+recipient.ResidentId;returned.Place=PossessionPlace.RightHand;NpcOwnership.Assign(returned,recipient.Resident,recipient.GroupId);Inventory.Items.Add(returned);
        state.Tie(PlayerPropertyKey,PlayerRules.SolarDays).Grievance=.8;state.Tie(PlayerPropertyKey,PlayerRules.SolarDays).Trust=-.6;
        NpcBelongings.Equip(recipient.Belongings,Array.Empty<Possession>(),recipient.Planet);PublishNpcGroundGoods(recipient);
        Check(GiveNpcItem(recipient,returned)&&state.Tie(PlayerPropertyKey,PlayerRules.SolarDays).Grievance<.8&&state.Tie(PlayerPropertyKey,PlayerRules.SolarDays).Trust<0,"Returning actual property partly repairs the dispute without erasing distrust");
        NpcBelongings.Equip(recipient.Belongings,Array.Empty<Possession>(),recipient.Planet);PublishNpcGroundGoods(recipient);
        var held=HumanEquipment.Item(PossessionKind.LargeRock,recipient.ResidentId,"blocked-gift",recipient.Planet);held.MassKg=12;held.Place=PossessionPlace.RightHand;recipient.Belongings.Items.Add(held);
        var longGift=HumanEquipment.Item(PossessionKind.WoodenSpear,PlayerPropertyKey,"long-gift",Position);longGift.HeightM=8;longGift.CellsHigh=80;longGift.Place=PossessionPlace.RightHand;Inventory.Items.Add(longGift);ClaimPlayerItem(longGift);
        Check(!GiveNpcItem(recipient,longGift)&&Inventory.Find(longGift.Id)==longGift&&recipient.Belongings.Find(longGift.Id)==null,"A recipient without physical room refuses a gift without deleting it");
        // Execute an actual addressed line through the conversation delivery
        // path; model quality is evaluated separately, with the model enabled.
        var discussion=new NpcDiscussion{Topic="how households share food",FirstView="share broadly",SecondView="keep a household portion",Disagreement=true};
        npcConversation=new SocialConversation{A=recipient.ResidentId,B=other.ResidentId,Discussion=discussion,Until=Time.unscaledTimeAsDouble+30};
        var heard=new NpcEventQueue.Event{Person=recipient.ResidentId,Speaker=other.ResidentId};DeliverNpcSpeech(recipient,heard,"I think households should share food broadly.","person:"+other.ResidentId);
        Check(NpcMemoryFor(recipient.ResidentId).Social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Argument&&e.Subject==other.ResidentId)&&NpcMemoryFor(other.ResidentId).Social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Argument&&e.Subject==recipient.ResidentId),"An executor-delivered local disagreement changes both participants' named relationships");
        EndNpcConversation();npcPlans.Clear();npcEvents.Clear();
        foreach(var person in livingPeople.Values)person.CurrentActivity=HumanActivity.Craft;
        Place(recipient,motion.X+25,motion.Z,270);Place(other,motion.X+25,motion.Z+1.4,180);
        var beforeRequests=npcModel.Requests;double day=PlayerRules.SolarDays;
        var peerState=NpcMemoryFor(other.ResidentId).Social;
        state.Record(new NpcIncident{Kind=NpcIncidentKind.Theft,Actor=PlayerPropertyKey,Subject=recipient.ResidentId,SolarDay=day,Position=recipient.Planet,Detail="You saw a visitor take a stone scraper."});
        state.NextSocialSolarDay=peerState.NextSocialSolarDay=0;npcSocialScanAt=0;npcSocialCursor=Array.IndexOf(livingPeople.Values.ToArray(),recipient);
        Check(!NpcPlayerCanHear(recipient)&&!NpcPlayerCanHear(other),"Both social participants are outside player earshot");
        UpdateNpcProceduralSocial();
        Check(peerState.Incidents.Any(e=>e.Kind==NpcIncidentKind.Theft&&!e.FirstHand&&e.Reporter==recipient.ResidentId),"The actual local scheduler exchanges a witnessed fact with the model disabled and player out of earshot");
        Check(npcModel.Requests==beforeRequests&&npcEvents.Count==0&&npcConversation==null,"Procedural social outcomes create no model requests, transcripts or queued dialogue");
        long sequence=peerState.EventSequence;npcSocialScanAt=0;UpdateNpcProceduralSocial();
        Check(state.NextSocialSolarDay>day&&peerState.EventSequence==sequence,"Saved social cooldown prevents repeating the same encounter each scan");
        npcModel.Config.Enabled=true;npcModel.Config.BackgroundReactions=true;npcAmbientAt=0;
        Check(!QueueNpcEvent(recipient,"offscreen reaction",priority:3)&&npcEvents.Count==0,"Even high-priority incidental reactions cannot enter the queue outside earshot");
        Place(recipient,motion.X+1.5,motion.Z,270);Place(other,motion.X+1.5,motion.Z+1.4,180);
        view.transform.position=recipient.Root.transform.position+Vector3.up+Vector3.left*1.5f;npcHearing.Clear();
        Check(NpcPlayerCanHear(recipient)&&QueueNpcEvent(recipient,"audible witness reaction",priority:3),"An audible incidental reaction is eligible when the model is free");
        npcPending=npcEvents.Take(Time.unscaledTimeAsDouble);var pending=npcPending;
        var unfinished=new System.Threading.Tasks.TaskCompletionSource<string>();npcThinking=unfinished.Task;
        for(int i=0;i<30;i++)QueueNpcEvent(other,"another incidental witness reaction",priority:3);
        Check(npcEvents.Count==0&&npcModel.Requests==beforeRequests,"A busy model rejects background chatter instead of accumulating a backlog");
        npcTalkTarget=recipient;SpeakToNpc("What happened to your scraper?");
        Check(npcSuppressed==pending&&npcEvents.HasDirect(recipient.ResidentId),"Player speech preempts a high-priority background reaction from the same NPC");
        unfinished.SetCanceled();npcThinking=null;npcPending=null;npcSuppressed=null;npcEvents.Clear();npcPlans.Clear();npcTalkTarget=null;
        npcModel.Config.Enabled=false;npcHearing.Clear();
        var barrier=new GameObject("Social hearing test wall");barrier.transform.position=(view.transform.position+recipient.Root.transform.position+Vector3.up)*.5f;
        var wall=barrier.AddComponent<BoxCollider>();wall.size=new Vector3(.3f,3,3);Physics.SyncTransforms();
        Check(!NpcPlayerCanHear(recipient),"A nearby solid wall blocks player earshot");DestroyImmediate(barrier);npcHearing.Clear();
        // This capture shows actual residents, not a fabricated generated conversation.
        npcThoughts=true;NpcMemoryFor(recipient.ResidentId).Trigger="Procedural social exchange";NpcMemoryFor(recipient.ResidentId).Thought="Shared a witnessed theft report; no generated transcript.";
        Place(recipient,motion.X+4,motion.Z-.7,270);Place(other,motion.X+4,motion.Z+.7,270);UpdateCamera();
        var toward=transform.InverseTransformDirection((recipient.Root.transform.position+other.Root.transform.position)*.5f+Vector3.up*1.2f-view.transform.position).normalized;heading=Mathf.Atan2(toward.x,toward.z)*Mathf.Rad2Deg;pitch=-Mathf.Asin(toward.y)*Mathf.Rad2Deg;UpdateCamera();npcAimed=recipient;npcAimAt=Time.unscaledTimeAsDouble+2;
        paused=false;livingBenchmarkFreeze=true;yield return NpcLifeImage(output,"procedural-social-exchange");livingBenchmarkFreeze=false;paused=true;
        var all=Inventory.Items.Concat(livingPeople.Values.Where(p=>p.Belongings!=null).SelectMany(p=>p.Belongings.Items)).ToArray();Check(all.Select(p=>p.Id).Distinct().Count()==all.Length,"No gift or refusal duplicates a possession ID");
        LivingState.Copy().Validate();Inventory.Validate();foreach(var p in adults)p.Belongings.Validate();Check(true,"Social transactions and their real possessions validate together");
        if(Environment.GetCommandLineArgs().Contains("--worldgen-social-model"))
        {
            npcModel.Config.Enabled=true;npcModel.Config.BackgroundReactions=true;npcEvents.Clear();npcPlans.Clear();npcAmbientAt=npcSocialAt=0;npcSocialScanAt=Time.unscaledTimeAsDouble+300;npcTalkTarget=null;CloseChat();npcSocialCheckTrace=true;
            Place(recipient,motion.X+4,motion.Z-.7,270);Place(other,motion.X+4,motion.Z+.7,270);UpdateCamera();npcHearing.Clear();Physics.SyncTransforms();yield return null;
            int start=npcUtterances;PerformNpcSocialExchange(recipient,other);
            Check(npcConversation!=null&&npcEvents.Count==1,"An audible procedural encounter admits one optional live-model conversation");
            paused=false;until=Time.unscaledTimeAsDouble+180;bool streamed=false;int maxQueue=0;double statusAt=0;
            while(npcUtterances<start+2&&npcLastError.Length==0&&Time.unscaledTimeAsDouble<until)
            {
                maxQueue=Math.Max(maxQueue,npcEvents.Count);streamed|=npcSpeech.Values.Any(s=>s.Pace.Shown>0&&!s.Final);
                if(Time.unscaledTimeAsDouble>=statusAt){statusAt=Time.unscaledTimeAsDouble+1;File.AppendAllText(Path.Combine(output,"live-social-status.txt"),"Queue="+npcEvents.Count+" pending="+npcPending?.Person+" thinking="+(npcThinking!=null)+" model="+npcModel.Status+" busy="+npcModel.IsBusy+" errors="+npcLastError+" speech="+npcSpeech.Count+" utterances="+(npcUtterances-start)+" conversation="+(npcConversation!=null)+" hearing="+NpcPlayerCanHear(recipient)+","+NpcPlayerCanHear(other)+" peer="+NpcCanHear(recipient,other.Root.transform.position+Vector3.up,5)+" gap="+HumanDistance(recipient.Planet,other.Planet)+" flags="+paused+","+mapOpen+","+ecologyOpen+","+CommandChatStopped+" ready="+Ready+" pose="+humanReviewPose+"\n");}
                if(npcEvents.Count==0&&npcThinking==null&&npcSpeech.Count==0&&Time.unscaledTimeAsDouble>until-170)break;
                yield return null;
            }
            Check(npcUtterances>=start+2,"Both actual residents deliver a live Qwen conversational turn: "+npcLastError);
            Check(streamed&&maxQueue<=1,"Live ambient speech streams without building more than one queued request");
            File.WriteAllText(Path.Combine(output,"live-social-first.json"),JsonUtility.ToJson(NpcMemoryFor(recipient.ResidentId),true));
            File.WriteAllText(Path.Combine(output,"live-social-second.json"),JsonUtility.ToJson(NpcMemoryFor(other.ResidentId),true));
            npcTalkTarget=recipient;int before=npcUtterances;npcLastError="";SpeakToNpc("What time of day is it now?");
            until=Time.unscaledTimeAsDouble+180;while(npcUtterances==before&&npcLastError.Length==0&&Time.unscaledTimeAsDouble<until)yield return null;
            Check(npcUtterances>before&&npcConversation==null,"Player dialogue interrupts the ambient exchange and receives a real model reply: "+npcLastError);
            npcThoughts=true;OpenChat();yield return NpcLifeImage(output,"audible-social-and-player-reply");CloseChat();paused=true;
        }
        File.WriteAllText(Path.Combine(output,"recipient-memory.json"),JsonUtility.ToJson(NpcMemoryFor(recipient.ResidentId),true));paused=false;DisposeNpcMinds();
    }
}
