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
        Check(WildlifeAbundance.EffectiveDayMinutes(1)==15&&WildlifeAbundance.EffectiveDayMinutes(2880)==1440,"Wildlife encounter pacing clamps to15minutes..24hours independently of the actual clock");
        Check(WildlifeAbundance.TravelPace(1,50,650)==1&&WildlifeAbundance.TravelPace(15,2500,650)>1,"Nearby animals retain normal travel while offscreen groups receive bounded encounter pacing");
        void ClearItems()
        {CancelItemCharge();ClearInventoryPhysics();foreach(var root in inventoryVisuals.Values.ToArray())DestroyInventoryVisual(root);inventoryVisuals.Clear();inventoryVisualPlaces.Clear();Inventory.Items.Clear();Inventory.Crafts.Clear();itemProjectiles.Clear();inventoryNearbyAt=0;}
        IEnumerator Image(string name,Animal focus=null)
        {
            yield return null;var hidden=new List<GameObject>();if(focus!=null){var box=focus.Renderer.bounds;view.transform.position=box.center+new Vector3(2,1.7f,-2).normalized*Mathf.Clamp((float)focus.Spawn.Profile.lengthM*1.5f,.5f,4);view.transform.LookAt(box.center);foreach(var other in animals.Values)if(other!=focus&&other.Root.activeSelf){other.Root.SetActive(false);hidden.Add(other.Root);}foreach(var item in Inventory.Items.Where(p=>p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand))if(inventoryVisuals.TryGetValue(item.Id,out var held)&&held.activeSelf){held.SetActive(false);hidden.Add(held);}}var old=view.targetTexture;var previous=RenderTexture.active;var target=new RenderTexture(1280,800,24);target.Create();view.targetTexture=target;view.Render();RenderTexture.active=target;
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            RenderTexture.active=previous;view.targetTexture=old;target.Release();Destroy(target);Destroy(image);foreach(var held in hidden)held.SetActive(true);
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
        InitializeInventory();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();motion.Place(motion.X,motion.Z,Ground(motion.X,motion.Z));TestHumanView(0);TerrainMapViewport.Degrees(Position,out _,out double longitude);PlayerRules.SetLocalSolarHour(12,longitude);nextDaylightAt=0;SetDaylight(LocalSolarHour,skyYear);
        var effects=Resources.LoadAll<AudioClip>("Items/Audio");Check(effects.Length==18&&effects.All(c=>c.channels==1&&c.length>.2f&&c.length<1.3f),"All eighteen local AI Foley transients load as short spatial mono clips");
        ClearItems();
        Check(ExecuteGive(new[]{"/give","chert"}).Success&&ExecuteGive(new[]{"/give","quartzite"}).Success,"Knapping fixture supplies two ordinary compatible stones");
        var stoneCore=Inventory.Items.First(p=>StoneCrafting.Rock(p)&&StoneCrafting.FractureQuality(p)>0);
        Check(StartInventoryKnapping(stoneCore.Id),"Real inventory knapping starts with occupied hands");
        var stoneJob=ActiveItemCraft;stoneJob.WorkSeconds=stoneJob.RequiredSeconds-.000001;UpdateStoneCrafting();
        var newFlake=Inventory.Items.FirstOrDefault(p=>p.Kind==PossessionKind.StoneFlake);
        Check(newFlake!=null&&inventoryDrops.TryGetValue(newFlake.Id,out var flakeDrop)&&flakeDrop.Body!=null&&flakeDrop.Collider.bounds.size.sqrMagnitude>0,"Freshly chipped flake receives physical drop collision in the same frame as creation");
        Check(inventoryVisuals[newFlake.Id].GetComponentsInChildren<MeshFilter>().All(m=>m.sharedMesh!=null),"Fresh flake visual has no deferred empty mesh children");
        stoneCore.Place=PossessionPlace.Ground;ReleasePossession(stoneCore,true);
        Check(inventoryDrops.ContainsKey(stoneCore.Id)&&inventoryVisuals[stoneCore.Id].GetComponentsInChildren<MeshFilter>().All(m=>m.sharedMesh!=null),"Worked source stone also drops immediately with valid surface geometry");
        ClearItems();
        yield return Deer();var deer=animals.Values.First(a=>a.Manual&&a.Spawn.Profile.slug=="deer"&&!a.Dead);yield return CheckWildlifeRadar(deer,report);Aim(deer,1.15f);deer.MaximumHealth=deer.Health=Math.Max(200,deer.MaximumHealth);paused=true;
        ExecuteGive(new[]{"/give","wooden","spear"});var spear=Inventory.QuickHeld;Check(spear?.Kind==PossessionKind.WoodenSpear,"A real held spear is selected for melee");yield return Image("01-before-hit");
        double health=deer.Health;StrikeWithItem(spear,false);Check(deer.Health<health&&deer.Health>0,"Spear melee damages the visible surface without an instant generic kill");
        Check(deer.ThreatUntil>deer.Life&&deer.TargetSpeed>0,"A surviving struck deer immediately attempts to flee");
        Check(deer.Blood!=null&&deer.Blood.Capture().Any(b=>b>0),"Impact paints a nonempty local UV stain without repainting the original coat");yield return Image("02-blood-hit");
        var beforeCorpse=(Vector3[])PosedAnimal(deer).Clone();
        DamageAnimal(deer,10000,deer.Renderer.bounds.center,view.transform.forward,5);Check(deer.Dead&&deer.Ragdoll!=null&&deer.Ragdoll.BodyCount>0,"Lethal damage starts articulated ragdoll physics, no death animation");
        paused=false;yield return new WaitForSecondsRealtime(4);paused=true;
        Check(AnimalPalette(deer).All(m=>Enumerable.Range(0,16).All(i=>float.IsFinite(m[i]))),"Corpse palette remains finite after physics simulation");
        yield return Image("03-ragdoll",deer);UpdateCamera();
        var afterCorpse=PosedAnimal(deer);var corpseSkin=AnimalSurface(deer);var stretch=new List<float>();
        for(int face=0;face<corpseSkin.Triangles.Length;face+=3)for(int edge=0;edge<3;edge++)
        {int a=corpseSkin.Triangles[face+edge],b=corpseSkin.Triangles[face+(edge+1)%3];if(!corpseSkin.Visible(a,deer.Parts))continue;float length=Vector3.Distance(beforeCorpse[a],beforeCorpse[b]);if(length>.001f)stretch.Add(Vector3.Distance(afterCorpse[a],afterCorpse[b])/length);}
        stretch.Sort();float p99=stretch[(int)((stretch.Count-1)*.99f)];File.WriteAllText(Path.Combine(output,"corpse-skin-stretch.txt"),"Edges "+stretch.Count+"; p99 stretch "+p99+"; maximum "+stretch.Last());
        using(var outputVertices=new BinaryWriter(File.Create(Path.Combine(output,"corpse-skin-vertices.bin"))))foreach(var points in new[]{beforeCorpse,afterCorpse})foreach(var p in points){outputVertices.Write(p.x);outputVertices.Write(p.y);outputVertices.Write(p.z);}
        Check(p99<2.5f,"Articulated corpse avoids widespread extreme skin stretching (99th percentile "+p99.ToString("F2")+")");
        CaptureWildlife();var saved=WorldJson.Read<PlaySession>(WorldJson.Write(PlayerRules));saved.Validate();var dead=saved.Wildlife.Individuals.Single(p=>p.Id==deer.Spawn.Id);
        Check(dead.Dead&&dead.CorpsePose?.Length==deer.Model.Asset.BoneCount*16&&dead.BloodMask?.Length==65536,"Health, original identity, corpse pose and stain survive full save");
        int retainedRecords=Wildlife.Individuals.Count;Wildlife.PopulationVersion=0;EnsureWildlifePopulationVersion();
        Check(Wildlife.PopulationVersion==WildlifeAbundance.Version&&Wildlife.Groups.Count==0&&Wildlife.Individuals.Count==retainedRecords&&Wildlife.Individuals.Any(p=>p.Id==deer.Spawn.Id&&p.Dead),"Upgrading population proposals preserves the actual saved corpse and clears obsolete procedural groups");
        var spawn=deer.Spawn;var model=deer.Model;ulong deadId=spawn.Id;RemoveAnimal(deadId);
        var restoredCandidates=new List<FaunaPrototypeSpawn>();MergeSavedWildlifeCandidates(restoredCandidates);var restoredBody=restoredCandidates.Single(p=>p.Id==deadId);
        Check((restoredBody.Position-dead.Position).Length*frame.RadiusM<.001,"Saved corpse is admitted at its current location without its original planner candidate");
        AddAnimal(restoredBody,model);deer=animals[deadId];Check(deer.Dead&&deer.Ragdoll!=null&&deer.Manual,"Streaming the same saved identity restores its death and manual ownership");
        RemoveAnimal(deadId);ClearItems();paused=false;
        yield return Deer();var stoneTarget=animals.Values.First(a=>a.Manual&&a.Spawn.Profile.slug=="deer"&&!a.Dead);Aim(stoneTarget,4);
        var rock=new Possession{Id="combat-test-rock",Kind=PossessionKind.SmallRock,Place=PossessionPlace.RightHand,Position=Position,WidthM=.045,HeightM=.04,DepthM=.04,MassKg=.1,CellsWide=1,CellsHigh=1,Variant=ToolStone.Encode(ToolStoneMaterial.Chert,0)};Inventory.Items.Add(rock);
        var aim=PlayerAttackRay();health=stoneTarget.Health;LaunchItemProjectile(rock,aim.origin+aim.direction*.4f,aim.direction,(float)ItemUseRules.ThrowSpeed(rock,1));
        Check(rock.InFlight&&inventoryDrops.ContainsKey(rock.Id),"Thrown stone is the same physical inventory item with live mass/velocity");
        double impactDeadline=Time.unscaledTimeAsDouble+4;while(rock.InFlight&&Time.unscaledTimeAsDouble<impactDeadline)yield return null;
        Check(stoneTarget.Health<health,"Swept thrown-stone flight actually damages an animal");Check(Inventory.Find(rock.Id)==rock,"Thrown stone remains recoverable, not consumed as a hit effect");
        paused=true;ClearItems();RemoveAnimal(stoneTarget.Spawn.Id);paused=false;
        yield return Deer();var arrowTarget=animals.Values.First(a=>a.Manual&&a.Spawn.Profile.slug=="deer"&&!a.Dead);Aim(arrowTarget,5);arrowTarget.MaximumHealth=arrowTarget.Health=Math.Max(200,arrowTarget.MaximumHealth);var bow=Hold(PossessionKind.Bow);var arrow=Hold(PossessionKind.Arrow);health=arrowTarget.Health;
        BeginItemCharge(bow,false,0,false);Check(itemCharge!=null&&itemCharge.Ammo==arrow,"Bow charge selects carried ammunition and borrows the off hand");
        var chargedSave=new PlaySession();StorePlayer(chargedSave);
        Check(itemCharge==null&&Inventory.AtHand(PossessionPlace.RightHand)==bow&&Inventory.AtHand(PossessionPlace.LeftHand)==arrow&&chargedSave.Inventory.AtHand(PossessionPlace.LeftHand)?.Id==arrow.Id,"Saving a drawn bow restores borrowed hands and preserves unfired ammunition");
        BeginItemCharge(bow,false,0,false);itemCharge.Started-=1.2;ReleaseItemCharge();
        impactDeadline=Time.unscaledTimeAsDouble+4;while(arrow.InFlight&&Time.unscaledTimeAsDouble<impactDeadline)yield return null;
        Check(arrowTarget.Health<health,"Bow launches a damaging physical arrow");Check(arrow.AttachedAnimal==arrowTarget.Spawn.Id&&!arrow.InFlight,"Penetrating arrow embeds into the actual hit animal");
        yield return new WaitForSecondsRealtime(.4f);UpdateAttachedProjectiles();Check(arrow.Position.Length>.99999&&arrow.AttachedAnimal==arrowTarget.Spawn.Id,"Embedded arrow follows the current skin-bone pose");paused=true;yield return Image("04-embedded-arrow");
        TerrainHandoff.Coordinates(frame,arrow.Position,out double ax,out double az);motion.Place(ax-.4,az,Ground(ax-.4,az));RefreshInventoryNearby(true);
        Check(MovePossession(arrow.Id,PossessionPlace.LeftHand)&&arrow.AttachedAnimal==0,"Embedded arrow can be physically retrieved within reach");Inventory.Validate();
        foreach(var ranged in new[]{("Sling",PossessionKind.Sling),("Spear-thrower",PossessionKind.SpearThrower)})
        {
            ClearItems();yield return new WaitForSecondsRealtime(.85f);paused=true;Aim(arrowTarget,5);
            var launcher=Hold(ranged.Item2);Possession ammunition;
            if(ranged.Item2==PossessionKind.Sling)
            {
                var given=ExecuteGive(new[]{"/give","pebble"});ammunition=Inventory.Items.FirstOrDefault(p=>p.Kind==PossessionKind.SmallRock);
                Check(given.Success&&ammunition!=null&&ammunition.MassKg>=.015&&ammunition.MassKg<=.3&&ammunition.WidthM<.05,"Pebble command supplies an ordinary, realistically sized sling stone");
            }
            else ammunition=Hold(PossessionKind.SpearDart);
            health=arrowTarget.Health;BeginItemCharge(launcher,false,0,false);
            Check(itemCharge!=null&&itemCharge.Ammo==ammunition,ranged.Item1+" selects its actual carried ammunition");
            itemCharge.Started-=1.2;ReleaseItemCharge();paused=false;double shotDeadline=Time.unscaledTimeAsDouble+4;
            while(arrowTarget.Health>=health&&ammunition.InFlight&&Time.unscaledTimeAsDouble<shotDeadline)yield return null;
            paused=true;
            Check(arrowTarget.Health<health,ranged.Item1+" launches a physical projectile that damages the visible animal surface");
            Check(Inventory.AtHand(PossessionPlace.RightHand)==launcher&&ammunition.Place==PossessionPlace.Ground,ranged.Item1+" retains the launcher without recalling its fired ammunition");
        }
        ClearItems();UpdateHumans();UpdateCamera();
        var orphanArrow=PortableTechnology.Create(PossessionKind.Arrow,Position);orphanArrow.AttachedHuman=int.MaxValue;orphanArrow.GroundOffsetM=1.7;Inventory.Items.Add(orphanArrow);
        var orphanPosition=orphanArrow.Position;UpdateAttachedProjectiles();
        Check(orphanArrow.AttachedHuman==0&&orphanArrow.GroundMoving&&!orphanArrow.InFlight&&(orphanArrow.Position-orphanPosition).Length==0&&orphanArrow.GroundOffsetM==1.7,"A vanished temporary human releases its arrow at the last recorded pose without deleting or relaunching it");
        const ulong streamedOwner=ulong.MaxValue-1;
        var streamedRecord=new WildlifeIndividual{Id=streamedOwner,Species="deer",Position=Position,Health=50,MaximumHealth=50};Wildlife.Individuals.Add(streamedRecord);wildlifeRecords?.Add(streamedOwner,streamedRecord);
        var streamedArrow=PortableTechnology.Create(PossessionKind.Arrow,Position);streamedArrow.AttachedAnimal=streamedOwner;Inventory.Items.Add(streamedArrow);
        try{UpdateAttachedProjectiles();Check(streamedArrow.AttachedAnimal==streamedOwner&&!streamedArrow.GroundMoving,"Streaming out a saved animal retains its embedded arrow attachment");}
        finally{Wildlife.Individuals.Remove(streamedRecord);wildlifeRecords?.Remove(streamedOwner);}
        ClearItems();
        var savedHands=Hold(PossessionKind.StoneTippedSpear);var digger=new Possession{Id="dig-test",Kind=PossessionKind.DiggingStick,Position=Position,WidthM=.03,HeightM=1,DepthM=.03,MassKg=.5,CellsHigh=10};Inventory.Items.Add(digger);bool completed=false;
        combatGuarding=true;
        BeginWorldToolWork(digger,"Controlled tool-work check",120,()=>completed=true);Check(worldToolWork!=null&&savedHands.Place==PossessionPlace.Ground&&digger.Place==PossessionPlace.RightHand&&!combatGuarding,"World tool work borrows occupied hands without extra storage or retaining a stowed weapon's guard");
        worldToolWork.Done=119.999;paused=false;UpdateWorldToolWork();paused=true;Check(completed&&worldToolWork==null,"Timed world action completes");Check(savedHands.Place==PossessionPlace.RightHand,"Timed world action restores the displaced spear; actual place="+savedHands.Place);
        BeginWorldToolWork(digger,"Interrupted work",120,()=>{});CancelWorldToolWork();Check(savedHands.Place==PossessionPlace.RightHand&&Inventory.ToolWorkEquipment==null,"Interrupted gathering restores equipment and clears temporary work state");
        var switchFibre=PortableTechnology.Create(PossessionKind.RawFibre,Position);Inventory.Items.Add(switchFibre);SetCraftWorkArea(switchFibre);bool gatheringCancelled=false;
        BeginWorldToolWork(digger,"Switching to crafting",120,()=>{},()=>gatheringCancelled=true);
        Check(StartPortableCraft("prepare-fibres",switchFibre.Id)&&gatheringCancelled&&worldToolWork==null&&Inventory.ToolWorkEquipment==null,"Starting a craft cancels gathering before borrowing hands again");
        CancelItemCraft(ActiveItemCraft);
        Check(savedHands.Place==PossessionPlace.RightHand,"Cancelling the replacement craft restores the original spear, not the borrowed digging stick");
        inventoryOpen=true;
        foreach(var kind in PortableTechnology.Items.Select(p=>p.Kind))
        {
            var sample=PortableTechnology.Create(kind,Position);ItemThumbnail(sample);UpdateItemThumbnails();var thumbnail=itemThumbnails[ItemThumbnailKey(sample)];
            var target=RenderTexture.GetTemporary(192,192,0);var prior=RenderTexture.active;Graphics.Blit(thumbnail,target);RenderTexture.active=target;var pixels=new Texture2D(192,192,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,192,192),0,0);pixels.Apply();
            Check(pixels.GetPixels32().Count(p=>p.a>64)>100,"Actual textured model thumbnail renders: "+kind);File.WriteAllBytes(Path.Combine(output,"item-"+kind+".png"),pixels.EncodeToPNG());Destroy(pixels);RenderTexture.active=prior;RenderTexture.ReleaseTemporary(target);
        }
        inventoryOpen=false;
        var labelOwl=animalProfiles.First(p=>p.slug=="owl");var labelledTaxon=sourceWorld.Ecology.Taxa[0];string originalTaxonLabel=labelledTaxon.Name,labelCacheKey=labelOwl.slug+"/0";
        bool hadLabelCache=wildlifeBehaviours.TryGetValue(labelCacheKey,out var priorLabelBehaviour);
        try
        {
            labelledTaxon.Name="western forest owl";wildlifeBehaviours.Remove(labelCacheKey);
            Check(!AnimalBehaviour(labelOwl,0).Migratory,"A display-name fragment cannot turn an owl into a migratory tern");
        }
        finally{labelledTaxon.Name=originalTaxonLabel;if(hadLabelCache)wildlifeBehaviours[labelCacheKey]=priorLabelBehaviour;else wildlifeBehaviours.Remove(labelCacheKey);}
        ClearItems();reviewWildlifeBehaviour=true;paused=false;yield return Deer();var wary=animals.Values.First(a=>a.Manual&&!a.Dead&&a.Spawn.Profile.slug=="deer");paused=true;Aim(wary,8);wary.BehaviourAt=0;wary.ThreatUntil=0;UpdateAnimalBehaviour(wary);
        double awayX=wary.X-motion.X,awayZ=wary.Z-motion.Z;Check(wary.TargetSpeed>0&&Math.Sin(wary.TargetHeading*Math.PI/180)*awayX+Math.Cos(wary.TargetHeading*Math.PI/180)*awayZ>0,"Uninjured deer notices and flees a nearby player");
        paused=false;ExecuteAnimalSpawnCommand("/spawn wolf");double wolfDeadline=Time.unscaledTimeAsDouble+35;while(!animals.Values.Any(a=>a.Manual&&a.Spawn.Profile.slug=="wolf")&&Time.unscaledTimeAsDouble<wolfDeadline)yield return null;paused=true;
        var hunter=animals.Values.First(a=>a.Manual&&a.Spawn.Profile.slug=="wolf");hunter.X=motion.X+80;hunter.Z=motion.Z+8;wary.X=hunter.X+3;wary.Z=hunter.Z;
        foreach(var a in new[]{hunter,wary}){a.Planet=frame.Position(a.X,a.Z);a.GroundY=Ground(a.X,a.Z);a.ThreatUntil=0;a.TargetSpeed=a.Speed=0;a.NextDecision=a.Life+1000;a.BehaviourAt=0;UpdateAnimal(a,0);}
        nextWildlifeIndex=0;IndexWildlifeNeighbours();UpdateAnimalBehaviour(hunter);Check(hunter.TargetSpeed>0&&Math.Sin(hunter.TargetHeading*Math.PI/180)>.8,"A hungry wolf pursues nearby deer rather than wandering independently");
        wary.X=hunter.X+.3;wary.Z=hunter.Z;wary.Planet=frame.Position(wary.X,wary.Z);wary.GroundY=Ground(wary.X,wary.Z);UpdateAnimal(wary,0);
        double unreachableHealth=wary.Health;var preyRootBefore=wary.Root.transform.localPosition;hunter.AttackAt=0;
        try
        {
            wary.Root.transform.localPosition+=Vector3.up*8;AnimalContactAttack(hunter,wary);
            Check(wary.Health==unreachableHealth&&hunter.AttackAt==0,"An animal cannot bite vertically unreachable prey or spend a contact cooldown on it");
        }
        finally{wary.Root.transform.localPosition=preyRootBefore;hunter.AttackAt=0;}
        wary.Health=1;AnimalContactAttack(hunter,wary);Check(wary.Dead&&wary.Ragdoll!=null,"Predator contact attacks use actual animal health and ragdoll death");
        double current=PlayerRules.SolarDays;var groupSpawn=new FaunaPrototypeSpawn{Id=99101,GroupId=991,GroupSize=5,GroupIndex=0,Profile=hunter.Spawn.Profile,Position=Position,Hash=123,Taxon=-1};
        RegisterWildlifeGroups(new[]{groupSpawn});var tracked=wildlifeGroups[991];TerrainHandoff.Coordinates(frame,tracked.Position,out double tx,out double tz);tracked.Goal=frame.Position(tx+12,tz);tracked.Intent=WildlifeIntent.Travel;tracked.NextDecisionDays=current+1;tracked.LastSolarDays=current-1/(PlayerRules.Settings.RealMinutesPerDay*60);
        V beforeGroup=tracked.Position;TickWildlifeGroup(tracked,current);Check((tracked.Position-beforeGroup).Length*frame.RadiusM>.01,"A background group advances without rendering or individual bone poses");
        wildlifeGroups.Clear();wildlifeGroupIndex.Clear();RegisterWildlifeGroups(new[]{groupSpawn});Check(wildlifeGroups[991]==tracked&&wildlifeGroupIndex.Values.Any(b=>b.Contains(991)),"Saved groups rebuild their spatial index and retain their actual goal");
        V farGround=frame.Position(motion.X+3100,motion.Z);bool farReady=TryWildlifeGround(farGround,out _);double farDeadline=Time.unscaledTimeAsDouble+20;
        while(!farReady&&Time.unscaledTimeAsDouble<farDeadline){yield return null;farReady=TryWildlifeGround(farGround,out _);}
        Check(farReady,"Regional wildlife ground loads asynchronously beyond the walking patch without an out-of-range terrain query");
        var priorWanted=animalWanted.ToArray();animalWanted.Clear();var unseen=animalProfiles.First(p=>p.medium=="ground"&&!animalModels.ContainsKey(p.slug)&&!animalLoadFailures.Contains(p.slug));
        var unseenSpawn=new FaunaPrototypeSpawn{Profile=unseen,Position=frame.Position(motion.X+unseen.VisibleRadiusM+100,motion.Z)};animalWanted.Add(unseenSpawn);
        Check(NextVisibleAnimalModel()==null,"A tracked animal outside visual range cannot trigger mesh/texture/collision-skin loading");unseenSpawn.Position=Position;
        Check(NextVisibleAnimalModel()==unseenSpawn,"The same animal becomes eligible for its visual model when it enters range");animalWanted.Clear();animalWanted.AddRange(priorWanted);
        foreach(string slug in new[]{"ground-squirrel","goose"})
        {
            paused=false;ExecuteAnimalSpawnCommand("/spawn "+slug);double bodyDeadline=Time.unscaledTimeAsDouble+35;
            while(!animals.Values.Any(a=>a.Manual&&!a.Dead&&a.Spawn.Profile.slug==slug)&&Time.unscaledTimeAsDouble<bodyDeadline)yield return null;
            paused=true;var subject=animals.Values.FirstOrDefault(a=>a.Manual&&!a.Dead&&a.Spawn.Profile.slug==slug);Check(subject!=null,"Representative ragdoll subject loads: "+slug);
            subject.X+=12;subject.Planet=frame.Position(subject.X,subject.Z);subject.GroundY=Ground(subject.X,subject.Z);
            subject.Clip=subject.Model.Asset.Clips.FirstOrDefault(c=>c.role=="idle")??subject.Clip;subject.OldClip=null;subject.Time=0;subject.Speed=subject.TargetSpeed=0;subject.Heading=subject.TargetHeading=0;subject.NextDecision=subject.Life+1000;subject.Fade=1;UpdateAnimal(subject,0);
            yield return Image("before-corpse-"+slug,subject);UpdateCamera();
            var originalSubject=(Vector3[])PosedAnimal(subject).Clone();
            DamageAnimal(subject,10000,subject.Renderer.bounds.center,Vector3.right);paused=false;yield return new WaitForSecondsRealtime(4);paused=true;
            var collapsedSubject=PosedAnimal(subject);var subjectSurface=AnimalSurface(subject);float maximumExtension=0;int worstA=0,worstB=0;
            for(int face=0;face<subjectSurface.Triangles.Length;face+=3)for(int edge=0;edge<3;edge++)
            {int a=subjectSurface.Triangles[face+edge],b=subjectSurface.Triangles[face+(edge+1)%3];if(!subjectSurface.Visible(a,subject.Parts))continue;float extension=Vector3.Distance(collapsedSubject[a],collapsedSubject[b])-Vector3.Distance(originalSubject[a],originalSubject[b]);if(extension>maximumExtension){maximumExtension=extension;worstA=a;worstB=b;}}
            File.WriteAllText(Path.Combine(output,"corpse-"+slug+"-stretch.txt"),"Maximum edge extension m "+maximumExtension+"; animal length m "+subject.Spawn.Profile.lengthM+"; mass kg "+subject.MassKg+"; physical bodies "+subject.Ragdoll.BodyCount+"\nWorst edge "+worstA+" / "+worstB+"\nOriginal "+originalSubject[worstA].ToString("F6")+" / "+originalSubject[worstB].ToString("F6")+"\nCollapsed "+collapsedSubject[worstA].ToString("F6")+" / "+collapsedSubject[worstB].ToString("F6")+"\nJoints "+subjectSurface.Joints[worstA]+" / "+subjectSurface.Joints[worstB]+"\nWeights "+subjectSurface.Weights[worstA]+" / "+subjectSurface.Weights[worstB]);
            yield return Image("corpse-"+slug,subject);UpdateCamera();
            Check(subject.Ragdoll.Connected,"Every corpse limb remains connected to its common root: "+slug);
            Check(maximumExtension<subject.Spawn.Profile.lengthM*.15,"Corpse avoids sharp skin spikes relative to animal size: "+slug);
            Check(subject.Ragdoll.BodyCount>2&&AnimalPalette(subject).All(m=>Enumerable.Range(0,16).All(i=>float.IsFinite(m[i]))),"Original bird/root-head skeleton retains multiple stable corpse bodies: "+slug);
        }
        if(SpawnHumans(1)>0)
        {
            var npc=humanNpcs.Last();var staleArrow=PortableTechnology.Create(PossessionKind.Arrow,Position);staleArrow.AttachedHuman=npc.Id;Inventory.Items.Add(staleArrow);
            inventoryInitialized=false;InitializeInventory();
            Check(staleArrow.AttachedHuman==0&&staleArrow.GroundMoving,"Session inventory initialization detaches an old NPC arrow even if a live NPC has reused its ID");
            DamageHuman(npc,1000,null,Vector3.forward,10);Check(npc.Health<=0&&npc.Ragdoll!=null,"Manually spawned human NPC also supports combat death and ragdoll");
        }
        ExecuteAnimalSpawnCommand("/spawn clear");Check(!Wildlife.Individuals.Any(p=>p.Manual)&&!animalWanted.Any(p=>p.Id==deadId),"Explicit spawn clearing removes saved command corpses so they cannot reappear");
        reviewWildlifeBehaviour=false;
        ClearItems();PlayerRules.Mode=GameMode.Survival;Vitals.Restore();ApplyPlayerMode();ExecuteGive(new[]{"/give","wooden","spear"});
        bool wasPaused=paused;paused=false;Vitals.Unconscious=true;
        try{Check(CombatStopped,"Survival unconsciousness stops item combat independently of the death screen");}
        finally{Vitals.Unconscious=false;paused=wasPaused;}
        combatGuarding=true;
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
        var report=new List<string>();var errors=new List<string>();Application.LogCallback listener=(m,t,k)=>{if(k==LogType.Error||k==LogType.Exception){errors.Add(m);File.WriteAllText(Path.Combine(output,"failure.txt"),m+"\n"+t);Application.Quit(1);}};Application.logMessageReceived+=listener;
        double deadline=Time.realtimeSinceStartupAsDouble+180;while((Busy||!worldReady||humans==null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(!worldReady||Busy||humans==null)throw new InvalidOperationException("Item-use fixture unavailable");play.Settings.HorizontalScale=.1;TerrainVideoOptions.Current.ViewDistanceM=1000;TerrainVideoOptions.Revision++;ExploreTerrain(TerrainMapViewport.FromDegrees(50,10));deadline=Time.realtimeSinceStartupAsDouble+120;
        while((terrainExplorer==null||!terrainExplorer.PerformanceGroundSettled)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(terrainExplorer==null||!terrainExplorer.PerformanceGroundSettled)throw new InvalidOperationException("Item-use terrain did not finish its requested geometry");
        if(Environment.GetCommandLineArgs().Contains("--worldgen-wildlife-audio-check"))yield return terrainExplorer.CheckWildlifeAudioFlow(output,report);
        else if(Environment.GetCommandLineArgs().Contains("--worldgen-solid-scatter-check"))yield return terrainExplorer.CheckSolidScatterFlow(output,report);
        else if(Environment.GetCommandLineArgs().Contains("--worldgen-nature-check"))yield return terrainExplorer.CheckNatureFlow(output,report);
        else if(Environment.GetCommandLineArgs().Contains("--worldgen-save-slots-check"))yield return CheckSaveSlotsRuntime(output,report);
        else if(Environment.GetCommandLineArgs().Contains("--worldgen-woodwork-check"))yield return terrainExplorer.CheckWoodworkReplacement(report);
        else if(Environment.GetCommandLineArgs().Contains("--worldgen-firefly-check"))yield return terrainExplorer.CheckFireflyFlow(output,report);
        else if(Environment.GetCommandLineArgs().Contains("--worldgen-item-water-check"))yield return terrainExplorer.CheckItemWaterFlow(output,report);
        else yield return terrainExplorer.CheckItemUseFlow(output,report);
        CapturePlayer();play.Validate();ReturnFromPlay("Simulation");yield return null;yield return null;
        Application.logMessageReceived-=listener;if(errors.Count>0)throw new InvalidOperationException(string.Join(" | ",errors));report.Add("PASS no runtime errors or exceptions");File.WriteAllLines(Path.Combine(output,"checks.txt"),report);Debug.Log("ITEM USE FLOW PASS "+report.Count);Application.Quit(0);
    }
}
