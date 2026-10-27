using System.Collections.Generic;
using UnityEngine;

// Own only meshes created for this streamed object. Built-in/shared meshes,
// fauna assets and cached materials must never be registered here.
[ExecuteAlways]
public sealed class SurvivalOwnedMeshes : MonoBehaviour
{
    readonly List<Mesh> meshes=new List<Mesh>();
    public void Register(Mesh mesh){if(mesh!=null&&!meshes.Contains(mesh))meshes.Add(mesh);}
    void OnDestroy()
    {
        foreach(var mesh in meshes)if(mesh!=null)
        {
            if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);
        }
        meshes.Clear();
    }
}
