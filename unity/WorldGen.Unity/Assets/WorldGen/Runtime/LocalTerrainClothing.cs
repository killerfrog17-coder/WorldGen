using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class GarmentVisual
    {
        public SkinnedMeshRenderer Skin,Source;
        public MeshRenderer[] Folded;
        public SkinnedMeshRenderer[] HoodHair=Array.Empty<SkinnedMeshRenderer>();
        public bool[] HairEnabled=Array.Empty<bool>();
    }
    readonly Dictionary<GameObject,GarmentVisual> garmentVisuals=new Dictionary<GameObject,GarmentVisual>();
    readonly Dictionary<(Mesh,PossessionKind),Mesh> garmentMeshes=new Dictionary<(Mesh,PossessionKind),Mesh>();
    bool GarmentBone(PossessionKind kind,string name)
    {
        if(kind==PossessionKind.HideHood)return name.StartsWith("head")||name.StartsWith("neck");
        if(kind==PossessionKind.HideFootwear)return name.StartsWith("foot")||name.StartsWith("ball")||name.StartsWith("toe");
        if(kind==PossessionKind.HideLeggings)return name.StartsWith("pelvis")||name.StartsWith("thigh")||name.StartsWith("calf");
        bool torso=name.StartsWith("spine")||name.StartsWith("pelvis")||name.StartsWith("clavicle");
        return torso||kind!=PossessionKind.HideWrap&&(name.StartsWith("upperarm")||name.StartsWith("lowerarm"));
    }
    Mesh GarmentMesh(SkinnedMeshRenderer source,PossessionKind kind,Transform body)
    {
        var key=(source.sharedMesh,kind);if(garmentMeshes.TryGetValue(key,out var cached))return cached;
        var original=source.sharedMesh;var vertices=original.vertices;var normals=original.normals;var weights=original.boneWeights;var bones=source.bones;
        if(weights.Length!=vertices.Length)throw new InvalidOperationException("Garment source has no native skin weights: "+source.name);
        float height=original.bounds.size.y,thickness=height*(kind==PossessionKind.HideHood?.023f:kind==PossessionKind.FurCoat?.018f:.008f);
        var forward=source.transform.InverseTransformDirection(body.forward).normalized;
        bool Included(int index)
        {
            var weight=weights[index];int bone=weight.boneIndex0;float largest=weight.weight0;
            if(weight.weight1>largest){largest=weight.weight1;bone=weight.boneIndex1;}
            if(weight.weight2>largest){largest=weight.weight2;bone=weight.boneIndex2;}
            if(weight.weight3>largest)bone=weight.boneIndex3;
            if(bone<0||bone>=bones.Length||bones[bone]==null||!GarmentBone(kind,bones[bone].name))return false;
            // An open face, rather than a skin-tight fur mask.
            return kind!=PossessionKind.HideHood||vertices[index].y>original.bounds.max.y-height*.035f||Vector3.Dot(normals[index],forward)<.15f;
        }
        var included=Enumerable.Range(0,vertices.Length).Select(Included).ToArray();var mapping=new Dictionary<int,int>();
        var positions=new List<Vector3>();var ns=new List<Vector3>();var skinWeights=new List<BoneWeight>();var uv=new List<Vector2>();var tags=new List<Vector2>();var colours=new List<Color>();var triangles=new List<int>();
        int Vertex(int old)
        {
            if(mapping.TryGetValue(old,out int id))return id;id=positions.Count;mapping.Add(old,id);
            positions.Add(vertices[old]+normals[old]*thickness);ns.Add(normals[old]);skinWeights.Add(weights[old]);
            uv.Add(new Vector2(.5f+Mathf.Atan2(vertices[old].z,vertices[old].x)/(Mathf.PI*2),(vertices[old].y-original.bounds.min.y)/Mathf.Max(.001f,height)));
            tags.Add(new Vector2(4,0));colours.Add(new Color(.69f,.53f,.35f,1));return id;
        }
        var indices=original.triangles;
        for(int i=0;i<indices.Length;i+=3)
        {
            int a=indices[i],b=indices[i+1],c=indices[i+2];if(!included[a]||!included[b]||!included[c])continue;
            triangles.Add(Vertex(a));triangles.Add(Vertex(b));triangles.Add(Vertex(c));
        }
        if(triangles.Count<3)throw new InvalidOperationException("Garment mask found no body surface: "+kind+" / "+source.name);
        var mesh=new Mesh{name="Paleolithic garment "+kind,indexFormat=positions.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16};
        mesh.SetVertices(positions);mesh.SetNormals(ns);mesh.SetUVs(0,uv);mesh.SetUVs(1,tags);mesh.SetColors(colours);mesh.boneWeights=skinWeights.ToArray();mesh.bindposes=original.bindposes;mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        garmentMeshes.Add(key,mesh);return mesh;
    }
    void PoseGarment(GameObject root,Possession item,Transform body)
    {
        if(!garmentVisuals.TryGetValue(root,out var visual))
        {
            var source=body.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMesh!=null&&!r.sharedMesh.name.StartsWith("Paleolithic garment ")).OrderByDescending(r=>r.sharedMesh.vertexCount).FirstOrDefault();
            if(source==null)return;
            var child=new GameObject("Fitted hide garment");child.layer=root.layer;child.transform.SetParent(root.transform,false);
            var renderer=child.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=GarmentMesh(source,item.Kind,body);renderer.bones=source.bones;renderer.rootBone=source.rootBone;renderer.sharedMaterial=PortableMaterial();renderer.localBounds=source.localBounds;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;renderer.receiveShadows=true;
            visual=new GarmentVisual{Skin=renderer,Source=source,Folded=root.GetComponentsInChildren<MeshRenderer>()};garmentVisuals.Add(root,visual);
            if(item.Kind==PossessionKind.HideHood)
            {
                visual.HoodHair=body.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>GarmentHair(r.name)).ToArray();
                visual.HairEnabled=visual.HoodHair.Select(r=>r.enabled).ToArray();
            }
        }
        foreach(var folded in visual.Folded)folded.enabled=false;
        foreach(var hair in visual.HoodHair)if(hair!=null)hair.enabled=false;
        visual.Skin.enabled=true;root.transform.SetPositionAndRotation(visual.Source.transform.position,visual.Source.transform.rotation);
        var size=visual.Source.transform.lossyScale;var parent=root.transform.parent==null?Vector3.one:root.transform.parent.lossyScale;
        root.transform.localScale=new Vector3(size.x/parent.x,size.y/parent.y,size.z/parent.z);RefreshPortableRoot(root,item);
    }
    void RestoreFoldedGarment(GameObject root)
    {
        if(!garmentVisuals.TryGetValue(root,out var visual))return;
        visual.Skin.enabled=false;foreach(var folded in visual.Folded)folded.enabled=true;
        for(int i=0;i<visual.HoodHair.Length;i++)if(visual.HoodHair[i]!=null)visual.HoodHair[i].enabled=visual.HairEnabled[i];
    }
    static bool GarmentHair(string name)
    {name=name.ToLowerInvariant();return name.Contains("hair")||name.Contains("ponytail")||name.Contains("short0");}
    void DisposeGarments()
    {garmentVisuals.Clear();foreach(var mesh in garmentMeshes.Values)if(mesh!=null)Destroy(mesh);garmentMeshes.Clear();}
}
