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
        TEXTURE2D(_WeatherConditions);SAMPLER(sampler_WeatherConditions);
        TEXTURE2D(_WeatherLayerField);SAMPLER(sampler_WeatherLayerField);
        TEXTURE2D(_PreviousWeatherConditions);SAMPLER(sampler_PreviousWeatherConditions);
        TEXTURE2D(_PreviousWeatherLayers);SAMPLER(sampler_PreviousWeatherLayers);
        TEXTURE2D_X_FLOAT(_WeatherDepthTexture);
        float4x4 _WeatherInvVP;
        float3 _WeatherCamera,_WeatherCenter,_WeatherEast,_WeatherNorth,_WeatherUp,_WeatherAnchor,_WeatherSun;
        float4 _WeatherLayers,_WeatherCover,_WeatherWind,_WeatherSunScreen,_WeatherEffects,_WeatherFieldRect,_WeatherShell,_WeatherWet,_WeatherFog;
        int _WeatherUseField,_WeatherCloudsEnabled;
        float4 _PreviousWeatherRect;float _WeatherFieldBlend;
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
        void WeatherAt(float3 p,out float4 weather,out float4 layers)
        {
            weather=float4(_WeatherCover.xy,_WeatherWet.xy);layers=float4(_WeatherLayers.xyz,_WeatherWet.w);
            if(_WeatherUseField>0)
            {
                float3 o=p-_WeatherCenter,geo=normalize(_WeatherEast*o.x+_WeatherUp*o.y+_WeatherNorth*o.z);
                float lon=atan2(geo.y,geo.x)*57.2957795,lat=asin(clamp(geo.z,-1,1))*57.2957795;
                float2 uv=(float2(fmod(lon-_WeatherFieldRect.x+720,360),lat-_WeatherFieldRect.y)+.5)/_WeatherFieldRect.zw;
                weather=SAMPLE_TEXTURE2D_LOD(_WeatherConditions,sampler_WeatherConditions,uv,0);
                layers=SAMPLE_TEXTURE2D_LOD(_WeatherLayerField,sampler_WeatherLayerField,uv,0);
                if(_WeatherFieldBlend<1)
                {
                    float2 oldUV=(float2(fmod(lon-_PreviousWeatherRect.x+720,360),lat-_PreviousWeatherRect.y)+.5)/_PreviousWeatherRect.zw;
                    weather=lerp(SAMPLE_TEXTURE2D_LOD(_PreviousWeatherConditions,sampler_PreviousWeatherConditions,oldUV,0),weather,_WeatherFieldBlend);
                    layers=lerp(SAMPLE_TEXTURE2D_LOD(_PreviousWeatherLayers,sampler_PreviousWeatherLayers,oldUV,0),layers,_WeatherFieldBlend);
                }
            }
            weather.x=max(weather.x,1-exp(-weather.z/.035));
            if(_WeatherCloudsEnabled==0)weather.xy=0;
        }
        float Density(float3 p,float height,out float4 weather,out float4 layers)
        {
            WeatherAt(p,weather,layers);
            float3 q=NoisePosition(p);
            // Shape in all three dimensions: a height-independent footprint would
            // extrude even rounded noise into flat-sided pillars.
            float2 macro=SAMPLE_TEXTURE3D_LOD(_WeatherNoise,sampler_WeatherNoise,q/8192,0).rg;
            float wet=(1-exp(-max(0,weather.z)/.08))*smoothstep(.1,.5,weather.x);
            float underside=layers.x+layers.y*(macro.r-.5)*.12;
            float thickness=layers.y*lerp(.70,1,macro.g);
            float h=(height-underside)/max(1,thickness);if(h<=0||h>=1)return 0;
            float2 shape=SAMPLE_TEXTURE3D_LOD(_WeatherNoise,sampler_WeatherNoise,q/4096+float3(.17,.31,.47),0).rg;
            float detail=SAMPLE_TEXTURE3D_LOD(_WeatherNoise,sampler_WeatherNoise,q/1024,0).g;
            float n=saturate(shape.g*1.25+shape.r*.20);
            float threshold=lerp(1,.50,weather.x);
            float body=smoothstep(threshold,threshold+.24,n)*smoothstep(.20,.65,macro.g);
            // Rain-bearing nimbus is part of this very volume, not a separate storm
            // billboard. Every wet column has an opaque interior above its rain shaft.
            body=max(body,wet*(.38+.62*shape.g));
            float profile=smoothstep(0,.10,h)*(1-smoothstep(.62,1,h));
            return max(0,body*profile-(1-detail)*.055*(1-wet))*saturate(weather.x*3.2);
        }
        half4 CloudFrag(Varyings input):SV_Target
        {
            float2 uv=input.texcoord;float3 ray=Ray(uv),o=_WeatherCamera-_WeatherCenter;float limit=min(120000,DepthDistance(uv));
            float bottom=_WeatherUseField>0?_WeatherShell.x:_WeatherLayers.x,top=_WeatherUseField>0?_WeatherShell.y:_WeatherLayers.x+_WeatherLayers.y;
            float2 interval=Shell(o,ray,bottom,top);interval.y=min(interval.y,limit);
            float3 color=0;float transmission=1;int steps=(int)_WeatherCover.z;
            if(interval.y>interval.x&&_WeatherCloudsEnabled>0&&(_WeatherUseField>0||_WeatherCover.x>.01))
            {
                float span=interval.y-interval.x;float jitter=.375+.25*frac(sin(dot(floor(uv*_ScreenParams.xy),float2(12.9898,78.233)))*43758.5453);
                for(int k=0;k<96;k++)
                {
                    if(k>=steps||transmission<.025)break;
                    // Concentrate samples near the observer; a uniform120km march
                    // could step completely over the cloud directly overhead.
                    float a=(float)k/steps,b=(float)(k+1)/steps;
                    float start=span*a*a,stepM=span*(b*b-a*a);
                    float t=interval.x+start+jitter*stepM;float3 p=_WeatherCamera+ray*t;float h=length(p-_WeatherCenter)-_WeatherRadius;
                    float4 weather,layers;float density=Density(p,h,weather,layers);if(density<.005)continue;
                    float dark=saturate(weather.x*weather.x*.2+(1-exp(-weather.z/3))*.65+weather.w*.15),heightFraction=saturate((h-layers.x)/layers.y);
                    float shade=exp(-density*layers.y*(1-heightFraction)*.0015/max(.2,abs(dot(_WeatherSun,normalize(p-_WeatherCenter)))));
                    float forward=pow(saturate(dot(ray,_WeatherSun)),12);
                    float3 light=_WeatherAmbient*lerp(.65,.27,dark)+_WeatherSunColor*_WeatherCover.w*(.12+.82*shade)*lerp(1,.35,dark)*(1+forward*.45);
                    float alpha=1-exp(-density*stepM*.0032);color+=transmission*alpha*light;transmission*=1-alpha;
                }
            }
            float highBase=_WeatherUseField>0?9500:_WeatherLayers.z;
            float2 high=Shell(o,ray,highBase,highBase+450);high.y=min(high.y,limit);
            if(high.y>high.x&&_WeatherCloudsEnabled>0)
            {
                float3 p=_WeatherCamera+ray*(high.x+high.y)*.5;float4 weather,layers;WeatherAt(p,weather,layers);
                float3 q=NoisePosition(p);q.xz=float2(q.x*.25+q.z*.25,q.z*2);
                float n=SAMPLE_TEXTURE3D_LOD(_WeatherNoise,sampler_WeatherNoise,q/16384,0).r;
                float detail=SAMPLE_TEXTURE3D_LOD(_WeatherNoise,sampler_WeatherNoise,(q+float3(800,1800,2100))/8192,0).r;
                float alpha=smoothstep(.58-weather.y*.15,.8,n*.75+detail*.25)*weather.y*.5;
                color+=transmission*alpha*(_WeatherAmbient+_WeatherSunColor*.6*_WeatherCover.w);transmission*=1-alpha;
            }
            // Spatial rain/snow curtains below their actual cloud columns, including distant storms.
            float optical=0,span=max(0,limit-300),curtainDark=0;
            if(_WeatherFog.z>0&&span>0)for(int j=0;j<6;j++)
            {
                float3 p=_WeatherCamera+ray*(300+span*(j+.5)/6);float4 weather,layers;WeatherAt(p,weather,layers);
                float altitude=length(p-_WeatherCenter)-_WeatherRadius;
                float below=(1-smoothstep(layers.x,layers.x+200,altitude))*smoothstep(.1,.5,weather.x),snow=min(weather.z,layers.w);
                curtainDark=max(curtainDark,saturate((1-exp(-weather.z/3))*.8+weather.w*.2)*below);
                optical+=(.0014*pow(max(0,weather.z-snow),.85)+.0045*pow(max(0,snow),.8))*below*span/6*_WeatherFog.z;
            }
            float rainTransmission=exp(-min(30,optical));float3 mist=_WeatherAmbient*.55+_WeatherSunColor*_WeatherCover.w*.28*lerp(1,.45,curtainDark);
            color=color*rainTransmission+mist*(1-rainTransmission);transmission*=rainTransmission;
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
            if(_WeatherDebugDepth==1)return half4(saturate(DepthDistance(uv)/20000).xxx,1);
            float2 pixel=1.0/float2(_ScreenParams.x/4,_ScreenParams.y/4);
            float4 cloud=SAMPLE_TEXTURE2D(_WeatherCloudTexture,sampler_WeatherCloudTexture,uv)*.4;
            cloud+=(SAMPLE_TEXTURE2D(_WeatherCloudTexture,sampler_WeatherCloudTexture,uv+float2(pixel.x,0))+SAMPLE_TEXTURE2D(_WeatherCloudTexture,sampler_WeatherCloudTexture,uv-float2(pixel.x,0))+SAMPLE_TEXTURE2D(_WeatherCloudTexture,sampler_WeatherCloudTexture,uv+float2(0,pixel.y))+SAMPLE_TEXTURE2D(_WeatherCloudTexture,sampler_WeatherCloudTexture,uv-float2(0,pixel.y)))*.15;
            if(_WeatherDebugDepth==2)return half4(cloud.aaa,1);
            if(DepthDistance(uv)<300)cloud=0;scene=scene*(1-cloud.a)+cloud.rgb;
            // Full-resolution near rain and shallow ground fog avoid low-resolution foreground halos.
            if(_WeatherFog.z>0)
            {
                float3 ray=Ray(uv),o=_WeatherCamera-_WeatherCenter;float distance=min(120000,DepthDistance(uv));
                float h0=max(0,length(o)-_WeatherRadius-_WeatherFog.y),h1=max(0,length(o+ray*distance)-_WeatherRadius-_WeatherFog.y);
                float dh=h1-h0,integral=abs(dh)<.01?exp(-h0/80):80*(exp(-h0/80)-exp(-h1/80))/dh;
                float nearRain=(.0014*pow(max(0,_WeatherWet.z),.85)+.0045*pow(max(0,_WeatherWet.w),.8))*min(distance,300)*(1-smoothstep(_WeatherLayers.x,_WeatherLayers.x+200,length(o)-_WeatherRadius))*smoothstep(.1,.5,_WeatherCover.x);
                float optical=(_WeatherFog.x*.018*max(0,integral)*distance+nearRain)*_WeatherFog.z;
                float dark=saturate((1-exp(-_WeatherWet.x/3))*.8+_WeatherWet.y*.2);
                float3 mist=_WeatherAmbient*.55+_WeatherSunColor*_WeatherCover.w*.28*lerp(1,.45,dark);scene=lerp(mist,scene,exp(-min(30,optical)));
            }
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
