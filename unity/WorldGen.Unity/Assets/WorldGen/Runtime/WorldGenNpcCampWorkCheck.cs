using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckNpcCampWork(string output,List<string> report)
    {
        void Check(bool value,string text){if(!value){File.WriteAllLines(Path.Combine(output,"failed-checks.txt"),report.Concat(new[]{"FAIL "+text}));Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);Debug.Log("NPC CAMP PASS "+text);}
        npcModel??=new NpcModelClient();npcModel.Config.Enabled=false;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();
        double until=Time.unscaledTimeAsDouble+100;while((!livingGroups.TryGetValue(PlayerRules.Identity.Group,out var ready)||!ready.CampLoaded||!livingPeople.Values.Any(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18))&&Time.unscaledTimeAsDouble<until)yield return null;
        Check(livingGroups.TryGetValue(PlayerRules.Identity.Group,out var group)&&group.CampLoaded,"The actual generated household camp is loaded");paused=true;livingQueryAt=Time.unscaledTimeAsDouble+180;while(livingWork!=null)yield return null;
        var shelter=Camp.Objects.FirstOrDefault(c=>c.NpcGroup==group.Situation.Band.Id&&c.Blueprint!=null&&NpcCampWork.NextPacking(c.Blueprint,NpcCarryCampPoles(group))!=null);Check(shelter!=null,"The actual shelter contains recoverable portable contributions");
        var actor=livingPeople.Values.First(p=>p.GroupId==group.Situation.Band.Id&&p.Resident.AgeYears>=18);actor.Threat=null;npcPlans.Clear();group.TenderDuty=null;
        var currentHouse=LivingHouse(actor);Check(currentHouse!=null,"The actual adult has a household shelter at the current camp");
        var oldHouse=currentHouse.Copy();oldHouse.Id+="-old-site-lookup-proof";oldHouse.NpcSite=currentHouse.NpcSite^7919;
        try
        {
            Camp.Objects.Insert(0,oldHouse);actor.CampHouse=oldHouse;actor.HouseSite=group.Situation.Camp.Id;actor.HouseCheckAt=actor.SimulationAge+30;
            Check(LivingHouse(actor)==currentHouse,"An old household shelter nearby cannot replace the current site's cached household destination");
        }
        finally{Camp.Objects.Remove(oldHouse);actor.CampHouse=currentHouse;}
        NpcBelongings.Equip(actor.Belongings,Array.Empty<Possession>(),actor.Planet);PublishNpcGroundGoods(actor);
        var initialPhase=group.Situation.Phase;group.Situation.Phase=HumanCampPhase.Packing;group.Situation.Progress=.99;
        string[] BeforeIds()=>Camp.Objects.SelectMany(c=>c.OwnedMaterials()).Concat(Inventory.Items).Concat(livingPeople.Values.SelectMany(p=>p.Belongings?.Items??new List<Possession>())).Select(p=>p.Id).OrderBy(x=>x).ToArray();
        double Mass()=>Camp.Objects.Sum(c=>c.MassKg)+Inventory.Items.Sum(p=>p.MassKg)+livingPeople.Values.Sum(p=>p.Belongings?.Items.Sum(i=>i.MassKg)??0);
        var ids=BeforeIds();double mass=Mass();int packed=npcCampPacked;UpdateLivingCampConstruction(group);
        Check(ids.SequenceEqual(BeforeIds())&&Math.Abs(mass-Mass())<1e-8,"An itinerary progress change alone does not remove, create or swap any material");
        var routine=actor.Routine;actor.Routine=new HumanActivitySample{Person=actor.ResidentId,Group=actor.GroupId,Activity=HumanActivity.PackCamp,Position=shelter.Position,Destination=shelter.Position};V target=actor.Planet;
        PrepareNpcCampTask(actor,group,ref actor.Routine,.001f,ref target);var task=actor.CampTask;Check(task!=null&&task.Part.Length>0,"A real adult claims a specific existing component");
        var workedShelter=Camp.Objects.First(c=>c.Id==task.Object);var component=CampBlueprints.Components(workedShelter.Blueprint).First(c=>c.Key==task.Part);string key=component.Key;var materials=CampBlueprints.State(workedShelter.Blueprint,component).Materials.Select(p=>p.Id).ToArray();
        TerrainHandoff.Coordinates(frame,target,out double x,out double z);actor.Motor.Place(x+3,z,CampFloor(x+3,z));actor.Planet=frame.Position(actor.Motor.X,actor.Motor.Z);actor.Root.transform.localPosition=new Vector3((float)(actor.Motor.X-originX),(float)(actor.Motor.Y-TerrainMotion.EyeHeightM),(float)(actor.Motor.Z-originZ));actor.Speed=0;actor.Yaw=actor.TargetYaw=270;
        var start=actor.Planet;until=Time.unscaledTimeAsDouble+35;
        while(npcCampPacked==packed&&Time.unscaledTimeAsDouble<until){UpdateLivingHuman(actor,false,Math.Min(.1f,Time.unscaledDeltaTime));yield return null;}
        Check(npcCampPacked==packed+1&&HumanDistance(start,actor.Planet)>.6,"The worker walks to the structure and spends work time before dismantling a component");
        Check(materials.All(id=>CampBlueprints.State(workedShelter.Blueprint,component)?.Materials.All(p=>p.Id!=id)!=false),"The removed component no longer owns its recovered goods");
        Check(materials.All(id=>actor.Belongings.Find(id)!=null||Inventory.Find(id)!=null),"The same recovered materials are carried or physically set down, rather than deleted");
        Check(ids.SequenceEqual(BeforeIds())&&Math.Abs(mass-Mass())<1e-8,"Physical dismantling conserves all material identities and mass");
        var held=actor.Belongings.Items.Where(p=>p.CampMaterial==actor.GroupId&&actor.Belongings.Carried(p)).ToArray();Check(held.Length>0&&held.All(p=>!NpcMaterialUse.Fuel(p)),"Recovered shelter cargo is really carried and excluded from automatic fire fuel");
        group.Situation.Phase=HumanCampPhase.Occupied;actor.CampTask=null;actor.Routine=new HumanActivitySample{Person=actor.ResidentId,Group=actor.GroupId,Activity=HumanActivity.RepairShelter,Position=workedShelter.Position,Destination=workedShelter.Position};int supplied=npcCampSupplied;
        until=Time.unscaledTimeAsDouble+45;while(!CampBlueprints.Done(workedShelter.Blueprint,component)&&Time.unscaledTimeAsDouble<until){UpdateLivingHuman(actor,false,Math.Min(.1f,Time.unscaledDeltaTime));yield return null;}
        Check(npcCampSupplied>supplied&&CampBlueprints.Done(workedShelter.Blueprint,component),"A worker physically supplies and finishes the missing component using the recovered stock");
        Check(ids.SequenceEqual(BeforeIds())&&Math.Abs(mass-Mass())<1e-8,"Reassembly conserves the same material identities and mass");
        var newCamp=HumanCampLayout.Create(group.Situation,group.Residents,group.Anchor,frame.RadiusM,sourceWorld.Recipe.Seed,Camp.SimulatedSeconds,false);
        Check(newCamp.Any(c=>c.Blueprint!=null)&&newCamp.All(c=>!c.OwnedMaterials().Any()),"An observed arrival creates empty plans and hearths without manufacturing a second shelter or fire fuel");
        Check(newCamp.All(c=>!NpcCampHasGroundWear(c)),"An empty observed arrival does not erase the grass beneath unbuilt shelters, empty beds or unlit hearths");
        var available=NpcCampParts(workedShelter);var tie=available.FirstOrDefault(c=>c.Kind==CampPartKind.Binding&&CampBlueprints.State(workedShelter.Blueprint,c)?.Materials.Count>0);var poles=available.Where(c=>(c.Kind==CampPartKind.Post||c.Kind==CampPartKind.Beam||c.Kind==CampPartKind.Rib)&&CampBlueprints.State(workedShelter.Blueprint,c)?.Materials.Count>0).Take(2).ToArray();
        Check(tie!=null&&poles.Length==2,"The actual shelter has real lashing and poles for the bound-load check");
        var bundleIds=new List<string>();foreach(var part in poles.Concat(new[]{tie}))foreach(var p in NpcCampWork.Remove(workedShelter,part.Key)){bundleIds.Add(p.Id);actor.Belongings.Items.Add(p);}
        var bundle=NpcCampWork.Bundle(actor.Belongings,actor.GroupId,workedShelter.NpcHousehold,actor.Planet);File.WriteAllText(Path.Combine(output,"bound-load.json"),JsonUtility.ToJson(bundle,true));
        Check(bundle!=null&&actor.Belongings.Carried(bundle)&&bundleIds.All(id=>bundle.BundledMaterials.Any(p=>p.Id==id)),"Actual recovered poles and lashing become one carried bound load with their exact contents preserved");
        UpdateNpcProps(actor,group,ActualNpcCarryRole(actor));Check(actor.NpcProps.Any(p=>p.Item==bundle&&p.Root.GetComponentsInChildren<Renderer>().Length>=2),"The bound load appears on the actual person using the existing material models");
        var carrier=livingPeople.Values.First(p=>p.GroupId==actor.GroupId&&p.Resident.Household==bundle.OwnerHousehold&&p.Resident.AgeYears>=18);
        bundle.Place=PossessionPlace.Ground;bundle.ContainerId="";bundle.Position=actor.Planet;PublishNpcGroundGoods(actor);
        NpcBelongings.Equip(carrier.Belongings,Array.Empty<Possession>(),carrier.Planet);PublishNpcGroundGoods(carrier);
        var heldProof=new[]{PortableTechnology.Create(PossessionKind.StoneBlade,carrier.Planet),PortableTechnology.Create(PossessionKind.StoneBlade,carrier.Planet)};
        heldProof[0].Place=PossessionPlace.RightHand;heldProof[1].Place=PossessionPlace.LeftHand;foreach(var tool in heldProof)carrier.Belongings.Items.Add(tool);
        TerrainHandoff.Coordinates(frame,bundle.Position,out double loadX,out double loadZ);carrier.Motor.Place(loadX,loadZ,CampFloor(loadX,loadZ));carrier.Planet=bundle.Position;
        V retrieve=bundle.Position;Check(RecoverNpcCampCargo(carrier,.1f,ref retrieve)&&carrier.Belongings.Find(bundle.Id)==bundle&&carrier.Belongings.Carried(bundle)&&Inventory.Find(bundle.Id)==null,"A real packed load is retrieved with both hands initially occupied, stowing or setting down the held tools automatically");
        Check(bundleIds.All(id=>bundle.BundledMaterials.Any(p=>p.Id==id)),"Occupied-hand retrieval preserves every original bound contribution");
        foreach(var tool in heldProof){carrier.Belongings.Items.Remove(tool);Inventory.Items.Remove(tool);}carrier.Belongings.Validate();
        var foraging=new HumanActivitySample{Activity=HumanActivity.Travel,Person=carrier.ResidentId,Group=carrier.GroupId,Position=frame.Position(carrier.Motor.X+50,carrier.Motor.Z)};
        V delivery=foraging.Position;group.Situation.Phase=HumanCampPhase.Occupied;carrier.CampCargoDeliveredSite=0;PrepareNpcCampTask(carrier,group,ref foraging,.05f,ref delivery);
        Check(HumanDistance(delivery,LivingHouse(carrier)?.Position??group.Anchor)<.01,"A scheduled forager delivers the real shelter load home before setting off on a new outing");
        TerrainHandoff.Coordinates(frame,delivery,out double deliveryX,out double deliveryZ);carrier.Motor.Place(deliveryX,deliveryZ,CampFloor(deliveryX,deliveryZ));carrier.Planet=delivery;
        PrepareNpcCampTask(carrier,group,ref foraging,.05f,ref delivery);
        Check(carrier.Belongings.Find(bundle.Id)==null&&bundleIds.All(id=>Inventory.Find(id)!=null),"Arrival delivery opens the same actual bound load and stages its original materials for builders instead of carrying it away foraging");
        // Isolate the departure predicate from still-fitted tents and other
        // loads so this specifically tests personal kit, not a vacuous hold.
        var hiddenObjects=Camp.Objects.Where(c=>c.NpcGroup==actor.GroupId).ToArray();
        var hiddenGoods=Inventory.Items.Where(p=>p.OwnerGroup==actor.GroupId||p.CampMaterial==actor.GroupId).ToArray();
        var kit=HumanEquipment.Item(PossessionKind.StoneFlake,carrier.ResidentId,"departure-proof",carrier.Planet);NpcOwnership.Assign(kit,carrier.Resident,carrier.GroupId);kit.Place=PossessionPlace.Ground;
        var priorTask=carrier.CampTask;var kitPhase=group.Situation.Phase;
        try
        {
            foreach(var obj in hiddenObjects)Camp.Objects.Remove(obj);foreach(var item in hiddenGoods)Inventory.Items.Remove(item);Inventory.Items.Add(kit);carrier.CampTask=new NpcCampTask();
            Check(NpcBelongings.CanTake(carrier.Belongings,kit)&&NpcCampNeedsPacking(group),"A departure with all tent loads ready still waits for a nearby owner's useful cutting tool when real carrying space remains");
            V kitTarget=carrier.Planet;group.Situation.Phase=HumanCampPhase.Packing;
            var kitRoutine=new HumanActivitySample{Activity=HumanActivity.PackCamp,Person=carrier.ResidentId,Group=carrier.GroupId,Position=carrier.Planet};
            for(int i=0;i<30&&Inventory.Find(kit.Id)!=null;i++)PrepareNpcCampTask(carrier,group,ref kitRoutine,.05f,ref kitTarget);
            Check(carrier.Belongings.Find(kit.Id)==kit&&Inventory.Find(kit.Id)==null,"The owner retrieves the same cutting edge through repeated real camp-task ticks without resetting pickup work each frame");
            Check(!NpcCampNeedsPacking(group),"Retrieving the last feasible useful kit releases the isolated departure hold");
        }
        finally
        {
            carrier.Belongings.Items.Remove(kit);Inventory.Items.Remove(kit);Inventory.Items.AddRange(hiddenGoods);Camp.Objects.AddRange(hiddenObjects);carrier.CampTask=priorTask;group.Situation.Phase=kitPhase;
        }
        // A previously unvisited partial camp receives its missing INITIAL
        // stock once as real loose/bound goods, not a future progress factory.
        var initial=new CampObject{Id=shelter.Id+"-initial-stock-proof",Kind=CampKind.Blueprint,NpcGroup=actor.GroupId,NpcHousehold=actor.Resident.Household,Position=shelter.Position,Blueprint=CampBlueprints.Create(CampDesign.LeanTo,2,2,CampCover.Hide)};
        HumanCampLayout.FitShelter(initial,new double[9]);var expectedGoods=initial.OwnedMaterials().ToArray();var expectedIds=expectedGoods.Select(p=>p.Id).OrderBy(id=>id).ToArray();double expectedMass=expectedGoods.Sum(p=>p.MassKg);
        initial.Blueprint.Parts.Clear();initial.NpcNeedsInitialStock=true;Camp.Objects.Add(initial);StageInitialNpcSupplies(initial);
        var staged=Inventory.Items.Where(p=>p.Id.StartsWith(initial.Id+"-material-",StringComparison.Ordinal)||p.BundledMaterials?.Any(m=>m.Id.StartsWith(initial.Id+"-material-",StringComparison.Ordinal))==true).ToArray();
        var recovered=staged.SelectMany(p=>p.BundledMaterials??new List<Possession>{p}).ToArray();
        Check(!initial.NpcNeedsInitialStock&&!initial.OwnedMaterials().Any()&&expectedIds.SequenceEqual(recovered.Select(p=>p.Id).OrderBy(id=>id))&&Math.Abs(expectedMass-staged.Sum(p=>p.MassKg))<1e-8,"An initial partially erected camp stages its complete finite stock once with exact original IDs and mass");
        int countBefore=Inventory.Items.Count;StageInitialNpcSupplies(initial);Check(Inventory.Items.Count==countBefore,"A second initial-stock request cannot manufacture another load");
        foreach(var p in staged)Inventory.Items.Remove(p);Camp.Objects.Remove(initial);
        var crewBefore=group.CampWorkers.ToArray();var carrierPoint=carrier.Planet;var carrierRoutine=carrier.Routine;var carrierTask=carrier.CampTask;var carrierMaterial=carrier.MaterialTask;bool crewNeed=group.NeedsCampWork;var tenderBefore=group.TenderDuty;
        try
        {
            TerrainHandoff.Coordinates(frame,group.Anchor,out double crewX,out double crewZ);carrier.Planet=frame.Position(crewX+120,crewZ);carrier.Routine=new HumanActivitySample{Activity=HumanActivity.BuildCamp};carrier.CampTask=new NpcCampTask{Packing=false};carrier.MaterialTask=null;group.TenderDuty=null;group.NeedsCampWork=true;group.CampWorkers.Clear();group.CampWorkers.Add(carrier.ResidentId);group.CampWorkersAt=0;
            UpdateNpcCampWorkers(group);Check(group.CampWorkers.Contains(carrier.ResidentId)&&group.CampWorkers.Count<=4,"An assigned builder fetching real wood beyond the camp boundary remains on the bounded construction crew");
        }
        finally{carrier.Planet=carrierPoint;carrier.Routine=carrierRoutine;carrier.CampTask=carrierTask;carrier.MaterialTask=carrierMaterial;group.NeedsCampWork=crewNeed;group.TenderDuty=tenderBefore;group.CampWorkers.Clear();foreach(long person in crewBefore)group.CampWorkers.Add(person);}
        StoreLivingCamp(group);CaptureLivingPerson(actor);InvalidateCampVisual(workedShelter);
        LivingState.Copy().Validate();Check(true,"Physical camp work and local itinerary state validate for saving");
        actor.Routine=routine;group.Situation.Phase=initialPhase;npcThoughts=true;
        motion.Place(actor.Motor.X-5,actor.Motor.Z+2,CampFloor(actor.Motor.X-5,actor.Motor.Z+2));humanCamera.Restore(0,false);UpdateHumans();
        for(int i=0;i<8;i++){UpdateCamera();var d=transform.InverseTransformDirection(actor.Root.transform.position+transform.up*.7f-view.transform.position).normalized;heading=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;pitch=-Mathf.Asin(d.y)*Mathf.Rad2Deg;UpdateHumans();}
        paused=false;livingBenchmarkFreeze=true;yield return NpcLifeImage(output,"material-backed-camp-work");livingBenchmarkFreeze=false;paused=true;
        var offlineHouse=Camp.Objects.First(o=>o.NpcGroup==group.Situation.Band.Id&&o.Blueprint!=null&&CampBlueprints.Complete(o.Blueprint)&&NpcCampParts(o).Any(c=>c.Kind==CampPartKind.Cover));
        var offlinePart=NpcCampParts(offlineHouse).First(c=>c.Kind==CampPartKind.Cover);
        var offlineGoods=NpcCampWork.Remove(offlineHouse,offlinePart.Key);Inventory.Items.AddRange(offlineGoods);
        var offlineIds=offlineGoods.Select(p=>p.Id).ToHashSet();double offlineMass=offlineGoods.Sum(p=>p.MassKg);
        CaptureLivingHumans();UnloadLivingCamp(group);
        foreach(var person in livingPeople.Values.Where(p=>p.GroupId==group.Situation.Band.Id).ToArray())
        {CaptureLivingPerson(person);DestroyHuman(person);humanNpcs.Remove(person);livingPeople.Remove(person.ResidentId);}
        double savedSolar=PlayerRules.SolarDays;PlayerRules.SolarDays+=.1;
        double catchUpUntil=Time.unscaledTimeAsDouble+20;
        while(!group.CampLoaded&&Time.unscaledTimeAsDouble<catchUpUntil){LoadLivingCamp(group);yield return null;}
        Check(group.CampLoaded,"A previously visited unloaded camp returns through the actual asynchronous catch-up path");
        var returnedHouse=Camp.Objects.First(o=>o.Id==offlineHouse.Id);var restored=CampBlueprints.State(returnedHouse.Blueprint,offlinePart)?.Materials??new List<Possession>();
        Check(CampBlueprints.Done(returnedHouse.Blueprint,offlinePart)&&restored.Any(p=>offlineIds.Contains(p.Id))&&Math.Abs(restored.Sum(p=>p.MassKg)-offlineMass)<1e-8,"Unobserved repair uses the same finite staged roof material after unload and return");
        Check(!Inventory.Items.Any(p=>offlineIds.Contains(p.Id)),"The returned shelter does not leave a duplicate loose roof material behind");
        PlayerRules.SolarDays=savedSolar;LivingState.Copy().Validate();Check(true,"Unloaded camp catch-up preserves valid saved local state");
        paused=false;DisposeNpcMinds();
    }
}
