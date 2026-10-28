using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    // Work borrows hands; it never creates extra inventory space or deletes a
    // displaced item. A slung weapon/attached tool gets priority over bag space.
    void PutAwayForWork(Possession item)
    {
        var slot=PortableTechnology.Wear(item);
        if(slot.HasValue&&Inventory.Move(item.Id,slot.Value,"",0,0,false,out _))return;
        if(Inventory.Stow(item.Id,out _))return;
        bool carried=Inventory.Carried(item);float sideSign=item.Place==PossessionPlace.LeftHand?-1:1;item.Place=PossessionPlace.Ground;item.ContainerId="";
        if(carried){ReleasePossession(item,false);SetAsideForWork(item,sideSign);}else SetCraftWorkArea(item);
    }
    void SetAsideForWork(Possession item,float sideSign)
    {
        if(!inventoryDrops.TryGetValue(item.Id,out var drop)||drop.Body==null||drop.Collider==null)return;
        float Height(double x,double z)=>(float)VisibleGround(x,z,Ground(x,z));
        double x=motion.X,z=motion.Z;
        float sx=Height(x+.3,z)-Height(x-.3,z),sz=Height(x,z+.3)-Height(x,z-.3);
        Vector3 side=new Vector3(-sz,0,sx);if(side.sqrMagnitude<.001f){side=transform.InverseTransformDirection(view.transform.right);side.y=0;}side.Normalize();
        x+=side.z*.4*sideSign;z-=side.x*.4*sideSign;float half=(float)Math.Max(item.HeightM,item.WidthM)*.5f;
        float floor=Mathf.Max(Height(x,z),Height(x+side.x*half,z+side.z*half),Height(x-side.x*half,z-side.z*half));
        Quaternion rotation=Quaternion.FromToRotation(Vector3.up,transform.TransformDirection(side));var extent=Vector3.Scale(drop.Collider.size*.5f,drop.Body.transform.lossyScale);
        float vertical=Mathf.Abs((rotation*Vector3.right).y)*extent.x+Mathf.Abs((rotation*Vector3.up).y)*extent.y+Mathf.Abs((rotation*Vector3.forward).y)*extent.z;
        Vector3 center=transform.TransformPoint(new Vector3((float)(x-originX),floor+vertical+.035f,(float)(z-originZ)));
        Vector3 position=center-rotation*Vector3.Scale(drop.Collider.center,drop.Body.transform.lossyScale);
        drop.Body.transform.SetPositionAndRotation(position,rotation);drop.Body.position=position;drop.Body.rotation=rotation;
        drop.Body.linearVelocity=drop.Body.angularVelocity=Vector3.zero;drop.Body.linearDamping=2;drop.Body.angularDamping=3;
        Physics.SyncTransforms();CaptureInventoryPhysics();
    }
    CraftEquipmentState PrepareWorkHands(IEnumerable<string> borrowed)
    {
        combatGuarding=false;
        var ids=new HashSet<string>(borrowed);var state=CraftEquipmentState.Capture(Inventory,ids);UpdateInventoryVisuals();
        foreach(var p in Inventory.Items.Where(p=>p.Place==PossessionPlace.LeftHand||p.Place==PossessionPlace.RightHand).ToArray())
        {
            if(ids.Contains(p.Id)){p.Place=PossessionPlace.Ground;p.ContainerId="";SetCraftWorkArea(p);}
            else PutAwayForWork(p);
        }
        return state;
    }
    void RestoreWorkHands(ItemCraftJob job)
    {
        var state=job?.Equipment;if(state==null)return;job.Equipment=null;
        RefreshInventoryNearby(true);var originals=new HashSet<string>(state.PreviousHands.Select(p=>p.Id));
        foreach(var p in Inventory.Items.Where(p=>(p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand)&&!originals.Contains(p.Id)).ToArray())
        {
            var before=state.Borrowed.FirstOrDefault(b=>b.Id==p.Id);bool returned=false;
            if(before!=null&&before.Place!=PossessionPlace.RightHand&&before.Place!=PossessionPlace.LeftHand&&before.Place!=PossessionPlace.Ground)
            {
                var container=before.Place==PossessionPlace.Container?Inventory.Find(before.Container):null;
                if(before.Place!=PossessionPlace.Container||container!=null&&AccessiblePossession(container))
                    returned=Inventory.Move(p.Id,before.Place,before.Container,before.X,before.Y,before.Rotated,out _);
            }
            if(!returned)PutAwayForWork(p);
        }
        // A borrowed off-hand tool may temporarily occupy the primary hand.
        // Clear those assignments together before restoring either original;
        // otherwise restoration order leaves the other original on the ground.
        foreach(var before in state.PreviousHands)
        {
            var p=Inventory.Find(before.Id);
            if(p!=null&&(p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand)&&p.Place!=before.Place)
            {p.Place=PossessionPlace.Ground;p.ContainerId="";SetCraftWorkArea(p);}
        }
        RefreshInventoryNearby(true);
        foreach(var before in state.PreviousHands)
        {
            var p=Inventory.Find(before.Id);if(p==null||!AccessiblePossession(p))
            {if(inventoryReviewMode&&p!=null){TerrainHandoff.Coordinates(frame,p.Position,out double px,out double pz);Debug.Log("WORK RESTORE UNREACHABLE "+p.Kind+" place="+p.Place+" distance="+(p.Position-Position).Length*frame.RadiusM+" groundOffset="+p.GroundOffsetM+" trueGround="+Ground(px,pz)+" visible="+VisibleGround(px,pz,Ground(px,pz))+" moving="+p.GroundMoving);}continue;}
            if(Inventory.Move(p.Id,before.Place,"",0,0,false,out _)){RemoveDroppedBody(p.Id);p.GroundMoving=false;}
        }
        inventoryNearbyAt=0;if(inventoryOpen)RenderInventoryPreview();
    }
}
