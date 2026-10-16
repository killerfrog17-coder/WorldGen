using System.Diagnostics;
using System.Reflection;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class LanguageTests
{
    public static void Run(Action<bool,string> check)
    {
        var parent=LanguageGenerator.Create(73421,0,0);var child=LanguageGenerator.Create(73421,1,2000,parent);var unrelated=LanguageGenerator.Create(73421,2,2000);
        check(LanguageGenerator.Valid(parent)&&LanguageGenerator.Valid(child)&&LanguageGenerator.Valid(unrelated),"Generated profiles satisfy bounded structural contracts");
        check(child.FamilySeed==parent.FamilySeed&&child.FamilyId==parent.Id&&child.ParentId==parent.Id,"Descendants inherit a root seed and family rather than independent random words");
        check(unrelated.FamilySeed!=parent.FamilySeed,"Independent homelands have independent root vocabularies");
        check(StateHash.Compute(parent)==StateHash.Compute(LanguageGenerator.Create(73421,0,0)),"Complete profiles reproduce from named seed streams");
        string[] concepts={"river","river.bank","bank.finance","stone","people","land","settlement","sea","ocean","lake","house","ship","writing","iron","king","temple"};
        var words=concepts.Select(c=>LanguageGenerator.Word(parent,c)).ToArray();
        foreach(var concept in concepts.Reverse())LanguageGenerator.Word(parent,concept);
        check(words.Select(w=>w.Text).SequenceEqual(concepts.Select(c=>LanguageGenerator.Word(parent,c).Text)),"Lazy vocabulary is independent of request order");
        check(words.All(w=>w.Text.Length>0&&w.Text.Length<=30&&w.Phonetics.Contains('ˈ')),"Words have bounded lengths and explicit pronunciation/stress");
        var compound=LanguageGenerator.Compound(parent,"stone","river");
        check(compound.Text.Contains(LanguageGenerator.Word(parent,"stone").Text)&&compound.Text.Contains(LanguageGenerator.Word(parent,"river").Text),"Compounds reuse exactly the same standalone semantic roots");
        int changed=concepts.Count(c=>LanguageGenerator.Word(parent,c).Phonetics!=LanguageGenerator.Word(child,c).Phonetics);
        check(changed>0&&changed<concepts.Length,"Daughter languages exhibit inherited cognates with regular changes, not wholesale rerolls");
        string proto=LanguageGenerator.Word(parent,"stone").Text;var evolved=LanguageGenerator.Evolve(child,4000);
        check(parent.Revision==0&&evolved.Revision==1&&child.Revision==0&&LanguageGenerator.Word(parent,"stone").Text==proto,"Evolution leaves ancestors and published profiles unchanged");
        var borrowed=LanguageGenerator.Borrow(unrelated,parent,"writing",2500);
        check(borrowed.Loans.Length==1&&borrowed.Loans[0].SourceId==parent.Id&&unrelated.Loans.Length==0,"Borrowing records its donor and date without mutating the donor or recipient snapshot");
        check(ReferenceEquals(borrowed,LanguageGenerator.Borrow(borrowed,parent,"writing",2600)),"Repeated requests never reborrow or consume new randomness");
        check(LanguageGenerator.Valid(borrowed)&&LanguageGenerator.Valid(LanguageGenerator.Evolve(borrowed,4000)),"Loan phonemes and regular sound changes remain valid");
        check(LanguageGenerator.Word(parent,"river.bank").Text!=LanguageGenerator.Word(parent,"bank.finance").Text,"Distinct English senses do not share a raw-spelling hash key");
        check(Enumerable.Range(0,20).Any(i=>LanguageGenerator.ForeignName(parent,unrelated,LanguageNameKind.Region,i).Text!=LanguageGenerator.Name(parent,LanguageNameKind.Region,i).Text),"Different language lenses can produce different geographical names");
        for(int i=0;i<40;i++)borrowed=LanguageGenerator.Borrow(borrowed,parent,"domain:"+i,3000);
        check(borrowed.Loans.Length==LanguageGenerator.MaximumLoans,"Contact vocabulary has a strict constant bound");
        foreach(var kind in Enum.GetValues<LanguageNameKind>())
        {
            var name=LanguageGenerator.Name(parent,kind,43,"river");
            check(LanguageGenerator.ForeignName(parent,parent,kind,43,"river").Text==name.Text,"Native lens preserves "+kind+" endonyms");
            check(LanguageGenerator.ForeignName(parent,unrelated,kind,43,"river").Text==LanguageGenerator.ForeignName(parent,unrelated,kind,43,"river").Text,"Foreign "+kind+" names are stable");
        }
        check(LanguageGenerator.Sentence(parent,"person","see","river").EndsWith('.')&&LanguageGenerator.Sentence(parent,"person","see","river")==LanguageGenerator.Sentence(parent,"person","see","river"),"Future document scaffolds use the same vocabulary and stored word order");
        int distinct=Enumerable.Range(0,500).Select(i=>LanguageGenerator.Name(parent,LanguageNameKind.Settlement,i,"river").Text).Distinct().Count();
        check(distinct>300,"Shared geographical roots do not collapse most towns to one repeated name");
        foreach(int seed in new[]{0,1,-1,int.MinValue,17,42,73421})
        for(int i=0;i<16;i++)
        {
            var p=LanguageGenerator.Create(seed,i,0);check(LanguageGenerator.Valid(p),"Cross-seed sound profile validates");
            var names=Enumerable.Range(0,50).Select(id=>LanguageGenerator.Name(p,LanguageNameKind.Settlement,id,"river").Text).ToArray();
            check(names.All(n=>n.Length>=3&&n.Length<=32&&!n.Any(char.IsDigit)),"Names are bounded readable forms without numeric collision suffixes");
        }
        bool rejected=false;try{LanguageGenerator.Word(parent,new string('x',129));}catch(ArgumentException){rejected=true;}check(rejected,"Unbounded text is not accepted as a concept ID");
        var malformed=parent.Copy();malformed.SoundMap[0]=31;check(!LanguageGenerator.Valid(malformed),"Invalid sound-category mappings are rejected");
        // Benchmark, not a wall-time-dependent authoritative decision/assertion.
        var timer=Stopwatch.StartNew();long length=0;
        for(int i=0;i<100000;i++)length+=LanguageGenerator.Name(parent,LanguageNameKind.Settlement,i,"river").Text.Length;
        Console.WriteLine($"Language naming: 100,000 uncached names in {timer.Elapsed.TotalMilliseconds:F1} ms; {length} characters; {distinct}/500 distinct river-town names.");
        foreach(int id in new[]{0,2,4,6})
        {
            var p=LanguageGenerator.Create(73421,id,0);
            Console.WriteLine($"Family {id}: "+string.Join(", ",Enumerable.Range(0,8).Select(i=>LanguageGenerator.Capitalize(LanguageGenerator.Name(p,LanguageNameKind.Settlement,i,"river").Text))));
        }
    }
    public static void World(string path,Action<bool,string> check,string output=null)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;string before=source.ComputeHash();var engine=new Prehistory(world);
        var s=engine.UpgradeToLanguageHistory(source);engine.Validate(s);
        check(s.History.Version==Prehistory.LanguageHistoryVersion&&source.ComputeHash()==before&&source.History.Languages==null,"Explicit upgrade preserves the original historical checkpoint");
        check(s.Society.Cultures.Select(c=>c.Name).SequenceEqual(source.Society.Cultures.Select(c=>c.Name))&&s.Society.Settlements.Select(c=>c.Name).SequenceEqual(source.Society.Settlements.Select(c=>c.Name)),"Upgrading does not erase established culture/city names");
        check(s.History.Languages.Profiles.Length==s.History.Cultures.Length&&engine.UpgradeToResearchPacingHistory(s)==s,"Language profiles align with actors and older entry points cannot downgrade30");
        var copy=s.Copy();if(copy.History.Languages.Profiles.Length>0){copy.History.Languages.Profiles[0].SoundMap[0]=9;check(s.ComputeHash()==s.Hash&&!ReferenceEquals(copy.History.Languages.Profiles[0].SoundMap,s.History.Languages.Profiles[0].SoundMap),"Published language profiles are deeply detached");}
        var later=engine.AdvanceYears(s,10);engine.Validate(later);
        var old=engine.AdvanceYears(source,10);engine.Validate(old);
        check(later.History.Aggregates.TotalPopulation==old.History.Aggregates.TotalPopulation&&later.Society.Settlements.Length==old.Society.Settlements.Length&&later.History.Aggregates.Cells.Fingerprint()==old.History.Aggregates.Cells.Fingerprint(),"Naming changes do not alter population, settlement formation or control");
        check(StateHash.Compute(later.History.Cultures.Select(c=>c.Technology).ToArray())==StateHash.Compute(old.History.Cultures.Select(c=>c.Technology).ToArray()),"History30 retains29's calibrated research rules");
        var split=engine.AdvanceYears(engine.AdvanceYears(s,4),6);check(split.Hash==later.Hash,"Language-bearing event continuation is partition independent");
        world.Prehistory=later;var restored=WorldFile.Read(WorldFile.Write(world));check(restored.Prehistory.Hash==later.Hash&&engine.AdvanceYears(restored.Prehistory,2).Hash==engine.AdvanceYears(later,2).Hash,"Language profiles survive full world save/load and deterministic continuation");
        var dictionary=WorldJson.Read<HistoricalLanguages>(WorldJson.Write(later.History.Languages));check(StateHash.Compute(dictionary)==StateHash.Compute(later.History.Languages),"Language metadata also round-trips the shared JSON adapter");
        if(output!=null){if(File.Exists(output))throw new IOException("Refusing to overwrite a language review fixture");Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllBytes(output,WorldFile.Write(world));}
        if(s.History.Cultures.Length>0)
        {
            var doc=HistoryAtlas.Read(s,"people",0,world);check(doc.Sections.Any(x=>x.Title=="Language and names"&&x.Timeline.Length==6),"Culture atlas exposes language phonetics and meaningful root examples");
            int cell=s.Society.Cultures[0].OriginCell;
            var probe=s.Copy();var queue=new SimulationEventQueue(probe.Timeline.Events);var queueField=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.NonPublic|BindingFlags.Instance)!;
            queueField.SetValue(engine,queue);
            try{typeof(Prehistory).GetMethod("FoundAggregateCulture",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,new object[]{probe,engine.CellPatch[cell],cell,0});probe.Timeline.Events=queue.Ordered();}
            finally{queueField.SetValue(engine,null);}
            int child=probe.History.Cultures.Length-1;probe.Seal();engine.Validate(probe);
            check(probe.History.Languages.Profiles[child].FamilySeed==s.History.Languages.Profiles[0].FamilySeed,"New migrant cultures inherit a language at foundation, not after unrelated names are archived");
            check(probe.History.Archive.Last().Text.Contains(probe.Society.Cultures[child].Name),"Culture foundation records its final language-based name");
        }
        world.Prehistory=null;var fresh=new Prehistory(world);var prepared=fresh.Prepare();fresh.Validate(prepared);
        check(prepared.History.Version==Prehistory.LanguageHistoryVersion&&prepared.History.Languages.Profiles.Length==0,"Fresh worlds initialize the new language rules before humans form cultures");
    }
}
