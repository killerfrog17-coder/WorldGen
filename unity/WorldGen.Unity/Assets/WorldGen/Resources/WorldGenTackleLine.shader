Shader "WorldGen/TackleLine"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{float4 position:POSITION;};struct O{float4 position:SV_POSITION;};
            O vert(A a){O o;o.position=TransformObjectToHClip(a.position.xyz);return o;}
            half4 frag(O i):SV_Target{return half4(.42,.34,.22,1);}
            ENDHLSL
        }
    }
}
