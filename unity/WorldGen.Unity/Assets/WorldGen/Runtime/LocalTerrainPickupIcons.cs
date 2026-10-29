using UnityEngine;
using WorldGen.Core;
public sealed partial class LocalTerrainExplorer
{
    Texture2D pickupCategoryAtlas;
    int PickupCategory(Possession item)
    {
        if(StoneCrafting.Rock(item))return 0;if(WoodCrafting.Wood(item))return item.Kind==PossessionKind.WoodenSpear?3:1;if(item.Kind==PossessionKind.StoneFlake)return 2;
        switch(PortableTechnology.Item(item.Kind)?.Form)
        {
            case PortableForm.Spear:case PortableForm.Arrow:return 3;
            case PortableForm.Bow:return 4;
            case PortableForm.Fibre:case PortableForm.Cord:case PortableForm.Sling:case PortableForm.Net:case PortableForm.Belt:return 5;
            case PortableForm.Hide:case PortableForm.Strip:return 6;
            case PortableForm.Pouch:case PortableForm.Pack:case PortableForm.Satchel:case PortableForm.Quiver:case PortableForm.Case:case PortableForm.Frame:case PortableForm.Basket:case PortableForm.BarkBox:return 7;
            case PortableForm.Bone:case PortableForm.Antler:case PortableForm.Needle:return 8;
            case PortableForm.Reed:return 9;
            case PortableForm.Hook:return 10;
            case PortableForm.Feather:return 11;
            case PortableForm.Shaft:return 1;
            default:return 2;
        }
    }
    void DrawPickupCategory(Rect rect,Possession item)
    {
        if(Foraging.Food(item)){var icon=PortableIcon(item);if(icon!=null)GUI.DrawTexture(rect,icon,ScaleMode.ScaleToFit,true);return;}
        if(pickupCategoryAtlas==null)pickupCategoryAtlas=Resources.Load<Texture2D>("PickupCategoryMasksV1");if(pickupCategoryAtlas==null)return;
        int category=PickupCategory(item);GUI.DrawTextureWithTexCoords(rect,pickupCategoryAtlas,new Rect((category%4)*.25f,(2-category/4)/3f,.25f,1/3f),true);
    }
}
