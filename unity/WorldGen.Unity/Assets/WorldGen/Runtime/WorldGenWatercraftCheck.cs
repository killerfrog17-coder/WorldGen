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
   bool dryStart=false;for(int ring=0;ring<40&&!dryStart;ring++)for(int i=0;i<16&&!dryStart;i++){double a=i*Math.PI/8,x=ring*4*Math.Sin(a),z=ring*4*Math.Cos(a);if(double.IsFinite(SampleMotionWater(x,z).Surface))continue;motion.Place(x,z,CampFloor(x,z));dryStart=true;}Check(dryStart,"Actual dry bank is available for hewing the heavy limb");SetInventoryOpen(true);
   SetInventoryOpen(false);var axe=PortableTechnology.Create(PossessionKind.StoneAxe,Position);Inventory.Items.Add(axe);SetCraftWorkArea(axe);
   double treeUntil=Time.unscaledTimeAsDouble+60;while(!scatterChunks.Values.Any(c=>c.Roots!=null&&c.Source.Instances.Any(t=>t.Kind==ScatterKind.GeneratedTree&&t.HeightM>=8&&(t.Position-Position).Length*frame.RadiusM<150))&&Time.unscaledTimeAsDouble<treeUntil)yield return null;
   var trees=scatterChunks.Values.Where(c=>c.Roots!=null).SelectMany(c=>c.Source.Instances).Where(t=>t.Kind==ScatterKind.GeneratedTree&&t.HeightM>=8&&(t.Position-Position).Length*frame.RadiusM<150).OrderBy(t=>(t.Position-Position).Length).Take(30).ToArray();bool limbReady=false;Debug.Log("HEAVY LIMB candidate trees="+trees.Length);
   foreach(var tree in trees)
   {TerrainHandoff.Coordinates(frame,tree.Position,out double tx,out double tz);for(int side=0;side<8&&!limbReady;side++)
    {double a=side*Math.PI/4;motion.Place(tx+Math.Sin(a)*2.4,tz+Math.Cos(a)*2.4,CampFloor(tx+Math.Sin(a)*2.4,tz+Math.Cos(a)*2.4));PreparePlayerSolids();yield return null;var target=new Vector3((float)(tx-originX),(float)CampFloor(tx,tz)+1,(float)(tz-originZ));for(int aim=0;aim<8;aim++){UpdateHumans();UpdateCamera();var dir=(target-view.transform.localPosition).normalized;heading=Mathf.Atan2(dir.x,dir.z)*Mathf.Rad2Deg;pitch=-Mathf.Asin(dir.y)*Mathf.Rad2Deg;UpdateCamera();}limbReady=HeavyLimbReason().Length==0;if(!limbReady)Debug.Log("HEAVY LIMB site "+tree.Id+" height="+tree.HeightM+" reason="+HeavyLimbReason());}if(limbReady)break;}
   Check(limbReady,"An actual generated tree offers a reachable thick limb");SetCraftWorkArea(axe);CutHeavyLimb(axe);double gatherUntil=Time.unscaledTimeAsDouble+25;while(worldToolWork!=null&&Time.unscaledTimeAsDouble<gatherUntil)yield return null;
   var paddleBranch=Inventory.Items.LastOrDefault(p=>p.Kind==PossessionKind.Branch&&p.Id.StartsWith("heavy-limb-",StringComparison.Ordinal));Check(paddleBranch!=null&&paddleBranch.Hands==2&&paddleBranch.WidthM>=.12,"Actual timed axe work gathers a finite thick limb from that generated tree");
   var blade=PortableTechnology.Create(PossessionKind.StoneBlade,Position);Inventory.Items.Add(blade);SetCraftWorkArea(blade);SetCraftWorkArea(paddleBranch);SetCraftWorkArea(axe);SetInventoryOpen(true);
   RefreshInventoryNearby(true);Check(StartPortableCraft("hew-paddle",paddleBranch.Id),"Actual thick limb, axe and blade start paddle hewing");double until=Time.unscaledTimeAsDouble+70;while(Inventory.Crafts.Count>0&&Time.unscaledTimeAsDouble<until)yield return null;Check(paddleBranch.Kind==PossessionKind.Paddle,"Timed hewing produces the actual usable wooden paddle; kind="+paddleBranch.Kind+" jobs="+string.Join(";",Inventory.Crafts.Select(j=>j.WorkSeconds+"/"+j.RequiredSeconds+" running="+j.Running)));Check(MovePossession(paddleBranch.Id,PossessionPlace.RightHand),"The completed paddle is physically picked up before moving to the launch site");SetInventoryOpen(false);
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
    if(design==CampDesign.SkinBoat)
    {double coatUntil=Time.unscaledTimeAsDouble+25;while(!animalModels.ContainsKey("deer")&&Time.unscaledTimeAsDouble<coatUntil)yield return null;UpdatePortableAppearances();
     var hideRoot=portableRoots.FirstOrDefault(p=>p.Value.AppearanceSpecies=="deer"&&p.Key!=null&&p.Key.transform.IsChildOf(campVisuals[c.Id].Root.transform)).Key;var properties=new MaterialPropertyBlock();if(hideRoot!=null)hideRoot.GetComponent<Renderer>().GetPropertyBlock(properties);
     Check(hideRoot!=null&&animalModels.ContainsKey("deer")&&properties.GetTexture("_SourceCoats")==animalModels["deer"].Asset.Coats,"Fitted hull panels bind the actual donated deer coat texture");}
    foreach(var p in Inventory.Items.Where(p=>p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand).ToArray())MovePossession(p.Id,PossessionPlace.Ground);Check(MovePossession(paddleBranch.Id,PossessionPlace.RightHand),"The real carved paddle can be held for propulsion");
    BoardWatercraft(c);Check(activeWatercraft==c&&!motion.Swimming,"Ordinary boarding puts the player on the actual floating hull");double waterY=SampleMotionWater(cx,cz).Surface;
    Check(Math.Abs(campVisuals[c.Id].Root.transform.localPosition.y-(waterY-PrimitiveWatercraft.Draft(c,WatercraftLoad)))<.15,"Hull visual follows the physical loaded waterline rather than the seabed");
    var target=campVisuals[c.Id].Root.transform.localPosition+Vector3.up*.25f;landscapeReviewCamera=target+new Vector3(3,2,-3);landscapeReviewRotation=Quaternion.LookRotation(target-landscapeReviewCamera.Value,Vector3.up);humanReviewBodyMask=0;chatLines.Clear();UpdateCamera();yield return Shot(design==CampDesign.ReedRaft?"reed-raft-afloat":"deer-hide-frame-boat");humanReviewBodyMask=-1;landscapeReviewCamera=null;
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
  finally{humanReviewBodyMask=-1;landscapeReviewCamera=null;TestKeyboard(null);InputSystem.settings.backgroundBehavior=background;InputSystem.RemoveDevice(keyboard);oldKeyboard?.MakeCurrent();}
 }
}
