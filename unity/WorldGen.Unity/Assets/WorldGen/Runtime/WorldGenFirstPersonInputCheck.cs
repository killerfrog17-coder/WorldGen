using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    IEnumerator CheckFirstPersonInputFlow(List<string> report)
    {
        void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("FIRST PERSON INPUT "+why);report.Add("PASS "+why);Debug.Log("FIRST PERSON INPUT PASS "+why);}
        var oldKeyboard=Keyboard.current;var oldMouse=Mouse.current;var background=InputSystem.settings.backgroundBehavior;
        var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
        keyboard.MakeCurrent();mouse.MakeCurrent();TestKeyboard(keyboard);InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var mouseState=new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.5f)};
        Possession Spear()
        {
            var held=Inventory.AtHand(PossessionPlace.RightHand);if(held!=null)MovePossession(held.Id,PossessionPlace.Ground);
            var item=PortableTechnology.Create(PossessionKind.StoneTippedSpear,Position);Inventory.Items.Add(item);
            if(!Inventory.Move(item.Id,PossessionPlace.RightHand,"",0,0,false,out var reason))throw new InvalidOperationException(reason);
            combatGlobalReady=combatRightReady=combatLeftReady=0;return item;
        }
        try
        {
            SetInventoryOpen(false);campMenu=null;campGroundMenu=campSleepMenu=false;chatOpen=mapOpen=ecologyOpen=false;paused=false;
            PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();motion.Flying=false;humanCamera.Restore(0,false);
            var spear=Spear();InputSystem.QueueStateEvent(mouse,new MouseState{position=mouseState.position}.WithButton(MouseButton.Left));yield return null;
            Check(pendingMelee[0]!=null&&viewItemActions.ContainsKey(spear.Id),"Actual left mouse starts the authored wind-up and queues contact");
            InputSystem.QueueStateEvent(mouse,mouseState);yield return new WaitForSecondsRealtime(.45f);
            Check(pendingMelee[0]==null&&Inventory.AtHand(PossessionPlace.RightHand)==spear,"Ordinary frame updates resolve the strike without dropping the held weapon");
            combatGlobalReady=combatRightReady=0;InputSystem.QueueStateEvent(mouse,new MouseState{position=mouseState.position}.WithButton(MouseButton.Right));yield return null;
            Check(itemCharge?.Weapon==spear&&itemCharge.Throw,"Actual right mouse holds a spear for a hand throw");
            yield return new WaitForSecondsRealtime(.85f);InputSystem.QueueStateEvent(mouse,mouseState);yield return null;
            Check(itemCharge==null&&(pendingRanged!=null||spear.Place==PossessionPlace.Ground),"Releasing the button advances from wind-up into the release stroke");
            yield return new WaitForSecondsRealtime(.25f);
            Check(pendingRanged==null&&spear.Place==PossessionPlace.Ground&&Inventory.AtHand(PossessionPlace.RightHand)!=spear,"Actual release launches the physical spear and relinquishes its hand ownership");
            var cancelled=Spear();BeginItemCharge(cancelled,false,1,true);itemCharge.Started-=1;ReleaseItemCharge();
            Check(pendingRanged!=null&&!cancelled.InFlight,"A hand-thrown item stays held until its authored release frame");
            SetInventoryOpen(true);ResolveRangedRelease(Time.unscaledTimeAsDouble+1);
            Check(pendingRanged==null&&!cancelled.InFlight&&Inventory.AtHand(PossessionPlace.RightHand)==cancelled,"Opening inventory during release cancels the pending throw without losing or firing its item");
        }
        finally
        {
            CancelItemCharge();Array.Clear(pendingMelee,0,pendingMelee.Length);SetInventoryOpen(false);paused=true;
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);TestKeyboard(null);
            if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();if(oldMouse!=null&&oldMouse.added)oldMouse.MakeCurrent();InputSystem.settings.backgroundBehavior=background;
        }
    }
}
