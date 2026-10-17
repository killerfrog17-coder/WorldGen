using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Current saved-world queries plus an explicitly controlled detached handler
// fixture. No random draw is replaced inside Core and no world is saved.
public static class HistoryContactCommunityRecognitionWorldTests
{
    public static void Run(string source,string output)=>RunLoaded(WorldFile.Read(File.ReadAllBytes(source)),source,output);
    public static void RunLoaded(World world,string source,string output)
    {
        if(File.Exists(output))throw new IOException("Use a new recognition audit output.");
        var original=world.Prehistory;string before=original.ComputeHash();var s=original.Copy();var a=s.History.Aggregates;
        var engine=new Prehistory(world);var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,flags)!.Invoke(engine,args);
        Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);
        long reads=a.CellReads,writes=a.CellWrites,events=a.ProcessedLocalEvents;int recordedRevision=a.Empires.Connections.Realms.Version;
        var checks=new List<object>();var rows=new List<object>();var positives=new List<(int Region,double Old,double New)>();
        void Check(bool pass,string message){checks.Add(new{Pass=pass,Message=message});if(!pass)throw new Exception(message);}
        Check(s.Hash==before,"The detached probe starts from the same saved checkpoint.");
        // These anchors are the actual AA26 Earth fields previously exported,
        // not IDs selected from a different evolved history.
        Check(before=="153e69ae9800322c28105ba0e355affa921ab673360387c65af524ec5d1df096","This recorded-case audit uses the exact AA26 final Earth source, not backdated or substituted geography.");
        foreach(int region in new[]{1457,1651,1734,1921,1771})
        {
            var r=a.Regions[region];var local=a.Living.Regions[region];long readBaseline=a.CellReads;
            int currentCourt=(int)Call("ProvincialRegionalCourt",s,region),teacher=currentCourt>=0?currentCourt:local.CivicTeacher;
            double remembered=local.CivicTeacher>=0?(double)Call("InstitutionalRegionalExample",s,region,local.CivicTeacher):0;
            double civic=teacher>=0?(double)Call("InstitutionalRegionalExample",s,region,teacher):0;
            long mask=a.Provinces.RegionalKnowledge[region];bool storage=(mask&(1L<<(int)HistoricalTechnology.Storage))!=0;
            bool qualified=HistoricalContactCommunityRecognition.Qualified(28,local.Teacher,s.History.Cultures.Length,local.Exposure,local.LastExchangeYear,s.Years,s.Population[region],storage,civic);
            a.Empires.Connections.Realms.Version=27;double old=(double)Call("LivingCommunityChance",s,region,100);
            a.Empires.Connections.Realms.Version=28;double current=(double)Call("LivingCommunityChance",s,region,100);
            bool callerEligible=r.Culture<0&&s.Population[region]>=400&&s.ArrivalYear[region]>=0&&s.Years-r.LastCultureYear>=800;
            Check(!qualified||callerEligible,"A qualified recorded case also passes the existing anonymous/raw-population/arrival/cooldown caller gates.");
            Check(qualified?current>old:current==old,"The actual private probability changes only for a qualified recorded contact.");
            if(qualified&&callerEligible&&current>old)positives.Add((region,old,current));
            rows.Add(new{Region=region,Year=s.Years,RawPeople=s.Population[region],r.Culture,r.BestCell,local.Teacher,local.Exposure,local.LastExchangeYear,
                RegionalKnowledge=mask,StorageDelivered=storage,CurrentActualRegionalCourt=currentCourt,RememberedCivicTeacher=local.CivicTeacher,local.CivicExposure,
                RememberedTeacherExample=remembered,ChosenCivicTeacher=teacher,ActualLocalCivicExample=civic,Qualified=qualified,CallerEligible=callerEligible,
                Literal27ProbabilityPer100ActorYears=old,Actual28ProbabilityPer100ActorYears=current});
            a.CellReads=readBaseline;
        }
        a.Empires.Connections.Realms.Version=recordedRevision;a.CellReads=reads;
        Check(s.ComputeHash()==before&&a.CellWrites==writes&&a.ProcessedLocalEvents==events,"All current probability queries restore marker/read counters and preserve every authoritative probe byte.");
        object fixture=null;
        if(positives.Count>0)
        {
            var positive=positives[0];var f=original.Copy();var fa=f.History.Aggregates;fa.Empires.Connections.Realms.Version=28;
            Call("PrepareAggregateEnvironment",f);Call("PrepareAggregateRelations",f);
            int region=positive.Region,site=fa.Regions[region].BestCell;
            // The midpoint is an EXPLICIT test input to the already computed
            // probabilities, not an actual historical PRNG realization.
            double controlledRoll=(positive.Old+positive.New)/2;
            Check(!(controlledRoll<positive.Old)&&controlledRoll<positive.New,"The same explicitly controlled roll separates old and new recognition opportunities.");
            long population=fa.TotalPopulation,raw=f.Population[region];int cultures=f.Society.Cultures.Length,towns=f.Society.Settlements.Length,courts=f.History.Polities.Length;
            var treasury=f.History.Polities.Select(p=>p.Treasury).ToArray();var food=f.Society.Settlements.Select(t=>t.FoodReserve).ToArray();
            var technicalJson=new JsonSerializerOptions{IncludeFields=true};
            string ExistingTechnical()=>JsonSerializer.Serialize(f.History.Cultures.Take(cultures).Select(c=>new{c.Technology,c.Research,c.Technical}),technicalJson);
            string technical=ExistingTechnical();
            var queue=new SimulationEventQueue(f.Timeline.Events);var queueField=typeof(Prehistory).GetField("aggregateQueue",flags)!;
            queueField.SetValue(engine,queue);
            try{Call("FoundAggregateCulture",f,region,site,-1);}finally{queueField.SetValue(engine,null);}
            int child=f.Society.Cultures.Length-1;var crops=f.History.Agriculture[child];var culture=f.Society.Cultures[child];
            Check(child==cultures&&fa.Regions[region].Culture==child,"The real existing founding handler creates exactly one local cultural actor.");
            Check(fa.TotalPopulation==population&&f.Population[region]==raw,"Recognition creates no households and leaves actual population ledgers unchanged.");
            Check(f.Society.Settlements.Length==towns&&f.History.Polities.Length==courts,"Recognition creates no town, court or automatic sovereignty.");
            Check(culture.Farming==0&&crops.AdoptedYear.All(y=>y==0)&&crops.Practice.All(y=>y==0)&&crops.Cultivars.All(c=>c==null),"Recognition grants no farming score, adopted crop, cultivar, or cultivation practice.");
            Check(treasury.SequenceEqual(f.History.Polities.Select(p=>p.Treasury))&&food.SequenceEqual(f.Society.Settlements.Select(t=>t.FoodReserve)),"Recognition neither pays nor mints existing court treasury or town food.");
            Check(ExistingTechnical()==technical,"All existing cultural knowledge, progress, allocations and research/learning budgets remain unchanged.");
            Check(original.ComputeHash()==before&&s.ComputeHash()==before,"The detached handler fixture cannot mutate either the original save or the unchanged query probe.");
            fixture=new{Region=region,Site=site,ControlledRoll=controlledRoll,OldRecognizes=controlledRoll<positive.Old,NewRecognizes=controlledRoll<positive.New,
                NewCulture=child,CultureFarming=culture.Farming,PopulationBefore=population,PopulationAfter=fa.TotalPopulation,TownsBefore=towns,TownsAfter=f.Society.Settlements.Length,
                CourtsBefore=courts,CourtsAfter=f.History.Polities.Length,AdoptedCrops=crops.AdoptedYear.Count(y=>y>0),HandlerOnlyNotSavedWorld=true};
        }
        Check(original.ComputeHash()==before,"The source historical hash remains unchanged after all observations and fixtures.");
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=Path.GetFullPath(source),Year=original.Years,Hash=before,SourceRevision=recordedRevision,ActualPositiveCases=positives.Count,Rows=rows,ControlledDetachedHandler=fixture,Checks=checks,
            Notes=new[]{"Current actual AA26 final fields/counters only; no backdating to Early Modern. Source is not upgraded, advanced or saved.",
                "Probability comparisons call the actual private LivingCommunityChance twice on the same detached state, changing only its explicit rule marker27/28 and restoring diagnostic reads.",
                "Current actual regional court comes from the existing bounded actual-cell population-cohort query, not recognized titles or an area census. Remembered civic teacher and actual-current teacher examples are both reported.",
                "The controlled midpoint roll is an explicit synthetic handler fixture, not a claim that this historical checkpoint's named PRNG actually drew that value. The real FoundAggregateCulture handler is invoked only on a separate detached copy.",
                "The new local actor can subsequently attempt its ordinary crop learning; this fixture proves no automatic crop, town, population or state grant, not actual future agricultural success or realistic Asian development."}},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Contact community actual-world: "+checks.Count+" PASS, actual qualifying cases "+positives.Count+", source unchanged "+before);
    }
}
