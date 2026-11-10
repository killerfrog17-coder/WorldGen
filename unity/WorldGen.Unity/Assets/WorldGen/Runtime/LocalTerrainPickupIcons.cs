using UnityEngine;
using WorldGen.Core;
public sealed partial class LocalTerrainExplorer
{
    Texture2D pickupCategoryAtlas,flakeableStonePickup;
    readonly Texture2D[] rockSizePickups=new Texture2D[3];
    static readonly string[] PickupIconResources={"PickupCategoryMasksV1","FlakeableStonePickupV1","PickupPebbleV1","PickupSmallRockV1","PickupLargeRockV1"};
    ResourceRequest[] pickupIconRequests;bool pickupIconsPrepared;
    void PreparePickupIcons()
    {
        if(pickupIconsPrepared)return;pickupIconsPrepared=true;
        pickupIconRequests=new ResourceRequest[PickupIconResources.Length];
        for(int i=0;i<pickupIconRequests.Length;i++)pickupIconRequests[i]=Resources.LoadAsync<Texture2D>(PickupIconResources[i]);
    }
    void UpdatePickupIconLoads()
    {
        if(pickupIconRequests==null)return;
        bool pending=false;
        for(int i=0;i<pickupIconRequests.Length;i++)
        {
            var request=pickupIconRequests[i];if(request==null)continue;
            if(!request.isDone){pending=true;continue;}
            var texture=request.asset as Texture2D;
            if(i==0)pickupCategoryAtlas=texture;else if(i==1)flakeableStonePickup=texture;else rockSizePickups[i-2]=texture;
            pickupIconRequests[i]=null;
        }
        if(!pending)pickupIconRequests=null;
    }
    int PickupCategory(Possession item)
    {
        if(StoneCrafting.Rock(item))return 0;if(WoodCrafting.Wood(item))return item.Kind==PossessionKind.WoodenSpear?3:1;if(item.Kind==PossessionKind.StoneFlake)return 2;
        switch(PortableTechnology.Item(item.Kind)?.Form)
        {
            case PortableForm.Spear:case PortableForm.Arrow:return 3;
            case PortableForm.Bow:return 4;
            case PortableForm.Fibre:case PortableForm.Cord:case PortableForm.Sling:case PortableForm.Net:case PortableForm.Belt:return 5;
            case PortableForm.GrindingStone:return 0;
            case PortableForm.Hide:case PortableForm.Strip:case PortableForm.Garment:return 6;
            case PortableForm.Pouch:case PortableForm.Pack:case PortableForm.Satchel:case PortableForm.Quiver:case PortableForm.Case:case PortableForm.Frame:case PortableForm.Basket:case PortableForm.BarkBox:return 7;
            case PortableForm.Bone:case PortableForm.Antler:case PortableForm.Needle:return 8;
            case PortableForm.Reed:return 9;
            case PortableForm.Hook:return 10;
            case PortableForm.Feather:return 11;
            case PortableForm.Shaft:return 1;
            default:return 2;
        }
    }
    bool PickupGraphic(Possession item,out Texture2D texture,out Rect uv)
    {
        uv=new Rect(0,0,1,1);texture=null;
        if(Foraging.Food(item)){texture=PortableIcon(item);return texture!=null;}
        if(StoneCrafting.Rock(item)&&StoneCrafting.WorkpieceReason(item).Length==0)
        {
            // Preparation starts with the inventory, not on first visibility.
            // A cold synchronous Resources.Load here stalled GUI.Repaint and
            // the render thread waiting for the atlas upload. While loading,
            // omit this icon instead of blocking or showing the wrong stone.
            if(pickupIconRequests!=null)return (texture=flakeableStonePickup)!=null;
            if(flakeableStonePickup!=null){texture=flakeableStonePickup;return true;}
        }
        if(StoneCrafting.Rock(item)||item.Kind==PossessionKind.Grindstone)
        {
            int size=(int)StoneCrafting.Size(item);
            if(pickupIconRequests!=null)return (texture=rockSizePickups[size])!=null;
            if(rockSizePickups[size]!=null){texture=rockSizePickups[size];return true;}
        }
        texture=pickupCategoryAtlas;int category=PickupCategory(item);uv=new Rect((category%4)*.25f,(2-category/4)/3f,.25f,1/3f);return texture!=null;
    }
    void DrawPickupCategory(Rect rect,Possession item)
    {
        if(!PickupGraphic(item,out var texture,out var uv))return;
        if(uv==new Rect(0,0,1,1))GUI.DrawTexture(rect,texture,ScaleMode.ScaleToFit,true);
        else GUI.DrawTextureWithTexCoords(rect,texture,uv,true);
    }
}
