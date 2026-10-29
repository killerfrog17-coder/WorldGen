using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;
using System.Text.Json;

static class RiverRepairTests
{
    public static double Height(float[] p,int[] triangles,double x,double z)
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
    public static void Run(string path,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadFile(path);string originalHash=StateHash.Compute(world);var terrain=new LocalTerrain(world,horizontalScale:.1);var rows=new List<object>();int failures=0,total=0;
        foreach(var site in new[]{(reach:70001,lat:-17.625665274156873,lon:.6638456954785965),(reach:85691,lat:-8.248825835553712,lon:17.85174984290807),(reach:106261,lat:3.7222531179708227,lon:32.684172258473716)})
        {
            var at=TerrainMapViewport.FromDegrees(site.lat,site.lon);var frame=new LocalTerrainFrame(at,terrain.RadiusM,0,.1);var patch=terrain.Patch(at,20000);
            var positions=Enumerable.Range(0,64).Select(i=>frame.Position((i%8-3.5)*50,(i/8-3.5)*50)).ToArray();var before=positions.Select(p=>patch.Sample(p)).ToArray();patch.RiverSpans();
            for(int i=0;i<positions.Length;i++){var after=patch.Sample(positions[i]);if(after.ElevationM!=before[i].ElevationM||after.WaterKind!=before[i].WaterKind||!after.WaterSurfaceM.Equals(before[i].WaterSurfaceM))throw new Exception("Patch sampling changed after river geometry was requested");}
            foreach(var s in terrain.Amplification.RiverReach(site.reach))if((s.From>=0||s.To>=0)&&s.LevelA!=s.LevelB)throw new Exception("Graph endpoint collar is sloping");
            foreach(var lod in new[]{(size:204.8,n:64),(size:819.2,n:32),(size:3276.8,n:16)})
            {
                double east=-lod.size/2+lod.size/lod.n*.37,north=-lod.size/2+lod.size/lod.n*.27;var mesh=LocalTerrainMesh.Build(terrain,frame,east,north,lod.size,lod.n);
                var faults=new List<object>();int tested=0,dry=0,hidden=0,missing=0,wrongLevel=0;double worst=0;
                foreach(var s in patch.RiverSpans(lod.size/lod.n>32?lod.size/lod.n*.1:0))
                foreach(double t in new[]{.05,.2,.4,.5,.6,.8,.95})foreach(double q in new[]{-.75,-.4,0,.4,.75})
                {
                    var center=(s.A*(1-t)+s.B*t).Unit;double width=s.WidthA*(1-t)+s.WidthB*t;if(width<.1)continue;var side=(s.SideA*(1-t)+s.SideB*t).Unit;var p=(center+side*(q*width/frame.RadiusM)).Unit;
                    TerrainHandoff.Coordinates(frame,p,out double x,out double z);x-=east;z-=north;if(x<4||x>lod.size-4||z<4||z>lod.size-4)continue;
                    var actual=patch.Sample(p);tested++;double water=Height(mesh.WaterPositions,mesh.WaterTriangles,x,z),ground=Height(mesh.Positions,mesh.Triangles,x,z);bool bad=false;
                    if(!actual.Underwater){dry++;bad=true;}else if(!double.IsFinite(water)){missing++;bad=true;}else if(ground>water+.005){hidden++;worst=Math.Max(worst,ground-water);bad=true;}
                    if(actual.Underwater&&double.IsFinite(water)&&Math.Abs(water-frame.SurfaceY(p,actual.WaterSurfaceM))>Math.Max(.02,Math.Pow(lod.size/lod.n,2)/frame.RadiusM*.5)){wrongLevel++;bad=true;}
                    if(bad&&faults.Count<30)faults.Add(new{reach=s.Reach,t,q,x,z,ground,water=double.IsFinite(water)?water:-99999,coreGround=actual.ElevationM,coreWater=actual.WaterSurfaceM,kind=actual.WaterKind.ToString()});
                }
                failures+=dry+hidden+missing+wrongLevel;total+=tested;
                var row=new{site=site.reach,lod.size,lod.n,tested,dry,hidden,missing,wrongLevel,worst,vertices=mesh.Positions.Length/3,triangles=mesh.Triangles.Length/3,ms=mesh.BuildMilliseconds,faults};rows.Add(row);Console.WriteLine(JsonSerializer.Serialize(row,new JsonSerializerOptions{NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals}));
            }
        }
        File.WriteAllText(Path.Combine(output,"probes.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true,NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals}));
        if(StateHash.Compute(world)!=originalHash)throw new Exception("Water derivation mutated the source world");
        if(failures>0)throw new Exception(failures+" failed river geometry probes; see report");
        Console.WriteLine("PASS "+total+" off-grid river geometry probes, 192 lazy/indexed sampling comparisons, graph collars and source-world preservation");
    }
}
