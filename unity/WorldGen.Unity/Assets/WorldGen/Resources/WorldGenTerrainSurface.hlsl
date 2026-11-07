#ifndef WORLDGEN_TERRAIN_SURFACE_INCLUDED
#define WORLDGEN_TERRAIN_SURFACE_INCLUDED
// Camera-scoped settings, never per-tile property blocks/material rewrites.
// Ranges: full detail, last texture, last normal/mask, last extra layer/axis.
float4 _TerrainSurfaceRanges, _TerrainSurfaceControls;
float4 _TerrainGeologyDatum;
float4 _TerrainBeddingOrigin;

float SurfacePatchHash(float2 p)
{float3 h=frac(float3(p.x,p.y,p.x)*.1031);h+=dot(h,h.yzx+33.33);return frac((h.x+h.y)*h.z);}
float SurfaceMossPatch(float3 world,float amount,float detail)
{
    // Ecological cover is area, not translucent green paint over every pixel.
    // Stable metre-scale cushions reveal litter between them. Analytic noise
    // adds no material fetch, geometry, draw call or per-frame CPU work.
    float2 p=float2(dot(world,TerrainEast().xyz),dot(world,TerrainNorth().xyz))/1.8;
    float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
    float noise=lerp(lerp(SurfacePatchHash(cell),SurfacePatchHash(cell+float2(1,0)),f.x),lerp(SurfacePatchHash(cell+float2(0,1)),SurfacePatchHash(cell+1),f.x),f.y);
    float width=max(.045,fwidth(noise)*1.5);
    float patch=smoothstep(1-amount-width,1-amount+width,noise);
    return lerp(amount,patch,detail);
}

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
void SurfaceLayers(O i,float extra,float3 litter,out int3 layers,out float3 fractions)
{
    float4 a=max(i.weights,0),b=max(i.groundB,0);
    float sum=max(.001,dot(a,1)+dot(b,1));a/=sum;b/=sum;
    layers=int3(0,0,0);fractions=0;
    float litterCover=saturate(dot(litter,1)),loose=a.x+a.z+a.w+b.y;
    // Select only near pixels. NO material loop or mask fetch beyond texture range.
    [unroll]for(int layer=0;layer<17;layer++)
    {
        float c=layer>=14?loose*litter[layer==14?0:layer==15?1:2]:GroundContribution(i,a,b,layer);
        // Fallen litter covers loose gravel/clay/organic substrate too. Only
        // replacing soil let those layers displace the scan from Medium's top2.
        if(layer==0||layer==2||layer==3||layer==5)c*=1-litterCover;
        if(c>fractions.x){fractions.z=fractions.y;layers.z=layers.y;fractions.y=fractions.x;layers.y=layers.x;fractions.x=c;layers.x=layer;}
        else if(c>fractions.y){fractions.z=fractions.y;layers.z=layers.y;fractions.y=c;layers.y=layer;}
        else if(c>fractions.z){fractions.z=c;layers.z=layer;}
    }
    float count=TerrainTest(TD_LAYER)?1:_TerrainSurfaceControls.z;
    fractions.y*=count>1.5?extra:0;fractions.z*=count>2.5?extra:0;
    fractions/=max(.001,dot(fractions,1));
}
float3 SurfaceLeafLight(float3 albedo,float3 normal,Light sun,float transmission,half canopy)
{
    if(TerrainTest(TD_UNLIT))return albedo;
    return albedo*(TerrainAmbient().rgb*WorldGenCanopyAmbient(canopy)+sun.color*(.48+.52*saturate(dot(normal,sun.direction))+transmission)*sun.shadowAttenuation*lerp(.025,1,TerrainDaylight()));
}

