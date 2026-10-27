using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator PerformanceHumanReview(string output)
    {
        var report=new List<string>();
        void Check(bool ok,string label){if(!ok)throw new InvalidOperationException("HUMAN CHECK "+label);report.Add("PASS "+label);}
        Check(humanPlayer!=null&&humanLibrary.People.Length==6,"six real MPFB variants loaded");
        TestNearbyFlatGround();TestWalking();yield return new WaitForSecondsRealtime(1);
        TerrainVideoOptions.GroundCover=false; // Expose actual feet for this diagnostic only.
        Check(humanNpcs.Count==0&&humanSpawnQueued==0,"humans do not spawn automatically");
        Check(ExecuteExplorerCommand("/spawn human clear").Success,"clear human spawn command");
        Check(ExecuteExplorerCommand("/spawn human 24").Success,"spawn human alias queues more than former cap");
        double spawnDeadline=Time.unscaledTime+15;
        while(humanSpawnQueued>0&&Time.unscaledTime<spawnDeadline)yield return null;
        Check(humanNpcs.Count==24,"more than sixteen live humans");
        Check(!ExecuteExplorerCommand("/spawn human -1").Success,"reject negative population request");
        var air=animalProfiles.First(p=>p.medium=="air"&&p.tiny);
        Check(ExecuteExplorerCommand("/spawn "+air.slug+" 160").Success,"queue more than previous animal command limit");
        double animalDeadline=Time.unscaledTime+40;
        while(animals.Values.Count(a=>a.Manual)<160&&Time.unscaledTime<animalDeadline)yield return null;
        Check(animals.Values.Count(a=>a.Manual)==160&&animalBoneData.Length/AnimalBones>128,"animal palette grows beyond former population cap");
        var gpu=new Matrix4x4[32];animalBoneBuffer.GetData(gpu,0,0,gpu.Length);
        Check(gpu.Select((m,i)=>m==animalBoneData[i]).All(b=>b),"grown GPU palette retains uploaded poses");
        var first=humanNpcs[0].Root.transform.localPosition;yield return new WaitForSecondsRealtime(8);
        Check(humanNpcs.Any(h=>h.Speed>.05f)||(humanNpcs[0].Root.transform.localPosition-first).sqrMagnitude>.01,"demo humans move with live clips");
        Check(ExecuteExplorerCommand("/spawn clear").Success&&humanNpcs.Count==0&&humanSpawnQueued==0,"spawn clear also clears humans and their queue");
        float idleYaw=humanPlayer.Yaw;heading+=90;yield return new WaitForSecondsRealtime(.5f);
        Check(Mathf.Abs(Mathf.DeltaAngle(idleYaw,humanPlayer.Yaw))<.1,"camera orbit does not turn stationary player");
        humanReviewPose=true;
        try
        {
            for(int variant=0;variant<6;variant++)
            {
                DestroyHuman(humanPlayer);humanPlayer=CreateHuman(variant,1);humanPlayer.Root.transform.localPosition=new Vector3((float)(motion.X-originX),(float)(motion.Y-WorldGen.Presentation.TerrainMotion.EyeHeightM),(float)(motion.Z-originZ));
                humanEyeOffset=humanPlayer.Definition.EyeHeight-(float)WorldGen.Presentation.TerrainMotion.EyeHeightM;
                Check(humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>!r.name.Contains("suit")),"no modern clothing in variant "+variant);
                foreach(string role in humanPlayer.Definition.Roles)
                {
                    int clip=Array.IndexOf(humanPlayer.Definition.Roles,role);Check(clip>=0,"variant "+variant+" clip "+role);
                    for(int phase=0;phase<3;phase++)
                    {
                        PoseHumanCheck(clip,humanPlayer.Definition.Clips[clip].length*phase/3);
                        foreach(var renderer in humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            var mesh=new Mesh();renderer.BakeMesh(mesh);var bounds=mesh.bounds;
                            Check(float.IsFinite(bounds.center.x)&&float.IsFinite(bounds.center.y)&&float.IsFinite(bounds.center.z)&&bounds.size.magnitude<5,"bounded native skin "+variant+" "+role+" "+renderer.name+" phase "+phase);
                            Destroy(mesh);
                        }
                    }
                }
                heading=0;pitch=8;humanPlayer.Root.transform.localRotation=Quaternion.Euler(0,180,0);PoseHumanCheck(Array.IndexOf(humanPlayer.Definition.Roles,"Walk"),.4);
                TestHumanView(4);yield return null;PerformanceCapture(Path.Combine(output,"human-"+variant+"-walk-front.png"));
                heading=90;TestHumanView(4);yield return null;PerformanceCapture(Path.Combine(output,"human-"+variant+"-walk-side.png"));
                heading=0;humanPlayer.Root.transform.localRotation=Quaternion.Euler(0,180,0);TestHumanView(1.2);
                yield return null;
                view.transform.localPosition=humanPlayer.Root.transform.localPosition+new Vector3(0,humanPlayer.Definition.EyeHeight,-.65f);view.transform.localRotation=Quaternion.identity;
                PerformanceCapture(Path.Combine(output,"human-"+variant+"-eyes.png"));
                if(variant==0)foreach(string role in new[]{"Run","Sprint"})
                {
                    heading=90;TestHumanView(3);PoseHumanCheck(Array.IndexOf(humanPlayer.Definition.Roles,role),.25);
                    yield return null;PerformanceCapture(Path.Combine(output,"run-option-"+role.Replace(' ','-')+".png"));
                }
            }
            heading=0;pitch=72;humanPlayer.Root.transform.localRotation=Quaternion.identity;PoseHumanCheck(Array.IndexOf(humanPlayer.Definition.Roles,"Idle"),0);
            TestHumanView(0);yield return null;PerformanceCapture(Path.Combine(output,"human-first-person-no-body.png"));
            Check(humanHideBody,"first-person activates camera-only body mask");
            heading=0;pitch=8;humanPlayer.Root.transform.localRotation=Quaternion.Euler(0,180,0);TestHumanView(4);yield return null;
            humanReviewBodyMask=0;yield return null;PerformanceCapture(Path.Combine(output,"human-body-visible.png"));var full=LastCapturePixels;
            humanReviewBodyMask=1;yield return null;PerformanceCapture(Path.Combine(output,"human-body-hidden.png"));var hidden=LastCapturePixels;
            report.Add("Body mask global="+Shader.GetGlobalFloat("_WorldGenFirstPersonHumanId")+"; material ids="+string.Join(",",humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.sharedMaterial.GetFloat("_HumanViewId"))));
            Check(full.Zip(hidden,(a,b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)).Count(d=>d>8)>50,"own-camera mask actually hides rendered body pixels");
            var other=Instantiate(view);other.name="Human other-player camera check";other.enabled=false;
            var target=new RenderTexture(1280,800,24);other.targetTexture=target;other.transform.position=view.transform.position;other.transform.rotation=view.transform.rotation;
            var prior=RenderTexture.active;var texture=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try
            {
                other.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,800),0,0);texture.Apply();
                File.WriteAllBytes(Path.Combine(output,"human-other-camera-full-body.png"),texture.EncodeToPNG());
                Check(Shader.GetGlobalFloat("_WorldGenFirstPersonHumanId")==0,"other camera sees complete human, no mesh/visibility mutation");
            }
            finally{RenderTexture.active=prior;other.targetTexture=null;target.Release();Destroy(target);Destroy(texture);Destroy(other.gameObject);}
            humanHideBody=false;
            Check(humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>r.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.On),"first-person body and head keep real shadow casters");
            humanCamera.Restore(0,false);humanCamera.ToggleLock();humanCamera.Zoom(-1);Check(humanCamera.FirstPerson,"first-person zoom lock");
            humanCamera.ToggleLock();humanCamera.Zoom(-1);Check(!humanCamera.FirstPerson,"zoom unlock restores third person");
        }
        finally{humanReviewPose=false;humanHideBody=false;humanReviewBodyMask=-1;File.WriteAllLines(Path.Combine(output,"human-check.txt"),report);}
    }
    void PoseHumanCheck(int selected,double time)
    {
        for(int i=0;i<humanPlayer.Inputs.Length;i++)
        {humanPlayer.Mixer.SetInputWeight(i,i==selected?1:0);humanPlayer.Inputs[i].SetTime(time);humanPlayer.Inputs[i].SetSpeed(0);}
        humanPlayer.Graph.Evaluate(0);
    }
}
