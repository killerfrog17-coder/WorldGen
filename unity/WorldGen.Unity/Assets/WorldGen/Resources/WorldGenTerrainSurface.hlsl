#ifndef WORLDGEN_TERRAIN_SURFACE_INCLUDED
#define WORLDGEN_TERRAIN_SURFACE_INCLUDED
// Camera-scoped settings, never per-tile property blocks/material rewrites.
// Ranges: full detail, last texture, last normal/mask, last extra layer/axis.
float4 _TerrainSurfaceRanges, _TerrainSurfaceControls;

float3 SurfaceAxes(float3 normal, float count, float extra)
{
    float3 w=pow(abs(normal),4);
    float3 main=w.x>=w.y&&w.x>=w.z?float3(1,0,0):w.y>=w.z?float3(0,1,0):float3(0,0,1);
    if(count<1.5||extra<=.001)return main;
    if(count<2.5)
    {
        if(w.x<=w.y&&w.x<=w.z)w.x=0;else if(w.y<=w.z)w.y=0;else w.z=0;
    }
    w/=max(.0001,dot(w,1));
    return lerp(main,w,extra);
}

float4 SurfaceArray(TEXTURE2D_ARRAY_PARAM(tex,samp),float3 p,float3 dx,float3 dy,float3 w,int layer)
{
    float4 value=0;
    [branch] if(w.x>.001)value+=SAMPLE_TEXTURE2D_ARRAY_GRAD(tex,samp,p.yz,layer,dx.yz,dy.yz)*w.x;
    [branch] if(w.y>.001)value+=SAMPLE_TEXTURE2D_ARRAY_GRAD(tex,samp,p.xz,layer,dx.xz,dy.xz)*w.y;
    [branch] if(w.z>.001)value+=SAMPLE_TEXTURE2D_ARRAY_GRAD(tex,samp,p.xy,layer,dx.xy,dy.xy)*w.z;
    return value;
}
float3 SurfaceMat(TEXTURE2D_PARAM(tex,samp),float3 p,float3 dx,float3 dy,float3 w)
{
    float3 value=0;
    [branch] if(w.x>.001)value+=SAMPLE_TEXTURE2D_GRAD(tex,samp,p.yz,dx.yz,dy.yz).rgb*w.x;
    [branch] if(w.y>.001)value+=SAMPLE_TEXTURE2D_GRAD(tex,samp,p.xz,dx.xz,dy.xz).rgb*w.y;
    [branch] if(w.z>.001)value+=SAMPLE_TEXTURE2D_GRAD(tex,samp,p.xy,dx.xy,dy.xy).rgb*w.z;
    return value;
}
float3 SurfaceBump(float3 p,float3 dx,float3 dy,float3 w,int layer)
{
    float3 bump=0;
    [branch] if(w.x>.001){float2 a=SAMPLE_TEXTURE2D_ARRAY_GRAD(_GroundNormals,sampler_GroundAlbedo,p.yz,layer,dx.yz,dy.yz).xy*2-1;bump+=float3(0,a.x,a.y)*w.x;}
    [branch] if(w.y>.001){float2 a=SAMPLE_TEXTURE2D_ARRAY_GRAD(_GroundNormals,sampler_GroundAlbedo,p.xz,layer,dx.xz,dy.xz).xy*2-1;bump+=float3(a.x,0,a.y)*w.y;}
    [branch] if(w.z>.001){float2 a=SAMPLE_TEXTURE2D_ARRAY_GRAD(_GroundNormals,sampler_GroundAlbedo,p.xy,layer,dx.xy,dy.xy).xy*2-1;bump+=float3(a.x,a.y,0)*w.z;}
    return bump;
}
void SurfaceLayers(O i,float extra,out int3 layers,out float3 fractions)
{
    float4 a=max(i.weights,0),b=max(i.groundB,0);
    float sum=max(.001,dot(a,1)+dot(b,1));a/=sum;b/=sum;
    layers=int3(0,0,0);fractions=0;
    // Select only near pixels. NO material loop or mask fetch beyond texture range.
    [unroll]for(int layer=0;layer<14;layer++)
    {
        float c=GroundContribution(i,a,b,layer);
        if(c>fractions.x){fractions.z=fractions.y;layers.z=layers.y;fractions.y=fractions.x;layers.y=layers.x;fractions.x=c;layers.x=layer;}
        else if(c>fractions.y){fractions.z=fractions.y;layers.z=layers.y;fractions.y=c;layers.y=layer;}
        else if(c>fractions.z){fractions.z=c;layers.z=layer;}
    }
    float count=TerrainTest(TD_LAYER)?1:_TerrainSurfaceControls.z;
    fractions.y*=count>1.5?extra:0;fractions.z*=count>2.5?extra:0;
    fractions/=max(.001,dot(fractions,1));
}
float3 SurfaceLeafLight(float3 albedo,float3 normal,Light sun,float transmission)
{
    if(TerrainTest(TD_UNLIT))return albedo;
    return albedo*(TerrainAmbient().rgb+sun.color*(.48+.52*saturate(dot(normal,sun.direction))+transmission)*sun.shadowAttenuation*lerp(.025,1,TerrainDaylight()));
}
float3 SurfaceMatLight(float3 normal,Light sun)
{
    if(TerrainTest(TD_UNLIT))return 1;
    return TerrainAmbient().rgb+sun.color*(.42+.58*saturate(dot(normal,sun.direction)))*sun.shadowAttenuation*lerp(.025,1,TerrainDaylight());
}

