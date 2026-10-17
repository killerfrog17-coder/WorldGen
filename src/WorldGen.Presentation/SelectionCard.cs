using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    [Serializable] public sealed class SelectionCard
    {
        public string Title="",Subtitle="",Note="",IllustrationKey="";
        public AtlasFact[] Facts=Array.Empty<AtlasFact>();
        public AtlasLink[] Bars=Array.Empty<AtlasLink>();
        public TechnicalProgress[] Projects=Array.Empty<TechnicalProgress>();
    }
    public static partial class HistoryAtlas
    {
        static AtlasLink TechnologyLink(PrehistoryState s,int culture,int cell)=>new AtlasLink{Kind="technology",Id=culture,Cell=cell,Name=s.Society.Cultures[culture].Name+" technology"};
        public static SelectionCard Card(PrehistoryState? s,int cell,HistoryOverlay overlay,Prehistory? engine=null)
        {
            var link=Selection(s,cell,overlay);var card=new SelectionCard{Title=link?.Name??"No recorded "+overlay.ToString().ToLowerInvariant(),Subtitle=overlay.ToString()};
            if(cell<0){card.Title="Select a location";return card;}
            if(link==null||s?.History==null)
            {
                if(engine!=null&&s!=null&&cell>=0&&cell<engine.CellPatch.Length&&(overlay==HistoryOverlay.States||overlay==HistoryOverlay.Civilizations||overlay==HistoryOverlay.Cultures||overlay==HistoryOverlay.Territories))
                {int patch=engine.CellPatch[cell];if(patch>=0){card.Title=engine.PatchName(patch);card.Subtitle=PrehistoryView.Lifestyle(engine.Patches[patch].Lifestyle);card.Facts=new[]{F("Regional population",N(s.Population[patch]))};}}
                return card;
            }
            var h=s.History;var facts=new List<AtlasFact>();
            switch(link.Kind)
            {
                case "language":
                    var speech=Prehistory.SpokenCommunityAt(s,cell);card.Subtitle="Spoken language";
                    if(speech!=null){facts.Add(F("Home language",SpeechName(s,speech.Primary)));if(speech.Minority>=0&&speech.MinorityPermille>0)facts.Add(F("Resident minority",SpeechName(s,speech.Minority)+" · "+(speech.MinorityPermille/10.0).ToString("0.#")+"% estimated"));if(speech.Second>=0&&speech.Second!=speech.Primary)facts.Add(F("Second language",SpeechName(s,speech.Second)+" · "+speech.BilingualPermille+" / 1,000"));if(speech.Administrative>=0)facts.Add(F("Administration",SpeechName(s,speech.Administrative)));card.Note="Home speech, bilingual ability and state language are separate. Open the atlas for sounds, ancestry and local communities.";}break;
                case "technology":
                    var hc=h.Cultures[link.Id];card.Title=WorldEraView.Name(WorldEraView.KnowledgeEra(hc));card.Subtitle=s.Society.Cultures[link.Id].Name+" · technology";
                    var projects=TechnicalResearchView.Read(s,link.Id);
                    facts.Add(F("Known practices",hc.Technology.Count(y=>y>0)+" / "+hc.Technology.Length));facts.Add(F("In practical use",WorldEraView.Name(WorldEraView.CultureEra(hc))));
                    card.Projects=projects.Where(p=>p.AllocationBasisPoints>0).Take(3).ToArray();card.Note=card.Projects.Length>0?"Active projects · bars show next-level progress":"No active research project recorded.";break;
                case "state":
                    var p=h.Polities[link.Id];card.Subtitle=p.Government;
                    bool realm=overlay==HistoryOverlay.States&&h.Aggregates?.Empires!=null;
                    facts.Add(F(realm?"Realm population":"Direct population",N(realm?HistoricalImperialHierarchy.Population(s,p.Id):p.Population)));facts.Add(F(realm?"Realm area":"Direct area",N(realm?HistoricalImperialHierarchy.AreaKm2(s,p.Id):p.AreaKm2)+" km²"));facts.Add(F("Capital",s.Society.Settlements[p.CapitalId].Name));facts.Add(F("Ruler",p.Ruler));facts.Add(F("Founded",Y(s,p.FoundedYear)));
                    int patron=HistoricalImperialHierarchy.Patron(s,p.Id);if(patron>=0)facts.Add(F("Within",h.Polities[patron].Name));break;
                case "people":
                    var c=s.Society.Cultures[link.Id];card.Subtitle=SocietyView.LocalStage(s,cell,c.Stage)+" · "+c.Subsistence;
                    facts.Add(F("Population",N(c.Population)));facts.Add(F("Area",N(c.AreaKm2)+" km²"));
                    card.Bars=new[]{("Cultivation",c.Farming),("Gathering",c.Gathering*(1000-c.Farming)/1000),("Hunting",c.Hunting*(1000-c.Farming)/1000),("Fishing",c.Fishing*(1000-c.Farming)/1000),("Herding",c.Herding*(1000-c.Farming)/1000)}.Where(v=>v.Item2>0).Select(v=>new AtlasLink{Name=v.Item1,Value=v.Item2}).ToArray();card.Note="Livelihood intensity · not population shares";break;
                case "city":
                    var t=s.Society.Settlements[link.Id];card.Subtitle=t.PolityId>=0?h.Polities[t.PolityId].Name:"Independent settlement";
                    if(h.RegionalTraditions!=null&&h.Aggregates.Resolve(t.PolityId,h)<0)card.Subtitle=HistoricalRegionalTraditions.Proto(s,t.Id)?"Embryonic civilization · "+s.Society.Cultures[t.CultureId].Name:"Village · "+s.Society.Cultures[t.CultureId].Name;
                    facts.Add(F("Residents",N(t.Population)));facts.Add(F("Food supports",N(t.FoodCapacity)));facts.Add(F("Founded",Y(s,t.FoundedYear)));
                    card.Bars=new[]{new AtlasLink{Name="Health",Value=t.Health},new AtlasLink{Name="Stability",Value=t.Stability}};break;
                case "religion":
                    var r=h.Religions[link.Id];card.Subtitle=r.Tradition;
                    var doctrine=Prehistory.ReligiousDoctrineAt(s,link.Id);
                    if(doctrine!=null){card.Subtitle=ReligiousDoctrines.Name(doctrine.Cosmology)+" · "+ReligiousDoctrines.Name(doctrine.Branch);facts.Add(F("Religious family",h.Religions[doctrine.RootId].Name));facts.Add(F("Authority",ReligiousDoctrines.Name(doctrine.Revelation)));}
                    var worship=Prehistory.ReligiousCommunityAt(s,cell);
                    if(worship!=null)
                    {
                        if(worship.Household>=0&&worship.Household<h.Religions.Length)facts.Add(F("Household practice",h.Religions[worship.Household].Name));
                        var localLay=Prehistory.LayAssociationAt(s,cell);if(localLay!=null&&localLay.Active&&localLay.Faith!=worship.Dominant)facts.Add(F("Local religious association",h.Religions[localLay.Faith].Name));
                        facts.Add(F("Local institutions",worship.Institution+" / 1,000"));facts.Add(F("Court policy",worship.Policy));
                        int otherResident=Prehistory.ReligiousPresenceAt(s,cell).Count(x=>x.ResidentEvidence&&x.Faith!=worship.Dominant);if(otherResident>0)facts.Add(F("Other recorded resident traditions",otherResident.ToString()));
                        card.Note="Map color shows public tradition, not uniform belief. Household and denominational communities can coexist; visitors alone are not resident converts.";
                    }
                    else{facts.Add(F("Origin",s.Society.Cultures[r.CultureId].Name));facts.Add(F("Founded",Y(s,r.FoundedYear)));}
                    facts.Add(F("Ethos",doctrine!=null?"Mutual aid "+doctrine.Charity+" · ritual discipline "+doctrine.Asceticism:r.Ethos));break;
                case "religious-body":
                    var body=Prehistory.ReligiousInstitutionAt(s,s.Society.Settlements[link.Id/2].Cell,link.Id%2);
                    if(body!=null){card.Subtitle=ReligiousDoctrines.Name(body.Form);facts.Add(F("Tradition",h.Religions[body.Faith].Name));facts.Add(F("Institutional support",body.Support+" / 1,000"));facts.Add(F("Local autonomy",body.Independence+" / 1,000"));card.Note="Institutional jurisdiction, not a state border or an adherent census.";}break;
                case "province":facts.Add(F("District area",N(HistoricalProvinceIdentity.Area(s,link.Id))+" km²"));card.Note="Administrative service district · not a culture boundary";break;
                default:card.Subtitle=link.Detail;break;
            }
            if((overlay==HistoryOverlay.States||overlay==HistoryOverlay.Territories)&&HistoricalRealmClaimantHistory.Enabled(s))
            {
                int city=h.Aggregates!.TownAt[cell];var claim=HistoricalRealmClaimantHistory.At(s,city);
                if(HistoricalRealmClaimantHistory.Active(s,claim)){facts.Add(F("Local claimant",claim.Name));card.Note="This city retains a local political challenge; a country, court and present sovereign are not the same historical identity.";}
            }
            if((overlay==HistoryOverlay.States||overlay==HistoryOverlay.Civilizations)&&SocietyView.ClaimOnly(s,cell))
                card.Note=(card.Note.Length>0?card.Note+" ":"")+SocietyView.PoliticalClaimNote;
            card.Facts=facts.ToArray();return card;
        }
        static void Technology(PrehistoryState s,int id,AtlasDocument d,List<AtlasSection> sections)
        {
            var c=s.Society.Cultures[id];var hc=s.History.Cultures[id];d.Title=c.Name+" technology";d.Cell=c.OriginCell;d.Subtitle=WorldEraView.Name(WorldEraView.KnowledgeEra(hc))+" knowledge · "+WorldEraView.Name(WorldEraView.CultureEra(hc))+" practical package";
            d.Introduction="What these people know, what they are researching, and what they can actually put to use. Knowledge can travel ahead of the skills, resources and infrastructure needed to deploy it.";
            var projects=TechnicalResearchView.Read(s,id);d.Facts=new[]{F("Known practices",hc.Technology.Count(y=>y>0)+" / "+hc.Technology.Length),F("Active projects",projects.Count(p=>p.AllocationBasisPoints>0).ToString()),F("Practical package",WorldEraView.Name(WorldEraView.CultureEra(hc)))};
            sections.Add(new AtlasSection{Title="Research desk",Text="Research effort is shared across active projects. Bars distinguish progress toward another level from practical deployment.",Technical=projects.Where(p=>p.AllocationBasisPoints>0).ToArray()});
            sections.Add(new AtlasSection{Title="Knowledge and deployment",Technical=projects});
            sections.Add(Links("People and institutions",new[]{CultureLink(c)}));
            sections.Add(new AtlasSection{Title="Adoption timeline",Text="Local adoption dates, not world-first inventions.",Timeline=hc.Technology.Select((y,k)=>new{y,k}).Where(x=>x.y>0).OrderBy(x=>x.y).ThenBy(x=>x.k).Select(x=>F(Y(s,x.y-1),HistoricalTechnologies.Name(s.History.Version,x.k))).ToArray()});
        }
        public static AtlasSection? OverviewVisual(AtlasDocument document)
        {
            var source=document.Sections.FirstOrDefault(s=>s.Diagram&&s.Links.Length>0)??document.Sections.FirstOrDefault(s=>s.Bars&&s.Links.Length>0);
            if(source!=null)return new AtlasSection{Title=source.Title,Diagram=source.Diagram,Bars=source.Bars,Unit=source.Unit,Links=source.Links.Take(6).ToArray()};
            var research=document.Sections.FirstOrDefault(s=>s.Technical.Any(p=>p.AllocationBasisPoints>0));
            return research==null?null:new AtlasSection{Title="Active research",Technical=research.Technical.Where(p=>p.AllocationBasisPoints>0).Take(3).ToArray()};
        }
    }
}
