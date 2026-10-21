Shader "WorldGen/GrassCanopy"
{
    Properties
    {
        _FoliageAtlas("Distant foliage",2D)="white" {}
        _GrassNear("Blade range",Float)=45 _GrassRange("Sprite range",Float)=350 _GrassWind("Wind",Float)=1
        _FogDistance("Fog distance",Float)=200000 _Daylight("Daylight",Float)=1
        _FogColor("Sky colour",Color)=(.57,.69,.76,1) _AmbientTint("Ambient",Color)=(.12,.14,.17,1)
    }
    SubShader
    {
        Tags {"RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"} Cull Off ZWrite On
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
            struct O{float4 positionCS:SV_POSITION;float3 position:TEXCOORD0;float3 normal:TEXCOORD1;float4 color:COLOR;float2 uv:TEXCOORD2;float3 shadowPosition:TEXCOORD3;};
            O vert(uint vid:SV_VertexID,uint iid:SV_InstanceID)
            {
                Root r=_CoverRoots[_VisibleRoots[iid]];O o=(O)0;
                float3 root=r.positionForm.xyz+_CoverOrigin.xyz;float d=distance(root,_WorldSpaceCameraPos);
                float transition=smoothstep(.80*_GrassNear,_GrassNear,d);
                float retirement=1-smoothstep(max(_GrassNear,_GrassRange*.75),_GrassRange,d);
                float birth=smoothstep(0,2,_Time.y-_CoverOrigin.w);
                float coverage=saturate(r.normal.w/.45);float alpha=transition*retirement*birth*coverage;
                if(alpha<.0001){o.positionCS=float4(0,0,0,1);return o;}
                float residual=saturate(1-dot(r.grasses,float4(1,1,1,1))-dot(r.herbs,float4(1,1,1,1)));
                float upright=dot(r.grasses,float4(1,1,1,1))+r.herbs.x+r.herbs.y+residual;
                // Match the upright plants, not an average diluted by moss/lichen.
                // Mats stay on the ground; they must not become tall billboard grass.
                float height=min(3.2,r.colorHeight.w*(dot(r.grasses,float4(.58,.90,.40,.85))+dot(r.herbs,float4(.26,.46,0,0))+residual*.19)/max(.01,upright));
                alpha*=saturate(upright*2);
                int v=vid%6;float u=(v==0||v==2||v==3)?0:1,t=(v==2||v==3||v==5)?1:0;
                float layer=vid>=6?1:0;float3 p=root;
                float2 horizontal=root.xz-_WorldSpaceCameraPos.xz;float len=length(horizontal);
                float2 across=len>.01?float2(horizontal.y,-horizontal.x)/len:float2(1,0);
                float2 forward=len>.01?horizontal/len:float2(0,1);
                // Two upright, staggered camera-facing banks. No coplanar caps,
                // no transparent ground overlays, no flattening the distant canopy.
                float scale=max(.6,r.positionForm.w);
                float2 offset=across*((u-.5)*scale*1.65*sqrt(saturate(alpha))+(layer-.5)*scale*.35)
                    +forward*((layer-.5)*scale*.72);
                float baseY=-dot(r.normal.xz,offset)/max(.3,r.normal.y);
                float wind=sin(root.x*.13+root.z*.11+_Time.y*1.6)*.035*_GrassWind;
                p+=float3(offset.x,baseY+t*height*(.90+.20*r.shape.z),offset.y);
                p.xz+=float2(.8,.4)*wind*height*t*t;
                float grass=dot(r.grasses,float4(1,1,1,1));
                float2 cell=grass>.5?(r.grasses.y+r.grasses.w>r.grasses.x+r.grasses.z?float2(.5,.5):float2(0,.5)):(r.herbs.y>r.herbs.x?float2(.5,0):float2(0,0));
                float2 tex=float2(r.shape.w>.5?1-u:u,t);
                o.uv=cell+.003+tex*.494;
                o.position=p;o.positionCS=TransformWorldToHClip(p);o.normal=normalize(float3(0,.8,0)+r.normal.xyz*.25);
                o.shadowPosition=root;o.color=float4(r.colorHeight.rgb*.90,alpha);return o;
            }
            half4 frag(O i):SV_Target
            {
                float4 bank=SAMPLE_TEXTURE2D(_FoliageAtlas,sampler_FoliageAtlas,i.uv);
                // Coverage-preserving atlas mips, depth-writing alpha cutout.
                // Distance fades narrow the banks instead of noisy pixel dithering.
                clip(bank.a-.28);clip(i.color.a-.001);
                float3 albedo=i.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                albedo=SRGBToLinear(albedo);
                #endif
                float detail=clamp(dot(bank.rgb,float3(.2126,.7152,.0722))/.20,.6,1.4);
                albedo*=lerp(1,detail,.20);
                // Stable ground-anchored shadow sample: camera-facing card changes
                // must not move tree shadows between overlapping ground overlays.
                float3 color=WorldGenGrassLight(albedo,i.normal,i.shadowPosition,_AmbientTint.rgb,_Daylight);
                color+=SecondaryCelestialLight(i.normal,albedo);
                return half4(ApplyPlanetAir(color,i.position,_WorldSpaceCameraPos,_FogColor.rgb,_FogDistance),1);
            }
            ENDHLSL
        }
    }
}
