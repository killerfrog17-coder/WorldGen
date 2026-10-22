using System;
using UnityEngine;
using WorldGen.Core;

// Shared shoot art. CPU pixels are retained once for bounded asynchronous model
// baking; they are never sampled on the CPU to animate/draw standing trees.
static class LocalTreeTextures
{
    public const int Size=256,Layers=32;
    public static Color32[][] Colour,Normals;
    public static void Create(out Texture2DArray colour,out Texture2DArray normals)
    {
        if(Colour==null)Generate();
        Upload(Colour,Normals,Size,"Tree shoots 2",out colour,out normals);
    }
    static void Generate()
    {
        Colour=new Color32[Layers][];Normals=new Color32[Layers][];
        for(int layer=0;layer<Layers;layer++)
        {
            int family=layer/4,variant=layer%4;var rng=new Rng(layer,"tree-shoot-art-2");
            var pixels=Colour[layer]=new Color32[Size*Size];var ns=Normals[layer]=new Color32[pixels.Length];
            // A branching leafy shoot, not unrelated ellipses floating on cards.
            void Stem(Vector2 a,Vector2 b,float width)
            {
                int count=Mathf.CeilToInt((b-a).magnitude*Size*2);
                for(int k=0;k<=count;k++)
                {
                    var p=Vector2.Lerp(a,b,k/(float)Math.Max(1,count));int radius=Math.Max(1,Mathf.RoundToInt(width*Size));
                    int x=Mathf.RoundToInt(p.x*Size),y=Mathf.RoundToInt(p.y*Size);
                    for(int dy=-radius;dy<=radius;dy++)for(int dx=-radius;dx<=radius;dx++)if(x+dx>=0&&x+dx<Size&&y+dy>=0&&y+dy<Size)
                    {int at=(y+dy)*Size+x+dx;pixels[at]=new Color32(113,113,113,255);ns[at]=new Color32(128,128,255,128);}
                }
            }
            Stem(new Vector2(.48f,.04f),new Vector2(.54f,.94f),.003f);
            int rows=family==4?12:7;
            for(int row=0;row<rows;row++)for(int sign=-1;sign<=1;sign+=2)
            {
                float y=.12f+row*.72f/rows,reach=(.15f+.16f*Mathf.Sin(y*Mathf.PI))*(.88f+(float)rng.Unit()*.22f);
                Vector2 a=new Vector2(.48f+.06f*y,y),tip=new Vector2(a.x+sign*reach,y+.11f);
                Stem(a,tip,.002f);
                int leaflets=family==4?7:family==3?3:2;
                for(int leaflet=0;leaflet<leaflets;leaflet++)
                {
                    float q=(leaflet+.65f)/leaflets;var center=Vector2.Lerp(a,tip,q)+new Vector2(sign*.025f,.025f);
                    float length=family==4?.060f:family==1?.12f:family==3?.078f:.105f;
                    float width=family==4?.010f:family==1?.029f:family==3?.031f:.052f;
                    length*=.85f+(float)rng.Unit()*.3f;
                    float angle=sign*(.55f+(float)rng.Unit()*.55f);var axis=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));var side=new Vector2(axis.y,-axis.x);
                    int x0=Math.Max(0,(int)((center.x-length-width)*Size)),x1=Math.Min(Size-1,(int)((center.x+length+width)*Size));
                    int y0=Math.Max(0,(int)((center.y-length-width)*Size)),y1=Math.Min(Size-1,(int)((center.y+length+width)*Size));
                    float variation=.87f+(float)rng.Unit()*.18f;
                    for(int ypix=y0;ypix<=y1;ypix++)for(int x=x0;x<=x1;x++)
                    {
                        var d=new Vector2((x+.5f)/Size-center.x,(ypix+.5f)/Size-center.y);
                        float u=Vector2.Dot(d,side)/width,v=Vector2.Dot(d,axis)/length;
                        float edge=1-u*u-v*v;
                        if(family==2)edge+=.15f*Mathf.Cos(v*(5+variant)*Mathf.PI);
                        float alpha=Mathf.Clamp01(edge*Size*.04f+.5f);int at=ypix*Size+x;
                        if(alpha*255<pixels[at].a)continue;
                        float vein=Mathf.Exp(-Mathf.Abs(u)*50)*.08f+.025f*Mathf.Cos((v+Mathf.Abs(u)*.6f)*68);
                        float shade=Mathf.Clamp01((.78f+.13f*(1-u*u)+vein+.04f*v)*variation);
                        byte c=(byte)(shade*255);pixels[at]=new Color32(c,c,c,(byte)(alpha*255));
                        var n=new Vector3(u*.48f,v*.16f,1).normalized;ns[at]=new Color32((byte)((n.x*.5f+.5f)*255),(byte)((n.y*.5f+.5f)*255),(byte)((n.z*.5f+.5f)*255),128);
                    }
                }
            }
        }
    }
    public static void Upload(Color32[][] colours,Color32[][] normals,int size,string name,out Texture2DArray colour,out Texture2DArray normal)
    {
        colour=new Texture2DArray(size,size,colours.Length,TextureFormat.RGBA32,true,false){name=name+" albedo",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=2};
        normal=new Texture2DArray(size,size,colours.Length,TextureFormat.RGBA32,true,true){name=name+" normal/depth",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=2};
        for(int layer=0;layer<colours.Length;layer++)
        {
            var c=colours[layer];var n=normals[layer];int covered=0;foreach(var p in c)if(p.a>=102)covered++;
            float target=covered/(float)c.Length;
            for(int mip=0,width=size;;mip++,width/=2)
            {
                colour.SetPixels32(c,layer,mip);normal.SetPixels32(n,layer,mip);if(width==1)break;
                int nextWidth=width/2;var next=new Color32[nextWidth*nextWidth];var ns=new Color32[next.Length];
                for(int y=0;y<nextWidth;y++)for(int x=0;x<nextWidth;x++)
                {
                    int weight=0,alpha=0,cr=0,cg=0,cb=0,nr=0,ng=0,nb=0,nd=0;
                    for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)
                    {int at=(y*2+dy)*width+x*2+dx;var p=c[at];var q=n[at];int a=p.a;weight+=a;alpha+=a;cr+=p.r*a;cg+=p.g*a;cb+=p.b*a;nr+=q.r*a;ng+=q.g*a;nb+=q.b*a;nd+=q.a*a;}
                    int index=y*nextWidth+x,div=Math.Max(1,weight);
                    next[index]=new Color32((byte)(cr/div),(byte)(cg/div),(byte)(cb/div),(byte)(alpha/4));
                    ns[index]=new Color32((byte)(nr/div),(byte)(ng/div),(byte)(nb/div),(byte)(nd/div));
                }
                float lo=0,hi=8;
                for(int k=0;k<10;k++){float scale=(lo+hi)*.5f;int count=0;foreach(var p in next)if(p.a*scale>=102)count++;if(count/(float)next.Length<target)lo=scale;else hi=scale;}
                float gain=(lo+hi)*.5f;for(int i=0;i<next.Length;i++){var p=next[i];p.a=(byte)Math.Min(255,p.a*gain);next[i]=p;}
                c=next;n=ns;
            }
        }
        colour.Apply(false,true);normal.Apply(false,true);
    }
}
