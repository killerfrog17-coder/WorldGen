using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    string OchreDigReason()
    {
        if(HeightAboveGround>3)return "Stand on the ground to dig.";var sample=walkingPatch.Sample(Position);
        if(sample.Underwater||CurrentWeather.TemperatureC< -2)return "This ground is submerged or frozen.";
        if(!PaleolithicBinders.OchreGround(sample.Ground))return "Look for iron-rich, fine reddish earth.";
        return (Inventory.ToolResources.FirstOrDefault(r=>r.Id==GroundWorkKey(Position))?.Ochre??0)>=2?"This ochre patch has already been worked.":"";
    }
    void BeginDigOchre(Possession tool)
    {
        string reason=OchreDigReason();if(reason.Length>0){InventoryFeedback(reason);return;}var record=ToolResource(GroundWorkKey(Position));
        BeginWorldToolWork(tool,"Digging ochre-rich earth",120,()=>{record.Ochre++;ReceiveToolMaterial(PortableTechnology.Create(PossessionKind.RawOchre,Position));});inventoryContext=null;
    }
    Possession AccessibleGlue()=>CraftAccessible().FirstOrDefault(p=>p.Kind==PossessionKind.CompoundAdhesive&&p.MassKg>=.005);
    string BondHaftReason(Possession item)
    {
        if(item.HaftBonded)return "This haft is already bonded.";
        if(AccessibleGlue()==null)return "Bring at least 5 g of prepared compound adhesive.";
        if((NearbyHeatingFire()?.FirePowerKw??0)<.5)return "Warm the adhesive beside a live hearth.";
        return "";
    }
    void RefreshChangedPossession(Possession item)
    {
        if(inventoryVisuals.TryGetValue(item.Id,out var visual)){RemoveDroppedBody(item.Id);DestroyInventoryVisual(visual);inventoryVisuals.Remove(item.Id);inventoryVisualPlaces.Remove(item.Id);}
        inventoryNearbyAt=0;if(inventoryOpen)RenderInventoryPreview();
    }
    void RemoveEmptyBinder(Possession item)
    {if(item.MassKg>1e-8)return;Inventory.Items.Remove(item);RefreshChangedPossession(item);}
    void BeginBondHaft(Possession item)
    {
        var reason=BondHaftReason(item);if(reason.Length>0){InventoryFeedback(reason);return;}var glue=AccessibleGlue();WitnessPropertyUse(item);WitnessPropertyUse(glue);
        BeginWorldToolWork(item,"Warming and sealing the haft",180,()=>
        {
            if(!CraftAccessible().Contains(glue)||!PaleolithicBinders.Bond(item,glue,(NearbyHeatingFire()?.FirePowerKw??0)>=.5)){InventoryFeedback("The adhesive or hearth is no longer ready.");return;}
            RemoveEmptyBinder(glue);if(glue.MassKg>0)RefreshChangedPossession(glue);RefreshChangedPossession(item);InventoryFeedback("Haft bonded with 5 g of compound adhesive.");
        });inventoryContext=null;
    }
    Possession AccessiblePigment()=>CraftAccessible().FirstOrDefault(p=>p.Kind==PossessionKind.OchrePowder&&p.MassKg>=.002);
    Possession PigmentWater()=>CraftAccessible().FirstOrDefault(p=>PaleolithicWater.Vessel(p)&&p.WaterLitres>=.005);
    string PaintItemReason(Possession item)=>item.PigmentMarked?"This item already carries ochre markings.":AccessiblePigment()==null?"Bring at least 2 g of ground ochre.":PigmentWater()==null?"Needs a small mouthful of water in a vessel.":"";
    void BeginPaintItem(Possession item)
    {
        var reason=PaintItemReason(item);if(reason.Length>0){InventoryFeedback(reason);return;}var powder=AccessiblePigment();var water=PigmentWater();WitnessPropertyUse(item);WitnessPropertyUse(powder);WitnessPropertyUse(water);
        BeginWorldToolWork(item,"Applying ochre markings",90,()=>
        {
            var available=CraftAccessible();if(!available.Contains(powder)||!available.Contains(water)||!PaleolithicBinders.Paint(item,powder,water)){InventoryFeedback("The pigment or water is no longer within reach.");return;}
            RemoveEmptyBinder(powder);if(powder.MassKg>0)RefreshChangedPossession(powder);RefreshChangedPossession(item);RefreshVesselMass(water);InventoryFeedback("Ochre markings applied.");
        });inventoryContext=null;
    }
    void GatherFireCharcoal(CampObject fire)
    {
        var stick=BoilingStick();if(stick==null){InventoryFeedback("Bring a sound stick to rake the charcoal safely.");return;}
        BeginWorldToolWork(stick,"Raking charcoal scraps",60,()=>
        {
            if(!Camp.Objects.Contains(fire)||(fire.Position-Position).Length*frame.RadiusM>2.5){InventoryFeedback("The hearth is out of reach.");return;}
            var charcoal=PaleolithicBinders.CollectCharcoal(fire,Position);if(charcoal==null){InventoryFeedback("No cooled charcoal scraps are ready.");return;}ReceiveToolMaterial(charcoal);InvalidateCampVisual(fire);
        });
    }
    void ApplyLegacyPigment(GameObject root,Possession item)
    {
        if(!item.PigmentMarked||PortableTechnology.Item(item.Kind)!=null)return;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            var mesh=filter.sharedMesh;if(mesh==null)continue;
            if(!inventoryCraftMeshes.TryGetValue(root,out var owned))inventoryCraftMeshes.Add(root,owned=new System.Collections.Generic.List<Mesh>());
            if(!owned.Contains(mesh)){mesh=Instantiate(mesh);filter.sharedMesh=mesh;owned.Add(mesh);}
            var colors=mesh.colors;var vertices=mesh.vertices;if(colors.Length!=vertices.Length)colors=Enumerable.Repeat(Color.white,vertices.Length).ToArray();
            var pigment=new Color((float)item.PigmentColour.X,(float)item.PigmentColour.Y,(float)item.PigmentColour.Z,0);
            for(int i=0;i<colors.Length;i++)if(Mathf.Repeat(vertices[i].y*3,1)>.34f){float alpha=colors[i].a;colors[i]=Color.Lerp(colors[i],pigment,.8f);colors[i].a=alpha;}
            mesh.colors=colors;
        }
    }
}
