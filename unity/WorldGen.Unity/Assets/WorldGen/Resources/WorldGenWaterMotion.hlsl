#ifndef WORLDGEN_WATER_MOTION_INCLUDED
#define WORLDGEN_WATER_MOTION_INCLUDED
float4 _WorldGenWaterWind;
float WaterSwell(float2 p,float depth,float ocean)
{
    float speed=length(_WorldGenWaterWind.xy);float2 dir=speed>.01?_WorldGenWaterWind.xy/speed:float2(.8,.6);
    float a=lerp(.035,.28,saturate(speed/14))*lerp(.2,1,ocean)*saturate(depth/2.5);
    float k=6.2831853/75,l=6.2831853/43;float2 other=float2(-dir.y*.6+dir.x*.8,dir.x*.6+dir.y*.8);
    return a*(sin(dot(p,dir)*k-_Time.y*sqrt(9.81*k))+.45*sin(dot(p,other)*l-_Time.y*sqrt(9.81*l)));
}
#endif
