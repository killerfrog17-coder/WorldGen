#ifndef WORLDGEN_LOD_FADE_INCLUDED
#define WORLDGEN_LOD_FADE_INCLUDED
// Stable screen-space thresholds: opaque complementary coverage, no blend sorting.
float WorldGenDither(float2 pixel)
{
    uint2 p=(uint2)pixel&7;
    uint value=(((p.x^p.y)&1)<<1)|(p.y&1);
    value=(value<<2)|((((p.x^p.y)>>1)&1)<<1)|((p.y>>1)&1);
    value=(value<<2)|((((p.x^p.y)>>2)&1)<<1)|((p.y>>2)&1);
    return (value+.5)/64;
}
float WorldGenFadeWeight(float2 timings)
{
    float a=timings.x<=0?1:smoothstep(0,3,_Time.y-timings.x);
    float b=timings.y<=0?1:1-smoothstep(0,3,_Time.y-timings.y);
    return min(a,b);
}
void WorldGenScatterFade(float2 pixel,float4 fade)
{
    float d=WorldGenDither(pixel);
    // Vegetation/props respond only to distance. Loading is not another fade.
    clip(d-fade.x);clip(fade.y-d);
}
#endif
