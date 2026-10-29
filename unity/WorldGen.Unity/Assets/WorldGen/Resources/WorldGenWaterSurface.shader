Shader "WorldGen/WaterSurface"
{
 Properties
 {
  _Water("Water",Float)=1 _WaterNormals("Water slopes",2D)="gray"{}
  [HideInInspector] _SwellScale("Swell displacement",Float)=1
  _FogDistance("Fog distance",Float)=200000 _FogColor("Sky colour",Color)=(.57,.69,.76,1)
  _AmbientTint("Ambient",Color)=(.12,.14,.17,1) _Daylight("Daylight",Float)=1
  [HideInInspector] _TerrainSceneEnabled("Shared scene",Float)=0
  [HideInInspector] _LocalOrigin("Origin",Vector)=(0,0,0,0)
  [HideInInspector] _CoverageEnabled("Coverage",Float)=0 _CoverageTileSize("Tile",Float)=1
  _TerrainCoverage("Coverage",2D)="black"{} _CoverageFadeTimes("Times",2D)="black"{}
  _TransitionEnabled("Transition",Float)=0 _LodFade("Fade",Vector)=(0,0,0,0) _LodSafetyRadius("Safety",Float)=0
  [HideInInspector] _FloraSurface("Flora",2D)="black"{} _FloraSurfaceEnabled("Flora enabled",Float)=0
  [HideInInspector] _MidSurface("Mid flora",2D)="black"{} _MidSurfaceEnabled("Mid enabled",Float)=0
  [HideInInspector] _ForestSurface("Forest",2D)="black"{} _ForestSurfaceEnabled("Forest enabled",Float)=0
 }
 SubShader
 {
  Tags {"RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline"}
  Pass
  {
   Tags {"LightMode"="UniversalForward"} Cull Off ZWrite On Blend One Zero
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
   #include "WorldGenTerrainMaterial.hlsl"
   #include "WorldGenAir.hlsl"
   #include "WorldGenLodFade.hlsl"
   #include "WorldGenWaterMotion.hlsl"
   TEXTURE2D(_WaterNormals);SAMPLER(sampler_WaterNormals);
   Texture2D<float4> _TerrainCoverage,_CoverageFadeTimes;
   struct A {float4 positionOS:POSITION;float4 color:COLOR;};
   struct O {float4 positionCS:SV_POSITION;float3 position:TEXCOORD0;float4 data:TEXCOORD1;float2 coverage:TEXCOORD2;};
   O vert(A v)
   {
    O o;o.position=TransformObjectToWorld(v.positionOS.xyz);o.data=v.color;o.coverage=v.positionOS.xz/max(.001,_CoverageTileSize);
    if(v.color.a<1.5||v.color.a>2.5)o.position.y+=_SwellScale*WaterSwell(o.position.xz+_TerrainLocalOrigin.xy,v.color.b,step(2.5,v.color.a));
    o.positionCS=TransformWorldToHClip(o.position);return o;
   }
   float2 Slopes(float2 uv){return SAMPLE_TEXTURE2D(_WaterNormals,sampler_WaterNormals,uv).rg*2-1;}
   half4 frag(O i):SV_Target
   {
    float dither=WorldGenDither(i.positionCS.xy),distance=length(i.position-_WorldSpaceCameraPos);
    float safety=1-smoothstep(_LodSafetyRadius*.65,max(.001,_LodSafetyRadius),length(i.position.xz-_WorldSpaceCameraPos.xz));
    if(_TransitionEnabled>.5){float f=max(WorldGenFadeWeight(_LodFade.xy),safety);if(_LodFade.z>.5)clip(dither-f);else clip(f-dither);}
    if(_CoverageEnabled>.5)
    {
     float2 uv=clamp(i.coverage,0,.99999994);int node=0;
     [loop]for(int depth=0;depth<24;depth++)
     {
      uv*=2;int2 quadrant=(int2)uv;float4 children=_TerrainCoverage.Load(int3(node%256,node/256,0));float next=children[quadrant.x+2*quadrant.y];
      if(next<0){if(_TransitionEnabled<.5)clip(-1);int index=(int)(-next-1);float2 times=_CoverageFadeTimes.Load(int3(index%256,index/256,0)).xy;clip(dither-max(WorldGenFadeWeight(times),safety));break;}
      if(next<=0)break;node=(int)next-1;uv-=quadrant;
     }
    }
    float river=1-step(.4,abs(i.data.a-2)),ocean=step(2.5,i.data.a);
    float2 p=i.position.xz+_TerrainLocalOrigin.xy,wind=_WorldGenWaterWind.xy;
    float2 flow=lerp(wind*.06,i.data.xy,river);float phase=frac(_Time.y/6),other=frac(phase+.5),blend=abs(phase*2-1);
    float2 slope=lerp(Slopes((p-flow*(phase*6))*.42),Slopes((p-flow*(other*6))*.42),blend);
    slope+=Slopes((p+float2(-flow.y,flow.x)*_Time.y*.2)*.09)*.4;
    float3 normal=normalize(cross(ddy(i.position),ddx(i.position)));if(normal.y<0)normal=-normal;
    float2 swellSlope=(1-river)*WaterSwellSlope(p,i.data.b,ocean);
    if(river<.5)normal=normalize(float3(-swellSlope.x,1,-swellSlope.y));
    normal=normalize(normal+float3(slope.x,0,slope.y)*lerp(.25,.55,saturate(length(wind)/14+river*i.data.z)));
    float3 eye=GetWorldSpaceNormalizeViewDir(i.position);if(dot(normal,eye)<0)normal=-normal;
    float2 screen=i.positionCS.xy/_ScaledScreenParams.xy;float raw=SampleSceneDepth(screen);
    float eyeDepth=LinearEyeDepth(raw,_ZBufferParams),surfaceDepth=-TransformWorldToView(i.position).z;
    float thickness=max(0,eyeDepth-surfaceDepth);
    float2 refracted=clamp(screen+normal.xz*.008*saturate(thickness),.001,.999);
    if(LinearEyeDepth(SampleSceneDepth(refracted),_ZBufferParams)<surfaceDepth)refracted=screen;
    float3 behind=SampleSceneColor(refracted),fog=TerrainFogColor().rgb;
    float3 tint=lerp(float3(.035,.13,.10),float3(.015,.085,.13),ocean)*lerp(.035,1,TerrainDaylight())+TerrainAmbient().rgb*.15;
    float3 transmittance=exp(-min(thickness,150)*lerp(float3(.32,.14,.095),float3(.18,.065,.035),ocean));
    float3 result=behind*transmittance+tint*(1-transmittance);
    float3 reflected=WorldGenFogSkyColor(reflect(-eye,normal),fog);
    float fresnel=.02+.98*pow(1-saturate(dot(normal,eye)),5);result=lerp(result,reflected,fresnel);
    Light sun=GetMainLight();float glint=pow(saturate(dot(normal,normalize(eye+sun.direction))),180);
    result+=sun.color*glint*.65*TerrainDaylight();
    float flecks=lerp(SAMPLE_TEXTURE2D(_WaterNormals,sampler_WaterNormals,(p-flow*(phase*6))*.17).b,SAMPLE_TEXTURE2D(_WaterNormals,sampler_WaterNormals,(p-flow*(other*6))*.17).b,blend);
    float foam=(1-smoothstep(.03,.5,thickness))*.08+river*(flecks*lerp(.22,.6,saturate(i.data.z))*smoothstep(.15,.6,length(flow))+saturate(i.data.z)*smoothstep(.45,.8,abs(slope.x+slope.y))*.3);
    result=lerp(result,float3(.72,.8,.77)*lerp(.035,1,TerrainDaylight()),saturate(foam));
    return half4(ApplyPlanetAir(result,i.position,_WorldSpaceCameraPos,fog,TerrainFogDistance()),1);
   }
   ENDHLSL
  }
 }
}
