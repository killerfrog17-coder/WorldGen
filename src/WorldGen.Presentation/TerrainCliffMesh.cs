using System;
using System.Collections.Generic;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Resolve curved steep ground within an existing tile. Shared
    /// interior edges split together; tile and wet boundaries retain their
    /// existing contracts. This never changes the regular vegetation grids.</summary>
    public static class TerrainCliffMesh
    {
        public static LocalTerrainSample[] Refine(LocalTerrainMesh mesh,LocalTerrainSample[] original,
            Func<double,double,double> height,Func<double,double,LocalTerrainSample> geometry,CancellationToken token=default)
        {
            double step=mesh.SizeM/mesh.Segments;
            // Giant horizon products deliberately use approximate geometry.
            // Subdividing that approximation cannot recover a real cliff.
            if(step>256||step<.5||original.Length!=mesh.Positions.Length/3)return original;
            bool steep=false;
            for(int t=0;t<mesh.Triangles.Length&&!steep;t+=3)
            {
                int a=mesh.Triangles[t],b=mesh.Triangles[t+1],c=mesh.Triangles[t+2];
                if(original[a].Underwater||original[b].Underwater||original[c].Underwater)continue;
                for(int e=0;e<3;e++)
                {
                    int u=mesh.Triangles[t+e]*3,v=mesh.Triangles[t+(e+1)%3]*3;
                    double dx=mesh.Positions[u]-mesh.Positions[v],dz=mesh.Positions[u+2]-mesh.Positions[v+2],dy=mesh.Positions[u+1]-mesh.Positions[v+1];
                    if(dy*dy>.49*(dx*dx+dz*dz)){steep=true;break;}
                }
            }
            if(!steep)return original;
            var p=new List<float>(mesh.Positions);var triangles=new List<int>(mesh.Triangles);
            List<float>? normals=null,colors=null,weights=null,ground=null,rocks=null;
            List<LocalTerrainSample>? samples=null;
            int initial=p.Count/3,limit=initial+Math.Min(8192,Math.Max(128,(mesh.Segments+1)*(mesh.Segments+1)*3));
            double tolerance=Math.Max(.15,step/128);
            (int,int) Key(int a,int b)=>a<b?(a,b):(b,a);
            double X(int a)=>p[a*3];double Y(int a)=>p[a*3+1];double Z(int a)=>p[a*3+2];
            LocalTerrainSample Sample(int a)=>samples==null?original[a]:samples[a];
            bool Wet(int a){var s=Sample(a);return s.Underwater||s.WaterKind==LocalWaterKind.River&&s.WaterFootprintM> -2;}
            bool Perimeter(int a,int b)=>Math.Abs(X(a))<1e-5&&Math.Abs(X(b))<1e-5
                ||Math.Abs(Z(a))<1e-5&&Math.Abs(Z(b))<1e-5
                ||Math.Abs(X(a)-mesh.SizeM)<1e-5&&Math.Abs(X(b)-mesh.SizeM)<1e-5
                ||Math.Abs(Z(a)-mesh.SizeM)<1e-5&&Math.Abs(Z(b)-mesh.SizeM)<1e-5;
            double Slope(int a,int b){double dx=X(a)-X(b),dz=Z(a)-Z(b);return Math.Abs(Y(a)-Y(b))/Math.Max(.001,Math.Sqrt(dx*dx+dz*dz));}
            bool Curved(int a,int b,int c,out double faceLength)
            {
                double dx=X(b)-X(a),dz=Z(b)-Z(a),ex=X(c)-X(a),ez=Z(c)-Z(a),det=dx*ez-dz*ex;
                faceLength=1;if(Math.Abs(det)<1e-10)return false;
                double nx=-((Y(b)-Y(a))*ez-(Y(c)-Y(a))*dz)/det,nz=-(dx*(Y(c)-Y(a))-ex*(Y(b)-Y(a)))/det;
                faceLength=Math.Sqrt(nx*nx+1+nz*nz);
                double Dot(int v)
                {
                    double vx=normals==null?mesh.Normals[v*3]:normals[v*3],vy=normals==null?mesh.Normals[v*3+1]:normals[v*3+1],vz=normals==null?mesh.Normals[v*3+2]:normals[v*3+2];
                    return (vx*nx+vy+vz*nz)/Math.Max(1e-12,Math.Sqrt(vx*vx+vy*vy+vz*vz));
                }
                // A steep plane is already exact. Probe only a bend indicated
                // by the existing halo normals versus its actual triangle.
                return Math.Min(Dot(a),Math.Min(Dot(b),Dot(c)))<faceLength*.997;
            }
            double Height(double x,double z)=>height(mesh.EastM+x,mesh.NorthM+z);
            for(int pass=0;pass<3&&p.Count/3<limit;pass++)
            {
                token.ThrowIfCancellationRequested();
                var blocked=new HashSet<(int,int)>();
                for(int t=0;t<triangles.Count;t+=3)
                {
                    int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
                    if(!Wet(a)&&!Wet(b)&&!Wet(c))continue;
                    blocked.Add(Key(a,b));blocked.Add(Key(b,c));blocked.Add(Key(c,a));
                }
                var probes=new Dictionary<(int,int),double>();var selected=new Dictionary<(int,int),int>();
                double Midpoint(int a,int b)
                {
                    var k=Key(a,b);if(probes.TryGetValue(k,out double value))return value;
                    value=Height((X(a)+X(b))*.5,(Z(a)+Z(b))*.5);probes.Add(k,value);return value;
                }
                void Select(int a,int b)
                {
                    var k=Key(a,b);if(selected.ContainsKey(k)||blocked.Contains(k)||Perimeter(a,b)||p.Count/3+selected.Count>=limit)return;
                    selected.Add(k,-1);
                }
                for(int t=0;t<triangles.Count;t+=3)
                {
                    if((t&767)==0)token.ThrowIfCancellationRequested();
                    int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
                    if(Wet(a)||Wet(b)||Wet(c)||Math.Max(Slope(a,b),Math.Max(Slope(b,c),Slope(c,a)))<.7||!Curved(a,b,c,out double faceLength))continue;
                    bool found=false;
                    void Probe(int u,int v)
                    {
                        if(blocked.Contains(Key(u,v))||Perimeter(u,v))return;
                        if(Math.Abs(Midpoint(u,v)-(Y(u)+Y(v))*.5)>tolerance*faceLength){Select(u,v);found=true;}
                    }
                    Probe(a,b);Probe(b,c);Probe(c,a);
                    // A symmetric scarp may put all three edge midpoints close
                    // to their chords while still bending inside the triangle.
                    if(!found&&Math.Abs(Height((X(a)+X(b)+X(c))/3,(Z(a)+Z(b)+Z(c))/3)-(Y(a)+Y(b)+Y(c))/3)>tolerance*faceLength)
                    {Select(a,b);Select(b,c);Select(c,a);}
                }
                if(selected.Count==0)break;
                if(samples==null)
                {
                    normals=new List<float>(mesh.Normals);colors=new List<float>(mesh.Colors);weights=new List<float>(mesh.SurfaceWeights);
                    ground=new List<float>(mesh.GroundWeightsB);rocks=new List<float>(mesh.Lithology);samples=new List<LocalTerrainSample>(original);
                }
                var keys=new List<(int,int)>(selected.Keys);
                foreach(var k in keys)
                {
                    int a=k.Item1,b=k.Item2,id=p.Count/3;double x=(X(a)+X(b))*.5,z=(Z(a)+Z(b))*.5,y=Midpoint(a,b);
                    var exact=geometry(mesh.EastM+x,mesh.NorthM+z);
                    if(exact.Underwater||exact.WaterKind==LocalWaterKind.River&&exact.WaterFootprintM> -2){selected.Remove(k);continue;}
                    samples!.Add(LocalTerrain.LocalTerrainPatch.BlendEcology(Sample(a),Sample(b),Sample(a),Sample(b),.5,0,exact));
                    p.Add((float)x);p.Add((float)y);p.Add((float)z);
                    double probe=Math.Max(.25,Math.Min(2,step/8)),nx=(y-Height(x+probe,z))/probe,nz=(y-Height(x,z+probe))/probe,length=Math.Sqrt(nx*nx+1+nz*nz);
                    normals!.Add((float)(nx/length));normals.Add((float)(1/length));normals.Add((float)(nz/length));
                    void Blend(List<float> values,int stride){for(int j=0;j<stride;j++)values.Add((values[a*stride+j]+values[b*stride+j])*.5f);}
                    Blend(colors!,4);Blend(weights!,4);Blend(ground!,4);Blend(rocks!,6);
                    mesh.MinHeightM=Math.Min(mesh.MinHeightM,y);mesh.MaxHeightM=Math.Max(mesh.MaxHeightM,y);selected[k]=id;
                }
                var next=new List<int>(triangles.Count+selected.Count*6);
                void Triangle(int a,int b,int c){next.Add(a);next.Add(b);next.Add(c);}
                for(int t=0;t<triangles.Count;t+=3)
                {
                    int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
                    int ab=selected.TryGetValue(Key(a,b),out int i)?i:-1,bc=selected.TryGetValue(Key(b,c),out i)?i:-1,ca=selected.TryGetValue(Key(c,a),out i)?i:-1;
                    int mask=(ab>=0?1:0)|(bc>=0?2:0)|(ca>=0?4:0);
                    if(mask==0){Triangle(a,b,c);continue;}
                    if(mask==7){Triangle(a,ab,ca);Triangle(ab,b,bc);Triangle(ca,bc,c);Triangle(ab,bc,ca);continue;}
                    // Rotate each one/two-edge case to AB or AB+BC. All emitted
                    // triangles preserve input winding and share the same cuts.
                    if(mask==2||mask==6){int v=a;a=b;b=c;c=v;v=ab;ab=bc;bc=ca;ca=v;}
                    else if(mask==4||mask==5){int v=a;a=c;c=b;b=v;v=ab;ab=ca;ca=bc;bc=v;}
                    if(bc<0){Triangle(a,ab,c);Triangle(ab,b,c);}
                    else{Triangle(a,ab,c);Triangle(ab,bc,c);Triangle(ab,b,bc);}
                }
                triangles=next;
            }
            if(samples==null)return original;
            mesh.Positions=p.ToArray();mesh.Normals=normals!.ToArray();mesh.Colors=colors!.ToArray();mesh.SurfaceWeights=weights!.ToArray();
            mesh.GroundWeightsB=ground!.ToArray();mesh.Lithology=rocks!.ToArray();mesh.Triangles=triangles.ToArray();return samples.ToArray();
        }
    }
}