half4 ShadeTerrainSurface(O i,float distance,float3 farColor,float3 fogColor)
{
    float3 n=normalize(i.normal);
    float3 world=TerrainTextureAnchor().xyz+TerrainEast().xyz*(i.position.x+TerrainOrigin().x)+TerrainNorth().xyz*(i.position.z+TerrainOrigin().y)+TerrainUp().xyz*i.position.y;
    float3 p=world/max(.05,_TextureMetres),dx=ddx(p),dy=ddy(p);
    float3 worldDx=ddx(world),worldDy=ddy(world);
    float4 range=_TerrainSurfaceRanges;
    if(TerrainTest(TD_NEAR))range.y=min(range.y,max(1,_TerrainDiagnosticRadius));
    bool textures=_TerrainSurfaceControls.x>.5&&!TerrainTest(TD_TEXTURES);
    float detail=1-smoothstep(min(range.x,range.y*.5),max(.01,range.y),distance);
    float normalDetail=1-smoothstep(range.z*.45,max(.01,range.z),distance);
    float extra=1-smoothstep(range.x*.7,max(.01,range.w),distance);
    float3 albedo=farColor;
    // Stable mean properties of these rough, nonmetallic ground materials.
    // Fine mask variation is reserved for the close High/Ultra path.
    float ao=.93,rough=lerp(.88,.55,saturate(i.groundB.w)),metal=0;
    [branch] if(textures&&detail>.001)
    {
        int3 layers;float3 fractions;SurfaceLayers(i,extra,layers,fractions);
        float3 np=n.x*TerrainEast().xyz+n.y*TerrainUp().xyz+n.z*TerrainNorth().xyz;
        float axes=TerrainTest(TD_AXIS)?1:_TerrainSurfaceControls.w;
        float3 w=SurfaceAxes(np,axes,extra),colour=0,bump=0;float4 mask=0;
        bool colourRead=!TerrainTest(TD_ALBEDO),normalRead=normalDetail>.001&&_TerrainSurfaceControls.y>=1&&!TerrainTest(TD_NORMALS);
        bool maskRead=normalDetail>.001&&_TerrainSurfaceControls.y>=3&&!TerrainTest(TD_MASKS);
        [unroll]for(int at=0;at<3;at++)
        {
            [branch] if(fractions[at]>.001)
            {
                if(colourRead)colour+=SurfaceArray(TEXTURE2D_ARRAY_ARGS(_GroundAlbedo,sampler_GroundAlbedo),p,dx,dy,w,layers[at]).rgb*fractions[at];
                if(normalRead)bump+=SurfaceBump(p,dx,dy,w,layers[at])*fractions[at];
                if(maskRead)mask+=SurfaceArray(TEXTURE2D_ARRAY_ARGS(_GroundMasks,sampler_GroundAlbedo),p,dx,dy,w,layers[at])*fractions[at];
            }
        }
        float scale=2;
        #ifndef UNITY_COLORSPACE_GAMMA
        scale=4.672;
        #endif
        if(colourRead)albedo*=lerp(1,colour*scale,detail*.88);
        if(normalRead)
        {
            bump-=np*dot(np,bump);np=normalize(np+bump*normalDetail);
            n=normalize(float3(dot(np,TerrainEast().xyz),dot(np,TerrainUp().xyz),dot(np,TerrainNorth().xyz)));
        }
        if(maskRead){ao=lerp(ao,mask.r,normalDetail);rough=lerp(rough,mask.g,normalDetail);metal=mask.a*normalDetail;}
    }
    if(TerrainTest(TD_MASKS)){ao=.9;rough=.85;metal=0;}
    float cover=saturate(i.color.a);
    [branch] if(distance<16&&!TerrainTest(TD_GRAIN))
    {
        float grain=sin(world.x*29+sin(world.z*17))*sin(world.z*43+world.x*7);
        albedo*=1+grain*cover*.13*(1-smoothstep(3,16,distance));
    }
    rough=lerp(rough,.88,cover);ao=lerp(ao,.9,cover);
    InputData input=(InputData)0;input.positionWS=i.position;input.normalWS=n;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.position);
    input.shadowCoord=TransformWorldToShadowCoord(i.position);input.bakedGI=TerrainAmbient().rgb;input.shadowMask=1;input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
    SurfaceData s=(SurfaceData)0;s.albedo=albedo;s.alpha=1;s.occlusion=ao;s.smoothness=1-rough;s.metallic=metal;s.normalTS=half3(0,0,1);
    Light sun=(Light)0;float transmission=0;
    if(!TerrainTest(TD_UNLIT)){sun=WorldGenFoliageLight(i.position);transmission=pow(saturate(dot(-sun.direction,input.viewDirectionWS)),3)*.22;}
    half3 color;
    if(TerrainTest(TD_UNLIT))color=albedo;
    else if(TerrainTest(TD_SIMPLE))color=SurfaceLeafLight(albedo,n,sun,transmission);
    else color=UniversalFragmentPBR(input,s).rgb;

    // Ecological weights/tints remain at every distance. Only their microscopic
    // material textures retire; these few weights are NOT an 850m texture stack.
    float4 flora=float4(cover,0,0,cover);
    if(_FloraSurfaceEnabled>.5&&textures)flora=SAMPLE_TEXTURE2D(_FloraSurface,sampler_FloraSurface,i.coverageUV*(1-_FloraSurface_TexelSize.xy)+.5*_FloraSurface_TexelSize.xy);
    float3 mix=max(0,float3(flora.a,flora.g,flora.b))/.45*_GroundFlora;mix/=max(1,dot(mix,1));
    if(TerrainTest(TD_GRASS))mix.x=0;if(TerrainTest(TD_MOSS))mix.y=0;if(TerrainTest(TD_LICHEN))mix.z=0;
    float3 axes=SurfaceAxes(TerrainUp().xyz,TerrainTest(TD_AXIS)?1:_TerrainSurfaceControls.w,extra);
    float3 vegetation=0;
    [branch] if(mix.x>.001)
    {
        float3 mat=.5;
        if(textures&&detail>.001)mat=lerp(mat,SurfaceMat(TEXTURE2D_ARGS(_GrassField,sampler_GrassField),world/8,worldDx/8,worldDy/8,axes),detail);
        float tint=.9111;
        #ifndef UNITY_COLORSPACE_GAMMA
        tint=.815;
        #endif
        float3 normal=normalize(float3((mat.g-.5)*.35,.8,(mat.b-.5)*.35)+n*.25);
        vegetation+=SurfaceLeafLight(farColor*(tint*mat.r*2),normal,sun,transmission)*mix.x;
    }
    [branch] if(mix.y+mix.z>.001)
    {
        float3 moss=float3(.27,.35,.16),lichen=float3(.51,.50,.40);
        #ifndef UNITY_COLORSPACE_GAMMA
        moss=SRGBToLinear(moss);lichen=SRGBToLinear(lichen);
        #endif
        if(mix.y>.001)
        {
            float3 mat=.5;
            if(textures&&detail>.001)mat=lerp(mat,SurfaceMat(TEXTURE2D_ARGS(_MossField,sampler_MossField),world/.8,worldDx/.8,worldDy/.8,axes),detail);
            float3 normal=normalize(n+float3(mat.g-.5,0,mat.b-.5)*1.3);
            vegetation+=lerp(farColor,moss,.4)*(mat.r*2)*SurfaceMatLight(normal,sun)*mix.y;
        }
        if(mix.z>.001)
        {
            float3 mat=.5;
            if(textures&&detail>.001)mat=lerp(mat,SurfaceMat(TEXTURE2D_ARGS(_LichenField,sampler_LichenField),world/.8,worldDx/.8,worldDy/.8,axes),detail);
            float3 normal=normalize(n+float3(mat.g-.5,0,mat.b-.5));
            vegetation+=lerp(farColor,lichen,.55)*(mat.r*2)*SurfaceMatLight(normal,sun)*mix.z;
        }
    }
    color=color*(1-dot(mix,1))+vegetation;
    if(_ForestSurfaceEnabled>.5&&TerrainForestEnabled()>.5&&textures&&!TerrainTest(TD_FOREST))
    {
        float4 forest=SAMPLE_TEXTURE2D(_ForestSurface,sampler_ForestSurface,i.coverageUV*(1-_ForestSurface_TexelSize.xy)+.5*_ForestSurface_TexelSize.xy);
        float3 leaves=forest.rgb;
        #ifndef UNITY_COLORSPACE_GAMMA
        leaves=SRGBToLinear(leaves);
        #endif
        color=lerp(color,SurfaceLeafLight(leaves,n,sun,transmission),saturate(forest.a)*smoothstep(700,1800,distance)*.8);
    }
    if(_MidSurfaceEnabled>.5&&textures&&!TerrainTest(TD_MID))
    {
        float4 mid=SAMPLE_TEXTURE2D(_MidSurface,sampler_MidSurface,i.coverageUV*(1-_MidSurface_TexelSize.xy)+.5*_MidSurface_TexelSize.xy);
        float3 leaves=mid.rgb;
        #ifndef UNITY_COLORSPACE_GAMMA
        leaves=SRGBToLinear(leaves);
        #endif
        color=lerp(color,SurfaceLeafLight(leaves,n,sun,transmission),saturate(mid.a)*smoothstep(180,1000,distance));
    }
    if(!TerrainTest(TD_UNLIT))color+=SecondaryCelestialLight(n,albedo);
    return half4(TerrainTestAir(color,i.position,_WorldSpaceCameraPos,fogColor,TerrainFogDistance()),1);
}
#endif
