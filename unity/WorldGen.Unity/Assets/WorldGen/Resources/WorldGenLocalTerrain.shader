Shader "WorldGen/LocalTerrain"
{
    Properties
    {
        _Water("Water",Float)=0 _FogDistance("Fog distance",Float)=200000
        _Daylight("Daylight",Float)=1
        [HideInInspector] _TerrainCull("Diagnostic land culling",Float)=0
        _FogColor("Sky colour",Color)=(.57,.69,.76,1) _AmbientTint("Ambient light",Color)=(.12,.14,.17,1)
        _GroundAlbedo("Soil sand gravel clay rock organic salt ice",2DArray)=""{}
        _GroundNormals("Linear RGB tangent normals",2DArray)=""{}
        _GroundMasks("R AO, G roughness, B height, A metallic",2DArray)=""{}
        _TextureMetres("Repeat metres",Float)=2
        _RockLayers("Six lithology layers",Float)=0
        _GroundFlora("Ground canopy shading",Float)=0
        _GrassField("Filtered grass mat: colour and slope",2D)="gray"{}
        _MossField("Stationary moss cushions",2D)="gray"{}
        _LichenField("Stationary lichen crust",2D)="gray"{}
        _FloraSurface("Ground vegetation material weights",2D)="black"{}
        _FloraSurfaceEnabled("Local vegetation weights",Float)=0
        _MidSurface("Mid-height foliage colour/coverage",2D)="black"{}
        _MidSurfaceEnabled("Mid-height cover weights",Float)=0
        _ForestSurface("Tree canopy colour/coverage",2D)="black"{}
        _ForestSurfaceEnabled("Tree canopy weights",Float)=0
        _ForestFarEnabled("Distant forest colour and volumes",Float)=1
        _TerrainCoverage("Resident coverage tree",2D)="black"{}
        _CoverageEnabled("Partial fallback",Float)=0
        _CoverageTileSize("Tile size",Float)=1
        _CoverageFadeTimes("Coverage transition times",2D)="black"{}
        _TransitionEnabled("Smooth terrain transitions",Float)=0
        _LodFade("Incoming/outgoing times, reverse",Vector)=(0,0,0,0)
        _LodSafetyRadius("Immediate ground radius",Float)=0
        [HideInInspector] _TerrainSceneEnabled("Shared terrain scene state",Float)=0
        [HideInInspector] _LocalOrigin("Local origin",Vector)=(0,0,0,0)
        [HideInInspector] _TextureAnchor("Texture anchor",Vector)=(0,0,0,0)
        [HideInInspector] _PlanetEast("Planet east",Vector)=(0,0,0,0)
        [HideInInspector] _PlanetNorth("Planet north",Vector)=(0,0,0,0)
        [HideInInspector] _PlanetUp("Planet up",Vector)=(0,0,0,0)
        [HideInInspector] _AirGeometry("Air geometry",Vector)=(0,0,0,0)
        [HideInInspector] _AirObserverM("Air observer",Float)=0
        [HideInInspector] _AirHaze("Air haze",Float)=0
        [HideInInspector] _SecondaryLightCount("Secondary count",Float)=0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" } Cull [_TerrainCull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_local_fragment _ _RECEIVE_SHADOWS_OFF
            #pragma multi_compile_local_fragment _ WORLDGEN_TERRAIN_LAB
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WorldGenLighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "WorldGenTerrainMaterial.hlsl"
            #include "WorldGenAir.hlsl"
            #include "WorldGenLodFade.hlsl"
            #include "WorldGenGrassLight.hlsl"
            #include "WorldGenTerrainDiagnostics.hlsl"
            TEXTURE2D_ARRAY(_GroundAlbedo);SAMPLER(sampler_GroundAlbedo);
            TEXTURE2D_ARRAY(_GroundNormals);TEXTURE2D_ARRAY(_GroundMasks);
            TEXTURE2D(_GrassField);SAMPLER(sampler_GrassField);
            TEXTURE2D(_MossField);SAMPLER(sampler_MossField);TEXTURE2D(_LichenField);SAMPLER(sampler_LichenField);
            TEXTURE2D(_FloraSurface);SAMPLER(sampler_FloraSurface);
            TEXTURE2D(_MidSurface);SAMPLER(sampler_MidSurface);
            TEXTURE2D(_ForestSurface);SAMPLER(sampler_ForestSurface);
            Texture2D<float4> _TerrainCoverage,_CoverageFadeTimes;
            struct A { float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 color:COLOR;float4 weights:TEXCOORD1;float4 groundB:TEXCOORD2;float4 rocks:TEXCOORD4;float2 rocksB:TEXCOORD5; };
            struct O { float4 positionCS:SV_POSITION;float3 normal:TEXCOORD0;float3 position:TEXCOORD1;float4 color:COLOR;float4 weights:TEXCOORD2;float4 groundB:TEXCOORD3;float4 rocks:TEXCOORD4;float2 rocksB:TEXCOORD5;float2 coverageUV:TEXCOORD6; };
            O vert(A a){O o;o.position=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.position);o.normal=TransformObjectToWorldNormal(a.normalOS);o.color=a.color;o.weights=a.weights;o.groundB=a.groundB;o.rocks=a.rocks;o.rocksB=a.rocksB;o.coverageUV=a.positionOS.xz/max(.001,_CoverageTileSize);return o;}
            float GroundContribution(O i,float4 weights,float4 groundB,int layer)
            {
                float contribution=layer<4?weights[layer&3]:layer<8?groundB[layer&3]:groundB.x*(layer<12?i.rocks[(layer-8)&3]:i.rocksB[(layer-12)&1])*_RockLayers;
                if(layer==4&&_RockLayers>.5)contribution=0;return contribution;
            }
            #include "WorldGenTerrainSurface.hlsl"
            half4 frag(O i):SV_Target
            {
                float safety=1-smoothstep(_LodSafetyRadius*.65,max(.001,_LodSafetyRadius),length(i.position.xz-_WorldSpaceCameraPos.xz));
                float dither=WorldGenDither(i.positionCS.xy);
                if(_TransitionEnabled>.5&&!TerrainTest(TD_COVERAGE))
                {
                    float fade=max(WorldGenFadeWeight(_LodFade.xy),safety);
                    if(_LodFade.z>.5)clip(dither-fade);else clip(fade-dither);
                }
                if(_CoverageEnabled>.5&&!TerrainTest(TD_COVERAGE))
                {
                    float2 uv=clamp(i.coverageUV,0,.99999994);int node=0;
                    [loop] for(int depth=0;depth<24;depth++)
                    {
                        uv*=2;int2 quadrant=(int2)uv;
                        float4 children=_TerrainCoverage.Load(int3(node%256,node/256,0));
                        float next=children[quadrant.x+2*quadrant.y];
                        if(next<0)
                        {
                            if(_TransitionEnabled<.5)clip(-1);
                            int index=(int)(-next-1);float2 times=_CoverageFadeTimes.Load(int3(index%256,index/256,0)).xy;
                            clip(dither-max(WorldGenFadeWeight(times),safety));break;
                        }
                        if(next<=0)break;
                        node=(int)next-1;uv-=quadrant;
                    }
                }
                float3 fogColor=TerrainFogColor().rgb,farColor=i.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                farColor=SRGBToLinear(farColor);if(_TerrainSceneEnabled<.5)fogColor=SRGBToLinear(fogColor);
                #endif
                float distance=length(i.position-_WorldSpaceCameraPos);
                if(_Water<.5&&i.color.a< -1.5)
                {
                    clip(TerrainForestEnabled()-.5);
                    float fade=smoothstep(950,1400,distance);clip(dither-(1-fade));
                    float3 canopy=WorldGenGrassLight(farColor,normalize(i.normal),i.position,TerrainAmbient().rgb,TerrainDaylight());
                    return half4(TerrainTestAir(canopy,i.position,_WorldSpaceCameraPos,fogColor,TerrainFogDistance()),1);
                }
                if(_Water>.5)
                {
                    clip(i.color.a-.01);
                    if(i.color.a>1.5)
                    {
                        // River attributes occupy the existing colour channel:
                        // xy flow in m/s, z turbulence, alpha2 marks river water.
                        // No screen-space reflection, scene copy or extra pass.
                        float2 flow=i.color.xy;float speed=length(flow),turbulence=saturate(i.color.z);
                        float2 direction=flow/max(.05,speed),across=float2(-direction.y,direction.x);
                        float2 p=i.position.xz+TerrainOrigin().xy;
                        float along=dot(p,direction)-_Time.y*speed;
                        float crossStream=dot(p,across);
                        float phase=along*2.4+sin(crossStream*1.7)*.4;
                        float ripples=sin(phase)+.35*sin(along*5.7+crossStream*3.1);
                        float3 geometric=normalize(cross(ddy(i.position),ddx(i.position)));
                        if(geometric.y<0)geometric=-geometric;
                        float amplitude=lerp(.025,.12,turbulence);
                        float3 waterNormal=normalize(geometric+float3(direction.x*cos(phase),0,direction.y*cos(phase))*amplitude+
                            float3(across.x,0,across.y)*sin(crossStream*2.8+along)*amplitude*.5);
                        float3 eye=GetWorldSpaceNormalizeViewDir(i.position);
                        float fresnel=.035+.965*pow(1-saturate(abs(dot(eye,waterNormal))),5);
                        Light sun=GetMainLight();
                        float glint=pow(saturate(dot(waterNormal,normalize(eye+sun.direction))),180)*.85;
                        float3 deep=float3(.025,.11,.13)*lerp(.05,1,TerrainDaylight())+TerrainAmbient().rgb*.12;
                        float3 reflected=lerp(fogColor*.65,fogColor*1.12,saturate(reflect(-eye,waterNormal).y*.5+.5))*lerp(.03,1,TerrainDaylight());
                        float foam=turbulence*smoothstep(1.12,1.32,ripples)*.45;
                        float3 result=lerp(deep,reflected,fresnel)+sun.color*glint*TerrainDaylight();
                        result=lerp(result,float3(.7,.77,.75)*lerp(.04,1,TerrainDaylight()),foam);
                        return half4(TerrainTestAir(result,i.position,_WorldSpaceCameraPos,fogColor,TerrainFogDistance()),1);
                    }
                    return half4(TerrainTestAir(farColor*.92*lerp(.035,1,TerrainDaylight())+TerrainAmbient().rgb*.12,i.position,_WorldSpaceCameraPos,fogColor,TerrainFogDistance()),1);
                }
                [branch] if(TerrainTest(TD_FLAT))return half4(farColor,1);
                return ShadeTerrainSurface(i,distance,farColor,fogColor);
            }
            ENDHLSL
        }
    }
}
