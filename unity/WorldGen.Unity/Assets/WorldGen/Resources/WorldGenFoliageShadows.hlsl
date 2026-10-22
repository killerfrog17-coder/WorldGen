#ifndef WORLDGEN_FOLIAGE_SHADOWS_INCLUDED
#define WORLDGEN_FOLIAGE_SHADOWS_INCLUDED
#include "WorldGenCanopyShadows.hlsl"
// Camera-scoped: enabled, shadow range, filter footprint in shadow texels, taps.
// Keep the stock path for standalone fixtures and for the owner A/B toggle.
float4 _WorldGenFoliageShadowSettings;
Light WorldGenFoliageLight(float3 position)
{
    Light light=GetMainLight();
    if(_WorldGenFoliageShadowSettings.x<.5)
    {
        light.shadowAttenuation=MainLightRealtimeShadow(TransformWorldToShadowCoord(position));
    }
    else
    {
    #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    float fade=GetMainLightShadowFade(position);
    [branch] if(fade<.999)
    {
        float4 coord=TransformWorldToShadowCoord(position);
        #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        light.shadowAttenuation=MainLightRealtimeShadow(coord);
        #else
        [branch] if(!(BEYOND_SHADOW_FAR(coord))&&all(coord.xy>=0)&&all(coord.xy<=1))
        {
            ShadowSamplingData sampling=GetMainLightShadowSamplingData();
            sampling.shadowOffset0*=_WorldGenFoliageShadowSettings.z;
            sampling.shadowOffset1*=_WorldGenFoliageShadowSettings.z;
            half value;
            if(_WorldGenFoliageShadowSettings.w>8)
            {
                sampling.shadowmapSize.xy/=max(.1,_WorldGenFoliageShadowSettings.z);
                sampling.shadowmapSize.zw*=max(.1,_WorldGenFoliageShadowSettings.z);
                value=SampleShadowmapFilteredMediumQuality(TEXTURE2D_ARGS(_MainLightShadowmapTexture,sampler_LinearClampCompare),coord,sampling);
            }
            else if(_WorldGenFoliageShadowSettings.w>0&&_WorldGenFoliageShadowSettings.w<3)
            {
                value=.5*(SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture,sampler_LinearClampCompare,coord.xyz+float3(sampling.shadowOffset0.xy,0))+
                          SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture,sampler_LinearClampCompare,coord.xyz+float3(sampling.shadowOffset1.zw,0)));
            }
            else value=SampleShadowmapFilteredLowQuality(TEXTURE2D_ARGS(_MainLightShadowmapTexture,sampler_LinearClampCompare),coord,sampling);
            light.shadowAttenuation=LerpWhiteTo(value,GetMainLightShadowParams().x);
        }
        #endif
        light.shadowAttenuation=lerp(light.shadowAttenuation,1,fade);
    }
    #endif
    }
    light.shadowAttenuation=WorldGenCanopyShadow(light.shadowAttenuation,position);
    return light;
}
#endif
