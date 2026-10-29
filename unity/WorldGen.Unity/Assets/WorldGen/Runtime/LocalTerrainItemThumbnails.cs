using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using WorldGen.Core;
public sealed partial class LocalTerrainExplorer
{
    readonly Dictionary<string,Texture2D> itemThumbnails=new Dictionary<string,Texture2D>();
    readonly Queue<Possession> itemThumbnailQueue=new Queue<Possession>();readonly HashSet<string> itemThumbnailWanted=new HashSet<string>();
    Camera itemThumbnailCamera;RenderTexture itemThumbnailTarget;
    string ItemThumbnailKey(Possession p)=>p.Kind+"/"+p.Variant+"/"+System.Math.Round(p.WidthM/System.Math.Max(.001,p.HeightM),2)+"/"+System.Math.Round(p.DepthM/System.Math.Max(.001,p.HeightM),2)+"/"+p.AppearanceSpecies+"/"+p.AppearanceCoat+"/"+p.FlakeRemovals+"/"+(PortableFur(p)&&animalModels.ContainsKey(p.AppearanceSpecies)?"coat":"base")+"/"+p.FoodSpecies+"/"+p.Cooked+"/"+p.Burnt;
    Texture2D ItemThumbnail(Possession item)
    {
        string key=ItemThumbnailKey(item);if(itemThumbnails.TryGetValue(key,out var texture))return texture;
        if(itemThumbnailWanted.Add(key))itemThumbnailQueue.Enqueue(item.Copy());return null;
    }
    void UpdateItemThumbnails()
    {
        if(!inventoryOpen||itemThumbnailQueue.Count==0)return;
        if(itemThumbnailCamera==null)
        {
            var root=new GameObject("Item thumbnail camera",typeof(Camera),typeof(UniversalAdditionalCameraData));root.transform.SetParent(transform,false);itemThumbnailCamera=root.GetComponent<Camera>();
            itemThumbnailCamera.enabled=false;itemThumbnailCamera.cullingMask=1<<28;itemThumbnailCamera.clearFlags=CameraClearFlags.SolidColor;itemThumbnailCamera.backgroundColor=Color.clear;
            itemThumbnailCamera.orthographic=true;itemThumbnailCamera.nearClipPlane=.01f;itemThumbnailCamera.farClipPlane=20;itemThumbnailCamera.allowHDR=false;itemThumbnailCamera.allowMSAA=true;
            itemThumbnailTarget=new RenderTexture(192,192,24,RenderTextureFormat.ARGB32){antiAliasing=4};itemThumbnailTarget.Create();itemThumbnailCamera.targetTexture=itemThumbnailTarget;
        }
        var item=itemThumbnailQueue.Dequeue();string key=ItemThumbnailKey(item);var visual=CreatePossessionVisual(item,29);foreach(var t in visual.GetComponentsInChildren<Transform>())t.gameObject.layer=28;
        visual.transform.SetParent(transform,false);visual.transform.localPosition=new Vector3(0,-1000,0);visual.transform.localRotation=Quaternion.Euler(5,-22,-18);
        var renderers=visual.GetComponentsInChildren<Renderer>();if(renderers.Length==0){DestroyInventoryVisual(visual);return;}var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        foreach(var renderer in renderers)
        {
            if(PortableTechnology.Item(item.Kind)!=null)continue;
            if(inventoryPreviewRockMaterial==null){inventoryPreviewRockMaterial=new Material(Resources.Load<Shader>("WorldGenInventoryPreview"));inventoryPreviewMaterials.Add(inventoryPreviewRockMaterial);}renderer.sharedMaterial=inventoryPreviewRockMaterial;
            var props=new MaterialPropertyBlock();props.SetFloat("_VertexColours",1);props.SetColor("_Color",new Color((float)item.Tint.X,(float)item.Tint.Y,(float)item.Tint.Z));renderer.SetPropertyBlock(props);
        }
        itemThumbnailCamera.transform.position=bounds.center+new Vector3(1,.25f,-3).normalized*4;itemThumbnailCamera.transform.LookAt(bounds.center);
        itemThumbnailCamera.orthographicSize=Mathf.Max(.03f,bounds.size.magnitude*.52f);var previous=RenderTexture.active;itemThumbnailCamera.Render();RenderTexture.active=itemThumbnailTarget;
        var texture=new Texture2D(192,192,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,192,192),0,0);texture.Apply(false,true);RenderTexture.active=previous;
        itemThumbnails[key]=texture;DestroyInventoryVisual(visual);
    }
    void DisposeItemThumbnails()
    {
        foreach(var texture in itemThumbnails.Values)Destroy(texture);itemThumbnails.Clear();itemThumbnailQueue.Clear();itemThumbnailWanted.Clear();
        if(itemThumbnailCamera!=null)Destroy(itemThumbnailCamera.gameObject);itemThumbnailCamera=null;if(itemThumbnailTarget!=null){if(RenderTexture.active==itemThumbnailTarget)RenderTexture.active=null;itemThumbnailTarget.Release();Destroy(itemThumbnailTarget);}itemThumbnailTarget=null;
    }
}
