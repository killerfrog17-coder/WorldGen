using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class LakeInletTests
{
    public static void Run(string path,bool scan)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));string hash=StateHash.Compute(w);
        var terrain=new LocalTerrain(w,horizontalScale:.1);int checkedPoints=0,blocked=0,channels=0;
        var candidates=terrain.Hydrology.Channels.Where(c=>w.Hydrology.WaterBody[c.To]>=0&&
            w.Hydrology.LakeCoverageFraction[c.To]>=.5&&w.Hydrology.WaterBody[c.From]!=w.Hydrology.WaterBody[c.To])
            .OrderBy(c=>w.Hydrology.WaterBody[c.To]==277?0:1).ThenByDescending(c=>c.WidthM).Take(12);
        foreach(var c in candidates)
        {
            var at=(c.A+c.B).Unit;var patch=terrain.Patch(at,Math.Max(4000,c.LengthM));
            var spans=patch.RiverSpans().Where(s=>s.Reach==c.From).ToArray();if(spans.Length==0)continue;
            int dry=0,holes=0;V firstHole=default;
            foreach(var s in spans)foreach(double t in new[]{.25,.5,.75})
            {
                var p=(s.A*(1-t)+s.B*t).Unit;var sample=terrain.Patch(p,2000).Sample(p);checkedPoints++;
                if(!sample.Underwater){holes++;firstHole=p;}
                if(sample.WaterKind==LocalWaterKind.Lake&&!sample.Underwater)dry++;
            }
            channels++;blocked+=holes;
            Console.WriteLine($"Inlet {c.From}->{c.To}, lake {w.Hydrology.WaterBody[c.To]}: {spans.Length} spans, {holes} dry channel samples ({dry} lake-owned), first hole {firstHole.X:R},{firstHole.Y:R},{firstHole.Z:R}");
            if(!scan&&holes>0)throw new Exception("Incoming channel terminates in dry terrain before reaching lake");
        }
        if(channels==0||checkedPoints==0)throw new Exception("No actual lake inlets tested");
        if(!scan&&w.Recipe.Seed==11610541)
        {
            // These were dry lake-owned channel points in Hyper10, including
            // one of lake277's incoming branches and an independent lake186.
            foreach(var p in new[]{new V(.969225813,-.222932866,-.104413893),new V(-.87537332,.233957807,-.423066538)})
            {
                var sample=terrain.Patch(p,2000).Sample(p);var frame=new LocalTerrainFrame(p,terrain.RadiusM,0,.1);
                var mesh=LocalTerrainMesh.Build(terrain,frame,-102.4,-102.4,204.8,32);
                int hits=0;double expected=frame.SurfaceY(p,sample.WaterSurfaceM);
                for(int k=0;k<mesh.WaterTriangles.Length;k+=3)
                {
                    var v=mesh.WaterPositions;int a=mesh.WaterTriangles[k]*3,b=mesh.WaterTriangles[k+1]*3,c=mesh.WaterTriangles[k+2]*3;
                    double bx=v[b]-v[a],bz=v[b+2]-v[a+2],cx=v[c]-v[a],cz=v[c+2]-v[a+2],det=bx*cz-bz*cx;
                    if(Math.Abs(det)<1e-12)continue;
                    double u=((102.4-v[a])*cz-(102.4-v[a+2])*cx)/det,t=(bx*(102.4-v[a+2])-bz*(102.4-v[a]))/det;
                    if(u< -1e-6||t< -1e-6||u+t>1.000001)continue;
                    double y=v[a+1]+u*(v[b+1]-v[a+1])+t*(v[c+1]-v[a+1]);
                    if(Math.Abs(y-expected)<.05)hits++;
                }
                if(hits==0)throw new Exception("Connected inlet field has no drawn surface at reproduced cutoff");
                Console.WriteLine($"Cutoff mesh regression: {hits} covering triangles; {sample.WaterKind} {sample.ElevationM:F3}/{sample.WaterSurfaceM:F3}m");
            }
        }
        if(StateHash.Compute(w)!=hash)throw new Exception("Inlet queries changed source world/history");
        Console.WriteLine($"{(blocked==0?"PASS":"REPRODUCED")} {channels} inlets, {checkedPoints} centre-line points, {blocked} dry points; source unchanged {hash}");
    }
}
