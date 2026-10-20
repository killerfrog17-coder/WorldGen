using System;
using System.Collections.Generic;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Intersect connected water surfaces with both their footprint and
    /// the ACTUAL drawn ground. Dry-corner potential levels never define water
    /// geometry, and unrelated water bodies never share an interpolation plane.</summary>
    public static class LocalWaterMesh
    {
        struct Vertex
        {
            public double X,Z,Y,Ground,Footprint;
            public static Vertex Between(Vertex a,Vertex b,double t)=>new Vertex{
                X=a.X+(b.X-a.X)*t,Z=a.Z+(b.Z-a.Z)*t,Y=a.Y+(b.Y-a.Y)*t,
                Ground=a.Ground+(b.Ground-a.Ground)*t,
                Footprint=a.Footprint+(b.Footprint-a.Footprint)*t};
        }
        static bool SameBody(LocalTerrainSample a,LocalTerrainSample b)=>a.WaterKind==b.WaterKind&&a.WaterBodyKey==b.WaterBodyKey;
        public static void Build(LocalTerrainMesh mesh,LocalTerrainFrame frame,LocalTerrainSample[] samples)
        {
            int side=mesh.Segments+1;double step=mesh.SizeM/mesh.Segments;
            var vertices=new List<float>();var colors=new List<float>();var indices=new List<int>();
            var shared=new Dictionary<(long,long,long),int>();
            var input=new Vertex[12];var clipped=new Vertex[12];
            var corners=new int[3];var points=new V[3];
            int Clip(Vertex[] source,int count,Vertex[] target,bool depth)
            {
                int n=0;
                for(int k=0;k<count;k++)
                {
                    var p=source[k];var q=source[(k+1)%count];
                    double pd=depth?p.Y-p.Ground:p.Footprint,qd=depth?q.Y-q.Ground:q.Footprint;
                    bool pWet=pd>=0,qWet=qd>=0;
                    if(pWet)target[n++]=p;
                    if(pWet!=qWet)target[n++]=Vertex.Between(p,q,pd/(pd-qd));
                }
                return n;
            }
            int Index(Vertex v)
            {
                var key=((long)Math.Round(v.X*10000),(long)Math.Round(v.Z*10000),(long)Math.Round(v.Y*10000));
                if(shared.TryGetValue(key,out int index))return index;
                index=vertices.Count/3;shared.Add(key,index);
                vertices.Add((float)v.X);vertices.Add((float)(v.Y+.025));vertices.Add((float)v.Z);
                colors.Add(.13f);colors.Add(.37f);colors.Add(.46f);colors.Add(1);return index;
            }
            void Polygon(Vertex[] polygon,int count)
            {
                if(count<3)return;
                int first=Index(polygon[0]),previous=Index(polygon[1]);
                for(int k=2;k<count;k++)
                {
                    var p=polygon[0];var q=polygon[k-1];var r=polygon[k];
                    double area=(q.X-p.X)*(r.Z-p.Z)-(q.Z-p.Z)*(r.X-p.X);
                    int current=Index(r);
                    if(Math.Abs(area)>1e-10&&first!=previous&&previous!=current&&first!=current)
                    {indices.Add(first);indices.Add(previous);indices.Add(current);}previous=current;
                }
            }
            void Triangle(int a,int b,int c)
            {
                if(!samples[a].Underwater&&!samples[b].Underwater&&!samples[c].Underwater)return;
                corners[0]=a;corners[1]=b;corners[2]=c;
                for(int k=0;k<3;k++)
                {int i=corners[k];points[k]=frame.Position(mesh.EastM+mesh.Positions[i*3],mesh.NorthM+mesh.Positions[i*3+2]);}
                for(int seed=0;seed<3;seed++)
                {
                    var water=samples[corners[seed]];
                    if(!water.Underwater||double.IsInfinity(water.WaterSurfaceM))continue;
                    bool duplicate=false;
                    for(int k=0;k<seed;k++)if(samples[corners[k]].Underwater&&SameBody(water,samples[corners[k]]))duplicate=true;
                    if(duplicate)continue;
                    for(int k=0;k<3;k++)
                    {
                        int i=corners[k];var sample=samples[i];bool same=SameBody(water,sample);
                        double level=0;int contributors=0;
                        // Extrapolate only from WET evidence for this body. A dry
                        // vertex can carry an upland/other reach potential that
                        // is thousands of metres above the legitimate surface.
                        for(int j=0;j<3;j++)
                        {
                            var source=samples[corners[j]];if(!source.Underwater||!SameBody(water,source))continue;
                            double dx=points[k].X-points[j].X,dy=points[k].Y-points[j].Y,dz=points[k].Z-points[j].Z;
                            double predicted=source.WaterSurfaceM+source.WaterGradient.X*dx+source.WaterGradient.Y*dy+source.WaterGradient.Z*dz;
                            if(source.WaterKind==LocalWaterKind.River)predicted=Math.Max(source.WaterMinimumM,Math.Min(source.WaterMaximumM,predicted));
                            level+=predicted;contributors++;
                        }
                        level/=contributors;
                        if(same&&sample.Underwater)level=sample.WaterSurfaceM;
                        if(water.WaterKind==LocalWaterKind.Lake||water.WaterKind==LocalWaterKind.Ocean)level=water.WaterSurfaceM;
                        double footprint=same?(sample.WaterKind==LocalWaterKind.None?sample.WaterMaskM:sample.WaterFootprintM):-Math.Max(1,step);
                        // Infinity is useful in Core for unrestricted ocean
                        // interiors, not in polygon intersection arithmetic.
                        footprint=Math.Max(-1e9,Math.Min(1e9,footprint));
                        input[k]=new Vertex{X=mesh.Positions[i*3],Z=mesh.Positions[i*3+2],
                            Y=frame.SurfaceY(points[k],level),Ground=mesh.Positions[i*3+1],Footprint=footprint};
                    }
                    int count=Clip(input,3,clipped,false);
                    if(count<3)continue;
                    count=Clip(clipped,count,input,true);
                    if(count<3)continue;
                    Polygon(input,count);
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
