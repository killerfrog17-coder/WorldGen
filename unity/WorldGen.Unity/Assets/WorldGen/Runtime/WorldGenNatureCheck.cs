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
    public IEnumerator CheckNatureFlow(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("NATURE "+text);report.Add("PASS "+text);Debug.Log("NATURE PASS "+text);}
        void Image(string name)
        {
            var previous=RenderTexture.active;var old=view.targetTexture;var target=new RenderTexture(1280,800,24);target.Create();view.targetTexture=target;view.Render();RenderTexture.active=target;
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            RenderTexture.active=previous;view.targetTexture=old;target.Release();Destroy(target);Destroy(image);
        }
        InitializeInventory();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();ClearInventoryPhysics();Inventory.Items.Clear();Inventory.Crafts.Clear();
        ExecuteAnimalSpawnCommand("/spawn deer");double deadline=Time.unscaledTimeAsDouble+35;
        while(!animals.Values.Any(a=>a.Manual&&a.Spawn.Profile.slug=="deer"&&!a.Dead)&&Time.unscaledTimeAsDouble<deadline)yield return null;
        var deer=animals.Values.FirstOrDefault(a=>a.Manual&&a.Spawn.Profile.slug=="deer"&&!a.Dead);Check(deer!=null,"Original deer loads for actual corpse interaction");paused=true;
        deer.X=motion.X;deer.Z=motion.Z+1.8;deer.Planet=frame.Position(deer.X,deer.Z);deer.GroundY=Ground(deer.X,deer.Z);deer.Speed=deer.TargetSpeed=0;deer.Fade=1;deer.Clip=deer.Model.Asset.Clips.First(c=>c.role=="idle");deer.Time=0;deer.OldClip=null;deer.CollisionFrame=-1;UpdateAnimal(deer,0);
        DamageAnimal(deer,deer.MaximumHealth+1,deer.Renderer.bounds.center,Vector3.forward,0,true);yield return null;yield return null;
        Check(deer.Dead&&deer.Ragdoll!=null,"Loot requires a genuinely killed ragdoll animal");
        view.transform.LookAt(deer.Renderer.bounds.center);Check(CorpseReach(deer),"Actual rendered carcass is reachable from player eye");
        Check(AimedCorpse(out _) == deer,"Invisible centre aim selects the corpse's posed skin");
        EnsureAnimalLoot(deer);int count=Inventory.Items.Count;EnsureAnimalLoot(deer);Check(count>0&&Inventory.Items.Count==count,"Repeated searches do not regenerate corpse contents");
        Check(Inventory.CarriedMassKg==0,"Unclaimed corpse contents contribute no carried mass or ground clutter");
        var bag=PortableTechnology.Create(PossessionKind.HidePack,Position);bag.Place=PossessionPlace.Back;Inventory.Items.Add(bag);
        var meat=Inventory.Items.First(p=>p.Kind==PossessionKind.RawMeat);Check(TakeCorpseItem(meat)&&meat.Place==PossessionPlace.Container,"Loot enters actual worn container using physical packing");
        var hide=Inventory.Items.First(p=>p.Kind==PossessionKind.RawHide);Check(hide.AppearanceSpecies==deer.Spawn.Profile.slug&&hide.AppearanceCoat==deer.Coat&&hide.SourceAnimalId==deer.Spawn.Id,"Hide provenance uses this animal's original coat");
        Vitals.Restore();Vitals.Hunger=60;Vitals.Thirst=50;double mass=meat.MassKg;EatPossession(meat,false);Check(meat.MassKg<mass&&Vitals.Hunger<60&&Vitals.FoodExposures==null,"Eating corpse meat consumes mass and supplies food without raw-meat penalties");
        var session=WorldJson.Read<PlaySession>(WorldJson.Write(PlayerRules));session.Validate();Check(session.Inventory.Items.Any(p=>p.Place==PossessionPlace.Corpse)&&session.Inventory.ToolResources.Any(p=>p.Id=="loot-"+deer.Spawn.Id.ToString("x16")&&p.Harvests==1),"Save round-trip preserves remaining corpse loot and generation ledger");
        // Finish the real eating gesture before inspecting a held weapon. That
        // gesture intentionally hides private held-item copies while consuming.
        yield return new WaitForSecondsRealtime(1.3f);humanCamera.Restore(0,false);humanCameraDistance=0;UpdateCamera();
        var spear=PortableTechnology.Create(PossessionKind.StoneTippedSpear,Position);spear.Place=PossessionPlace.RightHand;Inventory.Items.Add(spear);UpdateInventoryVisuals();humanHideBody=true;
        viewArmPoseImmediate=true;UpdateInventoryVisuals();var root=viewPossessions[spear.Id].Root;var origin=root.transform.position;combatGlobalReady=combatRightReady=0;StrikeWithItem(spear,false);yield return new WaitForSecondsRealtime(.18f);UpdateInventoryVisuals();
        Check(viewItemActions.ContainsKey(spear.Id)&&Vector3.Distance(origin,root.transform.position)>.025f,"Spear strike visibly moves the held model in first person");
        Image("spear-thrust");
        itemCharge=new ItemCharge{Weapon=spear,Ammo=spear,Started=Time.unscaledTimeAsDouble-1,Duration=.8,Throw=true};UpdateInventoryVisuals();var wound=root.transform.position;CancelItemCharge();UpdateInventoryVisuals();Check(Vector3.Distance(wound,root.transform.position)>.05f,"Throw wind-up and cancellation have distinct reversible view poses");
        Inventory.Items.Remove(spear);viewItemActions.Clear();
        var bow=PortableTechnology.Create(PossessionKind.Bow,Position);bow.Place=PossessionPlace.RightHand;Inventory.Items.Add(bow);UpdateInventoryVisuals();var bowRoot=viewPossessions[bow.Id].Root;var bowRest=bowRoot.transform.position;
        Check(Vector3.Dot(bowRoot.transform.up,view.transform.up)>.95f,"First-person bow remains vertical instead of inheriting the spear's forward tilt");
        var arrow=PortableTechnology.Create(PossessionKind.Arrow,Position);Inventory.Items.Add(arrow);
        itemCharge=new ItemCharge{Weapon=bow,Ammo=arrow,Started=Time.unscaledTimeAsDouble-1.1,Duration=1.05};UpdateInventoryVisuals();Check(Vector3.Distance(bowRoot.GetComponentInChildren<LineRenderer>().GetPosition(1),new Vector3(.38f,.5f,0))>.05f,"Bow drawing moves the string toward the real drawing hand without changing the source rig");Image("bow-draw");CancelItemCharge();Inventory.Items.Remove(bow);Inventory.Items.Remove(arrow);
        var sourcePlant=scatterChunks.Values.Where(c=>c.Roots!=null).SelectMany(c=>c.Source.Instances).Where(p=>p.Kind==ScatterKind.MidPlant||p.Kind==ScatterKind.Herb).OrderBy(p=>(p.Position-Position).Length).FirstOrDefault();
        Check(sourcePlant.Id!=0,"Loaded real terrain contains placed resource plants");
        TerrainHandoff.Coordinates(frame,sourcePlant.Position,out double px,out double pz);motion.X=px;motion.Z=pz;motion.Y=VisibleGround(px,pz,Ground(px,pz))+TerrainMotion.EyeHeightM;UpdateCamera();natureRefreshAt=0;RefreshNatureSources();
        var source=natureSources.FirstOrDefault(s=>s.Products.Any(p=>p.Kind==PossessionKind.RawFibre||p.Kind==PossessionKind.ReedBundle));
        Check(source!=null,"Actual regional plant identity resolves to obtainable fibre or reeds");
        var product=source.Products.First(p=>p.Kind==PossessionKind.RawFibre||p.Kind==PossessionKind.ReedBundle);int before=Inventory.Items.Count;GatherPlant(source,product);Check(Inventory.Items.Count==before+1,"Gathering an actual placed plant produces a physical craft input");
        GatherPlant(source,product);Check(Inventory.Items.Count==before+1&&!ForageReady(source,product,out _),"Plant depletion prevents immediate repeated gathering");
        double oldYear=skyYear;skyYear=Math.Floor(oldYear)+1;Check(ForageReady(source,product,out _),"Material renewal follows the actual simulation calendar year");skyYear=oldYear;
        var ledger=WorldJson.Read<PlayerInventory>(WorldJson.Write(Inventory));ledger.Validate();Check(ledger.ToolResources.Any(r=>r.Id==ForageLedgerKey(source,product)&&r.Harvests==1),"Actual plant depletion survives saving");
        SetInventoryOpen(true);
        foreach(var kind in new[]{PossessionKind.Berries,PossessionKind.Fruit,PossessionKind.Nuts,PossessionKind.Mushroom,PossessionKind.EdibleRoot,PossessionKind.RawMeat,PossessionKind.RawFish,PossessionKind.AnimalFat})
        {
            var item=PortableTechnology.Create(kind,Position);ItemThumbnail(item);for(int i=0;i<3;i++){UpdateItemThumbnails();yield return null;}var texture=ItemThumbnail(item);Check(texture!=null&&texture.width==192,"Food geometry and actual thumbnail render: "+kind);
            var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(192,192,0,RenderTextureFormat.ARGB32);Graphics.Blit(texture,target);RenderTexture.active=target;
            var image=new Texture2D(192,192,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,192,192),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,"food-"+kind+".png"),image.EncodeToPNG());Destroy(image);RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);
        }
        var clearedIds=new HashSet<ulong>(Wildlife.Individuals.Where(p=>p.Manual).Select(p=>p.Id));ExecuteAnimalSpawnCommand("/spawn clear");
        Check(!Inventory.Items.Any(p=>p.Place==PossessionPlace.Corpse&&clearedIds.Contains(p.CorpseOwner))&&!Inventory.ToolResources.Any(p=>clearedIds.Any(id=>p.Id=="loot-"+id.ToString("x16"))),"Clearing spawned corpses removes only unclaimed contents and prevents stale loot-generation IDs");
        CaptureWildlife();CaptureInventoryPhysics();PlayerRules.Validate();SetInventoryOpen(false);viewArmPoseImmediate=false;paused=false;
    }
}
