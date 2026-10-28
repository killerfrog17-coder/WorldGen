using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckFireflyFlow(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("FIREFLY "+text);report.Add("PASS "+text);Debug.Log("FIREFLY PASS "+text);}
        var profile=animalProfiles.Single(p=>p.derivedFirefly);
        Check(profile.radiusM==220&&profile.VisibleRadiusM==220&&animalProfiles.Single(p=>p.slug=="butterflies-and-moths-guild").VisibleRadiusM==220,"Packaged butterflies and fireflies have220m visibility");
        while(animalLoading!=null)yield return null;
        if(!animalModels.ContainsKey(profile.slug))yield return LoadAnimalModel(profile);
        Check(animalModels.ContainsKey(profile.slug),"Unchanged firefly bundle and collision skin load successfully");
        var spawn=new FaunaPrototypeSpawn{Id=++animalManualSequence,Hash=12345678,Position=Position,Profile=profile,GroundM=Ground(motion.X,motion.Z),WaterM=double.NaN,HeightM=1.2,Taxon=-1};
        Check(AddAnimal(spawn,animalModels[profile.slug],true),"Real firefly actor can be admitted");var animal=animals[spawn.Id];
        Check(animal.Firefly?.Family==AmbientFaunaFamily.Firefly&&animal.Firefly.RegionId==sourceWorld.Ecology.Region[terrain.NearestCell(Position)],"Real actor uses its generated local ecoregion display profile");
        yield return CheckWildlifeRadar(animal,report);
        debugOpen=debugFull=wildlifeRadarOpen=true;wildlifeRadarRefreshAt=0;yield return null;yield return null;
        // Batch players omit IMGUI repaint. Use the same readout metrics and
        // layout function, without requiring a GUI event or fabricated panel.
        if(debugLeft==null)
        {
            debugLeft=new GUIStyle(MenuSkin!=null?MenuSkin.label:GUIStyle.none){fontSize=14,wordWrap=true,richText=false,alignment=TextAnchor.UpperLeft};
            debugRight=new GUIStyle(debugLeft){alignment=TextAnchor.UpperRight};debugHeadingLeft=new GUIStyle(debugLeft){fontStyle=FontStyle.Bold};debugHeadingRight=new GUIStyle(debugHeadingLeft){alignment=TextAnchor.UpperRight};
        }
        UpdateDebugReadout();wildlifeRadarPanel=WildlifeRadarLayout(out _,out _,out _);
        File.WriteAllText(Path.Combine(output,"radar-layout.json"),JsonUtility.ToJson(new FireflyRadarLayout{panel=wildlifeRadarPanel,vegetation=debugCornerRects[1],terrain=debugCornerRects[3],width=Screen.width,height=Screen.height},true));
        Check(wildlifeRadarPanel.width>0&&wildlifeRadarPanel.x>=Screen.width*.5f&&Mathf.Abs(wildlifeRadarPanel.xMax-(Screen.width-12))<1,"Radar is on the right screen edge");
        Check(!wildlifeRadarPanel.Overlaps(debugCornerRects[1])&&!wildlifeRadarPanel.Overlaps(debugCornerRects[3]),"Radar avoids Vegetation and Terrain/System rectangles");
        var originalWeather=CurrentWeather;double originalYear=skyYear,originalHour=LocalSolarHour;TerrainMapViewport.Degrees(Position,out double latitude,out double longitude);
        var lineage=animal.Firefly;CurrentWeather=new WeatherFrame(lineage.Activity.PreferredTemperatureC,.9,0,0,0,0,1500,1700,8500,0,0,0,0);
        skyYear=(latitude>=0?.56:.06)+lineage.Activity.SeasonPeakOffsetDays/365;PlayerRules.SetLocalSolarHour((lineage.Activity.StartSolarHour+2)%24,longitude);
        float maximum=0;for(int i=0;i<2000;i++){animal.Life=i*.01;UpdateFireflyGlow(animal,100);maximum=Mathf.Max(maximum,animal.FireflyGlow);}
        Check(maximum>.8&&!animal.Renderer.enabled,"Real warm evening actor flashes and uses its distant luminous point instead of a subpixel mesh");
        PlayerRules.SetLocalSolarHour(12,longitude);UpdateFireflyGlow(animal,10);Check(animal.FireflyGlow==0&&animal.Renderer.enabled,"Actual actor has no noon glow; true body remains available nearby");
        CurrentWeather=originalWeather;skyYear=originalYear;PlayerRules.SetLocalSolarHour(originalHour,longitude);RemoveAnimal(spawn.Id);
        PrepareFireflyGlows();Check(fireflyMaterial!=null&&fireflyMaterial.shader.isSupported,"Packaged emissive sprite shader is supported");
        var cameraObject=new GameObject("Firefly render fixture",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;
        camera.transform.position=new Vector3(0,10000,0);camera.transform.rotation=Quaternion.identity;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<31;camera.fieldOfView=60;camera.nearClipPlane=.1f;camera.farClipPlane=300;
        var target=new RenderTexture(512,256,24);target.Create();camera.targetTexture=target;var image=new Texture2D(512,256,TextureFormat.RGB24,false);
        var priorRT=RenderTexture.active;var fixtureMaterial=new Material(fireflyMaterial);fixtureMaterial.SetFloat("_VegetationSceneEnabled",0);
        try
        {
            foreach(float distance in new[]{10f,100f,200f})
            {
                yield return null; // Each scheduled draw belongs to a fresh frame.
                float size=Mathf.Max(.025f,2.2f*2*Mathf.Tan(30*Mathf.Deg2Rad)*distance/256);
                var matrices=new[]{Matrix4x4.TRS(camera.transform.position+Vector3.forward*distance,Quaternion.identity,Vector3.one*size)};
                var props=new MaterialPropertyBlock();props.SetVectorArray("_FireflyGlowColor",new[]{new Vector4(.65f,1,.08f,1)*5/(1+Mathf.Pow(distance/100,2))});
                Graphics.DrawMeshInstanced(fireflyQuad,0,fixtureMaterial,matrices,1,props,ShadowCastingMode.Off,false,31,camera,LightProbeUsage.Off);
                camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,512,256),0,0);image.Apply();var pixels=image.GetPixels32();
                int bright=pixels.Count(p=>p.g>20);Check(bright>=1&&bright<64,"Actual instanced glow renders a small luminous point at "+distance+"m");
                File.WriteAllBytes(Path.Combine(output,"glow-"+distance+"m.png"),image.EncodeToPNG());
            }
        }
        finally{RenderTexture.active=priorRT;camera.targetTexture=null;target.Release();Destroy(target);Destroy(image);Destroy(fixtureMaterial);Destroy(cameraObject);}
        debugOpen=debugFull=wildlifeRadarOpen=false;
        yield return CheckTargetEntities(report);
    }
    IEnumerator CheckTargetEntities(List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("TARGET "+text);report.Add("PASS "+text);Debug.Log("TARGET PASS "+text);}
        InitializeInventory();ExecuteAnimalSpawnCommand("/spawn deer");double deadline=Time.unscaledTimeAsDouble+35;
        while(!animals.Values.Any(a=>a.Manual&&a.Spawn.Profile.slug=="deer"&&!a.Dead)&&Time.unscaledTimeAsDouble<deadline)yield return null;
        var animal=animals.Values.FirstOrDefault(a=>a.Manual&&a.Spawn.Profile.slug=="deer"&&!a.Dead);Check(animal!=null,"Target fixture loads the original deer");paused=true;animal.Fade=1;
        bool Recognizes(string word)
        {
            animal.CollisionFrame=-1;var surface=AnimalSurface(animal);var vertices=PosedAnimal(animal);
            for(int face=0;face<surface.Triangles.Length;face+=3)
            {
                int a=surface.Triangles[face],b=surface.Triangles[face+1],c=surface.Triangles[face+2];if(!surface.Visible(a,animal.Parts))continue;
                var point=(vertices[a]+vertices[b]+vertices[c])/3;var normal=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).normalized;if(normal.sqrMagnitude<.5f)continue;
                string label=TargetActorOrItem(new Ray(point+normal*.3f,-normal),1,out _);
                if(label!=null&&label.Contains(word)&&label.Contains(animal.Spawn.Profile.name))return true;
            }
            return false;
        }
        Check(Recognizes("animal"),"Target selection recognizes the real live animal skin");
        DamageAnimal(animal,10000,animal.Renderer.bounds.center,Vector3.zero,0);UpdateAnimal(animal,0);
        Check(Recognizes("corpse"),"Target selection recognizes the same original-skinned ragdoll as a corpse");
        var item=new Possession{Id="target-debug-fixture",Kind=PossessionKind.SmallRock,Place=PossessionPlace.Ground,WidthM=.1,HeightM=.1,DepthM=.1,MassKg=.7,Position=Position};
        Inventory.Items.Add(item);var root=CreatePossessionVisual(item,30);root.transform.SetParent(transform,false);root.transform.position=view.transform.position+Vector3.up*100;inventoryVisuals[item.Id]=root;
        var renderer=root.GetComponentsInChildren<Renderer>().First(r=>r.enabled);var ray=new Ray(renderer.bounds.center+Vector3.back*.5f,Vector3.forward);
        string name=TargetActorOrItem(ray,1,out _);Check(name!=null&&name.Contains("Small rock")&&name.Contains("ground item"),"Target selection recognizes a real loose inventory item");
        item.Place=PossessionPlace.RightHand;name=TargetActorOrItem(ray,1,out _);Check(name==null||!name.Contains("ground item"),"Own held equipment is excluded from world-item target selection");
        Inventory.Items.Remove(item);inventoryVisuals.Remove(item.Id);DestroyInventoryVisual(root);paused=false;
    }
    [Serializable] sealed class FireflyRadarLayout {public Rect panel,vegetation,terrain;public int width,height;}
}
