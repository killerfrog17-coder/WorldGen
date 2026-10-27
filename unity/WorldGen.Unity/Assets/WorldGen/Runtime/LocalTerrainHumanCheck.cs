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
        Check(ExecuteExplorerCommand("/humans clear").Success,"clear NPC command");
        Check(ExecuteExplorerCommand("/spawn human 3").Success&&humanNpcs.Count==3,"spawn human alias and count");
        Check(!ExecuteExplorerCommand("/humans 500").Success,"bounded NPC population");
        var first=humanNpcs[0].Root.transform.localPosition;yield return new WaitForSecondsRealtime(8);
        Check(humanNpcs.Any(h=>h.Speed>.05f)||(humanNpcs[0].Root.transform.localPosition-first).sqrMagnitude>.01,"demo humans move with live clips");
        humanReviewPose=true;
        try
        {
            for(int variant=0;variant<6;variant++)
            {
                DestroyHuman(humanPlayer);humanPlayer=CreateHuman(variant,1);humanPlayer.Root.transform.localPosition=new Vector3((float)(motion.X-originX),(float)(motion.Y-WorldGen.Presentation.TerrainMotion.EyeHeightM),(float)(motion.Z-originZ));
                humanEyeOffset=humanPlayer.Definition.EyeHeight-(float)WorldGen.Presentation.TerrainMotion.EyeHeightM;
                foreach(string role in new[]{"Idle","Walk","Run","Sprint","Crouch","Jump","Swim"})
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
            }
            heading=0;pitch=72;humanPlayer.Root.transform.localRotation=Quaternion.identity;PoseHumanCheck(Array.IndexOf(humanPlayer.Definition.Roles,"Idle"),0);
            TestHumanView(0);yield return null;PerformanceCapture(Path.Combine(output,"human-first-person-body.png"));
            Check(humanHideHead,"first-person activates camera-only head mask");
            heading=0;pitch=8;humanPlayer.Root.transform.localRotation=Quaternion.Euler(0,180,0);TestHumanView(4);yield return null;
            humanReviewHeadMask=0;yield return null;PerformanceCapture(Path.Combine(output,"human-head-visible.png"));var full=LastCapturePixels;
            humanReviewHeadMask=1;yield return null;PerformanceCapture(Path.Combine(output,"human-head-hidden.png"));var hidden=LastCapturePixels;
            report.Add("Head mask global="+Shader.GetGlobalFloat("_WorldGenFirstPersonHumanId")+"; material ids="+string.Join(",",humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.sharedMaterial.GetFloat("_HumanViewId"))));
            Check(full.Zip(hidden,(a,b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)).Count(d=>d>8)>50,"own-camera mask actually changes rendered head pixels");
            var other=Instantiate(view);other.name="Human other-player camera check";other.enabled=false;
            var target=new RenderTexture(1280,800,24);other.targetTexture=target;other.transform.position=view.transform.position;other.transform.rotation=view.transform.rotation;
            var prior=RenderTexture.active;var texture=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try
            {
                other.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,800),0,0);texture.Apply();
                File.WriteAllBytes(Path.Combine(output,"human-other-camera-full-head.png"),texture.EncodeToPNG());
                Check(Shader.GetGlobalFloat("_WorldGenFirstPersonHumanId")==0,"other camera sees complete human, no mesh/visibility mutation");
            }
            finally{RenderTexture.active=prior;other.targetTexture=null;target.Release();Destroy(target);Destroy(texture);Destroy(other.gameObject);}
            humanHideHead=false;
            Check(humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>r.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.On),"first-person body and head keep real shadow casters");
            humanCamera.Restore(0,false);humanCamera.ToggleLock();humanCamera.Zoom(-1);Check(humanCamera.FirstPerson,"first-person zoom lock");
            humanCamera.ToggleLock();humanCamera.Zoom(-1);Check(!humanCamera.FirstPerson,"zoom unlock restores third person");
        }
        finally{humanReviewPose=false;humanHideHead=false;humanReviewHeadMask=-1;File.WriteAllLines(Path.Combine(output,"human-check.txt"),report);}
    }
    void PoseHumanCheck(int selected,double time)
    {
        for(int i=0;i<humanPlayer.Inputs.Length;i++)
        {humanPlayer.Mixer.SetInputWeight(i,i==selected?1:0);humanPlayer.Inputs[i].SetTime(time);humanPlayer.Inputs[i].SetSpeed(0);}
        humanPlayer.Graph.Evaluate(0);
    }
}
