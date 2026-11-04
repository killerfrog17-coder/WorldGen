using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class CanyonTests
{
    public static void Controls()
    {
        int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        foreach(double width in new[]{400d,1000,2800})foreach(double relief in new[]{150d,500,1200})
        {
            var reach=new CanyonValleys.Reach{HalfWidth=width,Relief=relief,Strength=.8};double previous=0;
            for(int i=0;i<=1000;i++)
            {
                double distance=width*i/1000,level=650,top=level+1000;
                double floor=reach.Floor(distance,level),cut=reach.Cut(top,distance,level,out double exposure);
                Check(double.IsFinite(floor)&&floor>=level&&floor>=previous,"Terraced canyon walls rise continuously away from the floor");
                Check(double.IsFinite(cut)&&cut<=top&&cut>=level,"Canyon primitives only cut dry ground and never cut below their river datum");
                Check(exposure>=0&&exposure<=1,"Canyon rock exposure stays bounded");previous=floor;
                Check(reach.Cut(level-4,distance,level,out _)==level-4,"A pre-existing wet bed is never raised by a broad valley");
            }
            Check(reach.Cut(1650,width,650,out _)==1650&&Math.Abs(reach.Cut(1650,width*.999999,650,out _)-1650)<1e-7,"Canyon support retires continuously before neighbouring uncut ground");
            Check(reach.Uplift(0)>0&&reach.Uplift(width*12)==0&&reach.Uplift(width*12.01)==0,"The surrounding uplift belongs to a finite plateau province");
            var distances=new HashSet<int>();
            for(int i=0;i<1000;i++)
            {
                var at=new V(1700+i*17,300,54000-i*7);double d=width*.8;
                double eroded=CanyonValleys.ErodedDistance(at,73421,d,width);
                Check(double.IsFinite(eroded)&&eroded>=d*.55&&eroded<=d*1.15,"Erosional shoulders and gullies remain bounded");
                Check(CanyonValleys.ErodedDistance(at,73421,0,width)==0,"Wall erosion preserves the parent drainage datum");
                Check(CanyonValleys.ErodedDistance(at,73421,width*1.6,width)==width*1.6,"Wall erosion is local to the canyon");
                Check(Math.Abs(CanyonValleys.ErodedDistance(at,73421,width*(1.6-1e-7),width)-width*(1.6-1e-7))<1e-8,"Gully support retires continuously");
                double next=CanyonValleys.ErodedDistance(at+new V(.001,0,0),73421,d,width);
                Check(Math.Abs(next-eroded)<.1,$"Gully coordinates remain continuous across arbitrary tile edges: width {width}, point {at}, {eroded:R} -> {next:R}");
                distances.Add((int)Math.Round(eroded));
            }
            Check(distances.Count>50,"Canyon shoulders vary along their length rather than making constant-width tracks");
        }
        Console.WriteLine($"PASS {checks} bounded cut-only canyon profiles and exposed beds");
    }
    public static void World(string path,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadFile(path);var clock=System.Diagnostics.Stopwatch.StartNew();
        var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);var canyons=terrain.Amplification.Canyons;
        Console.WriteLine($"Canyon candidate reaches {canyons.Reaches.Length}, indexed faces {canyons.IndexedFaces}; prepare {clock.Elapsed.TotalSeconds:F2}s");
        var rows=new List<object>();
        foreach(var reach in canyons.Reaches.OrderByDescending(r=>r.Strength*r.Relief).Take(40))
        {
            var spans=terrain.Amplification.RiverReach(reach.Source);if(spans.Length<4)continue;
            var s=spans[spans.Length/2];V center=(s.A+s.B).Unit,side=(s.SideA+s.SideB).Unit;
            var patch=terrain.Patch(center,80000);double floor=patch.Sample(center).ElevationM;
            V coarse=(reach.A+reach.B).Unit,coarseSide=V.Cross(coarse,reach.B-reach.A).Unit;
            var wall=patch.Sample((coarse+coarseSide*(reach.HalfWidth*.45/(terrain.RadiusM*.1))).Unit);
            Console.WriteLine($"Canyon probe {reach.Source}: strength {reach.Strength:F3}, width {reach.HalfWidth:F0}, datum {reach.LevelA:F1}..{reach.LevelB:F1}, floor {floor:F1}, wall {wall.ElevationM:F1}, exposed {wall.Province.CanyonExposure:F3}");
            if(wall.Province.CanyonExposure<.05)continue;
            V rim=(center+side*(reach.HalfWidth*1.05/(terrain.RadiusM*.1))).Unit;
            double high=patch.Sample(rim).ElevationM;if(high-floor<80)continue;
            var frame=new LocalTerrainFrame(center,terrain.RadiusM,0,.1);
            var a=LocalTerrainMesh.Build(terrain,frame,-128,-64,128,32);var b=LocalTerrainMesh.Build(terrain,frame,0,-64,128,32);
            if(a.WaterTriangles.Length==0&&b.WaterTriangles.Length==0)throw new Exception("Incised canyon has no rendered river");
            void Check(bool ok,string why){if(!ok)throw new Exception("Canyon water: "+why);}
            RiverscapeTests.Boundary(a,Check,frame,patch);RiverscapeTests.Boundary(b,Check,frame,patch);
            for(int z=0;z<=32;z++)if(a.Positions[(z*33+32)*3+1]!=b.Positions[z*33*3+1])throw new Exception("Canyon shared tile edge differs");
            if(a.Positions.Any(x=>!float.IsFinite(x))||b.Positions.Any(x=>!float.IsFinite(x)))throw new Exception("Nonfinite canyon mesh");
            rows.Add(new{reach.Source,Position=new[]{center.X,center.Y,center.Z},reach.HalfWidth,reach.Relief,reach.Strength,Floor=floor,Rim=high});
            if(rows.Count==4)break;
        }
        if(rows.Count<2)throw new Exception("Actual world does not supply two genuinely incised canyon contexts");
        File.WriteAllText(Path.Combine(output,"canyons.json"),System.Text.Json.JsonSerializer.Serialize(rows,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {rows.Count} actual incised river contexts, finite meshes and exact shared edges; {clock.Elapsed.TotalSeconds:F2}s");
    }
}
