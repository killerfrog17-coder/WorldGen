using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static partial class HistoryAtlas
    {
        static AtlasLink ClaimLink(PrehistoryState s,int id)
        {
            var c=s.History.Aggregates.Empires.Connections;var t=c.Titles[id];int holder=s.History.Aggregates.Resolve(t.Holder,s.History);
            var post=s.History.Aggregates.Empires.Territories.Posts.FirstOrDefault(p=>c.CellRegions[p.Cell]==t.Region);
            int cell=post?.Cell??Enumerable.Range(0,c.CellRegions.Length).First(i=>c.CellRegions[i]==t.Region);
            return new AtlasLink{Kind="claim",Id=id,Cell=cell,Name=(post==null?"Regional territorial title":post.Name+" territory"),Context=holder<0?"Title no longer has a surviving government":s.History.Polities[holder].Name,Detail="Recognized sovereignty; not uniform direct control",Value=t.AreaKm2};
        }
        static string ClaimSummary(PrehistoryState s,int id)
        {var t=s.History.Aggregates.Empires.Connections.Titles[id];return ClaimLink(s,id).Name+" covers a recognized claim of "+N(t.AreaKm2)+" km², agreed in year "+Y(s,t.EstablishedYear)+". This is not a census of settlers or evidence of control over every local community. "+t.Basis+".";}
        static void Claim(PrehistoryState s,int id,AtlasDocument d,List<AtlasSection> sections)
        {
            var c=s.History.Aggregates?.Empires?.Connections;if(c==null||id<0||id>=c.Titles.Length){d.Title="Title unavailable";return;}
            var t=c.Titles[id];var link=ClaimLink(s,id);d.Title=link.Name;d.Cell=link.Cell;d.Subtitle=link.Context;d.Introduction=ClaimSummary(s,id);
            int owner=s.History!.Aggregates!.Resolve(t.Holder,s.History);var links=new List<AtlasLink>();if(owner>=0)links.Add(StateLink(s,s.History.Polities[owner],"Current title holder"));links.Add(StateLink(s,s.History.Polities[t.Recognizer],"Government recording recognition"));
            if(t.PreviousHolder>=0)links.Add(StateLink(s,s.History.Polities[t.PreviousHolder],"Previous title holder"));
            sections.Add(Links("Parties to the title",links,"A transfer changes legal title and exchanges treasury funds. It does not replace inhabitants or erase their culture."));
            sections.Add(new AtlasSection{Title="Title record",Facts=new[]{F("Recognized extent",N(t.AreaKm2)+" km²"),F("Established",Y(s,t.EstablishedYear)),F("Latest transfer",Y(s,t.LastTransferYear)),F("Latest consideration",N(t.LastPrice)+" account units")}});
        }
        static void ColonialSections(PrehistoryState s,string kind,int id,List<AtlasSection> sections)
        {
            var c=s.History.Aggregates?.Empires?.Connections;if(c==null||id<0)return;
            if(kind=="state"&&id<c.Governments.Length)
            {var titles=c.Titles.Where(t=>s.History!.Aggregates!.Resolve(t.Holder,s.History)==id).Select(t=>ClaimLink(s,t.Id));if(titles.Any())sections.Add(Links("Recognized territorial titles",titles,"These agreements are separate from administered territory and resident population."));}
            if(kind=="state"&&c.Ambitions!=null&&id<c.Ambitions.Governments.Length)
            {
                var aim=c.Ambitions.Governments[id];
                sections.Add(new AtlasSection{Title="Territorial ambitions",Text=aim.Description+". Desired territory is not a recognized claim or actual control. Expansion still requires forces, funds, administration and tolerable war exhaustion.",Facts=new[]{F("Goal adopted",Y(s,aim.SinceYear)),F("Geographic scope",aim.Scope==HistoricalAmbitionScope.Subcontinental?"Surrounding landmass within approximately 1,800 km of the homeland":aim.Scope.ToString())}});
            }
            if(kind=="state"&&c.Geography!=null&&id<c.Geography.Governments.Length)
            {
                var map=c.Geography.Governments[id];var places=Enumerable.Range(0,map.Places.Count).Select(k=>map.Places.At(k)).ToArray();
                sections.Add(new AtlasSection{Title="Known world and voyages",Text="This state's dated reports are not an omniscient view. Rumors can encourage exploration; observed shores permit planning; a completed returning survey verifies navigation. Claims, actual control and populated settlements remain separate.",
                    Facts=new[]{F("Known coastal regions",N(map.Places.Count)),F("Hearsay only",N(places.Count(r=>map.Grade[r]==1))),F("Observed or exchanged directions",N(places.Count(r=>map.Grade[r]==2))),F("Verified navigation",N(places.Count(r=>map.Grade[r]==3))),F("Completed surveys",N(map.Surveys)),F("Successful new footholds",N(map.Landfalls))},
                    Timeline=map.Missions.Select(m=>F("Expected year "+Y(s,(int)(m.Due/SimulationTime.Year)),m.Kind==HistoricalSeaMission.Report?"A trading partner's geographical report is in transit.":m.Kind==HistoricalSeaMission.Survey?"A funded coastal survey is awaiting its return report.":"A supplied colonial venture is en route; local access and settlement are not guaranteed.")).ToArray()});
            }
            if(kind!="people"||id>=c.Cultures.Length)return;var record=c.Cultures[id];
            var facts=new List<AtlasFact>{F("Regional ancestry","Ecoregional community "+record.PrecursorRegion)};
            if(record.DormantSince>=0)facts.Add(F("No surviving recorded communities",Y(s,record.DormantSince)));
            var links=new List<AtlasLink>();if(record.ColonialSponsor>=0)links.Add(StateLink(s,s.History.Polities[record.ColonialSponsor],"Founding overseas sponsor"));if(record.CivilizationRoot>=0&&record.CivilizationRoot!=id)links.Add(CultureLink(s.Society.Cultures[record.CivilizationRoot]));
            if(record.InstitutionalOriginCulture>=0&&record.InstitutionalOriginCulture!=id){var origin=CultureLink(s.Society.Cultures[record.InstitutionalOriginCulture]);origin.Detail="Recorded source of institutional traditions, not a claim of identical culture";links.Add(origin);}
            sections.Add(new AtlasSection{Title="Continuity and changing traditions",Text="Named traditions can change with livelihoods and institutions without their communities appearing independently or their ancestry disappearing.",Facts=facts.ToArray(),Links=links.ToArray(),Timeline=record.Episodes.Select(e=>F(Y(s,e.Since)+"–"+Y(s,e.Until),e.Name+": "+e.Reason)).ToArray()});
            if(record.Charts.Length>0)sections.Add(new AtlasSection{Title="Exchanged sailing knowledge",Text="Only routes actually charted or learned through contact are recorded here.",Facts=record.Charts.Select(x=>F("Coast chart, year "+Y(s,x.Year),"Region "+x.Region+"; source: "+s.Society.Cultures[x.SourceCulture].Name)).ToArray()});
        }
    }
}
