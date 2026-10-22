#ifndef WORLDGEN_AIR_INCLUDED
#define WORLDGEN_AIR_INCLUDED
#ifndef WORLDGEN_AIR_CUSTOM_DATA
float4 _AirGeometry;float _AirObserverM,_AirHaze;
int _SecondaryLightCount;float4 _SecondaryDirections[4],_SecondaryColors[4];
#define WG_AIR_GEOMETRY _AirGeometry
#define WG_AIR_OBSERVER _AirObserverM
#define WG_AIR_HAZE _AirHaze
#define WG_SECONDARY_COUNT _SecondaryLightCount
#define WG_SECONDARY_DIRECTION(i) _SecondaryDirections[i]
#define WG_SECONDARY_COLOR(i) _SecondaryColors[i]
#endif
float3 SecondaryCelestialLight(float3 normal,float3 albedo)
{
    float3 result=0;[unroll]for(int i=0;i<4;i++){if(i>=WG_SECONDARY_COUNT)break;result+=albedo*WG_SECONDARY_COLOR(i).rgb*saturate(dot(normal,WG_SECONDARY_DIRECTION(i).xyz));}return result;
}
float AirColumn(float distance,float h0,float h1,float scale)
{
    float a=max(0,h0)/scale,t=(max(0,h1)-max(0,h0))/scale;
    float integral=abs(t)<.001?exp(-a)*(1-t*.5+t*t/6):(exp(-a)-exp(-max(0,h1)/scale))/t;
    return distance*integral;
}
float3 ApplyPlanetAir(float3 color,float3 position,float3 camera,float3 hazeColor,float distanceLimit)
{
    float2 horizontal=position.xz+WG_AIR_GEOMETRY.zw;float radial=WG_AIR_GEOMETRY.x+WG_AIR_GEOMETRY.y+position.y;
    float altitude=sqrt(radial*radial+dot(horizontal,horizontal))-WG_AIR_GEOMETRY.x;
    float distance=length(position-camera);
    float rayleigh=AirColumn(distance,WG_AIR_OBSERVER,altitude,8000),aerosol=AirColumn(distance,WG_AIR_OBSERVER,altitude,1200)*WG_AIR_HAZE;
    float3 transmission=exp(-rayleigh*float3(.0000058,.0000135,.0000331)-aerosol*.000012);
    float3 air=color*transmission+hazeColor*(1-transmission);
    return lerp(air,hazeColor,smoothstep(distanceLimit*.86,distanceLimit,distance));
}
#endif
