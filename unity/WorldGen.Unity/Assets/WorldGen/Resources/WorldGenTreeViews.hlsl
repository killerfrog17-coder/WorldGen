#ifndef WORLDGEN_TREE_VIEWS_INCLUDED
#define WORLDGEN_TREE_VIEWS_INCLUDED
TEXTURE2D_ARRAY(_TreeColourViews);SAMPLER(sampler_TreeColourViews);
TEXTURE2D_ARRAY(_TreeNormalViews);SAMPLER(sampler_TreeNormalViews);
// Distance transition, then projected-diameter transition. Zero disables LOD.
float4 _WorldGenTreeViewRanges;float _WorldGenTreeViewProjection;
float3 WorldGenTreeViewDirection(uint index)
{float2 q=float2(index%4u,index/4u)/3.*2-1;return normalize(float3((q.x+q.y)*.5,1-max(abs(q.x),abs(q.y)),(q.x-q.y)*.5));}
void WorldGenTreeViewBasis(float3 d,out float3 r,out float3 u)
{r=cross(float3(0,1,0),d);r=dot(r,r)<.0001?float3(1,0,0):normalize(r);u=normalize(cross(d,r));}
void WorldGenTreeSelectViews(float3 direction,out uint3 indices,out float3 weights)
{
    float3 d=direction;d.y=max(0,d.y);d/=max(.0001,abs(d.x)+abs(d.y)+abs(d.z));
    float2 q=saturate(float2(d.x+d.z,d.x-d.z)*.5+.5)*3;
    uint2 cell=min((uint2)floor(q),uint2(2,2));float2 f=q-cell;uint a=cell.y*4u+cell.x;
    if(f.x+f.y<1){indices=uint3(a,a+1,a+4);weights=float3(1-f.x-f.y,f.x,f.y);}
    else{a+=5;indices=uint3(a,a-1,a-4);weights=float3(f.x+f.y-1,1-f.x,1-f.y);}
}
float2 WorldGenTreeViewUV(uint index,float3 p,float3 view,float radius)
{
    float3 d=WorldGenTreeViewDirection(index),r,u;WorldGenTreeViewBasis(d,r,u);
    p-=view*dot(p,d)/max(.2,dot(view,d));
    return .5+float2(dot(p,r),dot(p,u))/(2*radius);
}
half4 WorldGenTreeCoverage(uint index,float3 p,float3 view,float radius)
{
    float2 uv=WorldGenTreeViewUV(index,p,view,radius);
    half4 color=SAMPLE_TEXTURE2D_ARRAY(_TreeColourViews,sampler_TreeColourViews,uv,index);
    color.a*=all(uv>=0)&&all(uv<=1);return color;
}
void WorldGenTreeFullView(uint index,float3 p,float3 view,float radius,half4 coverage,bool prefetched,out half4 color,out half3 normal)
{
    float3 d=WorldGenTreeViewDirection(index),r,u;WorldGenTreeViewBasis(d,r,u);
    float2 uv=WorldGenTreeViewUV(index,p,view,radius);
    if(!prefetched)coverage=SAMPLE_TEXTURE2D_ARRAY(_TreeColourViews,sampler_TreeColourViews,uv,index);
    half4 depth=SAMPLE_TEXTURE2D_ARRAY(_TreeNormalViews,sampler_TreeNormalViews,uv,index);
    float t=(coverage.a>.2?(depth.a-.5)*2*radius:0)/max(.2,dot(view,d));
    uv+=float2(dot(view,r),dot(view,u))*t/(2*radius);
    color=SAMPLE_TEXTURE2D_ARRAY(_TreeColourViews,sampler_TreeColourViews,uv,index);color.a*=all(uv>=0)&&all(uv<=1);
    normal=SAMPLE_TEXTURE2D_ARRAY(_TreeNormalViews,sampler_TreeNormalViews,uv,index).rgb*2-1;
}
void WorldGenTreeCombine(half4 a,half4 b,half4 c,float3 weights,out half4 color,out float3 covered)
{
    covered=weights*float3(a.a,b.a,c.a);float alpha=covered.x+covered.y+covered.z;
    color=half4((a.rgb*covered.x+b.rgb*covered.y+c.rgb*covered.z)/max(.001,alpha),alpha);
}
void WorldGenTreeFullSurface(uint3 indices,float3 weights,float3 p,float3 view,float radius,bool early,out half4 color,out half3 normal)
{
    half4 a,b,c;half3 na,nb,nc;
    #if SHADER_TARGET >= 45
    if(early)
    {
        half4 aa=WorldGenTreeCoverage(indices.x,p,view,radius),ab=WorldGenTreeCoverage(indices.y,p,view,radius),ac=WorldGenTreeCoverage(indices.z,p,view,radius);
        if(WorldGenQuadAll(max(aa.a,max(ab.a,ac.a))<=.2?1:0)>.5)clip(-1);
        WorldGenTreeFullView(indices.x,p,view,radius,aa,true,a,na);WorldGenTreeFullView(indices.y,p,view,radius,ab,true,b,nb);WorldGenTreeFullView(indices.z,p,view,radius,ac,true,c,nc);
    }
    else
    #endif
    {WorldGenTreeFullView(indices.x,p,view,radius,0,false,a,na);WorldGenTreeFullView(indices.y,p,view,radius,0,false,b,nb);WorldGenTreeFullView(indices.z,p,view,radius,0,false,c,nc);}
    float3 covered;WorldGenTreeCombine(a,b,c,weights,color,covered);normal=normalize(na*covered.x+nb*covered.y+nc*covered.z+view*.00001);
}
float2 WorldGenTreeViewLod(float distance,float worldRadius)
{
    if(_WorldGenTreeViewRanges.x<=0)return 0;
    float middle=smoothstep(_WorldGenTreeViewRanges.x,_WorldGenTreeViewRanges.y,distance);
    float pixels=_WorldGenTreeViewProjection*worldRadius/max(1,distance);
    float far=1-smoothstep(_WorldGenTreeViewRanges.w,_WorldGenTreeViewRanges.z,pixels);
    return float2(middle,far*middle);
}
void WorldGenTreeSurface(float3 p,float3 view,float radius,float2 lod,bool early,out half4 color,out half3 normal)
{
    uint3 indices;float3 weights;WorldGenTreeSelectViews(view,indices,weights);
    if(lod.x<=0){WorldGenTreeFullSurface(indices,weights,p,view,radius,early,color,normal);return;}
    uint dominant=weights.x>=weights.y&&weights.x>=weights.z?indices.x:weights.y>=weights.z?indices.y:indices.z;
    if(lod.y>=1)color=WorldGenTreeCoverage(dominant,p,view,radius);
    else
    {
        float3 covered;WorldGenTreeCombine(WorldGenTreeCoverage(indices.x,p,view,radius),WorldGenTreeCoverage(indices.y,p,view,radius),WorldGenTreeCoverage(indices.z,p,view,radius),weights,color,covered);
        if(lod.y>0)color=lerp(color,WorldGenTreeCoverage(dominant,p,view,radius),lod.y);
    }
    // A smooth crown envelope replaces subpixel normal/depth reconstruction.
    float front=sqrt(max(.001,radius*radius-dot(p,p)));normal=normalize(p+view*front);
    if(lod.x<1)
    {
        half4 detailed;half3 detailedNormal;
        // Incoming coverage may survive even when the detailed ray is empty.
        WorldGenTreeFullSurface(indices,weights,p,view,radius,false,detailed,detailedNormal);
        color=lerp(detailed,color,lod.x);normal=normalize(lerp(detailedNormal,normal,lod.x));
    }
}
// View choice and affine ray projection are constant/linear across a tree
// billboard. Prepare them at vertices; retain the literal path for A/B checks.
struct WorldGenPreparedTreeViews {uint3 indices;float3 weights;float4 a,b,c;};
float4 WorldGenPrepareTreeView(uint index,float3 p,float3 view,float radius)
{
    float3 d=WorldGenTreeViewDirection(index),r,u;WorldGenTreeViewBasis(d,r,u);
    float inverse=rcp(max(.2,dot(view,d)));
    float3 projected=p-view*(dot(p,d)*inverse);
    return float4(.5+float2(dot(projected,r),dot(projected,u))/(2*radius),float2(dot(view,r),dot(view,u))*inverse);
}
WorldGenPreparedTreeViews WorldGenPrepareTreeViews(float3 p,float3 view,float radius,float2 lod)
{
    WorldGenPreparedTreeViews o=(WorldGenPreparedTreeViews)0;
    // Tiny horizon crowns retain the cheap one-view path. Do not prepare three
    // projections at six vertices for a billboard covering only a few pixels.
    if(lod.y>=1)return o;
    WorldGenTreeSelectViews(view,o.indices,o.weights);
    o.a=WorldGenPrepareTreeView(o.indices.x,p,view,radius);
    o.b=WorldGenPrepareTreeView(o.indices.y,p,view,radius);
    o.c=WorldGenPrepareTreeView(o.indices.z,p,view,radius);return o;
}
half4 WorldGenPreparedTreeCoverage(float4 projection,uint index)
{
    half4 color=SAMPLE_TEXTURE2D_ARRAY(_TreeColourViews,sampler_TreeColourViews,projection.xy,index);return color;
}
half4 WorldGenTreeMaskCoverage(half4 color,float2 uv)
{color.a*=all(uv>=0)&&all(uv<=1);return color;}
void WorldGenPreparedTreeFullView(float4 projection,uint index,half4 coverage,out half4 color,out half3 normal)
{
    half4 depth=SAMPLE_TEXTURE2D_ARRAY(_TreeNormalViews,sampler_TreeNormalViews,projection.xy,index);
    float2 uv=projection.xy+projection.zw*(coverage.a>.2?depth.a-.5:0);
    color=WorldGenTreeMaskCoverage(SAMPLE_TEXTURE2D_ARRAY(_TreeColourViews,sampler_TreeColourViews,uv,index),uv);
    normal=SAMPLE_TEXTURE2D_ARRAY(_TreeNormalViews,sampler_TreeNormalViews,uv,index).rgb*2-1;
}
void WorldGenPreparedTreeSurface(WorldGenPreparedTreeViews prepared,float3 p,float3 view,float radius,float2 lod,bool early,out half4 color,out half3 normal)
{
    color=0;normal=view;
    if(lod.y>=1){WorldGenTreeSurface(p,view,radius,lod,early,color,normal);return;}
    half4 rawA=WorldGenPreparedTreeCoverage(prepared.a,prepared.indices.x),rawB=WorldGenPreparedTreeCoverage(prepared.b,prepared.indices.y),rawC=WorldGenPreparedTreeCoverage(prepared.c,prepared.indices.z);
    half4 aa=WorldGenTreeMaskCoverage(rawA,prepared.a.xy),ab=WorldGenTreeMaskCoverage(rawB,prepared.b.xy),ac=WorldGenTreeMaskCoverage(rawC,prepared.c.xy);
    if(lod.x>0)
    {
        float3 covered;WorldGenTreeCombine(aa,ab,ac,prepared.weights,color,covered);
        if(lod.y>0)
        {
            half4 dominant=prepared.weights.x>=prepared.weights.y&&prepared.weights.x>=prepared.weights.z?aa:prepared.weights.y>=prepared.weights.z?ab:ac;
            color=lerp(color,dominant,lod.y);
        }
        float front=sqrt(max(.001,radius*radius-dot(p,p)));normal=normalize(p+view*front);
        if(lod.x>=1)return;
    }
    half4 a,b,c;half3 na,nb,nc;
    bool masked=false;
    #if SHADER_TARGET >= 45
    if(early&&lod.x<=0)
    {
        if(WorldGenQuadAll(max(aa.a,max(ab.a,ac.a))<=.2?1:0)>.5)clip(-1);
        masked=true;
    }
    #endif
    // The initial colour samples also supply the simplified representation
    // through an LOD blend; no separate three-read simplified surface pass.
    WorldGenPreparedTreeFullView(prepared.a,prepared.indices.x,masked?aa:rawA,a,na);
    WorldGenPreparedTreeFullView(prepared.b,prepared.indices.y,masked?ab:rawB,b,nb);
    WorldGenPreparedTreeFullView(prepared.c,prepared.indices.z,masked?ac:rawC,c,nc);
    float3 covered;half4 detailed;WorldGenTreeCombine(a,b,c,prepared.weights,detailed,covered);
    half3 detailedNormal=normalize(na*covered.x+nb*covered.y+nc*covered.z+view*.00001);
    if(lod.x<=0){color=detailed;normal=detailedNormal;}
    else{color=lerp(detailed,color,lod.x);normal=normalize(lerp(detailedNormal,normal,lod.x));}
}
#endif
