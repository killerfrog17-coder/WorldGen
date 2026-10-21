#ifndef WORLDGEN_WIND_INCLUDED
#define WORLDGEN_WIND_INCLUDED
// Main bend + procedural detail (Crysis); two advected gust layers (Tsushima).
// The desired/effective control split is documented by SpeedTree.
// These are adaptations for our procedural geometry, not those engines' code.
float4 _GrassWindDirection,_VegetationDirection,_VegetationControls,_VegetationPhases;
float4 _VegetationNoiseU,_VegetationNoiseV,_VegetationAnchor,_VegetationSphere;
float _WorldWindPlantsEnabled,_WeatherDrivenVegetationWind;
float WorldGenOriginalGrassWave(float3 anchor)
{
    return (sin(anchor.x*.13+anchor.z*.11+_Time.y*1.6)+.4*sin(anchor.x*.37-anchor.z*.18+_Time.y*2.7))*.14;
}
uint VegetationHash(uint x){x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;return x^(x>>16);}
float VegetationNoiseCorner(int2 p)
{
    uint h=VegetationHash((asuint(p.x)&127u)*73856093u^(asuint(p.y)&127u)*19349663u);
    return (h&0xffffffu)/16777216.;
}
float VegetationNoise(float2 q)
{
    int2 cell=(int2)floor(q);float2 f=frac(q);f=f*f*(3-2*f);
    return lerp(lerp(VegetationNoiseCorner(cell),VegetationNoiseCorner(cell+int2(1,0)),f.x),
        lerp(VegetationNoiseCorner(cell+int2(0,1)),VegetationNoiseCorner(cell+int2(1,1)),f.x),f.y);
}
float2 VegetationGustUV(float3 root)
{
    // Stable geographic difference, independent of floating-origin shifts.
    float3 d=float3(root.x-_VegetationAnchor.x,0,root.z-_VegetationAnchor.z);
    float r=max(1,_VegetationAnchor.w),term=2*r*dot(_VegetationSphere.xyz,d)+dot(d,d);
    float distance=sqrt(max(1,r*r+term));
    d=(d-_VegetationSphere.xyz*(term/(r+distance)))*(_VegetationSphere.w/distance);
    return float2(dot(d,_VegetationNoiseU.xyz)+_VegetationNoiseU.w,dot(d,_VegetationNoiseV.xyz)+_VegetationNoiseV.w);
}
float VegetationLocalStrength(float3 root)
{
    float2 uv=VegetationGustUV(root);
    float coarse=VegetationNoise(uv),detail=VegetationNoise(uv*5+float2(37,11));
    float threshold=lerp(.78,.30,saturate(_VegetationControls.z));
    float gust=smoothstep(threshold,min(.99,threshold+.20),coarse);
    // Broad positive gusts control the envelope. Small detail cannot become
    // high-frequency vertex shaking; it only mildly modulates that envelope.
    return clamp((_VegetationControls.x+_VegetationControls.y*gust)*(.96+.08*detail),0,3);
}
float3 WorldGenVegetationOffset(float3 root,float flexibility)
{
    if(_WeatherDrivenVegetationWind<.5)return _GrassWindDirection.xyz*WorldGenOriginalGrassWave(root);
    float strength=VegetationLocalStrength(root);
    float detail=(sin(root.x*.13+root.z*.11+_VegetationPhases.x)+.4*sin(root.x*.37-root.z*.18+_VegetationPhases.y))*.14;
    float mainBend=strength*strength/(strength*strength+2.25);
    return _VegetationDirection.xyz*(mainBend+detail*strength)*flexibility;
}
float3 WorldGenScatterWind(float3 p,float3 root,float weight,float flexibility,float herb,float4 traits)
{
    if(flexibility<=0)return p;
    if(herb>.5)
    {
        if(_WorldWindPlantsEnabled<.5)return p;
        float height=length(TransformObjectToWorld(float3(0,traits.w,0))-root);
        float3 bend=WorldGenVegetationOffset(root,_WeatherDrivenVegetationWind>.5?traits.x:1);
        float3 delta=normalize(float3(0,1,0)+bend)-float3(0,1,0);
        return p+delta*(height*weight*weight);
    }
    float wave=sin(_Time.y*1.4+p.x*.4+p.z*.27);
    if(_WeatherDrivenVegetationWind>.5)
    {
        // Existing tree/shrub proxy art shares the coarse gust, not a new rig.
        float strength=VegetationLocalStrength(root);
        return p+_VegetationDirection.xyz*(flexibility*weight*weight*(wave*strength+.35*strength*strength/(strength*strength+2.25)));
    }
    p.xz+=float2(wave,wave*.47)*flexibility*weight*weight;
    return p;
}
#endif
