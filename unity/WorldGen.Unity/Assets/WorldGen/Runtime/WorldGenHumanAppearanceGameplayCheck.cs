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
    public IEnumerator CheckHumanAppearanceGameplay(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("APPEARANCE GAMEPLAY "+text);report.Add("PASS "+text);}
        npcModel??=new NpcModelClient();npcModel.Config.Enabled=false;
        PlayerRules.Mode=GameMode.Creative;Vitals.Restore();ApplyPlayerMode();TestHumanView(0);paused=false;
        TerrainMapViewport.Degrees(Position,out _,out double longitude);PlayerRules.SetLocalSolarHour(12,longitude);WeatherControl.Set("clear");nextDaylightAt=0;SetDaylight(LocalSolarHour,skyYear);
        Check(humanPlayer?.Resident?.Phenotype!=null,"Embodied player uses the actual generated resident's inherited appearance");
        double until=Time.realtimeSinceStartupAsDouble+90;
        while(!livingPeople.Values.Any(a=>a.GroupId==PlayerRules.Identity.Group&&a.Resident?.AgeYears>=18)&&Time.realtimeSinceStartupAsDouble<until)yield return null;
        var peer=livingPeople.Values.FirstOrDefault(a=>a.GroupId==PlayerRules.Identity.Group&&a.Resident?.AgeYears>=18);
        Check(peer!=null&&peer.Phenotype!=null,"Another actual member of the same band has an individually shaped body");
        var context=NpcContext(peer,new NpcEventQueue.Event{Person=peer.ResidentId,Speaker=0,Words="Tell me about me."},new[]{"player","none"});
        Check(context.Contains(HumanPhenotypes.VisibleAppearance(humanPlayer.Resident.Phenotype,humanPlayer.Resident.Grooming,humanPlayer.Resident.AgeYears)),"Actual dialogue context describes saved hair and eyes rather than a preset-model label");
        File.WriteAllText(Path.Combine(output,"appearance-context.txt"),context);
        foreach(var held in Inventory.Items.Where(p=>p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand||p.Place==PossessionPlace.Torso).ToArray())
        {Check(Inventory.Move(held.Id,PossessionPlace.Ground,"",0,0,false,out _),"Existing possession is retained while preparing the QA outfit");held.Position=Position;}
        Possession Equip(PossessionKind kind,PossessionPlace place)
        {
            var item=PortableTechnology.Create(kind,Position,"deer",1);Inventory.Items.Add(item);
            Check(Inventory.Move(item.Id,place,"",0,0,false,out string reason),"Real test possession equipped: "+kind+" "+reason);return item;
        }
        Equip(PossessionKind.StoneTippedSpear,PossessionPlace.RightHand);Equip(PossessionKind.StoneBlade,PossessionPlace.LeftHand);var coat=Equip(PossessionKind.FurCoat,PossessionPlace.Torso);
        inventoryNearbyAt=0;UpdateInventoryVisuals();yield return new WaitForSecondsRealtime(1);
        var eyes=humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name.Contains("high-poly"));var mesh=new Mesh();eyes.BakeMesh(mesh,true);var center=Vector3.zero;foreach(var p in mesh.vertices)center+=eyes.transform.TransformPoint(p);center/=mesh.vertexCount;Destroy(mesh);
        Check(Math.Abs(view.transform.position.y-center.y)<.18,"First-person camera height follows the inherited body and real eyes");
        Check(playerSolid!=null&&Math.Abs(playerSolid.height-(HumanActualEye(humanPlayer)+.1f*humanPlayer.Root.transform.localScale.y))<.02,"Player solid capsule follows actual inherited stature");
        Capture(Path.Combine(output,"inherited-first-person.png"));
        var peerFocus=NpcEye(peer)-transform.up*.1f;view.transform.position=peerFocus+peer.Root.transform.forward*1.8f;view.transform.LookAt(peerFocus);Capture(Path.Combine(output,"inherited-camp-peer.png"));UpdateCamera();
        SetInventoryOpen(true);inventoryPreviewYaw=160;RenderInventoryPreview();yield return null;
        Check(inventoryPreviewActor?.Phenotype!=null&&inventoryPreviewActor.Phenotype.Traits.SequenceEqual(humanPlayer.Phenotype.Traits),"Inventory portrait retains all inherited traits");
        Check(inventoryPreviewActor.Root.transform.localScale==humanPlayer.Root.transform.localScale,"Inventory portrait retains actual body size");
        SkinnedMeshRenderer Body(HumanActor actor)=>actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name.ToLowerInvariant().EndsWith(".body")||r.name.EndsWith("_Body"));
        var body=Body(humanPlayer);var portrait=Body(inventoryPreviewActor);
        Check(HumanNativeMesh(body.sharedMesh).vertices.SequenceEqual(HumanNativeMesh(portrait.sharedMesh).vertices),"Inventory portrait uses the same personally shaped anatomy");
        Check(portrait.sharedMaterial.GetFloat("_PigmentMode")==1&&portrait.sharedMaterial.GetColor("_PigmentColor")==body.sharedMaterial.GetColor("_PigmentColor"),"Inventory portrait uses the same inherited pigmentation");
        var worn=garmentVisuals[inventoryVisuals[coat.Id]];
        Check(HumanNativeMesh(worn.Source.sharedMesh)==HumanNativeMesh(body.sharedMesh),"Actual worn clothing fits the inherited source skin");
        Check(garmentVisuals.Values.Any(v=>v.Source==portrait),"Inventory clothing fits the inherited portrait body");
        yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,"inherited-inventory.png"));yield return new WaitForEndOfFrame();
        SetInventoryOpen(false);var saved=PlayerRules.Copy();StorePlayer(saved);saved.Validate();
        var read=WorldJson.Read<PlaySession>(WorldJson.Write(saved));read.Validate();
        Check(read.Identity.Person.Phenotype.Traits.SequenceEqual(humanPlayer.Phenotype.Traits)&&read.Identity.Person.Grooming.Hair==humanPlayer.Resident.Grooming.Hair,"Complete played session preserves controlled-person appearance");
        var remembered=read.Humans.People.FirstOrDefault(p=>p.Person==peer.ResidentId);
        Check(remembered?.Phenotype!=null&&remembered.Phenotype.Traits.SequenceEqual(peer.Phenotype.Traits),"Complete played session preserves encountered NPC appearance");
        File.WriteAllText(Path.Combine(output,"actual-player.json"),WorldJson.Write(read.Identity));
        Check(Inventory.Items.Any(p=>p.Kind==PossessionKind.StoneTippedSpear)&&Inventory.Items.Any(p=>p.Kind==PossessionKind.StoneBlade),"Held tools remain real possessions through portrait and save operations");
    }
}
