using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    [Serializable] sealed class NaturalPoseEvidence
    {
        public string Variant,Role,Item;public float Time,Clearance,GripError,Pitch;
        public string[] BoneNames;public Vector3[] Bones,Vertices;public int[] CoreTriangles;
        public Quaternion[] Rotations;public Vector3[] PropPoints;public Vector3 PropStart,PropEnd,LeftGrip,RightGrip;
    }
    public IEnumerator CheckNaturalHumanFlow(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("NATURAL HUMAN "+text);report.Add("PASS "+text);Debug.Log("NATURAL HUMAN PASS "+text);}
        void Clear()
        {
            CancelItemCharge();Array.Clear(pendingMelee,0,pendingMelee.Length);viewItemActions.Clear();viewReleaseAt=viewGatherAt=viewPunchAt=viewConsumeAt=-100;
            viewArmDiagnosticClip=null;viewArmDiagnosticDt=-1;sharedBowItem=null;sharedBowRaise=sharedBowDraw=0;
            ClearSharedAmmo();RestoreSharedSlings();ClearInventoryPhysics();foreach(var root in inventoryVisuals.Values.ToArray())DestroyInventoryVisual(root);inventoryVisuals.Clear();inventoryVisualPlaces.Clear();Inventory.Items.Clear();Inventory.Crafts.Clear();
        }
        Possession Hold(PossessionKind kind,PossessionPlace hand=PossessionPlace.RightHand)
        {
            var item=PortableTechnology.Create(kind,Position);Inventory.Items.Add(item);
            if(!Inventory.Move(item.Id,hand,"",0,0,false,out string reason))throw new InvalidOperationException("Natural fixture: "+reason);return item;
        }
        void Image(string name,int angle)
        {
            var p=view.transform.position;var r=view.transform.rotation;int mask=humanReviewBodyMask;
            if(angle>=0)
            {
                humanReviewBodyMask=0;var body=humanPlayer.Root.transform;
                Vector3 offset=angle==0?new Vector3(1.9f,1.5f,3.3f):new Vector3(3.5f,1.5f,.2f);
                view.transform.position=body.TransformPoint(offset);view.transform.LookAt(body.position+Vector3.up*.85f*NaturalScale);
            }
            var previous=view.targetTexture;var active=RenderTexture.active;var target=new RenderTexture(768,768,24);target.Create();view.targetTexture=target;
            view.Render();RenderTexture.active=target;var image=new Texture2D(768,768,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());RenderTexture.active=active;view.targetTexture=previous;target.Release();Destroy(target);Destroy(image);
            view.transform.SetPositionAndRotation(p,r);humanReviewBodyMask=mask;
        }
        NaturalPoseEvidence Export(Possession item,string role,float time,bool surface)
        {
            var body=humanPlayer.Root.transform;var names=inventoryBones.Keys.Where(k=>k=="pelvis"||k=="head"||k.StartsWith("spine_",StringComparison.Ordinal)||k.StartsWith("upperarm_",StringComparison.Ordinal)||k.StartsWith("lowerarm_",StringComparison.Ordinal)||k.StartsWith("hand_",StringComparison.Ordinal)||k.StartsWith("thigh_",StringComparison.Ordinal)||k.StartsWith("calf_",StringComparison.Ordinal)||k.StartsWith("foot_",StringComparison.Ordinal)).OrderBy(k=>k).ToArray();
            var result=new NaturalPoseEvidence{Variant=humanPlayer.Definition.Variant,Role=role,Time=time,Pitch=pitch,BoneNames=names,
                Bones=names.Select(k=>body.InverseTransformPoint(inventoryBones[k].position)).ToArray(),Rotations=names.Select(k=>Quaternion.Inverse(body.rotation)*inventoryBones[k].rotation).ToArray(),
                LeftGrip=body.InverseTransformPoint(SharedGripPosition("l")),RightGrip=body.InverseTransformPoint(SharedGripPosition("r")),Item=item?.Kind.ToString()??"none"};
            if(item!=null&&inventoryVisuals.TryGetValue(item.Id,out var prop))
            {
                Vector3 start=prop.transform.position,end=start+prop.transform.up*(float)item.HeightM;
                result.PropStart=body.InverseTransformPoint(start);result.PropEnd=body.InverseTransformPoint(end);
                if(item.Kind==PossessionKind.Bow)
                {
                    result.PropPoints=Enumerable.Range(0,17).Select(i=>{float t=i/16f;return body.InverseTransformPoint(prop.transform.TransformPoint(new Vector3(.38f*Mathf.Pow(t*2-1,2),t,0)));}).ToArray();
                }
                else result.PropPoints=new[]{result.PropStart,result.PropEnd};
                result.Clearance=NaturalBodyClearance(start,end,Mathf.Max(.009f*NaturalScale,(float)item.WidthM*.1f));
                string side=NaturalSide(item);float fraction=StoneCrafting.Rock(item)||item.Kind==PossessionKind.Bow?.5f:WoodCrafting.Wood(item)||PortableTechnology.Item(item.Kind)?.Long==true?.35f:.18f;
                result.GripError=Vector3.Distance(SharedGripPosition(side),Vector3.Lerp(start,end,fraction));
            }
            if(surface)
            {
                var vertices=new List<Vector3>();var triangles=new List<int>();var mesh=new Mesh();
                foreach(var renderer in body.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var source=renderer.sharedMesh;if(source==null||source.vertexCount<10000)continue;
                    renderer.BakeMesh(mesh);var weights=source.boneWeights;var bind=renderer.bones;var v=mesh.vertices;int offset=vertices.Count;
                    vertices.AddRange(v.Select(p=>body.InverseTransformPoint(renderer.transform.TransformPoint(p))));
                    bool Core(int index)
                    {
                        var w=weights[index];float sum=0;
                        void Add(int b,float a){string name=bind[b].name;if(name=="pelvis"||name=="head"||name.StartsWith("spine_",StringComparison.Ordinal)||name.StartsWith("thigh_",StringComparison.Ordinal))sum+=a;}
                        Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);return sum>.65f;
                    }
                    var t=mesh.triangles;for(int i=0;i<t.Length;i+=3)if(Core(t[i])&&Core(t[i+1])&&Core(t[i+2])){triangles.Add(offset+t[i]);triangles.Add(offset+t[i+1]);triangles.Add(offset+t[i+2]);}
                }
                Destroy(mesh);var used=triangles.Distinct().OrderBy(i=>i).ToArray();var remap=new Dictionary<int,int>();for(int i=0;i<used.Length;i++)remap[used[i]]=i;
                result.Vertices=used.Select(i=>vertices[i]).ToArray();result.CoreTriangles=triangles.Select(i=>remap[i]).ToArray();
            }
            return result;
        }
        bool previousMode=HumanAnimationOptions.Kimodo;int originalVariant=Array.IndexOf(humanLibrary.People,humanPlayer.Definition);var oldPosition=humanPlayer.Root.transform.localPosition;
        try
        {
            SetInventoryOpen(false);campMenu=null;campGroundMenu=campSleepMenu=false;chatOpen=mapOpen=ecologyOpen=false;paused=false;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();
            motion.Flying=false;motion.Place(motion.X,motion.Z,Ground(motion.X,motion.Z));HumanAnimationOptions.Set(true);
            humanCamera.Restore(0,false);humanCameraDistance=0;heading=pitch=0;humanReviewPose=true;Clear();
            foreach(int variant in new[]{1,4,0,2,3,5})
            {
                DestroyHuman(humanPlayer);humanPlayer=CreateHuman(variant,1);humanPlayer.Root.transform.localPosition=oldPosition;humanPlayer.Yaw=humanPlayer.TargetYaw=0;
                AnimateHuman(humanPlayer,0,false,false,1);
                sharedCameraEyeValid=false;UpdateInventoryVisuals();
                Check(humanPlayer.Shared.Clips.Count==70,"All 70 restrained roles bind to original character "+humanPlayer.Definition.Variant);
                var cases=new[]{(PossessionKind.StoneTippedSpear,"SpearReady2"),(PossessionKind.StoneTippedSpear,"SpearThrust2"),
                    (PossessionKind.StoneTippedSpear,"SpearReady1"),(PossessionKind.StoneTippedSpear,"SpearThrust1"),
                    (PossessionKind.HaftedKnife,"ToolCutA"),(PossessionKind.HaftedKnife,"ToolCutB"),(PossessionKind.StoneAxe,"ToolChop"),(PossessionKind.StoneAxe,"ToolBlunt"),
                    (PossessionKind.Bow,"BowReady"),(PossessionKind.Bow,"BowDraw"),(PossessionKind.Bow,"BowRelease"),
                    (PossessionKind.Sling,"SlingWhirl"),(PossessionKind.SpearThrower,"SpearThrowCharge"),(PossessionKind.StoneTippedSpear,"SpearThrowCharge")};
                foreach(var sample in cases)
                {
                    Clear();var item=Hold(sample.Item1);if(sample.Item2.EndsWith("1",StringComparison.Ordinal))Hold(PossessionKind.Arrow,PossessionPlace.LeftHand);
                    UpdateInventoryVisuals();viewArmDiagnosticClip=sample.Item2;viewArmDiagnosticDt=1f/30;
                    var records=new List<NaturalPoseEvidence>();Vector3 firstPelvis=Vector3.zero;float pelvisExcursion=0;
                    int steps=variant==1||variant==4?9:3;
                    for(int frame=0;frame<steps;frame++)
                    {
                        float t=frame/(float)(steps-1);viewArmDiagnosticTime=HumanActionDuration(sample.Item2)*t;
                        for(int settle=0;settle<6;settle++)
                        {
                            humanPlayer.Root.transform.localRotation=Quaternion.Euler(0,heading+NaturalBowYaw,0);
                            UpdateInventoryVisuals();
                        }
                        UpdateCamera();
                        var record=Export(item,sample.Item2,t,(variant==1||variant==4)&&frame%2==0);records.Add(record);
                        int hip=Array.IndexOf(record.BoneNames,"pelvis");if(frame==0)firstPelvis=record.Bones[hip];else pelvisExcursion=Mathf.Max(pelvisExcursion,Mathf.Abs(record.Bones[hip].y-firstPelvis.y));
                        if((variant==1||variant==4)&&sample.Item2!="SpearReady1")
                        {
                            string name=humanPlayer.Definition.Variant+"-"+sample.Item1+"-"+sample.Item2+"-"+frame.ToString("D2");Image(name+"-front",0);Image(name+"-side",1);
                            if(sample.Item2=="BowDraw"||sample.Item2=="SpearThrust2"||sample.Item2=="ToolCutB")Image(name+"-first",-1);
                        }
                        yield return null;
                    }
                    File.WriteAllText(Path.Combine(output,humanPlayer.Definition.Variant+"-"+sample.Item1+"-"+sample.Item2+".json"),"["+string.Join(",",records.Select(r=>JsonUtility.ToJson(r)))+"]");
                    if(sample.Item2.StartsWith("Tool",StringComparison.Ordinal)||sample.Item2.StartsWith("SpearThrust",StringComparison.Ordinal)||sample.Item2.StartsWith("Bow",StringComparison.Ordinal))Check(pelvisExcursion<.035f,"No imported squat during "+sample.Item2+" on "+humanPlayer.Definition.Variant);
                }
                Clear();var spear=Hold(PossessionKind.StoneTippedSpear);viewArmDiagnosticClip="SpearReady2";viewArmDiagnosticTime=.4;viewArmDiagnosticDt=.1f;
                for(int i=0;i<6;i++)UpdateInventoryVisuals();var carried=Quaternion.Inverse(humanPlayer.Root.transform.rotation)*SharedSpearDirection;var grip=humanPlayer.Root.transform.InverseTransformPoint(SharedGripPosition("r"));
                foreach(float look in new[]{-60f,60f,0f})
                {
                    pitch=look;for(int i=0;i<6;i++)UpdateInventoryVisuals();UpdateCamera();
                    Check(Vector3.Angle(carried,Quaternion.Inverse(humanPlayer.Root.transform.rotation)*SharedSpearDirection)<.1f,"Resting spear ignores look pitch "+look+" on "+humanPlayer.Definition.Variant);
                    Check(Vector3.Distance(grip,humanPlayer.Root.transform.InverseTransformPoint(SharedGripPosition("r")))<.01f,"Resting spear grip stays body-relative on "+humanPlayer.Definition.Variant);
                }
                pitch=0;Clear();yield return null;
            }
            Check(true,"Actual native motion and skin/prop evidence exported for six character builds; visual review still required");
        }
        finally
        {
            Clear();DestroyHuman(humanPlayer);humanPlayer=CreateHuman(Math.Max(0,originalVariant),1);humanPlayer.Root.transform.localPosition=oldPosition;humanReviewPose=false;HumanAnimationOptions.Set(previousMode);sharedCameraEyeValid=false;
        }
    }
}
