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
        public SmallLake Pond;
        public VolcanicTerrain.Volcano Volcano;
        public BedrockRemnants.Outcrop Outcrop;
        public RiverscapeSpan? Fall;
        public CanyonValleys.Reach Canyon;
        public int RiverReach=-1;
        public string Coast,Landform;
    }
    sealed class LandscapeReviewSite{public V Position,CoastDirection;public SmallLake Pond;public LocalTerrainSample Sample;public VolcanicTerrain.Volcano Volcano;public BedrockRemnants.Outcrop Outcrop;public CanyonValleys.Reach Canyon;public RiverscapeSpan? Fall;public int RiverReach=-1;public double RiverWidth;public string Coast,Landform;}
    public static IEnumerator CheckLandscape(string path,string output,string kind)
    {
        Directory.CreateDirectory(output);RecordNpcCheckPackage(output);var checks=new List<string>();
        bool encounteredError=false;
        Application.logMessageReceived+=(message,trace,type)=>{if(type==LogType.Error||type==LogType.Exception){encounteredError=true;File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("LANDSCAPE "+text);checks.Add("PASS "+text);Debug.Log("LANDSCAPE "+text);}
        Application.targetFrameRate=60;QualitySettings.vSyncCount=0;AudioListener.volume=0;
        // The harness bypasses the main menu's preference loading. Use an
        // explicit real game preset, rather than obsolete120/450m field defaults.
        var arguments=Environment.GetCommandLineArgs();int qualityOption=Array.IndexOf(arguments,"--worldgen-landscape-quality");
        TerrainVideoOptions.ResetVegetation();TerrainVideoOptions.ApplyAllPlantQuality(qualityOption>=0?int.Parse(arguments[qualityOption+1]):3);
        bool drylandReview=kind=="SandSea"||kind=="SaltFlat";
        double reviewRange=drylandReview?2000:900,reviewFootprint=drylandReview?1200:600;
        TerrainVideoOptions.Current.ViewDistanceM=reviewRange;
        var read=Task.Run(()=>WorldFile.ReadFile(path));while(!read.IsCompleted)yield return null;var world=read.GetAwaiter().GetResult();string history=world.Prehistory.Hash;
        string workerPath=Path.GetFullPath(Path.Combine(Application.dataPath,"../TerrainWorker/WorldGen.Cli.exe"));
        var worker=new TerrainMeshProcess(workerPath,world,.1,Path.GetFullPath(path));
        var timer=System.Diagnostics.Stopwatch.StartNew();var setup=Task.Run(()=>{worker.Prepare(default);return LocalTerrainDerivationCache.Create(world,horizontalScale:.1);});
        while(!setup.IsCompleted)yield return null;var model=setup.GetAwaiter().GetResult();double preparation=timer.Elapsed.TotalMilliseconds;
        var choose=Task.Run(()=>
        {
            var cells=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>!world.Hydrology.OceanConnected[i]&&world.ElevationM[i]>5);
            if(kind=="Canyon")
            {
                foreach(var reach in model.Amplification.Canyons.Reaches.Where(r=>r.HalfWidth<1800).OrderByDescending(r=>r.Strength*r.Relief).Take(80))
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
                foreach(var channel in model.Hydrology.Channels.Where(c=>WaterfallProfiles.CanHost(c.From,world.Recipe.Seed)&&c.WidthM>4&&c.WidthM<600&&(c.StartM-c.EndM)/Math.Max(1,c.LengthM)>.02)
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
            else if(kind=="AlluvialRiver")
            {
                foreach(var channel in model.Hydrology.Channels.Where(c=>c.WidthM>12&&c.WidthM<180&&c.StartM>15&&c.StartM<1800&&(c.StartM-c.EndM)/Math.Max(1,c.LengthM)<.008)
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
                foreach(var channel in model.Hydrology.Channels.Where(c=>c.WidthM>12&&c.WidthM<150&&c.StartM>40&&c.StartM<1600&&(c.StartM-c.EndM)/Math.Max(1,c.LengthM)<.008)
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
                bool biomeQuery=Enum.TryParse(kind,out Biome requestedBiome);
                var requested=biomeQuery?default:(LandscapeRegion)Enum.Parse(typeof(LandscapeRegion),kind);
                foreach(int cell in cells.Where(i=>biomeQuery?model.Profile(i).Biome==requestedBiome:LandscapeProvince.Region(model.Profile(i).Landscape,world.Surface.Cells[i].Position*(model.RadiusM*.1),world.Recipe.Seed,model.Profile(i).Biome)==requested)
                    .OrderByDescending(i=>kind=="Badlands"?model.Profile(i).Landscape.Badlands:0).Take(96))
                {
                    var patch=model.Patch(world.Surface.Cells[cell].Position,18000);
                    foreach(var offset in new[]{new V(5000,0,0),new V(-6000,0,4000),new V(8500,0,-5000)})
                    {
                        var at=patch.Position(offset.X,offset.Z);var s=patch.Sample(at);
                        if(s.Underwater||s.ElevationM<=5||(biomeQuery?s.Biome!=requestedBiome:s.Landscape!=requested))continue;
                        if(!biomeQuery&&requested==LandscapeRegion.WoodedSwamp)
                        {
                            // Test the actual conjunction of woody cover and
                            // standing water, rather than another dry bank.
                            foreach(var pond in model.Amplification.SmallLakesNear(at,450))
                            {
                                foreach(int side in new[]{-1,1})
                                {
                                    var bank=(pond.Center+pond.North*(side*pond.Radius*1.35/(model.RadiusM*.1))).Unit;
                                    var bankSample=model.Patch(bank,3000).Sample(bank);
                                    if(!bankSample.Underwater&&bankSample.Landscape==requested&&bankSample.Forest.Coverage>.2)
                                        return new LandscapeReviewSite{Position=bank,Sample=bankSample};
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
        File.WriteAllText(Path.Combine(output,"review-site.json"),WorldJson.Write(new LandscapeSceneReceipt{Requested=kind,Position=site.Position,Volcano=site.Volcano,Outcrop=site.Outcrop,Canyon=site.Canyon,Fall=site.Fall,RiverReach=site.RiverReach,Coast=site.Coast,Landform=site.Landform,Elevation=site.Sample.ElevationM}));
        if(site.Volcano!=null){reviewRange=Math.Max(10000,site.Volcano.RadiusM*4.5);reviewFootprint=site.Volcano.RadiusM*3.6;}
        if(site.RiverReach>=0){reviewRange=3500;reviewFootprint=1800;}
        if(site.Coast!=null){reviewRange=3000;reviewFootprint=1600;}
        if(site.Landform!=null){reviewRange=3000;reviewFootprint=1600;}
        if(site.Outcrop!=null){reviewRange=6000;reviewFootprint=site.Outcrop.RadiusM*3.6;}
        if(site.Fall.HasValue){reviewRange=2500;reviewFootprint=1200;}
        if(site.Canyon!=null){reviewRange=Math.Max(6000,site.Canyon.HalfWidth*4.5);reviewFootprint=site.Canyon.HalfWidth*3.6;}
        TerrainVideoOptions.Current.ViewDistanceM=reviewRange;
        var play=new PlaySession{Mode=GameMode.Spectator,SolarDays=.125};play.Settings.HorizontalScale=.1;play.PrimeMeridianLongitude=Math.Atan2(site.Position.Y,site.Position.X)*180/Math.PI;
        var owner=new GameObject("Actual landscape review").AddComponent<LocalTerrainExplorer>();owner.viewDistance=reviewRange;
        owner.Initialize(Task.FromResult(model),site.Position,()=>{},world,play,worker:worker);
        timer.Restart();double deadline=Time.realtimeSinceStartupAsDouble+180;
        while((!owner.Ready||!owner.NearReady)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(owner.Ready&&owner.NearReady&&owner.Error=="","Actual worker terrain neighbourhood is uploaded without errors");double loadSeconds=timer.Elapsed.TotalSeconds;
        owner.TestView(3);owner.TestWeatherOverride=new WeatherFrame(22,.6,0,0,.15,.05,1800,500,Math.Max(7000,reviewRange*1.5),2,1,3,0);owner.SetDaylight(12,190000.5);
        var keys=new[]{site.Position,owner.frame.Position(-80,0),owner.frame.Position(80,0)}.Select(p=>owner.scatter.Address(p)).Distinct().ToArray();deadline=Time.realtimeSinceStartupAsDouble+90;
        while(keys.Any(k=>!owner.scatterChunks.TryGetValue(k,out var chunk)||chunk.Groups==null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(keys.All(k=>owner.scatterChunks.TryGetValue(k,out var chunk)&&chunk.Groups!=null),"Actual surrounding vegetation/scatter is resident");
        deadline=Time.realtimeSinceStartupAsDouble+300;double reportAt=0;
        while((!owner.AllReady&&owner.terrainRevealRadius<reviewFootprint||owner.StreamingFogDistance<reviewFootprint)&&Time.realtimeSinceStartupAsDouble<deadline)
        {
            if(Time.realtimeSinceStartupAsDouble>=reportAt){reportAt=Time.realtimeSinceStartupAsDouble+5;File.WriteAllText(Path.Combine(output,"coverage.txt"),"Tiles "+owner.TileCount+" / "+owner.WantedCount+"; terrain "+owner.terrainRevealRadius+"m; forest "+owner.forestRevealRadius+"m; fog "+owner.StreamingFogDistance+"m; "+owner.Error);}
            yield return null;
        }
        Check(owner.AllReady||owner.terrainRevealRadius>=reviewFootprint,"Wide capture has an actually loaded surrounding terrain footprint");
        Check(owner.StreamingFogDistance>=reviewFootprint,"Normal terrain and vegetation reveal expose the reviewed landscape extent");
        for(int i=0;i<90;i++)yield return null;
        Check(owner.artSet!=null&&owner.artSet.Albedo!=null&&owner.artSet.Albedo.depth==17&&owner.artSet.Normals.depth==17&&owner.artSet.Masks.depth==17,"Actual player has all seventeen registered surface layers");
        Check(owner.waterMaterial.shader.name=="WorldGen/WaterSurface","Actual water review uses the production water surface shader");
        Check(owner.tiles.Values.Any(t=>t.Materials!=null&&t.Materials.Any(m=>m!=null&&m.HasProperty("_ForestFloorEnabled")&&m.GetFloat("_ForestFloorEnabled")>.5f&&m.GetTexture("_ForestFloor")!=null)),"Actual uploaded ground materials bind forest-floor weights");
        var sample=owner.walkingPatch.Sample(site.Position);Check(double.IsFinite(sample.ElevationM),"Canonical local terrain remains finite");
        if(site.Pond!=null)
        {
            Check(sample.Underwater&&SmallWaterBodies.IsLocal(sample.WaterBodyKey)&&sample.WaterSurfaceM-sample.ElevationM>.9,"Pond contains actual water above its cut bed");
            Check(owner.PlayerWaterAppearance(sample).Fresh,"Actual player drinking path classifies the local pond as freshwater");
            Check(double.IsFinite(owner.SampleMotionWater(0,0).Surface),"Swimming controller sees the same pond surface");
            Check(owner.tiles.Values.Any(t=>t.Renderers!=null&&t.Renderers.Length>1),"Actual uploaded tiles contain water renderers");
        }
        var target=new RenderTexture(1536,864,24);target.Create();var prior=RenderTexture.active;owner.view.targetTexture=target;
        double floor=site.Pond?.Level??sample.ElevationM;float y=(float)owner.frame.SurfaceY(site.Position,floor);
        var cameraReceipts=new List<string>();Color[] fallMotionPixels=null;
        IEnumerator Capture(string name,Vector3 offset,bool walking=false,double hour=12,bool faceCenter=false,Vector3? targetPoint=null)
        {
            owner.nextDaylightAt=0;owner.SetDaylight(hour,190000+(site.Position.Z<0?0:.5));
            Check(owner.SkyFrame.Daylight>.75,"Art capture uses actual summer daylight: "+name);
            var camera=new Vector3(offset.x,y+.5f+offset.y,offset.z);
            double offsetRadius=new Vector2(offset.x,offset.z).magnitude;
            double inspectedRadius=site.Outcrop?.RadiusM??site.Volcano?.RadiusM??site.Canyon?.HalfWidth??Math.Min(200,reviewFootprint*.2);
            Check(Math.Min(owner.terrainRevealRadius,owner.forestRevealRadius)>offsetRadius+inspectedRadius,"Detached capture has resident coverage beyond the reviewed feature: "+name);
            var cameraPoint=owner.frame.Position(camera.x,camera.z);var cameraGround=model.Patch(cameraPoint,3000).Sample(cameraPoint);
            float cameraFloor=(float)owner.frame.SurfaceY(cameraPoint,cameraGround.ElevationM);
            camera.y=walking?cameraFloor+1.8f:Mathf.Max(camera.y,cameraFloor+2);
            var look=walking?new Vector3(-offset.x,-new Vector2(offset.x,offset.z).magnitude*.10f,-offset.z):new Vector3(0,y+.5f,0)-camera;
            if(faceCenter)look=new Vector3(0,y+.5f,0)-camera;
            if(targetPoint.HasValue)look=targetPoint.Value-camera;
            if(site.Fall.HasValue)
            {
                var fall=site.Fall.Value;V at=(fall.A+fall.B).Unit;TerrainHandoff.Coordinates(owner.frame,at,out double fx,out double fz);
                var targetPoint=new Vector3((float)fx,(float)owner.frame.SurfaceY(at,(fall.LevelA+fall.LevelB)*.5),(float)fz);
                look=targetPoint-camera;
            }
            cameraReceipts.Add(name+" camera="+camera+" ground="+cameraFloor+" eyeClearance="+(camera.y-cameraFloor)+" solarHour="+hour);
            owner.landscapeReviewCamera=camera;owner.landscapeReviewRotation=Quaternion.LookRotation(look);owner.UpdateCamera();owner.UpdatePlayerWater(true);
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
            if(kind=="AlluvialRiver")Check((camera-new Vector3(0,y+.5f,0)).magnitude<owner.publishedFog*.85f,"Alluvial feature center is inside the actual camera reveal distance");
            owner.UpdatePlayerWater(true);owner.BuildScatterBatches();owner.DrawScatter();owner.DrawGroundCover();owner.view.Render();RenderTexture.active=target;
            var image=new Texture2D(1536,864,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1536,864),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            if(name=="fall-base"||name=="fall-base-later")
            {
                var pixels=new Color[25];for(int z=0;z<5;z++)for(int x=0;x<5;x++)pixels[z*5+x]=image.GetPixel(766+x,430+z);
                if(fallMotionPixels!=null)Check(pixels.Where((c,i)=>Mathf.Abs(c.r-fallMotionPixels[i].r)+Mathf.Abs(c.g-fallMotionPixels[i].g)+Mathf.Abs(c.b-fallMotionPixels[i].b)>.01f).Count()>10,"Falling water visibly changes in a fixed central sheet sample across actual frames");
                fallMotionPixels=pixels;
            }
            Destroy(image);RenderTexture.active=prior;
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
        float width=(float)(site.Pond?.Radius??site.Volcano?.RadiusM??site.Outcrop?.RadiusM??site.Canyon?.HalfWidth??(site.Fall.HasValue?Math.Min(350,Math.Max(80,(site.Fall.Value.LevelA-site.Fall.Value.LevelB)*1.6+site.RiverWidth*2)):site.Landform!=null?300:site.Coast!=null?220:site.RiverReach>=0?Math.Min(800,Math.Max(300,site.RiverWidth*14)):kind=="SandSea"?180:70));
        if(kind=="AlluvialRiver")width=(float)Math.Min(350,Math.Max(160,site.RiverWidth*3));
        Quaternion orientation=Quaternion.identity;
        if(kind=="AlluvialRiver")orientation=Quaternion.LookRotation(new Vector3((float)V.Dot(site.CoastDirection,owner.frame.East),0,(float)V.Dot(site.CoastDirection,owner.frame.North)));
        if(site.Coast!=null)orientation=Quaternion.LookRotation(new Vector3((float)V.Dot(site.CoastDirection,owner.frame.East),0,(float)V.Dot(site.CoastDirection,owner.frame.North)));
        if(site.Outcrop!=null)orientation=Quaternion.LookRotation(new Vector3((float)V.Dot(site.Outcrop.Side,owner.frame.East),0,(float)V.Dot(site.Outcrop.Side,owner.frame.North)));
        if(site.Canyon!=null)
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
        yield return owner.StartCoroutine(Capture("wide",orientation*new Vector3(width*.3f,width*.6f,width*1.5f)));
        yield return owner.StartCoroutine(Capture("ground",orientation*new Vector3(0,0,(float)(kind=="AlluvialRiver"?-6:site.Canyon!=null?site.RiverWidth*.8+8:10)),true));
        if(arguments.Contains("--worldgen-bark-review"))
        {
            var roots=owner.forestFields.Values.Where(f=>f.Ready).SelectMany(f=>f.Roots.SelectMany(pair=>pair.Value.Select(slot=>pair.Key.Data[slot])))
                .Where(r=>(r.Field>>16)==0&&r.Height>6&&r.Height<45)
                .OrderBy(r=>r.Position.x*r.Position.x+r.Position.z*r.Position.z).Take(3).ToArray();
            Check(roots.Length>0,"Bark review finds actual resident individual tree roots");
            for(int i=0;i<roots.Length;i++)
            {
                var tree=roots[i];var center=tree.Position-new Vector3((float)owner.originX,0,(float)owner.originZ);
                var toward=new Vector3(-center.x,0,-center.z).normalized;if(toward.sqrMagnitude<.5f)toward=Vector3.back;
                var offset=center+toward*Mathf.Max(1.6f,tree.Height*.05f+1);offset.y=0;
                yield return owner.StartCoroutine(Capture("bark-forest-"+i,offset,true,targetPoint:center+Vector3.up*1.6f));
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
            Check(sample.WaterKind==LocalWaterKind.Ocean&&sample.WaterSurfaceM==0,"Coast shares the actual ocean datum");
            Check(sample.Biome!=Biome.Ocean,"Dry refined shoreline uses its adjacent land community");
            yield return owner.StartCoroutine(Capture("coast-along",orientation*new Vector3(width,0,-20),true));
            yield return owner.StartCoroutine(Capture("coast-seaward",orientation*new Vector3(0,width*.35f,width*1.3f)));
        }
        if(site.Landform!=null)
        {
            yield return owner.StartCoroutine(Capture("landform-side",new Vector3(width,0,-width*.3f),true,faceCenter:site.Outcrop!=null));
            yield return owner.StartCoroutine(Capture("landform-overview",new Vector3(-width*.5f,width*.8f,-width*1.4f)));
            yield return owner.StartCoroutine(Capture("landform-morning",new Vector3(width*.3f,width*.6f,width*1.5f),hour:9));
            if(site.Outcrop!=null)yield return owner.StartCoroutine(Capture("outcrop-base",orientation*new Vector3(0,0,(float)site.Outcrop.RadiusZ*1.16f),true,faceCenter:true));
            if(site.Outcrop!=null&&site.Outcrop.Dome)
            {
                var axis=site.Outcrop.Axis;
                var offset=new Vector3((float)V.Dot(axis,owner.frame.East),0,(float)V.Dot(axis,owner.frame.North))*(float)(-site.Outcrop.RadiusX*.85);
                yield return owner.StartCoroutine(Capture("dome-joint-face",offset,true,faceCenter:true));
            }
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
        Check(world.Prehistory.Hash==history,"Local landscape queries do not mutate regional history");
        File.WriteAllText(Path.Combine(output,"scene.json"),WorldJson.Write(new LandscapeSceneReceipt{Requested=kind,Actual=sample.Landscape.ToString(),HistoryHash=history,Position=site.Position,Elevation=sample.ElevationM,
            Water=double.IsFinite(sample.WaterSurfaceM)?sample.WaterSurfaceM:0,Depth=sample.Underwater?sample.WaterSurfaceM-sample.ElevationM:0,PrepareMs=preparation,NearLoadSeconds=loadSeconds,Tiles=owner.tiles.Count,ScatterPages=owner.scatterChunks.Count,
            WaterRenderers=owner.tiles.Values.Count(t=>t.Renderers!=null&&t.Renderers.Length>1),Pond=site.Pond,Volcano=site.Volcano,Outcrop=site.Outcrop,Canyon=site.Canyon,Fall=site.Fall,RiverReach=site.RiverReach,Coast=site.Coast,Landform=site.Landform,
            NearbyPools=pools,Reeds=reeds,SubmergedReeds=submergedReeds,SampleBiome=sample.Biome.ToString(),Province=sample.Province,ForestCoverage=sample.Forest.Coverage,
            BakedTrees=owner.BakedTreeModels,BakedStands=owner.BakedForestModels,PendingTreeBakes=owner.PendingTreeModels,
            TreeQuality=TerrainVideoOptions.TreeQuality,TreeNear=TerrainVideoOptions.TreeNearLod,TreeFar=TerrainVideoOptions.TreeFarLod,
            TerrainReveal=owner.terrainRevealRadius,ForestReveal=owner.forestRevealRadius,FogDistance=owner.StreamingFogDistance,
            CoverWeights=Enumerable.Range(0,9).Select(f=>sample.Cover.Weight(f)).ToArray()}));
        // Exercise the real asynchronous command only after all scene evidence
        // is recorded, so its teleport cannot contaminate the reviewed cameras.
        string landmarkKind=kind=="Volcano"?"volcano":kind=="Waterfall"?"waterfall":kind=="Canyon"?"canyon":kind=="Inselberg"?"outcrop":kind=="GraniteDome"||kind=="JointedDome"?"dome":kind=="TowerKarst"?"karst":null;
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
