Shader "WorldGen/GrassCanopy"
{
    Properties
    {
        _FoliageAtlas("Distant foliage",2D)="white" {}
        _GrassNear("Blade range",Float)=45 _GrassRange("Canopy range",Float)=120 _GrassWind("Wind",Float)=1
        _FogDistance("Fog distance",Float)=200000 _Daylight("Daylight",Float)=1
        _FogColor("Sky colour",Color)=(.57,.69,.76,1) _AmbientTint("Ambient",Color)=(.12,.14,.17,1)
    }
    SubShader
    {
        Tags {"RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "WorldGenAir.hlsl"
            #include "WorldGenGrassLight.hlsl"
            struct Root{float4 positionForm;float4 colorHeight;float4 shape;float4 normal;float4 grasses;float4 herbs;};
            StructuredBuffer<Root> _CoverRoots;StructuredBuffer<uint> _VisibleRoots;
            TEXTURE2D(_FoliageAtlas);SAMPLER(sampler_FoliageAtlas);
            float4 _CoverOrigin,_FogColor,_AmbientTint;float _FogDistance,_Daylight,_GrassNear,_GrassRange,_GrassWind;
            struct O{float4 positionCS:SV_POSITION;float3 position:TEXCOORD0;float3 normal:TEXCOORD1;float4 color:COLOR;float2 uv:TEXCOORD2;float cap:TEXCOORD3;};
            O vert(uint vid:SV_VertexID,uint iid:SV_InstanceID)
            {
                Root r=_CoverRoots[_VisibleRoots[iid]];O o=(O)0;
                float3 root=r.positionForm.xyz+_CoverOrigin.xyz;float d=distance(root,_WorldSpaceCameraPos);
                float transition=smoothstep(.80*_GrassNear,_GrassNear,d);
                float retirement=1-smoothstep(max(_GrassNear,_GrassRange*.60),_GrassRange,d);
                float birth=smoothstep(0,2,_Time.y-_CoverOrigin.w);
                float coverage=saturate(r.normal.w/.45);float alpha=transition*retirement*birth*coverage;
                if(alpha<.0001){o.positionCS=float4(0,0,0,1);return o;}
                float residual=saturate(1-dot(r.grasses,float4(1,1,1,1))-dot(r.herbs,float4(1,1,1,1)));
                float height=min(3.2,r.colorHeight.w*(dot(r.grasses,float4(.58,.90,.40,.85))+dot(r.herbs,float4(.26,.46,.045,.018))+residual*.19));
                // Settle the aggregate height into the matching far-ground surface.
                float volume=1-smoothstep(max(_GrassNear,_GrassRange*.55),_GrassRange,d);
                int v=vid%6;float u=(v==0||v==2||v==3)?0:1,t=(v==2||v==3||v==5)?1:0;
                bool cap=vid>=6;float3 p=root;
                float2 horizontal=root.xz-_WorldSpaceCameraPos.xz;float len=length(horizontal);
                float2 across=len>.01?float2(horizontal.y,-horizontal.x)/len:float2(1,0);
                float2 offset=cap?(float2(u,t)-.5)*.62:across*((u-.5)*.96);
                float baseY=-dot(r.normal.xz,offset)/max(.3,r.normal.y);
                float wind=sin(root.x*.13+root.z*.11+_Time.y*1.6)*.035*_GrassWind;
                p+=float3(offset.x,baseY+(cap?.55:t)*height*volume,offset.y);
                if(!cap)p.xz+=float2(.8,.4)*wind*height*t*t;
                float grass=dot(r.grasses,float4(1,1,1,1));
                float2 cell=grass>.5?(r.grasses.y+r.grasses.w>r.grasses.x+r.grasses.z?float2(.5,.5):float2(0,.5)):(r.herbs.y>r.herbs.x?float2(.5,0):float2(0,0));
                float2 tex=cap?float2(u,.08+t*.25):float2(r.shape.w>.5?1-u:u,t);
                o.uv=cell+.003+tex*.494;o.cap=cap?1:0;
                o.position=p;o.positionCS=TransformWorldToHClip(p);o.normal=normalize(float3(0,.8,0)+r.normal.xyz*.25);
                o.color=float4(r.colorHeight.rgb*.90,alpha);return o;
            }
            half4 frag(O i):SV_Target
            {
                float4 bank=SAMPLE_TEXTURE2D(_FoliageAtlas,sampler_FoliageAtlas,i.uv);
                // Smooth opacity, not screen-door speckling. The dense cap
                // represents average canopy cover; upright fringe gives silhouette.
                float alpha=i.color.a*lerp(bank.a,1,i.cap);clip(alpha-.003);
                float3 albedo=i.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                albedo=SRGBToLinear(albedo);
                #endif
                float detail=clamp(dot(bank.rgb,float3(.2126,.7152,.0722))/.20,.6,1.4);
                albedo*=lerp(1,detail,.20);
                float3 color=WorldGenGrassLight(albedo,i.normal,i.position,_AmbientTint.rgb,_Daylight);
                color+=SecondaryCelestialLight(i.normal,albedo);
                return half4(ApplyPlanetAir(color,i.position,_WorldSpaceCameraPos,_FogColor.rgb,_FogDistance),alpha);
            }
            ENDHLSL
        }
    }
}
