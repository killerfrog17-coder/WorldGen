Shader "WorldGen/GroundCover"
{
    Properties { _FogDistance("Fog distance",Float)=200000 _Daylight("Daylight",Float)=1 _FogColor("Sky colour",Color)=(.57,.69,.76,1) _AmbientTint("Ambient",Color)=(.12,.14,.17,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" } Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "WorldGenAir.hlsl"
        #include "WorldGenLodFade.hlsl"
        struct Root{float4 positionForm;float4 colorHeight;float4 shape;float4 normal;};
        StructuredBuffer<Root> _CoverRoots;
        float4 _CoverOrigin,_FogColor,_AmbientTint;float _FogDistance,_Daylight;int _CoverLod;
        struct O{float4 positionCS:SV_POSITION;float3 position:TEXCOORD0;float3 normal:TEXCOORD1;float3 color:COLOR;float2 fade:TEXCOORD2;float height:TEXCOORD3;};
        O vert(uint vid:SV_VertexID,uint iid:SV_InstanceID)
        {
            O o=(O)0;Root r=_CoverRoots[iid];float3 root=r.positionForm.xyz+_CoverOrigin.xyz;
            float d=length(root-_WorldSpaceCameraPos);float near=1-smoothstep(25,42,d),far=smoothstep(65,90,d);
            float visibility=1-smoothstep(130,160,d);float weight=_CoverLod==0?near:_CoverLod==1?1-near-far:far;
            if(weight<.0001||visibility<.0001){o.positionCS=float4(0,0,0,1);return o;}
            int segments=_CoverLod==0?3:_CoverLod==1?2:1,blade=vid/(segments*6),v=vid%6;
            int segment=(vid/6)%segments;
            float t=(segment+((v==2||v==3||v==5)?1:0))/(float)segments;
            float side=(v==0||v==2||v==3)?-1:1;
            float angle=blade*2.399963+r.shape.y*6.283185;float2 dir=float2(sin(angle),cos(angle)),across=float2(dir.y,-dir.x);
            int form=(int)r.positionForm.w;
            float height=r.colorHeight.w*(.76+.24*sin(blade*4.13+r.shape.z*8));
            float width=(form<4?.014:form==4?.055:form==5?.065:form==6?.10:form==7?.12:.055)*r.shape.x;
            float profile=form<4?pow(1-t,.65):(1-t)*(.20+sin(t*3.14159)*(.7+.3*sin(t*20)))+.015;
            width*=profile*(_CoverLod==0?1:_CoverLod==1?1.4:2.0);
            float2 spread=dir*(form<4?.11:form<6?.12:.16)*sqrt((blade+.5)/7.);
            float bend=form<4?.28:form<6?.65:1.2;
            float wind=(sin(root.x*.13+root.z*.11+_Time.y*1.6)+.4*sin(root.x*.37-root.z*.18+_Time.y*2.7))*.10;
            float3 up=normalize(lerp(float3(0,1,0),r.normal.xyz,form>=6?.9:.3));
            float spreadY=-dot(r.normal.xz,spread)/max(.3,r.normal.y);
            float3 p=root+up*(height*t)+float3(spread.x,spreadY,spread.y)+float3(dir.x,0,dir.y)*(bend*height*t*t)+float3(.8,0,.4)*(wind*height*t*t)+float3(across.x,0,across.y)*(width*side);
            float birth=smoothstep(0,2,_Time.y-_CoverOrigin.w);
            float low=_CoverLod==0?0:_CoverLod==1?near:1-far;
            o.fade=float2(low,low+weight)*visibility*birth;
            o.position=p;o.positionCS=TransformWorldToHClip(p);o.normal=normalize(up+float3(across.x,.8,across.y)*.25);
            o.color=r.colorHeight.rgb*(.84+.24*r.shape.z)*lerp(.55,1.12,t);o.height=t;return o;
        }
        void Fade(O i){float threshold=WorldGenDither(i.positionCS.xy);clip(threshold-i.fade.x);clip(i.fade.y-threshold);}
        half4 frag(O i):SV_Target
        {
            Fade(i);float3 albedo=i.color;
            #ifndef UNITY_COLORSPACE_GAMMA
            albedo=SRGBToLinear(albedo);
            #endif
            Light sun=GetMainLight(TransformWorldToShadowCoord(i.position));
            float diffuse=.48+.52*saturate(dot(i.normal,sun.direction));
            float transmission=pow(saturate(dot(-sun.direction,GetWorldSpaceNormalizeViewDir(i.position))),3)*.22;
            float3 color=albedo*(_AmbientTint.rgb+sun.color*(diffuse+transmission)*sun.shadowAttenuation*lerp(.025,1,_Daylight));
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
