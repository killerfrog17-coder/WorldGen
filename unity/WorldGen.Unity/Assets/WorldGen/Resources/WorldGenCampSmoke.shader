Shader "WorldGen/CampSmoke"
{
 Properties { _VegetationSceneEnabled("Shared lighting",Float)=1 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass
  {
   Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "WorldGenVegetationScene.hlsl"
   struct A{float4 position:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
   struct O{float4 position:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;half4 color:COLOR;};
   O vert(A a){O o;o.world=TransformObjectToWorld(a.position.xyz);o.position=TransformWorldToHClip(o.world);o.uv=a.uv;o.color=a.color;return o;}
   half4 frag(O i):SV_Target
   {
    float2 p=(i.uv-.5)*2;
    float shape=saturate(1-dot(p,p));shape*=shape;
    float wisps=.75+.25*sin(p.x*7+p.y*9)*sin(p.y*5-p.x*6);
    float3 color=lerp(float3(.045,.04,.035),float3(.32,.31,.29),saturate(VegetationDaylight(1)));
    color=ApplyPlanetAir(color,i.world,_WorldSpaceCameraPos,VegetationFogLinear(float3(.285,.434,.539)),VegetationFogDistance(60000));
    return half4(color,shape*wisps*i.color.a);
   }
   ENDHLSL
  }
 }
}
