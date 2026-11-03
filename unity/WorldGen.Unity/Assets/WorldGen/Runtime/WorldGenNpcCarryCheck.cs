using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    static IEnumerator CheckNpcCarrying(LocalTerrainExplorer owner,Camera camera,RenderTexture target,string output)
    {
        var ages=Resources.Load<WorldGenHumanAgeLibrary>("HumanAges/Library");camera.cullingMask=(1<<29)|(1<<30);
        foreach(int variant in new[]{0,1,4})
        {
            var parent=owner.CreateHuman(variant,700+variant,true);parent.ResidentId=700+variant;parent.Resident=new ForagerPersonSample{AgeYears=30};parent.Motor=new TerrainMotion();parent.Motor.Place(0,0,0);
            owner.livingPeople[parent.ResidentId]=parent;parent.Planet=new V(1,0,0);
            var age=ages.Find(owner.humanLibrary.People[variant].Variant,1);var infant=owner.CreateHuman(variant,800+variant,true,age);infant.ResidentId=800+variant;infant.Resident=new ForagerPersonSample{AgeYears=.5};infant.Routine=new HumanActivitySample{Partner=parent.ResidentId,Activity=HumanActivity.Carried};infant.Motor=new TerrainMotion();infant.Root.transform.localScale=Vector3.one*.65f;
            foreach(string role in new[]{"Social","SitGround","WorkKneeling","SleepSide"})
            {
                parent.Routine=new HumanActivitySample{Activity=role=="SleepSide"?HumanActivity.Sleep:HumanActivity.Childcare};
                for(int i=0;i<60;i++)
                {
                    owner.AnimateHuman(parent,0,false,false,1f/30);parent.Shared.Routine(role,i/30d,1f/30);parent.Shared.Carry(null,0,1f/30);parent.Graph.Evaluate(0);
                    parent.Root.transform.localPosition=Vector3.up*parent.Shared.Clips[role].GroundOffsetM;owner.UpdateCarriedHuman(infant,1f/30);
                }
                yield return null;
                if(Vector3.Distance(parent.Root.transform.localPosition,infant.Root.transform.localPosition)>1.5f||!infant.Root.activeSelf)throw new InvalidOperationException("Infant carrier loses its linked guardian");
                foreach(var skin in infant.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh=new Mesh();skin.BakeMesh(mesh,true);
                    if(mesh.vertices.Any(p=>skin.transform.TransformPoint(p).y<-.04f))throw new InvalidOperationException("Carried infant penetrates the ground in "+role);
                    Destroy(mesh);
                }
                foreach(int side in new[]{0,1})
                {
                    var focus=Vector3.up*.85f;camera.orthographic=true;camera.orthographicSize=1;camera.transform.position=focus+(side==0?new Vector3(0,.05f,4):new Vector3(4,.05f,0));camera.transform.LookAt(focus);
                    RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});var previous=RenderTexture.active;RenderTexture.active=target;
                    var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(output,"carrier-"+parent.Definition.Variant+"-"+role+"-"+side+".png"),texture.EncodeToPNG());Destroy(texture);RenderTexture.active=previous;
                }
            }
            owner.livingPeople.Remove(parent.ResidentId);owner.DestroyHuman(parent);owner.DestroyHuman(infant);yield return null;
        }
        Debug.Log("NPC linked infant carrier scenes PASS");
    }
}
