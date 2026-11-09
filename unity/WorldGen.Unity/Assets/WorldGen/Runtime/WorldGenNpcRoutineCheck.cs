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
    public IEnumerator CheckNpcMaterialRoutine(string output,List<string> report)
    {
        void Check(bool value,string text){if(!value){File.WriteAllLines(Path.Combine(output,"failed-checks.txt"),report.Concat(new[]{"FAIL "+text}));Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);Debug.Log("NPC ROUTINE PASS "+text);}
        npcModel??=new NpcModelClient();npcModel.Config.Enabled=false;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();
        double until=Time.unscaledTimeAsDouble+100;while((!livingGroups.TryGetValue(PlayerRules.Identity.Group,out var ready)||!ready.CampLoaded||!livingPeople.Values.Any(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18)||scatterChunks.Count<2)&&Time.unscaledTimeAsDouble<until)yield return null;
        Check(livingGroups.TryGetValue(PlayerRules.Identity.Group,out var group)&&group.CampLoaded,"The real camp and surrounding natural-resource tiles load");
        var actor=livingPeople.Values.First(p=>p.GroupId==group.Situation.Band.Id&&p.Resident.AgeYears>=18);paused=true;livingQueryAt=Time.unscaledTimeAsDouble+1000;while(livingWork!=null)yield return null;
        group.Situation.Phase=HumanCampPhase.Occupied;group.TenderDuty=null;actor.Threat=null;actor.Daily=null;
        TestWeatherOverride=new WeatherFrame(22,.45,0,0,.15,0,1500,500,10000,1,1,2,0);TerrainMapViewport.Degrees(Position,out _,out double longitude);PlayerRules.SetLocalSolarHour(12,longitude);nextDaylightAt=0;SetDaylight(LocalSolarHour,skyYear);
        actor.HouseCheckAt=0;var home=LivingHouse(actor)?.Position??group.Anchor;TerrainHandoff.Coordinates(frame,home,out double x,out double z);actor.Motor.Place(x+2,z+2,CampFloor(x+2,z+2));actor.Planet=frame.Position(actor.Motor.X,actor.Motor.Z);actor.Root.transform.localPosition=new Vector3((float)(actor.Motor.X-originX),(float)(actor.Motor.Y-TerrainMotion.EyeHeightM),(float)(actor.Motor.Z-originZ));
        NpcBelongings.Equip(actor.Belongings,Array.Empty<Possession>(),actor.Planet);PublishNpcGroundGoods(actor);actor.MaterialTask=new NpcMaterialTask();actor.Routine=new HumanActivitySample{Person=actor.ResidentId,Group=actor.GroupId,Activity=HumanActivity.GatherFuel,Position=home,Destination=home,Role="FuelOutbound"};
        var start=actor.Planet;double travel=0;int harvested=npcMaterialHarvests;until=Time.unscaledTimeAsDouble+150;
        while(actor.MaterialTask.Deliveries==0&&Time.unscaledTimeAsDouble<until)
        {actor.Threat=null;UpdateLivingHuman(actor,false,Math.Min(.1f,Time.unscaledDeltaTime));travel=Math.Max(travel,HumanDistance(start,actor.Planet));yield return null;}
        File.WriteAllText(Path.Combine(output,"haul-state.txt"),"task="+actor.TaskDetail+"; travel="+travel+"; harvests="+(npcMaterialHarvests-harvested)+"; deliveries="+actor.MaterialTask.Deliveries);
        Check(actor.MaterialTask.Deliveries>0&&travel>3&&npcMaterialHarvests>harvested,"The worker actually walks out, gathers a real resource, and returns a load to the household");
        File.WriteAllText(Path.Combine(output,"delivery-state.txt"),"homeDistance="+HumanDistance(actor.Planet,home)+"; "+string.Join("; ",actor.MaterialTask.LastDelivered.Select(id=>id+": world="+Inventory.Find(id)?.Place+", actor="+actor.Belongings.Find(id)?.Place+", positionDistance="+(Inventory.Find(id)==null?-1:HumanDistance(Inventory.Find(id).Position,home)))));
        Check(actor.MaterialTask.LastDelivered.Length>0&&actor.MaterialTask.LastDelivered.All(id=>Inventory.Find(id)?.Place==PossessionPlace.Ground),"The delivered items really leave the hands/carrier and exist once on the ground");
        Check(actor.MaterialTask.LastDelivered.All(id=>HumanDistance(Inventory.Find(id).Position,home)<2.5),"The actual delivered load is on the household doorstep rather than left at its source");
        Check(actor.MaterialTask.LastDelivered.Any(id=>Inventory.Find(id)?.NaturalId>0),"The delivered load includes an actual shared-ledger natural branch");
        var fire=Camp.Objects.First(c=>c.Kind==CampKind.Fire&&c.NpcGroup==actor.GroupId);foreach(var p in fire.Materials.ToArray()){fire.Materials.Remove(p);p.Place=PossessionPlace.Ground;p.Position=fire.Position;Inventory.Items.Add(p);}fire.Burning=false;fire.EmberSeconds=0;fire.Wetness=0;
        // Explicit qualified-firemaker fixture. The starting band may only know
        // tending; this grants one technique and one real command-equivalent
        // kit, without generating the fire's tinder, kindling or wood.
        var knowledge=group.Residents.Techniques;group.Residents.Techniques=(bool[])knowledge.Clone();group.Residents.Techniques[9]=true;
        var kit=HumanEquipment.Item(PossessionKind.HandDrillSet,actor.ResidentId,"routine-ignition-kit",actor.Planet);NpcOwnership.Assign(kit,actor.Resident,actor.GroupId);kit.Place=PossessionPlace.Ground;Inventory.Items.Add(kit);
        actor.MaterialTask=new NpcMaterialTask();group.TendedFire=fire;group.TendUntil=Time.unscaledTimeAsDouble+300;group.TenderDuty=new HumanActivitySample{Person=actor.ResidentId,Group=actor.GroupId,Activity=HumanActivity.TendFire,Position=fire.Position,Destination=fire.Position};
        until=Time.unscaledTimeAsDouble+260;while(!fire.Burning&&Time.unscaledTimeAsDouble<until){actor.Threat=null;UpdateLivingHuman(actor,false,Math.Min(.1f,Time.unscaledDeltaTime));yield return null;}
        File.WriteAllText(Path.Combine(output,"ignition-state.txt"),"task="+actor.TaskDetail+"; fuel="+fire.FuelKg+"; tinder="+fire.Materials.Where(CampRules.Tinder).Sum(p=>p.MassKg)+"; kindling="+fire.Materials.Where(CampRules.Kindling).Sum(p=>p.MassKg)+"; kit="+NpcMaterialUse.HasIgnitionKit(actor.Belongings));
        Check(fire.Burning&&fire.FuelKg>0,"A qualified worker completes a cold start through physical material retrieval, preparation and fire-drill work");
        Check(NpcMaterialUse.HasIgnitionKit(actor.Belongings)&&!Inventory.Items.Any(p=>p.Id==kit.Id),"The fire-making kit is actually retrieved and carried, rather than assumed to exist");group.Residents.Techniques=knowledge;
        CaptureLivingPerson(actor);StoreLivingCamp(group);LivingState.Copy().Validate();Inventory.Validate();Check(true,"The full movement/material/fire cycle validates for saving");
        motion.Place(actor.Motor.X-5,actor.Motor.Z+3,CampFloor(actor.Motor.X-5,actor.Motor.Z+3));humanCamera.Restore(0,false);UpdateHumans();for(int i=0;i<8;i++){UpdateCamera();var d=transform.InverseTransformDirection(actor.Root.transform.position+transform.up*.7f-view.transform.position).normalized;heading=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;pitch=-Mathf.Asin(d.y)*Mathf.Rad2Deg;UpdateHumans();}
        paused=false;livingBenchmarkFreeze=true;npcThoughts=true;yield return NpcLifeImage(output,"completed-material-fire-routine");livingBenchmarkFreeze=false;TestWeatherOverride=null;DisposeNpcMinds();
    }
}
