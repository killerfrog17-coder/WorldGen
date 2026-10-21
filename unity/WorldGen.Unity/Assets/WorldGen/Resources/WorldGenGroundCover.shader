Shader "WorldGen/GroundCover"
{
    Properties { _GrassNear("Blade range",Float)=45 _GrassRange("Grass range",Float)=120 _GrassWind("Wind",Float)=1 _FogDistance("Fog distance",Float)=200000 _Daylight("Daylight",Float)=1 _FogColor("Sky colour",Color)=(.57,.69,.76,1) _AmbientTint("Ambient",Color)=(.12,.14,.17,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" } Cull Off ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "WorldGenAir.hlsl"
        #include "WorldGenLodFade.hlsl"
        #include "WorldGenGrassLight.hlsl"
        #include "WorldGenWind.hlsl"
        struct Root{float4 positionForm;float4 colorHeight;float4 shape;float4 normal;float4 grasses;float4 herbs;};
        StructuredBuffer<Root> _CoverRoots;
        StructuredBuffer<uint> _VisibleRoots;
        float4 _CoverOrigin,_CoverTileOrigin,_FogColor,_AmbientTint,_BladeCounts;float _FogDistance,_Daylight,_GrassRange,_GrassWind,_GrassNear;int _CoverLod;uint _CoverSeed;
        struct O{float4 positionCS:SV_POSITION;float3 position:TEXCOORD0;float3 normal:TEXCOORD1;float3 color:COLOR;float2 fade:TEXCOORD2;float2 leaf:TEXCOORD3;};
        float BladeRandom(float blade,float salt,float seed){return frac(sin(blade*127.1+salt*311.7+seed*74.7)*43758.5453);}
        uint LeafMix(uint x){x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;return x^(x>>16);}
        float LeafRand(uint x){return (LeafMix(x)&0xffffffu)/16777216.;}
        O vert(uint vid:SV_VertexID,uint iid:SV_InstanceID)
        {
            O o=(O)0;Root r=_CoverRoots[_VisibleRoots[iid]];float3 root=r.positionForm.xyz+_CoverOrigin.xyz;
            float d=length(root-_WorldSpaceCameraPos);
            bool distant=_CoverLod>=2;
            float near=1-smoothstep(.40*_GrassNear,.60*_GrassNear,d);
            float handoff=1-smoothstep(.80*_GrassNear,_GrassNear,d);
            float visibility=distant?1-smoothstep(max(_GrassNear,_GrassRange*.65),_GrassRange,d):handoff;
            float weight=_CoverLod==0?near:distant?1-handoff:1-near;
            if(weight<.0001||visibility<.0001){o.positionCS=float4(0,0,0,1);return o;}
            int segments=_CoverLod==0?(int)_BladeCounts.w:_CoverLod==1?2:1,slot=vid/(segments*6),blade=slot,v=vid%6;
            float seed=r.shape.y*127+r.shape.z*31;
            float2 microOffset=0,jitter=r.shape.yz;
            if(distant)
            {
                // A 1.2m coarse field contains four canonical 0.6m cells.
                // Interleave their leaf prefixes, preserving close root positions,
                // species choices and animation instead of inventing sprite tufts.
                int micro=slot&3;int2 cellOffset=int2(micro==1||micro==2?1:0,micro==1||micro==3?1:0);
                int2 cell=(int2)round((r.positionForm.xz+_CoverTileOrigin.xy)/1.2-.5)*2+cellOffset;
                uint hash=LeafMix(asuint(cell.x)*73856093u^asuint(cell.y)*19349663u^_CoverSeed);
                jitter=float2(LeafRand(hash+5),LeafRand(hash+6));seed=jitter.x*127+jitter.y*31;
                microOffset=(float2(cellOffset)-.5)*.60;blade=slot/4;
            }
            float pick=BladeRandom(blade,8,seed),grassTotal=dot(r.grasses,float4(1,1,1,1)),cumulative=0;int form=8;
            bool herbGroup=pick>=grassTotal;float4 mixture=herbGroup?r.herbs:r.grasses;
            float target=herbGroup?pick-grassTotal:pick;
            [unroll]for(int species=0;species<4;species++)
            {
                cumulative+=mixture[species];
                if(target<cumulative){form=species+(herbGroup?4:0);break;}
            }
            float maximum=_CoverLod==0?_BladeCounts.x:_BladeCounts.y;
            float reduction=lerp(1,.75,saturate(d/(_CoverLod==0?.60*_GrassNear:_GrassNear)));
            // Coverage controls density through the field, not coin-flip gaps
            // that erase whole squares. Mesic ground reaches a continuous sward.
            float occupancy=saturate(r.normal.w/.45);
            // Extra roots close to the feet, smoothly retired before the usual
            // geometry LOD. Distant blade density and width stay unchanged.
            float closeFill=_CoverLod==0?lerp(.5,1,1-smoothstep(4,14,d)):1;
            float countF=distant?_BladeCounts.y*3*pow(_GrassNear/max(_GrassNear,d),2):maximum*reduction*closeFill;
            float bladesF=countF*occupancy;
            if(slot>=ceil(bladesF)){o.positionCS=float4(0,0,0,1);return o;}
            float fractional=slot==(int)ceil(bladesF)-1?max(.001,bladesF-slot):1;
            int segment=(vid/6)%segments;
            float t=(segment+((v==2||v==3||v==5)?1:0))/(float)segments;
            float side=(v==0||v==2||v==3)?-1:1;
            float a=BladeRandom(blade,1,seed),b=BladeRandom(blade,2,seed),c=BladeRandom(blade,3,seed);
            float angle=a*6.283185;float2 dir=float2(sin(angle),cos(angle));
            float leafAngle=BladeRandom(blade,5,seed)*6.283185;
            float3 across=float3(cos(leafAngle),0,sin(leafAngle));
            static const float heights[9]={.58,.90,.40,.85,.26,.46,.045,.018,.19};
            float height=min(3.2,heights[form]*r.colorHeight.w*(.70+.55*b));
            float width=(form<4?.010:form<6?.030:form<8?.025:.035)*r.shape.x*(.65+.7*c);
            // A one-segment distant leaf must retain width at its endpoints;
            // the near leaf's sin(pi*t) outline would collapse both to zero.
            float profile=form<4?pow(saturate(1-t),.65):distant?lerp(.85,.06,t):sin(t*3.14159265)*(.65+.35*sin(t*21));
            // Fractional blade retirement + bounded width compensation (AMD's
            // LOD principle). Each leaf has its own root, not a shared star fan.
            float leavesPerCell=distant?countF*.25:countF;
            width*=clamp(_BladeCounts.z/max(.1,leavesPerCell),1,24);
            // Bounded projected coverage for thin/edge-on leaves, not a large
            // camera-facing bank. Fractional retirement still reaches zero.
            float3 view=GetWorldSpaceNormalizeViewDir(root);
            float3 cameraAcross=normalize(cross(view,float3(0,1,0))+float3(.0001,0,0));
            cameraAcross*=dot(cameraAcross,across)<0?-1:1;
            float edge=abs(dot(across,view));across=normalize(lerp(across,cameraAcross,edge*edge*.85));
            float pixelMetres=2*d/max(1,_ScreenParams.y*abs(UNITY_MATRIX_P[1][1]));
            width=max(width,min(.20,pixelMetres*.28)*smoothstep(15,60,d));
            width*=profile*fractional;
            // Low-discrepancy square distribution tiles the ground continuously.
            // Prefixes stay spread across the whole square at every density/LOD.
            float2 spread=microOffset+(frac(float2(.754877666,.569840296)*(blade+.5)+jitter)-.5)*.60;
            float spreadY=-dot(r.normal.xz,spread)/max(.3,r.normal.y);
            float3 basePosition=root+float3(spread.x,spreadY,spread.y);
            if(form==6||form==7)basePosition.y+=.015;
            float bend=.18+.35*c;
            float3 anchor=root+float3(microOffset.x,-dot(r.normal.xz,microOffset)/max(.3,r.normal.y),microOffset.y);
            float3 velocity=_GrassWind>.5&&form!=6&&form!=7?WorldGenWindVelocity(anchor):float3(0,0,0);
            float speed=length(velocity);
            float3 wind=speed>.0001?velocity/speed*(.85*speed/(speed+7)):float3(0,0,0);
            float3 up=normalize(lerp(float3(0,1,0),r.normal.xyz,form>=6?.85:.12));
            float3 p1=up*(height*.55);
            float3 p2=normalize(up+float3(dir.x,0,dir.y)*bend+wind)*height;
            float3 tangent=normalize(2*(1-t)*p1+2*t*(p2-p1));
            float3 curve=2*(1-t)*t*p1+t*t*p2;
            curve=lerp(curve,p2*t,smoothstep(.60*_GrassNear,.80*_GrassNear,d));
            float3 p=basePosition+curve+across*(width*side);
            float low=_CoverLod==0?0:distant?handoff:near;
            o.fade=float2(low,low+weight)*visibility;
            o.position=p;o.positionCS=TransformWorldToHClip(p);
            o.normal=normalize(lerp(up*.8+cross(across,tangent)*.35,up,smoothstep(20,120,d)*.85));
            // Converge to the terrain's statistical canopy albedo as leaves
            // become subpixel. Height, local palette and wind remain intact.
            float tone=lerp((.78+.44*c)*lerp(.48,1.2,t),.82,smoothstep(30,160,d));
            o.color=r.colorHeight.rgb*tone;o.leaf=float2(side,t);return o;
        }
        void Fade(O i){float threshold=WorldGenDither(i.positionCS.xy);clip(threshold-i.fade.x);clip(i.fade.y-threshold);}
        half4 frag(O i):SV_Target
        {
            Fade(i);
            float3 albedo=i.color;
            #ifndef UNITY_COLORSPACE_GAMMA
            albedo=SRGBToLinear(albedo);
            #endif
            albedo*=.86+.14*(1-abs(i.leaf.x));
            float3 color=WorldGenGrassLight(albedo,i.normal,i.position,_AmbientTint.rgb,_Daylight);
            color+=SecondaryCelestialLight(i.normal,albedo);
            return half4(ApplyPlanetAir(color,i.position,_WorldSpaceCameraPos,_FogColor.rgb,_FogDistance),1);
        }
        float depth(O i):SV_Target{Fade(i);return i.positionCS.z;}
        ENDHLSL
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            ENDHLSL
        }
        Pass
        {
            Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment depth
            ENDHLSL
        }
    }
}
