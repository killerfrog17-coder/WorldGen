using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckInventoryPrototype(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("INVENTORY CHECK "+text);report.Add("PASS "+text);Debug.Log("INVENTORY CHECK PASS "+text);}
        var oldBackground=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();TestKeyboard(keyboard);
        IEnumerator KeyPress(Key key)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;}
        IEnumerator Image(string name)
        {yield return new WaitForEndOfFrame();var p=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,name+".png"),p.EncodeToPNG());Destroy(p);}
        try
        {
            Check(Inventory.Items.Count==1&&Inventory.Bag!=null,"one testing bag, no invisible body storage or extra items");
            double deadline=Time.unscaledTimeAsDouble+90;
            while(scatterChunks.Values.Sum(c=>c.Source.Instances.Count(p=>p.Kind==ScatterKind.Stone&&RockPossession(p).Kind==PossessionKind.SmallRock))<2&&Time.unscaledTimeAsDouble<deadline)yield return null;
            var stones=scatterChunks.Values.SelectMany(c=>c.Source.Instances).Where(p=>p.Kind==ScatterKind.Stone&&RockPossession(p).Kind==PossessionKind.SmallRock).Take(2).ToArray();
            Check(stones.Length==2,"real generated ground rocks are available");
            void Aim(ScatterInstance stone)
            {
                TerrainHandoff.Coordinates(frame,stone.Position,out double x,out double z);motion.Place(x+.65,z,Ground(x+.65,z));TestHumanView(0);
                RefreshInventoryNearby(true);var n=inventoryNearby.FirstOrDefault(n=>n.Item.NaturalId==stone.Id);Check(n!=null,"ground rock within actual reach");
                // Yaw from the body centre, not the offset first-person camera:
                // repeatedly aiming around its 30cm forward offset oscillates.
                heading=Mathf.Atan2(n.Point.x-(float)(motion.X-originX),n.Point.z-(float)(motion.Z-originZ))*Mathf.Rad2Deg;
                UpdateCamera();var dir=(n.Point-view.transform.localPosition).normalized;pitch=-Mathf.Asin(dir.y)*Mathf.Rad2Deg;
                UpdateCamera();Debug.Log("INVENTORY AIM PREP point="+n.Point+" ray="+new Ray(view.transform.localPosition,view.transform.forward)+" screen="+view.WorldToViewportPoint(n.Point));
            }
            Aim(stones[0]);yield return KeyPress(Key.E);
            Check(Inventory.AtHand(PossessionPlace.RightHand)?.NaturalId==stones[0].Id&&CollectedRock(stones[0]),"E picks actual stone into right hand and hides original: "+string.Join(",",Inventory.Items.Select(i=>i.Name+" "+i.Place+" "+i.NaturalId)));
            Aim(stones[1]);yield return KeyPress(Key.E);
            Check(Inventory.AtHand(PossessionPlace.LeftHand)?.NaturalId==stones[1].Id,"second pickup occupies free left hand");
            yield return Image("held-rocks");
            yield return KeyPress(Key.R);Check(InventoryBusy,"bag stow has a deliberate access delay");yield return new WaitForSecondsRealtime(.8f);
            var stored=Inventory.Items.First(i=>i.NaturalId==stones[1].Id);
            Check(stored.Place==PossessionPlace.Container&&Inventory.AtHand(PossessionPlace.LeftHand)==null,"R stows active item and releases hand");
            double loaded=Inventory.CarriedMassKg;string bagId=Inventory.Bag.Id;
            yield return KeyPress(Key.B);yield return new WaitForSecondsRealtime(.8f);RefreshInventoryNearby(true);
            Check(Inventory.Bag==null&&stored.ContainerId==bagId&&Inventory.CarriedMassKg<loaded,"B sets down loaded bag without spilling contents");
            Check(inventoryNearby.Any(n=>n.Item.Id==bagId),"dropped bag and its contents accessible through Nearby");
            yield return KeyPress(Key.B);yield return new WaitForSecondsRealtime(.8f);
            Check(Inventory.Bag?.Id==bagId&&Math.Abs(Inventory.CarriedMassKg-loaded)<1e-8,"B re-equips same bag and restores real load");
            yield return KeyPress(Key.Q);yield return KeyPress(Key.G);
            Check(Inventory.AtHand(PossessionPlace.RightHand)==null&&Inventory.Items.First(i=>i.NaturalId==stones[0].Id).Place==PossessionPlace.Ground,"Q selects other hand and G drops its actual item");
            Inventory.Validate();
            var copy=WorldJson.Read<PlayerInventory>(WorldJson.Write(Inventory));copy.Validate();Check(copy.Items.Count==3&&copy.CollectedNatural.Count==2,"packing, hands, dropped item and depleted roots survive serialization");
            Check(!MovePossession(stored.Id,PossessionPlace.Container,bagId,6,0),"live invalid packing rejects out-of-bounds transfer");
            inventoryNotice="";inventoryNoticeUntil=0;
            yield return KeyPress(Key.I);Check(inventoryOpen&&Paused&&!WantsMouseLook,"I opens inventory, pauses player and releases cursor");
            Check(inventoryPortrait!=null&&inventoryPreviewActor!=null&&inventoryPreviewCamera.cullingMask==(1<<29),"rotatable native human rendered by isolated portrait camera");
            var active=Inventory.ActiveHand;yield return KeyPress(Key.Q);Check(Inventory.ActiveHand!=active,"hand hotkeys remain usable while inventory is open");
            inventoryPreviewYaw=160;RenderInventoryPreview();yield return Image("inventory-front");
            inventoryPreviewYaw=250;RenderInventoryPreview();yield return Image("inventory-side");
            var before=Inventory.Items.Count;inventoryDrag=new NearbyPossession{Item=stored};inventoryDragRotated=false;
            yield return KeyPress(Key.R);Check(inventoryDragRotated&&Inventory.Items.Count==before,"R rotates drag preview without moving or duplicating ownership");inventoryDrag=null;
            yield return KeyPress(Key.Tab);Check(!inventoryOpen&&!Paused,"Tab closes inventory");
            Teleport(Position);yield return null;Check(CollectedRock(stones[0])&&CollectedRock(stones[1]),"terrain/chart reload cannot restore collected natural rocks");
            var detached=new PlaySession();StorePlayer(detached);Check(detached.Inventory.Items.Count==Inventory.Items.Count,"capture into a fresh session includes inventory");detached.Inventory.Items.Clear();Check(Inventory.Items.Count==3,"captured inventory is independent");
            File.WriteAllLines(Path.Combine(output,"inventory-checks.txt"),report);
        }
        finally{TestKeyboard(null);InputSystem.RemoveDevice(keyboard);InputSystem.settings.backgroundBehavior=oldBackground;}
    }
}
public sealed partial class WorldGenRuntime
{
    IEnumerator CheckPlayerInventoryFlow()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);AudioListener.volume=0;
        var report=new List<string>();var errors=new List<string>();Application.LogCallback listener=(m,t,k)=>{if(k==LogType.Error||k==LogType.Exception)errors.Add(m);};Application.logMessageReceived+=listener;
        double deadline=Time.realtimeSinceStartupAsDouble+180;
        while((Busy||!worldReady||humans==null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(!worldReady||Busy||humans==null)throw new InvalidOperationException("Inventory fixture unavailable");
        TerrainVideoOptions.Current.ViewDistanceM=1000;TerrainVideoOptions.Revision++;
        ExploreTerrain(TerrainMapViewport.FromDegrees(50,10));deadline=Time.realtimeSinceStartupAsDouble+120;
        while((terrainExplorer==null||!terrainExplorer.Ready||!terrainExplorer.CenterReady)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(terrainExplorer==null||!terrainExplorer.Ready)throw new InvalidOperationException("Inventory terrain unavailable");
        yield return terrainExplorer.CheckInventoryPrototype(output,report);
        CapturePlayer();var restored=WorldJson.Read<PlaySession>(WorldJson.Write(play));restored.Validate();
        if(restored.Inventory.Items.Count!=3||restored.Inventory.CollectedNatural.Count!=2)throw new InvalidOperationException("Session lost inventory");report.Add("PASS full play-session inventory round trip");
        ReturnFromPlay("Simulation");yield return null;yield return null;
        Application.logMessageReceived-=listener;if(errors.Count>0)throw new InvalidOperationException(string.Join(" | ",errors));report.Add("PASS no runtime errors or exceptions");
        File.WriteAllLines(Path.Combine(output,"inventory-checks.txt"),report);Debug.Log("PLAYER INVENTORY FLOW PASS "+report.Count);Application.Quit(0);
    }
}
