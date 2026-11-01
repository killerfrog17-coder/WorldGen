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
    public IEnumerator CheckBlueprintFlow(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("BLUEPRINT "+text);report.Add("PASS "+text);Debug.Log("BLUEPRINT PASS "+text);}
        var oldKeyboard=Keyboard.current;var oldMouse=Mouse.current;var background=InputSystem.settings.backgroundBehavior;
        var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();TestKeyboard(keyboard);keyboard.MakeCurrent();mouse.MakeCurrent();InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        IEnumerator KeyPress(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;}
        IEnumerator Click(bool right=false){InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(right?MouseButton.Right:MouseButton.Left));yield return null;yield return null;InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;}
        void Aim(Vector3 target)
        {UpdateHumans();UpdateCamera();var delta=(target-view.transform.localPosition).normalized;heading=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;pitch=-Mathf.Asin(delta.y)*Mathf.Rad2Deg;UpdateCamera();}
        Vector3 Floor(double x,double z)=>new Vector3((float)(x-originX),(float)CampFloor(x,z),(float)(z-originZ));
        var made=new List<CampObject>();
        IEnumerator Picture(CampObject c,string name)
        {
            campUpdateAt=0;UpdateCamp();yield return null;var origin=BlueprintOrigin(c);var prior=view.targetTexture;var active=RenderTexture.active;var target=new RenderTexture(1280,800,24);target.Create();view.targetTexture=target;
            float size=(float)Math.Max(c.Blueprint.WidthM,c.Blueprint.DepthM);view.transform.localPosition=origin+new Vector3(size*1.2f,2.2f,(c.Blueprint.Design==CampDesign.BrushHut?1:-1)*size*1.8f);view.transform.LookAt(transform.TransformPoint(origin+Vector3.up*.8f));view.Render();RenderTexture.active=target;
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);RenderTexture.active=active;view.targetTexture=prior;target.Release();Destroy(target);UpdateCamera();
        }
        try
        {
            paused=false;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();motion.Flying=false;campMenu=null;SetInventoryOpen(false);TestHumanView(0);
            TerrainMapViewport.Degrees(Position,out _,out double longitude);PlayerRules.SetLocalSolarHour(12,longitude);nextDaylightAt=0;SetDaylight(LocalSolarHour,skyYear);
            yield return KeyPress(Key.V);Check(campPlansOpen&&!paused,"V opens camp plans through actual keyboard input");yield return KeyPress(Key.Escape);Check(!campPlansOpen&&!paused,"Escape closes plans without pausing");
            double startX=motion.X,startZ=motion.Z;
            foreach(CampDesign design in Enum.GetValues(typeof(CampDesign)))
            {
                var plan=CampBlueprints.Create(design,2,2);Vector3 site=default;bool found=false;double bestGrade=double.MaxValue;
                for(int ring=0;ring<60&&bestGrade>.3;ring++)for(int step=0;step<24&&bestGrade>.3;step++)
                {
                    double a=step*Math.PI/12,x=startX+Math.Sin(a)*(ring+1)*3,z=startZ+Math.Cos(a)*(ring+1)*3;var at=frame.Position(x,z);if(made.Any(other=>(other.Position-at).Length*frame.RadiusM<8))continue;
                    var point=Floor(x,z);if(BlueprintSite(plan,point,0,out _)){double grade=plan.FloorHeights.Max()-plan.FloorHeights.Min();if(grade<bestGrade){site=point;found=true;bestGrade=grade;}}
                }
                Check(found,"A clear dry footprint can be found for "+design);
                double cx=site.x+originX,cz=site.z+originZ;motion.Place(cx-2,cz-2,Ground(cx-2,cz-2));heading=0;pitch=45;UpdateHumans();UpdateCamera();
                BeginBlueprint(design);blueprintYaw=0;bool round=design==CampDesign.BrushHut;bool line=design==CampDesign.Windbreak||design==CampDesign.DryingRack;
                var anchor=site-(round?Vector3.zero:new Vector3(1,0,line?0:1));anchor.y=(float)CampFloor(anchor.x+originX,anchor.z+originZ);Aim(anchor);UpdateBlueprintPlacement();Check(blueprintValid,"First anchor preview is valid for "+design+": "+blueprintReason);
                yield return Click();Check(blueprintAnchored,"First mouse click anchors "+design);
                if(design==CampDesign.LeanTo)
                {
                    float before=blueprintYaw;yield return KeyPress(Key.R);Check(Mathf.Abs(blueprintYaw-before-15)<.01,"R rotates placement without stowing an item");blueprintYaw=0;yield return Click(true);Check(!blueprintAnchored&&blueprintPlacement!=null&&!paused,"Right mouse returns from sizing to the first anchor");Aim(anchor);yield return Click();
                }
                var end=round?anchor+Vector3.right:anchor+new Vector3(2,0,line?0:2);end.y=(float)CampFloor(end.x+originX,end.z+originZ);Aim(end);UpdateBlueprintPlacement();
                Check(blueprintValid&&Math.Abs(blueprintPlacement.WidthM-2)<.01,"Drag selects a bounded2m footprint for "+design+": "+blueprintReason);
                yield return Click();var c=Camp.Objects.Last();made.Add(c);Check(c.Kind==CampKind.Blueprint&&c.Blueprint.Design==design&&blueprintPlacement==null,"Second mouse click saves the "+design+" blueprint");
                Check(campVisuals[c.Id].Root.GetComponentsInChildren<Renderer>().Length>0&&!CampBlueprints.Complete(c.Blueprint),"Unbuilt "+design+" displays its real component ghost");
                if(design==CampDesign.LeanTo)
                {
                    PlayerRules.Mode=GameMode.Survival;ApplyPlayerMode();Vitals.Restore();PlayerRules.Settings.ItemCraftingTimeMultiplier=1;
                    var spear=new Possession{Id="blueprint-held-spear",Kind=PossessionKind.WoodenSpear,Position=Position,HeightM=1.8,WidthM=.03,DepthM=.03,MassKg=.8,WoodSoundness=.9,WoodStraightness=.9};Inventory.Items.Add(spear);Inventory.Hold(spear.Id,out _);
                    var next=CampBlueprints.Components(c.Blueprint).First();var supply=BlueprintCreativeMaterial(c.Blueprint,next);Inventory.Items.Add(supply);inventoryNearbyAt=0;BuildBlueprintPart(c);
                    Check(blueprintJob!=null&&spear.Place!=PossessionPlace.RightHand,"Construction borrows occupied hands with no bag requirement");yield return new WaitForSecondsRealtime(.3f);CancelBlueprintWork();
                    Check(CampBlueprints.State(c.Blueprint,next).WorkSeconds>0&&!CampBlueprints.Done(c.Blueprint,next)&&Inventory.AtHand(PossessionPlace.RightHand)==spear,"Interrupted fitting retains partial labour and restores the held spear");
                    var saved=WorldJson.Read<PlaySession>(WorldJson.Write(PlayerRules));saved.Validate();Check(saved.Camp.Objects.Single(o=>o.Id==c.Id).Blueprint.Parts.Single().WorkSeconds>0,"An actual player save retains an unfinished blueprint and labour");
                    PlayerRules.Settings.ItemCraftingTimeMultiplier=.001;
                    int guard=0;
                    while(!CampBlueprints.Complete(c.Blueprint)&&guard++<300)
                    {
                        var component=CampBlueprints.Components(c.Blueprint).First(d=>!CampBlueprints.Done(c.Blueprint,d));
                        if(!CampBlueprints.HasMaterial(c.Blueprint,component)){Inventory.Items.Add(BlueprintCreativeMaterial(c.Blueprint,component));inventoryNearbyAt=0;}
                        BuildBlueprintPart(c);double until=Time.unscaledTimeAsDouble+3;while(blueprintJob!=null&&Time.unscaledTimeAsDouble<until)yield return null;
                    }
                    Check(CampBlueprints.Complete(c.Blueprint),"Survival construction completes using real materials and timed work");
                    Check(Inventory.AtHand(PossessionPlace.RightHand)==spear,"Repeated component fitting consistently restores prior hands");
                    PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();PlayerRules.Settings.ItemCraftingTimeMultiplier=1;
                }
                else
                {
                    int guard=0;while(!CampBlueprints.Complete(c.Blueprint)&&guard++<300)BuildBlueprintPart(c);
                    Check(CampBlueprints.Complete(c.Blueprint),"Creative construction fits all "+design+" components");
                }
                campUpdateAt=0;UpdateCamp();Physics.SyncTransforms();Check(campVisuals[c.Id].Root.GetComponent<MeshCollider>()?.sharedMesh.vertexCount>0,"Completed "+design+" has a solid fitted mesh collider");
                yield return Picture(c,"complete-"+design);
                if(design==CampDesign.BrushHut)
                {
                    var origin=BlueprintOrigin(c);float radius=(float)c.Blueprint.WidthM*.5f;double ax=origin.x+originX,az=origin.z+originZ+radius+.6;
                    motion.Place(ax,az,Ground(ax,az));heading=180;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftCtrl));yield return null;PreparePlayerSolids();
                    Check(playerSolid.height<1.4f,"Crouching reduces the actual movement collider for small shelter entrances");
                    for(int step=0;step<150;step++){PreparePlayerSolids();motion.Move(1d/60,1,0,0,180,0,false,false,PlayerSolidGround,walk:true,speedScale:.55);}
                    File.WriteAllText(Path.Combine(output,"hut-entry.txt"),"origin="+origin+" feet="+PlayerFeet()+" colliderHeight="+playerSolid.height+" footing="+string.Join(",",c.Blueprint.FloorHeights)+"\nContacts: "+string.Join(",",Physics.OverlapCapsule(transform.TransformPoint(PlayerFeet()+Vector3.up*.26f),transform.TransformPoint(PlayerFeet()+Vector3.up*1.08f),.24f,1<<ScatterSolidLayer).Select(hit=>hit.name)));
                    Check((PlayerFeet()-origin).sqrMagnitude<.8f*.8f,"The actual character motor can enter the small hut through its doorway");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                    var slab=new GameObject("Controlled low headroom",typeof(BoxCollider));slab.layer=ScatterSolidLayer;slab.transform.SetParent(transform,false);slab.transform.localPosition=PlayerFeet()+Vector3.up*1.6f;slab.GetComponent<BoxCollider>().size=new Vector3(1,.08f,1);Physics.SyncTransforms();PreparePlayerSolids();
                    Check(playerForcedCrouch&&PlayerCrouching&&playerSolid.height<1.4f,"Low headroom prevents standing into the roof after releasing crouch");DestroyImmediate(slab);
                    motion.Place(ax,az+2,Ground(ax,az+2));PreparePlayerSolids();Check(!playerForcedCrouch&&playerSolid.height>1.7f,"Standing height returns when the player leaves low headroom");
                }
            }
            var shelter=made.First(c=>c.Blueprint.Design==CampDesign.LeanTo);Check(BlueprintProtectionAt(shelter.Position).Rain>.7,"The completed shelter contributes actual local weather protection");
            var store=made.First(c=>c.Blueprint.Design==CampDesign.MaterialStore);TerrainHandoff.Coordinates(frame,store.Position,out double sx,out double sz);motion.Place(sx,sz-1.5,Ground(sx,sz-1.5));UpdateHumans();
            var branch=new Possession{Id="stored-branch",Kind=PossessionKind.Branch,Position=Position,HeightM=1.5,WidthM=.03,DepthM=.03,MassKg=.7,WoodSoundness=.8,WoodStraightness=.8};Inventory.Items.Add(branch);StoreBlueprintItem(store,branch);inventoryNearbyAt=0;RefreshInventoryNearby(true);
            Check(branch.Place==PossessionPlace.CampStorage&&!Inventory.Carried(branch)&&CraftAccessible().Any(p=>p.Id==branch.Id),"Stored material is nearby-craft accessible without adding player carry load");
            Check(MovePossession(branch.Id,PossessionPlace.Ground)&&branch.Place==PossessionPlace.Ground,"Stored material can be removed through the normal inventory transfer path");
            var rack=made.First(c=>c.Blueprint.Design==CampDesign.DryingRack);TerrainHandoff.Coordinates(frame,rack.Position,out double rx,out double rz);motion.Place(rx,rz-1.5,Ground(rx,rz-1.5));UpdateHumans();var food=PortableTechnology.Create(PossessionKind.RawMeat,Position,"deer",0);Inventory.Items.Add(food);StoreBlueprintItem(rack,food);
            Check(food.Place==PossessionPlace.CampStorage,"Rack hangs an actual meat item with its animal provenance");
            double calories=food.MassKg*food.FoodKcalPerKg;CampBlueprints.Dry(food,1000000,0,.4,3,20,true);rack.Blueprint.Revision++;campUpdateAt=0;UpdateCamp();Check(food.Dried&&food.Smoked&&Math.Abs(calories-food.MassKg*food.FoodKcalPerKg)<1e-7,"Drying yields retrievable lighter food with unchanged total calories");
            var roundtrip=WorldJson.Read<PlaySession>(WorldJson.Write(PlayerRules));roundtrip.Validate();Check(roundtrip.Camp.Objects.Count(c=>c.Kind==CampKind.Blueprint)==5&&roundtrip.Inventory.Find(food.Id).CampOwner==rack.Id,"Finished plans, stock and preserved food round-trip a player save");
            int fitted=store.Blueprint.Parts.Count;UndoBlueprintPart(store);Check(store.Blueprint.Parts.Count==fitted-1&&!CampBlueprints.Complete(store.Blueprint),"Dismantling removes a component and exposes the remaining plan");
            RemoveCamp(rack);PlayerRules.Validate();Check(food.Place==PossessionPlace.Ground&&food.CampOwner==null&&!Camp.Objects.Contains(rack),"Dismantling a rack safely releases its food and all materials");
            Check(Camp.Objects.All(c=>c.Kind!=CampKind.FrameWood),"Blueprint construction does not create a per-stick structural dependency graph");
        }
        finally
        {
            CancelBlueprintWork();CancelBlueprintPlacement();campPlansOpen=false;campMenu=null;PlayerRules.Mode=GameMode.Creative;PlayerRules.Settings.ItemCraftingTimeMultiplier=1;
            InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);TestKeyboard(null);InputSystem.settings.backgroundBehavior=background;if(oldMouse!=null)oldMouse.MakeCurrent();if(oldKeyboard!=null)oldKeyboard.MakeCurrent();
        }
    }
}
