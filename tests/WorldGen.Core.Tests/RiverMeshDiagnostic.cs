using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class RiverMeshDiagnostic
{
    public static void Cost(string input,int source,string output,string cache)
    {
        var world=WorldFile.ReadFile(input);var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1,directory:cache);
        var span=terrain.Amplification.RiverReach(source).First(s=>s.LevelA>5&&s.LevelB>1&&Math.Min(s.WidthA,s.WidthB)>250);
        V center=(span.A+span.B).Unit;var frame=new LocalTerrainFrame(center,terrain.RadiusM,0,.1);var rows=new List<object>();
        foreach(var shape in new[]{(size:3276.8,n:16),(size:13107.2,n:8)})
        {
            var timing=new TerrainBuildTiming();var mesh=LocalTerrainMesh.Build(terrain,frame,-shape.size/2,-shape.size/2,shape.size,shape.n,timing:timing);
            rows.Add(new{shape.size,shape.n,mesh.BuildMilliseconds,GroundVertices=mesh.Positions.Length/3,WaterTriangles=mesh.WaterTriangles.Length/3,timing.FullSurfaceQueries,timing.GeometryQueries,Stages=TerrainBuildTiming.Names.Select((name,i)=>new{Name=name,Ms=timing.Milliseconds[i]}).ToArray()});
            Console.WriteLine($"River tile {shape.size}/{shape.n}: {mesh.BuildMilliseconds:F1}ms, {mesh.Positions.Length/3} ground vertices; "+string.Join("; ",timing.Milliseconds.Select((ms,i)=>TerrainBuildTiming.Names[i]+" "+ms.ToString("F1"))));
        }
        File.WriteAllText(output,JsonSerializer.Serialize(new{Version=LocalTerrain.Version,Source=source,Scope="Isolated CoreCLR mesh component; not game FPS or complete loading time",Rows=rows},new JsonSerializerOptions{WriteIndented=true}));
    }
    public static void Point(string input,V point,string output,string cache)
    {
        var world=WorldFile.ReadFile(input);var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1,directory:cache);var patch=terrain.Patch(point,40000);
        var actual=patch.Geometry(point);var spans=patch.RiverSpans();var rows=new List<object>();double Dot(V a,V b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        foreach(var s in spans)
        {
            V d=s.B-s.A,p=point-s.A;double u=D.Clamp(Dot(p,d)/Math.Max(1e-30,Dot(d,d))),distance=(p-d*u).Length*terrain.RadiusM*.1,width=s.WidthA+(s.WidthB-s.WidthA)*u;
            if(distance>width*5+8)continue;
            rows.Add(new{s.Reach,s.Receiver,s.From,s.To,s.PoolA,s.PoolB,Primitive=s.Primitive.ToString(),s.LevelA,s.LevelB,s.WidthA,s.WidthB,s.DepthA,s.DepthB,U=u,Distance=distance,Margin=width-distance,Length=d.Length*terrain.RadiusM*.1,A=new[]{s.A.X,s.A.Y,s.A.Z},B=new[]{s.B.X,s.B.Y,s.B.Z}});
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllText(output,JsonSerializer.Serialize(new{Version=LocalTerrain.Version,Point=new[]{point.X,point.Y,point.Z},actual.ElevationM,actual.PreWaterElevationM,actual.WaterSurfaceM,actual.WaterMaskM,actual.WaterBodyKey,Kind=actual.WaterKind.ToString(),Spans=rows},new JsonSerializerOptions{WriteIndented=true,NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals}));
        Console.WriteLine($"Point: {actual.WaterKind}/{actual.WaterBodyKey}, stage {actual.WaterSurfaceM:R}, ground {actual.ElevationM:R}; {rows.Count} nearby profiles");
    }
    public static void Nodes(string input,string output,int[] sources,string cache=null)
    {
        var world=WorldFile.ReadFile(input);var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1,directory:cache);var h=world.Hydrology;
        var amp=terrain.Amplification;var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var nodes=(Array)amp.GetType().GetField("rootNodes",flags)!.GetValue(amp)!;
        var accepted=((int[])amp.GetType().GetField("lakeFaces",flags)!.GetValue(amp)!).Where(x=>x>=0).ToHashSet();
        var owners=(int[])amp.GetType().GetField("lakeOutletOwners",flags)!.GetValue(amp)!;
        var datums=amp.ExportRiverDatums();var collars=(double[])amp.GetType().GetField("riverCollarRadii",flags)!.GetValue(amp)!;
        var seen=new HashSet<int>();var rows=new List<object>();
        foreach(int source in sources)
        {
            int id=source;
            for(int k=0;k<12&&id>=0;k++)
            {
                if(!seen.Add(id))break;object node=nodes.GetValue(id)!;var t=node.GetType();object Field(string name)=>t.GetField(name)!.GetValue(node)!;
                int body=h.WaterBody[id];object lake=body<0?null!:new{h.Waters[body].Id,h.Waters[body].SurfaceElevationM,h.Waters[body].OutletCell,h.Waters[body].DownstreamCell,h.Waters[body].Endorheic};
                var spans=amp.RiverReach(id);var position=world.Surface.Cells[id].Position;var actual=terrain.Patch(position,30000).Geometry(position);
                object raw=amp.GetType().GetMethod("FitActualRiverNode",flags)!.Invoke(amp,new object[]{id})!;
                object fitted=amp.GetType().GetMethod("ActualRiverNode",flags)!.Invoke(amp,new object[]{id})!;
                object Stage(object value)=>new{Level=value.GetType().GetField("Level")!.GetValue(value),Radius=value.GetType().GetField("Radius")!.GetValue(value)};
                rows.Add(new{Id=id,Receiver=h.Receiver[id],Ocean=h.OceanConnected[id],RawElevation=world.ElevationM[id],Type=Field("Type").ToString(),Height=Field("Height"),Crust=Field("Crust"),Water=Field("Water"),LakeKey=Field("LakeKey"),RootLakeAccepted=accepted.Contains((int)(ulong)Field("LakeKey")-1),BodyAccepted=accepted.Contains(body),OutletOwner=owners[id],Flow=Field("Flow"),Datum=datums[id],Collar=collars[id],RawStage=Stage(raw),FittedStage=Stage(fitted),Body=lake,Actual=new{actual.ElevationM,actual.WaterSurfaceM,Kind=actual.WaterKind.ToString(),actual.WaterBodyKey},Spans=spans.Length,Head=spans.Length>0?new[]{spans[0].LevelA,spans[^1].LevelB}:Array.Empty<double>()});
                id=h.Receiver[id];
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true,NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals}));
        Console.WriteLine("Node diagnosis: "+rows.Count+" actual nodes; "+output);
    }
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
