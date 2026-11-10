using System;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    Possession HeldWaterVessel()
    {var right=Inventory.AtHand(PossessionPlace.RightHand);if(PaleolithicWater.Vessel(right))return right;var left=Inventory.AtHand(PossessionPlace.LeftHand);return PaleolithicWater.Vessel(left)?left:null;}
    void RefreshVesselMass(Possession item)
    {
        if(inventoryVisuals.TryGetValue(item.Id,out var visual))
        {var body=visual.GetComponent<Rigidbody>();if(body!=null)body.mass=(float)item.MassKg;}
        inventoryNearbyAt=0;
    }
    bool FillWaterVessel(Possession item)
    {
        if(Spectating||Vitals.Incapacitated||!PaleolithicWater.Vessel(item))return false;
        var source=AimedWater(float.PositiveInfinity);if(source==null){InventoryFeedback("Look at water within reach to fill it.");return false;}
        if(PaleolithicWater.Space(item)<.001){InventoryFeedback("The vessel is full.");return false;}
        CancelItemCharge();CancelWorldToolWork();PauseItemCraft(ActiveItemCraft);
        WitnessPropertyUse(item);double added=PaleolithicWater.Fill(item,source.Quality,PaleolithicWater.Space(item));
        if(added<=0)return false;RefreshVesselMass(item);BeginConsumeGesture(true);PlayBodyCue("drink");
        InventoryFeedback("Filled "+added.ToString("0.##")+" L of "+source.Quality.Name.ToLowerInvariant()+".");return true;
    }
    bool DrinkWaterVessel(Possession item)
    {
        if(Spectating||Vitals.Incapacitated||item.WaterLitres<=0)return false;
        CancelItemCharge();CancelWorldToolWork();PauseItemCraft(ActiveItemCraft);WitnessPropertyUse(item);
        double draw=Rng.Value(sourceWorld.Recipe.Seed,"player-water-exposure",Vitals.WaterExposureSequence);
        double taken=PaleolithicWater.Drink(item,Vitals,Mode,.25,draw);if(taken<=0)return false;
        RefreshVesselMass(item);BeginConsumeGesture(true);PlayBodyCue("drink");InventoryFeedback("Drank "+taken.ToString("0.##")+" L; "+item.WaterLitres.ToString("0.##")+" L left.");return true;
    }
    void EmptyWaterVessel(Possession item)
    {
        WitnessPropertyUse(item);double volume=PaleolithicWater.Drain(item,item.WaterLitres);RefreshVesselMass(item);
        InventoryFeedback("Poured out "+volume.ToString("0.##")+" L.");
    }
}
