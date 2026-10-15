Shader "WorldGen/HistoryBorder"
{
    Properties { _Opacity ("Opacity", Float)=1 _MapMode ("Map", Float)=0 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _MapMode,_Opacity;
            struct A{float4 positionOS:POSITION;float3 mapPosition:TEXCOORD1;};
            struct V{float4 positionCS:SV_POSITION;float x:TEXCOORD0;};
            V vert(A i){V o;float3 p=_MapMode>.5?i.mapPosition:i.positionOS.xyz;o.positionCS=TransformObjectToHClip(p);o.x=TransformObjectToWorld(p).x;return o;}
            half4 frag(V i):SV_Target{if(_MapMode>.5)clip(2-abs(i.x));return half4(.08,.09,.10,_Opacity);}
            ENDHLSL
        }
    }
}
