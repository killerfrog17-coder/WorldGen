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
    public sealed class LandscapeSceneReceipt
    {
        public string Requested,Actual,HistoryHash;
        public V Position;public double Elevation,Water,Depth,PrepareMs,NearLoadSeconds;
        public int Tiles,ScatterPages,WaterRenderers;
        public SmallLake Pond;
    }
    sealed class LandscapeReviewSite{public V Position;public SmallLake Pond;public LocalTerrainSample Sample;}
    public static IEnumerator CheckLandscape(string path,string output,string kind)
    {
        Directory.CreateDirectory(output);RecordNpcCheckPackage(output);var checks=new List<string>();
        Application.logMessageReceived+=(message,trace,type)=>{if(type==LogType.Error||type==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("LANDSCAPE "+text);checks.Add("PASS "+text);Debug.Log("LANDSCAPE "+text);}
        Application.targetFrameRate=60;QualitySettings.vSyncCount=0;AudioListener.volume=0;
        var read=Task.Run(()=>WorldFile.ReadFile(path));while(!read.IsCompleted)yield return null;var world=read.GetAwaiter().GetResult();string history=world.Prehistory.Hash;
        string workerPath=Path.GetFullPath(Path.Combine(Application.dataPath,"../TerrainWorker/WorldGen.Cli.exe"));
        var worker=new TerrainMeshProcess(workerPath,world,.1,Path.GetFullPath(path));
        var timer=System.Diagnostics.Stopwatch.StartNew();var setup=Task.Run(()=>{worker.Prepare(default);return LocalTerrainDerivationCache.Create(world,horizontalScale:.1);});
        while(!setup.IsCompleted)yield return null;var model=setup.GetAwaiter().GetResult();double preparation=timer.Elapsed.TotalMilliseconds;
        var choose=Task.Run(()=>
        {
            var cells=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>!world.Hydrology.OceanConnected[i]&&world.ElevationM[i]>5);
            if(kind=="pond")
            {
                foreach(int cell in cells.Where(i=>world.Ecology.Habitat[i].MeanTenthsC>50&&Math.Abs(world.Surface.Cells[i].Position.Z)<.9)
                    .OrderByDescending(i=>model.Profile(i).Landscape.Wet+model.Profile(i).Landscape.Glacial*.4).Take(96))
                {
                    var lakes=model.Amplification.SmallLakesNear(world.Surface.Cells[cell].Position,4000);
                    foreach(var pond in lakes)
                    {var s=model.Patch(pond.Center,2000).Sample(pond.Center);if(s.Underwater&&s.WaterBodyKey==pond.Id)return new LandscapeReviewSite{Position=pond.Center,Pond=pond,Sample=s};}
                }
            }
            else
            {
                var requested=(LandscapeRegion)Enum.Parse(typeof(LandscapeRegion),kind);
                foreach(int cell in cells.Where(i=>LandscapeProvince.Region(model.Profile(i).Landscape,world.Surface.Cells[i].Position*(model.RadiusM*.1),world.Recipe.Seed,model.Profile(i).Biome)==requested).Take(96))
                {
                    var patch=model.Patch(world.Surface.Cells[cell].Position,18000);
                    foreach(var offset in new[]{new V(5000,0,0),new V(-6000,0,4000),new V(8500,0,-5000)})
                    {var at=patch.Position(offset.X,offset.Z);var s=patch.Sample(at);if(!s.Underwater&&s.ElevationM>5&&s.Landscape==requested)return new LandscapeReviewSite{Position=at,Sample=s};}
                }
            }
            throw new InvalidOperationException("No suitable actual landscape context: "+kind);
        });
        while(!choose.IsCompleted)yield return null;var site=choose.GetAwaiter().GetResult();Check(site!=null,"Actual generated world supplies the requested context");
        var play=new PlaySession{Mode=GameMode.Creative,SolarDays=.125};play.Settings.HorizontalScale=.1;play.PrimeMeridianLongitude=Math.Atan2(site.Position.Y,site.Position.X)*180/Math.PI;
        var owner=new GameObject("Actual landscape review").AddComponent<LocalTerrainExplorer>();owner.viewDistance=900;
        owner.Initialize(Task.FromResult(model),site.Position,()=>{},world,play,worker:worker);
        timer.Restart();double deadline=Time.realtimeSinceStartupAsDouble+180;
        while((!owner.Ready||!owner.NearReady)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(owner.Ready&&owner.NearReady&&owner.Error=="","Actual worker terrain neighbourhood is uploaded without errors");double loadSeconds=timer.Elapsed.TotalSeconds;
        owner.TestView(3);owner.TestWeatherOverride=new WeatherFrame(22,.6,0,0,.15,.05,1800,500,7000,2,1,3,0);owner.SetDaylight(12,190000.5);
        var keys=new[]{site.Position,owner.frame.Position(-80,0),owner.frame.Position(80,0)}.Select(p=>owner.scatter.Address(p)).Distinct().ToArray();deadline=Time.realtimeSinceStartupAsDouble+90;
        while(keys.Any(k=>!owner.scatterChunks.TryGetValue(k,out var chunk)||chunk.Groups==null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(keys.All(k=>owner.scatterChunks.TryGetValue(k,out var chunk)&&chunk.Groups!=null),"Actual surrounding vegetation/scatter is resident");
        for(int i=0;i<90;i++)yield return null;
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
        void Capture(string name,Vector3 offset)
        {
            var focus=new Vector3(0,y+.5f,0);owner.view.transform.localPosition=focus+offset;owner.view.transform.LookAt(owner.transform.TransformPoint(focus));
            owner.BuildScatterBatches();owner.DrawScatter();owner.DrawGroundCover();owner.view.Render();RenderTexture.active=target;
            var image=new Texture2D(1536,864,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1536,864),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);RenderTexture.active=prior;
        }
        float width=(float)(site.Pond?.Radius??70);Capture("wide",new Vector3(width*1.1f,width*.65f,width*1.5f));Capture("ground",new Vector3(0,2.1f,Math.Max(8,width*.9f)));
        Check(world.Prehistory.Hash==history,"Local landscape queries do not mutate regional history");
        File.WriteAllText(Path.Combine(output,"scene.json"),WorldJson.Write(new LandscapeSceneReceipt{Requested=kind,Actual=sample.Landscape.ToString(),HistoryHash=history,Position=site.Position,Elevation=sample.ElevationM,
            Water=double.IsFinite(sample.WaterSurfaceM)?sample.WaterSurfaceM:0,Depth=sample.Underwater?sample.WaterSurfaceM-sample.ElevationM:0,PrepareMs=preparation,NearLoadSeconds=loadSeconds,Tiles=owner.tiles.Count,ScatterPages=owner.scatterChunks.Count,
            WaterRenderers=owner.tiles.Values.Count(t=>t.Renderers!=null&&t.Renderers.Length>1),Pond=site.Pond}));
        File.WriteAllLines(Path.Combine(output,"checks.txt"),checks);Debug.Log("LANDSCAPE PASS "+checks.Count);owner.view.targetTexture=null;target.Release();Destroy(target);Application.Quit(0);
    }
}
