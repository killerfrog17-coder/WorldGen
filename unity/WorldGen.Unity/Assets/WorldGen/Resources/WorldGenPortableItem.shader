Shader "WorldGen/PortableItem"
{
    Properties
    {
        _SourceCoats("Original animal coats",2DArray)=""{}
        _ItemSurfaces("Shared craft surface atlas",2D)="white"{}
        _HasFur("Use original coat",Float)=0 _Coat("Coat",Float)=0
        _FurRect("Torso UV rectangle",Vector)=(0,0,1,1)
        _Preview("Inventory lighting",Float)=0 _Wearer("Camera-scoped wearer",Float)=0
        _BaseMap("Shadow opacity",2D)="white"{} _BaseColor("Shadow tint",Color)=(1,1,1,1) _Cutoff("Cutoff",Float)=0 _Cull("Cull",Float)=0
        _VegetationSceneEnabled("Shared lighting",Float)=1 _FogDistance("Fog distance",Float)=60000
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "WorldGenLighting.hlsl"
        #include "WorldGenVegetationScene.hlsl"
        #include "WorldGenAir.hlsl"
        TEXTURE2D_ARRAY(_SourceCoats);SAMPLER(sampler_SourceCoats);
        TEXTURE2D(_ItemSurfaces);SAMPLER(sampler_ItemSurfaces);
        float _HasFur,_Coat,_Preview,_Wearer,_FogDistance;float4 _FurRect;float _WorldGenFirstPersonHumanId;
        struct A{float4 position:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float2 surface:TEXCOORD1;half4 color:COLOR;};
        struct O{float4 position:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;half4 color:COLOR;nointerpolation float surface:TEXCOORD3;};
        O vert(A a){O o;o.position=TransformObjectToHClip(a.position.xyz);o.world=TransformObjectToWorld(a.position.xyz);o.normal=TransformObjectToWorldNormal(a.normal);o.uv=a.uv;o.color=a.color;o.surface=a.surface.x;return o;}
        void ClipWearer(){clip(_Preview<.5&&_Wearer>.5&&_WorldGenFirstPersonHumanId>.5&&abs(_Wearer-_WorldGenFirstPersonHumanId)<.25?-1:1);}
        ENDHLSL
        Pass
        {
            Tags {"LightMode"="UniversalForward"} Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            half4 frag(O i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                ClipWearer();half3 color=i.color.rgb;
                float slot=clamp(round(i.surface),0,7);float2 tile=float2(fmod(slot,4),1-floor(slot/4));
                float2 grain=1-abs(frac(i.uv*2)*2-1);float2 texuv=(tile+lerp(.012,.988,grain))*float2(.25,.5);
                half3 surface=i.surface>7.5?half3(1,1,1):SAMPLE_TEXTURE2D(_ItemSurfaces,sampler_ItemSurfaces,texuv).rgb;
                color=i.surface>7.5?i.color.rgb:surface*(.7+.6*i.color.rgb);
                if(_HasFur>.5&&i.color.a>.5)color=SAMPLE_TEXTURE2D_ARRAY(_SourceCoats,sampler_SourceCoats,lerp(_FurRect.xy,_FurRect.zw,saturate(i.uv)),_Coat).rgb;
                half3 n=normalize(i.normal)*IS_FRONT_VFACE(face,1,-1);
                if(_Preview>.5){half light=.45+.5*saturate(dot(n,normalize(float3(-.5,.8,-.6))));return half4(color*light,1);}
                InputData input=(InputData)0;input.positionWS=i.world;input.normalWS=n;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);
                input.shadowCoord=TransformWorldToShadowCoord(i.world);input.bakedGI=VegetationAmbient(float4(.12,.14,.17,1)).rgb;input.shadowMask=1;
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.position);
                SurfaceData material=(SurfaceData)0;material.albedo=color;material.alpha=1;material.occlusion=1;material.normalTS=half3(0,0,1);
                // Rough, nonmetallic natural materials. One shared albedo read;
                // no generated per-item normal/mask textures or reflection maps.
                material.smoothness=slot<.5?.28:slot<1.5?.16:slot<2.5?.05:slot<3.5?.1:slot<4.5?.14:slot<5.5?.24:.04;
                if(_HasFur>.5&&i.color.a>.5)material.smoothness=.04;
                color=WorldGenCanopyPbr(UniversalFragmentPBR(input,material).rgb,material.albedo,input.bakedGI,1,WorldGenCanopyShadow(1,i.world))+SecondaryCelestialLight(n,material.albedo);
                return half4(ApplyPlanetAir(color,i.world,_WorldSpaceCameraPos,VegetationFogLinear(SRGBToLinear(float3(.57,.69,.76))),VegetationFogDistance(_FogDistance)),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth
            half4 fragDepth(O i):SV_Target{ClipWearer();return 0;}
            ENDHLSL
        }
    }
}
