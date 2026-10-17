using System.Reflection;
using System.Runtime.Serialization;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HistoryReligiousNetworkTests
{
    static object Call(Prehistory e,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(e,args);
    public static void Run(Action<bool,string> check)
    {
        var r=new HistoricalReligiousNetworks{SinceYear=80,Communities=new HistoricalCellTree(200*HistoricalReligiousNetworks.Fields)};
        r.Write(7,0,4);r.Write(7,1,300);r.Policies=r.Policies.With(13,2);
        string original=StateHash.Compute(r);var copy=r.Copy();copy.Write(7,1,550);copy.Policies=copy.Policies.With(13,1);
        check(r.Read(7,1)==300&&r.Policies[13]==2&&StateHash.Compute(r)==original,"Religion publication detaches sparse community and court writes without copying terrain.");
        check(StateHash.Compute(copy)!=original,"Local religious pressure and government policy participate in canonical integrity.");
        using(var bytes=new MemoryStream())
        {
            var serializer=new DataContractSerializer(typeof(HistoricalReligiousNetworks));serializer.WriteObject(bytes,r);bytes.Position=0;
            var restored=(HistoricalReligiousNetworks)serializer.ReadObject(bytes);
            check(StateHash.Compute(restored)==original&&restored.Read(7,0)==4,"Religious communities and court policies retain exact canonical bytes through serialization.");
        }
        var maximum=new HistoricalCellTree(2560000*HistoricalReligiousNetworks.Fields).With(2559999*HistoricalReligiousNetworks.Fields+12,900);
        check(maximum.Length<=33554432&&maximum[2559999*HistoricalReligiousNetworks.Fields+12]==900,"Maximum-detail religion fields fit the persistent radix tree and remain sparse.");
        check(StateHash.Compute(new HistoricalColonialConnections())=="3e6f409c4cdc77e11d2119ec182e566258a65f5eaa5265d0ebc2bd8ea6d094ea","An absent religious revision preserves the exact preceding colonial canonical bytes.");
        check(HistoricalReligiousNetworkEffects.ActiveYears(100,200,110,110)==80,"One delivered visit cannot imply a century of continuously renewed religious exchange.");
        check(HistoricalReligiousNetworkEffects.ActiveYears(200,250,110,220)==50,"Recent repeated actual contact supports a bounded continuous local exposure interval.");
        check(HistoricalReligiousNetworkEffects.ActiveYears(250,300,110,110)==0,"Stale religious contact cannot remain a live missionary route.");
        int sustained=0,isolated=500;
        for(int k=0;k<20;k++)
        {
            sustained=HistoricalReligiousNetworkEffects.Pressure(sustained,25,25,ReligiousReach.Missionary,800,950,false);
            isolated=HistoricalReligiousNetworkEffects.Pressure(isolated,25,0,ReligiousReach.Missionary,800,950,false);
        }
        check(sustained>=700&&isolated==100,"Sustained supported contact can establish a community; absent contact causes influence to decay.");
        check(!HistoricalReligiousNetworkEffects.Adopt(1000,149,1000)&&!HistoricalReligiousNetworkEffects.Adopt(1000,1000,99)&&HistoricalReligiousNetworkEffects.Adopt(700,150,100),"Dominance needs pressure, generations of local continuity and actual tradition residence.");
        check(HistoricalReligiousNetworkEffects.Institution(800,100,false,false)<HistoricalReligiousNetworkEffects.Institution(800,100,true,true),"Institutions decline without provisions rather than becoming a permanent free missionary multiplier.");
        check(HistoricalReligiousNetworkEffects.FoodCost(10000,40)==200,"Ritual institutions debit a finite real annual provision cost.");
        check(HistoricalReligiousNetworkEffects.Pressure(0,100,100,ReligiousReach.Portable,700,180,false)<HistoricalReligiousNetworkEffects.Pressure(0,100,100,ReligiousReach.Portable,700,950,false),"Restrictions impede competing public institutions without instantly changing private beliefs.");
        check(HistoricalReligiousNetworkEffects.CarrierStrength(ReligiousReach.Missionary,0)==HistoricalReligiousNetworkEffects.CarrierStrength(ReligiousReach.Portable,0)&&HistoricalReligiousNetworkEffects.CarrierStrength(ReligiousReach.Missionary,800)>HistoricalReligiousNetworkEffects.CarrierStrength(ReligiousReach.Portable,800),"The strongest recent carrier uses local paid support; an unsupported missionary label cannot outcompete the same household tradition for free.");
        ReligiousNews(check);
        HistoryReligiousInstitutionTests.Run(check);
    }
    static void ReligiousNews(Action<bool,string> check)
    {
        var s=new PrehistoryState{Years=1000,Population=new[]{1000000L},Society=new SocietyState{Cultures=new[]{new CultureIdentity{Id=0,Name="Aru",Population=1000000}},Settlements=new[]{new Settlement{Id=0,Name="Mera",Population=10000},new Settlement{Id=1,Name="Leto",Population=100}}},History=new HistoryState{Version=Prehistory.LivingWorldHistoryVersion,Cultures=new[]{new HistoricalCulture{Id=0,Technology=new int[39]}},Religions=new[]{new HistoricalReligion{Id=0,Name="Mera teaching"}},Polities=new[]{new HistoricalPolity{Id=0,Name="Aru",Population=1000000},new HistoricalPolity{Id=1,Name="Leto",Population=100}},Dynamics=new HistoricalDynamics{States=new[]{new HistoricalStateDynamics(),new HistoricalStateDynamics()}}}};
        var news=new ChronicleNews(s);var story=new HistoricalEvent{Year=1000,Category=ChronicleCategory.Society,Kind="religious-schism",PolityId=0,SettlementId=0,ReligionId=0,Text="Mera teaching establishes an autonomous ritual community. Household continuity persists."};
        check(news.Project(story)?.Text=="Mera teaching establishes an autonomous ritual community.","A significant religious schism reaches the editorial news with its actual causal record.");
        story.Kind="religious-synthesis";check(news.Project(story)!=null,"A significant local religious synthesis is visible to the observer.");
        story.Kind="religious-renewal";check(news.Project(story)!=null,"An actual ethical renewal responding to captured patronage can enter the editorial record.");
        story.Kind="religious-policy";check(news.Project(story)!=null,"A major court's religious-policy change can lead the society news.");
        story.PolityId=1;story.SettlementId=1;check(news.Project(story)==null,"Minor routine court policies remain in the archive without displacing larger news.");
        story.Kind="religious-schism";check(news.Project(story)==null,"A tiny local schism does not automatically become world news.");
        story.PolityId=0;story.SettlementId=0;story.Kind="religious-exchange";check(news.Project(story)==null,"Routine religious contact is retained in history without becoming an editorial lead.");
        check(ChronicleNews.Priority("religious-schism")>ChronicleNews.Priority("religious-policy")&&ChronicleNews.Priority("religious-schism")<ChronicleNews.Priority("nuclear-strike"),"Distinctive religious formation can preempt routine society news without outranking extraordinary mass events.");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var source=w.Prehistory;var e=new Prehistory(w);string original=source.Hash;
        var s=e.UpgradeRealmLifecycles(source);e.Validate(s);var a=s.History.Aggregates;var r=a.Empires.Connections.Religion;
        string profilesBefore=StateHash.Compute(s.History.Religions);Call(e,"UpgradeReligiousNetworkRules",s);s.Seal();e.Validate(s);
        check(r.Version==2&&StateHash.Compute(s.History.Religions)==profilesBefore,"Explicit religious institution revision2 preserves established actors and profiles rather than inventing an unrecorded past.");
        check(r!=null&&r.SinceYear==s.Years,"Only explicit realm revision upgrade initializes religious-network rules.");
        var legacyProfile=s.Copy();legacyProfile.History.Aggregates.Empires.Connections.Religion.Version=1;string legacyProfileHash=StateHash.Compute(legacyProfile.History.Religions);
        Call(e,"InitializeReligiousProfile",legacyProfile,0);check(StateHash.Compute(legacyProfile.History.Religions)==legacyProfileHash,"Frozen religion1 never rewrites its generic established profile under the later profile constructor.");
        var newProfiles=s.Copy();for(int id=0;id<Math.Min(16,newProfiles.History.Religions.Length);id++)Call(e,"InitializeReligiousProfile",newProfiles,id);
        check(newProfiles.History.Religions.Take(16).Select(x=>x.Ethos).Distinct().Count()>1,"New profiles in this actual seed express distinct supported religious vocations without geography or ethnicity templates.");
        var family=newProfiles.History.Cultures.OrderBy(x=>x.ReligionId).GroupBy(x=>x.ReligionId).Select(x=>x.First()).Take(2).ToArray();
        if(family.Length==2)
        {
            Call(e,"InheritReligiousProfile",newProfiles,family[1].Id,family[0].Id);var descendant=newProfiles.History.Religions[family[1].ReligionId];var ancestor=newProfiles.History.Religions[family[0].ReligionId];
            check(descendant.ParentId==ancestor.Id&&descendant.Ethos==ancestor.Ethos&&descendant.Ritual==ancestor.Ritual,"Actual migrant-community profiles inherit immediate stored obligations and observance rather than reseeding all belief text or walking ancestors.");
        }
        check(a.TotalPopulation==source.History.Aggregates.TotalPopulation&&s.History.Cultures.Select(x=>x.ReligionId).SequenceEqual(source.History.Cultures.Select(x=>x.ReligionId)),"Religion upgrade preserves inhabitants, established faiths and culture default identities.");
        check(ReferenceEquals(e.UpgradeRealmLifecycles(s),s),"The complete revised realm/religion upgrade is idempotent.");
        string beforeView=s.ComputeHash();var sections=HistoryAtlas.ReligiousNetworkSections(s,s.History.Cultures[0].ReligionId);
        check(sections.Length==2&&sections.All(x=>x.Links.Length<=12)&&s.ComputeHash()==beforeView,"On-demand religious profiles expose bounded real community examples without changing the historical checkpoint.");
        w.Prehistory=s;var restored=WorldFile.Read(WorldFile.Write(w)).Prehistory;
        check(restored.Hash==s.Hash&&restored.History.Aggregates.Empires.Connections.Religion!=null,"The actual-world religious revision survives validated full-world save/load.");
        var full=e.AdvanceYears(s,20);var pieces=e.AdvanceYears(e.AdvanceYears(restored,7),13);e.Validate(full);
        check(full.Hash==pieces.Hash,"Religion, policy and institution events replay across save/load and20 versus7+13-year publication partitions.");
        check(source.ComputeHash()==original,"A reviewed old checkpoint is never silently rewritten by religious revision.");
        int target=-1,donor=-1;
        foreach(var city in a.Cities)
        {
            if(a.Living.Cities[city.Id].AbandonedYear>=0)continue;
            foreach(int peer in city.Neighbors.Take(12))
            {
                var from=s.Society.Settlements[peer];var to=s.Society.Settlements[city.Id];
                if(peer==city.Id||from.Population<600||to.Population<100||from.FoodReserve<=from.Population||!a.Cities[peer].Supported||a.Living.Cities[peer].AbandonedYear>=0)continue;
                if(!(bool)Call(e,"InstitutionalRouteAllowed",s,from.Cell,to.Cell,from.CultureId))continue;
                target=city.Id;donor=peer;break;
            }
            if(target>=0)break;
        }
        check(target>=0,"The actual-world control contains an existing physically reachable town contact for causal religion tests.");
        if(target<0)return;
        // Controlled local experiment, separate from valid chronological replay.
        // Advance only these two endpoint actors; no demographic process runs.
        var test=s.Copy();var ta=test.History.Aggregates;var tr=ta.Empires.Connections.Religion;var t=test.Society.Settlements[target];var d=test.Society.Settlements[donor];
        int sourceDisruption=ta.Living.Cities[donor].DisruptedUntil,targetDisruption=ta.Living.Cities[target].DisruptedUntil;
        ta.Living.Cities[donor].DisruptedUntil=test.Years+100;
        bool interruptedSource=!(bool)Call(e,"ReligiousTownCarrierAllowed",test,donor,target);
        ta.Living.Cities[donor].DisruptedUntil=sourceDisruption;ta.Living.Cities[target].DisruptedUntil=test.Years+100;
        bool interruptedTarget=!(bool)Call(e,"ReligiousTownCarrierAllowed",test,donor,target);
        ta.Living.Cities[target].DisruptedUntil=targetDisruption;
        check(interruptedSource&&interruptedTarget,"The actual town-carrier path rejects disruption at either endpoint despite an existing physical neighbor edge.");
        int incumbent=HistoricalCells.Religion(test,t.Cell),challenger=Enumerable.Range(0,test.History.Religions.Length).First(x=>x!=incumbent);
        ta.Cells=ta.Cells.With(d.Cell*5+2,challenger);ta.Cells=ta.Cells.With(t.Cell*5+2,incumbent);tr.Write(d.Cell,4,900);
        test.History.Religions[challenger].Reach=ReligiousReach.Missionary;
        int owner=HistoricalCells.Polity(test,t.Cell);if(owner>=0)tr.Policies=tr.Policies.With(owner*4,test.History.Dynamics.States[owner].LeaderId).With(owner*4+1,0).With(owner*4+2,test.Years);
        int defaultFaith=test.History.Cultures[t.CultureId].ReligionId;long people=ta.TotalPopulation,foodBefore=d.FoodReserve,treasury=test.History.Polities.Sum(x=>x.Treasury);
        string untouched=tr.Communities.Fingerprint();
        Call(e,"RecordReligiousContact",test,d.CultureId,t.CultureId,-1,t.Cell);
        check(tr.Communities.Fingerprint()==untouched,"A culture identity or unspecified endpoint delivers no religious exposure.");
        int sea=Array.FindIndex(e.CellPatch,x=>x<0);
        if(sea>=0)Call(e,"RecordReligiousContact",test,d.CultureId,t.CultureId,d.Cell,sea);
        check(tr.Communities.Fingerprint()==untouched,"Unsupported water endpoints cannot be converted through a remembered cultural connection.");
        Call(e,"RecordReligiousContact",test,d.CultureId,t.CultureId,d.Cell,t.Cell);
        check(HistoricalCells.Religion(test,t.Cell)==incumbent,"A successful first religious visit does not instantly convert the receiving town.");
        for(int k=0;k<14;k++)
        {
            test.Years+=40;
            // Keep this an intentionally supported-source control; actual food
            // is still debited on every funded mission and institution turn.
            tr.Write(d.Cell,4,900);
            Call(e,"RecordReligiousContact",test,d.CultureId,t.CultureId,d.Cell,t.Cell);
            Call(e,"AdvanceReligiousCommunity",test,t.CultureId,t.Cell,target);
        }
        check(HistoricalCells.Religion(test,t.Cell)==challenger&&e.ReligiousCommunity(test,t.Cell).Household==incumbent,"Repeated actual supported endpoint exchange changes local public predominance while retaining inherited household identity.");
        check(test.History.Cultures[t.CultureId].ReligionId==defaultFaith,"Local adoption never repaints a whole cultural territory by changing its default religion.");
        check(ta.TotalPopulation==people&&test.History.Polities.Sum(x=>x.Treasury)<=treasury,"Religion produces no inhabitants or funds.");
        check(d.FoodReserve<foodBefore,"Supported outward missions and religious institutions spend real donor provisions.");
        ta.Cells=ta.Cells.With(d.Cell*5+2,challenger);tr.Write(d.Cell,4,950);
        Call(e,"InheritReligiousHouseholds",test,t.Cell,d.Cell,incumbent,100L);
        var diaspora=e.ReligiousCommunity(test,t.Cell);
        check(diaspora.Dominant==incumbent&&diaspora.Household==challenger&&diaspora.Institution==0&&diaspora.Pressure==0,"Actual migrant households retain a carried tradition without overwriting indigenous public rites or bringing a ready-made institution.");
        Call(e,"InheritReligiousHouseholds",test,t.Cell,d.Cell,-1,0L);
        check(HistoricalCells.Religion(test,t.Cell)==challenger&&test.History.Cultures[t.CultureId].ReligionId==defaultFaith,"A genuinely new migrant community starts from its actual departure faith rather than the remote cultural default.");
        check(ta.TotalPopulation==people,"Religious migrant initialization neither fabricates nor transfers demographic accounts a second time.");
        ta.Cells=ta.Cells.With(t.Cell*5+2,incumbent).With(d.Cell*5+2,challenger);tr.Write(t.Cell,6,-1);tr.Write(t.Cell,7,-1);tr.Write(t.Cell,10,-1);
        int version=ta.Empires.Connections.Realms.Version;ta.Empires.Connections.Realms.Version=2;
        string noDelivery=tr.Communities.Fingerprint();Call(e,"RecordDeliveredRegionalReligion",test,d.Cell,t.Cell);
        check(tr.Communities.Fingerprint()==noDelivery,"Frozen realm2 never silently receives the later rural regional religion delivery behavior.");
        int sourceCulture=HistoricalCells.Culture(test,d.Cell),sourceDefault=test.History.Cultures[sourceCulture].ReligionId;
        test.History.Cultures[sourceCulture].ReligionId=incumbent;ta.Empires.Connections.Realms.Version=3;Call(e,"RecordDeliveredRegionalReligion",test,d.Cell,t.Cell);
        check(tr.Read(t.Cell,6)==challenger&&tr.Read(t.Cell,7)==test.Years,"Revision3 actual regional deliveries freeze the source endpoint faith and receipt date, independently of a global technical teacher.");
        Console.WriteLine("Regional religion receipt: source cell "+d.Cell+" faith "+challenger+" -> target cell "+t.Cell+" visitor "+tr.Read(t.Cell,6)+" at year "+tr.Read(t.Cell,7)+"; local public faith remains "+HistoricalCells.Religion(test,t.Cell));
        ta.Empires.Connections.Realms.Version=version;test.History.Cultures[sourceCulture].ReligionId=sourceDefault;
        if(owner>=0)
        {
            var p=test.History.Polities[owner];long actual=p.Treasury;p.Treasury=0;
            check(!(bool)Call(e,"ReligiousPatronagePaid",test,owner,t.Cell,20)&&p.Treasury==0,"An empty court cannot pay for religious patronage or borrow imaginary funds.");p.Treasury=actual;
        }
        int originalRules=r.Version;var invalid=s.Copy();invalid.History.Aggregates.Empires.Connections.Religion.Version=99;invalid.Seal();bool rejected=false;try{e.Validate(invalid);}catch(ArgumentException){rejected=true;}
        check(rejected&&s.History.Aggregates.Empires.Connections.Religion.Version==originalRules,"Unknown religious rules are rejected without aliasing a prior publication.");
    }
    public static void Review(string path,string folder)
    {
        if(Directory.Exists(folder))throw new ArgumentException("Use a new religious-review output folder.");
        var w=WorldFile.Read(File.ReadAllBytes(path));var s=w.Prehistory;var e=new Prehistory(w);e.Validate(s);
        var rows=new List<string>{"town,cell,culture,residents,publicTradition,householdTradition,visitorTradition,influence,institution,ritualFamiliarity,lastContact,policy"};
        string Q(string value)=>"\""+(value??"").Replace("\"","\"\"")+"\"";
        string Faith(int id)=>id>=0?s.History.Religions[id].Name:"unrecorded";
        int supported=0,pressured=0,changed=0;var counts=new SortedDictionary<int,int>();
        foreach(var t in s.Society.Settlements)
        {
            if(t.Population<=0||s.History.Aggregates.Living.Cities[t.Id].AbandonedYear>=0)continue;
            int dominant=HistoricalCells.Religion(s,t.Cell);if(!counts.TryAdd(dominant,1))counts[dominant]++;
            var local=e.ReligiousCommunity(s,t.Cell);
            if(local!=null){if(local.Institution>=400)supported++;if(local.Pressure>0)pressured++;if(local.Household>=0&&local.Household!=dominant)changed++;}
            rows.Add(string.Join(",",Q(t.Name),t.Cell,t.CultureId,t.Population,Q(Faith(dominant)),Q(Faith(local?.Household??-1)),Q(Faith(local?.Visitor??-1)),local?.Pressure??0,local?.Institution??0,local?.Blending??0,local?.LastContact??-1,Q(local?.Policy??"legacy religion rules")));
        }
        Directory.CreateDirectory(folder);File.WriteAllLines(Path.Combine(folder,"local-religion.csv"),rows);
        var faithRows=new List<string>{"religion,parent,culture,founded,reach,publicTowns,favoredSurvivingCourts,name"};
        foreach(var faith in s.History.Religions)faithRows.Add(string.Join(",",faith.Id,faith.ParentId,faith.CultureId,faith.FoundedYear,faith.Reach,counts.TryGetValue(faith.Id,out int places)?places:0,s.History.Polities.Count(p=>p.EndedYear<0&&p.ReligionId==faith.Id),Q(faith.Name)));
        File.WriteAllLines(Path.Combine(folder,"religious-actors.csv"),faithRows);
        var eventRows=new List<string>{"kind,count"};
        foreach(var group in s.History.Archive.Where(x=>x.Kind.StartsWith("religious-",StringComparison.Ordinal)||x.Kind=="court-conversion").GroupBy(x=>x.Kind).OrderBy(x=>x.Key))eventRows.Add(group.Key+","+group.Count());
        File.WriteAllLines(Path.Combine(folder,"religious-events.csv"),eventRows);
        File.WriteAllText(Path.Combine(folder,"religion-summary.txt"),"Year "+s.Years+"\nHash "+s.Hash+"\nOptional religious networks "+(s.History.Aggregates.Empires.Connections.Religion!=null)+"\nActive towns "+(rows.Count-1)+"\nSupported ritual institutions (index>=400) "+supported+"\nTowns with competing public influence "+pressured+"\nTowns retaining a distinct household tradition "+changed+"\nReligious actors "+s.History.Religions.Length+"\n\nCurrent public predominance, town counts (not believer counts):\n"+string.Join("\n",counts.OrderByDescending(x=>x.Value).ThenBy(x=>x.Key).Select(x=>x.Value+" "+Faith(x.Key))));
        Console.WriteLine("Religious review "+folder+"; "+(rows.Count-1)+" actual towns; "+supported+" supported institutions; "+pressured+" competing influences.");
    }
}
