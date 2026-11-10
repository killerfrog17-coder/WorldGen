using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldGen.Core;
using WorldGen.Presentation;
public sealed partial class LocalTerrainExplorer
{
    CampObject activeWatercraft;bool watercraftRestored;
    CampBlueprint watercraftCheckedPlan;int watercraftCheckedRevision=-1;bool watercraftCheckedComplete;
    readonly Collider[] watercraftContacts=new Collider[32];
    static readonly (double,double)[] watercraftCorners={(-.45,-.45),(-.45,.45),(.45,-.45),(.45,.45)};
    bool WatercraftUsable(CampObject c)
    {if(!PrimitiveWatercraft.Boat(c))return false;if(watercraftCheckedPlan!=c.Blueprint||watercraftCheckedRevision!=c.Blueprint.Revision){watercraftCheckedPlan=c.Blueprint;watercraftCheckedRevision=c.Blueprint.Revision;watercraftCheckedComplete=CampBlueprints.Complete(c.Blueprint)&&c.Blueprint.Parts.All(p=>p.Weathering<.65);}return watercraftCheckedComplete&&WatercraftLoad<=PrimitiveWatercraft.CapacityKg(c);}
    double WatercraftLoad=>70*Math.Pow((humanPlayer?.Definition.EyeHeight??1.65f)/1.65,3)+Inventory.CarriedMassKg;
    double WatercraftFloor(CampObject c,double x,double z)
    {var water=SampleMotionWater(x,z);double ground=CampFloor(x,z);return double.IsFinite(water.Surface)&&water.Surface>ground?Math.Max(ground,water.Surface-PrimitiveWatercraft.Draft(c,activeWatercraft==c?WatercraftLoad:0)):ground;}
    double BlueprintPlacementFloor(double x,double z)
    {double ground=CampFloor(x,z);if(blueprintPlacement==null||!PrimitiveWatercraft.Design(blueprintPlacement.Design))return ground;var water=SampleMotionWater(x,z);return double.IsFinite(water.Surface)?Math.Max(ground,water.Surface):ground;}
    bool WatercraftSite(CampBlueprint p,Vector3 center,float yaw,out string reason)
    {
        reason="";p.FloorHeights=new double[9];var rotation=Quaternion.Euler(0,yaw,0);double datum=double.NaN;
        foreach(float x in new[]{-.5f,0,.5f})foreach(float z in new[]{-.5f,0,.5f})
        {var at=center+rotation*new Vector3(x*(float)p.WidthM,0,z*(float)p.DepthM);var water=SampleMotionWater(at.x+originX,at.z+originZ);double depth=water.Surface-CampFloor(at.x+originX,at.z+originZ);
         if(!double.IsFinite(water.Surface)||depth<.08||depth>1.5){reason="Lay out the watercraft in shallow water, 8 cm to 1.5 m deep.";return false;}
         if(double.IsNaN(datum))datum=water.Surface;else if(Math.Abs(water.Surface-datum)>.2){reason="Choose a calm, level stretch of water.";return false;}}
        var blockers=Physics.OverlapBox(transform.TransformPoint(center+Vector3.up*.3f),new Vector3((float)p.WidthM*.5f,.25f,(float)p.DepthM*.5f),transform.rotation*rotation,1<<ScatterSolidLayer,QueryTriggerInteraction.Ignore);
        if(blockers.Length>0){reason="Leave room around rocks, trees and other craft.";return false;}return true;
    }
    void BoardWatercraft(CampObject c)
    {
        if(activeWatercraft!=null||!WatercraftUsable(c)){InventoryFeedback("Finish the hull and seals, and keep the load within its buoyancy.");return;}
        TerrainHandoff.Coordinates(frame,c.Position,out double x,out double z);var water=SampleMotionWater(x,z);
        if(!double.IsFinite(water.Surface)||water.Surface-CampFloor(x,z)<PrimitiveWatercraft.Draft(c,WatercraftLoad)+.02){InventoryFeedback("The hull is grounded; it needs slightly deeper water.");return;}
        activeWatercraft=c;PlayerRules.WatercraftId=c.Id;CancelItemCharge();CancelWorldToolWork();CancelBlueprintWork();campMenu=null;CaptureMouse();
        PlaceOnWatercraft(c,x,z);InventoryFeedback("Aboard. W/S paddle, A/D turn, E leave. Hold a paddle to propel the craft.");
    }
    void PlaceOnWatercraft(CampObject c,double x,double z)
    {double floor=WatercraftFloor(c,x,z)+(c.Blueprint.Design==CampDesign.ReedRaft?.4:.08);motion.Place(x,z,floor);survivalSprinting=false;}
    bool WatercraftClear(CampObject c,double x,double z)
    {
        double a=c.Heading*Math.PI/180,s=Math.Sin(a),co=Math.Cos(a),draft=PrimitiveWatercraft.Draft(c,WatercraftLoad);
        foreach(var corner in watercraftCorners)
        {double dx=corner.Item1*c.Blueprint.WidthM,dz=corner.Item2*c.Blueprint.DepthM,px=x+co*dx+s*dz,pz=z-s*dx+co*dz;var water=SampleMotionWater(px,pz);if(!double.IsFinite(water.Surface)||water.Surface-CampFloor(px,pz)<draft+.02)return false;}
        var center=transform.TransformPoint(new Vector3((float)(x-originX),(float)(SampleMotionWater(x,z).Surface+.18),(float)(z-originZ)));
        int contacts=Physics.OverlapBoxNonAlloc(center,new Vector3((float)c.Blueprint.WidthM*.45f,.2f,(float)c.Blueprint.DepthM*.45f),watercraftContacts,transform.rotation*Quaternion.Euler(0,(float)c.Heading,0),1<<ScatterSolidLayer,QueryTriggerInteraction.Ignore);
        if(contacts==watercraftContacts.Length)return false;for(int i=0;i<contacts;i++)if(!campVisuals.TryGetValue(c.Id,out var own)||!watercraftContacts[i].transform.IsChildOf(own.Root.transform))return false;
        return true;
    }
    void UpdateWatercraft()
    {
        if(!Ready)return;if(!watercraftRestored){watercraftRestored=true;if(!string.IsNullOrEmpty(PlayerRules.WatercraftId))activeWatercraft=Camp.Objects.FirstOrDefault(c=>c.Id==PlayerRules.WatercraftId&&PrimitiveWatercraft.Boat(c));}
        var c=activeWatercraft;if(c==null)return;
        if(DeathVisible||Spectating||!Camp.Objects.Contains(c)||!WatercraftUsable(c)){LeaveWatercraft();return;}
        TerrainHandoff.Coordinates(frame,c.Position,out double x,out double z);double dt=Math.Max(0,Math.Min(.1,Time.unscaledDeltaTime));
        if(!paused&&!mapOpen&&!ecologyOpen&&!chatOpen)
        {
            bool controls=!inventoryOpen&&(Application.isFocused||testKeyboard!=null);var k=ActiveKeyboard;
            if(controls&&!inventoryReviewMode){var delta=Mouse.current?.delta.ReadValue()??Vector2.zero;heading+=delta.x*.1f;pitch=Mathf.Clamp(pitch-delta.y*.1f,-85,85);}
            double forward=controls&&k!=null?(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0):0,turn=controls&&k!=null?(k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0):0;
            var paddle=Inventory.Items.FirstOrDefault(p=>p.Kind==PossessionKind.Paddle&&(p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand)&&p.UseDamage<1);
            bool effort=paddle!=null&&Math.Abs(forward)>.01;if(effort&&SurvivalActive&&!SpendCombatStamina(dt*.25))effort=false;
            var water=SampleMotionWater(x,z);double oldHeading=c.Heading;var step=PrimitiveWatercraft.Step(c,dt,forward,turn,water.FlowX,water.FlowZ,effort);
            if(WatercraftClear(c,x+step.X,z+step.Z)){x+=step.X;z+=step.Z;c.Position=frame.Position(x,z);}else{c.Heading=oldHeading;c.BoatVelocityEast=c.BoatVelocityNorth=0;}
        }
        PlaceOnWatercraft(c,x,z);
        if(campVisuals.TryGetValue(c.Id,out var visual)){visual.Root.transform.localPosition=BlueprintOrigin(c);visual.Root.transform.localRotation=Quaternion.Euler(0,(float)c.Heading,0);}
    }
    void LeaveWatercraft()
    {
        var c=activeWatercraft;if(c==null)return;activeWatercraft=null;PlayerRules.WatercraftId="";TerrainHandoff.Coordinates(frame,c.Position,out double x,out double z);
        double bx=x,bz=z,best=double.PositiveInfinity;
        for(int i=0;i<12;i++){double a=i*Math.PI/6,px=x+Math.Sin(a)*(c.Blueprint.WidthM*.5+.85),pz=z+Math.Cos(a)*(c.Blueprint.DepthM*.5+.85);var water=SampleMotionWater(px,pz);double ground=CampFloor(px,pz),depth=double.IsFinite(water.Surface)?Math.Max(0,water.Surface-ground):0;if(depth<best){best=depth;bx=px;bz=pz;}}
        var wet=SampleMotionWater(bx,bz);double floor=CampFloor(bx,bz);if(double.IsFinite(wet.Surface)&&wet.Surface>floor+1)floor=wet.Surface-1.1;
        motion.Place(bx,bz,floor);InventoryFeedback("You leave the craft.");
    }
}
