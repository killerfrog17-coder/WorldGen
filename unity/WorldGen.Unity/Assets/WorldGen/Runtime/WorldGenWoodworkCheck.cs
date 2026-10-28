using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckWoodworkReplacement(List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("WOODWORK "+text);report.Add("PASS "+text);Debug.Log("WOODWORK PASS "+text);}
        InitializeInventory();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();ClearInventoryPhysics();Inventory.Items.Clear();Inventory.Crafts.Clear();
        var branch=new Possession{Id="trim-regression-branch",Kind=PossessionKind.Branch,Place=PossessionPlace.Ground,Position=Position,WidthM=.03,DepthM=.03,HeightM=.9,MassKg=.4,WoodSoundness=.95,WoodStraightness=1,Tint=new V(1,1,1)};
        var cutter=new Possession{Id="trim-regression-cutter",Kind=PossessionKind.StoneFlake,Place=PossessionPlace.Ground,Position=Position,WidthM=.1,HeightM=.01,DepthM=.07,MassKg=.07,EdgeSharpness=.8};
        Inventory.Items.Add(branch);Inventory.Items.Add(cutter);SetCraftWorkArea(branch);SetCraftWorkArea(cutter,1);SetInventoryOpen(true);UpdateInventoryVisuals();
        foreach(var action in new[]{ItemCraftAction.TrimBranch,ItemCraftAction.StripBark,ItemCraftAction.DiggingStick})
        {
            RefreshInventoryNearby(true);Check(StartInventoryKnapping(branch.Id,action),"Nearby branch/cutter starts "+action+" with initially empty hands");
            var oldRoot=inventoryVisuals[branch.Id];var job=ActiveItemCraft;job.WorkSeconds=job.RequiredSeconds;UpdateStoneCrafting();
            Check(ActiveItemCraft==null&&!inventoryDrops.ContainsKey(branch.Id)&&!oldRoot.activeSelf,"Completion unregisters result physics before destroying/replacing its old visual: "+action);
            yield return null;yield return null;UpdateInventoryVisuals();RefreshInventoryNearby(true);CaptureInventoryPhysics();
            Check(inventoryDrops.Values.All(d=>d.Body!=null&&d.Collider!=null)&&inventoryVisuals[branch.Id]!=null,"Next frames retain only valid drop bodies after "+action);
        }
        Check(branch.Kind==PossessionKind.DiggingStick&&branch.BarkRemoved&&Inventory.Items.Count==2,"Work keeps the same branch identity and cutter without losing/duplicating items");
        Check(MovePossession(branch.Id,PossessionPlace.RightHand),"Finished wooden item can still be picked up");
        Check(MovePossession(branch.Id,PossessionPlace.Ground),"Finished wooden item can still be physically dropped");
        yield return null;yield return null;CaptureInventoryPhysics();
        var saved=WorldJson.Read<PlayerInventory>(WorldJson.Write(Inventory));saved.Validate();Check(saved.Items.Any(p=>p.Id==branch.Id&&p.Kind==PossessionKind.DiggingStick),"Inventory/physics capture remains saveable after woodworking");
        var stale=inventoryDrops[branch.Id];Destroy(stale.Body);yield return null;
        RefreshInventoryNearby(true);CaptureInventoryPhysics();UpdateInventoryVisuals();
        Check(inventoryDrops.Values.All(d=>d.Body!=null),"Unexpected retired native body cannot poison subsequent updates or save capture");
        ClearInventoryPhysics();Check(inventoryDrops.Count==0,"World/menu cleanup retires all item physics without exceptions");SetInventoryOpen(false);
    }
}
