using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Presentation
{
    internal static class TerrainSurfaceEdges
    {
        public static void Apply(LocalTerrainMesh mesh,double[]? edges,Func<double,double,double,TerrainSurfaceVertex> evaluate,CancellationToken token)
        {
            if(edges==null)return;
            TerrainEdges.Validate(edges);
            int n=mesh.Segments,side=n+1;double step=mesh.SizeM/n;
            var cache=new Dictionary<(double,double,double),TerrainSurfaceVertex>();
            var written=new Dictionary<int,double>();
            double Radius(double x,double z,double initial)
            {
                double radius=initial,tolerance=step*1e-6;
                if(Math.Abs(x-mesh.EastM)<tolerance)radius=Math.Max(radius,TerrainEdges.SurfaceAt(edges,0,z-mesh.NorthM));
                if(Math.Abs(x-mesh.EastM-mesh.SizeM)<tolerance)radius=Math.Max(radius,TerrainEdges.SurfaceAt(edges,1,z-mesh.NorthM));
                if(Math.Abs(z-mesh.NorthM)<tolerance)radius=Math.Max(radius,TerrainEdges.SurfaceAt(edges,2,x-mesh.EastM));
                if(Math.Abs(z-mesh.NorthM-mesh.SizeM)<tolerance)radius=Math.Max(radius,TerrainEdges.SurfaceAt(edges,3,x-mesh.EastM));
                return radius;
            }
            TerrainSurfaceVertex Vertex(double x,double z,double h)
            {
                h=Radius(x,z,h);var key=(x,z,h);
                if(!cache.TryGetValue(key,out var value)){value=evaluate(x,z,h);cache.Add(key,value);}return value;
            }
            // Correct corner/T-junction slopes first. These point constraints
            // affect attributes only, never collapse another geometry segment.
            for(int edge=0;edge<4;edge++)for(int k=0;k<=n;k++)
            {
                int x=edge==0?0:edge==1?n:k,z=edge==2?0:edge==3?n:k,i=z*side+x;
                double xx=mesh.EastM+x*step,zz=mesh.NorthM+z*step,h=Radius(xx,zz,step);
                if(h<=step*1.001||written.ContainsKey(i))continue;
                var v=Vertex(xx,zz,h);TerrainSurfaceVertex.Write(mesh,i,v,v,0);written.Add(i,step);
            }
            written.Clear();
            for(int edge=0;edge<4;edge++)
            {
                token.ThrowIfCancellationRequested();
                for(int k=0;k<=n;k++)
                {
                    double coarse=TerrainEdges.At(edges,edge,k*step);if(coarse<=step*1.001)continue;
                    int x=edge==0?0:edge==1?n:k,z=edge==2?0:edge==3?n:k,i=z*side+x;
                    if(written.TryGetValue(i,out double prior)&&prior>coarse)continue;
                    double along=(edge<2?mesh.NorthM:mesh.EastM)+k*step,lo=Math.Floor(along/coarse)*coarse,t=(along-lo)/coarse;
                    double xx=mesh.EastM+x*step,zz=mesh.NorthM+z*step;
                    var a=Vertex(edge<2?xx:lo,edge<2?lo:zz,coarse);
                    var b=Vertex(edge<2?xx:lo+coarse,edge<2?lo+coarse:zz,coarse);
                    mesh.Positions[i*3+1]=(float)(a.Height*(1-t)+b.Height*t);
                    TerrainSurfaceVertex.Write(mesh,i,a,b,t);written[i]=coarse;
                }
            }
            // Coarse edge collapse can move an extremum. Bounds describe the
            // submitted surface, not the unstitched physical grid.
            mesh.MinHeightM=double.MaxValue;mesh.MaxHeightM=double.MinValue;
            for(int i=0;i<mesh.GroundVertices;i++){double y=mesh.Positions[i*3+1];mesh.MinHeightM=Math.Min(mesh.MinHeightM,y);mesh.MaxHeightM=Math.Max(mesh.MaxHeightM,y);}
        }
    }
}
