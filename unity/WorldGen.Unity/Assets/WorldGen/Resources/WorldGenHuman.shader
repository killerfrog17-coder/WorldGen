Shader "WorldGen/Human"
{
    Properties
    {
        _BloodMask("Runtime impact stains",2D)="black"{} _BloodEnabled("Impact stains",Float)=0 _BloodColor("Blood pigment",Color)=(.23,.012,.009,1)
        _BaseMap("Pigment and opacity",2D)="white"{}
        _Roughness("Roughness",Range(.05,1))=.72
        _Cutoff("Alpha threshold",Range(0,1))=0
        [PerRendererData] _HumanViewId("Human view identity",Float)=0
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "WorldGenBlood.hlsl"
        TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BloodColor;float _BloodEnabled;
        float4 _BaseMap_ST;float _Roughness,_Cutoff,_HumanViewId;
        CBUFFER_END
        // Camera-scoped identity, never a global mutation of a person's mesh.
        float _WorldGenFirstPersonHumanId;
        float _WorldGenFirstPersonBodyVisible;
        struct A {float4 position:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;};
        struct O {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;float2 uv:TEXCOORD2;half head:TEXCOORD3;};
        O vert(A a)
        {
            O o;o.world=TransformObjectToWorld(a.position.xyz);o.positionCS=TransformWorldToHClip(o.world);
            o.normal=TransformObjectToWorldNormal(a.normal);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.head=a.color.r;return o;
        }
        void CutLocalBody(half head)
        {
            bool ownView=_WorldGenFirstPersonHumanId>.5&&abs(_HumanViewId-_WorldGenFirstPersonHumanId)<.25;
            clip(ownView&&(_WorldGenFirstPersonBodyVisible<.5||head>.5)?-1:1);
        }
        half4 Pigment(float2 uv){half4 p=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv);clip(p.a-max(.001,_Cutoff));return p;}
        ENDHLSL
        Pass
        {
            Name "Forward" Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "WorldGenLighting.hlsl"
            #include "WorldGenVegetationScene.hlsl"
            half4 frag(O i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                CutLocalBody(i.head);half3 albedo=WorldGenBlood(Pigment(i.uv).rgb,i.uv,_BloodEnabled,_BloodColor.rgb);
                half3 n=normalize(i.normal)*IS_FRONT_VFACE(face,1,-1);
                InputData input=(InputData)0;input.positionWS=i.world;input.normalWS=n;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);
                input.shadowCoord=TransformWorldToShadowCoord(i.world);input.bakedGI=VegetationAmbient(float4(.12,.14,.17,1)).rgb;input.shadowMask=1;
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                SurfaceData s=(SurfaceData)0;s.albedo=albedo;s.alpha=1;s.occlusion=1;s.normalTS=half3(0,0,1);s.smoothness=1-_Roughness;
                half3 color=WorldGenCanopyPbr(UniversalFragmentPBR(input,s).rgb,albedo,input.bakedGI,1,WorldGenCanopyShadow(1,i.world))+SecondaryCelestialLight(n,albedo);
                color+=CampDiffuseLight(i.world,n,albedo);
                return half4(ApplyPlanetAir(color,i.world,_WorldSpaceCameraPos,VegetationFogLinear(float3(.285,.434,.539)),VegetationFogDistance(60000)),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection,_LightPosition;
            O shadowVert(A a)
            {
                O o=vert(a);float3 direction=_LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                direction=normalize(_LightPosition-o.world);
                #endif
                o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.world,o.normal,direction));
                #if UNITY_REVERSED_Z
                o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #else
                o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            // The complete body casts shadows even when hidden from its own camera.
            half4 shadowFrag(O i):SV_Target{Pigment(i.uv);return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment depthFrag
            half4 depthFrag(O i):SV_Target{CutLocalBody(i.head);Pigment(i.uv);return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags {"LightMode"="DepthNormals"} ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment normalsFrag
            half4 normalsFrag(O i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target{CutLocalBody(i.head);Pigment(i.uv);return half4(normalize(i.normal)*IS_FRONT_VFACE(face,1,-1),0);}
            ENDHLSL
        }
    }
}
