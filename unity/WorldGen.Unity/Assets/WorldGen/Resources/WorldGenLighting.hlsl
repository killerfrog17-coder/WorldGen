#ifndef WORLDGEN_LIGHTING_INCLUDED
#define WORLDGEN_LIGHTING_INCLUDED
// Keep bundled URP's PBR/GI/additional-light implementation; wrap main-light
// visibility and supply our own rough-surface environment, not a second BRDF.
// RealtimeLights must be defined BEFORE the narrowly scoped macro below.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#include "WorldGenCanopyShadows.hlsl"
Light WorldGenPBRMainLight(){return GetMainLight();}
Light WorldGenPBRMainLight(InputData input,half4 shadowMask,AmbientOcclusionFactor ao)
{
    Light light=GetMainLight(input,shadowMask,ao);
    light.shadowAttenuation=WorldGenCanopyShadow(light.shadowAttenuation,input.positionWS);
    return light;
}
// The camera skybox does not replace the scene's default reflection probe.
// Its daylight cubemap must not illuminate bare cliffs during our custom night.
// Rough terrain/scatter use the generated sky's mean radiance (including night
// and moonlight) instead. No cubemap capture, texture read or per-tile update.
float4 _WorldGenSkyReflection;
half4 WorldGenEnvironmentColor()
{
    // Preserve a sane engine fallback for standalone materials/editor fixtures.
    return lerp(_GlossyEnvironmentColor,(half4)_WorldGenSkyReflection,saturate(_WorldGenSkyReflection.w));
}
#ifndef _ENVIRONMENTREFLECTIONS_OFF
#define _ENVIRONMENTREFLECTIONS_OFF
#define WORLDGEN_RESTORE_ENVIRONMENT_KEYWORD
#endif
#define _GlossyEnvironmentColor WorldGenEnvironmentColor()
#define GetMainLight WorldGenPBRMainLight
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#undef GetMainLight
#undef _GlossyEnvironmentColor
#ifdef WORLDGEN_RESTORE_ENVIRONMENT_KEYWORD
#undef _ENVIRONMENTREFLECTIONS_OFF
#undef WORLDGEN_RESTORE_ENVIRONMENT_KEYWORD
#endif
#endif
