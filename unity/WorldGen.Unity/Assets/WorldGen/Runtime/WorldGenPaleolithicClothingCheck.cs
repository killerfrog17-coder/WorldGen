using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    IEnumerator CheckPaleolithicClothingFlow(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);report.Add("PASS "+text);Debug.Log("CLOTHING CHECK PASS "+text);}
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();var capture=ScreenCapture.CaptureScreenshotAsTexture();
            var pixels=capture.GetPixels32();bool visible=pixels.Any(p=>p.r>45||p.g>45||p.b>45);
            Check(visible,"Screenshot contains rendered game content: "+name);File.WriteAllBytes(Path.Combine(output,name+".png"),capture.EncodeToPNG());Destroy(capture);
        }
        InitializeInventory();SetInventoryOpen(true);yield return null;
        var garments=new List<Possession>();
        foreach(var kind in new[]{PossessionKind.FurCoat,PossessionKind.HideLeggings,PossessionKind.HideFootwear,PossessionKind.HideHood})
        {
            var item=PortableTechnology.Create(kind,Position,"deer",1);Inventory.Items.Add(item);garments.Add(item);
            Check(MovePossession(item.Id,PortableTechnology.Wear(item).Value),"Wear actual garment: "+kind);
        }
        var pack=PortableTechnology.Create(PossessionKind.HidePack,Position,"deer",1);Inventory.Items.Add(pack);Check(MovePossession(pack.Id,PossessionPlace.Back),"Pack remains independent of clothing");
        UpdateInventoryVisuals();double until=Time.unscaledTimeAsDouble+30;
        while((inventoryPreviewActor==null||!animalModels.ContainsKey("deer"))&&Time.unscaledTimeAsDouble<until)yield return null;
        Check(inventoryPreviewActor!=null,"Actual native inventory actor exists");
        UpdatePortableAppearances();inventoryPreviewYaw=180;RenderInventoryPreview();yield return Shot("clothes-front");
        inventoryPreviewYaw=0;RenderInventoryPreview();yield return Shot("clothes-back");
        foreach(var item in garments)
        {
            var root=inventoryVisuals[item.Id];Check(garmentVisuals.TryGetValue(root,out var visual)&&visual.Skin.sharedMesh.vertexCount>0,"Garment uses the actor's skin weights: "+item.Kind);
            Check(visual.Skin.bones.Length==visual.Source.bones.Length&&visual.Skin.sharedMesh.bindposes.Length==visual.Source.sharedMesh.bindposes.Length,"Skin bind poses are retained: "+item.Kind);
            Check(visual.Folded.All(r=>!r.enabled),"Folded object is hidden while worn: "+item.Kind);
        }
        var markedItem=garments[0];var markedRoot=inventoryVisuals[markedItem.Id];var markedSkin=garmentVisuals[markedRoot].Skin;var markProps=new MaterialPropertyBlock();
        markedItem.PigmentMarked=true;markedItem.PigmentColour=new V(.7,.15,.03);RefreshPortableRoot(markedRoot,markedItem);markedSkin.GetPropertyBlock(markProps);
        Check(markProps.GetFloat("_MarkCoverage")==1&&Math.Abs(markProps.GetColor("_MarkColour").r-.7)<.001,"Cached appearance invalidates when actual pigment changes");
        RefreshPortableRoot(markedRoot,markedItem);markedSkin.GetPropertyBlock(markProps);Check(markProps.GetFloat("_HasFur")==1&&markProps.GetFloat("_Wearer")==1,"Repeated cached appearance retains actual fur and player visibility");
        markedItem.PigmentMarked=false;RefreshPortableRoot(markedRoot,markedItem);markedSkin.GetPropertyBlock(markProps);Check(markProps.GetFloat("_MarkCoverage")==0,"Removing pigment refreshes the same native garment skin");
        var coat=garments[0];Check(MovePossession(coat.Id,PossessionPlace.Ground),"Take coat off without losing it");UpdateInventoryVisuals();
        var folded=garmentVisuals[inventoryVisuals[coat.Id]];Check(!folded.Skin.enabled&&folded.Folded.All(r=>r.enabled),"Removing clothing restores the folded ground item");
        Check(MovePossession(coat.Id,PossessionPlace.Torso),"Re-equip the same coat");UpdateInventoryVisuals();
        var hood=garments[3];var hoodVisual=garmentVisuals[inventoryVisuals[hood.Id]];
        Check(hoodVisual.HoodHair.All(r=>!r.enabled),"Hood covers hair without scalp clipping");
        Check(MovePossession(hood.Id,PossessionPlace.Ground),"Remove hood");UpdateInventoryVisuals();
        Check(hoodVisual.HoodHair.Where((r,i)=>hoodVisual.HairEnabled[i]).All(r=>r.enabled),"Removing hood restores the wearer's original hair");
        Check(MovePossession(hood.Id,PossessionPlace.Head),"Put hood back on");UpdateInventoryVisuals();
        var npc=CreateHuman(0,9051);var npcCoat=PortableTechnology.Create(PossessionKind.FurCoat,Position,"deer",1);npcCoat.Place=PossessionPlace.Torso;
        var npcProp=CreatePossessionVisual(npcCoat,30);npcProp.transform.SetParent(npc.Root.transform,false);var bones=npc.Root.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
        PosePortableWear(npcProp,npcCoat,npc.Root.transform,bones);var properties=new MaterialPropertyBlock();garmentVisuals[npcProp].Skin.GetPropertyBlock(properties);
        Check(properties.GetFloat("_Wearer")==0,"Another person's outfit is not clipped by first-person player visibility");
        var visualNpc=garmentVisuals[npcProp];var poseMesh=new Mesh();
        for(int frame=0;frame<30;frame++)AnimateHuman(npc,2.5f,false,false,1f/30);
        PosePortableWear(npcProp,npcCoat,npc.Root.transform,bones);visualNpc.Skin.BakeMesh(poseMesh,true);
        var points=poseMesh.vertices;var worldBounds=new Bounds(visualNpc.Skin.transform.TransformPoint(points[0]),Vector3.zero);foreach(var p in points)worldBounds.Encapsulate(visualNpc.Skin.transform.TransformPoint(p));
        Check(points.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z))&&worldBounds.size.y>.5f&&worldBounds.size.y<1.8f&&worldBounds.size.x<2,"Native walking pose retains finite, metre-scale garment geometry");
        Destroy(poseMesh);DestroyInventoryVisual(npcProp);DestroyHuman(npc);
        Inventory.Validate();Check(PaleolithicClothing.Insulation(Inventory)>.85,"Native inventory supplies full cold outfit protection");
        inventoryPreviewYaw=25;RenderInventoryPreview();yield return Shot("clothes-outfit");SetInventoryOpen(false);
    }
}
