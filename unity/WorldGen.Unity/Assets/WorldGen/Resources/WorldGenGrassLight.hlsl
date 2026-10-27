#ifndef WORLDGEN_GRASS_LIGHT_INCLUDED
#define WORLDGEN_GRASS_LIGHT_INCLUDED
#include "WorldGenFoliageShadows.hlsl"
// Shared leafy-volume lighting for blades, aggregate canopy and terrain cover.
float3 WorldGenGrassLight(float3 albedo,float3 normal,float3 position,float3 ambient,float daylight)
{
    half canopy;Light sun=WorldGenFoliageLight(position,canopy);
    float diffuse=.48+.52*saturate(dot(normal,sun.direction));
    float transmission=pow(saturate(dot(-sun.direction,GetWorldSpaceNormalizeViewDir(position))),3)*.22;
    return albedo*(ambient*WorldGenCanopyAmbient(canopy)+sun.color*(diffuse+transmission)*sun.shadowAttenuation*lerp(.025,1,daylight));
}
#endif
