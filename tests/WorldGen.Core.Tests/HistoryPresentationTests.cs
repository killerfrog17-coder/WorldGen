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
        var news=new PrehistoryState{Years=1000,Society=new SocietyState(),History=new HistoryState{Polities=new[]{new HistoricalPolity{Population=10000}}}};
        news.History.Archive.Add(new HistoricalEvent{Year=900,Kind="great-conquest",PolityId=0,Category=ChronicleCategory.Politics,Text="A capital falls."});
        news.History.Archive.Add(new HistoricalEvent{Year=990,Kind="succession",Category=ChronicleCategory.Politics,Text="An ordinary ruler takes office."});
        news.History.Archive.Add(new HistoricalEvent{Year=995,Kind="trade",Category=ChronicleCategory.Politics,Text="An ordinary agreement."});
        news.History.Archive.Add(new HistoricalEvent{Year=998,Kind="technology",Category=ChronicleCategory.Discoveries,Text="Iron working spreads."});
        check(SocietyView.Headlines(news).Single(e=>e.Category==ChronicleCategory.Politics).Text=="A capital falls.","Routine appointments and agreements cannot crowd out important headlines");
        check(news.History.Archive.Count==4&&SocietyView.Headlines(news).Length==2,"Filtering headlines preserves complete historical records and distinct categories");
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
