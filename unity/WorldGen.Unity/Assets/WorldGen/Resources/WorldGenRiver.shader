Shader "WorldGen/RiverStroke"
{
    Properties { _MapMode("Map",Float)=0 _Linear("Linear",Float)=0 _WorldRadiusM("Radius",Float)=6371000 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _MapMode,_Linear,_WorldRadiusM;
            struct Attributes { float4 positionOS:POSITION;float3 mapPosition:TEXCOORD1;float4 stroke:TEXCOORD4; };
            struct Varyings { float4 positionCS:SV_POSITION;float3 sphere:TEXCOORD0;float4 stroke:TEXCOORD1;float mapX:TEXCOORD2; };
            Varyings vert(Attributes i)
            {
                Varyings o;float3 p=_MapMode>.5?i.mapPosition:i.positionOS.xyz;
                o.positionCS=TransformObjectToHClip(p);o.mapX=TransformObjectToWorld(p).x;o.sphere=normalize(i.positionOS.xyz);o.stroke=i.stroke;return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                if(_MapMode>.5)clip(2-abs(i.mapX));
                float3 p=normalize(i.sphere);float pixelM=max(1,max(length(ddx(p)),length(ddy(p)))*_WorldRadiusM);
                float width=max(0,i.stroke.z),order=max(1,i.stroke.w),envelope=25000+8*width;
                float limit=1500*pow(3,min(11,order-1));float visibility=1-smoothstep(limit*.7,limit*1.3,pixelM);
                float halfWidth=min(envelope*.95,max(width*.75,pixelM*(.5+.025*log2(1+width))));
                float distanceM=length(float2(i.stroke.x,max(0,i.stroke.y)))*envelope;
                float aa=max(1,fwidth(distanceM));float alpha=(1-smoothstep(halfWidth-aa*.6,halfWidth+aa*.6,distanceM))*visibility;
                clip(alpha-.003);float3 color=lerp(float3(.075,.31,.48),float3(.035,.25,.43),saturate(log2(1+width)/12));
                if(_Linear>.5)color=pow(color,2.2);return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
