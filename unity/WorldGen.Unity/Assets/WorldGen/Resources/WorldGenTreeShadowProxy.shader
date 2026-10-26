Shader "WorldGen/TreeShadowProxy"
{
    Properties {
        _ShadowCard("Baked silhouette",Float)=0 _ShadowCoverage("LOD coverage interval",Vector)=(0,1,0,0)
        _TreeColourViews("Tree silhouettes",2DArray)=""{}
        _ImpostorBounds("Object centre/radius",Vector)=(0,.5,0,1)
    }
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
            #include "WorldGenLodFade.hlsl"
            #include "WorldGenTreeViews.hlsl"
            float3 _LightDirection;
            float _ShadowCard;float4 _ImpostorBounds;
            UNITY_INSTANCING_BUFFER_START(ShadowProps)
                UNITY_DEFINE_INSTANCED_PROP(float4,_ShadowCoverage)
            UNITY_INSTANCING_BUFFER_END(ShadowProps)
            struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct O{float4 positionCS:SV_POSITION;float2 uvA:TEXCOORD0;float2 uvB:TEXCOORD1;float2 uvC:TEXCOORD2;nointerpolation uint3 indices:TEXCOORD3;nointerpolation float3 weights:TEXCOORD4;nointerpolation float2 coverage:TEXCOORD5;};
            O vert(A a)
            {
                UNITY_SETUP_INSTANCE_ID(a);O o=(O)0;
                float3 p=a.positionOS.xyz,normal=TransformObjectToWorldNormal(a.normalOS);
                if(_ShadowCard>.5)
                {
                    float3 d=normalize(mul((float3x3)GetWorldToObjectMatrix(),_LightDirection)),r,u;WorldGenTreeViewBasis(d,r,u);
                    float3 local=(r*(a.uv.x*2-1)+u*(a.uv.y*2-1))*_ImpostorBounds.w;
                    p=_ImpostorBounds.xyz+local;normal=_LightDirection;
                    WorldGenTreeSelectViews(d,o.indices,o.weights);
                    o.uvA=WorldGenTreeViewUV(o.indices.x,local,d,_ImpostorBounds.w);o.uvB=WorldGenTreeViewUV(o.indices.y,local,d,_ImpostorBounds.w);o.uvC=WorldGenTreeViewUV(o.indices.z,local,d,_ImpostorBounds.w);
                }
                float3 world=TransformObjectToWorld(p);
                float4 clip=TransformWorldToHClip(ApplyShadowBias(world,normal,_LightDirection));
                #if UNITY_REVERSED_Z
                clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #else
                clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #endif
                o.positionCS=clip;o.coverage=UNITY_ACCESS_INSTANCED_PROP(ShadowProps,_ShadowCoverage).xy;return o;
            }
            half4 frag(O i):SV_Target
            {
                float d=WorldGenDither(i.positionCS.xy);clip(d-i.coverage.x);clip(i.coverage.y-d);
                if(_ShadowCard>.5)
                {
                    half a=SAMPLE_TEXTURE2D_ARRAY(_TreeColourViews,sampler_TreeColourViews,i.uvA,i.indices.x).a*(all(i.uvA>=0)&&all(i.uvA<=1));
                    half b=SAMPLE_TEXTURE2D_ARRAY(_TreeColourViews,sampler_TreeColourViews,i.uvB,i.indices.y).a*(all(i.uvB>=0)&&all(i.uvB<=1));
                    half c=SAMPLE_TEXTURE2D_ARRAY(_TreeColourViews,sampler_TreeColourViews,i.uvC,i.indices.z).a*(all(i.uvC>=0)&&all(i.uvC<=1));
                    clip(dot(float3(a,b,c),i.weights)-.4);
                }
                return 0;
            }
            ENDHLSL
        }
    }
}
