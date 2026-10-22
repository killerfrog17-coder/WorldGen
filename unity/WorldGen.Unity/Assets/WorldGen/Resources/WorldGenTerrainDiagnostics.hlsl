#ifndef WORLDGEN_TERRAIN_DIAGNOSTICS_INCLUDED
#define WORLDGEN_TERRAIN_DIAGNOSTICS_INCLUDED
// Matches TerrainDiagnosticFlags. Camera-scoped, zero for normal rendering.
uint _TerrainDiagnosticFlags;
float _TerrainDiagnosticRadius;
#define TD_FLAT 1u
#define TD_TEXTURES 2u
#define TD_ALBEDO 4u
#define TD_NORMALS 8u
#define TD_MASKS 16u
#define TD_AXIS 32u
#define TD_LAYER 64u
#define TD_SIMPLE 128u
#define TD_UNLIT 256u
#define TD_GRASS 512u
#define TD_MOSS 1024u
#define TD_LICHEN 2048u
#define TD_MID 4096u
#define TD_FOREST 8192u
#define TD_AIR 16384u
#define TD_GRAIN 32768u
#define TD_NEAR 65536u
#define TD_COVERAGE 131072u
bool TerrainTest(uint flags)
{
    #if defined(WORLDGEN_TERRAIN_LAB)
    return (_TerrainDiagnosticFlags&flags)!=0u;
    #else
    return false; // Compile diagnostic branches out of normal rendering.
    #endif
}
float3 TerrainTestAxes(float3 weights)
{
    if(!TerrainTest(TD_AXIS))return weights;
    return weights.x>=weights.y&&weights.x>=weights.z?float3(1,0,0):weights.y>=weights.z?float3(0,1,0):float3(0,0,1);
}
float4 TerrainArray(TEXTURE2D_ARRAY_PARAM(tex,samp),float3 p,float3 weights,int layer)
{
    if(TerrainTest(TD_AXIS))
        return SAMPLE_TEXTURE2D_ARRAY(tex,samp,weights.x>.5?p.yz:weights.y>.5?p.xz:p.xy,layer);
    return SAMPLE_TEXTURE2D_ARRAY(tex,samp,p.yz,layer)*weights.x+
           SAMPLE_TEXTURE2D_ARRAY(tex,samp,p.xz,layer)*weights.y+
           SAMPLE_TEXTURE2D_ARRAY(tex,samp,p.xy,layer)*weights.z;
}
float3 TerrainMat(TEXTURE2D_PARAM(tex,samp),float3 p,float3 weights)
{
    if(TerrainTest(TD_AXIS))return SAMPLE_TEXTURE2D(tex,samp,weights.x>.5?p.yz:weights.y>.5?p.xz:p.xy).rgb;
    return SAMPLE_TEXTURE2D(tex,samp,p.yz).rgb*weights.x+SAMPLE_TEXTURE2D(tex,samp,p.xz).rgb*weights.y+SAMPLE_TEXTURE2D(tex,samp,p.xy).rgb*weights.z;
}
float3 TerrainTestAir(float3 colour,float3 position,float3 eye,float3 fog,float range)
{[branch] if(TerrainTest(TD_AIR))return colour;return ApplyPlanetAir(colour,position,eye,fog,range);}
float3 TerrainTestLeafLight(float3 albedo,float3 normal,float3 position,float3 ambient,float daylight)
{[branch] if(TerrainTest(TD_UNLIT))return albedo;return WorldGenGrassLight(albedo,normal,position,ambient,daylight);}
float3 TerrainTestMatLight(float3 normal,float3 position,float3 ambient,float daylight)
{
    [branch] if(TerrainTest(TD_UNLIT))return 1;
    Light sun=GetMainLight(TransformWorldToShadowCoord(position));
    return ambient+sun.color*(.42+.58*saturate(dot(normal,sun.direction)))*sun.shadowAttenuation*lerp(.025,1,daylight);
}
#endif
