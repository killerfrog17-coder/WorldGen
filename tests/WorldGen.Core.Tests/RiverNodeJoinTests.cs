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
        var close=RiverscapeKernel.InsertJunctionCollars(new[]{P(0),P(149),P(400),P(1000)},radius,150,0);
        Check(!close.Contains(P(149))&&close.Any(p=>Math.Abs((p-P(0)).Length*radius-150)<.003),"Near-coincident original sampling station retires while the exact collar remains");
        Check(close[0].Equals(P(0))&&close[^1].Equals(P(1000)),"Collar cleanup cannot move either graph endpoint");
        foreach(double angle in new[]{15d,30,60,90,120,175,180})
        {
            double theta=angle*Math.PI/180,half=75,r=RiverscapeKernel.ConfluenceRadius(half,Math.Cos(theta));
            Check(double.IsFinite(r)&&r>=RiverscapeKernel.JunctionRadius(half)-1e-8,"Confluence stage includes its node disk");
            Check(Math.Abs((r-half-2)*Math.Sin(theta*.5)-RiverscapeKernel.JunctionRadius(half))<1e-7,"Shared stage includes the farthest overlap plus the full paired section");
        }
        var first=RiverPlanform.Build(t=>P(t*1000),64,1000,20,.001,.8,.1,radius,81,new V(0,1,.3),new V(0,1,-.2));
        var again=RiverPlanform.Build(t=>P(t*1000),64,1000,20,.001,.8,.1,radius,81,new V(0,1,.3),new V(0,1,-.2));
        Check(first.SequenceEqual(again)&&first[0].Equals(P(0))&&first[^1].Equals(P(1000)),"Shared-tangent channel fitting is deterministic and keeps endpoints");
        foreach(double uncut in new[]{-5d,0,100})foreach(double cut in new[]{uncut-100,uncut-5,uncut})
        foreach(double bank in new[]{uncut-150,uncut-10,uncut+5})foreach(double distance in new[]{-1d,0,2,2.25,3,5,7.999,8,9})
        {
            double result=RiverscapeKernel.ProtectBank(cut,uncut,bank,distance);
            Check(double.IsFinite(result)&&result>=cut&&result<=uncut,"Combining river cuts preserves banks without raising original terrain");
            if(distance<0||distance>=8||bank<=cut)Check(result==cut,"Distant or already-satisfied bank constraints do not change terrain");
        }
        Console.WriteLine($"PASS {checks} river join/collar controls");
    }
    public static void World(string path,string cache)
    {
        var world=WorldFile.ReadFile(path);var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1,directory:cache);
        var through=typeof(HyperAmplification).GetMethod("ThroughRiverNode",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        int nodes=0,checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        foreach(var channel in terrain.Hydrology.Channels.OrderBy(c=>Rng.Mix((uint)c.From^9217u)))
        {
            var before=terrain.Amplification.RiverReach(channel.From);if(before.Length==0)continue;
            var a=before[^1];if(a.To<0||!(bool)through.Invoke(terrain.Amplification,new object[]{a.To})!)continue;
            var after=terrain.Amplification.RiverReach(a.To);Check(after.Length>0,"Ordinary endpoint has its actual downstream channel");var b=after[0];
            Check(!b.PoolA&&a.B.Equals(b.A)&&a.SideB.Equals(b.SideA),$"Join {channel.From}->{a.To}: pool={b.PoolA}, endpoint delta={(a.B-b.A).Length:R}, side delta={(a.SideB-b.SideA).Length:R}");
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
