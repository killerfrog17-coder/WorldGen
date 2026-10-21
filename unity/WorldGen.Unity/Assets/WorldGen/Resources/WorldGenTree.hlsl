#ifndef WORLDGEN_TREE_INCLUDED
#define WORLDGEN_TREE_INCLUDED
// Species-shaped foliage packets: twelve leaf silhouettes and a thin rachis
// per fitted, folded surface, not twelve separate meshes or giant opaque leaves.
void WorldGenTreeLeafClip(float2 uv,float material,float shape)
{
    if(material<1.5||material>2.5)return;
    float nearest=abs(uv.x-.5)-.012;
    [unroll]for(int row=0;row<6;row++)
    {
        float y=(row+.5)/6.;float width=.35*sin(y*3.14159265);
        [unroll]for(int side=-1;side<=1;side+=2)
        {
            float2 center=float2(.5+side*width*.58,y+(side>0?.026:-.026));
            float2 p=(uv-center)/float2(max(.025,width*.66),.095);
            p.y+=side*p.x*.28;
            float ellipse=dot(p,p)-1;
            if(shape>1.5&&shape<2.5)ellipse+=.16*cos(atan2(p.y,p.x)*6);
            nearest=min(nearest,ellipse*.04);
        }
    }
    clip(-nearest);
}
float3 WorldGenTreeWind(float3 p,float3 root,float2 mask,float enabled,float4 traits)
{
    if(enabled<=0)return p;
    float height=length(TransformObjectToWorld(float3(0,1,0))-root);
    float3 bend=WorldGenVegetationOffset(root,traits.x);
    float3 delta=normalize(float3(0,1,0)+bend)-float3(0,1,0);
    p+=delta*(height*mask.x*mask.x);
    if(mask.y>.5&&mask.y<2.5)
    {
        float3 direction=_WeatherDrivenVegetationWind>.5?_VegetationDirection.xyz:_GrassWindDirection.xyz;
        float strength=_WeatherDrivenVegetationWind>.5?min(1.5,VegetationLocalStrength(root)):1;
        p+=direction*WorldGenOriginalGrassWave(p)*(.12*strength*mask.x);
    }
    return p;
}
#endif
