Shader "WorldGen/ForestPackets"
{
    Properties { [HideInInspector] _VegetationSceneEnabled("Shared camera scene",Float)=0

        _TreeColourViews("Actual-tree stand colours",2DArray)=""{} _TreeNormalViews("Stand normals and depth",2DArray)=""{}
        _ImpostorBounds("Object centre/radius",Vector)=(0,.48,0,1.5)
        [HideInInspector] _TreeEarlyAlpha("Conservative empty-ray rejection",Float)=1
        _AmbientTint("Ambient",Color)=(.12,.14,.17,1) _FogColor("Fog",Color)=(.57,.69,.76,1) _FogDistance("Fog distance",Float)=60000
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+2"}
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
            #include "WorldGenVegetationScene.hlsl"
            #include "WorldGenLodFade.hlsl"
            TEXTURE2D_ARRAY(_TreeColourViews);SAMPLER(sampler_TreeColourViews);
            TEXTURE2D_ARRAY(_TreeNormalViews);SAMPLER(sampler_TreeNormalViews);
            struct Root {float3 position;float width;float height;float yaw;float slopeX;float slopeZ;float3 tint;uint field;int cellX;int cellY;uint face;float padding;};
            StructuredBuffer<Root> _Roots;StructuredBuffer<uint> _Visible;
            float4 _ImpostorBounds,_AmbientTint,_FogColor,_ForestChart,_ForestRange;float _FogDistance,_TreeEarlyAlpha;
            struct O {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 local:TEXCOORD1;nointerpolation float3 direction:TEXCOORD2;nointerpolation uint root:TEXCOORD3;};
            float3 Rotate(float3 p,float angle){float s,c;sincos(angle,s,c);return float3(c*p.x+s*p.z,p.y,-s*p.x+c*p.z);}
            void Basis(float3 d,out float3 r,out float3 u){r=cross(float3(0,1,0),d);r=dot(r,r)<.0001?float3(1,0,0):normalize(r);u=normalize(cross(d,r));}
            float3 Direction(uint index){float2 q=float2(index%4u,index/4u)/3.*2-1;return normalize(float3((q.x+q.y)*.5,1-max(abs(q.x),abs(q.y)),(q.x-q.y)*.5));}
            O vert(uint vertexID:SV_VertexID,uint instanceID:SV_InstanceID)
            {
                O o;uint index=_Visible[instanceID];Root root=_Roots[index];float3 origin=root.position-_ForestChart.xyz;
                float3 scale=float3(root.width,root.height,root.width);
                float3 eye=_WorldSpaceCameraPos-origin;eye.y-=eye.x*root.slopeX+eye.z*root.slopeZ;eye=Rotate(eye,-root.yaw)/scale;
                float3 direction=normalize(eye-_ImpostorBounds.xyz),r,u;Basis(direction,r,u);
                const float2 corners[6]={float2(-1,-1),float2(-1,1),float2(1,-1),float2(1,-1),float2(-1,1),float2(1,1)};
                float2 uv=corners[vertexID%6u];float3 p=_ImpostorBounds.xyz+(r*uv.x+u*uv.y)*_ImpostorBounds.w;
                float3 relative=Rotate(p*scale,root.yaw);relative.y+=relative.x*root.slopeX+relative.z*root.slopeZ;
                o.world=origin+relative;o.positionCS=TransformWorldToHClip(o.world);o.local=p-_ImpostorBounds.xyz;o.direction=direction;o.root=index;return o;
            }
            half4 ViewCoverage(uint index,float3 localPoint,float3 view)
            {
                float3 d=Direction(index),r,u;Basis(d,r,u);float rayDot=max(.2,dot(view,d));float3 p=localPoint-view*dot(localPoint,d)/rayDot;
                float2 uv=.5+float2(dot(p,r),dot(p,u))/(2*_ImpostorBounds.w);return SAMPLE_TEXTURE2D_ARRAY(_TreeColourViews,sampler_TreeColourViews,uv,index);
            }
            void SampleView(uint index,float3 localPoint,float3 view,half4 initial,bool prefetched,out half4 color,out half3 normal)
            {
                float3 d=Direction(index),r,u;Basis(d,r,u);float rayDot=max(.2,dot(view,d));
                float3 p=localPoint-view*dot(localPoint,d)/rayDot;
                float2 uv=.5+float2(dot(p,r),dot(p,u))/(2*_ImpostorBounds.w);
                half4 alpha;if(prefetched)alpha=initial;else alpha=SAMPLE_TEXTURE2D_ARRAY(_TreeColourViews,sampler_TreeColourViews,uv,index);
                half4 depth=SAMPLE_TEXTURE2D_ARRAY(_TreeNormalViews,sampler_TreeNormalViews,uv,index);
                float t=(alpha.a>.2?(depth.a-.5)*2*_ImpostorBounds.w:0)/rayDot;
                uv+=float2(dot(view,r),dot(view,u))*t/(2*_ImpostorBounds.w);
                color=SAMPLE_TEXTURE2D_ARRAY(_TreeColourViews,sampler_TreeColourViews,uv,index);color.a*=all(uv>=0)&&all(uv<=1)?1:0;
                normal=SAMPLE_TEXTURE2D_ARRAY(_TreeNormalViews,sampler_TreeNormalViews,uv,index).rgb*2-1;
            }
            half4 frag(O i):SV_Target
            {
                Root root=_Roots[i.root];float distance=length(root.position-_ForestChart.xyz-_WorldSpaceCameraPos);
                // Complement the exact-tree interval [0,1-weight], rather than
                // drawing the same screen-door pixels twice and leaving holes.
                float weight=smoothstep(_ForestRange.x,_ForestRange.y,distance);clip(WorldGenDither(i.positionCS.xy)-(1-weight));
                float3 d=i.direction;d.y=max(0,d.y);d/=max(.0001,abs(d.x)+abs(d.y)+abs(d.z));
                float2 q=saturate(float2(d.x+d.z,d.x-d.z)*.5+.5)*3;uint2 cell=min((uint2)floor(q),uint2(2,2));float2 f=q-cell;
                uint a=cell.y*4u+cell.x,b,c;float3 w;
                if(f.x+f.y<1){b=a+1;c=a+4;w=float3(1-f.x-f.y,f.x,f.y);}else{a+=5;b=a-1;c=a-4;w=float3(f.x+f.y-1,1-f.x,1-f.y);}
                half4 ca,cb,cc;half3 na,nb,nc;
                if(_TreeEarlyAlpha>.5)
                {
                    half4 aa=ViewCoverage(a,i.local,i.direction),ab=ViewCoverage(b,i.local,i.direction),ac=ViewCoverage(c,i.local,i.direction);
                    if(WorldGenQuadAll(max(aa.a,max(ab.a,ac.a))<=.2?1:0)>.5)clip(-1);
                    SampleView(a,i.local,i.direction,aa,true,ca,na);SampleView(b,i.local,i.direction,ab,true,cb,nb);SampleView(c,i.local,i.direction,ac,true,cc,nc);
                }
                else {SampleView(a,i.local,i.direction,0,false,ca,na);SampleView(b,i.local,i.direction,0,false,cb,nb);SampleView(c,i.local,i.direction,0,false,cc,nc);}
                w*=float3(ca.a,cb.a,cc.a);float alpha=w.x+w.y+w.z;clip(alpha-.4);
                half3 albedo=(ca.rgb*w.x+cb.rgb*w.y+cc.rgb*w.z)/max(.001,alpha)*SRGBToLinear(root.tint);
                float3 normal=normalize(na*w.x+nb*w.y+nc*w.z);normal=Rotate(normal/float3(root.width,root.height,root.width),root.yaw);normal.x-=root.slopeX*normal.y;normal.z-=root.slopeZ*normal.y;normal=normalize(normal);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));half diffuse=saturate((dot(normal,sun.direction)+.2)/1.2),transmission=.14*pow(saturate(dot(-normal,sun.direction)),2);
                half3 colour=albedo*(VegetationAmbient(_AmbientTint).rgb*1.4+sun.color*(diffuse+transmission)*sun.shadowAttenuation)+SecondaryCelestialLight(normal,albedo);
                return half4(ApplyPlanetAir(colour,i.world,_WorldSpaceCameraPos,SRGBToLinear(VegetationFog(_FogColor).rgb),VegetationFogDistance(_FogDistance)),1);
            }
            ENDHLSL
        }
    }
}
