using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class WaterWorldAudit
{
    public static void Run(string planet,string sessionPath,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadFile(planet);var play=WorldLibrary.ReadPlaySession(sessionPath);
        double scale=play?.Settings.HorizontalScale??.1;var terrain=new LocalTerrain(world,horizontalScale:scale);
        Console.WriteLine("WORLD "+world.Recipe.TerrainSource+" seed "+world.Recipe.Seed+" scale "+scale+" radius "+terrain.RadiusM);
        var at=play?.PlayerPosition??new V(1,0,0);TerrainMapViewport.Degrees(at,out double lat,out double lon);Console.WriteLine("Saved player "+lat+", "+lon+"; heading "+play?.Heading+" pitch "+play?.Pitch);
        var mouths=terrain.Hydrology.Channels.Where(c=>c.EndM<=0&&c.WidthM>5).OrderBy(c=>((c.A+c.B).Unit-at).Length).Take(160).Where(c=>terrain.Amplification.RiverReach(c.From).Any(s=>s.LevelB==0)).Take(40).ToArray();
        Console.WriteLine("Actual active sea-datum reaches "+mouths.Length);
        var csv=new StringBuilder("reach,t,lat,lon,level_a,level_b,ground,water,kind,wet,halfwidth\n");int dry=0,total=0,collars=0;var cases=new System.Collections.Generic.List<object>();
        foreach(var c in mouths)
        {
            var point=terrain.Amplification.RiverPoint(c.From,.9);var spans=terrain.Amplification.RiverReach(c.From);
            foreach(var span in spans)
            {
                if(span.From>=0&&Math.Abs(span.LevelB-span.LevelA)>.001||span.To>=0&&Math.Abs(span.LevelB-span.LevelA)>.001)collars++;
                if(span.LevelA!=0||span.LevelB!=0)continue;
                var midpoint=(span.A+span.B).Unit;var stationPatch=terrain.Patch(midpoint,Math.Max(3000,(span.B-span.A).Length*terrain.RadiusM/scale));
                foreach(double f in new[]{.2,.5,.8})
                {
                    point=(span.A*(1-f)+span.B*f).Unit;var sample=stationPatch.Sample(point);TerrainMapViewport.Degrees(point,out double a,out double b);total++;
                    csv.AppendLine(string.Join(",",c.From,f,a,b,span.LevelA,span.LevelB,sample.ElevationM,sample.WaterSurfaceM,sample.WaterKind,sample.Underwater,span.WidthA));
                    if(!sample.Underwater&&sample.ElevationM>span.LevelA+.03)
                    {dry++;if(cases.Count<12)cases.Add(new{reach=c.From,latitude=a,longitude=b,ground=sample.ElevationM,water=sample.WaterSurfaceM,kind=sample.WaterKind.ToString(),width=span.WidthA});}
                }
            }
        }
        File.WriteAllText(Path.Combine(output,"mouth-centres.csv"),csv.ToString());File.WriteAllText(Path.Combine(output,"bad-mouths.json"),JsonSerializer.Serialize(cases,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Sea-level stations "+total+"; water hidden beneath dry ground "+dry+"; nonflat endpoint collars "+collars);
        File.WriteAllText(Path.Combine(output,"summary.txt"),"TerrainSource "+world.Recipe.TerrainSource+"\nScale "+scale+"\nPlayer "+lat+", "+lon+"\nSea stations "+total+"\nDry mouths "+dry+"\nNonflat endpoint collars "+collars);
        double Drawn(float[] p,int[] triangles,double x,double z)
        {
            double top=double.NegativeInfinity;
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i]*3,b=triangles[i+1]*3,c=triangles[i+2]*3;double bx=p[b]-p[a],bz=p[b+2]-p[a+2],cx=p[c]-p[a],cz=p[c+2]-p[a+2],det=bx*cz-bz*cx;if(Math.Abs(det)<1e-10)continue;
                double u=((x-p[a])*cz-(z-p[a+2])*cx)/det,v=(bx*(z-p[a+2])-bz*(x-p[a]))/det;
                if(u>=-1e-6&&v>=-1e-6&&u+v<=1.000001)top=Math.Max(top,p[a+1]+u*(p[b+1]-p[a+1])+v*(p[c+1]-p[a+1]));
            }
            return top;
        }
        var rendered=new System.Collections.Generic.List<object>();int tried=0;
        foreach(var c in mouths)
        {
            var options=terrain.Amplification.RiverReach(c.From).Where(s=>s.LevelA==0&&s.LevelB==0&&s.WidthA>.8).ToArray();if(options.Length==0)continue;
            var s=options[options.Length/2];var p=(s.A+s.B).Unit;var frame=new LocalTerrainFrame(p,terrain.RadiusM,0,scale);TerrainMapViewport.Degrees(p,out double a,out double b);
            foreach(var shape in new[]{(size:102.4,n:64),(size:819.2,n:32),(size:3276.8,n:16)})
            {
                double east=-shape.size/2+shape.size/shape.n*.37,north=-shape.size/2+shape.size/shape.n*.27;
                var timer=System.Diagnostics.Stopwatch.StartNew();var mesh=LocalTerrainMesh.Build(terrain,frame,east,north,shape.size,shape.n);
                double ground=Drawn(mesh.Positions,mesh.Triangles,-east,-north),water=Drawn(mesh.WaterPositions,mesh.WaterTriangles,-east,-north);
                var row=new{reach=c.From,latitude=a,longitude=b,size=shape.size,segments=shape.n,step=shape.size/shape.n,width=s.WidthA,ground,water,visible=water>ground+.001,ms=timer.Elapsed.TotalMilliseconds};rendered.Add(row);Console.WriteLine(JsonSerializer.Serialize(row));
            }
            if(++tried>=4)break;
        }
        File.WriteAllText(Path.Combine(output,"rendered-mouths.json"),JsonSerializer.Serialize(rendered,new JsonSerializerOptions{WriteIndented=true}));
        var junctions=new System.Collections.Generic.List<object>();
        foreach(var c in terrain.Hydrology.Channels.OrderBy(c=>((c.A+c.B).Unit-at).Length).Take(100))
        {
            foreach(var s in terrain.Amplification.RiverReach(c.From).Where(s=>s.From>=0||s.To>=0))
            {if(Math.Abs(s.LevelA-s.LevelB)<.001)continue;TerrainMapViewport.Degrees(s.From>=0?s.A:s.B,out double a,out double b);junctions.Add(new{reach=s.Reach,node=s.From>=0?s.From:s.To,latitude=a,longitude=b,s.LevelA,s.LevelB,s.WidthA,s.WidthB});}
        }
        File.WriteAllText(Path.Combine(output,"nonflat-junction-collars.json"),JsonSerializer.Serialize(junctions,new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));Console.WriteLine("Nonflat nearby junction collars "+junctions.Count);
    }
}
