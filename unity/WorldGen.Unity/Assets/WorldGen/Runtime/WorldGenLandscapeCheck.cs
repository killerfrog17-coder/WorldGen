using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    public sealed class GlacialTroughReceipt
    {
        public int Source,Target;public double HalfWidth,Relief,Strength;
        public static GlacialTroughReceipt From(GlacialValleys.Reach? reach)
        {
            if(!reach.HasValue)return null;var r=reach.Value;
            return new GlacialTroughReceipt{Source=r.Source,Target=r.Target,HalfWidth=r.HalfWidth,Relief=r.Relief,Strength=r.Strength};
        }
    }
    public sealed class FjordReceipt
    {
        public int Mouth,Ocean,Head,Sections;public double Length,HalfWidth,Depth,Relief;
        public static FjordReceipt From(FjordCoasts.Inlet? inlet)
        {if(!inlet.HasValue)return null;var f=inlet.Value;return new FjordReceipt{Mouth=f.Mouth,Ocean=f.Ocean,Head=f.Head,Sections=f.Count,Length=f.LengthM,HalfWidth=f.HalfWidth,Depth=f.Depth,Relief=f.Relief};}
    }
    // Explicit test camera held across frames while the ordinary renderer
    // completes newly visible tree art. Normal gameplay leaves this unset.
    Vector3? landscapeReviewCamera;Quaternion landscapeReviewRotation;
    public sealed class LandscapeSceneReceipt
    {
        public string Requested,Actual,HistoryHash;
        public V Position;public double Elevation,Water,Depth,PrepareMs,NearLoadSeconds;
        public int Tiles,ScatterPages,WaterRenderers;
        public int NearbyPools,Reeds,SubmergedReeds;
        public int BakedTrees,BakedStands,PendingTreeBakes;
        public int TreeQuality;public float TreeNear,TreeFar;
        public double TerrainReveal,ForestReveal,FogDistance;
        public string SampleBiome;public LandscapeControls Province;public double ForestCoverage;public double[] CoverWeights;
        public SmallLake Pond,WetlandPool;
        public VolcanicTerrain.Volcano Volcano;
        public BedrockRemnants.Outcrop Outcrop;
        public RiverscapeSpan? Fall;
        public CanyonValleys.Reach Canyon;
        public GlacialTroughReceipt Trough;
        public FjordReceipt Fjord;
        public int RiverReach=-1;
        public V RiverCenter;public double RiverWidth;
        public string Coast,Landform;
        public DuneLandmark Dune;
    }
    sealed class LandscapeReviewSite{public V Position,CoastDirection,RiverCenter;public SmallLake Pond,WetlandPool;public LocalTerrainSample Sample;public VolcanicTerrain.Volcano Volcano;public BedrockRemnants.Outcrop Outcrop;public CanyonValleys.Reach Canyon;public GlacialValleys.Reach? Trough;public FjordCoasts.Inlet? Fjord;public RiverscapeSpan? Fall;public DuneLandmark Dune;public int RiverReach=-1;public double RiverWidth;public string Coast,Landform;}
    public static IEnumerator CheckLandscape(string path,string output,string kind)
    {
        Directory.CreateDirectory(output);RecordNpcCheckPackage(output);var checks=new List<string>();
        bool encounteredError=false;
        Application.logMessageReceived+=(message,trace,type)=>{if(type==LogType.Error||type==LogType.Exception){encounteredError=true;File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("LANDSCAPE "+text);checks.Add("PASS "+text);Debug.Log("LANDSCAPE "+text);}
        AudioListener.volume=0;
        // The harness bypasses the main menu's preference loading. Use an
        // explicit real game preset, rather than obsolete120/450m field defaults.
        var arguments=Environment.GetCommandLineArgs();int qualityOption=Array.IndexOf(arguments,"--worldgen-landscape-quality");
        bool presented=arguments.Contains("--worldgen-landscape-presented");
        // Keep visual reviews paced without the driver's positive-target-FPS
        // timestamp wait. That wait stalled two visible runs outside managed
        // code; batch screenshots and uncapped gameplay benchmarks completed.
        // This harness never changes the owner's saved display settings.
        Application.targetFrameRate=presented?-1:60;QualitySettings.vSyncCount=presented?1:0;
        int windowOption=Array.IndexOf(arguments,"--worldgen-landscape-window-mode");
        if(presented&&windowOption>=0&&arguments[windowOption+1]=="Borderless")
        {Screen.SetResolution(Screen.width,Screen.height,FullScreenMode.FullScreenWindow);yield return null;}
        File.WriteAllText(Path.Combine(output,"review-window.txt"),Screen.fullScreenMode+" "+Screen.width+"x"+Screen.height+" presented="+presented);
        int sourceOption=Array.IndexOf(arguments,"--worldgen-landscape-source"),pinnedSource=sourceOption<0?-1:int.Parse(arguments[sourceOption+1]);
        TerrainVideoOptions.ResetVegetation();TerrainVideoOptions.ApplyAllPlantQuality(qualityOption>=0?int.Parse(arguments[qualityOption+1]):3);
        bool duneReview=kind=="BarchanDunes"||kind=="StarDunes";
        bool drylandReview=kind=="SandSea"||kind=="SaltFlat"||duneReview;
        bool wideReview=drylandReview||kind=="WetlandComplex";
        double reviewRange=arguments.Contains("--worldgen-cliff-review")?6000:wideReview?2000:900,reviewFootprint=wideReview?1200:600;
        if(duneReview){reviewRange=6000;reviewFootprint=kind=="StarDunes"?3200:1800;}
        TerrainVideoOptions.Current.ViewDistanceM=reviewRange;
        var read=Task.Run(()=>WorldFile.ReadFile(path));while(!read.IsCompleted)yield return null;var world=read.GetAwaiter().GetResult();string history=world.Prehistory?.Hash;
        string workerPath=Path.GetFullPath(Path.Combine(Application.dataPath,"../TerrainWorker/WorldGen.Cli.exe"));
        var worker=new TerrainMeshProcess(workerPath,world,.1,Path.GetFullPath(path));
        var timer=System.Diagnostics.Stopwatch.StartNew();var setup=Task.Run(()=>{worker.Prepare(default);return LocalTerrainDerivationCache.Create(world,horizontalScale:.1);});
        while(!setup.IsCompleted)yield return null;var model=setup.GetAwaiter().GetResult();double preparation=timer.Elapsed.TotalMilliseconds;
        var choose=Task.Run(()=>
        {
            var cells=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>!world.Hydrology.OceanConnected[i]&&world.ElevationM[i]>5);
            if(arguments.Contains("--worldgen-bark-review"))cells=cells.Where(i=>model.Profile(i).CoarseReliefM<180);
            if(duneReview)
            {
                var dune=DuneLandmarks.Find(world,model,kind=="StarDunes",source:pinnedSource);
                if(dune==null)throw new InvalidOperationException("No actual suitable "+kind+" field found");
                return new LandscapeReviewSite{Position=dune.Position,CoastDirection=dune.Downwind,Dune=dune,Sample=model.Patch(dune.Position,3000).Sample(dune.Position),Landform=kind};
            }
            else if(kind=="Mangrove")
            {
                foreach(var coast in TidalForestSites.Nearshore(world,model))
                {
                    var sample=model.Patch(coast.Position,5000).Sample(coast.Position);
                    return new LandscapeReviewSite{Position=coast.Position,CoastDirection=coast.Seaward,Sample=sample,Coast=kind,Landform=kind};
                }
            }
            else if(kind=="SaltMarsh")
            {
                // Review actual intertidal ground. The generic inland finder
                // deliberately rejects ground <=5m and cannot inspect this.
                foreach(var coast in TidalMarshSites.Nearshore(world,model,pinnedSource))
                {
                    var sample=model.Patch(coast.Position,5000).Sample(coast.Position);
                    return new LandscapeReviewSite{Position=coast.Position,Sample=sample,CoastDirection=coast.Seaward,Landform="SaltMarsh"};
                }
            }
            else if(kind=="Fjord"||kind=="FjordMouth"||kind=="FjordWoodland")
            {
                var inlets=model.Amplification.Fjords.Inlets.Where(f=>pinnedSource<0||f.Head==pinnedSource).OrderByDescending(f=>kind=="FjordWoodland"?model.Profile(f.Mouth).Forest.Coverage/(1+f.Relief/900):f.LengthM*f.Relief);
                foreach(var inlet in inlets)
                {
                    int sectionIndex=inlet.First+(kind=="FjordMouth"?inlet.Count*4/5:inlet.Count/2);
                    var section=model.Amplification.Fjords.Sections[sectionIndex];V center=(section.A+section.B).Unit,side=V.Cross(center,section.B-section.A).Unit;
                    var sample=model.Patch(center,30000).Sample(center);if(!sample.Underwater||sample.WaterKind!=LocalWaterKind.Ocean)continue;
                    return new LandscapeReviewSite{Position=center,CoastDirection=side,Sample=sample,Fjord=inlet,Landform=kind};
                }
            }
            else if(kind=="GlacialTrough")
            {
                foreach(var reach in model.Amplification.Glaciers.Reaches.Where(r=>pinnedSource<0||r.Source==pinnedSource).OrderByDescending(r=>r.Strength*r.Relief).Take(80))
                {
                    var spans=model.Amplification.RiverReach(reach.Source);if(spans.Length<4)continue;
                    var middle=spans[spans.Length/2];V center=(middle.A+middle.B).Unit,coarse=(reach.A+reach.B).Unit,side=V.Cross(coarse,reach.B-reach.A).Unit;
                    var patch=model.Patch(center,60000);var sample=patch.Sample(center);
                    var wall=patch.Sample((coarse+side*(reach.HalfWidth*.6/(model.RadiusM*.1))).Unit);
                    var rim=patch.Sample((coarse+side*(reach.HalfWidth*1.1/(model.RadiusM*.1))).Unit);
                    if(sample.Biome==Biome.Ice||wall.Province.GlacialExposure<.03||rim.ElevationM-sample.ElevationM<80)continue;
                    return new LandscapeReviewSite{Position=center,CoastDirection=side,Sample=sample,Trough=reach,RiverReach=reach.Source,RiverWidth=middle.WidthA*2,Landform=kind};
                }
            }
            else if(kind=="Canyon")
            {
                foreach(var reach in model.Amplification.Canyons.Reaches.Where(r=>r.HalfWidth<1800&&(pinnedSource<0||r.Source==pinnedSource)).OrderByDescending(r=>r.Strength*r.Relief).Take(80))
                {
                    var spans=model.Amplification.RiverReach(reach.Source);if(spans.Length<4)continue;
                    var middle=spans[spans.Length/2];V center=(middle.A+middle.B).Unit,side=(middle.SideA+middle.SideB).Unit;
                    var patch=model.Patch(center,80000);var sample=patch.Sample(center);
                    var rim=patch.Sample((center+side*(reach.HalfWidth*1.05/(model.RadiusM*.1))).Unit);
                    V coarse=(reach.A+reach.B).Unit,coarseSide=V.Cross(coarse,reach.B-reach.A).Unit;
                    var wall=patch.Sample((coarse+coarseSide*(reach.HalfWidth*.45/(model.RadiusM*.1))).Unit);
                    if(rim.ElevationM-sample.ElevationM<100||wall.Province.CanyonExposure<.05)continue;
                    return new LandscapeReviewSite{Position=center,CoastDirection=side,Sample=sample,Canyon=reach,RiverReach=reach.Source,RiverWidth=middle.WidthA*2,Landform=kind};
                }
            }
            else if(kind=="Waterfall")
            {
                foreach(var channel in model.Hydrology.Channels.Where(c=>(pinnedSource<0||c.From==pinnedSource)&&WaterfallProfiles.CanHost(c.From,world.Recipe.Seed)&&c.WidthM>4&&c.WidthM<600&&(c.StartM-c.EndM)/Math.Max(1,c.LengthM)>.02)
                    .GroupBy(c=>c.From).Select(g=>g.First()).OrderBy(c=>Rng.Mix(unchecked((uint)c.From^0x45bca1u))).Take(128))
                {
                    if(world.Ecology.Habitat[channel.From].WarmTenthsC<100)continue;
                    foreach(var fall in model.Amplification.RiverReach(channel.From).Where(s=>s.Primitive==RiverFlowPrimitive.Waterfall))
                    foreach(int side in new[]{-1,1})
                    {
                        V bank=(fall.B+fall.SideB*(side*(fall.WidthB*2+10)/(model.RadiusM*.1))).Unit;
                        var sample=model.Patch(bank,30000).Sample(bank);if(sample.Underwater)continue;
                        return new LandscapeReviewSite{Position=bank,Sample=sample,Fall=fall,RiverReach=channel.From,RiverWidth=fall.WidthA*2};
                    }
                }
            }
            else if(kind=="Inselberg"||kind=="GraniteDome"||kind=="JointedDome")
            {
                foreach(var outcrop in model.Amplification.Remnants.Forms.Where(f=>f.Dome==(kind!="Inselberg")&&(kind!="JointedDome"||f.Jointed)).OrderByDescending(f=>f.HeightM))
                {
                    var sample=model.Patch(outcrop.Center,30000).Sample(outcrop.Center);
                    if(sample.Underwater||sample.Province.RemnantExposure<.9)continue;
                    return new LandscapeReviewSite{Position=outcrop.Center,Sample=sample,Landform=kind,Outcrop=outcrop};
                }
            }
            else if(kind=="TowerKarst")
            {
                bool karst=kind=="TowerKarst";
                var candidates=cells.Where(i=>world.Surface.Cells[i].Neighbors.All(n=>!world.Hydrology.OceanConnected[n]))
                    .Where(i=>karst?model.Profile(i).Landscape.Karst>.35&&model.Profile(i).Landscape.Moisture>.65&&model.Profile(i).Landscape.Glacial<.25&&world.Ecology.Habitat[i].WarmTenthsC>220:model.Profile(i).Landscape.Remnant>.35)
                    .OrderByDescending(i=>karst?model.Profile(i).Landscape.Karst:model.Profile(i).Landscape.Remnant).Take(128);
                foreach(int cell in candidates)
                {
                    V center=world.Surface.Cells[cell].Position;var patch=model.Patch(center,30000);
                    foreach(int x in new[]{-9000,-4500,0,4500,9000})foreach(int z in new[]{-9000,-4500,0,4500,9000})
                    {
                        V at=patch.Position(x,z),metres=at*(model.RadiusM*.1);var control=model.Profile(cell).Landscape;
                        double expected=ErodedTerrain.HeightOffset(metres,world.Recipe.Seed,control);
                        if(expected<(karst?80:55))continue;
                        var sample=patch.Sample(at);if(sample.Underwater||sample.ElevationM<30)continue;
                        if(sample.Province.Karst<.3)continue;
                        return new LandscapeReviewSite{Position=at,Sample=sample,Landform=kind};
                    }
                }
            }
            else if(kind=="WavePlatform")
            {
                foreach(var coast in CoastalSites.WavePlatforms(world,model))
                {
                    var sample=model.Patch(coast.Position,10000).Sample(coast.Position);
                    return new LandscapeReviewSite{Position=coast.Position,Sample=sample,Coast=kind,CoastDirection=coast.Seaward};
                }
            }
            else if(kind=="CoastalBeach"||kind=="CoastalCliffs")
            {
                bool cliffs=kind=="CoastalCliffs";
                foreach(int cell in cells.Where(i=>world.Ecology.Habitat[i].WarmTenthsC>150&&Math.Abs(world.Surface.Cells[i].Position.Z)<.85&&world.Surface.Cells[i].Neighbors.Any(n=>world.Hydrology.OceanConnected[n]))
                    .Where(i=>cliffs||model.Profile(i).Landscape.Wet<.35)
                    .OrderBy(i=>cliffs?-model.Profile(i).CoarseReliefM:model.Profile(i).CoarseReliefM).Take(160))
                {
                    int ocean=world.Surface.Cells[cell].Neighbors.First(n=>world.Hydrology.OceanConnected[n]);
                    V land=world.Surface.Cells[cell].Position,sea=world.Surface.Cells[ocean].Position;
                    var patch=model.Patch((land+sea).Unit,(land-sea).Length*model.RadiusM+1000);
                    V low=land,high=sea;
                    for(int j=0;j<20;j++){V mid=(low+high).Unit;if(patch.Sample(mid).ElevationM>0)low=mid;else high=mid;}
                    V direction=V.Tangent(low,sea).Unit;V bank=(low-direction*(cliffs?35:8)/(model.RadiusM*.1)).Unit;
                    var sample=patch.Sample(bank);
                    if(sample.Underwater||sample.WaterKind!=LocalWaterKind.Ocean||sample.ElevationM<.5||sample.ElevationM>180)continue;
                    if(!cliffs&&(sample.Ground.Gravel>.32||CoastalTerrain.ShoreMud(sample.Ground)>.3))continue;
                    if(!cliffs)
                    {
                        var inland=patch.Sample((bank-direction*(35/(model.RadiusM*.1))).Unit);
                        if(sample.ElevationM>4||inland.ElevationM-sample.ElevationM>5||sample.Ground.Sand<.65)continue;
                    }
                    return new LandscapeReviewSite{Position=bank,Sample=sample,Coast=kind,CoastDirection=direction};
                }
            }
            else if(kind=="Volcano")
            {
                foreach(var volcano in model.Amplification.Volcanoes.Forms.Where(v=>v.AgeMyr==0&&v.RadiusM<=3500).OrderByDescending(v=>v.HeightM))
                {
                    var s=model.Patch(volcano.Center,20000).Sample(volcano.Center);
                    if(s.Underwater||s.ElevationM<20)continue;
                    return new LandscapeReviewSite{Position=volcano.Center,Sample=s,Volcano=volcano};
                }
            }
            else if(kind=="RiverReach")
            {
                if(pinnedSource<0)throw new InvalidOperationException("RiverReach review requires an explicit source");
                // Inspect a specific stream, lake inlet, confluence or sea
                // outlet without relabelling it as a different landscape.
                foreach(var span in model.Amplification.RiverReach(pinnedSource))
                {
                    double half=(span.WidthA+span.WidthB)*.5;if(half<.6)continue;
                    V center=(span.A+span.B).Unit,side=(span.SideA+span.SideB).Unit;var patch=model.Patch(center,40000);
                    var water=patch.Sample(center);if(!water.Underwater||!double.IsFinite(water.WaterSurfaceM))continue;
                    foreach(double extra in new[]{3d,10,35,100,300})foreach(int sign in new[]{-1,1})
                    {
                        V bank=(center+side*(sign*(half+extra)/(model.RadiusM*.1))).Unit;var sample=patch.Sample(bank);if(sample.Underwater)continue;
                        return new LandscapeReviewSite{Position=bank,Sample=sample,RiverCenter=center,RiverReach=pinnedSource,RiverWidth=half*2,CoastDirection=side*-sign};
                    }
                }
            }
            else if(kind=="MajorRiver")
            {
                foreach(var channel in model.Hydrology.Channels.Where(c=>pinnedSource<0||c.From==pinnedSource).OrderByDescending(c=>c.WidthM).ThenBy(c=>c.From).Take(250))
                foreach(var span in model.Amplification.RiverReach(channel.From).Where(s=>s.LevelA>5&&s.LevelB>1&&Math.Min(s.WidthA,s.WidthB)>250).OrderBy(s=>Math.Abs(s.LevelA-s.LevelB)))
                {
                    V center=(span.A+span.B).Unit;var patch=model.Patch(center,40000);var water=patch.Sample(center);
                    if(!water.Underwater||water.WaterKind!=LocalWaterKind.River||water.WaterSurfaceM-water.ElevationM<10)continue;
                    double half=(span.WidthA+span.WidthB)*.5;V side=(span.SideA+span.SideB).Unit;
                    foreach(double extra in new[]{15d,75,200})foreach(int sign in new[]{-1,1})
                    {
                        V bank=(center+side*(sign*(half+extra)/(model.RadiusM*.1))).Unit;var sample=patch.Sample(bank);if(sample.Underwater)continue;
                        return new LandscapeReviewSite{Position=bank,Sample=sample,RiverCenter=center,RiverReach=channel.From,RiverWidth=half*2,CoastDirection=side*-sign};
                    }
                }
            }
            else if(kind=="AlluvialRiver")
            {
                foreach(var channel in model.Hydrology.Channels.Where(c=>(pinnedSource<0||c.From==pinnedSource)&&c.WidthM>12&&c.WidthM<180&&c.StartM>15&&c.StartM<1800&&(c.StartM-c.EndM)/Math.Max(1,c.LengthM)<.008)
                    .OrderByDescending(c=>c.WidthM).ThenBy(c=>c.From).Take(64))
                {
                    var spans=model.Amplification.RiverReach(channel.From);
                    foreach(var span in spans.Where(s=>s.Alluvium>.15&&s.Primitive==RiverFlowPrimitive.Calm)
                        .OrderByDescending(s=>Math.Abs(s.BankBendA+s.BankBendB)*s.Alluvium).Take(4))
                    {
                        double half=(span.WidthA+span.WidthB)*.5;V center=(span.A+span.B).Unit,side=(span.SideA+span.SideB).Unit;
                        int sign=span.BankBendA+span.BankBendB>=0?1:-1;
                        V bar=(center+side*(sign*half*.82/(model.RadiusM*.1))).Unit;
                        var s=model.Patch(bar,8000).Sample(bar);
                        if(s.Underwater||s.WaterKind!=LocalWaterKind.River||s.ElevationM<s.WaterSurfaceM+.04||s.WaterFootprintM<.2)continue;
                        return new LandscapeReviewSite{Position=bar,Sample=s,RiverReach=channel.From,RiverWidth=half*2,CoastDirection=side*-sign};
                    }
                }
            }
            else if(kind=="MeanderingRiver")
            {
                foreach(var channel in model.Hydrology.Channels.Where(c=>(pinnedSource<0||c.From==pinnedSource)&&c.WidthM>12&&c.WidthM<150&&c.StartM>40&&c.StartM<1600&&(c.StartM-c.EndM)/Math.Max(1,c.LengthM)<.008)
                    .OrderBy(c=>Rng.Mix(unchecked((uint)c.From^(uint)world.Recipe.Seed))).Take(160))
                {
                    var spans=model.Amplification.RiverReach(channel.From);if(spans.Length<8)continue;
                    var middle=spans[spans.Length/2];double half=(middle.WidthA+middle.WidthB)*.5;
                    V center=(middle.A+middle.B).Unit,bank=(center+middle.SideA*((half*2+6)/(model.RadiusM*.1))).Unit;
                    var s=model.Patch(bank,10000).Sample(bank);if(s.Underwater)continue;
                    double pathLength=spans.Sum(span=>(span.B-span.A).Length),chord=(spans[spans.Length-1].B-spans[0].A).Length;
                    if(pathLength/Math.Max(1e-9,chord)<1.03)continue;
                    return new LandscapeReviewSite{Position=bank,Sample=s,RiverReach=channel.From,RiverWidth=half*2};
                }
            }
            else if(kind=="WetlandComplex")
            {
                foreach(int cell in cells.Where(i=>model.Profile(i).Landscape.Wet>.8&&world.Ecology.Habitat[i].WarmTenthsC>80)
                    .OrderBy(i=>Rng.Mix(unchecked((uint)i^0x32175u))).Take(48))
                foreach(var pond in model.Amplification.SmallLakesNear(world.Surface.Cells[cell].Position,4000).Where(p=>p.Complex))
                {
                    var patch=model.Patch(pond.Center,6000);
                    for(int z=-3;z<=3;z++)for(int x=-3;x<=3;x++)
                    {
                        V at=(pond.Center+pond.East*(x*pond.Radius/5/(model.RadiusM*.1))+pond.North*(z*pond.Radius/5/(model.RadiusM*.1))).Unit;
                        var s=patch.Sample(at);if(s.Underwater&&s.WaterBodyKey==pond.Id&&s.WaterSurfaceM-s.ElevationM>.03)
                            return new LandscapeReviewSite{Position=at,Pond=pond,Sample=s};
                    }
                }
            }
            else if(kind=="pond")
            {
                foreach(int cell in cells.Where(i=>world.Ecology.Habitat[i].MeanTenthsC>50&&Math.Abs(world.Surface.Cells[i].Position.Z)<.9)
                    .OrderByDescending(i=>model.Profile(i).Landscape.Wet+model.Profile(i).Landscape.Glacial*.4).Take(96))
                {
                    var lakes=model.Amplification.SmallLakesNear(world.Surface.Cells[cell].Position,4000);
                    foreach(var pond in lakes)
                    {var s=model.Patch(pond.Center,2000).Sample(pond.Center);if(s.Underwater&&s.WaterBodyKey==pond.Id&&s.WaterSurfaceM-s.ElevationM>.9)return new LandscapeReviewSite{Position=pond.Center,Pond=pond,Sample=s};}
                }
            }
            else
            {
                // Inspection-only alias: compare warm perennial marsh with
                // the existing cold glacial marsh, without changing habitats.
                bool warmMarsh=kind=="WarmMarsh";
                bool WarmEnough(int id)=>world.Ecology.Habitat[id].ColdTenthsC>=80&&world.Ecology.Habitat[id].WarmTenthsC>=220;
                string query=warmMarsh?"FreshwaterMarsh":kind;
                bool biomeQuery=Enum.TryParse(query,out Biome requestedBiome);
                var requested=biomeQuery?default:(LandscapeRegion)Enum.Parse(typeof(LandscapeRegion),query);
                foreach(int cell in cells.Where(i=>biomeQuery?model.Profile(i).Biome==requestedBiome:LandscapeProvince.Region(model.Profile(i).Landscape,world.Surface.Cells[i].Position*(model.RadiusM*.1),world.Recipe.Seed,model.Profile(i).Biome)==requested)
                    .Where(i=>!warmMarsh||WarmEnough(i))
                    .OrderByDescending(i=>kind=="Badlands"?model.Profile(i).Landscape.Badlands:0).Take(96))
                {
                    var patch=model.Patch(world.Surface.Cells[cell].Position,18000);
                    foreach(var offset in new[]{new V(5000,0,0),new V(-6000,0,4000),new V(8500,0,-5000)})
                    {
                        var at=patch.Position(offset.X,offset.Z);var s=patch.Sample(at);
                        if(s.Underwater||s.ElevationM<=5||(biomeQuery?s.Biome!=requestedBiome:s.Landscape!=requested))continue;
                        if(!biomeQuery&&(requested==LandscapeRegion.WoodedSwamp||requested==LandscapeRegion.FreshwaterMarsh))
                        {
                            // Test the actual conjunction of woody cover and
                            // standing water, rather than another dry bank.
                            foreach(var pond in model.Amplification.SmallLakesNear(at,450).Where(p=>p.Hummocks))
                            {
                                foreach(double radius in new[]{.35,.65,.9})for(int angle=0;angle<16;angle++)
                                {
                                    double a=angle*Math.PI/8;
                                    var bank=(pond.Center+(pond.East*Math.Cos(a)+pond.North*Math.Sin(a))*(radius*pond.Radius/(model.RadiusM*.1))).Unit;
                                    var bankSample=model.Patch(bank,3000).Sample(bank);
                                    if(warmMarsh&&!WarmEnough(bankSample.Cell))continue;
                                    if(!bankSample.Underwater&&bankSample.Landscape==requested&&(requested!=LandscapeRegion.WoodedSwamp||bankSample.Forest.Coverage>.2))
                                        return new LandscapeReviewSite{Position=bank,Sample=bankSample,WetlandPool=pond,CoastDirection=(bank-pond.Center).Unit};
                                }
                            }
                            continue;
                        }
                        return new LandscapeReviewSite{Position=at,Sample=s};
                    }
                }
            }
            throw new InvalidOperationException("No suitable actual landscape context: "+kind);
        });
        while(!choose.IsCompleted)yield return null;var site=choose.GetAwaiter().GetResult();Check(site!=null,"Actual generated world supplies the requested context");
        var localHabitat=world.Ecology.Habitat[site.Sample.Cell];
        File.WriteAllText(Path.Combine(output,"review-climate.txt"),$"cell={site.Sample.Cell}; biome={localHabitat.Biome}; cold={localHabitat.ColdTenthsC*.1:R}C; warm={localHabitat.WarmTenthsC*.1:R}C; mean={localHabitat.MeanTenthsC*.1:R}C");
        File.WriteAllText(Path.Combine(output,"review-site.json"),WorldJson.Write(new LandscapeSceneReceipt{Requested=kind,Position=site.Position,Volcano=site.Volcano,Outcrop=site.Outcrop,Canyon=site.Canyon,Trough=GlacialTroughReceipt.From(site.Trough),Fjord=FjordReceipt.From(site.Fjord),Fall=site.Fall,RiverReach=site.RiverReach,RiverCenter=site.RiverCenter,RiverWidth=site.RiverWidth,Coast=site.Coast,Landform=site.Landform,Dune=site.Dune,Elevation=site.Sample.ElevationM}));
        if(site.Volcano!=null){reviewRange=Math.Max(10000,site.Volcano.RadiusM*4.5);reviewFootprint=site.Volcano.RadiusM*3.6;}
        if(site.RiverReach>=0){reviewRange=3500;reviewFootprint=1800;}
        if(site.Coast!=null){reviewRange=3000;reviewFootprint=1600;}
        if(site.Landform!=null&&!duneReview){reviewRange=3000;reviewFootprint=1600;}
        if(site.Outcrop!=null){reviewRange=6000;reviewFootprint=site.Outcrop.RadiusM*3.6;}
        if(site.Fall.HasValue){reviewRange=2500;reviewFootprint=1200;}
        if(site.Canyon!=null){reviewRange=Math.Max(6000,site.Canyon.HalfWidth*4.5);reviewFootprint=site.Canyon.HalfWidth*3.6;}
        if(site.Trough.HasValue){reviewRange=Math.Max(6000,site.Trough.Value.HalfWidth*4.5);reviewFootprint=site.Trough.Value.HalfWidth*3.6;}
        if(site.Fjord.HasValue){reviewRange=Math.Max(6000,site.Fjord.Value.HalfWidth*4.5);reviewFootprint=site.Fjord.Value.HalfWidth*3.6;}
        if(arguments.Contains("--worldgen-chunk-fast-flight"))reviewRange=Math.Max(6000,reviewRange);
        int effortOption=Array.IndexOf(arguments,"--worldgen-chunk-effort");
        if(arguments.Contains("--worldgen-chunk-loading-check")&&effortOption>=0)TerrainVideoOptions.Current.Effort=Math.Max(0,Math.Min(3,int.Parse(arguments[effortOption+1])));
        if(arguments.Contains("--worldgen-chunk-loading-check"))TerrainVideoOptions.HardChunkUpdates=arguments.Contains("--worldgen-chunk-hard-updates");
        int rangeOption=Array.IndexOf(arguments,"--worldgen-chunk-view"),terrainQualityOption=Array.IndexOf(arguments,"--worldgen-chunk-quality");
        if(rangeOption>=0)reviewRange=Math.Max(1000,Math.Min(250000,double.Parse(arguments[rangeOption+1],System.Globalization.CultureInfo.InvariantCulture)));
        if(terrainQualityOption>=0)TerrainVideoOptions.Current.Quality=Math.Max(0,Math.Min(4,int.Parse(arguments[terrainQualityOption+1])));
        TerrainVideoOptions.Current.ViewDistanceM=reviewRange;
        var play=new PlaySession{Mode=GameMode.Spectator,SolarDays=.125};play.Settings.HorizontalScale=.1;play.PrimeMeridianLongitude=Math.Atan2(site.Position.Y,site.Position.X)*180/Math.PI;
        var owner=new GameObject("Actual landscape review").AddComponent<LocalTerrainExplorer>();owner.viewDistance=reviewRange;
        owner.Initialize(Task.FromResult(model),site.Position,()=>{if(arguments.Contains("--worldgen-landscape-keep-open"))Application.Quit(0);},world,play,worker:worker);
        var bootstrapCamera=Camera.main;if(bootstrapCamera!=null&&bootstrapCamera!=owner.view)bootstrapCamera.enabled=false;
        if(arguments.Contains("--worldgen-chunk-loading-check"))
        {yield return owner.CheckChunkLoading(output,preparation);yield break;}
        timer.Restart();double deadline=Time.realtimeSinceStartupAsDouble+180;
        while((!owner.Ready||!owner.NearReady)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(owner.Ready&&owner.NearReady&&owner.Error=="","Actual worker terrain neighbourhood is uploaded without errors");double loadSeconds=timer.Elapsed.TotalSeconds;
        if(kind=="Canyon"||kind=="Waterfall")
        {
            int supportSteps=0,steepStarts=0;double minimumClearance=double.PositiveInfinity,maximumAnalyticDifference=0;
            foreach(double radius in new[]{16d,32,64})for(int direction=0;direction<8;direction++)
            {
                double angle=direction*Math.PI/4,x=Math.Cos(angle)*radius,z=Math.Sin(angle)*radius;
                double drawn=owner.VisibleGround(x,z,double.NaN);if(!double.IsFinite(drawn))continue;
                maximumAnalyticDifference=Math.Max(maximumAnalyticDifference,Math.Abs(drawn-owner.Ground(x,z)));
                double dx=owner.VisibleGround(x+.5,z,drawn)-owner.VisibleGround(x-.5,z,drawn),dz=owner.VisibleGround(x,z+.5,drawn)-owner.VisibleGround(x,z-.5,drawn);
                if(dx*dx+dz*dz>1)steepStarts++;
                var walker=new TerrainMotion();walker.Place(x,z,owner.PlayerSolidGround(x,z));
                for(int step=0;step<24;step++)
                {
                    walker.Move(1d/60,1,0,0,direction*45,0,false,false,owner.PlayerSolidGround,walk:true);
                    double surface=owner.VisibleGround(walker.X,walker.Z,double.NaN);
                    if(!double.IsFinite(surface))throw new InvalidOperationException("Native walker left resident drawn terrain");
                    minimumClearance=Math.Min(minimumClearance,walker.Y-TerrainMotion.EyeHeightM-surface);supportSteps++;
                }
                yield return null;
            }
            File.WriteAllText(Path.Combine(output,"player-ground-contact.txt"),"Native terrain movement support; steps="+supportSteps+"; steep starts="+steepStarts+"; minimum foot/drawn-triangle clearance="+minimumClearance+"m; maximum drawn/analytic difference="+maximumAnalyticDifference+"m");
            Check(supportSteps>0&&steepStarts>0&&minimumClearance>=-.025,"Terrain movement support remains above actual steep rendered terrain");
        }
        owner.TestView(3);owner.TestWeatherOverride=new WeatherFrame(22,.6,0,0,.15,.05,1800,500,Math.Max(7000,reviewRange*1.5),2,1,3,0);owner.SetDaylight(12,190000.5);
        var keys=new[]{site.Position,owner.frame.Position(-80,0),owner.frame.Position(80,0)}.Select(p=>owner.scatter.Address(p)).Distinct().ToArray();deadline=Time.realtimeSinceStartupAsDouble+90;
        while(keys.Any(k=>!owner.scatterChunks.TryGetValue(k,out var chunk)||chunk.Groups==null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(keys.All(k=>owner.scatterChunks.TryGetValue(k,out var chunk)&&chunk.Groups!=null),"Actual surrounding vegetation/scatter is resident");
        deadline=Time.realtimeSinceStartupAsDouble+300;double reportAt=0;
        while((!owner.AllReady&&owner.terrainRevealRadius<reviewFootprint||owner.StreamingFogDistance<reviewFootprint)&&Time.realtimeSinceStartupAsDouble<deadline)
        {
            if(Time.realtimeSinceStartupAsDouble>=reportAt)
            {
                reportAt=Time.realtimeSinceStartupAsDouble+5;
                string active=string.Join(" | ",owner.jobs.Select(j=>$"{j.Key.X},{j.Key.Z},L{j.Key.Level},size{j.Key.SizeM:F1},n{j.Segments}:{j.Task.Status}"));
                string state=$"t={Time.realtimeSinceStartupAsDouble:F1}s; {owner.Diagnostics}; terrain {owner.terrainRevealRadius:F1}m; forest {owner.forestRevealRadius:F1}m; fog {owner.StreamingFogDistance:F1}m; {owner.PerformanceForestState}; active {active}; {owner.Error}";
                File.WriteAllText(Path.Combine(output,"coverage.txt"),state);File.AppendAllText(Path.Combine(output,"loading-history.txt"),state+"\n");
            }
            yield return null;
        }
        Check(owner.AllReady||owner.terrainRevealRadius>=reviewFootprint,"Wide capture has an actually loaded surrounding terrain footprint");
        Check(owner.StreamingFogDistance>=reviewFootprint,"Normal terrain and vegetation reveal expose the reviewed landscape extent");
        for(int i=0;i<90;i++)yield return null;
        Check(owner.artSet!=null&&owner.artSet.Albedo!=null&&owner.artSet.Albedo.depth==17&&owner.artSet.Normals.depth==17&&owner.artSet.Masks.depth==17,"Actual player has all seventeen registered surface layers");
        if(arguments.Contains("--worldgen-cliff-review"))
        {
            Check(owner.artSet.CliffAlbedo!=null&&owner.artSet.CliffAlbedo.depth==2&&owner.artSet.CliffNormals!=null&&owner.artSet.CliffNormals.depth==2,"Actual player has both generated cliff-face materials");
            var walls=owner.tiles.Values.Where(t=>t.Materials!=null).SelectMany(t=>t.Materials).Where(m=>m.shader.name=="WorldGen/LocalTerrain"&&m.GetFloat("_Water")<.5f).ToArray();
            Check(walls.Length>0&&walls.All(m=>m.GetFloat("_CliffLayers")>.5f&&m.GetTexture("_CliffAlbedo")==owner.artSet.CliffAlbedo&&m.GetTexture("_CliffNormals")==owner.artSet.CliffNormals),"Every uploaded ground tile binds the generated cliff arrays");
        }
        Check(owner.waterMaterial.shader.name=="WorldGen/WaterSurface","Actual water review uses the production water surface shader");
        Check(owner.tiles.Values.Any(t=>t.Materials!=null&&t.Materials.Any(m=>m!=null&&m.HasProperty("_ForestFloorEnabled")&&m.GetFloat("_ForestFloorEnabled")>.5f&&m.GetTexture("_ForestFloor")!=null)),"Actual uploaded ground materials bind forest-floor weights");
        var sample=owner.walkingPatch.Sample(site.Position);Check(double.IsFinite(sample.ElevationM),"Canonical local terrain remains finite");
        if(site.Dune!=null)
        {
            Check(!sample.Underwater&&sample.Province.Sand>.2&&sample.Province.Frozen<.2,"Actual dune inspection is dry mobile sand rather than an icefield or flooded basin");
            Check(site.Dune.Weight>.55&&site.Dune.FeatureHeightM>(kind=="StarDunes"?40:5),"Reviewed dune has actual fine-scale form support and meaningful relief");
        }
        if(site.Pond!=null)
        {
            Check(sample.Underwater&&SmallWaterBodies.IsLocal(sample.WaterBodyKey)&&sample.WaterSurfaceM-sample.ElevationM>(site.Pond.Complex?.01:.9),"Pond contains actual water above its cut bed");
            Check(owner.PlayerWaterAppearance(sample).Fresh,"Actual player drinking path classifies the local pond as freshwater");
            Check(double.IsFinite(owner.SampleMotionWater(0,0).Surface),"Swimming controller sees the same pond surface");
            Check(owner.tiles.Values.Any(t=>t.Renderers!=null&&t.Renderers.Length>1),"Actual uploaded tiles contain water renderers");
        }
        if(site.WetlandPool!=null)
        {
            var hollow=model.Patch(site.WetlandPool.Center,3000).Sample(site.WetlandPool.Center);
            Check(!sample.Underwater&&hollow.Underwater&&hollow.WaterBodyKey==site.WetlandPool.Id,"Wetland review contains a dry interior hummock and its actual flooded hollow");
            Check(hollow.WaterSurfaceM==site.WetlandPool.Level&&owner.PlayerWaterAppearance(hollow).Fresh,"Flooded hollow retains one accepted fresh water head");
        }
        if(site.Fjord.HasValue)
        {
            Check(sample.Underwater&&sample.WaterKind==LocalWaterKind.Ocean&&sample.WaterSurfaceM==0,"Fjord has real sea-level water above its eroded bed");
            Check(!owner.PlayerWaterAppearance(sample).Fresh,"Actual player drinking path classifies flooded fjord as salt water");
            double seaY=owner.frame.SurfaceY(site.Position,0),swell=WaterSurface.Swell(0,0,Time.time,owner.CurrentWeather.WindEastMps,owner.CurrentWeather.WindNorthMps,-sample.ElevationM,true);
            Check(Math.Abs(owner.SampleMotionWater(0,0).Surface-seaY-swell)<.01,"Swimming controller sees the same sea datum and ordinary wave displacement in the local frame");
            var swimmer=new TerrainMotion{Water=owner.SampleMotionWater};swimmer.Place(0,0,owner.Ground(0,0));swimmer.Y=seaY+swell-.5;
            for(int i=0;i<300;i++)swimmer.Move(1d/60,0,0,0,0,0,false,false,owner.PlayerSolidGround);
            Check(swimmer.Swimming&&Math.Abs(swimmer.Y-owner.SampleMotionWater(swimmer.X,swimmer.Z).Surface-.12)<.15,"Actual movement controller floats in the deep fjord instead of falling onto its seabed");
            Check(owner.tiles.Values.Any(t=>t.Renderers!=null&&t.Renderers.Length>1),"Flooded inlet has actual uploaded ocean renderers");
        }
        var target=new RenderTexture(1536,864,24);target.Create();var prior=RenderTexture.active;owner.view.targetTexture=target;
        if(presented)
        {
            owner.view.enabled=false;
            var display=owner.gameObject.AddComponent<WorldGenLandscapePresenter>();display.View=owner.view;display.Surface=target;
        }
        double floor=site.Fjord.HasValue?0:site.Pond?.Level??sample.ElevationM;float y=(float)owner.frame.SurfaceY(site.Position,floor);
        var cameraReceipts=new List<string>();Color[] fallMotionPixels=null;
        IEnumerator Capture(string name,Vector3 offset,bool walking=false,double hour=12,bool faceCenter=false,Vector3? inspectionTarget=null)
        {
            int selection=Array.IndexOf(arguments,"--worldgen-landscape-captures");
            if(selection>=0&&!arguments[selection+1].Split(',').Contains(name))yield break;
            owner.nextDaylightAt=0;owner.SetDaylight(hour,190000+(site.Position.Z<0?0:.5));
            Check(owner.SkyFrame.Daylight>.75,"Art capture uses actual summer daylight: "+name);
            var camera=new Vector3(offset.x,y+.5f+offset.y,offset.z);
            double offsetRadius=new Vector2(offset.x,offset.z).magnitude;
            double inspectedRadius=site.Dune?.RadiusM??site.Outcrop?.RadiusM??site.Volcano?.RadiusM??site.Canyon?.HalfWidth??site.Trough?.HalfWidth??site.Fjord?.HalfWidth??Math.Min(200,reviewFootprint*.2);
            if(!arguments.Contains("--worldgen-cliff-review"))Check(Math.Min(owner.terrainRevealRadius,owner.forestRevealRadius)>offsetRadius+inspectedRadius,"Detached capture has resident coverage beyond the reviewed feature: "+name);
            var cameraPoint=owner.frame.Position(camera.x,camera.z);var cameraGround=model.Patch(cameraPoint,3000).Sample(cameraPoint);
            float cameraFloor=(float)owner.frame.SurfaceY(cameraPoint,cameraGround.ElevationM);
            if(site.Coast!=null||site.Fjord.HasValue)cameraFloor=Mathf.Max(cameraFloor,(float)owner.frame.SurfaceY(cameraPoint,0));
            if(site.Pond!=null&&cameraGround.Underwater)cameraFloor=Mathf.Max(cameraFloor,(float)owner.frame.SurfaceY(cameraPoint,cameraGround.WaterSurfaceM));
            camera.y=walking?cameraFloor+1.8f:Mathf.Max(camera.y,cameraFloor+2);
            if(!walking&&!inspectionTarget.HasValue&&offset.y>30)
                camera.y=Mathf.Max(camera.y,cameraFloor+Mathf.Min(400,offset.y*.5f));
            var look=walking?new Vector3(-offset.x,-new Vector2(offset.x,offset.z).magnitude*.10f,-offset.z):new Vector3(0,y+.5f,0)-camera;
            if(faceCenter)look=new Vector3(0,y+.5f,0)-camera;
            if(inspectionTarget.HasValue)look=inspectionTarget.Value-camera;
            if(site.RiverCenter.Length>.1)
            {
                TerrainHandoff.Coordinates(owner.frame,site.RiverCenter,out double rx,out double rz);
                var river=model.Patch(site.RiverCenter,4000).Geometry(site.RiverCenter);
                look=new Vector3((float)rx,(float)owner.frame.SurfaceY(site.RiverCenter,river.WaterSurfaceM),(float)rz)-camera;
            }
            if(site.Fall.HasValue)
            {
                var fall=site.Fall.Value;V at=(fall.A+fall.B).Unit;TerrainHandoff.Coordinates(owner.frame,at,out double fx,out double fz);
                var targetPoint=new Vector3((float)fx,(float)owner.frame.SurfaceY(at,(fall.LevelA+fall.LevelB)*.5),(float)fz);
                look=targetPoint-camera;
            }
            cameraReceipts.Add(name+" camera="+camera+" look="+look+" ground="+cameraFloor+" eyeClearance="+(camera.y-cameraFloor)+" solarHour="+hour);
            owner.landscapeReviewCamera=camera;owner.landscapeReviewRotation=Quaternion.LookRotation(look);
            if(arguments.Contains("--worldgen-cliff-review"))
            {
                // A detached camera used to inspect nearby walls through the
                // origin's distant LOD. Stream from the actual observer, just
                // as ordinary spectator movement does.
                owner.motion.X=camera.x+owner.originX;owner.motion.Z=camera.z+owner.originZ;owner.motion.Y=camera.y;
                owner.RefreshWanted();double observerDeadline=Time.realtimeSinceStartupAsDouble+120;
                double inspectedDistance=Math.Min(reviewRange*.85,Math.Max(100,look.magnitude*1.1));
                while((!owner.NearReady||owner.DetailPlanning||owner.terrainRevealRadius<inspectedDistance)&&Time.realtimeSinceStartupAsDouble<observerDeadline)yield return null;
                Check(owner.NearReady&&!owner.DetailPlanning&&owner.terrainRevealRadius>=inspectedDistance,"Actual review observer has requested terrain detail through its inspected view: "+name);
            }
            owner.UpdateCamera();owner.UpdatePlayerWater(true);
            owner.BuildScatterBatches();
            double bakedDeadline=Time.realtimeSinceStartupAsDouble+90,nextBakeReport=0;
            bool VisiblePending()=>owner.drawBatches.Any(b=>b.Model.TreeBake&&!b.Model.TreeBaked)||owner.forestBatches.Values.Any(b=>b.Model.TreeBake&&!b.Model.TreeBaked);
            while(VisiblePending()&&Time.realtimeSinceStartupAsDouble<bakedDeadline)
            {
                if(Time.realtimeSinceStartupAsDouble>=nextBakeReport){nextBakeReport=Time.realtimeSinceStartupAsDouble+2;File.WriteAllText(Path.Combine(output,"tree-readiness.txt"),name+" "+owner.PerformanceForestState);}
                yield return null;
            }
            Check(!VisiblePending(),"Visible distant-tree models complete while the review camera stays still");
            owner.nextDaylightAt=0;owner.SetDaylight(hour,190000+(site.Position.Z<0?0:.5));
            owner.SetFog();
            if(presented)
            {
                // A slow parallax pass gives the owner spatial context. Keep
                // exact final capture poses and reserve whip turns for stress
                // tests. Close ground/roof poses hold still to avoid clipping.
                bool orbit=name.StartsWith("cliff-mass-")||camera.y-cameraFloor>30;
                var focus=camera+look;double start=Time.realtimeSinceStartupAsDouble;
                while(Time.realtimeSinceStartupAsDouble-start<10)
                {
                    float t=(float)((Time.realtimeSinceStartupAsDouble-start)/10);
                    float angle=orbit?-6*(1-Mathf.SmoothStep(0,1,t)):0;
                    var scenic=focus+Quaternion.AngleAxis(angle,Vector3.up)*(camera-focus);
                    owner.landscapeReviewCamera=scenic;owner.landscapeReviewRotation=Quaternion.LookRotation(focus-scenic);
                    owner.UpdateCamera();yield return null;
                }
                owner.landscapeReviewCamera=camera;owner.landscapeReviewRotation=Quaternion.LookRotation(look);owner.UpdateCamera();
            }
            if(kind=="AlluvialRiver")Check((camera-new Vector3(0,y+.5f,0)).magnitude<owner.publishedFog*.85f,"Alluvial feature center is inside the actual camera reveal distance");
            owner.UpdatePlayerWater(true);
            if(arguments.Contains("--worldgen-cliff-review"))
            {
                var at=owner.frame.Position(camera.x+owner.originX,camera.z+owner.originZ);var physical=model.Patch(at,3000).Geometry(at);
                File.AppendAllText(Path.Combine(output,"observer-state.txt"),name+" frameR="+owner.frame.RadiusM+" frameAltitude="+owner.frame.AltitudeM+" origin="+owner.originX+","+owner.originZ+" generation="+owner.generation+" motion="+owner.motion.X+","+owner.motion.Y+","+owner.motion.Z+" camera="+owner.view.transform.position+" desired="+camera+" localground="+owner.frame.SurfaceY(at,physical.ElevationM)+" drawn="+owner.VisibleGround(camera.x+owner.originX,camera.z+owner.originZ,double.NaN)+" water="+owner.frame.SurfaceY(at,physical.WaterSurfaceM)+" fog="+owner.publishedFog+" terrainReveal="+owner.terrainRevealRadius+" forestReveal="+owner.forestRevealRadius+" underwater="+owner.waterScreen.Blend+" tiles="+owner.tiles.Count+" active="+owner.tiles.Values.Count(t=>t.Root.activeInHierarchy)+" cull="+owner.view.cullingMask+"\n");
            }
            owner.BuildScatterBatches();owner.DrawScatter();owner.DrawGroundCover();owner.view.Render();RenderTexture.active=target;
            var image=new Texture2D(1536,864,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1536,864),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            if(name=="cliff-wall-near"&&arguments.Contains("--worldgen-cliff-review"))
            {
                var walls=owner.tiles.Values.Where(t=>t.Materials!=null).SelectMany(t=>t.Materials).Where(m=>m.shader.name=="WorldGen/LocalTerrain"&&m.GetFloat("_Water")<.5f).ToArray();
                var actual=image.GetPixels32();
                try
                {
                    foreach(var wall in walls)wall.SetFloat("_CliffLayers",0);
                    owner.view.Render();RenderTexture.active=target;
                    var fallback=new Texture2D(1536,864,TextureFormat.RGB24,false);
                    try
                    {
                        fallback.ReadPixels(new Rect(0,0,1536,864),0,0);fallback.Apply();
                        File.WriteAllBytes(Path.Combine(output,"cliff-wall-material-off.png"),fallback.EncodeToPNG());
                        var before=fallback.GetPixels32();long delta=0;int changed=0;
                        for(int pixel=0;pixel<actual.Length;pixel++)
                        {
                            int difference=Math.Abs(actual[pixel].r-before[pixel].r)+Math.Abs(actual[pixel].g-before[pixel].g)+Math.Abs(actual[pixel].b-before[pixel].b);
                            delta+=difference;if(difference>3)changed++;
                        }
                        File.WriteAllText(Path.Combine(output,"cliff-material-difference.txt"),"Same-camera material-only RGB mean absolute difference="+(delta/(actual.Length*3d))+"; changed pixels="+changed+"/"+actual.Length);
                        Check(changed>actual.Length*.05,"Generated wall material visibly affects actual terrain, independently of the separate ledge meshes");
                    }
                    finally{Destroy(fallback);}
                }
                finally{foreach(var wall in walls)wall.SetFloat("_CliffLayers",1);RenderTexture.active=prior;}
            }
            if(name=="fall-base"||name=="fall-base-later")
            {
                var pixels=new Color[25];for(int z=0;z<5;z++)for(int x=0;x<5;x++)pixels[z*5+x]=image.GetPixel(766+x,430+z);
                if(fallMotionPixels!=null)Check(pixels.Where((c,i)=>Mathf.Abs(c.r-fallMotionPixels[i].r)+Mathf.Abs(c.g-fallMotionPixels[i].g)+Mathf.Abs(c.b-fallMotionPixels[i].b)>.01f).Count()>10,"Falling water visibly changes in a fixed central sheet sample across actual frames");
                fallMotionPixels=pixels;
            }
            Destroy(image);RenderTexture.active=prior;
            if(name=="valley-axis"&&arguments.Contains("--worldgen-water-shading-review"))
            {
                var waterMaterials=owner.tiles.Values.Where(t=>t.Materials!=null).SelectMany(t=>t.Materials).Where(m=>m!=null&&m.shader.name=="WorldGen/WaterSurface").Distinct().ToArray();
                Check(waterMaterials.Length>0,"Water shading review finds actual uploaded water materials");
                string[] modes={"geometry-normal","ripple-normal","depth","flow"};
                try
                {
                    foreach(var material in waterMaterials)material.EnableKeyword("WORLDGEN_WATER_REVIEW");
                    for(int mode=0;mode<modes.Length;mode++)
                    {
                        foreach(var material in waterMaterials)material.SetFloat("_WaterReview",mode+1);
                        owner.view.Render();RenderTexture.active=target;
                        var diagnostic=new Texture2D(1536,864,TextureFormat.RGB24,false);diagnostic.ReadPixels(new Rect(0,0,1536,864),0,0);diagnostic.Apply();
                        File.WriteAllBytes(Path.Combine(output,"water-"+modes[mode]+".png"),diagnostic.EncodeToPNG());Destroy(diagnostic);RenderTexture.active=prior;
                    }
                }
                finally{foreach(var material in waterMaterials){material.SetFloat("_WaterReview",0);material.DisableKeyword("WORLDGEN_WATER_REVIEW");}}
            }
            if(name=="wide"&&site.Landform=="TowerKarst")
            {
                // Diagnose silhouettes against the ACTUAL resident triangles,
                // separately from their atmospheric contrast in the normal shot.
                int grounded=0,missing=0;double maximum=0;var levels=new SortedDictionary<int,int>();
                foreach(var field in owner.forestFields.Values)if(field.Ready)
                foreach(var pair in field.Roots)foreach(int slot in pair.Value)
                {
                    var root=pair.Key.Data[slot];var p=root.Position-new Vector3((float)owner.originX,0,(float)owner.originZ);
                    if((p-camera).magnitude>owner.landMaterial.GetFloat("_FogDistance"))continue;
                    double actual=owner.VisibleGround(root.Position.x,root.Position.z,double.NaN,out int level);
                    if(double.IsNaN(actual)){missing++;continue;}
                    grounded++;maximum=Math.Max(maximum,Math.Abs(root.Position.y+.045-actual));
                    levels.TryGetValue(level,out int count);levels[level]=count+1;
                }
                File.WriteAllText(Path.Combine(output,"forest-ground.txt"),"Resident roots "+grounded+"; absent resident ground "+missing+"; maximum root/triangle error "+maximum+"m; levels "+string.Join(", ",levels.Select(p=>p.Key+":"+p.Value)));
                Check(grounded>0&&missing==0&&maximum<.2,"Reviewed forest roots meet resident rendered triangles");
                float fog=owner.landMaterial.GetFloat("_FogDistance");
                owner.landMaterial.SetFloat("_FogDistance",10000);owner.PublishTerrainScene();
                owner.DrawScatter();owner.DrawGroundCover();owner.view.Render();RenderTexture.active=target;
                var clear=new Texture2D(1536,864,TextureFormat.RGB24,false);clear.ReadPixels(new Rect(0,0,1536,864),0,0);clear.Apply();
                File.WriteAllBytes(Path.Combine(output,"diagnostic-wide-long-visibility.png"),clear.EncodeToPNG());Destroy(clear);RenderTexture.active=prior;
                owner.landMaterial.SetFloat("_FogDistance",fog);owner.PublishTerrainScene();
            }
            if(Environment.GetCommandLineArgs().Contains("--worldgen-grass-review"))
            {
                foreach(bool clumps in new[]{false,true})
                {
                    TerrainVideoOptions.GrassClumps=clumps;
                    owner.DrawScatter();owner.DrawGroundCover();owner.view.Render();RenderTexture.active=target;
                    var comparison=new Texture2D(1536,864,TextureFormat.RGB24,false);comparison.ReadPixels(new Rect(0,0,1536,864),0,0);comparison.Apply();
                    File.WriteAllBytes(Path.Combine(output,name+(clumps?"-clumps.png":"-blades.png")),comparison.EncodeToPNG());Destroy(comparison);RenderTexture.active=prior;
                }
                Check(owner.groundClumpMaterial!=null,"Textured clump shader is supported and loaded");
                if(name=="ground")
                {
                    var flags=TerrainDiagnostics.Flags;
                    TerrainDiagnostics.Flags=TerrainDiagnosticFlags.Unlit|TerrainDiagnosticFlags.NoGrassMat|TerrainDiagnosticFlags.NoMoss|TerrainDiagnosticFlags.NoLichen|TerrainDiagnosticFlags.NoAir;
                    TerrainDiagnostics.Changed();owner.ApplyTerrainDiagnostics();
                    owner.DrawScatter();owner.DrawGroundCover();owner.view.Render();RenderTexture.active=target;
                    var bare=new Texture2D(1536,864,TextureFormat.RGB24,false);bare.ReadPixels(new Rect(0,0,1536,864),0,0);bare.Apply();
                    File.WriteAllBytes(Path.Combine(output,"ground-material-unlit.png"),bare.EncodeToPNG());Destroy(bare);RenderTexture.active=prior;
                    TerrainDiagnostics.Flags=flags;TerrainDiagnostics.Changed();owner.ApplyTerrainDiagnostics();
                }
            }
            if(Environment.GetCommandLineArgs().Contains("--worldgen-tree-alpha-review"))
            {
                foreach(float cutoff in new[]{.4f,.30f,.22f})
                {
                    foreach(var material in owner.ownedScatterMaterials)if(material!=null&&material.HasProperty("_TreeViewCutoff"))material.SetFloat("_TreeViewCutoff",cutoff);
                    owner.DrawScatter();owner.DrawGroundCover();owner.view.Render();RenderTexture.active=target;
                    var comparison=new Texture2D(1536,864,TextureFormat.RGB24,false);comparison.ReadPixels(new Rect(0,0,1536,864),0,0);comparison.Apply();
                    File.WriteAllBytes(Path.Combine(output,name+"-coverage-"+Mathf.RoundToInt(cutoff*100)+".png"),comparison.EncodeToPNG());Destroy(comparison);RenderTexture.active=prior;
                }
                foreach(var material in owner.ownedScatterMaterials)if(material!=null&&material.HasProperty("_TreeViewCutoff"))LocalTreeTextures.BindViews(material);
            }
            if(arguments.Contains("--worldgen-tree-shadow-review"))
            {
                foreach(float trunkRange in new[]{180,45})
                {
                    WorldGenPerformance.TreeTrunkShadowRange=trunkRange;owner.BuildScatterBatches();
                    owner.DrawScatter();owner.DrawGroundCover();owner.view.Render();RenderTexture.active=target;
                    var comparison=new Texture2D(1536,864,TextureFormat.RGB24,false);comparison.ReadPixels(new Rect(0,0,1536,864),0,0);comparison.Apply();
                    File.WriteAllBytes(Path.Combine(output,name+"-trunks-"+(int)trunkRange+".png"),comparison.EncodeToPNG());Destroy(comparison);RenderTexture.active=prior;
                    checks.Add(owner.PerformanceVerifyTrunkBudget());
                }
                WorldGenPerformance.TreeTrunkShadowRange=0;
            }
        }
        float width=(float)(site.Pond?.Radius??site.Volcano?.RadiusM??site.Outcrop?.RadiusM??site.Canyon?.HalfWidth??site.Trough?.HalfWidth??site.Fjord?.HalfWidth??(site.Fall.HasValue?Math.Min(350,Math.Max(80,(site.Fall.Value.LevelA-site.Fall.Value.LevelB)*1.6+site.RiverWidth*2)):site.Landform!=null?300:site.Coast!=null?220:site.RiverReach>=0?Math.Min(800,Math.Max(300,site.RiverWidth*14)):kind=="SandSea"?180:70));
        if(site.Dune!=null)width=(float)site.Dune.RadiusM;
        if(kind=="AlluvialRiver")width=(float)Math.Min(350,Math.Max(160,site.RiverWidth*3));
        Quaternion orientation=Quaternion.identity;
        if(site.WetlandPool!=null)orientation=Quaternion.LookRotation(new Vector3((float)V.Dot(site.CoastDirection,owner.frame.East),0,(float)V.Dot(site.CoastDirection,owner.frame.North)));
        if(kind=="AlluvialRiver"||kind=="MajorRiver"||kind=="RiverReach")orientation=Quaternion.LookRotation(new Vector3((float)V.Dot(site.CoastDirection,owner.frame.East),0,(float)V.Dot(site.CoastDirection,owner.frame.North)));
        if(site.Coast!=null)orientation=Quaternion.LookRotation(new Vector3((float)V.Dot(site.CoastDirection,owner.frame.East),0,(float)V.Dot(site.CoastDirection,owner.frame.North)));
        if(site.Dune!=null)orientation=Quaternion.LookRotation(new Vector3((float)V.Dot(site.CoastDirection,owner.frame.East),0,(float)V.Dot(site.CoastDirection,owner.frame.North)));
        if(site.Outcrop!=null)orientation=Quaternion.LookRotation(new Vector3((float)V.Dot(site.Outcrop.Side,owner.frame.East),0,(float)V.Dot(site.Outcrop.Side,owner.frame.North)));
        if(site.Canyon!=null||site.Trough.HasValue||site.Fjord.HasValue)
        {
            V side=site.CoastDirection;
            orientation=Quaternion.LookRotation(new Vector3((float)V.Dot(side,owner.frame.East),0,(float)V.Dot(side,owner.frame.North)));
        }
        if(kind=="SandSea")
        {
            var first=new V(.73,.41,.54);double alignment=site.Position.X*first.X+site.Position.Y*first.Y+site.Position.Z*first.Z;
            var direction=1-alignment*alignment<.4?new V(.13,.85,-.51):first;
            orientation=Quaternion.LookRotation(new Vector3((float)V.Dot(direction,owner.frame.East),0,(float)V.Dot(direction,owner.frame.North)));
        }
        var wideOffset=site.Fjord.HasValue?new Vector3(width*.12f,Mathf.Max(width*1.2f,(float)site.Fjord.Value.Relief),width*.5f):new Vector3(width*.3f,width*.6f,width*1.5f);
        yield return owner.StartCoroutine(Capture("wide",orientation*wideOffset));
        yield return owner.StartCoroutine(Capture("ground",orientation*new Vector3(0,0,(float)(kind=="AlluvialRiver"?-6:site.Canyon!=null||site.Trough.HasValue?site.RiverWidth*.8+8:10)),true));
        if(site.Pond!=null)yield return owner.StartCoroutine(Capture("pond-waterline",orientation*new Vector3(0,0,width*1.15f),true,faceCenter:true));
        if(site.Dune!=null)
        {
            yield return owner.StartCoroutine(Capture("dune-face",orientation*new Vector3(0,0,width*.65f),true,faceCenter:true));
            yield return owner.StartCoroutine(Capture("dune-raking",orientation*new Vector3(width*.3f,width*.65f,width*1.4f),hour:9.5,faceCenter:true));
        }
        if(site.WetlandPool!=null)yield return owner.StartCoroutine(Capture("wetland-waterline",orientation*new Vector3(0,0,4),true));
        if(kind=="Mangrove")
        {
            var actual=owner.scatterChunks.Values.Where(c=>c.Source?.Instances!=null).SelectMany(c=>c.Source.Instances)
                .Where(t=>t.Kind==ScatterKind.GeneratedTree&&TreeFlora.TidalCode(t.Variant)&&(t.Position-site.Position).Length*model.RadiusM*.1<70)
                .GroupBy(t=>TreeFlora.RootForm(t.Variant)).Select(g=>g.OrderBy(t=>(t.Position-site.Position).Length).First()).ToArray();
            Check(actual.Length>=2,"Actual resident tidal forest contains multiple inherited aerial root forms");
            foreach(var tree in actual)
            {
                TerrainHandoff.Coordinates(owner.frame,tree.Position,out double tx,out double tz);
                var center=new Vector3((float)tx,(float)owner.frame.SurfaceY(tree.Position,tree.GroundM),(float)tz);
                var toward=RenderSettings.sun!=null?-RenderSettings.sun.transform.forward:Vector3.back;toward.y=0;toward.Normalize();if(toward.sqrMagnitude<.5f)toward=Vector3.back;
                var offset=center+toward*Mathf.Max(2.2f,(float)tree.HeightM*.20f);offset.y=0;
                var mesh=LocalScatterMeshes.Build(ScatterKind.GeneratedTree,0,tree.Variant);
                Check(mesh.subMeshCount==1&&mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"Aerial roots share the existing finite single-mesh tree architecture");
                File.AppendAllText(Path.Combine(output,"mangrove-models.txt"),TreeFlora.RootForm(tree.Variant)+" code="+tree.Variant+" vertices="+mesh.vertexCount+" triangles="+mesh.triangles.Length/3+" height="+tree.HeightM+" ground="+tree.GroundM+"\n");Destroy(mesh);
                yield return owner.StartCoroutine(Capture("roots-"+TreeFlora.RootForm(tree.Variant).ToString().ToLowerInvariant(),offset,true,inspectionTarget:center+Vector3.up*Mathf.Min(1.4f,(float)tree.HeightM*.12f)));
            }
            var flooded=owner.scatterChunks.Values.Where(c=>c.Source?.Instances!=null).SelectMany(c=>c.Source.Instances)
                .Where(t=>t.Kind==ScatterKind.GeneratedTree&&TreeFlora.TidalCode(t.Variant)&&TreeFlora.RootForm(t.Variant)==TreeRootForm.Prop&&t.GroundM<-.15&&t.GroundM>-.9&&(t.Position-site.Position).Length*model.RadiusM*.1<500)
                .OrderBy(t=>(t.Position-site.Position).Length).ToArray();
            Check(flooded.Length>0,"Actual resident prop-root tree grows in shallow coastal water");
            var waterTree=flooded[0];var waterRoot=model.Patch(waterTree.Position,3000).Sample(waterTree.Position);
            Check(waterRoot.Underwater&&waterRoot.WaterKind==LocalWaterKind.Ocean&&waterRoot.WaterSurfaceM==0,"Flooded mangrove shares the real salt-water surface");
            TerrainHandoff.Coordinates(owner.frame,waterTree.Position,out double wx,out double wz);
            var waterCenter=new Vector3((float)wx,(float)owner.frame.SurfaceY(waterTree.Position,0),(float)wz);
            var shoreward=orientation*Vector3.forward;var waterOffset=waterCenter+shoreward*4;waterOffset.y=0;
            yield return owner.StartCoroutine(Capture("tidal-waterline",waterOffset,true,inspectionTarget:waterCenter+Vector3.up*.7f));
        }
        if(arguments.Contains("--worldgen-bark-review"))
        {
            var roots=owner.forestFields.Values.Where(f=>f.Ready).SelectMany(f=>f.Roots.SelectMany(pair=>pair.Value.Select(slot=>pair.Key.Data[slot])))
                .Where(r=>(r.Field>>16)==0&&r.Height>6&&r.Height<45&&r.SlopeX*r.SlopeX+r.SlopeZ*r.SlopeZ<.04f)
                .OrderBy(r=>r.Position.x*r.Position.x+r.Position.z*r.Position.z).Take(3).ToArray();
            Check(roots.Length>0,"Bark review finds actual resident individual tree roots");
            for(int i=0;i<roots.Length;i++)
            {
                var tree=roots[i];var center=tree.Position-new Vector3((float)owner.originX,0,(float)owner.originZ);
                var toward=RenderSettings.sun!=null?-RenderSettings.sun.transform.forward:Vector3.back;toward.y=0;toward.Normalize();if(toward.sqrMagnitude<.5f)toward=Vector3.back;
                var offset=center+toward*Mathf.Max(1.6f,tree.Height*.05f+1);offset.y=0;
                yield return owner.StartCoroutine(Capture("bark-forest-"+i,offset,true,inspectionTarget:center+Vector3.up*1.6f));
            }
        }
        if(site.Volcano!=null)
        {
            yield return owner.StartCoroutine(Capture("crater-overview",new Vector3(width*.05f,width*.65f,width*.1f)));
            yield return owner.StartCoroutine(Capture("crater-rim",new Vector3((float)(site.Volcano.RadiusM*site.Volcano.CraterFraction),0,0),true,faceCenter:true));
            yield return owner.StartCoroutine(Capture("wide-other-side",new Vector3(-width*.3f,width*.6f,-width*1.5f)));
            Check(sample.Province.BareRock>.8,"Young volcanic crater has exposed geological substrate");
        }
        if(site.RiverReach>=0)
        {
            Check(owner.tiles.Values.Any(t=>t.Renderers!=null&&t.Renderers.Length>1),"Reviewed channel has actual uploaded water geometry");
            yield return owner.StartCoroutine(Capture("river-overview",new Vector3(-width*.4f,width*.7f,-width*1.2f)));
        }
        if(site.Coast!=null)
        {
            if(site.Coast=="WavePlatform")
            {
                Check(sample.Province.ShoreExposure>=.5,"Wave-cut shore uses an actual exposed coastal platform");
                var physical=LocalGeology.Surface(sample.Ground,site.Position,world.Recipe.Seed,sample.ElevationM,1,sample.OceanInfluence,0,model.RadiusM*.1,sample.Province.ExposedRock);
                Check(physical.Rock>=sample.Province.ShoreExposure-.01,"Actual platform binds exposed rock rather than generic beach sand");
            }
            Check(sample.WaterKind==LocalWaterKind.Ocean&&sample.WaterSurfaceM==0,"Coast shares the actual ocean datum");
            Check(sample.Biome!=Biome.Ocean,"Dry refined shoreline uses its adjacent land community");
            yield return owner.StartCoroutine(Capture("coast-along",orientation*new Vector3(site.Coast=="WavePlatform"?22:width,0,site.Coast=="WavePlatform"?0:-20),true));
            yield return owner.StartCoroutine(Capture("coast-seaward",orientation*new Vector3(0,width*.35f,width*1.3f)));
        }
        if(site.Landform!=null)
        {
            if(site.Canyon!=null)
                yield return owner.StartCoroutine(Capture("landform-side",new Vector3(width,(float)site.Canyon.Relief*.7f,-width*.3f)));
            else yield return owner.StartCoroutine(Capture("landform-side",new Vector3(width,0,-width*.3f),true,faceCenter:site.Outcrop!=null));
            yield return owner.StartCoroutine(Capture("landform-overview",new Vector3(-width*.5f,width*.8f,-width*1.4f)));
            yield return owner.StartCoroutine(Capture("landform-morning",new Vector3(width*.3f,width*.6f,width*1.5f),hour:9));
            if(site.Outcrop!=null)yield return owner.StartCoroutine(Capture("outcrop-base",orientation*new Vector3(0,0,(float)site.Outcrop.RadiusZ*1.16f),true,faceCenter:true));
            if(site.Outcrop!=null&&site.Outcrop.Dome)
            {
                var axis=site.Outcrop.Axis;
                var offset=new Vector3((float)V.Dot(axis,owner.frame.East),0,(float)V.Dot(axis,owner.frame.North))*(float)(-site.Outcrop.RadiusX*.85);
                yield return owner.StartCoroutine(Capture("dome-joint-face",offset,true,faceCenter:true));
            }
            if(site.Trough.HasValue)
            {
                var trough=site.Trough.Value;V coarse=(trough.A+trough.B).Unit;
                var wall=model.Patch(coarse,20000).Sample((coarse+site.CoastDirection*(trough.HalfWidth*.6/(model.RadiusM*.1))).Unit);
                Check(wall.Province.GlacialExposure>.03,"Actual glaciated trough has physically exposed valley walls");
                Check(sample.Underwater&&sample.WaterKind==LocalWaterKind.River,"Modern river occupies the shared eroded trough floor");
                yield return owner.StartCoroutine(Capture("valley-floor",orientation*new Vector3(width*.3f,0,(float)(site.RiverWidth*1.3+12)),true,faceCenter:true));
                yield return owner.StartCoroutine(Capture("valley-axis",orientation*new Vector3(width*.65f,width*.12f,0)));
            }
            if(site.Fjord.HasValue)yield return owner.StartCoroutine(Capture("fjord-axis",orientation*new Vector3(width*1.5f,width*.12f,0)));
        }
        if(arguments.Contains("--worldgen-cliff-review"))
        {
            Check(owner.ReviewCliffWall(out var wallTarget,out var wallNormal),"Actual steep exposed wall has a clear70m inspection line");
            var contextEye=wallTarget+wallNormal*70+Vector3.up*15;
            yield return owner.StartCoroutine(Capture("cliff-wall-context",new Vector3(contextEye.x,contextEye.y-y-.5f,contextEye.z),inspectionTarget:wallTarget));
            var nearEye=wallTarget+wallNormal*20+Vector3.up*5;
            yield return owner.StartCoroutine(Capture("cliff-wall-near",new Vector3(nearEye.x,nearEye.y-y-.5f,nearEye.z),inspectionTarget:wallTarget));
            var touchEye=wallTarget+wallNormal*4+Vector3.up*.6f;
            yield return owner.StartCoroutine(Capture("cliff-wall-touch",new Vector3(touchEye.x,touchEye.y-y-.5f,touchEye.z),inspectionTarget:wallTarget));
            Check(owner.scatterChunks.Values.All(chunk=>chunk.Source.Instances.All(p=>p.Kind!=ScatterKind.Outcrop||!CliffRocks.Is(p.Variant))),
                "Retired oversized cliff-scatter masses are absent from actual resident terrain");
        }
        if(site.Fall.HasValue)
        {
            yield return owner.StartCoroutine(Capture("fall-side",new Vector3(-width*.5f,width*.1f,width*.4f)));
            var fall=site.Fall.Value;V downstream=V.Tangent(fall.B,fall.B+(fall.B-fall.A)).Unit;
            V basePoint=(fall.B+downstream*(Math.Max(18,site.RiverWidth*3)/(model.RadiusM*.1))+fall.SideB*(site.RiverWidth/(model.RadiusM*.1))).Unit;
            TerrainHandoff.Coordinates(owner.frame,basePoint,out double bx,out double bz);
            float by=(float)owner.frame.SurfaceY(basePoint,fall.LevelB+2)-y-.5f;
            yield return owner.StartCoroutine(Capture("fall-base",new Vector3((float)bx,by,(float)bz)));
            yield return new WaitForSecondsRealtime(.25f);
            yield return owner.StartCoroutine(Capture("fall-base-later",new Vector3((float)bx,by,(float)bz)));
        }
        if(kind=="SandSea"||kind=="SaltFlat")
        {
            foreach(int angle in new[]{90,180,270})yield return owner.StartCoroutine(Capture("ground-"+angle,orientation*Quaternion.Euler(0,angle,0)*new Vector3(0,0,10),true));
            yield return owner.StartCoroutine(Capture("morning-wide",orientation*new Vector3(width*.3f,width*.6f,width*1.5f),hour:9));
            yield return owner.StartCoroutine(Capture("morning-ground",orientation*new Vector3(0,0,10),true,9));
            if(kind=="SandSea")
            {
                yield return owner.StartCoroutine(Capture("ridge-walk",orientation*new Vector3(180,0,180),true));
                yield return owner.StartCoroutine(Capture("morning-ridge-walk",orientation*new Vector3(180,0,180),true,9));
            }
        }
        File.WriteAllLines(Path.Combine(output,"cameras.txt"),cameraReceipts);
        int reeds=0,submergedReeds=0;var plantPatch=model.Patch(site.Position,5000);
        foreach(var chunk in owner.scatterChunks.Values)
        {
            if(chunk.Source?.Instances==null)continue;
            foreach(var plant in chunk.Source.Instances)
            {
                if(plant.Kind!=ScatterKind.MidPlant||new MidMorphology(plant.Variant).Form!=MidGrowthForm.ReedStand||(plant.Position-site.Position).Length*model.RadiusM*.1>150)continue;
                reeds++;if(plantPatch.Sample(plant.Position).Underwater)submergedReeds++;
            }
        }
        int pools=model.Amplification.SmallLakesNear(site.Position,250).Length;
        Check(world.Prehistory?.Hash==history,"Local landscape queries do not mutate regional history");
        File.WriteAllText(Path.Combine(output,"scene.json"),WorldJson.Write(new LandscapeSceneReceipt{Requested=kind,Actual=sample.Landscape.ToString(),HistoryHash=history,Position=site.Position,Elevation=sample.ElevationM,
            Water=double.IsFinite(sample.WaterSurfaceM)?sample.WaterSurfaceM:0,Depth=sample.Underwater?sample.WaterSurfaceM-sample.ElevationM:0,PrepareMs=preparation,NearLoadSeconds=loadSeconds,Tiles=owner.tiles.Count,ScatterPages=owner.scatterChunks.Count,
            WaterRenderers=owner.tiles.Values.Count(t=>t.Renderers!=null&&t.Renderers.Length>1),Pond=site.Pond,WetlandPool=site.WetlandPool,Volcano=site.Volcano,Outcrop=site.Outcrop,Canyon=site.Canyon,Trough=GlacialTroughReceipt.From(site.Trough),Fjord=FjordReceipt.From(site.Fjord),Fall=site.Fall,RiverReach=site.RiverReach,RiverCenter=site.RiverCenter,RiverWidth=site.RiverWidth,Coast=site.Coast,Landform=site.Landform,Dune=site.Dune,
            NearbyPools=pools,Reeds=reeds,SubmergedReeds=submergedReeds,SampleBiome=sample.Biome.ToString(),Province=sample.Province,ForestCoverage=sample.Forest.Coverage,
            BakedTrees=owner.BakedTreeModels,BakedStands=owner.BakedForestModels,PendingTreeBakes=owner.PendingTreeModels,
            TreeQuality=TerrainVideoOptions.TreeQuality,TreeNear=TerrainVideoOptions.TreeNearLod,TreeFar=TerrainVideoOptions.TreeFarLod,
            TerrainReveal=owner.terrainRevealRadius,ForestReveal=owner.forestRevealRadius,FogDistance=owner.StreamingFogDistance,
            CoverWeights=Enumerable.Range(0,9).Select(f=>sample.Cover.Weight(f)).ToArray()}));
        // Exercise the real asynchronous command only after all scene evidence
        // is recorded, so its teleport cannot contaminate the reviewed cameras.
        if(presented&&arguments.Contains("--worldgen-landscape-keep-open"))
        {
            Check(!encounteredError,"Interactive scenic review starts without runtime errors");
            File.WriteAllLines(Path.Combine(output,"checks.txt"),checks);
            var eye=owner.view.transform.localPosition;var angles=owner.view.transform.localRotation.eulerAngles;
            owner.motion.X=eye.x+owner.originX;owner.motion.Z=eye.z+owner.originZ;owner.motion.Y=eye.y;
            owner.heading=angles.y;owner.pitch=angles.x>180?angles.x-360:angles.x;
            owner.landscapeReviewCamera=null;owner.view.targetTexture=null;owner.view.enabled=true;
            Destroy(owner.GetComponent<WorldGenLandscapePresenter>());target.Release();Destroy(target);
            owner.RefreshWanted();owner.UpdateCamera();if(Application.isFocused)owner.CaptureMouse();
            File.WriteAllText(Path.Combine(output,"interactive-ready.txt"),"Actual spectator controls active. No performance measurement. Close this review normally before rebuilding its slot.");
            while(owner!=null)yield return null;
            yield break;
        }
        string landmarkKind=kind=="Volcano"?"volcano":kind=="Waterfall"?"waterfall":kind=="Canyon"?"canyon":kind=="GlacialTrough"?"glacial":site.Fjord.HasValue?"fjord":kind=="Mangrove"?"mangrove":kind=="Inselberg"?"outcrop":kind=="GraniteDome"||kind=="JointedDome"?"dome":kind=="TowerKarst"?"karst":null;
        if(landmarkKind!=null)
        {
            var command=owner.ExecuteExplorerCommand("/landmark "+landmarkKind);
            Check(command.Success,"Landmark command accepts "+landmarkKind);
            double searchDeadline=Time.realtimeSinceStartupAsDouble+120;int oldGeneration=owner.generation;
            while(owner.landmarkSearch!=null&&Time.realtimeSinceStartupAsDouble<searchDeadline)yield return null;
            Check(owner.landmarkSearch==null&&owner.generation>oldGeneration,"Landmark command finds and visits an actual generated example");
            var destination=model.Patch(owner.Position,20000).Sample(owner.Position);
            Check(!destination.Underwater&&double.IsFinite(owner.motion.Y)&&float.IsFinite(owner.heading)&&float.IsFinite(owner.pitch),"Landmark destination is dry with finite camera placement");
            File.WriteAllText(Path.Combine(output,"landmark.txt"),landmarkKind+" "+owner.Position+"; elevation "+destination.ElevationM+"; generation "+owner.generation);
            yield return new WaitForSecondsRealtime(3);
        }
        Check(!encounteredError,"Scene and post-teleport updates contain no runtime errors");
        File.WriteAllLines(Path.Combine(output,"checks.txt"),checks);Debug.Log("LANDSCAPE PASS "+checks.Count);owner.view.targetTexture=null;target.Release();Destroy(target);Application.Quit(0);
    }
}
