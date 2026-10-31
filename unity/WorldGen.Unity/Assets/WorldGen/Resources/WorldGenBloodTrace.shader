Shader "WorldGen/BloodTrace"
{
    Properties{_VegetationSceneEnabled("World lighting",Float)=1}
    SubShader
    {
        Tags{"RenderType"="Transparent" "Queue"="AlphaTest+5" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Tags{"LightMode"="UniversalForward"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off Offset -1,-1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WorldGenLighting.hlsl"
            #include "WorldGenVegetationScene.hlsl"
            struct A{float4 position:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct O{float4 position:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float4 color:COLOR;};
            O vert(A i){O o;o.world=TransformObjectToWorld(i.position.xyz);o.position=TransformWorldToHClip(o.world);o.uv=i.uv;o.color=i.color;return o;}
            half4 frag(O i):SV_Target
            {
                float2 q=i.uv*2-1;float a=atan2(q.y,q.x),rim=.62+.12*sin(a*5)+.08*sin(a*9+2);float alpha=1-smoothstep(rim-.08,rim+.03,length(q));
                alpha=max(alpha,(1-smoothstep(.07,.13,length(q-float2(.63,.41))))*.85);alpha=max(alpha,(1-smoothstep(.04,.09,length(q-float2(-.53,.7))))*.75);clip(alpha-.01);
                Light sun=GetMainLight();half3 albedo=i.color.rgb;half3 color=albedo*(VegetationAmbient(float4(.12,.14,.17,1)).rgb+sun.color*(.2+.8*saturate(sun.direction.y)));
                color+=SecondaryCelestialLight(float3(0,1,0),albedo)+CampDiffuseLight(i.world,float3(0,1,0),albedo);return half4(color,alpha*.88);
            }
            ENDHLSL
        }
    }
}
