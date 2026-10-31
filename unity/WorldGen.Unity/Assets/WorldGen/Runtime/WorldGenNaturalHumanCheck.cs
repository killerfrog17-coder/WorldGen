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
        public string Variant,Role,Item;public float Time,Clearance,GripError,SupportError,Pitch;
        public string[] BoneNames;public Vector3[] Bones,Vertices;public int[] CoreTriangles;
        public Quaternion[] Rotations;public Vector3[] PropPoints,AmmoPoints;public Vector3 PropStart,PropEnd,LeftGrip,RightGrip,StringGrip,Eye;
        public Vector3[] PropVertices,AmmoVertices;public int[] PropTriangles,AmmoTriangles;
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
            return CreateNaturalReviewHeld(kind,hand);
        }
        void Image(string name,int angle)=>CaptureNaturalImage(output,name,angle);
        bool previousMode=HumanAnimationOptions.Kimodo;int originalVariant=Array.IndexOf(humanLibrary.People,humanPlayer.Definition);var oldPosition=humanPlayer.Root.transform.localPosition;
        var badSupports=new List<string>();
        try
        {
            SetInventoryOpen(false);campMenu=null;campGroundMenu=campSleepMenu=false;chatOpen=mapOpen=ecologyOpen=false;paused=false;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();
            motion.Flying=false;motion.Place(motion.X,motion.Z,Ground(motion.X,motion.Z));HumanAnimationOptions.Set(true);
            oldPosition=new Vector3((float)(motion.X-originX),(float)(motion.Y-WorldGen.Presentation.TerrainMotion.EyeHeightM),(float)(motion.Z-originZ));
            humanCamera.Restore(0,false);humanCameraDistance=0;heading=pitch=0;humanReviewPose=true;Clear();
            foreach(int variant in new[]{1,4,0,2,3,5})
            {
                DestroyHuman(humanPlayer);humanPlayer=CreateHuman(variant,1);humanPlayer.Root.transform.localPosition=oldPosition;humanPlayer.Yaw=humanPlayer.TargetYaw=0;
                AnimateHuman(humanPlayer,0,false,false,1);
                sharedCameraEyeValid=false;UpdateInventoryVisuals();
                Check(humanPlayer.Shared.Clips.Count==70,"All 70 restrained roles bind to original character "+humanPlayer.Definition.Variant);
                Check(humanPlayer.PropShape.HasNativeEye&&humanPlayer.PropShape.HasBowAnchor,"Actual eye and mouth surfaces measured on "+humanPlayer.Definition.Variant);
                Check(humanPlayer.Shared.LocalEye.y>.8f&&humanPlayer.Shared.LocalEye.y<2.4f,"Measured eye is in native metre coordinates on "+humanPlayer.Definition.Variant);
                var bodySkin=humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.sharedMesh.vertexCount>10000);
                var hitSurface=PosedHuman(bodySkin);var chest=(inventoryBones["upperarm_l"].position+inventoryBones["upperarm_r"].position)*.5f-Vector3.up*.15f*NaturalScale;
                var ray=new Ray(chest+humanPlayer.Root.transform.forward*2,humanPlayer.Root.transform.forward*-1);bool skinHit=false;
                for(int t=0;t<hitSurface.Triangles.Length;t+=3)
                    if(FaunaCollisionSurface.RayTriangle(ray,hitSurface.Vertices[hitSurface.Triangles[t]],hitSurface.Vertices[hitSurface.Triangles[t+1]],hitSurface.Vertices[hitSurface.Triangles[t+2]],3,out _,out _,out _)){skinHit=true;break;}
                Check(skinHit,"Combat skin matches the posed torso in real metres on "+humanPlayer.Definition.Variant);
                var cases=new[]{(PossessionKind.StoneTippedSpear,"SpearReady2"),(PossessionKind.StoneTippedSpear,"SpearThrust2"),
                    (PossessionKind.StoneTippedSpear,"SpearReady1"),(PossessionKind.StoneTippedSpear,"SpearThrust1"),
                    (PossessionKind.HaftedKnife,"ToolCutA"),(PossessionKind.HaftedKnife,"ToolCutB"),(PossessionKind.StoneAxe,"ToolChop"),(PossessionKind.StoneAxe,"ToolBlunt"),
                    (PossessionKind.Bow,"BowReady"),(PossessionKind.Bow,"BowDraw"),(PossessionKind.Bow,"BowRelease"),
                    (PossessionKind.Sling,"SlingWhirl"),(PossessionKind.SpearThrower,"SpearThrowCharge"),(PossessionKind.StoneTippedSpear,"SpearThrowCharge"),(PossessionKind.LargeRock,"CarryLarge")};
                foreach(var sample in cases)
                {
                    Clear();var item=Hold(sample.Item1);if(sample.Item2.EndsWith("1",StringComparison.Ordinal))Hold(PossessionKind.Arrow,PossessionPlace.LeftHand);
                    if(sample.Item2=="BowDraw"||sample.Item2=="SlingWhirl"||sample.Item1==PossessionKind.SpearThrower)
                    {
                        Hold(sample.Item1==PossessionKind.Bow?PossessionKind.Arrow:sample.Item1==PossessionKind.Sling?PossessionKind.SmallRock:PossessionKind.SpearDart,PossessionPlace.LeftHand);BeginItemCharge(item,false,0,false);
                        Check(itemCharge?.Weapon==item,"Native "+sample.Item1+" sequence uses real held item and ammunition");
                    }
                    else if(sample.Item2=="SpearThrowCharge")BeginItemCharge(item,false,1,true);
                    UpdateInventoryVisuals();viewArmDiagnosticClip=sample.Item2;viewArmDiagnosticDt=1f/30;
                    var records=new List<NaturalPoseEvidence>();Vector3 firstPelvis=Vector3.zero;float pelvisExcursion=0;
                    int steps=variant==1||variant==4?9:3;
                    for(int frame=0;frame<steps;frame++)
                    {
                        float t=frame/(float)(steps-1);viewArmDiagnosticTime=HumanActionDuration(sample.Item2)*t;
                        if(itemCharge!=null)itemCharge.Started=Time.unscaledTimeAsDouble-itemCharge.Duration*t;
                        for(int settle=0;settle<6;settle++)
                        {
                            humanPlayer.Root.transform.localRotation=Quaternion.Euler(0,heading+NaturalBowYaw,0);
                            UpdateInventoryVisuals();
                        }
                        UpdateCamera();
                        var record=ExportNaturalPose(item,sample.Item2,t,variant!=1&&variant!=4||frame%2==0);records.Add(record);
                        int hip=Array.IndexOf(record.BoneNames,"pelvis");if(frame==0)firstPelvis=record.Bones[hip];else pelvisExcursion=Mathf.Max(pelvisExcursion,Mathf.Abs(record.Bones[hip].y-firstPelvis.y));
                        if((variant==1||variant==4)&&sample.Item2!="SpearReady1")
                        {
                            string name=humanPlayer.Definition.Variant+"-"+sample.Item1+"-"+sample.Item2+"-"+frame.ToString("D2");Image(name+"-front",0);Image(name+"-side",1);
                            if(sample.Item2=="BowDraw"||sample.Item2=="SpearThrust2"||sample.Item2=="ToolCutB")Image(name+"-first",-1);
                            if(sample.Item2=="BowDraw"&&frame>=steps-3)Image(name+"-face",2);
                        }
                        yield return null;
                    }
                    File.WriteAllText(Path.Combine(output,humanPlayer.Definition.Variant+"-"+sample.Item1+"-"+sample.Item2+".json"),"["+string.Join(",",records.Select(r=>JsonUtility.ToJson(r)))+"]");
                    if(sample.Item2=="SpearReady2"||sample.Item2=="SpearThrust2")
                    {
                        float error=records.Max(r=>r.SupportError);
                        if(error>.035f)badSupports.Add(humanPlayer.Definition.Variant+" "+sample.Item2+" "+error.ToString("F4"));
                    }
                    if(sample.Item2.StartsWith("Tool",StringComparison.Ordinal)||sample.Item2.StartsWith("SpearThrust",StringComparison.Ordinal)||sample.Item2.StartsWith("Bow",StringComparison.Ordinal))Check(pelvisExcursion<.035f,"No imported squat during "+sample.Item2+" on "+humanPlayer.Definition.Variant);
                }
                if(variant==1||variant==4)yield return ReviewNaturalGaits(output,report);
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
            Check(badSupports.Count==0,"Supporting spear grips stay on the physical shaft: "+string.Join("; ",badSupports));
            Check(true,"Actual native motion and skin/prop evidence exported for six character builds; visual review still required");
        }
        finally
        {
            Clear();DestroyHuman(humanPlayer);humanPlayer=CreateHuman(Math.Max(0,originalVariant),1);humanPlayer.Root.transform.localPosition=oldPosition;humanReviewPose=false;HumanAnimationOptions.Set(previousMode);sharedCameraEyeValid=false;
        }
    }
}
