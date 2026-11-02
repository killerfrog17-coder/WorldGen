using System;
using System.Linq;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static class PaleolithicView
    {
        public static string Age(PrehistoryState s)
        {
            if(!Prehistory.Paleolithic(s))return "";var c=s.Paleolithic.Communities;
            double complex=c.Where(x=>x.Residents>=25).Select(x=>PaleolithicRules.Adoption(s.Paleolithic,x,(int)PaleolithicTechnique.Bows)+PaleolithicRules.Adoption(s.Paleolithic,x,(int)PaleolithicTechnique.Needles)).DefaultIfEmpty(0).Max();
            return complex>.8?"Late Paleolithic":"Middle Paleolithic";
        }
        public static string Describe(Prehistory engine,PrehistoryState s,int cell,HistoryOverlay overlay=HistoryOverlay.Cultures)
        {
            if(engine==null||s==null||cell<0||cell>=engine.CellPatch.Length)return "Select a location";
            int patch=engine.CellPatch[cell];if(patch<0)return "No habitable land patch";var climate=engine.PaleolithicClimateAt(s,cell);
            string text=engine.PatchName(patch)+"\n"+Age(s)+" · "+SimulationClockView.Year(s,s.Years)+"\nClimate anomaly "+climate.TemperatureDeltaC.ToString("+0.0;-0.0;0.0")+" °C · moisture ×"+climate.PrecipitationMultiplier.ToString("0.00")+"\nSea-level fall "+climate.SeaFallM.ToString("0")+" m (migration model)";
            if(overlay==HistoryOverlay.States||overlay==HistoryOverlay.Territories||overlay==HistoryOverlay.Civilizations||overlay==HistoryOverlay.Provinces)return text+"\nNo centralized state or administrative territory is recorded here.";
            if(overlay==HistoryOverlay.HumanSpecies)return text+"\nHuman species: "+HumanRangeState.MaskName(HumanRangeState.Mask(s,cell));
            var c=s.Paleolithic.Communities.FirstOrDefault(x=>x.Patch==patch);if(c==null)return text+"\nNo resident sapiens community.";
            var band=engine.PaleolithicBandAt(s,cell);text+="\n\nHomo sapiens · "+s.Population[patch].ToString("N0")+" residents in this patch";
            if(band==null)return text+"\nNo surviving local band.";
            text+="\nExample band: "+band.Name+"\nSpeech: "+band.LanguageName+" ("+band.Language.WordOrder+", oral)\n\nLOCAL KNOWLEDGE · share of band samples";
            for(int k=0;k<PaleolithicRules.TechniqueCount;k++){double a=PaleolithicRules.Adoption(s.Paleolithic,c,k);if(a>.005)text+="\n"+PaleolithicRules.Names[k]+": "+(100*a).ToString("0.0")+"%";}
            text+="\n\nSPEECH DEVELOPMENT · model capacities";for(int k=0;k<4;k++)text+="\n"+((PaleolithicSpeech)k)+": "+(100*band.Speech[k]).ToString("0.0")+"%";
            text+="\nExample: "+PaleolithicRules.Utterance(band,"we","hunt","deer","tomorrow","food");
            return text+"\n\nLocal band identities are stable. Knowledge is distinct from available materials. Later ages are disabled.";
        }
        static AtlasFact Fact(string label,string value)=>new AtlasFact{Label=label,Value=value};
        static AtlasLink[] Knowledge(PrehistoryState s,PaleolithicCommunity c)=>Enumerable.Range(0,PaleolithicRules.TechniqueCount).Select(k=>new AtlasLink{Name=PaleolithicRules.Names[k],Value=(int)Math.Round(1000*PaleolithicRules.Adoption(s.Paleolithic,c,k)),Detail=string.Join(", ",PaleolithicRules.Prerequisites((PaleolithicTechnique)k).Select(t=>PaleolithicRules.Names[t]))}).ToArray();
        public static SelectionCard Card(PrehistoryState s,int cell,HistoryOverlay overlay,Prehistory? engine)
        {
            var card=new SelectionCard{Title="Select a location",Subtitle=overlay==HistoryOverlay.HumanSpecies?"Human species":overlay.ToString()};if(cell<0||cell>=s.Society.CellCulture.Length)return card;
            var map=PaleolithicMaps.For(s);int id=map.Id(cell,overlay);var c=map.Community(cell);
            if(id<0){card.Title="No recorded "+(overlay==HistoryOverlay.HumanSpecies?"human species":overlay.ToString().ToLowerInvariant());card.Note=overlay==HistoryOverlay.States||overlay==HistoryOverlay.Territories||overlay==HistoryOverlay.Provinces||overlay==HistoryOverlay.Civilizations?"No centralized state or administrative territory exists here.":"No population record for this layer at this location.";return card;}
            card.Title=map.Name(id,overlay);
            if(overlay==HistoryOverlay.HumanSpecies)
            {
                card.Note="Species ranges can overlap. Colour mixtures identify coexisting populations.";
                int patch=s.HumanRanges?.CellPatch[cell]??c?.Patch??-1;
                var facts=new System.Collections.Generic.List<AtlasFact>();if(c!=null)facts.Add(Fact("Homo sapiens",s.Population[c.Patch].ToString("N0")+" regional residents"));
                if(s.HumanRanges!=null&&patch>=0)foreach(var l in s.HumanRanges.Lineages){double n=HumanRangeState.ResidentsAt(s,l,patch);if(n>=1)facts.Add(Fact(HumanRangeState.Name(l.Species),n.ToString("N0")+" regional residents"));}card.Facts=facts.ToArray();return card;
            }
            if(c==null)return card;
            card.Subtitle=s.Society.Cultures[c.Identity].Name+" · "+Age(s);
            card.Facts=new[]{Fact("Regional population",s.Population[c.Patch].ToString("N0")),Fact("Species",HumanRangeState.Name(c.Species))};
            if(overlay==HistoryOverlay.Technology)
            {card.Bars=Knowledge(s,c);card.Note="Share of local bands with each technique. Learning spreads gradually; equipment and materials are separate.";}
            else if(overlay==HistoryOverlay.Languages)
            {
                var l=map.Language(id)!;card.Facts=card.Facts.Concat(new[]{Fact("Word order",l.WordOrder),Fact("Speech tradition","Oral"),Fact("Sound changes",l.Revision.ToString())}).ToArray();
                card.Bars=Enumerable.Range(0,4).Select(k=>new AtlasLink{Name=((PaleolithicSpeech)k).ToString(),Value=(int)Math.Round(1000*PaleolithicRules.SpeechAt(s.Paleolithic,c,k))}).ToArray();
                double transition=D.Clamp((s.Paleolithic.Seconds-c.LanguageChangeSeconds)/(1000.0*SimulationTime.Year));
                if(c.PreviousLanguage!=null&&transition<1)card.Note="New speech variant: "+(transition*100).ToString("0")+"% adoption. Older speech still coexists; local bands have related dialects.";else card.Note="Related local dialects share inherited roots. Speech capacities and cultural identity are separate.";
            }
            else card.Note="Mobile hunter-gatherer communities; no centralized state.";
            return card;
        }
        public static AtlasLink? Selection(PrehistoryState s,int cell,HistoryOverlay overlay)
        {
            var map=PaleolithicMaps.For(s);int id=map.Id(cell,overlay);if(id<0)return null;var c=map.Community(cell);
            return new AtlasLink{Kind=overlay==HistoryOverlay.Technology?"technology":overlay==HistoryOverlay.Languages?"language":overlay==HistoryOverlay.HumanSpecies?"human-species":"people",Id=overlay==HistoryOverlay.Technology?c!.Identity:id,Cell=cell,Name=map.Name(id,overlay)};
        }
        public static AtlasLink[] Directory(PrehistoryState s,string kind,string search,bool past)
        {
            HistoryOverlay overlay=kind=="technology"?HistoryOverlay.Technology:kind=="language"?HistoryOverlay.Languages:kind=="human-species"?HistoryOverlay.HumanSpecies:HistoryOverlay.Cultures;
            if(kind!="technology"&&kind!="language"&&kind!="people"&&kind!="human-species")return Array.Empty<AtlasLink>();
            var entries=kind=="technology"?s.Society.Cultures.Where(c=>past||c.Population>0).Select(c=>new AtlasLink{Kind=kind,Id=c.Id,Cell=c.OriginCell,Name=c.Name+" technology"}):PaleolithicMaps.For(s).Legend(overlay,"Default").Select(e=>new AtlasLink{Kind=kind,Id=e.ColorId,Cell=e.Cell,Name=e.Name});
            return entries.Where(e=>e.Name.IndexOf(search??"",StringComparison.OrdinalIgnoreCase)>=0).Select(e=>{e.Measures=AtlasTables.Measures(s,e);return e;}).ToArray();
        }
        public static AtlasDocument Document(PrehistoryState s,string kind,int id,Prehistory? engine)
        {
            var d=new AtlasDocument{Kind=kind,Id=id,Year=s.Years,Subtitle=SimulationClockView.Year(s,s.Years)+" · "+Age(s),Title="Paleolithic world"};
            if(kind=="world")
            {d.Facts=new[]{Fact("Homo sapiens",SocietyView.Population(s).ToString("N0")),Fact("Resident communities",s.Society.Cultures.Count(c=>c.Population>0).ToString()),Fact("States","0")};d.Sections=new[]{new AtlasSection{Title="Local knowledge",Text="Open a community's technology profile for all techniques and their adoption shares.",Links=Directory(s,"technology","",false)},new AtlasSection{Title="Recorded speech communities",Links=Directory(s,"language","",false)},new AtlasSection{Title="Human species",Links=Directory(s,"human-species","",false)}};return d;}
            var entry=Directory(s,kind,"",true).FirstOrDefault(e=>e.Id==id);if(entry==null){d.Title="Record unavailable";return d;}
            var overlay=kind=="technology"?HistoryOverlay.Technology:kind=="language"?HistoryOverlay.Languages:kind=="human-species"?HistoryOverlay.HumanSpecies:HistoryOverlay.Cultures;
            var card=Card(s,entry.Cell,overlay,engine);d.Title=entry.Name;d.Cell=entry.Cell;d.Introduction=card.Note;d.Facts=card.Facts;
            var c=PaleolithicMaps.For(s).Community(entry.Cell);
            var sections=new System.Collections.Generic.List<AtlasSection>();
            if(kind=="technology"&&c!=null)
            {sections.Add(new AtlasSection{Title="Local knowledge and adoption",Bars=true,Unit="% of local bands",Links=Knowledge(s,c).Select(l=>new AtlasLink{Name=l.Name,Value=l.Value/10,Detail=l.Detail}).ToArray()});sections.Add(new AtlasSection{Title="Technique requirements",Facts=Knowledge(s,c).Where(l=>l.Detail.Length>0).Select(l=>Fact(l.Name,l.Detail)).ToArray()});}
            if(kind=="language")
            {var l=PaleolithicMaps.For(s).Language(id)!;sections.Add(new AtlasSection{Title="Sounds and reusable roots",Facts=new[]{Fact("Water",LanguageGenerator.Word(l,"water").Text),Fact("Fire",LanguageGenerator.Word(l,"fire").Text),Fact("Spear",LanguageGenerator.Word(l,"spear").Text),Fact("Word order",l.WordOrder)},Text=card.Note});sections.Add(new AtlasSection{Title="Speech capacities",Bars=true,Unit="% of local bands",Links=card.Bars.Select(b=>new AtlasLink{Name=b.Name,Value=b.Value/10}).ToArray()});}
            if(c!=null)sections.Add(new AtlasSection{Title="People and knowledge",Links=new[]{new AtlasLink{Kind="people",Id=c.Identity,Cell=entry.Cell,Name=s.Society.Cultures[c.Identity].Name},new AtlasLink{Kind="technology",Id=c.Identity,Cell=entry.Cell,Name="Local technology"}}});
            d.Sections=sections.ToArray();return d;
        }
    }
}
