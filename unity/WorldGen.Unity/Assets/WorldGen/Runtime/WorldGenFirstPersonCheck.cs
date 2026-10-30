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
            CancelItemCharge();Array.Clear(pendingMelee,0,pendingMelee.Length);viewItemActions.Clear();ClearInventoryPhysics();foreach(var root in inventoryVisuals.Values.ToArray())DestroyInventoryVisual(root);
            inventoryVisuals.Clear();inventoryVisualPlaces.Clear();Inventory.Items.Clear();Inventory.Crafts.Clear();viewReleaseAt=viewGatherAt=viewPunchAt=viewConsumeAt=-100;viewArmDiagnosticClip=null;
        }
        Possession Hold(PossessionKind kind,PossessionPlace hand=PossessionPlace.RightHand)
        {
            bool wood=kind==PossessionKind.Branch||kind==PossessionKind.TrimmedPole||kind==PossessionKind.DiggingStick||kind==PossessionKind.WoodenSpear;
            var p=kind==PossessionKind.SmallRock||kind==PossessionKind.LargeRock?
                new Possession{Id="view-rock-"+Guid.NewGuid().ToString("N"),Kind=kind,Position=Position,WidthM=kind==PossessionKind.LargeRock?.32:.07,HeightM=kind==PossessionKind.LargeRock?.24:.06,DepthM=kind==PossessionKind.LargeRock?.27:.06,MassKg=kind==PossessionKind.LargeRock?8:.15,CellsWide=kind==PossessionKind.LargeRock?4:1,CellsHigh=kind==PossessionKind.LargeRock?3:1,Tint=new V(1,1,1),Variant=ToolStone.Encode(ToolStoneMaterial.Chert,0)}:
                wood?WoodCrafting.Branch(new ScatterInstance{Kind=ScatterKind.Branch,Position=Position,WidthM=1.5,HeightM=.032,Variant=0}):PortableTechnology.Create(kind,Position);
            if(wood){p.Kind=kind;p.Id="view-wood-"+Guid.NewGuid().ToString("N");p.NaturalId=0;}
            Inventory.Items.Add(p);Check(Inventory.Move(p.Id,hand,"",0,0,false,out _),"Hold "+kind+" in "+hand);return p;
        }
        void FiniteArms(string action)
        {
            bool finite=true;foreach(var renderer in viewArmRenderers){var baked=new Mesh();renderer.BakeMesh(baked);finite&=baked.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z));Destroy(baked);}
            Check(finite,"Finite skinned arm geometry: "+action);
        }
        InitializeInventory();Clear();viewArmPoseImmediate=true;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();Vitals.Restore();motion.Flying=false;motion.Place(motion.X,motion.Z,CampFloor(motion.X,motion.Z));humanCamera.Restore(0,false);heading=0;pitch=0;humanCameraDistance=0;UpdateHumans();UpdateCamera();paused=true;
        TerrainMapViewport.Degrees(Position,out _,out double longitude);PlayerRules.SetLocalSolarHour(12,longitude);SetDaylight(LocalSolarHour,skyYear);
        var original=humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.sharedMesh).ToArray();
        var spear=Hold(PossessionKind.StoneTippedSpear);UpdateInventoryVisuals();
        Check(viewArms!=null&&viewArmMeshes.Count>0&&viewArmMeshes.Sum(m=>m.triangles.Length)>300,"Private first-person arms contain actual native skinned geometry");
        Check(viewArms.Clips.Count>=30&&viewArms.Bones.ContainsKey("SpearGrip.R"),"Dedicated native first-person mesh and baked handling takes are present");
        Check(original.SequenceEqual(humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.sharedMesh)),"World avatar meshes stay unmodified for third person and other viewers");
        Check(SupportSpear(spear),"An empty offhand supports the spear without forcing its inventory ownership to change");FiniteArms("two-hand spear");Image("01-spear-two-hand");
        var stone=Hold(PossessionKind.StonePoint,PossessionPlace.LeftHand);UpdateInventoryVisuals();Check(!SupportSpear(spear),"An occupied offhand makes the same spear one-handed");FiniteArms("one-hand spear");Image("02-spear-one-hand");Inventory.Items.Remove(stone);
        AnimateViewItem(spear);viewItemActions[spear.Id].At-=.26;UpdateInventoryVisuals();FiniteArms("thrust");Image("03-spear-thrust");viewItemActions.Clear();
        itemCharge=new ItemCharge{Weapon=spear,Ammo=spear,Started=Time.unscaledTimeAsDouble-.8,Duration=.8,Throw=true};UpdateInventoryVisuals();FiniteArms("throw wind-up");Image("04-spear-wind-up");
        itemCharge=null;AnimateViewItem(spear,true);Inventory.Items.Remove(spear);UpdateInventoryVisuals();Image("05-throw-follow-through");
        Clear();AnimateViewItem(null);viewPunchAt-=.15;UpdateInventoryVisuals();Image("05b-unarmed-punch");viewPunchAt=-100;combatGuarding=true;UpdateInventoryVisuals();FiniteArms("unarmed guard");Image("05c-unarmed-guard");combatGuarding=false;var knife=Hold(PossessionKind.HaftedKnife);AnimateViewItem(knife);viewItemActions[knife.Id].At-=.12;UpdateInventoryVisuals();FiniteArms("knife cut");Image("06-knife-cut");
        Clear();var bow=Hold(PossessionKind.Bow);UpdateInventoryVisuals();Image("07-bow-rest");
        var arrow=PortableTechnology.Create(PossessionKind.Arrow,Position);Inventory.Items.Add(arrow);itemCharge=new ItemCharge{Weapon=bow,Ammo=arrow,Started=Time.unscaledTimeAsDouble-1.05,Duration=1.05};UpdateInventoryVisuals();FiniteArms("bow draw");
        Check(viewNockedArrow!=null&&Vector3.Distance(viewNockedArrow.transform.position,BowDrawPoint(1))<.01f,"A real arrow model is nocked at the drawing hand");
        var bowRest=viewPossessions[bow.Id].Root.transform.position+viewPossessions[bow.Id].Root.transform.up*(float)bow.HeightM*.55f;
        Check(Vector3.Cross(viewNockedArrow.transform.up,bowRest-viewNockedArrow.transform.position).magnitude<.01,"The nocked shaft passes through the actual bow rest");
        var line=viewPossessions[bow.Id].Root.GetComponentInChildren<LineRenderer>();Image("08-bow-draw");Check(line!=null&&Vector3.Distance(line.GetPosition(1),new Vector3(.38f,.5f,0))>.05,"Bow string follows the drawing hand rather than remaining straight");CancelItemCharge();
        Clear();BeginGatherGesture(view.transform.TransformPoint(new Vector3(.1f,-.2f,1)));viewGatherAt-=.45;UpdateInventoryVisuals();FiniteArms("foraging reach");Image("09-foraging-reach");
        var cameraRoot=new GameObject("Other viewer check",typeof(Camera));var other=cameraRoot.GetComponent<Camera>();BeginViewArmsCamera(default,other);Check(viewArmRenderers.All(r=>!r.enabled),"Private arms are excluded from every other camera");Destroy(cameraRoot);
        humanCamera.Restore(4,false);humanCameraDistance=4;UpdateCamera();UpdateInventoryVisuals();Check(!viewArmsVisible&&humanPlayer.Root.activeSelf,"Third person hides private arms and retains the full world avatar");
        humanCamera.Restore(0,false);humanCameraDistance=0;UpdateCamera();
        // All six original body variants must supply arms without touching source assets.
        for(int variant=0;variant<humanLibrary.People.Length;variant++)
        {var keep=humanPlayer;humanPlayer=CreateHuman(variant,-200);EnsureViewArms();Check(viewArmMeshes.Count>0,"Native arms available for body variant "+humanLibrary.People[variant].Variant);DestroyHuman(humanPlayer);humanPlayer=keep;}
        EnsureViewArms();Clear();
        var largeRock=Hold(PossessionKind.LargeRock);UpdateInventoryVisuals();FiniteArms("large rock carry");Image("09b-large-rock-carry");
        Clear();var sling=Hold(PossessionKind.Sling);var pebble=Hold(PossessionKind.SmallRock,PossessionPlace.LeftHand);
        itemCharge=new ItemCharge{Weapon=sling,Ammo=pebble,Started=Time.unscaledTimeAsDouble-.65,Duration=.8};UpdateInventoryVisuals();
        Check(viewPossessions[sling.Id].SlingMesh!=null&&viewNockedArrow!=null&&!viewPossessions.ContainsKey(pebble.Id),"A sling has its own moving cords/pouch and one loaded stone, with no duplicate offhand projectile");Image("09c-sling-whirl");
        Clear();var thrower=Hold(PossessionKind.SpearThrower);var dart=Hold(PossessionKind.SpearDart,PossessionPlace.LeftHand);
        itemCharge=new ItemCharge{Weapon=thrower,Ammo=dart,Started=Time.unscaledTimeAsDouble-.8,Duration=.8};UpdateInventoryVisuals();
        Check(viewNockedArrow!=null&&Vector3.Distance(viewNockedArrow.transform.position,viewPossessions[thrower.Id].Root.transform.TransformPoint(new Vector3(.25f,.96f,.12f)))<.01,"Spear-thrower visibly carries its real dart at the hook before release");Image("09d-atlatl-loaded");
        foreach(string role in new[]{"WorkKnapping","WorkCut","WorkTwist","WorkDig","Eat","Drink"})
        {
            Clear();if(role=="WorkKnapping"){Hold(PossessionKind.SmallRock);Hold(PossessionKind.SmallRock,PossessionPlace.LeftHand);}
            if(role=="WorkCut"){Hold(PossessionKind.HaftedKnife);Hold(PossessionKind.DartShaft,PossessionPlace.LeftHand);}
            if(role=="WorkTwist"){Hold(PossessionKind.Cordage);Hold(PossessionKind.RawFibre,PossessionPlace.LeftHand);}
            if(role=="WorkDig")Hold(PossessionKind.DiggingStick);
            viewArmDiagnosticClip=role;viewArmDiagnosticTime=viewArms.Duration(role)*.45;UpdateInventoryVisuals();FiniteArms(role);Image("work-"+role);
        }
        Clear();
        // Visual evidence is a complete sampled sequence through the same
        // animation graph and private held models, not finite-vertex assertions.
        // These captures require human visual review before accepting the work.
        var film=Path.Combine(output,"motion");Directory.CreateDirectory(film);
        viewArmPoseImmediate=false;viewArmDiagnosticDt=1/30f;
        foreach(string sequence in new[]{"spear-thrust","knife-cut","throw-charge","throw-release","bow-draw","gather"})
        {
            Clear();Possession held=sequence.StartsWith("spear")||sequence.StartsWith("throw")?Hold(PossessionKind.StoneTippedSpear):sequence=="knife-cut"?Hold(PossessionKind.HaftedKnife):sequence=="bow-draw"?Hold(PossessionKind.Bow):null;
            Possession ammunition=null;if(sequence=="bow-draw"){ammunition=PortableTechnology.Create(PossessionKind.Arrow,Position);Inventory.Items.Add(ammunition);}
            UpdateInventoryVisuals();
            for(int frame=0;frame<=24;frame++)
            {
                double phase=frame/24.0,now=Time.unscaledTimeAsDouble;
                if(sequence=="spear-thrust"||sequence=="knife-cut")
                {AnimateViewItem(held);viewItemActions[held.Id].At=now-phase*viewItemActions[held.Id].Duration;}
                else if(sequence=="throw-charge"||sequence=="bow-draw")itemCharge=new ItemCharge{Weapon=held,Ammo=ammunition??held,Started=now-phase,Duration=1,Throw=sequence=="throw-charge"};
                else if(sequence=="throw-release"){viewReleaseKind=ItemPrimaryUse.Thrust;viewReleaseLeft=false;viewReleaseSpear=true;viewReleaseAt=now-phase*.62;Inventory.Items.Remove(held);}
                else viewGatherAt=now-phase*1.05;
                UpdateInventoryVisuals();Image("motion/"+sequence+"-"+frame.ToString("D3"));
            }
        }
        viewArmDiagnosticDt=-1;viewArmPoseImmediate=true;Clear();
        var pack=PortableTechnology.Create(PossessionKind.HidePack,Position);pack.Place=PossessionPlace.RightHand;Inventory.Items.Add(pack);
        Check(EquipFromPortrait(pack),"Portrait equipment drop wears a compatible carrier through normal inventory validation");
        Check(Inventory.WornContainers.Contains(pack)&&pack.Place==PossessionPlace.Back,"Only actual worn equipment supplies named container space");
        PlayerRules.Mode=GameMode.Survival;Vitals.Restore();bool capacityConsistent=true;
        foreach(double burst in new[]{0,.01,.1,.5,1})foreach(double fatigue in new[]{0,.6})
        {
            Vitals.Exertion.Burst=burst;Vitals.Exertion.Fatigue=fatigue;
            double run=TerrainMotion.RunSpeedMps*PlayerEffortSpeedScale(true,false),jog=TerrainMotion.JogSpeedMps*PlayerEffortSpeedScale(false,false);
            capacityConsistent&=run+1e-6>=jog;
        }
        Check(capacityConsistent,"The same exertion limit applies to jogging and sprinting without a control-speed reversal");
        SetInventoryOpen(true);PlayerRules.Mode=GameMode.Survival;Vitals.Restore();ApplyPlayerMode();paused=false;
        motion.Y+=2;motion.Grounded=false;motion.VerticalSpeed=0;double airborneY=motion.Y;
        double clock=PlayerRules.SolarDays,hunger=Vitals.Hunger,campClock=Camp.SimulatedSeconds;yield return new WaitForSecondsRealtime(.6f);
        Check(inventoryOpen&&!Paused&&PlayerRules.SolarDays>clock,"Opening inventory leaves the world clock running");
        Check(motion.Y<airborneY-.3,"Opening inventory in midair does not suspend gravity");
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
        Vitals.Restore();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();Clear();yield return CheckFirstPersonInputFlow(report);Clear();paused=false;
    }
}
