using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    IEnumerator CheckCampInputFlow(string output,List<string> report,CampObject bed,Possession spear)
    {
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("CAMP INPUT "+message);report.Add("PASS "+message);Debug.Log("CAMP INPUT PASS "+message);}
        var oldKeyboard=Keyboard.current;var oldMouse=Mouse.current;var oldBackground=InputSystem.settings.backgroundBehavior;
        var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();TestKeyboard(keyboard);keyboard.MakeCurrent();mouse.MakeCurrent();InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        IEnumerator KeyPress(Key key)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;}
        void AimAt(double x,double z,double height=0)
        {
            heading=Mathf.Atan2((float)(x-motion.X),(float)(z-motion.Z))*Mathf.Rad2Deg;UpdateCamera();
            var point=new Vector3((float)(x-originX),(float)CampFloor(x,z)+(float)height,(float)(z-originZ));var direction=(point-view.transform.localPosition).normalized;pitch=-Mathf.Asin(direction.y)*Mathf.Rad2Deg;UpdateCamera();campGroundAt=0;
        }
        try
        {
            campMenu=null;campGroundMenu=campSleepMenu=false;SetInventoryOpen(false);paused=false;chatOpen=mapOpen=ecologyOpen=false;
            PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();motion.Flying=false;humanCamera.Restore(0,false);
            yield return KeyPress(Key.Z);Check(campSleepMenu&&!paused&&!inventoryOpen,"Z opens the ground rest dialog through actual keyboard events");
            yield return KeyPress(Key.Escape);Check(!CampUiOpen&&!paused,"Escape closes the rest dialog without also opening pause");
            TerrainHandoff.Coordinates(frame,bed.Position,out double bx,out double bz);AimAt(bx,bz,bed.BeddingDepthM*.5);yield return KeyPress(Key.E);
            Check(campMenu==bed&&!inventoryOpen,"E opens the aimed bedding interaction through the player input path");
            yield return KeyPress(Key.Escape);Check(!CampUiOpen&&!paused,"Escape closes a placed-material menu without pausing");
            BeginCampSleep(1,false,bed);Check(campSleeping,"Input wake fixture starts sleep on the actual bedding");yield return KeyPress(Key.Space);
            Check(!campSleeping&&!campResting&&CampTimeRate==1&&!paused,"Space wakes the player and restores ordinary time without opening pause");
            BeginCampSleep(1,false,bed);yield return KeyPress(Key.Escape);Check(!campSleeping&&!paused,"Escape wakes the player without opening the pause menu");
            Vitals.Health=60;BeginCampSleep(1,false,bed);Vitals.Health=65;UpdateCampSleep();Vitals.Health=64;UpdateCampSleep();
            Check(!campSleeping,"Damage interrupts sleep even after health has recovered above its initial value");Vitals.Restore();

            // Select a reachable, unoccupied placement using the same ground ray.
            var branch=WoodCrafting.Branch(new ScatterInstance{Position=Position,Kind=ScatterKind.Branch,WidthM=.9,HeightM=.03,Variant=0});branch.Id="input-placement-branch";branch.NaturalId=0;Inventory.Items.Add(branch);inventoryNearbyAt=0;
            bool found=false;
            for(int i=0;i<16&&!found;i++)
            {double angle=i*Math.PI/8;AimAt(motion.X+Math.Sin(angle)*2,motion.Z+Math.Cos(angle)*2);BeginCampPlacement(branch,CampKind.Stake);found=campPlaceValid;if(!found)CancelCampPlacement();}
            Check(found,"A player-reachable empty placement is available for mouse input checks");
            var blocker=new GameObject("Camp placement obstruction check");
            try
            {
                blocker.transform.position=Vector3.Lerp(view.transform.position,transform.TransformPoint(campPlacePoint),.5f);blocker.AddComponent<BoxCollider>().size=Vector3.one*.3f;Physics.SyncTransforms();campGroundAt=0;UpdateCampPlacement();
                Check(!campPlaceValid,"Solid objects between the player and the ground prevent placing materials through them");
            }
            finally{DestroyImmediate(blocker);campGroundAt=0;UpdateCampPlacement();}
            Check(campPlaceValid,"Placement becomes available immediately after the obstruction is removed");
            double beforeCooldown=combatRightReady;combatGlobalReady=0;int count=Camp.Objects.Count;
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));yield return null;
            Check(campPlaceMaterial==null&&Camp.Objects.Count==count&&itemCharge==null&&Inventory.AtHand(PossessionPlace.RightHand)==spear,"Right-click cancels placement without charging or throwing the held spear");
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
            BeginCampPlacement(branch,CampKind.Stake);Check(campPlaceValid,"Canceled placement can be started again immediately");
            beforeCooldown=combatRightReady;combatGlobalReady=0;
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return null;
            Check(campPlaceMaterial==null&&Camp.Objects.Count==count+1&&combatRightReady==beforeCooldown&&itemCharge==null,"Left-click places the branch without also swinging the held spear");
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);TestKeyboard(null);
            if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();if(oldMouse!=null&&oldMouse.added)oldMouse.MakeCurrent();InputSystem.settings.backgroundBehavior=oldBackground;
            CancelCampPlacement();campMenu=null;campGroundMenu=campSleepMenu=false;if(campSleeping||campResting)WakeCamp("Input check complete.");
        }
    }
}
