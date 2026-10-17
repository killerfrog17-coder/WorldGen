using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static partial class HistoryAtlas
    {
        static AtlasLink SpokenLanguageLink(PrehistoryState s,int id,int cell)=>new AtlasLink{Kind="language",Id=id,Cell=cell,Name=s.History.Languages.Spoken.Varieties[id].Name,Detail="Spoken language"};
        static string SpeechName(PrehistoryState s,int id)=>id<0?"Not recorded":s.History.Languages.Spoken.Varieties[id].Name;
        public static AtlasSection? SpokenLocalSection(PrehistoryState s,int cell)
        {
            var p=Prehistory.SpokenCommunityAt(s,cell);if(p==null)return null;
            var facts=new List<AtlasFact>{F("Main home language",SpeechName(s,p.Primary))};
            var bars=new List<AtlasLink>{new AtlasLink{Name=SpeechName(s,p.Primary),Value=1000-p.MinorityPermille,Kind="language",Id=p.Primary,Cell=cell}};
            if(p.Minority>=0&&p.MinorityPermille>0)bars.Add(new AtlasLink{Name=SpeechName(s,p.Minority),Value=p.MinorityPermille,Kind="language",Id=p.Minority,Cell=cell});
            if(p.Second>=0&&p.Second!=p.Primary)facts.Add(F("Second language",SpeechName(s,p.Second)+" · competence "+p.BilingualPermille+" / 1,000"));
            if(p.Administrative>=0)facts.Add(F("Administrative language",SpeechName(s,p.Administrative)));
            if(p.Liturgical>=0)facts.Add(F("Ritual language",SpeechName(s,p.Liturgical)));
            if(p.LastContact>=0)facts.Add(F("Last physical language contact",Y(s,p.LastContact)));
            return new AtlasSection{Title="Spoken languages here",Facts=facts.ToArray(),Links=bars.ToArray(),Bars=true,Unit="household estimate / 1,000",Text=(p.RegionalEstimate?"Regional approximation. ":"Local community estimate. ")+"Home-language shares partition households; second-language ability overlaps them. Neither a ruler's language nor a ritual language implies that everyone speaks it at home. Up to two home communities and one second language are represented, not a complete linguistic census."};
        }
        static void SpokenLanguage(PrehistoryState s,int id,AtlasDocument d,List<AtlasSection> sections)
        {
            var r=s.History.Languages.Spoken;var v=r.Varieties[id];var p=v.Sounds;
            d.Title=v.Name;d.Cell=v.Cell;d.Subtitle="Spoken language · "+(v.Parent<0?"Recorded foundation":"Descendant of "+r.Varieties[v.Parent].Name);
            d.Introduction="This speech community is distinct from political allegiance and cultural identity. Households carry language through migration; repeated contact supports bilingualism and borrowing, while prolonged separation can produce related local varieties. An administrative language need not replace the local vernacular.";
            d.Facts=new[]{F("Recorded since",Y(s,v.FoundedYear)),F("Sounds",LanguageGenerator.SoundInventory(p)),F("Sentence order",p.WordOrder),F("Loan roots",N(p.Loans.Length))};
            sections.Add(new AtlasSection{Title="Sounds and reusable roots",Facts=new[]{F("Name structure",p.ModifierFirst?"Descriptive root before place word":"Place word before descriptive root"),F("Writing profile",p.Script+" scaffold; actual literacy depends on the community")},Timeline=new[]{"river","sea","house","land","people","stone"}.Select(c=>{var w=LanguageGenerator.Word(p,c);return F(c,w.Text+" /"+w.Phonetics+"/");}).ToArray()});
            var related=r.Varieties.Where(x=>x.Id!=id&&(x.Parent==id||x.Id==v.Parent)).Take(16).Select(x=>{var link=SpokenLanguageLink(s,x.Id,x.Cell);link.Value=SpokenLanguageEffects.Intelligibility(p,x.Sounds);link.Detail="Structural intelligibility "+link.Value+" / 1,000 (approximate; learned bilingualism is separate)";return link;});
            sections.Add(Links("Related speech communities",related,"Relatedness and actual language competence are distinct. The score compares bounded sound/grammar profiles, not a measured comprehension test.",true,"/ 1,000"));
            var local=SpokenLocalSection(s,v.Cell);if(local!=null)sections.Add(local);
            var towns=s.Society.Settlements.Where(t=>t.Population>0&&s.History.Aggregates.Living.Cities[t.Id].AbandonedYear<0).Select(t=>new{Town=t,Speech=Prehistory.SpokenCommunityAt(s,t.Cell)}).Where(x=>x.Speech!=null&&(x.Speech.Primary==id||x.Speech.Minority==id||x.Speech.Second==id)).OrderByDescending(x=>x.Town.Population).ThenBy(x=>x.Town.Id).Take(16).Select(x=>{var link=TownLink(s,x.Town);link.Detail=x.Speech.Primary==id?"Main home language":x.Speech.Minority==id?"Resident home-language minority":"Learned second language";return link;});
            sections.Add(Links("Recorded speech communities",towns,"Examples from actual towns, not estimated worldwide speaker counts."));
        }
    }
}
