Shader "WorldGen/LocalTerrain"
{
    Properties
    {
        _Water("Water",Float)=0 _FogDistance("Fog distance",Float)=200000
        _Daylight("Daylight",Float)=1
        _FogColor("Sky colour",Color)=(.57,.69,.76,1) _AmbientTint("Ambient light",Color)=(.12,.14,.17,1)
        _GroundAlbedo("Herb, soil, rock, snow albedo",2DArray)=""{}
        _GroundNormals("Linear RGB tangent normals",2DArray)=""{}
        _GroundMasks("R AO, G roughness, B height, A metallic",2DArray)=""{}
        _TextureMetres("Repeat metres",Float)=2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" } Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D_ARRAY(_GroundAlbedo);SAMPLER(sampler_GroundAlbedo);
            TEXTURE2D_ARRAY(_GroundNormals);TEXTURE2D_ARRAY(_GroundMasks);
            float _Water,_FogDistance,_TextureMetres,_Daylight;float4 _LocalOrigin,_TextureAnchor,_PlanetEast,_PlanetNorth,_PlanetUp,_FogColor,_AmbientTint;
            struct A { float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 color:COLOR;float4 weights:TEXCOORD1; };
            struct O { float4 positionCS:SV_POSITION;float3 normal:TEXCOORD0;float3 position:TEXCOORD1;float4 color:COLOR;float4 weights:TEXCOORD2; };
            O vert(A a){O o;o.position=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.position);o.normal=TransformObjectToWorldNormal(a.normalOS);o.color=a.color;o.weights=a.weights;return o;}
            float3 TriplanarAlbedo(float3 p,float3 w,int layer)
            {
                return SAMPLE_TEXTURE2D_ARRAY(_GroundAlbedo,sampler_GroundAlbedo,p.yz,layer).rgb*w.x+
                       SAMPLE_TEXTURE2D_ARRAY(_GroundAlbedo,sampler_GroundAlbedo,p.xz,layer).rgb*w.y+
                       SAMPLE_TEXTURE2D_ARRAY(_GroundAlbedo,sampler_GroundAlbedo,p.xy,layer).rgb*w.z;
            }
            half4 frag(O i):SV_Target
            {
                float3 fogColor=_FogColor.rgb,farColor=i.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                farColor=SRGBToLinear(farColor);fogColor=SRGBToLinear(fogColor);
                #endif
                float distance=length(i.position-_WorldSpaceCameraPos);
                if(_Water>.5){clip(i.color.a-.01);return half4(lerp(farColor*.92*lerp(.035,1,_Daylight),fogColor,smoothstep(_FogDistance*.25,_FogDistance,distance)),1);}
                float3 n=normalize(i.normal);
                float3 np=n.x*_PlanetEast.xyz+n.y*_PlanetUp.xyz+n.z*_PlanetNorth.xyz;
                float3 w=pow(abs(np),4);w/=max(.0001,w.x+w.y+w.z);
                // Absolute planet-oriented phase with a small modulo anchor avoids giant world-space UVs.
                float3 worldTexture=_TextureAnchor.xyz+_PlanetEast.xyz*(i.position.x+_LocalOrigin.x)+
                    _PlanetNorth.xyz*(i.position.z+_LocalOrigin.y)+_PlanetUp.xyz*i.position.y;
                float3 p=worldTexture/max(.05,_TextureMetres);
                float3 albedo=0,bump=0;float ao=0,rough=0,metal=0;
                float4 weights=max(i.weights,0);weights/=max(.001,dot(weights,1));
                [unroll]for(int layer=0;layer<4;layer++)
                {
                    float contribution=weights[layer];if(contribution<.005)continue;
                    albedo+=TriplanarAlbedo(p,w,layer)*contribution;
                    float3 nx=SAMPLE_TEXTURE2D_ARRAY(_GroundNormals,sampler_GroundAlbedo,p.yz,layer).xyz*2-1;
                    float3 ny=SAMPLE_TEXTURE2D_ARRAY(_GroundNormals,sampler_GroundAlbedo,p.xz,layer).xyz*2-1;
                    float3 nz=SAMPLE_TEXTURE2D_ARRAY(_GroundNormals,sampler_GroundAlbedo,p.xy,layer).xyz*2-1;
                    bump+=(float3(0,nx.x,nx.y)*w.x+float3(ny.x,0,ny.y)*w.y+float3(nz.x,nz.y,0)*w.z)*contribution;
                    float4 mask=SAMPLE_TEXTURE2D_ARRAY(_GroundMasks,sampler_GroundAlbedo,p.yz,layer)*w.x+
                        SAMPLE_TEXTURE2D_ARRAY(_GroundMasks,sampler_GroundAlbedo,p.xz,layer)*w.y+
                        SAMPLE_TEXTURE2D_ARRAY(_GroundMasks,sampler_GroundAlbedo,p.xy,layer)*w.z;
                    ao+=mask.r*contribution;rough+=mask.g*contribution;metal+=mask.a*contribution;
                }
                float near=1-smoothstep(120,850,distance);bump-=np*dot(np,bump);
                float3 perturbed=normalize(np+bump*near);
                n=normalize(float3(dot(perturbed,_PlanetEast.xyz),dot(perturbed,_PlanetUp.xyz),dot(perturbed,_PlanetNorth.xyz)));
                // Preserve large-scale biome colour while close materials supply actual metre-scale surface structure.
                albedo=lerp(farColor,albedo,saturate(near*.88));
                InputData input=(InputData)0;input.positionWS=i.position;input.normalWS=n;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.position);
                input.shadowCoord=TransformWorldToShadowCoord(i.position);input.bakedGI=_AmbientTint.rgb;input.shadowMask=half4(1,1,1,1);
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                SurfaceData s=(SurfaceData)0;s.albedo=albedo;s.alpha=1;s.occlusion=ao;s.smoothness=1-rough;s.metallic=metal;s.normalTS=half3(0,0,1);
                half3 color=UniversalFragmentPBR(input,s).rgb;
                return half4(lerp(color,fogColor,smoothstep(_FogDistance*.25,_FogDistance,distance)),1);
            }
            ENDHLSL
        }
    }
}
