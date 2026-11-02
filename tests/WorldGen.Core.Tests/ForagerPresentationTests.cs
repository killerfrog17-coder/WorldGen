using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
static class ForagerPresentationTests
{
    public static void Run(string file)
    {
        var w=WorldFile.ReadFile(file);var s=w.Prehistory;var engine=new Prehistory(w);var f=s.Foragers;var map=ForagerMaps.For(s);int checks=0;
        void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        var active=f.Cohorts.Where(c=>c.Population>=1).ToArray();double population=active.Sum(c=>c.Population);
        foreach(var overlay in Enum.GetValues<HistoryOverlay>())
        {
            var legend=SocietyView.Legend(s,overlay,engine:engine);bool political=overlay==HistoryOverlay.States||overlay==HistoryOverlay.Civilizations||overlay==HistoryOverlay.Territories||overlay==HistoryOverlay.Provinces||overlay==HistoryOverlay.ReligiousInstitutions;
            Check(political?legend.Length==0:legend.Length>0,"Political layers stay empty; actual cultural/language/belief/toolkit/species layers have records: "+overlay);
            if(!political)Check(Math.Abs(legend.Sum(e=>e.Population)-population)<active.Length*2,"Overlay totals account for all human lineages and minorities exactly once: "+overlay);
            var codes=PrehistoryView.Codes(w,engine,s,overlay:overlay);Check(codes.Length==w.Surface.Cells.Length,"Actual renderer codes cover the whole surface");
            foreach(var e in legend.Take(12))
            {
                var card=HistoryAtlas.Card(s,e.Cell,overlay,engine);Check(card.Title.Length>0&&card.Facts.Length>0,"Visible map cards expose real observer data: "+overlay);
                Check(e.Color.X>=0&&e.Color.X<=1&&e.Color.Y>=0&&e.Color.Y<=1&&e.Color.Z>=0&&e.Color.Z<=1,"Palette channels are valid");
            }
        }
        foreach(string kind in new[]{"people","technology","ethnicity","language","language-family","religion","human-species"})
        foreach(var entry in HistoryAtlas.Directory(s,kind).Take(16))
        {
            var document=HistoryAtlas.Read(s,entry.Kind,entry.Id,engine:engine);Check(document.Title!="Record unavailable"&&document.Facts.Length>0,"Atlas opens the exact selected record: "+kind);
            Check(!document.Subtitle.Contains("BC")&&!document.Subtitle.Contains("BP"),"Calendar is the world's own elapsed year");AtlasTables.Measures(s,entry);
        }
        foreach(var c in active.Take(50))Check(map.Id(f.Districts[c.District].Cell,HistoryOverlay.States)==-1,"A population home range never masquerades as a state");
        var relatedTraditions=f.Beliefs.GroupBy(b=>b.Family).FirstOrDefault(g=>g.Count()>1)?.Take(2).ToArray();
        if(relatedTraditions!=null)
        {
            var a=map.Color(relatedTraditions[0].Id,HistoryOverlay.Religions);var b=map.Color(relatedTraditions[1].Id,HistoryOverlay.Religions);
            Check((a/a.Length-b/b.Length).Length>.1,"Separate local traditions have distinct hues, not brightness shades of one ancestral religion");
        }
        Check(s.Hash==s.ComputeHash(),"All map/card/directory/atlas queries leave the simulation unchanged");
        Console.WriteLine($"PASS {checks} forager observer integration checks");
    }
}
