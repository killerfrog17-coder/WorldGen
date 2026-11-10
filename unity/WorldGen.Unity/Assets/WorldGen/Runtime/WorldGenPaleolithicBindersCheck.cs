using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    IEnumerator CheckPaleolithicBindersFlow(string output,List<string> report)
    {
        Application.targetFrameRate=60;
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);report.Add("PASS "+text);Debug.Log("BINDERS CHECK PASS "+text);}
        IEnumerator Shot(string name)
        {for(int i=0;i<24;i++)yield return null;yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();Check(image.GetPixels32().Any(p=>p.r>45||p.g>45||p.b>45),"Rendered native game content: "+name);File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);}
        Possession Add(PossessionKind kind)
        {var p=PortableTechnology.Item(kind)!=null?PortableTechnology.Create(kind,Position):HumanEquipment.Item(kind,557,"native-binder",Position);Inventory.Items.Add(p);SetCraftWorkArea(p);return p;}
        IEnumerator WorkDone()
        {double until=Time.unscaledTimeAsDouble+20;while(worldToolWork!=null&&Time.unscaledTimeAsDouble<until)yield return null;Check(worldToolWork==null,"Timed native material work finishes");}
        IEnumerator Finish(string id,Possession p)
        {RefreshInventoryNearby(true);Check(StartPortableCraft(id,p.Id),"Actual contextual action starts: "+id);double limit=Time.unscaledTimeAsDouble+20;while(Inventory.Crafts.Any(j=>j.WorkpieceId==p.Id)&&Time.unscaledTimeAsDouble<limit)yield return null;Check(!Inventory.Crafts.Any(j=>j.WorkpieceId==p.Id),"Timed native crafting finishes: "+id);Inventory.Validate();}
        InitializeInventory();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();PlayerRules.Settings.ItemCraftingTimeMultiplier=.1;
        WeatherControl.Set("clear");PlayerRules.SetLocalSolarHour(12,Math.Atan2(Position.Y,Position.X)*180/Math.PI);SetDaylight(LocalSolarHour,skyYear);
        double startX=motion.X,startZ=motion.Z;bool found=false;
        foreach(double radius in new[]{0d,30,100,300,900,1800})
        {
            for(int i=0;i<16;i++)
            {double a=i*Math.PI/8,x=startX+Math.Sin(a)*radius,z=startZ+Math.Cos(a)*radius;var sample=walkingPatch.Sample(frame.Position(x,z));if(sample.Underwater||!PaleolithicBinders.OchreGround(sample.Ground))continue;double fx=x-1.3,fz=z-.8;double grade=Math.Max(Math.Abs(CampFloor(fx+.4,fz)-CampFloor(fx-.4,fz)),Math.Abs(CampFloor(fx,fz+.4)-CampFloor(fx,fz-.4)))/.8;if(grade>.12||walkingPatch.Sample(frame.Position(fx,fz)).Underwater)continue;motion.X=x;motion.Z=z;motion.Y=Ground(x,z)+TerrainMotion.EyeHeightM;motion.Grounded=true;found=true;break;}
            if(found)break;yield return null;
        }
        Check(found,"A real iron-rich deposit with a nearby flat work area exists");UpdateCamera();var digger=Add(PossessionKind.DiggingStick);Check(OchreDigReason().Length==0,"Actual soil enables ochre digging");BeginDigOchre(digger);yield return WorkDone();var ore=Inventory.Items.LastOrDefault(p=>p.Kind==PossessionKind.RawOchre);Check(ore!=null,"Actual finite ground work yields ochre-rich earth");
        var mill=Add(PossessionKind.Grindstone);var hammer=HumanEquipment.Item(PossessionKind.SmallRock,557,"r",Position);Inventory.Items.Add(hammer);SetCraftWorkArea(hammer);var water=Add(PossessionKind.Waterskin);PaleolithicWater.Fill(water,new WaterQuality(LocalWaterKind.River,.05,0,0),.5);MovePossession(water.Id,PossessionPlace.Shoulder);
        SetInventoryOpen(true);yield return Finish("grind-ochre",ore);Check(ore.Kind==PossessionKind.OchrePowder,"The dug mineral becomes an actual pigment batch");
        // Heat a real mixture through the same camp ownership/choice path.
        // Tree-product placement is audited separately; this supplied gum is a
        // raw QA input, never an unearned NPC inventory or a fabricated tree.
        var gum=Add(PossessionKind.PlantGum);yield return Finish("gum-ochre-glue",gum);Check(gum.Kind==PossessionKind.AdhesiveMixture,"Measured gum/ochre/water produce a real unheated mixture");SetInventoryOpen(false);
        var tinder=Add(PossessionKind.TinderBundle);var kindling=Add(PossessionKind.KindlingBundle);double fireX=motion.X-1.3,fireZ=motion.Z-.8;UpdateHumans();UpdateCamera();view.transform.LookAt(transform.TransformPoint(new Vector3((float)(fireX-originX),(float)CampFloor(fireX,fireZ),(float)(fireZ-originZ))));campGroundAt=0;BeginCampPlacement(tinder,CampKind.Fire);Check(campPlaceValid,"Actual work site permits a reachable hearth: "+campPlaceReason);ConfirmCampPlacement();var fire=Camp.Objects.Last();AddCampMaterial(fire,kindling);
        var fuel=WoodCrafting.Branch(new ScatterInstance{Id=611,Kind=ScatterKind.Branch,Position=Position,WidthM=2,HeightM=.07});fuel.Id="native-binder-fuel";fuel.NaturalId=0;fuel.MassKg=8;Inventory.Items.Add(fuel);SetCraftWorkArea(fuel);AddCampMaterial(fire,fuel);AddCampMaterial(fire,mill);Check(fire.Materials.Any(p=>p.Id==mill.Id),"Actual grinding stone supplies the hearth warming surface");LightCamp(fire);
        RefreshCampChoices(fire,true);campChoiceIndex=campChoices.FindIndex(c=>c.Item==gum&&c.Action==0);Check(campChoiceIndex>=0,"The actual mixture appears in quick-fire warming choices");RememberCampChoice();QuickFireAction(fire);Check(fire.CookingItems.Contains(gum)&&Inventory.Find(gum.Id)==null,"Warming transfers this exact mixture into the hearth");for(int i=0;i<120&&gum.Kind!=PossessionKind.CompoundAdhesive;i++)CampRules.Advance(fire,30,0,.4,0,.2);Check(gum.Kind==PossessionKind.CompoundAdhesive,"Actual fueled warming produces compound adhesive");
        RefreshCampChoices(fire,true);campChoiceIndex=campChoices.FindIndex(c=>c.Item==gum&&c.Action==1);RememberCampChoice();QuickFireAction(fire);Check(Inventory.Find(gum.Id)==gum,"The exact prepared glue returns from the fire");
        var spear=Add(PossessionKind.StoneTippedSpear);double mass=gum.MassKg;Check(BondHaftReason(spear).Length==0,"Live hearth and actual glue permit haft sealing");BeginBondHaft(spear);yield return WorkDone();Check(spear.HaftBonded&&Math.Abs(gum.MassKg-(mass-.005))<1e-8,"Timed application transfers exactly five grams into the same spear");
        var pigment=Add(PossessionKind.OchrePowder);Check(PaintItemReason(spear).Length==0,"Actual remaining pigment and water enable markings");BeginPaintItem(spear);yield return WorkDone();Check(spear.PigmentMarked,"Timed application marks the actual spear");
        foreach(var held in Inventory.Items.Where(p=>p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand).ToArray())MovePossession(held.Id,PossessionPlace.Ground);
        Check(MovePossession(spear.Id,PossessionPlace.RightHand),"Painted spear can be held normally");SetInventoryOpen(true);RenderInventoryPreview();inventoryContext=new NearbyPossession{Item=spear};inventoryMenuAt=new Vector2(Screen.width*.65f,Screen.height*.2f);yield return Shot("ochre-marked-bonded-spear");inventoryContext=null;SetInventoryOpen(false);
        for(int i=0;i<180;i++)CampRules.Advance(fire,60,0,.4,0,.2);fire.Burning=false;fire.EmberSeconds=0;Check(fire.AshKg>=.03,"Actual burned fuel supplies finite charcoal-bearing residue");double ash=fire.AshKg;GatherFireCharcoal(fire);yield return WorkDone();Check(Inventory.Items.Any(p=>p.Kind==PossessionKind.Charcoal)&&Math.Abs(fire.AshKg-(ash-.03))<1e-8,"Raking creates actual charcoal and removes thirty grams of hearth residue");
        Inventory.Validate();var saved=new PlaySession();StorePlayer(saved);WorldJson.Read<PlaySession>(WorldJson.Write(saved)).Validate();Check(true,"Full native capture/reload retains worked mineral patches, pigments and the bonded weapon");
    }
}
