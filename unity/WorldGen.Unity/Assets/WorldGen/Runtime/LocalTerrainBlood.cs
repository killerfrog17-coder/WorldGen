using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    static Color BloodPigment(string slug)
    {
        bool Has(params string[] words)=>words.Any(w=>slug.IndexOf(w,StringComparison.OrdinalIgnoreCase)>=0);
        if(Has("spider","crab","lobster","snail","octopus","squid","clam","mussel"))return new Color(.16f,.27f,.27f);
        if(Has("butterfl","moth","beetle","mosquito","midge","fly","ant-","ants-","bee","wasp","termite","grasshopper","cricket","katydid","cicada","millipede","centipede"))return new Color(.33f,.29f,.12f);
        return new Color(.23f,.012f,.009f);
    }
    void RestoreAnimalBlood(Animal animal)
    {
        if(wildlifeRecords.TryGetValue(animal.Spawn.Id,out var record)&&record.BloodMask!=null)
        {animal.Blood=new WorldGenBloodCanvas(record.BloodMask);BindAnimalBlood(animal);}
    }
    void BindAnimalBlood(Animal animal)
    {animal.Props.SetFloat("_BloodEnabled",1);animal.Props.SetTexture("_BloodMask",animal.Blood.Texture);animal.Props.SetColor("_BloodColor",BloodPigment(animal.Spawn.Profile.slug));animal.Renderer.SetPropertyBlock(animal.Props);}
    partial void PaintAnimalBlood(Animal animal,Vector3 point,Vector3 normal,double damage)
    {
        var surface=AnimalSurface(animal);var vertices=PosedAnimal(animal);float radius=Mathf.Min((float)animal.Spawn.Profile.lengthM*.22f,Mathf.Clamp(.035f+Mathf.Sqrt((float)damage)*.009f,.035f,.17f));
        if(animal.Blood==null)animal.Blood=new WorldGenBloodCanvas();
        animal.Blood.Stamp(vertices,surface.Uv,surface.Triangles,point,normal,radius,i=>surface.Visible(i,animal.Parts));BindAnimalBlood(animal);
    }
    partial void CaptureAnimalBlood(Animal animal,WildlifeIndividual saved){saved.BloodMask=animal.Blood?.Capture();}
    partial void ClearAnimalBlood(Animal animal){animal.Blood?.Dispose();animal.Blood=null;}
    void PaintHumanBlood(HumanActor actor,CombatHit hit,double damage)
    {
        if(hit?.HumanRenderer==null)return;
        if(!actor.Blood.TryGetValue(hit.HumanRenderer,out var canvas)){canvas=new WorldGenBloodCanvas();actor.Blood.Add(hit.HumanRenderer,canvas);}
        canvas.Stamp(hit.Vertices,hit.Uvs,hit.Triangles,hit.Point,hit.Normal,Mathf.Clamp(.035f+Mathf.Sqrt((float)damage)*.009f,.035f,.16f));
        BindHumanBlood(hit.HumanRenderer,canvas);
    }
    void BindHumanBlood(SkinnedMeshRenderer renderer,WorldGenBloodCanvas canvas)
    {var props=new MaterialPropertyBlock();renderer.GetPropertyBlock(props);props.SetFloat("_BloodEnabled",1);props.SetTexture("_BloodMask",canvas.Texture);props.SetColor("_BloodColor",BloodPigment("human"));renderer.SetPropertyBlock(props);}
    void CapturePlayerBlood()
    {
        if(humanPlayer==null)return;Vitals.Blood=humanPlayer.Blood.Select(p=>new PlayerBloodLayer{Renderer=p.Key.name,Mask=p.Value.Capture()}).ToList();
    }
    void RestorePlayerBlood()
    {
        if(humanPlayer==null||Vitals.Blood==null)return;
        foreach(var renderer in humanPlayer.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {var saved=Vitals.Blood.FirstOrDefault(b=>b.Renderer==renderer.name);if(saved!=null){var canvas=new WorldGenBloodCanvas(saved.Mask);humanPlayer.Blood[renderer]=canvas;BindHumanBlood(renderer,canvas);}}
    }
    partial void ClearHumanBlood(HumanActor actor)
    {
        foreach(var pair in actor.Blood){if(pair.Key!=null){var props=new MaterialPropertyBlock();pair.Key.GetPropertyBlock(props);props.SetFloat("_BloodEnabled",0);pair.Key.SetPropertyBlock(props);}pair.Value.Dispose();}actor.Blood.Clear();
    }
}
