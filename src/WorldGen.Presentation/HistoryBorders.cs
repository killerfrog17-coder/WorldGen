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
        public float[] Segments(World w,PrehistoryState? s)
        {
            if(ReferenceEquals(w,world)&&ReferenceEquals(s,state))return segments;
            var next=s==null?null:HistoricalMapSnapshot.For(s);
            if(ReferenceEquals(w,world)&&s?.Started==state?.Started&&next!=null&&next.SameBoundaries(projection)){state=s;projection=next;return segments;}
            world=w;state=s;projection=next;var list=new List<float>();var h=s?.History;
            if(h!=null&&s!.Started&&HistoricalCells.Count(s)==w.Surface.Cells.Length)foreach(var edge in w.Surface.Edges)
            {
                int a=projection!.Boundary(edge.A),b=projection.Boundary(edge.B);
                if(a==b||w.Hydrology.OceanConnected[edge.A]||w.Hydrology.OceanConnected[edge.B])continue;
                foreach(int triangle in new[]{edge.TriangleA,edge.TriangleB})
                {
                    var p=w.Surface.Corners[triangle];
                    list.Add((float)p.X);list.Add((float)p.Y);list.Add((float)p.Z);
                }
            }
            return segments=list.ToArray();
        }
        public ViewData Mesh(World w,PrehistoryState? s)
        {
            var values=Segments(w,s);var data=new ViewData{Lines=new float[values.Length],LineColors=new float[values.Length],Unlit=true};
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
