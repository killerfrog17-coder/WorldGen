using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    // An on-demand projection, never a second demographic store. No later-city
    // actors, ethnicity labels or language parentage are invented here.
    public static class HomininAtlasView
    {
        public static readonly (string Kind,string Name)[] Categories={
            ("population","Regional populations"),("lineage","Biological lineages"),
            ("people","Traditions"),("language","Communication"),("technology","Learned practices")};
        static string N(long value)=>value.ToString("N0",CultureInfo.InvariantCulture);
        static long People(HomininPopulation p)=>(long)Math.Round(p.People);
        static AtlasFact Fact(string label,string value,params AtlasLink[] links)=>new AtlasFact{Label=label,Value=value,Related=links};
        static AtlasSection Links(string title,IEnumerable<AtlasLink> links,string text="")=>new AtlasSection{Title=title,Text=text,Links=links.ToArray()};
        static AtlasMeasure Number(string key,long value)=>new AtlasMeasure{Key=key,Numeric=true,Number=value,Text=N(value)};
        static AtlasMeasure Text(string key,string value)=>new AtlasMeasure{Key=key,Text=value};
        static AtlasMeasure Year(PrehistoryState s,string key,int year)=>year<0?Text(key,"—"):new AtlasMeasure{Key=key,Numeric=true,Number=year,Text=HomininView.Date(s,year)};
        static string Category(string kind)=>Categories.FirstOrDefault(c=>c.Kind==AtlasNavigation.DirectoryKind(kind)).Name??"Early human history";
        static string Stage(HomininHistory h,HomininTradition t)=>HomininHistory.StageName(HomininHistory.Stage(t,h.RulesVersion<3));
        static string[] Practices(HomininTradition t)=>Enum.GetValues(typeof(LithicKnowledge)).Cast<LithicKnowledge>().Where(k=>k!=LithicKnowledge.None&&(t.Knowledge&k)!=0).Select(Prehistory.LithicSkillName).ToArray();
        sealed class Index
        {
            public readonly PrehistoryState State;public readonly HomininHistory H;
            public readonly HomininPopulation[] Residents;
            public readonly Dictionary<int,long> Lineages,Traditions,Languages;
            public Index(PrehistoryState state)
            {
                if(state?.DeepPast?.Active!=true||state.Society==null)throw new ArgumentException("An active early-history state is required.");
                State=state;H=state.DeepPast;Residents=H.Populations.Where(p=>p.People>0).ToArray();
                Dictionary<int,long> Count(Func<HomininPopulation,int> key)=>Residents.GroupBy(key).ToDictionary(g=>g.Key,g=>g.Sum(People));
                Lineages=Count(p=>p.Lineage);Traditions=Count(p=>p.Tradition);Languages=Count(p=>p.Language);
            }
            public long Count(Dictionary<int,long> totals,int id)=>totals.TryGetValue(id,out long value)?value:0;
            public string LanguageName(int id)=>H.Version>=2?H.Languages[id].Name:"Communication identity "+id;
            public AtlasLink Population(HomininPopulation p)=>new AtlasLink{Kind="population",Id=p.Id,Name=H.Lineages[p.Lineage].Name+" · group "+(p.Id+1),Value=People(p),Detail=N(People(p))+" people",Context=H.Traditions[p.Tradition].Name+" · "+LanguageName(p.Language)};
            public AtlasLink Lineage(int id)=>new AtlasLink{Kind="lineage",Id=id,Name=H.Lineages[id].Name,Value=Count(Lineages,id),Detail=N(Count(Lineages,id))+" people",Historical=H.Lineages[id].ExtinctYear>=0&&Count(Lineages,id)==0,Context=H.Lineages[id].SapiensLike?"Sapiens-like lineage":"Other hominin lineage"};
            public AtlasLink Tradition(int id,bool technology=false)=>new AtlasLink{Kind=technology?"technology":"people",Id=id,Name=H.Traditions[id].Name,Value=Count(Traditions,id),Detail=Stage(H,H.Traditions[id]),Cell=id<State.Society.Cultures.Length?State.Society.Cultures[id].OriginCell:-1,Historical=Count(Traditions,id)==0};
            public AtlasLink Language(int id)=>new AtlasLink{Kind="language",Id=id,Name=LanguageName(id),Value=Count(Languages,id),Detail=N(Count(Languages,id))+" people represented",Historical=Count(Languages,id)==0};
        }
        public static string[] Columns(string kind)
        {
            switch(AtlasNavigation.DirectoryKind(kind))
            {
                case "population":return new[]{"Name","People","Lineage","Tradition","Language"};
                case "lineage":return new[]{"Name","People","Recorded","Type","Status"};
                case "people":return new[]{"Name","Population","Stage","Founded","Parent tradition"};
                case "language":return new[]{"Name","People","Recorded","Parent communication","Status"};
                case "technology":return new[]{"Name","Population","Stage","Capabilities"};
                default:return new[]{"Name","Details"};
            }
        }
        static AtlasMeasure[] Measures(Index x,AtlasLink row)
        {
            var h=x.H;int id=row.Id;
            switch(row.Kind)
            {
                case "population":var p=x.Residents.FirstOrDefault(v=>v.Id==id);return p==null?Array.Empty<AtlasMeasure>():new[]{Number("People",People(p)),Text("Lineage",h.Lineages[p.Lineage].Name),Text("Tradition",h.Traditions[p.Tradition].Name),Text("Language",x.LanguageName(p.Language))};
                case "lineage" when id>=0&&id<h.Lineages.Length:var l=h.Lineages[id];return new[]{Number("People",x.Count(x.Lineages,id)),Year(x.State,"Recorded",l.FoundedYear),Text("Type",l.SapiensLike?"Sapiens-like":"Other hominin"),Text("Status",LineageStatus(x,l))};
                case "people" when id>=0&&id<h.Traditions.Length:var t=h.Traditions[id];return new[]{Number("Population",x.Count(x.Traditions,id)),Text("Stage",Stage(h,t)),Year(x.State,"Founded",t.FoundedYear),Text("Parent tradition",t.Parent<0?"Founding tradition":h.Traditions[t.Parent].Name)};
                case "technology" when id>=0&&id<h.Traditions.Length:return new[]{Number("Population",x.Count(x.Traditions,id)),Text("Stage",Stage(h,h.Traditions[id])),Number("Capabilities",Practices(h.Traditions[id]).Length)};
                case "language" when id>=0&&id<h.NextLanguage:var language=h.Version>=2?h.Languages[id]:null;return new[]{Number("People",x.Count(x.Languages,id)),Year(x.State,"Recorded",language?.FoundedYear??-1),Text("Parent communication",language?.Parent>=0?x.LanguageName(language.Parent):"Earlier parentage unrecorded"),Text("Status",x.Count(x.Languages,id)>0?"Currently represented":"No current population")};
                default:return new[]{Text("Details",row.Detail)};
            }
        }
        public static AtlasMeasure[] Measures(PrehistoryState s,AtlasLink row)=>Measures(new Index(s),row);
        public static AtlasLink[] Directory(PrehistoryState s,string kind,string search="",bool past=false)
        {
            var x=new Index(s);IEnumerable<AtlasLink> rows;
            switch(AtlasNavigation.DirectoryKind(kind))
            {
                case "population":rows=x.Residents.Select(x.Population);break;
                case "lineage":rows=x.H.Lineages.Where(l=>past||l.ExtinctYear<0||x.Count(x.Lineages,l.Id)>0).Select(l=>x.Lineage(l.Id));break;
                case "people":case "technology":bool technology=AtlasNavigation.DirectoryKind(kind)=="technology";rows=x.H.Traditions.Where(t=>past||!s.Started||x.Count(x.Traditions,t.Id)>0).Select(t=>x.Tradition(t.Id,technology));break;
                case "language":rows=Enumerable.Range(0,x.H.NextLanguage).Where(id=>past||!s.Started||x.Count(x.Languages,id)>0).Select(x.Language);break;
                default:return Array.Empty<AtlasLink>();
            }
            return rows.Where(r=>r.Name.IndexOf(search??"",StringComparison.OrdinalIgnoreCase)>=0).OrderByDescending(r=>r.Value).ThenBy(r=>r.Id).Select(r=>{r.Measures=Measures(x,r);return r;}).ToArray();
        }
        public static AtlasLink? Selection(PrehistoryState s,int cell,HistoryOverlay overlay)
        {
            // The cell publication records its local tradition, not a unique
            // language or ethnicity. Other lenses use the full location card.
            if(!SocietyView.PopulationFocus(overlay)&&overlay!=HistoryOverlay.Technology)return null;
            if(cell<0||cell>=s.Society.CellCulture.Length)return null;int id=s.Society.CellCulture[cell];
            return id<0||id>=s.DeepPast.Traditions.Length?null:PublishedTradition(s,id,overlay==HistoryOverlay.Technology);
        }
        static AtlasLink PublishedTradition(PrehistoryState s,int id,bool technology=false)
        {
            var t=s.DeepPast.Traditions[id];var published=s.Society.Cultures[id];
            return new AtlasLink{Kind=technology?"technology":"people",Id=id,Name=t.Name,Value=published.Population,Cell=published.OriginCell,Detail=Stage(s.DeepPast,t),Historical=published.Population==0};
        }
        public static AtlasLink[] ProfilesAt(PrehistoryState s,int cell)
        {var link=Selection(s,cell,HistoryOverlay.Cultures);return link==null?Array.Empty<AtlasLink>():new[]{link,PublishedTradition(s,link.Id,true)};}
        public static string Summary(PrehistoryState s,AtlasLink link)
        {
            int id=link.Id;var h=s.DeepPast;
            switch(link.Kind)
            {
                case "people":case "technology":return id>=0&&id<h.Traditions.Length?h.Traditions[id].Name+" · "+Stage(h,h.Traditions[id])+" · "+N(link.Value)+" people represented.":"Record unavailable.";
                case "lineage":return id>=0&&id<h.Lineages.Length?h.Lineages[id].Name+" is a biological lineage, independently of learned tradition.":"Record unavailable.";
                case "population":return link.Detail+" · A regional population record, not an individually simulated NPC tribe.";
                case "language":return link.Detail+" · A recorded communication identity; vocabulary and grammar are not yet modeled.";
                default:return "Open an early-history record for its regional context.";
            }
        }
        static int Cell(Prehistory? engine,int region)=>engine!=null&&region>=0&&region<engine.Patches.Length?engine.Patches[region].Cell:-1;
        static string LineageStatus(Index x,HomininLineage l)=>l.ExtinctYear<0?"Extant":x.Count(x.Lineages,l.Id)>0?"Recorded extinct, but still represented in saved populations":"Extinct "+HomininView.Date(x.State,l.ExtinctYear);
        static AtlasFact Record(PrehistoryState s,HomininEvent e)
        {
            var family=s.DeepPast.Lineages[e.Lineage];var lineage=new AtlasLink{Kind="lineage",Id=e.Lineage,Name=family.Name,Historical=family.ExtinctYear>=0};
            // Global biological extinction records have no cultural attribution;
            // their old default Tradition=0 must not turn into a fabricated link.
            return e.Kind=="extinction"?Fact(HomininView.Date(s,e.Year),e.Text,lineage):Fact(HomininView.Date(s,e.Year),e.Text,lineage,PublishedTradition(s,e.Tradition));
        }
        public static AtlasTimeline Timeline(PrehistoryState s,string kind="world",int id=-1,int offset=0,bool includeRoutine=false)
        {
            var page=new AtlasTimeline{IncludeRoutine=includeRoutine};
            var events=s.DeepPast.Events.Select((e,i)=>(Event:e,Order:i)).Where(v=>kind=="world"||kind=="lineage"&&v.Event.Lineage==id||(kind=="people"||kind=="technology")&&v.Event.Kind!="extinction"&&v.Event.Tradition==id).OrderByDescending(v=>v.Event.Year).ThenByDescending(v=>v.Order).ToArray();
            page.Total=events.Length;int last=page.Total==0?0:(page.Total-1)/page.PageSize*page.PageSize;page.Offset=Math.Min(last,Math.Max(0,offset)/page.PageSize*page.PageSize);
            page.Records=events.Skip(page.Offset).Take(page.PageSize).Select(v=>Record(s,v.Event)).ToArray();return page;
        }
        public static AtlasFact NewsDetail(PrehistoryState s,int year,int tradition,int cell,ChronicleCategory category)
        {
            int at=Array.FindLastIndex(s.Society.Events,e=>e.Year==year&&e.CultureId==tradition&&e.Cell==cell&&e.Category==category);
            return at>=0&&at<s.DeepPast.Events.Length&&s.DeepPast.Events[at].Year==year&&s.DeepPast.Events[at].Tradition==tradition?Record(s,s.DeepPast.Events[at]):Fact(HomininView.Date(s,year),"Open recorded history for the regional context.");
        }
        public static AtlasDocument Read(PrehistoryState s,string kind="world",int id=-1,World? world=null,Prehistory? engine=null)
        {
            var x=new Index(s);var h=x.H;var d=new AtlasDocument{Kind=kind,Id=id,Year=s.Years,EpochYear=SimulationClockView.EpochYear(s),Subtitle=HomininView.Date(s),SnapshotDate=HomininView.Date(s),Note="Regional population estimates and inherited identities. A regional group is not an individually simulated person or a placed NPC tribe."};
            var sections=new List<AtlasSection>();
            if(AtlasNavigation.IsDirectory(kind)){d.Title=Category(kind);d.Note="";return d;}
            if(kind=="view"){d.Title="Map settings";d.Note="";return d;}
            d.Title="Record unavailable";d.Introduction="This category or record is not present in the early-history model.";
            if(kind=="world")
            {
                var stages=HomininView.Stages(s)??throw new InvalidOperationException("An early-history stage publication is required.");
                d.Title="Early human world";d.Introduction=s.Started?"Biological ancestry, learned traditions and communication identities are recorded independently, from the founding populations onward.":"The early Homo scenario is prepared. Start the simulation to establish its founding population.";
                d.Facts=new[]{Fact("People",N(s.Population.Sum())),Fact("Regional populations",N(x.Residents.Length)),Fact("Living lineages",N(x.Residents.Select(p=>p.Lineage).Distinct().Count())),Fact("Living traditions",N(x.Traditions.Count)),Fact("Communication identities",N(x.Languages.Count)),Fact("Technical frontier",stages.Population>0?HomininHistory.StageName(stages.Frontier):"No resident traditions"),Fact("Most common stage",stages.Population>0?HomininHistory.StageName(stages.Predominant):"No resident traditions")};
                sections.Add(new AtlasSection{Title="Technical stages",Text=stages.Description,Bars=true,Unit="people",Links=Enumerable.Range(0,stages.PeopleByStage.Length).Where(i=>stages.PeopleByStage[i]>0).Select(i=>new AtlasLink{Name=HomininHistory.StageName((LithicStage)i),Value=stages.PeopleByStage[i],Detail=N(stages.PeopleByStage[i])+" people"}).ToArray()});
                sections.Add(Links("Regional populations",x.Residents.OrderByDescending(People).ThenBy(p=>p.Id).Take(12).Select(x.Population),"Largest current regional groups; the directory includes every recorded living group."));
                sections.Add(Links("Biological lineages",h.Lineages.Where(l=>l.ExtinctYear<0||x.Count(x.Lineages,l.Id)>0).Select(l=>x.Lineage(l.Id)),"Lineages are biological records, not ethnic or cultural categories. Extinct lineages remain in historical records."));
                sections.Add(Links("Traditions and practices",h.Traditions.Where(t=>x.Count(x.Traditions,t.Id)>0).OrderByDescending(t=>x.Count(x.Traditions,t.Id)).Take(12).Select(t=>x.Tradition(t.Id)),"Technical stages describe local traditions; they are not one global age imposed on every population."));
                sections.Add(new AtlasSection{Title="Scope of this world",Text="Historical climate and resource changes affect these regional accounts. Rendered ice sheets, demographic NPC tribes and the later city/state handoff are not yet connected to this model."});
            }
            else if(kind=="population"&&x.Residents.FirstOrDefault(v=>v.Id==id) is HomininPopulation population)
            {
                d.Title=x.Population(population).Name;d.Cell=Cell(engine,population.Region);d.Introduction=N(People(population))+" people are represented by this regional population, following "+h.Traditions[population.Tradition].Name+".";
                d.Facts=new[]{Fact("People",N(People(population))),Fact("Biological lineage",h.Lineages[population.Lineage].Name,x.Lineage(population.Lineage)),Fact("Learned tradition",h.Traditions[population.Tradition].Name,x.Tradition(population.Tradition)),Fact("Communication identity",x.LanguageName(population.Language),x.Language(population.Language)),Fact("Established",HomininView.Date(s,population.FoundedYear))};
                if(engine!=null)sections.Add(new AtlasSection{Title="Regional setting",Text=engine.PatchName(population.Region)});
                sections.Add(Links("Biological ancestry",population.Ancestry.Where(a=>a.Share>=.005).Select(a=>{var link=x.Lineage(a.Lineage);link.Detail=(a.Share*100).ToString("F1",CultureInfo.InvariantCulture)+"%";return link;}),"Components of at least 0.5% shown. Smaller components remain in the saved ancestry; this is separate from learned culture."));
                sections.Add(new AtlasSection{Title="Learning and communication",Facts=new[]{Fact("Communication proxy",(population.Communication*100).ToString("F0",CultureInfo.InvariantCulture)),Fact("Learning proxy",(population.Learning*100).ToString("F0",CultureInfo.InvariantCulture))},Text="Aggregate model traits, not a reconstruction of speech, syntax or intelligence."});
                var habitat=h.Habitats.FirstOrDefault(e=>e.Region==population.Region);if(habitat!=null)sections.Add(new AtlasSection{Title="Historical environment",Facts=new[]{Fact("Temperature from map baseline",habitat.TemperatureDelta.ToString("F1",CultureInfo.InvariantCulture)+" °C"),Fact("Rainfall multiplier",habitat.RainFactor.ToString("F2",CultureInfo.InvariantCulture)),Fact("Woody cover",(habitat.WoodyCover*100).ToString("F0",CultureInfo.InvariantCulture)+"%"),Fact("Ice suitability",(habitat.Ice*100).ToString("F0",CultureInfo.InvariantCulture)+"%")},Text="Regional historical estimates; the rendered landscape is not yet rebuilt from these values."});
            }
            else if(kind=="lineage"&&id>=0&&id<h.Lineages.Length)
            {
                var l=h.Lineages[id];d.Title=l.Name;d.Cell=Cell(engine,l.OriginRegion);d.Introduction=l.ExtinctYear<0?"An extant biological lineage. Its populations can follow different traditions and communication identities.":x.Count(x.Lineages,id)>0?"This saved lineage has an extinction date, but still has represented populations. The inherited record is inconsistent; its date has not been silently rewritten.":"This lineage is no longer recorded as a distinct living population. Its ancestry may survive in descendants or mixed populations.";
                d.Facts=new[]{Fact("People in this lineage",N(x.Count(x.Lineages,id))),Fact("Recorded",HomininView.Date(s,l.FoundedYear)),Fact("Status",LineageStatus(x,l)),Fact("Class",l.SapiensLike?"Sapiens-like":"Other hominin")};
                sections.Add(Links("Branching ancestry",(l.Parent<0?Array.Empty<AtlasLink>():new[]{x.Lineage(l.Parent)}).Concat(h.Lineages.Where(v=>v.Parent==id).Select(v=>x.Lineage(v.Id))),"Recorded parent and descendant lineages; cultural descent is tracked separately."));
                sections.Add(Links("Founding ancestry",l.FoundingAncestry.Where(a=>a.Share>=.005).Select(a=>{var link=x.Lineage(a.Lineage);link.Detail=(a.Share*100).ToString("F1",CultureInfo.InvariantCulture)+"%";return link;})));
                sections.Add(Links("Current populations",x.Residents.Where(p=>p.Lineage==id).OrderByDescending(People).Take(24).Select(x.Population)));
            }
            else if((kind=="people"||kind=="technology")&&id>=0&&id<h.Traditions.Length)
            {
                var t=h.Traditions[id];d.Title=(kind=="technology"?"Practices · ":"")+t.Name;d.Cell=Cell(engine,t.OriginRegion);d.Introduction="An inherited learned tradition represented by "+N(x.Count(x.Traditions,id))+" people. Knowledge can spread between biological lineages.";
                d.Facts=new[]{Fact("Population",N(x.Count(x.Traditions,id))),Fact("Local technical stage",Stage(h,t)),Fact("Established",HomininView.Date(s,t.FoundedYear)),Fact("Recorded practices",N(Practices(t).Length))};
                sections.Add(Links("Learned practices",Practices(t).Select(name=>new AtlasLink{Name=name,Detail="Recorded practice"}),"These are recorded historical capabilities. Prototype survival recipes are not yet restricted by the player's historical tradition."));
                sections.Add(Links("Cultural descent",(t.Parent<0?Array.Empty<AtlasLink>():new[]{x.Tradition(t.Parent)}).Concat(h.Traditions.Where(v=>v.Parent==id).Select(v=>x.Tradition(v.Id))),"Learned culture does not automatically follow biological ancestry."));
                sections.Add(Links("Current populations",x.Residents.Where(p=>p.Tradition==id).OrderByDescending(People).Take(24).Select(x.Population)));
            }
            else if(kind=="language"&&id>=0&&id<h.NextLanguage)
            {
                var l=h.Version>=2?h.Languages[id]:null;d.Title=x.LanguageName(id);d.Cell=l==null?-1:Cell(engine,l.OriginRegion);d.Introduction="A recorded communication identity, represented by "+N(x.Count(x.Languages,id))+" people. This archive does not yet simulate vocabulary, scripts or grammar.";
                d.Facts=new[]{Fact("People represented",N(x.Count(x.Languages,id))),Fact("Recorded",l==null||l.FoundedYear<0?"Unrecorded":HomininView.Date(s,l.FoundedYear)),Fact("Status",x.Count(x.Languages,id)>0?"Currently represented":"No current population")};
                sections.Add(Links("Communication ancestry",(l?.Parent>=0?new[]{x.Language(l.Parent)}:Array.Empty<AtlasLink>()).Concat(h.Version>=2?h.Languages.Where(v=>v.Parent==id).Select(v=>x.Language(v.Id)):Array.Empty<AtlasLink>()),l==null||l.LegacyOriginUnknown?"Earlier parentage was not recorded in this save and has not been invented.":"Recorded parent and descendant communication identities."));
                sections.Add(Links("Current populations",x.Residents.Where(p=>p.Language==id).OrderByDescending(People).Take(24).Select(x.Population)));
            }
            if(world!=null&&d.Cell>=0&&d.Cell<world.Surface.Cells.Length){var location=SocietyView.Location(world,d.Cell);d.Longitude=Math.Atan2(location.Y,location.X)*180/Math.PI;d.Latitude=Math.Asin(location.Z)*180/Math.PI;}
            if(kind=="world"||kind=="lineage"||kind=="people"||kind=="technology")
            {
                var history=Timeline(s,kind,id);
                sections.Add(new AtlasSection{Title="Recorded history",Text=history.Total==0?"No events have been recorded for this subject.":"Recorded events, most recent first.",Timeline=history.Records});
            }
            d.Sections=sections.ToArray();return d;
        }
    }
}
