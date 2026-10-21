using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public readonly struct ScatterBlend
    {
        public readonly int Near,Far;public readonly double Blend,Visibility;
        public ScatterBlend(int near,int far,double blend,double visibility){Near=near;Far=far;Blend=blend;Visibility=visibility;}
    }
    public static class ScatterLodBlend
    {
        public static ScatterBlend At(ScatterKind kind,double width,double distance)
        {
            bool tree=kind==ScatterKind.Broadleaf||kind==ScatterKind.Conifer||kind==ScatterKind.SavannaTree;
            double range=tree?1800:kind==ScatterKind.Grass?95:kind==ScatterKind.Shrub?220:kind==ScatterKind.Stone?70:kind==ScatterKind.Outcrop?1800:650;
            double Smooth(double lo,double hi,double x){double t=Math.Max(0,Math.Min(1,(x-lo)/(hi-lo)));return t*t*(3-2*t);}
            double visibility=1-Smooth(range*.85,range,distance);
            if(tree){if(distance<105)return new ScatterBlend(0,1,Smooth(65,105,distance),visibility);return new ScatterBlend(1,2,Smooth(280,420,distance),visibility);}
            double split=Math.Max(30,kind==ScatterKind.Outcrop?width*8:30);
            return new ScatterBlend(0,1,Smooth(split*.8,split*1.2,distance),visibility);
        }
    }
}
