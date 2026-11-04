using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    const int CampMaskSide=1024;
    Texture2D campClearingTexture;byte[] campClearingPixels;double campMaskX,campMaskZ,campMaskCheckAt;int campMaskSignature=int.MinValue,campMaskCount;
    LocalTerrainFrame campMaskFrame;
    sealed class GroundFootprint{public CampObject Source;public double X,Z,W,D,Heading,Strength;public bool Fire;}
    readonly List<GroundFootprint> campFootprints=new List<GroundFootprint>();
    void UpdateCampClearing()
    {
        if(frame==null||Time.unscaledTimeAsDouble<campMaskCheckAt)return;campMaskCheckAt=Time.unscaledTimeAsDouble+1;
        double minX=Math.Floor(motion.X/64)*64-512,minZ=Math.Floor(motion.Z/64)*64-512;
        int signature=17;campFootprints.Clear();double today=LivingClock.CalendarDay;
        foreach(var obj in Camp.Objects)
        {
            if(obj.Kind!=CampKind.Blueprint&&obj.Kind!=CampKind.Fire&&obj.Kind!=CampKind.Bedding||HumanDistance(obj.Position,Position)>800)continue;
            TerrainHandoff.Coordinates(frame,obj.Position,out double x,out double z);double strength=1;
            if(obj.NpcSite!=0)
            {
                var memory=LivingState.Camps.Find(c=>c.Site==obj.NpcSite);
                if(memory!=null&&memory.AbandonedCalendarDay>=0)strength=Math.Max(0,1-(today-memory.AbandonedCalendarDay)/150);
            }
            if(strength<=0)continue;
            double w=obj.Blueprint?.WidthM??(obj.ClearedWidthM>0?obj.ClearedWidthM:obj.Kind==CampKind.Fire?3:2),d=obj.Blueprint?.DepthM??(obj.ClearedDepthM>0?obj.ClearedDepthM:obj.Kind==CampKind.Fire?3:1.5);
            campFootprints.Add(new GroundFootprint{Source=obj,X=x,Z=z,W=w,D=d,Heading=obj.Heading,Strength=strength,Fire=obj.Kind==CampKind.Fire});
            unchecked{signature=signature*31+obj.Id.GetHashCode();signature=signature*31+(int)Math.Round(x*10);signature=signature*31+(int)Math.Round(z*10);signature=signature*31+(int)Math.Round(w*100+d*100+obj.Heading*10+strength*100);}
        }
        if(campMaskFrame==frame&&campMaskSignature==signature&&campMaskX==minX&&campMaskZ==minZ)return;
        campMaskFrame=frame;campMaskSignature=signature;campMaskX=minX;campMaskZ=minZ;campMaskCount=campFootprints.Count;
        if(campClearingPixels==null)campClearingPixels=new byte[CampMaskSide*CampMaskSide];else Array.Clear(campClearingPixels,0,campClearingPixels.Length);
        void Paint(double cx,double cz,double w,double d,double heading,double strength)
        {
            double radius=Math.Sqrt(w*w+d*d)*.5+1,a=heading*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a);
            int x0=Math.Max(0,(int)Math.Floor(cx-radius-minX)),x1=Math.Min(CampMaskSide-1,(int)Math.Ceiling(cx+radius-minX));
            int z0=Math.Max(0,(int)Math.Floor(cz-radius-minZ)),z1=Math.Min(CampMaskSide-1,(int)Math.Ceiling(cz+radius-minZ));
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
            {
                double dx=minX+x+.5-cx,dz=minZ+z+.5-cz,lx=dx*c-dz*s,lz=dx*s+dz*c;
                double ox=Math.Max(0,Math.Abs(lx)-w*.5),oz=Math.Max(0,Math.Abs(lz)-d*.5);
                byte value=(byte)(255*strength*Math.Max(0,1-Math.Sqrt(ox*ox+oz*oz)/.9));int index=z*CampMaskSide+x;
                if(value>campClearingPixels[index])campClearingPixels[index]=value;
            }
        }
        foreach(var footprint in campFootprints)Paint(footprint.X,footprint.Z,footprint.W+.8,footprint.D+.8,footprint.Heading,footprint.Strength);
        foreach(var house in campFootprints)
        {
            if(house.Fire)continue;GroundFootprint nearest=null;double best=80*80;
            foreach(var fire in campFootprints)
            {
                if(!fire.Fire||fire.Source.NpcSite!=house.Source.NpcSite||fire.Source.NpcGroup!=house.Source.NpcGroup)continue;
                double dx=fire.X-house.X,dz=fire.Z-house.Z,distance=dx*dx+dz*dz;if(distance<best){best=distance;nearest=fire;}
            }
            if(nearest==null)continue;double length=Math.Sqrt(best);int steps=Math.Max(1,(int)Math.Ceiling(length));
            for(int i=0;i<=steps;i++){double t=i/(double)steps;Paint(house.X+(nearest.X-house.X)*t,house.Z+(nearest.Z-house.Z)*t,.65,.65,0,house.Strength*.9);}
        }
        if(campClearingTexture==null){campClearingTexture=new Texture2D(CampMaskSide,CampMaskSide,TextureFormat.R8,false,true){name="Local camp ground wear",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};}
        campClearingTexture.LoadRawTextureData(campClearingPixels);campClearingTexture.Apply(false,false);coverVisibilityRevision++;scatterDirty=true;
    }
    bool CampClearedPlant(in ScatterInstance item)
    {
        bool plant=item.Kind==ScatterKind.MidPlant||item.Kind==ScatterKind.Shrub||item.Kind==ScatterKind.Herb||item.Kind==ScatterKind.Fungus||item.Kind==ScatterKind.GeneratedTree&&item.HeightM<1.8;
        if(!plant||campMaskCount==0||campMaskFrame!=frame)return false;
        TerrainHandoff.Coordinates(frame,item.Position,out double x,out double z);int px=(int)Math.Floor(x-campMaskX),pz=(int)Math.Floor(z-campMaskZ);
        return px>=0&&pz>=0&&px<CampMaskSide&&pz<CampMaskSide&&campClearingPixels[pz*CampMaskSide+px]>160;
    }
    void BindCampClearing()
    {
        groundCoverPlacement.SetInt("_CampClearingEnabled",campMaskCount>0?1:0);
        groundCoverPlacement.SetTexture(1,"_CampClearing",campClearingTexture!=null?campClearingTexture:Texture2D.blackTexture);
        groundCoverPlacement.SetVector("_CampClearingRect",new Vector4((float)(campMaskX-originX),(float)(campMaskZ-originZ),CampMaskSide,0));
    }
    void DisposeCampClearing(){if(campClearingTexture!=null)Destroy(campClearingTexture);campClearingTexture=null;campClearingPixels=null;campFootprints.Clear();}
}
