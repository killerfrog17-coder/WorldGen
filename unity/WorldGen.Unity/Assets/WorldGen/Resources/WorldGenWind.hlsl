#ifndef WORLDGEN_WIND_INCLUDED
#define WORLDGEN_WIND_INCLUDED
// Bound from absolute world time and double-precision geographic phases,
// never Unity _Time or an observer/chunk random seed. See Core.WeatherWind.
float4 _WorldWindMean,_WorldWindStats,_WorldWindAnchor,_WorldWindSphere;
float4 _WorldWindWave0,_WorldWindWave1,_WorldWindWave2,_WorldWindWave3;
uint _WorldWindSeed,_WorldWindSlot0,_WorldWindSlot1;
float _WorldWindPlantsEnabled;
uint WindMix(uint x){x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;return x^(x>>16);}
float WindUnit(uint x){return (WindMix(x)&0xffffffu)/16777216.;}
float3 WorldWindOffset(float3 p)
{
    // Stable difference between normalized planet positions. Ignore altitude:
    // nearby leaves and their roots sample the same ground-addressed gust.
    float3 d=float3(p.x-_WorldWindAnchor.x,0,p.z-_WorldWindAnchor.z);
    float r=max(1,_WorldWindAnchor.w),term=2*r*dot(_WorldWindSphere.xyz,d)+dot(d,d);
    float length=sqrt(max(1,r*r+term));
    return (d-_WorldWindSphere.xyz*(term/(r+length)))*(_WorldWindSphere.w/length);
}
float WorldWindPulse(float q,uint origin,uint lane,float chance,float duration)
{
    float cell=floor(q),f=q-cell;
    uint h=WindMix((origin+(uint)(int)cell)^_WorldWindSeed^((lane+1u)*0x651eddu));
    float active=smoothstep(WindUnit(h)-.04,WindUnit(h)+.04,chance);
    float start=.10+.24*WindUnit(h+1u);
    float width=min(.45,duration/(lane==0u?64:109))*(.8+.4*WindUnit(h+2u));
    float u=saturate((f-start)/max(.03,width));
    return 16*u*u*(1-u)*(1-u)*active*(.7+.3*WindUnit(h+3u));
}
float3 WorldGenWindAtOffset(float3 d,float age,float4 meanWind,float4 stats)
{
    float q0=dot(d,_WorldWindWave0.xyz)+_WorldWindWave0.w+age/64;
    float q1=dot(d,_WorldWindWave1.xyz)+_WorldWindWave1.w+age/109;
    float gust=max(WorldWindPulse(q0,_WorldWindSlot0,0u,stats.x,stats.y),
        .7*WorldWindPulse(q1,_WorldWindSlot1,1u,stats.x*.75,stats.y*1.3));
    float fine=sin((dot(d,_WorldWindWave2.xyz)+_WorldWindWave2.w+age/3.7)*6.2831853);
    float flutter=sin((dot(d,_WorldWindWave3.xyz)+_WorldWindWave3.w+age/2.1)*6.2831853);
    float mean=length(meanWind.xyz);
    float3 dir=mean>.0001?meanWind.xyz/mean:float3(1,0,0);
    float speed=max(0,mean*(1+stats.z*.12*fine)+max(0,meanWind.w-mean)*gust);
    float across=speed*stats.z*(.09*fine+.035*flutter);
    return dir*speed+cross(dir,_WorldWindSphere.xyz)*across;
}
float3 WorldGenWindVelocity(float3 root){return WorldGenWindAtOffset(WorldWindOffset(root),0,_WorldWindMean,_WorldWindStats);}
float2 WorldGenWindTarget(float3 velocity)
{
    float speed=length(velocity.xz);
    return speed>.0001?velocity.xz/speed*(1.20*speed*speed/(speed*speed+64)):float2(0,0);
}
#ifndef WORLDGEN_WIND_COMPUTE
Texture2DArray<float2> _PlantWindPrevious,_PlantWindNext;
SamplerState sampler_PlantWindPrevious,sampler_PlantWindNext;
float4 _PlantWindMap;
float _PlantWindBlend,_PlantWindReady;
float2 WorldGenPlantMotion(float3 root,float response,float damping)
{
    if(_PlantWindReady<.5)return WorldGenWindTarget(WorldGenWindVelocity(root));
    float2 uv=(root.xz-_PlantWindMap.xy)*_PlantWindMap.z;
    float2 modes[4];
    [unroll]for(int mode=0;mode<4;mode++)
    {
        float2 a=_PlantWindPrevious.SampleLevel(sampler_PlantWindPrevious,float3(uv,mode),0);
        float2 b=_PlantWindNext.SampleLevel(sampler_PlantWindNext,float3(uv,mode),0);
        modes[mode]=lerp(a,b,_PlantWindBlend);
    }
    return lerp(lerp(modes[0],modes[1],response),lerp(modes[2],modes[3],response),damping);
}
float3 WorldGenGrassTraits(int form,float height,float width,float3 color)
{
    float f=form==2?.72:form==3?.78:form==5?1.05:.95;
    float gene=.5+.5*sin(dot(color,float3(2.3,4.1,1.7))*11);
    return float3(clamp(f*(.22+.95*height/(height+.45))/(.8+.2*width),.15,1.2),
        saturate(.12+.85/(1+height*2)+.10*(width-1)),.15+.7*gene);
}
// Roots stay fixed; flexible shoots lean as a connected stem, not UV islands.
float3 WorldGenScatterWind(float3 p,float3 root,float weight,float flexibility,float herb,float4 traits)
{
    if(flexibility<=0||(herb>.5&&_WorldWindPlantsEnabled<.5))return p;
    if(herb>.5)
    {
        float stature=length(TransformObjectToWorld(float3(0,traits.w,0))-root);
        float leverage=.35+.65*saturate(stature/.8);
        float response=saturate(traits.y+.25*(.5-stature));
        float2 bend=WorldGenPlantMotion(root,response,traits.z)*traits.x*leverage;
        float size=length(bend),height=max(0,p.y-root.y);
        float angle=min(1.35,size)*weight;
        float2 dir=size>.0001?bend/size:float2(0,0);
        return p+float3(dir.x*(height*sin(angle)),height*(cos(angle)-1),dir.y*(height*sin(angle)));
    }
    return p+WorldGenWindVelocity(root)*(flexibility*.20*weight*weight);
}
#endif
#endif
