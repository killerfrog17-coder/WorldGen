using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class CliffRockTests
{
    public static void Run(string path)
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        foreach(RockFamily family in Enum.GetValues<RockFamily>())for(int shape=0;shape<8;shape++)
        {
            int code=CliffRocks.Encode(family,shape);Check(CliffRocks.Is(code)&&CliffRocks.Family(code)==family&&CliffRocks.Model(code)>=0&&CliffRocks.Model(code)<3,"Cliff anatomy retains lithology and a valid source model");
        }
        int downFaces=0;
        for(int source=0;source<3;source++)for(int lod=0;lod<3;lod++)
        {
            string file=$"unity/WorldGen.Unity/Assets/WorldGen/Resources/CliffRockV1/cliff-{source}-l{lod}.bytes";
            using var reader=new BinaryReader(File.OpenRead(file));Check(reader.ReadUInt32()==0x31435257,"Recorded cliff geometry header");
            int vertices=reader.ReadInt32(),indices=reader.ReadInt32();Check(indices/3<=new[]{3200,1050,260}[lod],"Cliff LOD respects the triangle budget");
            var positions=new V[vertices];
            for(int i=0;i<vertices;i++)
            {
                positions[i]=new V(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());var normal=new V(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                Check(double.IsFinite(positions[i].X)&&double.IsFinite(positions[i].Y)&&double.IsFinite(positions[i].Z)&&normal.Length>.9&&normal.Length<1.1,"Scan geometry and normals are finite and usable");reader.ReadSingle();reader.ReadSingle();
            }
            var edges=new Dictionary<(int,int),int>();double facing=0;
            for(int t=0;t<indices;t+=3)
            {
                int a=reader.ReadInt32(),b=reader.ReadInt32(),c=reader.ReadInt32();Check(a>=0&&a<vertices&&b>=0&&b<vertices&&c>=0&&c<vertices,"Cliff indices remain in range");
                foreach(var edge in new[]{(a,b),(b,c),(c,a)}){var sorted=(Math.Min(edge.Item1,edge.Item2),Math.Max(edge.Item1,edge.Item2));edges.TryGetValue(sorted,out int count);edges[sorted]=count+1;}
                var cross=V.Cross(positions[b]-positions[a],positions[c]-positions[a]);if(cross.Y<-.00001)downFaces++;facing+=cross.Z;
            }
            Check(edges.Values.All(n=>n<=2)&&facing>0,"Prepared scan faces outward without nonmanifold edges; embedded ground supplies its interior");
            Check(reader.BaseStream.Position==reader.BaseStream.Length,"Cliff data is complete without trailing arrays");
        }
        Check(downFaces>50,"Prepared geometry contains real downward-facing ledge surfaces");
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
                    double up=V.Dot(rock.Normal,rock.Position);
                    Check(up>0&&up<.62&&Math.Abs(rock.Normal.Length-1)<1e-6,"Actual face normal fits a steep wall rather than an upright floating slab");
                }
            }
            Check(found>0,"Real qualifying canyon terrain produces cliff geometry");Console.WriteLine($"Actual cliffs {found}; first-build aggregate {elapsed:F1}ms, not a frame-time measurement");
        }
        Console.WriteLine($"PASS {checks} cliff shape, ledge and placement checks");
    }
}
