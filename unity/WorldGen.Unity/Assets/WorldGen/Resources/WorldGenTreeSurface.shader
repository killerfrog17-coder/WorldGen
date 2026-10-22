Shader "WorldGen/TreeSurface2"
{
    Properties
    {
        _TreeLeaves("Baked shoots",2DArray)=""{} _TreeLeafNormals("Shoot normals",2DArray)=""{}
        _TreeColourViews("Tree albedo views",2DArray)=""{} _TreeNormalViews("Tree normal/depth views",2DArray)=""{}
        _TreeLeafShape("Leaf shape",Float)=0 _TreeBarkPattern("Bark",Float)=0
        _Impostor("Multi-view representation",Float)=0 _ImpostorBounds("Centre and radius",Vector)=(0,.5,0,.7)
        _Wind("Flexibility",Float)=.03 _PlantTraits("Plant traits",Vector)=(.03,.5,.7,1)
        _FogDistance("Fog distance",Float)=60000 _FogColor("Fog",Color)=(.57,.69,.76,1) _AmbientTint("Ambient",Color)=(.12,.14,.17,1)
        [PerRendererData] _Tint("Instance tint",Color)=(1,1,1,1)
        [PerRendererData] _LodTransition("Distance transition",Vector)=(0,1,0,0)
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "WorldGenAir.hlsl"
        #include "WorldGenLodFade.hlsl"
        #include "WorldGenWind.hlsl"
        #include "WorldGenTree.hlsl"
        TEXTURE2D_ARRAY(_TreeLeafNormals);SAMPLER(sampler_TreeLeafNormals);
        TEXTURE2D_ARRAY(_TreeColourViews);SAMPLER(sampler_TreeColourViews);
        TEXTURE2D_ARRAY(_TreeNormalViews);SAMPLER(sampler_TreeNormalViews);
        float4 _PlantTraits,_ImpostorBounds,_AmbientTint,_FogColor;
        float _TreeLeafShape,_TreeBarkPattern,_Impostor,_Wind,_FogDistance;
        float3 _LightDirection;
        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4,_Tint)
            UNITY_DEFINE_INSTANCED_PROP(float4,_LodTransition)
        UNITY_INSTANCING_BUFFER_END(Props)
        struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;float2 mask:TEXCOORD1;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
        struct O {float4 positionCS:SV_POSITION;float3 position:TEXCOORD0;float3 normal:TEXCOORD1;float4 tangent:TEXCOORD2;float2 uv:TEXCOORD3;float2 mask:TEXCOORD4;float4 color:COLOR;float3 local:TEXCOORD5;nointerpolation float3 direction:TEXCOORD6;UNITY_VERTEX_INPUT_INSTANCE_ID};
        float3 ViewDirection(int index)
        {float2 q=float2(index%4,index/4)/3.*2-1;return normalize(float3((q.x+q.y)*.5,1-max(abs(q.x),abs(q.y)),(q.x-q.y)*.5));}
        void ViewBasis(float3 d,out float3 r,out float3 u)
        {r=cross(float3(0,1,0),d);r=dot(r,r)<.0001?float3(1,0,0):normalize(r);u=normalize(cross(d,r));}
        O vert(A a)
        {
            O o=(O)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);
            float3 p=a.positionOS.xyz;
            if(_Impostor>.5)
            {
                float3 d=normalize(TransformWorldToObject(_WorldSpaceCameraPos)-_ImpostorBounds.xyz),r,u;ViewBasis(d,r,u);
                p=_ImpostorBounds.xyz+(r*(a.uv.x*2-1)+u*(a.uv.y*2-1))*_ImpostorBounds.w;
                o.direction=d;o.local=p;
            }
            float3 world=TransformObjectToWorld(p),root=TransformObjectToWorld(float3(0,0,0));
            if(_Impostor<.5)world=WorldGenTreeWind(world,root,a.mask,_Wind,_PlantTraits);
            o.position=world;o.positionCS=TransformWorldToHClip(world);o.normal=TransformObjectToWorldNormal(a.normalOS);
            o.tangent=float4(TransformObjectToWorldDir(a.tangentOS.xyz),a.tangentOS.w);o.uv=a.uv;o.mask=a.mask;o.color=a.color;return o;
        }
        void ViewSample(int index,float3 localPoint,float3 direction,out half4 colour,out half3 normal)
        {
            float3 d=ViewDirection(index),r,u;ViewBasis(d,r,u);
            float rayDot=max(.2,dot(direction,d));
            float3 projected=localPoint-direction*dot(localPoint,d)/rayDot;
            float2 uv=.5+float2(dot(projected,r),dot(projected,u))/(2*_ImpostorBounds.w);
            // Begin on this capture's centre plane, not the camera billboard's
            // plane. Empty pixels carry no valid depth: keep that initial ray
            // intersection rather than interpreting transparent black as -R.
            half4 coverage=SAMPLE_TEXTURE2D_ARRAY(_TreeColourViews,sampler_TreeColourViews,uv,index);
            half4 depth=SAMPLE_TEXTURE2D_ARRAY(_TreeNormalViews,sampler_TreeNormalViews,uv,index);
            float distance=(coverage.a>.2?(depth.a-.5)*2*_ImpostorBounds.w:0)/rayDot;
            uv+=float2(dot(direction,r),dot(direction,u))*distance/(2*_ImpostorBounds.w);
            colour=SAMPLE_TEXTURE2D_ARRAY(_TreeColourViews,sampler_TreeColourViews,uv,index);
            colour.a*=all(uv>=0)&&all(uv<=1)?1:0;
            normal=SAMPLE_TEXTURE2D_ARRAY(_TreeNormalViews,sampler_TreeNormalViews,uv,index).rgb*2-1;
        }
        void Surface(O i,out half3 albedo,out half3 normal,out half foliage)
        {
            if(_Impostor>.5)
            {
                float3 d=i.direction;d.y=max(0,d.y);d/=max(.0001,abs(d.x)+abs(d.y)+abs(d.z));
                float2 q=saturate(float2(d.x+d.z,d.x-d.z)*.5+.5)*3;
                int2 cell=min((int2)floor(q),int2(2,2));float2 f=q-cell;
                int a=cell.y*4+cell.x,b,c;float3 weights;
                if(f.x+f.y<1){b=a+1;c=a+4;weights=float3(1-f.x-f.y,f.x,f.y);}
                else{a+=5;b=a-1;c=a-4;weights=float3(f.x+f.y-1,1-f.x,1-f.y);}
                half4 ca,cb,cc;half3 na,nb,nc;float3 p=i.local-_ImpostorBounds.xyz;
                ViewSample(a,p,i.direction,ca,na);ViewSample(b,p,i.direction,cb,nb);ViewSample(c,p,i.direction,cc,nc);
                half3 w=weights*half3(ca.a,cb.a,cc.a);half alpha=w.x+w.y+w.z;clip(alpha-.4);
                albedo=(ca.rgb*w.x+cb.rgb*w.y+cc.rgb*w.z)/max(.001,alpha);
                normal=TransformObjectToWorldNormal(normalize(na*w.x+nb*w.y+nc*w.z));foliage=1;
            }
            else
            {
                foliage=step(.5,i.mask.y);albedo=SRGBToLinear(i.color.rgb);normal=normalize(i.normal);
                if(i.mask.y>1.5&&i.mask.y<2.5)
                {
                    float layer=_TreeLeafShape*4+round((i.mask.y-2)*10);
                    half4 tex=SAMPLE_TEXTURE2D_ARRAY(_TreeLeaves,sampler_TreeLeaves,i.uv,layer);clip(tex.a-.4);albedo*=tex.rgb;
                    half3 detail=SAMPLE_TEXTURE2D_ARRAY(_TreeLeafNormals,sampler_TreeLeafNormals,i.uv,layer).rgb*2-1;
                    half3 t=normalize(i.tangent.xyz);normal=normalize(normal+(t*detail.x+cross(normal,t)*i.tangent.w*detail.y)*.45);
                }
                albedo*=.72+.28*saturate(i.mask.x*1.7);
                if(foliage<.5)albedo*=.94+.06*sin(i.uv.x*(55+_TreeBarkPattern*13)+sin(i.uv.y*11)*.5);
            }
            albedo*=SRGBToLinear(UNITY_ACCESS_INSTANCED_PROP(Props,_Tint).rgb);
        }
        half4 frag(O i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(i);WorldGenScatterFade(i.positionCS.xy,UNITY_ACCESS_INSTANCED_PROP(Props,_LodTransition));
            half3 albedo,n;half foliage;Surface(i,albedo,n,foliage);
            // Crown-envelope normals describe a volume, not the arbitrary side
            // of a shoot card. Only actual blade/wood surfaces flip by face.
            if(_Impostor<.5&&i.mask.y<1.5)n*=IS_FRONT_VFACE(face,1,-1);
            Light sun=GetMainLight(TransformWorldToShadowCoord(i.position));
            half diffuse=saturate((dot(n,sun.direction)+foliage*.2)/(1+foliage*.2));
            half transmission=foliage*.14*pow(saturate(dot(-n,sun.direction)),2);
            half3 color=albedo*(_AmbientTint.rgb*1.4+sun.color*(diffuse+transmission)*sun.shadowAttenuation)+SecondaryCelestialLight(n,albedo);
            return half4(ApplyPlanetAir(color,i.position,_WorldSpaceCameraPos,SRGBToLinear(_FogColor.rgb),_FogDistance),1);
        }
        half4 depthFrag(O i):SV_Target
        {UNITY_SETUP_INSTANCE_ID(i);WorldGenScatterFade(i.positionCS.xy,UNITY_ACCESS_INSTANCED_PROP(Props,_LodTransition));half3 c,n;half f;Surface(i,c,n,f);return 0;}
        O shadowVert(A a)
        {
            O o=vert(a);o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.position,normalize(o.normal),_LightDirection));
            #if UNITY_REVERSED_Z
            o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
            #else
            o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
            #endif
            return o;
        }
        ENDHLSL
        Pass
        {
            Tags {"LightMode"="UniversalForward"} Cull Off
            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} Cull Off ColorMask 0 ZWrite On
            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex shadowVert
            #pragma fragment depthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} Cull Off ColorMask R ZWrite On
            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment depthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
