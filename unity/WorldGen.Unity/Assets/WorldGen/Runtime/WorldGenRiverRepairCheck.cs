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
        var cameraAudit=new List<string>{"capture,clearance_m"};
        double DrawnFloor(LocalTerrainMesh mesh,double x,double z)
        {
            double result=double.NegativeInfinity;var p=mesh.Positions;var triangles=mesh.Triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i]*3,b=triangles[i+1]*3,c=triangles[i+2]*3;double bx=p[b]-p[a],bz=p[b+2]-p[a+2],cx=p[c]-p[a],cz=p[c+2]-p[a+2],det=bx*cz-bz*cx;if(Math.Abs(det)<1e-10)continue;
                double u=((x-p[a])*cz-(z-p[a+2])*cx)/det,v=(bx*(z-p[a+2])-bz*(x-p[a]))/det;
                if(u>=-1e-6&&v>=-1e-6&&u+v<=1.000001)result=Math.Max(result,p[a+1]+u*(p[b+1]-p[a+1])+v*(p[c+1]-p[a+1]));
            }
            return result;
        }
        void AboveGroundEye(Vector3 desired,Vector3 target,LocalTerrainMesh mesh=null)
        {
            var local=transform.InverseTransformPoint(desired);double x=local.x+originX,z=local.z+originZ;
            double floor=mesh==null?CampFloor(x,z):DrawnFloor(mesh,x-mesh.EastM,z-mesh.NorthM);
            if(!double.IsFinite(floor))floor=CampFloor(x,z);
            local.y=Mathf.Max(local.y,(float)floor+1.65f);view.transform.localPosition=local;view.transform.LookAt(target);
        }
        Color32[] Image(string name,bool underwater=false,LocalTerrainMesh mesh=null)
        {
            if(!underwater)
            {
                var eye=view.transform.localPosition;double x=eye.x+originX,z=eye.z+originZ;
                double floor=mesh==null?CampFloor(x,z):DrawnFloor(mesh,x-mesh.EastM,z-mesh.NorthM);double clearance=eye.y-floor;
                if(double.IsFinite(floor)&&clearance<1)throw new InvalidOperationException("Diagnostic camera is inside/below the drawn bank: "+name+" / "+clearance);
                cameraAudit.Add(name+","+clearance.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            }
            nextDaylightAt=0;SetDaylight(LocalSolarHour,skyYear);PublishTerrainScene();
            // Teleporting from below sea level can leave the normal camera's
            // underwater fade active. All these capture poses are above water.
            UpdatePlayerWater(true);
            if(underwater?waterScreen.Blend<.99f:waterScreen.Blend>.01f)throw new InvalidOperationException("Capture camera is on the wrong side of the water surface: "+name);
            var prior=RenderTexture.active;var old=view.targetTexture;var target=new RenderTexture(1280,800,24);target.Create();view.targetTexture=target;view.Render();RenderTexture.active=target;
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();var pixels=image.GetPixels32();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());RenderTexture.active=prior;view.targetTexture=old;target.Release();Destroy(target);Destroy(image);return pixels;
        }
        PlayerRules.Mode=GameMode.Spectator;ApplyPlayerMode();humanCamera.Restore(0,false);WeatherControl.Set("clear");int oldSunMask=localSun.cullingMask;localSun.cullingMask|=1<<31;
        var sites=new[]{("mouth",-17.625665274156873,.6638456954785965,70001),("junction",-8.248825835553712,17.85174984290807,85691),("major",8.025672555331576,-66.52253994300001,114117)};
        foreach(var site in sites)
        {
            if(site.Item1=="major"){viewDistance=Math.Min(3000,MaximumViewDistance);TerrainVideoOptions.Current.ViewDistanceM=viewDistance;TerrainVideoOptions.Revision++;}
            var at=TerrainMapViewport.FromDegrees(site.Item2,site.Item3);if((Position-at).Length*frame.RadiusM>.1)Teleport(at);paused=false;
            double deadline=Time.unscaledTimeAsDouble+180;while(!PerformanceGroundSettled&&Time.unscaledTimeAsDouble<deadline)yield return null;paused=true;
            if(!PerformanceGroundSettled)throw new InvalidOperationException("River repair site did not settle: "+site.Item1);
            PlayerRules.SetLocalSolarHour(12,site.Item3);SetDaylight(LocalSolarHour,skyYear);UpdateCamera();
            TerrainHandoff.Coordinates(frame,at,out double x,out double z);var sample=walkingPatch.Sample(at);double water=frame.SurfaceY(at,sample.WaterSurfaceM);
            var target=transform.TransformPoint(new Vector3((float)(x-originX),(float)water,(float)(z-originZ)));
            var profile=terrain.Amplification.RiverReach(site.Item4);var nearest=profile.OrderBy(s=>((s.A+s.B).Unit-at).Length).First();float scaleCamera=Mathf.Clamp((float)Math.Max(nearest.WidthA,nearest.WidthB)/12,1,12);
            AboveGroundEye(target+new Vector3(35,45,-45)*scaleCamera,target);Image(site.Item1+"-streamed");
            if(site.Item1=="junction")
            {
                for(int f=0;f<4;f++){AboveGroundEye(target+new Vector3(7,3,-9),target+new Vector3(-4,0,8));Image("flow-"+f);yield return new WaitForSecondsRealtime(.6f);}
            }
            int dryBanks=0;var center=(nearest.A+nearest.B).Unit;var sideAxis=(nearest.SideA+nearest.SideB).Unit;double halfWidth=(nearest.WidthA+nearest.WidthB)*.5;
            foreach(int side in new[]{-1,1})foreach(double factor in new[]{1.01,1.05,1.15,1.3,1.7,2.5,4})
            {
                var bank=(center+sideAxis*(side*(halfWidth*factor+.7)/frame.RadiusM)).Unit;var bankSample=terrain.Patch(bank,3000).Sample(bank);if(bankSample.Underwater)continue;
                TerrainHandoff.Coordinates(frame,bank,out double bx,out double bz);double floor=CampFloor(bx,bz);
                var bankEye=transform.TransformPoint(new Vector3((float)(bx-originX),(float)floor+1.65f,(float)(bz-originZ)));AboveGroundEye(bankEye,target);Image(site.Item1+"-standing-bank-"+(side<0?"left":"right"));dryBanks++;break;
            }
            if(dryBanks==0)throw new InvalidOperationException("No actual dry-bank viewpoint found for "+site.Item1);report.Add("PASS captured actual standing dry-bank views for "+site.Item1);
            File.WriteAllLines(Path.Combine(output,site.Item1+"-profile.txt"),profile.Select(s=>$"{s.From},{s.To}: {s.LevelA:R} -> {s.LevelB:R}; {s.WidthA:R},{s.WidthB:R}; depths {s.DepthA:R},{s.DepthB:R}"));
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
                AboveGroundEye(target+new Vector3(35,45,-45),target,source);Image(site.Item1+"-lod"+lod.n+"-"+(int)lod.size,mesh:source);
                AboveGroundEye(target+new Vector3(7,2,-9),target,source);Image(site.Item1+"-low-"+(int)lod.size,mesh:source);
                DestroyImmediate(root);foreach(var mesh in meshes)Destroy(mesh);foreach(var material in materials)Destroy(material);
            }
            view.cullingMask=oldMask;report.Add("PASS captured actual procedural "+site.Item1+" and three off-grid production mesh LODs");
            if(site.Item1=="major")
            {
                if(sample.WaterKind!=LocalWaterKind.River||sample.WaterSurfaceM-sample.ElevationM<15||nearest.WidthA+nearest.WidthB<1000)throw new InvalidOperationException("Actual major river is not broad and deep");
                report.Add("PASS actual river exceeds 1km width and 15m water depth");
                if(!PlayerWaterQuality(sample).Fresh)throw new InvalidOperationException("Deep inland river became saline");report.Add("PASS deep inland river remains freshwater");
                PlayerRules.Mode=GameMode.Survival;Vitals.Restore();ApplyPlayerMode();motion.Place(x,z,Ground(x,z));motion.Y=water-.5-humanEyeOffset;motion.Water=SampleMotionWater;motion.WaterEyeOffset=humanEyeOffset;
                for(int i=0;i<300;i++)motion.Move(1d/60,0,0,0,heading,0,false,false,PlayerSolidGround);
                if(!motion.Swimming||Math.Abs(motion.Y+humanEyeOffset-SampleMotionWater(motion.X,motion.Z).Surface-.12)>.15)throw new InvalidOperationException("Cannot float in a deep major river");report.Add("PASS player floats above the actual deep riverbed");
                double beforeX=motion.X,beforeZ=motion.Z;for(int i=0;i<180;i++)motion.Move(1d/60,0,0,0,heading,0,false,false,PlayerSolidGround);
                if(Math.Sqrt(Math.Pow(motion.X-beforeX,2)+Math.Pow(motion.Z-beforeZ,2))<1)throw new InvalidOperationException("Major river current does not carry a floating player");report.Add("PASS actual major-river current carries a passive swimmer");
                motion.Y=SampleMotionWater(motion.X,motion.Z).Surface-humanEyeOffset-2;UpdateHumans();UpdateCamera();view.transform.localPosition=new Vector3((float)(motion.X-originX),(float)water-2,(float)(motion.Z-originZ));view.transform.rotation=Quaternion.Euler(5,30,0);Image("major-underwater",true);
                if(waterScreen.Blend<.99||!waterScreen.BlurEyes||waterScreen.DrawCount==0)throw new InvalidOperationException("Deep river lacks actual naked-eye underwater postprocessing");report.Add("PASS actual deep river renders underwater attenuation and naked-eye blur");
                PlayerRules.Mode=GameMode.Spectator;ApplyPlayerMode();
            }
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
        localSun.cullingMask=oldSunMask;File.WriteAllLines(Path.Combine(output,"camera-clearance.csv"),cameraAudit);File.WriteAllLines(Path.Combine(output,"checks.txt"),report);
    }
}
