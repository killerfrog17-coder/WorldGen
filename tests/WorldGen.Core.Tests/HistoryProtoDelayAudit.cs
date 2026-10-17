using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Security.Cryptography;
using WorldGen.Core;
using WorldGen.Serialization;

// Archive inspection plus explicitly dated opportunity arithmetic. No replay,
// live handler, rules upgrade, or inferred historical deployment from final tech.
public static class HistoryProtoDelayAudit
{
    public static void Run(string source,string folder,string output)
    {
        if(File.Exists(output))throw new IOException("Use a new proto audit output.");
        var world=WorldFile.Read(File.ReadAllBytes(source));var original=world.Prehistory;string hash=original.ComputeHash();
        var s=original.Copy();var h=s.History;var a=h.Aggregates;var engine=new Prehistory(world);
        if(a.Empires.Connections.Realms.Version!=19)throw new Exception("Requires actual frozen source19.");
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(engine,args);
        long reads=a.CellReads,writes=a.CellWrites,events=a.ProcessedLocalEvents;
        Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);
        double[] Field(string name)=>(double[])typeof(Prehistory).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(engine);
        var river=Field("riverAccess");var season=Field("landscapeSeason");
        int first=h.Polities.Min(p=>p.FoundedYear);
        object Place(int cell)=>new{Cell=cell,Latitude=Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI,
            Longitude=Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI,
            Climate=world.Climate.Classification[cell].ToString(),Biome=world.Ecology.Habitat[cell].Biome.ToString()};
        var proto=h.Archive.Where(e=>e.Year<first&&(e.Kind=="proto-urban-center"||e.Kind=="proto-urban-decline")).OrderBy(e=>e.Year).ThenBy(e=>e.Id).ToArray();
        var protoIds=proto.Select(e=>e.SettlementId).Where(id=>id>=0).Distinct().ToHashSet();
        var chronology=h.Archive.Where(e=>protoIds.Contains(e.SettlementId)).OrderBy(e=>e.Year).ThenBy(e=>e.Id)
            .Select(e=>new{e.Id,e.Year,e.Kind,e.SettlementId,e.CultureId,e.PolityId,e.Text,Place=Place(e.Cell)}).ToArray();
        var episodes=proto.Where(e=>e.Kind=="proto-urban-center").Select(e=>
        {
            var next=h.Archive.Where(n=>n.SettlementId==e.SettlementId&&n.Year>=e.Year&&n.Id!=e.Id&&
                (n.Kind=="proto-urban-decline"||n.Kind=="abandonment"||n.Kind=="independent-institutions"||n.Kind=="institutional-diffusion"))
                .OrderBy(n=>n.Year).ThenBy(n=>n.Id).FirstOrDefault();
            return new{Town=e.SettlementId,Start=e.Year,StartEvent=e.Text,Place=Place(e.Cell),
                NextYear=next?.Year,YearsToNext=next==null?(int?)null:next.Year-e.Year,NextKind=next?.Kind,NextText=next?.Text};
        }).ToArray();
        using var review=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"review.json")));
        var metadata=new List<object>();var initialCohort=new HashSet<int>();
        using(var initial=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"year-150000","towns.json"))))
            foreach(var town in initial.RootElement.EnumerateArray())initialCohort.Add(town.GetProperty("Id").GetInt32());
        foreach(var row in review.RootElement.GetProperty("Rows").EnumerateArray())
        {
            int year=row.GetProperty("Year").GetInt32();if(year>first+3000)continue;
            string file=Path.Combine(folder,"year-"+year,"opportunities.json");if(!File.Exists(file))continue;
            using var document=JsonDocument.Parse(File.ReadAllText(file));
            foreach(var t in document.RootElement.GetProperty("Towns").EnumerateArray())
            {
                int Id(string key)=>t.GetProperty(key).GetInt32();long Long(string key)=>t.GetProperty(key).GetInt64();double Number(string key)=>t.GetProperty(key).GetDouble();
                int id=Id("Id"),cell=Id("Cell"),culture=Id("CultureId");long population=Long("Population");
                if(year==150000)initialCohort.Add(id);
                bool supported=t.GetProperty("Supported").GetBoolean(),qualified=t.GetProperty("Proto").GetBoolean();
                if(Id("Owner")>=0&&!protoIds.Contains(id))continue;
                bool provisioned=HistoricalProtoCentreEffects.Provisioned(population,Long("Rural")+Long("Suburban"),Id("Farming"),Id("Institution"),
                    Number("Fiscal"),Number("ReserveYears"),supported,Id("DeclineYears")>0,Id("Peers"));
                if(!provisioned&&!protoIds.Contains(id))continue;
                // Exported Peers counts ALL neighbors; actual actor counts only
                // >=500-person peers. Bound that missing historical subset.
                double low=HistoricalProtoCentreEffects.Coordination(Number("Fiscal"),population,Id("Institution"),river[cell],0,season[cell]);
                double high=HistoricalProtoCentreEffects.Coordination(Number("Fiscal"),population,Id("Institution"),river[cell],Id("Peers"),season[cell]);
                double environment=Number("Environment"),staples=Number("Staples"),homeland=Number("Homeland");int exposure=Id("CivicExampleExposure");
                double lowAnnual=HistoricalProtoCentreEffects.AnnualOpportunity(low,environment,staples,homeland,exposure);
                double highAnnual=HistoricalProtoCentreEffects.AnnualOpportunity(high,environment,staples,homeland,exposure);
                var anomaly=HistoricalClimateVariability.At(world.Recipe.Seed,world.Ecology.Province[cell],year/10*10);
                var habitat=(HabitatCell)Call("CropHabitat",s,cell);
                double responseLowMitigation=HistoricalClimateVariability.FoodResponse(habitat,anomaly,river[cell],Id("Farming")>=180,0);
                double responseHighMitigation=HistoricalClimateVariability.FoodResponse(habitat,anomaly,river[cell],Id("Farming")>=180,1);
                double shockLow=D.Clamp((1-responseHighMitigation)*3),shockHigh=D.Clamp((1-responseLowMitigation)*3);
                metadata.Add(new{Year=year,Id=id,Name=t.GetProperty("Name").GetString(),Culture=culture,Place=Place(cell),Population=population,Age=Id("Age"),
                    Owner=Id("Owner"),Farming=Id("Farming"),Institution=Id("Institution"),Fiscal=Number("Fiscal"),Environment=environment,Staples=staples,Homeland=homeland,
                    ReserveYears=Number("ReserveYears"),Supported=supported,Decline=Id("DeclineYears"),QualifiedProto=qualified,ProtoSince=Id("ProtoSince"),
                    Preparation=Id("Preparation"),CivicPreparation=Id("CivicPreparationYears"),CivicExampleExposure=exposure,
                    ProvisionedUsingAllExportedPeers=provisioned,ExportedPeers=Id("Peers"),ActualQualifyingPeersUnknown=true,
                    CoordinationLow=low,CoordinationHigh=high,AnnualQualificationLow=lowAnnual,AnnualQualificationHigh=highAnnual,
                    MinimumConditionalExpectedQualificationYears=highAnnual>0?1/highAnnual:(double?)null,
                    MatureProtoConsolidationHazardLow=HistoricalProtoCentreEffects.InstitutionalHazard(low,1000),
                    MatureProtoConsolidationHazardHigh=HistoricalProtoCentreEffects.InstitutionalHazard(high,1000),
                    ExactClimateAnomalyAtObservation=anomaly,FoodResponseMitigation0=responseLowMitigation,FoodResponseMitigation1=responseHighMitigation,
                    GenericUnorganizedDispersalHazardLow=HistoricalProtocentreEffects.AnnualDispersalHazard(environment,Number("Fiscal"),shockLow,false),
                    GenericUnorganizedDispersalHazardHigh=HistoricalProtocentreEffects.AnnualDispersalHazard(environment,Number("Fiscal"),shockHigh,false)});
            }
        }
        var cohort=initialCohort.Concat(protoIds).Distinct().OrderBy(id=>id).Select(id=>
        {
            var town=s.Society.Settlements[id];var life=a.Living.Cities[id];
            var abandonment=h.Archive.Where(e=>e.SettlementId==id&&e.Kind=="abandonment").OrderBy(e=>e.Year).FirstOrDefault();
            return new{town.Id,town.Name,town.FoundedYear,life.AbandonedYear,life.PeakPopulation,life.PeakYear,
                Abandonment=abandonment==null?null:new{abandonment.Year,abandonment.Text},Place=Place(town.Cell),CurrentCulture=town.CultureId,
                Notice="Final aggregate fields are not reconstructed deathbed food/supply; actual archived reason/date and dated150000 observations remain separate."};
        }).ToArray();
        long delta=a.CellReads-reads;a.CellReads=reads;
        if(s.ComputeHash()!=hash||original.ComputeHash()!=hash||a.CellWrites!=writes||a.ProcessedLocalEvents!=events)throw new Exception("Proto audit changed authoritative state.");
        string core=typeof(Prehistory).Assembly.Location;
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=source,Year=s.Years,Hash=hash,Realm=a.Empires.Connections.Realms.Version,Core=core,
            CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(core))),FirstState=first,DiagnosticReadCounterDelta=delta,
            Episodes=episodes,Cohort=cohort,HistoricalOpportunityBounds=metadata,FullFocusedArchive=chronology,
            Notes=new[]{"No simulation handler, replay, save upgrade, authoritative mutation or world save.",
                "Dated opportunity inputs are original exported observations. Only immutable actual river/season/geography is joined from accepted terrain.",
                "Historical exported Peers is all neighbors, not the >=500 subset: reported coordination/hazards are honest lower/upper bounds, not invented exact historical peer counts.",
                "Climate anomaly is exact for each observation's year/province, but historical technology deployment is NOT backdated from final state. Mitigation0..1 bounds are explicit.",
                "Current source19 generic dispersal does not recognize QualifiedProto as institutional continuity; archived generic early-village abandonment of qualified centres is distinguished from actual food-distress retirement.",
                "Expected waiting times assume unchanged qualified conditions and concern one site; they are not predicted historical dates or universal archaeological laws."}},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
        Console.WriteLine("Proto audit "+s.Years+": "+episodes.Length+" prestate episodes, "+metadata.Count+" dated opportunity bounds; hash preserved.");
    }
}
