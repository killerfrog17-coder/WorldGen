using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    readonly Dictionary<PossessionKind,Texture2D> portableIcons=new Dictionary<PossessionKind,Texture2D>();
    readonly Dictionary<string,Texture2D> portableFurIcons=new Dictionary<string,Texture2D>();Material portableIconMaterial;
    Texture2D PortableIcon(Possession p)
    {
        var d=PortableTechnology.Item(p.Kind);
        if(!portableIcons.TryGetValue(p.Kind,out var icon))
        {
            icon=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float u=(x-31.5f)/29,v=y/63f;bool inside=false;Color color=new Color(.65f,.48f,.28f);
                switch(d.Form)
                {
                    case PortableForm.Food:inside=u*u+(v-.5f)*(v-.5f)*4<.48f;color=Color.white;break;
                    case PortableForm.Pouch:case PortableForm.Satchel:case PortableForm.Pack:case PortableForm.Quiver:case PortableForm.BarkBox:case PortableForm.Basket:
                        inside=v>.13f&&v<.8f&&Mathf.Abs(u)<.65f-.18f*Mathf.Abs(v-.5f);if(d.Form==PortableForm.Basket&&inside)color*=((x/3+y/3)%2==0?1:.72f);if(v>.8f&&v<.95f&&Mathf.Abs(Mathf.Sqrt(u*u+(v-.8f)*(v-.8f)*12)-.5f)<.07f)inside=true;break;
                    case PortableForm.Belt:case PortableForm.Cord:
                        float r=Mathf.Sqrt(u*u+(v-.5f)*(v-.5f)*4);inside=(r>.35f&&r<.49f)||(r>.55f&&r<.69f)||(r>.75f&&r<.86f);break;
                    case PortableForm.Frame:inside=v>.05f&&v<.95f&&(Mathf.Abs(Mathf.Abs(u)-.6f)<.08f||Mathf.Abs(u)<.6f&&Mathf.Abs(v*4-Mathf.Round(v*4))<.1f);break;
                    case PortableForm.Hide:inside=v>.08f&&v<.92f&&Mathf.Abs(u)<.6f+.16f*Mathf.Cos(v*18);break;
                    case PortableForm.Fibre:case PortableForm.Reed:case PortableForm.Strip:inside=v>.08f&&v<.92f&&Mathf.Abs(u)<.65f&&Mathf.Abs((u+.05f*Mathf.Sin(v*9))*5-Mathf.Round((u+.05f*Mathf.Sin(v*9))*5))<.18f;break;
                    case PortableForm.Bone:inside=v>.1f&&v<.9f&&Mathf.Abs(u)<(v<.23f||v>.77f?.42f:.15f);color=new Color(.84f,.8f,.66f);break;
                    case PortableForm.Antler:inside=Mathf.Abs(u-.2f*Mathf.Sin(v*5))<.09f||v>.3f&&Mathf.Abs(Mathf.Abs(u)-((v*4)%1)*.65f)<.07f;color=new Color(.8f,.72f,.54f);break;
                    case PortableForm.Needle:case PortableForm.Shaft:inside=v>.06f&&v<.94f&&Mathf.Abs(u)<.065f;color=d.Form==PortableForm.Needle?new Color(.85f,.81f,.68f):color;break;
                    case PortableForm.Point:case PortableForm.Blade:case PortableForm.Chopper:inside=v>.1f&&v<.92f&&Mathf.Abs(u)<.7f*(1-Mathf.Abs(v-.48f)*1.7f);color=p.Kind==PossessionKind.BonePoint||p.Kind==PossessionKind.BarbedPoint?new Color(.85f,.8f,.64f):new Color(.47f,.49f,.47f);break;
                    case PortableForm.Knife:inside=v>.08f&&v<.9f&&Mathf.Abs(u)<(v>.5f?.24f:.17f);if(v>.5f)color=new Color(.49f,.5f,.48f);break;
                    case PortableForm.Axe:inside=v>.05f&&v<.9f&&Mathf.Abs(u)<.09f||v>.64f&&v<.9f&&Mathf.Abs(u)<.7f;if(v>.6f)color=new Color(.49f,.51f,.48f);break;
                    case PortableForm.Spear:case PortableForm.Arrow:
                        inside=v>.04f&&v<.87f&&Mathf.Abs(u)<.055f||v>.8f&&v<.98f&&Mathf.Abs(u)<(.98f-v)*2;
                        if(d.Form==PortableForm.Arrow&&v<.28f&&v>.1f&&Mathf.Abs(u)<.25f)inside=true;if(v>.82f)color=new Color(.61f,.62f,.58f);break;
                    case PortableForm.Bow:inside=Mathf.Abs(u-(.7f*(v*2-1)*(v*2-1)-.35f))<.075f||p.Kind==PossessionKind.Bow&&Mathf.Abs(u-.35f)<.025f;break;
                    case PortableForm.Thrower:inside=Mathf.Abs(u)<.13f||v>.85f&&Mathf.Abs(u)<.48f;break;
                    case PortableForm.Feather:inside=v>.05f&&v<.95f&&Mathf.Abs(u)<.5f*Mathf.Sin(v*Mathf.PI);color=new Color(.79f,.71f,.51f);break;
                    case PortableForm.Net:inside=v>.1f&&v<.9f&&Mathf.Abs(u)<.8f&&((x+y)%9<2||(x-y+90)%9<2);break;
                    case PortableForm.Sling:inside=Mathf.Abs(u)<.3f&&v>.35f&&v<.6f||Mathf.Abs(Mathf.Abs(u)-.4f)<.04f;break;
                    case PortableForm.Hook:inside=Mathf.Abs(Mathf.Sqrt(u*u+(v-.45f)*(v-.45f)*4)-.55f)<.1f&&(v<.65f||u<-.25f);color=new Color(.84f,.8f,.66f);break;
                    case PortableForm.Case:inside=v>.08f&&v<.92f&&Mathf.Abs(u)<.26f;break;
                }
                if(inside)pixels[y*64+x]=color*(.8f+.2f*v);
            }
            icon.SetPixels(pixels);icon.Apply();portableIcons.Add(p.Kind,icon);
        }
        if(!PortableFur(p))return icon;portableCoatWanted.Add(p.AppearanceSpecies);
        string key=p.Kind+"/"+p.AppearanceSpecies+"/"+p.AppearanceCoat;
        if(portableFurIcons.TryGetValue(key,out var furIcon))return furIcon;
        if(Event.current!=null&&Event.current.type!=EventType.Repaint||!animalModels.TryGetValue(p.AppearanceSpecies,out var model))return icon;
        if(portableCoatPins.Add(p.AppearanceSpecies))model.Users++;
        if(portableCoatRegions==null)return icon;
        if(portableIconMaterial==null)portableIconMaterial=new Material(Resources.Load<Shader>("WorldGenPortableIcon"));
        portableIconMaterial.SetTexture("_SourceCoats",model.Asset.Coats);portableIconMaterial.SetFloat("_Coat",Mathf.Clamp(p.AppearanceCoat,0,model.Asset.Coats.depth-1));
        var rect=portableCoatRegions.TryGetValue(p.AppearanceSpecies,out var region)?region.rect:new[]{.2f,.2f,.4f,.4f};portableIconMaterial.SetVector("_FurRect",new Vector4(rect[0],rect[1],rect[2],rect[3]));
        var target=RenderTexture.GetTemporary(64,64,0,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;Graphics.Blit(icon,target,portableIconMaterial);RenderTexture.active=target;
        furIcon=new Texture2D(64,64,TextureFormat.RGBA32,false);furIcon.ReadPixels(new Rect(0,0,64,64),0,0);furIcon.Apply();RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);portableFurIcons.Add(key,furIcon);return furIcon;
    }
    void DisposePortableIcons(){foreach(var icon in portableIcons.Values)Destroy(icon);foreach(var icon in portableFurIcons.Values)Destroy(icon);portableIcons.Clear();portableFurIcons.Clear();if(portableIconMaterial!=null)Destroy(portableIconMaterial);portableIconMaterial=null;}
}
