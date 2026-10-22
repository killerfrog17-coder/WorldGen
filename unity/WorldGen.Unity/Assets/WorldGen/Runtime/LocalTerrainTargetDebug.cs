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
                        var p=chunk.Source.Instances[i];
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
        if(plant.Kind!=ScatterKind.GeneratedTree&&plant.Kind!=ScatterKind.MidPlant&&!IsSpecialist(plant.Kind))return plant.Kind.ToString();
        // Identity is resolved only for a new aimed-at plant, not every fast HUD tick.
        var patch=terrain.MapPatch(plant.Position,2048/HorizontalScale);var flora=terrain.FloraRegion(patch.Sample(plant.Position).Cell);
        if(plant.Kind==ScatterKind.GeneratedTree)foreach(var s in flora.Trees)if(s.MorphologyCode==(plant.Variant&~7))return s.Name+" · tree / "+s.Form;
        if(plant.Kind==ScatterKind.MidPlant)foreach(var s in flora.MidPlants)if((s.MorphologyCode&~3)==(plant.Variant&~3))return s.Name+" · mid-height / "+s.Form;
        if(IsSpecialist(plant.Kind))foreach(var s in flora.Specialists)if(s.MorphologyCode==plant.Variant)return s.Name+" · "+s.Form;
        return plant.Kind+" · regional identity unavailable";
    }
}
