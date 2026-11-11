using System;
using System.Collections.Generic;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    // A visible bank on a shared tile edge is one piecewise curve: coarse grid
    // anchors plus the same finite channel constraints. Independent collapse
    // onto the old regular grid otherwise disagrees around an inserted bank.
    internal static class RiverBoundaryCurves
    {
        public static void Apply(LocalTerrainMesh mesh,LocalTerrain terrain,LocalTerrainFrame frame,LocalTerrain.LocalTerrainPatch patch,double[]? edgeSteps,List<float> positions,Func<double,double,int> insert,CancellationToken token)
        {
            var events=new HashSet<long>[4];var anchors=new HashSet<long>[4];var spacing=new double[4];
            long Key(double at)=>(long)Math.Round(at*1000);
            double Begin(int edge)=>edge<2?mesh.NorthM:mesh.EastM;
            double Fixed(int edge)=>(edge<2?mesh.EastM:mesh.NorthM)+(edge==1||edge==3?mesh.SizeM:0);
            for(int edge=0;edge<4;edge++)
            {
                events[edge]=new HashSet<long>();anchors[edge]=new HashSet<long>();
                double step=mesh.SizeM/mesh.Segments;spacing[edge]=step;
                for(int k=0;k<=mesh.Segments;k++)
                {
                    double coarse=Math.Max(step,edgeSteps==null?step:TerrainEdges.At(edgeSteps,edge,k*step));spacing[edge]=Math.Max(spacing[edge],coarse);
                    double low=Math.Floor((Begin(edge)+k*step)/coarse)*coarse;anchors[edge].Add(Key(low));anchors[edge].Add(Key(low+coarse));
                }
            }
            double maximum=0;foreach(double size in spacing)maximum=Math.Max(maximum,size);
            var samplePatch=maximum>mesh.SizeM?terrain.Patch(frame.Position(mesh.EastM+mesh.SizeM*.5,mesh.NorthM+mesh.SizeM*.5),(mesh.SizeM+maximum*2+32)/frame.HorizontalScale):patch;
            void Segment(V a,V b)
            {
                TerrainHandoff.Coordinates(frame,a,out double ax,out double az);TerrainHandoff.Coordinates(frame,b,out double bx,out double bz);
                for(int edge=0;edge<4;edge++)
                {
                    double origin=edge<2?ax:az,delta=edge<2?bx-ax:bz-az,fixedAt=Fixed(edge),along=edge<2?az:ax,run=edge<2?bz-az:bx-ax;
                    void Add(double value){if(value>=Begin(edge)-spacing[edge]-.001&&value<=Begin(edge)+mesh.SizeM+spacing[edge]+.001)events[edge].Add(Key(value));}
                    if(Math.Abs(delta)<1e-12){if(Math.Abs(origin-fixedAt)<.0001){Add(along);Add(along+run);}continue;}
                    double t=(fixedAt-origin)/delta;if(t>=-1e-10&&t<=1+1e-10)Add(along+run*D.Clamp(t));
                }
            }
            var pools=new HashSet<int>();
            void Pool(int id,V at,double width)
            {
                if(id<0||width<=0||!pools.Add(id))return;
                foreach(double radius in new[]{width,RiverscapeKernel.JunctionRadius(width)})for(int i=0;i<RiverscapeKernel.JunctionSides;i++)
                    Segment(RiverscapeKernel.JunctionPoint(at,radius,i*Math.PI*2/RiverscapeKernel.JunctionSides,frame.RadiusM),RiverscapeKernel.JunctionPoint(at,radius,(i+1)*Math.PI*2/RiverscapeKernel.JunctionSides,frame.RadiusM));
            }
            // Include narrow rivers and both bank rings even when the distant
            // tile simplifies its interior bathymetry. Shared edges need the
            // same finite events independently of either side's interior LOD.
            foreach(var s in samplePatch.RiverSpans())
            {
                token.ThrowIfCancellationRequested();Segment(s.A,s.B);
                foreach(double extra in new[]{0d,2})
                {
                    double wa=s.WidthA+(s.WidthA>0?extra:0),wb=s.WidthB+(s.WidthB>0?extra:0);
                    V al=(s.A+s.SideA*(wa/frame.RadiusM)).Unit,ar=(s.A-s.SideA*(wa/frame.RadiusM)).Unit,bl=(s.B+s.SideB*(wb/frame.RadiusM)).Unit,br=(s.B-s.SideB*(wb/frame.RadiusM)).Unit;
                    Segment(al,bl);Segment(ar,br);Segment(al,ar);Segment(bl,br);
                }
                if(s.PoolA)Pool(s.From,s.A,s.WidthA);if(s.PoolB)Pool(s.To,s.B,s.WidthB);
            }
            for(int edge=0;edge<4;edge++)
            {
                if(events[edge].Count==0)continue;token.ThrowIfCancellationRequested();
                var keys=new SortedSet<long>(anchors[edge]);keys.UnionWith(events[edge]);
                var along=new double[keys.Count];var heights=new double[keys.Count];int cursor=0;double fixedAt=Key(Fixed(edge))/1000d;
                foreach(long key in keys)
                {
                    double value=key/1000d;along[cursor]=value;V at=frame.Position(edge<2?fixedAt:value,edge<2?value:fixedAt);
                    heights[cursor++]=frame.SurfaceY(at,samplePatch.Elevation(at));
                }
                foreach(long key in events[edge])
                {
                    double value=key/1000d-Begin(edge);if(value<-.0001||value>mesh.SizeM+.0001)continue;value=Math.Max(0,Math.Min(mesh.SizeM,value));
                    insert(edge<2?(edge==0?0:mesh.SizeM):value,edge<2?value:(edge==2?0:mesh.SizeM));
                }
                double tolerance=Math.Max(.001,mesh.SizeM*2e-7);
                for(int id=0;id<positions.Count/3;id++)
                {
                    double axis=positions[id*3+(edge<2?0:2)],end=edge==1||edge==3?mesh.SizeM:0;if(Math.Abs(axis-end)>tolerance)continue;
                    double value=Begin(edge)+positions[id*3+(edge<2?2:0)];int found=Array.BinarySearch(along,value),lo=Math.Max(0,Math.Min(along.Length-2,found>=0?found:~found-1));
                    double t=D.Clamp((value-along[lo])/Math.Max(1e-12,along[lo+1]-along[lo]));positions[id*3+1]=(float)(heights[lo]*(1-t)+heights[lo+1]*t);
                }
            }
        }
    }
}
