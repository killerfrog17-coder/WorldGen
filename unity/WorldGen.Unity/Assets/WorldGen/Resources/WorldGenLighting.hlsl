#ifndef WORLDGEN_LIGHTING_INCLUDED
#define WORLDGEN_LIGHTING_INCLUDED
// Keep bundled URP's complete PBR/GI/additional-light implementation. Only its
// main-light acquisition is wrapped; do not fork/copy the BRDF implementation.
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
#define GetMainLight WorldGenPBRMainLight
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#undef GetMainLight
#endif
