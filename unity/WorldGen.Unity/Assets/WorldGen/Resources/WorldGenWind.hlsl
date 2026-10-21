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
float3 WorldGenWindVelocity(float3 root)
{
    float3 d=WorldWindOffset(root);
    float q0=dot(d,_WorldWindWave0.xyz)+_WorldWindWave0.w;
    float q1=dot(d,_WorldWindWave1.xyz)+_WorldWindWave1.w;
    float gust=max(WorldWindPulse(q0,_WorldWindSlot0,0u,_WorldWindStats.x,_WorldWindStats.y),
        .7*WorldWindPulse(q1,_WorldWindSlot1,1u,_WorldWindStats.x*.75,_WorldWindStats.y*1.3));
    float fine=sin((dot(d,_WorldWindWave2.xyz)+_WorldWindWave2.w)*6.2831853);
    float flutter=sin((dot(d,_WorldWindWave3.xyz)+_WorldWindWave3.w)*6.2831853);
    float mean=length(_WorldWindMean.xyz);
    float3 dir=mean>.0001?_WorldWindMean.xyz/mean:float3(1,0,0);
    float speed=max(0,mean*(1+_WorldWindStats.z*.12*fine)+max(0,_WorldWindMean.w-mean)*gust);
    float across=speed*_WorldWindStats.z*(.09*fine+.035*flutter);
    return dir*speed+cross(dir,_WorldWindSphere.xyz)*across;
}
// Roots stay fixed; flexible shoots lean as a connected stem, not UV islands.
float3 WorldGenScatterWind(float3 p,float3 root,float weight,float flexibility,float herb)
{
    if(flexibility<=0||(herb>.5&&_WorldWindPlantsEnabled<.5))return p;
    float3 velocity=WorldGenWindVelocity(root);
    if(herb>.5)
    {
        velocity*=_WorldWindPlantsEnabled;
        float speed=length(velocity),height=max(0,p.y-root.y);
        float angle=flexibility*(speed/(speed+7))*weight;
        float3 dir=speed>.0001?velocity/speed:float3(0,0,0);
        return p+dir*(height*sin(angle))+float3(0,height*(cos(angle)-1),0);
    }
    return p+velocity*(flexibility*.20*weight*weight);
}
#endif
