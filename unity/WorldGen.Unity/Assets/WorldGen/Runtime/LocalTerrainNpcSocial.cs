using System;
using System.Linq;
using System.Text;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class SocialConversation {public long A,B;public int Turns=4;public double Until;public NpcDiscussion Discussion;public bool Recorded;}
    SocialConversation npcConversation;double npcSocialAt,npcWaitSocialDay;long npcSocialSequence;
    bool NpcCanHear(HumanActor listener,Vector3 source,float radius)
    {var at=listener.Root.transform.position+Vector3.up;var delta=source-at;return delta.magnitude<radius&&(delta.magnitude<.1f||TraceCombatObstacle(new Ray(at,delta.normalized),Math.Max(0,delta.magnitude-.1f),checkGround:false)==null);}
    string NpcSocialContext(HumanActor actor,LivingGroup group,ForagerPersonSample speaker)
    {
        var band=group.Residents;var culture=sourceWorld.Prehistory.Foragers.Cultures[band.Culture];
        if(group.Social==null||group.SocialCulture!=culture){group.Social=ForagerSocialLife.Create(culture,band);group.SocialCulture=culture;}
        var social=group.Social;var text=new StringBuilder(" Camp customs: "+social.DecisionMaking+" "+social.Sharing+" "+social.Visitors+" "+social.SpeechStyle+" "+social.Roles(band));
        var personal=NpcMemoryFor(actor.ResidentId).Social;
        var ties=band.Members.Where(p=>p.Id!=actor.ResidentId&&p.AgeYears>=4).Select(p=>(person:p,affinity:NpcSocialRules.Affinity(culture,actor.Resident,p,band,personal))).OrderByDescending(p=>p.affinity).ToArray();
        if(speaker!=null)text.Append(" Your actual feelings toward the speaker: ").Append(NpcSocialRules.Opinion(personal,speaker.Id)).Append('.');
        foreach(var tie in ties.Take(2))if(tie.affinity>.45)text.Append(" You often enjoy talking and working with ").Append(tie.person.Name).Append('.');
        if(ties.Length>0&&ties[ties.Length-1].affinity<-.25)text.Append(" You have some personal friction with ").Append(ties[ties.Length-1].person.Name).Append("; this does not mean violence.");
        foreach(var subject in new[]{actor.Resident,speaker})if(subject!=null&&band.Members.Any(p=>p.Id==subject.Id))
        {
            var kin=band.Members.Where(p=>p.Id!=subject.Id).Select(p=>(person:p,relation:ForagerSocialLife.Relationship(subject,p,band))).Where(p=>p.relation!="a fellow band member"&&p.relation!="a member of your household").Take(8);
            text.Append(subject.Id==actor.ResidentId?" YOUR known relatives here: ":" SPEAKER's known relatives here: ");foreach(var k in kin)text.Append(k.person.Name).Append(" is ").Append(k.relation.Replace("your ",subject.Id==actor.ResidentId?"your ":"their ")).Append("; ");
        }
        var situation=group.Situation;double days=(situation.DepartureDay-LivingClock.CalendarDay)/LivingClock.CalendarRate;
        text.Append(" Current camp phase: ").Append(situation.Phase).Append('.');
        if(situation.PermanentBase)text.Append(" This is a long-term base; no departure is currently planned.");
        else if(situation.Phase==HumanCampPhase.Travelling)text.Append(" Your group is moving to the next seasonal camp.");
        else text.Append(" Current plan is to leave this camp in roughly ").Append(days>=2?Math.Ceiling(days).ToString("0")+" local solar days":Math.Max(0,Math.Round(days*24/2)*2).ToString("0")+" local solar hours").Append("; plans can change. Next camp purpose: ").Append(situation.Destination?.Purpose??"the next seasonal camp").Append('.');
        return text.ToString();
    }
    void UpdateNpcSocial()
    {
        double now=Time.unscaledTimeAsDouble;
        if(npcConversation!=null&&(now>npcConversation.Until||!livingPeople.TryGetValue(npcConversation.A,out var a0)||!livingPeople.TryGetValue(npcConversation.B,out var b0)||HumanDistance(a0.Planet,Position)>14||HumanDistance(b0.Planet,Position)>14||a0.CurrentActivity==HumanActivity.Retreat||b0.CurrentActivity==HumanActivity.Retreat))CancelNpcConversation();
        if(campResting){WaitNpcApproach();return;}
        if(npcConversation!=null||npcTalkTarget!=null&&chatOpen||npcThinking!=null||npcEvents.Count>0||npcSpeech.Count>0||now<npcSocialAt||campSleeping)return;
        npcSocialAt=now+12; if(npcModel?.Config.BackgroundReactions!=true)return;
        var nearby=livingPeople.Values.Where(p=>p.Health>40&&p.Resident.AgeYears>=7&&p.Speed<.6f&&p.CurrentActivity!=HumanActivity.Sleep&&p.CurrentActivity!=HumanActivity.Retreat&&HumanDistance(p.Planet,Position)<10).OrderBy(p=>p.ResidentId).ToArray();
        HumanActor first=null,second=null;double best=double.MinValue;
        foreach(var a in nearby)foreach(var b in nearby)
        {
            if(a.ResidentId>=b.ResidentId||a.GroupId!=b.GroupId||HumanDistance(a.Planet,b.Planet)>6)continue;
            var group=livingGroups[a.GroupId];var c=sourceWorld.Prehistory.Foragers.Cultures[group.Residents.Culture];
            double score=NpcSocialRules.Affinity(c,a.Resident,b.Resident,group.Residents,NpcMemoryFor(a.ResidentId).Social)+Rng.Value(c.Seed,"conversation-pair",unchecked((int)(++npcSocialSequence)^ (int)a.ResidentId^(int)b.ResidentId))*.6;
            if(Rng.Value(c.Seed,"disagreement-opportunity",(int)(now/60))<.16)
            {var d=NpcDiscussion.For(NpcAwarenessFor(a).Personality,NpcAwarenessFor(b).Personality,c,a.Resident.AgeYears<14||b.Resident.AgeYears<14);if(d.Disagreement)score+=1.4;}
            if(score>best){best=score;first=a;second=b;}
        }
        if(first==null)return;
        var ray=new Ray(first.Root.transform.position+Vector3.up,second.Root.transform.position-first.Root.transform.position);
        if(TraceCombatObstacle(ray,(float)HumanDistance(first.Planet,second.Planet),checkGround:false)!=null)return;
        var customs=sourceWorld.Prehistory.Foragers.Cultures[livingGroups[first.GroupId].Residents.Culture];
        var discussion=NpcDiscussion.For(NpcAwarenessFor(first).Personality,NpcAwarenessFor(second).Personality,customs,first.Resident.AgeYears<14||second.Resident.AgeYears<14);
        npcConversation=new SocialConversation{A=first.ResidentId,B=second.ResidentId,Until=now+100,Discussion=discussion};
        NpcListen(second,first.ResidentId);
        string topic=discussion.Disagreement?"You and "+second.Resident.Name+" have different views on "+discussion.Topic+". Your view: "+discussion.FirstView+". Their tendency: "+discussion.SecondView+". Express your own view without inventing a missing meal, past insult or fight.":"Start a short natural conversation about actual current work, camp plans or your own interest in "+NpcAwarenessFor(first).Personality.Interest+". Do not invent events or possessions.";
        if(!QueueNpcEvent(first,topic,priority:2,speaker:second.ResidentId)){EndNpcConversation();return;}
        NpcListen(first,second.ResidentId);var culture=sourceWorld.Prehistory.Foragers.Cultures[livingGroups[first.GroupId].Residents.Culture];npcSocialAt=now+Math.Max(npcModel.Config.VisitorCooldownSeconds,40+55*(1-culture.GuestAccess));
    }
    void EndNpcConversation()
    {
        if(npcConversation==null)return;
        foreach(long id in new[]{npcConversation.A,npcConversation.B})if(npcPlans.TryGetValue(id,out var plan)&&plan.Listening)npcPlans.Remove(id);
        npcConversation=null;
    }
    void CancelNpcConversation()
    {
        if(npcPending?.Speaker>0){npcSuppressed=npcPending;npcModel?.CancelCurrent();}
        foreach(var line in npcSpeech.Values.Where(s=>s.Event.Speaker>0&&!s.Delivered).ToArray())AbortNpcSpeech(line.Event);
        npcEvents.CancelSocial();EndNpcConversation();
    }
    void WaitNpcApproach()
    {
        if(npcThinking!=null||npcEvents.Count>0||npcModel?.Config.BackgroundReactions!=true||PlayerRules.SolarDays<npcWaitSocialDay)return;
        npcWaitSocialDay=PlayerRules.SolarDays+600/(PlayerRules.Settings.RealMinutesPerDay*60);
        var actor=livingPeople.Values.Where(p=>p.GroupId==PlayerRules.Identity?.Group&&p.Resident.AgeYears>=18&&p.Health>40&&p.Speed<.3&&p.CurrentActivity!=HumanActivity.Sleep&&HumanDistance(p.Planet,Position)<12).OrderBy(p=>p.ResidentId).FirstOrDefault();
        if(actor==null)return;
        if(QueueNpcEvent(actor,"A familiar bandmate is waiting nearby. Approach and start a brief conversation about the actual camp plans or current work, without inventing an urgent problem.",priority:2))
        {npcTalkTarget=actor;npcPlans[actor.ResidentId]=new MindPlan{Decision=new NpcDecision{action="approach",target="player",seconds=20},Event=new NpcEventQueue.Event{Person=actor.ResidentId},Until=Time.unscaledTimeAsDouble+40};WakeCamp(actor.Resident.Name+" approaches to speak to you.");}
    }
    void ContinueNpcConversation(HumanActor actor,NpcEventQueue.Event heard,string speech,string target)
    {
        foreach(var observer in livingPeople.Values.Where(p=>p!=actor&&p.Health>0&&p.Resident.AgeYears>=4&&NpcCanHear(p,actor.Root.transform.position+Vector3.up,4)).Take(4))
            if(target!="person:"+observer.ResidentId)NpcMemoryFor(observer.ResidentId).Hear(actor.ResidentId,actor.Resident.Name,speech);
        if(target=="player"){if(campResting||campSleeping)WakeCamp(actor.Resident.Name+" is speaking to you.");return;}
        if(!target.StartsWith("person:")||!long.TryParse(target.Substring(7),out var id)||!livingPeople.TryGetValue(id,out var other)||HumanDistance(actor.Planet,other.Planet)>5)return;
        if(!NpcCanHear(other,actor.Root.transform.position+transform.up,5))return;
        if(npcConversation!=null&&!npcConversation.Recorded&&(actor.ResidentId==npcConversation.A||actor.ResidentId==npcConversation.B))
        {
            npcConversation.Recorded=true;var d=npcConversation.Discussion;
            NpcSocialRules.SharedConversation(NpcMemoryFor(actor.ResidentId).Social,other.ResidentId,PlayerRules.SolarDays,d?.Disagreement==true,d?.Topic??"their actual work");
            NpcSocialRules.SharedConversation(NpcMemoryFor(other.ResidentId).Social,actor.ResidentId,PlayerRules.SolarDays,d?.Disagreement==true,d?.Topic??"their actual work");
        }
        bool continuing=npcConversation!=null&&(actor.ResidentId==npcConversation.A||actor.ResidentId==npcConversation.B)&&--npcConversation.Turns>0&&heard.Depth<3&&HumanDistance(actor.Planet,Position)<12;
        string personalView=npcConversation?.Discussion?.Disagreement==true?" Your own view on "+npcConversation.Discussion.Topic+": "+(other.ResidentId==npcConversation.A?npcConversation.Discussion.FirstView:npcConversation.Discussion.SecondView)+". You can disagree naturally without inventing events.":"";
        if(continuing&&QueueNpcEvent(other,"A nearby bandmate addressed you. Respond to what they actually said; you may ask one relevant follow-up, or finish naturally."+personalView,speech,2,actor.ResidentId,heard.Depth+1))
        {npcConversation.Until=Time.unscaledTimeAsDouble+60;NpcListen(actor,other.ResidentId);NpcListen(other,actor.ResidentId);}
        else {NpcMemoryFor(id).Hear(actor.ResidentId,actor.Resident.Name,speech);EndNpcConversation();}
    }
}
