using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    [Serializable] public sealed class AtlasTimeline
    {
        public int Offset,Total,PageSize=40; public bool IncludeRoutine;
        public AtlasFact[] Records=Array.Empty<AtlasFact>();
    }
    public static partial class HistoryAtlas
    {
        static readonly HashSet<string> RoutineKinds=new HashSet<string>(new[]{"succession","battle","trade","contact","religious-community","harvest-shock","incorporation","sea-contact","charted-coast","frontier-retreat","provincial-unrest","provincial-resistance","district-office","district-office-closed","district-incorporated","incorporation-abandoned","settlement-sponsorship","administrative-post","administrative-withdrawal"},StringComparer.Ordinal);
        static AtlasFact Record(PrehistoryState s,HistoricalEvent e)
        {
            var links=new List<AtlasLink>();var h=s.History;
            if(e.SettlementId>=0&&e.SettlementId<s.Society.Settlements.Length)links.Add(TownLink(s,s.Society.Settlements[e.SettlementId]));
            if(e.PolityId>=0&&e.PolityId<h.Polities.Length)links.Add(StateLink(s,h.Polities[e.PolityId]));
            if(e.OtherPolityId>=0&&e.OtherPolityId<h.Polities.Length&&e.OtherPolityId!=e.PolityId)links.Add(StateLink(s,h.Polities[e.OtherPolityId]));
            if(e.CultureId>=0&&e.CultureId<s.Society.Cultures.Length)links.Add(CultureLink(s.Society.Cultures[e.CultureId]));
            if(e.ReligionId>=0&&e.ReligionId<h.Religions.Length)links.Add(FaithLink(h.Religions[e.ReligionId]));
            return new AtlasFact{Label="Year "+Y(s,e.Year),Value=e.Text,Related=links.ToArray()};
        }
        public static AtlasTimeline Timeline(PrehistoryState? s,string kind="world",int id=-1,int offset=0,bool includeRoutine=false)
        {
            var page=new AtlasTimeline{IncludeRoutine=includeRoutine};if(s?.History==null||s.Society==null)return page;
            int cell=kind=="city"&&id>=0&&id<s.Society.Settlements.Length?s.Society.Settlements[id].Cell:-1;
            var ruler=kind=="ruler"&&id>=0&&id<(s.History.Dynamics?.Leaders.Length??0)?s.History.Dynamics!.Leaders[id]:null;
            bool Includes(HistoricalEvent e)
            {
                switch(kind)
                {
                    case "world":return true;
                    case "province":return HistoricalProvinceIdentity.Exists(s,id)&&e.Cell==HistoricalProvinceIdentity.Center(s,id);
                    case "state":return id>=0&&(e.PolityId==id||e.OtherPolityId==id);
                    case "city":return id>=0&&(e.SettlementId==id||cell>=0&&e.Cell==cell);
                    case "people":return id>=0&&e.CultureId==id;
                    case "religion":return id>=0&&e.ReligionId==id;
                    case "ruler":return ruler!=null&&(e.PolityId==ruler.PolityId||e.OtherPolityId==ruler.PolityId)&&e.Year>=ruler.StartedYear&&(ruler.EndedYear<0||e.Year<=ruler.EndedYear);
                    default:return false;
                }
            }
            var events=s.History.Archive.Where(Includes).Where(e=>includeRoutine||!RoutineKinds.Contains(e.Kind)).OrderByDescending(e=>e.Year).ThenByDescending(e=>e.Id).ToArray();
            page.Total=events.Length;int last=events.Length==0?0:((events.Length-1)/page.PageSize)*page.PageSize;
            page.Offset=Math.Min(last,Math.Max(0,offset)/page.PageSize*page.PageSize);
            page.Records=events.Skip(page.Offset).Take(page.PageSize).Select(e=>Record(s,e)).ToArray();return page;
        }
    }
}
