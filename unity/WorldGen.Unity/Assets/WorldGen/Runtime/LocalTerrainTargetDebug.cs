using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    struct TargetCandidate {public ScatterInstance Plant;public Bounds Bounds;public float Distance;}
    readonly List<TargetCandidate> targetCandidates=new List<TargetCandidate>();
    readonly Dictionary<ulong,string> targetNames=new Dictionary<ulong,string>();
    readonly RaycastHit[] targetEntityHits=new RaycastHit[16];
    string targetDisplay="Looking at · —";double targetRefreshAt,targetNameAt;LocalTerrainFrame targetFrame;Vector3 targetOrigin;
    void UpdateTargetReadout(double now)
    {
        if(view==null)return;
        var eye=view.transform.localPosition;var ray=new Ray(eye,view.transform.forward);
        // Broad candidate collection is slower; fast aiming never scans the whole forest.
        if(now>=targetRefreshAt||targetFrame!=frame||(eye-targetOrigin).sqrMagnitude>64)
        {
            targetRefreshAt=now+.25;targetFrame=frame;targetOrigin=eye;targetCandidates.Clear();
            if(ScatterEnabled)foreach(var chunk in scatterChunks.Values)
            {
                if(chunk.Bounds==null||chunk.AllBounds.SqrDistance(eye)>80*80)continue;
                foreach(var group in chunk.Groups)
                {
                    if(group.Bounds.SqrDistance(eye)>80*80)continue;
                    foreach(int i in group.Indices)
                    {
                        var p=chunk.Source.Instances[i];if(CollectedRock(p))continue;
                        if(IsSpecialist(p.Kind)&&!TerrainVideoOptions.SpecialistPlants||p.Kind==ScatterKind.MidPlant&&!TerrainVideoOptions.MidPlants||p.Kind==ScatterKind.GeneratedTree&&!TerrainVideoOptions.GeneratedTrees||!TerrainVideoOptions.KeepVegetation(p.Kind,p.Id))continue;
                        float distance=chunk.Bounds[i].SqrDistance(eye);if(distance>80*80)continue;
                        if(TerrainVideoOptions.VegetationBlend(p.Kind,p.WidthM,Math.Sqrt(distance)).Visibility<=0)continue;
                        // Rooted body envelope, excluding the large wind/culling padding.
                        var root=chunk.Roots[i];float h=(float)p.HeightM,w=(float)p.WidthM;
                        targetCandidates.Add(new TargetCandidate{Plant=p,Distance=distance,Bounds=new Bounds(root+Vector3.up*h*.5f,new Vector3(Math.Max(.08f,w),Math.Max(.08f,h),Math.Max(.08f,w)))});
                    }
                }
            }
            targetCandidates.Sort((a,b)=>a.Distance.CompareTo(b.Distance));
            if(targetCandidates.Count>256)targetCandidates.RemoveRange(256,targetCandidates.Count-256);
        }
        float closest=80;TargetCandidate? selected=null;string entity=null;
        foreach(var candidate in targetCandidates)if(candidate.Bounds.IntersectRay(ray,out float t)&&t<closest){closest=t;selected=candidate;}
        int hits=Physics.RaycastNonAlloc(view.transform.position,view.transform.forward,targetEntityHits,closest,view.cullingMask,QueryTriggerInteraction.Ignore);
        for(int i=0;i<hits;i++)
        {
            var hit=targetEntityHits[i];var identity=hit.collider.GetComponentInParent<WorldGenSpeciesIdentity>();
            if(identity!=null&&hit.distance<closest){closest=hit.distance;entity=identity.SpeciesName+" · "+identity.SpeciesType;selected=null;}
        }
        // Native animal skins and item meshes are drawn without species colliders.
        // Their actual posed surfaces must participate, including saved corpses.
        var worldRay=new Ray(view.transform.position,view.transform.forward);
        string dynamicName=TargetActorOrItem(worldRay,80,out float dynamicDistance);
        if(dynamicName!=null)
        {
            bool blocked=dynamicDistance>=closest&&entity!=null;
            foreach(var candidate in targetCandidates)
            {
                var p=candidate.Plant;
                bool tree=p.Kind==ScatterKind.GeneratedTree||p.Kind==ScatterKind.Broadleaf||p.Kind==ScatterKind.Conifer||p.Kind==ScatterKind.SavannaTree;
                if(tree)
                {
                    float trunk=(float)p.WidthM*(p.Kind==ScatterKind.GeneratedTree?(float)new TreeMorphology(p.Variant).TrunkRadius:.035f);
                    Vector3 root=candidate.Bounds.center-Vector3.up*(float)p.HeightM*.5f;
                    if(RayTrunk(ray,root,Mathf.Max(.015f,trunk),(float)p.HeightM*.88f,dynamicDistance,out _,out _)){blocked=true;break;}
                }
                else if((p.Kind==ScatterKind.Boulder||p.Kind==ScatterKind.Outcrop||p.Kind==ScatterKind.Stone)&&candidate.Bounds.IntersectRay(ray,out float t)&&t<dynamicDistance){blocked=true;break;}
            }
            // A canopy's loose bounding box is not an opaque wall below its leaves.
            if(!blocked){closest=dynamicDistance;entity=dynamicName;selected=null;}
        }
        // Only resident height grids, never generation queries, for line-of-sight.
        for(float t=1;t<closest;t+=2)
        {
            var at=ray.GetPoint(t);if(VisibleGround(at.x+originX,at.z+originZ,double.NegativeInfinity)>at.y+.2){selected=null;entity=null;break;}
        }
        if(entity!=null){targetDisplay="Looking at · "+entity;return;}
        if(!selected.HasValue){targetDisplay="Looking at · —";return;}
        var plant=selected.Value.Plant;
        if(!targetNames.TryGetValue(plant.Id,out string name))
        {
            if(now<targetNameAt){targetDisplay="Looking at · "+plant.Kind;return;}
            targetNameAt=now+.25;name=ResolveTargetName(plant);
            if(targetNames.Count>=256)targetNames.Clear();targetNames[plant.Id]=name;
        }
        targetDisplay="Looking at · "+name;
    }
    string ResolveTargetName(ScatterInstance plant)
    {
        if(plant.Kind==ScatterKind.Stone||plant.Kind==ScatterKind.Boulder||plant.Kind==ScatterKind.Branch)
        {
            var item=RockPossession(plant);
            if(item.MassKg<=25&&(plant.Kind==ScatterKind.Branch||plant.WidthM<=.55&&plant.HeightM<=.45))return InventoryDisplayName(item)+" · ground item";
        }
        if(plant.Kind!=ScatterKind.GeneratedTree&&plant.Kind!=ScatterKind.MidPlant&&!IsSpecialist(plant.Kind))return plant.Kind.ToString();
        // Identity is resolved only for a new aimed-at plant, not every fast HUD tick.
        var patch=terrain.MapPatch(plant.Position,2048/HorizontalScale);var flora=terrain.FloraRegion(patch.Sample(plant.Position).Cell);
        if(plant.Kind==ScatterKind.GeneratedTree)foreach(var s in flora.Trees)if(s.MorphologyCode==(plant.Variant&~7))return s.Name+" · tree / "+s.Form;
        if(plant.Kind==ScatterKind.MidPlant)foreach(var s in flora.MidPlants)if((s.MorphologyCode&~3)==(plant.Variant&~3))return s.Name+" · mid-height / "+s.Form;
        if(IsSpecialist(plant.Kind))foreach(var s in flora.Specialists)if(s.MorphologyCode==plant.Variant)return s.Name+" · "+s.Form;
        return plant.Kind+" · regional identity unavailable";
    }
    string TargetActorOrItem(Ray ray,float maximum,out float distance)
    {
        distance=maximum;string label=null;
        foreach(var animal in animals.Values)
        {
            if(animal.Retired||animal.Root==null||!animal.Root.activeInHierarchy||animal.Fade<.15f||!animal.Renderer.enabled)continue;
            if(!animal.Renderer.bounds.IntersectRay(ray,out float entry)||entry>distance)continue;
            var surface=AnimalSurface(animal);
            if(!surface.Raycast(ray,PosedAnimal(animal),animal.Parts,distance,out float t,out _,out _,out _))continue;
            distance=t;string name=animal.Spawn.Profile.name;
            if(animal.Spawn.Taxon>=0&&animal.Spawn.Taxon<sourceWorld.Ecology.Taxa.Length)name=sourceWorld.Ecology.Taxa[animal.Spawn.Taxon].Name+" ("+name+")";
            label=name+(animal.Dead?" · corpse":" · animal · "+animal.Health.ToString("0.#")+"/"+animal.MaximumHealth.ToString("0.#")+" HP");
        }
        foreach(var human in humanNpcs)
        {
            if(human.Root==null||!human.Root.activeInHierarchy)continue;
            foreach(var renderer in human.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(!renderer.enabled||!renderer.bounds.IntersectRay(ray,out float entry)||entry>distance)continue;
                var surface=PosedHuman(renderer);
                for(int face=0;face<surface.Triangles.Length;face+=3)
                {
                    int a=surface.Triangles[face],b=surface.Triangles[face+1],c=surface.Triangles[face+2];
                    if(!FaunaCollisionSurface.RayTriangle(ray,surface.Vertices[a],surface.Vertices[b],surface.Vertices[c],distance,out float t,out _,out _))continue;
                    distance=t;label="Human · "+(human.Health<=0?"corpse":"NPC · "+human.Health.ToString("0.#")+"/100 HP");
                }
            }
        }
        foreach(var item in Inventory.Items)
        {
            if(item.Place!=PossessionPlace.Ground||!inventoryVisuals.TryGetValue(item.Id,out var root)||root==null||!root.activeInHierarchy)continue;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if(!renderer.enabled||!renderer.bounds.IntersectRay(ray,out float t)||t>=distance)continue;
                distance=t;label=InventoryDisplayName(item)+" · "+(item.InFlight?"in flight":"ground item");
            }
        }
        return label==null?null:label+" · "+distance.ToString("0.#")+" m";
    }
}
