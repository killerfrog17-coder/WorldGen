using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    // Read-only, on-demand documents. These are not part of a world save or its hash.
    [Serializable] public sealed class AtlasLink
    {
        public string Kind="",Name="",Detail="",Context=""; public int Id,Cell=-1; public long Value; public bool Historical;
        public AtlasMeasure[] Measures=Array.Empty<AtlasMeasure>();
    }
    [Serializable] public sealed class AtlasFact { public string Label="",Value="",Swatch=""; public AtlasLink[] Related=Array.Empty<AtlasLink>(); }
    [Serializable] public sealed class AtlasSection
    {
        public string Title="",Text="",Unit="";
        public AtlasFact[] Facts=Array.Empty<AtlasFact>();
        public AtlasLink[] Links=Array.Empty<AtlasLink>();
        public AtlasFact[] Timeline=Array.Empty<AtlasFact>();
        public bool Bars,Diagram,CompactGrid;
        public TechnicalProgress[] Technical=Array.Empty<TechnicalProgress>();
    }
    [Serializable] public sealed class AtlasDocument
    {
        public string Kind="world",Title="World at a glance",Subtitle="",Introduction="",Note="";
        public int Id=-1,Cell=-1,Year,EpochYear=-1;
        public double Longitude,Latitude;
        public AtlasFact[] Facts=Array.Empty<AtlasFact>();
        public AtlasSection[] Sections=Array.Empty<AtlasSection>();
        public HabitatPortrait? Habitat;
        public HumanAppearance? Appearance;
    }
    public static partial class HistoryAtlas
    {
        static string Y(PrehistoryState s,int year)=>SimulationClockView.YearNumber(year,SimulationClockView.EpochYear(s));
        static string N(long n)=>n.ToString("N0",CultureInfo.InvariantCulture);
        static AtlasFact F(string label,string value)=>new AtlasFact{Label=label,Value=value};
        static bool RegimeSuccessor(PrehistoryState s,HistoricalPolity p)
            =>p.ParentId>=0&&s.History.International!=null&&p.ParentId<s.History.International.Accounts.Length&&s.History.International.Accounts[p.ParentId].ContinuitySuccessor==p.Id;
        static AtlasLink StateLink(PrehistoryState s,HistoricalPolity p,string detail="",long value=0)=>new AtlasLink{Kind="state",Id=p.Id,Name=p.Name,Cell=s.Society.Settlements[p.CapitalId].Cell,Detail=detail,Context=(s.History.Aggregates?.Empires?.Connections?.Realms!=null?"Country founded ":RegimeSuccessor(s,p)?"Regime since ":"Founded ")+Y(s,HistoricalRealmLifecycles.Founded(s,p.Id))+" · "+s.Society.Settlements[p.CapitalId].Name,Value=value,Historical=p.EndedYear>=0};
        static AtlasLink TownLink(PrehistoryState s,Settlement t)=>new AtlasLink{Kind="city",Id=t.Id,Name=t.Name,Cell=t.Cell,Detail=N(t.Population)+" residents",Context="Founded "+Y(s,t.FoundedYear),Value=t.Population,Historical=t.Population==0};
        static AtlasLink CultureLink(CultureIdentity c)=>new AtlasLink{Kind="people",Id=c.Id,Name=c.Name,Cell=c.OriginCell,Detail=N(c.Population)+" people",Value=c.Population};
        static AtlasLink FaithLink(HistoricalReligion r)=>new AtlasLink{Kind="religion",Id=r.Id,Name=r.Name,Cell=r.HolyCell,Detail=r.Tradition};
        static AtlasLink LeaderLink(PrehistoryState s,HistoricalLeader l)=>new AtlasLink{Kind="ruler",Id=l.Id,Name=l.Name,Cell=s.Society.Settlements[s.History.Polities[l.PolityId].CapitalId].Cell,Detail="Years "+Y(s,l.StartedYear)+"–"+(l.EndedYear<0?"present":Y(s,l.EndedYear)),Context=s.History.Polities[l.PolityId].Name+" · accession "+Y(s,l.StartedYear),Historical=l.EndedYear>=0||s.History.Polities[l.PolityId].EndedYear>=0};
        static AtlasSection Links(string title,IEnumerable<AtlasLink> links,string text="",bool bars=false,string unit="")=>new AtlasSection{Title=title,Text=text,Links=links.ToArray(),Bars=bars,Unit=unit};
        static AtlasSection Text(string title,string text)=>new AtlasSection{Title=title,Text=text};
        static AtlasSection Events(PrehistoryState s,Func<HistoricalEvent,bool> includes,bool routine=false)
        {
            var events=s.History.Archive.Where(includes).Where(e=>routine||!RoutineKinds.Contains(e.Kind)).OrderByDescending(e=>e.Year).ThenByDescending(e=>e.Id).Take(40).ToArray();
            return new AtlasSection{Title="Recorded history",Text=events.Length==0?"No major events are recorded for this subject yet.":"Most recent milestones first. Browse older pages or include routine records for the complete archive.",Timeline=events.Select(e=>Record(s,e)).ToArray()};
        }
        public static AtlasLink? Selection(PrehistoryState? s,int cell,HistoryOverlay overlay)
        {
            if(s?.Society==null||s.History==null||cell<0||cell>=HistoricalCells.Count(s))return null;
            int id=SocietyView.SelectionId(s,cell,overlay);if(id==-1)return null;
            if(id<=-2)return CultureLink(s.Society.Cultures[-2-id]);
            switch(overlay)
            {
                case HistoryOverlay.States:case HistoryOverlay.Territories:return StateLink(s,s.History.Polities[id]);
                case HistoryOverlay.Civilizations:case HistoryOverlay.Cultures:return CultureLink(s.Society.Cultures[id]);
                case HistoryOverlay.Provinces:return ProvinceLink(s,id);
                case HistoryOverlay.Religions:return FaithLink(s.History.Religions[id]);
                case HistoryOverlay.ReligiousInstitutions:return ReligiousBodyLink(s,id/2,id%2);
                case HistoryOverlay.Languages:return SpokenLanguageLink(s,id,cell);
                case HistoryOverlay.Ethnicities:return EthnicLink(s,id,cell);
                case HistoryOverlay.Technology:return TechnologyLink(s,id,cell);
                default:return null;
            }
        }
        public static string Summary(PrehistoryState? s,AtlasLink? link)
        {
            if(s?.History==null||s.Society==null||link==null)return "Select an inhabited location to explore its history.";
            switch(link.Kind)
            {
                case "technology":return WorldEraView.Name(WorldEraView.KnowledgeEra(s.History.Cultures[link.Id]))+" · "+s.Society.Cultures[link.Id].Name;
                case "state":return StateSummary(s,s.History.Polities[link.Id]);
                case "province":return ProvinceSummary(s,link.Id);
                case "claim":return ClaimSummary(s,link.Id);
                case "city":var t=s.Society.Settlements[link.Id];return t.Name+" is home to "+N(t.Population)+" residents of the "+s.Society.Cultures[t.CultureId].Name+" people. "+(t.PolityId>=0?"It belongs to "+s.History.Polities[t.PolityId].Name+".":"It is outside centralized state administration.")+" The settlement dates to year "+Y(s,t.FoundedYear)+".";
                case "people":var c=s.Society.Cultures[link.Id];return "The "+c.Name+" people number "+N(c.Population)+". Their recorded way of life centers on "+c.Subsistence.ToLowerInvariant()+". Explore their profile for cultural contacts, crops, and adopted practices.";
                case "religion":var r=s.History.Religions[link.Id];return r.Name+" originated among the "+s.Society.Cultures[r.CultureId].Name+" people in year "+Y(s,r.FoundedYear)+". "+r.Tradition+". Its recorded ethos is "+r.Ethos+".";
                default:return link.Name;
            }
        }
        static string StateSummary(PrehistoryState s,HistoricalPolity p)
        {
            if(p.EndedYear>=0)return p.Name+(RegimeSuccessor(s,p)?"'s government took office in year ":" was founded in year ")+Y(s,p.FoundedYear)+" and its government ended in year "+Y(s,p.EndedYear)+". Its people and cities may survive under later courts.";
            string text=p.Name+" is based at "+s.Society.Settlements[p.CapitalId].Name+" and governed under "+p.Government.ToLowerInvariant()+". It directly administers "+N(p.Population)+" people; "+p.Ruler+" currently rules.";
            int patron=HistoricalImperialHierarchy.Patron(s,p.Id),subjects=s.History.Aggregates?.Empires?.Governments[p.Id].SubjectCount??0;
            if(patron>=0)text+=" It retains its local institutions as a constituent of "+s.History.Polities[patron].Name+".";
            if(subjects>0)text+=" Including "+N(subjects)+" constituent governments, its realm contains "+N(HistoricalImperialHierarchy.Population(s,p.Id))+" people.";
            if(s.History.Aggregates?.Empires?.Connections?.Realms!=null)return text+" Its state institutions date to year "+Y(s,HistoricalRealmLifecycles.Founded(s,p.Id))+".";
            return text+(RegimeSuccessor(s,p)?" Its current government dates to year ":" Founded in year ")+Y(s,p.FoundedYear)+".";
        }
        public static AtlasLink[] ProfilesAt(PrehistoryState? s,int cell)
        {
            if(s?.History==null||s.Society==null||cell<0||cell>=HistoricalCells.Count(s))return Array.Empty<AtlasLink>();
            var links=new List<AtlasLink>();int state=HistoricalCells.Polity(s,cell),faith=HistoricalCells.Religion(s,cell);
            int claim=HistoricalRecognizedClaims.TitleAt(s,cell);if(claim>=0&&HistoricalRecognizedClaims.Holder(s,cell)>=0)links.Add(ClaimLink(s,claim));
            int sovereign=state<0?-1:HistoricalImperialHierarchy.Sovereign(s,state);
            if(state>=0)links.Add(StateLink(s,s.History.Polities[state],sovereign==state?"Sovereign state":"Local government"));
            int province=HistoricalProvinceIdentity.Of(s,cell);if(province>=0)links.Add(ProvinceLink(s,province));
            if(sovereign>=0&&sovereign!=state)links.Insert(0,StateLink(s,s.History.Polities[sovereign],"Imperial government"));
            var town=s.Society.Settlements.FirstOrDefault(t=>t.Cell==cell&&t.Population>0);if(town!=null)links.Add(TownLink(s,town));
            var culture=SocietyView.Culture(s,cell);if(culture==null&&state>=0)culture=s.Society.Cultures[s.History.Polities[state].CultureId];
            if(culture!=null)links.Add(CultureLink(culture));if(faith>=0)links.Add(FaithLink(s.History.Religions[faith]));int voice=Prehistory.PrimarySpokenLanguageAt(s,cell);if(voice>=0)links.Add(SpokenLanguageLink(s,voice,cell));int identity=Prehistory.PrimaryEthnicityAt(s,cell);if(identity>=0)links.Add(EthnicLink(s,identity,cell));return links.ToArray();
        }
        public static string ProfileLabel(AtlasLink link)=>link.Kind=="notable"?"Notable person":link.Kind=="ethnicity"?"Ethnic identity":link.Kind=="language"?"Language":link.Kind=="organization"?"Organization":link.Kind=="technology"?"Technology":link.Kind=="claim"?"Title":link.Kind=="province"?"Province":link.Kind=="religion"?"Belief":link.Kind=="people"?"People":link.Kind=="city"?"City":link.Kind=="ruler"?"Ruler":link.Detail=="Imperial government"?"Realm":link.Detail=="Local government"?"Local state":"State";
        public static AtlasLink[] Directory(PrehistoryState? s,string kind,string search="",bool past=false)
        {
            if(s?.Society==null||s.History==null)return Array.Empty<AtlasLink>();
            var h=s.History;IEnumerable<AtlasLink> entries;
            switch(kind)
            {
                case "organization":entries=(h.International?.Organizations??Array.Empty<HistoricalInternationalOrganization>()).Where(o=>past||o.EndedYear<0).OrderByDescending(o=>o.Members.Count).ThenBy(o=>o.Id).Select(o=>OrganizationLink(s,o));break;
                case "technology":entries=s.Society.Cultures.Where(c=>c.Population>0||past).OrderByDescending(c=>WorldEraView.KnowledgeEra(h.Cultures[c.Id])).ThenBy(c=>c.Id).Select(c=>TechnologyLink(s,c.Id,c.OriginCell));break;
                case "province":entries=ProvinceDirectory(s);break;
                case "state":entries=h.Polities.Where(p=>past||p.EndedYear<0).OrderByDescending(p=>p.Population).ThenBy(p=>p.Id).Select(p=>StateLink(s,p,p.EndedYear<0?p.Government:"Ended in year "+Y(s,p.EndedYear),p.Population));break;
                case "city":entries=s.Society.Settlements.Where(t=>past||t.Population>0).OrderByDescending(t=>t.Population).ThenBy(t=>t.Id).Select(t=>TownLink(s,t));break;
                case "people":entries=s.Society.Cultures.OrderByDescending(c=>c.Population).ThenBy(c=>c.Id).Select(CultureLink);break;
                case "religion":entries=h.Religions.Select(FaithLink);break;
                case "language":entries=(h.Languages?.Spoken?.Varieties??Array.Empty<HistoricalSpeechVariety>()).Select(v=>SpokenLanguageLink(s,v.Id,v.Cell));break;
                case "ethnicity":entries=(h.Identities?.Peoples??Array.Empty<HistoricalPeopleIdentity>()).Select(v=>EthnicLink(s,v.Id,v.HomeCell));break;
                case "religious-body":entries=s.Society.Settlements.SelectMany(t=>new[]{0,1}.Select(slot=>Prehistory.ReligiousInstitutionAt(s,t.Cell,slot))).Where(b=>b!=null&&b.Support>=100).OrderBy(b=>b.Town).ThenBy(b=>b.Slot).Select(b=>ReligiousBodyLink(s,b.Town,b.Slot));break;
                case "ruler":entries=(h.Dynamics?.Leaders??Array.Empty<HistoricalLeader>()).Where(l=>past||l.EndedYear<0&&h.Polities[l.PolityId].EndedYear<0).OrderByDescending(l=>l.Renown).ThenBy(l=>l.Id).Select(l=>LeaderLink(s,l));break;
                case "notable":entries=h.Notables==null?Array.Empty<AtlasLink>():h.Notables.People.Where(p=>past||p.DeathYear<0).OrderByDescending(p=>p.Renown).ThenBy(p=>p.Id).Select(p=>NotableLink(s,p.Id));break;
                default:return Array.Empty<AtlasLink>();
            }
            return entries.Where(e=>e.Name.IndexOf(search??"",StringComparison.OrdinalIgnoreCase)>=0).Select(e=>{e.Measures=AtlasTables.Measures(s,e);return e;}).ToArray();
        }
        public static AtlasDocument Read(PrehistoryState? s,string kind="world",int id=-1,World? world=null,Prehistory? engine=null)
        {
            var d=new AtlasDocument{Kind=kind,Id=id,Year=s?.Years??0,EpochYear=SimulationClockView.EpochYear(s),Subtitle=SimulationClockView.Year(s,s?.Years??0),Note="Generated history. Indicators are model estimates; populations use aggregate accounts, not an individual census."};
            if(AtlasNavigation.IsDirectory(kind)){d.Title=AtlasNavigation.CategoryName(kind);d.Note="";return d;}
            if(kind=="view"){d.Title="Map settings";d.Note="";return d;}
            if(s?.Society==null||s.History==null){d.Introduction=(s==null?"":N(SocietyView.Population(s))+" people are currently present. ")+"Human history has not yet produced recorded cultures, cities, or states. Run the simulation to begin building this world's atlas.";if(s!=null)d.Facts=new[]{F("People",N(SocietyView.Population(s)))};return d;}
            var h=s.History;var sections=new List<AtlasSection>();
            switch(kind)
            {
                case "world":Overview(s,d,sections);WorldExhibits(s,engine,sections);break;
                case "organization":Organization(s,id,d,sections);break;
                case "technology" when id>=0&&id<h.Cultures.Length:Technology(s,id,d,sections);break;
                case "province":Province(s,id,d,sections);break;
                case "claim":Claim(s,id,d,sections);break;
                case "state" when id>=0&&id<h.Polities.Length:State(s,h.Polities[id],d,sections);break;
                case "city" when id>=0&&id<s.Society.Settlements.Length:City(s,s.Society.Settlements[id],d,sections);break;
                case "people" when id>=0&&id<s.Society.Cultures.Length:People(s,s.Society.Cultures[id],d,sections,world);break;
                case "religion" when id>=0&&id<h.Religions.Length:Religion(s,h.Religions[id],d,sections);break;
                case "religious-body" when id>=0&&id<s.Society.Settlements.Length*2:ReligiousBody(s,id,d,sections);break;
                case "language" when id>=0&&id<(h.Languages?.Spoken?.Varieties.Length??0):SpokenLanguage(s,id,d,sections);break;
                case "ethnicity" when id>=0&&id<(h.Identities?.Peoples.Length??0):EthnicPeople(s,id,d,sections);break;
                case "ruler" when id>=0&&id<(h.Dynamics?.Leaders.Length??0):Ruler(s,h.Dynamics!.Leaders[id],d,sections);break;
                case "notable" when id>=0&&id<(h.Notables?.People.Count??0):NotableBiography(s,id,d,sections);break;
                default:d.Title="Record unavailable";d.Introduction="This record is not present in the current world.";break;
            }
            if(world!=null&&d.Cell>=0&&d.Cell<world.Surface.Cells.Length)
            {
                var location=SocietyView.Location(world,d.Cell);d.Longitude=Math.Atan2(location.Y,location.X)*180/Math.PI;d.Latitude=Math.Asin(location.Z)*180/Math.PI;
                if(kind!="technology")
                {
                    d.Habitat=HabitatPortrait.At(world,d.Cell);
                    var setting=Setting(world,d.Cell,kind=="state"||kind=="ruler"?"Capital's setting":"Local setting");
                    if(setting!=null)sections.Insert(Math.Min(1,sections.Count),setting);
                }
            }
            ColonialSections(s,kind,id,sections);LivingWorldSections(s,kind,id,d.Cell,world,engine,sections,d);d.Sections=sections.ToArray();return d;
        }
        static AtlasSection? Setting(World world,int cell,string title)
        {
            if(world.Hydrology.OceanConnected.Length>cell&&world.Hydrology.OceanConnected[cell])
            {
                var islands=SubcellGeography.Find(world,cell);if(islands==null)return Text(title,"The recorded location is in an ocean-connected parent cell. Land-specific local environmental detail is unavailable here.");
                return new AtlasSection{Title=title,Text="The recorded location is represented on the largest sub-cell island within an ocean-connected map cell. The parent cell's ocean biome and soil are not treated as an island land environment; finer island habitat is not recorded yet.",Facts=new[]{F("Sub-cell islands",N(islands.IslandCount)),F("Total represented island area",(islands.TotalAreaM2/1e6).ToString("0.#",CultureInfo.InvariantCulture)+" km²"),F("Largest represented island",(islands.LargestIslandAreaM2/1e6).ToString("0.#",CultureInfo.InvariantCulture)+" km²")}};
            }
            if(world.Ecology==null||cell>=world.Ecology.Habitat.Length)return null;
            var habitat=world.Ecology.Habitat[cell];int region=world.Ecology.Region[cell];
            string biome=System.Text.RegularExpressions.Regex.Replace(habitat.Biome.ToString(),"([a-z])([A-Z])","$1 $2").ToLowerInvariant();
            string flood=habitat.Flooding==FloodRegime.RiverSeasonal?"Seasonal river flooding is recorded in this cell.":habitat.Flooding==FloodRegime.RiverPersistent?"Persistent river-associated flooding is recorded here.":habitat.Flooding==FloodRegime.Tidal?"This cell contains tidal wetland habitat.":habitat.Flooding==FloodRegime.LakeMargin?"Lake-margin flooding is recorded here.":"No modeled flood regime is recorded in this cell.";
            string location=region>=0?"The recorded location lies within "+world.Ecology.Regions[region].Name+", a "+biome+" ecoregion.":"The parent map cell is classified as "+biome+".";
            return new AtlasSection{Title=title,Text=location+" "+flood+" These describe the generated map's cell-scale environment, not precise streets, buildings, or a reconstructed climate for the current historical year.",Facts=new[]{F("Biome",biome),F("Soil",habitat.Soil.ToString()),F("Mean map temperature",(habitat.MeanTenthsC/10.0).ToString("0.#",CultureInfo.InvariantCulture)+" °C"),F("Growing season",N(habitat.GrowingDays)+" days"),F("Seasonal flood duration",N(habitat.FloodDaysPerYear)+" days per map year"),F("Floodplain fraction",(habitat.FloodFractionPermille/10.0).ToString("0.#",CultureInfo.InvariantCulture)+"% of parent cell")}};
        }
        static void Overview(PrehistoryState s,AtlasDocument d,List<AtlasSection> a)
        {
            var h=s.History;var states=h.Polities.Where(p=>p.EndedYear<0).OrderByDescending(p=>p.Population).ThenBy(p=>p.Id).ToArray();var towns=s.Society.Settlements.Where(t=>t.Population>0).ToArray();
            int governments=states.Length;states=states.Where(p=>HistoricalImperialHierarchy.Patron(s,p.Id)<0).OrderByDescending(p=>HistoricalImperialHierarchy.Population(s,p.Id)).ThenBy(p=>p.Id).ToArray();
            long global=SocietyView.Population(s),urban=towns.Sum(t=>t.Population);int wars=h.Relations.Count(r=>r.WarSince>=0&&h.Polities[r.A].EndedYear<0&&h.Polities[r.B].EndedYear<0);
            d.Introduction="This world has "+N(global)+" people, "+N(states.Length)+" surviving states, and "+N(towns.Length)+" inhabited named settlements. "+(states.Length==0?"Political life is still organized outside centralized states.":states[0].Name+" is the most populous surviving state; political and cultural boundaries are not necessarily the same.")+" "+(wars==0?"No interstate wars are currently recorded.":N(wars)+" interstate wars are currently active.");
            d.Facts=new[]{F("People",N(global)),F("Named-settlement residents",N(urban)+(global>0?" ("+(100.0*urban/global).ToString("0.#",CultureInfo.InvariantCulture)+"% of all people)":"")),F("Surviving states",N(states.Length)),F("Active wars",N(wars))};
            if(h.Aggregates?.Empires!=null){d.Introduction+=" These overarching entities include "+N(governments-states.Length)+" constituent governments with their own local institutions.";d.Facts=d.Facts.Concat(new[]{F("Constituent governments",N(governments-states.Length))}).ToArray();}
            d.Note+=" Simulation rules: "+h.Version+".";
            a.Add(Links("Political centers",states.Take(12).Select(p=>StateLink(s,p,N(HistoricalImperialHierarchy.Population(s,p.Id))+" people · "+p.Government,HistoricalImperialHierarchy.Population(s,p.Id))),"Compare overarching populations, including constituent governments. People outside these entities are not missing from the world total.",true,"people"));
            a.Add(Links("Urban centers",towns.OrderByDescending(t=>t.Population).ThenBy(t=>t.Id).Take(12).Select(t=>TownLink(s,t)),"Named-settlement population only, not the surrounding rural catchment.",true,"residents"));
            a.Add(Text("Reading this moment",(global==0?"No population is currently recorded.":"About "+(100.0*urban/global).ToString("0.#",CultureInfo.InvariantCulture)+"% of the world's people live in named settlements; the rest are represented by rural communities.")+" "+(states.Length>0?states[0].Name+" is the largest surviving court by population, but size alone says nothing about how secure its rule is.":"No centralized state is currently recorded.")+" "+(wars>0?"Open a belligerent state's diplomatic neighborhood and campaign records to explore the current conflicts.":"Peace in the interstate records does not imply that every community is stable or prosperous.")));
            var ties=h.Relations.Where(r=>h.Polities[r.A].EndedYear<0&&h.Polities[r.B].EndedYear<0&&(r.WarSince>=0||r.Alliance||r.Trade)).OrderByDescending(r=>r.WarSince>=0).ThenBy(r=>r.A).ThenBy(r=>r.B);
            a.Add(new AtlasSection{Title="Connections and fault lines",Text="Recorded interstate ties, not every possible route. Select a state for its diplomatic neighborhood.",Links=ties.Take(40).Select(r=>StateLink(s,h.Polities[r.A],Relation(s,r)+" — "+h.Polities[r.B].Name)).ToArray()});
            a.Add(Links("Peoples and ways of life",s.Society.Cultures.OrderByDescending(c=>c.Population).ThenBy(c=>c.Id).Take(12).Select(CultureLink),"Peoples can live under several states; a civilization is not necessarily a united empire.",true,"people"));
            if(h.International!=null)a.Add(Links("International organizations",h.International.Organizations.Where(o=>o.EndedYear<0).OrderByDescending(o=>o.Members.Count).ThenBy(o=>o.Id).Take(12).Select(o=>OrganizationLink(s,o)),"Actual member governments and paid common funds; membership does not guarantee peace."));
            a.Add(new AtlasSection{Title="Knowledge across the world",Text="How many recorded cultures have adopted each practice. These are independent capabilities, not a single progress score. "+(h.Modern==null?"This save retains its medieval technology ceiling.":"Further development is limited to "+h.Modern.TechnologyCeiling+" practices; adoption and infrastructure effects take time."),Bars=true,Unit="cultures",Links=Enumerable.Range(0,HistoricalTechnologies.Count(h.Version)).Select(k=>new AtlasLink{Name=HistoricalTechnologies.Name(h.Version,k),Value=h.Cultures.Count(c=>HistoricalTechnologies.Has(c,(HistoricalTechnology)k)),Detail=N(h.Cultures.Count(c=>HistoricalTechnologies.Has(c,(HistoricalTechnology)k)))+" of "+N(h.Cultures.Length)+" recorded cultures"}).ToArray()});
            if(h.Aggregates!=null)
            {
                var coverage=HistoricalTechnologyCoverage.Measure(s);
                a.Add(new AtlasSection{Title="Reach of practices",Text="Estimated people represented by communities with each recorded capability. Local city/catchment accounts use their local culture; country remote accounts use their recorded governing institutions; other regional accounts use their recorded regional tradition. This is an institutional-access proxy, not individual literacy, access to electricity, or a census of users. Unassigned foragers are not assumed to possess a practice merely because a distant teacher knows it.",Bars=true,Unit="people",Links=Enumerable.Range(0,HistoricalTechnologies.Count(h.Version)).Select(k=>new AtlasLink{Name=HistoricalTechnologies.Name(h.Version,k),Value=coverage[k],Detail=N(coverage[k])+" people · "+(100.0*coverage[k]/Math.Max(1,global)).ToString("0.#",CultureInfo.InvariantCulture)+"% of world population"}).ToArray()});
            }
            a.Add(Links("Belief and political patronage",h.Religions.Select(r=>new{Religion=r,Courts=states.Count(p=>p.ReligionId==r.Id)}).Where(x=>x.Courts>0).OrderByDescending(x=>x.Courts).ThenBy(x=>x.Religion.Id).Take(12).Select(x=>{var link=FaithLink(x.Religion);link.Value=x.Courts;link.Detail=N(x.Courts)+" affiliated surviving courts";return link;}),"Court affiliations compare political patronage, not numbers of believers. Open a tradition to explore its beliefs and related communities.",true,"courts"));
            a.Add(Events(s,e=>true));
        }
        static string Relation(PrehistoryState s,HistoricalRelation r)=>r.WarSince>=0?"At war since year "+Y(s,r.WarSince):r.Alliance?(r.Trade?"Alliance and trade":"Alliance"):r.Trade?"Trading relationship":"Diplomatic contact";
        static void State(PrehistoryState s,HistoricalPolity p,AtlasDocument d,List<AtlasSection> a)
        {
            var h=s.History;var c=s.Society.Cultures[p.CultureId];var capital=s.Society.Settlements[p.CapitalId];var towns=s.Society.Settlements.Where(t=>t.PolityId==p.Id&&t.Population>0).OrderByDescending(t=>t.Population).ThenBy(t=>t.Id).ToArray();var relations=p.EndedYear>=0?Array.Empty<HistoricalRelation>():h.Relations.Where(r=>(r.A==p.Id||r.B==p.Id)&&h.Polities[r.A==p.Id?r.B:r.A].EndedYear<0).ToArray();
            d.Title=p.Name;d.Cell=capital.Cell;d.Subtitle=p.Government+" · "+(p.EndedYear<0?"Surviving state":"Historical state");
            bool continuity=RegimeSuccessor(s,p);
            var realm=h.Aggregates?.Empires?.Connections?.Realms?.Governments[p.Id];
            d.Introduction=p.Name+(continuity?"'s recorded government took office in year ":" was founded in year ")+Y(s,p.FoundedYear)+" among the "+c.Name+" people. "+(continuity?"It continues the institutions of an earlier government; this is not the first appearance of the country or its people. ":"")+(p.EndedYear<0?"Its court is based at "+capital.Name+", and its current ruler is "+p.Ruler+". It administers "+N(p.Population)+" people across "+N(p.AreaKm2)+" km².":"This government ended in year "+Y(s,p.EndedYear)+". Its people and cities may continue under later governments.");
            if(p.EndedYear<0)d.Introduction+=" "+(relations.Any(r=>r.WarSince>=0)?"War currently shapes its relations with neighboring courts.":relations.Any(r=>r.Trade)?"Recorded trading relationships connect it to neighboring courts.":"No active interstate war or trading treaty is recorded.");
            d.Facts=new[]{F(continuity?"Regime established":"Founded", "Year "+Y(s,p.FoundedYear)),F("Status",p.EndedYear<0?"Surviving":"Ended in year "+Y(s,p.EndedYear)),F("Population",p.EndedYear>=0&&p.LastEstimateYear==0?"No preserved last-active estimate":N(p.Population)+(p.LastEstimateYear>0?" (estimate from year "+Y(s,p.LastEstimateYear)+")":"")),F("Land area",p.EndedYear>=0&&p.LastEstimateYear==0?"No preserved last-active estimate":N(p.AreaKm2)+" km²"),F("Government",p.Government),F("Army levy",N(p.Army)),F("Treasury",N(p.Treasury)+" model units")};
            if(realm!=null)
            {
                d.Facts[0]=F("Country founded","Year "+Y(s,realm.FoundedYear));
                string prefix=p.Name+(continuity?"'s recorded government took office in year ":" was founded in year ")+Y(s,p.FoundedYear);
                d.Introduction=d.Introduction.Replace(prefix,p.Name+" dates its state institutions to year "+Y(s,realm.FoundedYear));
            }
            a.Add(Links("People, court, and belief",new[]{CultureLink(c),TownLink(s,capital)}.Concat(p.ReligionId>=0&&p.ReligionId<h.Religions.Length?new[]{FaithLink(h.Religions[p.ReligionId])}:Array.Empty<AtlasLink>()),"The court's religious affiliation is distinct from the local faith map."));
            if(h.Identities!=null){int ruling=h.Identities.RulingIdentities[p.Id];a.Add(Links("Ruling heritage",new[]{EthnicLink(s,ruling,h.Identities.Peoples[ruling].HomeCell)},"The court's inherited identity is recorded separately from the capital's residents and the other peoples living under its rule."));}
            var capitalWorship=ReligiousLocalSection(s,capital.Cell);if(capitalWorship!=null)a.Add(capitalWorship);
            var capitalSpeech=SpokenLocalSection(s,capital.Cell);if(capitalSpeech!=null)a.Add(capitalSpeech);
            AddIdentityLocalSections(s,capital.Cell,a);
            NotableCourtSection(s,p.Id,a);
            var fieldForces=MilitaryOperationsSection(s,p.Id);if(fieldForces!=null)a.Add(fieldForces);
            var claimants=RealmClaimantsSection(s,p.Id);if(claimants!=null)a.Add(claimants);
            var lineage=(p.ParentId>=0?new[]{StateLink(s,h.Polities[p.ParentId],"Recorded predecessor realm")}:Array.Empty<AtlasLink>()).Concat(h.Polities.Where(x=>x.ParentId==p.Id).OrderBy(x=>x.FoundedYear).ThenBy(x=>x.Id).Select(x=>StateLink(s,x,"Descendant court founded in year "+Y(s,x.FoundedYear)))).ToArray();
            if(lineage.Length>0)a.Add(Links("Political lineage",lineage,"These links record predecessor and descendant courts, not a biological dynasty or proof that their people disappeared."));
            a.Add(Links("Cities",towns.Select(t=>TownLink(s,t)),"Current inhabited sites under this state's administration.",true,"residents"));
            var neighborhood=Links("Diplomatic neighborhood",relations.OrderByDescending(r=>r.WarSince>=0).ThenBy(r=>r.A).ThenBy(r=>r.B).Select(r=>StateLink(s,h.Polities[r.A==p.Id?r.B:r.A],Relation(s,r))),"War, alliance, and exchange are separate relationships. Diagram shows up to eight recorded neighbors, not geographical distance. The list includes all recorded neighbors.");
            neighborhood.Diagram=true;a.Add(neighborhood);
            var deps=h.Dynamics?.Dependencies??Array.Empty<HistoricalDependency>();
            if(h.Aggregates?.Empires!=null&&p.EndedYear<0)
            {
                int patron=HistoricalImperialHierarchy.Patron(s,p.Id);var g=h.Aggregates.Empires.Governments[p.Id];
                var constituents=h.Polities.Where(x=>x.EndedYear<0&&HistoricalImperialHierarchy.Patron(s,x.Id)==p.Id).Select(x=>StateLink(s,x,"Constituent government; local institutions retained",x.Population));
                if(patron>=0)constituents=new[]{StateLink(s,h.Polities[patron],"Overarching imperial government")}.Concat(constituents);
                var imperial=Links("Empire and constituent governments",constituents,"Imperial allegiance governs foreign affairs and tribute, not local culture. Constituents keep their own courts, cities and population accounts. The States map shows the overarching entity; Territories reveals its local governments.",true,"directly administered residents");
                imperial.Facts=new[]{F("Imperial population",N(HistoricalImperialHierarchy.Population(s,p.Id))),F("Imperial area",N(HistoricalImperialHierarchy.AreaKm2(s,p.Id))+" km²"),F("Constituent governments",N(g.SubjectCount)),F("Local autonomy",(g.AutonomyMilli/10.0).ToString("0.0",CultureInfo.InvariantCulture)+" / 100"),F("Annual tribute delivered",N(g.AnnualTribute)+" account units")};imperial.Diagram=true;a.Add(imperial);
            }
            else a.Add(Links("Overlords and dependent courts",deps.Where(x=>x.EndedYear<0&&(x.PatronId==p.Id||x.ClientId==p.Id)).Select(x=>StateLink(s,h.Polities[x.PatronId==p.Id?x.ClientId:x.PatronId],x.ClientId==p.Id?"Receives tribute from this court":"Pays tribute to this court")),"Dependent courts retain their own political identity."));
            if(h.Cultures[p.CultureId].Technical!=null)a.Add(new AtlasSection{Title="Research and practical technology",Text="Specialist communities divide finite effort among concurrent projects. Knowledge can arrive through exchange long before workshops, infrastructure and resources support widespread use. These are the ruling culture's technical networks, not a claim that every resident has identical equipment.",Technical=TechnicalResearchView.Read(s,p.CultureId)});
            var metrics=new List<AtlasFact>{F("Cohesion",N(p.Cohesion)+" / 1,000"),F("Legitimacy",N(p.Legitimacy)+" / 1,000"),F("War exhaustion",N(p.Exhaustion)+" / 1,000")};
            if(realm!=null&&p.EndedYear<0)metrics.Add(F(HistoricalImperialHierarchy.Patron(s,p.Id)>=0?"Present imperial allegiance since":"Independent foreign policy since","Year "+Y(s,h.Aggregates!.Empires!.Governments[p.Id].SinceYear)));
            if(realm!=null)a.Add(new AtlasSection{Title="Country and government continuity",Text="A new ruler or constitution does not found a new country. The contested-court date is not proof of an unchanged dynasty: ordinary rulers, policies and imperial allegiance can change between recorded contests.",Facts=new[]{F("Country founded","Year "+Y(s,realm.FoundedYear)),F("Last contested court change",realm.RegimeChanges>0?"Year "+Y(s,realm.RegimeSince):"None recorded since foundation"),F("Recorded contested court changes",N(realm.RegimeChanges)),F("Succession",realm.CrisisUntil>s.Years?"Central authority contested until year "+Y(s,realm.CrisisUntil):"No active central succession crisis"+(HistoricalRealmClaimantHistory.Enabled(s)?"; local claims can remain":"")),F("Regional court separations",N(realm.RegionalSeparations))}});
            if(h.Dynamics!=null&&p.Id<h.Dynamics.States.Length)
            {
                var ds=h.Dynamics.States[p.Id];metrics.Add(F("Administrative strain",N(ds.Strain)+" / 1,000"));metrics.Add(F("Public works",N(ds.PublicWorks)+" / 1,000"));metrics.Add(F("Peak population",N(ds.PeakPopulation)));metrics.Add(F("Peak area",N(ds.PeakAreaKm2)+" km²"));
                var campaigns=h.Dynamics.Campaigns.Where(x=>x.A==p.Id||x.B==p.Id).ToArray();
                var episodes=h.Archive.Where(e=>(e.PolityId==p.Id||e.OtherPolityId==p.Id)&&new[]{"great-conquest","succession-crisis","realm-succession-crisis","secession","provincial-independence","realm-claimant-government","empire-founded","collapse","dependency-founded","tributary-revolt","dynasty-change"}.Contains(e.Kind)).OrderByDescending(e=>e.Year).ThenByDescending(e=>e.Id).Take(3).OrderBy(e=>e.Year).ThenBy(e=>e.Id).ToArray();
                string arc="Over its recorded history, "+p.Name+" has been involved in "+N(campaigns.Length)+" campaigns. These records contain "+N(campaigns.Sum(x=>(long)x.Conquests))+" settlement captures across both sides, not a count of distinct cities it currently owns. Its highest stored population is "+N(ds.PeakPopulation)+"; the date of that peak is not recorded.";
                if(episodes.Length>0)arc+=" Recent turning points include "+string.Join(" ",episodes.Select(e=>"year "+Y(s,e.Year)+": "+e.Text));
                a.Add(Text("History in brief",arc));
                a.Add(Text("Political character",ds.Tradition+". "+(ds.Strain>=400?"Its modeled administrative burden is high.":"Its modeled administrative burden is comparatively limited.")+(ds.DisputedUntil>s.Years?" A disputed succession remains unresolved.":"")));
                a.Add(Links("Rulers and succession",h.Dynamics.Leaders.Where(l=>l.PolityId==p.Id).OrderByDescending(l=>l.StartedYear).ThenByDescending(l=>l.Id).Take(25).Select(l=>LeaderLink(s,l)),"Most recent rulers first. These are recorded appointments, not a complete biological genealogy."));
                var officeholders=h.Dynamics.Leaders.Where(l=>l.PolityId==p.Id).OrderBy(l=>l.Id).ToArray();var completedTenures=officeholders.Where(l=>l.EndedYear>=0).ToArray();var latest=officeholders.LastOrDefault();
                string successionText=latest==null||string.IsNullOrEmpty(latest.Selection)?"The method of selecting officeholders is not preserved in these records.":"The most recent recorded transfer of leadership was by "+latest.Selection.ToLowerInvariant()+", in year "+Y(s,latest.StartedYear)+".";
                if((h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||(h.Version=="history-16"||h.Aggregates!=null))))))successionText+=" Routine appointments can preserve the authority of continuing councils and institutions; replacing a personal dynasty or imposing a contested successor causes greater disruption. Neither arrangement guarantees stability.";
                a.Add(new AtlasSection{Title="Leadership and continuity",Text=successionText,Facts=new[]{F("Recorded officeholders",N(officeholders.Length)),F("Completed tenures",N(completedTenures.Length)),F("Mean completed tenure",completedTenures.Length==0?"Not yet observed":completedTenures.Average(l=>l.EndedYear-l.StartedYear).ToString("0.0",CultureInfo.InvariantCulture)+" years"),F("Recorded disputed successions",N(h.Archive.Count(e=>(e.Kind=="succession-crisis"||e.Kind=="realm-succession-crisis")&&(e.PolityId==p.Id||e.OtherPolityId==p.Id))))}});
                a.Add(new AtlasSection{Title="Campaigns",Timeline=h.Dynamics.Campaigns.Where(w=>w.A==p.Id||w.B==p.Id).OrderByDescending(w=>w.StartedYear).ThenByDescending(w=>w.Id).Take(25).Select(w=>F("Year "+Y(s,w.StartedYear),w.Name+": "+w.Objective+". "+(string.IsNullOrEmpty(w.Context)?"":w.Context+" ")+w.Outcome+"; "+N(w.Conquests)+" captures; "+N(w.CasualtiesA+w.CasualtiesB)+" recorded casualties.")).ToArray()});
            }
            a.Add(new AtlasSection{Title="Institutions and pressures",Facts=metrics.ToArray()});
            if(h.Administration!=null&&p.EndedYear<0)
            {
                if(h.Aggregates!=null){var aggregate=h.Aggregates.Polities[p.Id];a.Add(new AtlasSection{Title="Population and provisioning accounts",Text="Cities track urban residents, suburban households, and their rural supply catchments separately. Remote residents use a regional aggregate rather than a repeated census of map cells. Transport, public works, conquest, and unrest change the practical cost of government.",Facts=new[]{F("Urban residents",N(aggregate.Urban)),F("Suburban residents",N(aggregate.Suburban)),F("Catchment rural residents",N(aggregate.Rural)),F("Remote residents",N(aggregate.Remote)),F("Receipts per modeled year",N(aggregate.Receipts)),F("Service costs per modeled year",N(aggregate.ServiceCost))}});}
                var account=h.Administration.Accounts.FirstOrDefault(x=>x.PolityId==p.Id);var projects=h.Administration.Projects.Where(x=>x.PolityId==p.Id&&x.EndedYear<0).ToArray();var offices=h.Administration.Offices.Where(x=>x.PolityId==p.Id&&x.EndedYear<0).ToArray();
                var facts=new List<AtlasFact>{F("Funded incorporation projects",N(projects.Length)),F("Staffed regional offices",N(offices.Length))};
                if(account!=null)facts.AddRange(new[]{F("Rural receipts in last annual account",N(account.RuralReceipts)+" account units"),F("District and office upkeep",N(account.ServiceCost)+" account units"),F("Residents outside supported reach",N(account.DisconnectedResidents))});
                if((h.Version=="history-11"||(h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||(h.Version=="history-16"||h.Aggregates!=null)))))))facts.Add(F("Offices retaining earlier administrations",N(offices.Count(o=>o.PredecessorPolityId>=0))));
                if((h.Version=="history-11"||(h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||(h.Version=="history-16"||h.Aggregates!=null))))))&&account!=null)facts.AddRange(new[]{F("Supported urban receipts",N(account.UrbanReceipts)+" account units"),F("Connected urban residents",N(account.TaxableUrbanResidents)),F("Urban residents outside supported reach",N(account.DisconnectedUrbanResidents))});
                a.Add(new AtlasSection{Title="Administration and expansion",Text="Control must remain connected to the capital through usable land, river or shipping routes. Regional offices require accounts, sustained local communities and paid officials; towns do not grant free new spheres of control. Incorporation needs setup funds, continuing expenditure and years of local negotiation. Trade and tributary relationships are distinct from direct administration.",Facts=facts.ToArray(),Timeline=projects.Select(x=>F("Expected year "+Y(s,x.DueYear),"District establishment begun in year "+Y(s,x.StartedYear)+(x.Cells!=null?" across "+N(x.Cells.Length)+" map cells":"")+"; ongoing annual commitment "+N(x.AnnualCost)+" account units. This is planned work, not guaranteed annexation.")).ToArray()});
                if(offices.Length>0)a.Add(Links("Regional administration",offices.OrderBy(o=>o.FoundedYear).ThenBy(o=>o.Id).Select(o=>{var link=TownLink(s,s.Society.Settlements[o.TownId]);link.Detail="Office established in year "+Y(s,o.FoundedYear)+"; "+N(o.AnnualCost)+" account units of annual upkeep"+(o.PredecessorPolityId>=0?". Retains an earlier administration from "+h.Polities[o.PredecessorPolityId].Name:".");return link;}),"Select an administrative center to read its local history. Offices organize nearby districts; friendly transit can maintain communication, but hostile interruptions and unpaid upkeep can sever that connection."));
            }
            else if(h.Administration!=null)
            {
                var offices=h.Administration.Offices.Where(o=>o.PolityId==p.Id).OrderBy(o=>o.FoundedYear).ThenBy(o=>o.Id).ToArray();
                a.Add(Links("Recorded administration",offices.Select(o=>{var link=TownLink(s,s.Society.Settlements[o.TownId]);link.Detail="Office established in year "+Y(s,o.FoundedYear)+(o.EndedYear>=0?"; ended in year "+Y(s,o.EndedYear):"; no closure date recorded")+". The site's present population and controller are separate from this former office.";return link;}),"This state no longer administers territory. Its last annual budget is not preserved; zero-valued current accounts are not evidence of its former receipts or population. These links identify recorded offices, not present possessions."));
            }
            a.Add(Events(s,e=>e.PolityId==p.Id||e.OtherPolityId==p.Id));
        }
        static void City(PrehistoryState s,Settlement t,AtlasDocument d,List<AtlasSection> a)
        {
            var h=s.History;var c=s.Society.Cultures[t.CultureId];bool ruled=t.PolityId>=0&&t.PolityId<h.Polities.Length;var p=ruled?h.Polities[t.PolityId]:null;
            var portLedger=h.Aggregates?.Empires?.Connections?.ProvisionedPorts;int portIndex=portLedger?.ByTown[t.Id]??-1;
            var port=portLedger!=null&&portIndex>=0?portLedger.Ports[portIndex]:null;
            d.Title=t.Name;d.Cell=t.Cell;d.Subtitle=t.Population>0?"Inhabited settlement":"Uninhabited recorded site";
            d.Introduction=t.Name+" was founded in year "+Y(s,t.FoundedYear)+" by the "+c.Name+" people. "+(t.Population>0?"Today it has "+N(t.Population)+" residents. ":"No residents are currently recorded at this site. ")+(p==null?"It is outside centralized state administration.":"It is "+(p.CapitalId==t.Id?"the capital of ":"administered by ")+p.Name+".")+" Its recorded local government is "+t.Government+".";
            d.Facts=new[]{F("Founded","Year "+Y(s,t.FoundedYear)),F("Residents",N(t.Population)),F(port==null?"Food-support capacity":"Installed harbor capacity",N(port==null?t.FoodCapacity:port.MaximumResidents)+" people"),F("Food reserves",N(t.FoodReserve)+" person-years"),F("Health index",N(t.Health)+" / 1,000"),F("Stability index",N(t.Stability)+" / 1,000")};
            if(port!=null)
            {
                if(t.Population>0)d.Subtitle="Provisioned harbor";
                string delivery=port.LastSupplyElapsedYears>0?N(port.LastReceivedFood)+" person-years over a "+N(port.LastSupplyElapsedYears)+"-year review interval":"No supply review yet";
                a.Add(new AtlasSection{Title="Harbor and supply",Text=t.Name+" was established as a supplied harbor, not a new farming hinterland. Its named supplier is "+s.Society.Settlements[port.SupplierTownId].Name+". Cargo uses real warehouse stocks and paid shipping; losing the route or its support can exhaust reserves and cause abandonment. Installed handling capacity is not a guarantee of available food.",Facts=new[]{F("Last successful delivery",port.LastDeliveryYear<0?"None recorded":"Year "+Y(s,port.LastDeliveryYear)),F("Last supply review",port.LastSupplyElapsedYears==0?"Not yet reviewed":"Year "+Y(s,port.LastSupplyYear)),F("Last received cargo",delivery),F("Total cargo received",N(port.ReceivedFood)+" person-years"),F("Total cargo lost",N(port.LostFood)+" person-years"),F("Freight paid",N(port.ShippingPaid)+" account units"),F("Harbor expansion paid",N(port.ExpansionPaid)+" account units"),F("Later settler arrivals",N(port.AdditionalMigrants)+" people")},Links=new[]{TownLink(s,s.Society.Settlements[port.SupplierTownId])}});
                if(port.SupplierHistory!=null)
                {
                    var network=port.SupplierHistory;
                    var sources=network.Recent.AsEnumerable().Reverse().Take(4).Select(record=>
                    {
                        var link=TownLink(s,s.Society.Settlements[record.SupplierTownId]);
                        link.Context="Year "+Y(s,record.SinceYear)+(record.UntilYear<0?" onward":"–"+Y(s,record.UntilYear));
                        link.Detail=N(record.ReceivedFood)+" person-years delivered; "+N(record.LostFood)+" lost; "+N(record.ShippingPaid)+" freight paid";
                        return link;
                    }).ToArray();
                    a.Add(new AtlasSection{Title="Supply network",Text="A port can arrange a replacement supplier only through a working, affordable route to a farming community under its current government. These are the latest recorded suppliers, not a claim that today's supplier provided all earlier cargo. Older changes remain in the historical chronicle; older cargo accounts are retained as combined totals.",Facts=new[]{F("Original supplier",s.Society.Settlements[network.OriginalSupplierTownId].Name),F("Recorded supplier changes",N(network.EarlierSegments+network.Recent.Length-1)),F("Earlier combined deliveries",N(network.EarlierReceivedFood)+" person-years"),F("Supply arrangements paid",N(network.EarlierSetupPaid+network.Recent.Sum(x=>x.SetupPaid))+" account units")},Links=sources});
                }
            }
            var coastalFarms=h.Aggregates?.Empires?.Connections?.CoastalFarming;int farmParent=coastalFarms?.Families[t.Id]??-1;
            if(farmParent>=0)
                a.Add(new AtlasSection{Title="Coastal settlement lineage",Text="This community descends from a paid local settlement venture. Its founders came from an actual nearby community and established a finite farming catchment with suitable carried crops. Its household account cannot recruit people or assume food imports from a distant national population merely because it shares a government. It must sustain itself through local production, actual deliveries and its own people.",Links=new[]{TownLink(s,s.Society.Settlements[farmParent])}});
            if(h.Aggregates?.Living!=null)
            {
                var life=h.Aggregates.Living.Cities[t.Id];var catchment=h.Aggregates.Cities[t.Id];
                if(life.AbandonedYear>=0){d.Subtitle="Abandoned settlement and historical site";d.Introduction+=" The community ceased to occupy this site in year "+Y(s,life.AbandonedYear)+"; its former inhabitants and later communities are not treated as an immortal city.";}
                a.Add(new AtlasSection{Title="Settlement history and hinterland",Facts=new[]{F("Peak recorded residents",N(life.PeakPopulation)),F("Peak year","Year "+Y(s,life.PeakYear)),F("Suburban residents",N(catchment.Suburban)),F("Surrounding rural residents",N(catchment.Rural)),F(port==null?"Supply area":"Reserved harbor footprint",N(catchment.AreaKm2)+" km²"),F("Status",life.AbandonedYear>=0?"Abandoned in year "+Y(s,life.AbandonedYear):"Inhabited")}});
            }
            var ties=new List<AtlasLink>{CultureLink(c)};if(p!=null)ties.Add(StateLink(s,p));int faith=HistoricalCells.Religion(s,t.Cell);if(faith>=0)ties.Add(FaithLink(h.Religions[faith]));a.Add(Links("Community and connections",ties));
            var localWorship=ReligiousLocalSection(s,t.Cell);if(localWorship!=null)a.Add(localWorship);
            var localSpeech=SpokenLocalSection(s,t.Cell);if(localSpeech!=null)a.Add(localSpeech);
            AddIdentityLocalSections(s,t.Cell,a);
            var localClaimant=LocalClaimantSection(s,t.Id);if(localClaimant!=null)a.Add(localClaimant);
            a.Add(Text("Daily life",c.Subsistence+". Recorded local resources include "+c.Resources+". The model tracks cultivation, food reserves, health, and a craft share; it does not yet record individual occupations, buildings, or named residents beyond rulers."));
            if(h.Modern?.CivicRivalries!=null)
            {
                var memories=h.Modern.CivicRivalries.Where(r=>r.TownA==t.Id||r.TownB==t.Id).OrderByDescending(r=>HistoricalCivicMemoryEffects.Intensity(r,s.Years)).ThenBy(r=>r.Id).ToArray();
                if(memories.Length>0)a.Add(Links("Civic rivalries and memory",memories.Select(r=>
                {
                    var other=s.Society.Settlements[r.TownA==t.Id?r.TownB:r.TownA];var link=TownLink(s,other);int intensity=HistoricalCivicMemoryEffects.Intensity(r,s.Years);
                    if(other.Name==t.Name)link.Name+=" (another settlement)";
                    link.Detail=(intensity>=500?"A strong civic rivalry":intensity>=180?"An enduring civic rivalry":"A fading historical rivalry")+", first recorded in year "+Y(s,r.FirstYear)+" during "+h.Dynamics.Campaigns[r.FirstCampaignId].Name+"; last renewed in year "+Y(s,r.LastConflictYear)+"."+(t.PolityId>=0&&t.PolityId==other.PolityId?" Both cities now answer to the same state; annexation did not erase their separate civic history.":"");return link;
                }),"These ties arise from actual nearby city conflicts, not from different cultures alone. Their political influence fades over time and is moderated by common institutions. Deep grievances can obstruct cooperation; severe common pressures can still bring former rivals together. This does not mean that rival cities are perpetually at war."));
            }
            if(t.Population>0)a.Add(new AtlasSection{Title="Food and everyday security",Text=t.Name+" holds roughly "+((double)t.FoodReserve/t.Population).ToString("0.#",CultureInfo.InvariantCulture)+" years of food at its current population, before future harvests or incoming supplies. "+(port!=null?"Its residents depend on delivered provisions rather than a recorded local farming catchment.":t.FoodCapacity<t.Population?"Its population exceeds its recorded food-support capacity; outside provisioning and reserves matter.":"Its recorded food-support capacity, including modeled incoming supplies, is at least as large as its present population.")+" This is a modeled food balance, not a guarantee against the next shortage.",Bars=true,Unit="people",Links=new[]{new AtlasLink{Name="Residents",Value=t.Population,Detail=N(t.Population)+" people"},new AtlasLink{Name=port==null?"Recorded food-support capacity":"Installed harbor capacity",Value=t.FoodCapacity,Detail=N(t.FoodCapacity)+" people"}}});
            a.Add(new AtlasSection{Title="Local economy",Facts=new[]{F("Cultivation intensity",N(t.CultivatedPermille)+" / 1,000"),F("Craft-share index",N(t.CraftSharePermille)+" / 1,000")}});
            if(t.Id<h.Towns.Length)
            {
                var ht=h.Towns[t.Id];var facts=new List<AtlasFact>{F("Walls",N(ht.Walls))};
                if(ht.Cropping!=null)
                {
                    var survey=ht.Cropping;
                    a.Add(new AtlasSection{Title="Crops and cultivated food",Text=survey.CropIds.Length==0?"No suitable domesticated field crops were recorded in the last local survey. Fishing, gathering or imported food may still support residents.":"The latest local crop survey records the food plants cultivated around this settlement. Shares are modeled crop-mix allocations, not measured acreage; crop traditions elsewhere among the same people need not grow here.",Facts=new[]{F("Last survey","Year "+Y(s,survey.Year))}.Concat(survey.CropIds.Select((id,k)=>F(HistoricalCropCatalog.Crops[id].Name,(survey.SharesPermille[k]/10.0).ToString("0.#",CultureInfo.InvariantCulture)+"% of modeled mix"))).ToArray()});
                }
                if(ht.Demography!=null)
                {
                    var dem=ht.Demography;
                    long migration=dem.RuralInflow-dem.RuralOutflow;
                    string narrative=dem.NaturalChange<0?"The latest update records a negative growth-and-shortage balance before separate crowding and outbreak losses.":"The latest update records a positive or unchanged growth-and-shortage balance before separate crowding and outbreak losses.";
                    a.Add(new AtlasSection{Title="Population and movement",Text=narrative+" Local rural arrivals and departures are recorded separately. Ordinary background growth and food shortages enter the growth balance; chronic crowding, outbreaks, campaign casualties and other events can reduce the population independently. These are modeled update flows, not a census, a fabricated time series or an exact annual rate. Public works can buffer the crowding burden.",Facts=new[]{F("Last recorded account","Year "+Y(s,dem.Year)),F("Growth / shortage balance",(dem.NaturalChange>0?"+":"")+N(dem.NaturalChange)+" people"),F("Arrivals from nearby countryside",N(dem.RuralInflow)+" people"),F("Departures to countryside",N(dem.RuralOutflow)+" people"),F("Chronic crowding losses",N(dem.ChronicLoss)+" people"),F("Lifetime rural arrivals",N(dem.TotalRuralInflow)),F("Lifetime rural departures",N(dem.TotalRuralOutflow))}});
                }
                if((h.Version=="history-8"||(h.Version=="history-9"||(h.Version=="history-10"||(h.Version=="history-11"||(h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||(h.Version=="history-16"||h.Aggregates!=null)))))))))&&t.PolityId<0)a.Add(new AtlasSection{Title="State formation",Text=(h.Version=="history-9"||(h.Version=="history-10"||(h.Version=="history-11"||(h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||(h.Version=="history-16"||h.Aggregates!=null))))))))?"A prosperous town can remain decentralized. Supported local councils sometimes build sustained central administration, more readily around collectable harvests, shared infrastructure and nearby institutional examples. Poor support erodes that practice; neither age nor borrowed knowledge grants a state.":"Centralized rule is not granted just because this settlement is old or large. Local food production, reserves, stability, storage practices and organized institutions must support it over time. Poor support erodes preparation; the final transition remains uncertain.",Facts=new[]{F((h.Version=="history-9"||(h.Version=="history-10"||(h.Version=="history-11"||(h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||(h.Version=="history-16"||h.Aggregates!=null))))))))?"Accumulated administrative practice":"Accumulated supported preparation",N(ht.CivicPreparationYears)+" years"),F("Minimum practice for founding","150 years")}});
                if(h.Version=="history-6"||h.Version=="history-7"||h.Version=="history-8"||(h.Version=="history-9"||(h.Version=="history-10"||(h.Version=="history-11"||(h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||(h.Version=="history-16"||h.Aggregates!=null)))))))))facts.AddRange(new[]{F(h.Aggregates==null?"Lifetime food imports":"Latest recorded food imports",N(ht.ImportedFood)+" person-years"),F(h.Aggregates==null?"Lifetime food exports":"Latest recorded food exports",N(ht.ExportedFood)+" person-years"),F(h.Aggregates==null?"Lifetime outgoing transport losses":"Latest outgoing transport losses",N(ht.FoodTransportLoss)+" person-years"),F("Recorded shortage years",N(ht.ShortageYears)),F("Lifetime modeled disease deaths",N(ht.DiseaseDeaths))});
                if(h.Version=="history-7"||h.Version=="history-8"||(h.Version=="history-9"||(h.Version=="history-10"||(h.Version=="history-11"||(h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||(h.Version=="history-16"||h.Aggregates!=null)))))))))a.Add(new AtlasSection{Title="Local political life",Text=ht.AutonomyPressure>=300?"Organized local institutions are pressing for autonomy under the current court. This movement may subside or develop into a revolt; separation is not guaranteed.":"No sustained autonomy movement is currently recorded here. Different local and ruling cultures alone do not cause rebellion.",Facts=new[]{F("Autonomy pressure",N(ht.AutonomyPressure)+" / 1,000")}});
                if(ht.LastHarvestHazard!=HarvestHazard.None)facts.Add(F("Last recorded harvest shock",ht.LastHarvestHazard+", year "+Y(s,ht.LastHarvestHazardYear)));
                if((h.Version=="history-15"||(h.Version=="history-16"||h.Aggregates!=null)))a.Add(new AtlasSection{Title="Institutional connections",Text=ht.CivicTeacherPolityId<0?"No neighboring state is recorded as a source of this town's administrative experience. Independent centralization remains exceptional, even for a prosperous community.":"Sustained local exchange has brought experience of government from "+h.Polities[ht.CivicTeacherPolityId].Name+". This is institutional learning, not ancestry, conquest or a guarantee of statehood. Contact must persist to maintain strong exposure.",Facts=new[]{F("Current institutional exposure",N(ht.CivicExampleExposure)+" / 1,000")},Links=ht.CivicTeacherPolityId<0?Array.Empty<AtlasLink>():new[]{StateLink(s,h.Polities[ht.CivicTeacherPolityId])}});
                if(ht.DiseaseKind!=HistoricalDiseaseKind.None)facts.Add(F("Last recorded disease syndrome",HistoricalDiseaseEffects.Names[(int)ht.DiseaseKind]));
                a.Add(new AtlasSection{Title="Provisioning and resilience",Text="Person-years represent stored food sufficient to support that many people for one year. Latest recorded flows are not lifetime totals; cumulative disease and shortage records are labeled separately. Transport losses here belong to this town's outgoing cargo; an imported harbor's incoming route losses are recorded under Harbor and supply.",Facts=facts.ToArray()});
            }
            a.Add(Events(s,e=>e.SettlementId==t.Id||e.Cell==t.Cell));
        }
        static void People(PrehistoryState s,CultureIdentity c,AtlasDocument d,List<AtlasSection> a,World? world)
        {
            var h=s.History;var hc=h.Cultures[c.Id];d.Title=c.Name;d.Cell=c.OriginCell;d.Subtitle="People and cultural tradition";
            d.Introduction="The "+c.Name+" are a recorded cultural community dating to year "+Y(s,c.FoundedYear)+". Their modeled way of life centers on "+c.Subsistence.ToLowerInvariant()+". They number "+N(c.Population)+" people in the current checkpoint. Shared culture does not mean that all members answer to a single court.";
            var homeSpeech=SpokenLocalSection(s,c.OriginCell);if(homeSpeech!=null)a.Add(homeSpeech);
            var homeIdentity=EthnicLocalSection(s,c.OriginCell);if(homeIdentity!=null)a.Add(homeIdentity);
            d.Facts=new[]{F("Recorded origin","Year "+Y(s,c.FoundedYear)),F("People",N(c.Population)),F("Cultural footprint",N(c.AreaKm2)+" km²"),F("Organization index",N(c.Organization)+" / 1,000"),F("Settlement tendency",N(hc.Sedentism)+" / 1,000"),F("Surplus index",N(hc.Surplus)+" / 1,000"),F("Institutional index",N(hc.Institution)+" / 1,000")};
            var links=new List<AtlasLink>();if(hc.ParentCultureId>=0)links.Add(CultureLink(s.Society.Cultures[hc.ParentCultureId]));if(hc.LanguageId>=0)links.Add(CultureLink(s.Society.Cultures[hc.LanguageId]));if(hc.ReligionId>=0)links.Add(FaithLink(h.Religions[hc.ReligionId]));
            a.Add(Links("Origins, language, and belief",links,"The language-family label refers to the originating recorded culture. Ancestry and language do not determine a people's abilities."));
            if(h.RegionalTraditions!=null)
            {
                var traditions=h.RegionalTraditions;
                var proto=s.Society.Settlements.Where(t=>t.CultureId==c.Id&&t.Population>0&&HistoricalRegionalTraditions.Proto(s,t.Id)&&h.Aggregates.Resolve(t.PolityId,h)<0).ToArray();
                a.Add(new AtlasSection{Title="Regional tradition and local centers",Text="A shared regional way of life is not a single state. Ordinary villages remain villages; proto-urban institutions occupy individual supported centers, not this people's entire homeland.",Facts=new[]{F("Connected habitat regions",N(Math.Max(0,traditions.RegionalCounts[c.Id]))),F("Current proto-urban centers",N(proto.Length)),F("Regional continuity",traditions.Retired[c.Id]>=0?"Tradition dissolved in "+Y(s,traditions.Retired[c.Id])+"; descendant communities continued":"Living tradition")},Links=proto.Select(t=>TownLink(s,t)).ToArray()});
            }
            if(h.Languages!=null)
            {
                var language=h.Languages.Profiles[c.Id];bool writing=HistoricalTechnologies.Has(hc,HistoricalTechnology.Writing);
                a.Add(new AtlasSection{Title="Language and names",Text="Names share reusable roots and sound rules. Related languages inherit roots with regular sound changes; borrowed vocabulary follows recorded contact. Pronunciations are procedural IPA guides, not recorded speech. Existing place names survive later language changes.",
                    Facts=new[]{F("Language family",s.Society.Cultures[language.FamilyId].Name),F("Sounds",LanguageGenerator.SoundInventory(language)),F("Sentence order",language.WordOrder),F("Name pattern",language.ModifierFirst?"Descriptive root before place word":"Place word before descriptive root"),F("Writing",writing?language.Script+" profile; shown in transliteration":"Unwritten; shown in transliteration"),F("Recorded loan roots",N(language.Loans.Length))},
                    Timeline=new[]{"river","sea","settlement","land","people","stone"}.Select(concept=>{var word=LanguageGenerator.Word(language,concept);return F(concept,LanguageGenerator.Capitalize(word.Text)+" /"+word.Phonetics+"/");}).ToArray()});
                if(world?.Ecology!=null)
                {
                    var names=new List<AtlasFact>();
                    string LocalName(LanguageNameKind kind,int identity)=>LanguageGenerator.Capitalize(LanguageGenerator.Name(language,kind,identity).Text);
                    names.Add(F("Home region",LocalName(LanguageNameKind.Region,world.Ecology.Region[c.OriginCell])));
                    names.Add(F("Home landmass",LocalName(LanguageNameKind.Continent,world.Landmass[c.OriginCell])));
                    if(world.Hydrology.RiverOrder[c.OriginCell]>0)names.Add(F("Home river system",LocalName(LanguageNameKind.River,world.Hydrology.Watershed[c.OriginCell])));
                    a.Add(new AtlasSection{Title="Names for the homeland",Text="Names in this people's current language, not universal labels. Another language may name the same geographical feature differently. The home region uses the existing ecoregion identity; this is not yet a historical regional-boundary system.",Facts=names.ToArray()});
                }
            }
            var activities=new[]{("Cultivation",c.Farming),("Gathering",c.Gathering*(1000-c.Farming)/1000),("Hunting",c.Hunting*(1000-c.Farming)/1000),("Fishing",c.Fishing*(1000-c.Farming)/1000),("Herding",c.Herding*(1000-c.Farming)/1000)};
            a.Add(new AtlasSection{Title="Ways of making a living",Text="Relative modeled livelihood intensities, not a census of workers or exact calorie shares. Resources: "+c.Resources,Unit="/ 1,000",Bars=true,Links=activities.Select(x=>new AtlasLink{Name=x.Item1,Value=x.Item2,Detail=N(x.Item2)+" / 1,000"}).ToArray()});
            a.Add(Links("States associated with these people",h.Polities.Where(p=>p.CultureId==c.Id).OrderBy(p=>p.EndedYear>=0).ThenByDescending(p=>p.Population).ThenBy(p=>p.Id).Select(p=>StateLink(s,p,p.EndedYear<0?p.Government:"Historical state"))));
            a.Add(Links("Settlements",s.Society.Settlements.Where(t=>t.CultureId==c.Id&&t.Population>0).OrderByDescending(t=>t.Population).ThenBy(t=>t.Id).Select(t=>TownLink(s,t)),bars:true,unit:"residents"));
            a.Add(Links("Cultural contacts",c.Contacts.Where(i=>i>=0&&i<s.Society.Cultures.Length).Select(i=>CultureLink(s.Society.Cultures[i])),"Recorded contact does not imply conquest, migration, or friendship."));
            a.Add(new AtlasSection{Title="Practices and knowledge",Text="Dates are first recorded adoption by this culture, not claims of world-first invention.",Timeline=hc.Technology.Select((year,k)=>new{year,k}).Where(x=>x.year>0).OrderBy(x=>x.year).ThenBy(x=>x.k).Select(x=>F("Year "+Y(s,x.year-1),HistoricalTechnologies.Name(h.Version,x.k))).ToArray()});
            if(hc.Technical!=null)a.Add(new AtlasSection{Title="Research and practical technology",Text="A level records accumulated technical knowledge. Research shares sum to no more than 100%; adding projects does not create effort. Practical deployment is separate and depends on supporting skills, livelihoods and capital.",Technical=TechnicalResearchView.Read(s,c.Id)});
            if(h.Agriculture!=null&&c.Id<h.Agriculture.Length)
            {
                var crops=h.Agriculture[c.Id];
                if(crops.Animals!=null)a.Add(new AtlasSection{Title="Animals and husbandry",Text="Breeding traditions develop from accessible wild populations or actual cultural contacts. Food and draft benefits apply only in suitable local habitats. These are coarse management lineages, not invented wild abundance or a complete breed genealogy.",Facts=crops.Animals.Where(v=>v.AdoptedYear>0).Select(v=>F(v.Name,"Recorded since year "+Y(s,v.AdoptedYear-1)+(v.SourceCulture<0?"; developed from local wild populations":"; introduced through "+s.Society.Cultures[v.SourceCulture].Name))).ToArray()});
                if(crops.Cultivars!=null)a.Add(new AtlasSection{Title="Selected food plants",Text="These are inherited crop lineages, not a promise that every field in this people's territory can grow them. Successful cultivation slowly selects maturity, seasonal warmth, water requirements and day-length response within bounded limits. Local husbandry is experience earned in occupied fields; it is not inherited automatically with introduced seed. Stored root staples can improve through food processing. Local city surveys show which plants are actually suitable there.",Facts=crops.Cultivars.Select((v,k)=>new{v,k}).Where(x=>x.v!=null).Select(x=>F(HistoricalCropCatalog.Crops[x.v.CropId].Name,x.v.MaturityDays+"-day maturity; "+N(x.v.SelectedYears)+" effective years of selection; "+N(crops.HusbandryYears?[x.k]??0)+" local husbandry years; minimum warm-season proxy "+((x.v.MinimumWarmTenthsC==0?HistoricalCropCatalog.Crops[x.v.CropId].MinimumWarmC*10:x.v.MinimumWarmTenthsC)/10.0).ToString("0.#",CultureInfo.InvariantCulture)+" °C")).ToArray()});
                a.Add(new AtlasSection{Title="Crop traditions",Text=(h.Version=="history-9"||(h.Version=="history-10"||(h.Version=="history-11"||(h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||(h.Version=="history-16"||h.Aggregates!=null))))))))?"Food traditions can develop in occupied fields away from the founding campsite. Introduced crops may arrive through intermediary communities; learning them does not imply common ancestry, a change of language or political submission.":"",Timeline=crops.AdoptedYear.Select((year,k)=>new{year,k}).Where(x=>x.year>0).Select(x=>F("Year "+Y(s,x.year-1),HistoricalCrops.Names[x.k]+(crops.SourceCulture[x.k]<0?" — local domestication":" — introduced from a tradition recorded among "+s.Society.Cultures[crops.SourceCulture[x.k]].Name))).ToArray()});
                if((h.Version=="history-9"||(h.Version=="history-10"||(h.Version=="history-11"||(h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||(h.Version=="history-16"||h.Aggregates!=null)))))))))a.Add(Links("Introduced food traditions",crops.SourceCulture.Where(id=>id>=0).Distinct().OrderBy(id=>id).Select(id=>CultureLink(s.Society.Cultures[id])),"These links identify recorded knowledge sources, not necessarily direct contact with their original inventors."));
            }
            if(h.OceanSettlements!=null)
            {
                var voyages=h.OceanSettlements.Where(v=>v.SourceCulture==c.Id||v.DestinationCulture==c.Id).OrderByDescending(v=>v.Year).ThenByDescending(v=>v.Id).ToArray();
                if(voyages.Any(v=>v.SailingDistanceM>v.DistanceM))a.Add(new AtlasSection{Title="Navigated sea passages",Text="Some departures followed a longer all-water route around intervening land. Food provisions and vessel range use that sailing distance, rather than treating a peninsula as either impassable or free to sail through.",Timeline=voyages.Where(v=>v.SailingDistanceM>v.DistanceM).Select(v=>F("Year "+Y(s,v.Year),N(v.SailingDistanceM/1000)+" km sailing distance; approximately "+N(v.DistanceM/1000)+" km between the destination and departure coasts.")).ToArray()});
                if(voyages.Length>0)a.Add(new AtlasSection{Title="Overseas communities",Text="These records describe successful settlement opportunities, not every attempted voyage or a continuously usable trade route. Founders transfer existing people and suitable practices; related communities remain politically independent unless later institutions establish control.",Timeline=voyages.Select(v=>F("Year "+Y(s,v.Year),N(v.Migrants)+" founders crossed approximately "+N(v.DistanceM/1000)+" km between the "+s.Society.Cultures[v.SourceCulture].Name+" and "+s.Society.Cultures[v.DestinationCulture].Name+" communities."+(v.DepartureTownId>=0?" They departed from "+s.Society.Settlements[v.DepartureTownId].Name+", including "+N(v.UrbanMigrants)+" town residents; "+N(v.Provisions)+" person-years of food provisioned the passage." : "")+(v.ArrivalFood>0?" A further "+N(v.ArrivalFood)+" person-years of food were transferred into the landing settlement's reserve.":""))).ToArray()});
            }
            a.Add(Events(s,e=>e.CultureId==c.Id));
        }
        static void Religion(PrehistoryState s,HistoricalReligion r,AtlasDocument d,List<AtlasSection> a)
        {
            var h=s.History;d.Title=r.Name;d.Cell=r.HolyCell;d.Subtitle="Religious tradition";
            d.Introduction=r.Name+" first appears in the records in year "+Y(s,r.FoundedYear)+", among the "+s.Society.Cultures[r.CultureId].Name+" people. "+r.Tradition+". "+(r.ParentId>=0?"It branches from "+h.Religions[r.ParentId].Name+".":"No parent tradition is recorded.");
            d.Facts=new[]{F("Founded","Year "+Y(s,r.FoundedYear)),F("Dominant-local-tradition cells",N(Enumerable.Range(0,HistoricalCells.Count(s)).Count(i=>HistoricalCells.Religion(s,i)==r.Id)))};
            if(Prehistory.ReligiousDoctrineAt(s,r.Id)==null)a.Add(Text("Beliefs and observance","Ethos: "+r.Ethos+". Practices: "+r.Ritual+"."));
            a.Add(Text("Reading the religious map","A cell's color represents its dominant recorded tradition, not unanimous belief or a census of adherents. Political conquest does not by itself establish how many people hold a belief."));
            a.AddRange(ReligiousNetworkSections(s,r.Id));
            a.AddRange(ReligiousDoctrineSections(s,r.Id));
            a.Add(Links("Related traditions",(r.ParentId>=0?new[]{FaithLink(h.Religions[r.ParentId])}:Array.Empty<AtlasLink>()).Concat(h.Religions.Where(x=>x.ParentId==r.Id).Select(FaithLink))));
            a.Add(Links("Originating people",new[]{CultureLink(s.Society.Cultures[r.CultureId])}));
            a.Add(Links("Affiliated courts",h.Polities.Where(p=>p.EndedYear<0&&p.ReligionId==r.Id).OrderByDescending(p=>p.Population).ThenBy(p=>p.Id).Select(p=>StateLink(s,p,p.Government)),"Court affiliation is not the population's religious census."));
            a.Add(Events(s,e=>e.ReligionId==r.Id));
        }
        static void Ruler(PrehistoryState s,HistoricalLeader l,AtlasDocument d,List<AtlasSection> a)
        {
            var p=s.History.Polities[l.PolityId];d.Title=l.Name;d.Cell=s.Society.Settlements[p.CapitalId].Cell;d.Subtitle="Recorded ruler of "+p.Name;
            d.Introduction=l.Name+" took office in "+p.Name+" in year "+Y(s,l.StartedYear)+". "+(l.EndedYear<0?"This reign is still recorded as active.":"The reign ended in year "+Y(s,l.EndedYear)+".")+(!string.IsNullOrEmpty(l.Legacy)?" "+l.Legacy:"");
            d.Facts=new[]{F("Accession","Year "+Y(s,l.StartedYear)),F("Appointment",string.IsNullOrEmpty(l.Selection)?"Not recorded":l.Selection),F("Victories or captures",N(l.Victories)),F("Defeats",N(l.Defeats)),F("Public-work reforms",N(l.Reforms))};
            a.Add(Links("Court and succession",new[]{StateLink(s,p)}.Concat(l.PredecessorId>=0?new[]{LeaderLink(s,s.History.Dynamics.Leaders[l.PredecessorId])}:Array.Empty<AtlasLink>())));
            int person=Prehistory.RulerPersonId(s,l.Id);if(person>=0)a.Add(Links("Person and family",new[]{NotableLink(s,person)},"This reign is one office in a person's life. Follow the biography for lifespan, family and other work."));
            a.Add(new AtlasSection{Title="Leadership profile",Text="Modeled leadership traits, not objective personal merit or an ethnic ranking.",Bars=true,Unit="/ 1,000",Links=new[]{("Military",l.Military),("Administration",l.Administration),("Diplomacy",l.Diplomacy),("Ambition",l.Ambition),("Renown",l.Renown)}.Select(x=>new AtlasLink{Name=x.Item1,Value=x.Item2,Detail=N(x.Item2)+" / 1,000"}).ToArray()});
            a.Add(Events(s,e=>(e.PolityId==p.Id||e.OtherPolityId==p.Id)&&e.Year>=l.StartedYear&&(l.EndedYear<0||e.Year<=l.EndedYear)));
        }
    }
}
