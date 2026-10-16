using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
public static class HistoryPresentationTests
{
    public static void Run(Action<bool,string> check)
    {
        HistoryAtlasTests.Run(check);
        var metric=new HistoryLegendEntry{Population=1234567,AreaKm2=234567};
        check(SocietyView.SortValue(metric,"Population")=="1,234,567"&&SocietyView.SortValue(metric,"Land area")=="234,567 km²","Ranked rows expose population or land area with units");
        check(SocietyView.SortValue(metric,"Default")=="","Abstract dominance score is not exposed as a metric");
        var news=new PrehistoryState{Years=1000,Society=new SocietyState{Cultures=new[]{new CultureIdentity{Id=0,Name="Riverfolk"},new CultureIdentity{Id=1,Name="Coastfolk"}}},History=new HistoryState{Cultures=new[]{new HistoricalCulture{Id=0},new HistoricalCulture{Id=1}},Polities=new[]{new HistoricalPolity{Population=10000}}}};
        news.History.Archive.Add(new HistoricalEvent{Year=900,Kind="great-conquest",PolityId=0,Category=ChronicleCategory.Politics,Text="A capital falls."});
        news.History.Archive.Add(new HistoricalEvent{Year=990,Kind="succession",Category=ChronicleCategory.Politics,Text="An ordinary ruler takes office."});
        news.History.Archive.Add(new HistoricalEvent{Year=995,Kind="trade",Category=ChronicleCategory.Politics,Text="An ordinary agreement."});
        news.History.Cultures[0].Technology[(int)HistoricalTechnology.Iron]=999;news.History.Cultures[1].Technology[(int)HistoricalTechnology.Iron]=1000;
        news.History.Archive.Add(new HistoricalEvent{Year=998,CultureId=0,Kind="technology",Category=ChronicleCategory.Discoveries,Text="Riverfolk develops iron working."});
        news.History.Archive.Add(new HistoricalEvent{Year=999,CultureId=1,Kind="technology",Category=ChronicleCategory.Discoveries,Text="Coastfolk learns iron working."});
        check(SocietyView.Headlines(news).Single(e=>e.Category==ChronicleCategory.Politics).Text=="A capital falls.","Routine appointments and agreements cannot crowd out important headlines");
        check(news.History.Archive.Count==5&&SocietyView.Headlines(news).Length==2,"Filtering headlines preserves complete historical records and distinct categories");
        check(SocietyView.Headlines(news).Single(e=>e.Category==ChronicleCategory.Discoveries).Text=="Riverfolk discovered "+HistoricalTechnologies.Name(news.History.Version,(int)HistoricalTechnology.Iron)+".","Only the first discovery is news; later adoption stays in the archive");
        var feed=new ChronicleFeed();var initial=feed.Read(news);
        for(int i=0;i<5000;i++)news.History.Archive.Add(new HistoricalEvent{Year=1000,Kind="charted-coast",Category=ChronicleCategory.Discoveries,Text="A routine chart update."});
        check(feed.Read(news).Any(e=>e.Year==998&&e.Category==ChronicleCategory.Discoveries),"An incremental news cursor retains a real headline through thousands of routine chart records");
        check(feed.Read(news).Select(e=>e.Text).SequenceEqual(initial.Select(e=>e.Text)),"Repeated viewer refreshes do not churn the lead stories");
        var alerts=news.Copy();var alertsFeed=new ChronicleFeed();alertsFeed.Read(alerts);
        alerts.History.Archive.Add(new HistoricalEvent{Year=1000,Kind="war",PolityId=0,Category=ChronicleCategory.Politics});
        alertsFeed.Read(alerts);
        alerts.History.Archive.Add(new HistoricalEvent{Year=1001,Kind="nuclear-strike",PolityId=0,Category=ChronicleCategory.Politics,Text="A nuclear attack devastates a capital. Full details remain archived."});
        check(alertsFeed.Read(alerts).Single(e=>e.Category==ChronicleCategory.Politics).Text=="A nuclear attack devastates a capital.","An extraordinary event replaces routine war news within its editorial window");
        alerts.History.Archive.Add(new HistoricalEvent{Year=1002,Kind="peace",PolityId=0,Category=ChronicleCategory.Politics});
        check(alertsFeed.Read(alerts).Single(e=>e.Category==ChronicleCategory.Politics).Year==1001,"Lesser news cannot immediately bury an extraordinary same-window event");
        alerts.History.Archive.Add(new HistoricalEvent{Year=1005,Kind="international-foundation",PolityId=0,Category=ChronicleCategory.Politics,Text="Two states establish a trading league. Its budget is finite."});
        check(alertsFeed.Read(alerts).Single(e=>e.Category==ChronicleCategory.Politics).Year==1005,"Later-window diplomacy still replaces an older extraordinary headline");
        var projection=new ChronicleNews(alerts);
        check(projection.Project(new HistoricalEvent{Kind="state",PolityId=0,Category=ChronicleCategory.Politics})!=null,"Actual state-foundation event names reach the news projection");
        check(projection.Project(new HistoricalEvent{Kind="nuclear-arsenal",PolityId=0,Category=ChronicleCategory.Politics})!=null,"Paid arsenal commissioning is politics news, not another claim of invention");
        var collision=news.Copy();collision.Society.Settlements=new[]{new Settlement{Id=0,Name="Capital",Cell=0}};collision.History.Archive.Add(new HistoricalEvent{Year=1001,Kind="war",PolityId=0,Category=ChronicleCategory.Politics,Text="A conventional war begins."});
        collision.History.Archive.Add(new HistoricalEvent{Year=1001,Kind="nuclear-strike",PolityId=0,Category=ChronicleCategory.Politics,Text="A nuclear attack devastates a capital. Its actual losses are recorded."});
        check(HistoryAtlas.NewsDetail(collision,1001,-1,-1,ChronicleCategory.Politics).Value.StartsWith("A nuclear attack",StringComparison.Ordinal),"Opening an extraordinary headline retrieves the matching prioritized event, not a lesser same-year record");
        var detail=HistoryAtlas.NewsDetail(news,998,0,-1,ChronicleCategory.Discoveries);
        check(detail.Value=="Riverfolk develops iron working."&&detail.Related.Any(l=>l.Kind=="people"),"A short invention headline opens the full original record and its linked people");
        var stale=news.Copy();stale.Years=1500;check(SocietyView.Headlines(stale).Length==0,"Old news does not remain as a current headline indefinitely");
        check(SocietyView.OverlayId(null!,-1)==-1,"No historical overlay owner before a checkpoint exists");
        check(SocietyView.Describe(null!,-1)==""&&SocietyView.Culture(null,-1)==null,"Empty historical selection is safe during asynchronous loading");
        var untouched=new byte[]{1,2,3,4};SocietyView.ColorPalette(null!,untouched,0);
        check(untouched.SequenceEqual(new byte[]{1,2,3,4}),"Missing history does not alter the base palette");
        string fixture="artifacts/societies-1/Earth.world";World world;
        if(File.Exists(fixture))world=WorldFile.Read(File.ReadAllBytes(fixture));else{var recipe=Recipe.Preset("Earth");recipe.Detail=5000;world=new WorldPipeline().Generate(recipe);}
        foreach(var data in SurfaceMesh.Chunks(world,new ViewOptions{CachedDisplay=true,GridSpacing=0,ShowRivers=false}).Take(2))
        {
            check(data.BlendCellIds.Length==data.CellIds.Length*2&&data.NaturalHeight.Length==data.CellIds.Length,"Continuous ground attributes align with categorical cells");
            check(data.BlendCellIds.All(i=>i>=0&&i<world.Surface.Cells.Length),"Ground blend references real cells");
            check(data.NaturalHeight.All(float.IsFinite),"Finite shore heights");
            for(int i=0;i<data.Triangles.Length;i+=3)if(data.CellIds[data.Triangles[i]]!=data.CellIds[data.Triangles[i+1]]||data.CellIds[data.Triangles[i]]!=data.CellIds[data.Triangles[i+2]])throw new Exception("Historical ownership interpolates across cells");
            check(true,"Every historical overlay triangle has one cell owner");
            var projected=MapView.Project(data,0,0,true);
            check(projected.BlendCellIds.Length==projected.CellIds.Length*2&&projected.NaturalHeight.Length==projected.CellIds.Length,"Projection preserves continuous ground attributes");
            for(int i=0;i<projected.Triangles.Length;i+=3)if(projected.CellIds[projected.Triangles[i]]!=projected.CellIds[projected.Triangles[i+1]]||projected.CellIds[projected.Triangles[i]]!=projected.CellIds[projected.Triangles[i+2]])throw new Exception("Map seam mixes historical owners");
            check(true,"Map seam keeps historical territories crisp");
        }
    }
}
