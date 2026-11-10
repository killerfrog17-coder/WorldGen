Shader "WorldGen/ForestPackets"
{
    Properties { [HideInInspector] _VegetationSceneEnabled("Shared camera scene",Float)=0

        _TreeColourViews("Actual-tree stand colours",2DArray)=""{} _TreeNormalViews("Stand normals and depth",2DArray)=""{}
        [HideInInspector] _ForestMacro("Regional canopy patches",Float)=0
        _ImpostorBounds("Object centre/radius",Vector)=(0,.48,0,1.5)
        [HideInInspector] _ImpostorExtents("Model extents and edge padding",Vector)=(0,0,0,0)
        [HideInInspector] _TreeTightQuad("Trim empty billboard space",Float)=1
        [HideInInspector] _TreeEarlyAlpha("Conservative empty-ray rejection",Float)=1
        [HideInInspector] _TreeViewCutoff("Distant coverage threshold",Float)=.4
        _AmbientTint("Ambient",Color)=(.12,.14,.17,1) _FogColor("Fog",Color)=(.57,.69,.76,1) _FogDistance("Fog distance",Float)=60000
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+2"}
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "WorldGenVegetationScene.hlsl"
            #include "WorldGenLodFade.hlsl"
            #include "WorldGenFoliageShadows.hlsl"
            #include "WorldGenTreeViews.hlsl"
            struct Root {float3 position;float width;float height;float yaw;float slopeX;float slopeZ;float3 tint;uint field;int cellX;int cellY;uint face;float padding;};
            StructuredBuffer<Root> _Roots;StructuredBuffer<uint> _Visible;
            StructuredBuffer<int4> _Tiles;StructuredBuffer<float> _ForestBirth;float _ForestClock;uint _ForestHashMask;
            uint CanopyMix(uint x){x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;return x^(x>>16);}
            float ForestFade(Root root)
            {
                if(root.padding<1.5){float birth=_ForestBirth[root.field&65535u];return birth>0?saturate((_ForestClock-birth)/.7):1;}
                int x=root.cellX/32,z=root.cellY/32;uint face=root.face&7u;
                uint slot=CanopyMix((uint)x*73856093u^(uint)z*19349663u^face*15485863u)&_ForestHashMask;
                [loop]for(uint attempt=0;attempt<=_ForestHashMask;attempt++)
                {int4 item=_Tiles[slot];if(item.w==0)break;if(item.x==x&&item.y==z&&item.z==0&&(item.w&7)==(int)face+1){float birth=_ForestBirth[(item.w>>3)-1];return birth>0?1-saturate((_ForestClock-birth)/.7):0;}slot=(slot+1)&_ForestHashMask;}
                return 1;
            }
            float4 _ImpostorBounds,_ImpostorExtents,_AmbientTint,_FogColor,_ForestChart,_ForestRange;float _FogDistance,_TreeEarlyAlpha,_TreeViewCutoff,_TreeTightQuad,_ForestMacro;
            struct O {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 local:TEXCOORD1;nointerpolation float3 direction:TEXCOORD2;nointerpolation uint root:TEXCOORD3;nointerpolation float2 viewLod:TEXCOORD4;
                #if defined(WORLDGEN_TREE_EFFICIENT)
                nointerpolation uint3 viewIndices:TEXCOORD5;nointerpolation float3 viewWeights:TEXCOORD6;float4 viewA:TEXCOORD7;float4 viewB:TEXCOORD8;float4 viewC:TEXCOORD9;
                #endif
            };
            float3 Rotate(float3 p,float angle){float s,c;sincos(angle,s,c);return float3(c*p.x+s*p.z,p.y,-s*p.x+c*p.z);}
            O vert(uint vertexID:SV_VertexID,uint instanceID:SV_InstanceID)
            {
                O o=(O)0;uint index=_Visible[instanceID];Root root=_Roots[index];float3 origin=root.position-_ForestChart.xyz;
                if(_ForestMacro>.5)
                {
                    // Four low-cost crown masses per coverage sample. These
                    // are terrain-following canopy, never stretched tree art.
                    uint cap=vertexID/24u,sector=(vertexID%24u)/3u,corner=vertexID%3u;
                    float angle=(sector+(corner==2u?1:0))*.78539816339+root.yaw;
                    float width=root.width*sqrt(saturate(_ForestChart.w));
                    float2 centre=float2((cap&1u)?1:-1,(cap&2u)?1:-1)*width*.24;
                    float2 radial=corner==0u?0:float2(cos(angle),sin(angle))*width*.39;
                    float peak=.82+.14*sin(root.yaw+cap*2.71);
                    float3 relative=float3(centre.x+radial.x,root.height*(corner==0u?peak:.46),centre.y+radial.y);
                    relative.y+=relative.x*root.slopeX+relative.z*root.slopeZ;
                    o.world=origin+relative;o.positionCS=TransformWorldToHClip(o.world);o.root=index;
                    o.direction=normalize(float3(cos(angle)*root.height*.4,max(.2,root.width*.39),sin(angle)*root.height*.4));
                    return o;
                }
                float3 scale=float3(root.width,root.height,root.width);
                float3 eye=_WorldSpaceCameraPos-origin;eye.y-=eye.x*root.slopeX+eye.z*root.slopeZ;eye=Rotate(eye,-root.yaw)/scale;
                float3 direction=normalize(eye-_ImpostorBounds.xyz),r,u;WorldGenTreeViewBasis(direction,r,u);
                const float2 corners[6]={float2(-1,-1),float2(-1,1),float2(1,-1),float2(1,-1),float2(-1,1),float2(1,1)};
                float2 uv=corners[vertexID%6u],halfSize=WorldGenTreeQuadHalfSize(r,u,_ImpostorExtents,_ImpostorBounds.w,_TreeTightQuad);
                float3 p=_ImpostorBounds.xyz+r*uv.x*halfSize.x+u*uv.y*halfSize.y;
                float3 relative=Rotate(p*scale,root.yaw);relative.y+=relative.x*root.slopeX+relative.z*root.slopeZ;
                o.world=origin+relative;o.positionCS=TransformWorldToHClip(o.world);o.local=p-_ImpostorBounds.xyz;o.direction=direction;o.root=index;o.viewLod=WorldGenTreeViewLod(length(origin-_WorldSpaceCameraPos),_ImpostorBounds.w*max(root.width,root.height));
                #if defined(WORLDGEN_TREE_EFFICIENT)
                WorldGenPreparedTreeViews prepared=WorldGenPrepareTreeViews(o.local,o.direction,_ImpostorBounds.w,o.viewLod);
                o.viewIndices=prepared.indices;o.viewWeights=prepared.weights;o.viewA=prepared.a;o.viewB=prepared.b;o.viewC=prepared.c;
                #endif
                return o;
            }
            void ForestSurface(O i,bool early,out half4 color,out half3 normal)
            {
                #if defined(WORLDGEN_TREE_EFFICIENT)
                WorldGenPreparedTreeViews prepared;prepared.indices=i.viewIndices;prepared.weights=i.viewWeights;prepared.a=i.viewA;prepared.b=i.viewB;prepared.c=i.viewC;
                WorldGenPreparedTreeSurface(prepared,i.local,i.direction,_ImpostorBounds.w,i.viewLod,early,color,normal);
                #else
                WorldGenTreeSurface(i.local,i.direction,_ImpostorBounds.w,i.viewLod,early,color,normal);
                #endif
            }
            half4 frag(O i):SV_Target
            {
                Root root=_Roots[i.root];float distance=length(root.position-_ForestChart.xyz-_WorldSpaceCameraPos);
                // Complement the exact-tree interval [0,1-weight], rather than
                // drawing the same screen-door pixels twice and leaving holes.
                float weight=smoothstep(_ForestRange.x,_ForestRange.y,distance);clip(WorldGenDither(i.positionCS.xy)-(1-weight));
                float fade=ForestFade(root),dither=WorldGenDither(i.positionCS.xy+float2(17,31));clip(root.padding>1.5?dither-(1-fade):fade-dither);
                half4 color=1;half3 objectNormal=i.direction;
                if(_ForestMacro<.5){ForestSurface(i,_TreeEarlyAlpha>.5,color,objectNormal);clip(color.a-_TreeViewCutoff);}
                half3 albedo=color.rgb*SRGBToLinear(root.tint);
                float3 normal=_ForestMacro>.5?objectNormal:Rotate(objectNormal/float3(root.width,root.height,root.width),root.yaw);normal.x-=root.slopeX*normal.y;normal.z-=root.slopeZ*normal.y;normal=normalize(normal);
                half canopy;Light sun=WorldGenFoliageLight(i.world,canopy);half diffuse=saturate((dot(normal,sun.direction)+.2)/1.2),transmission=.14*pow(saturate(dot(-normal,sun.direction)),2);
                half3 colour=albedo*(VegetationAmbient(_AmbientTint).rgb*(1.4*WorldGenCanopyAmbient(canopy))+sun.color*(diffuse+transmission)*sun.shadowAttenuation)+SecondaryCelestialLight(normal,albedo);
                return half4(ApplyPlanetAir(colour,i.world,_WorldSpaceCameraPos,VegetationFogLinear(SRGBToLinear(_FogColor.rgb)),VegetationFogDistance(_FogDistance)),1);
            }
            half4 depthFrag(O i):SV_Target
            {
                Root root=_Roots[i.root];float distance=length(root.position-_ForestChart.xyz-_WorldSpaceCameraPos);
                float weight=smoothstep(_ForestRange.x,_ForestRange.y,distance);clip(WorldGenDither(i.positionCS.xy)-(1-weight));
                float fade=ForestFade(root),dither=WorldGenDither(i.positionCS.xy+float2(17,31));clip(root.padding>1.5?dither-(1-fade):fade-dither);
                if(_ForestMacro<.5){half4 color;half3 normal;ForestSurface(i,false,color,normal);clip(color.a-_TreeViewCutoff);}return 0;
            }
        ENDHLSL
        Pass
        {
            Tags {"LightMode"="UniversalForward"} Cull Off ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile _ WORLDGEN_FLAT_TREE_DEPTH
            #pragma multi_compile_local _ WORLDGEN_TREE_EFFICIENT
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            ENDHLSL
        }
        Pass
        {
            Name "FoliageDepth" Tags {"LightMode"="WorldGenFoliageDepth"} Cull Off ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile _ WORLDGEN_FLAT_TREE_DEPTH
            #pragma multi_compile_local _ WORLDGEN_TREE_EFFICIENT
            #pragma vertex vert
            #pragma fragment depthFrag
            ENDHLSL
        }
    }
}
