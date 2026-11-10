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
        IEnumerator Shot(string name){yield return new WaitForEndOfFrame();var capture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,name+".png"),capture.EncodeToPNG());Destroy(capture);}
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
        UpdatePortableAppearances();inventoryPreviewYaw=0;RenderInventoryPreview();yield return Shot("clothes-front");
        inventoryPreviewYaw=140;RenderInventoryPreview();yield return Shot("clothes-back");
        foreach(var item in garments)
        {
            var root=inventoryVisuals[item.Id];Check(garmentVisuals.TryGetValue(root,out var visual)&&visual.Skin.sharedMesh.vertexCount>0,"Garment uses the actor's skin weights: "+item.Kind);
            Check(visual.Skin.bones.Length==visual.Source.bones.Length&&visual.Skin.sharedMesh.bindposes.Length==visual.Source.sharedMesh.bindposes.Length,"Skin bind poses are retained: "+item.Kind);
            Check(visual.Folded.All(r=>!r.enabled),"Folded object is hidden while worn: "+item.Kind);
        }
        var coat=garments[0];Check(MovePossession(coat.Id,PossessionPlace.Ground),"Take coat off without losing it");UpdateInventoryVisuals();
        var folded=garmentVisuals[inventoryVisuals[coat.Id]];Check(!folded.Skin.enabled&&folded.Folded.All(r=>r.enabled),"Removing clothing restores the folded ground item");
        Check(MovePossession(coat.Id,PossessionPlace.Torso),"Re-equip the same coat");UpdateInventoryVisuals();
        var npc=CreateHuman(0,9051);var npcCoat=PortableTechnology.Create(PossessionKind.FurCoat,Position,"deer",1);npcCoat.Place=PossessionPlace.Torso;
        var npcProp=CreatePossessionVisual(npcCoat,30);npcProp.transform.SetParent(npc.Root.transform,false);var bones=npc.Root.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
        PosePortableWear(npcProp,npcCoat,npc.Root.transform,bones);var properties=new MaterialPropertyBlock();garmentVisuals[npcProp].Skin.GetPropertyBlock(properties);
        Check(properties.GetFloat("_Wearer")==0,"Another person's outfit is not clipped by first-person player visibility");
        var visualNpc=garmentVisuals[npcProp];var poseMesh=new Mesh();
        for(int frame=0;frame<30;frame++)AnimateHuman(npc,2.5f,false,false,1f/30);
        PosePortableWear(npcProp,npcCoat,npc.Root.transform,bones);visualNpc.Skin.BakeMesh(poseMesh,true);
        Check(poseMesh.vertices.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z))&&poseMesh.bounds.size.sqrMagnitude>0,"Native walking pose produces finite garment skin geometry");
        Destroy(poseMesh);DestroyInventoryVisual(npcProp);DestroyHuman(npc);
        Inventory.Validate();Check(PaleolithicClothing.Insulation(Inventory)>.85,"Native inventory supplies full cold outfit protection");
        inventoryPreviewYaw=25;RenderInventoryPreview();yield return Shot("clothes-outfit");SetInventoryOpen(false);
    }
}
