using System.Reflection;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
public static class NotablePeopleTests
{
    static object Call(Prehistory e,string name,params object[] a)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,a)!;
    static void Save(HistoricalNotableHistory r,HistoricalNotablePerson p)=>typeof(HistoricalNotableHistory).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(r,new object[]{p,false});
    static void InQueue(Prehistory e,PrehistoryState s,Action action)
    {
        var f=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;var q=new SimulationEventQueue(s.Timeline.Events);f.SetValue(e,q);
        try{action();}finally{f.SetValue(e,null);}s.Timeline.Events=q.Ordered();
    }
    public static void Run(Action<bool,string> check)
    {
        var c=new HistoricalPersonCollection();for(int i=0;i<300;i++)c.Add(new HistoricalNotablePerson{Id=i,Name="Person "+i,Skills=new[]{1,2,3,4,5,6}});
        var copy=c.Copy();copy.Add(new HistoricalNotablePerson{Id=300,Name="Later"});var row=c[0];row.Name="Tampered";row.Skills[0]=999;
        check(c.Count==300&&copy.Count==301&&c[0].Name=="Person 0"&&copy[0].Skills[0]==1,"Paged immutable biographies detach across public reads, array edits and appends.");
        var json=WorldJson.Read<HistoricalPersonCollection>(WorldJson.Write(c));check(json.Count==300&&json[255].Name==c[255].Name,"New person pages retain the shared collection JSON contract.");
        check(AtlasNavigation.Group("Family and household")=="People & place"&&AtlasNavigation.Group("Recorded achievements")=="History","Biographical sections have predictable atlas navigation.");
        var dates=Enumerable.Range(0,10000).Select(id=>HistoricalNotableHistory.DeathOffset(id,71673)).ToArray();
        check(dates.Distinct().Count()==dates.Length&&dates.Min()>0&&dates.Max()<SimulationTime.Year,"Individual mortality dates are distinct inside a year instead of a January1 event pile-up.");
    }
    public static void World(string input,string output,Action<bool,string> check)
    {
        Directory.CreateDirectory(output);var world=WorldFile.Read(File.ReadAllBytes(input));var e=new Prehistory(world);var old=world.Prehistory;string hash=old.ComputeHash();
        var s=e.UpgradeRealmLifecycles(old);e.Validate(s);var r=s.History.Notables;
        check(r!=null&&s.History.Aggregates.Empires.Connections.Realms.Version>=35,"Explicit upgrade activates the shared notable-person foundation.");
        check(old.ComputeHash()==hash&&old.History.Notables==null,"Old rules and old checkpoint remain literal.");
        check(r.People.Count==s.History.Polities.Count(p=>p.EndedYear<0)&&r.People.All(p=>p.Parent1<0&&p.Children.Length==0&&p.Deeds.Length==0),"Upgrade records existing office-holders, not invented past relatives or accomplishments.");
        check(s.History.Aggregates.TotalPopulation==old.History.Aggregates.TotalPopulation&&s.History.Aggregates.Cells.Fingerprint()==old.History.Aggregates.Cells.Fingerprint(),"Prospective people create no residents, territory or demographic accounts.");
        check(ReferenceEquals(s,e.UpgradeRealmLifecycles(s)),"The complete people upgrade is idempotent.");
        var fresh=e.Start(e.Prepare());e.Validate(fresh);check(fresh.History.Notables!=null&&fresh.History.Notables.People.Count==0,"Fresh prehistory has no fabricated royal court or great people.");
        var probe=s.Copy();var a=probe.History.Aggregates;int state=probe.History.Polities.First(p=>p.EndedYear<0).Id,city=probe.History.Polities[state].CapitalId,cell=probe.Society.Settlements[city].Cell,culture=probe.Society.Settlements[city].CultureId;
        a.Cities[city].Supported=true;int researcher=-1;
        InQueue(e,probe,()=>Call(e,"NotableResearch",probe,culture,cell,6,100000L,false,0));researcher=probe.History.Notables.Towns[city*8];
        check(researcher>=0&&probe.History.Notables.People[researcher].ResearchWork==100000&&probe.History.Notables.People[researcher].Deeds.Length==0,"Actual partial work retains a researcher and effort, not a fictional discovery.");
        string tech=StateHash.Compute(probe.History.Cultures[culture].Technical);int deeds=probe.History.Notables.People[researcher].Deeds.Length;
        InQueue(e,probe,()=>Call(e,"NotableResearch",probe,culture,cell,6,0L,true,999));
        check(probe.History.Notables.People[researcher].Deeds.Length==deeds&&StateHash.Compute(probe.History.Cultures[culture].Technical)==tech,"An unearned completion cannot invent either an achievement or technical progress.");
        probe.History.Cultures[culture].Technical.Levels[6]=1;probe.History.Cultures[culture].Technical.LevelYear[6]=probe.Years;
        InQueue(e,probe,()=>Call(e,"NotableResearch",probe,culture,cell,6,0L,true,1));
        check(probe.History.Notables.People[researcher].Deeds.Any(d=>d.Technology==6&&d.Level==1&&d.Event>=0),"A completed local research level credits its actual participant and dated source event.");
        var doc=HistoryAtlas.Read(probe,"notable",researcher,world);string read=probe.ComputeHash();HistoryAtlas.Directory(probe,"notable");
        check(doc.Sections.Any(x=>x.Title=="Recorded achievements")&&doc.Sections.Any(x=>x.Title=="Abilities and interests")&&probe.ComputeHash()==read,"Biographies, traits, deeds and directory remain read-only.");
        var command=s.Copy();var cr=command.History.Notables;int first=command.History.Dynamics.Campaigns.Length,other=command.History.Polities.First(p=>p.Id!=state).Id;
        command.History.Dynamics.Campaigns.Add(new HistoricalCampaign{Id=first,A=state,B=other,AttackerId=state,StartedYear=command.Years,EndedYear=-1,Name="Controlled supplied front"});
        command.History.Dynamics.Campaigns.Add(new HistoricalCampaign{Id=first+1,A=state,B=other,AttackerId=state,StartedYear=command.Years,EndedYear=-1,Name="Controlled second front"});
        int count=cr.People.Count;double noPower=(double)Call(e,"NotableCommandPower",command,state,first,0.0);
        check(noPower==0&&cr.People.Count==count,"An unsupplied zero-power expedition creates neither command strength nor an officer.");
        double power=0;InQueue(e,command,()=>power=(double)Call(e,"NotableCommandPower",command,state,first,100.0));int officer=cr.Commands[first*2];
        double expected=100*(.4+cr.People[officer].Skills[0]/1000.0)/(.4+command.History.Dynamics.Leaders[command.History.Dynamics.States[state].LeaderId].Military/1000.0);
        check(officer>=0&&Math.Abs(power-expected)<.00001,"An assigned commander's own aptitude replaces, rather than stacks on, the generic ruler term.");
        InQueue(e,command,()=>Call(e,"NotableCommandPower",command,state,first+1,100.0));
        check(cr.Commands[(first+1)*2]!=officer,"One officer is not simultaneously assigned to two active fronts.");
        Call(e,"NotableBattle",command,state,first,cell,true);
        check(cr.People[officer].Deeds.Any(d=>d.Campaign==first)&&cr.People[officer].Renown>0,"A real victory callback credits the assigned officer and actual campaign, not an unrelated ruler.");
        var family=s.Copy();var fr=family.History.Notables;var h=family.History;int leader=h.Dynamics.States[state].LeaderId,person=Prehistory.RulerPersonId(family,leader);
        a=family.History.Aggregates;a.Communities.Governments[state].RepresentationMilli=100;a.Communities.Governments[state].CentralizationMilli=500;
        int child=-1;InQueue(e,family,()=>{Call(e,"RegisterNotableRuler",family,state,h.Dynamics.Leaders[leader],false);child=(int)Call(e,"CreateNotable",family,h.Polities[state].CultureId,cell,state,HistoricalPersonRoles.RoyalFamily,family.Years,person,-1,null!);});
        var parent=fr.People[person];parent.Children=new[]{child};Save(fr,parent);fr.Courts=fr.Courts.With(state*8+2,child);
        var incoming=new HistoricalLeader{Id=h.Dynamics.Leaders.Length,Name="Unrelated candidate"};Call(e,"PrepareNotableSuccession",family,state,incoming);
        check(incoming.Name==fr.People[child].Name&&fr.Leaders[incoming.Id]==child&&incoming.Selection.Contains("regency"),"A recorded minor heir can inherit through a regency, not be replaced with a fabricated adult descendant.");
        check(parent.House==fr.People[child].House&&fr.People[child].Parent1==person&&!fr.People[child].BirthEstimated,"Dynastic descent and exact prospective birth are distinct from estimated adult origins.");
        // End a term, not a life: office replacement must retain the former person.
        family= s.Copy();fr=family.History.Notables;h=family.History;int previous=Prehistory.RulerPersonId(family,h.Dynamics.States[state].LeaderId);
        InQueue(e,family,()=>Call(e,"AppointAggregateLeader",family,state));
        check(fr.People[previous].DeathYear<0&&h.Dynamics.Leaders[s.History.Dynamics.States[state].LeaderId].EndedYear==family.Years,"An ordinary end of office is not silently converted into a death.");
        var full=e.AdvanceYears(s,80);var split=e.AdvanceYears(e.AdvanceYears(s,29),51);e.Validate(full);
        check(full.Hash==split.Hash,"Person deaths, births, careers, commissions and political events replay exactly across29+51years.");
        check(full.History.Notables.People.Any(p=>p.DeathYear>=0)&&full.History.Notables.People.Count>r.People.Count,"Natural continuation exercises actual lifespan events and recruitment, not only dormant metadata.");
        world.Prehistory=full;var loaded=WorldFile.Read(WorldFile.Write(world));var cold=new Prehistory(loaded);cold.Validate(loaded.Prehistory);
        check(cold.AdvanceYears(loaded.Prehistory,12).Hash==e.AdvanceYears(full,12).Hash,"New person pages, lifetime clocks and relationships survive actual binary cold continuation.");
        var json=WorldJson.Read<HistoricalNotableHistory>(WorldJson.Write(full.History.Notables));check(StateHash.Compute(json)==StateHash.Compute(full.History.Notables),"Full people ledger and digest roots roundtrip shared JSON.");
        File.WriteAllBytes(Path.Combine(output,"review.world"),WorldFile.Write(world));File.WriteAllText(Path.Combine(output,"expected12.hash"),e.AdvanceYears(full,12).Hash);File.WriteAllText(Path.Combine(output,"legacy12.hash"),e.AdvanceYears(old,12).Hash);
        var people=full.History.Notables.People.ToArray();var focus=people.OrderByDescending(p=>p.Deeds.Length).ThenBy(p=>p.Id).First();
        File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Hash=full.Hash,People=people.Length,Living=people.Count(p=>p.DeathYear<0),Children=people.Count(p=>p.Parent1>=0),Deeds=people.Sum(p=>p.Deeds.Length),Research=people.Count(p=>p.ResearchWork>0),Focus=new{Id=focus.Id,focus.Name,focus.Cell},Family=people.Where(p=>p.Children.Length>0).Select(p=>new{Id=p.Id,p.Name,p.Cell}).FirstOrDefault()},new System.Text.Json.JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        check(old.ComputeHash()==hash,"Tests and new-world continuations do not mutate the legacy input.");
    }
}
