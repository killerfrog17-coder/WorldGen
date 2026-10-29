Shader "Hidden/WorldGen/SurvivalScreen"
{
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline"} ZWrite Off ZTest Always Cull Off
  Pass
  {
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
   float4 _SurvivalEffects;
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float FrostNoise(float2 p)
   {
    float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(hash(cell),hash(cell+float2(1,0)),f.x),lerp(hash(cell+float2(0,1)),hash(cell+1),f.x),f.y);
   }
   half4 Frag(Varyings i):SV_Target
   {
    float2 uv=i.texcoord;float cold=_SurvivalEffects.x,hot=_SurvivalEffects.y,weak=_SurvivalEffects.z,pain=_SurvivalEffects.w;
    float2 centred=uv*2-1;float edge=smoothstep(.45,1,max(abs(centred.x),abs(centred.y)));
    float2 heatWarp=float2(sin(uv.y*90+_Time.y*4),cos(uv.x*80-_Time.y*3))*hot*edge*.0015;
    float3 scene=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+heatWarp).rgb;
    if(weak>.05)
    {
     float2 d=weak*float2(2/_ScreenParams.x,2/_ScreenParams.y);
     float3 blur=scene*2+SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+d).rgb+SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv-d).rgb;
     scene=lerp(scene,blur*.25,weak);
    }
    float2 warp=float2(FrostNoise(uv*9),FrostNoise(uv*9+17));
    float grain=FrostNoise(uv*45+warp*4),fine=FrostNoise(uv*130+warp*13);
    float frost=edge*cold*smoothstep(.12,.85,grain+edge*.6);
    float vein=pow(saturate(1-abs(FrostNoise((uv+warp*.01)*float2(65,45))-.5)*28),3);
    scene=lerp(scene,float3(.72,.84,.89),saturate(frost*(.25+vein*.55+fine*.2)));
    float flame=edge*hot*(.25+.10*sin(uv.x*43+_Time.y*4)+.08*sin(uv.y*57-_Time.y*6));
    scene=lerp(scene,float3(.78,.15,.018),saturate(flame));
    float grey=dot(scene,float3(.2126,.7152,.0722));scene=lerp(scene,grey.xxx,weak*.72);
    float pulse=.5+.5*sin(_Time.y*(weak*2+3));scene*=1-weak*(.16+edge*.55+pulse*.08);
    scene=lerp(scene,float3(.46,.015,.008),pain*edge*.45);
    return half4(scene,1);
   }
   ENDHLSL
  }
 }
}
