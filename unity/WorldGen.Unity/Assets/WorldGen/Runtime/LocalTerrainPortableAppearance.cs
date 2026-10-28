using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    [Serializable] sealed class PortableCoatRegion {public string slug;public float[] rect;public int coats;}
    [Serializable] sealed class PortableCoatRegionList {public PortableCoatRegion[] regions;}
    readonly Dictionary<GameObject,Possession> portableRoots=new Dictionary<GameObject,Possession>();
    readonly HashSet<string> portableCoatWanted=new HashSet<string>(),portableCoatPins=new HashSet<string>();
    Dictionary<string,PortableCoatRegion> portableCoatRegions;Material portableItemMaterial;double portableAppearanceAt;
    readonly MaterialPropertyBlock portableItemProps=new MaterialPropertyBlock();
    bool PendingPortableCoats=>portableCoatWanted.Any(s=>!animalModels.ContainsKey(s)&&!animalLoadFailures.Contains(s));
    bool PortableFur(Possession p)
    {var form=PortableTechnology.Item(p.Kind)?.Form;return !string.IsNullOrEmpty(p.AppearanceSpecies)&&(form==PortableForm.Hide||form==PortableForm.Pouch||form==PortableForm.Pack||form==PortableForm.Satchel||form==PortableForm.Quiver);}
    Material PortableMaterial()
    {
        if(portableItemMaterial!=null)return portableItemMaterial;
        portableItemMaterial=new Material(Resources.Load<Shader>("WorldGenPortableItem")){name="Portable craft materials",enableInstancing=true};RegisterScatterMaterial(portableItemMaterial);return portableItemMaterial;
    }
    void CreatePortableParts(GameObject root,Possession p,int layer)
    {
        AddCraftMesh(root,LocalScatterMeshes.BuildPortable(p),PortableMaterial(),p,layer);portableRoots[root]=p;
        if(PortableFur(p))portableCoatWanted.Add(p.AppearanceSpecies);RefreshPortableRoot(root,p);
    }
    void RefreshPortableRoot(GameObject root,Possession p)
    {
        if(root==null)return;
        if(portableCoatRegions==null)
        {var text=Resources.Load<TextAsset>("PortableCoatRegions");portableCoatRegions=text==null?new Dictionary<string,PortableCoatRegion>():JsonUtility.FromJson<PortableCoatRegionList>(text.text).regions.ToDictionary(r=>r.slug);}
        portableItemProps.Clear();portableItemProps.SetFloat("_Preview",root.layer==29?1:0);
        bool worn=PortableTechnology.Worn(p.Place)||p.Place==PossessionPlace.Container&&PortableTechnology.Belt(Inventory.Find(p.ContainerId))&&Inventory.Carried(p);
        portableItemProps.SetFloat("_Wearer",root.layer!=29&&worn?1:0);
        if(PortableFur(p)&&animalModels.TryGetValue(p.AppearanceSpecies,out var model))
        {
            if(portableCoatPins.Add(p.AppearanceSpecies))model.Users++;
            portableItemProps.SetTexture("_SourceCoats",model.Asset.Coats);portableItemProps.SetFloat("_HasFur",1);portableItemProps.SetFloat("_Coat",Mathf.Clamp(p.AppearanceCoat,0,model.Asset.Coats.depth-1));
            var r=portableCoatRegions.TryGetValue(p.AppearanceSpecies,out var region)?region.rect:new[]{.2f,.2f,.4f,.4f};portableItemProps.SetVector("_FurRect",new Vector4(r[0],r[1],r[2],r[3]));
        }
        foreach(var renderer in root.GetComponentsInChildren<Renderer>())renderer.SetPropertyBlock(portableItemProps);
    }
    void UpdatePortableAppearances()
    {
        if(Time.unscaledTimeAsDouble<portableAppearanceAt)return;portableAppearanceAt=Time.unscaledTimeAsDouble+.5;
        int pins=portableCoatPins.Count;foreach(var pair in portableRoots)if(pair.Key!=null)RefreshPortableRoot(pair.Key,pair.Value);
        if(pins!=portableCoatPins.Count&&inventoryOpen)RenderInventoryPreview();
    }
    void DisposePortableAppearance()
    {
        foreach(var slug in portableCoatPins)if(animalModels.TryGetValue(slug,out var model))model.Users=Math.Max(0,model.Users-1);
        portableCoatPins.Clear();portableCoatWanted.Clear();portableRoots.Clear();DisposePortableIcons();portableItemMaterial=null;
    }
    void PosePortableWear(GameObject root,Possession p,Transform body,Dictionary<string,Transform> bones)
    {
        root.transform.rotation=body.rotation;
        if(p.Place==PossessionPlace.SlungBack)
        {root.transform.position=bones["spine_02"].position-body.forward*.23f-Vector3.up*(float)p.HeightM*.48f;root.transform.rotation=body.rotation*Quaternion.Euler(0,0,28);}
        else if(p.Place==PossessionPlace.Back)
            root.transform.position=bones["spine_02"].position-body.forward*(.15f+(float)p.DepthM*.5f)-Vector3.up*((float)p.HeightM*.5f+.08f);
        else if(p.Place==PossessionPlace.Shoulder)
            root.transform.position=bones["spine_02"].position+body.right*.24f+body.forward*.04f-Vector3.up*.46f;
        else if(p.Place==PossessionPlace.Waist)
        {
            bool belt=PortableTechnology.Belt(p);root.transform.position=bones["spine_02"].position-Vector3.up*.3f+(belt?Vector3.zero:body.right*.21f+body.forward*.09f);
            if(belt)root.transform.localScale=new Vector3(.43f,.05f,.28f);
        }
        else
        {
            root.transform.position=bones["spine_02"].position-Vector3.up*.34f+body.forward*.13f+body.right*((p.X-1.5f)*.065f);
            root.transform.position-=Vector3.up*(float)p.HeightM*.65f;
        }
        RefreshPortableRoot(root,p);
    }
}
