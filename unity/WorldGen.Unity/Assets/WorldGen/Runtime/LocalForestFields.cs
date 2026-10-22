using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    [StructLayout(LayoutKind.Sequential)]
    struct ForestRoot
    {public Vector3 Position;public float Width,Height,Yaw,SlopeX,SlopeZ;public Vector3 Tint;public uint Field;}
    [StructLayout(LayoutKind.Sequential)]
    struct ForestHash {public int X,Z,Level,State;}
    sealed class ForestBatch
    {
        public ArtModel Model;public V Pigment;public readonly List<ForestRoot> Data=new List<ForestRoot>();public readonly Stack<int> Free=new Stack<int>();
        public GraphicsBuffer Roots,Visible,Args;public int Capacity,DirtyLow=int.MaxValue,DirtyHigh;public MaterialPropertyBlock Properties=new MaterialPropertyBlock();
        public void Dirty(int at){DirtyLow=Math.Min(DirtyLow,at);DirtyHigh=Math.Max(DirtyHigh,at+1);}
        public void Dispose(){Roots?.Dispose();Visible?.Dispose();Args?.Dispose();}
    }
    sealed class ForestField
    {
        public TerrainTileKey Key;public Tile Tile;public int Id,Pitch;public Bounds Bounds;
        public readonly Dictionary<ForestBatch,List<int>> Roots=new Dictionary<ForestBatch,List<int>>();
        public bool Ready=>Roots.Keys.All(b=>b.Model.TreeBaked);
    }
    readonly Dictionary<TerrainTileKey,ForestField> forestFields=new Dictionary<TerrainTileKey,ForestField>();
    readonly Dictionary<int,ForestBatch> forestBatches=new Dictionary<int,ForestBatch>();
    readonly Stack<int> forestFreeIds=new Stack<int>();int forestNextId,forestRootCount,forestHashMask;
    readonly int[] forestFlags=new int[8192];GraphicsBuffer forestFlagBuffer,forestHashBuffer;
    ComputeShader forestCull;bool forestHashDirty;LocalTerrainFrame forestFrame;double nextForestBuild,nextForestVisibility;
    readonly Vector4[] forestPlanes=new Vector4[6];readonly Plane[] forestPlaneWork=new Plane[6];
    public int ForestFieldCount=>forestFields.Count;
    public int ForestPacketCount=>forestRootCount;
    public int ForestDrawCalls {get;private set;}
    static uint ForestMix(uint x){x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;return x^(x>>16);}
    static uint ForestKeyHash(int x,int z,int level)=>ForestMix(unchecked((uint)x*73856093u^(uint)z*19349663u^(uint)level*83492791u));
    ArtModel ForestArt(int code)
    {
        long key=ScatterArtKey(ScatterKind.GeneratedTree,code,2)|(1L<<49);
        if(scatterArt.TryGetValue(key,out var model))return model;
        model=CreateRebuiltTree(key,code,2);model.TreeStand=true;return model;
    }
    void UpdateForestFields()
    {
        if(!ReferenceEquals(forestFrame,frame)){ResetForestFields();forestFrame=frame;}
        if(!TerrainVideoOptions.TreeRebuild||!TerrainVideoOptions.TreeImpostors||!TerrainVideoOptions.FarForest||!Ready||!CenterReady)return;
        if(forestCull==null){forestCull=Resources.Load<ComputeShader>("WorldGenForestCull");if(forestCull==null||!SystemInfo.supportsComputeShaders)return;forestFlagBuffer=new GraphicsBuffer(GraphicsBuffer.Target.Structured,forestFlags.Length,4);}
        double now=Time.realtimeSinceStartupAsDouble;
        foreach(var batch in forestBatches.Values)batch.Model.LastUsed=now;
        if(now<nextForestBuild)return;nextForestBuild=now+.025;
        var eye=view.transform.localPosition;
        // Fields own an immutable height/cover snapshot. Replacing a terrain
        // object is NOT a reason to delete its still-valid forest. Retain an
        // obsolete fallback until a ready ancestor or four ready children can
        // cover it; GPU ownership suppresses overlap. No remove/reseed cycle
        // merely because the camera crossed a distance band.
        foreach(var key in forestFields.Keys.Where(k=>!tiles.ContainsKey(k)).ToArray())
        {
            var bounds=forestFields[key].Bounds;bounds.center-=new Vector3((float)originX,0,(float)originZ);
            if(bounds.SqrDistance(eye)>(viewDistance+2048)*(viewDistance+2048)||ForestReplacementReady(key))RemoveForestField(key);
        }
        if(NearTerrainPending()||forestRootCount>=524288)return;
        // Install one bounded field per update, nearest missing first. Tile
        // resolution never caps nearby tree density or regenerates local trees.
        TerrainTileKey selected=default;Tile source=null;double best=double.MaxValue;
        float near=Math.Max(1,TerrainVideoOptions.TreeIdentityRange*.60f);
        foreach(var pair in tiles)
        {
            if(forestFields.ContainsKey(pair.Key)||pair.Value.ForestCover==null||pair.Value.ForestCover.Length==0)continue;
            double x=pair.Key.X*pair.Key.SizeM-originX,z=pair.Key.Z*pair.Key.SizeM-originZ,size=pair.Key.SizeM;
            double dx=Math.Max(Math.Max(x-eye.x,eye.x-x-size),0),dz=Math.Max(Math.Max(z-eye.z,eye.z-z-size),0),distance=dx*dx+dz*dz;
            double farX=Math.Max(Math.Abs(x-eye.x),Math.Abs(x+size-eye.x)),farZ=Math.Max(Math.Abs(z-eye.z),Math.Abs(z+size-eye.z));
            if(farX*farX+farZ*farZ<near*near||distance>viewDistance*viewDistance)continue;
            if(distance<best){best=distance;selected=pair.Key;source=pair.Value;}
        }
        if(source==null)return;
        if(forestFreeIds.Count==0&&forestNextId==forestFlags.Length)return;
        int id=forestFreeIds.Count>0?forestFreeIds.Pop():forestNextId++;
        var field=new ForestField{Key=selected,Tile=source,Id=id};forestFields.Add(selected,field);
        BuildForestField(field);forestHashDirty=true;nextForestVisibility=0;
    }
    int ForestPitch(TerrainTileKey key,Vector3 eye)
    {
        double size=key.SizeM;
        int pitch=32;
        // Bound huge fallback products; normal nearby products use32m packets.
        while(size/pitch>64)pitch*=2;return pitch;
    }
    bool ForestReplacementReady(TerrainTileKey key)
    {
        for(int level=key.Level+1;level<=16;level++)
        {
            int shift=level-key.Level;var parent=new TerrainTileKey(key.X>>shift,key.Z>>shift,level,HorizontalScale);
            if(forestFields.TryGetValue(parent,out var field)&&field.Ready&&tiles.ContainsKey(parent))return true;
        }
        if(key.Level==0)return false;
        for(int z=0;z<2;z++)for(int x=0;x<2;x++)
        {
            var child=new TerrainTileKey(key.X*2+x,key.Z*2+z,key.Level-1,HorizontalScale);
            if(!forestFields.TryGetValue(child,out var field)||!field.Ready)return false;
        }
        return true;
    }
    void BuildForestField(ForestField field)
    {
        var tile=field.Tile;var key=field.Key;double size=key.SizeM,east=key.X*size,north=key.Z*size;
        int pitch=field.Pitch=ForestPitch(key,view.transform.localPosition);
        int n=tile.Segments,side=n+1;float low=float.MaxValue,high=float.MinValue,maxTree=0;
        Span<int> corners=stackalloc int[4];Span<double> weights=stackalloc double[4];
        foreach(float y in tile.Heights){low=Math.Min(low,y);high=Math.Max(high,y);}
        double Value(int a,int b,double u,double v,int channel)
        {
            int p=(b*side+a)*8;return tile.ForestCover[p+channel]*(1-u)*(1-v)+tile.ForestCover[p+8+channel]*u*(1-v)+tile.ForestCover[p+side*8+channel]*(1-u)*v+tile.ForestCover[p+(side+1)*8+channel]*u*v;
        }
        for(int z=(int)Math.Floor(north/pitch);z<(int)Math.Ceiling((north+size)/pitch);z++)for(int x=(int)Math.Floor(east/pitch);x<(int)Math.Ceiling((east+size)/pitch);x++)
        {
            uint hash=ForestKeyHash(x,z,pitch);float unit=(hash&0xffffff)/16777216f;
            double px=(x+.5+(((hash>>8)&255)/255.0-.5)*.38)*pitch,pz=(z+.5+(((hash>>16)&255)/255.0-.5)*.38)*pitch;
            if(px<east||px>=east+size||pz<north||pz>=north+size)continue;
            double gx=(px-east)/size*n,gz=(pz-north)/size*n;int a=Math.Min(n-1,(int)gx),b=Math.Min(n-1,(int)gz);double u=gx-a,v=gz-b;
            float cover=(float)Value(a,b,u,v,3);if(cover<.025f||unit>Math.Sqrt(cover))continue;
            // Select a discrete source anatomy, never interpolate packed bits.
            int strongest=0;double strongestWeight=-1;corners[0]=b*side+a;corners[1]=b*side+a+1;corners[2]=(b+1)*side+a;corners[3]=(b+1)*side+a+1;weights[0]=(1-u)*(1-v);weights[1]=u*(1-v);weights[2]=(1-u)*v;weights[3]=u*v;
            for(int k=0;k<4;k++){double weight=weights[k]*tile.ForestCover[corners[k]*8+3];if(weight>strongestWeight){strongestWeight=weight;strongest=corners[k];}}
            int code=BitConverter.SingleToInt32Bits(tile.ForestCover[strongest*8+7])&0x70ffff00;
            if(((code>>28)&7)>5)continue;
            if(!forestBatches.TryGetValue(code,out var batch)){batch=new ForestBatch{Model=ForestArt(code),Pigment=new TreeMorphology(code).Leaf};forestBatches.Add(code,batch);}
            int h=b*side+a;float h00=tile.Heights[h],h10=tile.Heights[h+1],h01=tile.Heights[h+side],h11=tile.Heights[h+side+1];
            float height=(float)(u+v<=1?h00+(h10-h00)*u+(h01-h00)*v:h11+(h01-h11)*(1-u)+(h10-h11)*(1-v));
            var pigment=batch.Pigment;
            float slopeX=(float)((u+v<=1?h10-h00:h11-h01)/(size/n)),slopeZ=(float)((u+v<=1?h01-h00:h11-h10)/(size/n));
            var root=new ForestRoot{Position=new Vector3((float)px,height,(float)pz),Width=pitch/1.7f,Height=(float)Value(a,b,u,v,4),Yaw=unit*Mathf.PI*2,SlopeX=slopeX,SlopeZ=slopeZ,Field=(uint)field.Id|((uint)key.Level<<16),
                Tint=new Vector3(Mathf.Clamp((float)(Value(a,b,u,v,0)/Math.Max(.02,pigment.X)),.65f,1.4f),Mathf.Clamp((float)(Value(a,b,u,v,1)/Math.Max(.02,pigment.Y)),.65f,1.4f),Mathf.Clamp((float)(Value(a,b,u,v,2)/Math.Max(.02,pigment.Z)),.65f,1.4f))};
            if(root.Height<1)continue;maxTree=Math.Max(maxTree,root.Height);
            int slot;if(batch.Free.Count>0){slot=batch.Free.Pop();batch.Data[slot]=root;}else{slot=batch.Data.Count;batch.Data.Add(root);}batch.Dirty(slot);forestRootCount++;
            if(!field.Roots.TryGetValue(batch,out var slots)){slots=new List<int>();field.Roots.Add(batch,slots);}slots.Add(slot);
        }
        field.Bounds=new Bounds(new Vector3((float)(east+size*.5),(low+high+maxTree)*.5f,(float)(north+size*.5)),new Vector3((float)size+pitch*3,high-low+maxTree*3,(float)size+pitch*3));
    }
    void RemoveForestField(TerrainTileKey key)
    {
        var field=forestFields[key];foreach(var pair in field.Roots)foreach(int slot in pair.Value){pair.Key.Data[slot]=default;pair.Key.Free.Push(slot);pair.Key.Dirty(slot);forestRootCount--;}
        forestFlags[field.Id]=0;forestFreeIds.Push(field.Id);forestFields.Remove(key);forestHashDirty=true;nextForestVisibility=0;
    }
    void PrepareForestHash()
    {
        if(!forestHashDirty)return;forestHashDirty=false;int count=16;while(count<forestFields.Count*4)count*=2;
        var hash=new ForestHash[count];forestHashMask=count-1;
        foreach(var field in forestFields.Values)
        {
            if(!field.Ready)continue;int slot=(int)(ForestKeyHash(field.Key.X,field.Key.Z,field.Key.Level)&(uint)forestHashMask);
            while(hash[slot].State!=0)slot=(slot+1)&forestHashMask;hash[slot]=new ForestHash{X=field.Key.X,Z=field.Key.Z,Level=field.Key.Level,State=1};
        }
        if(forestHashBuffer==null||forestHashBuffer.count!=count){forestHashBuffer?.Dispose();forestHashBuffer=new GraphicsBuffer(GraphicsBuffer.Target.Structured,count,16);}
        forestHashBuffer.SetData(hash);nextForestVisibility=0;
    }
    void DrawForestFields()
    {
        ForestDrawCalls=0;if(!TerrainVideoOptions.TreeRebuild||!TerrainVideoOptions.TreeImpostors||!TerrainVideoOptions.FarForest||forestCull==null||view==null||forestFields.Count==0)return;
        PrepareForestHash();var eye=view.transform.localPosition;
        if(Time.realtimeSinceStartupAsDouble>=nextForestVisibility)
        {
            nextForestVisibility=Time.realtimeSinceStartupAsDouble+.20;
            foreach(var field in forestFields.Values)
            {var bounds=field.Bounds;bounds.center-=new Vector3((float)originX,0,(float)originZ);bounds.Expand(12+(bounds.center-eye).magnitude*.04f);bool occluded=field.Ready&&TerrainOccluded(bounds,eye);forestFlags[field.Id]=field.Ready&&!occluded?1:0;}
            forestFlagBuffer.SetData(forestFlags);
        }
        GeometryUtility.CalculateFrustumPlanes(view,forestPlaneWork);for(int i=0;i<6;i++)forestPlanes[i]=new Vector4(forestPlaneWork[i].normal.x,forestPlaneWork[i].normal.y,forestPlaneWork[i].normal.z,forestPlaneWork[i].distance);
        float reach=TerrainVideoOptions.TreeIdentityRange,start=(float)VegetationQuality.ForestStart(reach);
        forestCull.SetVectorArray("_Planes",forestPlanes);forestCull.SetVector("_Eye",eye);forestCull.SetVector("_Chart",new Vector4((float)originX,0,(float)originZ,(float)(TerrainLod.BaseSizeM*HorizontalScale)));
        forestCull.SetVector("_Ranges",new Vector4(start,reach,(float)viewDistance,forestHashMask));forestCull.SetBuffer(0,"_Fields",forestFlagBuffer);forestCull.SetBuffer(0,"_Tiles",forestHashBuffer);
        foreach(var batch in forestBatches.Values)
        {
            var model=batch.Model;model.LastUsed=Time.realtimeSinceStartupAsDouble;if(!model.TreeBaked||batch.Data.Count==0)continue;
            if(batch.Capacity<batch.Data.Count)
            {
                batch.Dispose();batch.Capacity=Mathf.NextPowerOfTwo(Math.Max(64,batch.Data.Count));batch.Roots=new GraphicsBuffer(GraphicsBuffer.Target.Structured,batch.Capacity,48);batch.Visible=new GraphicsBuffer(GraphicsBuffer.Target.Append,batch.Capacity,4);batch.Args=new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,1,GraphicsBuffer.IndirectDrawArgs.size);
                batch.Args.SetData(new[]{new GraphicsBuffer.IndirectDrawArgs{vertexCountPerInstance=6}});batch.DirtyLow=0;batch.DirtyHigh=batch.Data.Count;
            }
            if(batch.DirtyLow<batch.DirtyHigh){batch.Roots.SetData(batch.Data,batch.DirtyLow,batch.DirtyLow,batch.DirtyHigh-batch.DirtyLow);batch.DirtyLow=int.MaxValue;batch.DirtyHigh=0;}
            batch.Visible.SetCounterValue(0);forestCull.SetInt("_RootCount",batch.Data.Count);forestCull.SetBuffer(0,"_Roots",batch.Roots);forestCull.SetBuffer(0,"_Visible",batch.Visible);forestCull.Dispatch(0,(batch.Data.Count+63)/64,1,1);GraphicsBuffer.CopyCount(batch.Visible,batch.Args,4);
            var props=batch.Properties;props.SetBuffer("_Roots",batch.Roots);props.SetBuffer("_Visible",batch.Visible);props.SetVector("_ForestChart",new Vector4((float)originX,0,(float)originZ,0));props.SetVector("_ForestRange",new Vector4(start,reach,0,0));
            var material=model.Parts[0].Material;var parameters=new RenderParams(material){camera=view,layer=30,worldBounds=new Bounds(eye,Vector3.one*(float)viewDistance*2.2f),matProps=props,shadowCastingMode=ShadowCastingMode.Off,receiveShadows=true};
            Graphics.RenderPrimitivesIndirect(in parameters,MeshTopology.Triangles,batch.Args,1);ForestDrawCalls++;
        }
    }
    void ResetForestFields()
    {
        foreach(var batch in forestBatches.Values)batch.Dispose();forestBatches.Clear();forestFields.Clear();forestFreeIds.Clear();forestRootCount=forestNextId=0;
        forestFlagBuffer?.Dispose();forestHashBuffer?.Dispose();forestFlagBuffer=forestHashBuffer=null;forestCull=null;Array.Clear(forestFlags,0,forestFlags.Length);forestHashDirty=true;nextForestBuild=nextForestVisibility=0;
    }
}
