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
#if SHADER_TARGET >= 45
// Exact 2x2 all-lane reduction. A pixel-only alpha test is NOT conservative:
// a neighbouring parallax offset can select a different coverage-preserving
// mip even when this pixel's offset is zero. Require the entire quad to have
// zero offsets, so initial and final texture gradients agree as well.
float WorldGenQuadAll(float value)
{
    float row=value*(1-saturate(abs(ddx_fine(value))));
    return row*(1-saturate(abs(ddy_fine(row))));
}
#endif
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
