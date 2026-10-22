#ifndef WORLDGEN_TERRAIN_MATERIAL_INCLUDED
#define WORLDGEN_TERRAIN_MATERIAL_INCLUDED
// Every terrain material value (including engine-supplied texture sizes) lives
// in one persistent material buffer. Textures/samplers stay outside it.
CBUFFER_START(UnityPerMaterial)
float4 _FloraSurface_TexelSize,_MidSurface_TexelSize,_ForestSurface_TexelSize;
float4 _LocalOrigin,_TextureAnchor,_PlanetEast,_PlanetNorth,_PlanetUp,_FogColor,_AmbientTint,_LodFade;
float4 _AirGeometry;
float _FloraSurfaceEnabled,_MidSurfaceEnabled,_ForestSurfaceEnabled,_ForestFarEnabled;
float _CoverageEnabled,_CoverageTileSize,_TransitionEnabled,_LodSafetyRadius;
float _Water,_FogDistance,_TextureMetres,_Daylight,_RockLayers,_GroundFlora,_TerrainSceneEnabled;
float _AirObserverM,_AirHaze,_SecondaryLightCount;
float _TerrainCull;
CBUFFER_END
// Legacy/direct material fixtures may still supply these non-property arrays;
// production terrain reads the shared scene array instead. They are globals,
// not undeclared fields inside UnityPerMaterial.
float4 _SecondaryDirections[4],_SecondaryColors[4];

// Nineteen float4s, matching LocalTerrainMaterials.cs. A camera binds ONE shared
// buffer; day/weather/origin changes never rewrite every tile material.
CBUFFER_START(WorldGenTerrainScene)
float4 _TerrainFogColor,_TerrainAmbient,_TerrainSettings;
float4 _TerrainLocalOrigin,_TerrainWaterOrigin,_TerrainTextureAnchor;
float4 _TerrainEast,_TerrainNorth,_TerrainUp,_TerrainAirGeometry,_TerrainAirSettings;
float4 _TerrainSecondaryDirections[4],_TerrainSecondaryColors[4];
CBUFFER_END
float4 TerrainFogColor(){return _TerrainSceneEnabled>.5?_TerrainFogColor:_FogColor;}
float4 TerrainAmbient(){return _TerrainSceneEnabled>.5?_TerrainAmbient:_AmbientTint;}
float TerrainFogDistance(){return _TerrainSceneEnabled>.5?_TerrainSettings.x:_FogDistance;}
float TerrainDaylight(){return _TerrainSceneEnabled>.5?_TerrainSettings.y:_Daylight;}
float TerrainForestEnabled(){return _TerrainSceneEnabled>.5?_TerrainSettings.z:_ForestFarEnabled;}
float4 TerrainOrigin(){return _TerrainSceneEnabled>.5?(_Water>.5?_TerrainWaterOrigin:_TerrainLocalOrigin):_LocalOrigin;}
float4 TerrainTextureAnchor(){return _TerrainSceneEnabled>.5?_TerrainTextureAnchor:_TextureAnchor;}
float4 TerrainEast(){return _TerrainSceneEnabled>.5?_TerrainEast:_PlanetEast;}
float4 TerrainNorth(){return _TerrainSceneEnabled>.5?_TerrainNorth:_PlanetNorth;}
float4 TerrainUp(){return _TerrainSceneEnabled>.5?_TerrainUp:_PlanetUp;}

// The air include keeps the identical equations/default path for vegetation.
// Terrain alone supplies shared-buffer inputs rather than per-material updates.
#define WORLDGEN_AIR_CUSTOM_DATA
#define WG_AIR_GEOMETRY (_TerrainSceneEnabled>.5?_TerrainAirGeometry:_AirGeometry)
#define WG_AIR_OBSERVER (_TerrainSceneEnabled>.5?_TerrainAirSettings.x:_AirObserverM)
#define WG_AIR_HAZE (_TerrainSceneEnabled>.5?_TerrainAirSettings.y:_AirHaze)
#define WG_SECONDARY_COUNT (_TerrainSceneEnabled>.5?(int)_TerrainAirSettings.z:_SecondaryLightCount)
#define WG_SECONDARY_DIRECTION(i) (_TerrainSceneEnabled>.5?_TerrainSecondaryDirections[i]:_SecondaryDirections[i])
#define WG_SECONDARY_COLOR(i) (_TerrainSceneEnabled>.5?_TerrainSecondaryColors[i]:_SecondaryColors[i])
#endif
