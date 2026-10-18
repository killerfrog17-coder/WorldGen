using System.Reflection;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
public static class HistoryDomesticPoliticsTests
{
    static object Call(Prehistory e,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args)!;
    static void InQueue(Prehistory e,PrehistoryState s,Action action)
    {
        var f=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;var q=new SimulationEventQueue(s.Timeline.Events);f.SetValue(e,q);
        try{action();}finally{f.SetValue(e,null);}s.Timeline.Events=q.Ordered();
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(var values in new[]{new[]{1,2,3,4,5},new[]{0,1,0,0,0},new[]{int.MaxValue,int.MaxValue,int.MaxValue,1,1}})
        {var shares=HistoricalDomesticEffects.Normalize(values);check(shares.Sum()==1000&&shares.All(x=>x>=0),"Influence conserves exactly one thousand shares");}
        check(HistoricalDomesticEffects.Approach(200,800,0,30)==200,"No elapsed time creates no organizational development");
        check(HistoricalDomesticEffects.Approach(200,800,30,30)>200&&HistoricalDomesticEffects.Approach(200,800,30,30)<800,"Organizations and grievances evolve gradually");
        var groups=new[]{new HistoricalPowerGroup{Influence=600,Organization=900,Grievance=900,Governing=false},new HistoricalPowerGroup{Influence=400,Organization=600,Grievance=100,Governing=true}};
        check(HistoricalDomesticEffects.Opposition(groups)>0,"Organized excluded grievance creates pressure");groups[0].Governing=true;check(HistoricalDomesticEffects.Opposition(groups)==0,"Inclusion removes this opposition contribution");
        check(HistoricalDomesticEffects.ContestHazard(.9,100,900,100,false)==0,"No armed office seizure without actual military means");
        check(HistoricalDomesticEffects.ContestHazard(.9,800,900,100,true)==0,"Normal legitimate government is not a random coup lottery");
        check(HistoricalDomesticEffects.ContestHazard(.9,100,900,900,true)<HistoricalDomesticEffects.ContestHazard(.9,100,900,100,true),"Representation mitigates armed office contest risk");
        check(AtlasNavigation.Group("Domestic power groups")=="Power & relations"&&AtlasNavigation.Group("Institutional bargaining")=="Power & relations","Domestic politics has predictable Atlas navigation");
        var d=new HistoricalDomesticPolitics();d.Groups=d.Groups.With(6,500);var copy=d.Copy();copy.Groups=copy.Groups.With(6,900);check(d.Groups[6]==500,"Political publications share immutable pages safely");
        check(StateHash.Compute(d)!=StateHash.Compute(copy),"Domestic edits participate in canonical hashes");
        var roundtrip=WorldJson.Read<HistoricalDomesticPolitics>(WorldJson.Write(copy));check(StateHash.Compute(roundtrip)==StateHash.Compute(copy),"Political ledger JSON roundtrip is exact");
    }
    public static void World(string input,string output,Action<bool,string> check)
    {
        Directory.CreateDirectory(output);var w=WorldFile.Read(File.ReadAllBytes(input));var e=new Prehistory(w);var old=w.Prehistory;string oldHash=old.ComputeHash();
        var s=e.UpgradeRealmLifecycles(old);e.Validate(s);check(s.History.Aggregates.Empires.Connections.Realms.Version==37,"Upgrade activates domestic institutions");
        check(old.ComputeHash()==oldHash&&old.History.Aggregates.Empires.Connections.Realms.Domestic==null,"Old36 source and canonical bytes stay literal");
        check(ReferenceEquals(s,e.UpgradeRealmLifecycles(s)),"Upgrade is idempotent");
        check(SocietyView.Population(s)==SocietyView.Population(old)&&s.History.Polities.Zip(old.History.Polities).All(p=>p.First.Treasury==p.Second.Treasury&&p.First.Army==p.Second.Army&&p.First.Population==p.Second.Population),"Registration creates no money, armies or people");
        int id=s.History.Polities.First(p=>p.EndedYear<0&&s.History.Aggregates.Polities[p.Id].Cities.Count>0).Id;var original=e.DomesticProfile(s,id)!;
        check(original.Groups.Length==5&&original.Groups.Sum(g=>g.Influence)==1000&&original.Groups.Any(g=>g.Governing),"Persistent political groups reconcile and have a coalition");
        string before=s.ComputeHash();var article=HistoryAtlas.Read(s,"state",id,w,e);check(article.Sections.Any(p=>p.Title=="Domestic power groups"&&p.Links.Length==5)&&article.Sections.Any(p=>p.Title=="Institutional bargaining"),"Real dossier exposes power and dated bargaining");
        var detached=e.DomesticProfile(s,id)!;detached.Groups[0].Grievance=1000;check(s.ComputeHash()==before&&e.DomesticProfile(s,id)!.Groups[0].Grievance==original.Groups[0].Grievance,"Queries detach and Atlas is read-only");
        var test=s.Copy();var p=test.History.Polities[id];var domestic=test.History.Aggregates.Empires.Connections.Realms.Domestic;int at=id*12;
        p.Treasury=0;Call(e,"BargainDomesticCoalition",test,id,e.DomesticProfile(test,id)!);check(p.Treasury==0&&e.DomesticProfile(test,id)!.LastDecision==3,"Unfunded bargaining grants no free reform");
        p.Treasury=1000000;long cash=p.Treasury;Call(e,"BargainDomesticCoalition",test,id,e.DomesticProfile(test,id)!);check(p.Treasury<cash&&cash-p.Treasury==e.DomesticProfile(test,id)!.NegotiationCost,"Actual negotiation debits include rejected proposals");
        // Controlled extreme causal fixture, separate from natural replay.
        int officer=-1;InQueue(e,test,()=>officer=(int)Call(e,"CreateNotable",test,p.CultureId,test.Society.Settlements[p.CapitalId].Cell,id,HistoricalPersonRoles.Commander,-1,-1,-1,null!));
        domestic.Groups=domestic.Groups.WithBlock((id*5+2)*12+1,new[]{650,950,950,officer,p.CapitalId});domestic.Governments=domestic.Governments.With(at+3,1).With(at+4,950);
        int[] fixtureShares={75,100,650,75,100};for(int k=0;k<5;k++)domestic.Groups=domestic.Groups.With((id*5+k)*12+1,fixtureShares[k]);
        p.Legitimacy=100;p.Exhaustion=900;p.Army=1000;test.History.Aggregates.Communities.Governments[id].RepresentationMilli=150;
        int previous=test.History.Dynamics.States[id].LeaderId,previousPerson=Prehistory.RulerPersonId(test,previous),states=test.History.Polities.Length;long population=SocietyView.Population(test);cash=p.Treasury;
        p.Treasury=0;Call(e,"ConsiderDomesticOfficeContest",test,id,100000);check(test.History.Dynamics.States[id].LeaderId==previous,"Insolvent officers cannot finance an office takeover");p.Treasury=cash;
        p.Army=0;Call(e,"ConsiderDomesticOfficeContest",test,id,100000);check(test.History.Dynamics.States[id].LeaderId==previous,"No army means no armed takeover");p.Army=1000;
        p.Legitimacy=900;Call(e,"ConsiderDomesticOfficeContest",test,id,100000);check(test.History.Dynamics.States[id].LeaderId==previous,"High legitimacy defeats this armed contest even in the extreme fixture");p.Legitimacy=100;
        Call(e,"ConsiderDomesticOfficeContest",test,id,100000);
        check(test.History.Dynamics.States[id].LeaderId!=previous&&test.History.Dynamics.Leaders[previous].EndedYear==test.Years,"Organized armed contest replaces the office holder");
        check(test.History.Polities.Length==states&&SocietyView.Population(test)==population&&p.Treasury==cash-200&&p.Army==1000,"Contest conserves country, people and army while paying costs");
        check(previousPerson<0||test.History.Notables.People[previousPerson].DeathYear<0,"Deposition is not death");
        check(Prehistory.RulerPersonId(test,test.History.Dynamics.States[id].LeaderId)==officer,"Existing commander becomes ruler without an invented duplicate");
        check(test.History.Aggregates.Empires.Connections.Realms.Governments[id].FoundedYear==s.History.Aggregates.Empires.Connections.Realms.Governments[id].FoundedYear,"Coup does not refound country");
        Call(e,"ValidateDomesticPolitics",test);check(true,"Actual office-transfer record passes political integrity validation");
        int incumbent=test.History.Dynamics.States[id].LeaderId;Call(e,"ConsiderDomesticOfficeContest",test,id,100000);check(test.History.Dynamics.States[id].LeaderId==incumbent,"Post-contest cooldown prevents repeated immediate transfers");
        var end=e.AdvanceYears(s,60);e.Validate(end);var split=e.AdvanceYears(e.AdvanceYears(s,23),37);check(end.Hash==split.Hash,"Natural60-year politics is partition invariant");
        w.Prehistory=s;var cold=WorldFile.Read(WorldFile.Write(w));check(cold.Prehistory.Hash==s.Hash,"Binary save retains political ledger");
        var coldEnd=new Prehistory(cold).AdvanceYears(cold.Prehistory,60);check(end.Hash==coldEnd.Hash,"Cold replay preserves political outcomes");
        check(s.ComputeHash()==before,"Natural continuation does not mutate publication");
        var ledger=end.History.Aggregates.Empires.Connections.Realms.Domestic;var json=WorldJson.Read<HistoricalDomesticPolitics>(WorldJson.Write(ledger));check(StateHash.Compute(json)==StateHash.Compute(ledger),"Actual sparse JSON records roundtrip");
        var profiles=end.History.Polities.Where(p=>p.EndedYear<0).Select(p=>e.DomesticProfile(end,p.Id)).Where(v=>v!=null).ToArray();
        check(profiles.All(v=>v!.Groups.Sum(g=>g.Influence)==1000)&&profiles.Any(v=>v!.LastYear>s.Years),"Natural groups evolve and reconcile");
        w.Prehistory=end;File.WriteAllBytes(Path.Combine(output,"review.world"),WorldFile.Write(w));File.WriteAllText(Path.Combine(output,"review.hash"),end.Hash);
        File.WriteAllText(Path.Combine(output,"state.json"),WorldJson.Write(HistoryAtlas.Read(end,"state",id,w,e)));
        File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Old=oldHash,Upgraded=before,End=end.Hash,States=profiles.Length,Groups=profiles.Length*5,Decisions=profiles.Count(v=>v!.LastDecision>0),Contests=profiles.Sum(v=>v!.Contests)},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Natural60-year result "+end.Hash);
    }
}
