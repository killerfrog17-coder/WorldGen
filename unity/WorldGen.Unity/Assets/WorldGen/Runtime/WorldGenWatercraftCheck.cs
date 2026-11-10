using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
public sealed partial class LocalTerrainExplorer
{
 IEnumerator CheckWatercraftFlow(string output,List<string> report)
 {
  Application.targetFrameRate=60;
  void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);report.Add("PASS "+text);Debug.Log("WATERCRAFT CHECK PASS "+text);}
  IEnumerator Shot(string name){for(int i=0;i<20;i++)yield return null;yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();Check(image.GetPixels32().Any(p=>p.r>45||p.g>45||p.b>45),"Rendered game content: "+name);File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);}
  InitializeInventory();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();humanCamera.Restore(0,false);PlayerRules.Settings.ItemCraftingTimeMultiplier=.1;WeatherControl.Set("clear");PlayerRules.SetLocalSolarHour(12,Math.Atan2(Position.Y,Position.X)*180/Math.PI);SetDaylight(LocalSolarHour,skyYear);
  var oldKeyboard=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>();TestKeyboard(keyboard);var background=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
  try
  {
   var paddleBranch=WoodCrafting.Branch(new ScatterInstance{Id=776,Kind=ScatterKind.Branch,Position=Position,WidthM=1.6,HeightM=.14});paddleBranch.NaturalId=0;paddleBranch.Id="native-paddle-limb";Inventory.Items.Add(paddleBranch);SetCraftWorkArea(paddleBranch);
   foreach(var kind in new[]{PossessionKind.StoneAxe,PossessionKind.StoneBlade}){var tool=PortableTechnology.Create(kind,Position);Inventory.Items.Add(tool);SetCraftWorkArea(tool);}
   RefreshInventoryNearby(true);Check(StartPortableCraft("hew-paddle",paddleBranch.Id),"Actual thick limb, axe and blade start paddle hewing");double until=Time.unscaledTimeAsDouble+70;while(Inventory.Crafts.Count>0&&Time.unscaledTimeAsDouble<until)yield return null;Check(paddleBranch.Kind==PossessionKind.Paddle,"Timed hewing produces the actual usable wooden paddle");
   foreach(var design in new[]{CampDesign.ReedRaft,CampDesign.SkinBoat})
   {
    var plan=CampBlueprints.Create(design,design==CampDesign.ReedRaft?1.5:2,design==CampDesign.ReedRaft?2.5:2,design==CampDesign.SkinBoat?CampCover.Hide:CampCover.Reed);
    bool site=false;double cx=0,cz=0;string reason="";
    for(int ring=1;ring<=50&&!site;ring++)for(int i=0;i<24&&!site;i++)
    {double a=i*Math.PI/12,x=ring*4*Math.Sin(a),z=ring*4*Math.Cos(a);var water=SampleMotionWater(x,z);if(!double.IsFinite(water.Surface))continue;var center=new Vector3((float)(x-originX),(float)water.Surface,(float)(z-originZ));if(!WatercraftSite(plan,center,0,out reason))continue;cx=x;cz=z;site=true;}
    Check(site,"Actual coastal terrain provides a complete shallow-water footprint for "+design+": "+reason);
    motion.Place(cx-2,cz,CampFloor(cx-2,cz));PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();motion.Flying=false;
    var c=new CampObject{Kind=CampKind.Blueprint,Position=frame.Position(cx,cz),Blueprint=plan,LastSimulatedSeconds=Camp.SimulatedSeconds};Camp.Objects.Add(c);
    // QA provides finite raw supplies, then uses the survival construction path.
    foreach(var part in CampBlueprints.Components(plan))
    {var item=BlueprintCreativeMaterial(plan,part);item.Id="native-raw-"+Guid.NewGuid().ToString("N");if(CampRules.Hide(item)){item.AppearanceSpecies="deer";item.AppearanceCoat=1;item.MaterialOrigins=new List<MaterialOrigin>{new MaterialOrigin{Species="deer",Coat=1,Seed=123}};}Inventory.Items.Add(item);SetCraftWorkArea(item);}
    PlayerRules.Mode=GameMode.Survival;ApplyPlayerMode();Vitals.Restore();double materialMass=Inventory.Items.Sum(p=>p.MassKg),before=c.MassKg;
    until=Time.unscaledTimeAsDouble+180;
    while(!CampBlueprints.Complete(plan)&&Time.unscaledTimeAsDouble<until)
    {if(blueprintJob==null){RefreshInventoryNearby(true);BuildBlueprintPart(c);}yield return null;}
    Check(CampBlueprints.Complete(plan)&&Math.Abs((materialMass-Inventory.Items.Sum(p=>p.MassKg))-(c.MassKg-before))<1e-6,"Every actual hull component and lashing consumes finite supplied goods in survival: "+design);
    Check(PrimitiveWatercraft.Seaworthy(c,WatercraftLoad),"Completed real hull supports the player and carried gear");
    campUpdateAt=0;UpdateCamp();Check(campVisuals.ContainsKey(c.Id),"Native completed watercraft mesh is resident");
    if(design==CampDesign.SkinBoat)Check(portableRoots.Any(p=>p.Value.AppearanceSpecies=="deer"&&p.Key.transform.IsChildOf(campVisuals[c.Id].Root.transform)),"Fitted hull panels render the specific donated animal hide");
    foreach(var p in Inventory.Items.Where(p=>p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand).ToArray())MovePossession(p.Id,PossessionPlace.Ground);Check(MovePossession(paddleBranch.Id,PossessionPlace.RightHand),"The real carved paddle can be held for propulsion");
    BoardWatercraft(c);Check(activeWatercraft==c&&!motion.Swimming,"Ordinary boarding puts the player on the actual floating hull");double waterY=SampleMotionWater(cx,cz).Surface;
    Check(Math.Abs(campVisuals[c.Id].Root.transform.localPosition.y-(waterY-PrimitiveWatercraft.Draft(c,WatercraftLoad)))<.15,"Hull visual follows the physical loaded waterline rather than the seabed");
    var target=campVisuals[c.Id].Root.transform.localPosition+Vector3.up*.25f;landscapeReviewCamera=target+new Vector3(3,2,-3);landscapeReviewRotation=Quaternion.LookRotation(target-landscapeReviewCamera.Value,Vector3.up);UpdateCamera();yield return Shot(design==CampDesign.ReedRaft?"reed-raft-afloat":"deer-hide-frame-boat");landscapeReviewCamera=null;
    // Use the actual gameplay keyboard path. A one-second turn chooses the
    // clearest heading available in this particular shoreline, then paddle.
    int bestHeading=-1;for(int h=0;h<24;h++){c.Heading=h*15;bool clear=true;for(int step=1;step<=8;step++){double a=c.Heading*Math.PI/180;if(!WatercraftClear(c,cx+Math.Sin(a)*step*.5,cz+Math.Cos(a)*step*.5)){clear=false;break;}}if(clear){bestHeading=h*15;break;}}
    Check(bestHeading>=0,"Shallow launch site connects to a real navigable water corridor");c.Heading=bestHeading;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));until=Time.unscaledTimeAsDouble+3;while(Time.unscaledTimeAsDouble<until)yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
    double travelled=(c.Position-frame.Position(cx,cz)).Length*frame.RadiusM;Check(travelled>1&&activeWatercraft==c&&!motion.Swimming,"Actual held-paddle W input moves the craft and carries the player over real water: "+travelled.ToString("0.00")+" m");
    var saved=new PlaySession();StorePlayer(saved);var restored=WorldJson.Read<PlaySession>(WorldJson.Write(saved));restored.Validate();Check(restored.WatercraftId==c.Id&&restored.Camp.Objects.Any(b=>b.Id==c.Id),"Boarded craft, motion and original hull goods survive full session capture/reload");
    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;Check(activeWatercraft==null&&PlayerRules.WatercraftId=="","Actual E input leaves the craft without removing its physical hull");
    PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();motion.Place(0,0,CampFloor(0,0));
   }
  }
  finally{landscapeReviewCamera=null;TestKeyboard(null);InputSystem.settings.backgroundBehavior=background;InputSystem.RemoveDevice(keyboard);oldKeyboard?.MakeCurrent();}
 }
}
