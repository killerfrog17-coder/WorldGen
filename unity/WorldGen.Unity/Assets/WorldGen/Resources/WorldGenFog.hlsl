#ifndef WORLDGEN_FOG_INCLUDED
#define WORLDGEN_FOG_INCLUDED
// Camera-scoped scene globals. Colours are already in the render colour space.
// Horizon.w enables them; Zenith.w is the streaming range; Glow.w is air density.
float4 _WorldGenFogSkyZenith,_WorldGenFogSkyHorizon,_WorldGenFogSkyGlow;
float3 WorldGenSkyAirColor(float height,float3 zenith,float3 horizon,float3 glow,float density)
{
    float up=saturate(height);
    float3 color=lerp(horizon,zenith,pow(up,.42));
    if(height<0)color=lerp(horizon,zenith*.15,saturate(-height*3));
    return lerp(zenith*.05,color,density)+glow*density*(1.4-up*.4);
}
float3 WorldGenFogSkyColor(float3 ray,float3 fallback)
{
    if(_WorldGenFogSkyHorizon.w<.5)return fallback;
    return WorldGenSkyAirColor(ray.y,_WorldGenFogSkyZenith.rgb,_WorldGenFogSkyHorizon.rgb,_WorldGenFogSkyGlow.rgb,_WorldGenFogSkyGlow.w);
}
float WorldGenStreamingFogOpacity(float distance,float limit)
{return smoothstep(max(.001,limit)*.72,max(.001,limit),distance);}
#endif
