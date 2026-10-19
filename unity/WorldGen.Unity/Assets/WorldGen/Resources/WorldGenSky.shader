Shader "WorldGen/PlayableSky"
{
    Properties { _Stars("Stars",2D)="black"{} }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_Stars);SAMPLER(sampler_Stars);
            float4 _Zenith,_Horizon,_StarRight,_StarUp,_StarForward;
            float _StarVisibility,_Daylight,_Twilight;int _BodyCount;
            float4 _BodyDirections[8],_BodyColors[8],_BodyParameters[8];
            struct A { float3 vertex:POSITION; };
            struct O { float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0; };
            O vert(A a){O o;o.positionCS=TransformObjectToHClip(a.vertex);o.direction=a.vertex;return o;}
            half4 frag(O i):SV_Target
            {
                float3 d=normalize(i.direction),zenith=_Zenith.rgb,horizon=_Horizon.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                zenith=SRGBToLinear(zenith);horizon=SRGBToLinear(horizon);
                #endif
                float up=saturate(d.y),vertical=pow(up,.42);
                float3 color=lerp(horizon,zenith,vertical);
                if(d.y<0)color=lerp(horizon*.72,zenith*.15,saturate(-d.y*3));
                for(int body=0;body<8;body++)
                {
                    if(body>=_BodyCount)break;
                    float mu=dot(d,_BodyDirections[body].xyz),radius=_BodyParameters[body].x;
                    float edge=max(fwidth(mu),.0000003);
                    float disk=smoothstep(cos(radius)-edge,cos(radius)+edge,mu);
                    float horizonMask=smoothstep(-.015,.01,d.y);
                    float halo=pow(saturate(mu),80)*.12+pow(saturate(mu),12)*pow(1-up,3)*_Twilight*.42;
                    color+=_BodyColors[body].rgb*(disk*3+halo)*horizonMask*_BodyParameters[body].y;
                }
                float3 equatorial=float3(dot(d,_StarRight.xyz),dot(d,_StarUp.xyz),dot(d,_StarForward.xyz));
                float2 uv=float2(atan2(equatorial.x,equatorial.z)*.159154943+.5,asin(clamp(equatorial.y,-1,1))*.318309886+.5);
                float3 stars=SAMPLE_TEXTURE2D(_Stars,sampler_Stars,uv).rgb;
                float band=exp(-pow(dot(equatorial,normalize(float3(.47,.79,.39)))*9,2));
                float twinkle=1+.05*sin(_Time.y*1.4+dot(equatorial,float3(431,1703,997)));
                color+=(stars*1.5*twinkle+float3(.0018,.0026,.0045)*band)*_StarVisibility*smoothstep(0,.08,d.y);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
