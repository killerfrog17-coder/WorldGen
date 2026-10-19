using UnityEngine;
using WorldGen.Core;

/// <summary>Small, tileable placeholder PBR data. Replace arrays/material in the art set, not placement code.</summary>
static class LocalGroundTextures
{
    static float Noise(float u,float v,int grid,int seed)
    {
        float x=u*grid,y=v*grid;int a=Mathf.FloorToInt(x),b=Mathf.FloorToInt(y);float tx=x-a,ty=y-b;tx=tx*tx*(3-2*tx);ty=ty*ty*(3-2*ty);
        float Hash(int i,int j)=> (Rng.Mix((uint)((i%grid+grid)%grid+((j%grid+grid)%grid)*grid+seed))&65535)/65535f;
        return Mathf.Lerp(Mathf.Lerp(Hash(a,b),Hash(a+1,b),tx),Mathf.Lerp(Hash(a,b+1),Hash(a+1,b+1),tx),ty);
    }
    public static Texture2DArray[] Create()
    {
        const int size=128;var arrays=new[]{new Texture2DArray(size,size,14,TextureFormat.RGBA32,true,false),new Texture2DArray(size,size,14,TextureFormat.RGBA32,true,true),new Texture2DArray(size,size,14,TextureFormat.RGBA32,true,true)};
        for(int layer=0;layer<14;layer++)
        {
            var height=new float[size*size];var albedo=new Color[size*size];var normal=new Color[size*size];var mask=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=x/(float)size,v=y/(float)size,a=Noise(u,v,8,layer*111+81),b=Noise(u,v,32,layer*333+51),c=Noise(u,v,64,layer*211+77);
                float relief=layer==1?.16f*b+.08f*c+.08f*Mathf.Sin(u*Mathf.PI*32+a*2):layer==2?Mathf.Pow(b,2)*1.5f+.25f*c:layer==3?.2f*a+.1f*c:layer==4?Mathf.Abs(a-.5f)*1.3f+.3f*b:layer==7?.15f*b+.05f*c:.4f*a+.3f*b+.2f*c;
                int i=y*size+x;height[i]=relief*(layer==2?.025f:layer==4?.022f:layer==1||layer==7?.003f:.008f);
                albedo[i]=new Color(.5f,.5f,.5f)*(.82f+.24f*a+.15f*c);albedo[i].a=1;mask[i]=new Color(.86f+.14f*b,layer==7?.55f:.78f+.2f*c,relief,0);
                if(layer>=8)
                {
                    int family=layer-8;
                    float beds=.5f+.5f*Mathf.Sin(v*Mathf.PI*(family==4?16:8)+a*.9f);
                    float joint=Mathf.Pow(Mathf.Clamp01(1-Mathf.Abs(Noise(u,v,4,739)-.5f)*18),3);
                    float grain=family==0?.35f*b+.65f*c:family==1?.65f*b+.35f*c:family==2?.6f*b+.4f*beds:family==4?.35f*b+.65f*beds:family==5?.3f*c+.7f*beds:.7f*a+.3f*c;
                    float shade=.72f+.48f*grain-.18f*joint;
                    albedo[i]=new Color(.5f*shade,.5f*shade,.5f*shade,1);
                    height[i]=(grain-.6f*joint)*(family==0?.012f:family==4?.01f:.025f);
                    mask[i]=new Color(.8f+.2f*(1-joint),family==0?.70f+.2f*c:.84f+.14f*c,grain,0);
                }
            }
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=(height[y*size+(x+1)%size]-height[y*size+(x+size-1)%size])/(4f/size),dy=(height[((y+1)%size)*size+x]-height[((y+size-1)%size)*size+x])/(4f/size);
                Vector3 n=new Vector3(-dx,-dy,1).normalized;normal[y*size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);
            }
            arrays[0].SetPixels(albedo,layer);arrays[1].SetPixels(normal,layer);arrays[2].SetPixels(mask,layer);
        }
        foreach(var array in arrays){array.wrapMode=TextureWrapMode.Repeat;array.filterMode=FilterMode.Trilinear;array.anisoLevel=4;array.Apply(true,true);}return arrays;
    }
}
