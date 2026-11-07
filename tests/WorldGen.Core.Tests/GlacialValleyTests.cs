using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

static class GlacialValleyTests
{
    public static void Controls()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        foreach(double width in new[]{380d,900,1900})foreach(double relief in new[]{120d,500,950})
        foreach(double strength in new[]{.1,.5,1})
        {
            var reach=new GlacialValleys.Reach(0,1,new V(1,0,0),new V(1,.01,0).Unit,width,relief,strength);
            double previous=double.NegativeInfinity;
            for(int j=0;j<=1000;j++)
            {
                double distance=j*width/800,level=200,ground=1000;
                double floor=reach.Floor(distance,level),cut=reach.Cut(ground,distance,level,out double exposure);
                Check(double.IsFinite(floor)&&floor>=previous,"U-shaped section rises monotonically from the floor");previous=floor;
                Check(double.IsFinite(cut)&&cut<=ground&&cut>=level,"Glacial erosion never raises terrain or cuts under its river datum");
                Check(exposure>=0&&exposure<=1,"Exposed valley wall mask is bounded");
                if(distance>=width)Check(cut==ground&&exposure==0,"Finite trough support returns exactly to inherited terrain");
                if(distance<=width*.28)Check(exposure==0,"Valley floor retains depositional vegetation and alluvium");
                Check(reach.Cut(100,distance,level,out double untouched)==100&&untouched==0,"Pre-existing deeper channels are not filled or painted as newly eroded walls");
            }
            Check(reach.Floor(width*.25,200)-reach.Floor(0,200)<relief*.002,"Wide glacial floor is gentle relative to the enclosing walls");
            Check(reach.Floor(width*.85,200)-reach.Floor(width*.35,200)>relief*.4,"Trough has a distinct steep-sided U cross section");
            double justInside=reach.Cut(1000,width*(1-1e-6),200,out double edgeExposure);
            Check(Math.Abs(justInside-1000)<1e-6&&edgeExposure<1e-6,"Trough and exposure meet unmodified terrain continuously");
        }
        var exposed=new LandscapeControls{GlacialExposure=1};
        Check(LandscapeControls.Blend(default,exposed,.5).ExposedRock==.5,"Glacial wall exposure blends with shared physical material controls");
        Check(LandscapeProvince.RootedAdmission(exposed,new V(1000,2000,3000),731)==0,"Bare valley wall does not retain rooted vegetation");
        var weathered=LandscapeProvince.Ground(new GroundProfile{DepthM=1,Organic=.2},exposed,new V(1000,2000,3000),731);
        Check(weathered.DepthM<.01&&weathered.Organic<.001,"Eroded wall removes soil and organics consistently with its rock material");
        Console.WriteLine($"PASS {checks} glacial trough controls");
    }
    public static void World(string path,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadFile(path);string history=world.Prehistory?.Hash;
        var clock=System.Diagnostics.Stopwatch.StartNew();var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);double prepared=clock.Elapsed.TotalSeconds;
        var valleys=terrain.Amplification.Glaciers;double radius=terrain.RadiusM*.1;
        Console.WriteLine($"Glacial reaches {valleys.Reaches.Length}, faces {valleys.IndexedFaces}, index {valleys.IndexBytes} bytes, max candidates {valleys.MaximumCandidates}; prepare {prepared:F2}s");
        if(valleys.Reaches.Length==0)throw new Exception("No genuinely glaciated mountain drainage in the actual world");
        int probes=0,focused=0;double Dot(V a,V b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        for(int face=0;face<world.Surface.Triangles.Length/3;face++)
        {
            bool focus=valleys.CandidateCount(face)>0&&focused<160;
            if(!focus&&face%701!=0)continue;if(focus)focused++;
            var ids=world.Surface.Triangles;V point=(world.Surface.Cells[ids[face*3]].Position+world.Surface.Cells[ids[face*3+1]].Position+world.Surface.Cells[ids[face*3+2]].Position).Unit;
            double indexed=valleys.Apply(point,face,3000,out double exposure),brute=3000,bare=0;
            foreach(var r in valleys.Reaches)
            {
                V d=r.B-r.A,p=point-r.A;double raw=Dot(p,d)/Math.Max(1e-30,Dot(d,d));
                brute=r.Cut(brute,(p-d*D.Clamp(raw)).Length*radius,valleys.Level(r,raw),out double b);bare=Math.Max(bare,b);
            }
            if(Math.Abs(indexed-brute)>1e-9||Math.Abs(exposure-bare)>1e-9)throw new Exception("Numeric trough index missed a physical footprint at face "+face);
            probes++;
        }
        var rows=new List<object>();var checkedPoints=new List<(V Point,LocalTerrainSample Sample)>();
        foreach(var r in valleys.Reaches.OrderByDescending(r=>r.Strength*r.Relief).Take(80))
        {
            var spans=terrain.Amplification.RiverReach(r.Source);if(spans.Length<4)continue;
            var span=spans[spans.Length/2];V center=(span.A+span.B).Unit,coarse=(r.A+r.B).Unit,side=V.Cross(coarse,r.B-r.A).Unit;
            var patch=terrain.Patch(center,60000);var floor=patch.Sample(center);
            LocalTerrainSample Wall(int sign)=>patch.Sample((coarse+side*(sign*r.HalfWidth*.6/radius)).Unit);
            var left=Wall(-1);var right=Wall(1);int direction=left.Province.GlacialExposure>right.Province.GlacialExposure?-1:1;var wall=direction<0?left:right;
            var rim=patch.Sample((coarse+side*(direction*r.HalfWidth*1.1/radius)).Unit);
            Console.WriteLine($"Trough {r.Source}: width {r.HalfWidth:F0}, strength {r.Strength:F2}, floor {floor.ElevationM:F0}, wall {wall.ElevationM:F0}, exposure {wall.Province.GlacialExposure:F3}, rim {rim.ElevationM:F0}");
            if(wall.Province.GlacialExposure<.03||rim.ElevationM-floor.ElevationM<80)continue;
            if(!floor.Underwater||floor.WaterKind!=LocalWaterKind.River)throw new Exception("Actual glacial floor lost its modern river");
            var frame=new LocalTerrainFrame(center,terrain.RadiusM,0,.1);
            var a=LocalTerrainMesh.Build(terrain,frame,-128,-64,128,32);var b=LocalTerrainMesh.Build(terrain,frame,0,-64,128,32);
            if(a.WaterTriangles.Length+b.WaterTriangles.Length==0)throw new Exception("Glacial floor has no rendered water triangles");
            void Check(bool ok,string why){if(!ok)throw new Exception("Glacial water: "+why);}
            RiverscapeTests.Boundary(a,Check,frame,patch);RiverscapeTests.Boundary(b,Check,frame,patch);
            for(int z=0;z<=32;z++)if(a.Positions[(z*33+32)*3+1]!=b.Positions[z*33*3+1])throw new Exception("Glacial valley shared mesh edge differs");
            if(a.Positions.Any(x=>!float.IsFinite(x))||b.Positions.Any(x=>!float.IsFinite(x)))throw new Exception("Nonfinite glacial valley mesh");
            var transect=new List<object>();
            for(int j=-24;j<=24;j++)
            {
                double distance=r.HalfWidth*j/20;V p=(coarse+side*(distance/radius)).Unit;var s=patch.Sample(p);
                if(Math.Abs(patch.Elevation(p)-s.ElevationM)>.002)throw new Exception("Height-only glacial query differs from shared surface");
                checkedPoints.Add((p,s));
                transect.Add(new{Distance=distance,s.ElevationM,Exposure=s.Province.GlacialExposure});
            }
            rows.Add(new{r.Source,Position=new{center.X,center.Y,center.Z},r.HalfWidth,r.Relief,r.Strength,Floor=floor.ElevationM,Rim=rim.ElevationM,Exposure=wall.Province.GlacialExposure,Transect=transect});
            if(rows.Count==4)break;
        }
        if(rows.Count<2)throw new Exception("Fewer than two actual glacial troughs have a river and exposed sides: "+rows.Count);
        var warm=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        if(!warm.Amplification.UsedCachedRiverDatums)throw new Exception("Completed glacial river derivation was not reused");
        foreach(var point in checkedPoints.AsEnumerable().Reverse())
        {
            var reconstructed=warm.Patch(point.Point,5000).Sample(point.Point);
            if(StateHash.Compute(reconstructed)!=StateHash.Compute(point.Sample))throw new Exception("Warm/reverse-order glacial reconstruction changed terrain, water, soil or vegetation");
        }
        if(world.Prehistory?.Hash!=history)throw new Exception("Local glacial terrain changed broad history");
        File.WriteAllText(Path.Combine(output,"glacial-valleys.json"),System.Text.Json.JsonSerializer.Serialize(new{PreparedSeconds=prepared,Reaches=valleys.Reaches.Length,valleys.IndexedFaces,valleys.IndexBytes,valleys.MaximumCandidates,IndexProbes=probes,ExactCachedSamples=checkedPoints.Count,Sites=rows},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {rows.Count} actual glacial river valleys, {probes} exact index comparisons, terrain/water boundaries/shared edges/history; {clock.Elapsed.TotalSeconds:F2}s");
    }
}
