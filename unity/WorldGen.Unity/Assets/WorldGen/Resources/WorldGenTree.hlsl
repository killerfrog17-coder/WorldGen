#ifndef WORLDGEN_TREE_INCLUDED
#define WORLDGEN_TREE_INCLUDED
TEXTURE2D_ARRAY(_TreeLeaves);SAMPLER(sampler_TreeLeaves);
// One filtered lookup replaces the unfiltered procedural fragment loop.
half3 WorldGenTreeLeafClip(float2 uv,float material,float shape)
{
    if(material<1.5||material>2.5)return 1;
    float variant=round((material-2)*10);
    half4 leaf=SAMPLE_TEXTURE2D_ARRAY(_TreeLeaves,sampler_TreeLeaves,uv,shape*4+variant);
    clip(leaf.a-.4);return leaf.rgb*1.12;
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
#if SHADER_TARGET >= 45
StructuredBuffer<float4> _WorldGenTreeWindData;
float _WorldGenTreeWindCacheEnabled;
#endif
float3 WorldGenTreeWindCached(float3 p,float3 root,float2 mask,float enabled,float4 traits,float slot)
{
    #if SHADER_TARGET >= 45
    if(_WorldGenTreeWindCacheEnabled>.5&&slot>=0)
    {
        if(enabled<=0)return p;
        float4 cached=_WorldGenTreeWindData[(uint)slot];p+=cached.xyz*(mask.x*mask.x);
        if(mask.y>.5&&mask.y<2.5)
        {
            float3 direction=_WeatherDrivenVegetationWind>.5?_VegetationDirection.xyz:_GrassWindDirection.xyz;
            p+=direction*WorldGenOriginalGrassWave(p)*(.12*cached.w*mask.x);
        }
        return p;
    }
    #endif
    return WorldGenTreeWind(p,root,mask,enabled,traits);
}
#endif
