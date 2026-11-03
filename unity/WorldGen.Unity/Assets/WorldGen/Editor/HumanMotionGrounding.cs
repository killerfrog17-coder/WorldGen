using System.Collections.Generic;
using System.Linq;
using UnityEngine;

static class HumanMotionGrounding
{
    public static float Offset(GameObject prefab,AnimationClip clip,GameObject adult=null,float ratio=1)
    {
        var root=Object.Instantiate(prefab);var renderers=root.GetComponentsInChildren<SkinnedMeshRenderer>();var rest=new Dictionary<Transform,Vector3>();
        var old=adult==null?null:adult.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First().localPosition);
        if(old!=null)foreach(var bone in renderers.SelectMany(r=>r.bones).Where(t=>t!=null).Distinct())rest[bone]=bone.localPosition;
        var minimum=new List<float>();var mesh=new Mesh();
        try
        {
            foreach(float t in new[]{.08f,.28f,.5f,.72f,.92f})
            {
                clip.SampleAnimation(root,clip.length*t);
                if(old!=null)foreach(var pair in rest)pair.Key.localPosition=pair.Key.name=="pelvis"?pair.Value+(pair.Key.localPosition-old[pair.Key.name])*ratio:pair.Value;
                float low=float.PositiveInfinity;
                foreach(var renderer in renderers)
                {renderer.BakeMesh(mesh,true);foreach(var vertex in mesh.vertices)low=Mathf.Min(low,root.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex)).y);}
                minimum.Add(low);
            }
            // Support the body surface, including hands/hips while lying down.
            // This is measured offline; no skin baking occurs in gameplay.
            return Mathf.Clamp(-minimum.Min()+.008f,-.25f,.4f);
        }
        finally{Object.DestroyImmediate(mesh);Object.DestroyImmediate(root);}
    }
}
