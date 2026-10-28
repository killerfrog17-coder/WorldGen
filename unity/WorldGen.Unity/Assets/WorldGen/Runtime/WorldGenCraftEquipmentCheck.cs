using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    IEnumerator CheckAutomaticCraftEquipment(List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);report.Add("PASS "+text);Debug.Log("CRAFT EQUIPMENT PASS "+text);}
        InitializeInventory();ExecuteGive(new[]{"/give","wooden","spear"});ExecuteGive(new[]{"/give","small","rock"});
        var right=Inventory.AtHand(PossessionPlace.RightHand);var left=Inventory.AtHand(PossessionPlace.LeftHand);
        Check(right?.Kind==PossessionKind.WoodenSpear&&left?.Kind==PossessionKind.SmallRock,"Craft equipment fixture fills both hands");
        var fibre=PortableTechnology.Create(PossessionKind.RawFibre,Position);Inventory.Items.Add(fibre);SetCraftWorkArea(fibre);
        PlayerRules.Settings.ItemCraftingTimeMultiplier=1;SetInventoryOpen(true);RefreshInventoryNearby(true);
        Check(StartPortableCraft("prepare-fibres",fibre.Id),"Craft starts with occupied hands without a put-away prompt");
        var job=ActiveItemCraft;Check(job.Equipment.PreviousHands.Count==2&&right.Place==PossessionPlace.Ground&&left.Place==PossessionPlace.Ground,"No bag: previous items are real ground drops");
        var saved=WorldJson.Read<PlayerInventory>(WorldJson.Write(Inventory));saved.Validate();Check(saved.Crafts[0].Equipment.PreviousHands.Count==2,"Previous equipment survives saved work");
        PauseItemCraft(job);Check(Inventory.AtHand(PossessionPlace.RightHand)==right&&Inventory.AtHand(PossessionPlace.LeftHand)==left,"Pause retrieves the same dropped spear and rock into their original hands");
        PlayerRules.Settings.ItemCraftingTimeMultiplier=.1;Check(StartPortableCraft("prepare-fibres",fibre.Id),"Resume automatically borrows hands again");
        double deadline=Time.unscaledTimeAsDouble+12;while(Inventory.Crafts.Count>0&&Time.unscaledTimeAsDouble<deadline)yield return null;
        Check(fibre.Kind==PossessionKind.PreparedFibre&&Inventory.AtHand(PossessionPlace.RightHand)==right&&Inventory.AtHand(PossessionPlace.LeftHand)==left,"Completion restores original hands and keeps the finished product");
        Check(StartPortableCraft("twist-cord",fibre.Id),"Another action automatically stages hands");CancelItemCraft(ActiveItemCraft);
        Check(Inventory.AtHand(PossessionPlace.RightHand)==right&&Inventory.AtHand(PossessionPlace.LeftHand)==left&&fibre.Kind==PossessionKind.PreparedFibre,"Cancellation restores equipment without consuming inputs");
        foreach(var p in new[]{right,left}){p.Place=PossessionPlace.Ground;SetCraftWorkArea(p);}
        var bow=PortableTechnology.Create(PossessionKind.Bow,Position);Inventory.Items.Add(bow);Inventory.Hold(bow.Id,out _);RefreshInventoryNearby(true);
        Check(StartPortableCraft("twist-cord",fibre.Id)&&bow.Place==PossessionPlace.SlungBack,"Self bow is slung on the back before using hands");CancelItemCraft(ActiveItemCraft);
        Check(Inventory.AtHand(PossessionPlace.RightHand)==bow,"Slung bow returns to the original hand");Inventory.Validate();
        ClearInventoryPhysics();foreach(var root in inventoryVisuals.Values.ToArray())DestroyInventoryVisual(root);inventoryVisuals.Clear();inventoryVisualPlaces.Clear();
        PlayerRules.Inventory=new PlayerInventory();inventoryNearbyAt=0;RenderInventoryPreview();
    }
}
