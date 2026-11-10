using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class RiverNodeJoinTests
{
    public static void Controls()
    {
        const double radius=637100;int checks=0;
        void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        V P(double distance)=>new V(1,distance/radius,0).Unit;
        var points=new[]{P(0),P(400),P(800),P(1000)};
        Check(ReferenceEquals(points,RiverscapeKernel.InsertJunctionCollars(points,radius,0,0)),"Ordinary joins add no collar stations or allocation");
        var fitted=RiverscapeKernel.InsertJunctionCollars(points,radius,150,180);
        Check(fitted.Length==points.Length+2&&fitted[0].Equals(points[0])&&fitted[^1].Equals(points[^1]),"Pool boundaries insert stations without moving either graph endpoint");
        Check(fitted.Any(p=>Math.Abs((p-points[0]).Length*radius-150)<.003),"Source collar ends at its actual radius instead of the next coarse station");
        Check(fitted.Any(p=>Math.Abs((p-points[^1]).Length*radius-180)<.003),"Receiving collar ends at its actual radius instead of the preceding coarse station");
        for(int i=1;i<fitted.Length;i++)Check(fitted[i].Y>fitted[i-1].Y,"Inserted collar stations remain in channel order");
        var first=RiverPlanform.Build(t=>P(t*1000),64,1000,20,.001,.8,.1,radius,81,new V(0,1,.3),new V(0,1,-.2));
        var again=RiverPlanform.Build(t=>P(t*1000),64,1000,20,.001,.8,.1,radius,81,new V(0,1,.3),new V(0,1,-.2));
        Check(first.SequenceEqual(again)&&first[0].Equals(P(0))&&first[^1].Equals(P(1000)),"Shared-tangent channel fitting is deterministic and keeps endpoints");
        Console.WriteLine($"PASS {checks} river join/collar controls");
    }
    public static void World(string path,string cache)
    {
        var world=WorldFile.ReadFile(path);var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1,directory:cache);
        int nodes=0,checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        foreach(var channel in terrain.Hydrology.Channels.OrderBy(c=>Rng.Mix((uint)c.From^9217u)))
        {
            var before=terrain.Amplification.RiverReach(channel.From);if(before.Length==0)continue;
            var a=before[^1];if(a.To<0||a.PoolB)continue;
            var after=terrain.Amplification.RiverReach(a.To);Check(after.Length>0,"Ordinary endpoint has its actual downstream channel");var b=after[0];
            Check(!b.PoolA&&a.B.Equals(b.A)&&a.SideB.Equals(b.SideA),"Both reaches share exact endpoint position and cross-section orientation");
            Check(a.WidthB==b.WidthA&&a.DepthB==b.DepthA&&a.LevelB==b.LevelA,"Width, bed depth and water head agree exactly across an ordinary join");
            foreach(int sign in new[]{-1,1})
            {
                V left=(a.B+a.SideB*(sign*(a.WidthB+2)/(terrain.RadiusM*.1))).Unit;
                V right=(b.A+b.SideA*(sign*(b.WidthA+2)/(terrain.RadiusM*.1))).Unit;
                Check(left.Equals(right),"Actual render underlap vertices meet without a circular filler");
            }
            if(a.LevelB>0)
            {
                var patch=terrain.Patch(a.B,(a.WidthB*4+1500)/.1);var sample=patch.Geometry(a.B);
                Check(sample.Underwater&&sample.WaterKind==LocalWaterKind.River&&Math.Abs(sample.WaterSurfaceM-a.LevelB)<.03,"Actual scalar water/bed remains connected at the shared node");
            }
            if(++nodes>=64)break;
        }
        Check(nodes==64,"Join audit covers 64 distinct actual river graph nodes");
        Console.WriteLine($"PASS {checks} exact geometry and scalar water controls at {nodes} actual ordinary joins");
    }
}
