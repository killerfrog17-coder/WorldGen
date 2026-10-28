using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    // Work borrows hands; it never creates extra inventory space or deletes a
    // displaced item. A slung weapon/attached tool gets priority over bag space.
    void PutAwayForWork(Possession item)
    {
        var slot=PortableTechnology.Wear(item);
        if(slot.HasValue&&Inventory.Move(item.Id,slot.Value,"",0,0,false,out _))return;
        if(Inventory.Stow(item.Id,out _))return;
        bool carried=Inventory.Carried(item);item.Place=PossessionPlace.Ground;item.ContainerId="";
        if(carried)ReleasePossession(item,false);else SetCraftWorkArea(item);
    }
    CraftEquipmentState PrepareWorkHands(IEnumerable<string> borrowed)
    {
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
        RefreshInventoryNearby(true);
        foreach(var before in state.PreviousHands)
        {
            var p=Inventory.Find(before.Id);if(p==null||!AccessiblePossession(p))continue;
            if(Inventory.Move(p.Id,before.Place,"",0,0,false,out _)){RemoveDroppedBody(p.Id);p.GroundMoving=false;}
        }
        inventoryNearbyAt=0;if(inventoryOpen)RenderInventoryPreview();
    }
}
