using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    HumanGroupMemory NpcGroupRecord(long id)
    {
        var record=LivingState.Groups.FirstOrDefault(g=>g.Group==id);if(record==null){record=new HumanGroupMemory{Group=id};LivingState.Groups.Add(record);}return record;
    }
    NpcCommunityState NpcCommunity(LivingGroup group)=>NpcGroupRecord(group.Situation.Band.Id).Community??(NpcGroupRecord(group.Situation.Band.Id).Community=new NpcCommunityState());
    NpcSocialState ExistingNpcSocial(long person)=>LivingState.Minds.FirstOrDefault(p=>p.Person==person)?.Social;
    ForagerCulture EffectiveNpcCulture(LivingGroup group)
    {
        var basis=sourceWorld.Prehistory.Foragers.Cultures[(group.Residents??group.Situation.Band).Culture];var local=LivingState.Groups.FirstOrDefault(g=>g.Group==group.Situation.Band.Id)?.Community;
        double sharing=local?.Sharing??-1,visitors=local?.Visitors??-1;
        if(group.PolicyBasis!=basis||group.PolicyCulture==null||group.PolicySharing!=sharing||group.PolicyVisitors!=visitors)
        {group.PolicyBasis=basis;group.PolicySharing=sharing;group.PolicyVisitors=visitors;group.PolicyCulture=NpcCommunityRules.Culture(basis,local);}return group.PolicyCulture;
    }
    long NpcCoordinator(LivingGroup group)
    {
        var c=EffectiveNpcCulture(group);if(group.Social==null||group.SocialCulture!=c){group.Social=ForagerSocialLife.Create(c,group.Residents);group.SocialCulture=c;}
        long saved=LivingState.Groups.FirstOrDefault(g=>g.Group==group.Situation.Band.Id)?.Community?.Coordinator??0;
        if(saved>0&&group.Residents.Members.Any(p=>p.Id==saved))group.Social.Coordinator=saved;return group.Social.Coordinator;
    }
    NpcWelcome NpcWelcomeAvailable(HumanActor actor)
    {
        if(PlayerRules.Identity?.Group==actor.GroupId||actor.Threat!=null)return NpcWelcome.None;
        var group=livingGroups[actor.GroupId];return NpcCommunityRules.Welcome(EffectiveNpcCulture(group),group.Residents,actor.ResidentId,NpcCoordinator(group),PlayerPropertyKey,ExistingNpcSocial);
    }
    string[] NpcSocialActions(HumanActor actor,NpcEventQueue.Event request)
    {
        if(request.Speaker!=0||string.IsNullOrEmpty(request.Words)||actor.Threat!=null)return Array.Empty<string>();
        var group=livingGroups[actor.GroupId];var list=new List<string>();
        if(NpcWelcomeAvailable(actor)!=NpcWelcome.None)list.Add("invite");
        if(PlayerRules.Identity?.Group==actor.GroupId&&NpcCoordinator(group)!=PlayerPropertyKey&&NpcCommunityRules.CanSupportLeader(actor.Resident,NpcAwarenessFor(actor).Personality,PlayerPropertyKey,NpcMemoryFor(actor.ResidentId).Social))list.Add("endorse");
        if(PlayerRules.Identity?.Group==actor.GroupId&&NpcCoordinator(group)==PlayerPropertyKey&&NpcSupportedCustoms(actor).Length>0)list.Add("support_custom");return list.ToArray();
    }
    string[] NpcSupportedCustoms(HumanActor actor)=>Enum.GetValues(typeof(NpcCustom)).Cast<NpcCustom>().Where(c=>NpcCommunityRules.Supports(c,NpcAwarenessFor(actor).Personality,livingGroups[actor.GroupId].Situation.SecurityRisk)).Select(c=>c.ToString()).ToArray();
    string NpcCommunityContext(HumanActor actor)
    {
        var group=livingGroups[actor.GroupId];long coordinator=NpcCoordinator(group);var welcome=NpcWelcomeAvailable(actor);var local=LivingState.Groups.FirstOrDefault(g=>g.Group==actor.GroupId)?.Community;
        bool guest=local?.Guests.Contains(PlayerPropertyKey)==true;
        string text=" Actual local coordinator: "+(group.Residents.Members.FirstOrDefault(p=>p.Id==coordinator)?.Name??"not presently established")+". Speaker's status here: "+(PlayerRules.Identity?.Group==actor.GroupId?"member":guest?"welcomed guest":"visitor")+".";
        if(welcome!=NpcWelcome.None)text+=" If the speaker asks to stay or join, the available invite action currently offers "+welcome.ToString().ToLowerInvariant()+" status; do not promise stronger status.";
        if(PlayerRules.Identity?.Group==actor.GroupId&&coordinator==PlayerPropertyKey)text+=" The speaker is the actual current coordinator. You still have your own opinion. Custom proposals you personally could support: "+string.Join(", ",NpcSupportedCustoms(actor))+". support_custom records your support; it does not unilaterally change the band. Use it only when discussing that specific proposal.";
        return text;
    }
    bool ApplyNpcCommunityDecision(HumanActor actor,NpcDecision decision)
    {
        if(decision.target!="player"||actor.Threat!=null||HumanDistance(actor.Planet,Position)>3.2)return false;
        var group=livingGroups[actor.GroupId];var local=NpcCommunity(group);long player=PlayerPropertyKey;
        if(decision.action=="invite")
        {
            var welcome=NpcWelcomeAvailable(actor);if(welcome==NpcWelcome.None)return false;
            if(!local.Guests.Contains(player))local.Guests.Add(player);
            if(welcome==NpcWelcome.Member)JoinNpcCommunity(actor,group,local);
            NpcMemoryFor(actor.ResidentId).Social.Record(new NpcIncident{Kind=NpcIncidentKind.Invitation,Actor=actor.ResidentId,Subject=player,SolarDay=PlayerRules.SolarDays,Position=actor.Planet,Detail="You welcomed the visitor as a "+welcome.ToString().ToLowerInvariant()+" of "+group.Residents.Name+"."});
            InventoryFeedback(group.Residents.Name+" welcomes you as a "+welcome.ToString().ToLowerInvariant()+".");return true;
        }
        if(PlayerRules.Identity?.Group!=actor.GroupId)return false;
        if(decision.action=="endorse")
        {
            if(!NpcCommunityRules.CanSupportLeader(actor.Resident,NpcAwarenessFor(actor).Personality,player,NpcMemoryFor(actor.ResidentId).Social))return false;
            if(local.LeaderCandidate!=player){local.LeaderCandidate=player;local.LeaderSupport.Clear();}if(!local.LeaderSupport.Contains(actor.ResidentId))local.LeaderSupport.Add(actor.ResidentId);
            NpcMemoryFor(actor.ResidentId).Social.Record(new NpcIncident{Kind=NpcIncidentKind.Leadership,Actor=actor.ResidentId,Subject=player,SolarDay=PlayerRules.SolarDays,Position=actor.Planet,Detail="You personally support this member taking a coordinating role."});
            bool chosen=NpcCommunityRules.Elect(local,group.Residents,EffectiveNpcCulture(group),player);if(chosen){group.Social=null;InventoryFeedback("The community supports you as its coordinator.");}return true;
        }
        if(decision.action=="support_custom"&&NpcCoordinator(group)==player&&Enum.TryParse(decision.proposal,out NpcCustom proposal)&&NpcCommunityRules.Supports(proposal,NpcAwarenessFor(actor).Personality,group.Situation.SecurityRisk))
        {
            local.Coordinator=player;bool accepted=NpcCommunityRules.VoteCustom(local,group.Residents,actor.ResidentId,player,proposal,PlayerRules.SolarDays);
            NpcMemoryFor(actor.ResidentId).Social.Record(new NpcIncident{Kind=NpcIncidentKind.Leadership,Actor=actor.ResidentId,Subject=player,SolarDay=PlayerRules.SolarDays,Position=actor.Planet,Detail="You supported a proposal about "+proposal+" in the camp discussion."});
            if(accepted){group.PolicyCulture=null;group.Social=null;group.Situation.WatchPolicy=local.Watch;livingQueryAt=0;InventoryFeedback("The community agrees to the proposed custom.");}return true;
        }
        return false;
    }
    void JoinNpcCommunity(HumanActor host,LivingGroup group,NpcCommunityState local)
    {
        long coordinator=NpcCoordinator(group);var f=sourceWorld.Prehistory.Foragers;var identity=PlayerRules.Identity?.Copy();
        if(identity==null)
        {
            var person=new ForagerPersonSample{Id=PlayerPropertyKey,Name=LanguageGenerator.Capitalize(string.IsNullOrWhiteSpace(Inventory.PlayerName)?"Visitor":Inventory.PlayerName),AgeYears=25,BirthYear=LivingClock.Years-25,DeathYear=LivingClock.Years+60,Appearance=Enumerable.Repeat(.5,8).ToArray(),Knowledge=new double[ForagerTechnology.All.Length],Skills=Enumerable.Repeat(.25,8).ToArray()};
            identity=new PlayerIdentity{Person=person,Techniques=(bool[])group.Residents.Techniques.Clone(),Culture=group.Residents.Culture,Language=group.Residents.Language,Belief=group.Residents.Belief,Lineage=group.Residents.Lineage,CultureName=f.Cultures[group.Residents.Culture].Name,LanguageName=f.Languages[group.Residents.Language].Name,BeliefName=f.Beliefs[group.Residents.Belief].Name,LineageName=f.Lineages[group.Residents.Lineage].Name};
        }
        identity.Group=group.Situation.Band.Id;identity.Cohort=group.Residents.Cohort;identity.District=group.Residents.District;identity.BandName=group.Residents.Name;identity.Person.Household=host.Resident.Household;identity.Validate();
        if(!local.Joined.Any(p=>p.Id==identity.Person.Id))local.Joined.Add(PlayerIdentity.CopyPerson(identity.Person));
        foreach(var family in identity.Family)if(!local.JoinedFamilies.Any(p=>p.Id==family.Id))local.JoinedFamilies.Add(family);
        local.Coordinator=coordinator;group.Residents=NpcCommunityRules.WithJoined(group.Residents,local);group.Social=null;PlayerRules.Identity=identity;
        humanPlayer.Resident=identity.Person;humanPlayer.ResidentId=identity.Person.Id;humanPlayer.GroupId=identity.Group;humanPlayer.Root.name=identity.Person.Name;
        var record=LivingState.People.FirstOrDefault(p=>p.Person==identity.Person.Id);if(record!=null)record.Group=identity.Group;
        livingQueryAt=0;
    }
}
