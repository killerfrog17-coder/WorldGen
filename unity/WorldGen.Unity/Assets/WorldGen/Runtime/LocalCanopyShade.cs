using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    // A bounded shelter summary, not another tree population or shadow camera.
    // Cached actual crowns are independent of camera heading and mesh/view LOD.
    const int CanopySide=256;const float CanopySpan=1536,CanopyStep=CanopySpan/CanopySide;
    readonly Texture2D[] canopyTextures=new Texture2D[2];
    readonly Vector4[] canopyRects=new Vector4[2];
    readonly List<ScatterChunk> canopyChunks=new List<ScatterChunk>();
    readonly Dictionary<int,float> canopyBases=new Dictionary<int,float>();
    Color[] canopyPixels;LocalTerrainFrame canopyFrame;
    int canopyFront=-1,canopyBuild,canopyPhase,canopyChunk,canopyRoot,canopyFinish;
    int canopySourceRevision,canopyCapturedRevision,canopyBuiltRevision=-1,canopyOptions=-1;
    Vector2 canopyAnchor,canopyBuiltAnchor;double canopyBlendStarted,nextCanopyBuild,canopyOriginX,canopyOriginZ;
    bool CanopyActive=>TerrainVideoOptions.CanopyShade&&UseTreeShadowProxies&&ScatterEnabled&&localSun!=null&&localSun.shadows!=UnityEngine.LightShadows.None&&localSun.intensity>0;
    public bool CanopyShadeReady=>canopyFront>=0;
    public bool CanopyShadeBuilding=>canopyPhase!=0;
    void UpdateCanopyShade()
    {
        if(!CanopyActive||view==null||frame==null||mapOpen)return;
        if(!ReferenceEquals(canopyFrame,frame))
        {
            canopyFrame=frame;canopyPhase=0;canopyFront=-1;canopyBuiltRevision=-1;canopyChunks.Clear();nextCanopyBuild=0;
            canopyRects[0]=canopyRects[1]=Vector4.zero;canopyOriginX=originX;canopyOriginZ=originZ;
        }
        // Floating-origin changes also invalidate local-height/position data.
        if(canopyOriginX!=originX||canopyOriginZ!=originZ)
        {
            for(int i=0;i<2;i++){canopyRects[i].x-=(float)(originX-canopyOriginX);canopyRects[i].y-=(float)(originZ-canopyOriginZ);}
            canopyOriginX=originX;canopyOriginZ=originZ;canopyPhase=0;canopyBuiltRevision=-1;nextCanopyBuild=0;
        }
        var eye=view.transform.position;
        var anchor=new Vector2(Mathf.Floor(eye.x/128)*128-CanopySpan*.5f,Mathf.Floor(eye.z/128)*128-CanopySpan*.5f);
        int options=TerrainVideoOptions.TreeDensity.GetHashCode();
        if(canopyPhase==0)
        {
            if(Time.realtimeSinceStartupAsDouble<nextCanopyBuild||
                (canopyBuiltRevision==canopySourceRevision&&canopyBuiltAnchor==anchor&&canopyOptions==options))return;
            canopyAnchor=anchor;canopyCapturedRevision=canopySourceRevision;canopyOptions=options;
            canopyBuild=canopyFront==0?1:0;
            if(canopyPixels==null)canopyPixels=new Color[CanopySide*CanopySide];else Array.Clear(canopyPixels,0,canopyPixels.Length);
            canopyChunks.Clear();foreach(var chunk in scatterChunks.Values)canopyChunks.Add(chunk);
            canopyChunk=canopyRoot=canopyFinish=0;canopyPhase=1;
        }
        long started=Stopwatch.GetTimestamp();double limit=Stopwatch.Frequency*.0006;
        if(canopyPhase==1)
        {
            int examined=0;
            while(canopyChunk<canopyChunks.Count&&examined<1024)
            {
                var chunk=canopyChunks[canopyChunk];
                if(chunk.Roots==null||canopyRoot>=chunk.Source.Instances.Length){canopyChunk++;canopyRoot=0;continue;}
                int index=canopyRoot++;var plant=chunk.Source.Instances[index];examined++;
                if(plant.Kind==ScatterKind.GeneratedTree&&TerrainVideoOptions.KeepVegetation(plant.Kind,plant.Id))RasterCanopyCrown(chunk,index,plant);
                if((examined&7)==0&&Stopwatch.GetTimestamp()-started>limit)break;
            }
            if(canopyChunk>=canopyChunks.Count){canopyPhase=2;canopyChunks.Clear();}
        }
        if(canopyPhase==2)
        {
            int end=Math.Min(canopyPixels.Length,canopyFinish+2048);
            for(;canopyFinish<end;canopyFinish++)
            {
                var p=canopyPixels[canopyFinish];
                if(p.b>0){float coverage=1-Mathf.Exp(-p.b);float scale=coverage/p.b;p.r*=scale;p.g*=scale;p.b=coverage;}
                canopyPixels[canopyFinish]=p;
            }
            if(canopyFinish==canopyPixels.Length)canopyPhase=3;
        }
        if(canopyPhase==3)
        {
            if(canopyTextures[canopyBuild]==null)canopyTextures[canopyBuild]=new Texture2D(CanopySide,CanopySide,TextureFormat.RGBAHalf,false,true){name="Placed-tree canopy shelter",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            canopyTextures[canopyBuild].SetPixels(canopyPixels);canopyTextures[canopyBuild].Apply(false,false);
            canopyRects[canopyBuild]=new Vector4(canopyAnchor.x,canopyAnchor.y,1/CanopySpan,1);
            canopyFront=canopyBuild;canopyBlendStarted=Time.realtimeSinceStartupAsDouble;canopyBuiltAnchor=canopyAnchor;canopyBuiltRevision=canopyCapturedRevision;
            canopyPhase=0;nextCanopyBuild=Time.realtimeSinceStartupAsDouble+1;
        }
    }
    void RasterCanopyCrown(ScatterChunk chunk,int index,ScatterInstance plant)
    {
        var root=transform.TransformPoint(chunk.Roots[index]);float radius=Mathf.Max(.5f,(float)plant.WidthM*.55f),height=(float)plant.HeightM;
        if(root.x+radius<canopyAnchor.x||root.z+radius<canopyAnchor.y||root.x-radius>canopyAnchor.x+CanopySpan||root.z-radius>canopyAnchor.y+CanopySpan)return;
        int code=TreeFlora.RenderCode(plant.Variant);
        if(!canopyBases.TryGetValue(code,out float bottom))
        {
            var morphology=new TreeMorphology(code);
            bottom=morphology.Form==TreeGrowthForm.NeedleCrown?Mathf.Clamp(.11f+(float)morphology.CrownBase*.20f,.14f,.23f):(float)morphology.CrownBase;
            // Bounded cache: these scalar traits are cheap to reconstruct when travelling.
            if(canopyBases.Count<512)canopyBases.Add(code,bottom);
        }
        int x0=Mathf.Clamp(Mathf.FloorToInt((root.x-radius-canopyAnchor.x)/CanopyStep),0,CanopySide-1),x1=Mathf.Clamp(Mathf.CeilToInt((root.x+radius-canopyAnchor.x)/CanopyStep),0,CanopySide-1);
        int z0=Mathf.Clamp(Mathf.FloorToInt((root.z-radius-canopyAnchor.y)/CanopyStep),0,CanopySide-1),z1=Mathf.Clamp(Mathf.CeilToInt((root.z+radius-canopyAnchor.y)/CanopyStep),0,CanopySide-1);
        // Include a fractional texel footprint so narrow crowns don't fall
        // between sample centres. Its area weight avoids inflating sparse trees.
        float footprint=Mathf.Max(radius,CanopyStep*.75f),area=Mathf.Min(1,radius*radius/(footprint*footprint));
        for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
        {
            float dx=(canopyAnchor.x+(x+.5f)*CanopyStep-root.x)/footprint,dz=(canopyAnchor.y+(z+.5f)*CanopyStep-root.z)/footprint;
            float weight=Mathf.Max(0,1-dx*dx-dz*dz)*area*1.8f;if(weight<=0)continue;
            int at=z*CanopySide+x;var p=canopyPixels[at];p.r+=(root.y+height)*weight;p.g+=(root.y+height*bottom)*weight;p.b+=weight;canopyPixels[at]=p;
        }
    }
    void BindCanopyShade(Camera camera)
    {
        bool active=camera==view&&CanopyActive&&canopyFront>=0;
        int front=Math.Max(0,canopyFront),previous=1-front;
        bool soft=TerrainVideoOptions.DistantTreeShadowCards;
        Shader.SetGlobalVector("_WorldGenCanopySettings",new Vector4(active?1:0,(float)Math.Min(1,(Time.realtimeSinceStartupAsDouble-canopyBlendStarted)/.5),soft?-1:90,soft?0:160));
        Shader.SetGlobalTexture("_WorldGenCanopyPrevious",canopyTextures[previous]??Texture2D.blackTexture);
        Shader.SetGlobalTexture("_WorldGenCanopyCurrent",canopyTextures[front]??Texture2D.blackTexture);
        Shader.SetGlobalVector("_WorldGenCanopyRect0",canopyRects[previous]);Shader.SetGlobalVector("_WorldGenCanopyRect1",canopyRects[front]);
    }
    void DisposeCanopyShade()
    {
        foreach(var texture in canopyTextures)if(texture!=null)Destroy(texture);
        canopyTextures[0]=canopyTextures[1]=null;canopyChunks.Clear();canopyBases.Clear();canopyPixels=null;canopyFront=-1;canopyPhase=0;
        Shader.SetGlobalVector("_WorldGenCanopySettings",Vector4.zero);
    }
}
