#ifndef WORLDGEN_BLOOD_INCLUDED
#define WORLDGEN_BLOOD_INCLUDED
TEXTURE2D(_BloodMask);SAMPLER(sampler_BloodMask);
half3 WorldGenBlood(half3 pigment,float2 uv,float enabled,float3 bloodColor)
{
    [branch]if(enabled<.5)return pigment;
    half amount=SAMPLE_TEXTURE2D(_BloodMask,sampler_BloodMask,uv).r;
    return lerp(pigment,bloodColor+pigment*.12,amount*.9);
}
#endif
