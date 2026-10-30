using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckFirstPersonFlow(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("FIRST PERSON "+text);report.Add("PASS "+text);Debug.Log("FIRST PERSON PASS "+text);}
        Color32[] Image(string name)
        {
            var old=view.targetTexture;var previous=RenderTexture.active;var rt=new RenderTexture(1280,800,24);rt.Create();view.targetTexture=rt;view.Render();RenderTexture.active=rt;
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();var pixels=image.GetPixels32();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            RenderTexture.active=previous;view.targetTexture=old;rt.Release();Destroy(rt);Destroy(image);return pixels;
        }
        void Clear()
        {
            CancelItemCharge();viewItemActions.Clear();ClearInventoryPhysics();foreach(var root in inventoryVisuals.Values.ToArray())DestroyInventoryVisual(root);
            inventoryVisuals.Clear();inventoryVisualPlaces.Clear();Inventory.Items.Clear();Inventory.Crafts.Clear();viewReleaseAt=viewGatherAt=-100;
        }
        Possession Hold(PossessionKind kind,PossessionPlace hand=PossessionPlace.RightHand)
        {var p=PortableTechnology.Create(kind,Position);Inventory.Items.Add(p);Check(Inventory.Move(p.Id,hand,"",0,0,false,out _),"Hold "+kind+" in "+hand);return p;}
        void FiniteArms(string action)
        {
            bool finite=true;foreach(var renderer in viewArmRenderers){var baked=new Mesh();renderer.BakeMesh(baked);finite&=baked.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z));Destroy(baked);}
            Check(finite,"Finite skinned arm geometry: "+action);
        }
        InitializeInventory();Clear();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();Vitals.Restore();motion.Flying=false;motion.Place(motion.X,motion.Z,CampFloor(motion.X,motion.Z));humanCamera.Restore(0,false);heading=0;pitch=0;humanCameraDistance=0;UpdateHumans();UpdateCamera();paused=true;
        TerrainMapViewport.Degrees(Position,out _,out double longitude);PlayerRules.SetLocalSolarHour(12,longitude);SetDaylight(LocalSolarHour,skyYear);
        var original=humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.sharedMesh).ToArray();
        var spear=Hold(PossessionKind.StoneTippedSpear);UpdateInventoryVisuals();
        Check(viewArms!=null&&viewArmMeshes.Count>0&&viewArmMeshes.Sum(m=>m.triangles.Length)>300,"Private first-person arms contain actual native skinned geometry");
        Check(original.SequenceEqual(humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.sharedMesh)),"World avatar meshes stay unmodified for third person and other viewers");
        Check(SupportSpear(spear),"An empty offhand supports the spear without forcing its inventory ownership to change");FiniteArms("two-hand spear");Image("01-spear-two-hand");
        var stone=Hold(PossessionKind.StonePoint,PossessionPlace.LeftHand);UpdateInventoryVisuals();Check(!SupportSpear(spear),"An occupied offhand makes the same spear one-handed");FiniteArms("one-hand spear");Image("02-spear-one-hand");Inventory.Items.Remove(stone);
        AnimateViewItem(spear);viewItemActions[spear.Id].At-=.26;UpdateInventoryVisuals();FiniteArms("thrust");Image("03-spear-thrust");viewItemActions.Clear();
        itemCharge=new ItemCharge{Weapon=spear,Ammo=spear,Started=Time.unscaledTimeAsDouble-.8,Duration=.8,Throw=true};UpdateInventoryVisuals();FiniteArms("throw wind-up");Image("04-spear-wind-up");
        itemCharge=null;AnimateViewItem(spear,true);Inventory.Items.Remove(spear);UpdateInventoryVisuals();Image("05-throw-follow-through");
        Clear();AnimateViewItem(null);viewPunchAt-=.15;UpdateInventoryVisuals();Image("05b-unarmed-punch");viewPunchAt=-100;var knife=Hold(PossessionKind.HaftedKnife);AnimateViewItem(knife);viewItemActions[knife.Id].At-=.12;UpdateInventoryVisuals();FiniteArms("knife cut");Image("06-knife-cut");
        Clear();var bow=Hold(PossessionKind.Bow);UpdateInventoryVisuals();Image("07-bow-rest");
        var arrow=PortableTechnology.Create(PossessionKind.Arrow,Position);Inventory.Items.Add(arrow);itemCharge=new ItemCharge{Weapon=bow,Ammo=arrow,Started=Time.unscaledTimeAsDouble-1.05,Duration=1.05};UpdateInventoryVisuals();FiniteArms("bow draw");
        Check(viewNockedArrow!=null&&Vector3.Distance(viewNockedArrow.transform.position,BowDrawPoint(1))<.01f,"A real arrow model is nocked at the drawing hand");
        var line=inventoryVisuals[bow.Id].GetComponentInChildren<LineRenderer>();Image("08-bow-draw");Debug.Log("BOW CHECK line="+(line==null?"missing":line.GetPosition(1).ToString("F3"))+" charge="+(itemCharge!=null)+" first="+humanHideBody);Check(line!=null&&Vector3.Distance(line.GetPosition(1),new Vector3(.38f,.5f,0))>.05,"Bow string follows the drawing hand rather than remaining straight");CancelItemCharge();
        Clear();BeginGatherGesture(view.transform.TransformPoint(new Vector3(.1f,-.2f,1)));viewGatherAt-=.45;UpdateInventoryVisuals();FiniteArms("foraging reach");Image("09-foraging-reach");
        var cameraRoot=new GameObject("Other viewer check",typeof(Camera));var other=cameraRoot.GetComponent<Camera>();BeginViewArmsCamera(default,other);Check(viewArmRenderers.All(r=>!r.enabled),"Private arms are excluded from every other camera");Destroy(cameraRoot);
        humanCamera.Restore(4,false);humanCameraDistance=4;UpdateCamera();UpdateInventoryVisuals();Check(!viewArmsVisible&&humanPlayer.Root.activeSelf,"Third person hides private arms and retains the full world avatar");
        humanCamera.Restore(0,false);humanCameraDistance=0;UpdateCamera();
        // All six original body variants must supply arms without touching source assets.
        for(int variant=0;variant<humanLibrary.People.Length;variant++)
        {var keep=humanPlayer;humanPlayer=CreateHuman(variant,-200);EnsureViewArms();Check(viewArmMeshes.Count>0,"Native arms available for body variant "+humanLibrary.People[variant].Variant);DestroyHuman(humanPlayer);humanPlayer=keep;}
        EnsureViewArms();Clear();
        var pack=PortableTechnology.Create(PossessionKind.HidePack,Position);pack.Place=PossessionPlace.RightHand;Inventory.Items.Add(pack);
        Check(EquipFromPortrait(pack),"Portrait equipment drop wears a compatible carrier through normal inventory validation");
        Check(Inventory.WornContainers.Contains(pack)&&pack.Place==PossessionPlace.Back,"Only actual worn equipment supplies named container space");
        SetInventoryOpen(true);PlayerRules.Mode=GameMode.Survival;Vitals.Restore();ApplyPlayerMode();paused=false;
        double clock=PlayerRules.SolarDays,hunger=Vitals.Hunger,campClock=Camp.SimulatedSeconds;yield return new WaitForSecondsRealtime(.6f);
        Check(inventoryOpen&&!Paused&&PlayerRules.SolarDays>clock,"Opening inventory leaves the world clock running");
        Check(Vitals.Hunger>hunger&&Camp.SimulatedSeconds>campClock,"Survival needs and camp time continue with inventory open");
        SetInventoryOpen(false);paused=true;UpdateInventoryVisuals();
        PlayerRules.Mode=GameMode.Survival;Vitals.Restore();paused=false;UpdatePlayerSurvival(0,0);UpdateSurvivalHud();paused=true;
        var mode=survivalHudCanvas.renderMode;var oldCam=survivalHudCanvas.worldCamera;int mask=view.cullingMask;
        survivalHudCanvas.renderMode=RenderMode.ScreenSpaceCamera;survivalHudCanvas.worldCamera=view;survivalHudCanvas.planeDistance=.4f;view.cullingMask|=1<<5;survivalHudCanvas.enabled=true;
        try
        {
            Vitals.Hunger=100;Vitals.WaterDeficitLitres=2.2;var values=SurvivalHudValues.From(Vitals,22);survivalHud.SetValues(values,true);Canvas.ForceUpdateCanvases();var hungry=Image("10-hungry-parched");
            Check(values.Fullness>.1&&values.Hydration>.35&&values.FoodAttention>0,"Hungry/parched warnings retain visible reserves before critical depletion");
            Vitals.ReserveKcal=0;Vitals.WaterDeficitLitres=7.5;values=SurvivalHudValues.From(Vitals,22);survivalHud.SetValues(values,true);Canvas.ForceUpdateCanvases();Image("11-critical-depletion");Check(values.Fullness==0&&values.Hydration==0,"Critical depletion visibly empties both need icons");
            Vitals.Restore();Vitals.FoodExposures=new List<FoodExposure>{new FoodExposure{DelaySeconds=0,RemainingDamage=30,RemainingSeconds=10800}};
            values=SurvivalHudValues.From(Vitals,22);survivalHud.SetValues(values,true);Canvas.ForceUpdateCanvases();var sick=Image("12-sickness");Check(values.Sickness==1&&hungry.Where((p,i)=>Math.Abs(p.g-sick[i].g)>12).Count()>10,"Active illness produces a real green stomach in the GPU HUD");
        }
        finally{survivalHudCanvas.renderMode=mode;survivalHudCanvas.worldCamera=oldCam;view.cullingMask=mask;}
        Vitals.Restore();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();Clear();paused=false;
    }
}
