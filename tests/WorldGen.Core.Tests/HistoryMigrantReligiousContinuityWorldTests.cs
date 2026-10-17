using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryMigrantReligiousContinuityWorldTests
{
    public static void Run(string source,string output)=>RunLoaded(WorldFile.Read(File.ReadAllBytes(source)),source,output);
    public static void RunLoaded(World world,string source,string output)
    {
        if(File.Exists(output))throw new IOException("Use a new migrant continuity output.");
        var original=world.Prehistory;string hash=original.ComputeHash();var a=original.History.Aggregates;
        var checks=new List<object>();void Check(bool pass,string message){checks.Add(new{Pass=pass,Message=message});if(!pass)throw new Exception(message);}
        int townId=Enumerable.Range(0,original.Society.Settlements.Length).FirstOrDefault(id=>{
            var town=original.Society.Settlements[id];if(town.Population<=0||a.Living.Cities[id].AbandonedYear>=0)return false;
            int faith=HistoricalCells.Religion(original,town.Cell);return faith>=0&&faith!=original.History.Cultures[town.CultureId].ReligionId;},-1);
        Check(townId>=0,"The actual saved world contains a living supplying town whose practiced publicfaith differs from its culture default.");
        var departure=original.Society.Settlements[townId];int parent=departure.CultureId;
        int region=Enumerable.Range(0,a.Regions.Length).FirstOrDefault(id=>a.Regions[id].Culture<0&&a.Regions[id].BestCell>=0&&HistoricalCells.Culture(original,a.Regions[id].BestCell)<0,-1);
        Check(region>=0,"The detached handler has an actual available anonymous regional site; no occupied town is overwritten.");
        int site=a.Regions[region].BestCell;
        var identity=Prehistory.MigrantReligiousContinuityAt(original,parent,site,departure.Cell,townId,0);
        Check(identity.Public==HistoricalCells.Religion(original,departure.Cell)&&identity.Public!=original.History.Cultures[parent].ReligionId&&!identity.CultureFallback,"The actual observed departure, not its culture default, determines carried public identity.");
        int remembered=a.Empires.Connections.Religion.Read(departure.Cell,5);
        Check(identity.Household==(remembered>=0?remembered:identity.Public),"The actual saved household memory is independently carried when present.");
        var s=original.Copy();s.History.Aggregates.Empires.Connections.Realms.Version=29;
        var engine=new Prehistory(world);var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,flags)!.Invoke(engine,args);
        Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);
        int religions=s.History.Religions.Length,cultures=s.History.Cultures.Length,towns=s.Society.Settlements.Length,courts=s.History.Polities.Length;
        var reaches=s.History.Religions.Select(r=>r.Reach).ToArray();long population=s.History.Aggregates.TotalPopulation;
        var raw=(long[])s.Population.Clone();var food=s.Society.Settlements.Select(t=>t.FoodReserve).ToArray();var treasury=s.History.Polities.Select(p=>p.Treasury).ToArray();
        var options=new JsonSerializerOptions{IncludeFields=true};
        string ExistingTechnical()=>JsonSerializer.Serialize(s.History.Cultures.Take(cultures).Select(c=>new{c.Technology,c.Research,c.Technical}),options);
        string technical=ExistingTechnical();
        string existingFaiths=JsonSerializer.Serialize(s.History.Religions,options);
        var queue=new SimulationEventQueue(s.Timeline.Events);var queueField=typeof(Prehistory).GetField("aggregateQueue",flags)!;queueField.SetValue(engine,queue);
        int child;
        try{child=(int)Call("FoundMigrantAggregateCulture",s,region,site,parent,departure.Cell,townId,0L);}finally{queueField.SetValue(engine,null);}
        var r=s.History.Aggregates.Empires.Connections.Religion;
        Check(child==cultures&&s.History.Cultures.Length==cultures+1,"The real migrant founder creates exactly one descendant culture.");
        Check(s.History.Cultures[child].ReligionId==identity.Public&&HistoricalCells.Religion(s,site)==identity.Public&&r.Read(site,5)==identity.Household,"The real full caller preserves actual publicfaithID and independent household memory after all crop/technical/language inheritance.");
        Check(s.History.Religions.Length==religions,"A descendant culture is not automatically a new sect; no orphan religious actor is allocated.");
        Check(reaches.SequenceEqual(s.History.Religions.Select(f=>f.Reach)),"Physical migration does not change existing faith vocations or promote them to missionary/portable.");
        Check(existingFaiths==JsonSerializer.Serialize(s.History.Religions,options),"Every existing faith retains its full name/profile/parent/holy-site/founding metadata; naming the child culture cannot rename its shared source faith.");
        Check(r.Read(site,4)==0&&r.Read(site,1)==0&&r.Read(site,9)==0,"The real founder grants neither a funded religious institution, conversion pressure nor a new institutional dispute.");
        Check(s.History.Aggregates.TotalPopulation==population&&raw.SequenceEqual(s.Population)&&towns==s.Society.Settlements.Length&&courts==s.History.Polities.Length,"The identity/founding fixture creates no people, town, court or sovereignty.");
        Check(food.SequenceEqual(s.Society.Settlements.Select(t=>t.FoodReserve))&&treasury.SequenceEqual(s.History.Polities.Select(p=>p.Treasury)),"Identity inheritance neither pays nor mints existing food reserves and court treasury.");
        Check(technical==ExistingTechnical(),"Every existing culture retains its complete technical knowledge/progress/budgets/deployment.");
        Check(original.ComputeHash()==hash,"The genuine source remains byte-for-byte unchanged after detached real-caller observation.");
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=Path.GetFullPath(source),Year=original.Years,Hash=hash,SourceRevision=a.Empires.Connections.Realms.Version,
            ActualDeparture=new{Town=townId,departure.Cell,ParentCulture=parent,CultureDefaultFaith=original.History.Cultures[parent].ReligionId,ObservedPublic=identity.Public,RememberedHousehold=remembered,CarriedHousehold=identity.Household,identity.CultureFallback},
            ControlledDetachedHandler=new{Region=region,Site=site,ChildCulture=child,ExplicitPriorResidents=0,ReligionsBefore=religions,ReligionsAfter=s.History.Religions.Length,Public=HistoricalCells.Religion(s,site),Household=r.Read(site,5),Institution=r.Read(site,4)},Checks=checks,
            Notes=new[]{"Departure rites and culture default are actual CURRENT saved values, not backdated to an earlier migration.","The target site is real but prior-resident0 is an explicit controlled vacant-site fixture; this is not a claim that the source checkpoint actually sent this expedition.","Only identity/founding is invoked on a detached copy. Real paid migration/food/cohort transfer gates remain in their original seven production callers.","No saved world is upgraded, advanced or written by this audit; no religious quota or retrospective lineage merge is applied."}},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Migrant religion actual-world "+checks.Count+" PASS, actual source town "+townId+" public "+identity.Public+" != culturedefault "+original.History.Cultures[parent].ReligionId+", original "+hash);
    }
}
