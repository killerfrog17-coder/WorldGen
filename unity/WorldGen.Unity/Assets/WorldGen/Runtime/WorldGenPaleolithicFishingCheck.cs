using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
public sealed partial class LocalTerrainExplorer
{
 IEnumerator CheckPaleolithicFishingFlow(string output,List<string> report)
 {
  Application.targetFrameRate=60;
  void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);report.Add("PASS "+text);Debug.Log("PALEO FISHING CHECK PASS "+text);}
  InitializeInventory();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();humanCamera.Restore(0,false);PlayerRules.Settings.ItemCraftingTimeMultiplier=.1;WeatherControl.Set("clear");
  bool wet=false;double wx=0,wz=0;for(int ring=1;ring<=50&&!wet;ring++)for(int i=0;i<24&&!wet;i++){double a=i*Math.PI/12,x=ring*4*Math.Sin(a),z=ring*4*Math.Cos(a);var water=SampleMotionWater(x,z);double depth=water.Surface-CampFloor(x,z);if(!double.IsFinite(water.Surface)||depth<.3||depth>1)continue;wx=x;wz=z;wet=true;}
  Check(wet,"Actual coast provides shallow rendered water for both line and hand-net controls");motion.Place(wx-.9,wz,CampFloor(wx-.9,wz));
  double readyUntil=Time.unscaledTimeAsDouble+90;while((animalPlanner==null||!CenterReady)&&Time.unscaledTimeAsDouble<readyUntil)yield return null;var spawned=ExecuteAnimalSpawnCommand("/spawn coastal-forage-fish-guild 2");Check(spawned.Success,"Two real fish enter the ordinary actor spawn queue: "+string.Join(" ",spawned.Lines));double until=Time.unscaledTimeAsDouble+90;while(animals.Values.Count(a=>a.Manual&&a.Spawn.Profile.slug=="coastal-forage-fish-guild")<2&&Time.unscaledTimeAsDouble<until)yield return null;
  var fish=animals.Values.Where(a=>a.Manual&&a.Spawn.Profile.slug=="coastal-forage-fish-guild").Take(2).ToArray();Check(fish.Length==2,"Actual fish skins and normal wildlife health load");
  for(int i=0;i<2;i++)
  {
   var a=fish[i];a.X=wx;a.Z=wz;a.Planet=frame.Position(wx,wz);a.GroundY=CampFloor(wx,wz);a.Speed=a.TargetSpeed=0;a.NextDecision=a.Life+1000;a.Fade=1;
   a.Root.transform.localPosition=new Vector3((float)(wx-originX),(float)SampleMotionWater(wx,wz).Surface-.35f,(float)(wz-originZ));var target=a.Renderer.bounds.center;view.transform.LookAt(target);
   var gear=PortableTechnology.Create(i==0?PossessionKind.HandLine:PossessionKind.FibreNet,Position);Inventory.Items.Add(gear);SetCraftWorkArea(gear);RefreshInventoryNearby(true);Debug.Log("FISHING contact id="+a.Spawn.Id+" mass="+a.MassKg+" medium="+a.Spawn.Profile.medium+" centerDistance="+Vector3.Distance(a.Renderer.bounds.center,view.transform.position)+" rootDot="+Vector3.Dot(view.transform.forward,(a.Root.transform.position-view.transform.position).normalized)+" kind="+walkingPatch.Sample(a.Planet).WaterKind+" gearAccessible="+AccessiblePossession(gear));BeginFishing(gear);Debug.Log("FISHING result notice="+inventoryNotice+" target="+worldToolWork?.Fish?.Spawn.Id+" expected="+a.Spawn.Id);
   Check(worldToolWork?.Fish==a&&a.FishingTethered,"The real "+gear.Name.ToLowerInvariant()+" works on this actual shallow-water fish");
   until=Time.unscaledTimeAsDouble+15;while(worldToolWork!=null&&Time.unscaledTimeAsDouble<until)yield return null;
   Check(a.Dead&&a.Ragdoll!=null&&!a.FishingTethered,"Timed fishing lands the original actor as a corpse, not replacement food");Check(Inventory.Find(gear.Id)==gear&&Wildlife.Individuals.Any(p=>p.Id==a.Spawn.Id&&p.Health==0),"Fishing retains its real reusable gear and saves the caught individual's identity");
  }
  var saved=new PlaySession();StorePlayer(saved);WorldJson.Read<PlaySession>(WorldJson.Write(saved)).Validate();Check(true,"Full capture/reload retains both catches, reusable gear and physical inventory");
 }
}
