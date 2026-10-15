using System;
using System.Linq;
using System.Collections.Generic;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static partial class HistoryAtlas
    {
        static AtlasLink ProvinceLink(PrehistoryState s,int identity)=>new AtlasLink{Kind="province",Id=identity,Cell=HistoricalProvinceIdentity.Center(s,identity),Name=HistoricalProvinceIdentity.Name(s,identity),Detail=N(HistoricalProvinceIdentity.Area(s,identity))+" km²",Value=HistoricalProvinceIdentity.Area(s,identity)};
        static AtlasLink[] ProvinceDirectory(PrehistoryState s)
        {
            var h=s.History;var a=h?.Aggregates;var t=a?.Empires?.Territories;if(t==null||a==null||h==null)return Array.Empty<AtlasLink>();
            return Enumerable.Range(0,t.DistrictAreas.Length).Where(id=>t.DistrictAreas[id]>0&&a.Resolve(s.Society.Settlements[id].PolityId,h)>=0).Select(id=>ProvinceLink(s,id))
                .Concat(t.Posts.Where(p=>p.AreaKm2>0&&p.ClosedYear<0&&a.Resolve(p.PolityId,h)>=0).Select(p=>ProvinceLink(s,HistoricalProvinceIdentity.PostIdentityOffset+p.Id))).OrderByDescending(p=>p.Value).ThenBy(p=>p.Id).ToArray();
        }
        static void Province(PrehistoryState s,int identity,AtlasDocument d,List<AtlasSection> sections)
        {
            var a=s.History.Aggregates;var t=a?.Empires?.Territories;if(t==null||a==null){d.Introduction="No provincial service records are stored in this older history.";return;}
            bool post=identity>=HistoricalProvinceIdentity.PostIdentityOffset;int index=post?identity-HistoricalProvinceIdentity.PostIdentityOffset:identity;
            if(index<0||index>=(post?t.Posts.Length:a.Cities.Length)){d.Introduction="This provincial record is unavailable.";return;}
            var office=post?t.Posts[index]:null;int cell=HistoricalProvinceIdentity.Center(s,identity),local=HistoricalCells.Polity(s,cell),sovereign=local<0?-1:HistoricalImperialHierarchy.Sovereign(s,local);
            d.Title=HistoricalProvinceIdentity.Name(s,identity);d.Cell=cell;d.Subtitle=post?"Delegated provincial administration":"District of an inhabited administrative center";
            d.Introduction=d.Title+(local>=0?" belongs to "+s.History.Polities[local].Name:" has no surviving central government")+(sovereign>=0&&sovereign!=local?", a constituent of "+s.History.Polities[sovereign].Name:"")+". Its recorded service district covers "+N(HistoricalProvinceIdentity.Area(s,identity))+" km². These are administrative connections, not an assertion that the countryside shares one culture.";
            if(post&&office!=null)d.Introduction+=" The office was established in year "+Y(s,office.FoundedYear)+". "+(office.ClosedYear>=0?"Its funding ended in year "+Y(s,office.ClosedYear)+"; local people and the historical district remain.":"It extends government through a paid local official rather than a newly created city.");
            var facts=new List<AtlasFact>{F("Recorded district area",N(HistoricalProvinceIdentity.Area(s,identity))+" km²")};
            if(post&&office!=null){facts.Add(F("Annual upkeep",N(office.AnnualCost)+" account units"));facts.Add(F("Delegated autonomy",(office.AutonomyMilli/10.0).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" / 100"));facts.Add(F("Network depth",N(office.Depth)));facts.Add(F("Communication effort",office.MessageEffortKm.ToString("N0",System.Globalization.CultureInfo.InvariantCulture)+" transport-effort km"));}
            else facts.Add(F("Center population",N(s.Society.Settlements[index].Population)));
            d.Facts=facts.ToArray();var links=new List<AtlasLink>();if(!post)links.Add(TownLink(s,s.Society.Settlements[index]));if(local>=0)links.Add(StateLink(s,s.History.Polities[local],"Local government"));if(sovereign>=0&&sovereign!=local)links.Add(StateLink(s,s.History.Polities[sovereign],"Overarching imperial government"));
            sections.Add(Links("Institutions and allegiance",links,"Direct administration, constituent allegiance and local culture are distinct."));
            sections.Add(Events(s,e=>e.Cell==cell));
        }
    }
}
