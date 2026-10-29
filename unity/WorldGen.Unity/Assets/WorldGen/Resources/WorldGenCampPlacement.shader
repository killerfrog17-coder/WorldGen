Shader "WorldGen/CampPlacement"
{
 Properties {_BaseColor("Colour",Color)=(.3,.8,.5,1)}
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent"}
  Pass
  {
   Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   float4 _BaseColor;
   struct A{float4 position:POSITION;};struct O{float4 position:SV_POSITION;};
   O vert(A a){O o;o.position=TransformObjectToHClip(a.position.xyz);return o;}
   half4 frag(O i):SV_Target{return _BaseColor;}
   ENDHLSL
  }
 }
}
