Shader "WorldGen/ForestPackets"
{
    Properties { [HideInInspector] _VegetationSceneEnabled("Shared camera scene",Float)=0

        _TreeColourViews("Actual-tree stand colours",2DArray)=""{} _TreeNormalViews("Stand normals and depth",2DArray)=""{}
        [HideInInspector] _ForestMacro("Regional canopy patches",Float)=0
        [HideInInspector] _ForestPacketFeather("World-space patch overlap",Float)=.35
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
            struct Ground {float4 a,b,c,d,e;};
            StructuredBuffer<Root> _Roots;StructuredBuffer<uint> _Visible;
            StructuredBuffer<Ground> _Ground;
            StructuredBuffer<int4> _Tiles;StructuredBuffer<int> _Fields;StructuredBuffer<float> _ForestBirth;float _ForestClock;uint _ForestHashMask;
            uint CanopyMix(uint x){x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;return x^(x>>16);}
            float ForestFade(Root root)
            {
                if(root.padding<1.5){float birth=_ForestBirth[root.field&65535u];return birth>0?saturate((_ForestClock-birth)/.7):1;}
                if((root.face&32u)!=0)return 1;
                int x=root.cellX/128,z=root.cellY/128;uint face=root.face&7u;
                uint slot=CanopyMix((uint)x*73856093u^(uint)z*19349663u^face*15485863u)&_ForestHashMask;
                [loop]for(uint attempt=0;attempt<=_ForestHashMask;attempt++)
                {int4 item=_Tiles[slot];if(item.w==0)break;if(item.x==x&&item.y==z&&item.z==0&&(item.w&7)==(int)face+1){float birth=_ForestBirth[(item.w>>3)-1];return birth>0?1-saturate((_ForestClock-birth)/.7):0;}slot=(slot+1)&_ForestHashMask;}
                return 1;
            }
            float4 _ImpostorBounds,_ImpostorExtents,_AmbientTint,_FogColor,_ForestChart,_ForestRange,_WorldGenForestDistanceRange;float _FogDistance,_TreeEarlyAlpha,_TreeViewCutoff,_TreeTightQuad,_ForestMacro;
            float4 _ForestPlanet0,_ForestPlanet1,_ForestPlanet2;uint _ForestLatticeCount,_ForestSeed;float _ForestPacketFeather,_ForestCanonical;
            float ForestDistanceWeight(float distance,Root root)
            {
                float nearWeight=(_Fields[root.field&65535u]&2)!=0?1:smoothstep(_ForestRange.x,_ForestRange.y,distance);
                float end=_WorldGenForestDistanceRange.y;
                return nearWeight*(end>0?1-smoothstep(_WorldGenForestDistanceRange.x,max(end,_WorldGenForestDistanceRange.x+.001),distance):1);
            }
            struct O {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 local:TEXCOORD1;nointerpolation float3 direction:TEXCOORD2;nointerpolation uint root:TEXCOORD3;nointerpolation float2 viewLod:TEXCOORD4;nointerpolation float2 renderScale:TEXCOORD10;nointerpolation float childFade:TEXCOORD11;nointerpolation float yaw:TEXCOORD12;nointerpolation float treeTint:TEXCOORD13;
                #if defined(WORLDGEN_TREE_EFFICIENT)
                nointerpolation uint3 viewIndices:TEXCOORD5;nointerpolation float3 viewWeights:TEXCOORD6;float4 viewA:TEXCOORD7;float4 viewB:TEXCOORD8;float4 viewC:TEXCOORD9;
                #endif
            };
            float3 Rotate(float3 p,float angle){float s,c;sincos(angle,s,c);return float3(c*p.x+s*p.z,p.y,-s*p.x+c*p.z);}
            float ForestUnit(uint seed){return (CanopyMix(seed)&0xffffffu)/16777216.;}
            float ForestGroundHeight(Ground ground,uint index)
            {
                // FXC evaluates vector indexing in both ternary arms while
                // unrolling placement retries. Use scalar lanes throughout.
                return index==0u?ground.a.x:index==1u?ground.a.y:index==2u?ground.a.z:index==3u?ground.a.w:
                    index==4u?ground.b.x:index==5u?ground.b.y:index==6u?ground.b.z:index==7u?ground.b.w:ground.c.x;
            }
            bool ForestChildGround(Ground ground,float2 offset,float radius,out float height,bool canonical)
            {
                float2 grid=clamp(offset/max(.001,radius)+1,0,2),cell=min(floor(grid),1),f=grid-cell;
                uint at=(uint)cell.y*3u+(uint)cell.x;
                uint4 indices=uint4(at,at+1u,at+3u,at+4u);
                float4 weights=float4((1-f.x)*(1-f.y),f.x*(1-f.y),(1-f.x)*f.y,f.x*f.y);
                uint forest=(uint)ground.c.y,solid=(uint)ground.c.z;
                float4 admitted=float4((forest>>indices.x)&1u,(forest>>indices.y)&1u,(forest>>indices.z)&1u,(forest>>indices.w)&1u);
                float4 supported=float4((solid>>indices.x)&1u,(solid>>indices.y)&1u,(solid>>indices.z)&1u,(solid>>indices.w)&1u);
                height=dot(weights,float4(ForestGroundHeight(ground,indices.x),ForestGroundHeight(ground,indices.y),ForestGroundHeight(ground,indices.z),ForestGroundHeight(ground,indices.w)));
                return dot(weights,supported)>.999&&(canonical||dot(weights,admitted)>=.5);
            }
            int ForestPresent(uint face,uint2 cell,uint level)
            {
                uint size=128u*(1u<<level);int x=(int)(cell.x/size),z=(int)(cell.y/size);
                uint slot=CanopyMix((uint)x*73856093u^(uint)z*19349663u^level*83492791u^face*15485863u)&_ForestHashMask;
                [loop]for(uint attempt=0;attempt<=_ForestHashMask;attempt++)
                {int4 item=_Tiles[slot];if(item.w==0)break;if(item.x==x&&item.y==z&&item.z==(int)level&&(item.w&7)==(int)face+1)return (item.w>>3)-1;slot=(slot+1u)&_ForestHashMask;}
                return -1;
            }
            void ForestFaceBasis(uint face,out float3 normal,out float3 east,out float3 north)
            {
                if(face<4u)
                {
                    normal=face<2u?float3(face==0u?1:-1,0,0):float3(0,face==2u?1:-1,0);
                    east=cross(float3(0,0,1),normal);north=float3(0,0,1);
                }
                else{normal=float3(0,0,face==4u?1:-1);east=float3(1,0,0);north=float3(0,face==4u?1:-1,0);}
            }
            float2 ForestPacketPosition(Root root,float2 q)
            {
                uint side=(root.face&32u)!=0?8u:min(4u<<(root.field>>16),_ForestLatticeCount);
                float2 left=float2(root.cellX,root.cellY),span=min((float)side,(float)_ForestLatticeCount-left);
                float2 cube=(left+q*span)*(2./_ForestLatticeCount)-1;
                float3 normal,east,north;ForestFaceBasis(root.face&7u,normal,east,north);
                float3 p=normal+east*cube.x+north*cube.y;
                return float2(dot(p,_ForestPlanet1.xyz),dot(p,_ForestPlanet2.xyz))*(_ForestPlanet0.w/dot(p,_ForestPlanet0.xyz));
            }
            float ForestChildFade(Root root,float2 position)
            {
                if(_ForestPlanet0.w<=0||_ForestLatticeCount==0u)return 1;
                float3 p=normalize(_ForestPlanet0.xyz+(_ForestPlanet1.xyz*position.x+_ForestPlanet2.xyz*position.y)/_ForestPlanet0.w),a=abs(p),n,u,v;uint face;
                if(a.x>=a.y&&a.x>=a.z)face=p.x>=0?0u:1u;
                else if(a.y>=a.z)face=p.y>=0?2u:3u;
                else face=p.z>=0?4u:5u;
                ForestFaceBasis(face,n,u,v);
                float denominator=dot(p,n);uint2 cell=(uint2)clamp(floor((float2(dot(p,u),dot(p,v))/denominator+1)*.5*_ForestLatticeCount),0,(float)_ForestLatticeCount-1);
                float fade=1;uint level=root.field>>16;
                [loop]for(uint fine=0;fine<level;fine++)
                {
                    int id=ForestPresent(face,cell,fine);if(id<0)continue;
                    float birth=_ForestBirth[id],age=_ForestClock-birth;
                    if(fine==0u&&birth>0&&age<.7){fade=min(fade,1-saturate(age/.7));continue;}
                    return 0;
                }
                return fade;
            }
            O vert(uint vertexID:SV_VertexID,uint instanceID:SV_InstanceID)
            {
                O o=(O)0;uint packed=_Visible[instanceID],index=_ForestCanonical>.5?packed/64u:packed;Root root=_Roots[index];float3 origin=root.position-_ForestChart.xyz;
                float width=root.width,height=root.height,yaw=root.yaw;o.childFade=1;o.treeTint=1;
                if(_ForestMacro>1.5&&(root.face&32u)!=0)
                {
                    uint child=packed%64u;uint2 cell=uint2(root.cellX,root.cellY)+uint2(child%8u,child/8u);
                    Ground ground=_Ground[index];uint lane=child/16u;uint allowed=(uint)(lane==0u?ground.e.x:lane==1u?ground.e.y:lane==2u?ground.e.z:ground.e.w);
                    if(child>=64u||any(cell>=_ForestLatticeCount)||((allowed>>(child&15u))&1u)==0u){o.positionCS=float4(0,0,0,1);return o;}
                    uint h=CanopyMix(cell.x*73856093u^cell.y*19349663u^(root.face&7u)*83492791u^_ForestSeed);
                    float2 jitter=.08+.84*float2(ForestUnit(h^0x68bc21ebu),ForestUnit(h^0x02e5be93u));
                    float2 q=(float2(child%8u,child/8u)+jitter)/8.;
                    float probability=lerp(lerp(ground.d.x,ground.d.y,q.x),lerp(ground.d.z,ground.d.w,q.x),q.y);
                    if(ForestUnit(h^0x27d4eb2du)>=probability*_ForestChart.w){o.positionCS=float4(0,0,0,1);return o;}
                    float cohort=ForestUnit(h^0x967a889bu),r=ForestUnit(h^0x368cc8b7u);
                    float size=(root.face&64u)!=0?(cohort<.69?.10+.35*r:cohort>.99?1.28+.28*r:.82+.25*r):
                        cohort<.12?.18+.16*r:cohort<.30?.40+.25*r:.72+.36*r;
                    width=root.width/6.6*size*(.88+.24*ForestUnit(h^0x9e3779b9u));height=root.height*size;yaw=6.28318530718*ForestUnit(h^0x85ebca6bu);
                    float2 offset=ForestPacketPosition(root,q)-root.position.xz;float groundY;
                    if(!ForestChildGround(ground,offset,root.padding*.75,groundY,true)){o.positionCS=float4(0,0,0,1);return o;}
                    origin.xz+=offset;origin.y=groundY-.045-_ForestChart.y;o.treeTint=.93+.12*ForestUnit(h^0xa24baed5u);
                }
                else if(_ForestMacro>1.5)
                {
                    uint child=vertexID/6u,seed=CanopyMix((root.face>>8)^((child+1u)*0x9e3779b9u));
                    float singleWidth=root.width/6.6,cohort=.55+.45*ForestUnit(seed^0x967a889bu);
                    width=singleWidth*cohort*(.93+.14*ForestUnit(seed^0x368cc8b7u))/.78645317;
                    height=root.height*cohort/.78581168;yaw+=6.28318530718*ForestUnit(seed^0x9e3779b9u);
                    Ground ground=_Ground[index];float2 offset=0;float groundY=0;bool supported=false;
                    bool packet=root.padding>3.5&&_ForestPlanet0.w>0&&_ForestLatticeCount>0u;
                    float2 phase=float2(ForestUnit((root.face>>8)^0x68bc21ebu),ForestUnit((root.face>>8)^0x02e5be93u));
                    [unroll]for(uint attempt=0;attempt<4u;attempt++)
                    {
                        uint jitter=attempt==0u?seed:CanopyMix(seed^((attempt+1u)*0x85ebca6bu));
                        float2 random=float2(ForestUnit(jitter^0x68bc21ebu),ForestUnit(jitter^0x02e5be93u));
                        if(packet)
                        {
                            float2 q=attempt==0u?frac(phase+child*float2(.754877666,.569840291)):random;
                            // Convolve the rectangular sampling footprint with
                            // stable world-space jitter. Adjacent patches blend
                            // their density/colour without adding more trees.
                            q+=(float2(ForestUnit(jitter^0xa24baed5u),ForestUnit(jitter^0x9fb21c65u))-.5)*(2*_ForestPacketFeather);
                            offset=ForestPacketPosition(root,q)-root.position.xz;
                        }
                        else
                        {
                            float2 layout=attempt==0u?(float2(child%6u,child/6u)-2.5+(random-.5)*.9):(random-.5)*5.9;
                            offset=Rotate(float3(layout.x*singleWidth,0,layout.y*singleWidth),root.yaw).xz;
                        }
                        float radius=(root.padding>3.5?root.padding:root.width)*.75;
                        if(ForestChildGround(ground,offset,radius,groundY,false)){supported=true;break;}
                    }
                    if(!supported){o.positionCS=float4(0,0,0,1);return o;}
                    origin.xz+=offset;origin.y=groundY-.045-_ForestChart.y;
                    o.childFade=ForestChildFade(root,root.position.xz+offset);
                    if(o.childFade<=0){o.positionCS=float4(0,0,0,1);return o;}
                }
                o.yaw=yaw;
                float3 scale=float3(width,height,width);o.renderScale=float2(width,height);
                float3 eyeRelative=_WorldSpaceCameraPos-origin;
                float3 eye=Rotate(eyeRelative,-yaw)/scale;
                float3 direction=normalize(eye-_ImpostorBounds.xyz),r,u;WorldGenTreeViewBasis(direction,r,u);
                const float2 corners[6]={float2(-1,-1),float2(-1,1),float2(1,-1),float2(1,-1),float2(-1,1),float2(1,1)};
                float2 uv=corners[vertexID%6u],halfSize=WorldGenTreeQuadHalfSize(r,u,_ImpostorExtents,_ImpostorBounds.w,_TreeTightQuad);
                float3 p=_ImpostorBounds.xyz+r*uv.x*halfSize.x+u*uv.y*halfSize.y;
                float3 relative=Rotate(p*scale,yaw);
                o.world=origin+relative;o.positionCS=TransformWorldToHClip(o.world);o.local=p-_ImpostorBounds.xyz;o.direction=direction;o.root=index;o.viewLod=WorldGenTreeViewLod(length(origin-_WorldSpaceCameraPos),_ImpostorBounds.w*max(width,height));
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
                float weight=ForestDistanceWeight(distance,root);clip(WorldGenDither(i.positionCS.xy)-(1-weight));
                float fade=_ForestMacro>1.5?i.childFade:ForestFade(root),dither=WorldGenDither(i.positionCS.xy+float2(17,31));clip(root.padding>1.5?dither-(1-fade):fade-dither);
                half4 color;half3 objectNormal;ForestSurface(i,_TreeEarlyAlpha>.5,color,objectNormal);clip(color.a-_TreeViewCutoff);
                half3 albedo=color.rgb*SRGBToLinear(root.tint)*i.treeTint;
                float3 normal=Rotate(objectNormal/float3(i.renderScale.x,i.renderScale.y,i.renderScale.x),i.yaw);
                normal=normalize(normal);
                half canopy;Light sun=WorldGenFoliageLight(i.world,canopy);half diffuse=saturate((dot(normal,sun.direction)+.2)/1.2),transmission=.14*pow(saturate(dot(-normal,sun.direction)),2);
                half3 colour=albedo*(VegetationAmbient(_AmbientTint).rgb*(1.4*WorldGenCanopyAmbient(canopy))+sun.color*(diffuse+transmission)*sun.shadowAttenuation)+SecondaryCelestialLight(normal,albedo);
                return half4(ApplyPlanetAir(colour,i.world,_WorldSpaceCameraPos,VegetationFogLinear(SRGBToLinear(_FogColor.rgb)),VegetationFogDistance(_FogDistance)),1);
            }
            half4 depthFrag(O i):SV_Target
            {
                Root root=_Roots[i.root];float distance=length(root.position-_ForestChart.xyz-_WorldSpaceCameraPos);
                float weight=ForestDistanceWeight(distance,root);clip(WorldGenDither(i.positionCS.xy)-(1-weight));
                float fade=_ForestMacro>1.5?i.childFade:ForestFade(root),dither=WorldGenDither(i.positionCS.xy+float2(17,31));clip(root.padding>1.5?dither-(1-fade):fade-dither);
                half4 color;half3 normal;ForestSurface(i,false,color,normal);clip(color.a-_TreeViewCutoff);return 0;
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
