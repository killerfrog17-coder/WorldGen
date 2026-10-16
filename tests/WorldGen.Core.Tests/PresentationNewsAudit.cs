using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class PresentationNewsAudit
{
    public static void Run(string file)
    {
        var s=WorldFile.Read(File.ReadAllBytes(file)).Prehistory;var h=s.History;var news=new ChronicleNews(s);
        int start=h.Polities.Select(p=>p.FoundedYear).DefaultIfEmpty(0).Min(),end=s.Years,years=Math.Max(1,end-start+1);
        Console.WriteLine(file+" · "+years+" historical years · "+h.Archive.Count+" archived events");
        var raw=new int[3,years];var filtered=new int[3,years];var buckets=new HashSet<(ChronicleCategory,int)>();var examples=new List<string>();
        foreach(var e in h.Archive)
        {
            if(e.Year<start||e.Year>end)continue;raw[(int)e.Category,e.Year-start]++;
            var projected=news.Project(e);if(projected==null)continue;
            if(examples.Count<12&&e.Category==ChronicleCategory.Discoveries)examples.Add(projected.Text);
            if(buckets.Add((e.Category,e.Year/ChronicleNews.Window(e.Category))))filtered[(int)e.Category,e.Year-start]++;
        }
        for(int c=0;c<3;c++)
        {
            string Stats(int[,] values){var v=Enumerable.Range(0,years).Select(y=>values[c,y]).OrderBy(x=>x).ToArray();return $"total={v.Sum()}, mean/year={v.Average():F3}, p95={v[(int)((years-1)*.95)]}, max/year={v[^1]}";}
            Console.WriteLine((ChronicleCategory)c+" raw "+Stats(raw)+" → editorial "+Stats(filtered));
            int recent=Math.Min(1000,years);Console.WriteLine("  Last "+recent+" years: raw="+Enumerable.Range(years-recent,recent).Sum(y=>raw[c,y])+", editorial="+Enumerable.Range(years-recent,recent).Sum(y=>filtered[c,y]));
        }
        Console.WriteLine("Most common kinds: "+string.Join(", ",h.Archive.GroupBy(e=>e.Kind).OrderByDescending(g=>g.Count()).Take(18).Select(g=>g.Key+"="+g.Count())));
        Console.WriteLine(string.Join(Environment.NewLine,examples));
        var timer=System.Diagnostics.Stopwatch.StartNew();var feed=new ChronicleFeed();var latest=feed.Read(s);double cold=timer.Elapsed.TotalMilliseconds;timer.Restart();for(int i=0;i<100;i++)feed.Read(s);
        Console.WriteLine($"Editorial feed: cold {cold:F2} ms; repeated unchanged reads {timer.Elapsed.TotalMilliseconds/100:F4} ms/read; "+string.Join(" | ",latest.Select(e=>e.Category+": "+e.Text)));
        Console.WriteLine("Checkpoint unchanged: "+(s.Hash==s.ComputeHash()));
    }
}
