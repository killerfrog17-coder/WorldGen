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
    public IEnumerator CheckNpcCommunity(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok){File.WriteAllLines(Path.Combine(output,"failed-checks.txt"),report.Concat(new[]{"FAIL "+text}));Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);Debug.Log("NPC COMMUNITY PASS "+text);}
        npcModel??=new NpcModelClient();npcModel.Config.Enabled=false;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();
        double until=Time.unscaledTimeAsDouble+90;while(!livingPeople.Values.Any(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18)&&Time.unscaledTimeAsDouble<until)yield return null;
        var host=livingPeople.Values.FirstOrDefault(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18);Check(host!=null,"An actual adult host exists");paused=true;livingQueryAt=Time.unscaledTimeAsDouble+40;while(livingWork!=null)yield return null;
        var group=livingGroups[host.GroupId];var local=NpcCommunity(group);local.Coordinator=host.ResidentId;local.Visitors=.85;local.Sharing=.9;group.PolicyCulture=null;
        // Controlled council fixture: the same real resident returns from
        // another actual nearby community. No additional body is generated.
        var other=livingGroups.Values.FirstOrDefault(g=>g.Situation.Band.Id!=host.GroupId);Check(other!=null,"Another actual nearby community exists for the membership fixture");
        var original=PlayerRules.Identity.Copy();PlayerRules.Identity=original.Copy();PlayerRules.Identity.Group=other.Situation.Band.Id;PlayerRules.Identity.BandName=other.Situation.Band.Name;humanPlayer.GroupId=PlayerRules.Identity.Group;
        motion.Place(host.Motor.X-1.5,host.Motor.Z,CampFloor(host.Motor.X-1.5,host.Motor.Z));UpdateHumans();host.Threat=null;long person=PlayerPropertyKey;var originalIds=Inventory.Items.Select(p=>p.Id).OrderBy(x=>x).ToArray();
        Check(NpcWelcomeAvailable(host)==NpcWelcome.Guest,"An established welcoming host can offer guest status before sufficient membership support");
        var share=PortableTechnology.Create(PossessionKind.Berries,Position);share.Id="council-shared-food";share.Place=PossessionPlace.Ground;share.OwnerGroup=host.GroupId;Inventory.Items.Add(share);
        Check(!PlayerMayTake(share),"An uninvited outsider does not acquire the community's property permissions");
        Check(ApplyNpcCommunityDecision(host,new NpcDecision{action="invite",target="player"})&&local.Guests.Contains(person)&&PlayerRules.Identity.Group!=host.GroupId,"The invitation executor welcomes a guest without silently promoting full membership");
        Check(PlayerMayTake(share),"The welcomed guest receives the actual communal-sharing permission");
        var personal=PortableTechnology.Create(PossessionKind.StoneBlade,Position);personal.Id="council-private-tool";personal.Place=PossessionPlace.Ground;NpcOwnership.Assign(personal,host.Resident,host.GroupId);Inventory.Items.Add(personal);Check(!PlayerMayTake(personal),"A guest invitation does not grant another person's private tools");
        foreach(var p in group.Residents.Members.Where(p=>p.AgeYears>=18&&p.Id!=person).Take(3)){var tie=NpcMemoryFor(p.Id).Social.Tie(person,PlayerRules.SolarDays);tie.Trust=.7;tie.Respect=.6;}
        Check(NpcWelcomeAvailable(host)==NpcWelcome.Member,"The actual leader can offer membership after multiple established directed relationships");
        Check(ApplyNpcCommunityDecision(host,new NpcDecision{action="invite",target="player"})&&PlayerRules.Identity.Group==host.GroupId&&PlayerRules.Identity.Person.Id==original.Person.Id,"Accepted membership changes affiliation while preserving the same person");
        Check(PlayerRules.Identity.Person.Mother==original.Person.Mother&&PlayerRules.Identity.Person.Father==original.Person.Father&&PlayerRules.Identity.Person.Partner==original.Person.Partner,"Joining preserves ancestry and existing family relationships");
        Check(originalIds.All(id=>Inventory.Find(id)!=null)&&Inventory.Items.Select(p=>p.Id).Distinct().Count()==Inventory.Items.Count,"Joining neither creates a replacement loadout nor loses carried possessions");
        Check(group.Residents.Members.Count(p=>p.Id==person)==1,"The joined resident occurs once in the local roster");
        var endorsement=new NpcDecision{action="endorse",target="player"};Check(ApplyNpcCommunityDecision(host,endorsement)&&NpcCoordinator(group)!=person,"One valid endorsement records support without making the player chief");
        local.LeaderCandidate=person;local.LeaderSupport=group.Residents.Members.Where(p=>p.AgeYears>=18&&p.Id!=person).Select(p=>p.Id).ToList();
        Check(NpcCommunityRules.Elect(local,group.Residents,EffectiveNpcCulture(group),person)&&NpcCoordinator(group)==person,"A controlled majority of actual adult members establishes the new coordinator");
        var proposals=NpcSupportedCustoms(host);Check(proposals.Length>0,"The host has specific personal positions on proposed customs");string proposal=proposals[0];var choice=(NpcCustom)Enum.Parse(typeof(NpcCustom),proposal);
        local.CustomVotes.Clear();Check(ApplyNpcCommunityDecision(host,new NpcDecision{action="support_custom",target="player",proposal=proposal})&&local.CustomVotes.Count==1,"A discussion records an individual's vote rather than imposing the chief's wish");
        int needed=Math.Max(2,(int)Math.Ceiling(group.Residents.Members.Count(p=>p.AgeYears>=18)*.55));
        local.CustomVotes=group.Residents.Members.Where(p=>p.AgeYears>=18&&p.Id!=person&&p.Id!=host.ResidentId).Take(Math.Max(0,needed-2)).Select(p=>new NpcCustomVote{Person=p.Id,Proposer=person,Custom=choice,SolarDay=PlayerRules.SolarDays}).ToList();
        Check(ApplyNpcCommunityDecision(host,new NpcDecision{action="support_custom",target="player",proposal=proposal})&&local.CustomVotes.Count==0,"A controlled majority adopts a specific local custom through the executor");
        LivingState.Copy().Validate();PlayerRules.Identity.Validate();Inventory.Validate();Check(true,"Membership, real possessions, decisions and preserved identity validate together");
        File.WriteAllText(Path.Combine(output,"community-memory.json"),JsonUtility.ToJson(local,true));paused=false;DisposeNpcMinds();
    }
}
