using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckKimodoHumanFlow(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("KIMODO HUMAN "+text);report.Add("PASS "+text);Debug.Log("KIMODO HUMAN PASS "+text);}
        void Image(string name,bool outside=false)
        {
            // Coroutines resume before LateUpdate's final grip fitting. Sample
            // that same final presentation before manually rendering a camera.
            UpdateInventoryVisuals();UpdateCamera();
            var position=view.transform.position;var rotation=view.transform.rotation;var old=view.targetTexture;var active=RenderTexture.active;int mask=humanReviewBodyMask;
            if(outside)humanReviewBodyMask=0;
            if(outside)
            {
                var cameraLocal=humanPlayer.Root.transform.localPosition+Quaternion.Euler(0,heading,0)*new Vector3(1.9f,1.7f,-2.5f);
                cameraLocal.y=Mathf.Max(cameraLocal.y,(float)Ground(cameraLocal.x+originX,cameraLocal.z+originZ)+.5f);
                view.transform.position=transform.TransformPoint(cameraLocal);view.transform.LookAt(humanPlayer.Root.transform.position+Vector3.up*.95f);
            }
            var target=new RenderTexture(1280,800,24);target.Create();view.targetTexture=target;view.Render();RenderTexture.active=target;
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            RenderTexture.active=active;view.targetTexture=old;target.Release();Destroy(target);Destroy(image);view.transform.SetPositionAndRotation(position,rotation);humanReviewBodyMask=mask;
        }
        void Clear()
        {
            CancelItemCharge();Array.Clear(pendingMelee,0,pendingMelee.Length);viewItemActions.Clear();viewReleaseAt=viewGatherAt=viewPunchAt=viewConsumeAt=-100;viewArmDiagnosticClip=null;viewArmDiagnosticDt=-1;
            ClearSharedAmmo();RestoreSharedSlings();ClearInventoryPhysics();foreach(var root in inventoryVisuals.Values.ToArray())DestroyInventoryVisual(root);inventoryVisuals.Clear();inventoryVisualPlaces.Clear();Inventory.Items.Clear();Inventory.Crafts.Clear();
        }
        Possession Hold(PossessionKind kind,PossessionPlace hand=PossessionPlace.RightHand)
        {
            var p=kind==PossessionKind.SmallRock?new Possession{Id="kimodo-rock-"+Guid.NewGuid().ToString("N"),Kind=kind,Place=PossessionPlace.Ground,Position=Position,WidthM=.07,HeightM=.06,DepthM=.06,MassKg=.15,CellsWide=1,CellsHigh=1,Tint=new V(1,1,1),Variant=ToolStone.Encode(ToolStoneMaterial.Chert,0)}:PortableTechnology.Create(kind,Position);
            Inventory.Items.Add(p);Check(Inventory.Move(p.Id,hand,"",0,0,false,out _),"Fixture holds "+kind+" in "+hand);return p;
        }
        var oldKeyboard=Keyboard.current;var oldMouse=Mouse.current;var background=InputSystem.settings.backgroundBehavior;bool oldMode=HumanAnimationOptions.Kimodo;
        var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();keyboard.MakeCurrent();mouse.MakeCurrent();TestKeyboard(keyboard);InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var neutral=new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.5f)};
        try
        {
            Check(kimodoHumanLibrary!=null&&kimodoHumanLibrary.People.Length==6,"Six separately fitted native character banks are packaged");
            foreach(var person in kimodoHumanLibrary.People)
            {
                Check(person.Clips.Length>=64&&person.Clips.Select(c=>c.Role).Distinct().Count()==person.Clips.Length,"Complete nonduplicate initial bank for "+person.Variant);
                Check(person.Clips.All(c=>c.Clip!=null&&c.Duration>0&&c.Duration<12),"Finite bounded native clip durations for "+person.Variant);
                Check(person.Clips.Where(c=>c.Role.EndsWith("Charge",StringComparison.Ordinal)||c.Role=="BowDraw").All(c=>!c.Loop),"Charge endpoints stay clamped instead of looping for "+person.Variant);
            }
            SetInventoryOpen(false);campMenu=null;campGroundMenu=campSleepMenu=false;chatOpen=mapOpen=ecologyOpen=false;paused=false;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();motion.Flying=false;motion.Place(motion.X,motion.Z,Ground(motion.X,motion.Z));
            humanCamera.Restore(0,false);humanCameraDistance=0;heading=0;pitch=0;UpdateCamera();Clear();
            var original=humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.sharedMesh).ToArray();
            HumanAnimationOptions.Set(false);yield return null;var spear=Hold(PossessionKind.StoneTippedSpear);UpdateInventoryVisuals();
            Check(!SharedHumanMotion&&viewArmsVisible&&viewArms!=null,"Legacy debug selection restores the original separate first-person arms");
            Check(PlayerPrefs.GetInt("human-kimodo-animation",-1)==0,"Legacy selection persists in settings");
            HumanAnimationOptions.Set(true);yield return new WaitForSecondsRealtime(.25f);UpdateInventoryVisuals();
            Check(SharedHumanMotion&&!viewArmsVisible&&!viewArms.Root.activeSelf,"Kimodo selection disables private arms and uses the world character");
            Check(PlayerPrefs.GetInt("human-kimodo-animation",-1)==1,"Kimodo selection persists in settings");
            Check(original.SequenceEqual(humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.sharedMesh)),"Changing animation mode preserves every original character mesh");
            var localEye=humanPlayer.Shared.LocalEye;var localHead=humanPlayer.Root.transform.InverseTransformPoint(inventoryBones["head"].position);
            Debug.Log("KIMODO EYE local="+localEye.ToString("F4")+" head="+localHead.ToString("F4")+" expectedHeight="+humanPlayer.Definition.EyeHeight+" camera="+humanPlayer.Root.transform.InverseTransformPoint(view.transform.position).ToString("F4"));
            Check(Mathf.Abs(localEye.y-humanPlayer.Definition.EyeHeight)<.15f,"Shared first-person eye stays at the character's actual authored eye height");
            var expectedRoot=new Vector3((float)(motion.X-originX),(float)(motion.Y-WorldGen.Presentation.TerrainMotion.EyeHeightM),(float)(motion.Z-originZ));
            Check(Vector3.Distance(expectedRoot,humanPlayer.Root.transform.localPosition)<.03f,"Generated clips do not overwrite controller-owned world position");
            Vector3 shaft=SharedSpearDirection,grips=SharedGripPosition("l")-SharedGripPosition("r");
            Check(Vector3.Cross(grips,shaft).magnitude<.06f&&Vector3.Dot(grips,shaft)>.08f,"Both supporting spear grips lie along the same physical shaft");
            Image("shared-spear-ready",true);pitch=65;UpdateCamera();Image("shared-look-down");pitch=0;UpdateCamera();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.C));
            for(int frame=0;frame<6;frame++){yield return new WaitForSecondsRealtime(.12f);Image("walk-"+frame.ToString("D2"),true);}
            Check(Vector3.Cross(SharedGripPosition("l")-SharedGripPosition("r"),SharedSpearDirection).magnitude<.06f,"Moving spear support retains contact with the physical shaft");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSecondsRealtime(.25f);
            BeginConsumeGesture(true);yield return new WaitForSecondsRealtime(.35f);UpdateInventoryVisuals();
            Check(humanPlayer.Shared.ActionRole=="Drink"&&!inventoryVisuals[spear.Id].activeSelf&&Inventory.AtHand(PossessionPlace.RightHand)==spear,"Drinking frees the visible hands while preserving held-item ownership");
            Image("shared-drink-occupied",true);viewConsumeAt=-100;yield return new WaitForSecondsRealtime(.15f);
            combatGlobalReady=combatRightReady=combatLeftReady=0;InputSystem.QueueStateEvent(mouse,new MouseState{position=neutral.position}.WithButton(MouseButton.Right));yield return null;
            Check(itemCharge?.Weapon==spear&&itemCharge.Throw,"Actual right mouse begins the shared spear wind-up");
            yield return new WaitForSecondsRealtime(1.2f);var heldPose=humanPlayer.Root.transform.InverseTransformPoint(inventoryBones["hand_r"].position);
            yield return new WaitForSecondsRealtime(2.2f);var laterPose=humanPlayer.Root.transform.InverseTransformPoint(inventoryBones["hand_r"].position);
            Check(itemCharge?.Weapon==spear&&!spear.InFlight&&pendingRanged==null,"A fully wound-up spear remains held beyond the generated clip duration");
            Check(Vector3.Distance(heldPose,laterPose)<.025f&&humanPlayer.Shared.ActionRole=="SpearThrowCharge","Holding charge retains the cocked endpoint without replaying the throw");
            Image("shared-spear-held",true);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.C));yield return new WaitForSecondsRealtime(1.1f);
            Check(itemCharge?.Weapon==spear&&humanPlayer.Shared.MoveSpeed>.3f,"Actual walking keeps the held throw active");
            var thigh=inventoryBones["thigh_l"].localRotation;yield return new WaitForSecondsRealtime(.23f);
            Check(Quaternion.Angle(thigh,inventoryBones["thigh_l"].localRotation)>1,"The walking legs continue animating underneath the held upper body");Image("shared-walk-held",true);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift));yield return new WaitForSecondsRealtime(1.1f);
            Check(itemCharge?.Weapon==spear&&humanPlayer.Shared.MoveSpeed>2,"Actual running keeps the held throw active");Image("shared-run-held",true);
            InputSystem.QueueStateEvent(mouse,neutral);yield return null;
            Check(itemCharge==null&&(pendingRanged!=null||spear.InFlight),"Releasing the actual mouse button begins the shared release stroke");
            yield return new WaitForSecondsRealtime(.65f);
            Check(pendingRanged==null&&spear.Place==PossessionPlace.Ground&&Inventory.AtHand(PossessionPlace.RightHand)!=spear,"The authored release event launches the physical spear once while moving");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSecondsRealtime(.4f);Clear();
            spear=Hold(PossessionKind.StoneTippedSpear);combatGlobalReady=combatRightReady=0;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=neutral.position}.WithButton(MouseButton.Right));yield return new WaitForSecondsRealtime(.35f);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=neutral.position}.WithButton(MouseButton.Right).WithButton(MouseButton.Left));yield return null;
            Check(itemCharge==null&&pendingRanged==null&&!spear.InFlight&&Inventory.AtHand(PossessionPlace.RightHand)==spear,"The opposite button cancels wind-up without firing or losing the spear");
            InputSystem.QueueStateEvent(mouse,neutral);yield return null;Clear();
            var bow=Hold(PossessionKind.Bow);var arrow=Hold(PossessionKind.Arrow,PossessionPlace.LeftHand);
            BeginItemCharge(bow,false,0,false);Check(itemCharge?.Ammo==arrow,"An actual bow charge borrows its real arrow");itemCharge.Started-=itemCharge.Duration;
            yield return new WaitForSecondsRealtime(.2f);UpdateInventoryVisuals();
            Check(sharedLoadedAmmo!=null&&!inventoryVisuals[arrow.Id].activeSelf,"The shared bow has one loaded arrow without an offhand duplicate");
            Vector3 nock=sharedLoadedAmmo.transform.position+sharedLoadedAmmo.transform.up*(float)arrow.HeightM*.5f;
            Check(Vector3.Distance(nock,SharedGripPosition("r"))<.015f,"Loaded ammunition follows the real drawing-hand grip");
            var bowRoot=inventoryVisuals[bow.Id];Vector3 brace=bowRoot.transform.TransformPoint(new Vector3(.38f,.5f,0));
            Check(Vector3.Distance(SharedGripPosition("r"),brace)>.2f,"Full charge visibly draws the actual bowstring");Image("shared-bow-drawn",true);Image("first-bow-drawn");
            CancelItemCharge();Clear();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return new WaitForSecondsRealtime(.12f);
            Check(!motion.Grounded,"Actual jump input enters the generated airborne motion");Image("shared-jump",true);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSecondsRealtime(.85f);Check(motion.Grounded,"Generated jump playback returns to grounded movement");
            foreach(var sample in new[]{(PossessionKind.HaftedKnife,"ToolCutA"),(PossessionKind.StoneAxe,"ToolChop"),(PossessionKind.SmallRock,"ThrowCharge"),(PossessionKind.Bow,"BowDraw"),(PossessionKind.Sling,"SlingWhirl"),(PossessionKind.SpearThrower,"SpearThrowCharge")})
            {
                var item=Hold(sample.Item1);UpdateInventoryVisuals();viewArmDiagnosticClip=sample.Item2;viewArmDiagnosticTime=HumanActionDuration(sample.Item2)*.85;viewArmDiagnosticDt=.1f;
                for(int i=0;i<8;i++)UpdateInventoryVisuals();
                Check(humanPlayer.Shared.ActionRole==sample.Item2,"Shared controller samples "+sample.Item2);Image("shared-"+sample.Item2,true);Image("first-"+sample.Item2);Clear();
            }
            foreach(string role in new[]{"Gather","Drink","Eat","WorkKnapping","WorkCut","WorkTwist","WorkDig","CarryLarge"})
            {
                viewArmDiagnosticClip=role;viewArmDiagnosticTime=HumanActionDuration(role)*.5;viewArmDiagnosticDt=.1f;
                for(int i=0;i<8;i++)UpdateInventoryVisuals();Image("shared-"+role,true);
                Check(humanPlayer.Shared.ActionRole==role,"Shared controller samples "+role);Clear();
            }
            foreach(bool generated in new[]{true,false})
            {
                Clear();HumanAnimationOptions.Set(generated);string mode=generated?"Kimodo":"Legacy";
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                humanCamera.Restore(3.4,false);humanCameraDistance=3.4f;heading=0;pitch=12;
                humanPlayer.Yaw=humanPlayer.TargetYaw=0;
                Hold(PossessionKind.StoneTippedSpear);
                yield return new WaitForSecondsRealtime(.3f);UpdateInventoryVisuals();
                var facing=humanPlayer.Yaw;
                var headBefore=Quaternion.Inverse(humanPlayer.Root.transform.rotation)*inventoryBones["head"].rotation;
                Image("third-"+mode+"-before-orbit");
                heading=110;pitch=18;yield return new WaitForSecondsRealtime(.35f);UpdateInventoryVisuals();
                Check(Mathf.Abs(Mathf.DeltaAngle(facing,humanPlayer.Yaw))<.5f,mode+" third-person camera orbit leaves an idle body facing unchanged");
                var headAfter=Quaternion.Inverse(humanPlayer.Root.transform.rotation)*inventoryBones["head"].rotation;
                Check(Quaternion.Angle(headBefore,headAfter)<12,mode+" idle head does not turn to follow the orbiting third-person camera");
                if(generated)Check(Vector3.Dot(SharedSpearDirection,humanPlayer.Root.transform.forward)>.999f,"Third-person idle spear stays aligned with its carrier instead of the camera");
                Image("third-"+mode+"-after-orbit");
                double fromX=motion.X,fromZ=motion.Z;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D,Key.C));yield return new WaitForSecondsRealtime(.7f);
                double travelX=motion.X-fromX,travelZ=motion.Z-fromZ;
                float travelYaw=(float)(Math.Atan2(travelX,travelZ)*180/Math.PI);
                Check(travelX*travelX+travelZ*travelZ>.03&&Mathf.Abs(Mathf.DeltaAngle(travelYaw,humanPlayer.Yaw))<12,mode+" third-person side movement turns the body toward actual travel");
                Check(Mathf.Abs(Mathf.DeltaAngle(heading,humanPlayer.Yaw))>45,mode+" third-person movement facing is independent of camera heading");
                Image("third-"+mode+"-moving-side");
                fromX=motion.X;fromZ=motion.Z;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.S,Key.C));yield return new WaitForSecondsRealtime(.7f);
                travelX=motion.X-fromX;travelZ=motion.Z-fromZ;travelYaw=(float)(Math.Atan2(travelX,travelZ)*180/Math.PI);
                Check(travelX*travelX+travelZ*travelZ>.03&&Mathf.Abs(Mathf.DeltaAngle(travelYaw,humanPlayer.Yaw))<12,mode+" third-person backward input retains the original turn-toward-travel behavior");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSecondsRealtime(.25f);
            }
            Clear();HumanAnimationOptions.Set(true);humanCamera.Restore(0,false);humanCameraDistance=0;heading=220;pitch=0;
            yield return new WaitForSecondsRealtime(.6f);
            Check(Mathf.Abs(Mathf.DeltaAngle(heading,humanPlayer.Yaw))<1,"Returning to shared first person still aligns the body with the viewing direction");
        }
        finally
        {
            Clear();HumanAnimationOptions.Set(oldMode);humanReviewPose=false;InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);TestKeyboard(null);
            if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();if(oldMouse!=null&&oldMouse.added)oldMouse.MakeCurrent();InputSystem.settings.backgroundBehavior=background;
        }
    }
}
