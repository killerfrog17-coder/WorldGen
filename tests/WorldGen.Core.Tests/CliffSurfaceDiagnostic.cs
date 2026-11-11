using System.Globalization;
using System.Text;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class CliffSurfaceDiagnostic
{
    public static void Run(string input,int source,string output,bool upper=false)
    {
        var world=WorldFile.ReadFile(input);var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        var spans=terrain.Amplification.RiverReach(source);var middle=spans[spans.Length/2];var center=(middle.A+middle.B).Unit;
        var frame=new LocalTerrainFrame(center,terrain.RadiusM,0,.1);var patch=terrain.Patch(center,20000);patch.RiverSpans();
        double Height(double x,double z){var p=frame.Position(x,z);return frame.SurfaceY(p,patch.Elevation(p));}
        double wx=0,wz=0;bool found=false;
        foreach(double radius in new[]{16d,32,64,128,256,512,768})
        {
            for(int angle=0;angle<32;angle++)
            {
                double a=angle*Math.PI/16,x=Math.Cos(a)*radius,z=Math.Sin(a)*radius;
                double sx=(Height(x+4,z)-Height(x-4,z))/8,sz=(Height(x,z+4)-Height(x,z-4))/8;
                if(sx*sx+sz*sz<1.4)continue;wx=x;wz=z;found=true;break;
            }
            if(found)break;
        }
        if(!found)throw new Exception("No nearby steep wall at actual canyon source");
        double size=upper?2048:256;if(upper){wx=0;wz=-700;}int segments=upper?128:64,raster=upper?256:128;
        double east=wx-size/2,north=wz-size/2;
        // Centre the diagnostic raster on the exact selected wall, with ordinary
        // production triangulation and a final-triangle height query.
        east=wx-size/2;north=wz-size/2;
        var mesh=LocalTerrainMesh.Build(terrain,frame,east,north,size,segments);var contact=TerrainContactSurface.Build(mesh);
        var csv=new StringBuilder("x,z,field,pre_water,drawn,regular,exposure\n");double maxError=0;int at=0;
        double Regular(double x,double z)
        {
            int side=segments+1;double gx=(x-east)*segments/size,gz=(z-north)*segments/size;int ix=Math.Min(segments-1,Math.Max(0,(int)gx)),iz=Math.Min(segments-1,Math.Max(0,(int)gz));double u=gx-ix,v=gz-iz;
            double A(int dx,int dz)=>mesh.Positions[((iz+dz)*side+ix+dx)*3+1];
            return u+v<=1?A(0,0)+(A(1,0)-A(0,0))*u+(A(0,1)-A(0,0))*v:A(1,1)+(A(0,1)-A(1,1))*(1-u)+(A(1,0)-A(1,1))*(1-v);
        }
        for(int z=0;z<=raster;z++)for(int x=0;x<=raster;x++)
        {
            double px=east+x*size/raster,pz=north+z*size/raster;var p=frame.Position(px,pz);var sample=patch.Geometry(p);double y=frame.SurfaceY(p,sample.ElevationM),pre=frame.SurfaceY(p,sample.PreWaterElevationM),regular=Regular(px,pz),drawn=regular;
            if(contact!=null&&contact.TryHeight(px-east,pz-north,out double h))drawn=h;maxError=Math.Max(maxError,Math.Abs(y-drawn));at++;
            csv.AppendLine(string.Join(",",new[]{px,pz,y,pre,drawn,regular,(double)sample.Province.CanyonExposure}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
        }
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"surface.csv"),csv.ToString());
        File.WriteAllText(Path.Combine(output,"site.json"),JsonSerializer.Serialize(new{Source=source,WallX=wx,WallZ=wz,East=east,North=north,Center=new[]{center.X,center.Y,center.Z},GridSize=size,RasterSamples=at,MaxMeshErrorM=maxError,Vertices=mesh.Positions.Length/3,Triangles=mesh.Triangles.Length/3,mesh.BuildMilliseconds},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Cliff diagnosis "+output+": "+at+" samples, max field/drawn delta "+maxError.ToString("F3")+"m");
    }
}
