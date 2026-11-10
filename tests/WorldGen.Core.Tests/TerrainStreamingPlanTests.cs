using WorldGen.Presentation;

static class TerrainStreamingPlanTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        foreach(double scale in new[]{.01,.1,1d,2d})
        {
            var o=new TerrainDetailOptions{ViewDistanceM=6000};double full=o.FullRadius(scale);
            Check(o.ProtectedRadius(scale,TerrainMotion.EyeHeightM)==full,"Walking retains its full-detail contact radius");
            Check(o.ProtectedRadius(scale,100000)==0,"High flight does not request a ground-contact micro-mesh floor");
            double previousRadius=full;
            for(int i=0;i<=100;i++)
            {double radius=o.ProtectedRadius(scale,TerrainMotion.EyeHeightM+full*i/100);Check(radius<=previousRadius&&radius>=0,"Airborne contact floor tapers continuously with altitude");previousRadius=radius;}
            var detailed=TerrainAdaptiveLod.Plan(0,0,0,scale,o,(k,n)=>Math.Min(140,k.SizeM/n*.3));
            var flat=TerrainAdaptiveLod.Plan(0,0,0,scale,o,(_,__)=>0);
            Check(detailed.Sum(p=>(long)p.Value*p.Value)>flat.Sum(p=>(long)p.Value*p.Value),"Fixture reproduces loss of known relief when using the bootstrap estimate");
            var held=TerrainStreamingPlan.ProtectNear(detailed,0,0,scale,full);
            Check(detailed.Count==held.Count&&detailed.All(p=>held.TryGetValue(p.Key,out int n)&&n==p.Value),"Stationary refresh preserves every terrain-aware key and resolution");
            var airborne=TerrainStreamingPlan.ProtectNear(detailed,0,0,scale,0);
            Check(airborne.Count==detailed.Count&&detailed.All(p=>airborne[p.Key]==p.Value),"Zero airborne floor preserves the known layout without splitting the tile under the camera");
            for(int step=0;step<20;step++)
            {
                double x=(step-10)*TerrainLod.ReplanDistance(scale),z=Math.Sin(step)*full;
                var next=TerrainStreamingPlan.ProtectNear(held,x,z,scale,full,64,6000);
                foreach(var p in held)
                {
                    if(TerrainLod.DistanceSquared(p.Key,x,z)>6000*6000)continue;
                    // Every old location remains covered at equal or finer
                    // sampling; a refinement may subdivide the original key.
                    foreach(var uv in new[]{(.1,.1),(.9,.1),(.1,.9),(.9,.9)})
                    {
                        double px=(p.Key.X+uv.Item1)*p.Key.SizeM,pz=(p.Key.Z+uv.Item2)*p.Key.SizeM;
                        var covers=next.Where(q=>px>=q.Key.X*q.Key.SizeM&&px<(q.Key.X+1)*q.Key.SizeM&&pz>=q.Key.Z*q.Key.SizeM&&pz<(q.Key.Z+1)*q.Key.SizeM).ToArray();
                        Check(covers.Length==1,"Held terrain has exactly one requested owner at each interior probe");
                        Check(covers[0].Key.SizeM/covers[0].Value<=p.Key.SizeM/p.Value,"Movement never downgrades known sampling while waiting for a new plan");
                    }
                }
                foreach(var p in next.Where(p=>TerrainLod.DistanceSquared(p.Key,x,z)<=full*full))
                    Check(p.Key.Level==0&&p.Value==64,"New nearby ground remains full detail while the async plan is pending");
                var edges=TerrainEdges.Build(next);
                foreach(var p in next.Take(8))Check(edges[p.Key].SequenceEqual(TerrainLod.EdgeSteps(p.Key,next)),"Retained/refined plan has valid mixed-LOD stitch requirements");
                held=next;
            }
            // Fast travel outside the old disk still requests actual nearby
            // ground, and cannot retain an unbounded corridor of old requests.
            var far=TerrainStreamingPlan.ProtectNear(held,100000,0,scale,full,64,6000);
            Check(far.Count>0&&far.Keys.All(k=>TerrainLod.DistanceSquared(k,100000,0)<=full*full),"Distant movement bounds old residency and fills the new nearby floor");
            var pending=TerrainStreamingPlan.ProtectNear(held,100000,0,scale,0,64,6000);
            var fallback=TerrainAdaptiveLod.Plan(100000,0,500,scale,o,(_,__)=>0);
            var covered=TerrainStreamingPlan.CompleteCoverage(pending,fallback);
            Check(covered.Count>0,"Airborne movement beyond the old disk still requests continuous fallback coverage");
            var merged=TerrainStreamingPlan.CompleteCoverage(detailed,flat);
            Check(merged.Count==detailed.Count&&detailed.All(p=>merged[p.Key]==p.Value),"Fallback fills gaps without replacing any already planned terrain-aware detail");
            var movedFallback=TerrainAdaptiveLod.Plan(full*2,full,500,scale,o,(_,__)=>0);
            merged=TerrainStreamingPlan.CompleteCoverage(detailed,movedFallback);
            Check(detailed.All(p=>merged.TryGetValue(p.Key,out int n)&&n==p.Value),"Fast-flight fallback never overwrites retained fine terrain");
            foreach(var probe in movedFallback.Keys)
            {
                double px=(probe.X+.37)*probe.SizeM,pz=(probe.Z+.63)*probe.SizeM;
                Check(merged.Count(q=>px>=q.Key.X*q.Key.SizeM&&px<(q.Key.X+1)*q.Key.SizeM&&pz>=q.Key.Z*q.Key.SizeM&&pz<(q.Key.Z+1)*q.Key.SizeM)==1,"Merged fallback is complete and non-overlapping at each requested probe");
            }
        }
        Console.WriteLine("PASS "+checks+" streaming-plan continuity checks.");
    }
}
