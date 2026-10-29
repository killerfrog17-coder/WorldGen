using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class NatureSource {public ScatterInstance Plant;public Bounds Bounds;public ForageProduct[] Products;public string Name;}
    readonly List<NatureSource> natureSources=new List<NatureSource>();double natureRefreshAt;Vector3 natureAt;LocalTerrainFrame natureFrame;
    NatureSource natureMenu;Animal corpseMenu;Vector2 natureScroll;
    void RefreshNatureSources()
    {
        if(!Ready||!inventoryInitialized)return;Vector3 eye=view.transform.localPosition;
        if(natureFrame==frame&&Time.unscaledTimeAsDouble<natureRefreshAt&&(eye-natureAt).sqrMagnitude<1)return;
        natureRefreshAt=Time.unscaledTimeAsDouble+.4;natureAt=eye;natureFrame=frame;natureSources.Clear();
        foreach(var chunk in scatterChunks.Values)
        {
            if(chunk.Roots==null||chunk.AllBounds.SqrDistance(eye)>49)continue;
            foreach(var group in chunk.Groups)
            {
                if(group.Bounds.SqrDistance(eye)>49)continue;
                foreach(int i in group.Indices)
                {
                    var p=chunk.Source.Instances[i];if(p.Kind!=ScatterKind.MidPlant&&p.Kind!=ScatterKind.GeneratedTree&&!IsSpecialist(p.Kind))continue;
                    Vector3 root=chunk.Roots[i];if((root-eye).sqrMagnitude>49||CollectedRock(p))continue;
                    var flora=terrain.FloraRegion(walkingPatch.Sample(p.Position).Cell);ForageProduct[] products=Array.Empty<ForageProduct>();string name="";
                    if(p.Kind==ScatterKind.MidPlant){var s=flora.MidPlants.FirstOrDefault(s=>(s.MorphologyCode&~3)==(p.Variant&~3));if(s!=null){products=Foraging.Products(s,p.WidthM);name=s.Name;}}
                    else if(p.Kind==ScatterKind.GeneratedTree){var s=flora.Trees.FirstOrDefault(s=>s.MorphologyCode==(p.Variant&~7));if(s!=null){products=Foraging.Products(s);name=s.Name;}}
                    else{var s=flora.Specialists.FirstOrDefault(s=>s.MorphologyCode==p.Variant);if(s!=null){products=Foraging.Products(s);name=s.Name;}}
                    if(products.Length==0)continue;
                    bool tree=p.Kind==ScatterKind.GeneratedTree;float h=(float)Math.Min(tree?2:p.HeightM,2.5),w=tree?Mathf.Max(.35f,(float)p.WidthM*.12f):Mathf.Max(.12f,(float)p.WidthM);
                    natureSources.Add(new NatureSource{Plant=p,Products=products,Name=name,Bounds=new Bounds(root+Vector3.up*h*.5f,new Vector3(w,h,w))});
                }
            }
        }
    }
    NatureSource AimedNature(out float distance)
    {
        RefreshNatureSources();var ray=new Ray(view.transform.localPosition,view.transform.forward);NatureSource target=null;distance=3;
        foreach(var source in natureSources)
        {
            if(!source.Bounds.IntersectRay(ray,out float t)||t>distance||!InventoryReach(source.Bounds.ClosestPoint(ray.origin)))continue;
            distance=t;target=source;
        }
        return target;
    }
    Animal AimedCorpse(out float distance)
    {
        var ray=new Ray(view.transform.position,view.transform.forward);Animal target=null;distance=3;
        foreach(var animal in animals.Values)
        {
            if(!animal.Dead||animal.Retired||animal.Root==null||!animal.Root.activeInHierarchy)continue;
            if(!animal.Renderer.bounds.IntersectRay(ray,out float t)||t>distance||!CorpseReach(animal))continue;
            var shape=AnimalSurface(animal);if(!shape.Raycast(ray,PosedAnimal(animal),animal.Parts,distance,out float actual,out _,out _,out _))continue;
            target=animal;distance=actual;
        }
        return target;
    }
    bool CorpseReach(Animal a)=>a!=null&&a.Dead&&!a.Retired&&a.Root!=null&&InventoryReach(transform.InverseTransformPoint(a.Renderer.bounds.ClosestPoint(view.transform.position)));
    bool CorpseItemReach(Possession p)=>p.CorpseOwner!=0&&animals.TryGetValue(p.CorpseOwner,out var a)&&CorpseReach(a);
    bool TryNatureInteract(float itemDistance)
    {
        var plant=AimedNature(out float pd);var corpse=AimedCorpse(out float cd);
        if(corpse!=null&&cd<itemDistance&&(plant==null||cd<pd))
        {EnsureAnimalLoot(corpse);SetInventoryOpen(true);corpseMenu=corpse;natureMenu=null;natureScroll=Vector2.zero;return true;}
        if(plant!=null&&pd<itemDistance){SetInventoryOpen(true);natureMenu=plant;corpseMenu=null;natureScroll=Vector2.zero;return true;}
        return false;
    }
    void EnsureAnimalLoot(Animal animal)
    {
        if(!animal.Dead)return;var record=ToolResource("loot-"+animal.Spawn.Id.ToString("x16"));if(record.Harvests>0)return;
        var p=animal.Spawn.Profile;
        // Tiny ambient invertebrates are not skinnable mammals. The same body
        // identity seeds all contents and a saved ledger prevents re-looting.
        bool bat=p.slug=="fruit-bat"||p.slug=="insectivorous-bat";
        bool marineMammal=new[]{"dolphin","dugong","orca","sea-lion","sea-otter","seal","walrus","whale"}.Contains(p.slug);
        bool reptile=new[]{"native-small-lizards-guild","crocodile","freshwater-turtle","monitor-lizard","sea-turtle","venomous-snake"}.Contains(p.slug);
        var appearance=animal.Model.Asset.Variations.SelectMany(c=>c.options).FirstOrDefault(o=>o.parts.Length==4&&Enumerable.Range(0,4).All(i=>o.parts[i]==(int)animal.Parts[i]));
        string feature=(appearance?.label??"").ToLowerInvariant();bool antlers=(feature.Contains("antler")||feature.Contains("rack"))&&!feature.Contains("none")&&!feature.Contains("female")&&!feature.Contains("no antler")&&!feature.Contains("antlerless");
        if(animal.MassKg>=.005&&FaunaBlood.Kind(p.slug)==FaunaBloodKind.Red)
            Inventory.Items.AddRange(Foraging.AnimalLoot(animal.Spawn.Id,p.slug,p.medium=="water"&&!marineMammal&&!reptile?"water":"ground",animal.MassKg,animal.Planet,animal.Coat,unchecked((int)animal.Spawn.Hash),p.medium=="air"&&!bat,bat||marineMammal||p.medium=="ground"&&!reptile,antlers));
        record.Harvests=1;RememberAnimal(animal,true);
    }
    string ForageLedgerKey(NatureSource source,ForageProduct product)=>"forage-"+source.Plant.Id.ToString("x16")+"-"+product.Kind;
    int ForageYear=>(int)Math.Floor(skyYear);
    bool ForageReady(NatureSource source,ForageProduct product,out string reason)
    {
        TerrainMapViewport.Degrees(source.Plant.Position,out double latitude,out _);
        if(!InventoryReach(source.Bounds.ClosestPoint(view.transform.localPosition))){reason="Out of reach.";return false;}
        if(!product.Available(skyYear,latitude,CurrentWeather.TemperatureC)){reason="Not in season.";return false;}
        if(product.Kind==PossessionKind.EdibleRoot&&!CraftAccessible().Any(p=>p.Kind==PossessionKind.DiggingStick&&ItemUseRules.Condition(p)>0)){reason="A digging stick is needed.";return false;}
        var record=Inventory.ToolResources.FirstOrDefault(r=>r.Id==ForageLedgerKey(source,product));
        if(record!=null&&record.HarvestYear==ForageYear&&record.Harvests>0){reason="Already gathered this year.";return false;}
        reason="";return true;
    }
    void GatherPlant(NatureSource source,ForageProduct product)
    {
        if(!ForageReady(source,product,out string reason)){InventoryFeedback(reason);return;}
        if(product.Kind==PossessionKind.EdibleRoot)
        {
            var tool=CraftAccessible().First(p=>p.Kind==PossessionKind.DiggingStick&&ItemUseRules.Condition(p)>0);
            BeginWorldToolWork(tool,"Digging out a root",120,()=>CompleteGatherPlant(source,product));SetInventoryOpen(false);return;
        }
        CompleteGatherPlant(source,product);
    }
    void CompleteGatherPlant(NatureSource source,ForageProduct product)
    {
        var record=ToolResource(ForageLedgerKey(source,product));record.HarvestYear=ForageYear;record.Harvests=1;
        ReceiveToolMaterial(Foraging.Gather(product,Position));PlayItemEffect("fibre-handle",view.transform.position,.25f,true);
        // Picking the visible mushroom removes that fruiting body. Bushes/reeds
        // stay standing after gathering a modest accessible bundle/fruit portion.
        if(source.Plant.Kind==ScatterKind.Fungus){inventoryCollected.Add(source.Plant.Id);Inventory.CollectedNatural.Add(source.Plant.Id);scatterDirty=true;natureRefreshAt=0;}
    }
    bool TakeCorpseItem(Possession item,bool notify=true)
    {
        if(item.Place!=PossessionPlace.Corpse||!CorpseItemReach(item))return false;
        if(!Inventory.Stow(item.Id,out _)&&!Inventory.Hold(item.Id,out _)){if(notify)InventoryFeedback("No free hand or suitable carrier space.");return false;}
        inventoryNearbyAt=0;if(notify)RenderInventoryPreview();return true;
    }
    void EatPossession(Possession item,bool sample)
    {
        RefreshInventoryNearby(true);if(!Foraging.Food(item)||!AccessiblePossession(item)||Inventory.Crafts.Any(j=>j.Running&&PortableCrafting.Affects(j,item.Id)))return;
        double eaten=Foraging.Consume(Vitals,item,sample);if(eaten<=0)return;
        PlayBodyCue("eat");
        if(item.MassKg<.00001){Inventory.Items.Remove(item);}
        inventoryContext=null;inventoryNearbyAt=0;
        InventoryFeedback(sample?"Tasted a small portion. Taste and the absence of immediate symptoms do not establish safety.":"Ate "+Math.Round(eaten*1000)+" g of "+item.Name.ToLowerInvariant()+".");
    }
    bool DrawNatureMenu(float scale)
    {
        if(natureMenu==null&&corpseMenu==null)return false;
        if(corpseMenu!=null&&!CorpseReach(corpseMenu)||natureMenu!=null&&!InventoryReach(natureMenu.Bounds.ClosestPoint(view.transform.localPosition))){natureMenu=null;corpseMenu=null;return false;}
        var rect=new Rect(Screen.width*.54f,Screen.height*.16f,Mathf.Min(540*scale,Screen.width*.43f),Screen.height*.68f);InventoryFill(rect,new Color(.035f,.055f,.06f,.97f));InventoryOutline(rect,InventoryGold);
        GUILayout.BeginArea(new Rect(rect.x+16*scale,rect.y+12*scale,rect.width-32*scale,rect.height-24*scale));
        GUILayout.Label(corpseMenu!=null?corpseMenu.Spawn.Profile.name+" — corpse":natureMenu.Name,new GUIStyle(GUI.skin.label){fontSize=Mathf.RoundToInt(22*scale)});
        GUILayout.Label("Carried: "+Inventory.CarriedMassKg.ToString("0.0")+" kg");natureScroll=GUILayout.BeginScrollView(natureScroll);
        if(corpseMenu!=null)
        {
            var items=Inventory.Items.Where(p=>p.Place==PossessionPlace.Corpse&&p.CorpseOwner==corpseMenu.Spawn.Id).ToArray();
            if(items.Length==0)GUILayout.Label("Empty.");
            else if(GUILayout.Button("Take all that fits",GUILayout.MinHeight(34*scale)))
            {int taken=0;foreach(var item in items)if(TakeCorpseItem(item,false))taken++;if(taken>0)RenderInventoryPreview();InventoryFeedback(taken==0?"No free hand or suitable carrier space.":"Collected "+taken+" items; anything that did not fit remains in the corpse.");}
            foreach(var item in items)
                if(GUILayout.Button("Take "+item.Name+"   ·   "+item.MassKg.ToString("0.###")+" kg",GUILayout.MinHeight(34*scale)))TakeCorpseItem(item);
        }
        else foreach(var product in natureMenu.Products)
        {
            bool ready=ForageReady(natureMenu,product,out string reason);bool previous=GUI.enabled;GUI.enabled=previous&&ready;
            string label=product.Kind==PossessionKind.RawFibre?"Gather fibrous stems":product.Kind==PossessionKind.ReedBundle?"Gather reeds":"Gather "+(PortableTechnology.Item(product.Kind)?.Name??product.Kind.ToString()).ToLowerInvariant();
            if(GUILayout.Button(new GUIContent(label,reason),GUILayout.MinHeight(36*scale)))GatherPlant(natureMenu,product);GUI.enabled=previous;
        }
        GUILayout.EndScrollView();if(!string.IsNullOrEmpty(GUI.tooltip))GUILayout.Label(GUI.tooltip);
        if(GUILayout.Button("Back to inventory",GUILayout.Height(32*scale))){natureMenu=null;corpseMenu=null;RefreshInventoryNearby(true);}
        if(GUILayout.Button("Close",GUILayout.Height(32*scale)))SetInventoryOpen(false);
        GUILayout.EndArea();return true;
    }
    string NaturePrompt(float itemDistance)
    {
        var plant=AimedNature(out float pd);var corpse=AimedCorpse(out float cd);
        if(corpse!=null&&cd<itemDistance&&(plant==null||cd<pd))return "Search "+corpse.Spawn.Profile.name+"  [E]";
        if(plant!=null&&pd<itemDistance)return plant.Name+"  [E]";return null;
    }
}
