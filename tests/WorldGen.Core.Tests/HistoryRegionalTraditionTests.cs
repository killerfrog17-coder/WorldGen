using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HistoryRegionalTraditionTests
{
    public static void Run(Action<bool,string> check)
    {
        bool Eligible(long people=1600,long rural=3200,int farm=400,int institution=500,double fiscal=.25,double reserve=.8,bool supplied=true,bool decline=false,int peers=0)
            =>HistoricalProtoCentreEffects.Provisioned(people,rural,farm,institution,fiscal,reserve,supplied,decline,peers);
        check(Eligible(),"A genuinely supported coordinated agrotown can mature without needing a second city.");
        check(!Eligible(people:200),"A small village is not a proto-civilization.");
        check(!Eligible(rural:100),"An isolated unsupported cluster is not an urban provisioning system.");
        check(Eligible(people:3000,rural:100),"A substantial supported center can qualify without a mandatory multiple-city deadlock.");
        check(!Eligible(farm:100)&&!Eligible(institution:200)&&!Eligible(fiscal:.03)&&!Eligible(reserve:.1)&&!Eligible(supplied:false)&&!Eligible(decline:true),"Population alone, or merely having a river, cannot substitute for institutions and provisioning.");
        int prepared=0;for(int year=0;year<300;year++)prepared=HistoricalProtoCentreEffects.Preparation(prepared,1,true);
        check(prepared==300,"Institutional maturation represents actual elapsed generations.");
        check(HistoricalProtoCentreEffects.Preparation(300,50,false)==200,"Institutional preparation can decay without deleting residents.");
        check(HistoricalProtoCentreEffects.Preparation(500,1000,true)==600&&HistoricalProtoCentreEffects.Preparation(100,1000,false)==0,"Preparation is bounded at both ends.");
        double ideal=HistoricalProtoCentreEffects.AnnualOpportunity(1,1,1,1,0);
        check(ideal>HistoricalProtoCentreEffects.AnnualOpportunity(1,.001,1,1,0)*100,"Difficult ecology strongly limits proto-urban emergence as well as state emergence.");
        check(ideal>HistoricalProtoCentreEffects.AnnualOpportunity(1,1,.01,1,0)*100,"A village without useful storable staples is not automatically a proto-civilization.");
        check(ideal>HistoricalProtoCentreEffects.AnnualOpportunity(1,1,1,.00001,0)*1000,"The homeland-origin prior applies before proto-civilization classification too.");
        check(HistoricalProtoCentreEffects.AnnualOpportunity(1,1,1,.00001,600)>ideal,"Actual delivered institutional examples ease local development, without making distant awareness a global unlock.");
        check(HistoricalProtoCentreEffects.InstitutionalHazard(.5,2000)>HistoricalProtoCentreEffects.InstitutionalHazard(.1,0),"Supported proto-centers can consolidate as their coordination matures, without repeating the pristine-origin lottery.");
        var s=Fixture();
        check(HistoricalCells.Culture(s,0)==0&&HistoricalCells.Culture(s,1)==0,"A regional tradition covers multiple occupied cells without any per-cell writes.");
        check(HistoricalCells.Culture(s,2)==-1&&HistoricalCells.Culture(s,3)==-1,"Empty and ocean regions cannot acquire phantom culture.");
        check(HistoricalCells.Stage(s,0)==1&&HistoricalCells.Stage(s,1)==1,"One ordinary village does not promote an entire ecoregion or culture to proto-civilization.");
        check(HistoricalCells.Religion(s,1)==0,"Regional ritual tradition is available without a separate cell flood fill.");
        s.History.RegionalTraditions.Qualified=s.History.RegionalTraditions.Qualified.With(0,1);
        check(HistoricalCells.Stage(s,0)==2&&HistoricalCells.Stage(s,1)==1,"A qualified proto-center is nested locally inside its broader advanced culture.");
        check(SocietyView.LocalStage(s,0,SocietyStage.AdvancedCulture)=="Embryonic civilization"&&SocietyView.LocalStage(s,1,SocietyStage.AdvancedCulture)=="Advanced culture","Local proto-center labels do not contradict or globally promote their parent tradition.");
        check(PrehistoryView.PrimitiveGray(170)==.78&&PrehistoryView.PrimitiveGray(195)==.70&&PrehistoryView.PrimitiveGray(215)==.46,"Foragers, advanced cultures and embryonic civilizations have distinct progressively darker grays.");
        s.History.Aggregates.Living.Cities[0].AbandonedYear=450;
        check(HistoricalCells.Stage(s,0)==1,"An abandoned town cannot maintain proto-civilization status.");
        s.History.Aggregates.Regions[0].Culture=-1;
        check(HistoricalCells.Culture(s,1)==-1,"A dissolved regional identity disappears without erasing the local population.");
        check(s.Population[0]==10000,"Removing a cultural classification creates no demographic collapse.");
        s=Fixture();s.History.Cultures[0].Technology[(int)HistoricalTechnology.Cultivation]=301;
        check(WorldEraView.Of(s)==2&&WorldEraView.FirstYears(s)[3]==-1,"A farming technique alone no longer triggers the Neolithic or slower playback.");
        check(SimulationClockView.AutoRate(s,WorldEraView.Of(s))==500d*SimulationTime.Year,"Advanced cultures select500 years per second without changing the observer era.");
        s.History.RegionalTraditions.FirstProtoYear=500;
        check(WorldEraView.Of(s)==3&&WorldEraView.FirstYears(s)[3]==500,"The first sustained proto-center supplies the observer's Neolithic milestone.");
        check(SimulationClockView.EraRate(WorldEraView.Of(s))==100d*SimulationTime.Year,"The qualified Neolithic transition selects 100 years per second.");
        s.History.Cultures[0].Technology[(int)HistoricalTechnology.Bronze]=401;
        check(WorldEraView.FirstYears(s)[4]==-1&&WorldEraView.Of(s)==3,"A metalworking craft without a state does not end the observer's Neolithic.");
        s.History.Polities=new[]{new HistoricalPolity{FoundedYear=600}};
        check(WorldEraView.Of(s)==4&&WorldEraView.FirstYears(s)[4]==600,"First state institutions, not first bronze craft, supply the Bronze boundary.");
        s.History.RegionalTraditions=null;
        check(WorldEraView.FirstYears(s)[3]==300,"Legacy saves retain their old cultivation-based milestone.");
    }
    static PrehistoryState Fixture()
    {
        var a=new HistoricalAggregateState{Cells=new HistoricalCellTree(20),TownAt=new HistoricalCellTree(4),Cultures=new[]{new HistoricalCultureAggregate{Id=0}},Regions=new[]{new HistoricalRegionAggregate{Id=0,Culture=0},new HistoricalRegionAggregate{Id=1}},Living=new HistoricalLivingHistory{Cities=new[]{new HistoricalLivingCity()},Regions=new[]{new HistoricalLivingRegion(),new HistoricalLivingRegion()}},Empires=new HistoricalImperialSystem{Connections=new HistoricalColonialConnections{CellRegions=HistoricalCellTree.From(4,i=>i<2?0:i==2?1:-1)}}};
        a.TownAt=a.TownAt.With(0,0);
        return new PrehistoryState{Years=500,Population=new long[]{10000,0},Society=new SocietyState{Cultures=new[]{new CultureIdentity{Id=0}}},History=new HistoryState{Version=Prehistory.LivingWorldHistoryVersion,RegionalTraditions=new HistoricalRegionalTraditions{Predecessors=new HistoricalCellTree(2)},Aggregates=a,Cultures=new[]{new HistoricalCulture{Id=0,ReligionId=0,Technology=new int[39]}}}};
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;var engine=new Prehistory(world);engine.Validate(original);string hash=original.Hash;
        var upgraded=engine.UpgradeRegionalTraditions(original);engine.Validate(upgraded);
        check(original.History.RegionalTraditions==null&&upgraded.History.RegionalTraditions!=null,"Regional rules are explicit, copy-on-write and absent from the predecessor.");
        check(upgraded.History.Aggregates.TotalPopulation==original.History.Aggregates.TotalPopulation,"Upgrade invents no residents.");
        check(upgraded.History.Cultures.Zip(original.History.Cultures).All(p=>p.First.Technology.SequenceEqual(p.Second.Technology)),"Upgrade does not grant technology.");
        check(upgraded.Society.Cultures.Where(c=>upgraded.History.Aggregates.Cultures[c.Id].CivilizationSince<0).All(c=>c.Stage==SocietyStage.AdvancedCulture),"Village-bearing traditions are not globally classified as embryonic civilizations.");
        check(engine.UpgradeRegionalTraditions(upgraded).Hash==upgraded.Hash,"Rules upgrade is idempotent.");
        var invalid=upgraded.Copy();invalid.History.RegionalTraditions.Version=2;invalid.Seal();bool rejected=false;try{engine.Validate(invalid);}catch(ArgumentException){rejected=true;}
        check(rejected&&upgraded.History.RegionalTraditions.Version==1,"Unknown rules fail validation and copied records do not alias their source.");
        world.Prehistory=upgraded;var restored=WorldFile.Read(WorldFile.Write(world)).Prehistory;engine.Validate(restored);
        check(restored.Hash==upgraded.Hash,"Saved regional and local institution records retain the exact hash.");
        var one=engine.AdvanceYears(upgraded,20);var pieces=engine.AdvanceYears(engine.AdvanceYears(upgraded,7),13);engine.Validate(one);
        check(one.Hash==pieces.Hash&&one.Hash==engine.AdvanceYears(restored,20).Hash,"New actor rules are repeatable across publication partitioning and reloads.");
        var display=HistoricalMapSnapshot.For(one);
        check(Enumerable.Range(0,world.Surface.Cells.Length).All(i=>display.Culture(i)==HistoricalCells.Culture(one,i)&&display.Stage(i)==HistoricalCells.Stage(one,i)),"Cached and literal maps agree on all regional traditions and local proto-centers.");
        var edges=(int[][])typeof(Prehistory).GetField("regionalCultureEdges",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(engine)!;
        check(edges.All(cells=>cells.Length<=8),"Regional cultural boundary representatives are precomputed and bounded, not discovered by a terrain census during simulation.");
        int frontierCulture=Enumerable.Range(0,one.History.Aggregates.Cultures.Length).First(id=>one.History.Aggregates.Cultures[id].CivilizationSince>=0&&edges[one.History.Aggregates.Cultures[id].Region].Any(cell=>HistoricalCells.Culture(one,cell)==id&&world.Surface.Cells[cell].Neighbors.Any(n=>engine.CellPatch[n]>=0&&HistoricalCells.Culture(one,n)!=id)));
        var seeded=one.Copy();seeded.History.Aggregates.Cultures[frontierCulture].Frontier=new HistoricalLocalBag();
        typeof(Prehistory).GetMethod("SeedRegionalCulturalFrontier",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(engine,new object[]{seeded,frontierCulture});
        check(seeded.History.Aggregates.Cultures[frontierCulture].Frontier.Count>0&&seeded.History.Aggregates.Cultures[frontierCulture].Frontier.Count<=8,"A region-based culture retains a local cell frontier after its first civilization, rather than becoming permanently unable to diffuse.");
        check(original.ComputeHash()==hash,"Actual-world checks preserve the original source.");
    }
    public static void Review(string path,string output,int maximumYear)
    {
        Directory.CreateDirectory(output);var world=WorldFile.Read(File.ReadAllBytes(path));world.Prehistory=null;
        var engine=new Prehistory(world);var session=new SimulationSession(engine,engine.Start(engine.Prepare()));var rows=new List<object>();var captures=new HashSet<string>();int stop=maximumYear;
        while(session.LastSnapshot.Years<stop)
        {
            session.AdvanceSeconds(Math.Min(1000,stop-session.LastSnapshot.Years)*SimulationTime.Year,int.MaxValue);
            var s=session.LastSnapshot;var a=s.History.Aggregates;int first=s.History.Polities.Select(p=>p.FoundedYear).DefaultIfEmpty(-1).Min();
            if(first>=0)stop=Math.Min(stop,first+1000);
            int cultures=s.Society.Cultures.Count(c=>c.Stage>=SocietyStage.AdvancedCulture),proto=s.Society.Settlements.Count(t=>HistoricalRegionalTraditions.Proto(s,t.Id)&&a.Resolve(t.PolityId,s.History)<0&&a.Living.Cities[t.Id].AbandonedYear<0),towns=s.Society.Settlements.Count(t=>t.Population>0);
            if(s.Years%10000==0||first>=0||s.Years>=stop)
            {
                var row=new{Year=s.Years,Era=WorldEraView.Of(s),AdvancedTraditions=cultures,RetiredTraditions=s.Society.Cultures.Length-cultures,ProtoCenters=proto,Towns=towns,States=s.History.Polities.Count(p=>p.EndedYear<0),FirstProto=s.History.RegionalTraditions.FirstProtoYear,FirstState=first,Population=a.TotalPopulation};rows.Add(row);Console.WriteLine(JsonSerializer.Serialize(row));
            }
            string milestone=first>=0?"first-state":proto>0?"first-proto":towns>0?"villages":"";
            if(milestone!=""&&captures.Add(milestone)){string directory=Path.Combine(output,milestone);Directory.CreateDirectory(directory);HistoryAudit.Snapshot(world,engine,s,directory,new[]{HistoryOverlay.States,HistoryOverlay.Cultures},compressed:true,backgroundOpacity:1,overlayOpacity:.58,includeLabels:false);}
        }
        world.Prehistory=session.Snapshot();engine.Validate(world.Prehistory);File.WriteAllBytes(Path.Combine(output,"final.world"),WorldFile.Write(world));
        File.WriteAllText(Path.Combine(output,"review.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
    }
}
