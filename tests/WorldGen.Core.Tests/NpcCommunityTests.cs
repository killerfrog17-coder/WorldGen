using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;

static class NpcCommunityTests
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string why){if(!ok)throw new Exception(why);n++;}
        var band=new ForagerBandSample{Id=7,Members=Enumerable.Range(1,5).Select(i=>new ForagerPersonSample{Id=i,AgeYears=30,Name="Resident"+i,Skills=new double[8]}).ToArray()};
        var culture=new ForagerCulture{Seed=12,GuestAccess=.8,FoodSharing=.8};var memories=new Dictionary<long,NpcSocialState>();foreach(var p in band.Members)memories[p.Id]=new NpcSocialState();
        NpcSocialState Mind(long id)=>memories.TryGetValue(id,out var state)?state:null;
        Check(NpcCommunityRules.Welcome(culture,band,2,1,9,Mind)==NpcWelcome.Guest,"A welcoming household host can offer a visit without granting membership unilaterally");
        Check(NpcCommunityRules.Welcome(culture,band,1,1,9,Mind)==NpcWelcome.Guest,"A leader's welcome is not automatic full membership without social support");
        memories[1].Tie(9,0).Trust=.5;memories[2].Tie(9,0).Trust=.5;
        Check(NpcCommunityRules.Welcome(culture,band,1,1,9,Mind)==NpcWelcome.Member,"An actual leader and multiple established relationships can admit a member");
        memories[1].Tie(9,0).Grievance=.8;Check(NpcCommunityRules.Welcome(culture,band,1,1,9,Mind)==NpcWelcome.None,"An unresolved serious grievance blocks a model-generated welcome");
        var local=new NpcCommunityState();local.Joined.Add(new ForagerPersonSample{Id=9,Name="Visitor",AgeYears=30,Skills=new double[8]});var joined=NpcCommunityRules.WithJoined(band,local);
        Check(joined.Members.Length==6&&band.Members.Length==5&&NpcCommunityRules.WithJoined(joined,local).Members.Length==6,"Local membership adds the same real person once without changing the broad population ledger");
        local.LeaderCandidate=9;Check(!NpcCommunityRules.Elect(local,joined,culture,9),"A declaration alone cannot confer leadership");local.LeaderSupport.AddRange(new long[]{1,2});Check(!NpcCommunityRules.Elect(local,joined,culture,9),"Insufficient endorsements do not confer leadership");local.LeaderSupport.Add(3);
        Check(NpcCommunityRules.Elect(local,joined,culture,9)&&local.Coordinator==9,"Sufficient actual adult support establishes local leadership");
        Check(!NpcCommunityRules.VoteCustom(local,joined,1,9,NpcCustom.ShareMore,1)&&!NpcCommunityRules.VoteCustom(local,joined,2,9,NpcCustom.ShareMore,1),"A chief's proposed custom still requires support");
        Check(NpcCommunityRules.VoteCustom(local,joined,3,9,NpcCustom.ShareMore,1)&&local.Sharing==.85,"A supported food-sharing custom affects only this community");
        var changed=NpcCommunityRules.Culture(culture,local);Check(changed.FoodSharing==.85&&culture.FoodSharing==.8,"Local norms preserve the regional parent culture");
        var copy=local.Copy();copy.Joined[0].Name="Changed";Check(local.Joined[0].Name=="Visitor","Local membership and decisions deep-copy");local.Validate();
        Check(NpcCommunityRules.Supports(NpcCustom.CautiousVisitors,new NpcPersonality{Vigilance=.9,Sociability=.1},.3)&&!NpcCommunityRules.Supports(NpcCustom.WelcomeVisitors,new NpcPersonality{Vigilance=.9,Sociability=.1},.3),"Individuals can oppose a chief's requested change according to their actual dispositions");
        Console.WriteLine("NPC community checks PASS: "+n);
    }
}
