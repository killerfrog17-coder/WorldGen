using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class RiverMeshDiagnostic
{
    public static void Run(string input,int source,double x,double z,string output)
    {
        var world=WorldFile.ReadFile(input);var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        var spans=terrain.Amplification.RiverReach(source);V center=default;bool found=false;
        foreach(var span in spans.Where(s=>s.LevelA>5&&s.LevelB>1&&Math.Min(s.WidthA,s.WidthB)>250).OrderBy(s=>Math.Abs(s.LevelA-s.LevelB)))
        {
            V at=(span.A+span.B).Unit;var sample=terrain.Patch(at,40000).Geometry(at);
            if(sample.Underwater&&sample.WaterKind==LocalWaterKind.River&&sample.WaterSurfaceM-sample.ElevationM>10){center=at;found=true;break;}
        }
        if(!found)throw new Exception("No actual deep major-river diagnostic site "+source);
        var frame=new LocalTerrainFrame(center,terrain.RadiusM,0,.1);const double size=3276.8;
        double east=-size/2+size/16*.37,north=-size/2+size/16*.27;
        var mesh=LocalTerrainMesh.Build(terrain,frame,east,north,size,16);var patch=terrain.Patch(center,40000);
        var rows=new List<object>();var triangles=mesh.WaterClipTriangles.Length>0?mesh.WaterClipTriangles:mesh.Triangles;
        object Actual(int id)
        {
            double ax=mesh.Positions[id*3],az=mesh.Positions[id*3+2],y=mesh.Positions[id*3+1];
            V p=frame.Position(mesh.EastM+ax,mesh.NorthM+az);var s=patch.Geometry(p);
            return new{Id=id,X=ax,Z=az,DrawnY=y,FieldY=frame.SurfaceY(p,s.ElevationM),PreWaterY=frame.SurfaceY(p,s.PreWaterElevationM),WaterY=frame.SurfaceY(p,s.WaterSurfaceM),Kind=s.WaterKind.ToString(),s.WaterBodyKey,s.WaterMaskM};
        }
        for(int i=0;i<triangles.Length;i+=3)
        {
            int a=triangles[i],b=triangles[i+1],c=triangles[i+2];var p=mesh.Positions;
            double ax=p[a*3],az=p[a*3+2],bx=p[b*3]-ax,bz=p[b*3+2]-az,cx=p[c*3]-ax,cz=p[c*3+2]-az,det=bx*cz-bz*cx;
            if(Math.Abs(det)<1e-12)continue;
            double u=((x-ax)*cz-(z-az)*cx)/det,v=(bx*(z-az)-bz*(x-ax))/det;
            if(u< -1e-5||v< -1e-5||u+v>1.00001)continue;
            rows.Add(new{Triangle=i/3,Area=det*.5,U=u,V=v,DrawnY=p[a*3+1]+(p[b*3+1]-p[a*3+1])*u+(p[c*3+1]-p[a*3+1])*v,Vertices=new[]{Actual(a),Actual(b),Actual(c)}});
        }
        V target=frame.Position(mesh.EastM+x,mesh.NorthM+z);var actual=patch.Geometry(target);
        var beforeIndex=actual;patch.RiverSpans();var afterIndex=patch.Geometry(target);
        var probes=new List<object>();foreach(double dz in new[]{-.02,0,.02})foreach(double dx in new[]{-.02,0,.02})
        {V at=frame.Position(mesh.EastM+x+dx,mesh.NorthM+z+dz);var s=patch.Geometry(at);probes.Add(new{dx,dz,Height=frame.SurfaceY(at,s.ElevationM),s.WaterSurfaceM,s.WaterBodyKey,s.WaterMaskM});}
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output,JsonSerializer.Serialize(new{Version=LocalTerrain.Version,Source=source,Center=new[]{center.X,center.Y,center.Z},X=x,Z=z,FieldY=frame.SurfaceY(target,actual.ElevationM),PreWaterY=frame.SurfaceY(target,actual.PreWaterElevationM),BeforeIndex=beforeIndex.ElevationM,AfterIndex=afterIndex.ElevationM,BeforeWater=beforeIndex.WaterSurfaceM,AfterWater=afterIndex.WaterSurfaceM,ContainingGroundTriangles=rows,NearbyProbes=probes},new JsonSerializerOptions{WriteIndented=true,NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals}));
        Console.WriteLine($"Index before {beforeIndex.ElevationM:R}, after {afterIndex.ElevationM:R}; water {beforeIndex.WaterSurfaceM:R}/{afterIndex.WaterSurfaceM:R}");
        Console.WriteLine($"Diagnostic {source}: {rows.Count} containing ground triangles, field {frame.SurfaceY(target,actual.ElevationM):F5}m; {output}");
    }
}
