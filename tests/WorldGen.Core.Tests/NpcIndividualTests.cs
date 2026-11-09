using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using WorldGen.Core;

static class NpcIndividualTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
        var c=new ForagerCulture{Seed=79,FoodSharing=.7,GuestAccess=.5,PrestigeCompetition=.5,ConflictAvoidance=.7};
        var person=new ForagerPersonSample{Id=17,AgeYears=30};var a=NpcPersonality.For(person,c);var b=NpcPersonality.For(person,c);
        Check(a.Describe()==b.Describe()&&a.Courage==b.Courage,"Personality regenerates deterministically without individual world ticks");
        var otherCulture=new ForagerCulture{Seed=93479,FoodSharing=.2,GuestAccess=.2,PrestigeCompetition=.9,ConflictAvoidance=.2};
        Check(NpcPersonality.For(person,otherCulture).Courage==a.Courage,"A person's basic courage does not reroll when their cultural surroundings change");
        var crowd=Enumerable.Range(1,100).Select(i=>NpcPersonality.For(new ForagerPersonSample{Id=i,AgeYears=30},c)).ToArray();
        Check(crowd.Select(p=>p.Interest).Distinct().Count()>=6&&crowd.Min(p=>p.Courage)<.2&&crowd.Max(p=>p.Courage)>.8,"People in one culture retain broad individual differences and interests");
        var mind=new NpcMemory{Person=17};var s=mind.Social;s.Advance(1);s.Fear=.8;s.Anger=.7;s.Grief=.9;
        s.Record(new NpcIncident{Kind=NpcIncidentKind.AnimalSeen,Animal=71,SolarDay=1,Detail="A wolf beside the stream.",Position=new V(1,0,0)});
        s.Record(new NpcIncident{Kind=NpcIncidentKind.AnimalSeen,Animal=71,SolarDay=1.01,Detail="A wolf moving away.",Position=new V(1,0,0)});
        Check(s.Incidents.Count==1&&s.Incidents[0].Detail.Contains("away"),"Repeated glimpses refresh an observation rather than append per-frame events");
        s.Tie(8,1).Grievance=.4;s.See(8,new V(1,0,0),1);
        var copy=mind.Copy();copy.Social.Incidents[0].Detail="changed";copy.Social.Relationships[0].Grievance=.1;copy.Social.KnownPeople[0].SeenDead=true;
        Check(s.Incidents[0].Detail!="changed"&&s.Relationships[0].Grievance==.4&&!s.KnownPeople[0].SeenDead,"Social memories, relationships and observed whereabouts deep-copy");
        s.Advance(1.1);Check(s.Fear<.2&&s.Grief>.8,"Acute fear subsides faster than grief");
        for(int i=0;i<80;i++){s.Record(new NpcIncident{Kind=NpcIncidentKind.Help,Subject=i+1,SolarDay=2+i,Detail="help"});s.Tie(i+100,2);s.See(i+100,new V(1,0,0),2+i);}
        Check(s.Incidents.Count==24&&s.Relationships.Count==48&&s.KnownPeople.Count==64,"Local records stay bounded rather than accumulating world-scale histories");
        var state=new HumanLocalState();state.Minds.Add(mind);state.Validate();var serializer=new DataContractSerializer(typeof(HumanLocalState));
        using(var stream=new MemoryStream()){serializer.WriteObject(stream,state);stream.Position=0;var loaded=(HumanLocalState)serializer.ReadObject(stream);loaded.Validate();Check(loaded.Minds[0].Social.Relationships.Count==48&&loaded.Minds[0].Social.Incidents.Count==24,"Social knowledge survives the actual save serializer");}
        var weather=new WeatherFrame(6,.8,5,0,.9,0,500,100,1000,4,4,7,0);
        Check(NpcLocalKnowledge.Daypart(7,12)=="morning"&&NpcLocalKnowledge.Daypart(0,12)=="night"&&NpcLocalKnowledge.Weather(weather).Contains("heavy rain"),"Daypart and weather descriptions reflect actual current conditions");
        var belief=new ForagerBelief{Name="River tradition",Animacy=.8,AncestorPresence=.8,SacredLandscape="river pools",SeasonalRite="remembering the dead",MortuaryPractice="burial marked by stones"};
        string beliefs=NpcLocalKnowledge.Beliefs(belief,Biome.TemperateForest,.8);
        Check(beliefs.Contains("river pools")&&beliefs.Contains("burial marked by stones")&&beliefs.Contains("seriously"),"Faith context includes actual local sacred places, rites, mortuary customs and personal devotion");
        Console.WriteLine("NPC individual checks PASS: "+checks);
    }
}
