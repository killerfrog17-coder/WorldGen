Shader "WorldGen/LocalTerrain"
{
    Properties { _Water("Water",Float)=0 _FogDistance("Fog distance",Float)=14000 }
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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            float _Water, _FogDistance; float4 _LocalOrigin;
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normal:TEXCOORD0; float3 position:TEXCOORD1; float4 color:COLOR; };
            float hash(float2 p) { p=frac(p*.1031);p+=dot(p,p.yx+33.33);return frac((p.x+p.y)*p.x); }
            float grainNoise(float2 p)
            {
                float2 q=floor(p),t=frac(p);t=t*t*(3-2*t);
                return lerp(lerp(hash(q),hash(q+float2(1,0)),t.x),lerp(hash(q+float2(0,1)),hash(q+1),t.x),t.y);
            }
            Varyings vert(Attributes v)
            {
                Varyings o; o.position=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.position);
                o.normal=TransformObjectToWorldNormal(v.normalOS); o.color=v.color; return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                if(_Water>.5)clip(i.color.a-.01);
                float shade=.42+.58*saturate(dot(normalize(i.normal),normalize(float3(-.45,.8,.3))));
                float footprint=max(length(ddx(i.position)),length(ddy(i.position)));
                float grain=1+.065*(1-smoothstep(1,15,footprint))*(grainNoise((i.position.xz+_LocalOrigin.xy)*.25)-.5);
                float3 color=i.color.rgb,fogColor=float3(.57,.69,.76);
                #ifndef UNITY_COLORSPACE_GAMMA
                color=SRGBToLinear(color);fogColor=SRGBToLinear(fogColor);
                #endif
                color*=(_Water>.5?.92:shade*grain);
                float fog=smoothstep(_FogDistance*.25,_FogDistance,length(i.position-_WorldSpaceCameraPos));
                return half4(lerp(color,fogColor,fog),1);
            }
            ENDHLSL
        }
    }
}
