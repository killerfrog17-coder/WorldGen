using System;
using System.Collections.Generic;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    // Political geometry, never a generation route. Cache by immutable checkpoint;
    // projection/opacity changes reuse these exact edge endpoints.
    public sealed class HistoryBorders
    {
        World? world; PrehistoryState? state; HistoricalMapSnapshot? projection;float[] segments=Array.Empty<float>();
        HistoryOverlay overlay;
        public float[] Segments(World w,PrehistoryState? s,HistoryOverlay selected=HistoryOverlay.States)
        {
            int Mode(HistoryOverlay value)=>value==HistoryOverlay.Provinces?2:value==HistoryOverlay.Territories?1:value==HistoryOverlay.Cultures?3:value==HistoryOverlay.Religions?4:value==HistoryOverlay.Technology?5:0;
            bool sameMode=Mode(selected)==Mode(overlay);
            if(ReferenceEquals(w,world)&&ReferenceEquals(s,state)&&sameMode)return segments;
            var next=s==null?null:HistoricalMapSnapshot.For(s);
            if(sameMode&&Mode(selected)==0&&ReferenceEquals(w,world)&&s?.Started==state?.Started&&next!=null&&next.SameBoundaries(projection)){state=s;projection=next;return segments;}
            world=w;state=s;projection=next;overlay=selected;var list=new List<float>();var h=s?.History;
            if(h!=null&&s!.Started&&HistoricalCells.Count(s)==w.Surface.Cells.Length)foreach(var edge in w.Surface.Edges)
            {
                int a=projection!.Boundary(edge.A,selected),b=projection.Boundary(edge.B,selected);
                if(a==b||w.Hydrology.OceanConnected[edge.A]||w.Hydrology.OceanConnected[edge.B])continue;
                foreach(int triangle in new[]{edge.TriangleA,edge.TriangleB})
                {
                    var p=w.Surface.Corners[triangle];
                    list.Add((float)p.X);list.Add((float)p.Y);list.Add((float)p.Z);
                }
            }
            return segments=list.ToArray();
        }
        public ViewData Mesh(World w,PrehistoryState? s,HistoryOverlay selected=HistoryOverlay.States)
        {
            var values=Segments(w,s,selected);var data=new ViewData{Lines=new float[values.Length],LineColors=new float[values.Length],Unlit=true};
            for(int i=0;i<values.Length;i+=3)
            {
                // Lift above the accepted exaggerated relief; borders are a map
                // annotation, not local topography. Reuse dual seam clipping.
                double radius=1.023;
                data.Lines[i]=(float)(values[i]*radius);data.Lines[i+1]=(float)(values[i+2]*radius);data.Lines[i+2]=(float)(-values[i+1]*radius);
                data.LineColors[i]=.12f;data.LineColors[i+1]=.14f;data.LineColors[i+2]=.16f;
            }
            return MapView.Project(data,0,-.02,true);
        }
    }
}
