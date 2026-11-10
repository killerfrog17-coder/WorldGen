using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    HumanPersonMemory NpcPersonRecord(HumanActor actor)
    {
        foreach(var known in LivingState.People)if(known.Person==actor.ResidentId)return known;
        var record=new HumanPersonMemory{Person=actor.ResidentId,Group=actor.GroupId,Position=actor.Planet,SolarDays=PlayerRules.SolarDays,Health=actor.Health};LivingState.People.Add(record);return record;
    }
    void InitializeNpcBelongings(HumanActor actor,LivingGroup group)
    {
        var record=NpcPersonRecord(actor);bool fresh=record.Inventory==null;actor.Belongings=record.Inventory??(record.Inventory=NpcBelongings.Seed(actor.Resident,group.Residents,actor.Planet));
        if(fresh)
        {
            string role=HumanEquipment.Role(actor.Resident,actor.Routine,actor.CurrentActivity,group.Residents.Techniques);
            V home=actor.Planet;
            if(group.Grounded){TerrainHandoff.Coordinates(frame,group.Anchor,out double x,out double z);var offset=HumanCampLayout.HouseholdOffset(group.Residents,actor.Resident.Household,group.Situation.Camp.Id,sourceWorld.Recipe.Seed);home=frame.Position(x+offset.X,z+offset.Z);}
            NpcBelongings.PrepareInitial(actor.Belongings,role,home);
        }
        PublishNpcGroundGoods(actor);
    }
    void PublishNpcGroundGoods(HumanActor actor)
    {
        var stock=actor.Belongings;if(stock==null)return;
        bool changed=false;foreach(var ground in stock.Items.Where(i=>i.Place==PossessionPlace.Ground).ToArray())
        {
            NpcBelongings.TransferTree(stock,Inventory,ground);changed=true;
        }
        if(changed)inventoryNearbyAt=0;
    }
    V NpcWorldItemPosition(Possession item)
    {
        for(int remaining=Inventory.Items.Count+1;item.Place==PossessionPlace.Container&&remaining-->0;){var parent=Inventory.Find(item.ContainerId);if(parent==null)break;item=parent;}return item.Position;
    }
    bool NpcCanUseWorldItem(HumanActor actor,Possession item)
    {
        if(Inventory.Carried(item)||item.Place==PossessionPlace.Corpse||item.Place==PossessionPlace.CampStorage||item.Place==PossessionPlace.Container&&item.OwnerPerson!=actor.ResidentId||Inventory.Crafts.Any(j=>j.Running&&PortableCrafting.Affects(j,item.Id)))return false;
        var culture=EffectiveNpcCulture(livingGroups[actor.GroupId]);
        bool campWork=item.CampMaterial==actor.GroupId&&item.OwnerGroup==actor.GroupId&&item.OwnerPerson==0&&(actor.CurrentActivity==HumanActivity.BuildCamp||actor.CurrentActivity==HumanActivity.PackCamp||actor.CurrentActivity==HumanActivity.RepairShelter);
        return (campWork||NpcOwnership.Allowed(item,actor.ResidentId,actor.GroupId,actor.Resident.Household,culture.FoodSharing))&&HumanDistance(NpcWorldItemPosition(item),actor.Planet)<35;
    }
    bool AcquireNpcItem(HumanActor actor,Possession item)
    {
        if(!NpcCanUseWorldItem(actor,item)||HumanDistance(NpcWorldItemPosition(item),actor.Planet)>1.7)return false;
        var stock=actor.Belongings;stock.Items.Add(item);var wear=PortableTechnology.Wear(item);bool fits=wear.HasValue&&stock.Move(item.Id,wear.Value,"",0,0,false,out _)||stock.Hold(item.Id,out _)||NpcBelongings.Pack(stock,item);stock.Items.Remove(item);if(!fits)return false;
        foreach(var moved in NpcBelongings.TransferTree(Inventory,stock,item))
        {if(moved.OwnerPerson==0&&moved.OwnerGroup==0)NpcOwnership.Assign(moved,actor.Resident,actor.GroupId);RemoveDroppedBody(moved.Id);DestroyInventoryVisual(inventoryVisuals.TryGetValue(moved.Id,out var root)?root:null);inventoryVisuals.Remove(moved.Id);}
        inventoryNearbyAt=0;return true;
    }
    void PrepareNpcTaskGear(HumanActor actor,LivingGroup group,HumanActivitySample routine,ref V target)
    {
        if(actor.Belongings==null)InitializeNpcBelongings(actor,group);
        if(actor.SimulationAge<actor.GearAt){if(actor.FetchingGear)target=actor.GearTarget;return;}
        AdvanceNpcTaskGear(actor,group,routine,ref target);
    }
    void AdvanceNpcTaskGear(HumanActor actor,LivingGroup group,HumanActivitySample routine,ref V target)
    {
        actor.GearAt=actor.SimulationAge+.5;actor.TaskWaiting=false;actor.FetchingGear=false;
        string desired=HumanEquipment.Role(actor.Resident,routine,actor.CurrentActivity,group.Residents.Techniques);actor.GearRole=desired??"";
        if(desired==null)
        {
            if(routine.Activity==HumanActivity.Sleep&&HumanDistance(actor.Planet,target)<1.5){NpcBelongings.Equip(actor.Belongings,Array.Empty<Possession>(),actor.Planet);PublishNpcGroundGoods(actor);}
            actor.TaskDetail=NpcActivityText(actor.CurrentActivity);return;
        }
        var stock=actor.Belongings;var wanted=NpcBelongings.ForTask(stock,desired);
        // Consider actual nearby owned/shared goods, never a newly-made prop.
        if(wanted.Length==0||desired=="WorkKnapping"&&wanted.Length<2)
        {
            var nearby=Inventory.Items.Where(p=>NpcBelongings.TaskItem(p,desired)&&NpcCanUseWorldItem(actor,p)).OrderBy(p=>HumanDistance(NpcWorldItemPosition(p),actor.Planet)).ToList();
            var viewOfGoods=new PlayerInventory{Items=nearby};var needed=NpcBelongings.ForTask(viewOfGoods,desired);
            if(needed.Length>0)
            {
                NpcBelongings.Equip(stock,wanted,actor.Planet);PublishNpcGroundGoods(actor);
                var item=needed[0];V point=NpcWorldItemPosition(item);
                if(HumanDistance(point,actor.Planet)>1.7){target=point;actor.GearTarget=point;actor.FetchingGear=true;actor.TaskWaiting=true;actor.TaskDetail="going to retrieve "+item.Name;return;}
                if(AcquireNpcItem(actor,item))wanted=NpcBelongings.ForTask(stock,desired);
            }
        }
        NpcBelongings.Equip(stock,wanted,actor.Planet);PublishNpcGroundGoods(actor);
        bool ready=desired==null||wanted.Length>0&&wanted.All(p=>stock.Carried(p));
        if(desired=="WorkKnapping")ready=wanted.Length==2&&wanted.All(p=>p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand);
        if(desired=="Drink"&&(group.Resources?.HasWater!=true||HumanDistance(actor.Planet,group.Resources.Water)>2.5))ready=false;
        actor.TaskWaiting=!ready&&(routine.Activity==HumanActivity.Craft||routine.Activity==HumanActivity.PrepareFood||routine.Activity==HumanActivity.FetchWater||routine.Activity==HumanActivity.TendFire);
        actor.TaskDetail=actor.TaskWaiting?desired=="Eat"?"looking for food; no meal is available":desired=="WorkKnapping"?"needs suitable stones before knapping":desired=="Drink"?"looking for reachable drinking water":"waiting for the actual required material":NpcActivityText(actor.CurrentActivity);
        if(actor.TaskWaiting)actor.GearRole="";
    }
    string ActualNpcCarryRole(HumanActor actor)
    {
        var stock=actor.Belongings;if(stock==null)return null;var held=stock.AtHand(PossessionPlace.RightHand)??stock.AtHand(PossessionPlace.LeftHand);
        if(held==null)return null;
        if(StoneCrafting.Rock(held)&&StoneCrafting.Rock(stock.AtHand(PossessionPlace.LeftHand)))return "WorkKnapping";
        if(held.Kind==PossessionKind.WoodenSpear||held.Kind==PossessionKind.StoneTippedSpear||held.Kind==PossessionKind.BoneTippedSpear)return "SpearReady1";
        if(Foraging.Food(held))return actor.CurrentActivity==HumanActivity.PrepareFood?"Eat":"ToolReady";
        if(held.Kind==PossessionKind.KindlingBundle||held.Kind==PossessionKind.Branch)return "CarryLarge";
        if(held.Kind==PossessionKind.CampBundle)return "CarryLarge";
        if(PaleolithicWater.Vessel(held))return actor.CurrentActivity==HumanActivity.FetchWater?"Drink":"WaterCarry";
        return "ToolReady";
    }
    void DropNpcPossessions(HumanActor actor)
    {
        if(actor.Belongings==null)return;
        foreach(var item in actor.Belongings.Items.Where(p=>p.Place!=PossessionPlace.Container).ToArray())
        {item.Place=PossessionPlace.Ground;item.ContainerId="";item.Position=actor.Planet;item.GroundOffsetM=.15;}
        PublishNpcGroundGoods(actor);CaptureLivingPerson(actor);
    }
    string NpcBelongingsContext(HumanActor actor)
    {
        var stock=actor.Belongings;return stock==null?"":" Actual physical task: "+actor.TaskDetail+". Items actually carried: "+string.Join(", ",stock.Items.Where(stock.Carried).Take(10).Select(p=>p.Name+(PaleolithicWater.Vessel(p)?" ("+p.WaterLitres.ToString("0.##")+" L water)":"")+(p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand?" (in hand)":" (packed/worn)")))+". Do not claim to eat, make, carry or use an item that is not actually present. A scheduled task is a plan, not proof that its materials exist.";
    }
}
