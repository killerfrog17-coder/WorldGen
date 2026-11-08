using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

// Read-only diagnosis of the actual captured wall, rather than a different
// qualifying point somewhere in the same canyon.
static class CliffVisibleFaceTests
{
    public static void Run(string worldPath,string nativeDirectory,string cameraName="landform-side")
    {
        var world=WorldFile.ReadFile(worldPath);var terrain=new LocalTerrain(world,horizontalScale:.1);
        using var receipt=JsonDocument.Parse(File.ReadAllText(Path.Combine(nativeDirectory,"scene.json")));
        var p=receipt.RootElement.GetProperty("Position");V origin=new(p.GetProperty("X").GetDouble(),p.GetProperty("Y").GetDouble(),p.GetProperty("Z").GetDouble());
        var patch=terrain.Patch(origin,120000);patch.RiverSpans();double altitude=patch.Elevation(origin);var frame=new LocalTerrainFrame(origin,terrain.RadiusM,altitude,.1);
        var line=File.ReadLines(Path.Combine(nativeDirectory,"cameras.txt")).Single(s=>s.StartsWith(cameraName+" "));
        var numbers=Regex.Match(line,@"camera=\(([^,]+), ([^,]+), ([^)]+)\)");double Number(int i)=>double.Parse(numbers.Groups[i].Value,CultureInfo.InvariantCulture);
        V camera=new(Number(1),Number(2),Number(3));var cameraAt=frame.Position(camera.X,camera.Z);
        Console.WriteLine($"Recorded camera{camera}, reconstructed floor{frame.SurfaceY(cameraAt,patch.Elevation(cameraAt)):F4}; {line}");
        var rows=new List<object>();double horizontal=Math.Sqrt(camera.X*camera.X+camera.Z*camera.Z);
        var recordedLook=Regex.Match(line,@"look=\(([^,]+), ([^,]+), ([^)]+)\)");
        var look=recordedLook.Success?new V(double.Parse(recordedLook.Groups[1].Value,CultureInfo.InvariantCulture),double.Parse(recordedLook.Groups[2].Value,CultureInfo.InvariantCulture),double.Parse(recordedLook.Groups[3].Value,CultureInfo.InvariantCulture)).Unit:new V(-camera.X,-horizontal*.1,-camera.Z).Unit;
        foreach(double vertical in new[]{0d,.25,.5})
        {
            V direction=new V(look.X,look.Y+vertical*Math.Sqrt(look.X*look.X+look.Z*look.Z),look.Z).Unit;double previous=0;bool found=false;
            for(double distance=1;distance<=8000;distance+=distance<80?1:distance<500?4:16)
            {
                V q=camera+direction*distance,at=frame.Position(q.X,q.Z);double floor=frame.SurfaceY(at,patch.Elevation(at));
                if(q.Y>floor){previous=distance;continue;}
                double lo=previous,hi=distance;
                for(int i=0;i<14;i++){double mid=(lo+hi)*.5;V m=camera+direction*mid,mp=frame.Position(m.X,m.Z);if(m.Y>frame.SurfaceY(mp,patch.Elevation(mp)))lo=mid;else hi=mid;}
                q=camera+direction*hi;at=frame.Position(q.X,q.Z);var sample=patch.Sample(at);double step=4;
                double dx=(patch.Elevation(frame.Position(q.X+step,q.Z))-patch.Elevation(frame.Position(q.X-step,q.Z)))/(2*step);
                double dz=(patch.Elevation(frame.Position(q.X,q.Z+step))-patch.Elevation(frame.Position(q.X,q.Z-step)))/(2*step);double up=1/Math.Sqrt(1+dx*dx+dz*dz);
                var material=LocalGeology.Surface(sample.Ground,at,world.Recipe.Seed,sample.ElevationM,up,sample.OceanInfluence,0,terrain.RadiusM*.1,sample.Province.ExposedRock);
                var row=new{ScreenVertical=vertical,Distance=hi,X=at.X,Y=at.Y,Z=at.Z,sample.ElevationM,Up=up,sample.Province.CanyonExposure,RockProvince=sample.Province.Rock,sample.Province.Karst,Bedrock=sample.Ground.Bedrock.ToString(),sample.Ground.DepthM,MaterialRock=material.Rock,WaterKind=sample.WaterKind.ToString()};rows.Add(row);
                Console.WriteLine($"Visible offset{vertical:F2}: distance{hi:F1}m, up{up:F3}, canyon{sample.Province.CanyonExposure:F3}, rockProvince{sample.Province.Rock:F3}, karst{sample.Province.Karst:F3}, bedrock{sample.Ground.Bedrock}, depth{sample.Ground.DepthM:F3}m, materialRock{material.Rock:F3}");
                found=true;break;
            }
            if(!found)Console.WriteLine($"Visible offset{vertical:F2}: sky/no ground within8km");
        }
        File.WriteAllText(Path.Combine(nativeDirectory,cameraName+"-visible-surface-probes.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
    }
}
