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
        var available=NpcCampParts(workedShelter);var tie=available.FirstOrDefault(c=>c.Kind==CampPartKind.Binding&&CampBlueprints.State(workedShelter.Blueprint,c)?.Materials.Count>0);var poles=available.Where(c=>(c.Kind==CampPartKind.Post||c.Kind==CampPartKind.Beam||c.Kind==CampPartKind.Rib)&&CampBlueprints.State(workedShelter.Blueprint,c)?.Materials.Count>0).Take(2).ToArray();
        Check(tie!=null&&poles.Length==2,"The actual shelter has real lashing and poles for the bound-load check");
        var bundleIds=new List<string>();foreach(var part in poles.Concat(new[]{tie}))foreach(var p in NpcCampWork.Remove(workedShelter,part.Key)){bundleIds.Add(p.Id);actor.Belongings.Items.Add(p);}
        var bundle=NpcCampWork.Bundle(actor.Belongings,actor.GroupId,workedShelter.NpcHousehold,actor.Planet);File.WriteAllText(Path.Combine(output,"bound-load.json"),JsonUtility.ToJson(bundle,true));
        Check(bundle!=null&&actor.Belongings.Carried(bundle)&&bundleIds.All(id=>bundle.BundledMaterials.Any(p=>p.Id==id)),"Actual recovered poles and lashing become one carried bound load with their exact contents preserved");
        UpdateNpcProps(actor,group,ActualNpcCarryRole(actor));Check(actor.NpcProps.Any(p=>p.Item==bundle&&p.Root.GetComponentsInChildren<Renderer>().Length>=2),"The bound load appears on the actual person using the existing material models");
        StoreLivingCamp(group);CaptureLivingPerson(actor);InvalidateCampVisual(workedShelter);
        LivingState.Copy().Validate();Check(true,"Physical camp work and local itinerary state validate for saving");
        actor.Routine=routine;group.Situation.Phase=initialPhase;npcThoughts=true;
        motion.Place(actor.Motor.X-5,actor.Motor.Z+2,CampFloor(actor.Motor.X-5,actor.Motor.Z+2));humanCamera.Restore(0,false);UpdateHumans();
        for(int i=0;i<8;i++){UpdateCamera();var d=transform.InverseTransformDirection(actor.Root.transform.position+transform.up*.7f-view.transform.position).normalized;heading=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;pitch=-Mathf.Asin(d.y)*Mathf.Rad2Deg;UpdateHumans();}
        paused=false;livingBenchmarkFreeze=true;yield return NpcLifeImage(output,"material-backed-camp-work");livingBenchmarkFreeze=false;DisposeNpcMinds();
    }
}
