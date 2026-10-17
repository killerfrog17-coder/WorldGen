using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HistoryMapSelectionTests
{
    // Imperial core, two client communities, a client's recognized title,
    // an unpopulated title, an unrelated court, a gray culture, and a court
    // whose cell has no local cultural/religious/provincial record.
    static PrehistoryState Fixture(int count,int[] sites)
    {
        int[] owners={0,1,1,-1,-1,2,-1,0},cultures={0,1,2,1,-1,2,1,-1},faiths={0,1,1,1,-1,1,-1,-1};
        var a=new HistoricalAggregateState{Cells=new HistoricalCellTree(count*5),TownAt=new HistoricalCellTree(count),Successors=new HistoricalCellTree(3),
            Polities=Enumerable.Range(0,3).Select(i=>new HistoricalPolityAggregate{Id=i,Remote=100}).ToArray()};
        var sources=new HistoricalCellTree(count);var regions=new HistoricalCellTree(count);
        for(int k=0;k<sites.Length;k++)
        {
            int cell=sites[k];regions=regions.With(cell,k);
            a.Cells=a.Cells.With(cell*5,cultures[k]).With(cell*5+1,owners[k]).With(cell*5+2,faiths[k]).With(cell*5+4,1);
            if(k<3||k==5)sources=sources.With(cell,k==0?0:k==5?2:1);
        }
        a.Empires=new HistoricalImperialSystem{Governments=new[]{new HistoricalImperialGovernment(),new HistoricalImperialGovernment{Patron=0},new HistoricalImperialGovernment()},
            Territories=new HistoricalTerritorialAdministration{Sources=sources,PostAt=new HistoricalCellTree(count),DistrictAreas=new long[]{100,200,300}},
            Connections=new HistoricalColonialConnections{CellRegions=regions,TitleAt=new HistoricalCellTree(8).With(3,0).With(4,1),
                Titles=new[]{new HistoricalRecognizedTitle{Id=0,Region=3,Holder=1},new HistoricalRecognizedTitle{Id=1,Region=4,Holder=0}},
                Cultures=new[]{new HistoricalCulturalContinuity{CivilizationRoot=0},new HistoricalCulturalContinuity{CivilizationRoot=0},new HistoricalCulturalContinuity{CivilizationRoot=2}}}};
        return new PrehistoryState{Started=true,Years=2000,Society=new SocietyState{
            Cultures=Enumerable.Range(0,3).Select(i=>new CultureIdentity{Id=i,Name="People "+i,OriginCell=sites[i],Stage=SocietyStage.AdvancedCulture}).ToArray(),
            Settlements=Enumerable.Range(0,3).Select(i=>new Settlement{Id=i,Name="Capital "+i,Cell=sites[i==2?5:i],CultureId=i,PolityId=i,Population=1000}).ToArray()},
            History=new HistoryState{Aggregates=a,
                Cultures=Enumerable.Range(0,3).Select(i=>{var c=new HistoricalCulture{Id=i,Technical=new HistoricalTechnicalPractice()};c.Technology[(int)HistoricalTechnology.Iron]=1;return c;}).ToArray(),
                Polities=Enumerable.Range(0,3).Select(i=>new HistoricalPolity{Id=i,Name="Court "+i,CapitalId=i,CultureId=i==2?0:i}).ToArray(),
                Religions=Enumerable.Range(0,2).Select(i=>new HistoricalReligion{Id=i,Name="Faith "+i,HolyCell=sites[i],CultureId=i}).ToArray()}};
    }
    public static void Run(Action<bool,string> check)
    {
        var s=Fixture(8,Enumerable.Range(0,8).ToArray());string before=WorldJson.Write(s);var display=HistoricalMapSnapshot.For(s);
        foreach(var mode in Enum.GetValues<HistoryOverlay>())for(int cell=-1;cell<=8;cell++)
            check(SocietyView.SelectionId(s,cell,mode)==display.SelectionId(cell,mode),"Cached and direct selection agree: "+mode+" / "+cell);
        void Subject(int cell,HistoryOverlay mode,string kind,int id)
        {var link=HistoryAtlas.Selection(s,cell,mode);check(link?.Kind==kind&&link.Id==id,"Click selects "+kind+" "+id+" in "+mode+" at "+cell);}
        Subject(1,HistoryOverlay.States,"state",0);
        Subject(1,HistoryOverlay.Territories,"state",1);
        Subject(1,HistoryOverlay.Civilizations,"people",0);
        Subject(1,HistoryOverlay.Cultures,"people",1);
        Subject(1,HistoryOverlay.Provinces,"province",1);
        Subject(1,HistoryOverlay.Religions,"religion",1);
        Subject(1,HistoryOverlay.Technology,"technology",1);
        foreach(int cell in new[]{3,4})
        {Subject(cell,HistoryOverlay.States,"state",0);Subject(cell,HistoryOverlay.Civilizations,"people",0);check(HistoryAtlas.Card(s,cell,HistoryOverlay.States).Title=="Court 0","Claimed land's main card is the sovereign, never its title");}
        foreach(var mode in new[]{HistoryOverlay.States,HistoryOverlay.Civilizations,HistoryOverlay.Territories})Subject(6,mode,"people",1);
        Subject(7,HistoryOverlay.Cultures,"people",0);Subject(7,HistoryOverlay.Technology,"technology",0);
        foreach(var mode in new[]{HistoryOverlay.Provinces,HistoryOverlay.Religions})check(HistoryAtlas.Selection(s,7,mode)==null,"Missing "+mode+" never substitutes a court or people");
        check(display.OverlayId(0,HistoryOverlay.Technology)==display.OverlayId(2,HistoryOverlay.Technology)&&display.SelectionId(0,HistoryOverlay.Technology)!=display.SelectionId(2,HistoryOverlay.Technology),"A shared technology-era color does not merge unrelated research subjects");
        check(display.ColorId(0,HistoryOverlay.States)==display.ColorId(5,HistoryOverlay.States)&&display.SelectionId(0,HistoryOverlay.States)!=display.SelectionId(5,HistoryOverlay.States),"Same ruling culture color does not merge independent states");
        check(HistoryAtlas.ProfilesAt(s,3).Any(l=>l.Kind=="claim"),"Territorial-title documents remain accessible as secondary profiles");
        check(WorldJson.Write(s)==before,"All map focus queries leave authoritative history unchanged");
        var legacy=s.Copy();legacy.History.Aggregates=null;
        legacy.Society.CellCulture=new[]{0};legacy.History.CellPolity=new[]{0};legacy.History.CellReligion=new[]{-1};legacy.History.Polities[0].EndedYear=1900;
        foreach(var mode in Enum.GetValues<HistoryOverlay>())
            check(SocietyView.SelectionId(legacy,0,mode)==HistoricalMapSnapshot.For(legacy).SelectionId(0,mode),"Legacy retired states have matching focus in "+mode);
        check(HistoryAtlas.Selection(legacy,0,HistoryOverlay.States)?.Kind=="people"&&SocietyView.OverlayId(legacy,0,HistoryOverlay.States)==-1,"An obsolete cell-owner record cannot resurrect an ended state");
    }
    public static void Visual(World world,Action<bool,string> check)
    {
        var engine=new Prehistory(world);
        int[] sites=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>engine.CellPatch[i]>=0&&!world.Hydrology.OceanConnected[i]&&world.Hydrology.LakeCoverageFraction[i]<.9).Take(8).ToArray();
        var s=Fixture(world.Surface.Cells.Length,sites);s.Population=Enumerable.Repeat(1000L,engine.Patches.Length).ToArray();
        foreach(bool optimized in new[]{true,false})foreach(var mode in Enum.GetValues<HistoryOverlay>())
        {
            var codes=PrehistoryView.Codes(world,engine,s,sites[1],mode,optimized:optimized);
            int selected=SocietyView.SelectionId(s,sites[1],mode);
            foreach(int cell in sites)
                check((codes[cell]==245||codes[cell]==255)==(SocietyView.SelectionId(s,cell,mode)==selected),"Highlight follows selected "+mode+" rather than tint/local title: "+cell+" / cache="+optimized);
        }
        var missing=PrehistoryView.Codes(world,engine,s,sites[7],HistoryOverlay.Religions);
        check(sites.All(cell=>missing[cell]!=245&&missing[cell]!=255),"A missing faith record highlights no unrelated culture");
        foreach(bool optimized in new[]{true,false})
        {
            var political=PrehistoryView.Codes(world,engine,s,sites[7],HistoryOverlay.States,optimized:optimized);
            check(Enumerable.Range(0,political.Length).Where(cell=>political[cell]==245||political[cell]==255).All(cell=>SocietyView.SelectionId(s,cell,HistoryOverlay.States)==0),"A state cell without a local culture does not also highlight an unrelated forager cluster: cache="+optimized);
        }
        foreach(var mode in new[]{HistoryOverlay.States,HistoryOverlay.Civilizations,HistoryOverlay.Territories})
        {var codes=PrehistoryView.Codes(world,engine,s,sites[6],mode);check(codes[sites[6]]==255&&codes[sites[1]]!=255,"Gray culture focus does not absorb its politically incorporated neighbors in "+mode);}
        var island=engine.Patches.FirstOrDefault(p=>p.Island);
        if(island!=null)
        {
            var islandSites=(int[])sites.Clone();islandSites[6]=island.Cells[0];
            var islandState=Fixture(world.Surface.Cells.Length,islandSites);islandState.Population=s.Population;
            foreach(bool optimized in new[]{true,false})
                check(PrehistoryView.Codes(world,engine,islandState,islandSites[6],HistoryOverlay.States,optimized:optimized)[islandSites[6]]==65,"A selected gray culture on a subcell island keeps its selected-island mask: cache="+optimized);
        }
        foreach(var layer in GlobeView.VisibleLayers.Where(l=>l!=Layer.Simulation))
        {
            var card=MapSelection.Read(world,sites[1],layer);
            if(layer==Layer.Ecoregions)
                check(card.Title==world.Ecology.Regions[world.Ecology.Region[sites[1]]].Name,"Ecoregion selection identifies the actual local ecoregion");
            else
                check(card.Subtitle==GlobeView.LayerName(layer)&&card.Title==CellInspection.Describe(world,sites[1],layer),"Base selection reports the active layer's measurement: "+layer);
        }
    }
}
