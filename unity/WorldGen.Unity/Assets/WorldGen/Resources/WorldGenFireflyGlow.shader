Shader "WorldGen/FireflyGlow"
{
    Properties { _VegetationSceneEnabled("Shared atmosphere",Float)=0 _FireflyGlowColor("Glow",Color)=(1,1,0,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WorldGenVegetationScene.hlsl"
            UNITY_INSTANCING_BUFFER_START(Fireflies)
                UNITY_DEFINE_INSTANCED_PROP(float4,_FireflyGlowColor)
            UNITY_INSTANCING_BUFFER_END(Fireflies)
            struct Attributes { float3 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings Vert(Attributes input)
            {
                Varyings output;UNITY_SETUP_INSTANCE_ID(input);UNITY_TRANSFER_INSTANCE_ID(input,output);
                output.world=TransformObjectToWorld(input.positionOS);output.positionCS=TransformWorldToHClip(output.world);output.uv=input.uv;return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float r=length(input.uv*2-1);float glow=exp(-r*r*10)*smoothstep(1,.65,r);
                float distance=length(input.world-_WorldSpaceCameraPos);
                // Extinction only: adding fog colour to an additive sprite would illuminate its rectangular footprint.
                float transmission=(1-WorldGenStreamingFogOpacity(distance,VegetationFogDistance(60000)))*exp(-distance*(.00002+max(0,WG_AIR_HAZE)*.000012));
                return half4(UNITY_ACCESS_INSTANCED_PROP(Fireflies,_FireflyGlowColor).rgb*glow*transmission,0);
            }
            ENDHLSL
        }
    }
}
