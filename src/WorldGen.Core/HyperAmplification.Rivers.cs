using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    public sealed partial class HyperAmplification
    {
        double RiverEnvelope(V point,Edge edge,double raw,double sediment,double best,int depth=0)
        {
            if(edge==null||edge.Type!=Kind.River)return best;
            double physical=radius*scale,length=(edge.B.Position-edge.A.Position).Length*physical;
            double distance=Math.Sqrt(SegmentDistance2(point,edge,out double along))*physical;
            double support=Width(Math.Max(edge.A.Flow,edge.B.Flow))*3+12;
            if(distance-length*.3>support)return best;
            if(edge.Depth<terminalDepth&&depth<20&&length>Math.Max(8,Width(Math.Max(edge.A.Flow,edge.B.Flow))*.4))
            {
                var split=Split(edge);
                best=RiverEnvelope(point,split.First,raw,sediment,best,depth+1);
                return RiverEnvelope(point,split.Last,raw,sediment,best,depth+1);
            }
            double water=Lerp(edge.A.Water,edge.B.Water,along),bed=Lerp(edge.A.Height,edge.B.Height,along);
            double flow=Lerp(edge.A.Flow,edge.B.Flow,along),d=Math.Max(0,water-bed);
            double width=Width(flow)*Math.Sqrt(D.Clamp(d/Math.Max(.001,Depth(flow))));
            var section=new RiverSection(water,width,d,flow,Math.Abs(edge.B.Water-edge.A.Water)/Math.Max(.001,length),sediment,width*3+12);
            double phase=(point.X*.73+point.Y*.41+point.Z*.54)*physical;
            return Math.Min(best,section.Cut(raw,distance,phase,seed*.013));
        }
        internal sealed partial class Patch
        {
            // Channel-aligned near tessellation must use amplified channels,
            // NOT the old unrelated coarse spline sampler. No global polyline
            // bake: enumerate only the requested physical neighbourhood.
            public LocalHydrology.Piece[] ChannelPieces(V centre,double extent,double spacing)
            {
                var pieces=new List<LocalHydrology.Piece>();var seen=new HashSet<ulong>();
                double physical=owner.radius*owner.scale,reach=extent*1.5;
                void EdgePieces(Edge edge,int depth=0)
                {
                    if(edge.Type!=Kind.River||!seen.Add(edge.Key))return;
                    double len=(edge.A.Position-edge.B.Position).Length*physical;
                    double distance=Math.Sqrt(SegmentDistance2(centre,edge,out _))*physical;
                    double width=Width(Math.Max(edge.A.Flow,edge.B.Flow));
                    if(distance-len*.3>reach+width)return;
                    if(len<=Math.Max(spacing,width*.4)||edge.Depth>=owner.terminalDepth||depth>=20)
                    {
                        if(Math.Max(edge.A.Water-edge.A.Height,edge.B.Water-edge.B.Height)<=.001)return;
                        pieces.Add(new LocalHydrology.Piece{A=edge.A.Position,B=edge.B.Position,HalfWidthM=width});return;
                    }
                    var split=owner.Split(edge);EdgePieces(split.First,depth+1);EdgePieces(split.Last,depth+1);
                }
                void Visit(Face face)
                {
                    V middle=(face.A.Position+face.B.Position+face.C.Position)/3;
                    if((middle-centre).Length*physical>reach+face.Size*1.1)return;
                    EdgePieces(face.AB);EdgePieces(face.BC);EdgePieces(face.CA);
                    if(face.Depth>=Math.Min(7,owner.terminalDepth))return;
                    foreach(var child in owner.Split(face).Faces)Visit(child);
                }
                foreach(int id in faces)Visit(owner.RootFace(id));
                return pieces.ToArray();
            }
        }
    }
}
