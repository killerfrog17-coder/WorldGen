using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class RiverGroundSeamTests
{
    public static void Run(string source)
    {
        var world=WorldFile.ReadFile(source);var terrain=new LocalTerrain(world,horizontalScale:.1);
        var at=TerrainMapViewport.FromDegrees(-17.625665274156873,.6638456954785965);
        var frame=new LocalTerrainFrame(at,terrain.RadiusM,0,.1);int checkedMeshes=0,failures=0;
        foreach(int segments in new[]{32,64,128})foreach(var tile in new[]{(-1,0),(-1,-1),(0,-1)})
        {
            const double size=102.4;var mesh=LocalTerrainMesh.Build(terrain,frame,tile.Item1*size,tile.Item2*size,size,segments);
            var edges=new Dictionary<(int,int),int>();double projectedArea=0;int inverted=0;
            for(int i=0;i<mesh.Triangles.Length;i+=3)
            {
                var p=mesh.Positions;int aa=mesh.Triangles[i]*3,bb=mesh.Triangles[i+1]*3,cc=mesh.Triangles[i+2]*3;
                double area=((double)p[bb]-p[aa])*(p[cc+2]-p[aa+2])-((double)p[bb+2]-p[aa+2])*(p[cc]-p[aa]);
                // Vertex welding already permits 1 mm of coordinate movement.
                // Almost-collinear pre-existing slivers can reverse within that
                // uncertainty; reject folds extending beyond it, independently
                // of the exact (zero-tolerance) shared-edge ownership check.
                double EdgeLength(int a,int b)=>Math.Sqrt(Math.Pow((double)p[a]-p[b],2)+Math.Pow((double)p[a+2]-p[b+2],2));
                if(area>.001*Math.Max(EdgeLength(aa,bb),Math.Max(EdgeLength(bb,cc),EdgeLength(cc,aa))))inverted++;
                projectedArea-=area*.5;
                for(int k=0;k<3;k++)
                {
                    int a=mesh.Triangles[i+k],b=mesh.Triangles[i+(k+1)%3];if(a==b)continue;
                    var key=a<b?(a,b):(b,a);edges.TryGetValue(key,out int count);edges[key]=count+1;
                }
            }
            bool Outer(int a,int b)
            {
                var p=mesh.Positions;
                foreach(int axis in new[]{0,2})foreach(double end in new[]{0d,size})
                    if(Math.Abs(p[a*3+axis]-end)<.001&&Math.Abs(p[b*3+axis]-end)<.001)return true;
                return false;
            }
            int open=edges.Count(e=>e.Value==1&&!Outer(e.Key.Item1,e.Key.Item2));checkedMeshes++;
            Console.WriteLine($"Ground seam {tile}/{segments}: {open} interior open edges; {inverted} folds beyond weld precision; area {projectedArea:R}");
            if(open!=0||inverted!=0||Math.Abs(projectedArea-size*size)>size*size*1e-6)failures++;
        }
        if(failures!=0)throw new Exception($"River bank terrain triangulation failed in {failures}/{checkedMeshes} meshes");
        Console.WriteLine($"PASS {checkedMeshes} river-bank ground meshes have no interior open edges");
    }
}
