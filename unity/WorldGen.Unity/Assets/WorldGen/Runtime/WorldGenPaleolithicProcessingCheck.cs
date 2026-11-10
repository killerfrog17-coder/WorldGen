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
    IEnumerator CheckPaleolithicProcessingFlow(string output,List<string> report)
    {
        Application.targetFrameRate=60;
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);report.Add("PASS "+text);Debug.Log("PROCESSING CHECK PASS "+text);}
        IEnumerator Shot(string name)
        {yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();Check(image.GetPixels32().Any(p=>p.r>45||p.g>45||p.b>45),"Rendered native game content: "+name);File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);}
        int supplied=0;
        Possession Add(PossessionKind kind)
        {var p=PortableTechnology.Create(kind,Position);Inventory.Items.Add(p);SetCraftWorkArea(p,supplied++);return p;}
        IEnumerator Finish(string id,Possession item)
        {
            RefreshInventoryNearby(true);Check(StartPortableCraft(id,item.Id),"Actual context action starts: "+id);double limit=Time.unscaledTimeAsDouble+20;
            while(Inventory.Crafts.Any(j=>j.WorkpieceId==item.Id)&&Time.unscaledTimeAsDouble<limit)yield return null;
            Check(!Inventory.Crafts.Any(j=>j.WorkpieceId==item.Id),"Timed native crafting completes: "+id);Inventory.Validate();
        }
        InitializeInventory();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();PlayerRules.Settings.ItemCraftingTimeMultiplier=.1;
        WeatherControl.Set("clear");PlayerRules.SetLocalSolarHour(12,Math.Atan2(Position.Y,Position.X)*180/Math.PI);SetDaylight(LocalSolarHour,skyYear);
        var spear=Add(PossessionKind.StoneTippedSpear);Check(MovePossession(spear.Id,PossessionPlace.RightHand),"Start with an occupied spear hand");
        var granite=new Possession{Id="native-granite",Kind=PossessionKind.LargeRock,Position=Position,MassKg=2.5,WidthM=.24,HeightM=.11,DepthM=.19,CellsWide=3,CellsHigh=2,Variant=ToolStone.Encode(ToolStoneMaterial.Granite,2)};
        var hammer=HumanEquipment.Item(PossessionKind.SmallRock,37,"r",Position);Inventory.Items.Add(granite);Inventory.Items.Add(hammer);SetCraftWorkArea(granite,supplied++);SetCraftWorkArea(hammer,supplied++);
        SetInventoryOpen(true);yield return Finish("peck-grindstone",granite);
        Check(granite.Kind==PossessionKind.Grindstone&&Inventory.AtHand(PossessionPlace.RightHand)==spear,"Nonflaking granite becomes a real grinding tool and the spear is restored");
        var nuts=Add(PossessionKind.Nuts);nuts.FoodSpecies="Test grove";nuts.FoodKcalPerKg=5370;double calories=nuts.MassKg*nuts.FoodKcalPerKg;
        yield return Finish("grind-nuts",nuts);Check(nuts.Kind==PossessionKind.PlantMeal&&Math.Abs(nuts.MassKg*nuts.FoodKcalPerKg-calories*.97)<1e-8,"Native grinding preserves source-specific food calories");
        var vessel=Add(PossessionKind.Waterskin);PaleolithicWater.Fill(vessel,new WaterQuality(LocalWaterKind.River,.08,0,0),.5);MovePossession(vessel.Id,PossessionPlace.Shoulder);
        yield return Finish("plant-cake",nuts);Check(nuts.Kind==PossessionKind.PlantCake&&Math.Abs(vessel.WaterLitres-.42)<1e-8&&Inventory.Find(vessel.Id)==vessel,"Native mixing uses 80 ml and retains the actual reusable vessel");
        inventorySelected=nuts.Id;yield return Shot("ground-food-equipment");SetInventoryOpen(false);
        // Exercise the shared campfire choice path, including actual ownership
        // transfer into and back out of the cooking-item list.
        var tinder=Add(PossessionKind.TinderBundle);var kindling=Add(PossessionKind.KindlingBundle);
        double x=motion.X-1.3,z=motion.Z-.8;UpdateHumans();UpdateCamera();view.transform.LookAt(transform.TransformPoint(new Vector3((float)(x-originX),(float)CampFloor(x,z),(float)(z-originZ))));
        BeginCampPlacement(tinder,CampKind.Fire);Check(campPlaceValid,"Actual dry ground accepts the cooking fire: "+campPlaceReason);ConfirmCampPlacement();var fire=Camp.Objects.Last();AddCampMaterial(fire,kindling);
        var fuel=WoodCrafting.Branch(new ScatterInstance{Id=718,Kind=ScatterKind.Branch,Position=Position,WidthM=2,HeightM=.06});fuel.Id="native-kitchen-fuel";fuel.NaturalId=0;Inventory.Items.Add(fuel);SetCraftWorkArea(fuel);AddCampMaterial(fire,fuel);LightCamp(fire);Check(fire.Burning,"The real campfire is burning");
        RefreshCampChoices(fire,true);campChoiceIndex=campChoices.FindIndex(c=>c.Item==nuts&&c.Action==0);Check(campChoiceIndex>=0,"The new cake appears in the existing quick-fire cooking choices");RememberCampChoice();QuickFireAction(fire);
        Check(fire.CookingItems.Contains(nuts)&&Inventory.Find(nuts.Id)==null,"Adding the cake transfers its actual ownership to the fire");
        for(int i=0;i<100&&!nuts.Cooked;i++)CampRules.Advance(fire,60,0,.4,0,.2);
        Check(nuts.Cooked&&!nuts.Burnt,"The fueled fire cooks the actual crafted cake");InvalidateCampVisual(fire);campUpdateAt=0;UpdateCamp();
        RefreshCampChoices(fire,true);campChoiceIndex=campChoices.FindIndex(c=>c.Item==nuts&&c.Action==1);Check(campChoiceIndex>=0,"The cooked cake can be retrieved through the same fire controls");RememberCampChoice();QuickFireAction(fire);
        Check(Inventory.Find(nuts.Id)==nuts&&!fire.CookingItems.Contains(nuts),"Taking cooked food returns the same item exactly once");SetInventoryOpen(true);inventoryContext=new NearbyPossession{Item=nuts};inventoryMenuAt=new Vector2(Screen.width*.65f,Screen.height*.2f);yield return Shot("cooked-plant-cake");inventoryContext=null;SetInventoryOpen(false);
        Inventory.Validate();var saved=new PlaySession();StorePlayer(saved);WorldJson.Read<PlaySession>(WorldJson.Write(saved)).Validate();Check(true,"Full native player/camp capture accepts processing and cooked food");
    }
}
