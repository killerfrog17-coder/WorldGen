using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    CampState Camp=>PlayerRules.Camp??(PlayerRules.Camp=new CampState());
    CampObject campMenu;bool campGroundMenu,campSleepMenu;
    Possession campPlaceMaterial;CampKind campPlaceKind;LineRenderer campPreview;
    Vector3 campPlacePoint;bool campPlaceValid;string campPlaceReason="";
    double campUpdateAt;bool campSleeping,campResting;double campSleepSeconds,campSleepHealth;
    CampObject sleepingBed;
    bool CampUiOpen=>campMenu!=null||campGroundMenu||campSleepMenu;
    bool CampBusy=>CampUiOpen||campPlaceMaterial!=null||campSleeping||campResting;
    public double CampTimeRate=>campSleeping||campResting?32:1;
    double campGroundAt;Vector3 campGroundEye,campGroundDirection,campGroundPoint;LocalTerrainSample campGroundSample;bool campGroundHit;
    bool CampGround(out Vector3 point,out LocalTerrainSample sample,bool refresh=false)
    {
        var ray=new Ray(view.transform.localPosition,view.transform.forward);point=default;sample=default;
        if(!refresh&&Time.unscaledTimeAsDouble<campGroundAt&&(ray.origin-campGroundEye).sqrMagnitude<.0025f&&Vector3.Dot(ray.direction,campGroundDirection)>.9998f){point=campGroundPoint;sample=campGroundSample;return campGroundHit;}
        campGroundAt=Time.unscaledTimeAsDouble+.1;campGroundEye=ray.origin;campGroundDirection=ray.direction;campGroundHit=false;campGroundPoint=default;campGroundSample=default;
        if(ray.direction.y>-.015f)return false;float previous=0;
        for(float t=.2f;t<=4+(float)humanCamera.Distance;t+=.2f)
        {
            var p=ray.GetPoint(t);double x=p.x+originX,z=p.z+originZ;double floor=Ground(x,z);
            if(p.y>floor){previous=t;continue;}
            float lo=previous,hi=t;for(int i=0;i<5;i++){float mid=(lo+hi)*.5f;var q=ray.GetPoint(mid);if(q.y>Ground(q.x+originX,q.z+originZ))lo=mid;else hi=mid;}
            p=ray.GetPoint(hi);x=p.x+originX;z=p.z+originZ;p.y=(float)Ground(x,z);
            if(Vector3.Distance(PlayerFeet(),p)>3)return false;
            point=campGroundPoint=p;sample=campGroundSample=walkingPatch.Sample(frame.Position(x,z));campGroundHit=true;return true;
        }
        return false;
    }
    bool CampGrassAvailable(Vector3 point,LocalTerrainSample sample)
    {
        if(sample.Underwater||sample.Cover.Grasses.Length<=.08)return false;
        double x=point.x+originX,z=point.z+originZ,gx=Ground(x+.5,z)-Ground(x-.5,z),gz=Ground(x,z+.5)-Ground(x,z-.5);
        return sample.Cover.Grasses.Length*GroundFlora.GrassSlopeAdmission(1/Math.Sqrt(1+gx*gx+gz*gz))>.08;
    }
    CampObject AimedCamp(out float distance)
    {
        distance=3.5f+(float)humanCamera.Distance;CampObject best=null;if(!Ready)return null;
        var ray=new Ray(view.transform.localPosition,view.transform.forward);
        foreach(var c in Camp.Objects)
        {
            if((c.Position-Position).Length*frame.RadiusM>6)continue;
            TerrainHandoff.Coordinates(frame,c.Position,out double x,out double z);float floor=(float)Ground(x,z),height=c.Kind==CampKind.Stake?(float)(c.Materials[0].HeightM-c.EmbedM):c.Kind==CampKind.Bedding?(float)c.BeddingDepthM+.1f:.35f;
            var center=new Vector3((float)(x-originX),floor+height*.5f,(float)(z-originZ));var bounds=new Bounds(center,c.Kind==CampKind.Stake?new Vector3(.12f,height,.12f):c.Kind==CampKind.Bedding?new Vector3(1,Mathf.Max(.1f,height),2):new Vector3(.95f,height,.95f));
            if(c.Kind==CampKind.Bedding)
            {
                var rotation=campVisuals.TryGetValue(c.Id,out var visual)?visual.SurfaceRotation:CampSurfaceRotation(c);
                Vector3 right=rotation*Vector3.right*.65f,up=rotation*Vector3.up*(height*.5f+.025f),forward=rotation*Vector3.forward*1.3f;
                bounds=new Bounds(new Vector3((float)(x-originX),floor,(float)(z-originZ))+rotation*(Vector3.up*height*.5f),2*new Vector3(Mathf.Abs(right.x)+Mathf.Abs(up.x)+Mathf.Abs(forward.x),Mathf.Abs(right.y)+Mathf.Abs(up.y)+Mathf.Abs(forward.y),Mathf.Abs(right.z)+Mathf.Abs(up.z)+Mathf.Abs(forward.z)));
            }
            if(bounds.IntersectRay(ray,out float d)&&d<distance&&InventoryReach(ray.GetPoint(d))){distance=d;best=c;}
        }
        return best;
    }
    string CampPrompt(float maximum)
    {
        if(CampBusy)return null;var c=AimedCamp(out float d);
        if(c!=null&&d<maximum)return (c.Kind==CampKind.Stake?"Planted stick - "+c.EmbedM.ToString("0.00")+" m embedded":c.Kind==CampKind.Bedding?"Bedding - "+(c.BeddingDepthM*100).ToString("0")+" cm":c.Burning?"Campfire":c.EmberSeconds>0?"Glowing embers":"Fire lay")+"  [E]";
        if(maximum<float.MaxValue||!CampGround(out var point,out var s)||!CampGrassAvailable(point,s))return null;
        if(AimedNature(out _)!=null||AimedCorpse(out _)!=null||AimedWater(maximum)!=null)return null;
        return "Grass and ground materials  [E]";
    }
    bool TryCampInteract(float maximum)
    {
        var c=AimedCamp(out float d);if(c!=null&&d<maximum){campMenu=c;CaptureMouse();return true;}
        if(AimedNature(out _)!=null||AimedCorpse(out _)!=null||AimedWater(maximum)!=null)return false;
        if(maximum<float.MaxValue||!CampGround(out var point,out var sample,true)||!CampGrassAvailable(point,sample))return false;
        campGroundMenu=true;CaptureMouse();return true;
    }
    bool HandleCampKeys(Keyboard k)
    {
        if(campSleeping||campResting){if(k.escapeKey.wasPressedThisFrame||k.spaceKey.wasPressedThisFrame)WakeCamp("You wake up.");return true;}
        if(CampUiOpen){if(k.escapeKey.wasPressedThisFrame){campMenu=null;campGroundMenu=campSleepMenu=false;CaptureMouse();}return true;}
        if(campPlaceMaterial!=null)
        {
            if(k.escapeKey.wasPressedThisFrame||Mouse.current?.rightButton.wasPressedThisFrame==true){CancelCampPlacement();return true;}
            if(Mouse.current?.leftButton.wasPressedThisFrame==true){if(campPlaceValid)ConfirmCampPlacement();else InventoryFeedback(campPlaceReason);}return true;
        }
        if(!inventoryOpen&&k.zKey.wasPressedThisFrame){campSleepMenu=true;CaptureMouse();return true;}return false;
    }
    void BeginCampPlacement(Possession material,CampKind kind)
    {
        CancelItemCharge();CancelWorldToolWork();PauseItemCraft(ActiveItemCraft);campMenu=null;campGroundMenu=campSleepMenu=false;
        campPlaceMaterial=material;campPlaceKind=kind;SetInventoryOpen(false);
        var root=new GameObject("Material placement footprint");root.transform.SetParent(transform,false);campPreview=root.AddComponent<LineRenderer>();campPreview.useWorldSpace=false;campPreview.loop=true;campPreview.positionCount=48;campPreview.widthMultiplier=.022f;
        campPreview.sharedMaterial=new Material(Resources.Load<Shader>("WorldGenCampPlacement"));campPreview.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;UpdateCampPlacement();
    }
    void UpdateCampPlacement()
    {
        if(campPlaceMaterial==null||campPreview==null)return;
        campPlaceValid=CampGround(out campPlacePoint,out var sample);campPlaceReason="Aim at reachable ground.";
        if(campPlaceValid)
        {
            double x=campPlacePoint.x+originX,z=campPlacePoint.z+originZ;
            double grade=Math.Max(Math.Abs(Ground(x+.4,z)-Ground(x-.4,z)),Math.Abs(Ground(x,z+.4)-Ground(x,z-.4)))/.8;
            if(sample.Underwater){campPlaceValid=false;campPlaceReason="Cannot place this in water.";}
            else if(grade>(campPlaceKind==CampKind.Stake?1.1:.35)){campPlaceValid=false;campPlaceReason="Choose flatter ground.";}
            else if(campPlaceKind==CampKind.Stake&&(sample.Ground.DepthM<.035||CurrentWeather.TemperatureC< -4)){campPlaceValid=false;campPlaceReason="This ground is too rocky or frozen to plant the stick.";}
            foreach(var c in Camp.Objects)
            {double distance=(c.Position-frame.Position(x,z)).Length*frame.RadiusM;if(distance<(c.Kind==CampKind.Bedding||campPlaceKind==CampKind.Bedding?1.15:.25)){campPlaceValid=false;campPlaceReason="Leave room around the existing placed materials.";break;}}
        }
        var color=campPlaceValid?new Color(.32f,.8f,.5f):new Color(.95f,.3f,.2f);campPreview.sharedMaterial.SetColor("_BaseColor",color);
        for(int i=0;i<48;i++){float a=i*Mathf.PI/24;float rx=campPlaceKind==CampKind.Bedding?.5f:campPlaceKind==CampKind.Stake?.075f:.45f,rz=campPlaceKind==CampKind.Bedding?1:rx;campPreview.SetPosition(i,campPlacePoint+new Vector3(Mathf.Sin(a)*rx,.035f,Mathf.Cos(a)*rz));}
    }
    void CancelCampPlacement(){campPlaceMaterial=null;if(campPreview!=null){Destroy(campPreview.sharedMaterial);Destroy(campPreview.gameObject);campPreview=null;}}
    Possession TakeCampMaterial(Possession source)
    {
        RefreshInventoryNearby(true);if(!AccessiblePossession(source))return null;
        if(Inventory.Crafts.Any(j=>PortableCrafting.Affects(j,source.Id))){InventoryFeedback("Finish or cancel the work on this material first.");return null;}
        if(Inventory.Find(source.Id)==null&&!MovePossession(source.Id,PossessionPlace.Ground))return null;
        var p=Inventory.Find(source.Id);if(p==null)return null;RemoveDroppedBody(p.Id);Inventory.Items.Remove(p);inventoryNearbyAt=0;return p;
    }
    void ConfirmCampPlacement()
    {
        if(!campPlaceValid||campPlaceMaterial==null)return;var p=TakeCampMaterial(campPlaceMaterial);if(p==null){CancelCampPlacement();return;}
        double x=campPlacePoint.x+originX,z=campPlacePoint.z+originZ;var s=walkingPatch.Sample(frame.Position(x,z));
        var c=new CampObject{Kind=campPlaceKind,Position=frame.Position(x,z),Heading=heading,SoilDepthM=s.Ground.DepthM,SoilFirmness=.8+s.Ground.Gravel,EmbedM=campPlaceKind==CampKind.Stake?Math.Min(.035,s.Ground.DepthM):0,Wetness=Math.Min(.8,CurrentWeather.RainMmPerHour*.12),LastSimulatedSeconds=Camp.SimulatedSeconds};
        CampRules.Add(c,p);Camp.Objects.Add(c);CancelCampPlacement();campUpdateAt=0;UpdateCamp();InventoryFeedback(c.Kind==CampKind.Stake?"Stick planted. Strike it with a rock to drive it deeper.":c.Kind==CampKind.Fire?"Tinder placed. Add kindling and fuel before lighting.":"Bedding material spread.");
    }
    void AddCampMaterial(CampObject c,Possession p)
    {if(c==null||p==null)return;var owned=TakeCampMaterial(p);if(owned==null)return;CampRules.Add(c,owned);InvalidateCampVisual(c);campUpdateAt=0;}
    bool HammerCamp(Possession rock,Ray ray,float reach)
    {
        if(!StoneCrafting.Rock(rock))return false;var c=AimedCamp(out float distance);if(c==null||c.Kind!=CampKind.Stake)return false;
        var contact=view.transform.position+view.transform.forward*distance;var direction=contact-ray.origin;float bodyDistance=direction.magnitude;
        if(bodyDistance>reach)return false;var obstruction=TraceCombatObstacle(new Ray(ray.origin,direction.normalized),bodyDistance);
        if(obstruction!=null&&obstruction.Distance<bodyDistance-.12f)return false;
        double moved=CampRules.Drive(c,rock);PlayItemEffect("stone-contact",contact,.5f);ApplyItemImpactWear(rock,3,false);InvalidateCampVisual(c);
        InventoryFeedback(moved>0?"Driven "+(moved*100).ToString("0.0")+" cm; embedded "+c.EmbedM.ToString("0.00")+" m.":"The stick cannot go deeper here.");return true;
    }
    void LightCamp(CampObject c)
    {
        RefreshInventoryNearby(true);var drill=CraftAccessible().FirstOrDefault(p=>p.Kind==PossessionKind.HandDrillSet&&p.UseDamage<1);string reason=CampRules.IgniteReason(c,drill!=null||UnlimitedResources);
        if(reason.Length>0){InventoryFeedback(reason);return;}campMenu=null;CaptureMouse();
        if(UnlimitedResources||c.EmberSeconds>0){CampRules.Ignite(c,true);InvalidateCampVisual(c);return;}
        if(Inventory.Find(drill.Id)==null&&!MovePossession(drill.Id,PossessionPlace.Ground))return;
        BeginWorldToolWork(drill,"Working a hand drill into tinder",480,()=>{if(Camp.Objects.Contains(c)&&CampRules.IgniteReason(c,true).Length==0){CampRules.Ignite(c,true);InvalidateCampVisual(c);InventoryFeedback("The tinder catches. Feed the fire with dry wood.");}});
    }
    void RemoveCamp(CampObject c)
    {
        if(c.Burning||c.EmberSeconds>0){InventoryFeedback("Extinguish and cool the fire first.");return;}
        foreach(var p in c.Materials){p.Position=c.Position;p.Place=PossessionPlace.Ground;p.ContainerId="";p.InFlight=false;p.GroundVelocity=p.GroundSpin=new V();p.GroundOffsetM=.06;p.GroundMoving=true;Inventory.Items.Add(p);}
        c.Materials.Clear();Camp.Objects.Remove(c);InvalidateCampVisual(c);campMenu=null;inventoryNearbyAt=0;CaptureMouse();
    }
    void GatherCampGrass()
    {
        if(!CampGround(out var point,out var s,true)||!CampGrassAvailable(point,s)){InventoryFeedback("No suitable grass within reach.");return;}
        var at=frame.Position(point.x+originX,point.z+originZ);var record=ToolResource("grass-"+GroundWorkKey(at));
        int year=ForageYear;if(record.HarvestYear!=year){record.HarvestYear=year;record.Harvests=0;}
        if(record.Harvests>=3){InventoryFeedback("This small patch has already been gathered.");return;}
        record.Harvests++;var p=PortableTechnology.Create(PossessionKind.DryGrass,at);ReceiveToolMaterial(p);campGroundMenu=false;CaptureMouse();
    }
}
