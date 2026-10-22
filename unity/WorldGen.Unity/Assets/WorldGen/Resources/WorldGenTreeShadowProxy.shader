Shader "WorldGen/TreeShadowProxy"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass
        {
            Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"}
            Cull Off ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            float4 vert(A a):SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(a);
                float3 world=TransformObjectToWorld(a.positionOS.xyz);
                float4 clip=TransformWorldToHClip(ApplyShadowBias(world,TransformObjectToWorldNormal(a.normalOS),_LightDirection));
                #if UNITY_REVERSED_Z
                clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #else
                clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #endif
                return clip;
            }
            half4 frag():SV_Target{return 0;}
            ENDHLSL
        }
    }
}
