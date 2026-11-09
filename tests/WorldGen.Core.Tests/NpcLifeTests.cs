using System;
using System.Linq;
using WorldGen.Core;

static class NpcLifeTests
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string why){if(!ok)throw new Exception(why);n++;}
        string json="{\"speech\":\"Hi \\"+"\"friend\\\" \\uD83D\\uDE00\\nthere\",\"thought\":\"private\",\"action\":\"speak\"}";
        var stream=new NpcSpeechStream();foreach(char c in json)stream.Append(c.ToString());
        Check(stream.Speech=="Hi \"friend\" 😀 there","Fragmented speech decodes escaped quotes, unicode pairs and newlines");
        Check(!stream.Speech.Contains("private")&&stream.Json==json,"Only speech is exposed and the original decision remains intact");
        var nested=new NpcSpeechStream();nested.Append("{\"thought\":\"speech: secret\",\"other\":{\"speech\":\"hidden\"},\"speech\":\"hello\"}");Check(nested.Speech=="hello","Nested fields and thought text cannot leak into dialogue");
        var pace=new NpcSpeechPace{Available="hello😀world"};pace.Advance(.1,20);Check(pace.Visible=="he","Dialogue appears progressively at speaking speed");
        for(int i=0;i<100;i++)pace.Advance(.05);Check(pace.Visible==pace.Available,"Unicode pairs do not stall paced dialogue");
        var clock=new LocalWaitClock();Check(clock.Advance(true,true,1)==0,"Paused waiting advances no time");for(int i=0;i<100;i++)clock.Feedback(1);Check(clock.Steps==32&&clock.Advance(true,false,.016)==1.6,"Idle processing headroom ramps within bounded physics steps");clock.Feedback(20);Check(clock.Steps==16,"An expensive frame immediately reduces waiting work");clock.Reset();Check(clock.Steps==1&&clock.Advance(false,false,.02)==.02,"Normal time is restored independently of waiting rate");Check(LocalWaitClock.Elapsed(1.5)=="1 d 12 h","Waiting reports actual elapsed local time");
        var parent=new ForagerPersonSample{Id=1,Name="A",Mother=10,Father=11,AgeYears=35,Skills=new double[8]};
        var aunt=new ForagerPersonSample{Id=2,Name="B",Mother=12,Father=13,Female=true,AgeYears=42,Skills=Enumerable.Repeat(1d,8).ToArray()};
        var cousin=new ForagerPersonSample{Id=3,Name="C",Mother=2,AgeYears=20,Skills=new double[8]};
        var band=new ForagerBandSample{Aggregation=1,Members=new[]{parent,aunt,cousin},FamilyRecords=new[]{new ForagerPersonRecord{Id=10,Mother=12,Father=13},new ForagerPersonRecord{Id=11}}};
        Check(ForagerSocialLife.Relationship(parent,aunt,band)=="your aunt","Extended kinship follows real shared ancestors");
        Check(ForagerSocialLife.Relationship(parent,cousin,band)=="your cousin","Cousins are resolved through parents and grandparents");
        Check(ForagerSocialLife.Relationship(parent,new ForagerPersonSample{Id=99},band)=="a fellow band member","Unknown ancestor IDs never fabricate kinship");
        var egalitarian=new ForagerCulture{Seed=42,Residence=ForagerResidence.ResidentialMobility,FoodSharing=.9,ConflictAvoidance=.8,GuestAccess=.6};
        var ranked=new ForagerCulture{Seed=42,Residence=ForagerResidence.AquaticResidence,FoodSharing=.3,PrestigeCompetition=.9,KinObligation=.9};
        var a=ForagerSocialLife.Create(egalitarian,band);var b=ForagerSocialLife.Create(ranked,band);
        Check(!a.Headperson&&b.Headperson&&a.DecisionMaking!=b.DecisionMaking,"Culture and residence produce distinct decision traditions without universal chiefs");
        Check(a.Guide==aunt.Id&&a.Teacher==aunt.Id,"Leadership can follow competence rather than a male-only rule");
        Check(ForagerSocialLife.Create(ranked,band).Coordinator==b.Coordinator,"Local social roles regenerate deterministically");
        Check(ForagerSocialLife.Affinity(egalitarian,parent,cousin,band)==ForagerSocialLife.Affinity(egalitarian,cousin,parent,band),"Non-family affinity is symmetric and deterministic");
        var pcm=NpcMumble.Build();Check(pcm.Length==8&&pcm.All(c=>c.Length>2000&&c.Length<5000&&c.All(x=>float.IsFinite(x)&&Math.Abs(x)<=.381)),"Shared prototype voice buffers are finite, short and peak bounded");
        Console.WriteLine("NPC life checks PASS: "+n);
    }
}