float2 SaltCellPoint(float2 cell)
{
    // Period matches the 1024m texture-origin wrap, including negative cells.
    cell=cell-floor(cell/512)*512;
    float3 p=frac(cell.xyx*float3(.1031,.1030,.0973));
    p+=dot(p,p.yzx+33.33);return .15+.7*frac((p.xx+p.yz)*p.zy);
}
void SurfaceSaltCrust(O i,float3 world,float distance,float3 farColor,inout float3 albedo,inout float3 normal,inout float rough)
{
    float sum=max(.001,dot(max(i.weights,0),1)+dot(max(i.groundB,0),1));
    float crust=smoothstep(.28,.65,max(0,i.groundB.z)/sum);
    if(crust<=.001)return;
    // Preserve the white macro surface when brown fine-material modulation
    // retires. The old substrate multiply turned pale salt back into beige dirt.
    float grain=saturate((dot(albedo,float3(.2126,.7152,.0722))/max(.02,dot(farColor,float3(.2126,.7152,.0722)))-.65)/.65);
    albedo=lerp(albedo,farColor*lerp(.96,1.02,grain),crust);rough=lerp(rough,.92,crust);
    float detail=1-smoothstep(35,110,distance);
    [branch] if(detail>.001)
    {
        float3 pn=normal.x*TerrainEast().xyz+normal.y*TerrainUp().xyz+normal.z*TerrainNorth().xyz;
        float3 an=abs(pn);int axis=an.x>an.y&&an.x>an.z?0:an.y>an.z?1:2;
        float2 q=(axis==0?world.yz:axis==1?world.xz:world.xy)*.5;
        float2 cell=floor(q),f=frac(q),v1=0,v2=0;float d1=1e8,d2=1e8;
        [unroll]for(int y=-1;y<=1;y++)[unroll]for(int x=-1;x<=1;x++)
        {
            float2 offset=float2(x,y),v=f-offset-SaltCellPoint(cell+offset);float d=dot(v,v);
            if(d<d1){d2=d1;v2=v1;d1=d;v1=v;}else if(d<d2){d2=d;v2=v;}
        }
        float r1=sqrt(max(1e-8,d1)),r2=sqrt(max(1e-8,d2)),edge=r2-r1;
        float t=saturate((edge-.015)/.085),ridge=1-t*t*(3-2*t);
        float2 gradient=(v2/r2-v1/r1)*(-6*t*(1-t)/.085)*.0125;
        float3 bump=axis==0?float3(0,gradient.x,gradient.y):axis==1?float3(gradient.x,0,gradient.y):float3(gradient.x,gradient.y,0);
        pn=normalize(pn-(bump-pn*dot(pn,bump))*detail*crust);
        normal=normalize(float3(dot(pn,TerrainEast().xyz),dot(pn,TerrainUp().xyz),dot(pn,TerrainNorth().xyz)));
        albedo*=1+ridge*.055*crust*detail;
    }
}
float3 SurfaceMatLight(float3 normal,Light sun,half canopy)
{
    if(TerrainTest(TD_UNLIT))return 1;
    return TerrainAmbient().rgb*WorldGenCanopyAmbient(canopy)+sun.color*(.42+.58*saturate(dot(normal,sun.direction)))*sun.shadowAttenuation*lerp(.025,1,TerrainDaylight());
}

void SurfaceStrata(O i,inout float3 albedo)
{
    // Exposed sedimentary faces keep metre-scale bedding after close scan
    // textures retire. No additional texture fetch, mesh or render pass.
    float materials=max(.001,dot(max(i.weights,0),1)+dot(max(i.groundB,0),1));
    float sedimentary=saturate(i.rocks.z+i.rocks.w+i.rocksB.x);
    float exposed=saturate((i.groundB.x+i.weights.w*.65)/materials)*sedimentary;
    exposed*=smoothstep(.08,.40,1-abs(normalize(i.normal).y));
    [branch] if(exposed>.001)
    {
        // Recover altitude from the curved local tangent frame. Using the
        // wrapped texture anchor here would move beds at every origin reset.
        float2 radial=i.position.xz+_TerrainGeologyDatum.zw;
        float altitude=i.position.y+_TerrainGeologyDatum.x+dot(radial,radial)*_TerrainGeologyDatum.y;
        float3 regional=_TerrainBeddingOrigin.xyz+(TerrainEast().xyz*radial.x+TerrainNorth().xyz*radial.y+TerrainUp().xyz*i.position.y)*.0001;
        float folded=altitude+sin(dot(regional,float3(.27,.41,.13))+_TerrainBeddingOrigin.w)*30
            +sin(dot(regional,float3(1.1,.7,.9)))*9;
        // Unequal bed thickness and gently dipping regional strata. Adjacent
        // beds inherit independent tones, joined continuously: a repeating
        // sawtooth made every valley look like the same striped layer cake.
        float strata=folded+sin(folded*.031)*8+sin(folded*.097)*2;
        float layer=strata/29,index=floor(layer),blend=smoothstep(.08,.92,frac(layer));
        float seed=_TerrainBeddingOrigin.w*71.3;
        float bed=lerp(SurfacePatchHash(float2(index,seed)),SurfacePatchHash(float2(index+1,seed)),blend);
        float carbonate=saturate(i.rocks.w/max(.001,sedimentary));
        float3 tint=lerp(lerp(float3(1.055,1.03,1),float3(.85,.81,.76),bed),
            lerp(float3(1.025,1.025,1.02),float3(.93,.94,.94),bed),carbonate);
        albedo*=lerp(1,tint,exposed);
    }
}
void SurfaceRockWeathering(O i,float3 world,float3 worldDx,float3 worldDy,inout float3 albedo)
{
    float total=max(.001,dot(max(i.weights,0),1)+dot(max(i.groundB,0),1));
    float bare=saturate((i.groundB.x+i.weights.w*.45)/total)*smoothstep(.06,.38,1-abs(normalize(i.normal).y));
    if(bare<.01||_TerrainSurfaceControls.y<1)return;
    // The microscopic scan retires at the normal surface range. A single
    // two-axis blend provides broad weathering variation across exposed
    // rock faces. At32m the generated rock grain averaged away over distant
    // walls;128m keeps larger patches visible. Same existing two fetches,
    // texture array and wrapped1024m anchor, with no new material or pass.
    int rock=0;float best=i.rocks.x;
    [unroll]for(int k=1;k<6;k++)
    {
        float weight=k<4?i.rocks[k&3]:i.rocksB[(k-4)&1];
        if(weight>best){best=weight;rock=k;}
    }
    float3 normal=normalize(i.normal),planetNormal=normal.x*TerrainEast().xyz+normal.y*TerrainUp().xyz+normal.z*TerrainNorth().xyz;
    float3 axes=SurfaceAxes(planetNormal,2,1);
    float shade=dot(SurfaceArray(TEXTURE2D_ARRAY_ARGS(_GroundAlbedo,sampler_GroundAlbedo),world/128,worldDx/128,worldDy/128,axes,8+rock).rgb,float3(.2126,.7152,.0722));
    #ifndef UNITY_COLORSPACE_GAMMA
    shade*=4.672;
    #else
    shade*=2;
    #endif
    albedo*=lerp(1,clamp(shade,.55,1.45),bare*.62);
}

