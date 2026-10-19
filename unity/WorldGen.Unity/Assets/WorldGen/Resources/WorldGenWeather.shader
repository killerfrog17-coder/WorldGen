Shader "WorldGen/Weather"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        TEXTURE3D(_WeatherNoise);SAMPLER(sampler_WeatherNoise);
        TEXTURE2D(_WeatherCloudTexture);SAMPLER(sampler_WeatherCloudTexture);
        TEXTURE2D_X_FLOAT(_WeatherDepthTexture);
        float4x4 _WeatherInvVP;
        float3 _WeatherCamera,_WeatherCenter,_WeatherEast,_WeatherNorth,_WeatherUp,_WeatherAnchor,_WeatherSun;
        float4 _WeatherLayers,_WeatherCover,_WeatherWind,_WeatherSunScreen,_WeatherEffects;
        float3 _WeatherSunColor,_WeatherAmbient;float _WeatherRadius;int _WeatherDebugDepth;
        float DepthDistance(float2 uv)
        {
            float raw=SAMPLE_TEXTURE2D_X(_WeatherDepthTexture,sampler_PointClamp,uv).r;
            #if UNITY_REVERSED_Z
            if(raw<=.00000001)return 200000;
            #else
            if(raw>=.99999999)return 200000;
            #endif
            return LinearEyeDepth(raw,_ZBufferParams);
        }
        float3 Ray(float2 uv)
        {
            #if UNITY_REVERSED_Z
            float depth=.0001;
            #else
            float depth=.9999;
            #endif
            return normalize(ComputeWorldSpacePosition(uv,depth,_WeatherInvVP)-_WeatherCamera);
        }
        float2 Sphere(float3 o,float3 d,float radius)
        {float b=dot(o,d),c=dot(o,o)-radius*radius,disc=b*b-c;if(disc<0)return float2(1,-1);float r=sqrt(disc);return float2(-b-r,-b+r);}
        float2 Shell(float3 o,float3 d,float bottom,float top)
        {
            float2 outer=Sphere(o,d,_WeatherRadius+top),inner=Sphere(o,d,_WeatherRadius+bottom);float a=max(0,outer.x),b=outer.y;
            if(inner.y>inner.x){if(inner.x>a)b=min(b,inner.x);else if(inner.y>a)a=inner.y;}return float2(a,b);
        }
        float3 NoisePosition(float3 p)
        {return _WeatherAnchor+_WeatherEast*(p.x-_WeatherWind.x)+_WeatherUp*p.y+_WeatherNorth*(p.z-_WeatherWind.y);}
        float Density(float3 p,float height)
        {
            float h=saturate((height-_WeatherLayers.x)/_WeatherLayers.y);float3 q=NoisePosition(p);
            float2 shapeNoise=SAMPLE_TEXTURE3D_LOD(_WeatherNoise,sampler_WeatherNoise,q/8192,0).rg;
            float broad=shapeNoise.r;
            float detail=SAMPLE_TEXTURE3D_LOD(_WeatherNoise,sampler_WeatherNoise,q/2048,0).r;
            float n=saturate((broad-.5)*1.8+.5+(shapeNoise.g-.45)*.45),threshold=lerp(.83,.22,_WeatherCover.x);
            // Full synoptic cover must actually close the deck, not merely lower a noise threshold.
            float deck=smoothstep(.72,.96,_WeatherCover.x);
            float body=lerp(smoothstep(threshold,threshold+.2,n),.8+.2*n,deck),profile=smoothstep(0,.12,h)*(1-smoothstep(.38,.95,h));
            return max(0,body*profile-(1-detail)*lerp(.18,.02,deck))*saturate(_WeatherCover.x*2.8);
        }
        half4 CloudFrag(Varyings input):SV_Target
        {
            if(max(_WeatherCover.x,_WeatherCover.y)<.01)return 0;
            float2 uv=input.texcoord;float3 ray=Ray(uv),o=_WeatherCamera-_WeatherCenter;float limit=min(120000,DepthDistance(uv));
            float2 interval=Shell(o,ray,_WeatherLayers.x,_WeatherLayers.x+_WeatherLayers.y);interval.y=min(interval.y,limit);
            float3 color=0;float transmission=1;int steps=(int)_WeatherCover.z;
            if(interval.y>interval.x&&_WeatherCover.x>.01)
            {
                float stepM=(interval.y-interval.x)/steps;float jitter=frac(sin(dot(floor(uv*_ScreenParams.xy),float2(12.9898,78.233)))*43758.5453);
                for(int k=0;k<64;k++)
                {
                    if(k>=steps||transmission<.025)break;float t=interval.x+(k+jitter)*stepM;float3 p=_WeatherCamera+ray*t;float h=length(p-_WeatherCenter)-_WeatherRadius;
                    float density=Density(p,h);if(density<.005)continue;
                    float sunDensity=Density(p+_WeatherSun*220,h+_WeatherSun.y*220),shade=exp(-sunDensity*2.2);
                    float forward=pow(saturate(dot(ray,_WeatherSun)),12);float3 light=_WeatherAmbient*.65+_WeatherSunColor*_WeatherCover.w*(.22+.72*shade)*(1+forward*.45);
                    float alpha=1-exp(-density*stepM*.0032);color+=transmission*alpha*light;transmission*=1-alpha;
                }
            }
            float2 high=Shell(o,ray,_WeatherLayers.z,_WeatherLayers.z+_WeatherLayers.w);high.y=min(high.y,limit);
            if(high.y>high.x&&_WeatherCover.y>.01)
            {
                float3 p=_WeatherCamera+ray*(high.x+high.y)*.5,q=NoisePosition(p);q.xz=float2(q.x*.25+q.z*.25,q.z*2);
                float n=SAMPLE_TEXTURE3D_LOD(_WeatherNoise,sampler_WeatherNoise,q/16384,0).r;
                float detail=SAMPLE_TEXTURE3D_LOD(_WeatherNoise,sampler_WeatherNoise,(q+float3(800,1800,2100))/8192,0).r;
                float alpha=smoothstep(.58-_WeatherCover.y*.15,.8,n*.75+detail*.25)*_WeatherCover.y*.5;
                color+=transmission*alpha*(_WeatherAmbient+_WeatherSunColor*.6*_WeatherCover.w);transmission*=1-alpha;
            }
            // Premultiplied scatter plus transmittance, composited before particles.
            return half4(color,1-transmission);
        }
        float OpenSky(float2 uv)
        {if(any(uv<0)||any(uv>1))return 0;
            #if UNITY_REVERSED_Z
            return 1-step(.00000001,SAMPLE_TEXTURE2D_X(_WeatherDepthTexture,sampler_PointClamp,uv).r);
            #else
            return step(.99999999,SAMPLE_TEXTURE2D_X(_WeatherDepthTexture,sampler_PointClamp,uv).r);
            #endif
        }
        half4 CompositeFrag(Varyings input):SV_Target
        {
            float2 uv=input.texcoord;float3 scene=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv).rgb;
            if(_WeatherDebugDepth>0)return half4(saturate(DepthDistance(uv)/20000).xxx,1);
            float2 pixel=1.0/float2(_ScreenParams.x/4,_ScreenParams.y/4);
            float4 cloud=SAMPLE_TEXTURE2D(_WeatherCloudTexture,sampler_WeatherCloudTexture,uv)*.4;
            cloud+=(SAMPLE_TEXTURE2D(_WeatherCloudTexture,sampler_WeatherCloudTexture,uv+float2(pixel.x,0))+SAMPLE_TEXTURE2D(_WeatherCloudTexture,sampler_WeatherCloudTexture,uv-float2(pixel.x,0))+SAMPLE_TEXTURE2D(_WeatherCloudTexture,sampler_WeatherCloudTexture,uv+float2(0,pixel.y))+SAMPLE_TEXTURE2D(_WeatherCloudTexture,sampler_WeatherCloudTexture,uv-float2(0,pixel.y)))*.15;
            if(DepthDistance(uv)<300)cloud=0;scene=scene*(1-cloud.a)+cloud.rgb;
            float2 sun=_WeatherSunScreen.xy,delta=(uv-sun)*float2(_ScreenParams.x/_ScreenParams.y,1);
            float sourceOpen=OpenSky(sun)*(1-SAMPLE_TEXTURE2D(_WeatherCloudTexture,sampler_WeatherCloudTexture,saturate(sun)).a)*_WeatherSunScreen.z*_WeatherSunScreen.w;
            if(sourceOpen>.001&&max(_WeatherEffects.x,_WeatherEffects.y)>.001)
            {
                float d=length(delta);float corona=(exp(-d*28)*.08+exp(-d*7)*.008)*_WeatherEffects.x;
                float rays=0;if(_WeatherEffects.y>.001)for(int k=1;k<=8;k++){float2 p=lerp(uv,sun,k/9.0);rays+=OpenSky(p)*(1-SAMPLE_TEXTURE2D(_WeatherCloudTexture,sampler_WeatherCloudTexture,p).a);}
                scene+=_WeatherSunColor*sourceOpen*(corona+rays*.0018*_WeatherEffects.y*exp(-d*2));
            }
            scene*= _WeatherEffects.z;scene=lerp(scene,1,saturate((_WeatherEffects.z-1)*.12));scene+=_WeatherEffects.w*.35;
            return half4(scene,1);
        }
        ENDHLSL
        Pass
        {
            Name "Cloud volume"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CloudFrag
            ENDHLSL
        }
        Pass
        {
            Name "Weather composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CompositeFrag
            ENDHLSL
        }
    }
}
