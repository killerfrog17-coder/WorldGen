Shader "WorldGen/PortableItem"
{
    Properties
    {
        _SourceCoats("Original animal coats",2DArray)=""{}
        _HasFur("Use original coat",Float)=0 _Coat("Coat",Float)=0
        _FurRect("Torso UV rectangle",Vector)=(0,0,1,1)
        _Preview("Inventory lighting",Float)=0 _Wearer("Camera-scoped wearer",Float)=0
        _BaseMap("Shadow opacity",2D)="white"{} _BaseColor("Shadow tint",Color)=(1,1,1,1) _Cutoff("Cutoff",Float)=0 _Cull("Cull",Float)=0
        _VegetationSceneEnabled("Shared lighting",Float)=1 _FogDistance("Fog distance",Float)=60000
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "WorldGenLighting.hlsl"
        #include "WorldGenVegetationScene.hlsl"
        #include "WorldGenAir.hlsl"
        TEXTURE2D_ARRAY(_SourceCoats);SAMPLER(sampler_SourceCoats);
        float _HasFur,_Coat,_Preview,_Wearer,_FogDistance;float4 _FurRect;float _WorldGenFirstPersonHumanId;
        struct A{float4 position:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;};
        struct O{float4 position:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;half4 color:COLOR;};
        O vert(A a){O o;o.position=TransformObjectToHClip(a.position.xyz);o.world=TransformObjectToWorld(a.position.xyz);o.normal=TransformObjectToWorldNormal(a.normal);o.uv=a.uv;o.color=a.color;return o;}
        void ClipWearer(){clip(_Preview<.5&&_Wearer>.5&&_WorldGenFirstPersonHumanId>.5&&abs(_Wearer-_WorldGenFirstPersonHumanId)<.25?-1:1);}
        ENDHLSL
        Pass
        {
            Tags {"LightMode"="UniversalForward"} Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            half4 frag(O i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                ClipWearer();half3 color=i.color.rgb;
                if(_HasFur>.5&&i.color.a>.5)color=SAMPLE_TEXTURE2D_ARRAY(_SourceCoats,sampler_SourceCoats,lerp(_FurRect.xy,_FurRect.zw,saturate(i.uv)),_Coat).rgb;
                half3 n=normalize(i.normal)*IS_FRONT_VFACE(face,1,-1);
                if(_Preview>.5){half light=.45+.5*saturate(dot(n,normalize(float3(-.5,.8,-.6))));return half4(color*light,1);}
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));half3 ambient=VegetationAmbient(float4(.18,.2,.23,1)).rgb;
                color*=ambient+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
                return half4(ApplyPlanetAir(color,i.world,_WorldSpaceCameraPos,VegetationFogLinear(SRGBToLinear(float3(.57,.69,.76))),VegetationFogDistance(_FogDistance)),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth
            half4 fragDepth(O i):SV_Target{ClipWearer();return 0;}
            ENDHLSL
        }
    }
}
