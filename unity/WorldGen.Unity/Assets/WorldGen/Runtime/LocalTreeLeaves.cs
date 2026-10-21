using UnityEngine;

// Cached procedural art, not analytic leaf silhouettes per shaded fragment.
static class LocalTreeLeaves
{
    public static Texture2DArray Create()
    {
        const int size=128,layers=32;
        var texture=new Texture2DArray(size,size,layers,TextureFormat.RGBA32,true,true)
        {name="Generated tree leaf clusters / coverage mips",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4};
        for(int layer=0;layer<layers;layer++)
        {
            int shape=layer/4,variant=layer%4;var pixels=new Color[size*size];uint state=(uint)(731+layer*1777);
            float Next(){state^=state<<13;state^=state>>17;state^=state<<5;return (state&65535)/65535f;}
            int rows=shape==3?8:6;
            for(int row=0;row<rows;row++)for(int sign=-1;sign<=1;sign+=2)
            {
                float y=.10f+(row+.5f)*.79f/rows+(Next()-.5f)*.035f;
                float reach=.11f+.23f*Mathf.Sin(y*Mathf.PI),angle=sign*(.55f+Next()*.4f);
                float cx=.5f+sign*reach*.65f,cy=y+(Next()-.5f)*.04f;
                float rx=shape==1?.068f:shape==3?.07f:.105f,ry=shape==1?.14f:.115f;
                rx*=.85f+Next()*.3f;ry*=.85f+Next()*.3f;
                for(int z=0;z<size;z++)for(int x=0;x<size;x++)
                {
                    float dx=(x+.5f)/size-cx,dy=(z+.5f)/size-cy;
                    float u=(dx*Mathf.Cos(angle)-dy*Mathf.Sin(angle))/rx,v=(dx*Mathf.Sin(angle)+dy*Mathf.Cos(angle))/ry;
                    float edge=1-u*u-v*v;
                    if(shape==2)edge+=.14f*Mathf.Cos(Mathf.Atan2(v,u)*(6+variant%2*2));
                    float alpha=Mathf.Clamp01(edge*size*.035f+.5f);
                    if(alpha<=pixels[z*size+x].a)continue;
                    float shade=.73f+.16f*(1-u*u)+.09f*v+Mathf.Exp(-Mathf.Abs(u)*35)*.07f;
                    pixels[z*size+x]=new Color(shade,shade,shade,alpha);
                }
            }
            for(int z=8;z<size-9;z++)for(int x=size/2-1;x<=size/2+1;x++)
                if(pixels[z*size+x].a<.85f)pixels[z*size+x]=new Color(.60f,.60f,.60f,.85f);
            int covered=0;foreach(var p in pixels)if(p.a>=.4f)covered++;
            float target=covered/(float)pixels.Length;texture.SetPixels(pixels,layer,0);
            for(int level=1,width=size/2;width>=1;level++,width/=2)
            {
                var next=new Color[width*width];int prior=width*2;
                for(int z=0;z<width;z++)for(int x=0;x<width;x++)
                {
                    Color sum=Color.clear;float weight=0,alpha=0;
                    for(int dz=0;dz<2;dz++)for(int dx=0;dx<2;dx++)
                    {Color p=pixels[(z*2+dz)*prior+x*2+dx];sum+=p*p.a;weight+=p.a;alpha+=p.a;}
                    Color c=weight>.00001f?sum/weight:new Color(.86f,.86f,.86f,0);c.a=alpha*.25f;next[z*width+x]=c;
                }
                // Preserve alpha-test occupancy rather than merely mean alpha.
                float low=0,high=8;
                for(int it=0;it<12;it++)
                {float s=(low+high)*.5f;int count=0;foreach(var c in next)if(c.a*s>=.4f)count++;
                    if(count/(float)next.Length<target)low=s;else high=s;}
                float scale=(low+high)*.5f;
                for(int j=0;j<next.Length;j++){var c=next[j];c.a=Mathf.Clamp01(c.a*scale);next[j]=c;}
                texture.SetPixels(next,layer,level);pixels=next;
            }
        }
        texture.Apply(false,true);return texture;
    }
}
