#ifndef WORLDGEN_CLIFF_SURFACE_INCLUDED
#define WORLDGEN_CLIFF_SURFACE_INCLUDED
float3 CliffMeanColour(int layer)
{
    // Generated masters' linear means: cliff-material-generation-20261108.json.
    float3 mean=layer==0?float3(.303344,.281661,.244894):float3(.377209,.306061,.229586);
    #ifdef UNITY_COLORSPACE_GAMMA
    mean=LinearToSRGB(mean);
    #endif
    return mean;
}
float3 CliffFaceColour(float3 photo,float3 pigment)
{
    float lum=max(.015,dot(pigment,float3(.2126,.7152,.0722)));
    float reference=.18;
    #ifdef UNITY_COLORSPACE_GAMMA
    reference=.46;
    #endif
    float3 hue=lerp(1,clamp(pigment/lum,.7,1.3),.22);
    float value=lerp(1,clamp(lum/reference,.65,1.25),.25);
    return photo*hue*value;
}
// Wall charts share physical altitude for v, so sedimentary bedding remains
// horizontal at every latitude. u uses periodic planet coordinates. The two
// dominant contour axes avoid projecting the wall image onto a floor plane.
float3 CliffFaceAxes(float3 normal,float3 up)
{
    float3 tangent=cross(up,normal),w=pow(abs(tangent),4);
    if(w.x<=w.y&&w.x<=w.z)w.x=0;else if(w.y<=w.z)w.y=0;else w.z=0;
    return w/max(.0001,dot(w,1));
}
float4 CliffFaceArray(TEXTURE2D_ARRAY_PARAM(tex,samp),float3 p,float3 dx,float3 dy,float3 altitude,float3 w,int layer)
{
    float4 value=0;p/=8;dx/=8;dy/=8;altitude/=8;
    [branch]if(w.x>.001)value+=SAMPLE_TEXTURE2D_ARRAY_GRAD(tex,samp,float2(p.x,altitude.x),layer,float2(dx.x,altitude.y),float2(dy.x,altitude.z))*w.x;
    [branch]if(w.y>.001)value+=SAMPLE_TEXTURE2D_ARRAY_GRAD(tex,samp,float2(p.y,altitude.x),layer,float2(dx.y,altitude.y),float2(dy.y,altitude.z))*w.y;
    [branch]if(w.z>.001)value+=SAMPLE_TEXTURE2D_ARRAY_GRAD(tex,samp,float2(p.z,altitude.x),layer,float2(dx.z,altitude.y),float2(dy.z,altitude.z))*w.z;
    return value;
}
float3 CliffFaceBump(TEXTURE2D_ARRAY_PARAM(tex,samp),float3 p,float3 dx,float3 dy,float3 altitude,float3 w,float3 up,int layer)
{
    float3 bump=0;p/=8;dx/=8;dy/=8;altitude/=8;
    [branch]if(w.x>.001){float2 a=SAMPLE_TEXTURE2D_ARRAY_GRAD(tex,samp,float2(p.x,altitude.x),layer,float2(dx.x,altitude.y),float2(dy.x,altitude.z)).xy*2-1;bump+=(float3(a.x,0,0)+up*a.y)*w.x;}
    [branch]if(w.y>.001){float2 a=SAMPLE_TEXTURE2D_ARRAY_GRAD(tex,samp,float2(p.y,altitude.x),layer,float2(dx.y,altitude.y),float2(dy.y,altitude.z)).xy*2-1;bump+=(float3(0,a.x,0)+up*a.y)*w.y;}
    [branch]if(w.z>.001){float2 a=SAMPLE_TEXTURE2D_ARRAY_GRAD(tex,samp,float2(p.z,altitude.x),layer,float2(dx.z,altitude.y),float2(dy.z,altitude.z)).xy*2-1;bump+=(float3(0,0,a.x)+up*a.y)*w.z;}
    return bump;
}
#endif
