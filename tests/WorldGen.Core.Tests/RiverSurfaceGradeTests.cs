using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class RiverSurfaceGradeTests
{
    public static void Run(string path)
    {
        int checks=0;
        void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        var world=WorldFile.ReadFile(path);var history=world.Prehistory.Hash;
        var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        int count=0,graded=0,falls=0,renderSource=-1;V? renderPoint=null;
        foreach(var channel in terrain.Hydrology.Channels.Where(c=>c.WidthM>5&&c.StartM>20).GroupBy(c=>c.From).Select(g=>g.First()).Take(48))
        {
            var spans=terrain.Amplification.RiverReach(channel.From);
            var expected=(RiverscapeSpan[])spans.Clone();
            RiverscapeKernel.PrepareSurfaceGrades(expected,terrain.RadiusM*.1);
            for(int i=0;i<spans.Length;i++)
            {
                var s=spans[i];count++;
                Check(double.IsFinite(s.SurfaceGradeA)&&s.SurfaceGradeA>=0&&s.SurfaceGradeA<=.6&&s.SurfaceGradeB>=0&&s.SurfaceGradeB<=.6,"Finite bounded station normals");
                Check(s.LevelA==expected[i].LevelA&&s.LevelB==expected[i].LevelB&&(s.A-expected[i].A).Length==0&&(s.B-expected[i].B).Length==0,"Shading preparation never moves actual water or banks");
                Check(s.SurfaceGradeA==expected[i].SurfaceGradeA&&s.SurfaceGradeB==expected[i].SurfaceGradeB,"Station grades are deterministic and idempotent");
                if(i>0)Check(s.SurfaceGradeA==spans[i-1].SurfaceGradeB,"Adjacent station grades are EXACT, before tile selection");
                if(s.SurfaceGradeA>0||s.SurfaceGradeB>0)graded++;
                if(!renderPoint.HasValue&&s.Primitive!=RiverFlowPrimitive.Waterfall&&s.LevelA>0&&s.LevelB>0&&
                    (s.SurfaceGradeA>1e-4||s.SurfaceGradeB>1e-4)&&Math.Min(s.WidthA,s.WidthB)>.6)
                {
                    V candidate=(s.A+s.B).Unit;var actual=terrain.Patch(candidate,5000).Geometry(candidate);
                    if(actual.Underwater&&actual.WaterKind==LocalWaterKind.River&&actual.WaterMaskM>.1)
                    {renderPoint=candidate;renderSource=s.Reach;}
                }
                if(s.Primitive==RiverFlowPrimitive.Waterfall){falls++;Check(s.SurfaceGradeA==0&&s.SurfaceGradeB==0,"Explicit falling sheets retain physical normals");}
            }
            if(spans.Length>0)Check(spans[0].SurfaceGradeA==0&&spans[^1].SurfaceGradeB==0,"Graph junction pools meet flat endpoint normals");
        }
        Check(count>100&&graded>0,"Actual graded river reaches exercised");
        Check(renderPoint.HasValue,"The loaded world provides an actually submerged graded river fixture");
        var point=renderPoint.Value;
        Console.WriteLine($"Grade render source {renderSource}, point {point.X:R},{point.Y:R},{point.Z:R}");
        var frame=new LocalTerrainFrame(point,terrain.RadiusM,0,.1);
        var mesh=LocalTerrainMesh.Build(terrain,frame,-100,-100,200,32);
        int ordinary=0,renderGraded=0;
        for(int i=0;i<mesh.WaterColors.Length;i+=4)
        {
            float tag=mesh.WaterColors[i+3];
            if(tag<1.5||tag>2.5)continue;
            if(tag>2.05){Check(tag<=2.20001,"Waterfall progress remains separate from ordinary river grades");continue;}
            ordinary++;Check(tag>=1.69999&&tag<=2.00001,"Ordinary grade tag stays in river classification interval");
            if(tag<1.99999)renderGraded++;
        }
        Check(ordinary>0,"Actual clipped river render payload exercised");
        Check(renderGraded>0,"Actual rendered river payload includes a nonzero prepared surface grade");
        Check(world.Prehistory.Hash==history,"No simulated history change");
        Console.WriteLine($"PASS {checks} station/render checks, {count} spans, {graded} graded, {falls} falling sheets, {ordinary} ordinary render vertices");
    }
}
