using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace WorldGen.Core
{
    public sealed partial class HyperAmplification
    {
        readonly struct RiverNodeStage
        {
            public readonly double Level,Radius;
            public RiverNodeStage(double level,double radius){Level=level;Radius=radius;}
        }
        readonly ConcurrentDictionary<int,RiverNodeStage> actualRiverNodes=new ConcurrentDictionary<int,RiverNodeStage>();
        struct JunctionSegment
        {public double AX,AZ,BX,BZ,Width,Bound;}

        bool FixedRiverNode(int node)=>rootNodes[node].Type==Kind.Lake||rootNodes[node].Type==Kind.Ocean||lakeOutletOwners[node]>=0||Fjords.OceanNode(node);
        RiverNodeStage ActualRiverNode(int node)
        {
            if(actualRiverNodes.TryGetValue(node,out var value))return value;
            lock(riverGate)
            {
                if(actualRiverNodes.TryGetValue(node,out value))return value;
                // Only follow a downstream dependency when its coarse bound
                // cannot prove the newly fitted node is already above it.
                // This is an iterative DAG evaluation, never a global census.
                List<(int Node,RiverNodeStage Raw)> pending=null;int current=node;
                while(!actualRiverNodes.TryGetValue(current,out value))
                {
                    var raw=FitActualRiverNode(current);int next=riverReceivers[current];
                    if(next<0||!riverActive[current]||FixedRiverNode(current)||raw.Level>=riverDatums[next])
                    {value=raw;actualRiverNodes[current]=value;break;}
                    pending??=new List<(int,RiverNodeStage)>();pending.Add((current,raw));
                    if(pending.Count>rootNodes.Length)throw new InvalidOperationException("Cycle in actual river-stage dependencies");
                    current=next;
                }
                if(pending!=null)for(int i=pending.Count-1;i>=0;i--)
                {
                    var item=pending[i];value=new RiverNodeStage(Math.Max(item.Raw.Level,value.Level),item.Raw.Radius);actualRiverNodes[item.Node]=value;
                }
                return actualRiverNodes[node];
            }
        }
        RiverNodeStage FitActualRiverNode(int node)
        {
            double level=riverDatums[node],width=riverNodeWidths[node],collar=RiverscapeKernel.JunctionRadius(width);
            bool through=ThroughRiverNode(node);
            if(riverIncoming[node]>0&&!through)collar=ActualJunctionRadius(node);
            bool fixedLevel=rootNodes[node].Type==Kind.Lake||rootNodes[node].Type==Kind.Ocean||lakeOutletOwners[node]>=0||Fjords.OceanNode(node);
            if(!fixedLevel&&riverIncoming[node]>0&&width>0)
            {
                var faceSet=new SortedSet<int>();
                for(int k=riverFaceOffsets[node];k<riverFaceOffsets[node+1];k++)
                {var f=roots[riverFaces[k]];foreach(int v in new[]{f.A,f.B,f.C})for(int j=riverFaceOffsets[v];j<riverFaceOffsets[v+1];j++)faceSet.Add(riverFaces[j]*3);}
                var ids=new int[faceSet.Count];faceSet.CopyTo(ids);var bare=new Patch(this,ids);double fitted=level,physical=radius*scale;
                void Bank(V p)=>fitted=Math.Min(fitted,bare.Sample(p,true).Height-RiverHydraulics.BankFreeboard(width));
                if(through)
                {
                    V at=rootNodes[node].Position,side=V.Cross(at,RiverNodeTangent(node)).Unit;
                    Bank((at+side*((width+2)/physical)).Unit);Bank((at-side*((width+2)/physical)).Unit);
                }
                else
                {
                    for(int direction=0;direction<64;direction++)Bank(RiverscapeKernel.JunctionPoint(rootNodes[node].Position,RiverscapeKernel.JunctionRadius(width),direction*Math.PI/32,physical));
                    for(int incident=riverIncidentOffsets[node];incident<riverIncidentOffsets[node+1];incident++)
                    {
                        int other=riverIncidents[incident],source=riverReceivers[other]==node?other:node,target=riverReceivers[source];
                        var points=RiverscapeKernel.InsertJunctionCollars(RiverPlanPoints(source),physical,source==node?collar:0,target==node?collar:0);
                        int n=points.Length-1;double nominal=RiverscapeKernel.HalfWidth(rootNodes[source].Flow,scale);
                        double Half(int station){double taper=Math.Min(1,Math.Min(station,n-station)/2d);return nominal*taper+(station<n/2?riverNodeWidths[source]:riverNodeWidths[target])*(1-taper);}
                        for(int j=0;j<n;j++)
                        {
                            double da=(points[j]-rootNodes[node].Position).Length*physical,db=(points[j+1]-rootNodes[node].Position).Length*physical;
                            if(Math.Min(da,db)>collar+.01)continue;
                            V sideA=V.Cross(points[j],j==0&&ThroughRiverNode(source)?RiverNodeTangent(source):(points[Math.Min(n,j+1)]-points[Math.Max(0,j-1)]).Unit).Unit;
                            V sideB=V.Cross(points[j+1],j==n-1&&ThroughRiverNode(target)?RiverNodeTangent(target):(points[Math.Min(n,j+2)]-points[j]).Unit).Unit;
                            foreach(double t in new[]{0d,.25,.5,.75,1d})
                            {
                                if(((points[j]*(1-t)+points[j+1]*t).Unit-rootNodes[node].Position).Length*physical>collar+.01)continue;
                                foreach(int sign in new[]{-1,1})
                                {
                                    V a=(points[j]+sideA*(sign*(Half(j)+2)/physical)).Unit,b=(points[j+1]+sideB*(sign*(Half(j+1)+2)/physical)).Unit;
                                    Bank((a*(1-t)+b*t).Unit);
                                }
                            }
                        }
                    }
                }
                level=Math.Min(level,fitted);
            }
            return new RiverNodeStage(level,collar);
        }

        double ActualJunctionRadius(int node)
        {
            double physical=radius*scale,width=riverNodeWidths[node],result=RiverscapeKernel.JunctionRadius(width);V at=rootNodes[node].Position;
            V east=V.Cross(Math.Abs(at.Z)<.99?new V(0,0,1):new V(0,1,0),at).Unit,north=V.Cross(at,east).Unit;
            var branches=new List<JunctionSegment[]>();
            for(int incident=riverIncidentOffsets[node];incident<riverIncidentOffsets[node+1];incident++)
            {
                int other=riverIncidents[incident],source=riverReceivers[other]==node?other:node,target=riverReceivers[source];var points=RiverPlanPoints(source);int n=points.Length-1;
                double nominal=RiverscapeKernel.HalfWidth(rootNodes[source].Flow,scale);
                double Half(int i){double t=Math.Min(1,Math.Min(i,n-i)/2d);return Math.Max(width,nominal*t+(i<n/2?riverNodeWidths[source]:riverNodeWidths[target])*(1-t));}
                var segments=new JunctionSegment[n];
                for(int i=0;i<n;i++)
                {
                    V a=points[i]-at,b=points[i+1]-at;double w=Math.Max(Half(i),Half(i+1))+2;
                    double ax=RiverDot(a,east)*physical,az=RiverDot(a,north)*physical,bx=RiverDot(b,east)*physical,bz=RiverDot(b,north)*physical;
                    segments[i]=new JunctionSegment{AX=ax,AZ=az,BX=bx,BZ=bz,Width=w,Bound=Math.Max(Math.Sqrt(ax*ax+az*az),Math.Sqrt(bx*bx+bz*bz))+w};
                }
                branches.Add(segments);
            }
            double PointDistance(double x,double z,in JunctionSegment s)
            {double dx=s.BX-s.AX,dz=s.BZ-s.AZ,t=D.Clamp(((x-s.AX)*dx+(z-s.AZ)*dz)/Math.Max(1e-20,dx*dx+dz*dz));return Math.Pow(x-s.AX-dx*t,2)+Math.Pow(z-s.AZ-dz*t,2);}
            double Distance(in JunctionSegment a,in JunctionSegment b)
            {
                double ax=a.BX-a.AX,az=a.BZ-a.AZ,bx=b.BX-b.AX,bz=b.BZ-b.AZ,det=ax*bz-az*bx;
                if(Math.Abs(det)>1e-12){double x=b.AX-a.AX,z=b.AZ-a.AZ,t=(x*bz-z*bx)/det,u=(x*az-z*ax)/det;if(t>=0&&t<=1&&u>=0&&u<=1)return 0;}
                return Math.Min(Math.Min(PointDistance(a.AX,a.AZ,in b),PointDistance(a.BX,a.BZ,in b)),Math.Min(PointDistance(b.AX,b.AZ,in a),PointDistance(b.BX,b.BZ,in a)));
            }
            for(int a=0;a<branches.Count;a++)for(int b=a+1;b<branches.Count;b++)
            foreach(var sa in branches[a])foreach(var sb in branches[b])
            {
                double pad=sa.Width+sb.Width;
                if(Math.Min(sa.AX,sa.BX)>Math.Max(sb.AX,sb.BX)+pad||Math.Max(sa.AX,sa.BX)<Math.Min(sb.AX,sb.BX)-pad||Math.Min(sa.AZ,sa.BZ)>Math.Max(sb.AZ,sb.BZ)+pad||Math.Max(sa.AZ,sa.BZ)<Math.Min(sb.AZ,sb.BZ)-pad)continue;
                if(Distance(in sa,in sb)<=pad*pad)result=Math.Max(result,Math.Min(sa.Bound,sb.Bound)+width);
            }
            return result;
        }
    }
}
