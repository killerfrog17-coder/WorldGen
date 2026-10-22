using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class TerrainDetailTests
{
    public static void Run(string path)
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        long Cost(Dictionary<TerrainTileKey,int> p)=>p.Sum(t=>(long)(t.Value+1)*(t.Value+1));
        var o=new TerrainDetailOptions{ViewDistanceM=60000};
        Check(o.Quality==2&&o.Effort==1&&o.Workers==2,"High/Balanced defaults");
        for(int effort=0;effort<4;effort++){o.Effort=effort;Check(o.Workers==effort+1&&o.Installations<=4&&o.UploadBudgetMs<=8,"Bounded effort budgets");}o.Effort=1;
        Check(TerrainAdaptiveLod.ProjectedError(10,1000,0,1080,68)>TerrainAdaptiveLod.ProjectedError(10,2000,0,1080,68),"Distance reduces projected error");
        Check(TerrainAdaptiveLod.ProjectedError(10,1000,0,1080,45)>TerrainAdaptiveLod.ProjectedError(10,1000,0,1080,90),"Zoom enlarges projected error");
        Check(TerrainAdaptiveLod.ProjectedError(10,1000,0,4320,68)==TerrainAdaptiveLod.ProjectedError(10,1000,0,2160,68),"Resolution quality ceiling");
        foreach(double scale in new[]{.01,.1,1d,2d})
        {
            var flat=TerrainAdaptiveLod.Plan(-37,23,0,scale,o,(_,__)=>0);
            var rough=TerrainAdaptiveLod.Plan(-37,23,0,scale,o,(k,n)=>Math.Min(200,k.SizeM/n*.15));
            Check(Cost(rough)>Cost(flat),"Rugged terrain receives more geometry");
            foreach(var p in rough)
            {
                Check(p.Value==16||p.Value==32||p.Value==64,"Bounded discrete resolutions");
                if(TerrainLod.DistanceSquared(p.Key,-37,23)<Math.Pow(o.FullRadius(scale),2))Check(p.Key.Level==0&&p.Value==64,"Protected full-detail radius");
            }
            foreach(double distance in new[]{10d,1000,50000})foreach(double a in new[]{0d,Math.PI/2,Math.PI,Math.PI*1.5})
            {
                double x=-37+distance*Math.Cos(a),z=23+distance*Math.Sin(a);
                Check(rough.Any(p=>x>=p.Key.X*p.Key.SizeM&&x<(p.Key.X+1)*p.Key.SizeM&&z>=p.Key.Z*p.Key.SizeM&&z<(p.Key.Z+1)*p.Key.SizeM),"All-direction coverage, not facing-dependent");
            }
            var edges=TerrainEdges.Build(rough);
            foreach(var p in rough.Take(25))Check(edges[p.Key].SequenceEqual(TerrainLod.EdgeSteps(p.Key,rough)),"Adaptive stitching matches literal edge lookup");
        }
        var costs=new List<long>();for(int q=0;q<5;q++){o.Quality=q;costs.Add(Cost(TerrainAdaptiveLod.Plan(0,0,100,.1,o,(k,n)=>Math.Min(100,k.SizeM/n*.1))));}
        Check(costs.SequenceEqual(costs.Order()),"Quality increases detail, not loading effort");
        var diagnostic=new TerrainDetailOptions{ViewDistanceM=60000,EstimateRelief=false};
        var ordinaryFlat=TerrainAdaptiveLod.Plan(0,0,0,.1,new TerrainDetailOptions{ViewDistanceM=60000},(_,__)=>0);
        var distanceOnly=TerrainAdaptiveLod.Plan(0,0,0,.1,diagnostic,(_,__)=>throw new Exception("Distance-only diagnostic still evaluated relief"));
        Check(distanceOnly.Count==ordinaryFlat.Count&&distanceOnly.All(p=>ordinaryFlat.TryGetValue(p.Key,out int n)&&n==p.Value),"Distance-only diagnostic preserves the zero-error plan and avoids estimator work");
        diagnostic.ProximityRefinement=false;
        var protectedOnly=TerrainAdaptiveLod.Plan(0,0,0,.1,diagnostic,(_,__)=>throw new Exception("Unexpected estimator work"));
        Check(Cost(protectedOnly)<Cost(distanceOnly),"Proximity diagnostic actually reduces refinement");
        Check(protectedOnly.Where(p=>TerrainLod.DistanceSquared(p.Key,0,0)<Math.Pow(diagnostic.FullRadius(.1),2)).All(p=>p.Key.Level==0&&p.Value==64),"Proximity diagnostic preserves the protected radius");
        foreach(int grid in new[]{32,16,8})
        {
            diagnostic.FinestSegments=grid;var coarser=TerrainAdaptiveLod.Plan(0,0,0,.1,diagnostic,(_,__)=>throw new Exception("Unexpected estimator work"));
            Check(coarser.Count==protectedOnly.Count&&coarser.Keys.All(protectedOnly.ContainsKey),"Grid diagnostic retains terrain footprint");
            Check(coarser.All(p=>p.Value==(p.Key.Level==0?grid:protectedOnly[p.Key])),"Grid diagnostic changes only finest tessellation");
            Check(Cost(coarser)<Cost(protectedOnly),"Grid diagnostic reduces vertex workload");
            var diagnosticEdges=TerrainEdges.Build(coarser);
            foreach(var p in coarser.Take(25))Check(diagnosticEdges[p.Key].SequenceEqual(TerrainLod.EdgeSteps(p.Key,coarser)),"Coarser diagnostic mesh keeps matching edge rules");
        }
        Check(new TerrainDetailOptions().EstimateRelief&&new TerrainDetailOptions().ProximityRefinement&&new TerrainDetailOptions().FinestSegments==64,"New options retain normal rendering defaults");
        using(var stop=new CancellationTokenSource()){stop.Cancel();try{TerrainAdaptiveLod.Plan(0,0,0,.1,o,(_,__)=>0,token:stop.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){checks++;}}
        if(path!=null)
        {
            var world=WorldFile.Read(File.ReadAllBytes(path));string hash=StateHash.Compute(world);
            var terrain=new LocalTerrain(world,horizontalScale:.1);var origin=world.Surface.Cells[Enumerable.Range(0,world.Surface.Cells.Length).First(i=>terrain.Profile(i).MountainHeightM>50)].Position;
            var frame=new LocalTerrainFrame(origin,terrain.RadiusM,0,.1);var estimator=new TerrainDetailEstimator(terrain,frame);
            foreach(int q in new[]{0,2,4})
            {
                o.Quality=q;var watch=System.Diagnostics.Stopwatch.StartNew();
                var plan=TerrainAdaptiveLod.Plan(0,0,200,.1,o,estimator.Error);
                Console.WriteLine($"Actual quality {q}: {plan.Count} tiles / {Cost(plan):N0} grid vertices; {watch.Elapsed.TotalMilliseconds:F1}ms / {estimator.Count} cached metadata");
                Check(plan.Count<2100&&plan.Count>0,"Actual metadata plan bounded");
            }
            Check(StateHash.Compute(world)==hash,"Adaptive presentation does not change world/history");
        }
        Console.WriteLine($"PASS {checks} adaptive terrain checks; synthetic quality costs [{string.Join(",",costs)}]");
    }
}
