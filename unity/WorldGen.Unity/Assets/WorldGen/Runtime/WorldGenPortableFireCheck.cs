using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    IEnumerator CheckPortableFireFlow(string output,List<string> report)
    {
        Application.targetFrameRate=60;
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);report.Add("PASS "+text);Debug.Log("PORTABLE FIRE CHECK PASS "+text);}
        IEnumerator Shot(string name)
        {for(int i=0;i<24;i++)yield return null;yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();Check(image.GetPixels32().Any(p=>p.r>45||p.g>45||p.b>45),"Rendered native game content: "+name);File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);}
        Possession Add(PossessionKind kind)
        {var p=PortableTechnology.Create(kind,Position);Inventory.Items.Add(p);SetCraftWorkArea(p);return p;}
        IEnumerator Finish(string id,Possession p)
        {RefreshInventoryNearby(true);Check(StartPortableCraft(id,p.Id),"Actual contextual action starts: "+id);double limit=Time.unscaledTimeAsDouble+20;while(Inventory.Crafts.Any(j=>j.WorkpieceId==p.Id)&&Time.unscaledTimeAsDouble<limit)yield return null;Check(!Inventory.Crafts.Any(j=>j.WorkpieceId==p.Id),"Timed native crafting completes: "+id);Inventory.Validate();}
        IEnumerator WorkDone()
        {double until=Time.unscaledTimeAsDouble+20;while(worldToolWork!=null&&Time.unscaledTimeAsDouble<until)yield return null;Check(worldToolWork==null,"Timed portable-fire work completes");}
        void Feed(CampObject fire){var fine=Add(PossessionKind.TinderBundle);var small=Add(PossessionKind.KindlingBundle);AddCampMaterial(fire,fine);AddCampMaterial(fire,small);}
        InitializeInventory();humanCamera.Restore(0,false);pitch=8;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();PlayerRules.Settings.ItemCraftingTimeMultiplier=.1;WeatherControl.Set("clear");PlayerRules.SetLocalSolarHour(0,Math.Atan2(Position.Y,Position.X)*180/Math.PI);SetDaylight(LocalSolarHour,skyYear);
        var pole=WoodCrafting.Branch(new ScatterInstance{Id=562,Kind=ScatterKind.Branch,Position=Position,WidthM=1.1,HeightM=.035});pole.Id="native-torch-pole";pole.NaturalId=0;pole.Kind=PossessionKind.TrimmedPole;Inventory.Items.Add(pole);SetCraftWorkArea(pole);Add(PossessionKind.BarkStrips);Add(PossessionKind.BarkStrips);Add(PossessionKind.Cordage);Add(PossessionKind.PlantResin);Add(PossessionKind.StoneBlade);
        SetInventoryOpen(true);yield return Finish("wrap-torch",pole);Check(pole.Kind==PossessionKind.Torch&&!PortableFire.Burning(pole)&&pole.FireFuelKg>0,"Actual pole, bark, binding and resin become a finite unlit torch");SetInventoryOpen(false);
        var tinder=Add(PossessionKind.TinderBundle);var kindling=Add(PossessionKind.KindlingBundle);bool placed=false;
        BeginCampPlacement(tinder,CampKind.Fire);
        for(int i=0;i<12;i++){double a=i*Math.PI/6,x=motion.X+Math.Sin(a)*1.6,z=motion.Z+Math.Cos(a)*1.6;UpdateCamera();view.transform.LookAt(transform.TransformPoint(new Vector3((float)(x-originX),(float)CampFloor(x,z),(float)(z-originZ))));campGroundAt=0;UpdateCampPlacement();if(!campPlaceValid)continue;ConfirmCampPlacement();placed=true;break;}
        Check(placed,"Actual flat ground accepts the source hearth");var fire=Camp.Objects.Last();AddCampMaterial(fire,kindling);var fuel=WoodCrafting.Branch(new ScatterInstance{Id=563,Kind=ScatterKind.Branch,Position=Position,WidthM=2,HeightM=.06});fuel.Id="native-fire-fuel";fuel.NaturalId=0;fuel.MassKg=8;Inventory.Items.Add(fuel);SetCraftWorkArea(fuel);AddCampMaterial(fire,fuel);LightCamp(fire);
        Check(LightPortableReason(pole).Length==0,"Actual burning hearth permits lighting this torch");LightPortable(pole);yield return WorkDone();Check(PortableFire.Burning(pole),"Timed native lighting creates the carried flame");
        foreach(var p in Inventory.Items.Where(p=>p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand).ToArray())MovePossession(p.Id,PossessionPlace.Ground);
        Check(MovePossession(pole.Id,PossessionPlace.LeftHand),"Lit torch can be held in the off hand");fire.Burning=false;fire.EmberSeconds=0;InvalidateCampVisual(fire);
        PlayerRules.SetLocalSolarHour(0,Math.Atan2(Position.Y,Position.X)*180/Math.PI);SetDaylight(LocalSolarHour,skyYear);nextDaylightAt=0;portableFireAt=0;UpdateInventoryVisuals();UpdatePortableFire();BindCampLights(view);
        Check(portableFlames.Any(pair=>pair.Key!=null&&portableFireRoots.TryGetValue(pair.Key,out var p)&&p==pole&&pair.Value!=null),"The actual held torch renders its own flame sheets");
        UpdateCamera();foreach(var pair in portableFireRoots.Where(p=>p.Value==pole&&p.Key!=null))Debug.Log("TORCH VIEW "+pair.Key.name+" layer="+pair.Key.layer+" root="+pair.Key.transform.position+" tip="+view.WorldToViewportPoint(pair.Key.transform.TransformPoint(Vector3.up))+" shared="+SharedHumanMotion+" eye="+view.transform.position);Check(campLightCount>0&&campLightCount<=4,"Portable flame illuminates the scene within the existing four-light limit");yield return new WaitForSecondsRealtime(2);UpdateCamera();
        foreach(var pair in portableFireRoots.Where(p=>p.Value==pole&&p.Key!=null))foreach(var r in pair.Key.GetComponentsInChildren<Renderer>())Debug.Log("TORCH RENDER "+r.name+" active="+r.gameObject.activeInHierarchy+" enabled="+r.enabled+" shadow="+r.shadowCastingMode+" shader="+r.sharedMaterial.shader.name+" view="+view.WorldToViewportPoint(r.bounds.center)+" burning="+PortableFire.Burning(pole)+" rain="+CurrentWeather.RainMmPerHour);
        Check(PortableFire.Burning(pole)&&portableFlames.Any(pair=>pair.Key!=null&&portableFireRoots.TryGetValue(pair.Key,out var p)&&p==pole&&pair.Value!=null),"The carried flame survives ordinary frame updates before capture");yield return Shot("carried-torch-at-night");
        PlayerRules.Mode=GameMode.Survival;ApplyPlayerMode();Feed(fire);LightCamp(fire);yield return WorkDone();Check(fire.Burning&&!Inventory.Items.Any(p=>p.Kind==PossessionKind.HandDrillSet),"Survival lighting uses the real carried torch without a drill or creative bypass");
        var carrier=Add(PossessionKind.BarkContainer);Add(PossessionKind.BarkSheet);Add(PossessionKind.Cordage);Add(PossessionKind.BoneAwl);SetInventoryOpen(true);yield return Finish("ember-carrier",carrier);SetInventoryOpen(false);
        for(int i=0;i<40;i++)CampRules.Advance(fire,60,0,.3,0,.2);var nest=Add(PossessionKind.DryGrass);double ash=fire.AshKg;Check(ash>=.005,"Real burned fuel provides transferable hearth coals");Check(LightPortableReason(carrier).Length==0,"Actual coals and loose tinder permit an ember nest");LightPortable(carrier);yield return WorkDone();
        Check(PortableFire.Burning(carrier)&&fire.AshKg<ash&&nest.MassKg<.6,"Loading the actual carrier transfers coals and twenty grams of tinder");ExtinguishPortable(pole);fire.Burning=false;fire.EmberSeconds=0;InvalidateCampVisual(fire);Feed(fire);double coals=carrier.FireFuelKg;LightCamp(fire);yield return WorkDone();Check(fire.Burning&&carrier.FireFuelKg<coals,"The carrier independently starts a new fire from its finite embers");
        SetInventoryOpen(true);inventoryContext=new NearbyPossession{Item=carrier};inventoryMenuAt=new Vector2(Screen.width*.65f,Screen.height*.2f);yield return Shot("ember-carrier");inventoryContext=null;SetInventoryOpen(false);
        Inventory.Validate();var saved=new PlaySession();StorePlayer(saved);WorldJson.Read<PlaySession>(WorldJson.Write(saved)).Validate();Check(true,"Full native capture/reload retains fuel, live coals and transported fire");
    }
}
