#ifndef WORLDGEN_CAMP_LIGHT_INCLUDED
#define WORLDGEN_CAMP_LIGHT_INCLUDED
int _WorldGenCampLightCount;
float4 _WorldGenCampLightPositions[4],_WorldGenCampLightColours[4];
float3 CampDiffuseLight(float3 position,float3 normal,float3 albedo)
{
    [branch]if(_WorldGenCampLightCount==0)return 0;
    float3 sum=0;
    [loop]for(int i=0;i<_WorldGenCampLightCount;i++)
    {
        float3 delta=_WorldGenCampLightPositions[i].xyz-position;float d2=dot(delta,delta);
        float edge=saturate(1-d2/_WorldGenCampLightPositions[i].w);
        float diffuse=.12+.88*saturate(dot(normal,delta*rsqrt(max(.001,d2))));
        sum+=_WorldGenCampLightColours[i].rgb*(edge*edge*diffuse/(1+d2));
    }
    return sum*albedo;
}
#endif
