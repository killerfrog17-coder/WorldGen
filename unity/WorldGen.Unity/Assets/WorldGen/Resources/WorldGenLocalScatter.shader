Shader "WorldGen/LocalScatter"
{
    Properties
    {
        _BaseMap("Albedo",2D)="white"{} _BaseColor("Tint",Color)=(1,1,1,1)
        _NormalMap("RGB tangent normal",2D)="bump"{} _MaskMap("R AO, G roughness, B height, A metallic",2D)="white"{}
        _Roughness("Roughness",Range(0,1))=.85 _Metallic("Metallic",Range(0,1))=0
        _NormalStrength("Normal strength",Range(0,2))=1 _Cutoff("Alpha cutoff",Range(0,1))=.4
        _RockAlbedo("Rock detail",2DArray)=""{} _RockNormals("Rock normals",2DArray)=""{}
        _RockLayer("Lithology",Float)=8 _RockLayers("Use rock textures",Float)=0
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha clip",Float)=0
        _Wind("Wind displacement (m)",Float)=0 _FogDistance("Fog distance",Float)=200000
        _HerbWind("Root-anchored herb bending",Float)=0
        _TreeSurface("Generated tree material and motion",Float)=0
        _TreeLeafShape("Generated leaf family",Float)=0
        _TreeBarkPattern("Generated bark pattern",Float)=0
        _PlantTraits("Flexibility, response speed, damping, reference height",Vector)=(1,.5,.5,1)
        _FogColor("Sky colour",Color)=(.57,.69,.76,1) _AmbientTint("Ambient light",Color)=(.12,.14,.17,1)
        [PerRendererData] _Tint("Instance tint",Color)=(1,1,1,1)
        [PerRendererData] _LodTransition("Distance-only opaque LOD interval",Vector)=(0,1,0,0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" } Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "WorldGenAir.hlsl"
            #include "WorldGenLodFade.hlsl"
            #include "WorldGenWind.hlsl"
            #include "WorldGenTree.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_NormalMap);TEXTURE2D(_MaskMap);
            TEXTURE2D_ARRAY(_RockAlbedo);SAMPLER(sampler_RockAlbedo);TEXTURE2D_ARRAY(_RockNormals);
            float _RockLayer,_RockLayers;
            float4 _BaseMap_ST,_BaseColor,_FogColor,_AmbientTint,_PlantTraits;float _Roughness,_Metallic,_NormalStrength,_Cutoff,_Wind,_FogDistance,_HerbWind;
            float _TreeSurface,_TreeLeafShape,_TreeBarkPattern;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4,_Tint)
                UNITY_DEFINE_INSTANCED_PROP(float4,_LodTransition)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;float2 bend:TEXCOORD1;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct O{float4 positionCS:SV_POSITION;float3 position:TEXCOORD0;float3 normal:TEXCOORD1;float4 tangent:TEXCOORD2;float2 uv:TEXCOORD3;float2 plant:TEXCOORD4;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            O vert(A a)
            {
                O o;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);
                float3 p=TransformObjectToWorld(a.positionOS.xyz),root=TransformObjectToWorld(float3(0,0,0));
                p=_TreeSurface>.5?WorldGenTreeWind(p,root,a.bend,_Wind,_PlantTraits):WorldGenScatterWind(p,root,_HerbWind>.5?a.bend.x:a.uv.y,_Wind,_HerbWind,_PlantTraits);
                o.position=p;o.positionCS=TransformWorldToHClip(p);o.normal=TransformObjectToWorldNormal(a.normalOS);
                o.tangent=float4(TransformObjectToWorldDir(a.tangentOS.xyz),a.tangentOS.w);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.plant=a.bend;o.color=a.color;return o;
            }
            half4 frag(O i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);WorldGenScatterFade(i.positionCS.xy,UNITY_ACCESS_INSTANCED_PROP(Props,_LodTransition));float4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;
                if(_TreeSurface>.5)
                {
                    WorldGenTreeLeafClip(i.uv,i.plant.y,_TreeLeafShape);
                    if(i.plant.y<.5)
                    {
                        float grain=sin(i.uv.x*(60+_TreeBarkPattern*13)+sin(i.uv.y*41)*(.6+_TreeBarkPattern*.35));
                        tex.rgb*=.91+.09*grain;
                    }
                }
                #ifdef _ALPHATEST_ON
                clip(tex.a-_Cutoff);
                #endif
                float3 tint=i.color.rgb*UNITY_ACCESS_INSTANCED_PROP(Props,_Tint).rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                tint=SRGBToLinear(tint);
                #endif
                float3 n=normalize(i.normal)*IS_FRONT_VFACE(face,1,-1),t=normalize(i.tangent.xyz);
                float3 bump=SAMPLE_TEXTURE2D(_NormalMap,sampler_BaseMap,i.uv).xyz*2-1;
                n=normalize(n+(t*bump.x+cross(n,t)*i.tangent.w*bump.y)*_NormalStrength);
                if(_RockLayers>.5)
                {
                    float3 w=pow(abs(n),4);w/=max(.001,w.x+w.y+w.z);float3 p=i.position*.65;
                    float3 grain=SAMPLE_TEXTURE2D_ARRAY(_RockAlbedo,sampler_RockAlbedo,p.yz,_RockLayer).rgb*w.x+SAMPLE_TEXTURE2D_ARRAY(_RockAlbedo,sampler_RockAlbedo,p.xz,_RockLayer).rgb*w.y+SAMPLE_TEXTURE2D_ARRAY(_RockAlbedo,sampler_RockAlbedo,p.xy,_RockLayer).rgb*w.z;
                    float3 a=SAMPLE_TEXTURE2D_ARRAY(_RockNormals,sampler_RockAlbedo,p.yz,_RockLayer).xyz*2-1,b=SAMPLE_TEXTURE2D_ARRAY(_RockNormals,sampler_RockAlbedo,p.xz,_RockLayer).xyz*2-1,c=SAMPLE_TEXTURE2D_ARRAY(_RockNormals,sampler_RockAlbedo,p.xy,_RockLayer).xyz*2-1;
                    n=normalize(n+float3(0,a.x,a.y)*w.x+float3(b.x,0,b.y)*w.y+float3(c.x,c.y,0)*w.z);
                    tex.rgb*=grain*2;
                }
                float4 mask=SAMPLE_TEXTURE2D(_MaskMap,sampler_BaseMap,i.uv);
                InputData input=(InputData)0;input.positionWS=i.position;input.normalWS=n;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.position);
                input.shadowCoord=TransformWorldToShadowCoord(i.position);input.bakedGI=_AmbientTint.rgb;input.shadowMask=half4(1,1,1,1);
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                SurfaceData s=(SurfaceData)0;s.albedo=tex.rgb*tint;s.alpha=1;s.occlusion=mask.r;s.metallic=_Metallic*mask.a;
                s.smoothness=1-saturate(mask.g*_Roughness);s.normalTS=half3(0,0,1);
                half3 color=UniversalFragmentPBR(input,s).rgb;
                if(_TreeSurface>.5&&i.plant.y>.5&&i.plant.y<2.5)
                {Light sun=GetMainLight(input.shadowCoord);color+=s.albedo*sun.color*(pow(saturate(dot(-n,sun.direction)),2)*.12)*sun.shadowAttenuation;}
                color+=SecondaryCelestialLight(n,s.albedo);
                float3 fog=_FogColor.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                fog=SRGBToLinear(fog);
                #endif
                return half4(ApplyPlanetAir(color,i.position,_WorldSpaceCameraPos,fog,_FogDistance),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define WORLDGEN_SHADOW_PASS
            #include "WorldGenScatterDepth.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #include "WorldGenScatterDepth.hlsl"
            ENDHLSL
        }
    }
}
