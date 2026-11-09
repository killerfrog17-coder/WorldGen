using System;
using System.Linq;
using System.Text;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class SocialConversation {public long A,B;public int Turns=4;public double Until;public NpcDiscussion Discussion;public bool Recorded;}
    SocialConversation npcConversation;double npcSocialAt,npcWaitSocialDay,npcSocialScanAt,npcAmbientAt;long npcSocialSequence;int npcSocialCursor;
    readonly System.Collections.Generic.Dictionary<long,(double At,Vector3 Ear,Vector3 Source,bool Audible)> npcHearing=new System.Collections.Generic.Dictionary<long,(double,Vector3,Vector3,bool)>();
    bool NpcPlayerCanHear(HumanActor actor)
    {
        if(actor?.Root==null||actor.Health<=0||view==null)return false;
        var source=actor.Root.transform.position+Vector3.up;var ear=view.transform.position;var delta=source-ear;float distance=delta.magnitude;if(distance>=10)return false;
        double now=Time.unscaledTimeAsDouble;
        if(npcHearing.TryGetValue(actor.ResidentId,out var cached)&&now-cached.At<.3&&(ear-cached.Ear).sqrMagnitude<.25f&&(source-cached.Source).sqrMagnitude<.25f)return cached.Audible;
        bool audible=distance<.1f||TraceCombatObstacle(new Ray(ear,delta/distance),Math.Max(0,distance-.15f))==null;
        if(npcHearing.Count>=128)npcHearing.Clear();npcHearing[actor.ResidentId]=(now,ear,source,audible);return audible;
    }
    bool NpcBackgroundRelevant(NpcEventQueue.Event item)
        =>livingPeople.TryGetValue(item.Person,out var actor)&&NpcPlayerCanHear(actor)&&
        (item.Speaker==0||livingPeople.TryGetValue(item.Speaker,out var peer)&&NpcPlayerCanHear(peer)&&NpcCanHear(actor,peer.Root.transform.position+Vector3.up,5));
    bool NpcBackgroundAvailable=>npcThinking==null&&npcModel?.IsBusy!=true&&npcEvents.Count==0&&!npcSpeech.Values.Any(s=>!s.Delivered)&&!(chatOpen&&npcTalkTarget!=null);
    bool NpcCanHear(HumanActor listener,Vector3 source,float radius)
    {var at=listener.Root.transform.position+Vector3.up;var delta=source-at;return delta.magnitude<radius&&(delta.magnitude<.1f||TraceCombatObstacle(new Ray(at,delta.normalized),Math.Max(0,delta.magnitude-.1f),checkGround:false)==null);}
    string NpcSocialContext(HumanActor actor,LivingGroup group,ForagerPersonSample speaker)
    {
        var band=group.Residents;var culture=EffectiveNpcCulture(group);NpcCoordinator(group);
        if(group.Social==null||group.SocialCulture!=culture){group.Social=ForagerSocialLife.Create(culture,band);group.SocialCulture=culture;}
        var social=group.Social;var text=new StringBuilder(" Camp customs: "+social.DecisionMaking+" "+social.Sharing+" "+social.Visitors+" "+social.SpeechStyle+" "+social.Roles(band));
        var personal=NpcMemoryFor(actor.ResidentId).Social;
        var ties=band.Members.Where(p=>p.Id!=actor.ResidentId&&p.AgeYears>=4&&!personal.KnownPeople.Any(k=>k.Person==p.Id&&k.SeenDead)).Select(p=>(person:p,affinity:NpcSocialRules.Affinity(culture,actor.Resident,p,band,personal))).OrderByDescending(p=>p.affinity).ToArray();
        if(speaker!=null)text.Append(" Your actual feelings toward the speaker: ").Append(NpcSocialRules.Opinion(personal,speaker.Id)).Append('.');
        foreach(var tie in ties.Take(2))if(tie.affinity>.45)text.Append(" You often enjoy talking and working with ").Append(tie.person.Name).Append('.');
        if(ties.Length>0&&ties[ties.Length-1].affinity<-.25)text.Append(" You have some personal friction with ").Append(ties[ties.Length-1].person.Name).Append("; this does not mean violence.");
        foreach(var subject in new[]{actor.Resident,speaker})if(subject!=null&&band.Members.Any(p=>p.Id==subject.Id))
        {
            var kin=band.Members.Where(p=>p.Id!=subject.Id).Select(p=>(person:p,relation:ForagerSocialLife.Relationship(subject,p,band))).Where(p=>p.relation!="a fellow band member"&&p.relation!="a member of your household").Take(8);
            text.Append(subject.Id==actor.ResidentId?" YOUR known relatives here: ":" SPEAKER's known relatives here: ");foreach(var k in kin)text.Append(k.person.Name).Append(" is ").Append(k.relation.Replace("your ",subject.Id==actor.ResidentId?"your ":"their ")).Append(personal.KnownPeople.Any(p=>p.Person==k.person.Id&&p.SeenDead)?" (known deceased); ":"; ");
        }
        var situation=group.Situation;double itineraryDelay=LivingState.Groups.FirstOrDefault(g=>g.Group==actor.GroupId)?.Travel?.DelayCalendarDays??0;double days=(situation.DepartureDay+itineraryDelay-LivingClock.CalendarDay)/LivingClock.CalendarRate;
        text.Append(" Current camp phase: ").Append(situation.Phase).Append('.');
        if(situation.PermanentBase)text.Append(" This is a long-term base; no departure is currently planned.");
        else if(situation.Phase==HumanCampPhase.Travelling)text.Append(" Your group is moving to the next seasonal camp.");
        else text.Append(" Current plan is to leave this camp in roughly ").Append(days>=2?Math.Ceiling(days).ToString("0")+" local solar days":Math.Max(0,Math.Round(days*24/2)*2).ToString("0")+" local solar hours").Append("; plans can change. Next camp purpose: ").Append(situation.Destination?.Purpose??"the next seasonal camp").Append('.');
        return text.Append(NpcCommunityContext(actor)).ToString();
    }
    void UpdateNpcSocial()
    {
        double now=Time.unscaledTimeAsDouble;
        if(npcConversation!=null&&(now>npcConversation.Until||!livingPeople.TryGetValue(npcConversation.A,out var a0)||!livingPeople.TryGetValue(npcConversation.B,out var b0)||!NpcPlayerCanHear(a0)||!NpcPlayerCanHear(b0)||a0.CurrentActivity==HumanActivity.Retreat||b0.CurrentActivity==HumanActivity.Retreat))CancelNpcConversation();
        UpdateNpcProceduralSocial();
        if(campResting)WaitNpcApproach();
    }
    bool NpcSocialFree(HumanActor p)=>p.Health>40&&p.Resident.AgeYears>=7&&p.Speed<.6f&&p.Threat==null&&!npcPlans.ContainsKey(p.ResidentId)&&
        (p.CurrentActivity==HumanActivity.Rest||p.CurrentActivity==HumanActivity.Socialise||p.CurrentActivity==HumanActivity.Teach||p.CurrentActivity==HumanActivity.Play);
    void UpdateNpcProceduralSocial()
    {
        double now=Time.unscaledTimeAsDouble,day=PlayerRules.SolarDays;if(now<npcSocialScanAt)return;npcSocialScanAt=now+1.5;
        // Only loaded people; inspect four initiators per scan, not every pair in a band or world.
        var people=livingPeople.Values.ToArray();if(people.Length==0)return;
        for(int i=0;i<Math.Min(4,people.Length);i++)
        {
            var first=people[npcSocialCursor++%people.Length];if(npcSocialCursor==int.MaxValue)npcSocialCursor=0;
            if(!NpcSocialFree(first)||NpcMemoryFor(first.ResidentId).Social.NextSocialSolarDay>day)continue;
            var group=livingGroups[first.GroupId];var culture=EffectiveNpcCulture(group);HumanActor second=null;double best=double.MinValue;
            foreach(var other in people)
            {
                if(other==first||other.GroupId!=first.GroupId||!NpcSocialFree(other)||HumanDistance(first.Planet,other.Planet)>4||NpcMemoryFor(other.ResidentId).Social.NextSocialSolarDay>day)continue;
                double score=NpcSocialRules.Affinity(culture,first.Resident,other.Resident,group.Residents,NpcMemoryFor(first.ResidentId).Social)+Rng.Value(culture.Seed,"social-pair",unchecked((int)(npcSocialSequence+other.ResidentId)))*.6;
                if(score>best&&NpcCanHear(first,other.Root.transform.position+Vector3.up,4)){best=score;second=other;}
            }
            if(second!=null)PerformNpcSocialExchange(first,second);
        }
    }
    void PerformNpcSocialExchange(HumanActor first,HumanActor second)
    {
        double now=Time.unscaledTimeAsDouble,day=PlayerRules.SolarDays;var group=livingGroups[first.GroupId];var culture=EffectiveNpcCulture(group);
        int sequence=unchecked((int)++npcSocialSequence);var left=NpcMemoryFor(first.ResidentId).Social;var right=NpcMemoryFor(second.ResidentId).Social;
        left.NextSocialSolarDay=right.NextSocialSolarDay=day+.012+.012*Rng.Value(culture.Seed,"social-spacing",sequence);
        var discussion=NpcDiscussion.For(NpcAwarenessFor(first).Personality,NpcAwarenessFor(second).Personality,culture,first.Resident.AgeYears<14||second.Resident.AgeYears<14);
        // Most routine company is unremarkable; disagreements are opportunities, not constant arguments.
        discussion.Disagreement&=Rng.Value(culture.Seed,"social-disagreement",sequence)<.16;
        if(Rng.Value(culture.Seed,"social-effect",sequence)<.7)
        {
            NpcSocialRules.SharedConversation(left,second.ResidentId,day,discussion.Disagreement,discussion.Topic);
            NpcSocialRules.SharedConversation(right,first.ResidentId,day,discussion.Disagreement,discussion.Topic);
        }
        NpcSocialExchange.ShareOne(left,right,first.ResidentId,first.Resident.Name,day,Math.Max(0,NpcSocialRules.Affinity(culture,second.Resident,first.Resident,group.Residents,right)));
        NpcSocialExchange.ShareOne(right,left,second.ResidentId,second.Resident.Name,day,Math.Max(0,NpcSocialRules.Affinity(culture,first.Resident,second.Resident,group.Residents,left)));
        foreach(var participant in new[]{first,second})
        {
            var partner=participant==first?second:first;var memory=NpcMemoryFor(participant.ResidentId);
            memory.Trigger="Time with a nearby bandmate";memory.Thought="Talking with "+partner.Resident.Name+" about "+discussion.Topic+".";
            npcPlans[participant.ResidentId]=new MindPlan{Decision=new NpcDecision{action="wait",target="person:"+partner.ResidentId,seconds=8},Event=new NpcEventQueue.Event{Person=participant.ResidentId,Speaker=partner.ResidentId,Background=true},Until=now+8+6*Rng.Value(culture.Seed,"social-duration",sequence),Spoke=true,ProceduralSocial=true,Stage="Socialising"};
        }
        // The social event has already happened. Rendering words is optional, never its executor.
        if(campSleeping||campResting||npcConversation!=null||!NpcBackgroundAvailable||now<npcSocialAt||npcModel?.Config.Enabled!=true||npcModel.Config.BackgroundReactions!=true||!NpcPlayerCanHear(first)||!NpcPlayerCanHear(second))return;
        var customs=sourceWorld.Prehistory.Foragers.Cultures[livingGroups[first.GroupId].Residents.Culture];
        npcConversation=new SocialConversation{A=first.ResidentId,B=second.ResidentId,Until=now+100,Discussion=discussion,Recorded=true};
        NpcListen(second,first.ResidentId);
        string topic=discussion.Disagreement?"You and "+second.Resident.Name+" have different views on "+discussion.Topic+". Your view: "+discussion.FirstView+". Their tendency: "+discussion.SecondView+". Express your own view without inventing a missing meal, past insult or fight.":"Start a short natural conversation about actual current work, camp plans or your own interest in "+NpcAwarenessFor(first).Personality.Interest+". Do not invent events or possessions.";
        if(!QueueNpcEvent(first,topic,priority:2,speaker:second.ResidentId)){EndNpcConversation();return;}
        NpcListen(first,second.ResidentId);npcSocialAt=now+Math.Max(npcModel.Config.VisitorCooldownSeconds,40+55*(1-customs.GuestAccess));
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
        if(target=="player"){if(!string.IsNullOrEmpty(heard.Words))NpcSocialRules.SharedConversation(NpcMemoryFor(actor.ResidentId).Social,PlayerPropertyKey,PlayerRules.SolarDays,false,"the actual conversation");if(campResting||campSleeping)WakeCamp(actor.Resident.Name+" is speaking to you.");return;}
        if(!target.StartsWith("person:")||!long.TryParse(target.Substring(7),out var id)||!livingPeople.TryGetValue(id,out var other)||HumanDistance(actor.Planet,other.Planet)>5)return;
        if(!NpcCanHear(other,actor.Root.transform.position+transform.up,5))return;
        if(npcConversation!=null&&!npcConversation.Recorded&&(actor.ResidentId==npcConversation.A||actor.ResidentId==npcConversation.B))
        {
            npcConversation.Recorded=true;var d=npcConversation.Discussion;
            NpcSocialRules.SharedConversation(NpcMemoryFor(actor.ResidentId).Social,other.ResidentId,PlayerRules.SolarDays,d?.Disagreement==true,d?.Topic??"their actual work");
            NpcSocialRules.SharedConversation(NpcMemoryFor(other.ResidentId).Social,actor.ResidentId,PlayerRules.SolarDays,d?.Disagreement==true,d?.Topic??"their actual work");
        }
        bool continuing=npcConversation!=null&&(actor.ResidentId==npcConversation.A||actor.ResidentId==npcConversation.B)&&--npcConversation.Turns>0&&heard.Depth<3&&NpcPlayerCanHear(actor)&&NpcPlayerCanHear(other);
        string personalView=npcConversation?.Discussion?.Disagreement==true?" Your own view on "+npcConversation.Discussion.Topic+": "+(other.ResidentId==npcConversation.A?npcConversation.Discussion.FirstView:npcConversation.Discussion.SecondView)+". You can disagree naturally without inventing events.":"";
        if(continuing&&QueueNpcEvent(other,"A nearby bandmate addressed you. Respond to what they actually said; you may ask one relevant follow-up, or finish naturally."+personalView,speech,2,actor.ResidentId,heard.Depth+1))
        {npcConversation.Until=Time.unscaledTimeAsDouble+60;NpcListen(actor,other.ResidentId);NpcListen(other,actor.ResidentId);}
        else {NpcMemoryFor(id).Hear(actor.ResidentId,actor.Resident.Name,speech);EndNpcConversation();}
    }
}
