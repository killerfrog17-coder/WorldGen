using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class NpcCampTask {public string Object="",Part="";public double Work,ChooseAt;public bool Packing;}
    int npcCampPacked,npcCampSupplied,npcCampWorked;
    sealed class NpcCampDefinition {public CampBlueprint Plan;public double Width,Depth,Height;public double[] Floor;public CampComponent[] Parts;}
    readonly Dictionary<string,NpcCampDefinition> npcCampGeometry=new Dictionary<string,NpcCampDefinition>();
    CampComponent[] NpcCampParts(CampObject shelter)
    {
        var p=shelter.Blueprint;
        if(!npcCampGeometry.TryGetValue(shelter.Id,out var cached)||cached.Plan!=p||cached.Width!=p.WidthM||cached.Depth!=p.DepthM||cached.Height!=p.ScreenHeightM||cached.Floor!=p.FloorHeights)
            npcCampGeometry[shelter.Id]=cached=new NpcCampDefinition{Plan=p,Width=p.WidthM,Depth=p.DepthM,Height=p.ScreenHeightM,Floor=p.FloorHeights,Parts=CampBlueprints.Components(p).ToArray()};
        return cached.Parts;
    }
    NpcCampTravel NpcTravel(LivingGroup group)
    {var record=NpcGroupRecord(group.Situation.Band.Id);return record.Travel??(record.Travel=new NpcCampTravel());}
    bool NpcCarryCampPoles(LivingGroup group)
        =>group.Situation.Camp.CarryPoles||sourceWorld.Prehistory.Foragers.Districts[group.Residents.District].Canopy<.2||group.Situation.Camp.Design==CampDesign.ConicalTent;
    bool NpcCampNeedsPacking(LivingGroup group)
        =>Camp.Objects.Any(c=>c.NpcGroup==group.Situation.Band.Id&&c.NpcSite==group.Situation.Camp.Id&&c.Blueprint!=null&&NpcCampParts(c).Any(p=>NpcCampWork.Portable(c.Blueprint,p,NpcCarryCampPoles(group))&&CampBlueprints.State(c.Blueprint,p)?.Materials.Count>0));
    bool HoldNpcCampDeparture(LivingGroup group,HumanGroupSituation incoming)
    {
        if(!group.CampLoaded||group.Residents==null||group.Situation.PermanentBase)return false;
        bool departing=incoming.Phase==HumanCampPhase.Packing||incoming.Phase==HumanCampPhase.Travelling||incoming.Camp.Id!=group.Situation.Camp.Id;
        var travel=NpcTravel(group);
        if(!departing&&travel.HoldCalendarDay<0)return false;
        travel.RebuildRequired=true;travel.DepartingSite=group.Situation.Camp.Id;
        if(NpcCampNeedsPacking(group))
        {
            if(travel.HoldCalendarDay<0)travel.HoldCalendarDay=Math.Min(LivingClock.CalendarDay-travel.DelayCalendarDays,group.Situation.DepartureDay-.00001);
            travel.DelayCalendarDays=Math.Max(travel.DelayCalendarDays,LivingClock.CalendarDay-travel.HoldCalendarDay);
            group.Situation.Phase=HumanCampPhase.Packing;group.Situation.LocalHour=incoming.LocalHour;return true;
        }
        travel.HoldCalendarDay=-1;return false;
    }
    bool NpcCampPartClaimed(HumanActor actor,CampObject obj,string part)
        =>livingPeople.Values.Any(p=>p!=actor&&p.GroupId==actor.GroupId&&p.Health>0&&p.Threat==null&&p.CampTask?.Object==obj.Id&&p.CampTask.Part==part&&p.SimulationAge<p.CampTask.ChooseAt+5);
    bool PrepareNpcCampTask(HumanActor actor,LivingGroup group,ref HumanActivitySample routine,float dt,ref V target)
    {
        if(dt<=0||actor.Resident.AgeYears<12||actor.Belongings==null)return false;
        bool packing=group.Situation.Phase==HumanCampPhase.Packing;
        if(routine.Activity==HumanActivity.Travel&&actor.Belongings.Items.Any(p=>actor.Belongings.Carried(p)&&p.CampMaterial==actor.GroupId))
        {actor.TaskWaiting=false;actor.TaskDetail="carrying the family's actual shelter materials to the next camp";return true;}
        bool building=group.CampLoaded&&(routine.Activity==HumanActivity.BuildCamp||routine.Activity==HumanActivity.RepairShelter||routine.Activity==HumanActivity.Craft);
        if(!packing&&!building){actor.CampTask=null;return false;}
        bool beginning=actor.CampTask==null;var task=actor.CampTask??(actor.CampTask=new NpcCampTask());
        if(packing&&(beginning||!task.Packing)){NpcBelongings.Equip(actor.Belongings,Array.Empty<Possession>(),actor.Planet);PublishNpcGroundGoods(actor);}
        if(building&&!packing)
        {
            var bundle=actor.Belongings.Items.FirstOrDefault(p=>actor.Belongings.Carried(p)&&p.Kind==PossessionKind.CampBundle&&p.CampMaterial==actor.GroupId);
            if(bundle!=null){target=LivingHouse(actor)?.Position??group.Anchor;actor.TaskDetail="bringing and opening the actual bound shelter materials";actor.TaskWaiting=false;if(HumanDistance(actor.Planet,target)<2){NpcCampWork.Unbundle(actor.Belongings,bundle,actor.Planet);PublishNpcGroundGoods(actor);}return true;}
        }
        CampObject shelter=Camp.Objects.FirstOrDefault(c=>c.Id==task.Object&&c.NpcGroup==actor.GroupId&&c.NpcSite==group.Situation.Camp.Id);
        CampComponent part=shelter?.Blueprint==null?null:NpcCampParts(shelter).FirstOrDefault(c=>c.Key==task.Part);
        if(task.Packing!=packing||part==null||packing&&CampBlueprints.State(shelter.Blueprint,part)?.Materials.Count==0||!packing&&CampBlueprints.Done(shelter.Blueprint,part))
        {shelter=null;part=null;task.Object=task.Part="";task.Work=0;}
        if(shelter==null&&actor.SimulationAge>=task.ChooseAt)
        {
            task.ChooseAt=actor.SimulationAge+1;task.Packing=packing;
            foreach(var house in Camp.Objects.Where(c=>c.NpcGroup==actor.GroupId&&c.NpcSite==group.Situation.Camp.Id&&c.Blueprint!=null).OrderBy(c=>c.NpcHousehold==actor.Resident.Household?0:1).ThenBy(c=>HumanDistance(c.Position,actor.Planet)))
            {
                var plan=house.Blueprint;var definitions=NpcCampParts(house);int stage=definitions.Where(c=>!CampBlueprints.Done(plan,c)).Select(c=>c.Stage).DefaultIfEmpty(4).Min();
                var choices=packing?definitions.Where(c=>NpcCampWork.Portable(plan,c,NpcCarryCampPoles(group))&&CampBlueprints.State(plan,c)?.Materials.Count>0).OrderByDescending(c=>c.Stage):definitions.Where(c=>c.Stage==stage&&!CampBlueprints.Done(plan,c)).OrderBy(c=>c.Key);
                part=choices.FirstOrDefault(c=>!NpcCampPartClaimed(actor,house,c.Key));if(part==null)continue;shelter=house;task.Object=house.Id;task.Part=part.Key;break;
            }
        }
        if(shelter==null||part==null)
        {
            if(packing){if(RecoverNpcCampCargo(actor,dt,ref target))return true;actor.TaskDetail="finished packing this household's shelter; waiting for the others";actor.TaskWaiting=false;target=group.Anchor;return true;}
            if(PrepareNpcCampBedding(actor,group,dt,ref target))return true;
            actor.CampTask=null;return false;
        }
        routine=new HumanActivitySample{Person=actor.ResidentId,Group=actor.GroupId,Activity=packing?HumanActivity.PackCamp:HumanActivity.BuildCamp,Position=shelter.Position,Destination=shelter.Position};actor.CurrentActivity=routine.Activity;
        target=NpcCampWorkPoint(actor,shelter,part);actor.TaskWaiting=false;actor.TaskDetail=(packing?"taking down ":"assembling ")+part.Name+" for "+(shelter.NpcHousehold==actor.Resident.Household?"the household shelter":"a bandmate's shelter");
        if(packing)
        {
            if(HumanDistance(actor.Planet,target)>1.8){actor.TaskWaiting=true;return true;}
            task.Work+=NpcWorkSeconds(dt);if(task.Work<Math.Max(15,part.Seconds*.25))return true;
            foreach(var material in NpcCampWork.Remove(shelter,part.Key))
            {
                material.Position=actor.Planet;var stock=actor.Belongings;stock.Items.Add(material);
                bool carried=stock.CarriedMassKg<28&&(NpcBelongings.Pack(stock,material)||stock.Hold(material.Id,out _));
                if(!carried){material.Place=PossessionPlace.Ground;material.ContainerId="";material.GroundOffsetM=.04;material.GroundRotation=new V(90,0,0);}
            }
            NpcCampWork.Bundle(actor.Belongings,actor.GroupId,shelter.NpcHousehold,actor.Planet,actor.Resident.AgeYears<16?12:24);
            PublishNpcGroundGoods(actor);npcCampPacked++;InvalidateCampVisual(shelter);task.Object=task.Part="";task.Work=0;task.ChooseAt=actor.SimulationAge+.2;StoreLivingCamp(group);return true;
        }
        var blueprint=shelter.Blueprint;
        if(!CampBlueprints.HasMaterial(blueprint,part))
        {
            bool cut=actor.Belongings.Items.Any(p=>actor.Belongings.Carried(p)&&WoodCrafting.Cutter(p));
            var material=actor.Belongings.Items.FirstOrDefault(p=>actor.Belongings.Carried(p)&&CampBlueprints.Suitable(blueprint,part,p)&&(part.Kg>0||p.HeightM<=part.LengthM+.1||cut));
            if(material!=null)
            {
                if(HumanDistance(actor.Planet,target)>1.8){actor.TaskDetail="carrying "+material.Name+" to the shelter";return true;}
                material.CampMaterial=actor.GroupId;
                if(CampBlueprints.Supply(blueprint,part.Key,actor.Belongings,material.Id,cut,out _,out _)){npcCampSupplied++;PublishNpcGroundGoods(actor);InvalidateCampVisual(shelter);StoreLivingCamp(group);}return true;
            }
            var gather=actor.MaterialTask??(actor.MaterialTask=new NpcMaterialTask());
            if(gather.Construction!=blueprint||gather.ConstructionKey!=part.Key){gather.Construction=blueprint;gather.ConstructionKey=part.Key;gather.Loose=null;gather.Natural=gather.Grass=false;gather.SearchAt=0;}
            if(!NpcHasMaterialTarget(gather))NpcFindMaterial(actor,gather,"shelter");
            actor.TaskWaiting=true;actor.TaskDetail="seeking actual "+CampBlueprints.Requirement(part)+" for the shelter";
            if(NpcHasMaterialTarget(gather)){NpcGatherMaterial(actor,gather,dt,ref target);foreach(var p in actor.Belongings.Items)if(actor.Belongings.Carried(p)&&CampBlueprints.Suitable(blueprint,part,p))p.CampMaterial=actor.GroupId;}return true;
        }
        if(HumanDistance(actor.Planet,target)<=1.8)
        {double work=CampBlueprints.WorkContribution(blueprint,part,NpcWorkSeconds(dt));if(work>0){npcCampWorked++;if(CampBlueprints.Done(blueprint,part)){InvalidateCampVisual(shelter);StoreLivingCamp(group);}}}
        return true;
    }
    V NpcCampWorkPoint(HumanActor actor,CampObject shelter,CampComponent part)
    {
        TerrainHandoff.Coordinates(frame,shelter.Position,out double x,out double z);double yaw=shelter.Heading*Math.PI/180;V p=part.Points.Aggregate(new V(),(a,b)=>a+b)/Math.Max(1,part.Points.Length);
        double length=Math.Sqrt(p.X*p.X+p.Z*p.Z),scale=length>.1?(length+.55)/length:0;double east=p.X*scale,north=p.Z*scale;
        return frame.Position(x+east*Math.Cos(yaw)+north*Math.Sin(yaw),z+north*Math.Cos(yaw)-east*Math.Sin(yaw));
    }
    bool PrepareNpcCampBedding(HumanActor actor,LivingGroup group,float dt,ref V target)
    {
        var bed=Camp.Objects.FirstOrDefault(c=>c.NpcGroup==actor.GroupId&&c.NpcSite==group.Situation.Camp.Id&&c.Kind==CampKind.Bedding&&c.NpcHousehold==actor.Resident.Household);
        int sleepers=group.Residents.Members.Count(p=>p.Household==actor.Resident.Household);double wanted=1+Math.Min(6,sleepers)*.5;
        if(bed==null||bed.Materials.Sum(p=>p.MassKg)>=wanted)return false;
        var stock=actor.Belongings;var material=stock.Items.FirstOrDefault(p=>stock.Carried(p)&&p.CampMaterial==0&&(p.Kind==PossessionKind.DryGrass||p.Kind==PossessionKind.LeafLitter||p.Kind==PossessionKind.ReedBundle));
        actor.CurrentActivity=HumanActivity.BuildCamp;actor.TaskWaiting=false;
        if(material!=null)
        {
            target=bed.Position;actor.TaskDetail="bringing gathered bedding to the household's sleeping place";
            if(HumanDistance(actor.Planet,target)<1.8){actor.CampTask.Work+=NpcWorkSeconds(dt);if(actor.CampTask.Work>=30){CampRules.Add(bed,material);stock.Items.Remove(material);actor.CampTask.Work=0;InvalidateCampVisual(bed);StoreLivingCamp(group);}}return true;
        }
        var task=actor.MaterialTask??(actor.MaterialTask=new NpcMaterialTask());
        if(task.Purpose!="bedding"){task.Purpose="bedding";task.Construction=null;task.Loose=null;task.Natural=task.Grass=false;task.SearchAt=0;}
        if(!NpcHasMaterialTarget(task))NpcFindMaterial(actor,task,"bedding");
        actor.TaskDetail="gathering actual dry plant material for the household bedding";if(NpcHasMaterialTarget(task))NpcGatherMaterial(actor,task,dt,ref target);else actor.TaskWaiting=true;return true;
    }
    bool RecoverNpcCampCargo(HumanActor actor,float dt,ref V target)
    {
        var stock=actor.Belongings;var bundle=stock.Items.FirstOrDefault(p=>p.Kind==PossessionKind.CampBundle&&stock.Carried(p));
        double limit=actor.Resident.AgeYears<16?12:24;
        var loose=Inventory.Items.Where(p=>p.Place==PossessionPlace.Ground&&NpcCanUseWorldItem(actor,p)&&
            (p.CampMaterial==actor.GroupId&&p.OwnerHousehold==actor.Resident.Household&&p.Kind!=PossessionKind.CampBundle&&(bundle==null||bundle.MassKg+p.MassKg<=limit)||p.OwnerPerson==actor.ResidentId&&p.CampMaterial==0))
            .OrderBy(p=>p.OwnerPerson==actor.ResidentId?1:0).ThenBy(p=>HumanDistance(p.Position,actor.Planet)).FirstOrDefault();
        if(loose==null)return false;target=loose.Position;actor.TaskWaiting=false;actor.TaskDetail="collecting the household's real packed belongings before leaving";
        if(HumanDistance(target,actor.Planet)>1.7)return true;
        actor.CampTask.Work+=NpcWorkSeconds(dt);if(actor.CampTask.Work<15)return true;actor.CampTask.Work=0;
        if(loose.CampMaterial==actor.GroupId)
        {
            NpcBelongings.TransferTree(Inventory,stock,loose);NpcCampWork.Bundle(stock,actor.GroupId,actor.Resident.Household,actor.Planet,limit);
            if(stock.Items.Contains(loose)&&!NpcBelongings.Pack(stock,loose))stock.Hold(loose.Id,out _);PublishNpcGroundGoods(actor);
        }
        else if(!AcquireNpcItem(actor,loose))return false;
        return true;
    }
}
