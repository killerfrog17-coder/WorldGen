using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;

// Offline observation only. None of these town/actor traversals run inside a
// simulation turn or feed their results back into authoritative decisions.
public static partial class HistoryWorldRealismReview
{
    static readonly JsonSerializerOptions ReviewJson=new(){WriteIndented=true,IncludeFields=true};
    static void OpportunityReview(World world,Prehistory engine,PrehistoryState s,string output)
    {
        var h=s.History;var a=h.Aggregates;
        double Inspect(string name,params object[] values)=>(double)typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,values)!;
        var opportunities=s.Society.Settlements.Where(t=>t.Population>=800&&a.Living.Cities[t.Id].AbandonedYear<0).Select(t=>{
            var city=a.Cities[t.Id];var hc=h.Cultures[t.CultureId];var c=s.Society.Cultures[t.CultureId];var record=h.Towns[t.Id];var region=a.Living.Regions[engine.CellPatch[t.Cell]];
            double fiscal=Inspect("LivingCropLandscape",s,t.CultureId,t.Cell,true),environment=Inspect("EnvironmentalEstablishment",s,t.Cell,t.CultureId),staples=Inspect("EnvironmentalStaples",s,t.CultureId,t.Cell),homeland=Inspect("HomelandOriginFactor",s,t.Cell);
            return new{t.Id,t.Name,t.Cell,t.CultureId,t.Population,Owner=a.Resolve(t.PolityId,h),Age=s.Years-t.FoundedYear,Climate=world.Climate.Classification[t.Cell].ToString(),Biome=world.Ecology.Habitat[t.Cell].Biome.ToString(),Fiscal=fiscal,Environment=environment,Staples=staples,Homeland=homeland,
                ReserveYears=t.FoodReserve/(double)Math.Max(1,t.Population),c.Farming,hc.Institution,hc.Surplus,city.Supported,city.Rural,city.Suburban,
                Proto=HistoricalRegionalTraditions.Proto(s,t.Id),ProtoSince=h.RegionalTraditions?.ProtoSince[t.Id]??-1,Preparation=h.RegionalTraditions?.Preparation[t.Id]??-1,
                record.CivicPreparationYears,record.CivicExampleExposure,record.CivicTeacherPolityId,region.CivicTeacher,region.CivicExposure,Peers=city.Neighbors.Length,a.Living.Cities[t.Id].DeclineYears};
        }).ToArray();
        var technical=h.Cultures.Where(c=>a.Cultures[c.Id].SupportedCities>0).Select(c=>new{c.Id,Name=s.Society.Cultures[c.Id].Name,c.Institution,c.Surplus,a.Cultures[c.Id].SupportedCities,a.Cultures[c.Id].SupportedUrban,Era=WorldEraView.CultureEra(c),Practice=c.Technical}).ToArray();
        File.WriteAllText(Path.Combine(output,"opportunities.json"),JsonSerializer.Serialize(new{Year=s.Years,Towns=opportunities,Technical=technical},ReviewJson));
    }
    public static void FinalPoliticalChronology(World world,string output)
    {
        var s=world.Prehistory;var h=s.History;var a=h.Aggregates;var eras=WorldEraView.FirstYears(s);
        int EraAt(int year){int era=2;for(int i=3;i<eras.Length;i++)if(eras[i]>=0&&eras[i]<=year)era=i;return era;}
        var events=h.Archive.Where(e=>e.Kind=="independent-institutions"||e.Kind=="institutional-diffusion"||e.Kind=="proto-urban-center"||e.Kind=="proto-urban-decline"||e.Kind=="realm-succession-crisis"||e.Kind=="abandonment"||e.Kind=="field-battle"||e.Kind=="war"||e.Kind=="peace"||e.Kind=="colonial-protectorate"||e.Kind=="colonial-council"||e.Kind=="sponsored-port").ToArray();
        File.WriteAllText(Path.Combine(output,"political-chronology.json"),JsonSerializer.Serialize(new{Year=s.Years,Eras=eras,
            Counts=events.GroupBy(e=>(Era:EraAt(e.Year),e.Kind)).OrderBy(g=>g.Key.Era).ThenBy(g=>g.Key.Kind).Select(g=>new{g.Key.Era,g.Key.Kind,Count=g.Count()}),
            Events=events.Select(e=>new{e.Id,e.Year,e.Kind,e.Cell,e.PolityId,e.OtherPolityId,e.SettlementId,e.Text}),
            Origins=h.Polities.Where(p=>p.ParentId<0).Select(p=>{var t=s.Society.Settlements[p.CapitalId];return new{p.Id,p.Name,p.FoundedYear,p.EndedYear,Capital=t.Name,t.Cell,Climate=world.Climate.Classification[t.Cell].ToString(),Biome=world.Ecology.Habitat[t.Cell].Biome.ToString(),Latitude=Math.Asin(world.Surface.Cells[t.Cell].Position.Z)*180/Math.PI,Longitude=Math.Atan2(world.Surface.Cells[t.Cell].Position.Y,world.Surface.Cells[t.Cell].Position.X)*180/Math.PI};})},ReviewJson));
    }
}
