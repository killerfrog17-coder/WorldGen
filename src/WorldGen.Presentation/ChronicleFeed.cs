using System;
using System.Linq;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    // Per-viewer cursor over append-only records. No recurring archive census.
    public sealed class ChronicleFeed
    {
        readonly ChronicleEvent?[] latest=new ChronicleEvent?[3];
        int processed,lastYear=-1;bool initialized;
        public ChronicleEvent[] Read(PrehistoryState state)
        {
            if(state?.History==null)return Array.Empty<ChronicleEvent>();
            var archive=state.History.Archive;int oldest=state.Years-(state.History.Polities.Length>0?300:2000);
            if(!initialized||archive.Count<processed||state.Years<lastYear)
            {
                Array.Clear(latest,0,latest.Length);processed=FirstAtOrAfter(archive,oldest);initialized=true;
            }
            if(processed<archive.Count)
            {
                var news=new ChronicleNews(state);
                for(int i=processed;i<archive.Count;i++)
                {
                    var e=archive[i];int category=(int)e.Category;if(e.Year<oldest||category<0||category>=3)continue;
                    if(latest[category]!=null&&e.Year/ChronicleNews.Window(e.Category)<=latest[category]!.Year/ChronicleNews.Window(e.Category))continue;
                    var item=news.Project(e);if(item!=null)latest[category]=item;
                }
                processed=archive.Count;
            }
            lastYear=state.Years;return latest.Where(e=>e!=null&&e.Year>=oldest).Select(e=>e!).OrderBy(e=>e.Year).ThenBy(e=>e.Category).ToArray();
        }
        public static int FirstAtOrAfter(HistoricalEventArchive archive,int year)
        {
            int low=0,high=archive.Count;while(low<high){int mid=low+(high-low)/2;if(archive[mid].Year<year)low=mid+1;else high=mid;}return low;
        }
    }
}
