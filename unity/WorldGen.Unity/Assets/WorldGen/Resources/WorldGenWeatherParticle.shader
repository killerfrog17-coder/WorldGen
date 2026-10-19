Shader "WorldGen/WeatherParticle"
{
    Properties { _Snow("Snow",Float)=0 _Bolt("Bolt",Float)=0 _LightTint("Light",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float4 position:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct B {float4 position:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            float _Snow,_Bolt;float4 _LightTint;
            B Vert(A i){B o;o.position=TransformObjectToHClip(i.position.xyz);o.uv=i.uv;o.color=i.color;return o;}
            half4 Frag(B i):SV_Target
            {
                float2 p=i.uv*2-1;float a=_Snow>.5?saturate(1-dot(p,p)):saturate(1-abs(p.x))*saturate(1-abs(p.y));
                return half4(i.color.rgb*(_Bolt>.5?float3(3,3,3):_LightTint.rgb),i.color.a*(_Bolt>.5?1:a));
            }
            ENDHLSL
        }
    }
}
