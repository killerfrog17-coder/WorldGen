using System.Reflection;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HistoryReligiousDoctrineTests
{
    static object Call(Prehistory e,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args)!;
    public static void Run(Action<bool,string> check)
    {
        HistoricalReligiousDoctrine Root(int id)=>ReligiousDoctrines.Root(id,k=>((id*313+k*137+17)%997)/997.0);
        for(int id=0;id<80;id++)
        {
            var p=Root(id);check(p.Axes().All(v=>v>=0&&v<=1000)&&p.FestivalDay<=p.CalendarDays,"Generated doctrine axes and calendars are bounded: "+id);
            check(StateHash.Compute(p)==StateHash.Compute(Root(id)),"Profiles are stateless and repeatable: "+id);
        }
        var a=Root(0);a.Images=950;a.Exclusivity=1000;a.Orthodoxy=1000;
        var same=ReligiousDoctrines.Compare(a,a);check(same.Tension==0&&same.BeliefAffinity==1000&&same.PracticeAffinity==1000,"A faith has no fabricated doctrinal disagreement with itself.");
        var close=ReligiousDoctrines.Descendant(a,1,ReligiousIssue.SacredImages);close.Images=0;
        var sects=ReligiousDoctrines.Compare(a,close);check(sects.BeliefAffinity==1000&&sects.PracticeAffinity>750&&sects.Tension>=550&&sects.Disagreement.Contains("images"),"Close relatives can have a sharp specific image dispute without being generically unrelated.");
        var mild=a.Copy();mild.Id=2;mild.Exclusivity=mild.Orthodoxy=0;var far=Root(3);far.Exclusivity=far.Orthodoxy=0;
        check(ReligiousDoctrines.Compare(mild,far).Tension==0,"Nonexclusive accommodating traditions are not made enemies merely by distance of beliefs.");
        check(ReligiousDoctrines.LocalGrievance(sects,0,true,0,900)==0&&ReligiousDoctrines.LocalGrievance(sects,2,false,0,900)==0,"Neither religious difference alone nor an unfunded prohibition creates enforced grievance.");
        check(ReligiousDoctrines.LocalGrievance(sects,2,true,0,900)>ReligiousDoctrines.LocalGrievance(sects,2,true,950,900),"Funded exclusion produces less grievance with actual local inclusion.");
        foreach(var issue in Enum.GetValues<ReligiousIssue>().Where(i=>i!=ReligiousIssue.None))
        {
            var child=ReligiousDoctrines.Descendant(a,4,issue);check(child.RootId==a.RootId&&child.Generation==a.Generation+1&&child.Issue==issue&&child.Cosmology==a.Cosmology&&child.Afterlife==a.Afterlife,"A branch records its cause and retains unrevised ancestry/beliefs: "+issue);
        }
        var synthesis=ReligiousDoctrines.Descendant(a,5,ReligiousIssue.SacredImages,far);
        check(synthesis.SecondaryParent==far.Id&&synthesis.Images==far.Images&&synthesis.Cosmology==a.Cosmology,"Synthesis has a physical donor lineage and changes a specific received practice.");
        var detached=new HistoricalReligiousTraditions{Doctrines=new[]{a},Institutions=new HistoricalCellTree(33554432)};var copy=detached.Copy();copy.Doctrines[0].Images=1;copy.Institutions=copy.Institutions.With(0,0);
        check(a.Images==950&&detached.Institutions[0]==-1,"Published doctrine records and persistent local bodies detach safely.");
        var high=a.Copy();high.Outreach=high.Universalism=high.Intermarriage=1000;var low=a.Copy();low.Outreach=low.Universalism=low.Intermarriage=0;
        check(ReligiousDoctrines.ConversionMultiplier(same,high,500)>ReligiousDoctrines.ConversionMultiplier(same,low,500),"Outreach, universal membership and intermarriage openness have actual adoption effects.");
        check(ReligiousDoctrines.MainIssue(a,close)==ReligiousIssue.SacredImages,"Branching identifies the actual main disagreement rather than comparing prose strings.");
        var tree=new HistoricalCellTree(33554432);var rng=new Rng(319,"religious-body-block-test");
        for(int trial=0;trial<100;trial++)
        {
            int start=trial<4?new[]{0,25,1020,33554400}[trial]:(int)(rng.Unit()*33554400),length=1+(int)(rng.Unit()*32);var values=Enumerable.Range(0,length).Select(_=>(int)(rng.Unit()*15)-1).ToArray();var literal=tree;
            for(int i=0;i<length;i++)literal=literal.With(start+i,values[i]);var block=tree.WithBlock(start,values);
            check(block.Fingerprint(true)==literal.Fingerprint(true),"Bounded record update has identical canonical values across radix/leaf boundaries: "+trial);
            check(ReferenceEquals(block.WithBlock(start,values),block),"Unchanged bounded records allocate no new tree: "+trial);tree=block;
        }
        int[] empty=Enumerable.Repeat(-1,32).ToArray();var erased=new HistoricalCellTree(64).WithBlock(17,Enumerable.Repeat(7,32).ToArray()).WithBlock(17,empty);
        check(erased.Fingerprint(true)==new HistoricalCellTree(64).Fingerprint(true),"Bounded deletion prunes empty paths exactly like scalar writes.");
    }
    public static void World(string path,string output,Action<bool,string> check)
    {
        Directory.CreateDirectory(output);var world=WorldFile.Read(File.ReadAllBytes(path));var old=world.Prehistory;var e=new Prehistory(world);e.Validate(old);string before=old.ComputeHash();
        var s=e.UpgradeRealmLifecycles(old);e.Validate(s);var h=s.History;var a=h.Aggregates;var religion=a.Empires.Connections.Religion;
        check(a.Empires.Connections.Realms.Version==31&&religion.Traditions.Doctrines.Length==h.Religions.Length,"Explicit31 upgrade creates exactly one structured profile per existing faith.");
        check(old.ComputeHash()==before&&StateHash.Compute(old.History.Religions)==StateHash.Compute(h.Religions),"Upgrade leaves old beliefs' names, dates, text and historical identities untouched.");
        check(old.History.Aggregates.Cells.Fingerprint()==a.Cells.Fingerprint()&&old.History.Aggregates.TotalPopulation==a.TotalPopulation&&old.Society.Settlements.Select(t=>t.FoodReserve).SequenceEqual(s.Society.Settlements.Select(t=>t.FoodReserve)),"Upgrade creates no people, food, territorial transfers or instant conversions.");
        check(ReferenceEquals(e.UpgradeRealmLifecycles(s),s),"Structured religious upgrade is idempotent.");
        var probe=s.Copy();var pa=probe.History.Aggregates;var pr=pa.Empires.Connections.Religion;var t=probe.Society.Settlements.First(t=>t.Population>=600&&pa.Living.Cities[t.Id].AbandonedYear<0);int faith=HistoricalCells.Religion(probe,t.Cell);
        // A controlled maintained-institution counterfactual, not a claim that
        // these provisions or disagreements occurred in the recorded history.
        pr.Write(t.Cell,4,900);Call(e,"AdvanceStructuredReligiousInstitutions",probe,t.Cell,t.Id,-1,0,false);
        var body=Prehistory.ReligiousInstitutionAt(probe,t.Cell);check(body!=null&&body.Faith==faith&&body.Seat==t.Id,"A local supported ritual body starts locally, not in an omniscient global church.");
        int bodyKey=t.Id*2*HistoricalReligiousTraditions.InstitutionFields;
        string institutions=pr.Traditions.Institutions.Fingerprint();Call(e,"AdvanceStructuredReligiousInstitutions",probe,t.Cell,t.Id,-1,0,false);
        check(institutions==pr.Traditions.Institutions.Fingerprint(),"A duplicate same-year caller performs no second religious body update.");
        var coercion=probe.Copy();var cr=coercion.History.Aggregates.Empires.Connections.Religion;var target=coercion.Society.Settlements[t.Id];
        int rival=Enumerable.Range(0,coercion.History.Religions.Length).First(i=>i!=faith);int ruler=coercion.History.Polities.First(p=>p.EndedYear<0).Id;
        var localDoctrine=cr.Traditions.Doctrines[faith];localDoctrine.Images=1000;localDoctrine.Exclusivity=localDoctrine.Orthodoxy=1000;
        var opposing=localDoctrine.Copy();opposing.Id=rival;opposing.Images=0;cr.Traditions.Doctrines[rival]=opposing;
        coercion.History.Polities[ruler].ReligionId=rival;target.Stability=900;string ownership=coercion.History.Aggregates.Cells.Fingerprint();
        coercion.Years+=40;Call(e,"AdvanceStructuredReligiousInstitutions",coercion,target.Cell,target.Id,ruler,2,true);
        var grievance=Prehistory.ReligiousInstitutionAt(coercion,target.Cell);
        check(grievance.Tension>100&&target.Stability<900,"A specific image prohibition plus funded coercion creates actual local tension and a stability consequence.");
        int priorTension=grievance.Tension;coercion.Years+=80;Call(e,"AdvanceStructuredReligiousInstitutions",coercion,target.Cell,target.Id,ruler,0,false);
        check(Prehistory.ReligiousInstitutionAt(coercion,target.Cell).Tension<priorTension,"Permitted worship lets enforced grievance subside without changing the people's doctrine.");
        check(coercion.History.Aggregates.Cells.Fingerprint()==ownership,"Religious tension never directly trades territory or instantly converts a town.");
        var communion=probe.Copy();var br=communion.History.Aggregates.Empires.Connections.Religion;
        var pair=communion.Society.Settlements.Where(x=>x.Population>=600&&communion.History.Aggregates.Living.Cities[x.Id].AbandonedYear<0&&HistoricalCells.Religion(communion,x.Cell)==faith).OrderBy(x=>x.Id).Take(2).ToArray();
        if(pair.Length==2)
        {
            br.Traditions.Doctrines[faith].ClericalAuthority=950;
            foreach(var site in pair){br.Write(site.Cell,4,900);Call(e,"AdvanceStructuredReligiousInstitutions",communion,site.Cell,site.Id,-1,0,false);}
            for(int k=0;k<12;k++){communion.Years+=20;Call(e,"RecordStructuredReligiousContact",communion,pair[1].Cell,pair[0].Cell,faith);Call(e,"AdvanceStructuredReligiousInstitutions",communion,pair[1].Cell,pair[1].Id,-1,0,false);}
            check(Prehistory.ReligiousInstitutionAt(communion,pair[1].Cell).Seat==pair[0].Id&&communion.History.Religions.Length==probe.History.Religions.Length,"Repeated delivered contact can unify authority without inventing a new religion.");
            communion.Years+=600;Call(e,"AdvanceStructuredReligiousInstitutions",communion,pair[1].Cell,pair[1].Id,-1,0,false);
            check(Prehistory.ReligiousInstitutionAt(communion,pair[1].Cell).Seat==pair[1].Id&&HistoricalCells.Religion(communion,pair[1].Cell)==faith,"Loss of maintained institutional contact restores local authority without changing faith or state.");
        }
        var branching=probe.Copy();var nr=branching.History.Aggregates.Empires.Connections.Religion;
        branching.Years+=2000;Call(e,"SeedLayReligiousCommunity",branching,t.Cell,faith);
        int layKey=t.Cell*HistoricalLayAssociations.Fields;nr.LayAssociations.Records=nr.LayAssociations.Records.With(layKey,faith).With(layKey+1,900).With(layKey+3,branching.Years-1000).With(layKey+5,900);
        nr.Traditions.Doctrines[faith].RulerSanctity=900;branching.Society.Settlements[t.Id].FoodReserve=branching.Society.Settlements[t.Id].Population*3;
        int faithCount=branching.History.Religions.Length;long reserve=branching.Society.Settlements[t.Id].FoodReserve;string publicBefore=branching.History.Aggregates.Cells.Fingerprint();
        for(int trial=0;trial<20&&branching.History.Religions.Length==faithCount;trial++){branching.Years+=1200;Call(e,"TryStructuredReligiousBranch",branching,t.Cell,t.Id,-1,faith,900,1200,true,true,false,-1);}
        check(branching.History.Religions.Length==faithCount+1&&nr.Traditions.Doctrines.Length==faithCount+1,"A controlled sustained, provisioned doctrinal dispute creates one linked denomination and its structured profile.");
        check(nr.Traditions.Doctrines[faithCount].Issue==ReligiousIssue.CourtAuthority&&nr.Traditions.Doctrines[faithCount].RulerSanctity<nr.Traditions.Doctrines[faith].RulerSanctity,"The new denomination really changes the disputed teaching, not only its name.");
        check(nr.LayAssociations.Records[layKey]==faithCount&&branching.History.Aggregates.Cells.Fingerprint()==publicBefore&&branching.Society.Settlements[t.Id].FoodReserve<reserve,"Branch organization pays from actual provisions but does not automatically change public religion or territory.");
        check(HistoricalMapSnapshot.ReligiousInstitutionIdentity(probe,t.Cell)==t.Id*2&&SocietyView.SelectionId(probe,t.Cell,HistoryOverlay.ReligiousInstitutions)==t.Id*2&&HistoryAtlas.Selection(probe,t.Cell,HistoryOverlay.ReligiousInstitutions)?.Kind=="religious-body","Institution map, selection identity and document select the same actual religious body.");
        string unchanged=probe.ComputeHash();var document=HistoryAtlas.Read(probe,"religion",faith,world:world);var institutionDoc=HistoryAtlas.Read(probe,"religious-body",t.Id*2,world:world);
        check(document.Sections.Any(x=>x.Title=="Social teachings")&&document.Sections.Any(x=>x.Title=="Observance and sacred obligations")&&institutionDoc.Title.Contains(t.Name)&&probe.ComputeHash()==unchanged,"Detailed doctrinal and institutional atlas documents are present and read-only.");
        File.WriteAllText(Path.Combine(output,"religion-document.json"),System.Text.Json.JsonSerializer.Serialize(document,new System.Text.Json.JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
        var child=probe.Copy();child.History.Aggregates.Empires.Connections.Religion.Traditions.Doctrines[faith].Images=(child.History.Aggregates.Empires.Connections.Religion.Traditions.Doctrines[faith].Images+500)%1001;
        check(child.ComputeHash()!=probe.ComputeHash()&&s.ComputeHash()==s.Hash,"Doctrinal edits participate in integrity hashes and do not mutate a source checkpoint.");
        var full=e.AdvanceYears(s,100);var split=e.AdvanceYears(e.AdvanceYears(s,37),63);e.Validate(full);
        check(full.Hash==split.Hash,"Actual religious/city/calendar updates are independent of publication partition.");
        check(full.History.Aggregates.Empires.Connections.Religion.Traditions.Institutions.Fingerprint()!=s.History.Aggregates.Empires.Connections.Religion.Traditions.Institutions.Fingerprint(),"Real due city events populate the saved institutional ledger.");
        world.Prehistory=full;var restoredWorld=WorldFile.Read(WorldFile.Write(world));var restored=restoredWorld.Prehistory;var cold=new Prehistory(restoredWorld);cold.Validate(restored);
        check(restored.Hash==full.Hash&&cold.AdvanceYears(restored,12).Hash==e.AdvanceYears(full,12).Hash,"Full-world persistence and cold12-year continuation preserve doctrines, bodies and future events.");
        var json=WorldJson.Read<PrehistoryState>(WorldJson.Write(full));e.Validate(json);check(json.Hash==full.Hash,"JSON roundtrip retains the optional religious extension and its integrity.");
        var fresh=e.Start(e.Prepare());e.Validate(fresh);check(fresh.History.Aggregates.Empires.Connections.Religion.Traditions!=null,"Fresh histories enable the new religion rules without an upgrade action.");
        var firstCulture=fresh.Copy();int origin=Array.FindIndex(e.CellPatch,p=>p==fresh.Origin);
        var queue=new SimulationEventQueue(firstCulture.Timeline.Events);var queueField=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;
        queueField.SetValue(e,queue);try{Call(e,"FoundAggregateCulture",firstCulture,fresh.Origin,origin,-1);}finally{queueField.SetValue(e,null);}
        firstCulture.Timeline.Events=queue.Ordered();firstCulture.Seal();e.Validate(firstCulture);
        check(firstCulture.History.Aggregates.Empires.Connections.Religion.Traditions.Doctrines.Length==firstCulture.History.Religions.Length&&firstCulture.History.Religions.Length==1,"An actual first cultural actor appends its structured doctrine in the correct order.");
        check(old.ComputeHash()==before,"All religious review work leaves the actual old saved world unchanged.");
        File.WriteAllBytes(Path.Combine(output,"review.world"),WorldFile.Write(world));
        File.WriteAllText(Path.Combine(output,"result.txt"),"PASS structured religion upgrade, actual100-year partition replay, cold save/load, read-only atlas and unchanged source. "+full.Hash);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void UpgradeLarge(World world,Prehistory engine,Action<bool,string> check)
    {
        var old=world.Prehistory;string hash=old.ComputeHash();Console.WriteLine("Large religion fixture loaded: "+world.Surface.Cells.Length+" cells; "+old.Society.Settlements.Length+" recorded towns.");
        var next=engine.UpgradeRealmLifecycles(old);engine.Validate(next);
        check(old.ComputeHash()==hash&&next.History.Aggregates.Cells.Fingerprint()==old.History.Aggregates.Cells.Fingerprint(),"Large-world explicit religious upgrade preserves its input and political cells.");
        world.Prehistory=next;
    }
    public static void Large(string path,string output,Action<bool,string> check)
    {
        Directory.CreateDirectory(output);var world=WorldFile.Read(File.ReadAllBytes(path));var e=new Prehistory(world);UpgradeLarge(world,e,check);GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        double worst=0;e.YearWorkObserver=(_,ms)=>worst=Math.Max(worst,ms);var watch=System.Diagnostics.Stopwatch.StartNew();
        world.Prehistory=e.AdvanceYears(world.Prehistory,12);watch.Stop();e.Validate(world.Prehistory);
        var state=world.Prehistory;var r=state.History.Aggregates.Empires.Connections.Religion.Traditions;
        check(r.Doctrines.Length==state.History.Religions.Length,"Large-world scheduled events keep faith profiles aligned.");
        check(!string.IsNullOrEmpty(r.Institutions.Fingerprint(true)),"Large-world validated religious bodies retain a verifiable sparse integrity hash.");
        var report=new{Cells=world.Surface.Cells.Length,Years=12,Milliseconds=watch.Elapsed.TotalMilliseconds,WorstYearMilliseconds=worst,Faiths=r.Doctrines.Length,Hash=state.Hash};
        File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(report,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Large religious continuation: "+System.Text.Json.JsonSerializer.Serialize(report));
        File.WriteAllBytes(Path.Combine(output,"review.world"),WorldFile.Write(world));
    }
}
