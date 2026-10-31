using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WorldFeedbackRevisionCheck
{
    public static void Run()
    {
        var lines=new List<string>();var library=Resources.Load<WorldGenHumanLibrary>("Humans/HumanLibrary");
        foreach(var definition in library.People)
        {
            var root=UnityEngine.Object.Instantiate(definition.Prefab);
            try
            {
                definition.Clips[Array.IndexOf(definition.Roles,"Idle")].SampleAnimation(root,.3f);
                foreach(float scale in new[]{1f,1.3f})
                {
                    root.transform.localScale=Vector3.one*scale;
                    foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var mesh=renderer.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes;var bones=renderer.bones;
                        if(weights.Length!=vertices.Length||bones.Length==0)continue;
                        var expected=new Vector3[vertices.Length];
                        for(int i=0;i<expected.Length;i++)
                        {
                            var w=weights[i];Vector3 p=Vector3.zero;
                            void Add(int b,float weight){if(weight>0)p+=bones[b].localToWorldMatrix.MultiplyPoint3x4(bind[b].MultiplyPoint3x4(vertices[i]))*weight;}
                            Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);expected[i]=p;
                        }
                        var errors=new double[2];
                        for(int mode=0;mode<2;mode++)
                        {
                            var baked=new Mesh();renderer.BakeMesh(baked,mode==1);var posed=baked.vertices;
                            errors[mode]=Enumerable.Range(0,posed.Length).Max(i=>(double)Vector3.Distance(expected[i],renderer.transform.TransformPoint(posed[i])));UnityEngine.Object.DestroyImmediate(baked);
                        }
                        lines.Add(definition.Variant+" scale="+scale+" "+renderer.name+" lossy="+renderer.transform.lossyScale+" falseError="+errors[0]+" trueError="+errors[1]);
                        if(errors[1]>.001)throw new InvalidOperationException("MPFB combat bake differs from bone-derived world skin: "+lines.Last());
                    }
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/world-feedback-20261031"));Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"skin-scale-editor.txt"),lines);Debug.Log("WORLD FEEDBACK SKIN SCALE AUDIT "+lines.Count);
    }
}
