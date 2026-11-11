using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WorldGen.Core;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    [Serializable] sealed class HumanReviewRow
    {public string Label,Culture,People,Species;public int Cohort,SourceCell;public ForagerPersonSample Person;}
    [Serializable] sealed class HumanReviewData
    {public string HistoryHash;public int Seed,Year;public HumanReviewRow[] Rows;public int DistinctPeople,DistinctSpecies;}
    public static IEnumerator CheckHumanPhenotypes(string samples,string output,bool quit=true)
    {
        Directory.CreateDirectory(output);var checks=new List<string>();
        void Check(bool ok,string label){if(!ok)throw new InvalidOperationException("PHENOTYPE "+label);checks.Add("PASS "+label);}
        if(quit)Application.logMessageReceived+=(m,t,k)=>{if(k==LogType.Error||k==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),m+"\n"+t);Application.Quit(1);}};
        var data=WorldJson.Read<HumanReviewData>(File.ReadAllText(samples));
        var owner=new GameObject("Inherited appearance validation").AddComponent<LocalTerrainExplorer>();owner.enabled=false;
        owner.humanLibrary=Resources.Load<WorldGenHumanLibrary>("HumanAppearance/Library");owner.kimodoHumanLibrary=Resources.Load<WorldGenHumanMotionBank>("HumanKimodo/Library");owner.EnsureHumanPhenotypes();
        Check(owner.humanPhenotypeLibrary!=null&&owner.humanPhenotypeLibrary.Hair.Length==216,"24 age/body templates retain all nine fitted grooming forms");
        var cam=new GameObject("Phenotype camera",typeof(Camera),typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();cam.enabled=false;cam.cullingMask=1<<29;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.13f,.15f,.17f);cam.nearClipPlane=.01f;cam.farClipPlane=30;
        var light=new GameObject("Phenotype daylight",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.LookRotation(new Vector3(-.3f,-1,-.8f));
        var scene=new Vector4[TerrainSceneVectors];scene[1]=new Vector4(.22f,.22f,.22f,1);scene[2]=new Vector4(1e9f,1,0,0);
        var buffer=new GraphicsBuffer(GraphicsBuffer.Target.Constant,TerrainSceneVectors,16);buffer.SetData(scene);Shader.SetGlobalConstantBuffer(TerrainSceneId,buffer,0,TerrainSceneVectors*16);Shader.SetGlobalFloat("_WorldGenFirstPersonHumanId",0);Shader.SetGlobalVector("_WorldGenSkyReflection",new Vector4(0,0,0,1));
        var target=new RenderTexture(900,1000,24);target.Create();cam.targetTexture=target;var previous=RenderTexture.active;
        void Capture(string name,Vector3 focus,float extent)
        {
            cam.orthographic=true;cam.orthographicSize=extent;cam.transform.position=focus+new Vector3(.14f,0,3);cam.transform.LookAt(focus);cam.Render();cam.Render();RenderTexture.active=target;
            var image=new Texture2D(900,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,900,1000),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());DestroyImmediate(image);
        }
        try
        {
            int index=0;var metrics=new List<string>();var hairMetrics=new List<string>();
            foreach(var row in data.Rows)
            {
                var person=row.Person;int variant=PlayerWorldEntry.Variant(person);var age=Resources.Load<WorldGenHumanAgeLibrary>("HumanAges/Library").Find(owner.humanLibrary.People[variant].Variant,person.AgeYears);
                var actor=owner.CreateHuman(variant,1000+index,true,age,person);actor.Resident=person;
                if(age!=null){double factor=person.AgeYears<age.Years?1-(age.Years-person.AgeYears)*.035:1+(person.AgeYears-age.Years)*.018;actor.Root.transform.localScale=actor.PhenotypeScale*(float)Math.Max(.78,Math.Min(1.09,factor));}
                void Pose(float speed,bool crouch){owner.AnimateHuman(actor,speed/(actor.Root.transform.localScale.y*actor.AgeMotionRatio),false,crouch,1f/30);actor.Shared.Routine(null,0,1f/30);actor.Shared.Carry(null,0,1f/30);actor.Graph.Evaluate(0);ApplyHumanAgePose(actor);}
                try
                {
                    foreach(var t in actor.Root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;
                    Check(actor.Shared!=null,"resident"+index+" retains native animation bank");
                    var body=actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name.ToLowerInvariant().EndsWith(".body")||r.name.EndsWith("_Body"));
                    Check(owner.humanDetailMeshes.ContainsKey(body.sharedMesh),"resident"+index+" has personally morphed full/middle/far bodies");
                    Check(body.sharedMesh.blendShapeCount==0,"resident"+index+" has no active runtime blendshape processing");
                    Check(body.sharedMaterials.All(m=>m.GetFloat("_PigmentMode")==1),"resident"+index+" inherited skin uses per-material palette");
                    Check(actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.name.StartsWith("hair groom_"))==(person.Grooming.Hair!=HumanHairStyle.Shaved),"resident"+index+" cultural hairstyle is visibly selected");
                    foreach(var pose in new[]{("idle",0f), ("walk",1.5f), ("run",3.4f), ("crouch",.7f)})
                    {
                        for(int step=0;step<20;step++)Pose(pose.Item2,pose.Item1=="crouch");
                        var vertices=body.sharedMesh.vertices;var weights=body.sharedMesh.boneWeights;var bind=body.sharedMesh.bindposes;
                        var baked=new Mesh();body.BakeMesh(baked,true);var bakedTrue=baked.vertices;body.BakeMesh(baked,false);var bakedFalse=baked.vertices;
                        double errTrue=0,errFalse=0;int probes=0;var bounds=new Bounds();bool first=true;
                        for(int v=0;v<vertices.Length;v+=Math.Max(1,vertices.Length/512))
                        {
                            var w=weights[v];Vector3 expected=Vector3.zero;
                            void Add(int b,float weight){if(weight>0)expected+=body.bones[b].localToWorldMatrix.MultiplyPoint3x4(bind[b].MultiplyPoint3x4(vertices[v]))*weight;}
                            Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);
                            Check(float.IsFinite(expected.x)&&float.IsFinite(expected.y)&&float.IsFinite(expected.z),"finite native skin sample");
                            errTrue=Math.Max(errTrue,(body.transform.TransformPoint(bakedTrue[v])-expected).magnitude);errFalse=Math.Max(errFalse,(body.transform.TransformPoint(bakedFalse[v])-expected).magnitude);probes++;
                            if(first){bounds=new Bounds(expected,Vector3.zero);first=false;}else bounds.Encapsulate(expected);
                        }
                        Check(errTrue<.002,"resident"+index+"/"+pose.Item1+" production combat scale agrees with actual bone matrices");
                        metrics.Add(index+","+pose.Item1+","+person.AgeYears+","+actor.Root.transform.localScale.y+","+errTrue+","+errFalse+","+bounds.size.y);
                        DestroyImmediate(baked);
                    }
                    for(int step=0;step<20;step++)Pose(0,false);
                    foreach(var hair in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.name.StartsWith("hair groom_")))
                    {
                        var mesh=new Mesh();hair.BakeMesh(mesh,true);var positions=mesh.vertices.Select(v=>hair.transform.TransformPoint(v)).ToArray();var hb=new Bounds(positions[0],Vector3.zero);foreach(var p in positions)hb.Encapsulate(p);
                        hairMetrics.Add(index+","+hair.name+","+hb.center.x+","+hb.center.y+","+hb.center.z+","+hb.size.x+","+hb.size.y+","+hb.size.z+","+owner.HumanActualEye(actor));DestroyImmediate(mesh);
                    }
                    var eyes=actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name.Contains("high-poly")||r.name.EndsWith("_Eyes"));var eyeMesh=new Mesh();eyes.BakeMesh(eyeMesh,true);var eyeCenter=Vector3.zero;foreach(var p in eyeMesh.vertices)eyeCenter+=eyes.transform.TransformPoint(p);eyeCenter/=eyeMesh.vertexCount;DestroyImmediate(eyeMesh);
                    float eye=owner.HumanActualEye(actor);hairMetrics.Add(index+",eye_mesh,"+eyeCenter.x+","+eyeCenter.y+","+eyeCenter.z+",0,0,0,"+eye);
                    Capture("resident-"+index+"-face",eyeCenter-Vector3.up*.035f,.26f*actor.Root.transform.localScale.y);
                    var bm=new Mesh();body.BakeMesh(bm,true);var points=bm.vertices.Select(p=>body.transform.TransformPoint(p)).ToArray();var bb=new Bounds(points[0],Vector3.zero);foreach(var p in points)bb.Encapsulate(p);DestroyImmediate(bm);
                    Capture("resident-"+index+"-body",bb.center,bb.size.y*.6f);
                    Check(owner.HumanActualEye(actor)>.3f&&owner.HumanActualEye(actor)<2.2f,"resident"+index+" scaled eye height bounded");
                    // Exercise the actual LOD meshes with the same person data.
                    var lod=owner.humanDetailMeshes[body.sharedMesh];Check(lod.Middle.vertexCount<lod.Original.vertexCount&&lod.Far.vertexCount<lod.Middle.vertexCount,"resident"+index+" retained reduced body detail");
                    index++;
                }
                finally
                {
                    actor.Root.SetActive(false);
                    if(Application.isPlaying)owner.DestroyHuman(actor);
                    else
                    {
                        actor.Shared?.Dispose();if(actor.Graph.IsValid())actor.Graph.Destroy();
                        foreach(var mesh in actor.PhenotypeMeshes){owner.humanDetailMeshes.Remove(mesh);DestroyImmediate(mesh);}
                        foreach(var material in actor.OwnedMaterials)DestroyImmediate(material);
                        DestroyImmediate(actor.Root);
                    }
                }
                yield return null;
            }
            File.WriteAllLines(Path.Combine(output,"skin-scale.csv"),new[]{"resident,pose,age,root_scale,bake_true_error_m,bake_false_error_m,skin_height_m"}.Concat(metrics));
            File.WriteAllLines(Path.Combine(output,"hair-fit.csv"),new[]{"resident,hair,x,y,z,width,height,depth,eye_height"}.Concat(hairMetrics));
            File.WriteAllText(Path.Combine(output,"samples.json"),WorldJson.Write(data));
        }
        finally{owner.DisposeHumanPhenotypes();RenderTexture.active=previous;cam.targetTexture=null;target.Release();buffer.Dispose();DestroyImmediate(target);DestroyImmediate(cam.gameObject);DestroyImmediate(light.gameObject);DestroyImmediate(owner.gameObject);}
        File.WriteAllLines(Path.Combine(output,"checks.txt"),checks);Debug.Log("HUMAN PHENOTYPE PASS "+checks.Count);if(quit)Application.Quit(0);
    }
}
