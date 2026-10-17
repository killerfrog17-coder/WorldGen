using System.Reflection;
using System.Runtime.Serialization;
using WorldGen.Core;
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
    }
    public static void World(string path,Action<bool,string> check)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var source=w.Prehistory;var e=new Prehistory(w);string original=source.Hash;
        var s=e.UpgradeRealmLifecycles(source);e.Validate(s);var a=s.History.Aggregates;var r=a.Empires.Connections.Religion;
        check(r!=null&&r.SinceYear==s.Years,"Only explicit realm revision upgrade initializes religious-network rules.");
        check(a.TotalPopulation==source.History.Aggregates.TotalPopulation&&s.History.Cultures.Select(x=>x.ReligionId).SequenceEqual(source.History.Cultures.Select(x=>x.ReligionId)),"Religion upgrade preserves inhabitants, established faiths and culture default identities.");
        check(ReferenceEquals(e.UpgradeRealmLifecycles(s),s),"The complete revised realm/religion upgrade is idempotent.");
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
        if(owner>=0)
        {
            var p=test.History.Polities[owner];long actual=p.Treasury;p.Treasury=0;
            check(!(bool)Call(e,"ReligiousPatronagePaid",test,owner,t.Cell,20)&&p.Treasury==0,"An empty court cannot pay for religious patronage or borrow imaginary funds.");p.Treasury=actual;
        }
        var invalid=s.Copy();invalid.History.Aggregates.Empires.Connections.Religion.Version=99;invalid.Seal();bool rejected=false;try{e.Validate(invalid);}catch(ArgumentException){rejected=true;}
        check(rejected&&s.History.Aggregates.Empires.Connections.Religion.Version==1,"Unknown religious rules are rejected without aliasing a prior publication.");
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
        File.WriteAllText(Path.Combine(folder,"religion-summary.txt"),"Year "+s.Years+"\nHash "+s.Hash+"\nOptional religious networks "+(s.History.Aggregates.Empires.Connections.Religion!=null)+"\nActive towns "+(rows.Count-1)+"\nSupported ritual institutions (index>=400) "+supported+"\nTowns with competing public influence "+pressured+"\nTowns retaining a distinct household tradition "+changed+"\nReligious actors "+s.History.Religions.Length+"\n\nCurrent public predominance, town counts (not believer counts):\n"+string.Join("\n",counts.OrderByDescending(x=>x.Value).ThenBy(x=>x.Key).Select(x=>x.Value+" "+Faith(x.Key))));
        Console.WriteLine("Religious review "+folder+"; "+(rows.Count-1)+" actual towns; "+supported+" supported institutions; "+pressured+" competing influences.");
    }
}
