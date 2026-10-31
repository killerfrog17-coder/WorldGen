using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    void ClearNaturalReviewHands()
    {
        CancelItemCharge();Array.Clear(pendingMelee,0,pendingMelee.Length);viewItemActions.Clear();viewReleaseAt=viewGatherAt=viewPunchAt=viewConsumeAt=-100;
        viewArmDiagnosticClip=null;sharedBowItem=null;sharedBowRaise=sharedBowDraw=0;ClearSharedAmmo();RestoreSharedSlings();Inventory.Items.Clear();UpdateInventoryVisuals();
    }
    IEnumerator ReviewNaturalGaits(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("NATURAL GAIT "+text);report.Add("PASS "+text);Debug.Log("NATURAL GAIT PASS "+text);}
        var body=humanPlayer.Root.transform;
        foreach(var kind in new[]{PossessionKind.StoneTippedSpear,PossessionKind.HaftedKnife,PossessionKind.Bow,PossessionKind.StoneAxe,PossessionKind.Sling})
        {
            ClearNaturalReviewHands();var item=CreateNaturalReviewHeld(kind);viewArmDiagnosticDt=1f/30;pitch=0;
            foreach(var gait in new[]{("Idle",0f,false),("Walk",1.5f,false),("Run",3.4f,false),("Crouch",.6f,true),("CrouchIdle",0f,true)})
            {
                var records=new List<NaturalPoseEvidence>();
                for(int frame=0;frame<30;frame++)
                {
                    body.localRotation=Quaternion.identity;humanPlayer.Yaw=humanPlayer.TargetYaw=heading=0;
                    AnimateHuman(humanPlayer,gait.Item2,false,gait.Item3,1f/30);UpdateInventoryVisuals();UpdateCamera();
                    if(frame==9||frame==19||frame==29)
                    {
                        // Hold this exact pose through one normal render before a manual capture.
                        yield return null;
                        var record=ExportNaturalPose(item,"Carry-"+gait.Item1,frame/30f,true);records.Add(record);
                        string name=humanPlayer.Definition.Variant+"-"+kind+"-"+gait.Item1+"-"+frame;
                        if(frame==19){CaptureNaturalImage(output,name+"-front",0);CaptureNaturalImage(output,name+"-side",1);}
                    }
                    yield return null;
                }
                string key=humanPlayer.Definition.Variant+"-"+kind+"-carry-"+gait.Item1;
                WriteNaturalPoses(Path.Combine(output,key+".json"),records);
                Check(records.All(r=>r.PropVertices!=null&&r.PropVertices.Length>0&&r.Vertices.Length>1000),"Actual prop and skin exported for "+key);
                Check(records.All(r=>r.PropVertices.Min(p=>p.y)>-.035f),"Held "+kind+" clears the floor during "+gait.Item1+" on "+humanPlayer.Definition.Variant);
                if(kind==PossessionKind.StoneTippedSpear)Check(records.Max(r=>r.SupportError)<.035f,"Spear support stays on shaft during "+gait.Item1+" on "+humanPlayer.Definition.Variant);
            }
        }
        ClearNaturalReviewHands();AnimateHuman(humanPlayer,0,false,false,1);yield return null;
    }
    IEnumerator ReviewNaturalSwimming(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("NATURAL SWIM "+text);report.Add("PASS "+text);Debug.Log("NATURAL SWIM PASS "+text);}
        var saved=Inventory.Items.ToArray();bool oldReview=humanReviewPose,oldMode=HumanAnimationOptions.Kimodo,oldPause=paused;float oldDt=viewArmDiagnosticDt;
        try
        {
            HumanAnimationOptions.Set(true);humanReviewPose=true;paused=true;viewArmDiagnosticDt=1f/30;pitch=0;
            Check(motion.Swimming,"Review uses the actual coastal swimming controller state");
            foreach(var sample in new[]{(PossessionKind.StoneTippedSpear,PossessionPlace.RightHand,"SwimCarry"),(PossessionKind.StoneTippedSpear,PossessionPlace.LeftHand,"SwimCarryLeft"),
                (PossessionKind.Bow,PossessionPlace.RightHand,"SwimCarryLeft"),(PossessionKind.HaftedKnife,PossessionPlace.RightHand,"SwimCarry"),(PossessionKind.LargeRock,PossessionPlace.RightHand,"SwimCarryTwo")})
            {
                ClearNaturalReviewHands();var item=CreateNaturalReviewHeld(sample.Item1,sample.Item2);var records=new List<NaturalPoseEvidence>();
                humanPlayer.Root.transform.localPosition=new Vector3((float)(motion.X-originX),(float)(motion.Y-WorldGen.Presentation.TerrainMotion.EyeHeightM),(float)(motion.Z-originZ));
                for(int frame=0;frame<40;frame++)
                {
                    AnimateHuman(humanPlayer,1,false,false,1f/30,true);UpdateInventoryVisuals();UpdateCamera();
                    if(frame%12==0)
                    {
                        // Hold this exact pose through one normal render before a manual capture.
                        yield return null;
                        var record=ExportNaturalPose(item,sample.Item3,frame/30f,true);records.Add(record);
                        string name="water-"+sample.Item1+"-"+sample.Item2+"-"+frame;
                        CaptureNaturalImage(output,name+"-surface",3);CaptureNaturalImage(output,name+"-first",-1);
                    }
                    yield return null;
                }
                Check(humanPlayer.Shared.LocomotionWeight(sample.Item3)>.9f,"Real water uses "+sample.Item3+" with "+sample.Item1+" in "+sample.Item2);
                if(sample.Item3!="SwimCarryTwo")
                {
                    bool freeLeft=NaturalSide(item)=="r";var points=records.Select(r=>freeLeft?r.LeftGrip:r.RightGrip).ToArray();
                    float excursion=points.Max(p=>Vector3.Distance(p,points[0]));Check(excursion>.08f,"The free arm propels the swimmer while carrying "+sample.Item1);
                }
                string key="water-"+humanPlayer.Definition.Variant+"-"+sample.Item1+"-"+sample.Item2;
                WriteNaturalPoses(Path.Combine(output,key+".json"),records);
            }
        }
        finally
        {
            ClearNaturalReviewHands();Inventory.Items.AddRange(saved);humanReviewPose=oldReview;HumanAnimationOptions.Set(oldMode);paused=oldPause;viewArmDiagnosticDt=oldDt;
            AnimateHuman(humanPlayer,1,false,false,.5f,true);UpdateInventoryVisuals();UpdateCamera();
        }
    }
    IEnumerator ReviewNaturalAim(string output,List<string> report,bool images)
    {
        var body=humanPlayer.Root.transform;
        foreach(var sample in new[]{(PossessionKind.Bow,"BowDraw"),(PossessionKind.StoneTippedSpear,"SpearThrowCharge"),(PossessionKind.StoneTippedSpear,"SpearThrust2")})
        {
            ClearNaturalReviewHands();var item=CreateNaturalReviewHeld(sample.Item1);viewArmDiagnosticDt=1f/30;
            if(sample.Item1==PossessionKind.Bow){CreateNaturalReviewHeld(PossessionKind.Arrow,PossessionPlace.LeftHand);BeginItemCharge(item,false,0,false);}
            var records=new List<NaturalPoseEvidence>();viewArmDiagnosticClip=sample.Item2;
            var clip=humanPlayer.Shared.Clips[sample.Item2];
            viewArmDiagnosticTime=clip.Duration*(sample.Item2=="SpearThrust2"?clip.Contact:1);
            foreach(float look in new[]{-45f,0f,45f})
            {
                pitch=look;
                for(int settle=0;settle<12;settle++)
                {
                    if(itemCharge!=null)itemCharge.Started=Time.unscaledTimeAsDouble-itemCharge.Duration;
                    body.localRotation=Quaternion.Euler(0,heading+NaturalBowYaw,0);UpdateInventoryVisuals();UpdateCamera();
                }
                // Hold this exact pose through one normal render before a manual capture.
                yield return null;
                var record=ExportNaturalPose(item,sample.Item2,look,true);records.Add(record);
                if(SupportSpear(item)&&sample.Item2=="SpearThrust2"&&record.SupportError>.035f)throw new InvalidOperationException("Aimed spear loses supporting grip on "+humanPlayer.Definition.Variant+" at "+look);
                if(images)
                {
                    string key=humanPlayer.Definition.Variant+"-aim-"+sample.Item2+"-"+look;
                    CaptureNaturalImage(output,key+"-front",0);CaptureNaturalImage(output,key+"-side",1);CaptureNaturalImage(output,key+"-first",-1);
                }
                yield return null;
            }
            WriteNaturalPoses(Path.Combine(output,humanPlayer.Definition.Variant+"-aim-"+sample.Item2+".json"),records);
            report.Add("PASS Native aimed "+sample.Item2+" sampled at three look pitches on "+humanPlayer.Definition.Variant);
        }
        pitch=0;ClearNaturalReviewHands();
    }
}
