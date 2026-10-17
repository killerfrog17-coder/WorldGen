using System.Reflection;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HistoryIdentityTests
{
    static object Call(Prehistory e,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args)!;
    public static void Run(Action<bool,string> check)
    {
        var a=new HistoricalPeopleIdentity{Lineages=new[]{0},Weights=new[]{1000}};
        var b=new HistoricalPeopleIdentity{Lineages=new[]{0,1},Weights=new[]{600,400}};
        check(HistoricalIdentityEffects.SharedInheritance(a,a)==1000&&HistoricalIdentityEffects.SharedInheritance(a,b)==600,"Compiled shared social inheritance is bounded and graded.");
        for(int inclusion=0;inclusion<=1000;inclusion+=10)
        {
            double diff=HistoricalIdentityEffects.PoliticalGrievance(1,900,900,inclusion,500,0,0,false);
            check(diff>=0&&diff<=.18,"Identity grievance is bounded: "+inclusion);
            check(diff<=HistoricalIdentityEffects.PoliticalGrievance(1,900,900,0,500,0,0,false),"Inclusion never increases ethnic exclusion pressure: "+inclusion);
        }
        check(HistoricalIdentityEffects.PoliticalGrievance(1,1000,1000,1000,1000,0,0,false)==0,"Ethnic difference is not an intrinsic cause of rebellion under inclusive rule.");
        check(HistoricalIdentityEffects.PoliticalGrievance(0,900,900,100,100,800,600,true)>0,"Coethnics can resent actual violence and revoked local rights.");
        check(SpokenLanguageEffects.PracticedCompetence(0,1000,800,30)>750,"Unrelated languages are learnable to high competence with sustained practice.");
        check(SpokenLanguageEffects.PracticedCompetence(0,40,800,900)>SpokenLanguageEffects.PracticedCompetence(0,40,800,30),"Relatedness changes learning rate, not a permanent proficiency ceiling.");
        var r=new HistoricalIdentityHistory{Peoples=new[]{a},Places=new HistoricalCellTree(12),Regions=new HistoricalCellTree(12)};var c=r.Copy();c.Peoples[0].Weights[0]=1;c.Memories=c.Memories.With(0,2);
        check(r.Peoples[0].Weights[0]==1000&&r.Memories[0]==-1,"Published social inheritance and immutable memory pages detach safely.");
    }
    public static void World(string path,string output,Action<bool,string> check)
    {
        Directory.CreateDirectory(output);var world=WorldFile.Read(File.ReadAllBytes(path));var old=world.Prehistory;string before=old.ComputeHash();var e=new Prehistory(world);
        var s=e.UpgradeRealmLifecycles(old);e.Validate(s);var r=s.History.Identities;
        check(r!=null&&s.History.Aggregates.Empires.Connections.Realms.Version>=33,"Explicit upgrade activates saved ethnic identity and local memory.");
        check(old.ComputeHash()==before&&old.History.Identities==null,"Old rules remain unmodified and do not silently acquire ethnic identities.");
        check(s.History.Aggregates.Cells.Fingerprint()==old.History.Aggregates.Cells.Fingerprint()&&s.History.Aggregates.TotalPopulation==old.History.Aggregates.TotalPopulation&&StateHash.Compute(s.History.Languages)==StateHash.Compute(old.History.Languages),"Upgrade creates no people, territory, language shift or religious conversion.");
        check(ReferenceEquals(e.UpgradeRealmLifecycles(s),s),"Identity upgrade is idempotent.");
        var town=s.Society.Settlements.First(t=>t.Population>=600&&s.History.Aggregates.Living.Cities[t.Id].AbandonedYear<0&&Prehistory.PrimaryEthnicityAt(s,t.Cell)>=0);
        int cell=town.Cell,native=Prehistory.PrimaryEthnicityAt(s,cell);
        var donor=s.Society.Settlements.First(t=>t.Population>0&&s.History.Aggregates.Living.Cities[t.Id].AbandonedYear<0&&Prehistory.PrimaryEthnicityAt(s,t.Cell)>=0&&Prehistory.PrimaryEthnicityAt(s,t.Cell)!=native);
        int incoming=Prehistory.PrimaryEthnicityAt(s,donor.Cell);var probe=s.Copy();Call(e,"InheritEthnicHouseholds",probe,cell,donor.Cell,900L,100L);
        var mixed=Prehistory.EthnicCommunityAt(probe,cell);
        check(mixed.Primary==native&&mixed.Minority==incoming&&mixed.MinorityPermille==100,"Actual migrant/resident proportions retain a local minority without overwriting the host identity.");
        check(probe.History.Aggregates.TotalPopulation==s.History.Aggregates.TotalPopulation&&probe.History.Aggregates.Cells.Fingerprint()==s.History.Aggregates.Cells.Fingerprint(),"Social membership estimates do not mint people or transfer sovereignty.");
        var regional=probe.Copy();var ethnicFingerprint=regional.History.Identities.Places.Fingerprint();var spokenFingerprint=regional.History.Languages.Spoken.Places.Fingerprint();regional.Years+=100;
        Call(e,"AdvanceEthnicCommunity",regional,cell,true);Call(e,"AdvanceSpokenCommunity",regional,cell,true);
        check(regional.History.Identities.Places.Fingerprint()==ethnicFingerprint&&regional.History.Languages.Spoken.Places.Fingerprint()==spokenFingerprint,"Regional clocks cannot overwrite the distinct identities or languages of a town located at their representative point.");
        var visited=s.Copy();Call(e,"RecordIdentityContact",visited,donor.Cell,cell);check(Prehistory.EthnicCommunityAt(visited,cell).Minority<0,"Foreign visitors alone do not create resident ethnic minorities.");
        var shifted=probe.Copy();var speech=shifted.History.Languages.Spoken;int localVoice=Prehistory.PrimarySpokenLanguageAt(shifted,cell),otherVoice=Enumerable.Range(0,speech.Varieties.Length).First(x=>x!=localVoice);
        speech.Places=speech.Places.WithBlock(cell*12,new[]{otherVoice,-1,0,-1,0,-1,shifted.Years,shifted.Years,0,-1,-1,-1});shifted.Years+=40;Call(e,"AdvanceEthnicCommunity",shifted,cell,false);
        check(Prehistory.PrimaryEthnicityAt(shifted,cell)==native,"A language change does not instantly change ethnic identity.");
        int owner=HistoricalCells.Polity(probe,cell);int opponent=probe.History.Polities.First(p=>p.EndedYear<0&&p.Id!=owner&&p.CapitalId!=town.Id).Id;
        Call(e,"RecordIdentityGovernmentTransfer",probe,town.Id,owner,opponent);var memory=Prehistory.LocalHistoricalMemoryAt(probe,town.Id);
        check(memory.Harm==0&&Prehistory.PrimaryEthnicityAt(probe,cell)==native,"A routine government transfer invents neither ethnic replacement nor violence.");
        Call(e,"RecordIdentityViolence",probe,town.Id,opponent,350);memory=Prehistory.LocalHistoricalMemoryAt(probe,town.Id);
        check(memory.WrongingState==opponent&&memory.Harm==350&&memory.RelatedTown==probe.History.Polities[opponent].CapitalId,"Actual capture memory attributes harm to a specific court and attacking center.");
        var local=probe.History.Aggregates.Communities.Cities[town.Id];local.InclusionMilli=100;double excluded=(double)Call(e,"LocalIdentityPoliticalGrievance",probe,town.Id,opponent,1.0);local.InclusionMilli=1000;
        double included=(double)Call(e,"LocalIdentityPoliticalGrievance",probe,town.Id,opponent,1.0);
        check(excluded>included,"Enforced exclusion, not ethnic difference alone, drives stronger local political grievance.");
        int harm=memory.Harm;probe.Years+=100;Call(e,"AdvanceLocalHistoricalMemory",probe,town.Id);check(Prehistory.LocalHistoricalMemoryAt(probe,town.Id).Harm<harm,"Inclusive generations permit attributed grievance to fade rather than permanent inherited hatred.");
        var elite=s.Copy();int ruler=elite.History.Polities.First(p=>p.EndedYear<0).Id,capital=elite.Society.Settlements[elite.History.Polities[ruler].CapitalId].Cell;int court=elite.History.Identities.RulingIdentities[ruler];
        elite.History.Identities.Places=elite.History.Identities.Places.WithBlock(capital*12,new[]{incoming,-1,0,700,700,elite.Years,elite.Years,-1,-1,0,0,0});
        check(elite.History.Identities.RulingIdentities[ruler]==court,"A capital's resident identity does not overwrite the ruling elite's independent heritage.");
        var fusion=s.Copy();var fr=fusion.History.Identities;int count=fr.Peoples.Length;var fa=fusion.History.Aggregates;
        fr.Places=fr.Places.WithBlock(cell*12,new[]{native,incoming,300,800,800,fusion.Years,fusion.Years,incoming,fusion.Years,900,300,0});
        fa.Cities[town.Id].Supported=true;fa.Communities.Cities[town.Id].InclusionMilli=1000;
        var fs=fusion.History.Languages.Spoken;fs.Places=fs.Places.WithBlock(cell*12,new[]{localVoice,otherVoice,300,otherVoice,800,-1,fusion.Years,fusion.Years,0,-1,-1,-1});
        for(int trial=0;trial<300&&fr.Peoples.Length==count;trial++){fusion.Years+=40;Call(e,"AdvanceEthnicCommunity",fusion,cell,false);}
        int child=Prehistory.PrimaryEthnicityAt(fusion,cell);
        check(fr.Peoples.Length==count+1&&fr.Peoples[child].Parent==native&&fr.Peoples[child].OtherParent==incoming&&fr.Peoples[child].Weights.Sum()==1000,"Sustained actual mixed residence and bilingual interaction can create a traced new local identity.");
        check(fr.Peoples[native].Name==r.Peoples[native].Name&&fusion.History.Aggregates.Cells.Fingerprint()==s.History.Aggregates.Cells.Fingerprint(),"Ethnogenesis neither rewrites its parent identities nor grants new states.");
        var display=HistoricalMapSnapshot.For(probe);
        check(display.OverlayId(cell,HistoryOverlay.Ethnicities)==SocietyView.SelectionId(probe,cell,HistoryOverlay.Ethnicities)&&HistoryAtlas.Selection(probe,cell,HistoryOverlay.Ethnicities)?.Kind=="ethnicity","Literal/cached ethnic map, selection and document use the same group identity.");
        string readHash=probe.ComputeHash();var card=HistoryAtlas.Card(probe,cell,HistoryOverlay.Ethnicities);var doc=HistoryAtlas.Read(probe,"ethnicity",native,world);var cityDoc=HistoryAtlas.Read(probe,"city",town.Id,world);
        check(card.Facts.Any(f=>f.Label=="Resident minority")&&doc.Sections.Any(x=>x.Title=="Inherited traditions")&&cityDoc.Sections.Any(x=>x.Title=="Local historical memory")&&probe.ComputeHash()==readHash,"Ethnic minority cards, inherited-tradition charts and local history articles are available and read-only.");
        var copy=s.Copy();copy.History.Identities.Peoples[0].Weights[0]=1;check(s.History.Identities.Peoples[0].Weights[0]==1000&&copy.ComputeHash()!=s.ComputeHash(),"Social inheritance is detached and protected by integrity hashes.");
        var full=e.AdvanceYears(s,80);var split=e.AdvanceYears(e.AdvanceYears(s,31),49);e.Validate(full);check(full.Hash==split.Hash,"Actual80-year identity/political/contact events replay across31+49-year partition.");
        world.Prehistory=full;var restored=WorldFile.Read(WorldFile.Write(world));var cold=new Prehistory(restored);check(restored.Prehistory.Hash==full.Hash&&cold.AdvanceYears(restored.Prehistory,12).Hash==e.AdvanceYears(full,12).Hash,"Cold binary save/load retains identities, town memories and future politics.");
        var json=WorldJson.Read<HistoricalIdentityHistory>(WorldJson.Write(full.History.Identities));check(StateHash.Compute(json)==StateHash.Compute(full.History.Identities),"Identity/memory extension roundtrips shared JSON.");
        var fresh=e.Start(e.Prepare());e.Validate(fresh);check(fresh.History.Identities!=null,"Fresh histories enable the new identity rules.");
        var first=fresh.Copy();int site=Array.FindIndex(e.CellPatch,x=>x==fresh.Origin);var queue=new SimulationEventQueue(first.Timeline.Events);var field=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;
        field.SetValue(e,queue);try{Call(e,"FoundAggregateCulture",first,fresh.Origin,site,-1);}finally{field.SetValue(e,null);}first.Timeline.Events=queue.Ordered();first.Seal();e.Validate(first);
        check(first.History.Identities.Peoples.Length==1&&Prehistory.PrimaryEthnicityAt(first,site)==0,"Actual first culture registration initializes shared local heritage.");
        File.WriteAllBytes(Path.Combine(output,"review.world"),WorldFile.Write(world));File.WriteAllText(Path.Combine(output,"expected12.hash"),e.AdvanceYears(full,12).Hash);File.WriteAllText(Path.Combine(output,"legacy12.hash"),e.AdvanceYears(old,12).Hash);
        check(old.ComputeHash()==before,"All identity review work preserves the original unupgraded world.");
        File.WriteAllText(Path.Combine(output,"result.txt"),"PASS identity/memory causal, migration, fusion, sovereignty/elite separation,80-year partition, binary/JSON/cold continuation. "+full.Hash);
    }
}
