using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HistoryAtlasTests
{
    public static void ReviewWorld(string source)
    {
        var world=WorldFile.Read(File.ReadAllBytes(source));var s=world.Prehistory;string before=s.ComputeHash();int documents=0,links=0;
        foreach(var kind in new[]{"state","city","people","religion","ruler"})
        {
            var directory=HistoryAtlas.Directory(s,kind,past:true);
            foreach(var link in directory.Take(300))
            {
                var doc=HistoryAtlas.Read(s,kind,link.Id,world);if(doc.Title!=link.Name)throw new Exception("Mismatched atlas entry: "+link.Name);
                foreach(var target in doc.Sections.SelectMany(x=>x.Links.Concat(x.Timeline.SelectMany(e=>e.Related))).Where(l=>l.Kind.Length>0))
                {
                    if(HistoryAtlas.Read(s,target.Kind,target.Id,world).Title!=target.Name)throw new Exception("Broken atlas link: "+target.Name);links++;
                }
                if(doc.Sections.GroupBy(x=>x.Title).Any(g=>g.Count()>1))throw new Exception("Duplicate article sections");documents++;
            }
            Console.WriteLine(kind+": "+directory.Length+" directory records");
        }
        if(s.ComputeHash()!=before)throw new Exception("Atlas modified simulation state");
        Console.WriteLine("PASS: "+documents+" sampled profiles, "+links+" cross-links; checkpoint unchanged "+before);
    }
    public static void Run(Action<bool,string> check)
    {
        var s=new PrehistoryState{Years=2000,Population=new long[]{8000},Society=new SocietyState{
            CellCulture=new[]{0,0,1},Cultures=new[]{new CultureIdentity{Id=0,Name="Riverfolk",OriginCell=0,Population=5000,Subsistence="Crop cultivation",Resources="Grain",Contacts=new[]{1}},new CultureIdentity{Id=1,Name="Coastfolk",OriginCell=2,Population=4000,Subsistence="Fishing"}},
            Settlements=new[]{new Settlement{Id=0,Name="First Harbor",CultureId=0,PolityId=0,Cell=0,Population=600,FoundedYear=1000},new Settlement{Id=1,Name="Old City",CultureId=1,PolityId=1,Cell=2,Population=400,FoundedYear=1200}}},
            History=new HistoryState{CellPolity=new[]{0,0,1},CellReligion=new[]{0,0,0},Cultures=new[]{new HistoricalCulture{Id=0,ReligionId=0,LanguageId=0},new HistoricalCulture{Id=1,ReligionId=0,LanguageId=1}},
            Religions=new[]{new HistoricalReligion{Id=0,CultureId=0,HolyCell=0,Name="River tradition",Ethos="Reciprocity",Ritual="Seasonal gatherings",Tradition="An ancestral teaching"}},
            Polities=new[]{new HistoricalPolity{Id=0,CultureId=0,CapitalId=0,Name="River Court",Population=7000,Ruler="Aster",FoundedYear=1500},new HistoricalPolity{Id=1,CultureId=1,CapitalId=1,Name="Coast Court",Population=2000,EndedYear=1900}},
            Relations=new[]{new HistoricalRelation{A=0,B=1,WarSince=1800}},Towns=new[]{new HistoricalTown{Id=0},new HistoricalTown{Id=1}},Dynamics=new HistoricalDynamics{States=new[]{new HistoricalStateDynamics{Id=0},new HistoricalStateDynamics{Id=1}}}}};
        s.History.Archive.Add(new HistoricalEvent{Id=0,Year=1500,PolityId=0,CultureId=0,Kind="state-founded",Text="River Court forms around First Harbor."});
        s.History.Archive.Add(new HistoricalEvent{Id=1,Year=1600,PolityId=0,Kind="battle",Text="Routine fighting."});
        s.History.Cultures[0].Technology[(int)HistoricalTechnology.Storage]=1201;
        string hash=s.ComputeHash(),serialized=WorldJson.Write(s);
        var overview=HistoryAtlas.Read(s);
        check(overview.Introduction.Contains("9,000 people")&&overview.Facts.Single(f=>f.Label=="Active wars").Value=="0","World overview conserves rural and settlement population and excludes wars involving ended states");
        check(overview.Sections.Single(x=>x.Title=="Political centers").Links.Length==1,"Overview ranks surviving states only");
        check(HistoryAtlas.Directory(s,"state").Length==1&&HistoryAtlas.Directory(s,"state",past:true).Length==2,"Atlas historical-record switch preserves former states");
        check(HistoryAtlas.Directory(s,"state","river").Single().Name=="River Court","Atlas names can be searched case-insensitively");
        var state=HistoryAtlas.Read(s,"state",0);check(state.Introduction.Contains("Aster")&&state.Sections.Any(x=>x.Title=="Diplomatic neighborhood"),"State articles combine prose, connected records, and diplomatic context");
        check(state.Sections.Single(x=>x.Title=="Recorded history").Timeline.Length==1&&s.History.Archive.Count==2,"Reader omits routine noise without deleting history");
        check(HistoryAtlas.Selection(s,0,HistoryOverlay.States)?.Kind=="state"&&HistoryAtlas.ProfilesAt(s,0).Any(l=>l.Kind=="city"&&l.Id==0),"State overlay selects its court while retaining direct access to the local named settlement");
        check(HistoryAtlas.Selection(s,1,HistoryOverlay.States)?.Kind=="state","A hinterland state cell opens its court rather than another culture");
        check(HistoryAtlas.Selection(s,0,HistoryOverlay.Religions)?.Kind=="religion","Religious view respects the selected dominant tradition");
        check(HistoryAtlas.Selection(s,0,HistoryOverlay.Technology)?.Kind=="technology"&&HistoryAtlas.Card(s,0,HistoryOverlay.Technology).Subtitle.Contains("technology"),"Technology map opens technology, even on a city cell");
        check(HistoryAtlas.Read(s,"technology",0).Sections.Any(x=>x.Title=="Knowledge and deployment"),"Technology reader has a dedicated knowledge/deployment section");
        check(HistoryAtlas.Selection(s,0,HistoryOverlay.Cultures)?.Kind=="people","Culture map selects people rather than a city's government");
        var noFaith=s.Copy();noFaith.History.CellReligion[0]=-1;
        check(HistoryAtlas.Selection(noFaith,0,HistoryOverlay.Religions)==null,"An unrecorded religion never falls back to an unrelated city or culture");
        check(HistoryAtlas.Selection(s,0,HistoryOverlay.Provinces)==null,"An absent provincial record does not masquerade as a city profile");
        check(HistoryAtlas.Card(s,0,HistoryOverlay.States).Facts.Any(x=>x.Label=="Capital"&&x.Value=="First Harbor"),"Political selection uses compact labeled facts");
        check(HistoryAtlas.OverviewVisual(state)?.Links.Length<=6,"Overview charts stay compact and use recorded data");
        check(HistoryAtlas.Read(s,"people",0).Sections.Single(x=>x.Title=="Practices and knowledge").Timeline.Single().Label=="Year 1,200","Practice dates decode stored adoption-year-plus-one correctly");
        check(HistoryAtlas.Read(s,"state",1).Introduction.Contains("Its people and cities may continue under later governments"),"Historic state entries do not equate state collapse with extinction of a people");
        check(HistoryAtlas.Read(s,"religion",0).Sections.Any(x=>x.Text.Contains("not unanimous belief")),"Religion profiles do not invent adherent counts");
        check(HistoryAtlas.Read(null).Sections.Length==0&&HistoryAtlas.Directory(null,"city").Length==0&&HistoryAtlas.Selection(s,-1,HistoryOverlay.States)==null,"Loading and pre-recorded-history states are safe");
        check(HistoryAtlas.Read(s,"state",99).Title=="Record unavailable","Missing records have a safe explanation rather than broken navigation");
        var hostile=new AtlasDocument{Id=0,Title="<script>"};var diagram=new AtlasSection{Links=new[]{new AtlasLink{Name="<img onerror='x'>",Detail="At war since year 1"}}};
        string svg=AtlasDiagram.DiplomaticSvg(hostile,diagram);check(!svg.Contains("<script>")&&!svg.Contains("<img")&&svg.Contains("&lt;img"),"Diagram escapes record names rather than injecting markup");
        var restored=WorldJson.Read<AtlasDocument>(WorldJson.Write(state));check(restored.Title==state.Title&&restored.Sections.Length==state.Sections.Length,"Atlas documents survive browser worker serialization");
        foreach(string kind in new[]{"state","city","people","religion","ruler"})foreach(var link in HistoryAtlas.Directory(s,kind,past:true))check(HistoryAtlas.Read(s,link.Kind,link.Id).Title==link.Name,"Directory entries resolve to matching profiles");
        check(HistoryAtlas.ProfilesAt(s,0).Select(l=>l.Kind).SequenceEqual(new[]{"state","city","people","religion"}),"Map inspection links separately to its court, city, people, and belief");
        check(HistoryAtlas.ProfilesAt(s,-1).Length==0,"No location has no accidental historical links");
        var foragers=s.Copy();foragers.Society.CellCulture[1]=-1;foragers.History.CellPolity[1]=-1;
        foragers.History.Archive.Add(new HistoricalEvent{Id=foragers.History.Archive.Count,Year=1800,Cell=1,ReligionId=0,Text="Traveling teachers share the river tradition."});
        string faithInspection=SocietyView.Describe(foragers,1,HistoryOverlay.Religions);
        check(faithInspection.Contains("River tradition")&&faithInspection.Contains("LOCAL CHRONICLE")&&faithInspection.Contains("Traveling teachers"),"Religious inspection works among followers without a named culture or state");
        var paged=s.Copy();paged.History.Archive=Enumerable.Range(0,97).Select(i=>new HistoricalEvent{Id=i,Year=1000+i,PolityId=0,SettlementId=0,Cell=0,Kind=i%2==0?"great-conquest":"battle",Text="Episode "+i}).ToList();
        string beforePaging=paged.ComputeHash();var first=HistoryAtlas.Timeline(paged,"state",0);var second=HistoryAtlas.Timeline(paged,"state",0,40);
        check(first.Total==49&&first.Records.Length==40&&second.Records.Length==9,"Milestone pages expose older history instead of truncating it permanently");
        check(first.Records[0].Value=="Episode 96"&&second.Records[0].Value=="Episode 16","Timeline paging uses stable descending year and record order");
        var all=HistoryAtlas.Timeline(paged,"city",0,80,true);check(all.Total==97&&all.Offset==80&&all.Records.Length==17,"Routine-record mode exposes the complete subject archive");
        check(HistoryAtlas.Timeline(paged,"state",0,int.MaxValue,true).Offset==80&&HistoryAtlas.Timeline(paged,"state",0,-10,true).Offset==0,"Out-of-range page requests are bounded without throwing");
        check(HistoryAtlas.Timeline(null).Total==0&&HistoryAtlas.Timeline(s,"state",999).Total==0,"Empty or unavailable histories have safe timeline pages");
        check(first.Records.All(r=>r.Related.Any(l=>l.Kind=="city"&&l.Id==0)&&r.Related.Any(l=>l.Kind=="state"&&l.Id==0)),"Recorded events retain links to named actors and locations");
        check(paged.ComputeHash()==beforePaging,"Filtering, paging, and linking the archive do not alter its authoritative records");
        check(HistoryAtlas.PlainText(state).Contains("River Court")&&HistoryAtlas.PlainText(state,second).Contains("9 of 49 matching records"),"Copied articles contain the narrative and identify partial history pages honestly");
        var local=new HabitatCell{Biome=Biome.TemperateForest,MeanTenthsC=140,GrowingDays=180,Flooding=FloodRegime.RiverSeasonal,FloodDaysPerYear=20,FloodFractionPermille=90};
        var geography=new World{Surface=new Surface{Cells=new[]{new Cell{Position=new V(1,0,0)},new Cell{Position=new V(0,1,0)},new Cell{Position=new V(-1,0,0)}}},Hydrology=new HydrologyResult{OceanConnected=new bool[3]},Ecology=new EcologyResult{Region=new[]{0,0,0},Habitat=new[]{local,local,local},Regions=new[]{new Ecoregion{Id=0,Name="River woods"}}}};
        string geographyHash=StateHash.Compute(geography);var setting=HistoryAtlas.Read(s,"city",0,geography).Sections.Single(x=>x.Title=="Local setting");
        check(setting.Text.Contains("River woods")&&setting.Text.Contains("Seasonal river flooding")&&setting.Facts.Any(f=>f.Value=="14 °C"),"Geographic context reads the existing biome, flood regime, and temperature in English");
        var portrait=HistoryAtlas.Read(s,"city",0,geography).Habitat;
        check(portrait!=null&&portrait.Key=="temperate"&&portrait.Conditions.Any(f=>f.Value.Contains("180 days")),"Habitat portraits use actual local biome and growing season");
        check(portrait.Fauna.Length==0&&portrait.Caption.Contains("Illustrative"),"A landscape illustration invents neither local animals nor a reconstruction of the city");
        var habitatCard=MapSelection.Read(geography,0,Layer.Ecoregions);
        check(habitatCard.Title=="River woods"&&habitatCard.IllustrationKey=="temperate"&&habitatCard.Facts.Any(f=>f.Label=="Growing season"),"Ecoregion selection uses actual habitat facts and an explicitly illustrative landscape");
        check(MapSelection.Read(geography,-1,Layer.Ecoregions).Title=="Select a location","Base-map cards are safe without a selected location");
        check(StateHash.Compute(geography)==geographyHash,"Reading geographic context does not regenerate or mutate terrain, climate, or ecology");
        check(s.ComputeHash()==hash&&WorldJson.Write(s)==serialized,"Atlas browsing and chart generation do not mutate the simulation checkpoint or archive");
        var closed=s.Copy();closed.History.Administration=new HistoricalAdministration{Accounts=new[]{new HistoricalAdministrativeAccount{PolityId=1,Year=2000}}};
        var former=HistoryAtlas.Read(closed,"state",1);
        check(!former.Sections.Any(x=>x.Title=="Administration and expansion")&&former.Sections.Single(x=>x.Title=="Recorded administration").Text.Contains("not preserved"),"Former states do not present current zero accounts as historical fiscal estimates");
    }
}
