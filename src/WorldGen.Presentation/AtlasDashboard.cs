using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Presentation
{
    public enum AtlasVisual { Metrics,Bars,Composition,Network,Timeline,Technology,Phonetics,Links,Appearance }
    public static class AtlasDashboard
    {
        public static AtlasFact[] HeadlineFacts(AtlasDocument d)
        {
            string[] keys=d.Kind=="state"?new[]{"Population","Land area","Government","Army levy","Treasury","Country founded"}:d.Kind=="city"?new[]{"Population","Residents","Founded","State","Culture","Food reserves"}:Array.Empty<string>();
            return keys.Select(k=>d.Facts.FirstOrDefault(f=>f.Label==k)).Where(f=>f!=null).Concat(d.Facts).Distinct().Take(6).ToArray();
        }
        public static string Lead(AtlasDocument d)
        {
            if(string.IsNullOrWhiteSpace(d.Introduction))return "";
            int stop=d.Introduction.IndexOf(". ",StringComparison.Ordinal);
            return stop>=0?d.Introduction.Substring(0,stop+1):d.Introduction;
        }
        static string[] Priorities(string kind)
        {
            switch(kind)
            {
                case "world":return new[]{"Population accounts","Human presence","Regional livelihoods","largest","Population","Reach of practices","Knowledge","Belief","Recorded history"};
                case "state":return new[]{"Empire and constituent","Diplomatic neighborhood","Field forces","Military","Public priorities","Political interests","Cities","Research","Financ","Stability","Crisis","Political lineage","Recorded history"};
                case "province":return new[]{"Institutions and allegiance","Identity and belonging","Spoken languages","Public worship","Local historical memory","Allegiance and sovereignty","Recorded history"};
                case "city":return new[]{"Food and everyday","Population and movement","Crops and cultivated","Settlement history and hinterland","Local political","Identity and belonging","Spoken languages","Community and connections","Allegiance and sovereignty","Recorded history"};
                case "people":return new[]{"Way of life","Subsistence","Livelihood","People and appearance","Spoken languages","Identity and belonging","Research","Crops","Contacts","Origins","Recorded history"};
                case "ethnicity":return new[]{"Inherited and emerging","Inherited traditions","People and appearance","Identity and belonging","Recorded communities","Before advanced cultures","Founding context"};
                case "language":return new[]{"Sounds and reusable roots","Related speech communities","Spoken languages","Recorded speech communities","Language and names"};
                case "religion":return new[]{"Beliefs and sources","Branches and doctrinal","Social teachings","Observance and sacred","Affiliated ritual","Public worship","Recorded history"};
                case "religious-body":return new[]{"Ritual community","Affiliat","Institution","Funding","Observance","Public worship","Recorded history"};
                case "technology":return new[]{"Research","Practical","Knowledge","Technical","Capabilities","Contacts","Sources"};
                case "ruler":return new[]{"Person and family","Public office","Abilities","Rulers","Succession","Recorded history"};
                case "notable":return new[]{"Recorded achievements","Family and household","Abilities and interests","Recorded public office","Career and affiliations"};
                case "organization":return new[]{"Member governments","Mandate and limits","Founding initiative","Institutions","Recorded history"};
                default:return new[]{"Local setting","People and appearance","Recorded history"};
            }
        }
        public static AtlasSection[] Panels(AtlasDocument d)
        {
            var priorities=Priorities(d.Kind);
            int Rank(AtlasSection section)
            {for(int i=0;i<priorities.Length;i++)if(section.Title.IndexOf(priorities[i],StringComparison.OrdinalIgnoreCase)>=0)return i;return 100;}
            bool Useful(AtlasSection s)=>s.Facts.Length>0||s.Technical.Length>0||s.Timeline.Length>0||s.Links.Any(l=>!s.Bars||l.Value>0);
            return d.Sections.Where(Useful).OrderBy(Rank).ThenBy(s=>Array.IndexOf(d.Sections,s)).Take(8).ToArray();
        }
        public static AtlasVisual Visual(AtlasSection s)
        {
            string title=s.Title;
            if(title=="People and appearance")return AtlasVisual.Appearance;
            if(title=="Sounds and reusable roots")return AtlasVisual.Phonetics;
            if(s.Technical.Length>0)return AtlasVisual.Technology;
            if(s.Timeline.Length>0||title=="Recorded history"||title=="Recorded achievements")return AtlasVisual.Timeline;
            if(s.Diagram||title.Contains("Family")||title.Contains("lineage")||title.Contains("emerging identities")||title.Contains("Related speech")||title.Contains("Branches and doctrinal")||title=="Member governments")return AtlasVisual.Network;
            if(s.Bars&&(title=="Way of life"||title=="Public priorities"||title=="Political interests"||title=="Identity and belonging"||title=="Home languages"))return AtlasVisual.Composition;
            if(s.Bars)return AtlasVisual.Bars;
            return s.Links.Length>0?AtlasVisual.Links:AtlasVisual.Metrics;
        }
        public static string Caption(AtlasSection s)
        {
            switch(Visual(s))
            {
                case AtlasVisual.Bars:return string.IsNullOrEmpty(s.Unit)?"Compare recorded values":s.Unit=="/ 1,000"?"Independent indices · 0–1,000":s.Unit;
                case AtlasVisual.Composition:return "Shares of this recorded account · not a census";
                case AtlasVisual.Network:return "Recorded connections · click a name to explore";
                case AtlasVisual.Timeline:return "Dated records · newest first";
                case AtlasVisual.Technology:return "Research effort, knowledge and practical deployment are separate";
                case AtlasVisual.Appearance:return "Regional variation · illustrative samples, not a reconstruction";
                case AtlasVisual.Phonetics:return "Sound inventory, grammar and reusable roots";
                default:return "";
            }
        }
        public static long Maximum(AtlasSection s)=>s.Unit=="/ 1,000"?1000:Math.Max(1,s.Links.Select(x=>Math.Abs(x.Value)).DefaultIfEmpty(1).Max());
        public static string BarLabel(AtlasSection s,AtlasLink link)
            =>s.Unit=="/ 1,000"?link.Value+" / 1,000":link.Value.ToString("N0",System.Globalization.CultureInfo.InvariantCulture)+(s.Unit.Length>0?" "+s.Unit:"");
        public static string Color(int index)=>new[]{"#8ebba8","#d2b577","#83abc9","#c897a5","#b2a4cb","#a6b980","#cb9374","#8cbfc0"}[Math.Abs(index%8)];
    }
}
