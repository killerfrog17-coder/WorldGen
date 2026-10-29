using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

static class RiverscapeTests
{
    public static void Run(string path,bool sitesOnly=false)
    {
        int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        for(int i=0;i<=100;i++)
        {double x=i/100d;Check(RiverscapeKernel.Weight(x)>=0&&RiverscapeKernel.Weight(x)<=1,"Compact primitive weight");Check(RiverscapeKernel.Smooth(x)>=0&&RiverscapeKernel.Smooth(x)<=1,"C2 longitudinal profile");}
        Check(RiverscapeKernel.Weight(1)==0&&RiverscapeKernel.Weight(2)==0,"No primitive outside compact support");
        Check(Math.Abs(RiverscapeKernel.HalfWidth(10000,.1)/RiverscapeKernel.HalfWidth(10000,1)-.1)<1e-9,"Channel widths scale horizontally");
        var w=WorldFile.Read(File.ReadAllBytes(path));string hash=StateHash.Compute(w);var watch=System.Diagnostics.Stopwatch.StartNew();
        var terrain=new LocalTerrain(w,horizontalScale:.1);Console.WriteLine($"prepare {watch.Elapsed.TotalSeconds:F3}s, {terrain.Amplification.RiverscapeReachCount} visible root reaches");
        if(sitesOnly)
        {
            int flat=Enumerable.Range(0,w.ElevationM.Length).Where(i=>w.ElevationM[i]>80&&w.ElevationM[i]<800).OrderBy(i=>w.Ecology.Habitat[i].ReliefM).First();
            var target=w.Recipe.TerrainSource=="Earth"?TerrainMapViewport.FromDegrees(30.5,31.2):w.Surface.Cells[flat].Position;
            var c=terrain.Hydrology.Channels.Where(c=>c.WidthM>10&&c.StartM>10&&c.StartM<1500).OrderBy(c=>(c.A-target).Length).First();
            var at=terrain.Amplification.RiverPoint(c.From,.5);var local=terrain.Patch(at,3000);
            var spans0=local.RiverSpans().Where(s=>s.Reach==c.From).OrderBy(s=>((s.A+s.B).Unit-at).Length).ToArray();
            if(spans0.Length>0)at=(spans0[0].A+spans0[0].B).Unit;
            var sample=terrain.Patch(at,2000).Sample(at);Console.WriteLine($"Native site reach {c.From}: {sample.WaterKind}, water {sample.WaterSurfaceM}, terrain {sample.ElevationM}, wet {sample.Underwater}");return;
        }
        var levels=new Dictionary<int,double>();int spans=0,tiles=0;double worstTile=0,maxDrop=0;
        var candidates=terrain.Hydrology.Channels.Where(c=>c.StartM>20&&c.StartM<5000&&c.WidthM>10).ToArray();
        var chosen=candidates.Take(4).Concat(candidates.OrderByDescending(c=>(c.StartM-c.EndM)/Math.Max(1,c.LengthM)).Take(4)).Distinct();
        foreach(var channel in chosen)
        {
            var p=terrain.Amplification.RiverPoint(channel.From,.5);var patch=terrain.Patch(p,3000);var pieces=patch.RiverSpans();
            foreach(var s in pieces)
            {
                spans++;Check(s.LevelA>=s.LevelB-1e-9,"Downstream profile is monotonic");
                Check(s.WidthA>=0&&s.WidthB>=0&&s.WidthA<=60.001&&s.WidthB<=60.001,"Finite scale-correct water width");
                void Junction(int node,double level){if(node<0)return;if(levels.TryGetValue(node,out double old))Check(Math.Abs(old-level)<1e-9,"Actual graph junctions share EXACT datum");else levels.Add(node,level);}
                Junction(s.From,s.LevelA);Junction(s.To,s.LevelB);
                var center=(s.A+s.B).Unit;
                if((center-p).Length*terrain.RadiusM<3000)
                {var sample=patch.Sample(center);Check(double.IsFinite(sample.ElevationM)&&sample.ElevationM<=sample.PreWaterElevationM+.002,"Finite cut-only bed");}
                maxDrop=Math.Max(maxDrop,s.LevelA-s.LevelB);
            }
            var frame=new LocalTerrainFrame(p,terrain.RadiusM,0,.1);watch.Restart();
            var mesh=LocalTerrainMesh.Build(terrain,frame,-102.4,-102.4,204.8,32);
            worstTile=Math.Max(worstTile,watch.Elapsed.TotalMilliseconds);tiles++;
            for(int i=0;i<mesh.WaterPositions.Length;i++)Check(float.IsFinite(mesh.WaterPositions[i]),"Finite river geometry");
            for(int i=0;i<mesh.WaterTriangles.Length;i++)Check(mesh.WaterTriangles[i]>=0&&mesh.WaterTriangles[i]*3<mesh.WaterPositions.Length,"Valid explicit ribbon topology");
            // Every river triangle must fit a single finite channel or junction;
            // a global cross-terrain sheet would exceed the bound by kilometres.
            for(int i=0;i<mesh.WaterTriangles.Length;i+=3)
            {
                int a=mesh.WaterTriangles[i],b=mesh.WaterTriangles[i+1],c=mesh.WaterTriangles[i+2];if(Math.Abs(mesh.WaterColors[a*4+3]-2)>.25)continue;
                foreach(int q in new[]{b,c})
                {double dx=mesh.WaterPositions[a*3]-mesh.WaterPositions[q*3],dz=mesh.WaterPositions[a*3+2]-mesh.WaterPositions[q*3+2];Check(Math.Sqrt(dx*dx+dz*dz)<=204.8*1.415,"No cross-world river triangles");}
            }
            Console.WriteLine($"reach {channel.From}: {pieces.Length} local spans, {mesh.WaterTriangles.Length/3} water triangles, {mesh.BuildMilliseconds:F1}ms mesh");
            Boundary(mesh,Check,frame,patch);
        }
        int mouths=0;
        foreach(var channel in terrain.Hydrology.Channels.Where(c=>c.EndM<=0&&c.WidthM>10).Take(12))
        {
            var p=terrain.Amplification.RiverPoint(channel.From,.99);var patch=terrain.Patch(p,3000);
            var sea=patch.RiverSpans().Where(s=>s.Reach==channel.From&&s.LevelA==0&&s.LevelB==0&&s.WidthB>0).ToArray();if(sea.Length==0)continue;
            p=(sea[0].A+sea[0].B).Unit;var frame=new LocalTerrainFrame(p,terrain.RadiusM,0,.1);
            var mouth=LocalTerrainMesh.Build(terrain,frame,-32,-32,64,16);
            Check(mouth.WaterTriangles.Length>0,"Sea-level mouth retains water geometry");
            bool covered=false;for(int i=0;i<mouth.WaterTriangles.Length;i+=3)
            {
                int a=mouth.WaterTriangles[i]*3,b=mouth.WaterTriangles[i+1]*3,c=mouth.WaterTriangles[i+2]*3;var q=mouth.WaterPositions;
                double bx=q[b]-q[a],bz=q[b+2]-q[a+2],cx=q[c]-q[a],cz=q[c+2]-q[a+2],det=bx*cz-bz*cx;if(Math.Abs(det)<1e-9)continue;
                double u=((32-q[a])*cz-(32-q[a+2])*cx)/det,v=(bx*(32-q[a+2])-bz*(32-q[a]))/det;
                if(u>=-1e-5&&v>=-1e-5&&u+v<=1.00001)covered=true;
            }
            Check(covered,"Actual sea-level river-mouth centre is covered, not skipped");if(++mouths>=3)break;
        }
        Check(mouths>=1,"At least one actual sea-level ocean connection tested");
        Check(StateHash.Compute(w)==hash,"Source world/history unchanged");Check(spans>0&&tiles>0,"Actual river cases exercised");
        Console.WriteLine($"PASS {checks} checks, {spans} spans/{tiles} tiles, max primitive drop {maxDrop:F2}m, cold tile worst {worstTile:F1}ms");
    }
    static void Boundary(LocalTerrainMesh mesh,Action<bool,string> check,LocalTerrainFrame frame,LocalTerrain.LocalTerrainPatch patch)
    {
        var incidence=new Dictionary<(int,int),int>();
        foreach(int i in Enumerable.Range(0,mesh.WaterTriangles.Length/3))
        {
            int a=mesh.WaterTriangles[i*3],b=mesh.WaterTriangles[i*3+1],c=mesh.WaterTriangles[i*3+2];
            void Edge(int p,int q){var key=(Math.Min(p,q),Math.Max(p,q));incidence.TryGetValue(key,out int n);incidence[key]=n+1;}
            Edge(a,b);Edge(b,c);Edge(c,a);
        }
        double Ground(double x,double z)
        {
            var triangles=mesh.WaterClipTriangles.Length>0?mesh.WaterClipTriangles:mesh.Triangles;
            int count=mesh.WaterClipTriangles.Length>0?triangles.Length:mesh.Segments*mesh.Segments*6;
            for(int k=0;k<count;k+=3)
            {
                int a=triangles[k]*3,b=triangles[k+1]*3,c=triangles[k+2]*3;var p=mesh.Positions;
                double bx=p[b]-p[a],bz=p[b+2]-p[a+2],cx=p[c]-p[a],cz=p[c+2]-p[a+2],det=bx*cz-bz*cx;if(Math.Abs(det)<1e-12)continue;
                double u=((x-p[a])*cz-(z-p[a+2])*cx)/det,v=(bx*(z-p[a+2])-bz*(x-p[a]))/det;
                if(u>=-1e-5&&v>=-1e-5&&u+v<=1.00001)return p[a+1]+(p[b+1]-p[a+1])*u+(p[c+1]-p[a+1])*v;
            }
            return double.NaN;
        }
        int checkedEdges=0,wa=0,wb=0;double worst=0,worstX=0,worstZ=0,worstY=0;
        bool Covered(double x,double z,double y,double ex,double ez)
        {
            var p=mesh.WaterPositions;var t=mesh.WaterTriangles;
            for(int k=0;k<t.Length;k+=3)
            {
                int a=t[k]*3,b=t[k+1]*3,c=t[k+2]*3;
                double bx=p[b]-p[a],bz=p[b+2]-p[a+2],cx=p[c]-p[a],cz=p[c+2]-p[a+2],det=bx*cz-bz*cx;if(Math.Abs(det)<1e-12)continue;
                double u=((x-p[a])*cz-(z-p[a+2])*cx)/det,v=(bx*(z-p[a+2])-bz*(x-p[a]))/det;
                double eu=((ex-p[a])*cz-(ez-p[a+2])*cx)/det,ev=(bx*(ez-p[a+2])-bz*(ex-p[a]))/det;
                if(u>=-1e-6&&v>=-1e-6&&u+v<=1.000001&&Math.Abs(p[a+1]+eu*(p[b+1]-p[a+1])+ev*(p[c+1]-p[a+1])-y)<.005)return true;
            }
            return false;
        }
        foreach(var edge in incidence.Where(e=>e.Value==1))
        {
            int a=edge.Key.Item1,b=edge.Key.Item2;if(Math.Abs(mesh.WaterColors[a*4+3]-2)>.25)continue;
            var p=mesh.WaterPositions;double x=(p[a*3]+p[b*3])*.5,z=(p[a*3+2]+p[b*3+2])*.5,y=(p[a*3+1]+p[b*3+1])*.5;
            if(Math.Abs(p[a*3]-p[b*3])+Math.Abs(p[a*3+2]-p[b*3+2])<.001)continue; // sub-millimetre clipping slivers
            if(x<.001||z<.001||x>mesh.SizeM-.001||z>mesh.SizeM-.001)continue;
            double ground=Ground(x,z);if(double.IsNaN(ground))continue;
            if(y-ground>.055)
            {
                double dx=p[b*3]-p[a*3],dz=p[b*3+2]-p[a*3+2],length=Math.Sqrt(dx*dx+dz*dz);
                if(length>.0001)
                {double eps=Math.Min(.01,length*.01);dx=dx/length*eps;dz=dz/length*eps;if(Covered(x-dz,z+dx,y,x,z)&&Covered(x+dz,z-dx,y,x,z))continue;}
            }
            if(y-ground>worst){worst=y-ground;worstX=x;worstZ=z;worstY=y;wa=a;wb=b;}checkedEdges++;
        }
        Console.WriteLine($"  boundary audit: {checkedEdges} river edges, max drawn-ground clearance {worst:F4}m");
        if(worst>.08)
        {
            var p=frame.Position(mesh.EastM+worstX,mesh.NorthM+worstZ);var s=patch.Sample(p);Console.WriteLine($"  bad edge {worstX:F3},{worstZ:F3}, y {worstY:F3}; pre {frame.SurfaceY(p,s.PreWaterElevationM):F3}, field ground {frame.SurfaceY(p,s.ElevationM):F3}, water {frame.SurfaceY(p,s.WaterSurfaceM):F3}, kind {s.WaterKind}, mask {s.WaterMaskM:F3}, footprint {s.WaterFootprintM:F3}, body {s.WaterBodyKey}");
            foreach(int i in new[]{wa,wb})Console.WriteLine($"   end {mesh.WaterPositions[i*3]:F3},{mesh.WaterPositions[i*3+1]:F3},{mesh.WaterPositions[i*3+2]:F3}");
            foreach(var span in patch.RiverSpans().Where(t=>(ulong)(uint)(t.Reach+1)==s.WaterBodyKey).OrderBy(t=>((t.A+t.B).Unit-p).Length).Take(3))
                Console.WriteLine($"   section {span.Reach}: {span.LevelA:F3}->{span.LevelB:F3}, width {span.WidthA:F3}->{span.WidthB:F3}, endpoints {span.From},{span.To}");
        }
        check(worst<.08,"Finite river edges meet actual drawn terrain (not just original grid estimates)");
    }
}
