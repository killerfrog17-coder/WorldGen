using System;
using System.Collections.Generic;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Clip the shared land/water height fields, not whole cell/tile water rectangles.</summary>
    public static class LocalWaterMesh
    {
        struct Vertex
        {
            public double X,Z,Level,Distance;
            public ulong Key;
            public static Vertex Between(Vertex a,Vertex b,double t)=>new Vertex{X=a.X+(b.X-a.X)*t,Z=a.Z+(b.Z-a.Z)*t,Level=a.Level+(b.Level-a.Level)*t,Distance=0,
                Key=(Math.Min(a.Key>>32,b.Key>>32)<<32)|Math.Max(a.Key>>32,b.Key>>32)};
        }
        public static void Build(LocalTerrainMesh mesh,LocalTerrainFrame frame,LocalTerrainSample[] samples)
        {
            int side=mesh.Segments+1;double step=mesh.SizeM/mesh.Segments;
            var vertices=new List<float>();var colors=new List<float>();var indices=new List<int>();
            var shared=new Dictionary<ulong,int>();
            var input=new Vertex[3];var clipped=new Vertex[6];
            void Triangle(int a,int b,int c)
            {
                bool wet=false;double fallback=0;
                for(int k=0;k<3;k++){int i=k==0?a:k==1?b:c;if(samples[i].Underwater){wet=true;fallback=samples[i].WaterSurfaceM;}}
                if(!wet)return;
                for(int k=0;k<3;k++)
                {
                    int i=k==0?a:k==1?b:c;var sample=samples[i];bool valid=!double.IsNaN(sample.WaterSurfaceM);
                    double x=mesh.Positions[i*3],z=mesh.Positions[i*3+2];
                    double drawnDepth=valid?frame.SurfaceY(frame.Position(mesh.EastM+x,mesh.NorthM+z),sample.WaterSurfaceM)-mesh.Positions[i*3+1]:-Math.Max(1,step);
                    input[k]=new Vertex{X=x,Z=z,Level=valid?sample.WaterSurfaceM:fallback,
                        Distance=valid?Math.Min(sample.WaterMaskM,drawnDepth):-Math.Max(1,step),Key=((ulong)(uint)i<<32)|(uint)i};
                }
                int count=0;
                for(int k=0;k<3;k++)
                {
                    var p=input[k];var q=input[(k+1)%3];bool pWet=p.Distance>=0,qWet=q.Distance>=0;
                    if(pWet)clipped[count++]=p;
                    if(pWet!=qWet)clipped[count++]=Vertex.Between(p,q,p.Distance/(p.Distance-q.Distance));
                }
                int first=-1,previous=-1;
                for(int k=0;k<count;k++)
                {
                    var v=clipped[k];
                    if(!shared.TryGetValue(v.Key,out int index))
                    {
                        index=vertices.Count/3;shared.Add(v.Key,index);V position=frame.Position(mesh.EastM+v.X,mesh.NorthM+v.Z);
                        vertices.Add((float)v.X);vertices.Add((float)frame.SurfaceY(position,v.Level+.025));vertices.Add((float)v.Z);
                        colors.Add(.13f);colors.Add(.37f);colors.Add(.46f);colors.Add(1);
                    }
                    if(k==0)first=index;else if(k>1){indices.Add(first);indices.Add(previous);indices.Add(index);}previous=index;
                }
            }
            if(mesh.WaterClipTriangles.Length>0)
            {var triangles=mesh.WaterClipTriangles;for(int i=0;i<triangles.Length;i+=3)Triangle(triangles[i],triangles[i+1],triangles[i+2]);}
            else for(int z=0;z<mesh.Segments;z++)for(int x=0;x<mesh.Segments;x++)
            {int a=z*side+x;Triangle(a,a+side,a+1);Triangle(a+1,a+side,a+side+1);}
            mesh.WaterPositions=vertices.ToArray();mesh.WaterColors=colors.ToArray();mesh.WaterTriangles=indices.ToArray();
        }
    }
}
