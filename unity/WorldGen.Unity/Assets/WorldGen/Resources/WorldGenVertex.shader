Shader "WorldGen/VertexSurface"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float4 color:COLOR;float3 normalOS:NORMAL;};
            struct Varyings {float4 positionCS:SV_POSITION;float4 color:COLOR;float3 normalWS:TEXCOORD0;};
            Varyings vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.color=i.color;o.normalWS=TransformObjectToWorldNormal(length(i.normalOS)>.1?i.normalOS:normalize(i.positionOS.xyz));return o;}
            half4 frag(Varyings i):SV_Target{float light=.55+.45*saturate(dot(normalize(i.normalWS),normalize(float3(-.5,.7,-1))));return half4(i.color.rgb*light,1);}
            ENDHLSL
        }
    }
}
