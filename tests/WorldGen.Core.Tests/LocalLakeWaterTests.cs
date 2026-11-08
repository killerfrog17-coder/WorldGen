using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
using System.Text.Json;

static class LocalLakeWaterTests
{
    public static void ProbeEdge(string worldPath,string edgePath,string output)
    {
        using var json=JsonDocument.Parse(File.ReadAllText(edgePath));var root=json.RootElement;
        V Vector(string name){var p=root.GetProperty(name);return new V(p[0].GetDouble(),p[1].GetDouble(),p[2].GetDouble());}
        var world=WorldFile.ReadPlanetaryFile(worldPath);var model=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        V point=Vector("Point");double radius=model.RadiusM*.1;var patch=model.Patch(point,3000);
        var physical=patch.Sample(point);var raw=patch.SampleMap(point,129);
        double Dot(V a,V b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        var rows=new List<(double distance,object value)>();
        foreach(var span in patch.RiverSpans())
        {
            V d=span.B-span.A,p=point-span.A;double u=Math.Clamp(Dot(p,d)/Math.Max(1e-30,Dot(d,d)),0,1);
            double distance=(p-d*u).Length*radius,width=span.WidthA*(1-u)+span.WidthB*u;
            if(distance>width+15)continue;
            V side=(span.SideA*(1-u)+span.SideB*u).Unit;
            V bank=(span.A*(1-u)+span.B*u+side*(Math.Sign(Dot(p,side))*(width+2)/radius)).Unit;
            var bed=patch.SampleMap(bank,129);
            rows.Add((Math.Abs(distance-width-2),new{span.Reach,span.Receiver,span.From,span.To,U=u,Distance=distance,Width=width,span.WidthA,span.WidthB,span.LevelA,span.LevelB,Primitive=span.Primitive.ToString(),BankOriginal=bed.ElevationM,OriginalAtPoint=raw.ElevationM,ActualAtPoint=physical.ElevationM}));
        }
        var selected=rows.OrderBy(r=>r.distance).Take(12).Select(r=>r.value).ToArray();
        File.WriteAllText(output,JsonSerializer.Serialize(selected,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(File.ReadAllText(output));
    }
    public static void ReviewMesh(string path,double x,double z)
    {
        using var reader=new BinaryReader(File.OpenRead(path));var mesh=TerrainMeshWire.ReadMesh(reader);
        var edges=new Dictionary<(int,int),int>();
        for(int i=0;i<mesh.WaterTriangles.Length;i+=3)for(int j=0;j<3;j++)
        {int a=mesh.WaterTriangles[i+j],b=mesh.WaterTriangles[i+(j+1)%3];var key=(Math.Min(a,b),Math.Max(a,b));edges.TryGetValue(key,out int count);edges[key]=count+1;}
        foreach(var row in edges.Where(p=>p.Value==1).OrderBy(p=>{
            int a=p.Key.Item1*3,b=p.Key.Item2*3;
            double dx=(mesh.WaterPositions[a]+mesh.WaterPositions[b])*.5-x,dz=(mesh.WaterPositions[a+2]+mesh.WaterPositions[b+2])*.5-z;return dx*dx+dz*dz;
        }).Take(3))
        {
            foreach(int i in new[]{row.Key.Item1,row.Key.Item2})Console.WriteLine($"edge vertex {i}: {mesh.WaterPositions[i*3]:R},{mesh.WaterPositions[i*3+1]:R},{mesh.WaterPositions[i*3+2]:R}; payload {string.Join(',',mesh.WaterColors.Skip(i*4).Take(4))}");
        }
        Console.WriteLine($"Land vertices {mesh.Positions.Length/3}, triangles {mesh.Triangles.Length/3}; water vertices {mesh.WaterPositions.Length/3}, triangles {mesh.WaterTriangles.Length/3}");
    }
    public static void Run()
    {
        int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        var frame=new LocalTerrainFrame(new V(1,0,0),637100,0,1);
        const ulong identity=0x8000000000000021UL;
        var lake=new SmallLake{Id=identity,Center=frame.Origin,East=frame.East,North=frame.North,Radius=40,Aspect=1,Level=0,Depth=2};
        LocalTerrainMesh Triangle(double size)=>new LocalTerrainMesh{Segments=1,SizeM=size,
            Positions=new float[]{0,-5,0,(float)size,-5,0,0,-5,(float)size},WaterClipTriangles=new[]{0,2,1}};
        LocalTerrainSample Wet(LocalWaterKind kind,ulong id)=>new LocalTerrainSample{ElevationM=-5,WaterSurfaceM=0,WaterMaskM=5,
            WaterKind=kind,WaterBodyKey=id,WaterFootprintM=lake.BedExtent*lake.Radius};
        int resolutions=0;
        SmallLake Resolve(ulong id,V at){resolutions++;Check(id==identity,"Only the encountered local body is resolved");return lake;}
        var samples=new[]{Wet(LocalWaterKind.Lake,identity),new LocalTerrainSample{ElevationM=-5,WaterSurfaceM=double.NaN,WaterMaskM=-1},new LocalTerrainSample{ElevationM=-5,WaterSurfaceM=9999,WaterMaskM=-1}};
        var mesh=Triangle(100);LocalWaterMesh.Build(mesh,frame,samples,localLakeAt:Resolve);
        Check(resolutions==1,"Local body lookup is once per body, not per triangle or corner");
        Check(mesh.WaterPositions.Where((v,i)=>i%3==0).Max()>50,"A missing dry-corner ID cannot truncate a known pond halfway across its footprint");
        var baseline=mesh.WaterPositions.ToArray();var indices=mesh.WaterTriangles.ToArray();
        samples[1].WaterKind=LocalWaterKind.Lake;samples[1].WaterBodyKey=44;samples[1].WaterSurfaceM=-9999;
        mesh=Triangle(100);LocalWaterMesh.Build(mesh,frame,samples,localLakeAt:Resolve);
        Check(baseline.SequenceEqual(mesh.WaterPositions)&&indices.SequenceEqual(mesh.WaterTriangles),"Unrelated dry-corner potentials and IDs cannot change the known pond");

        // A neighbouring refined triangle may retain no wet vertex at all.
        // Its missing local identity cannot punch a hole in a detected body.
        var dry=new LocalTerrainSample{ElevationM=-5,WaterSurfaceM=double.NaN,WaterMaskM=-1};
        samples=new[]{Wet(LocalWaterKind.Lake,identity),dry,dry,dry,dry,dry};
        mesh=new LocalTerrainMesh{Segments=1,SizeM=100,Positions=new float[]{0,-5,0,5,-5,0,0,-5,5,10,-5,10,20,-5,10,10,-5,20},WaterClipTriangles=new[]{0,2,1,3,5,4}};
        LocalWaterMesh.Build(mesh,frame,samples,localLakeAt:Resolve);
        Check(mesh.WaterPositions.Where((v,i)=>i%3==0).Max()>19.9,"Known pond coverage crosses a neighbouring triangle with no wet sample");

        lake.Radius=1000;
        samples=new[]{Wet(LocalWaterKind.Lake,identity),Wet(LocalWaterKind.Lake,identity),Wet(LocalWaterKind.Lake,identity)};
        mesh=Triangle(512);LocalWaterMesh.Build(mesh,frame,samples,localLakeAt:Resolve);
        Check(mesh.WaterTriangles.Length>3,"A coarse finite pond receives bounded spherical-datum refinement");
        double maximumError=0;
        for(int i=0;i<mesh.WaterTriangles.Length;i+=3)
        {
            double x=0,y=0,z=0;for(int j=0;j<3;j++){int at=mesh.WaterTriangles[i+j]*3;x+=mesh.WaterPositions[at]/3d;y+=mesh.WaterPositions[at+1]/3d;z+=mesh.WaterPositions[at+2]/3d;}
            double exact=frame.SurfaceY(frame.Position(x,z),0);maximumError=Math.Max(maximumError,Math.Abs(y-exact));
            Check(Math.Abs(y-exact)<.004,"The drawn pond stays on its physical datum at triangle interiors, not only vertices");
        }
        int before=resolutions;samples=new[]{Wet(LocalWaterKind.Ocean,1),Wet(LocalWaterKind.Ocean,1),Wet(LocalWaterKind.Ocean,1)};
        mesh=Triangle(512);LocalWaterMesh.Build(mesh,frame,samples,localLakeAt:Resolve);
        Check(mesh.WaterTriangles.Length==3&&resolutions==before,"The finite-pond fix does not refine an ocean or perform pond lookups there");
        Console.WriteLine($"PASS {checks} local pond footprint and curvature checks; maximum datum error {maximumError:F6}m");
    }
}
