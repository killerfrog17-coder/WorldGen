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
    IEnumerator CheckPaleolithicClayHeatFlow(string output,List<string> report)
    {
        Application.targetFrameRate=60;
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);report.Add("PASS "+text);Debug.Log("CLAY HEAT CHECK PASS "+text);}
        IEnumerator Shot(string name)
        {for(int i=0;i<20;i++)yield return null;yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();Check(image.GetPixels32().Any(p=>p.r>45||p.g>45||p.b>45),"Rendered native game content: "+name);File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);}
        Possession Add(PossessionKind kind)
        {var p=PortableTechnology.Item(kind)!=null?PortableTechnology.Create(kind,Position):HumanEquipment.Item(kind,321,"native-clay-tool",Position);Inventory.Items.Add(p);SetCraftWorkArea(p);return p;}
        InitializeInventory();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();PlayerRules.Settings.ItemCraftingTimeMultiplier=.1;
        WeatherControl.Set("clear");PlayerRules.SetLocalSolarHour(12,Math.Atan2(Position.Y,Position.X)*180/Math.PI);SetDaylight(LocalSolarHour,skyYear);
        // Locate real clay in the fixture, without changing the ground profile.
        double ox=motion.X,oz=motion.Z;bool found=false;
        foreach(double radius in new[]{0d,30,100,300,900,1800})
        {
            for(int i=0;i<16;i++)
            {
                double a=i*Math.PI/8,x=ox+Math.Sin(a)*radius,z=oz+Math.Cos(a)*radius;var sample=walkingPatch.Sample(frame.Position(x,z));
                if(sample.Underwater||!PaleolithicClay.ClayGround(sample.Ground))continue;
                double fx=x-1.3,fz=z-.8;
                double grade=Math.Max(Math.Abs(CampFloor(fx+.4,fz)-CampFloor(fx-.4,fz)),Math.Abs(CampFloor(fx,fz+.4)-CampFloor(fx,fz-.4)))/.8;
                if(grade>.12||walkingPatch.Sample(frame.Position(fx,fz)).Underwater)continue;
                motion.X=x;motion.Z=z;motion.Y=Ground(x,z)+TerrainMotion.EyeHeightM;motion.Grounded=true;found=true;break;
            }
            if(found)break;yield return null;
        }
        Check(found,"A real accessible clay-rich deposit exists near the test site");UpdateCamera();
        var digger=Add(PossessionKind.DiggingStick);Check(DigClayReason().Length==0,"Actual terrain enables contextual clay digging: "+DigClayReason());
        BeginDigClay(digger);double limit=Time.unscaledTimeAsDouble+20;
        while(worldToolWork!=null&&Time.unscaledTimeAsDouble<limit)yield return null;
        var clay=Inventory.Items.LastOrDefault(p=>p.Kind==PossessionKind.RawClay);Check(clay!=null&&worldToolWork==null,"Timed digging produces clay from the finite ground ledger");
        var skin=Add(PossessionKind.Waterskin);PaleolithicWater.Fill(skin,new WaterQuality(LocalWaterKind.River,.05,.3,0),1);
        Check(MovePossession(clay.Id,PossessionPlace.Ground),"Set freshly gathered clay in the work area");
        var spear=Add(PossessionKind.StoneTippedSpear);Check(MovePossession(spear.Id,PossessionPlace.RightHand),"Spear actually occupies the right hand before shaping");Check(MovePossession(skin.Id,PossessionPlace.Shoulder),"Water vessel is worn before shaping");
        SetInventoryOpen(true);RefreshInventoryNearby(true);Check(StartPortableCraft("shape-clay-pot",clay.Id),"Real contextual clay shaping starts with occupied hands");limit=Time.unscaledTimeAsDouble+20;
        while(Inventory.Crafts.Any(j=>j.WorkpieceId==clay.Id)&&Time.unscaledTimeAsDouble<limit)yield return null;
        Check(clay.Kind==PossessionKind.UnfiredClayPot&&Math.Abs(skin.WaterLitres-.9)<1e-8,"Native crafting makes greenware and consumes exactly 100 ml");
        Check(!clay.Dried&&clay.ProcessClockSet&&Inventory.AtHand(PossessionPlace.RightHand)==spear,"New greenware begins wet and the previous spear hand is restored");
        PlayerRules.SolarDays+=3;clayUpdateAt=0;UpdateClayDrying();Check(clay.Dried,"Real elapsed play days dry the same clay pot");
        inventoryContext=new NearbyPossession{Item=clay};inventoryMenuAt=new Vector2(Screen.width*.65f,Screen.height*.2f);yield return Shot("air-dried-pot");inventoryContext=null;SetInventoryOpen(false);
        var tinder=Add(PossessionKind.TinderBundle);var kindling=Add(PossessionKind.KindlingBundle);
        double fireX=motion.X-1.3,fireZ=motion.Z-.8;UpdateHumans();UpdateCamera();view.transform.LookAt(transform.TransformPoint(new Vector3((float)(fireX-originX),(float)CampFloor(fireX,fireZ),(float)(fireZ-originZ))));
        campGroundAt=0;BeginCampPlacement(tinder,CampKind.Fire);Check(campPlaceValid,"The real clay site accepts a reachable firing hearth: "+campPlaceReason);ConfirmCampPlacement();var fire=Camp.Objects.Last();AddCampMaterial(fire,kindling);
        var fuel=WoodCrafting.Branch(new ScatterInstance{Id=919,Kind=ScatterKind.Branch,Position=Position,WidthM=2,HeightM=.08});fuel.Id="native-pottery-fuel";fuel.NaturalId=0;fuel.MassKg=10;Inventory.Items.Add(fuel);SetCraftWorkArea(fuel);AddCampMaterial(fire,fuel);LightCamp(fire);
        RefreshCampChoices(fire,true);campChoiceIndex=campChoices.FindIndex(c=>c.Item==clay&&c.Action==0);Check(campChoiceIndex>=0,"Air-dried pot appears as Fire in the existing quick-fire controls");RememberCampChoice();QuickFireAction(fire);
        Check(fire.CookingItems.Contains(clay)&&Inventory.Find(clay.Id)==null,"Firing transfers the actual pot into the hearth");
        for(int i=0;i<600&&clay.Kind!=PossessionKind.FiredClayPot;i++)CampRules.Advance(fire,60,0,.3,0,.2);
        Check(clay.Kind==PossessionKind.FiredClayPot&&fire.FuelKg<10,"Fueled hearth firing changes the same pot into earthenware");
        PaleolithicWater.Fill(clay,new WaterQuality(LocalWaterKind.River,.05,.3,0),1.5);
        for(int i=0;i<100&&!clay.WaterBoiled;i++)CampRules.Advance(fire,30,0,.3,0,.2);
        Check(clay.WaterBoiled&&clay.WaterContamination<.001,"Actual in-fire earthenware heats and treats its finite contents");
        RefreshCampChoices(fire,true);campChoiceIndex=campChoices.FindIndex(c=>c.Item==clay&&c.Action==1);RememberCampChoice();QuickFireAction(fire);
        Check(Inventory.Find(clay.Id)==clay&&clay.WaterLitres==1.5,"The same filled hot-water pot returns through the fire controls");
        var stones=HumanEquipment.Item(PossessionKind.LargeRock,321,"r",Position);stones.MassKg=4;Inventory.Items.Add(stones);SetCraftWorkArea(stones);AddCampMaterial(fire,stones);
        Check(CampWaterHeating.Reason(fire,skin).Length>0,"Newly added stones are cold");
        for(int i=0;i<180&&CampWaterHeating.Reason(fire,skin).Length>0;i++)CampRules.Advance(fire,30,0,.3,0,.2);
        Check(StoneBoilReason(skin).Length==0,"The real hearth, water and handling stick allow stone boiling: "+StoneBoilReason(skin));
        double energy=fire.StoneHeatJ;BeginStoneBoiling(skin);limit=Time.unscaledTimeAsDouble+20;while(worldToolWork!=null&&Time.unscaledTimeAsDouble<limit)yield return null;
        Check(skin.WaterBoiled&&skin.WaterContamination<.001&&fire.StoneHeatJ<energy,"Timed stone boiling treats the actual waterskin and draws stored hearth heat");
        SetInventoryOpen(true);inventoryContext=new NearbyPossession{Item=clay};inventoryMenuAt=new Vector2(Screen.width*.65f,Screen.height*.2f);yield return Shot("fired-pot-and-boiled-water");inventoryContext=null;SetInventoryOpen(false);
        Inventory.Validate();var saved=new PlaySession();StorePlayer(saved);WorldJson.Read<PlaySession>(WorldJson.Write(saved)).Validate();Check(true,"Full player, worked ground, thermal hearth and water vessels capture/reload");
    }
}
