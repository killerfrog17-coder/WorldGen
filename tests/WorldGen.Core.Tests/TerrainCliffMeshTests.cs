using WorldGen.Core;
using WorldGen.Presentation;

static class TerrainCliffMeshTests
{
    public static void Run()
    {
        static double Scarp(double x,double z)
        {
            double t=Math.Clamp((x-30-12*Math.Sin(z*.07))/12+.5,0,1);
            return 120*t*t*(3-2*t);
        }
        static LocalTerrainSample Sample(double h)=>new(){ElevationM=h,WaterSurfaceM=double.NaN,WaterMaskM=-1};
        static (LocalTerrainMesh,LocalTerrainSample[]) Grid(Func<double,double,double> height)
        {
            int n=8,count=81;var m=new LocalTerrainMesh{SizeM=64,Segments=n,GroundVertices=count,
                Positions=new float[count*3],Normals=new float[count*3],Colors=new float[count*4],SurfaceWeights=new float[count*4],
                GroundWeightsB=new float[count*4],Lithology=new float[count*6],GroundCover=new float[count*16],
                MidCover=new float[count*6],ForestCover=new float[count*8],Triangles=LocalTerrainMesh.SurfaceTriangles(n)};
            var samples=new LocalTerrainSample[count];
            for(int z=0;z<=n;z++)for(int x=0;x<=n;x++)
            {
                int i=z*9+x;double px=x*8,pz=z*8,h=height(px,pz),nx=(height(px-.01,pz)-height(px+.01,pz))/.02,nz=(height(px,pz-.01)-height(px,pz+.01))/.02,length=Math.Sqrt(nx*nx+1+nz*nz);
                m.Positions[i*3]=(float)px;m.Positions[i*3+1]=(float)h;m.Positions[i*3+2]=(float)pz;
                m.Normals[i*3]=(float)(nx/length);m.Normals[i*3+1]=(float)(1/length);m.Normals[i*3+2]=(float)(nz/length);samples[i]=Sample(h);
            }
            return(m,samples);
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        var (flat,flatSamples)=Grid((x,z)=>x*.1+z*.2);int flatQueries=0;
        long before=GC.GetAllocatedBytesForCurrentThread();
        var flatResult=TerrainCliffMesh.Refine(flat,flatSamples,(x,z)=>{flatQueries++;return x*.1+z*.2;},(x,z)=>Sample(x*.1+z*.2));
        long flatBytes=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(ReferenceEquals(flatResult,flatSamples)&&flat.Positions.Length==243&&flatQueries==0,"Gentle planes must bypass refinement and height probes");
        var (plane,planeSamples)=Grid((x,z)=>x*2+z*.6);int planeQueries=0;
        Check(ReferenceEquals(TerrainCliffMesh.Refine(plane,planeSamples,(x,z)=>{planeQueries++;return x*2+z*.6;},(x,z)=>Sample(x*2+z*.6)),planeSamples)&&planeQueries==0,"Steep planes must retain their triangles without height queries");
        var (mesh,samples)=Grid(Scarp);var initial=(float[])mesh.Positions.Clone();int queries=0,geometry=0;
        before=GC.GetAllocatedBytesForCurrentThread();
        var refined=TerrainCliffMesh.Refine(mesh,samples,(x,z)=>{queries++;return Scarp(x,z);},(x,z)=>{geometry++;return Sample(Scarp(x,z));});
        long curvedBytes=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(mesh.Positions.Length>initial.Length,"Curved scarp must actually receive geometric refinement");
        Check(refined.Length==mesh.Positions.Length/3&&mesh.Normals.Length==mesh.Positions.Length&&mesh.Colors.Length==refined.Length*4&&mesh.Lithology.Length==refined.Length*6,"All refined attributes and samples align");
        Check(mesh.GroundCover.Length==81*16&&mesh.MidCover.Length==81*6&&mesh.ForestCover.Length==81*8,"Vegetation remains the original regular grid");
        for(int i=0;i<initial.Length;i++)Check(initial[i]==mesh.Positions[i],"Original geometry and perimeter heights remain unchanged");
        var edges=new Dictionary<(int,int),int>();
        void Edge(int a,int b){var k=a<b?(a,b):(b,a);edges[k]=edges.GetValueOrDefault(k)+1;}
        for(int t=0;t<mesh.Triangles.Length;t+=3)
        {
            int a=mesh.Triangles[t],b=mesh.Triangles[t+1],c=mesh.Triangles[t+2];
            double dx=mesh.Positions[b*3]-mesh.Positions[a*3],dz=mesh.Positions[b*3+2]-mesh.Positions[a*3+2];
            double ex=mesh.Positions[c*3]-mesh.Positions[a*3],ez=mesh.Positions[c*3+2]-mesh.Positions[a*3+2];
            Check(dx*ez-dz*ex< -1e-8,"No inverted or degenerate triangles");Edge(a,b);Edge(b,c);Edge(c,a);
        }
        foreach(var e in edges)
        {
            if(e.Value==2)continue;
            int a=e.Key.Item1,b=e.Key.Item2;
            bool boundary=mesh.Positions[a*3]==mesh.Positions[b*3]&&(mesh.Positions[a*3]==0||mesh.Positions[a*3]==64)
                ||mesh.Positions[a*3+2]==mesh.Positions[b*3+2]&&(mesh.Positions[a*3+2]==0||mesh.Positions[a*3+2]==64);
            Check(e.Value==1&&boundary,"All interior subdivisions are conforming; no T junctions or holes");
            Check(a<81&&b<81,"No perimeter subdivision");
        }
        var contact=TerrainContactSurface.Build(mesh);Check(contact!=null,"Collision uses final refined land triangles");
        double oldSquared=0,newSquared=0,oldMax=0,newMax=0;int count=0;
        for(double z=8.25;z<56;z+=.5)for(double x=8.25;x<56;x+=.5)
        {
            int ix=(int)(x/8),iz=(int)(z/8),a=iz*9+ix;double u=x/8-ix,v=z/8-iz;
            double old=u+v<=1?initial[a*3+1]*(1-u-v)+initial[(a+1)*3+1]*u+initial[(a+9)*3+1]*v
                :initial[(a+10)*3+1]*(u+v-1)+initial[(a+1)*3+1]*(1-v)+initial[(a+9)*3+1]*(1-u);
            Check(contact!.TryHeight(x,z,out double actual),"Refined surface covers every interior reference probe");
            double expected=Scarp(x,z),od=Math.Abs(old-expected),nd=Math.Abs(actual-expected);
            oldSquared+=od*od;newSquared+=nd*nd;oldMax=Math.Max(oldMax,od);newMax=Math.Max(newMax,nd);count++;
        }
        Check(newSquared<oldSquared*.15&&newMax<oldMax*.45,"Refinement must materially reduce curved-scarp aliasing against the same continuous height field");
        Console.WriteLine($"PASS cliff refinement: RMS {Math.Sqrt(oldSquared/count):F3}->{Math.Sqrt(newSquared/count):F3}m; max {oldMax:F3}->{newMax:F3}m; vertices 81->{refined.Length}; triangles 128->{mesh.Triangles.Length/3}; height queries {queries}; geometry queries {geometry}; allocation flat={flatBytes} curved={curvedBytes} bytes; vertex budget hit={refined.Length>=81+243}; flat/plane queries={flatQueries}/{planeQueries}");
    }
}
