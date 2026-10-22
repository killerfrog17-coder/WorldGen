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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
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
            float3 TriplanarAlbedo(float3 p,float3 w,int layer)
            {
                return TerrainArray(TEXTURE2D_ARRAY_ARGS(_GroundAlbedo,sampler_GroundAlbedo),p,w,layer).rgb;
            }
            float GroundContribution(O i,float4 weights,float4 groundB,int layer)
            {
                float contribution=layer<4?weights[layer&3]:layer<8?groundB[layer&3]:groundB.x*(layer<12?i.rocks[(layer-8)&3]:i.rocksB[(layer-12)&1])*_RockLayers;
                if(layer==4&&_RockLayers>.5)contribution=0;return contribution;
            }
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
                farColor=SRGBToLinear(farColor);fogColor=SRGBToLinear(fogColor);
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
                float3 n=normalize(i.normal);
                // Absolute planet-oriented phase with a small modulo anchor avoids giant world-space UVs.
                float3 worldTexture=TerrainTextureAnchor().xyz+TerrainEast().xyz*(i.position.x+TerrainOrigin().x)+
                    TerrainNorth().xyz*(i.position.z+TerrainOrigin().y)+TerrainUp().xyz*i.position.y;
                float near=1-smoothstep(120,850,distance),meanBlend=smoothstep(450,850,distance);
                float detailLimit=TerrainTest(TD_NEAR)?min(850,max(1,_TerrainDiagnosticRadius)):850;
                float3 albedo=farColor;float ao=0,rough=0,metal=0;
                float4 weights=max(i.weights,0),groundB=max(i.groundB,0);float sum=max(.001,dot(weights,1)+dot(groundB,1));weights/=sum;groundB/=sum;
                int dominant=0;
                [branch] if(TerrainTest(TD_LAYER)){float largest=-1;[loop]for(int layer=0;layer<14;layer++){float c=GroundContribution(i,weights,groundB,layer);if(c>largest){largest=c;dominant=layer;}}}
                // A real branch, not texture work multiplied by zero afterwards.
                // Far ground keeps the existing vertex palette and per-layer mean
                // AO/roughness/metal, with ONE coarsest-mip fetch per active layer
                // instead of nine triplanar albedo/normal/mask texture samples.
                [branch] if(distance<detailLimit)
                {
                    float3 np=n.x*TerrainEast().xyz+n.y*TerrainUp().xyz+n.z*TerrainNorth().xyz;
                    float3 w=pow(abs(np),4);w/=max(.0001,w.x+w.y+w.z);w=TerrainTestAxes(w);
                    float3 p=worldTexture/max(.05,_TextureMetres),detailAlbedo=0,bump=0;
                    [loop]for(int layer=0;layer<14;layer++)
                    {
                        float contribution=TerrainTest(TD_LAYER)?(layer==dominant?1:0):GroundContribution(i,weights,groundB,layer);
                        if(contribution<.005)continue;
                        [branch] if(!TerrainTest(TD_TEXTURES|TD_ALBEDO))detailAlbedo+=TriplanarAlbedo(p,w,layer)*contribution;
                        float3 nx=0,ny=0,nz=0;
                        [branch] if(!TerrainTest(TD_TEXTURES|TD_NORMALS))
                        {
                            if(!TerrainTest(TD_AXIS)||w.x>.5)nx=SAMPLE_TEXTURE2D_ARRAY(_GroundNormals,sampler_GroundAlbedo,p.yz,layer).xyz*2-1;
                            if(!TerrainTest(TD_AXIS)||w.y>.5)ny=SAMPLE_TEXTURE2D_ARRAY(_GroundNormals,sampler_GroundAlbedo,p.xz,layer).xyz*2-1;
                            if(!TerrainTest(TD_AXIS)||w.z>.5)nz=SAMPLE_TEXTURE2D_ARRAY(_GroundNormals,sampler_GroundAlbedo,p.xy,layer).xyz*2-1;
                        }
                        bump+=(float3(0,nx.x,nx.y)*w.x+float3(ny.x,0,ny.y)*w.y+float3(nz.x,nz.y,0)*w.z)*contribution;
                        float4 mask=float4(.9,.85,0,0);
                        [branch] if(!TerrainTest(TD_TEXTURES|TD_MASKS))
                        {
                            mask=TerrainArray(TEXTURE2D_ARRAY_ARGS(_GroundMasks,sampler_GroundAlbedo),p,w,layer);
                            [branch] if(distance>450)mask=lerp(mask,SAMPLE_TEXTURE2D_ARRAY_LOD(_GroundMasks,sampler_GroundAlbedo,float2(.5,.5),layer,16),meanBlend);
                        }
                        ao+=mask.r*contribution;rough+=mask.g*contribution;metal+=mask.a*contribution;
                    }
                    bump-=np*dot(np,bump);float3 perturbed=normalize(np+bump*near);
                    n=normalize(float3(dot(perturbed,TerrainEast().xyz),dot(perturbed,TerrainUp().xyz),dot(perturbed,TerrainNorth().xyz)));
                    // Mineral/organic palette remains authoritative at every distance.
                    float detailScale=2;
                    #ifndef UNITY_COLORSPACE_GAMMA
                    detailScale=4.672;
                    #endif
                    [branch] if(!TerrainTest(TD_TEXTURES|TD_ALBEDO))albedo=farColor*lerp(1,detailAlbedo*detailScale,saturate(near*.88));
                }
                else
                {
                    [loop]for(int layer=0;layer<14;layer++)
                    {
                        float contribution=TerrainTest(TD_LAYER)?(layer==dominant?1:0):GroundContribution(i,weights,groundB,layer);
                        if(contribution<.005)continue;
                        float4 mask=float4(.9,.85,0,0);
                        [branch] if(!TerrainTest(TD_TEXTURES|TD_MASKS))mask=SAMPLE_TEXTURE2D_ARRAY_LOD(_GroundMasks,sampler_GroundAlbedo,float2(.5,.5),layer,16);
                        ao+=mask.r*contribution;rough+=mask.g*contribution;metal+=mask.a*contribution;
                    }
                }
                // Fine organic/blade-scale grain without a distant lawn texture.
                // Vertex cover colour already carries the same ecological mixture
                // to every terrain LOD, well beyond individual plant draw distance.
                float cover=saturate(i.color.a);
                float detailVisible=1-smoothstep(3,16,distance);
                if(!TerrainTest(TD_GRAIN)){float plantGrain=sin(worldTexture.x*29+sin(worldTexture.z*17))*sin(worldTexture.z*43+worldTexture.x*7);albedo*=1+plantGrain*cover*.13*detailVisible;}
                rough=lerp(rough,.88,cover);ao=lerp(ao,.9,cover);
                InputData input=(InputData)0;input.positionWS=i.position;input.normalWS=n;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.position);
                input.shadowCoord=TransformWorldToShadowCoord(i.position);input.bakedGI=TerrainAmbient().rgb;input.shadowMask=half4(1,1,1,1);
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                SurfaceData s=(SurfaceData)0;s.albedo=albedo;s.alpha=1;s.occlusion=ao;s.smoothness=1-rough;s.metallic=metal;s.normalTS=half3(0,0,1);
                half3 color;
                [branch] if(TerrainTest(TD_UNLIT))color=albedo;
                else if(TerrainTest(TD_SIMPLE))color=WorldGenGrassLight(albedo,n,i.position,TerrainAmbient().rgb,TerrainDaylight());
                else color=UniversalFragmentPBR(input,s).rgb;
                // Continuous, filtered canopy shading survives the end of local
                // objects. Share mean albedo/lighting rather than expose bare soil.
                float4 flora=float4(cover,0,0,cover);
                if(_FloraSurfaceEnabled>.5&&!TerrainTest(TD_TEXTURES))flora=SAMPLE_TEXTURE2D(_FloraSurface,sampler_FloraSurface,i.coverageUV*(1-_FloraSurface_TexelSize.xy)+.5*_FloraSurface_TexelSize.xy);
                float3 mix=max(0,float3(flora.a,flora.g,flora.b))/.45*_GroundFlora;
                mix/=max(1,dot(mix,1));float grassMix=mix.x,mossMix=mix.y,lichenMix=mix.z;
                if(TerrainTest(TD_GRASS))grassMix=0;if(TerrainTest(TD_MOSS))mossMix=0;if(TerrainTest(TD_LICHEN))lichenMix=0;
                float3 q=worldTexture/8;
                float3 axes=pow(abs(TerrainUp().xyz),4);axes/=max(.001,dot(axes,1));axes=TerrainTestAxes(axes);
                // Generated mats are mean-neutral (.5 RGB); preserve the same
                // canopy lighting/colour without unresolved texture work far away.
                float3 mat=.5;
                [branch] if(distance<detailLimit&&grassMix>.001&&!TerrainTest(TD_TEXTURES))
                {
                    mat=TerrainMat(TEXTURE2D_ARGS(_GrassField,sampler_GrassField),q,axes);
                    mat=lerp(mat,float3(.5,.5,.5),meanBlend);
                }
                // Match the mean linear albedo of distant procedural leaves,
                // not dark leaves fading onto a bright, flat green floor.
                float meanTint=.9111;
                #ifndef UNITY_COLORSPACE_GAMMA
                meanTint=.815;
                #endif
                float fieldDetail=1;if(!TerrainTest(TD_GRAIN))fieldDetail+=.045*sin(worldTexture.x*.17+sin(worldTexture.z*.13));
                float3 grassAlbedo=farColor*(meanTint*mat.r*2*fieldDetail);
                float3 canopyNormal=normalize(float3((mat.g-.5)*.35,.8,(mat.b-.5)*.35)+n*.25);
                float3 vegetation=0;
                [branch] if(grassMix>0)vegetation=TerrainTestLeafLight(grassAlbedo,canopyNormal,i.position,TerrainAmbient().rgb,TerrainDaylight())*grassMix;
                if(mossMix+lichenMix>.001)
                {
                    // These are stationary material/normal layers, never waving
                    // geometry, with moist moss cushions and dry lichen flakes.
                    float3 matQ=worldTexture/.8;
                    float3 mossColour=float3(.27,.35,.16),lichenColour=float3(.51,.50,.40);
                    #ifndef UNITY_COLORSPACE_GAMMA
                    mossColour=SRGBToLinear(mossColour);lichenColour=SRGBToLinear(lichenColour);
                    #endif
                    if(mossMix>.001)
                    {
                        float3 matM=.5;
                        [branch] if(distance<detailLimit&&!TerrainTest(TD_TEXTURES))
                        {
                            matM=TerrainMat(TEXTURE2D_ARGS(_MossField,sampler_MossField),matQ,axes);
                            matM=lerp(matM,float3(.5,.5,.5),meanBlend);
                        }
                        float3 normalM=normalize(n+float3(matM.g-.5,0,matM.b-.5)*1.3);
                        float3 lightM=TerrainTestMatLight(normalM,i.position,TerrainAmbient().rgb,TerrainDaylight());
                        vegetation+=lerp(farColor,mossColour,.4)*(matM.r*2)*lightM*mossMix;
                    }
                    if(lichenMix>.001)
                    {
                        float3 matL=.5;
                        [branch] if(distance<detailLimit&&!TerrainTest(TD_TEXTURES))
                        {
                            matL=TerrainMat(TEXTURE2D_ARGS(_LichenField,sampler_LichenField),matQ,axes);
                            matL=lerp(matL,float3(.5,.5,.5),meanBlend);
                        }
                        float3 normalL=normalize(n+float3(matL.g-.5,0,matL.b-.5));
                        float3 lightL=TerrainTestMatLight(normalL,i.position,TerrainAmbient().rgb,TerrainDaylight());
                        vegetation+=lerp(farColor,lichenColour,.55)*(matL.r*2)*lightL*lichenMix;
                    }
                }
                color=color*(1-grassMix-mossMix-lichenMix)+vegetation;
                if(_ForestSurfaceEnabled>.5&&TerrainForestEnabled()>.5&&!TerrainTest(TD_TEXTURES|TD_FOREST))
                {
                    float4 forest=SAMPLE_TEXTURE2D(_ForestSurface,sampler_ForestSurface,i.coverageUV*(1-_ForestSurface_TexelSize.xy)+.5*_ForestSurface_TexelSize.xy);
                    float3 leaves=forest.rgb;
                    #ifndef UNITY_COLORSPACE_GAMMA
                    leaves=SRGBToLinear(leaves);
                    #endif
                    float amount=saturate(forest.a)*smoothstep(700,1800,distance);
                    color=lerp(color,TerrainTestLeafLight(leaves,n,i.position,TerrainAmbient().rgb,TerrainDaylight()),amount*.8);
                }
                if(_MidSurfaceEnabled>.5&&!TerrainTest(TD_TEXTURES|TD_MID))
                {
                    float4 mid=SAMPLE_TEXTURE2D(_MidSurface,sampler_MidSurface,i.coverageUV*(1-_MidSurface_TexelSize.xy)+.5*_MidSurface_TexelSize.xy);
                    float3 midAlbedo=mid.rgb;
                    #ifndef UNITY_COLORSPACE_GAMMA
                    midAlbedo=SRGBToLinear(midAlbedo);
                    #endif
                    // Projected foliage cover, not painted solid shrub-coloured soil.
                    // Fade in as fine instance coverage becomes unresolved.
                    float amount=saturate(mid.a)*smoothstep(180,1000,distance);
                    float3 midLight=TerrainTestLeafLight(midAlbedo,n,i.position,TerrainAmbient().rgb,TerrainDaylight());
                    color=lerp(color,midLight,amount);
                }
                if(!TerrainTest(TD_UNLIT))color+=SecondaryCelestialLight(n,s.albedo);
                return half4(TerrainTestAir(color,i.position,_WorldSpaceCameraPos,fogColor,TerrainFogDistance()),1);
            }
            ENDHLSL
        }
    }
}
