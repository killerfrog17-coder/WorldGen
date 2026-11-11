using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class RiverLodSeamTests
{
    readonly record struct Edge(double A,double B,double YA,double YB);
    static List<Edge> Boundary(LocalTerrainMesh mesh,bool right)
    {
        var result=new List<Edge>();double x=right?mesh.SizeM:0;var p=mesh.Positions;
        for(int i=0;i<mesh.Triangles.Length;i+=3)for(int j=0;j<3;j++)
        {
            int a=mesh.Triangles[i+j]*3,b=mesh.Triangles[i+(j+1)%3]*3;
            if(Math.Abs(p[a]-x)>.001||Math.Abs(p[b]-x)>.001||Math.Abs(p[a+2]-p[b+2])<.00001)continue;
            result.Add(new Edge(mesh.NorthM+p[a+2],mesh.NorthM+p[b+2],p[a+1],p[b+1]));
        }
        return result;
    }
    static double Height(List<Edge> edges,double at)
    {
        double result=double.NegativeInfinity;
        foreach(var e in edges)
        {
            if(at<Math.Min(e.A,e.B)-.0002||at>Math.Max(e.A,e.B)+.0002)continue;
            result=Math.Max(result,e.YA+(e.YB-e.YA)*D.Clamp((at-e.A)/(e.B-e.A)));
        }
        return result;
    }
    public static void Run(string path,int source)
    {
        var world=WorldFile.ReadFile(path);var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        var spans=terrain.Amplification.RiverReach(source);if(spans.Length==0)throw new Exception("Explicit mixed-LOD site is not an actual river");
        var s=spans[spans.Length/2];V at=((s.A+s.B).Unit+(s.SideA+s.SideB).Unit*((s.WidthA+s.WidthB)*.5/(terrain.RadiusM*.1))).Unit;
        var frame=new LocalTerrainFrame(at,terrain.RadiusM,0,.1);var patch=terrain.Patch(at,20000);
        var keys=new[]{new TerrainTileKey(-1,-1,3,.1),new TerrainTileKey(-1,0,3,.1),new TerrainTileKey(0,-1,2,.1),new TerrainTileKey(0,0,2,.1)};
        var plan=keys.ToDictionary(k=>k,k=>k.Level==3?16:32);var steps=TerrainEdges.Build(plan);
        var meshes=keys.Select(k=>LocalTerrainMesh.Build(terrain,frame,k.X*k.SizeM,k.Z*k.SizeM,k.SizeM,plan[k],edgeSteps:steps[k])).ToArray();
        if(meshes.All(m=>m.WaterClipTriangles.Length==0))throw new Exception("Mixed-LOD bank fixture did not exercise actual river constraints");
        int probes=0,hidden=0;double worst=0,worstAt=0;
        for(int half=0;half<2;half++)
        {
            var coarse=Boundary(meshes[half],true);var fine=Boundary(meshes[half+2],false);
            double low=meshes[half+2].NorthM,high=low+meshes[half+2].SizeM;
            var cuts=coarse.Concat(fine).SelectMany(e=>new[]{e.A,e.B}).Append(low).Append(high).Where(z=>z>=low&&z<=high).Distinct().OrderBy(z=>z).ToArray();
            var positions=cuts.Concat(cuts.Zip(cuts.Skip(1),(a,b)=>(a+b)*.5));
            foreach(double z in positions)
            {
                double a=Height(coarse,z),b=Height(fine,z);if(!double.IsFinite(a)||!double.IsFinite(b))throw new Exception("Actual drawn terrain boundary has an uncovered interval");
                V p=frame.Position(0,z);var sample=patch.Geometry(p);double water=frame.SurfaceY(p,sample.WaterSurfaceM);
                probes++;if(sample.Underwater&&Math.Max(a,b)<water-.005){hidden++;continue;}
                double gap=Math.Abs(a-b);if(gap>worst){worst=gap;worstAt=z;}
            }
        }
        Console.WriteLine($"River {source} mixed-LOD: {probes} actual edge/end/midpoint probes, {hidden} submerged; visible gap {worst:R}m at {worstAt:R}");
        if(probes<20||worst>.025)throw new Exception("Visible river-bank boundary opens across actual coarse/fine meshes");
        Console.WriteLine("PASS drawn coarse/fine river boundary");
    }
}
