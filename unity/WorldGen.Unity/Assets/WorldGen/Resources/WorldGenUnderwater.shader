Shader "Hidden/WorldGen/Underwater"
{
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline"} ZWrite Off ZTest Always Cull Off
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
  TEXTURE2D_X(_WaterBlurTexture);TEXTURE2D_X_FLOAT(_WaterDepthTexture);
  float4 _WaterVision,_WaterFogColour;float4x4 _WaterInverseVP;
  half4 EyeBlur(Varyings i):SV_Target
  {
   float2 uv=i.texcoord,dx=float2(5/_ScreenParams.x,0),dy=float2(0,5/_ScreenParams.y);
   float3 sum=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv).rgb*4;
   sum+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+dx+dy).rgb;
   sum+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv-dx+dy).rgb;
   sum+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+dx-dy).rgb;
   sum+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv-dx-dy).rgb;
   return half4(sum/8,1);
  }
  half4 WaterComposite(Varyings i):SV_Target
  {
   float2 uv=i.texcoord;float3 original=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv).rgb,scene=original;
   if(_WaterVision.y>.5)
   {
    float2 d=float2(10/_ScreenParams.x,10/_ScreenParams.y);
    scene=SAMPLE_TEXTURE2D_X(_WaterBlurTexture,sampler_LinearClamp,uv).rgb*4;
    scene+=SAMPLE_TEXTURE2D_X(_WaterBlurTexture,sampler_LinearClamp,uv+d).rgb+SAMPLE_TEXTURE2D_X(_WaterBlurTexture,sampler_LinearClamp,uv-d).rgb;
    scene+=SAMPLE_TEXTURE2D_X(_WaterBlurTexture,sampler_LinearClamp,uv+float2(d.x,-d.y)).rgb+SAMPLE_TEXTURE2D_X(_WaterBlurTexture,sampler_LinearClamp,uv+float2(-d.x,d.y)).rgb;scene/=8;
   }
   float raw=SAMPLE_TEXTURE2D_X(_WaterDepthTexture,sampler_PointClamp,uv).r;
   #if !UNITY_REVERSED_Z
   raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
   #endif
   float3 at=ComputeWorldSpacePosition(uv,raw,_WaterInverseVP);float3 delta=at-_WorldSpaceCameraPos;
   float distance=min(10000,length(delta));float3 ray=delta/max(.00001,length(delta));
   if(ray.y>.0001)distance=min(distance,max(0,(_WaterVision.z-_WorldSpaceCameraPos.y)/ray.y));
   float transmission=exp(-distance*_WaterVision.w);float3 water=scene*transmission+_WaterFogColour.rgb*(1-transmission);
   return half4(lerp(original,water,_WaterVision.x),1);
  }
  ENDHLSL
  Pass
  {
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment EyeBlur
   ENDHLSL
  }
  Pass
  {
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment WaterComposite
   ENDHLSL
  }
 }
}
