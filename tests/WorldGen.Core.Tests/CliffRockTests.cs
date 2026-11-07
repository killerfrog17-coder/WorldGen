using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class CliffRockTests
{
    public static void Run(string path)
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var signatures=new HashSet<string>();int ledges=0;
        foreach(RockFamily family in Enum.GetValues<RockFamily>())for(int shape=0;shape<8;shape++)
        {
            int code=CliffRocks.Encode(family,shape);Check(CliffRocks.Is(code)&&CliffRocks.Family(code)==family,"Cliff anatomy retains its exact lithology");
            var points=new List<V>();
            for(int row=0;row<CliffRocks.Rows(code);row++)for(int column=0;column<3;column++)for(int face=0;face<6;face++)
            for(int z=0;z<=3;z++)for(int x=0;x<=3;x++)
            {
                var p=CliffRocks.Surface(code,row,column,face,x*2d/3-1,z*2d/3-1);
                Check(double.IsFinite(p.X)&&double.IsFinite(p.Y)&&double.IsFinite(p.Z)&&Math.Abs(p.X)<1&&p.Y>-.2&&p.Y<1.2&&Math.Abs(p.Z)<.6,"Jointed block surface is finite and bounded at every resolution");points.Add(p);
            }
            signatures.Add(string.Join("|",points.Where((_,i)=>i%97==0).Select(p=>$"{p.X:R},{p.Y:R},{p.Z:R}")));
            double Front(int row)=>Enumerable.Range(0,3).SelectMany(column=>new[]{-1d,0,1}.SelectMany(u=>new[]{-1d,0,1}.Select(v=>CliffRocks.Surface(code,row,column,4,u,v).Z))).Max();
            if(Enumerable.Range(0,CliffRocks.Rows(code)-1).Any(row=>Front(row+1)-Front(row)>.025))ledges++;
        }
        Check(signatures.Count==48&&ledges>32,"Lineages vary and provide projecting ledges rather than one repeated ellipsoid");
        var wet=new LocalTerrainSample{ElevationM=5,WaterSurfaceM=10,WaterMaskM=1,Biome=Biome.TemperateForest};
        Check(!CliffRocks.TryCreate(1,new V(1,0,0),wet,null,637100,32,1,out _),"Submerged ground cannot create cliff masses or trigger terrain probes");
        if(path!=null)
        {
            var world=WorldFile.ReadFile(path);var model=new LocalTerrain(world,horizontalScale:.1);var scatter=new LocalScatter(model,.1);
            var sites=model.Amplification.Canyons.Reaches.OrderByDescending(r=>r.Relief).Take(3)
                .SelectMany(r=>new[]{-.65,.65}.Select(side=>((r.A+r.B).Unit+V.Cross((r.A+r.B).Unit,r.B-r.A).Unit*(r.HalfWidth*side/(model.RadiusM*.1))).Unit)).ToArray();
            if(sites.Length==0)sites=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>world.ElevationM[i]>30).OrderByDescending(i=>model.Profile(i).MountainHeightM).Take(6).Select(i=>world.Surface.Cells[i].Position).ToArray();
            int found=0;double elapsed=0;
            foreach(var key in sites.Select(p=>scatter.Address(p)).Distinct())
            {
                var watch=System.Diagnostics.Stopwatch.StartNew();var a=scatter.Build(key);elapsed+=watch.Elapsed.TotalMilliseconds;
                var b=scatter.Build(key);Check(a.Instances.Length==b.Instances.Length&&a.Instances.Zip(b.Instances).All(p=>p.First.Id==p.Second.Id&&p.First.Variant==p.Second.Variant&&p.First.GroundM==p.Second.GroundM),"Repeated patch reconstruction retains identities and physical anchors");
                Check(a.Instances.Select(p=>p.Id).Distinct().Count()==a.Instances.Length,"Cliff insertion never duplicates an existing rock identity");
                foreach(var rock in a.Instances.Where(p=>p.Kind==ScatterKind.Outcrop&&CliffRocks.Is(p.Variant)))
                {
                    found++;var sample=model.Patch(rock.Position,3000).Sample(rock.Position);
                    Check(Math.Abs(rock.GroundM-sample.ElevationM)<.001&&rock.HeightM>=12&&rock.HeightM<=280&&rock.WidthM>0,"Actual cliff remains fitted to the shared physical terrain");
                    Check(Math.Abs(V.Dot(rock.Normal,rock.Position))<1e-6&&Math.Abs(rock.Normal.Length-1)<1e-6,"Outward wall direction is tangent to the radial vertical axis");
                }
            }
            Check(found>0,"Real qualifying canyon terrain produces cliff geometry");Console.WriteLine($"Actual cliffs {found}; first-build aggregate {elapsed:F1}ms, not a frame-time measurement");
        }
        Console.WriteLine($"PASS {checks} cliff shape, ledge and placement checks");
    }
}
