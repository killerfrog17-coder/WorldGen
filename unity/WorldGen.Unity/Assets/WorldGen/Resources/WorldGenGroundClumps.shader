Shader "WorldGen/GroundClumps"
{
    Properties
    {
        [HideInInspector] _VegetationSceneEnabled("Shared scene",Float)=1
        _ClumpAtlas("Ground foliage",2D)="white"{}
        _GrassWind("Wind",Float)=1
        _FogDistance("Fog distance",Float)=200000 _Daylight("Daylight",Float)=1
        _FogColor("Fog",Color)=(.57,.69,.76,1) _AmbientTint("Ambient",Color)=(.12,.14,.17,1)
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite On
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "WorldGenVegetationScene.hlsl"
            #include "WorldGenLodFade.hlsl"
            #include "WorldGenCanopyShadows.hlsl"
            #include "WorldGenWind.hlsl"
            TEXTURE2D(_ClumpAtlas);SAMPLER(sampler_ClumpAtlas);
            struct Root {float4 positionForm,colorHeight,shape,normal,grasses,herbs;};
            StructuredBuffer<Root> _CoverRoots;StructuredBuffer<uint> _VisibleRoots;
            float4 _CoverOrigin,_CoverTileOrigin,_FogColor,_AmbientTint;float _GrassNear,_GrassRange,_GrassWind,_FogDistance,_Daylight,_ClumpDensity;int _CoverLod;uint _CoverSeed;
            struct O {float4 positionCS:SV_POSITION;float3 position:TEXCOORD0;float2 uv:TEXCOORD1;float3 color:COLOR;float2 fade:TEXCOORD2;float2 card:TEXCOORD3;};
            uint Mix(uint x){x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;return x^(x>>16);}
            float Rand(uint x){return (Mix(x)&0xffffffu)/16777216.;}
            float3 ClumpLight(float3 p,float3 n)
            {
                Light sun=GetMainLight();half shade=WorldGenCanopyShadow(1,p);
                // One shadow lookup per corner; the fragment only samples the
                // foliage image. Dense canopy retains its spatial sky shelter.
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && !defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 coord=TransformWorldToShadowCoord(p);
                if(!BEYOND_SHADOW_FAR(coord)&&all(coord.xy>=0)&&all(coord.xy<=1))
                    sun.shadowAttenuation=lerp(1,SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture,sampler_LinearClampCompare,coord.xyz),GetMainLightShadowParams().x);
                #endif
                sun.shadowAttenuation=min(sun.shadowAttenuation,shade);
                float3 ambient=VegetationAmbient(_AmbientTint).rgb*WorldGenCanopyAmbient(shade);
                return ambient+sun.color*(.55+.45*saturate(dot(n,sun.direction)))*sun.shadowAttenuation*lerp(.025,1,VegetationDaylight(_Daylight))
                    +SecondaryCelestialLight(n,1)+CampDiffuseLight(p,n,1);
            }
            O vert(uint vid:SV_VertexID,uint iid:SV_InstanceID)
            {
                O o=(O)0;Root r=_CoverRoots[_VisibleRoots[iid]];
                float3 root=r.positionForm.xyz+_CoverOrigin.xyz;
                float d=distance(root,_WorldSpaceCameraPos);bool far=_CoverLod>=2;
                float near=1-smoothstep(.40*_GrassNear,.60*_GrassNear,d),handoff=1-smoothstep(.80*_GrassNear,_GrassNear,d);
                float weight=_CoverLod==0?near:far?1-handoff:1-near;
                float low=_CoverLod==0?0:far?handoff:near;
                float expected=r.colorHeight.w*(dot(r.grasses,float4(.58,.90,.40,.85))+r.herbs.x*.26);
                float range=lerp(min(_GrassRange,65),_GrassRange,smoothstep(.45,.95,expected));
                float visibility=1-smoothstep(range*.65,range,d);
                if(weight<.001||visibility<.001){o.positionCS=float4(0,0,0,1);return o;}
                int2 canonical=(int2)round((r.positionForm.xz+_CoverTileOrigin.xy)/.60-.5);
                uint seed=Mix(asuint(canonical.x)*73856093u^asuint(canonical.y)*19349663u^_CoverSeed);
                int card=vid/6,v=vid%6;float2 jitter=r.shape.yz;float2 micro=0;
                if(far)
                {
                    int index=_CoverLod==2?card:_CoverLod==3?card*2:0;
                    int2 cellOffset=int2(index==1||index==2?1:0,index==1||index==3?1:0);
                    int2 cell=(int2)round((r.positionForm.xz+_CoverTileOrigin.xy)/1.2-.5)*2+cellOffset;
                    seed=Mix(asuint(cell.x)*73856093u^asuint(cell.y)*19349663u^_CoverSeed);
                    jitter=float2(Rand(seed+5),Rand(seed+6));micro=(float2(cellOffset)-.5)*.60;
                    card=0;
                }
                if(Rand(seed+41)>saturate(r.normal.w/.25)*saturate(_ClumpDensity)){o.positionCS=float4(0,0,0,1);return o;}
                float2 spread=micro+(jitter-.5)*.42;
                float3 anchor=root+float3(spread.x,-dot(r.normal.xz,spread)/max(.3,r.normal.y),spread.y);
                float t=(v==2||v==3||v==5)?1:0,side=(v==0||v==2||v==3)?-1:1;
                float angle=jitter.x*6.283185307+card*1.047197551;
                float3 across=float3(cos(angle),0,sin(angle));
                float3 eye=GetWorldSpaceNormalizeViewDir(anchor),facing=normalize(cross(eye,float3(0,1,0))+float3(.0001,0,0));
                facing*=dot(facing,across)<0?-1:1;
                across=normalize(lerp(across,facing,far?.80:0));
                float height=clamp(expected*(.80+.40*jitter.y),.10,3.2);
                float width=clamp(height*.58,.085,.54)*lerp(.85,1.2,jitter.y)*(_CoverLod==3?1.4:_CoverLod==4?2:1);
                float3 up=normalize(lerp(float3(0,1,0),r.normal.xyz,.12));
                float3 p=anchor+across*(side*width)+up*(t*height)+WorldGenVegetationOffset(anchor,1)*(_GrassWind*t*t*.65);
                bool herb=Rand(seed+19)<r.herbs.x;
                bool warm=!herb&&Rand(seed+23)<r.grasses.y+r.grasses.z+r.grasses.w;
                float2 tile=herb?float2(0,0):warm?float2(.5,.5):float2(0,.5);
                o.uv=tile+float2(side*.5+.5,t)*.496+.002;
                o.card=float2(side*.5+.5,t);
                float3 palette=r.colorHeight.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                palette=SRGBToLinear(palette);
                #endif
                o.color=palette*ClumpLight(anchor+up*height*.5,normalize(up*.8+float3(eye.x,0,eye.z)*.2))*(.90+.20*jitter.x);
                o.position=p;o.positionCS=TransformWorldToHClip(p);o.fade=float2(low,low+weight)*visibility;return o;
            }
            half4 frag(O i):SV_Target
            {
                float threshold=WorldGenDither(i.positionCS.xy);clip(threshold-i.fade.x);clip(i.fade.y-threshold);
                half4 tex=SAMPLE_TEXTURE2D(_ClumpAtlas,sampler_ClumpAtlas,i.uv);
                // The source is a continuous sward tile. Retire its cut side
                // edges before they can appear as small rectangular fences.
                float edge=smoothstep(0,.17,min(i.card.x,1-i.card.x));
                clip(tex.a*edge-.35);
                float3 color=i.color*clamp(tex.rgb*4.6,.20,1.5);
                return half4(ApplyPlanetAir(color,i.position,_WorldSpaceCameraPos,VegetationFog(_FogColor).rgb,VegetationFogDistance(_FogDistance)),1);
            }
            ENDHLSL
        }
    }
}
