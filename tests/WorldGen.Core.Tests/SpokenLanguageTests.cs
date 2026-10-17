using System.Reflection;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class SpokenLanguageTests
{
    static object Call(Prehistory e,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args)!;
    public static void Run(Action<bool,string> check)
    {
        for(int i=0;i<100;i++)
        {
            var p=LanguageGenerator.Create(183,i,0);var q=LanguageGenerator.Create(183,i+101,500,p);
            check(SpokenLanguageEffects.Intelligibility(p,p)==1000,"Self intelligibility: "+i);
            check(SpokenLanguageEffects.Intelligibility(p,q)>SpokenLanguageEffects.Intelligibility(p,LanguageGenerator.Create(931,i+201,0)),"Inherited sounds are closer than unrelated speech: "+i);
            check(SpokenLanguageEffects.Competence(0,40,650,900)>SpokenLanguageEffects.Competence(0,40,650,30),"Related speech is easier to learn: "+i);
        }
        check(SpokenLanguageEffects.Competence(800,100,0,1000)<800,"Unused second-language competence decays.");
        check(SpokenLanguageEffects.HomeShift(0,100,1000,0)==0,"No home shift without bilingual transmission.");
        check(SpokenLanguageEffects.HomeShift(900,100,1000,1000)<SpokenLanguageEffects.HomeShift(900,100,1000,0),"Protected home transmission slows replacement.");
        check(SpokenLanguageEffects.MigrantShare(900,100)==100&&SpokenLanguageEffects.MigrantShare(0,10)==1000,"Home shares use actual resident/migrant ratios.");
        var legacy=new HistoricalLanguages{Profiles=new[]{LanguageGenerator.Create(23,0,0)}};
        check(legacy.Copy().Spoken==null,"Copying an old language record does not silently enable speech rules.");
        var extended=legacy.Copy();extended.Spoken=new HistoricalSpokenLanguages{Varieties=new[]{new HistoricalSpeechVariety{Sounds=legacy.Profiles[0].Copy()}},Places=new HistoricalCellTree(12),Regions=new HistoricalCellTree(12)};
        var fork=extended.Copy();fork.Spoken.Varieties[0].Sounds.SoundMap[0]=9;fork.Spoken.Places=fork.Spoken.Places.With(0,0);
        check(extended.Spoken.Varieties[0].Sounds.SoundMap[0]==0&&extended.Spoken.Places[0]==-1,"Spoken profiles detach; persistent community pages remain immutable.");
    }
    public static void World(string path,string output,Action<bool,string> check)
    {
        Directory.CreateDirectory(output);var world=WorldFile.Read(File.ReadAllBytes(path));var e=new Prehistory(world);var old=world.Prehistory;string oldHash=old.ComputeHash();
        var s=e.UpgradeRealmLifecycles(old);e.Validate(s);
        File.WriteAllText(Path.Combine(output,"legacy12.hash"),e.AdvanceYears(old,12).Hash);
        check(s.History.Aggregates.Empires.Connections.Realms.Version>=32&&s.History.Languages.Spoken!=null,"Explicit upgrade activates local language communities.");
        check(old.ComputeHash()==oldHash&&s.History.Aggregates.Cells.Fingerprint()==old.History.Aggregates.Cells.Fingerprint()&&s.History.Aggregates.TotalPopulation==old.History.Aggregates.TotalPopulation,"Upgrade preserves its source, people and political/faith layers.");
        check(ReferenceEquals(s,e.UpgradeRealmLifecycles(s)),"Language upgrade is idempotent.");
        var town=s.Society.Settlements.First(t=>t.Population>100&&s.History.Aggregates.Living.Cities[t.Id].AbandonedYear<0&&Prehistory.SpokenCommunityAt(s,t.Cell)!=null);int cell=town.Cell;
        var p=Prehistory.SpokenCommunityAt(s,cell);var registry=s.History.Languages.Spoken;
        int rival=Enumerable.Range(0,registry.Varieties.Length).First(i=>i!=p.Primary);
        int donorCell=registry.Varieties[rival].Cell;
        // Controlled actual-world communities: an explicit migrant transaction,
        // not a claim that these particular households migrated in the archive.
        var probe=s.Copy();var r=probe.History.Languages.Spoken;
        r.Places=r.Places.WithBlock(donorCell*12,new[]{rival,-1,0,-1,0,-1,probe.Years,probe.Years,0,-1,-1,-1});
        Call(e,"InheritSpokenHouseholds",probe,cell,donorCell,900L,100L);
        var mixed=Prehistory.SpokenCommunityAt(probe,cell);
        check(mixed.Primary==p.Primary&&mixed.Minority==rival&&mixed.MinorityPermille==100,"Physical migrants create a minority without instantly converting resident speech.");
        check(probe.History.Aggregates.TotalPopulation==s.History.Aggregates.TotalPopulation&&probe.History.Aggregates.Cells.Fingerprint()==s.History.Aggregates.Cells.Fingerprint(),"Speech accounting creates no people, borders or religious conversion.");
        var untouched=s.Copy();Call(e,"RecordSpokenContact",untouched,donorCell,cell);var visit=Prehistory.SpokenCommunityAt(untouched,cell);
        check(visit.Minority<0&&visit.MinorityPermille==0,"Contact by itself is not a resident home-language minority.");
        var learning=probe.Copy();learning.Years+=40;Call(e,"AdvanceSpokenCommunity",learning,cell,false);var learned=Prehistory.SpokenCommunityAt(learning,cell);
        check(learned.BilingualPermille>mixed.BilingualPermille,"Actual continuing contact produces learned bilingual competence.");
        var rural=probe.Copy();rural.History.Aggregates.TownAt=rural.History.Aggregates.TownAt.With(cell,-1);rural.History.Aggregates.Cells=rural.History.Aggregates.Cells.With(cell*5+1,-1);rural.Years+=100;Call(e,"AdvanceSpokenCommunity",rural,cell,false);
        check(Prehistory.SpokenCommunityAt(rural,cell).BilingualPermille>0,"A hundred-year rural clock accounts for its80 active contact years rather than forgetting them at the endpoint.");
        int native=mixed.Primary;check(learned.Primary==native,"One contact generation does not erase the local home language.");
        var partition=probe.Copy();partition.Years+=40;Call(e,"AdvanceSpokenCommunity",partition,cell,false);string record=partition.History.Languages.Spoken.Places.Fingerprint();Call(e,"AdvanceSpokenCommunity",partition,cell,false);
        check(partition.History.Languages.Spoken.Places.Fingerprint()==record,"Duplicate same-year local callers do not do a second language update.");
        var isolated=s.Copy();var ir=isolated.History.Languages.Spoken;int beforeVarieties=ir.Varieties.Length;
        isolated.History.Aggregates.Cells=isolated.History.Aggregates.Cells.With(cell*5+1,-1);isolated.History.Aggregates.TownAt=isolated.History.Aggregates.TownAt.With(cell,-1);
        for(int trial=0;trial<12&&ir.Varieties.Length==beforeVarieties;trial++){isolated.Years+=6000;Call(e,"AdvanceSpokenCommunity",isolated,cell,false);}
        var dialect=Prehistory.SpokenCommunityAt(isolated,cell);
        check(ir.Varieties.Length==beforeVarieties+1&&ir.Varieties[dialect.Primary].Parent==native&&ir.Varieties[dialect.Primary].Sounds.FamilySeed==ir.Varieties[native].Sounds.FamilySeed,"A controlled isolated community develops a related local variety, not an unrelated random language.");
        check(s.History.Languages.Spoken.Varieties.Length==beforeVarieties,"Local divergence does not mutate the source snapshot or all speakers of the parent language.");
        // Religious plurality is represented by distinct resident roles, not by
        // inventing percentages from doctrinal influence or visiting teachers.
        var nr=probe.History.Aggregates.Empires.Connections.Religion;int faith=HistoricalCells.Religion(probe,cell),other=Enumerable.Range(0,probe.History.Religions.Length).First(i=>i!=faith);
        nr.Write(cell,5,other);nr.Write(cell,6,other);nr.Write(cell,7,probe.Years);
        var presence=Prehistory.ReligiousPresenceAt(probe,cell);
        check(presence.Any(x=>x.Faith==faith&&x.Public)&&presence.Any(x=>x.Faith==other&&x.Household&&x.ResidentEvidence),"Public religion and different household denominations coexist locally.");
        nr.Write(cell,5,faith);presence=Prehistory.ReligiousPresenceAt(probe,cell);
        check(presence.Any(x=>x.Faith==other&&x.RecentContact&&!x.ResidentEvidence),"Visiting religion alone is not called an established resident denomination.");
        int layKey=cell*HistoricalLayAssociations.Fields;
        nr.LayAssociations.Records=nr.LayAssociations.Records.WithBlock(layKey,new[]{other,700,probe.Years,probe.Years,town.Id,0,0,probe.Years});nr.Write(cell,4,700);
        Call(e,"AdvanceStructuredReligiousInstitutions",probe,cell,town.Id,-1,0,false);
        presence=Prehistory.ReligiousPresenceAt(probe,cell);
        check(presence.Any(x=>x.Faith==other&&x.Association&&x.Institution&&x.ResidentEvidence)&&Prehistory.ReligiousInstitutionAt(probe,cell).Faith!=Prehistory.ReligiousInstitutionAt(probe,cell,1).Faith,"One actual town can maintain distinct public and lay denominational institutions without a uniform cell religion.");
        var display=HistoricalMapSnapshot.For(probe);
        check(SocietyView.SelectionId(probe,cell,HistoryOverlay.Languages)==display.OverlayId(cell,HistoryOverlay.Languages)&&HistoryAtlas.Selection(probe,cell,HistoryOverlay.Languages)?.Kind=="language","Language palette, cached selection and article agree on actual speech rather than state/culture.");
        string readHash=probe.ComputeHash();var card=HistoryAtlas.Card(probe,cell,HistoryOverlay.Languages);var doc=HistoryAtlas.Read(probe,"language",native,world);var local=HistoryAtlas.SpokenLocalSection(probe,cell);
        check(card.Facts.Any(f=>f.Label=="Resident minority")&&doc.Sections.Any(x=>x.Title=="Sounds and reusable roots")&&local.Links.Length==2&&probe.ComputeHash()==readHash,"Language cards, sounds, ancestry and local mix are present and read-only.");
        var full=e.AdvanceYears(s,60);var split=e.AdvanceYears(e.AdvanceYears(s,23),37);e.Validate(full);check(full.Hash==split.Hash,"Local language/religion clocks replay identically across publication partitions.");
        world.Prehistory=full;var restored=WorldFile.Read(WorldFile.Write(world));var cold=new Prehistory(restored);check(restored.Prehistory.Hash==full.Hash&&cold.AdvanceYears(restored.Prehistory,12).Hash==e.AdvanceYears(full,12).Hash,"Cold binary world continuation retains local speech and religious plurality.");
        var json=WorldJson.Read<HistoricalSpokenLanguages>(WorldJson.Write(full.History.Languages.Spoken));check(StateHash.Compute(json)==StateHash.Compute(full.History.Languages.Spoken),"Optional language geography roundtrips JSON.");
        var fresh=e.Start(e.Prepare());e.Validate(fresh);check(fresh.History.Languages.Spoken!=null,"A fresh world enables the new speech rules.");
        var first=fresh.Copy();int site=Array.FindIndex(e.CellPatch,x=>x==fresh.Origin);var queue=new SimulationEventQueue(first.Timeline.Events);var q=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;
        q.SetValue(e,queue);try{Call(e,"FoundAggregateCulture",first,fresh.Origin,site,-1);}finally{q.SetValue(e,null);}first.Timeline.Events=queue.Ordered();first.Seal();e.Validate(first);
        check(first.History.Languages.Spoken.Varieties.Length==1&&Prehistory.PrimarySpokenLanguageAt(first,site)==0,"A newly registered first culture receives a real speech community and sound profile.");
        check(old.ComputeHash()==oldHash,"Review never mutates the source save.");
        File.WriteAllBytes(Path.Combine(output,"review.world"),WorldFile.Write(world));File.WriteAllText(Path.Combine(output,"expected12.hash"),e.AdvanceYears(full,12).Hash);
        File.WriteAllText(Path.Combine(output,"result.txt"),"PASS spoken communities, minority migration, bilingual contact, religious plurality, literal/cached focus,60-year partition, binary/JSON/cold replay. "+full.Hash);
    }
}
