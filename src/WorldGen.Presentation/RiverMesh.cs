using System;
using System.Collections.Generic;
using System.Threading;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    // Display geometry only. Routing, discharges, banks and saved water are unchanged.
    public static class RiverMesh
    {
        public const int ReachesPerChunk=1024;
        public const int SegmentsPerReach=8;
        public static double EnvelopeHalfWidth(double widthM)=>25000+8*Math.Max(0,widthM);
        public static double VisibilityLimit(int order)=>1500*Math.Pow(3,Math.Max(0,Math.Min(12,order)-1));
        public static IEnumerable<ViewData> Chunks(World world,CancellationToken token=default)
        {
            var reaches=world.Hydrology.Rivers;var cells=world.Surface.Cells;
            bool Water(int cell)=>cell<world.Hydrology.OceanConnected.Length&&world.Hydrology.OceanConnected[cell]||cell<world.Hydrology.LakeCoverageFraction.Length&&world.Hydrology.LakeCoverageFraction[cell]>=.9;
            double Height(int cell)=>Water(cell)?(cell<world.Hydrology.OceanConnected.Length&&world.Hydrology.OceanConnected[cell]?0:GlobeView.TryVisibleWater(world,cell,out var lake)?lake.SurfaceElevationM:world.ElevationM[cell]):world.ElevationM[cell];
            var outgoing=new Dictionary<int,int>();var incoming=new Dictionary<int,int>();
            bool Valid(RiverReach r)=>r.From>=0&&r.To>=0&&r.From<cells.Length&&r.To<cells.Length&&r.From!=r.To;
            for(int i=0;i<reaches.Length;i++)
            {
                if((i&1023)==0)token.ThrowIfCancellationRequested();var r=reaches[i];if(!Valid(r))continue;
                if(!outgoing.ContainsKey(r.From))outgoing.Add(r.From,i);
                if(!incoming.TryGetValue(r.To,out int prior)||r.DischargePotentialM3PerS>reaches[prior].DischargePotentialM3PerS)incoming[r.To]=i;
            }
            V Tangent(int cell,V fallback)
            {
                var p=cells[cell].Position;bool before=incoming.TryGetValue(cell,out int a),after=outgoing.TryGetValue(cell,out int b);
                V direction=before&&after?cells[reaches[b].To].Position-cells[reaches[a].From].Position:after?cells[reaches[b].To].Position-p:before?p-cells[reaches[a].From].Position:fallback;
                var tangent=V.Tangent(p,direction);return tangent.Length>1e-12?tangent.Unit:V.Tangent(p,fallback).Unit;
            }
            V At(V p,double height)=>new V(p.X,p.Z,-p.Y)*(1+Math.Max(0,height)*(14.0/6371000)+.001);
            for(int first=0;first<reaches.Length;first+=ReachesPerChunk)
            {
                token.ThrowIfCancellationRequested();var positions=new List<float>();var colors=new List<float>();var ids=new List<int>();var indices=new List<int>();var stroke=new List<float>();
                int Emit(V point,double height,int cell,double across,double cap,double width,int order)
                {
                    int id=ids.Count;var p=At(point,height);positions.Add((float)p.X);positions.Add((float)p.Y);positions.Add((float)p.Z);
                    colors.Add(.055f);colors.Add(.30f);colors.Add(.48f);ids.Add(cell);
                    stroke.Add((float)across);stroke.Add((float)cap);stroke.Add((float)width);stroke.Add(order);return id;
                }
                void Quad(int a,int b,int c,int d){indices.Add(a);indices.Add(b);indices.Add(c);indices.Add(a);indices.Add(c);indices.Add(d);}
                for(int i=first;i<Math.Min(first+ReachesPerChunk,reaches.Length);i++)
                {
                    if((i&127)==0)token.ThrowIfCancellationRequested();var r=reaches[i];if(!Valid(r)||Water(r.From)&&Water(r.To))continue;
                    var a=cells[r.From].Position;var b=cells[r.To].Position;double length=(b-a).Length;if(length<1e-12)continue;
                    var ta=Tangent(r.From,b-a);var tb=Tangent(r.To,b-a);double width=Math.Max(0,r.WidthM),endWidth=width;int order=Math.Max(1,r.Order),endOrder=order;
                    if(outgoing.TryGetValue(r.To,out int next)){endWidth=Math.Max(width,reaches[next].WidthM);endOrder=Math.Max(order,reaches[next].Order);}
                    V Point(double t){double t2=t*t,t3=t2*t;return (a*(2*t3-3*t2+1)+ta*(length*.45*(t3-2*t2+t))+b*(-2*t3+3*t2)+tb*(length*.45*(t3-t2))).Unit;}
                    V Derivative(double t){double t2=t*t;return a*(6*t2-6*t)+ta*(length*.45*(3*t2-4*t+1))+b*(-6*t2+6*t)+tb*(length*.45*(3*t2-2*t));}
                    double start=0,finish=1;
                    if(Water(r.From)||Water(r.To))
                    {
                        // Categorical shores bisect neighboring cell centers.
                        // Stop at the actual crossing, not the ocean/lake center.
                        double low=0,high=1;for(int search=0;search<16;search++){double middle=(low+high)*.5;if(V.Dot(Point(middle),a-b)>0)low=middle;else high=middle;}
                        if(Water(r.From))start=(low+high)*.5;else finish=(low+high)*.5;
                    }
                    double heightA=Height(r.From),heightB=Height(r.To);
                    int left=-1,right=-1;
                    for(int step=0;step<=SegmentsPerReach;step++)
                    {
                        double t=start+(finish-start)*step/SegmentsPerReach;V point=Point(t),derivative=Derivative(t);
                        V side=V.Cross(point,V.Tangent(point,derivative)).Unit;double w=width+(endWidth-width)*t,half=EnvelopeHalfWidth(w)/(world.Recipe.RadiusKm*1000);
                        double h=heightA*(1-t)+heightB*t;int owner=t<.5?r.From:r.To;
                        int l=Emit((point+side*half).Unit,h,owner,1,0,w,order),rr=Emit((point-side*half).Unit,h,owner,-1,0,w,order);
                        if(step>0)Quad(left,right,rr,l);left=l;right=rr;
                    }
                    void Cap(V point,V tangent,double w,int cell,int riverOrder,double direction,double height)
                    {
                        V side=V.Cross(point,tangent).Unit;double half=EnvelopeHalfWidth(w)/(world.Recipe.RadiusKm*1000);
                        int l=Emit((point+side*half).Unit,height,cell,1,0,w,riverOrder),rr=Emit((point-side*half).Unit,height,cell,-1,0,w,riverOrder);
                        int farR=Emit((point-side*half+tangent*(direction*half)).Unit,height,cell,-1,1,w,riverOrder),farL=Emit((point+side*half+tangent*(direction*half)).Unit,height,cell,1,1,w,riverOrder);Quad(l,rr,farR,farL);
                    }
                    Cap(Point(start),V.Tangent(Point(start),Derivative(start)).Unit,width+(endWidth-width)*start,r.From,order,-1,heightA*(1-start)+heightB*start);
                    Cap(Point(finish),V.Tangent(Point(finish),Derivative(finish)).Unit,width+(endWidth-width)*finish,r.To,endOrder,1,heightA*(1-finish)+heightB*finish);
                }
                if(ids.Count>0)yield return new ViewData{Positions=positions.ToArray(),Colors=colors.ToArray(),CellIds=ids.ToArray(),Triangles=indices.ToArray(),Stroke=stroke.ToArray(),Unlit=true};
            }
        }
    }
}
