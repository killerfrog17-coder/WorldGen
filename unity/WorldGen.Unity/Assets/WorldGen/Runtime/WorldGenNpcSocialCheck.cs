using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckNpcSocialActions(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok){File.WriteAllLines(Path.Combine(output,"failed-checks.txt"),report.Concat(new[]{"FAIL "+text}));Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);Debug.Log("NPC SOCIAL PASS "+text);}
        npcModel??=new NpcModelClient();npcModel.Config.Enabled=false;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();
        double until=Time.unscaledTimeAsDouble+90;while(livingPeople.Values.Count(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18)<2&&Time.unscaledTimeAsDouble<until)yield return null;
        var adults=livingPeople.Values.Where(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18).Take(2).ToArray();Check(adults.Length==2,"Two actual adult residents exist");paused=true;livingQueryAt=Time.unscaledTimeAsDouble+40;while(livingWork!=null)yield return null;
        var recipient=adults[0];var other=adults[1];
        void Place(HumanActor p,double x,double z,float yaw){p.Motor.Place(x,z,CampFloor(x,z));p.Planet=frame.Position(x,z);p.Root.transform.localPosition=new Vector3((float)(x-originX),(float)(p.Motor.Y-TerrainMotion.EyeHeightM),(float)(z-originZ));p.Root.transform.localRotation=Quaternion.Euler(0,yaw,0);p.Yaw=p.TargetYaw=yaw;p.CurrentActivity=HumanActivity.Socialise;p.Threat=null;}
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
        var all=Inventory.Items.Concat(livingPeople.Values.Where(p=>p.Belongings!=null).SelectMany(p=>p.Belongings.Items)).ToArray();Check(all.Select(p=>p.Id).Distinct().Count()==all.Length,"No gift or refusal duplicates a possession ID");
        LivingState.Copy().Validate();Inventory.Validate();foreach(var p in adults)p.Belongings.Validate();Check(true,"Social transactions and their real possessions validate together");
        File.WriteAllText(Path.Combine(output,"recipient-memory.json"),JsonUtility.ToJson(NpcMemoryFor(recipient.ResidentId),true));paused=false;DisposeNpcMinds();
    }
}
