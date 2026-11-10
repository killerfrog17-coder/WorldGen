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
 IEnumerator CheckDeadfallFlow(string output,List<string> report)
 {
  Application.targetFrameRate=60;
  void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);report.Add("PASS "+text);Debug.Log("DEADFALL CHECK PASS "+text);}
  IEnumerator Shot(string name){for(int i=0;i<20;i++)yield return null;yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();Check(image.GetPixels32().Any(p=>p.r>45||p.g>45||p.b>45),"Rendered game content: "+name);File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);}
  InitializeInventory();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();humanCamera.Restore(0,false);PlayerRules.Settings.ItemCraftingTimeMultiplier=.1;WeatherControl.Set("clear");PlayerRules.SetLocalSolarHour(12,Math.Atan2(Position.Y,Position.X)*180/Math.PI);SetDaylight(LocalSolarHour,skyYear);
  var branch=WoodCrafting.Branch(new ScatterInstance{Id=664,Kind=ScatterKind.Branch,Position=Position,WidthM=1.2,HeightM=.03});branch.Id="native-trigger";branch.NaturalId=0;Inventory.Items.Add(branch);SetCraftWorkArea(branch);var blade=PortableTechnology.Create(PossessionKind.StoneBlade,Position);Inventory.Items.Add(blade);SetCraftWorkArea(blade);
  RefreshInventoryNearby(true);Check(StartPortableCraft("carve-deadfall-trigger",branch.Id),"Actual contextual trigger carving starts");double until=Time.unscaledTimeAsDouble+20;while(Inventory.Crafts.Count>0&&Time.unscaledTimeAsDouble<until)yield return null;Check(branch.Kind==PossessionKind.DeadfallTrigger&&Inventory.Find(blade.Id)==blade,"Actual branch becomes a carved trigger and preserves its cutting tool");
  BeginCampPlacement(branch,CampKind.Deadfall);bool placed=false;
  for(int i=0;i<12;i++){double a=i*Math.PI/6,x=motion.X+Math.Sin(a)*1.6,z=motion.Z+Math.Cos(a)*1.6;UpdateCamera();view.transform.LookAt(transform.TransformPoint(new Vector3((float)(x-originX),(float)CampFloor(x,z),(float)(z-originZ))));campGroundAt=0;UpdateCampPlacement();if(!campPlaceValid)continue;ConfirmCampPlacement();placed=true;break;}
  Check(placed,"Ordinary ground placement accepts the actual trigger");var trap=Camp.Objects.Last();
  var rock=new Possession{Id="native-weight",Kind=PossessionKind.LargeRock,Position=Position,WidthM=.29,HeightM=.15,DepthM=.25,MassKg=10,CellsWide=3,CellsHigh=2,Variant=ToolStone.Encode(ToolStoneMaterial.Granite,0),Tint=new V(.7,.65,.6)};Inventory.Items.Add(rock);SetCraftWorkArea(rock);AddDeadfallMaterial(trap,rock);
  var nuts=PortableTechnology.Create(PossessionKind.Nuts,Position);Inventory.Items.Add(nuts);SetCraftWorkArea(nuts);double mass=nuts.MassKg;AddDeadfallMaterial(trap,nuts);
  Check(Deadfalls.Bait(trap).MassKg==.025&&Math.Abs(nuts.MassKg-(mass-.025))<1e-8,"Adding bait takes only twenty-five grams and keeps the remaining real food");
  SetDeadfall(trap);until=Time.unscaledTimeAsDouble+20;while(worldToolWork!=null&&Time.unscaledTimeAsDouble<until)yield return null;
  Check(trap.TrapArmed&&Inventory.Find(branch.Id)==null&&trap.Materials.Any(p=>p.Id==branch.Id),"Timed balancing returns the same wooden trigger into an armed physical trap");
  TerrainHandoff.Coordinates(frame,trap.Position,out double tx,out double tz);var target=transform.TransformPoint(new Vector3((float)(tx-originX),(float)CampFloor(tx,tz)+.15f,(float)(tz-originZ)));landscapeReviewCamera=target+transform.TransformDirection(new Vector3(.85f,1.1f,-1.5f));landscapeReviewRotation=Quaternion.LookRotation(target-landscapeReviewCamera.Value,transform.up);UpdateCamera();yield return Shot("armed-stone-deadfall");
  var session=new PlaySession();StorePlayer(session);WorldJson.Read<PlaySession>(WorldJson.Write(session)).Validate();Check(true,"Actual armed trap and original owned materials pass full session capture/reload");
  Check(ExecuteAnimalSpawnCommand("/spawn rabbit").Success,"Rabbit uses the ordinary animal spawn command");until=Time.unscaledTimeAsDouble+90;while(!animals.Values.Any(a=>a.Manual&&a.Spawn.Profile.slug=="rabbit")&&Time.unscaledTimeAsDouble<until)yield return null;
  var rabbit=animals.Values.FirstOrDefault(a=>a.Manual&&a.Spawn.Profile.slug=="rabbit");Check(rabbit!=null,"A real loaded rabbit actor is available for the controlled crossing");
  paused=true;rabbit.X=tx-.5;rabbit.Z=tz;rabbit.Planet=frame.Position(rabbit.X,rabbit.Z);rabbit.GroundY=CampFloor(tx,tz);rabbit.Speed=rabbit.TargetSpeed=0;rabbit.Fade=1;UpdateAnimal(rabbit,0);
  paused=false;deadfallAt=0;UpdateDeadfalls();rabbit.X=tx+.5;rabbit.Z=tz;rabbit.Planet=frame.Position(rabbit.X,rabbit.Z);double health=rabbit.Health;deadfallAt=0;UpdateDeadfalls();paused=true;
  Check(trap.TrapSprung&&!trap.TrapArmed&&rabbit.Health<health,"The actual runtime sweep springs the trap when the real actor crosses its footprint");Check(Deadfalls.Bait(trap)==null&&trap.Materials.Count==2,"Springing consumes the original bait but retains stone and trigger");
  if(rabbit.Dead)Check(rabbit.Ragdoll!=null&&Wildlife.Individuals.Any(a=>a.Id==rabbit.Spawn.Id&&a.Health==0),"A lethal impact produces the original animal corpse and persistent record");else Check(!rabbit.ThreatWasPlayer,"A surviving animal reacts to the physical trap without magically blaming the unseen player");
  yield return Shot("sprung-deadfall");Inventory.Validate();Camp.Validate();session=new PlaySession();StorePlayer(session);WorldJson.Read<PlaySession>(WorldJson.Write(session)).Validate();Check(true,"Sprung mechanism and actual injured or dead wildlife persist in the normal full save");landscapeReviewCamera=null;landscapeReviewRotation=null;paused=false;
 }
}
