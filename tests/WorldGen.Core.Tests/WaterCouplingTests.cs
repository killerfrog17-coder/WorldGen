using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class WaterCouplingTests
{
    public static void LakesWorld(string path)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));string hash=StateHash.Compute(world);
        var watch=System.Diagnostics.Stopwatch.StartNew();var terrain=new LocalTerrain(world,horizontalScale:.1);var amp=terrain.Amplification;
        Console.WriteLine($"Preparation {watch.Elapsed.TotalSeconds:F3}s; lake graph {amp.LakeBasinPreparationMilliseconds:F1}ms, {amp.LakeBasinCount} basins / {amp.LakeBasinFaces} faces, {amp.RejectedLakeBasins} rejected; {GC.GetTotalMemory(false)/1048576d:F1}MiB managed");
        int tested=0,edges=0,skipped=0;
        foreach(var body in world.Hydrology.Waters.Where(w=>w.Kind!=WaterKind.Ocean&&w.Cells.Length>0).OrderByDescending(w=>w.AreaM2))
        {
            int cell=body.Cells.OrderByDescending(i=>world.Hydrology.LakeCoverageFraction[i]).First();var centre=world.Surface.Cells[cell].Position;
            var s=terrain.Patch(centre,2000).Sample(centre);
            if(!s.Underwater||s.WaterKind!=LocalWaterKind.Lake){skipped++;continue;}
            var scan=new LocalTerrainFrame(centre,terrain.RadiusM,0,.1);V wet=centre,dry=default;bool found=false;
            for(int k=1;k<=100;k++)
            {
                V p=scan.Position(k*200,0);var t=terrain.Patch(p,2000).Sample(p);
                if(!t.Underwater){dry=p;found=true;break;}wet=p;
            }
            if(!found){skipped++;continue;}
            for(int k=0;k<22;k++){V p=(wet+dry).Unit;if(terrain.Patch(p,2000).Sample(p).Underwater)wet=p;else dry=p;}
            var frame=new LocalTerrainFrame(dry,terrain.RadiusM,terrain.Patch(dry,2000).Sample(dry).ElevationM,.1);
            foreach(var shape in new[]{(204.8,32),(3276.8,16)})
            {
                var mesh=LocalTerrainMesh.Build(terrain,frame,-shape.Item1/2,-shape.Item1/2,shape.Item1,shape.Item2);
                bool LakeEdge(double x,double z,double y)=>Math.Abs(y-frame.SurfaceY(frame.Position(mesh.EastM+x,mesh.NorthM+z),s.WaterSurfaceM))<.05;
                void Trace(double x,double z,double y)
                {
                    Console.WriteLine($"Boundary level {s.WaterSurfaceM:R}, drawn {y:R} at {x:R},{z:R}");
                    foreach(int v in Enumerable.Range(0,mesh.WaterPositions.Length/3).OrderBy(v=>Math.Pow(mesh.WaterPositions[v*3]-x,2)+Math.Pow(mesh.WaterPositions[v*3+2]-z,2)).Take(4))
                        Console.WriteLine($" edge vertex {v}: {mesh.WaterPositions[v*3]:R},{mesh.WaterPositions[v*3+1]:R},{mesh.WaterPositions[v*3+2]:R}; alpha {mesh.WaterColors[v*4+3]}");
                    double step=mesh.SizeM/mesh.Segments;int ix=(int)(x/step),iz=(int)(z/step);
                    for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                    {double px=(ix+dx)*step,pz=(iz+dz)*step;V at=frame.Position(mesh.EastM+px,mesh.NorthM+pz);var v=terrain.Patch(at,2000).Sample(at);Console.WriteLine($"  {px:F2},{pz:F2}: h {v.ElevationM:R}, w {v.WaterSurfaceM:R}, {v.WaterKind}/{v.WaterBodyKey}, wet {v.Underwater}");}
                }
                try{edges+=ContainedBoundary(mesh,LakeEdge,Trace);}catch(Exception error){throw new Exception($"Lake {body.Id}, size {shape.Item1}: "+error.Message,error);}
                if(!mesh.WaterPositions.All(float.IsFinite))throw new Exception("Non-finite lake surface");
                Console.WriteLine($"Lake {body.Id}, {body.AreaM2}m2: {shape.Item1:F0}m/{shape.Item2}, {mesh.WaterTriangles.Length/3} triangles, {mesh.BuildMilliseconds:F1}ms");
            }
            if(++tested==6)break;
        }
        if(tested==0||StateHash.Compute(world)!=hash)throw new Exception("No surviving source lakes or changed world");
        Console.WriteLine($"PASS {tested} lake shores / {edges} non-floating interior edges; skipped {skipped}; {watch.Elapsed.TotalSeconds:F2}s; unchanged {hash}");
    }
    public static void Shore(string path,string probes)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var terrain=new LocalTerrain(world,horizontalScale:.1);
        var row=File.ReadAllLines(probes).Last().Split(',');var at=new V(double.Parse(row[0]),double.Parse(row[1]),double.Parse(row[2]));
        var frame=new LocalTerrainFrame(at,terrain.RadiusM,0,.1);
        for(int direction=0;direction<8;direction++)foreach(double distance in new[]{0d,20,100,500,2000,5000})
        {
            double angle=direction*Math.PI/4;var p=frame.Position(Math.Sin(angle)*distance,Math.Cos(angle)*distance);var s=terrain.Patch(p,2000).Sample(p);
            Console.WriteLine($"{direction}/{distance}: ground {s.ElevationM:F3}, raw {s.PreWaterElevationM:F3}, water {s.WaterSurfaceM:F3}, kind {s.WaterKind}, wet {s.Underwater}");
        }
    }
    public static void Run()
    {
        int checks=0;
        void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        // The first native bank review caught a closed but steeply tilted lake:
        // do not blend a standing basin's datum down toward its submerged bed.
        var vertexType=typeof(HyperAmplification).GetNestedType("Vertex",System.Reflection.BindingFlags.NonPublic)!;
        var lakeVertex=Activator.CreateInstance(vertexType,true)!;
        void Set(string name,object value)=>vertexType.GetField(name)!.SetValue(lakeVertex,value);
        Set("Height",200d);Set("Water",210d);Set("LakeKey",1UL);Set("LakeLevel",250d);Set("LakeWeight",.01);Set("Distance",2000d);
        var profile=typeof(HyperAmplification).GetMethod("Profile",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.Invoke(null,new[]{lakeVertex})!;
        Check((double)profile.GetType().GetField("Water")!.GetValue(profile)! == 250d,"A submerged lake periphery retains its standing level, not a tilted dry potential");
        Check((double)profile.GetType().GetField("Height")!.GetValue(profile)! == 200d,"Lake closure cannot manufacture a raised bank");
        Set("LakeKey",0UL);Set("Water",75d);
        profile=typeof(HyperAmplification).GetMethod("Profile",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.Invoke(null,new[]{lakeVertex})!;
        Check((double)profile.GetType().GetField("Water")!.GetValue(profile)! == 75d,"Dry terrain cannot retire the supplied water height toward its ground");
        var frame=new LocalTerrainFrame(new V(1,0,0),6371000,0,.1);
        LocalTerrainMesh Triangle()=>new LocalTerrainMesh{Segments=1,SizeM=16,
            Positions=new float[]{0,-2,0,16,10,0,0,-2,16},WaterClipTriangles=new[]{0,2,1}};
        var samples=new[]{
            new LocalTerrainSample{ElevationM=-2,WaterSurfaceM=0,WaterMaskM=2},
            // A dry corner may carry a completely different river's potential
            // level. It must NEVER tilt an adjacent ocean/lake shoreline.
            new LocalTerrainSample{ElevationM=10,WaterSurfaceM=2000,WaterMaskM=-.1},
            new LocalTerrainSample{ElevationM=-2,WaterSurfaceM=0,WaterMaskM=2}};
        var mesh=Triangle();LocalWaterMesh.Build(mesh,frame,samples);
        Check(mesh.WaterTriangles.Length>0,"A legitimate shallow shore must retain water");
        Check(mesh.WaterPositions.Where((_,i)=>i%3==1).All(y=>y<.03),"Dry-corner water potentials cannot pull a shoreline upward into a floating sheet");
        var baseline=mesh.WaterPositions.ToArray();samples[1].WaterSurfaceM=-3000;
        mesh=Triangle();LocalWaterMesh.Build(mesh,frame,samples);
        Check(baseline.SequenceEqual(mesh.WaterPositions),"Changing an unrelated dry potential cannot change the water mesh at all");
        foreach(var kind in new[]{LocalWaterKind.Ocean,LocalWaterKind.Lake})
        {
            for(int i=0;i<3;i++){samples[i].WaterKind=kind;samples[i].WaterBodyKey=17;samples[i].WaterFootprintM=100;}
            samples[1].WaterSurfaceM=2000;
            mesh=Triangle();LocalWaterMesh.Build(mesh,frame,samples);
            Check(mesh.WaterTriangles.Length>0&&mesh.WaterPositions.Where((_,i)=>i%3==1).All(y=>y<.03),"Standing water retains one level even beside conflicting dry potential: "+kind);
            // Every emitted point lies at/above its ACTUAL supporting triangle,
            // and no point crosses onto the high dry side (ground=-2+.75*x).
            {
                double edge=mesh.WaterPositions.Where((_,i)=>i%3==0).Max();
                Check(Math.Abs(edge-(2/.75+LocalWaterMesh.OceanShoreOverlapM))<.002,"Ocean extends two horizontal metres under shore, not a slope-dependent vertical pad");
                Check(mesh.WaterPositions.Where((_,i)=>i%3==1).All(y=>Math.Abs(y)<.001),"Ocean underlap stays at sea level without a lifted rim");
            }
        }
        // Two disconnected lakes at different levels must not become a slope.
        samples[0]=new LocalTerrainSample{ElevationM=-2,WaterSurfaceM=0,WaterMaskM=2,WaterKind=LocalWaterKind.Lake,WaterBodyKey=1,WaterFootprintM=1};
        samples[2]=new LocalTerrainSample{ElevationM=-2,WaterSurfaceM=100,WaterMaskM=102,WaterKind=LocalWaterKind.Lake,WaterBodyKey=2,WaterFootprintM=1};
        mesh=Triangle();LocalWaterMesh.Build(mesh,frame,samples);
        foreach(int triangle in Enumerable.Range(0,mesh.WaterTriangles.Length/3))
        {
            var ys=Enumerable.Range(0,3).Select(k=>mesh.WaterPositions[mesh.WaterTriangles[triangle*3+k]*3+1]).ToArray();
            Check(ys.Max()-ys.Min()<.002,"A render triangle never joins different standing-water levels");
        }
        foreach(double slope in new[]{.001,.1,4d})
        {
            mesh=Triangle();
            double[] ground={-6*slope,10*slope,-6*slope};
            for(int i=0;i<3;i++)mesh.Positions[i*3+1]=(float)ground[i];
            samples=ground.Select(h=>new LocalTerrainSample{ElevationM=h,WaterSurfaceM=0,WaterMaskM=-h,
                WaterKind=LocalWaterKind.Ocean,WaterBodyKey=1,WaterFootprintM=double.PositiveInfinity}).ToArray();
            double ya=frame.SurfaceY(frame.Position(0,0),0),yb=frame.SurfaceY(frame.Position(16,0),0),yc=frame.SurfaceY(frame.Position(0,16),0);
            double da=ya-mesh.Positions[1],gx=(yb-mesh.Positions[4]-da)/16,gz=(yc-mesh.Positions[7]-da)/16;
            double norm=Math.Sqrt(gx*gx+gz*gz),furthest=0;
            LocalWaterMesh.Build(mesh,frame,samples);
            for(int i=0;i<mesh.WaterPositions.Length;i+=3)
            {
                double x=mesh.WaterPositions[i],z=mesh.WaterPositions[i+2];
                furthest=Math.Max(furthest,-(da+gx*x+gz*z)/norm);
                Check(Math.Abs(mesh.WaterPositions[i+1]-(ya+(yb-ya)*x/16+(yc-ya)*z/16))<.001,"Ocean buffer never changes the sea surface elevation");
            }
            Check(furthest>1.99&&furthest<2.01,"Shallow and steep shores use a bounded horizontal underlap, not a large vertical flood tolerance");
        }
        samples=Enumerable.Range(0,3).Select(_=>new LocalTerrainSample{ElevationM=10,WaterSurfaceM=0,WaterMaskM=-10,WaterKind=LocalWaterKind.Ocean}).ToArray();
        mesh=Triangle();for(int i=0;i<3;i++)mesh.Positions[i*3+1]=10;
        LocalWaterMesh.Build(mesh,frame,samples);
        Check(mesh.WaterTriangles.Length==0,"Ocean overlap does not start water beneath wholly dry inland triangles");
        // A large island's submerged flank remains ocean even after subdivision
        // replaces the original sea/land vertices with ordinary terrain nodes.
        var recipe=Recipe.Preset("Preview");recipe.Detail=5000;
        var surface=SphericalMesh.Build(recipe,default);int n=surface.Cells.Length,island=221;
        var h=new HydrologyResult{Receiver=Enumerable.Repeat(-1,n).ToArray(),VisitOrder=Enumerable.Range(0,n).ToArray(),
            OceanConnected=Enumerable.Repeat(true,n).ToArray(),WaterBody=Enumerable.Repeat(-1,n).ToArray(),LakeCoverageFraction=new double[n],
            NorthernSpringDischargeM3PerS=new double[n],NorthernSummerDischargeM3PerS=new double[n],NorthernAutumnDischargeM3PerS=new double[n],NorthernWinterDischargeM3PerS=new double[n]};
        var world=new World{Recipe=recipe,Surface=surface,ElevationM=Enumerable.Repeat(-2000,n).ToArray(),Hydrology=h};
        world.ElevationM[island]=3000;h.OceanConnected[island]=false;
        var terrain=new LocalTerrain(world,horizontalScale:.1);var at=surface.Cells[island].Position;
        int submerged=0;
        foreach(int sea in surface.Cells[island].Neighbors)
        for(int j=1;j<32;j++)
        {
            double t=j/32d;var point=(at*(1-t)+surface.Cells[sea].Position*t).Unit;var s=terrain.Patch(point,2000).Sample(point);
            if(s.ElevationM>=-.1)continue;
            submerged++;Check(s.Underwater&&s.WaterSurfaceM==0&&s.WaterKind==LocalWaterKind.Ocean,
                $"Submarine island flank cannot lose ocean at {s.ElevationM:F1}m: fraction {t:F3}");
        }
        Check(submerged>40,"Ocean regression actually visits deep submarine island slopes, not just shoreline points");
        samples=new[]{
            new LocalTerrainSample{ElevationM=-2,WaterSurfaceM=0,WaterMaskM=2,WaterKind=LocalWaterKind.Lake,ContinuousWaterSurface=true,WaterFootprintM=double.PositiveInfinity},
            new LocalTerrainSample{ElevationM=10,WaterSurfaceM=2000,WaterMaskM=-10,WaterKind=LocalWaterKind.Lake,ContinuousWaterSurface=true,WaterFootprintM=double.PositiveInfinity},
            new LocalTerrainSample{ElevationM=-2,WaterSurfaceM=0,WaterMaskM=2,WaterKind=LocalWaterKind.Lake,ContinuousWaterSurface=true,WaterFootprintM=double.PositiveInfinity}};
        mesh=Triangle();LocalWaterMesh.Build(mesh,frame,samples);
        Check(ContainedBoundary(mesh)>0,"A stale paired-field flag cannot bypass constant standing-water clipping");
        Check(mesh.WaterPositions.Where((_,i)=>i%3==1).All(y=>y<.03),"A mountain-side dry potential cannot warp a lake even with the old paired-field flag set");
        samples[1].WaterKind=LocalWaterKind.None;samples[1].WaterBodyKey=99;samples[1].WaterFootprintM=-100;
        mesh=Triangle();LocalWaterMesh.Build(mesh,frame,samples);
        Check(ContainedBoundary(mesh)>0,"Losing an inherited lake ID on the dry bank cannot truncate water above its bed");
        Check(mesh.WaterPositions.Where((_,i)=>i%3==0).Max()>2/.75,"Lake coverage continues through the true shore into its hidden underlap, not an ID midpoint");
        // A closed basin owns its connected faces and retains its dry divide
        // under local refinement, without a fine raster of the whole planet.
        h.OceanConnected=new bool[n];h.Waters=new[]{new WaterBody{Id=0,Kind=WaterKind.Lake,Endorheic=true,SurfaceElevationM=100,Cells=new[]{island}}};
        h.WaterBody=Enumerable.Repeat(-1,n).ToArray();h.WaterBody[island]=0;h.LakeCoverageFraction=new double[n];h.LakeCoverageFraction[island]=1;
        world.ElevationM=Enumerable.Repeat(101,n).ToArray();world.ElevationM[island]=90;
        terrain=new LocalTerrain(world,horizontalScale:.1);
        Check(terrain.Amplification.LakeBasinCount==1&&terrain.Amplification.RejectedLakeBasins==0,"Synthetic closed lake is accepted as a connected basin");
        int wetLake=0;
        foreach(int neighbour in surface.Cells[island].Neighbors)for(int j=0;j<=24;j++)
        {
            double t=j/24d;var p=(surface.Cells[island].Position*(1-t)+surface.Cells[neighbour].Position*t).Unit;var v=terrain.Patch(p,2000).Sample(p);
            if(v.ElevationM>=100)continue;
            Check(v.Underwater&&v.WaterKind==LocalWaterKind.Lake&&v.WaterSurfaceM==100,"Connected submerged shore retains the same fixed lake datum");wetLake++;
        }
        Check(wetLake>20,"Lake basin regression actually samples submerged banks");
        Console.WriteLine("PASS "+checks+" water coupling checks");
    }

    static int ContainedBoundary(LocalTerrainMesh mesh,Func<double,double,double,bool> include=null,Action<double,double,double> trace=null)
    {
        var incidence=new Dictionary<(int,int),int>();
        void Edge(int a,int b){var pair=(Math.Min(a,b),Math.Max(a,b));incidence.TryGetValue(pair,out int count);incidence[pair]=count+1;}
        for(int k=0;k<mesh.WaterTriangles.Length;k+=3)
        {int a=mesh.WaterTriangles[k],b=mesh.WaterTriangles[k+1],c=mesh.WaterTriangles[k+2];Edge(a,b);Edge(b,c);Edge(c,a);}
        double Ground(double x,double z)
        {
            int n=mesh.Segments,side=n+1;double gx=x/mesh.SizeM*n,gz=z/mesh.SizeM*n;
            int a=Math.Max(0,Math.Min(n-1,(int)Math.Floor(gx))),b=Math.Max(0,Math.Min(n-1,(int)Math.Floor(gz)));
            double u=gx-a,v=gz-b;
            var p=mesh.Positions;double h00=p[(b*side+a)*3+1],h10=p[(b*side+a+1)*3+1],h01=p[((b+1)*side+a)*3+1];
            if(u+v<=1)return h00+(h10-h00)*u+(h01-h00)*v;
            double h11=p[((b+1)*side+a+1)*3+1];return h11+(h01-h11)*(1-u)+(h10-h11)*(1-v);
        }
        int checkedEdges=0;
        bool Covered(double x,double z,double y,double edgeX,double edgeZ)
        {
            var p=mesh.WaterPositions;var t=mesh.WaterTriangles;
            for(int k=0;k<t.Length;k+=3)
            {
                int a=t[k]*3,b=t[k+1]*3,c=t[k+2]*3;
                double bx=p[b]-p[a],bz=p[b+2]-p[a+2],cx=p[c]-p[a],cz=p[c+2]-p[a+2],det=bx*cz-cx*bz;
                if(Math.Abs(det)<1e-12)continue;
                double u=((x-p[a])*cz-(z-p[a+2])*cx)/det,v=(bx*(z-p[a+2])-bz*(x-p[a]))/det;
                // Coverage is tested to either SIDE; continuity is evaluated
                // AT the shared edge. Comparing both side probes to one fixed
                // height wrongly reports holes on legitimately sloping rivers.
                double eu=((edgeX-p[a])*cz-(edgeZ-p[a+2])*cx)/det,ev=(bx*(edgeZ-p[a+2])-bz*(edgeX-p[a]))/det;
                if(u>=-1e-7&&v>=-1e-7&&u+v<=1.0000001&&Math.Abs(p[a+1]+eu*(p[b+1]-p[a+1])+ev*(p[c+1]-p[a+1])-y)<.005)return true;
            }
            return false;
        }
        foreach(var pair in incidence.Where(p=>p.Value==1))
        {
            var p=mesh.WaterPositions;int ia=pair.Key.Item1*3,ib=pair.Key.Item2*3;
            double x=(p[ia]+p[ib])*.5,z=(p[ia+2]+p[ib+2])*.5,y=(p[ia+1]+p[ib+1])*.5;
            if(include!=null&&!include(x,z,y))continue;
            double margin=Math.Max(.0001,mesh.SizeM*.000001);
            if(x<=margin||z<=margin||x>=mesh.SizeM-margin||z>=mesh.SizeM-margin)continue;
            if(mesh.Positions.Length==9&&Math.Abs(x+z-mesh.SizeM)<margin)continue;
            double ground=mesh.Positions.Length==9?-2+.75*x:Ground(x,z);
            // Conservative underlap gives adjacent triangles unequal hidden
            // edge lengths. A one-incidence index edge is not an exposed edge
            // if water actually covers BOTH sides of its midpoint.
            if(y-ground>.055)
            {
                double dx=p[ib]-p[ia],dz=p[ib+2]-p[ia+2],len=Math.Sqrt(dx*dx+dz*dz);
                double eps=Math.Min(.01,len*.01);dx=dx/len*eps;dz=dz/len*eps;
                if(Covered(x-dz,z+dx,y,x,z)&&Covered(x+dz,z-dx,y,x,z))continue;
            }
            if(y-ground>.055){trace?.Invoke(x,z,y);throw new Exception($"Exposed lateral water edge {y-ground:F3}m above drawn bed at {x:F3},{z:F3}; tile {mesh.SizeM}/{mesh.Segments}");}
            checkedEdges++;
        }
        return checkedEdges;
    }

    public static void World(string path)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));string hash=StateHash.Compute(world);
        var watch=System.Diagnostics.Stopwatch.StartNew();var terrain=new LocalTerrain(world,horizontalScale:.1);
        var reaches=terrain.Hydrology.Channels.Where(c=>c.StartM>20&&c.StartM<2500&&c.WidthM>10).Take(6).ToArray();
        var sites=reaches.Select(r=>("river-"+r.From,terrain.Amplification.RiverPoint(r.From,.5))).ToList();
        foreach(int i in Enumerable.Range(0,world.ElevationM.Length).Where(i=>world.Hydrology.OceanConnected[i]&&world.Surface.Cells[i].Neighbors.Any(n=>!world.Hydrology.OceanConnected[n])).Take(2))sites.Add(("coast-"+i,world.Surface.Cells[i].Position));
        foreach(int i in Enumerable.Range(0,world.ElevationM.Length).Where(i=>world.Hydrology.LakeCoverageFraction[i]>.7&&world.Hydrology.WaterBody[i]>=0).Take(2))sites.Add(("lake-"+i,world.Surface.Cells[i].Position));
        int checkedVertices=0,unsupported=0,bankEdges=0;double worstError=0,maxDepth=0,maxRaise=0;
        foreach(var site in sites)
        {
            var patch=terrain.Patch(site.Item2,24000);var center=patch.Sample(0,0);
            var frame=new LocalTerrainFrame(site.Item2,terrain.RadiusM,center.ElevationM,.1);
            foreach(var shape in new[]{(204.8,32),(3276.8,16)})
            {
                double tileRaise=0;
                var mesh=LocalTerrainMesh.Build(terrain,frame,-shape.Item1/2,-shape.Item1/2,shape.Item1,shape.Item2);
                if(!mesh.WaterPositions.All(float.IsFinite))throw new Exception("Nonfinite water mesh");
                bankEdges+=ContainedBoundary(mesh,trace:(xx,zz,yy)=>{
                    Console.WriteLine($"EDGE {site.Item1} at {xx:R},{zz:R},{yy:R}");
                    var edgeAt=frame.Position(mesh.EastM+xx,mesh.NorthM+zz);var edgeSample=patch.Sample(edgeAt);
                    Console.WriteLine($"seaY {frame.SurfaceY(edgeAt,0):R}; exact kind {edgeSample.WaterKind}, level {edgeSample.WaterSurfaceM:R}, exactY {frame.SurfaceY(edgeAt,edgeSample.WaterSurfaceM):R}");
                    var wp=mesh.WaterPositions;int printed=0;
                    for(int kk=0;kk<mesh.WaterTriangles.Length;kk+=3)
                    {
                        bool near=false;for(int vv=0;vv<3;vv++){int at=mesh.WaterTriangles[kk+vv]*3;if(Math.Abs(wp[at]-xx)<80&&Math.Abs(wp[at+2]-zz)<80)near=true;}
                        if(!near||printed++>=8)continue;
                        Console.WriteLine("water triangle "+string.Join(" / ",Enumerable.Range(0,3).Select(vv=>{int at=mesh.WaterTriangles[kk+vv]*3;return $"{wp[at]:R},{wp[at+2]:R}: {wp[at+1]:R}";})));
                    }
                    double spacing=mesh.SizeM/mesh.Segments;
                    int ix=(int)(xx/spacing),iz=(int)(zz/spacing);
                    for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                    {
                        double x=(ix+dx)*spacing,z=(iz+dz)*spacing;var at=frame.Position(mesh.EastM+x,mesh.NorthM+z);var ss=patch.Sample(at);
                        Console.WriteLine($"{x:F1},{z:F1}: h{ss.ElevationM:R},w{ss.WaterSurfaceM:R}, {ss.WaterKind}/{ss.WaterBodyKey}, mask{ss.WaterMaskM:R},range{ss.WaterMinimumM:R}..{ss.WaterMaximumM:R}");
                    }
                });
                for(int k=0;k<mesh.WaterPositions.Length;k+=3)
                {
                    double x=mesh.EastM+mesh.WaterPositions[k],z=mesh.NorthM+mesh.WaterPositions[k+2];var at=frame.Position(x,z);var s=patch.Sample(at);
                    if(s.ElevationM>s.PreWaterElevationM+.002)throw new Exception("Water shaping raised terrain instead of cutting its bed: "+site.Item1);
                    maxRaise=Math.Max(maxRaise,s.ElevationM-s.PreWaterElevationM);
                    tileRaise=Math.Max(tileRaise,s.ElevationM-s.PreWaterElevationM);
                    if(double.IsNaN(s.WaterSurfaceM)||s.WaterMaskM<-.5){unsupported++;continue;}
                    double expected=frame.SurfaceY(at,s.WaterSurfaceM)+.025;
                    double error=Math.Abs(mesh.WaterPositions[k+1]-expected);worstError=Math.Max(worstError,error);
                    if(shape.Item2==32&&error>5&&mesh.WaterPositions[k+1]>frame.SurfaceY(at,s.ElevationM)+.01)
                        throw new Exception($"Unsupported VISIBLE water level at {site.Item1}: error {error:F2}m, drawn {mesh.WaterPositions[k+1]:F2}, ground {frame.SurfaceY(at,s.ElevationM):F2}, expected {expected:F2}; {s.WaterKind}");
                    checkedVertices++;maxDepth=Math.Max(maxDepth,s.WaterSurfaceM-s.ElevationM);
                }
                Console.WriteLine($"{site.Item1} {shape.Item1:F0}m: {mesh.WaterTriangles.Length/3} triangles; {mesh.BuildMilliseconds:F1}ms; bank raise {tileRaise:F2}m");
            }
        }
        if(StateHash.Compute(world)!=hash)throw new Exception("Water coupling changed authoritative world/history");
        Console.WriteLine($"PASS {checkedVertices} actual-world water vertices; {bankEdges} contained lateral bank edges; {unsupported} outside exact footprint (linear LOD approximation); worst level error {worstError:F3}m; max sampled depth {maxDepth:F2}m; maximum bank raise {maxRaise:F2}m; total {watch.Elapsed.TotalSeconds:F2}s; unchanged {hash}");
    }

    public static void OceanWorld(string path)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));string hash=StateHash.Compute(world);
        var terrain=new LocalTerrain(world,horizontalScale:.1);int count=0;
        var coast=Enumerable.Range(0,world.ElevationM.Length).Where(i=>!world.Hydrology.OceanConnected[i]&&world.ElevationM[i]>0&&world.Surface.Cells[i].Neighbors.Any(j=>world.Hydrology.OceanConnected[j])).ToArray();
        foreach(int i in coast.Where((_,k)=>k%Math.Max(1,coast.Length/32)==0).Take(32))
        {
            int sea=world.Surface.Cells[i].Neighbors.First(j=>world.Hydrology.OceanConnected[j]);
            for(int j=0;j<=32;j++)
            {
                double t=j/32d;var at=(world.Surface.Cells[i].Position*(1-t)+world.Surface.Cells[sea].Position*t).Unit;
                var sample=terrain.Patch(at,2000).Sample(at);
                if(sample.ElevationM>=-.1)continue;
                count++;if(!sample.Underwater||sample.WaterKind!=LocalWaterKind.Ocean||sample.WaterSurfaceM!=0)
                    throw new Exception($"Ocean ends over submerged shelf at cell {i}, fraction {t:F3}, bed {sample.ElevationM:F1}m: {sample.WaterKind}, {sample.WaterSurfaceM}, footprint {sample.WaterFootprintM}, mask {sample.WaterMaskM}");
            }
        }
        if(count<32||StateHash.Compute(world)!=hash)throw new Exception("Ocean audit lacked submerged slopes or changed world state");
        Console.WriteLine($"PASS {count} actual-world submerged coastal slope points; world unchanged {hash}");
    }
}
