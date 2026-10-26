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
            #include "WorldGenFog.hlsl"
            TEXTURE2D(_Stars);SAMPLER(sampler_Stars);
            float4 _Zenith,_Horizon,_StarRight,_StarUp,_StarForward,_GalaxyPole,_LocalUp,_NightAirGlow;
            float _StarVisibility,_Daylight,_Twilight,_GalacticGlow,_StellarGlow,_FieldSeed,_AirDensity,_AerosolHaze;int _BodyCount;
            float4 _BodyDirections[8],_BodyColors[8],_BodyParameters[8],_BodyLightDirections[8];
            struct A { float3 vertex:POSITION; };
            struct O { float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0; };
            O vert(A a){O o;o.positionCS=TransformObjectToHClip(a.vertex);o.direction=a.vertex;return o;}
            float Hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
            float Noise(float3 p){float3 a=floor(p),t=frac(p);t=t*t*(3-2*t);return lerp(lerp(lerp(Hash(a),Hash(a+float3(1,0,0)),t.x),lerp(Hash(a+float3(0,1,0)),Hash(a+float3(1,1,0)),t.x),t.y),lerp(lerp(Hash(a+float3(0,0,1)),Hash(a+float3(1,0,1)),t.x),lerp(Hash(a+float3(0,1,1)),Hash(a+1),t.x),t.y),t.z);}
            float2 Craters(float3 n,float scale,float seed)
            {
                float3 p=n*scale+seed,cell=floor(p);float closest=100,second=100;
                [unroll]for(int x=-1;x<=1;x++)[unroll]for(int y=-1;y<=1;y++)[unroll]for(int z=-1;z<=1;z++)
                {float3 q=cell+float3(x,y,z),center=q+float3(Hash(q),Hash(q+17),Hash(q+53));float d=length(p-center);if(d<closest){second=closest;closest=d;}else second=min(second,d);}
                return float2(closest,second-closest);
            }
            float3 MoonAlbedo(float3 n,int kind,float seed)
            {
                float maria=Noise(n*4+seed),fine=Noise(n*29+seed);float2 crater=Craters(n,13,seed);
                float pit=lerp(.48,1,smoothstep(.09,.38,crater.x)),rim=.16*exp(-pow((crater.x-.43)*24,2));
                float material=(.7+.3*fine)*(pit+rim)*lerp(.46,1,smoothstep(.35,.55,maria));
                if(kind==2){material=(.65+.3*maria)*(.8+.2*pit)+rim*.3;return lerp(float3(material,material*.87,material*.73),float3(.85,.91,.98),smoothstep(.86,.98,abs(n.y)));}
                if(kind==3)return float3(.78,.9,1)*(.82+.15*fine)*(1-.34*exp(-crater.y*35))*(.92+.08*pit);
                if(kind==4)return float3(.75,.71,.67)*material*.65;
                return material.xxx;
            }
            half4 frag(O i):SV_Target
            {
                float3 d=normalize(i.direction),zenith=_Zenith.rgb,horizon=_Horizon.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                zenith=SRGBToLinear(zenith);horizon=SRGBToLinear(horizon);
                #endif
                float localHeight=dot(d,_LocalUp.xyz),up=saturate(localHeight);
                float3 color=WorldGenSkyAirColor(localHeight,zenith,horizon,_NightAirGlow.rgb,_AirDensity);
                float3 equatorial=float3(dot(d,_StarRight.xyz),dot(d,_StarUp.xyz),dot(d,_StarForward.xyz));
                float2 uv=float2(atan2(equatorial.x,equatorial.z)*.159154943+.5,asin(clamp(equatorial.y,-1,1))*.318309886+.5);
                float3 stars=SAMPLE_TEXTURE2D(_Stars,sampler_Stars,uv).rgb;
                float galLat=dot(equatorial,_GalaxyPole.xyz),band=exp(-pow(galLat*9,2));
                float clouds=Noise(equatorial*8+_FieldSeed)*.7+Noise(equatorial*23+_FieldSeed)*.3;
                float dust=lerp(.25,1,smoothstep(.35,.6,Noise(equatorial*31+_FieldSeed)));
                float core=pow(saturate(dot(equatorial,normalize(cross(_GalaxyPole.xyz,float3(1,0,0))))),12);
                float twinkle=1+.05*sin(_Time.y*1.4+dot(equatorial,float3(431,1703,997)));
                float stellarTransmission=exp(-_AirDensity*(.025+.035*_AerosolHaze)/max(.06,up));
                color+=(stars*1.5*twinkle+lerp(float3(.0018,.0026,.0045),float3(.009,.0065,.0035),core)*band*(.3+clouds)*dust*_GalacticGlow+float3(.3,.38,.55)*_StellarGlow*.12)*_StarVisibility*stellarTransmission;
                // Body order is sun, then far-to-near moons. Dark limbs hide the
                // background stars/sun instead of being additive glowing circles.
                for(int body=0;body<8;body++)
                {
                    if(body>=_BodyCount)break;
                    float3 direction=_BodyDirections[body].xyz;float mu=dot(d,direction),radius=_BodyParameters[body].x;
                    float edge=max(fwidth(mu),.00000003),disk=smoothstep(cos(radius)-edge,cos(radius)+edge,mu),horizon=1;
                    if(_BodyParameters[body].y<.5)
                    {float halo=(pow(saturate(mu),80)*.12+pow(saturate(mu),12)*pow(1-up,3)*_Twilight*.42)*_AirDensity;color+=_BodyColors[body].rgb*(disk*3+halo)*horizon;continue;}
                    if(disk<=0)continue;
                    float3 north=_StarUp.xyz,rx=normalize(cross(north,direction));float3 ry=normalize(cross(direction,rx));
                    float2 q=float2(dot(d,rx),dot(d,ry))/sin(radius);int kind=(int)_BodyParameters[body].y;float seed=_BodyParameters[body].z;
                    if(kind==4){float outline=.9+.06*sin(atan2(q.y,q.x)*7+seed);q/=float2(outline,.76);disk*=1-smoothstep(.97,1,dot(q,q));}
                    float z=sqrt(saturate(1-dot(q,q)));float3 n=normalize(q.x*rx+q.y*ry-z*direction),objectNormal=float3(q,z);
                    float lit=saturate(dot(n,_BodyLightDirections[body].xyz));float earthshine=.002*(1-_Daylight);
                    float3 albedo=MoonAlbedo(objectNormal,kind,seed)*_BodyColors[body].rgb;
                    float3 surface=albedo*(lit*.85+earthshine);
                    // A smooth highlight shoulder keeps bright ice reflective
                    // without clipping away its cracks and surface variation.
                    surface=surface/(1+surface);surface+=color*.48*_Daylight;
                    color=lerp(color,surface,disk*horizon);
                }
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
