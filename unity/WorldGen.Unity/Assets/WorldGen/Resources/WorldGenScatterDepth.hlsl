#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "WorldGenLodFade.hlsl"
#include "WorldGenWind.hlsl"
#include "WorldGenTree.hlsl"
TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
float4 _BaseMap_ST,_BaseColor,_PlantTraits;float _Cutoff,_Wind,_HerbWind;
float _TreeSurface,_TreeLeafShape;
float3 _LightDirection,_LightPosition;
UNITY_INSTANCING_BUFFER_START(Props)
    UNITY_DEFINE_INSTANCED_PROP(float4,_Tint)
    UNITY_DEFINE_INSTANCED_PROP(float4,_LodTransition)
UNITY_INSTANCING_BUFFER_END(Props)
struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float2 bend:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct O{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float material:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
O vert(A a)
{
    O o;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);
    float3 p=TransformObjectToWorld(a.positionOS.xyz),root=TransformObjectToWorld(float3(0,0,0));
    p=_TreeSurface>.5?WorldGenTreeWind(p,root,a.bend,_Wind,_PlantTraits):WorldGenScatterWind(p,root,_HerbWind>.5?a.bend.x:a.uv.y,_Wind,_HerbWind,_PlantTraits);
    #ifdef WORLDGEN_SHADOW_PASS
    float3 lightDirection=_LightDirection;
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
    lightDirection=normalize(_LightPosition-p);
    #endif
    p=ApplyShadowBias(p,TransformObjectToWorldNormal(a.normalOS),lightDirection);
    #endif
    o.positionCS=TransformWorldToHClip(p);
    #ifdef WORLDGEN_SHADOW_PASS
    #if UNITY_REVERSED_Z
    o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
    #else
    o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
    #endif
    #endif
    o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.material=a.bend.y;return o;
}
half4 frag(O i):SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);WorldGenScatterFade(i.positionCS.xy,UNITY_ACCESS_INSTANCED_PROP(Props,_LodTransition));
    if(_TreeSurface>.5)WorldGenTreeLeafClip(i.uv,i.material,_TreeLeafShape);
    #ifdef _ALPHATEST_ON
    clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a*_BaseColor.a-_Cutoff);
    #endif
    return i.positionCS.z;
}
