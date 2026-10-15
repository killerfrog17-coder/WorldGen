using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

public static partial class WorkerApi
{
    static World world;
    static HistoryOverlay historyOverlay=HistoryOverlay.States;
    static readonly HistoryBorders historyBorders=new HistoryBorders();
    static Prehistory humans;static int humanSelection=-1,humanVariant;static byte[] humanPalette;
    static SimulationSession humanSession;
    static HistoricalTechnologyCeiling preferredTechnologyCeiling=HistoricalTechnologyCeiling.Modern;
    // This worker build does not enable WASM threads. It uses the identical
    // ordered solver with one worker; native hosts may use bounded parallelism.
    static void EnsureHumans(){if(humans?.World!=world){humans=new Prehistory(world){MaximumWorkers=1};humanSelection=-1;humanPalette=null;if(world.Prehistory?.History?.Modern!=null)preferredTechnologyCeiling=world.Prehistory.History.Modern.TechnologyCeiling;}if(world.Prehistory==null){world.Prehistory=humans.Prepare();if(preferredTechnologyCeiling!=HistoricalTechnologyCeiling.Modern)world.Prehistory=humans.SetTechnologyCeiling(world.Prehistory,preferredTechnologyCeiling);}}
    [JSExport] public static string People(string command,double rawValue)
    {
        EnsureHumans();
        if(!double.IsFinite(rawValue)||rawValue!=Math.Floor(rawValue)||Math.Abs(rawValue)>9007199254740991)throw new ArgumentException("Simulation requests require exact integer values.");
        bool clock=command=="time"||command=="timeBatch";int value=clock?0:checked((int)rawValue);var previous=world.Prehistory;
        if(command.StartsWith("atlas-events:",StringComparison.Ordinal))
        {
            var parts=command.Split(':');if(parts.Length!=4||!int.TryParse(parts[2],out int offset))throw new ArgumentException("Invalid timeline request.");
            return WorldJson.Write(HistoryAtlas.Timeline(world.Prehistory,parts[1],value,offset,parts[3]=="all"));
        }
        if(command.StartsWith("atlas:",StringComparison.Ordinal))
        {
            var document=HistoryAtlas.Read(world.Prehistory,command.Substring(6),value,world);
            if(document.Cell>=0){var position=SocietyView.Location(world,document.Cell);document.Longitude=Math.Atan2(position.Y,position.X)*180/Math.PI;document.Latitude=Math.Asin(position.Z)*180/Math.PI;}
            return WorldJson.Write(document);
        }
        if(command.StartsWith("atlas-directory:",StringComparison.Ordinal))return WorldJson.Write(HistoryAtlas.Directory(world.Prehistory,command.Substring(16),past:true));
        if(command=="overlay"){if(value<0||value>4)throw new ArgumentException("Unknown historical overlay.");historyOverlay=(HistoryOverlay)value;}
        if(command=="lore")return SocietyView.CampaignLore(world.Prehistory,world);
        if(command=="calendar"){var timeline=world.Prehistory.Timeline;return WorldJson.Write(new CalendarSummary{Seconds=SimulationTime.Seconds(world.Prehistory),NextEventSeconds=timeline!=null&&timeline.Events.Length>0?timeline.Events[0].Due:-1,RulesVersion=world.Prehistory.History?.Version??""});}
        if(command=="start")world.Prehistory=humans.Start(world.Prehistory);
        if(command=="upgradeHistory")world.Prehistory=humans.UpgradeToCurrentHistory(world.Prehistory);
        if(command=="technologyCeiling"){world.Prehistory=humans.SetTechnologyCeiling(world.Prehistory,(HistoricalTechnologyCeiling)value);preferredTechnologyCeiling=(HistoricalTechnologyCeiling)value;}
        if(command=="step")world.Prehistory=humans.Advance(world.Prehistory,value);
        if(command=="stepYears")world.Prehistory=humans.SetStepYears(world.Prehistory,value);
        if(command=="autoStep")world.Prehistory=humans.SetStepYears(world.Prehistory,100,true);
        if(command=="years")world.Prehistory=humans.AdvanceYears(world.Prehistory,value);
        if(clock||command=="nextEvent")world.Prehistory=humans.UpgradeScheduling(world.Prehistory);
        if(clock)
        {
            if(humanSession==null||!ReferenceEquals(humanSession.LastSnapshot,world.Prehistory))humanSession=new SimulationSession(humans,world.Prehistory);world.Prehistory=humanSession.AdvanceSeconds((long)rawValue,command=="timeBatch"?SimulationClockView.FastMaximumEvents:1,maximumGlobalWork:command=="timeBatch"?SimulationClockView.FastMaximumGlobalWork:int.MaxValue);
        }
        if(command=="nextEvent"){if(humanSession==null||!ReferenceEquals(humanSession.LastSnapshot,world.Prehistory))humanSession=new SimulationSession(humans,world.Prehistory);world.Prehistory=humanSession.AdvanceSeconds(humanSession.NextEventDue-humanSession.Seconds,1);}
        if(command=="select")humanSelection=value;
        if(command=="another"||command=="move")
        {
            if(world.Prehistory.Started)throw new InvalidOperationException("Starting placement is locked after simulation begins.");
            int origin=command=="another"?humans.ChooseOrigin(++humanVariant):humanSelection<0?-1:humans.CellPatch[humanSelection];
            if(origin<0)throw new ArgumentException("Select a habitable land region first.");var ceiling=world.Prehistory.History?.Modern?.TechnologyCeiling;world.Prehistory=humans.Prepare(origin);if(ceiling.HasValue)world.Prehistory=humans.SetTechnologyCeiling(world.Prehistory,ceiling.Value);
        }
        var state=world.Prehistory;
        if(clock&&PrehistoryState.SameDomain(state,previous))return WorldJson.Write(new HumanSummary{ClockOnly=true,Years=state.Years,Started=state.Started,StepYears=Prehistory.YearsPerStep(state)});
        var patch=humans.Patches[state.Origin];var p=world.Surface.Cells[patch.Cells[patch.Cells.Length/2]].Position;
        var subject=HistoryAtlas.Selection(state,humanSelection,historyOverlay);
        bool political=historyOverlay==HistoryOverlay.States||historyOverlay==HistoryOverlay.Civilizations||historyOverlay==HistoryOverlay.Territories;
        return WorldJson.Write(new HumanSummary{Borders=political?historyBorders.Segments(world,state,historyOverlay):Array.Empty<float>(),Profiles=HistoryAtlas.ProfilesAt(state,humanSelection),Subject=subject,SelectionSummary=HistoryAtlas.Summary(state,subject),Legend=SocietyView.Legend(state,historyOverlay,engine:humans),Culture=SocietyView.Culture(state,humanSelection),Cultures=state.Society?.Cultures??Array.Empty<CultureIdentity>(),Events=SocietyView.Headlines(state),Population=SocietyView.Population(state),ManualStep=state.Society?.ManualStep??false,Marks=SocietyMarks(state),StepYears=Prehistory.YearsPerStep(state),OriginLon=Math.Atan2(p.Y,p.X)*180/Math.PI,OriginLat=Math.Asin(p.Z)*180/Math.PI,Status=PrehistoryView.Status(humans,state),Inspection=PrehistoryView.Describe(humans,state,humanSelection,historyOverlay),Started=state.Started,Years=state.Years,CanMove=humanSelection>=0&&humans.CellPatch[humanSelection]>=0&&humans.Patches[humans.CellPatch[humanSelection]].Capacity>=25});
    }
    public sealed class HumanSummary{public int Era=WorldEraView.Of(world.Prehistory);public int TechnologyCeiling=world.Prehistory.History?.Modern==null?-1:(int)world.Prehistory.History.Modern.TechnologyCeiling;public bool CanUpgradeHistory=Prehistory.CanUpgradeHistory(world.Prehistory);public int EpochYear=SimulationClockView.EpochYear(world.Prehistory);public long Seconds=SimulationTime.Seconds(world.Prehistory);public int PhaseStepYears=SocietyState.DefaultStep(world.Prehistory.Society?.Phase??SocietyStage.Foragers);public bool ClockOnly;public float[] Borders=Array.Empty<float>();public HistoryLegendEntry[] Legend;public string Status="",Inspection="",SelectionSummary="";public bool Started,CanMove,ManualStep;public int Years,StepYears;public double OriginLon,OriginLat;public long Population;public CultureIdentity Culture;public CultureIdentity[] Cultures;public ChronicleEvent[] Events;public SocietyMark[] Marks;public AtlasLink Subject;public AtlasLink[] Profiles=Array.Empty<AtlasLink>();}
    public sealed class CalendarSummary{public long Seconds,NextEventSeconds;public string RulesVersion="";}
    public sealed class SocietyMark{public string Name="";public double X,Y,Z;public bool City;}
    static SocietyMark[] SocietyMarks(PrehistoryState s){var marks=new List<SocietyMark>();if(s.Society!=null){foreach(var c in SocietyView.MapLabels(world,s,historyOverlay,humans)){var p=SocietyView.Location(world,c.LabelCell>=0?c.LabelCell:c.Cell);marks.Add(new SocietyMark{Name=c.LabelName.Length>0?c.LabelName:c.Name,X=p.X,Y=p.Y,Z=p.Z,City=false});}foreach(var t in s.Society.Settlements){if(t.Population<100)continue;var p=SocietyView.Location(world,t.Cell);marks.Add(new SocietyMark{Name=t.Name,X=p.X,Y=p.Y,Z=p.Z,City=true});}}return marks.ToArray();}
    static IEnumerator<ViewData> view;
    static Layer selectedLayer;
    static IEnumerator<DisplayCache.Piece> cachedView;
    [JSExport] public static void SelectLayer(int layer) { selectedLayer=(Layer)layer; }
    [JSExport] public static byte[] Palette(int layer)
    {
        if(layer==(int)Layer.Simulation&&humanPalette!=null&&humans?.World==world)
        {var copy=(byte[])humanPalette.Clone();var codes=PrehistoryView.Codes(world,humans,world.Prehistory,humanSelection,historyOverlay);for(int i=0;i<codes.Length;i++)copy[12+i*4+3]=codes[i];SocietyView.ColorPalette(world.Prehistory,copy,12,historyOverlay);return copy;}
        bool natural=layer>=1000;var candidate=(Layer)(natural?layer-1000:layer);if(!GenerationPreview.Available(world,candidate))return Array.Empty<byte>();
        var colors=natural?NaturalSurface.Colors(world,candidate,default):GlobeView.CellValues(world,candidate).Colors;int height=(world.Surface.Cells.Length+2047)/2048;
        var bytes=new byte[12+2048*height*4];Buffer.BlockCopy(new[]{-1,layer,height},0,bytes,0,12);
        for(int i=0;i<colors.Length/3;i++){for(int j=0;j<3;j++)bytes[12+i*4+j]=(byte)Math.Clamp((int)Math.Round(colors[i*3+j]*255),0,255);bytes[12+i*4+3]=255;}
        if(candidate==Layer.Simulation){var codes=PrehistoryView.Codes(world,humans?.World==world?humans:null,world.Prehistory,humanSelection,historyOverlay);for(int i=0;i<codes.Length;i++)bytes[12+i*4+3]=codes[i];SocietyView.ColorPalette(world.Prehistory,bytes,12,historyOverlay);humanPalette=(byte[])bytes.Clone();}
        return bytes;
    }
    [JSExport] public static void BeginCachedView(string options)
    {
        var settings=WorldJson.Read<ViewOptions>(options);selectedLayer=settings.Layer;cachedView?.Dispose();
        cachedView=DisplayCache.Geometry(world,settings.Projection,default).GetEnumerator();
    }
    [JSExport] public static byte[] NextCachedView()
    {
        if(cachedView==null || !cachedView.MoveNext())return Array.Empty<byte>();
        var piece=cachedView.Current;var data=piece.Data;int p=data.Positions.Length,t=data.Triangles.Length,l=data.Lines.Length;
        int kind=piece.Kind=="surface"?0:piece.Kind=="rivers"?1:piece.Kind=="wire"?2:piece.Kind=="plates"?3:piece.Kind=="ecology"?4:piece.Kind=="natural"?5:piece.Kind=="smallfeatures"?6:int.Parse(piece.Kind.Substring(4))+10;
        var packed=new byte[36+4*(p*2+t+l*2+data.CellIds.Length+data.NaturalHeight.Length+data.BlendCellIds.Length)];int offset=0;
        void Copy(Array a){int n=Buffer.ByteLength(a);Buffer.BlockCopy(a,0,packed,offset,n);offset+=n;}
        Copy(new[]{p,t,l,data.IsMap?1:0,data.Unlit?1:0,kind,data.CellIds.Length,data.NaturalHeight.Length,data.BlendCellIds.Length});Copy(data.Positions);Copy(data.Colors);Copy(data.Triangles);Copy(data.Lines);Copy(data.LineColors);Copy(data.CellIds);Copy(data.NaturalHeight);Copy(data.BlendCellIds);
        return packed;
    }
    public static void Main() { }
    [JSExport]
    public static string Generate(string recipe)
    {
        world = new WorldPipeline().Generate(WorldJson.ReadRecipe(recipe), p => Progress(p.Stage, p.Percent), preview: (stage,w)=>Preview(WorldJson.Write(GenerationPreview.Capture(stage,w))));
        return WorldJson.Write(WorldSummary.From(world));
    }
    [JSExport] public static string LoadWorld(string json)
    {
        world = WorldJson.ReadWorld(json);
        return WorldJson.Write(WorldSummary.From(world));
    }
    [JSExport] public static string LoadWorldFile(string base64)
    {
        Progress("Opening saved world",1);
        world = WorldFile.Read(Convert.FromBase64String(base64),status:stage=>Progress(stage,stage.StartsWith("Checking historical")?75:stage.StartsWith("Preparing historical")?60:stage.StartsWith("Checking generated")?40:stage.StartsWith("Checking")?30:10));
        return WorldJson.Write(WorldSummary.From(world));
    }
    // Internal worker-to-worker presentation input. Never exposed as a saved world.
    [JSExport] public static string LoadPreview(string json){world=WorldJson.Read<World>(json);return WorldJson.Write(WorldSummary.From(world));}
    [JSExport] public static void BeginView(string options) { var settings=WorldJson.Read<ViewOptions>(options);selectedLayer=settings.Layer;view?.Dispose(); view = SurfaceMesh.Chunks(world, settings).GetEnumerator(); }
    [JSExport] public static byte[] NextView()
    {
        if (view == null || !view.MoveNext()) return Array.Empty<byte>();
        var data = view.Current; int p = data.Positions.Length, t = data.Triangles.Length, l = data.Lines.Length;
        var packed = new byte[20 + 4 * (p * 2 + t + l * 2)];
        var header = new[] { p, t, l, data.IsMap ? 1 : 0, data.Unlit ? 1 : 0 }; int offset = 0;
        void Copy(Array array) { int size = Buffer.ByteLength(array); Buffer.BlockCopy(array, 0, packed, offset, size); offset += size; }
        Copy(header); Copy(data.Positions); Copy(data.Colors); Copy(data.Triangles); Copy(data.Lines); Copy(data.LineColors);
        if (!BitConverter.IsLittleEndian) for (int i = 0; i < packed.Length; i += 4) Array.Reverse(packed, i, 4);
        return packed;
    }
    [JSExport] public static string Describe(int cell){humanSelection=cell;if(selectedLayer==Layer.Simulation){EnsureHumans();humanSelection=cell;return PrehistoryView.Describe(humans,world.Prehistory,cell,historyOverlay);}return CellInspection.Describe(world, cell,selectedLayer)+(selectedLayer==Layer.Ecoregions?"\n---DETAILS---\n"+EcologyView.Summary(world,cell)+"\n\n"+EcologyView.Describe(world,cell)+"\n---WEB---\n"+FoodWebDiagram.Svg(world,cell)+"\n---PORTRAIT---\n"+WorldJson.Write(HabitatPortrait.At(world,cell)):"");}
    [JSExport] public static string DescribePoint(double x,double y,double z) => Describe(Erosion.Nearest(world.Surface,new V(x,y,z)));
    [JSExport] public static string Export(string kind) => kind == "world" ? Convert.ToBase64String(WorldFile.Write(world))
        : kind == "heightmap" ? Convert.ToBase64String(WorldJson.Heightmap(world)) : kind == "csv" ? WorldJson.Csv(world) : WorldJson.Write(world);
    [JSImport("progress", "worldgen")]
    public static partial void Progress(string stage, int percent);
    [JSImport("preview", "worldgen")]
    public static partial void Preview(string json);
}
