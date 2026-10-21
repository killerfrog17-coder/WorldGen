#ifndef WORLDGEN_WIND_INCLUDED
#define WORLDGEN_WIND_INCLUDED
// Literal pre-weather grass scalar/time/spatial pattern. Weather rotates only
// the old (.8,0,.4) axis, preserving its magnitude, not its strength or speed.
float4 _GrassWindDirection;
float _WorldWindPlantsEnabled;
float WorldGenOriginalGrassWave(float3 anchor)
{
    return (sin(anchor.x*.13+anchor.z*.11+_Time.y*1.6)+.4*sin(anchor.x*.37-anchor.z*.18+_Time.y*2.7))*.14;
}
float3 WorldGenScatterWind(float3 p,float3 root,float weight,float flexibility,float herb,float4 traits)
{
    if(flexibility<=0)return p;
    if(herb>.5)
    {
        if(_WorldWindPlantsEnabled<.5)return p;
        float height=length(TransformObjectToWorld(float3(0,traits.w,0))-root);
        float wave=WorldGenOriginalGrassWave(root);
        // Same change in Bezier tip as the grass: no extra angle/bias, history,
        // phenotype gain or frequency. Whole-shoot weight pins the roots.
        float3 delta=normalize(float3(0,1,0)+_GrassWindDirection.xyz*wave)-float3(0,1,0);
        return p+delta*(height*weight*weight);
    }
    // Original tree/shrub placeholder movement, unchanged by this grass rollback.
    float wave=sin(_Time.y*1.4+p.x*.4+p.z*.27);
    p.xz+=float2(wave,wave*.47)*flexibility*weight*weight;
    return p;
}
#endif
