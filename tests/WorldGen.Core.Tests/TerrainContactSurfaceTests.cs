using WorldGen.Presentation;

static class TerrainContactSurfaceTests
{
    public static void Run()
    {
        int checks=0;
        void Check(bool ok,string reason){if(!ok)throw new Exception(reason);checks++;}
        // A steep wall and a cut riverbed share the same regular corner heights.
        // The extra centre is part of the final mesh and must own contact height.
        var mesh=new LocalTerrainMesh{SizeM=16,Segments=4,GroundVertices=25,
            Positions=new float[]{0,0,0, 16,160,0, 0,0,16, 16,160,16, 8,-4,8},
            Triangles=new[]{0,2,4, 0,4,1, 1,4,3, 2,3,4}};
        var surface=TerrainContactSurface.Build(mesh)!;
        Check(surface.TryHeight(8,8,out double y)&&Math.Abs(y+4)<1e-8,"Actual refined riverbed centre, not regular-grid80m, owns contact");
        Check(surface.TryHeight(12,8,out y)&&Math.Abs(y-78)<1e-8,"Steep rendered triangle owns player support");
        Check(surface.TryHeight(0,8,out y)&&Math.Abs(y)<1e-8,"Shared edge retains exact contact");
        Check(surface.TryHeight(16,16,out y)&&Math.Abs(y-160)<1e-8,"Inclusive outer boundary resolves");
        Check(!surface.TryHeight(-1,8,out y),"Unowned query does not invent ground");
        Check(!surface.TryHeight(double.NaN,8,out y),"Invalid coordinates do not alias a grid cell");
        mesh.Positions[13]=90;
        Check(surface.TryHeight(8,8,out y)&&Math.Abs(y+4)<1e-8,"Contact retains geometry after transport arrays are reused");
        long originalBytes=surface.Bytes;surface.SetHeight(0,40);surface.SetHeight(2,40);
        Check(surface.TryHeight(0,8,out y)&&Math.Abs(y-40)<1e-8,"Contact follows a conformed resident edge");
        Check(surface.TryHeight(4,8,out y)&&Math.Abs(y-18)<1e-8,"Interior triangle interpolates its changed edge and unchanged riverbed");
        Check(surface.TryOriginalHeight(0,8,out y)&&Math.Abs(y)<1e-8&&surface.OriginalHeight(0)==0,"Canonical edge sampling survives resident fitting");
        Check(surface.TryOriginalHeight(4,8,out y)&&Math.Abs(y+2)<1e-8,"Original triangle interpolation is independent of fitted heights");
        Check(surface.Bytes==originalBytes+5*4,"Only edited contacts retain one extra height per vertex");
        surface.SetHeight(0,surface.OriginalHeight(0));surface.SetHeight(2,surface.OriginalHeight(2));
        Check(surface.TryHeight(4,8,out y)&&Math.Abs(y+2)<1e-8,"Restoring original edge vertices restores contact interpolation");
        mesh.SizeM=4096;var distant=TerrainContactSurface.Build(mesh);
        Check(distant!=null&&distant.TryHeight(0,8,out y)&&Math.Abs(y)<1e-8,"Refined distant resident edges retain their actual triangle contact");
        var regular=new LocalTerrainMesh{SizeM=16,Segments=4,GroundVertices=25,Positions=new float[75],Triangles=LocalTerrainMesh.SurfaceTriangles(4)};
        Check(TerrainContactSurface.Build(regular)==null,"Ordinary regular-grid interpolation has no duplicate contact data");
        Console.WriteLine("Terrain final-triangle contact PASS "+checks);
    }
}
