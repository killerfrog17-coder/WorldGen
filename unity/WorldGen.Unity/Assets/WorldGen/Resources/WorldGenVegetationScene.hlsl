#ifndef WORLDGEN_VEGETATION_SCENE_INCLUDED
#define WORLDGEN_VEGETATION_SCENE_INCLUDED
// Same camera-scoped 19-vector buffer as terrain. Standalone/custom fixtures
// retain their literal material uniforms unless explicitly opted in.
CBUFFER_START(WorldGenTerrainScene)
float4 _VegetationSceneData[19];
CBUFFER_END
float _VegetationSceneEnabled;
float4 VegetationFog(float4 legacy){return _VegetationSceneEnabled>.5?_VegetationSceneData[0]:legacy;}
// Shared colours are already linear; callers convert only their legacy input.
float3 VegetationFogLinear(float3 legacyLinear){return _VegetationSceneEnabled>.5?_VegetationSceneData[0].rgb:legacyLinear;}
float4 VegetationAmbient(float4 legacy){return _VegetationSceneEnabled>.5?_VegetationSceneData[1]:legacy;}
float VegetationFogDistance(float legacy){return _VegetationSceneEnabled>.5?_VegetationSceneData[2].x:legacy;}
float VegetationDaylight(float legacy){return _VegetationSceneEnabled>.5?_VegetationSceneData[2].y:legacy;}
float4 _AirGeometry;float _AirObserverM,_AirHaze;
int _SecondaryLightCount;float4 _SecondaryDirections[4],_SecondaryColors[4];
#define WORLDGEN_AIR_CUSTOM_DATA
#define WG_AIR_GEOMETRY (_VegetationSceneEnabled>.5?_VegetationSceneData[9]:_AirGeometry)
#define WG_AIR_OBSERVER (_VegetationSceneEnabled>.5?_VegetationSceneData[10].x:_AirObserverM)
#define WG_AIR_HAZE (_VegetationSceneEnabled>.5?_VegetationSceneData[10].y:_AirHaze)
#define WG_SECONDARY_COUNT (_VegetationSceneEnabled>.5?(int)_VegetationSceneData[10].z:_SecondaryLightCount)
#define WG_SECONDARY_DIRECTION(i) (_VegetationSceneEnabled>.5?_VegetationSceneData[11+i]:_SecondaryDirections[i])
#define WG_SECONDARY_COLOR(i) (_VegetationSceneEnabled>.5?_VegetationSceneData[15+i]:_SecondaryColors[i])
#include "WorldGenAir.hlsl"
#endif
