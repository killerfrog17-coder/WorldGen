using System;
using System.Linq;
using WorldGen.Core;

static class HumanDailyLifeTests
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string text){if(!ok)throw new Exception(text);n++;}
        var early=HumanLocalLife.SleepTiming(12,35,0,.5,.5);var late=HumanLocalLife.SleepTiming(12,35,1,.5,.5);
        Check(late.wake-early.wake>2,"Stable chronotypes produce meaningfully different wake times");
        Check(HumanLocalLife.SleepTiming(12,35,.5,.5,.1).wake!=HumanLocalLife.SleepTiming(12,35,.5,.5,.9).wake,"A person does not wake at precisely the same minute every day");
        var band=new ForagerBandSample{Id=88,Members=Enumerable.Range(1,14).Select(i=>new ForagerPersonSample{Id=i,AgeYears=i<5?3+i:22+i,Name="Person"+i}).ToArray()};
        var group=new HumanGroupSituation{Band=band,DaylightHours=12,LocalHour=1,SecurityRisk=0};var c=new ForagerCulture{Seed=21};
        Check(band.Members.All(p=>HumanDailyLife.NightRole(group,c,p,band,.666,true)!="NightWatch"),"A low-risk camp is not assigned universal formal sentries");
        int guardCases=0;
        for(int seed=0;seed<40;seed++)
        {c.Seed=seed;group.SecurityRisk=.9;for(int hour=19;hour<29;hour++){group.LocalHour=hour%24;var roles=band.Members.Select(p=>(person:p,role:HumanDailyLife.NightRole(group,c,p,band,(hour-9)/24d,true))).ToArray();Check(roles.Count(p=>p.role=="NightWatch")<=1&&roles.All(p=>p.role!="NightWatch"||p.person.AgeYears>=18),"Guard shifts select a single actual eligible adult");guardCases+=roles.Count(p=>p.role=="NightWatch");}}
        Check(guardCases>0,"Some higher-risk cultural traditions assign actual rotating night-watch roles");
        var child=band.Members[0];var clock=new HumanLifeClock(0,.5);var play=HumanDailyLife.Play(group,band,child,clock,42);
        Check(play.Activity==HumanActivity.Play&&play.Partner>0&&band.Members.Any(p=>p.Id==play.Partner&&p.Id!=child.Id&&p.AgeYears<14),"A child has a real mixed-age playmate rather than an arbitrary adult chore partner");
        Check(play.Role==HumanDailyLife.Play(group,band,child,clock,42).Role&&play.Partner==HumanDailyLife.Play(group,band,child,clock,42).Partner,"The same place and time regenerate the same play session");
        var games=Enumerable.Range(0,48).Select(i=>HumanDailyLife.Play(group,band,child,new HumanLifeClock(0,i/48d),42).Role).Distinct().Count();Check(games==3,"Play varies between chasing, hiding/seeking and make-believe");
        Console.WriteLine("Human daily life checks PASS: "+n);
    }
}
