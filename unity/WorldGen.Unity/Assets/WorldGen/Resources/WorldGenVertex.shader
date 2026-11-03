Shader "WorldGen/VertexSurface"
{
    Properties { _Unlit ("Unlit data colors", Float) = 0 _MapMode ("Map clipping", Float) = 0 _UsePalette ("Cell palette", Float) = 0 _SmallFeatures ("Subpixel feature visibility", Float) = 0 _Natural ("Continuous terrain", Float) = 0 _Linear ("Linear colors", Float) = 0 _Palette ("Palette", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _Unlit, _MapMode, _UsePalette, _Natural, _Linear, _SmallFeatures, _Simulation, _SatelliteOpacity, _OverlayOpacity, _BlendCells, _PaletteHeight; TEXTURE2D(_Palette); SAMPLER(sampler_Palette); TEXTURE2D(_HumanMask); SAMPLER(sampler_HumanMask);
            struct Attributes {float4 positionOS:POSITION;float4 color:COLOR;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float3 mapPosition:TEXCOORD1;float2 naturalHeight:TEXCOORD2;float2 blendCells:TEXCOORD3;};
            struct Varyings {float4 positionCS:SV_POSITION;float4 color:COLOR;float3 normalWS:TEXCOORD0;float mapX:TEXCOORD1;float2 uv:TEXCOORD2;float3 localSphere:TEXCOORD3;float height:TEXCOORD4;float4 human:TEXCOORD5;float3 civicTint:TEXCOORD6;};
            float hash3(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
            float noise3(float3 p){float3 b=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash3(b),hash3(b+float3(1,0,0)),f.x),lerp(hash3(b+float3(0,1,0)),hash3(b+float3(1,1,0)),f.x),f.y),lerp(lerp(hash3(b+float3(0,0,1)),hash3(b+float3(1,0,1)),f.x),lerp(hash3(b+float3(0,1,1)),hash3(b+float3(1,1,1)),f.x),f.y),f.z);}
            float2 cellUV(float id){return float2((fmod(id,2048)+.5)/2048,(floor(id/2048)+.5)/_PaletteHeight);}
            float _Regional,_RegionalHeight,_RegionalBackground,_DatedCells;
            TEXTURE2D(_RegionalCells);SAMPLER(sampler_RegionalCells);TEXTURE2D(_RegionalColors);SAMPLER(sampler_RegionalColors);TEXTURE2D(_RegionalBack);SAMPLER(sampler_RegionalBack);
            float regionIndex(float2 uv){float3 bytes=floor(SAMPLE_TEXTURE2D_LOD(_RegionalCells,sampler_RegionalCells,uv,0).rgb*255+.5);return dot(bytes,float3(1,256,65536))-1;}
            float2 regionUV(float index){return float2((fmod(index,2048)+.5)/2048,(floor(index/2048)+.5)/_RegionalHeight);}
            float4 humanMask(float2 uv)
            {
                if(_Regional<.5)return SAMPLE_TEXTURE2D_LOD(_HumanMask,sampler_HumanMask,uv,0);
                float4 cell=SAMPLE_TEXTURE2D_LOD(_RegionalCells,sampler_RegionalCells,uv,0);float index=regionIndex(uv);
                if(index<0)return float4(0,0,0,cell.a);
                float4 color=SAMPLE_TEXTURE2D_LOD(_RegionalColors,sampler_RegionalColors,regionUV(index),0);
                if(color.a<.9)color.a=cell.a;else if(cell.a<.1)color.a=color.a>.94?60.0/255:50.0/255;
                return color;
            }
            Varyings vert(Attributes i){Varyings o;float3 p=_MapMode>.5?i.mapPosition:i.positionOS.xyz;o.uv=i.uv;o.mapX=TransformObjectToWorld(p).x;o.positionCS=TransformObjectToHClip(p);o.color=_Natural>.5?float4(SAMPLE_TEXTURE2D_LOD(_Palette,sampler_Palette,i.uv,0).rgb,1):i.color;if(_BlendCells>.5)o.color.rgb=(o.color.rgb+SAMPLE_TEXTURE2D_LOD(_Palette,sampler_Palette,cellUV(i.blendCells.x),0).rgb+SAMPLE_TEXTURE2D_LOD(_Palette,sampler_Palette,cellUV(i.blendCells.y),0).rgb)/3;o.normalWS=TransformObjectToWorldNormal(length(i.normalOS)>.1?i.normalOS:normalize(i.positionOS.xyz));o.localSphere=normalize(i.positionOS.xyz);o.height=i.naturalHeight.x;float code=humanMask(i.uv).a;o.human=float4(code<.3?1:0,(code>.5||code>.1&&code<.3)?1:0,(code>.95||code>.2&&code<.3)?1:0,code>.88?.24:code>.8?.46:code>.73?.70:.78);o.civicTint=(code>.9&&code<.97||code>.18&&code<.245)?humanMask(i.uv).rgb:o.human.www;return o;}
            half4 frag(Varyings i):SV_Target
            {
                if(_SmallFeatures>.5)clip(abs(i.height)/max(1e-9,length(fwidth(normalize(i.localSphere))))-.65);
                if(_MapMode>.5)clip(2-abs(i.mapX));float light=lerp(.55+.45*saturate(dot(normalize(i.normalWS),normalize(float3(-.5,.7,-1)))),1,_Unlit);
                float3 color=_UsePalette>.5 && _Natural<.5?SAMPLE_TEXTURE2D(_Palette,sampler_Palette,i.uv).rgb:i.color.rgb;
                if(_Natural>.5)
                {
                    float3 p=normalize(i.localSphere);float fine=1-smoothstep(.0002,.0015,length(fwidth(p)));
                    color*=1+.045*(noise3(p*45)-.5)+.08*(noise3(p*600)-.5)*fine;
                    float3 ocean=lerp(float3(.025,.08,.16),float3(.12,.48,.56),saturate((i.height+6500)/6500));if(_Linear>.5)ocean=pow(ocean,2.2);
                    float aa=max(1,fwidth(i.height));color=lerp(ocean,color,smoothstep(-aa,aa,i.height));
                }
                if(_Regional>.5&&_RegionalBackground>.5){float index=regionIndex(i.uv);if(_DatedCells>.5)color=SAMPLE_TEXTURE2D(_RegionalBack,sampler_RegionalBack,i.uv).rgb;else if(index>=0)color=SAMPLE_TEXTURE2D(_RegionalBack,sampler_RegionalBack,regionUV(index)).rgb;}
                if(_Simulation>.5)
                {
                    float occupancy=smoothstep(.35,.65,i.human.y);
                    bool water=(_Natural>.5||_SmallFeatures>.5)?i.height<0:i.human.x>.5;
                    float3 plain=water?float3(.55,.78,.9):float3(1,1,1);
                    if(_Linear>.5)plain=pow(plain,2.2);
                    color=lerp(plain,color*light,_SatelliteOpacity);
                    if(!water&&occupancy>0){bool selected=i.human.z>.5;float3 tint=selected?lerp(i.civicTint,float3(1,1,1),.3):i.civicTint;if(_Linear>.5)tint=pow(tint,2.2);color=lerp(color,tint,_OverlayOpacity*occupancy);}
                    return half4(color,1);
                }
                return half4(color*light,1);
            }
            ENDHLSL
        }
    }
}
