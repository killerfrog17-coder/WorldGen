using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    // A private derivative of the local avatar. Neither the world avatar nor its
    // original mesh is modified; other cameras/players still see the full person.
    HumanActor viewArms;
    Dictionary<string,Transform> viewArmBones;
    readonly List<Mesh> viewArmMeshes=new List<Mesh>();
    readonly List<SkinnedMeshRenderer> viewArmRenderers=new List<SkinnedMeshRenderer>();
    double viewReleaseAt=-100,viewGatherAt=-100,viewPunchAt=-100;
    bool viewReleaseLeft,viewArmsVisible;
    ItemPrimaryUse viewReleaseKind;
    Vector3 viewGatherTarget;
    GameObject viewNockedArrow;string viewNockId;
    Renderer[] viewNockRenderers=Array.Empty<Renderer>();
    Material inventoryBowStringMaterial;
    Vector3 BowDrawPoint(float draw)=>view.transform.TransformPoint(Vector3.Lerp(new Vector3(-.18f,-.15f,.35f),new Vector3(.08f,-.10f,.28f),draw));
    void CreateBowString(GameObject root,int layer)
    {
        if(inventoryBowStringMaterial==null){inventoryBowStringMaterial=new Material(Resources.Load<Shader>("WorldGenInventoryPreview"));inventoryBowStringMaterial.SetColor("_Color",new Color(.59f,.47f,.31f));}
        var obj=new GameObject("Bow string",typeof(LineRenderer));obj.layer=layer;obj.transform.SetParent(root.transform,false);
        var line=obj.GetComponent<LineRenderer>();line.sharedMaterial=inventoryBowStringMaterial;line.useWorldSpace=false;line.positionCount=3;line.widthMultiplier=.003f;line.shadowCastingMode=ShadowCastingMode.Off;
        line.SetPositions(new[]{new Vector3(.38f,0,0),new Vector3(.38f,.5f,0),new Vector3(.38f,1,0)});
    }
    void UpdateBowString(GameObject root,Possession item)
    {
        var line=root.GetComponentInChildren<LineRenderer>();if(line==null)return;
        if(humanHideBody&&itemCharge?.Weapon==item&&!itemCharge.Throw)
        {
            float draw=Mathf.Clamp01((float)((Time.unscaledTimeAsDouble-itemCharge.Started)/itemCharge.Duration));
            line.SetPosition(1,root.transform.InverseTransformPoint(BowDrawPoint(draw)));
        }
        else line.SetPosition(1,new Vector3(.38f,.5f,0));
    }
    bool SupportSpear(Possession item)=>item!=null&&ItemUseRules.Profile(item).Primary==ItemPrimaryUse.Thrust&&
        item.HeightM>=1.1&&
        (Inventory.AtHand(item.Place==PossessionPlace.LeftHand?PossessionPlace.RightHand:PossessionPlace.LeftHand)==null||item.Hands==2)&&
        !(itemCharge?.Throw??false);

    void EnsureViewArms()
    {
        if(viewArms!=null&&viewArms.Definition==humanPlayer.Definition)return;
        DisposeViewArms();viewArms=CreateHuman(Array.IndexOf(humanLibrary.People,humanPlayer.Definition),-100);
        viewArms.Root.name="Local camera arms";viewArms.Root.transform.SetParent(view.transform,false);
        viewArmBones=viewArms.Root.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
        foreach(var renderer in viewArms.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var source=renderer.sharedMesh;
            if(!source.isReadable){renderer.enabled=false;continue;}
            var armBones=new HashSet<int>();
            for(int i=0;i<renderer.bones.Length;i++)
            {
                var b=renderer.bones[i];if(b==null)continue;
                if(b.IsChildOf(viewArmBones["upperarm_r"])||b.IsChildOf(viewArmBones["upperarm_l"]))armBones.Add(i);
            }
            var weights=source.boneWeights;
            bool Arm(int i)
            {
                var w=weights[i];float n=(armBones.Contains(w.boneIndex0)?w.weight0:0)+(armBones.Contains(w.boneIndex1)?w.weight1:0)+
                    (armBones.Contains(w.boneIndex2)?w.weight2:0)+(armBones.Contains(w.boneIndex3)?w.weight3:0);
                return n>.55f;
            }
            var faces=new List<int>();
            if(weights.Length==source.vertexCount)
            {
                var all=source.triangles;for(int i=0;i<all.Length;i+=3)
                    if(Arm(all[i])&&Arm(all[i+1])&&Arm(all[i+2])){faces.Add(all[i]);faces.Add(all[i+1]);faces.Add(all[i+2]);}
            }
            if(faces.Count==0){renderer.enabled=false;continue;}
            var mesh=Instantiate(source);mesh.name="Private arms of "+source.name;mesh.subMeshCount=1;mesh.SetTriangles(faces,0);viewArmMeshes.Add(mesh);
            renderer.sharedMesh=mesh;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
            renderer.localBounds=new Bounds(Vector3.zero,Vector3.one*5);renderer.updateWhenOffscreen=true;
            var materials=renderer.sharedMaterials.Select(m=>new Material(m)).ToArray();
            foreach(var m in materials){m.SetFloat("_HumanViewId",-100);viewArms.OwnedMaterials.Add(m);}renderer.sharedMaterials=materials;
            viewArmRenderers.Add(renderer);renderer.enabled=false;
        }
        RenderPipelineManager.beginCameraRendering+=BeginViewArmsCamera;
        RenderPipelineManager.endCameraRendering+=EndViewArmsCamera;
    }
    void BeginViewArmsCamera(ScriptableRenderContext context,Camera camera)
    {foreach(var r in viewArmRenderers)if(r!=null)r.enabled=viewArmsVisible&&camera==view;foreach(var r in viewNockRenderers)if(r!=null)r.enabled=viewArmsVisible&&camera==view;}
    void EndViewArmsCamera(ScriptableRenderContext context,Camera camera)
    {foreach(var r in viewArmRenderers)if(r!=null)r.enabled=false;foreach(var r in viewNockRenderers)if(r!=null)r.enabled=false;}
    void ClearViewNock()
    {
        if(viewNockedArrow!=null)DestroyInventoryVisual(viewNockedArrow);viewNockedArrow=null;viewNockRenderers=Array.Empty<Renderer>();
        if(viewNockId!=null&&inventoryVisuals.TryGetValue(viewNockId,out var original))original.SetActive(true);viewNockId=null;
    }
    void UpdateViewNock()
    {
        var ammo=itemCharge?.Weapon?.Kind==PossessionKind.Bow?itemCharge.Ammo:null;
        if(ammo==null||ammo.Kind!=PossessionKind.Arrow&&ammo.Kind!=PossessionKind.UnfletchedArrow){ClearViewNock();return;}
        if(viewNockId!=ammo.Id)
        {
            ClearViewNock();viewNockId=ammo.Id;viewNockedArrow=CreatePossessionVisual(ammo,30);viewNockedArrow.name="Private nocked arrow";viewNockedArrow.transform.SetParent(view.transform,true);
            viewNockRenderers=viewNockedArrow.GetComponentsInChildren<Renderer>();foreach(var renderer in viewNockRenderers){renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.enabled=false;}
        }
        if(inventoryVisuals.TryGetValue(ammo.Id,out var source))source.SetActive(false);
        float draw=Mathf.Clamp01((float)((Time.unscaledTimeAsDouble-itemCharge.Started)/itemCharge.Duration));
        viewNockedArrow.transform.position=BowDrawPoint(draw);
        var bow=inventoryVisuals[itemCharge.Weapon.Id];var rest=bow.transform.position+bow.transform.up*(float)itemCharge.Weapon.HeightM*.55f;
        viewNockedArrow.transform.rotation=Quaternion.FromToRotation(Vector3.up,(rest-viewNockedArrow.transform.position).normalized);
    }

    void BeginGatherGesture(Vector3 worldPoint)
    {viewGatherAt=Time.unscaledTimeAsDouble;viewGatherTarget=view.transform.InverseTransformPoint(worldPoint);}
    static void ArmIk(Transform upper,Transform lower,Transform hand,Vector3 target,Vector3 hint)
    {
        float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,hand.position);
        var delta=target-upper.position;float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.001f,(a+b)*.995f);var axis=delta.normalized;
        target=upper.position+axis*d;var bend=Vector3.ProjectOnPlane(hint,axis).normalized;
        float along=(a*a+d*d-b*b)/(2*d),height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));var elbow=upper.position+axis*along+bend*height;
        upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
    }
    void PoseViewGrip(string side,Vector3 target,Vector3 gripAxis,float curl)
    {
        var upper=viewArmBones["upperarm_"+side];var lower=viewArmBones["lowerarm_"+side];var hand=viewArmBones["hand_"+side];
        var middle=viewArmBones["middle_01_"+side];var index=viewArmBones["index_01_"+side];var pinky=viewArmBones["pinky_01_"+side];
        // Orient the fingers across the shaft; the shaft passes across the palm.
        Vector3 across=(index.position-pinky.position).normalized,forward=(middle.position-hand.position).normalized;
        var old=Quaternion.LookRotation(forward,Vector3.Cross(forward,across));
        Vector3 shaft=gripAxis.normalized,finger=Vector3.ProjectOnPlane(view.transform.forward*.7f+view.transform.right*(side=="r"?-.8f:.8f)-view.transform.up*.25f,shaft).normalized;
        var desired=Quaternion.LookRotation(finger,Vector3.Cross(finger,shaft));
        Quaternion wrist=desired*Quaternion.Inverse(old)*hand.rotation;
        // Hand origin is at the wrist: offset the palm centre to the actual grip.
        float palm=Vector3.Distance(hand.position,middle.position)*.65f;
        var palmDirection=Vector3.Cross(finger,shaft)*(side=="r"?1:-1);var wristTarget=target-finger*palm-palmDirection*.022f;
        ArmIk(upper,lower,hand,wristTarget,view.transform.TransformDirection(new Vector3(side=="r"?1:-1,-1,-.35f)));
        hand.rotation=wrist;
        foreach(string digit in new[]{"index","middle","ring","pinky"})
        {
            var first=viewArmBones[digit+"_01_"+side];var second=viewArmBones[digit+"_02_"+side];
            Vector3 curlAxis=shaft*(side=="r"?-1:1);
            foreach(string segment in new[]{"01","02","03"})if(viewArmBones.TryGetValue(digit+"_"+segment+"_"+side,out var bone))
                bone.rotation=Quaternion.AngleAxis((segment=="01"?48:segment=="02"?65:40)*curl,curlAxis)*bone.rotation;
        }
        if(viewArmBones.TryGetValue("thumb_01_"+side,out var thumb))thumb.rotation=Quaternion.AngleAxis((side=="r"?-26:26)*curl,hand.forward)*thumb.rotation;
    }
    void UpdateViewArms()
    {
        if(humanPlayer==null||!inventoryInitialized)return;
        bool visible=humanHideBody&&!DeathVisible&&!Spectating&&!inventoryOpen&&!campSleeping&&!campResting;
        if(!visible){viewArmsVisible=false;if(viewArms!=null)viewArms.Root.SetActive(false);return;}
        EnsureViewArms();viewArms.Root.SetActive(true);viewArmsVisible=true;
        viewArms.Root.transform.localPosition=new Vector3(0,-viewArms.Definition.EyeHeight-.035f,.10f);viewArms.Root.transform.localRotation=Quaternion.identity;
        AnimateHuman(viewArms,0,false,false,Math.Min(.1f,Time.unscaledDeltaTime));
        var right=Inventory.AtHand(PossessionPlace.RightHand);var left=Inventory.AtHand(PossessionPlace.LeftHand);
        var main=right??left;bool supporting=SupportSpear(main);
        double now=Time.unscaledTimeAsDouble;
        foreach(string side in new[]{"r","l"})
        {
            bool isRight=side=="r";var item=isRight?right:left;Vector3 target=view.transform.TransformPoint(new Vector3(isRight?.23f:-.23f,-.65f,.25f));Vector3 axis=view.transform.up;float curl=.12f;
            if(item!=null&&inventoryVisuals.TryGetValue(item.Id,out var held))
            {
                axis=held.transform.up;target=held.transform.position+axis*(float)item.HeightM*.35f;curl=.9f;
                if(item.Kind==PossessionKind.Bow)
                {
                    target=held.transform.position+axis*(float)item.HeightM*.5f;
                    if(!isRight){target+=view.transform.right*.02f;}
                }
                else if(!WoodCrafting.Wood(item)&&PortableTechnology.Item(item.Kind)?.Long!=true)target=held.transform.position+held.transform.up*(float)item.HeightM*.18f;
                if(item.Hands==2)target+=view.transform.right*(isRight?1:-1)*(float)item.WidthM*.32f;
            }
            if(main?.Kind==PossessionKind.Bow&&inventoryVisuals.TryGetValue(main.Id,out var bow))
            {
                float draw=itemCharge?.Weapon==main?Mathf.Clamp01((float)((now-itemCharge.Started)/itemCharge.Duration)):0;
                axis=bow.transform.up;
                target=isRight?BowDrawPoint(draw):bow.transform.position+axis*(float)main.HeightM*.5f;
                curl=isRight?.55f:.9f;
            }
            else if(supporting&&main!=null&&(main.Place==PossessionPlace.LeftHand?isRight:!isRight)&&inventoryVisuals.TryGetValue(main.Id,out var spear))
            {axis=spear.transform.up;float support=.10f;
                if(viewItemActions.TryGetValue(main.Id,out var stroke)){float phase=Mathf.Clamp01((float)((now-stroke.At)/Math.Max(.2,stroke.Duration)));support*=1-.9f*Mathf.Sin(phase*Mathf.PI);}
                target=spear.transform.position+axis*((float)main.HeightM*.35f+support);curl=.9f;}
            if(combatGuarding&&main==null){target=view.transform.TransformPoint(new Vector3(isRight?.15f:-.15f,-.1f,.4f));curl=.95f;}
            if(now-viewReleaseAt<.5&&viewReleaseLeft==!isRight)
            {
                float t=(float)((now-viewReleaseAt)/.5);
                target=viewReleaseKind==ItemPrimaryUse.Bow?view.transform.TransformPoint(Vector3.Lerp(new Vector3(.16f,-.1f,.28f),new Vector3(.25f,-.3f,.32f),t)):
                    view.transform.TransformPoint(Vector3.Lerp(new Vector3(isRight?.16f:-.16f,-.12f,.64f),new Vector3(isRight?.23f:-.23f,-.65f,.25f),t*t));curl=.05f;
            }
            if(now-viewGatherAt<1.05&&isRight)
            {float t=(float)((now-viewGatherAt)/1.05),reach=Mathf.Sin(t*Mathf.PI);var point=new Vector3(Mathf.Clamp(viewGatherTarget.x,-.3f,.3f),Mathf.Clamp(viewGatherTarget.y,-.4f,.1f),.6f);target=Vector3.Lerp(target,view.transform.TransformPoint(point),reach);curl=t<.55f?.1f:.8f;}
            if(now-viewPunchAt<.5&&isRight&&item==null)
            {float t=(float)((now-viewPunchAt)/.5),strike=Mathf.Sin(t*Mathf.PI);target=view.transform.TransformPoint(Vector3.Lerp(new Vector3(.2f,-.4f,.3f),new Vector3(.08f,-.12f,.65f),strike));curl=1;}
            PoseViewGrip(side,target,axis,curl);
        }
        UpdateViewNock();
    }
    void DisposeViewArms()
    {
        RenderPipelineManager.beginCameraRendering-=BeginViewArmsCamera;RenderPipelineManager.endCameraRendering-=EndViewArmsCamera;
        ClearViewNock();
        viewArmsVisible=false;viewArmRenderers.Clear();if(viewArms!=null)DestroyHuman(viewArms);viewArms=null;viewArmBones=null;
        foreach(var mesh in viewArmMeshes)if(mesh!=null)Destroy(mesh);viewArmMeshes.Clear();
    }
}
