using System;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using WorldGen.Core;

static class NpcSocialRulesTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;}
        Check(NpcSocialRules.Danger(9,100,.9,true,4,1,3,true,true,true)==NpcDangerResponse.Escape,"A child is not turned into a combatant by nearby allies");
        Check(NpcSocialRules.Danger(32,100,.6,true,3,1,5,true,true,true)==NpcDangerResponse.Defend,"An armed adult with support can defend against an assault");
        Check(NpcSocialRules.Danger(32,100,.6,true,2,1,8,false,true,true)==NpcDangerResponse.Protect,"An armed adult can protect another person");
        Check(NpcSocialRules.Danger(32,100,.6,false,0,1,40,false,false,false)==NpcDangerResponse.Watch,"A distant noticed threat is not an automatic sprint");
        Check(NpcSocialRules.Danger(32,15,.9,true,3,1,4,true,true,true)==NpcDangerResponse.Escape,"A severely wounded defender seeks safety");
        var observer=new NpcSocialState();var uninformed=new NpcSocialState();
        NpcSocialRules.Assault(observer,3,4,9,.9,1,new V(1,0,0),"Saw an attack on a sister");observer.Validate();
        Check(observer.Tie(9,1).Trust<0&&observer.Incidents.Single().Subject==4&&uninformed.Incidents.Count==0,"Evidence changes only the observer's directed relationship");
        Check(NpcSocialRules.DiscoverDeath(observer,4,1,2,new V(1,0,0),"Sister")&&observer.Grief>.8,"Personally discovering a close relative causes grief");
        Check(!NpcSocialRules.DiscoverDeath(observer,4,1,3,new V(1,0,0),"Sister")&&observer.Incidents.Count==2,"Seeing the same corpse again does not repeatedly trigger death events");
        var c=new ForagerCulture{Seed=37};var self=new ForagerPersonSample{Id=3,AgeYears=30};var other=new ForagerPersonSample{Id=9,AgeYears=30};var band=new ForagerBandSample{Members=new[]{self,other}};
        Check(NpcSocialRules.Affinity(c,self,other,band,observer)<NpcSocialRules.Affinity(c,self,other,band,uninformed),"Witnessed harmful behavior affects later social choice");
        var gifts=new NpcSocialState();NpcSocialRules.Gift(gifts,9,3,1,new V(1,0,0),"a useful cutting tool",false,true,.9);double earned=gifts.Tie(9,1).Trust;
        Check(earned>0&&gifts.Tie(9,1).Respect>0,"Actually receiving a useful personal gift builds directed trust and respect");
        NpcSocialRules.Gift(gifts,9,3,1.01,new V(1,0,0),"a second tool",false,true,.9);double twice=gifts.Tie(9,1).Trust;
        NpcSocialRules.Gift(gifts,9,3,1.02,new V(1,0,0),"another pebble",false,true,.9);Check(gifts.Tie(9,1).Trust==twice&&twice>earned,"Rapid repeated gifts do not farm unlimited trust even when the incident journal coalesces entries");
        var wronged=new NpcSocialState();wronged.Tie(9,1).Trust=-.7;wronged.Tie(9,1).Grievance=.8;NpcSocialRules.Gift(wronged,9,3,1,new V(1,0,0),"a stolen spear",true,false,1);
        Check(wronged.Tie(9,1).Trust<0&&wronged.Tie(9,1).Grievance<.8&&wronged.Tie(9,1).Respect==0,"Returning stolen property partly repairs the dispute and earns no gift prestige");
        var warm=new NpcPersonality{Kindness=.95,Sociability=.8,Vigilance=.2,Ambition=.1,Devotion=.3};var guarded=new NpcPersonality{Kindness=.05,Sociability=.3,Vigilance=.8,Ambition=.7,Devotion=.6};
        var difference=NpcDiscussion.For(warm,guarded,c);Check(difference.Disagreement&&difference.FirstView!=difference.SecondView&&difference.Topic.Contains("food"),"Different personal convictions produce a concrete disagreement inside one culture");
        NpcSocialRules.SharedConversation(gifts,7,2,true,difference.Topic);Check(gifts.Tie(7,2).Grievance>0&&gifts.Incidents.Any(e=>e.Kind==NpcIncidentKind.Argument),"Actually delivered disagreement affects the named person's relationship");
        double fondness=gifts.Tie(8,2).Affection;NpcSocialRules.SharedConversation(gifts,8,2,false,"work");Check(gifts.Tie(8,2).Affection>fondness,"Friendly shared time develops personal affection");
        var witness=new NpcSocialState();NpcSocialRules.Assault(witness,1,2,8,.8,3,new V(1,0,0),"You saw an assault.");
        var listener=new NpcSocialState();listener.KnownPeople.Add(new NpcKnownPerson{Person=2,LastSeenSolarDay=1,LastPosition=new V(0,1,0)});
        Check(NpcSocialExchange.ShareOne(witness,listener,1,"Ari",3.2),"Social facts spread without generating a transcript");
        var rumor=listener.Incidents.Single();Check(!rumor.FirstHand&&rumor.Reporter==1&&rumor.OriginPerson==1&&rumor.SolarDay==3&&rumor.Confidence==.75&&rumor.Detail.Contains("reported"),"Hearsay preserves provenance, date and reduced confidence");
        double trust=listener.Tie(8,3.2).Trust;Check(!NpcSocialExchange.ShareOne(witness,listener,1,"Ari",3.3)&&listener.Tie(8,3.3).Trust==trust,"Repeating gossip does not multiply relationship penalties");
        var third=new NpcSocialState();Check(NpcSocialExchange.ShareOne(listener,third,4,"Bela",3.4)&&third.Incidents.Single().Confidence<rumor.Confidence,"Forwarded reports lose confidence and retain their origin");
        Check(!NpcSocialExchange.ShareOne(third,witness,5,"Cai",3.5)&&!NpcSocialExchange.ShareOne(witness,witness,1,"Ari",3.5),"Gossip cannot loop back into an original eyewitness or self");
        Check(!NpcSocialExchange.ShareOne(witness,new NpcSocialState(),1,"Ari",6),"Old incidents are not spread indefinitely");
        NpcSocialRules.DiscoverDeath(witness,2,1,3.6,new V(1,0,0),"Relative");
        Check(NpcSocialExchange.ShareOne(witness,listener,1,"Ari",3.7,.8)&&listener.Grief>0&&listener.KnownPeople.Single().SeenDead,"A reliable report can inform a relative of a death");
        Check(listener.KnownPeople.Single().LastSeenSolarDay==1&&listener.KnownPeople.Single().LastPosition.Y==1,"A report does not fabricate a new personal sighting");
        var privateOnly=new NpcSocialState();privateOnly.Record(new NpcIncident{Kind=NpcIncidentKind.Argument,Actor=1,Subject=2,SolarDay=3,Position=new V(1,0,0),Detail="private words"});Check(!NpcSocialExchange.ShareOne(privateOnly,new NpcSocialState(),1,"Ari",3.1),"Routine private conversation content is not broadcast as gossip");
        listener.NextSocialSolarDay=4;var copied=listener.Copy();copied.Incidents[0].Detail="changed";Check(listener.Incidents[0].Detail!="changed"&&copied.NextSocialSolarDay==4,"Social exchange state copies independently");
        var serializer=new DataContractSerializer(typeof(NpcSocialState));using(var stream=new MemoryStream())
        {serializer.WriteObject(stream,listener);stream.Position=0;var loaded=(NpcSocialState)serializer.ReadObject(stream);loaded.Validate();Check(loaded.NextSocialSolarDay==4&&loaded.Incidents.Any(e=>e.OriginPerson==1&&!e.FirstHand),"Cooldowns and hearsay survive save/reload");}
        var upgrade=new NpcSocialState();NpcSocialExchange.ShareOne(witness,upgrade,1,"Ari",3.7);
        var reported=upgrade.Incidents.Single().Copy();var personal=reported.Copy();personal.FirstHand=true;personal.Confidence=1;personal.Reporter=personal.OriginPerson=personal.OriginSequence=0;personal.SolarDay+=.01;personal.Detail="Personally witnessed this event.";upgrade.Record(personal);
        Check(upgrade.Incidents.Count==1&&upgrade.Incidents[0].FirstHand&&upgrade.Incidents[0].Reporter==0&&upgrade.Incidents[0].Confidence==1,"Personal observation upgrades existing hearsay without duplicating the event");
        upgrade.Record(reported);Check(upgrade.Incidents[0].FirstHand&&upgrade.Incidents[0].Confidence==1,"A later report cannot downgrade an eyewitness memory");
        Console.WriteLine("NPC social response checks PASS: "+checks);
    }
}
