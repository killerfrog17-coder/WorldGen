Shader "WorldGen/CampFlame"
{
 Properties { [HideInInspector] _EmberMode("Embers",Float)=0 _Strength("Fire strength",Float)=1 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent"}
  Pass
  {
   Cull Off ZWrite Off Blend SrcAlpha One
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float _EmberMode,_Strength;
   CBUFFER_END
   struct A {float4 position:POSITION;float2 uv:TEXCOORD0;};struct O{float4 position:SV_POSITION;float2 uv:TEXCOORD0;float seed:TEXCOORD1;};
   O vert(A a){O o;o.position=TransformObjectToHClip(a.position.xyz);o.uv=a.uv;o.seed=dot(TransformObjectToWorld(float3(0,0,0)).xz,float2(1.37,2.31));return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
   half4 frag(O i):SV_Target
   {
    float2 p=i.uv;float t=_Time.y;float n=noise(float2(p.x*5+i.seed,p.y*6-t*3))+noise(float2(p.x*11,p.y*13-t*5))*.4;
    if(_EmberMode>.5)
    {
     float radial=length((p-.5)*2),coal=noise(p*17+i.seed);
     float glow=smoothstep(.45,.75,coal)*(1-smoothstep(.45,1,radial))*(.7+.15*sin(t*2+i.seed));
     return half4(2,.19,.008,glow*.6*_Strength);
    }
    float sway=sin(p.y*9-t*5+i.seed)*.05*p.y;float shape=1-abs(p.x-.5+sway)*2.4/(max(.08,1-p.y*.85));
    float alpha=smoothstep(.1,.5,shape+n*.36-p.y*.5)*(1-smoothstep(.55,1,p.y))*.48*smoothstep(0,.05,p.y);
    float core=saturate(shape-p.y*.7);float3 color=lerp(float3(1,.10,.005),float3(1,.74,.2),core);
    return half4(color*2,alpha*_Strength);
   }
   ENDHLSL
  }
 }
}
