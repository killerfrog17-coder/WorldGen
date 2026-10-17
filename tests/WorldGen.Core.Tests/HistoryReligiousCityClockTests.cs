using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryReligiousCityClockTests
{
    static object Call(Prehistory e,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args);
    public static void World(string path,Action<bool,string> check,string output=null)
    {
        if(output!=null&&Directory.Exists(output))throw new ArgumentException("Use a new religious-clock diagnostic directory.");
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;var engine=new Prehistory(world);engine.Validate(source);string original=source.ComputeHash();
        var s=engine.UpgradeRealmLifecycles(source);engine.Validate(s);var a=s.History.Aggregates;var r=a.Empires.Connections.Religion;
        check(a.Empires.Connections.Realms.Version>=8,"Only explicit realm8 upgrade enables the local city religious clock.");
        var towns=s.Society.Settlements.Where(t=>t.Population>0&&a.Living.Cities[t.Id].AbandonedYear<0&&HistoricalCells.Religion(s,t.Cell)>=0).ToArray();
        var family=towns.Where(t=>t.Population>=600).GroupBy(t=>t.CultureId).OrderByDescending(g=>g.Count()).First().Take(12).ToArray();
        check(family.Length>=4,"The saved fixture contains an actual multi-city culture, not duplicated invented settlements.");
        check(towns.All(t=>r.Read(t.Cell,2)==s.Years),"Explicit upgrade rebases existing live-town review dates rather than charging centuries of sampled-clock arrears.");
        check(source.History.Religions.Length==s.History.Religions.Length&&StateHash.Compute(source.History.Religions)==StateHash.Compute(s.History.Religions)&&source.History.Aggregates.TotalPopulation==a.TotalPopulation,"Clock migration preserves religious identities, histories and all residents.");
        check(source.Society.Settlements.Select(t=>t.FoodReserve).SequenceEqual(s.Society.Settlements.Select(t=>t.FoodReserve))&&source.History.Polities.Select(p=>p.Treasury).SequenceEqual(s.History.Polities.Select(p=>p.Treasury)),"Clock migration debits neither food nor treasury.");
        check(towns.All(t=>Prehistory.ReligiousCommunityAt(source,t.Cell).Household==Prehistory.ReligiousCommunityAt(s,t.Cell).Household&&Prehistory.ReligiousCommunityAt(source,t.Cell).Institution==Prehistory.ReligiousCommunityAt(s,t.Cell).Institution),"Rebasing preserves existing household memories and provisioned institutional support.");
        check(ReferenceEquals(engine.UpgradeRealmLifecycles(s),s),"Repeated explicit upgrade cannot keep resetting already enabled city clocks.");
        var probe=s.Copy();var pa=probe.History.Aggregates;var pr=pa.Empires.Connections.Religion;var before=new Dictionary<int,long>();
        foreach(var originalTown in family)
        {
            var t=probe.Society.Settlements[originalTown.Id];t.FoodReserve=t.Population*3;pa.Cities[t.Id].Supported=true;
            pr.Write(t.Cell,2,probe.Years);pr.Write(t.Cell,4,0);pr.Write(t.Cell,0,-1);pr.Write(t.Cell,1,0);pr.Write(t.Cell,6,-1);pr.Write(t.Cell,7,-1);pr.Write(t.Cell,8,0);pr.Write(t.Cell,9,0);pr.Write(t.Cell,10,-1);
            int owner=HistoricalCells.Polity(probe,t.Cell);if(owner>=0)pr.Policies=pr.Policies.With(owner*4,probe.History.Dynamics.States[owner].LeaderId).With(owner*4+1,0).With(owner*4+2,probe.Years);
            before[t.Id]=t.FoodReserve;
        }
        long people=pa.TotalPopulation,treasury=probe.History.Polities.Sum(p=>p.Treasury);int start=probe.Years;
        probe.Years=start+9;foreach(var t in family){Call(engine,"AdvanceLocalReligiousCity",probe,t.Id);Call(engine,"AdvanceReligiousCommunity",probe,t.CultureId,t.Cell,t.Id);}
        check(family.All(t=>probe.Society.Settlements[t.Id].FoodReserve==before[t.Id]&&pr.Read(t.Cell,2)==start),"Both city and culture/contact callers respect the same saved ten-year guard.");
        probe.Years=start+10;foreach(var t in family)Call(engine,"AdvanceLocalReligiousCity",probe,t.Id);
        check(family.All(t=>pr.Read(t.Cell,2)==probe.Years&&pr.Read(t.Cell,4)>0),"Every existing town in the multi-city culture receives its own due institution review, independent of culture sampling.");
        check(family.All(t=>before[t.Id]-probe.Society.Settlements[t.Id].FoodReserve==HistoricalReligiousNetworkEffects.FoodCost(t.Population,10)),"Each supported institution pays exactly its real ten-year provisions, without arbitrary retrospective charges.");
        var paid=family.ToDictionary(t=>t.Id,t=>probe.Society.Settlements[t.Id].FoodReserve);
        foreach(var t in family){Call(engine,"AdvanceLocalReligiousCity",probe,t.Id);Call(engine,"AdvanceReligiousCommunity",probe,t.CultureId,t.Cell,t.Id);}
        check(family.All(t=>probe.Society.Settlements[t.Id].FoodReserve==paid[t.Id]),"A same-year contact or culture review cannot double-charge a city's existing upkeep.");
        probe.Years=start+19;foreach(var t in family)Call(engine,"AdvanceLocalReligiousCity",probe,t.Id);
        check(family.All(t=>probe.Society.Settlements[t.Id].FoodReserve==paid[t.Id]&&pr.Read(t.Cell,2)==start+10),"Nine years since the last paid review accumulate without another invoice.");
        probe.Years=start+20;foreach(var t in family)Call(engine,"AdvanceLocalReligiousCity",probe,t.Id);
        check(family.All(t=>before[t.Id]-probe.Society.Settlements[t.Id].FoodReserve==2*HistoricalReligiousNetworkEffects.FoodCost(t.Population,10))&&pa.TotalPopulation==people&&probe.History.Polities.Sum(p=>p.Treasury)==treasury,"Two genuine upkeep intervals conserve residents and treasury while debiting precisely their existing food budgets.");
        var old=s.Copy();old.History.Aggregates.Empires.Connections.Realms.Version=7;string untouched=old.History.Aggregates.Empires.Connections.Religion.Communities.Fingerprint();Call(engine,"AdvanceLocalReligiousCity",old,family[0].Id);Call(engine,"InitializeLocalReligiousCityClocks",old);
        check(old.History.Aggregates.Empires.Connections.Religion.Communities.Fingerprint()==untouched,"Realm7 and earlier never acquire the new local upkeep or migration behavior implicitly.");
        var rural=s.Copy();int ruralCell=rural.Society.Cultures.Select(c=>c.OriginCell).First(cell=>rural.History.Aggregates.TownAt[cell]<0&&HistoricalCells.Culture(rural,cell)>=0);
        rural.History.Aggregates.Empires.Connections.Religion.Write(ruralCell,2,rural.Years);rural.Years++;
        Call(engine,"AdvanceReligiousCommunity",rural,HistoricalCells.Culture(rural,ruralCell),ruralCell,-1);
        check(rural.History.Aggregates.Empires.Connections.Religion.Read(ruralCell,2)==rural.Years,"A genuine rural community retains its actual-contact review cadence; the ten-year institution guard applies only to towns.");
        var abandoned=s.Copy();int former=family[0].Id;abandoned.Years+=20;abandoned.History.Aggregates.Living.Cities[former].AbandonedYear=abandoned.Years;
        string ghost=abandoned.History.Aggregates.Empires.Connections.Religion.Communities.Fingerprint();Call(engine,"AdvanceLocalReligiousCity",abandoned,former);
        check(abandoned.History.Aggregates.Empires.Connections.Religion.Communities.Fingerprint()==ghost,"An abandoned town cannot pay for or sustain a phantom local institution.");
        world.Prehistory=s;var restoredWorld=WorldFile.Read(WorldFile.Write(world));var restored=restoredWorld.Prehistory;var cold=new Prehistory(restoredWorld);cold.Validate(restored);
        check(restored.Hash==s.Hash&&towns.All(t=>restored.History.Aggregates.Empires.Connections.Religion.Read(t.Cell,2)==s.Years),"Full-world serialization retains the prospective saved local review dates.");
        var full=engine.AdvanceYears(s,12);var split=cold.AdvanceYears(cold.AdvanceYears(restored,5),7);engine.Validate(full);
        check(full.Hash==split.Hash,"Actual12-year city-calendar continuation equals cold save/load5+7-year partitioning.");
        var fa=full.History.Aggregates;var fr=fa.Empires.Connections.Religion;var survivors=full.Society.Settlements.Where(t=>t.Population>0&&fa.Living.Cities[t.Id].AbandonedYear<0&&HistoricalCells.Religion(full,t.Cell)>=0).ToArray();
        int max=survivors.Max(t=>full.Years-fr.Read(t.Cell,2));
        check(max<=12,"Existing annual city events bound every surviving local religious review age to at most12 years, including large cultures.");
        check(survivors.Where(t=>t.Id<s.Society.Settlements.Length&&t.FoundedYear<=s.Years).All(t=>fr.Read(t.Cell,2)>s.Years),"Every surviving pre-existing town actually completes a new due review; the twelve-year bound is not merely the rebased initial date.");
        check(source.ComputeHash()==original,"Local-clock tests, explicit migration and continuations never mutate their source checkpoint.");
        if(output!=null)
        {
            Directory.CreateDirectory(output);
            var rows=survivors.Select(t=>new{t.Id,t.Name,t.CultureId,t.Population,OriginalTown=t.Id<source.Society.Settlements.Length,BeforeReviewAge=t.Id<source.Society.Settlements.Length?source.Years-source.History.Aggregates.Empires.Connections.Religion.Read(t.Cell,2):-1,AfterReviewAge=full.Years-fr.Read(t.Cell,2),BeforeInstitution=t.Id<source.Society.Settlements.Length?source.History.Aggregates.Empires.Connections.Religion.Read(t.Cell,4):-1,AfterInstitution=fr.Read(t.Cell,4)}).ToArray();
            File.WriteAllText(Path.Combine(output,"review-ages.json"),JsonSerializer.Serialize(new{Source=Path.GetFullPath(path),SourceHash=original,Start=s.Years,End=full.Years,EndHash=full.Hash,Rules=fa.Empires.Connections.Realms.Version,MaxReviewAge=max,Towns=rows,Notes="Actual twelve-year saved-world continuation after explicit revision8 upgrade; not a fresh long-world acceptance run or an isolated religious intervention (other revision8 rules also apply). Before ages are the source's old clock, not the migration-rebased dates."},new JsonSerializerOptions{WriteIndented=true}));
            int count=HistoricalCells.Count(full);var publicMap=Enumerable.Range(0,count).Select(cell=>HistoricalCells.Religion(full,cell)).ToArray();var householdMap=Enumerable.Range(0,count).Select(cell=>fr.Read(cell,5)).ToArray();
            var legend=survivors.GroupBy(t=>publicMap[t.Cell]).OrderByDescending(g=>g.Count()).Take(12).Select(g=>$"{g.Key}: {full.History.Religions[g.Key].Name} / {full.History.Religions[g.Key].Reach} / {g.Count()} towns").ToArray();
            HistoryReligionEvidenceAudit.Draw(world,full,publicMap,Path.Combine(output,"public.svg"),"Actual revision8 public predominance after12 years",legend);
            HistoryReligionEvidenceAudit.Draw(world,full,householdMap,Path.Combine(output,"household.svg"),"Actual revision8 household memories; grey = unobserved",legend);
        }
        Console.WriteLine($"Religious city clocks: {towns.Length} actual source towns; multi-city fixture {family.Length}; max surviving review age {max} after12 actual years; source unchanged.");
    }
}
