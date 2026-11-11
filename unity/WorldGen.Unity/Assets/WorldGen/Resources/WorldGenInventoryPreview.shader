Shader "WorldGen/InventoryPreview"
{
    Properties { _PigmentMode("Inherited pigment region",Float)=0 _PigmentColor("Inherited colour",Color)=(1,1,1,1) _PigmentReference("Source luminance",Float)=.12 _VertexColours("Vertex colours",Float)=0  _BaseMap("Pigment",2D)="white"{} _Color("Colour",Color)=(1,1,1,1) _Cutoff("Cutout",Float)=0.001 [PerRendererData] _InventoryWearerId("Wearer",Float)=0 [PerRendererData] _WorldLighting("World lighting",Float)=0 }
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
            float4 _BaseMap_ST,_Color;float _Cutoff,_InventoryWearerId,_WorldLighting,_VertexColours;
            float4 _PigmentColor;float _PigmentMode,_PigmentReference;
            CBUFFER_END
            float _WorldGenFirstPersonHumanId;
            struct A{float4 position:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct O{float4 position:SV_POSITION;float3 normal:TEXCOORD0;float2 uv:TEXCOORD1;float4 color:COLOR;};
            O vert(A a){O o;o.position=TransformObjectToHClip(a.position.xyz);o.normal=TransformObjectToWorldNormal(a.normal);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.color=a.color;return o;}
            half4 frag(O i):SV_Target
            {
                half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);clip(c.a-max(.001,_Cutoff));
                if(_PigmentMode>.5)
                {
                    half luminance=dot(c.rgb,half3(.2126,.7152,.0722));half detail=clamp(luminance/max(.002,_PigmentReference),.22,2.2);
                    if(_PigmentMode<2.5)c.rgb=_PigmentColor.rgb*detail;
                    else{half chroma=max(c.r,max(c.g,c.b))-min(c.r,min(c.g,c.b));half iris=saturate(chroma*35)*smoothstep(.002,.012,luminance)*(1-smoothstep(.55,.8,luminance));c.rgb=lerp(c.rgb,_PigmentColor.rgb*clamp(detail,.4,1.5),iris);}
                }
                c*=_Color*lerp(half4(1,1,1,1),i.color,_VertexColours);
                clip(_InventoryWearerId>.5&&_WorldGenFirstPersonHumanId>.5&&abs(_InventoryWearerId-_WorldGenFirstPersonHumanId)<.25?-1:1);
                if(_WorldLighting>.5){Light sun=GetMainLight();return half4(c.rgb*(SampleSH(normalize(i.normal))+sun.color*saturate(dot(normalize(i.normal),sun.direction))),1);}
                half light=.42+.48*saturate(dot(normalize(i.normal),normalize(float3(-.5,.7,-.8))))+.16*saturate(dot(normalize(i.normal),normalize(float3(.8,.2,.6))));
                return half4(c.rgb*light,1);
            }
            ENDHLSL
        }
    }
}