void SurfaceSandDistance(O i,float3 world,float3 worldDx,float3 worldDy,float distance,float detail,float canopy,inout float3 albedo)
{
    if(TerrainTest(TD_ALBEDO)||TerrainTest(TD_NEAR))return;
    float total=max(.001,dot(max(i.weights,0),1)+dot(max(i.groundB,0),1));
    float sand=saturate(i.weights.y/total);
    float extension=(1-detail)*(1-smoothstep(64,160,distance))*smoothstep(.70,.95,sand)
        *(1-smoothstep(.05,.25,saturate(i.color.a)))*(1-smoothstep(.05,.30,canopy));
    [branch]if(extension>.001)
    {
        // Preserve the registered generated sand's4m footprint after the
        // general close-material path retires. Albedo only, at most two
        // existing-array reads; no distant normal or mask sampling.
        float3 normal=normalize(i.normal);
        float3 planetNormal=normal.x*TerrainEast().xyz+normal.y*TerrainUp().xyz+normal.z*TerrainNorth().xyz;
        float scale=.5/max(.05,_TextureMetres);
        float3 colour=SurfaceArray(TEXTURE2D_ARRAY_ARGS(_GroundAlbedo,sampler_GroundAlbedo),world*scale,worldDx*scale,worldDy*scale,SurfaceAxes(planetNormal,2,1),1).rgb;
        #ifndef UNITY_COLORSPACE_GAMMA
        colour*=4.672;
        #else
        colour*=2;
        #endif
        albedo*=lerp(1,colour,extension*.88);
    }
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
    float4 forestSurface=0;float3 floorMix=0;
    if(_ForestSurfaceEnabled>.5&&textures&&!TerrainTest(TD_FOREST))
    {
        float2 uv=i.coverageUV*(1-_ForestSurface_TexelSize.xy)+.5*_ForestSurface_TexelSize.xy;
        forestSurface=SAMPLE_TEXTURE2D(_ForestSurface,sampler_ForestSurface,uv);
        if(_ForestFloorEnabled>.5&&detail>.001)
        {
            float4 floorSurface=SAMPLE_TEXTURE2D(_ForestFloor,sampler_ForestFloor,uv);
            floorMix=floorSurface.rgb*smoothstep(.05,.65,floorSurface.a);
        }
    }
    float3 floorPigment=float3(.40,.29,.18)*floorMix.x+float3(.34,.27,.17)*floorMix.y+float3(.31,.29,.18)*floorMix.z;
    float floorWeight=saturate(dot(floorMix,1));
    floorPigment/=max(.001,floorWeight);
    #ifndef UNITY_COLORSPACE_GAMMA
    floorPigment=SRGBToLinear(floorPigment);
    #endif
    float looseFraction=saturate((i.weights.x+i.weights.z+i.weights.w+i.groundB.y)/max(.001,dot(max(i.weights,0),1)+dot(max(i.groundB,0),1)));
    albedo=lerp(albedo,floorPigment,floorWeight*detail*looseFraction);
    // Stable mean properties of these rough, nonmetallic ground materials.
    // Fine mask variation is reserved for the close High/Ultra path.
    float ao=.93,rough=lerp(.88,.55,saturate(i.groundB.w)),metal=0;
    [branch] if(textures&&detail>.001)
    {
        int3 layers;float3 fractions;SurfaceLayers(i,extra,floorMix,layers,fractions);
        float3 np=n.x*TerrainEast().xyz+n.y*TerrainUp().xyz+n.z*TerrainNorth().xyz;
        float axes=TerrainTest(TD_AXIS)?1:_TerrainSurfaceControls.w;
        float3 w=SurfaceAxes(np,axes,extra),colour=0,bump=0;float4 mask=0;
        bool colourRead=!TerrainTest(TD_ALBEDO),normalRead=normalDetail>.001&&_TerrainSurfaceControls.y>=1&&!TerrainTest(TD_NORMALS);
        bool maskRead=normalDetail>.001&&_TerrainSurfaceControls.y>=3&&!TerrainTest(TD_MASKS);
        [unroll]for(int at=0;at<3;at++)
        {
            [branch] if(fractions[at]>.001)
            {
                // Sand is a registered4m scan crop; other material slots keep
                // their existing2m repeat. No additional texture layer/read.
                float layerScale=layers[at]==1?.5:1;
                // Registered scan dimensions: broadleaf/needles2m, damp3m.
                if(layers[at]==16)layerScale*=2/3.0;
                if(layers[at]==10)layerScale*=2/1.83;
                if(layers[at]==11)layerScale*=2/4.3;
                if(layers[at]==13)layerScale*=2/2.4212;
                float3 lp=p*layerScale,ldx=dx*layerScale,ldy=dy*layerScale;
                if(colourRead)colour+=SurfaceArray(TEXTURE2D_ARRAY_ARGS(_GroundAlbedo,sampler_GroundAlbedo),lp,ldx,ldy,w,layers[at]).rgb*fractions[at];
                if(normalRead)bump+=SurfaceBump(lp,ldx,ldy,w,layers[at])*fractions[at];
                if(maskRead)mask+=SurfaceArray(TEXTURE2D_ARRAY_ARGS(_GroundMasks,sampler_GroundAlbedo),lp,ldx,ldy,w,layers[at])*fractions[at];
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
    SurfaceStrata(i,albedo);
    if(textures)SurfaceSandDistance(i,world,worldDx,worldDy,distance,detail,forestSurface.r,albedo);
    if(textures)SurfaceRockWeathering(i,world,worldDx,worldDy,albedo);
    SurfaceSaltCrust(i,world,distance,farColor,albedo,n,rough);
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
    Light sun=(Light)0;float transmission=0;half canopyShade=1;
    if(!TerrainTest(TD_UNLIT)){sun=WorldGenFoliageLight(i.position,canopyShade);transmission=pow(saturate(dot(-sun.direction,input.viewDirectionWS)),3)*.22;}
    half3 color;
    if(TerrainTest(TD_UNLIT))color=albedo;
    else if(TerrainTest(TD_SIMPLE))color=SurfaceLeafLight(albedo,n,sun,transmission,canopyShade);
    else color=WorldGenCanopyPbr(UniversalFragmentPBR(input,s).rgb,albedo,TerrainAmbient().rgb,ao,canopyShade);

    // Ecological weights/tints remain at every distance. Only their microscopic
    // material textures retire; these few weights are NOT an 850m texture stack.
    float4 flora=float4(cover,0,0,cover);
    if(_FloraSurfaceEnabled>.5&&textures)flora=SAMPLE_TEXTURE2D(_FloraSurface,sampler_FloraSurface,i.coverageUV*(1-_FloraSurface_TexelSize.xy)+.5*_FloraSurface_TexelSize.xy);
    float3 mix=max(0,float3(flora.a/.45,flora.g,flora.b/.45))*_GroundFlora;
    // Low plants/moss can coexist with litter. The old full-opacity green mat
    // erased every soil texture under a canopy even when stems were sparse.
    mix*=1-floorWeight*float3(.85,.25,.35);
    // The damp forest scan already contains real moss cushions interspersed
    // with litter. A second opaque moss field erased that photographed layout
    // and replaced it with large, smooth green islands.
    mix.y*=1-saturate(floorMix.z);
    // Ground colour follows the continuous terrain normal. A face derivative
    // here made each individual triangle a different grass/soil material.
    // Physical stems retain their separate geometric cliff/rooting test.
    mix.x*=smoothstep(.62,.86,abs(dot(normalize(i.normal),float3(0,1,0))));
    if(mix.y>.001)mix.y=SurfaceMossPatch(world,saturate(mix.y),detail);
    mix/=max(1,dot(mix,1));
    if(TerrainTest(TD_GRASS))mix.x=0;if(TerrainTest(TD_MOSS))mix.y=0;if(TerrainTest(TD_LICHEN))mix.z=0;
    float3 axes=SurfaceAxes(TerrainUp().xyz,TerrainTest(TD_AXIS)?1:_TerrainSurfaceControls.w,extra);
    float3 vegetation=0;
    [branch] if(mix.x>.001)
    {
        float3 mat=1;
        float scanScale=2;
        #ifndef UNITY_COLORSPACE_GAMMA
        scanScale=4.672;
        #endif
        if(textures&&detail>.001)mat=lerp(mat,SurfaceMat(TEXTURE2D_ARGS(_GrassField,sampler_GrassField),world/2,worldDx/2,worldDy/2,axes)*scanScale,detail);
        float tint=.9111;
        #ifndef UNITY_COLORSPACE_GAMMA
        tint=.815;
        #endif
        vegetation+=SurfaceLeafLight(farColor*(tint*mat),normalize(i.normal),sun,transmission,canopyShade)*mix.x;
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
            if(textures&&detail>.001)mat=lerp(mat,SurfaceMat(TEXTURE2D_ARGS(_MossField,sampler_MossField),world/.45,worldDx/.45,worldDy/.45,axes),detail);
            // Reuse the photographed relief to break up the cushion boundary;
            // smooth analytic contours alone looked like airbrushed green ovals.
            // No extra texture read, and the other cover weights stay bounded.
            float edge=smoothstep(.2,.8,mix.y+(mat.r-.5)*.8);
            mix.y=lerp(mix.y,min(edge,max(0,1-mix.x-mix.z)),detail);
            float3 normal=normalize(n+float3(mat.g-.5,0,mat.b-.5)*1.3);
            vegetation+=moss*(mat.r*2)*SurfaceMatLight(normal,sun,canopyShade)*mix.y;
        }
        if(mix.z>.001)
        {
            float3 mat=.5;
            if(textures&&detail>.001)mat=lerp(mat,SurfaceMat(TEXTURE2D_ARGS(_LichenField,sampler_LichenField),world/.8,worldDx/.8,worldDy/.8,axes),detail);
            float3 normal=normalize(n+float3(mat.g-.5,0,mat.b-.5));
            vegetation+=lerp(farColor,lichen,.55)*(mat.r*2)*SurfaceMatLight(normal,sun,canopyShade)*mix.z;
        }
    }
    color=color*(1-dot(mix,1))+vegetation;
    if(_ForestSurfaceEnabled>.5&&TerrainForestEnabled()>.5&&textures&&!TerrainTest(TD_FOREST))
    {
        float4 forest=forestSurface;
        float3 leaves=forest.rgb;
        #ifndef UNITY_COLORSPACE_GAMMA
        leaves=SRGBToLinear(leaves);
        #endif
        color=lerp(color,SurfaceLeafLight(leaves,n,sun,transmission,canopyShade),saturate(forest.a)*smoothstep(700,1800,distance)*.8);
    }
    if(_MidSurfaceEnabled>.5&&textures&&!TerrainTest(TD_MID))
    {
        float4 mid=SAMPLE_TEXTURE2D(_MidSurface,sampler_MidSurface,i.coverageUV*(1-_MidSurface_TexelSize.xy)+.5*_MidSurface_TexelSize.xy);
        float3 leaves=mid.rgb;
        #ifndef UNITY_COLORSPACE_GAMMA
        leaves=SRGBToLinear(leaves);
        #endif
        color=lerp(color,SurfaceLeafLight(leaves,n,sun,transmission,canopyShade),saturate(mid.a)*smoothstep(180,1000,distance));
    }
    if(!TerrainTest(TD_UNLIT))color+=SecondaryCelestialLight(n,albedo)+CampDiffuseLight(i.position,n,albedo);
    return half4(TerrainTestAir(color,i.position,_WorldSpaceCameraPos,fogColor,TerrainFogDistance()),1);
}
#endif
