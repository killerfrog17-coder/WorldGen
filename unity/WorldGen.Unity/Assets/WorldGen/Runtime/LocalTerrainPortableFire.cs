using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    readonly Dictionary<GameObject,Possession> portableFireRoots=new Dictionary<GameObject,Possession>();
    readonly Dictionary<GameObject,GameObject> portableFlames=new Dictionary<GameObject,GameObject>();
    double portableFireAt;
    void RegisterPortableFire(GameObject root,Possession item)
    {if(PortableFire.Item(item)){portableFireRoots[root]=item;RefreshPortableFlame(root,item);}}
    void UpdatePortableFire()
    {
        if(!Ready||Time.unscaledTimeAsDouble<portableFireAt)return;portableFireAt=Time.unscaledTimeAsDouble+.25;
        double wind=Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps);
        foreach(var p in Inventory.Items)if(PortableFire.Item(p))
        {
            var cover=BlueprintProtectionAt(Inventory.Carried(p)?Position:p.Position);
            PortableFire.Advance(p,PlayerRules.SolarDays,CurrentWeather.RainMmPerHour*(1-cover.Rain),wind*(1-cover.Wind),p.Place==PossessionPlace.Container);
            if(PortableFire.Burning(p)&&Inventory.Carried(p)&&motion.Swimming&&SampleMotionWater(motion.X,motion.Z).Surface>view.transform.localPosition.y+.15)PortableFire.Quench(p);
        }
        foreach(var root in portableFireRoots.Keys.ToArray())
        {
            if(root==null){portableFireRoots.Remove(root);portableFlames.Remove(root);continue;}
            var p=portableFireRoots[root];
            if(Inventory.Find(p.Id)!=p){var cover=BlueprintProtectionAt(p.Position);PortableFire.Advance(p,PlayerRules.SolarDays,CurrentWeather.RainMmPerHour*(1-cover.Rain),wind*(1-cover.Wind),p.Place==PossessionPlace.Container);}if(root.layer==30&&root.activeInHierarchy&&PortableFire.Burning(p))
            {
                var tip=transform.InverseTransformPoint(root.transform.TransformPoint(Vector3.up*.9f));var water=SampleMotionWater(tip.x+originX,tip.z+originZ);
                if(double.IsFinite(water.Surface)&&tip.y<water.Surface-.03)PortableFire.Quench(p);
            }
            RefreshPortableFlame(root,p);
        }
    }
    void RefreshPortableFlame(GameObject root,Possession item)
    {
        bool burning=PortableFire.Burning(item);bool exists=portableFlames.TryGetValue(root,out var flame)&&flame!=null;
        if(!burning){if(exists)Destroy(flame);portableFlames.Remove(root);return;}if(exists)return;
        flame=new GameObject("Portable fire glow");flame.layer=root.layer;flame.transform.SetParent(root.transform,false);portableFlames[root]=flame;
        bool torch=item.Kind==PossessionKind.Torch;
        if(campFlameMaterial==null)campFlameMaterial=new Material(Resources.Load<Shader>("WorldGenCampFlame"));
        if(campEmberMaterial==null){campEmberMaterial=new Material(Resources.Load<Shader>("WorldGenCampFlame"));campEmberMaterial.SetFloat("_EmberMode",1);}
        for(int i=0;i<(torch?2:1);i++)
        {
            var sheet=GameObject.CreatePrimitive(PrimitiveType.Quad);Destroy(sheet.GetComponent<Collider>());sheet.layer=root.layer;sheet.transform.SetParent(flame.transform,false);
            // Cancel item dimensions so the flame keeps its physical size.
            sheet.transform.localPosition=new Vector3(0,torch?1.04f:.97f,0);sheet.transform.localRotation=Quaternion.Euler(torch?0:90,i*90,0);
            sheet.transform.localScale=new Vector3((float)((torch?.18:.075)/item.WidthM),(float)((torch?.3:.075)/(torch?item.HeightM:item.DepthM)),1);
            var renderer=sheet.GetComponent<Renderer>();renderer.sharedMaterial=torch?campFlameMaterial:campEmberMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
    }
    bool FireSourceReady()=>NearbyHeatingFire() is CampObject c&&(c.Burning||c.EmberSeconds>=300)||Inventory.Items.Any(p=>Inventory.Carried(p)&&PortableFire.Burning(p));
    string LightPortableReason(Possession p)
    {
        if(PortableFire.Burning(p))return "Already alight.";
        if(!FireSourceReady())return "Bring it beside a live fire or carried flame.";
        if(p.Kind==PossessionKind.Torch)return p.FireRemainingSeconds>0&&p.FireFuelKg>0?"":"Rewrap the spent torch with fresh fuel.";
        if(p.FireFuelKg>0&&p.FireRemainingSeconds>0)return "";
        var fire=NearbyHeatingFire();if(fire==null||fire.AshKg<.005)return "A hearth needs enough live coals to transfer.";
        return CraftAccessible().Any(p=>CampRules.Tinder(p)&&p.MassKg>=.02)?"":"Bring at least 20 g of fine dry tinder for the ember nest.";
    }
    void LightPortable(Possession item)
    {
        string reason=LightPortableReason(item);if(reason.Length>0){InventoryFeedback(reason);return;}
        var fire=NearbyHeatingFire();var tinder=CraftAccessible().FirstOrDefault(p=>CampRules.Tinder(p)&&p.MassKg>=.02);WitnessPropertyUse(item);if(tinder!=null)WitnessPropertyUse(tinder);
        BeginWorldToolWork(item,item.Kind==PossessionKind.Torch?"Lighting the torch":"Packing a live ember nest",30,()=>
        {
            bool lit=item.Kind==PossessionKind.Torch?PortableFire.Light(item,FireSourceReady(),PlayerRules.SolarDays):item.FireFuelKg>0?PortableFire.Light(item,FireSourceReady(),PlayerRules.SolarDays):fire!=null&&Camp.Objects.Contains(fire)&&(fire.Position-Position).Length*frame.RadiusM<2.5&&tinder!=null&&CraftAccessible().Contains(tinder)&&PortableFire.LoadEmbers(item,fire,tinder,PlayerRules.SolarDays);
            if(!lit){InventoryFeedback("The fire or tinder is no longer ready.");return;}
            if(tinder!=null&&tinder.MassKg<=1e-8){Inventory.Items.Remove(tinder);RefreshChangedPossession(tinder);}RefreshChangedPossession(item);InventoryFeedback(item.Kind==PossessionKind.Torch?"Torch lit.":"Live coals secured in the carrier.");
        });inventoryContext=null;
    }
    void ExtinguishPortable(Possession item)
    {item.FireLit=false;RefreshChangedPossession(item);InventoryFeedback("Portable fire extinguished.");inventoryContext=null;}
    int AddPortableFireLights(int count)
    {
        // Share the existing maximum of four lights with hearths. No new Unity
        // lights, shadows, material pass, or all-NPC inventory scan.
        foreach(var pair in portableFireRoots)
        {
            var root=pair.Key;var p=pair.Value;if(root==null||root.layer!=30||!root.activeInHierarchy||!PortableFire.Burning(p))continue;
            var pos=root.transform.TransformPoint(Vector3.up*.95f);float distance=(pos-view.transform.position).sqrMagnitude;if(distance>18*18)continue;
            bool duplicate=false;for(int i=0;i<count;i++)if(((Vector3)campLightPositions[i]-pos).sqrMagnitude<.01f){duplicate=true;break;}if(duplicate)continue;
            int slot=count<4?count++:-1;
            if(slot<0){float far=distance;for(int i=0;i<4;i++){float d=((Vector3)campLightPositions[i]-view.transform.position).sqrMagnitude;if(d>far){far=d;slot=i;}}}if(slot<0)continue;
            float power=(float)PortableFire.LightPower(p)*3;campLightPositions[slot]=new Vector4(pos.x,pos.y,pos.z,p.Kind==PossessionKind.Torch?64:2);campLightColours[slot]=new Vector4(power,power*.34f,power*.075f,1);
        }
        return count;
    }
}
