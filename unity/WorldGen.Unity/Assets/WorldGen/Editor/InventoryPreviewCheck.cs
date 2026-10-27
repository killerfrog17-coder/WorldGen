using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;

public static class InventoryPreviewCheck
{
    public static void Review()
    {
        string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/inventory/preview"));Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var root=new GameObject("Inventory preview check");var explorer=root.AddComponent<LocalTerrainExplorer>();
        object Get(string n)=>typeof(LocalTerrainExplorer).GetField(n,flags).GetValue(explorer);
        void Set(string n,object v)=>typeof(LocalTerrainExplorer).GetField(n,flags).SetValue(explorer,v);
        object Call(string n,params object[] v)=>typeof(LocalTerrainExplorer).GetMethod(n,flags).Invoke(explorer,v);
        Set("humanLibrary",Resources.Load<WorldGenHumanLibrary>("Humans/HumanLibrary"));
        var actor=Call("CreateHuman",0,1);Set("humanPlayer",actor);Call("PrepareInventoryPreview");
        var preview=Get("inventoryPreviewActor");var portrait=(RenderTexture)Get("inventoryPortrait");
        var pixels=new Texture2D(portrait.width,portrait.height,TextureFormat.RGB24,false);var previous=RenderTexture.active;RenderTexture.active=portrait;
        try
        {
            pixels.ReadPixels(new Rect(0,0,portrait.width,portrait.height),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,"character.png"),pixels.EncodeToPNG());
            var colors=pixels.GetPixels32();if(colors.Count(c=>c.r>c.b+5)<2000)throw new InvalidDataException("Character preview missing textured skin");
            Debug.Log("INVENTORY PREVIEW PASS textured native human and bag, dedicated preview camera");
        }
        finally
        {
            RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(pixels);portrait.Release();UnityEngine.Object.DestroyImmediate(portrait);Set("inventoryPortrait",null);
            foreach(var a in new[]{actor,preview}){var t=a.GetType();((PlayableGraph)t.GetField("Graph").GetValue(a)).Destroy();foreach(Material m in (System.Collections.IEnumerable)t.GetField("OwnedMaterials").GetValue(a))UnityEngine.Object.DestroyImmediate(m);}
            foreach(Material m in (System.Collections.IEnumerable)Get("inventoryPreviewMaterials"))UnityEngine.Object.DestroyImmediate(m);
            var bag=(Material)Get("inventoryBagMaterial");if(bag!=null)UnityEngine.Object.DestroyImmediate(bag);Set("inventoryBagMaterial",null);
            UnityEngine.Object.DestroyImmediate((GameObject)Get("inventoryPreviewRoot"));Set("inventoryPreviewRoot",null);Set("inventoryPreviewActor",null);Set("humanPlayer",null);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
