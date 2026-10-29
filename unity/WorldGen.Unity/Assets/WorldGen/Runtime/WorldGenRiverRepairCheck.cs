using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckRiverRepairFlow(string output,List<string> report)
    {
        Color32[] Image(string name)
        {
            nextDaylightAt=0;SetDaylight(LocalSolarHour,skyYear);PublishTerrainScene();
            // Teleporting from below sea level can leave the normal camera's
            // underwater fade active. All these capture poses are above water.
            for(int i=0;i<512&&waterScreen.Blend>0;i++)UpdatePlayerWater();
            var prior=RenderTexture.active;var old=view.targetTexture;var target=new RenderTexture(1280,800,24);target.Create();view.targetTexture=target;view.Render();RenderTexture.active=target;
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();var pixels=image.GetPixels32();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());RenderTexture.active=prior;view.targetTexture=old;target.Release();Destroy(target);Destroy(image);return pixels;
        }
        PlayerRules.Mode=GameMode.Spectator;ApplyPlayerMode();humanCamera.Restore(0,false);WeatherControl.Set("clear");int oldSunMask=localSun.cullingMask;localSun.cullingMask|=1<<31;
        var sites=new[]{("mouth",-17.625665274156873,.6638456954785965,70001),("junction",-8.248825835553712,17.85174984290807,85691)};
        foreach(var site in sites)
        {
            var at=TerrainMapViewport.FromDegrees(site.Item2,site.Item3);Teleport(at);paused=false;
            double deadline=Time.unscaledTimeAsDouble+120;while(!PerformanceGroundSettled&&Time.unscaledTimeAsDouble<deadline)yield return null;paused=true;
            if(!PerformanceGroundSettled)throw new InvalidOperationException("River repair site did not settle: "+site.Item1);
            PlayerRules.SetLocalSolarHour(12,site.Item3);SetDaylight(LocalSolarHour,skyYear);UpdateCamera();
            TerrainHandoff.Coordinates(frame,at,out double x,out double z);var sample=walkingPatch.Sample(at);double water=frame.SurfaceY(at,sample.WaterSurfaceM);
            var target=transform.TransformPoint(new Vector3((float)(x-originX),(float)water,(float)(z-originZ)));
            view.transform.position=target+new Vector3(35,45,-45);view.transform.LookAt(target);Image(site.Item1+"-streamed");
            if(site.Item1=="junction")
            {
                view.transform.position=target+new Vector3(7,3,-9);view.transform.LookAt(target+new Vector3(-4,0,8));
                for(int f=0;f<4;f++){view.transform.position=target+new Vector3(7,3,-9);view.transform.LookAt(target+new Vector3(-4,0,8));Image("flow-"+f);yield return new WaitForSecondsRealtime(.6f);}
            }
            var profile=terrain.Amplification.RiverReach(site.Item4);File.WriteAllLines(Path.Combine(output,site.Item1+"-profile.txt"),profile.Select(s=>$"{s.From},{s.To}: {s.LevelA:R} -> {s.LevelB:R}; {s.WidthA:R},{s.WidthB:R}"));
            File.WriteAllText(Path.Combine(output,site.Item1+"-sample.txt"),$"ground={sample.ElevationM:R}; water={sample.WaterSurfaceM:R}; kind={sample.WaterKind}; wet={sample.Underwater}");
            int oldMask=view.cullingMask;view.cullingMask=1<<31;
            foreach(var lod in new[]{(size:204.8,n:64),(size:819.2,n:32),(size:3276.8,n:16)})
            {
                // Keep the source station away from grid vertices: centring it
                // on a vertex would conceal channels bridged by coarse faces.
                double east=x-lod.size/2+lod.size/lod.n*.37,north=z-lod.size/2+lod.size/lod.n*.27;
                var source=LocalTerrainMesh.Build(terrain,frame,east,north,lod.size,lod.n);Mesh[] meshes;using(var upload=TerrainUpload.Prepare(source,default))meshes=upload.Apply();
                var root=new GameObject("River render diagnostic");root.transform.SetParent(transform,false);root.transform.localPosition=new Vector3((float)(east-originX),0,(float)(north-originZ));
                var materials=new[]{new Material(landMaterial),new Material(waterMaterial)};
                for(int i=0;i<meshes.Length;i++)
                {
                    materials[i].SetFloat("_TerrainSceneEnabled",1);materials[i].SetFloat("_CoverageEnabled",0);materials[i].SetFloat("_TransitionEnabled",0);var part=new GameObject(i==0?"Ground":"Water",typeof(MeshFilter),typeof(MeshRenderer));part.layer=31;part.transform.SetParent(root.transform,false);part.GetComponent<MeshFilter>().sharedMesh=meshes[i];part.GetComponent<MeshRenderer>().sharedMaterial=materials[i];
                }
                view.transform.position=target+new Vector3(35,45,-45);view.transform.LookAt(target);Image(site.Item1+"-lod"+lod.n+"-"+(int)lod.size);
                view.transform.position=target+new Vector3(7,2,-9);view.transform.LookAt(target);Image(site.Item1+"-low-"+(int)lod.size);
                DestroyImmediate(root);foreach(var mesh in meshes)Destroy(mesh);foreach(var material in materials)Destroy(material);
            }
            view.cullingMask=oldMask;report.Add("PASS captured actual procedural "+site.Item1+" and three off-grid production mesh LODs");
        }
        int oceanCell=Enumerable.Range(0,sourceWorld.Surface.Cells.Length).Where(i=>sourceWorld.Hydrology.OceanConnected[i]&&sourceWorld.ElevationM[i]<-100).OrderBy(i=>(sourceWorld.Surface.Cells[i].Position-TerrainMapViewport.FromDegrees(-17.62,.66)).Length).First();
        var sea=sourceWorld.Surface.Cells[oceanCell].Position;Teleport(sea);paused=false;double seaDeadline=Time.unscaledTimeAsDouble+120;while(!PerformanceGroundSettled&&Time.unscaledTimeAsDouble<seaDeadline)yield return null;paused=true;
        TerrainMapViewport.Degrees(sea,out _,out double seaLon);PlayerRules.SetLocalSolarHour(12,seaLon);SetDaylight(LocalSolarHour,skyYear);UpdateCamera();
        TerrainHandoff.Coordinates(frame,sea,out double seaX,out double seaZ);double seaY=frame.SurfaceY(sea,0);
        motion.Place(seaX,seaZ,Ground(seaX,seaZ),true,1);motion.Y=seaY+1.8;UpdateCamera();var eye=transform.TransformPoint(new Vector3((float)(seaX-originX),(float)seaY+1.5f,(float)(seaZ-originZ)));view.transform.position=eye;view.transform.rotation=Quaternion.Euler(2,0,0);
        var waveMaterials=tiles.Values.Where(t=>t.Materials!=null).SelectMany(t=>t.Materials).Where(m=>m!=null&&m.HasProperty("_SwellScale")).Distinct().ToArray();
        var displaced=Image("ocean-displaced");foreach(var material in waveMaterials)material.SetFloat("_SwellScale",0);var flat=Image("ocean-flat-control");foreach(var material in waveMaterials)material.SetFloat("_SwellScale",1);
        int geometryChanged=0;for(int i=0;i<displaced.Length;i++)if(Math.Abs(displaced[i].r-flat[i].r)+Math.Abs(displaced[i].g-flat[i].g)+Math.Abs(displaced[i].b-flat[i].b)>15)geometryChanged++;
        if(geometryChanged<500)throw new InvalidOperationException("Ocean vertex displacement is not visibly different from the same-frame flat control");report.Add("PASS actual GPU ocean displacement differs from the flat control: "+geometryChanged+" pixels");
        var first=Image("ocean-motion-0");for(int f=1;f<=4;f++){yield return new WaitForSecondsRealtime(.6f);view.transform.position=eye;view.transform.rotation=Quaternion.Euler(2,0,0);Image("ocean-motion-"+f);}
        view.transform.position=eye;view.transform.rotation=Quaternion.Euler(2,0,0);int changed=0;var lastPixels=Image("ocean-motion-last");for(int i=0;i<first.Length;i++)if(Math.Abs(first[i].r-lastPixels[i].r)+Math.Abs(first[i].g-lastPixels[i].g)+Math.Abs(first[i].b-lastPixels[i].b)>15)changed++;
        if(changed<2000)throw new InvalidOperationException("Ocean surface did not visibly evolve in actual GPU renders");report.Add("PASS ocean surface visibly evolves across real rendered frames: "+changed+" pixels");
        File.WriteAllText(Path.Combine(output,"ocean-state.txt"),$"cell={oceanCell}; wind={CurrentWeather.WindEastMps:R},{CurrentWeather.WindNorthMps:R}; changed={changed}; geometry={geometryChanged}");
        localSun.cullingMask=oldSunMask;File.WriteAllLines(Path.Combine(output,"checks.txt"),report);
    }
}
