using System;
using System.Linq;
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
        Console.WriteLine("NPC social response checks PASS: "+checks);
    }
}
