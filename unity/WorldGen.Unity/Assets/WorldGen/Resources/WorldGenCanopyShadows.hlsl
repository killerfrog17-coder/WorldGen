#ifndef WORLDGEN_CANOPY_SHADOWS_INCLUDED
#define WORLDGEN_CANOPY_SHADOWS_INCLUDED
TEXTURE2D(_WorldGenCanopyPrevious);TEXTURE2D(_WorldGenCanopyCurrent);
SAMPLER(sampler_WorldGenCanopyCurrent);
float4 _WorldGenCanopyRect0,_WorldGenCanopyRect1,_WorldGenCanopySettings;
float _WorldGenCanopyAmbientDay;
// Reuse the shelter already evaluated for direct light. Dense overlapping crowns
// attenuate more sky fill than sparse cover; no additional field reads or passes.
half WorldGenCanopyAmbient(half shade)
{
    half cover=saturate((1-shade)*(1.0/.78));
    return 1-.28*cover*cover*_WorldGenCanopyAmbientDay;
}
float WorldGenCanopyCoverage(float4 sample,float3 position,float edge)
{
    float coverage=sample.b;
    float top=sample.r/max(.0001,coverage),bottom=sample.g/max(.0001,coverage);
    return coverage*edge*(1-smoothstep(bottom,max(bottom+2,top),position.y));
}
float WorldGenCanopyVisibility(float3 position)
{
    float2 uv1=(position.xz-_WorldGenCanopyRect1.xy)*_WorldGenCanopyRect1.z;
    float edge1=saturate(min(min(uv1.x,uv1.y),min(1-uv1.x,1-uv1.y))*16)*_WorldGenCanopyRect1.w;
    float cover=0;
    [branch] if(edge1>0)
    {
        float4 current=SAMPLE_TEXTURE2D_LOD(_WorldGenCanopyCurrent,sampler_WorldGenCanopyCurrent,uv1,0);
        cover=WorldGenCanopyCoverage(current,position,edge1);
    }
    // Steady state: one read inside coverage, none over the distant horizon.
    // Only a half-second field replacement needs both textures.
    [branch] if(_WorldGenCanopySettings.y<1)
    {
        float2 uv0=(position.xz-_WorldGenCanopyRect0.xy)*_WorldGenCanopyRect0.z;
        float edge0=saturate(min(min(uv0.x,uv0.y),min(1-uv0.x,1-uv0.y))*16)*_WorldGenCanopyRect0.w;
        float old=0;
        [branch] if(edge0>0)
        {
            float4 previous=SAMPLE_TEXTURE2D_LOD(_WorldGenCanopyPrevious,sampler_WorldGenCanopyCurrent,uv0,0);
            old=WorldGenCanopyCoverage(previous,position,edge0);
        }
        cover=lerp(old,cover,_WorldGenCanopySettings.y);
    }
    return 1-saturate(cover)*.78;
}
half WorldGenCanopyShadow(half detailed,float3 position)
{
    #if !defined(_RECEIVE_SHADOWS_OFF)
    [branch] if(_WorldGenCanopySettings.x>.5)
    {
        float distance=length(position-_WorldSpaceCameraPos);
        [branch] if(distance>_WorldGenCanopySettings.z)
        {
            float broad=lerp(1,WorldGenCanopyVisibility(position),smoothstep(_WorldGenCanopySettings.z,_WorldGenCanopySettings.w,distance));
            detailed=min(detailed,broad);
        }
    }
    #endif
    return detailed;
}
// Approximate only the sunlit part of PBR output. Retain indirect sky light,
// solid-object shadow outlines, secondary lights and emissions.
half3 WorldGenCanopyPbr(half3 color,half3 albedo,half3 ambient,half ao,half shade)
{
    half3 indirect=albedo*ambient*ao;
    return max(0,color-max(0,color-indirect)*(1-shade)-indirect*(1-WorldGenCanopyAmbient(shade)));
}
#endif
