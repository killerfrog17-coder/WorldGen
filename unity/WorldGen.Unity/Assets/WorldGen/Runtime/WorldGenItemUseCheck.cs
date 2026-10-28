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
    public IEnumerator CheckItemUseFlow(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("ITEM USE "+text);report.Add("PASS "+text);Debug.Log("ITEM USE PASS "+text);}
        void ClearItems()
        {CancelItemCharge();ClearInventoryPhysics();foreach(var root in inventoryVisuals.Values.ToArray())DestroyInventoryVisual(root);inventoryVisuals.Clear();inventoryVisualPlaces.Clear();Inventory.Items.Clear();Inventory.Crafts.Clear();itemProjectiles.Clear();inventoryNearbyAt=0;}
        IEnumerator Image(string name)
        {
            yield return null;var old=view.targetTexture;var target=new RenderTexture(1280,800,24);target.Create();view.targetTexture=target;view.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            RenderTexture.active=previous;view.targetTexture=old;target.Release();Destroy(target);Destroy(image);
        }
        IEnumerator Deer()
        {
            ExecuteAnimalSpawnCommand("/spawn deer");double deadline=Time.unscaledTimeAsDouble+35;
            while(!animals.Values.Any(a=>a.Manual&&a.Spawn.Profile.slug=="deer"&&!a.Dead)&&Time.unscaledTimeAsDouble<deadline)yield return null;
            Check(animals.Values.Any(a=>a.Manual&&a.Spawn.Profile.slug=="deer"&&!a.Dead),"Original deer bundle loads as a live health-bearing actor");
        }
        void Aim(Animal animal,float distance)
        {
            animal.Fade=1;
            for(int attempt=0;attempt<16;attempt++)
            {
                float yaw=attempt*22.5f;animal.X=motion.X+Math.Sin(yaw*Math.PI/180)*distance;animal.Z=motion.Z+Math.Cos(yaw*Math.PI/180)*distance;animal.Planet=frame.Position(animal.X,animal.Z);animal.GroundY=VisibleGround(animal.X,animal.Z,Ground(animal.X,animal.Z));
                animal.Heading=animal.TargetHeading=yaw+90;animal.Speed=animal.TargetSpeed=0;animal.NextDecision=animal.Life+1000;
                animal.Clip=animal.Model.Asset.Clips.First(c=>c.role=="idle");animal.OldClip=null;animal.Time=0;animal.BlendAge=1;animal.CollisionFrame=-1;UpdateAnimal(animal,0);
                var surface=AnimalSurface(animal);var vertices=PosedAnimal(animal);Vector3 eye=transform.TransformPoint(new Vector3((float)(motion.X-originX),(float)motion.Y+humanEyeOffset,(float)(motion.Z-originZ)));
                for(int face=0;face<surface.Triangles.Length;face+=15)
                {
                    int a=surface.Triangles[face],b=surface.Triangles[face+1],c=surface.Triangles[face+2];if(!surface.Visible(a,animal.Parts))continue;Vector3 point=(vertices[a]+vertices[b]+vertices[c])/3,local=animal.Root.transform.InverseTransformPoint(point);
                    if(local.y<animal.Model.Asset.Height*.47f||local.y>animal.Model.Asset.Height*.8f||Mathf.Abs(local.z)>.3f)continue;
                    var ray=new Ray(eye,(point-eye).normalized);var obstruction=TraceCombatObstacle(ray,(point-eye).magnitude+.03f);var hit=TraceLiving(ray,obstruction?.Distance??100);
                    if(hit?.Animal!=animal)continue;var direction=ray.direction;heading=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;pitch=-Mathf.Asin(direction.y)*Mathf.Rad2Deg;UpdateCamera();return;
                }
                if(attempt==0)Debug.Log("ITEM AIM DIAGNOSTIC root="+animal.Root.transform.localPosition+" bounds="+animal.Renderer.bounds+" mesh="+animal.Model.Asset.Mesh.bounds+" eye="+eye+" parts="+animal.Parts);
            }
            throw new InvalidOperationException("No clear actual deer surface ray for controlled combat fixture");
        }
        Possession Hold(PossessionKind kind)
        {var p=PortableTechnology.Create(kind,Position);Inventory.Items.Add(p);Inventory.Hold(p.Id,out _);return p;}
        InitializeInventory();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();motion.Place(motion.X,motion.Z,Ground(motion.X,motion.Z));TestHumanView(0);
        yield return Deer();var deer=animals.Values.First(a=>a.Manual&&a.Spawn.Profile.slug=="deer"&&!a.Dead);Aim(deer,1.15f);deer.MaximumHealth=deer.Health=Math.Max(200,deer.MaximumHealth);paused=true;
        ExecuteGive(new[]{"/give","wooden","spear"});var spear=Inventory.QuickHeld;Check(spear?.Kind==PossessionKind.WoodenSpear,"A real held spear is selected for melee");yield return Image("01-before-hit");
        double health=deer.Health;StrikeWithItem(spear,false);Check(deer.Health<health&&deer.Health>0,"Spear melee damages the visible surface without an instant generic kill");
        Check(deer.ThreatUntil>deer.Life&&deer.TargetSpeed>0,"A surviving struck deer immediately attempts to flee");
        Check(deer.Blood!=null&&deer.Blood.Capture().Any(b=>b>0),"Impact paints a nonempty local UV stain without repainting the original coat");yield return Image("02-blood-hit");
        DamageAnimal(deer,10000,deer.Renderer.bounds.center,view.transform.forward,5);Check(deer.Dead&&deer.Ragdoll!=null&&deer.Ragdoll.BodyCount>0,"Lethal damage starts articulated ragdoll physics, no death animation");
        paused=false;yield return new WaitForSecondsRealtime(4);paused=true;
        Check(AnimalPalette(deer).All(m=>Enumerable.Range(0,16).All(i=>float.IsFinite(m[i]))),"Corpse palette remains finite after physics simulation");yield return Image("03-ragdoll");
        CaptureWildlife();var saved=WorldJson.Read<PlaySession>(WorldJson.Write(PlayerRules));saved.Validate();var dead=saved.Wildlife.Individuals.Single(p=>p.Id==deer.Spawn.Id);
        Check(dead.Dead&&dead.CorpsePose?.Length==deer.Model.Asset.BoneCount*16&&dead.BloodMask?.Length==65536,"Health, original identity, corpse pose and stain survive full save");
        var spawn=deer.Spawn;var model=deer.Model;ulong deadId=spawn.Id;RemoveAnimal(deadId);AddAnimal(spawn,model,true);deer=animals[deadId];Check(deer.Dead&&deer.Ragdoll!=null,"Streaming the same procedural identity back in does not revive it");
        RemoveAnimal(deadId);ClearItems();paused=false;
        yield return Deer();var stoneTarget=animals.Values.First(a=>a.Manual&&a.Spawn.Profile.slug=="deer"&&!a.Dead);Aim(stoneTarget,4);
        var rock=new Possession{Id="combat-test-rock",Kind=PossessionKind.SmallRock,Place=PossessionPlace.RightHand,Position=Position,WidthM=.045,HeightM=.04,DepthM=.04,MassKg=.1,CellsWide=1,CellsHigh=1,Variant=ToolStone.Encode(ToolStoneMaterial.Chert,0)};Inventory.Items.Add(rock);
        var aim=PlayerAttackRay();health=stoneTarget.Health;LaunchItemProjectile(rock,aim.origin+aim.direction*.4f,aim.direction,(float)ItemUseRules.ThrowSpeed(rock,1));
        Check(rock.InFlight&&inventoryDrops.ContainsKey(rock.Id),"Thrown stone is the same physical inventory item with live mass/velocity");
        double impactDeadline=Time.unscaledTimeAsDouble+4;while(rock.InFlight&&Time.unscaledTimeAsDouble<impactDeadline)yield return null;
        Check(stoneTarget.Health<health,"Swept thrown-stone flight actually damages an animal");Check(Inventory.Find(rock.Id)==rock,"Thrown stone remains recoverable, not consumed as a hit effect");
        paused=true;ClearItems();RemoveAnimal(stoneTarget.Spawn.Id);paused=false;
        yield return Deer();var arrowTarget=animals.Values.First(a=>a.Manual&&a.Spawn.Profile.slug=="deer"&&!a.Dead);Aim(arrowTarget,5);arrowTarget.MaximumHealth=arrowTarget.Health=Math.Max(200,arrowTarget.MaximumHealth);var bow=Hold(PossessionKind.Bow);var arrow=Hold(PossessionKind.Arrow);health=arrowTarget.Health;
        BeginItemCharge(bow,false,0,false);Check(itemCharge!=null&&itemCharge.Ammo==arrow,"Bow charge selects carried ammunition and borrows the off hand");itemCharge.Started-=1.2;ReleaseItemCharge();
        impactDeadline=Time.unscaledTimeAsDouble+4;while(arrow.InFlight&&Time.unscaledTimeAsDouble<impactDeadline)yield return null;
        Check(arrowTarget.Health<health,"Bow launches a damaging physical arrow");Check(arrow.AttachedAnimal==arrowTarget.Spawn.Id&&!arrow.InFlight,"Penetrating arrow embeds into the actual hit animal");
        yield return new WaitForSecondsRealtime(.4f);UpdateAttachedProjectiles();Check(arrow.Position.Length>.99999&&arrow.AttachedAnimal==arrowTarget.Spawn.Id,"Embedded arrow follows the current skin-bone pose");paused=true;yield return Image("04-embedded-arrow");
        TerrainHandoff.Coordinates(frame,arrow.Position,out double ax,out double az);motion.Place(ax-.4,az,Ground(ax-.4,az));RefreshInventoryNearby(true);
        Check(MovePossession(arrow.Id,PossessionPlace.LeftHand)&&arrow.AttachedAnimal==0,"Embedded arrow can be physically retrieved within reach");Inventory.Validate();
        ClearItems();PlayerRules.Mode=GameMode.Survival;Vitals.Restore();ApplyPlayerMode();ExecuteGive(new[]{"/give","wooden","spear"});combatGuarding=true;
        double before=Vitals.Health,stamina=Vitals.Stamina;DamageHuman(humanPlayer,20,null,-view.transform.forward);Check(Vitals.Health>before-20&&Vitals.Stamina<stamina,"Facing weapon guard reduces incoming damage at a stamina cost");
        combatGuarding=false;DamageHuman(humanPlayer,1000,null,view.transform.forward);UpdatePlayerDeath();Check(DeathVisible&&Vitals.DeathCause==PlayerDeathCause.Combat&&playerRagdoll!=null,"Combat can kill the player through the existing ragdoll/death screen");yield return Image("05-player-death");
        File.WriteAllLines(Path.Combine(output,"checks.txt"),report);
    }
}
public sealed partial class WorldGenRuntime
{
    IEnumerator CheckItemUseRuntime()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);AudioListener.volume=0;
        var report=new List<string>();var errors=new List<string>();Application.LogCallback listener=(m,t,k)=>{if(k==LogType.Error||k==LogType.Exception)errors.Add(m);};Application.logMessageReceived+=listener;
        double deadline=Time.realtimeSinceStartupAsDouble+180;while((Busy||!worldReady||humans==null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(!worldReady||Busy||humans==null)throw new InvalidOperationException("Item-use fixture unavailable");TerrainVideoOptions.Current.ViewDistanceM=1000;TerrainVideoOptions.Revision++;ExploreTerrain(TerrainMapViewport.FromDegrees(50,10));deadline=Time.realtimeSinceStartupAsDouble+120;
        while((terrainExplorer==null||!terrainExplorer.Ready||!terrainExplorer.CenterReady)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(terrainExplorer==null||!terrainExplorer.Ready)throw new InvalidOperationException("Item-use terrain unavailable");
        yield return terrainExplorer.CheckItemUseFlow(output,report);CapturePlayer();play.Validate();ReturnFromPlay("Simulation");yield return null;yield return null;
        Application.logMessageReceived-=listener;if(errors.Count>0)throw new InvalidOperationException(string.Join(" | ",errors));report.Add("PASS no runtime errors or exceptions");File.WriteAllLines(Path.Combine(output,"checks.txt"),report);Debug.Log("ITEM USE FLOW PASS "+report.Count);Application.Quit(0);
    }
}
