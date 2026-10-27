using System;
using System.Globalization;
using System.Linq;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    [Serializable] public sealed class AtlasMeasure
    { public string Key="",Text="";public double Number;public bool Numeric; }
    public static class AtlasTables
    {
        static AtlasMeasure N(string key,double number,string unit="")=>new AtlasMeasure{Key=key,Number=number,Numeric=true,Text=number.ToString(number%1==0?"N0":"N1",CultureInfo.InvariantCulture)+unit};
        static AtlasMeasure T(string key,string text)=>new AtlasMeasure{Key=key,Text=text??""};
        static string Year(PrehistoryState s,int y)=>SimulationClockView.YearNumber(y,SimulationClockView.EpochYear(s));
        static AtlasMeasure Y(PrehistoryState s,string key,int year)=>new AtlasMeasure{Key=key,Number=year,Numeric=true,Text=Year(s,year)};
        public static string[] Columns(string kind)=>Columns(kind,null);
        public static string[] Columns(string kind,PrehistoryState? state)=>Columns(kind,state?.DeepPast?.Active==true);
        public static string[] Columns(string kind,bool earlyHumanHistory)
        {
            if(earlyHumanHistory)return HomininAtlasView.Columns(kind);
            switch(AtlasNavigation.DirectoryKind(kind))
            {
                case "state":return new[]{"Name","Population","Area km²","Government","Age","Era","Status"};
                case "province":return new[]{"Name","Area km²","Local government","Administration"};
                case "city":return new[]{"Name","Residents","Rural catchment","Food years","Founded","State","Status"};
                case "people":return new[]{"Name","Population","Area km²","Way of life","Stage","Founded"};
                case "ethnicity":return new[]{"Name","Recorded","Origin","Cultural context","Parent heritage"};
                case "language":return new[]{"Name","Recorded","Parent language","Script","Word order"};
                case "religion":return new[]{"Name","Founded","Parent tradition","Ethos","Ritual"};
                case "religious-body":return new[]{"Name","Town","Tradition","Support","Institution"};
                case "technology":return new[]{"Name","Era","Capabilities","Population","Active projects"};
                case "ruler":return new[]{"Name","State","Accession","Tenure","Reputation","Status"};
                case "notable":return new[]{"Name","Age","Role","Reputation","Recorded deeds","Status"};
                case "organization":return new[]{"Name","Members","Founded","Status"};
                default:return new[]{"Name","Details"};
            }
        }
        public static AtlasMeasure[] Measures(PrehistoryState s,AtlasLink row)
        {
            if(s==null)return Array.Empty<AtlasMeasure>();
            if(s.DeepPast?.Active==true)return HomininAtlasView.Measures(s,row);
            var h=s.History;int id=row.Id;
            switch(row.Kind)
            {
                case "state":var p=h.Polities[id];return new[]{N("Population",p.Population),N("Area km²",p.AreaKm2),T("Government",p.Government),N("Age",Math.Max(0,(p.EndedYear<0?s.Years:p.EndedYear)-HistoricalRealmLifecycles.Founded(s,id))),T("Era",WorldEraView.Name(WorldEraView.KnowledgeEra(h.Cultures[p.CultureId]))),T("Status",p.EndedYear<0?"Active":"Ended "+Year(s,p.EndedYear))};
                case "province":int cell=HistoricalProvinceIdentity.Center(s,id),owner=HistoricalCells.Polity(s,cell);return new[]{N("Area km²",HistoricalProvinceIdentity.Area(s,id)),T("Local government",owner<0?"No active court":h.Polities[owner].Name),T("Administration",id>=HistoricalProvinceIdentity.PostIdentityOffset?"Delegated office":"Settlement district")};
                case "city":var town=s.Society.Settlements[id];return new[]{N("Residents",town.Population),h.Aggregates==null?T("Rural catchment","Not recorded"):N("Rural catchment",h.Aggregates.Cities[id].Rural),town.Population>0?N("Food years",town.FoodReserve/(double)town.Population):T("Food years","—"),Y(s,"Founded",town.FoundedYear),T("State",town.PolityId<0?"Independent community":h.Polities[town.PolityId].Name),T("Status",town.Population>0?"Inhabited":"Abandoned")};
                case "people":var c=s.Society.Cultures[id];return new[]{N("Population",c.Population),N("Area km²",c.AreaKm2),T("Way of life",c.Subsistence),T("Stage",c.Stage.ToString()),Y(s,"Founded",c.FoundedYear)};
                case "ethnicity":var e=h.Identities.Peoples[id];return new[]{Y(s,"Recorded",e.FoundedYear),T("Origin",e.Origin.ToString()),T("Cultural context",e.Culture<0?"Regional community; no advanced-culture actor":s.Society.Cultures[e.Culture].Name),T("Parent heritage",e.Parent<0?"Earlier history unrecorded":h.Identities.Peoples[e.Parent].Name)};
                case "language":var v=h.Languages.Spoken.Varieties[id];return new[]{Y(s,"Recorded",v.FoundedYear),T("Parent language",v.Parent<0?"Earlier history unrecorded":h.Languages.Spoken.Varieties[v.Parent].Name),T("Script",v.Sounds.Script.ToString()),T("Word order",v.Sounds.WordOrder)};
                case "religion":var r=h.Religions[id];return new[]{Y(s,"Founded",r.FoundedYear),T("Parent tradition",r.ParentId<0?"Founding tradition":h.Religions[r.ParentId].Name),T("Ethos",r.Ethos),T("Ritual",r.Ritual)};
                case "religious-body":int t=id/2;var body=Prehistory.ReligiousInstitutionAt(s,s.Society.Settlements[t].Cell,id%2);return body==null?Array.Empty<AtlasMeasure>():new[]{T("Town",s.Society.Settlements[t].Name),T("Tradition",h.Religions[body.Faith].Name),N("Support",body.Support/10.0,"%"),T("Institution",id%2==0?"Public ritual seat":"Lay association")};
                case "technology":var tech=h.Cultures[id];return new[]{T("Era",WorldEraView.Name(WorldEraView.KnowledgeEra(tech))),N("Capabilities",tech.Technology.Count(x=>x>0)),N("Population",s.Society.Cultures[id].Population),N("Active projects",TechnicalResearchView.Read(s,id).Count(x=>x.AllocationBasisPoints>0))};
                case "ruler":var l=h.Dynamics.Leaders[id];return new[]{T("State",h.Polities[l.PolityId].Name),Y(s,"Accession",l.StartedYear),N("Tenure",Math.Max(0,(l.EndedYear<0?s.Years:l.EndedYear)-l.StartedYear)),N("Reputation",l.Renown),T("Status",l.EndedYear<0&&h.Polities[l.PolityId].EndedYear<0?"In office":"Former office")};
                case "notable":var person=Prehistory.NotablePersonAt(s,id);return new[]{N("Age",Math.Max(0,(person.DeathYear<0?s.Years:person.DeathYear)-person.BirthYear)),T("Role",person.Roles.ToString()),N("Reputation",person.Renown),N("Recorded deeds",person.Deeds.Length),T("Status",person.DeathYear<0?"Living":"Deceased")};
                case "organization":var o=h.International.Organizations[id];return new[]{N("Members",o.Members.Count),Y(s,"Founded",o.FoundedYear),T("Status",o.EndedYear<0?"Active":"Ended "+Year(s,o.EndedYear))};
                default:return new[]{T("Details",row.Detail)};
            }
        }
        public static AtlasMeasure Cell(AtlasLink row,string column)=>column=="Name"?T("Name",row.Name):row.Measures.FirstOrDefault(m=>m.Key==column)??T(column,"—");
        public static AtlasLink[] Sort(AtlasLink[] rows,string column,bool descending)
        {
            // Sorting only a requested, immutable UI directory; no simulation turns.
            var present=rows.Where(r=>Cell(r,column).Text!="—");var missing=rows.Where(r=>Cell(r,column).Text=="—");
            int Compare(AtlasLink a,AtlasLink b)
            {var x=Cell(a,column);var y=Cell(b,column);int cmp=x.Numeric&&y.Numeric?x.Number.CompareTo(y.Number):StringComparer.OrdinalIgnoreCase.Compare(x.Text,y.Text);if(descending)cmp=-cmp;return cmp!=0?cmp:a.Id.CompareTo(b.Id);}
            return present.OrderBy(x=>x,System.Collections.Generic.Comparer<AtlasLink>.Create(Compare)).Concat(missing.OrderBy(x=>x.Id)).ToArray();
        }
    }
}
