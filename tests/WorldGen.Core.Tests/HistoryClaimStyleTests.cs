using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

// Four renderer-only cells: administered, title-only, contested actual control,
// and unclaimed. No simulation handlers, terrain generation or rules upgrade.
public static class HistoryClaimStyleTests
{
    static PrehistoryState Fixture()
    {
        string emptyArchive=StateHash.Compute(new List<HistoricalEvent>());
        var a=new HistoricalAggregateState{Cells=new HistoricalCellTree(20),TownAt=new HistoricalCellTree(4),Successors=new HistoricalCellTree(2),LeaderDigests=new HistoricalCellTree(1),ArchiveBaseHash=emptyArchive,ArchiveHash=emptyArchive,
            Polities=new[]{new HistoricalPolityAggregate{Id=0},new HistoricalPolityAggregate{Id=1}}};
        var regions=new HistoricalCellTree(4);var sources=new HistoricalCellTree(4);
        for(int i=0;i<4;i++)
        {regions=regions.With(i,i);a.Cells=a.Cells.With(i*5,0).With(i*5+1,i==0?0:i==2?1:-1).With(i*5+4,1);}
        sources=sources.With(0,0).With(2,1);
        a.Empires=new HistoricalImperialSystem{Governments=new[]{new HistoricalImperialGovernment(),new HistoricalImperialGovernment()},
            Territories=new HistoricalTerritorialAdministration{Sources=sources,PostAt=new HistoricalCellTree(4),DistrictAreas=new long[]{10,10}},
            Connections=new HistoricalColonialConnections{CellRegions=regions,TitleAt=new HistoricalCellTree(4).With(1,0).With(2,1),
                Titles=new[]{new HistoricalRecognizedTitle{Id=0,Region=1,Holder=0},new HistoricalRecognizedTitle{Id=1,Region=2,Holder=0}},
                Cultures=new[]{new HistoricalCulturalContinuity(),new HistoricalCulturalContinuity()}}};
        return new PrehistoryState{Started=true,Years=1000,Society=new SocietyState{
            Cultures=new[]{new CultureIdentity{Id=0,Name="People",Stage=SocietyStage.Civilization},new CultureIdentity{Id=1,Name="Other people",Stage=SocietyStage.Civilization}},
            Settlements=new[]{new Settlement{Id=0,Name="Capital",Cell=0,CultureId=0,PolityId=0},new Settlement{Id=1,Name="Other capital",Cell=2,CultureId=1,PolityId=1}}},
            History=new HistoryState{Aggregates=a,Cultures=new[]{new HistoricalCulture{Id=0},new HistoricalCulture{Id=1}},
                Polities=new[]{new HistoricalPolity{Id=0,Name="Title holder",CapitalId=0,CultureId=0},new HistoricalPolity{Id=1,Name="Actual controller",CapitalId=1,CultureId=1}}}};
    }
    static bool Same(V a,V b)=>a.X==b.X&&a.Y==b.Y&&a.Z==b.Z;
    static double Chroma(V a)=>Math.Max(a.X,Math.Max(a.Y,a.Z))-Math.Min(a.X,Math.Min(a.Y,a.Z));
    public static void Run(Action<bool,string> check)
    {
        var s=Fixture();string before=WorldJson.Write(s),hash=s.ComputeHash();var display=HistoricalMapSnapshot.For(s);
        check(!display.ClaimOnly(0)&&display.ClaimOnly(1)&&!display.ClaimOnly(2)&&!display.ClaimOnly(3),"Only title-without-controller is pale");
        check(!display.ClaimOnly(-1)&&!display.ClaimOnly(4),"Out-of-range display queries are not claims");
        for(int cell=-1;cell<=4;cell++)check(display.ClaimOnly(cell)==SocietyView.ClaimOnly(s,cell),"Bounded click query matches cached display classification: "+cell);
        check(!SocietyView.ClaimOnly(null,0),"Missing checkpoint has no claim-only selection");
        check(display.Sovereign(1)==-1&&display.PoliticalSovereign(1)==0,"Title selection remains distinct from actual administration");
        check(display.PoliticalSovereign(2)==1&&display.OverlayId(2,HistoryOverlay.States)==1,"Contested actual administration wins over foreign title");
        foreach(var mode in Enum.GetValues<HistoryOverlay>())
        {
            V full=SocietyView.OverlayColor(s,0,mode),pale=SocietyView.OverlayColor(s,0,mode,true);
            bool political=mode==HistoryOverlay.States||mode==HistoryOverlay.Civilizations;
            check(Same(full,SocietyView.OverlayColor(s,0,mode,false)),"Actual controller retains original RGB in "+mode);
            check(political?!Same(full,pale):Same(full,pale),"Claim styling is limited to political overlays: "+mode);
            if(political)check(Chroma(pale)<Chroma(full)&&pale.X+pale.Y+pale.Z>full.X+full.Y+full.Z,"Claim RGB is lighter and less saturated: "+mode);
            var bytes=Enumerable.Range(0,16).Select(i=>(byte)(i%4==3?(i==7?245:235):17)).ToArray();
            var alpha=Enumerable.Range(0,4).Select(i=>bytes[i*4+3]).ToArray();
            SocietyView.ColorPalette(s,bytes,0,mode);
            for(int cell=0;cell<4;cell++)
            {
                check(bytes[cell*4+3]==alpha[cell],"Occupied and selected alpha codes remain unchanged: "+cell+"/"+mode);
                int owner=display.ColorId(cell,mode);if(owner<0)continue;
                // Same shared call and (owner,claimOnly) key as the Unity upload path.
                bool claim=display.ClaimOnly(cell);long key=((long)owner<<1)|(claim?1L:0L);
                var native=SocietyView.OverlayColor(s,owner,mode,claim);
                check(bytes[cell*4]==(byte)Math.Round(native.X*255)&&bytes[cell*4+1]==(byte)Math.Round(native.Y*255)&&bytes[cell*4+2]==(byte)Math.Round(native.Z*255),"Browser/native shared RGB agrees: "+cell+"/"+mode);
                if(cell==1&&political)check(key!=((long)display.ColorId(0,mode)<<1),"Same-holder claim has a separate native tint-cache key");
            }
        }
        check(display.SelectionId(0,HistoryOverlay.States)==display.SelectionId(1,HistoryOverlay.States),"Selecting a title still focuses its existing sovereign identity");
        var workerBytes=Enumerable.Repeat((byte)99,28).ToArray();workerBytes[19]=245;
        SocietyView.ColorPalette(s,workerBytes,12,HistoryOverlay.States);
        check(workerBytes.Take(12).All(value=>value==99)&&workerBytes[19]==245,"Browser worker palette header and selected alpha survive the actual12-byte offset");
        check(workerBytes.Skip(24).Take(3).All(value=>value==99),"Unclaimed land retains the caller's original base RGB");
        var card=HistoryAtlas.Card(s,1,HistoryOverlay.States);
        check(card.Title=="Title holder"&&card.Note.Contains(SocietyView.PoliticalClaimNote),"Claim selection retains sovereign title and explains missing local administration");
        check(!HistoryAtlas.Card(s,0,HistoryOverlay.States).Note.Contains(SocietyView.PoliticalClaimNote),"Actual control has no claim-only warning");
        check(!HistoryAtlas.Card(s,2,HistoryOverlay.States).Note.Contains(SocietyView.PoliticalClaimNote),"A foreign title cannot label actual control as claim-only");
        check(WorldJson.Write(s)==before&&s.ComputeHash()==hash,"Display styling and inspection leave all authoritative bytes/hash unchanged");
        var client=Fixture();client.History.Aggregates.Empires.Governments[1].Patron=0;
        check(!HistoricalMapSnapshot.For(client).ClaimOnly(2)&&!SocietyView.ClaimOnly(client,2),"Actual client administration is full color even under the title holder's sovereign");
        var ended=Fixture();ended.History.Polities[0].EndedYear=900;
        check(!HistoricalMapSnapshot.For(ended).ClaimOnly(1)&&!SocietyView.ClaimOnly(ended,1),"An obsolete title without a surviving holder is not colored as a live claim");
        var legacy=s.Copy();legacy.History.Aggregates=null;legacy.Society.CellCulture=new[]{0};legacy.History.CellPolity=new[]{0};legacy.History.CellReligion=new[]{-1};
        check(!HistoricalMapSnapshot.For(legacy).ClaimOnly(0),"Legacy state with no title extension is not falsely pale");
    }
}
