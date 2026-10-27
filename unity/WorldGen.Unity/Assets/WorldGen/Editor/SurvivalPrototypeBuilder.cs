using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WorldGen.Core;

public static class SurvivalPrototypeBuilder
{
    public static void Validate()
    {
        var state=new SurvivalState();state.Validate();
        string path=Path.Combine(Application.streamingAssetsPath,"fauna-rigs");
        if(Directory.GetFiles(path,"*.json").Length!=177)throw new InvalidDataException("Expected 177 original ragdoll skeleton sidecars.");
        foreach(var recipe in SurvivalCrafting.Recipes)foreach(var material in recipe.Materials)SurvivalItems.Get(material.item);
        Debug.Log("SURVIVAL STATIC READY: "+SurvivalItems.All.Count()+" items, "+SurvivalCrafting.Recipes.Length+" recipes, "+SurvivalCrafting.Shelters.Length+" shelter/camp designs, 177 original skeletons.");
    }
    public static void Prepare()
    {HumanRuntimeBuilder.Build();Validate();AssetDatabase.SaveAssets();}
    public static void CheckMeshLifetime()
    {
        var oppositeFaces=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up},triangles=new[]{0,2,1,0,1,2}};oppositeFaces.RecalculateNormals();
        if(oppositeFaces.normals.Any(n=>n.sqrMagnitude>.0001f))throw new InvalidOperationException("Expected the original duplicate-face normal cancellation regression.");
        UnityEngine.Object.DestroyImmediate(oppositeFaces);
        Mesh CreatePanel()
        {
            var m=LocalTerrainExplorer.CreateSurvivalShelterPanel(Vector3.zero,Vector3.right,Vector3.up);
            if(m.triangles.Length!=3||m.normals.Any(n=>n.sqrMagnitude<.99f))throw new InvalidOperationException("Shelter panel normals must not cancel between opposite shared faces.");
            return m;
        }
        var oldRoot=new GameObject("Unowned mesh regression");var unowned=CreatePanel();oldRoot.AddComponent<MeshFilter>().sharedMesh=unowned;
        UnityEngine.Object.DestroyImmediate(oldRoot);
        if(unowned==null)throw new InvalidOperationException("Expected the old unowned-mesh lifetime regression.");
        UnityEngine.Object.DestroyImmediate(unowned);
        for(int i=0;i<20;i++)
        {
            var root=new GameObject("Owned streamed shelter");var generated=CreatePanel();root.AddComponent<MeshFilter>().sharedMesh=generated;root.AddComponent<SurvivalOwnedMeshes>().Register(generated);
            var sharedPart=GameObject.CreatePrimitive(PrimitiveType.Cube);sharedPart.transform.SetParent(root.transform,false);var shared=sharedPart.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(root);
            if(generated!=null||shared==null)throw new InvalidOperationException("Streamed mesh lifetime: generated alive="+(generated!=null)+", shared alive="+(shared!=null));
        }
        Debug.Log("SURVIVAL MESH LIFETIME PASS: unowned-mesh and duplicate-face-normal regressions reproduced; 20 unload cycles release generated meshes and retain shared primitives.");
    }
}
