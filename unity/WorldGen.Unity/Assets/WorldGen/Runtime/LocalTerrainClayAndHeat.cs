using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    double clayUpdateAt;
    void UpdateClayDrying()
    {
        if(Time.unscaledTimeAsDouble<clayUpdateAt)return;clayUpdateAt=Time.unscaledTimeAsDouble+1;
        foreach(var p in Inventory.Items)
            if(PaleolithicClay.Green(p))PaleolithicClay.CatchUp(p,PlayerRules.SolarDays,p.Place==PossessionPlace.Ground?CurrentWeather.RainMmPerHour:0,CurrentWeather.Humidity);
    }
    string DigClayReason()
    {
        if(HeightAboveGround>3)return "Stand on the ground to dig.";
        var sample=walkingPatch.Sample(Position);
        if(sample.Underwater||CurrentWeather.TemperatureC< -2)return "This ground is submerged or frozen.";
        if(!PaleolithicClay.ClayGround(sample.Ground))return "Look for deep, fine clay-rich soil.";
        return (Inventory.ToolResources.FirstOrDefault(r=>r.Id==GroundWorkKey(Position))?.Clay??0)>=2?"This clay patch has already been worked.":"";
    }
    void BeginDigClay(Possession tool)
    {
        string reason=DigClayReason();if(reason.Length>0){InventoryFeedback(reason);return;}
        var record=ToolResource(GroundWorkKey(Position));
        BeginWorldToolWork(tool,"Digging clay",180,()=>{record.Clay++;ReceiveToolMaterial(PortableTechnology.Create(PossessionKind.RawClay,Position));});
    }
    CampObject NearbyHeatingFire()=>Camp.Objects.Where(c=>c.Kind==CampKind.Fire&&(c.Position-Position).Length*frame.RadiusM<2.5).OrderByDescending(c=>c.FirePowerKw>0||c.StoneHeatJ>1000).ThenBy(c=>(c.Position-Position).Length).FirstOrDefault();
    Possession BoilingStick()=>CraftAccessible().FirstOrDefault(p=>WoodCrafting.Wood(p)&&p.HeightM>=.35&&p.WidthM<=.06&&p.WoodSoundness>.3);
    string StoneBoilReason(Possession vessel)
    {
        var reason=CampWaterHeating.Reason(NearbyHeatingFire(),vessel);
        return reason.Length>0?reason:BoilingStick()==null?"Bring a sound stick to handle the heated stones.":"";
    }
    void BeginStoneBoiling(Possession vessel)
    {
        string reason=StoneBoilReason(vessel);if(reason.Length>0){InventoryFeedback(reason);return;}
        var fire=NearbyHeatingFire();var stick=BoilingStick();WitnessPropertyUse(vessel);WitnessPropertyUse(stick);
        BeginWorldToolWork(vessel,"Boiling water with heated stones",120+vessel.WaterLitres*120,()=>
        {
            if(!Camp.Objects.Contains(fire)||(fire.Position-Position).Length*frame.RadiusM>=2.5||!AccessiblePossession(stick)||!CampWaterHeating.Boil(fire,vessel))
            {InventoryFeedback("The water or heated stones are no longer ready.");return;}
            InventoryFeedback("Water boiled. Salt remains in salt water.");RefreshVesselMass(vessel);
        });
        inventoryContext=null;
    }
}
