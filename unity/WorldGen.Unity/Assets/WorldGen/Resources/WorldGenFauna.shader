Shader "WorldGen/AnimatedFauna"
{
    Properties
    {
        _BloodMask("Runtime impact stains",2D)="black"{} _BloodEnabled("Impact stains",Float)=0 _BloodColor("Blood pigment",Color)=(.23,.012,.009,1)
        _Coats("Baked native coat pigments",2DArray)=""{}
        _Mask("R roughness, G opacity, B emission",2D)="white"{}
        _Normals("RGB tangent normals",2D)="bump"{}
        _VegetationSceneEnabled("Shared scene",Float)=1
        _Coat("Coat",Float)=0 _PaletteOffset("Bone palette",Int)=0
        _Parts("Four 16-bit visibility masks",Vector)=(65535,65535,65535,65535)
        _Fade("Visible fraction",Float)=1 _Glow("Firefly pulse",Float)=0
        _Morph("Native wing fold",Float)=0
    }
    SubShader
    {
        Tags {"RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest"}
        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "WorldGenBlood.hlsl"
        #include "WorldGenLighting.hlsl"
        #include "WorldGenVegetationScene.hlsl"
        #include "WorldGenCanopyShadows.hlsl"
        StructuredBuffer<float4x4> _FaunaBones;
        TEXTURE2D_ARRAY(_Coats);SAMPLER(sampler_Coats);
        TEXTURE2D(_Mask);SAMPLER(sampler_Mask);
        TEXTURE2D(_Normals);SAMPLER(sampler_Normals);
        CBUFFER_START(UnityPerMaterial)
        float4 _BloodColor;float _BloodEnabled;
        float4 _Parts;float _Coat,_Fade,_Glow,_Morph;int _PaletteOffset;
        CBUFFER_END
        struct Attributes
        {
            float3 position:POSITION;float3 normal:NORMAL;float4 tangent:TANGENT;
            float2 uv:TEXCOORD0;float4 joints:TEXCOORD2;float4 weights:TEXCOORD3;float2 part:TEXCOORD4;
            float4 extraJoints:TEXCOORD5;float4 extraWeights:TEXCOORD6;
            float3 morph:TEXCOORD7;
        };
        float4x4 Skin(Attributes v)
        {
            uint4 id=(uint4)v.joints+(uint)_PaletteOffset;
            float4x4 result=_FaunaBones[id.x]*v.weights.x+_FaunaBones[id.y]*v.weights.y+_FaunaBones[id.z]*v.weights.z+_FaunaBones[id.w]*v.weights.w;
            [branch]if(dot(v.extraWeights,float4(1,1,1,1))>0)
            {
                uint4 more=(uint4)v.extraJoints+(uint)_PaletteOffset;
                result+=_FaunaBones[more.x]*v.extraWeights.x+_FaunaBones[more.y]*v.extraWeights.y+_FaunaBones[more.z]*v.extraWeights.z+_FaunaBones[more.w]*v.extraWeights.w;
            }
            return result;
        }
        float PartVisible(float value)
        {uint part=(uint)value;uint mask=(uint)_Parts[part/16u];return (mask>>(part%16u))&1u;}
        float Dither(float2 p)
        {return frac(52.9829189*frac(dot(floor(p),float2(.06711056,.00583715))));}
        void ClipFauna(float2 uv,float2 screen,float visible)
        {
            clip(visible-.5);clip(_Fade-Dither(screen));
            clip(SAMPLE_TEXTURE2D(_Mask,sampler_Mask,uv).g-.35);
        }
        ENDHLSL
        Pass
        {
            Name "Forward" Tags {"LightMode"="UniversalForward"} Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            struct Varyings
            {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float4 tangent:TEXCOORD2;float2 uv:TEXCOORD3;nointerpolation float visible:TEXCOORD4;};
            Varyings vert(Attributes v)
            {
                Varyings o;float4x4 skin=Skin(v);float3 p=mul(skin,float4(v.position+v.morph*_Morph,1)).xyz;
                o.world=TransformObjectToWorld(p);o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(mul((float3x3)skin,v.normal));
                o.tangent=float4(TransformObjectToWorldDir(mul((float3x3)skin,v.tangent.xyz)),v.tangent.w);
                o.uv=v.uv;o.visible=PartVisible(v.part.x);return o;
            }
            half4 frag(Varyings i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                ClipFauna(i.uv,i.positionCS.xy,i.visible);
                half3 albedo=WorldGenBlood(SAMPLE_TEXTURE2D_ARRAY(_Coats,sampler_Coats,i.uv,_Coat).rgb,i.uv,_BloodEnabled,_BloodColor.rgb);
                half3 mask=SAMPLE_TEXTURE2D(_Mask,sampler_Mask,i.uv).rgb;
                half3 n=normalize(i.normal)*IS_FRONT_VFACE(face,1,-1),t=normalize(i.tangent.xyz);
                half3 bump=SAMPLE_TEXTURE2D(_Normals,sampler_Normals,i.uv).rgb*2-1;
                n=normalize(n+(t*bump.x+cross(n,t)*i.tangent.w*bump.y)*.7);
                InputData input=(InputData)0;input.positionWS=i.world;input.normalWS=n;
                input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);input.shadowCoord=TransformWorldToShadowCoord(i.world);
                input.bakedGI=VegetationAmbient(float4(.12,.14,.17,1)).rgb;input.shadowMask=1;
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                SurfaceData surface=(SurfaceData)0;surface.albedo=albedo;surface.alpha=1;surface.occlusion=1;
                surface.smoothness=1-clamp(mask.r,.08,.97);surface.normalTS=half3(0,0,1);
                half3 color=WorldGenCanopyPbr(UniversalFragmentPBR(input,surface).rgb,albedo,input.bakedGI,1,WorldGenCanopyShadow(1,i.world))+SecondaryCelestialLight(n,albedo);
                color+=albedo*mask.b*_Glow;
                return half4(ApplyPlanetAir(color,i.world,_WorldSpaceCameraPos,VegetationFogLinear(SRGBToLinear(float3(.57,.69,.76))),VegetationFogDistance(60000)),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ColorMask 0 Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection,_LightPosition;
            struct O{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;nointerpolation float visible:TEXCOORD1;};
            O vert(Attributes v)
            {
                O o;float4x4 skin=Skin(v);float3 world=TransformObjectToWorld(mul(skin,float4(v.position+v.morph*_Morph,1)).xyz);
                float3 normal=TransformObjectToWorldNormal(mul((float3x3)skin,v.normal));
                float3 light=_LightDirection;
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                light=normalize(_LightPosition-world);
                #endif
                o.positionCS=TransformWorldToHClip(ApplyShadowBias(world,normal,light));
                #if UNITY_REVERSED_Z
                o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #else
                o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                o.uv=v.uv;o.visible=PartVisible(v.part.x);return o;
            }
            half4 frag(O i):SV_Target{ClipFauna(i.uv,i.positionCS.xy,i.visible);return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct O{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;nointerpolation float visible:TEXCOORD1;};
            O vert(Attributes v){O o;o.positionCS=TransformObjectToHClip(mul(Skin(v),float4(v.position+v.morph*_Morph,1)).xyz);o.uv=v.uv;o.visible=PartVisible(v.part.x);return o;}
            half4 frag(O i):SV_Target{ClipFauna(i.uv,i.positionCS.xy,i.visible);return 0;}
            ENDHLSL
        }
    }
}
