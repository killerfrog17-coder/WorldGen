#ifndef WORLDGEN_FOLIAGE_SHADOWS_INCLUDED
#define WORLDGEN_FOLIAGE_SHADOWS_INCLUDED
// Camera-scoped: enabled, shadow range, filter footprint in shadow texels, unused.
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
            half value=SampleShadowmapFilteredLowQuality(TEXTURE2D_ARGS(_MainLightShadowmapTexture,sampler_LinearClampCompare),coord,sampling);
            light.shadowAttenuation=LerpWhiteTo(value,GetMainLightShadowParams().x);
        }
        #endif
        light.shadowAttenuation=lerp(light.shadowAttenuation,1,fade);
    }
    #endif
    }
    return light;
}
#endif
