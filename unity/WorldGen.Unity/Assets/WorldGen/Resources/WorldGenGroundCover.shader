Shader "WorldGen/GroundCover"
{
    Properties { _FoliageAtlas("Foliage cutouts",2D)="white" {} _FogDistance("Fog distance",Float)=200000 _Daylight("Daylight",Float)=1 _FogColor("Sky colour",Color)=(.57,.69,.76,1) _AmbientTint("Ambient",Color)=(.12,.14,.17,1) }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" } Cull Off ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "WorldGenAir.hlsl"
        #include "WorldGenLodFade.hlsl"
        struct Root{float4 positionForm;float4 colorHeight;float4 shape;float4 normal;};
        StructuredBuffer<Root> _CoverRoots;
        TEXTURE2D(_FoliageAtlas);SAMPLER(sampler_FoliageAtlas);
        float4 _CoverOrigin,_FogColor,_AmbientTint;float _FogDistance,_Daylight;int _CoverLod;
        struct O{float4 positionCS:SV_POSITION;float3 position:TEXCOORD0;float3 normal:TEXCOORD1;float3 color:COLOR;float2 fade:TEXCOORD2;float2 uv:TEXCOORD3;};
        O vert(uint vid:SV_VertexID,uint iid:SV_InstanceID)
        {
            O o=(O)0;Root r=_CoverRoots[iid];float3 root=r.positionForm.xyz+_CoverOrigin.xyz;
            float d=length(root-_WorldSpaceCameraPos);float near=1-smoothstep(25,42,d),far=smoothstep(65,90,d);
            float visibility=1-smoothstep(130,160,d);float weight=_CoverLod==0?near:_CoverLod==1?1-near-far:far;
            if(weight<.0001||visibility<.0001){o.positionCS=float4(0,0,0,1);return o;}
            int segments=_CoverLod==0?3:_CoverLod==1?2:1,card=vid/(segments*6),v=vid%6;
            int segment=(vid/6)%segments;
            float t=(segment+((v==2||v==3||v==5)?1:0))/(float)segments;
            float side=(v==0||v==2||v==3)?-1:1;
            float angle=card*1.047198+r.shape.y*6.283185;float2 dir=float2(sin(angle),cos(angle)),across=float2(dir.y,-dir.x);
            int form=(int)r.positionForm.w;
            float height=r.colorHeight.w*(.90+.10*sin(card*4.13+r.shape.z*8));
            // Each bank contains dozens of leaves. Card width remains physical,
            // so tallgrass is taller rather than an enlarged single broad blade.
            float width=(form<4?.49:form==4?.40:form==5?.52:form<8?.20:.26)*r.shape.x;
            float2 spread=dir*.07;
            float bend=form<4?.12:form<6?.23:.35;
            float wind=(sin(root.x*.13+root.z*.11+_Time.y*1.6)+.4*sin(root.x*.37-root.z*.18+_Time.y*2.7))*.10;
            float3 up=normalize(lerp(float3(0,1,0),r.normal.xyz,form>=6?.9:.3));
            float spreadY=-dot(r.normal.xz,spread+across*(width*side*(1-t)))/max(.3,r.normal.y);
            float3 p=root+up*(height*t)+float3(spread.x,spreadY,spread.y)+float3(dir.x,0,dir.y)*(bend*height*t*t)+float3(.8,0,.4)*(wind*height*t*t)+float3(across.x,0,across.y)*(width*side);
            float birth=smoothstep(0,2,_Time.y-_CoverOrigin.w);
            float low=_CoverLod==0?0:_CoverLod==1?near:1-far;
            o.fade=float2(low,low+weight)*visibility*birth;
            o.position=p;o.positionCS=TransformWorldToHClip(p);o.normal=normalize(up+float3(across.x,.8,across.y)*.25);
            o.color=r.colorHeight.rgb*(.90+.20*r.shape.z)*lerp(.75,1.12,t);
            float2 cell=form==4||form>=6?float2(0,0):form==5?float2(.5,0):form==1||form==3?float2(.5,.5):float2(0,.5);
            float u=side*.5+.5;if(r.shape.w>.5)u=1-u;
            // A texel inset isolates atlas quadrants, including their mip chains.
            o.uv=cell+float2(.003+u*.494,.003+t*.494);return o;
        }
        void Fade(O i){float threshold=WorldGenDither(i.positionCS.xy);clip(threshold-i.fade.x);clip(i.fade.y-threshold);}
        half4 frag(O i):SV_Target
        {
            Fade(i);float4 foliage=SAMPLE_TEXTURE2D(_FoliageAtlas,sampler_FoliageAtlas,i.uv);clip(foliage.a-.35);
            float3 albedo=i.color;
            #ifndef UNITY_COLORSPACE_GAMMA
            albedo=SRGBToLinear(albedo);
            #endif
            // Use the bank's leaf detail without multiplying two dark green
            // albedos. Regional phenotype still controls the mean distant colour.
            float detail=clamp(dot(foliage.rgb,float3(.2126,.7152,.0722))/.20,.38,1.65);
            albedo*=detail;
            Light sun=GetMainLight(TransformWorldToShadowCoord(i.position));
            float diffuse=.48+.52*saturate(dot(i.normal,sun.direction));
            float transmission=pow(saturate(dot(-sun.direction,GetWorldSpaceNormalizeViewDir(i.position))),3)*.22;
            float3 color=albedo*(_AmbientTint.rgb+sun.color*(diffuse+transmission)*sun.shadowAttenuation*lerp(.025,1,_Daylight));
            color+=SecondaryCelestialLight(i.normal,albedo);
            return half4(ApplyPlanetAir(color,i.position,_WorldSpaceCameraPos,_FogColor.rgb,_FogDistance),1);
        }
        float depth(O i):SV_Target{Fade(i);clip(SAMPLE_TEXTURE2D(_FoliageAtlas,sampler_FoliageAtlas,i.uv).a-.35);return i.positionCS.z;}
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
