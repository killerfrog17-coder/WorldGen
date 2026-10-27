Shader "WorldGen/InventoryPreview"
{
    Properties { _BaseMap("Pigment",2D)="white"{} _Color("Colour",Color)=(1,1,1,1) _Cutoff("Cutout",Float)=0.001 [PerRendererData] _InventoryWearerId("Wearer",Float)=0 [PerRendererData] _WorldLighting("World lighting",Float)=0 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST,_Color;float _Cutoff,_InventoryWearerId,_WorldLighting;
            CBUFFER_END
            float _WorldGenFirstPersonHumanId;
            struct A{float4 position:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
            struct O{float4 position:SV_POSITION;float3 normal:TEXCOORD0;float2 uv:TEXCOORD1;};
            O vert(A a){O o;o.position=TransformObjectToHClip(a.position.xyz);o.normal=TransformObjectToWorldNormal(a.normal);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);return o;}
            half4 frag(O i):SV_Target
            {
                half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_Color;clip(c.a-max(.001,_Cutoff));
                clip(_InventoryWearerId>.5&&_WorldGenFirstPersonHumanId>.5&&abs(_InventoryWearerId-_WorldGenFirstPersonHumanId)<.25?-1:1);
                if(_WorldLighting>.5){Light sun=GetMainLight();return half4(c.rgb*(SampleSH(normalize(i.normal))+sun.color*saturate(dot(normalize(i.normal),sun.direction))),1);}
                half light=.42+.48*saturate(dot(normalize(i.normal),normalize(float3(-.5,.7,-.8))))+.16*saturate(dot(normalize(i.normal),normalize(float3(.8,.2,.6))));
                return half4(c.rgb*light,1);
            }
            ENDHLSL
        }
    }
}
