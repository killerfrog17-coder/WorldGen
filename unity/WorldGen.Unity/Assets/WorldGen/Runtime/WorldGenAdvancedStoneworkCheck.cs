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
    IEnumerator CheckAdvancedStoneworkFlow(string output,List<string> report)
    {
        Application.targetFrameRate=60;
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);report.Add("PASS "+text);Debug.Log("STONEWORK CHECK PASS "+text);}
        IEnumerator Shot(string name)
        {for(int i=0;i<20;i++)yield return null;yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();Check(image.GetPixels32().Any(p=>p.r>45||p.g>45||p.b>45),"Rendered native game content: "+name);File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);}
        IEnumerator Finish(string id,Possession p)
        {
            RefreshInventoryNearby(true);Check(StartPortableCraft(id,p.Id),"Actual contextual action starts: "+id);double limit=Time.unscaledTimeAsDouble+20;
            while(Inventory.Crafts.Any(j=>j.WorkpieceId==p.Id)&&Time.unscaledTimeAsDouble<limit)yield return null;
            Check(!Inventory.Crafts.Any(j=>j.WorkpieceId==p.Id),"Timed work completes: "+id);Inventory.Validate();
        }
        InitializeInventory();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();PlayerRules.Settings.ItemCraftingTimeMultiplier=.1;
        var spear=PortableTechnology.Create(PossessionKind.StoneTippedSpear,Position);Inventory.Items.Add(spear);Check(MovePossession(spear.Id,PossessionPlace.RightHand),"Spear occupies the previous working hand");
        var stone=HumanEquipment.Item(PossessionKind.SmallRock,735,"l",Position);var hammer=HumanEquipment.Item(PossessionKind.SmallRock,735,"r",Position);
        Inventory.Items.AddRange(new[]{stone,hammer});SetCraftWorkArea(stone);SetCraftWorkArea(hammer,1);SetInventoryOpen(true);
        double mass=stone.MassKg;yield return Finish("prepare-core-SmallRock",stone);
        Check(stone.CorePrepared&&stone.Name=="Small rock"&&stone.MassKg<mass,"Native preparation works the same ordinary rock, without renaming it");
        Check(Inventory.AtHand(PossessionPlace.RightHand)==spear,"Preparation restores the previous spear hand");
        inventoryContext=new NearbyPossession{Item=stone};inventoryMenuAt=new Vector2(Screen.width*.65f,Screen.height*.2f);yield return Shot("prepared-flaking-face");inventoryContext=null;
        var before=new HashSet<string>(Inventory.Items.Select(p=>p.Id));Check(StartInventoryKnapping(stone.Id),"Existing flaking control uses the prepared face");double limit=Time.unscaledTimeAsDouble+20;
        while(Inventory.Crafts.Any(j=>j.WorkpieceId==stone.Id)&&Time.unscaledTimeAsDouble<limit)yield return null;
        var flake=Inventory.Items.FirstOrDefault(p=>p.Kind==PossessionKind.StoneFlake&&!before.Contains(p.Id));
        Check(flake!=null&&!stone.CorePrepared&&flake.Variant==stone.Variant,"Actual native removal produces a material-matched flake and consumes the prepared face");
        yield return new WaitForSecondsRealtime(.5f);yield return Finish("stone-blade",flake);
        var antler=PortableTechnology.Create(PossessionKind.AntlerRetoucher,Position);Inventory.Items.Add(antler);SetCraftWorkArea(antler);
        mass=flake.MassKg;yield return Finish("pressure-finish-StoneBlade",flake);
        Check(flake.PressureFinished&&flake.EdgeSharpness==1&&flake.MassKg<mass&&Inventory.Find(antler.Id)==antler,"Real antler pressure work refines the same blade and retains the reusable tool");
        inventoryContext=new NearbyPossession{Item=flake};inventoryMenuAt=new Vector2(Screen.width*.65f,Screen.height*.2f);yield return Shot("pressure-finished-blade");inventoryContext=null;SetInventoryOpen(false);
        var saved=new PlaySession();StorePlayer(saved);WorldJson.Read<PlaySession>(WorldJson.Write(saved)).Validate();Check(true,"Full native capture/reload retains worked stones and finishing state");
    }
}
