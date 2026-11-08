using WorldGen.Core;
using WorldGen.Presentation;

static class LocalLakeWaterTests
{
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
