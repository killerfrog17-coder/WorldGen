using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Offline diagnostics only: inspect actors and three current proposals per town.
// These observations are not simulation counters and never feed back into rules.
public static class HistoryInlandTechnicalAudit
{
    public static void Run(string path,string output)
    {
        if(File.Exists(output))throw new ArgumentException("Refusing to overwrite an audit.");
        var w=WorldFile.Read(File.ReadAllBytes(path));var s=w.Prehistory;var e=new Prehistory(w);e.Validate(s);string hash=s.ComputeHash();
        var capture=Capture(w,e,s);if(s.ComputeHash()!=hash)throw new Exception("Read-only audit modified the checkpoint.");
        File.WriteAllText(output,JsonSerializer.Serialize(capture,new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));Console.WriteLine(output);
    }
    public static object Capture(World w,Prehistory e,PrehistoryState s)
    {
        object Call(string method,params object[] args)=>typeof(Prehistory).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args)!;
        Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);
        var h=s.History;var a=h.Aggregates;var counts=new SortedDictionary<string,int>();var samples=new List<object>();
        var relations=h.Relations.ToDictionary(r=>(r.A,r.B));
        void Count(string reason){counts.TryGetValue(reason,out int n);counts[reason]=n+1;}
        int towns=0,ratePositive=0,proposals=0,routePass=0,sourcePass=0,completePass=0;
        foreach(var town in s.Society.Settlements)
        {
            var city=a.Cities[town.Id];var life=a.Living.Cities[town.Id];if(life.AbandonedYear>=0)continue;towns++;
            int owner=a.Resolve(town.PolityId,h);double rate=(double)Call("LivingLocalTownRate",s,town.Id);
            if(rate<=0)
            {
                if(!city.Supported)Count("Parent unsupported");if(life.DeclineYears>0)Count("Parent in decline");if(town.Population<1000)Count("Parent under1000");if(town.FoodReserve<town.Population*.5)Count("Parent reserve under half urban year");
                if((long)Call("LivingFounderAvailability",s,town.Id)<1600)Count("Fewer than1600 transferable rural households");
                if(owner>=0&&(h.Polities[owner].Treasury<1000||a.Polities[owner].Receipts<=a.Polities[owner].ServiceCost))Count("Parent state lacks net operating surplus");
                continue;
            }
            ratePositive++;
            for(int trial=0;trial<3;trial++)
            {
                proposals++;int target=(int)Call("LivingLocalTownCandidate",s,town.Id,trial);var reasons=new List<string>();
                if(target==town.Cell)reasons.Add("Proposal returned source (invalid geography)");
                bool route=(bool)Call("InstitutionalRouteAllowed",s,town.Cell,target,town.CultureId);if(route)routePass++;else reasons.Add("Institutional route blocked");
                double effort=(double)Call("LivingTravelKm",s,town.Cell,target,town.CultureId),radius=owner>=0?(double)Call("TerritorialServiceRadius",s,owner,target):0;
                if(owner>=0&&effort>radius*1.5)reasons.Add("Beyond local administrative service radius");
                int host=HistoricalCells.Polity(s,target);
                if(host>=0&&host!=owner)
                {
                    relations.TryGetValue((Math.Min(owner,host),Math.Max(owner,host)),out var relation);
                    if(owner<0||relation==null||!relation.Trade||relation.WarSince>=0)reasons.Add("Foreign host lacks peaceful trade agreement");
                }
                int holder=HistoricalRecognizedClaims.Holder(s,target);
                if(owner>=0&&holder>=0&&holder!=owner&&HistoricalImperialHierarchy.Sovereign(s,holder)!=HistoricalImperialHierarchy.Sovereign(s,owner))
                {
                    relations.TryGetValue((Math.Min(owner,holder),Math.Max(owner,holder)),out var recognition);
                    if(recognition==null||recognition.WarSince<0&&recognition.Trust>=0)reasons.Add("Another recognized title blocks sponsorship");
                }
                bool foundation=(bool)Call("CanFoundLivingTown",s,town.CultureId,target,town.Id);if(foundation)sourcePass++;else reasons.Add("Local settlement/founder/crop/spacing gate");
                long available=(long)Call("LivingFounderAvailability",s,town.Id),founders=Math.Min(250,available/12),farms=Math.Min(available/3,Math.Max(800,founders*8)),people=founders+farms;
                object[] cost={s,town.Id,target,people,0L,0L};Call("LivingInlandSettlementCosts",cost);long provisions=(long)cost[4],investment=(long)cost[5];
                if(town.FoodReserve<people+provisions+town.Population/4)reasons.Add("Insufficient founding and carriage rations");
                if(owner>=0&&h.Polities[owner].Treasury<investment*2)reasons.Add("Insufficient founding capital");
                if(reasons.Count==0)completePass++;foreach(string reason in reasons)Count(reason);
                if(samples.Count<60)samples.Add(new{town.Id,town.Name,town.Cell,owner,Rate=rate,ExpectedYearsBeforeOpportunity=1/rate,Trial=trial,Target=target,EffortKm=effort,ServiceRadiusKm=radius,Reasons=reasons});
            }
        }
        var technology=h.Cultures.Where(c=>a.Cultures[c.Id].SupportedCities>0).OrderByDescending(c=>a.Cultures[c.Id].SupportedUrban).Take(24).Select(c=>{
            var p=c.Technical;var ca=a.Cultures[c.Id];double science=HistoricalTechnicalGraph.Installed(c,(int)HistoricalTechnology.ScientificMethod),printing=HistoricalTechnicalGraph.Installed(c,(int)HistoricalTechnology.Printing);
            double annual=4.2*(.35+.35*Math.Log(1+ca.SupportedUrban/1500.0))*(.3+.7*c.Institution/1000.0)*(.25+.75*c.Surplus/1000.0)*(1+4*science)*(1+printing)*Math.Sqrt(HistoricalModernEffects.Communication(c,s.Years))*.68*(double)Call("LivingCulturePoliticalLearning",s,c.Id);
            return new{c.Id,Name=s.Society.Cultures[c.Id].Name,ca.SupportedUrban,ca.SupportedCities,AnnualResearch=annual,Projects=Enumerable.Range(0,p.Levels.Length).Where(k=>p.Allocation[k]>0||k==9||k==12||k==14).Select(k=>new{Technology=k,Name=HistoricalTechnologies.Names[k],Level=p.Levels[k],Offer=p.Offers[k],Deployment=p.Deployment[k],KnownYear=p.LevelYear[k],FirstInstalled=p.InstalledYear[k]-1,Allocation=p.Allocation[k],Progress=p.Progress[k]/1000000.0,NextCost=HistoricalTechnicalGraph.Cost(s,k,p.Levels[k]+1)/1000000.0,LocalAnnualAllocated=annual*p.Allocation[k]/10000.0,Missing=HistoricalTechnicalGraph.Requirements(k).Where(r=>p.Levels[r.Technology]<r.Level).Select(r=>HistoricalTechnologies.Names[r.Technology]+" level"+r.Level).ToArray()}).ToArray()};
        }).ToArray();
        return new{Year=s.Years,Hash=s.Hash,Towns=towns,RatePositive=ratePositive,CurrentYearProposals=proposals,RoutePass=routePass,FoundationPass=sourcePass,CompletePassBeforeAdaptation=completePass,FailureCounts=counts,Warning="Contemporaneous three-bearing proposals, not historical rejection counters. Reasons overlap; adaptation is not rolled. All traversal is offline diagnostic work.",Samples=samples,Technical=technology};
    }
}
