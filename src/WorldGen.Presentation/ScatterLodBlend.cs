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
            =>At(kind,width,distance,0,0,double.PositiveInfinity);
        // Renderer-only controls: handoff ends and maximum physical draw range.
        // Zero handoffs retain the original type-specific defaults. World
        // placement/ecology and the original three-argument API stay separate.
        public static ScatterBlend At(ScatterKind kind,double width,double distance,double nearEnd,double farEnd,double maximumRange)
        {
            bool tree=kind==ScatterKind.Broadleaf||kind==ScatterKind.Conifer||kind==ScatterKind.SavannaTree;
            if(kind==ScatterKind.GeneratedTree)
            {
                double S(double a,double b){double t=D.Clamp((distance-a)/(b-a));return t*t*(3-2*t);}
                nearEnd=nearEnd>0?nearEnd:120;farEnd=farEnd>0?farEnd:450;
                double reach=Math.Max(1,Math.Min(maximumRange,D.Clamp(width*150,450,1400))),visible=1-S(Math.Min(950,reach*.8),reach);
                if(distance<nearEnd*(130d/120))return new ScatterBlend(0,1,S(nearEnd*.5,nearEnd),visible);
                return new ScatterBlend(1,2,S(Math.Max(nearEnd,farEnd*(260d/450)),Math.Max(nearEnd+1,farEnd)),visible);
            }
            if(kind==ScatterKind.MidPlant)
            {
                double SmoothMid(double a,double b){double t=D.Clamp((distance-a)/(b-a));return t*t*(3-2*t);}
                nearEnd=nearEnd>0?nearEnd:55;farEnd=farEnd>0?farEnd:200;
                double reach=Math.Max(1,Math.Min(maximumRange,D.Clamp(width*250,280,1400))),midVisibility=1-SmoothMid(reach*.80,reach);
                if(distance<nearEnd*(70d/55))return new ScatterBlend(0,1,SmoothMid(nearEnd*(28d/55),nearEnd),midVisibility);
                return new ScatterBlend(1,2,SmoothMid(Math.Max(nearEnd,farEnd*.55),Math.Max(nearEnd+1,farEnd)),midVisibility);
            }
            if(kind==ScatterKind.Herb||kind==ScatterKind.Fungus)
            {
                double end=Math.Max(1,Math.Min(maximumRange,kind==ScatterKind.Fungus?65:140));nearEnd=nearEnd>0?nearEnd:22;
                double SmoothHerb(double lo,double hi,double x){double t=Math.Max(0,Math.Min(1,(x-lo)/(hi-lo)));return t*t*(3-2*t);}
                return new ScatterBlend(0,1,SmoothHerb(nearEnd*(12d/22),nearEnd,distance),1-SmoothHerb(end*.78,end,distance));
            }
            double range=tree?1800:kind==ScatterKind.Grass?95:kind==ScatterKind.Shrub?220:kind==ScatterKind.Stone?70:kind==ScatterKind.Outcrop?1800:650;
            double Smooth(double lo,double hi,double x){double t=Math.Max(0,Math.Min(1,(x-lo)/(hi-lo)));return t*t*(3-2*t);}
            double visibility=1-Smooth(range*.85,range,distance);
            if(tree){if(distance<105)return new ScatterBlend(0,1,Smooth(65,105,distance),visibility);return new ScatterBlend(1,2,Smooth(280,420,distance),visibility);}
            double split=Math.Max(30,kind==ScatterKind.Outcrop?width*8:30);
            return new ScatterBlend(0,1,Smooth(split*.8,split*1.2,distance),visibility);
        }
    }
}
