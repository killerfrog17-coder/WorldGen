using System.Diagnostics;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class RiverBankContactAudit
{
    public static void Run(string input,string output,int[] sources=null)
    {
        var world=WorldFile.ReadFile(input);var timer=Stopwatch.StartNew();
        // Each audit receipt owns a disposable cache. An earlier unaccepted
        // fitting experiment must not silently supply this experiment's heads.
        var cache=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!,"bank-cache-"+Path.GetFileNameWithoutExtension(output));
        var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1,directory:cache);
        double preparation=timer.Elapsed.TotalSeconds;int probes=0,bad=0,wetCovered=0;
        Console.WriteLine($"BANK prepare {preparation:F3}s, fitting {terrain.Amplification.RiverscapeFittingMilliseconds:F1}ms, cached={terrain.Amplification.UsedCachedRiverDatums}");
        var failures=new List<object>();var summaries=new List<object>();
        var h=world.Hydrology;
        double Q(int id)=>(h.NorthernSpringDischargeM3PerS[id]+h.NorthernSummerDischargeM3PerS[id]+h.NorthernAutumnDischargeM3PerS[id]+h.NorthernWinterDischargeM3PerS[id])*.25;
        foreach(var band in new[]{(name:"small",min:0d,max:16d),(name:"local-fit",min:16d,max:64d),(name:"broad",min:64d,max:250d),(name:"major",min:250d,max:2001d)})
        {
            int count=0,bandProbes=0,bandBad=0;
            foreach(var channel in terrain.Hydrology.Channels.Where(c=>{double half=Math.Max(.6,Math.Min(2000,3*Math.Sqrt(Math.Max(0,Q(c.From)))));return (sources==null||sources.Contains(c.From))&&half>=band.min&&half<band.max;}).OrderBy(c=>Rng.Mix((uint)c.From^73421u)))
            {
                var spans=terrain.Amplification.RiverReach(channel.From);if(spans.Length==0)continue;
                bool FanContains(V point,bool start)
                {
                    var endpoint=start?spans[0]:spans[^1];
                    var flag=typeof(RiverscapeSpan).GetField(start?"PoolA":"PoolB");
                    if(flag!=null&&!(bool)flag.GetValue(endpoint)!)return false;
                    V center=start?endpoint.A:endpoint.B;double half=start?endpoint.WidthA:endpoint.WidthB;
                    if(half<=0)return false;
                    double r=RiverscapeKernel.JunctionRadius(half),radius=terrain.RadiusM*.1;
                    if((point-center).Length*radius>r+.001)return false;
                    for(int i=0;i<RiverscapeKernel.JunctionSides;i++)
                    {
                        V a=RiverscapeKernel.JunctionPoint(center,r,i*Math.PI*2/RiverscapeKernel.JunctionSides,radius);
                        V b=RiverscapeKernel.JunctionPoint(center,r,(i+1)*Math.PI*2/RiverscapeKernel.JunctionSides,radius);
                        V cross=V.Cross(b-a,point-a);
                        if(cross.X*center.X+cross.Y*center.Y+cross.Z*center.Z< -1e-14)return false;
                    }
                    return true;
                }
                count++;
                int stations=sources==null?16:spans.Length;
                for(int station=0;station<=stations;station++)
                {
                    int index=Math.Min(spans.Length-1,station*spans.Length/stations);var s=spans[index];
                    if(s.Primitive==RiverFlowPrimitive.Waterfall||s.LevelA<=0&&s.LevelB<=0)continue;
                    double t=station==stations?1:station%2==0?0:.5;
                    double level=s.LevelA+(s.LevelB-s.LevelA)*t;
                    V at=(s.A*(1-t)+s.B*t).Unit;
                    double width=s.WidthA+(s.WidthB-s.WidthA)*t;if(width<.01)continue;
                    var patch=terrain.Patch(at,(width*3+1500)/.1);
                    foreach(int side in new[]{-1,1})
                    {
                        // Probe the actual finite render underlap, using its
                        // paired corners, rather than an averaged bank normal.
                        V a=(s.A+s.SideA*(side*(s.WidthA+2)/(terrain.RadiusM*.1))).Unit;
                        V b=(s.B+s.SideB*(side*(s.WidthB+2)/(terrain.RadiusM*.1))).Unit;
                        V p=(a*(1-t)+b*t).Unit;var sample=patch.Geometry(p);
                        probes++;bandProbes++;
                        // The renderer removes ribbon fragments inside junction
                        // fans. Such an internal edge is not an open shoreline.
                        bool covered=sample.Underwater&&sample.WaterSurfaceM>=level-.03||FanContains(p,true)||FanContains(p,false);
                        if(covered)wetCovered++;
                        if(sample.ElevationM<level-.08&&!covered)
                        {
                            bad++;bandBad++;
                            if(failures.Count<100)failures.Add(new{Band=band.name,Source=channel.From,Receiver=spans[^1].To,Span=index,T=t,Side=side,Width=width,Level=level,Ground=sample.ElevationM,PreWater=sample.PreWaterElevationM,Clearance=level-sample.ElevationM,StartLevel=spans[0].LevelA,EndLevel=spans[^1].LevelB,DistanceToStart=(p-spans[0].A).Length*terrain.RadiusM*.1,DistanceToEnd=(p-spans[^1].B).Length*terrain.RadiusM*.1,Position=new[]{p.X,p.Y,p.Z}});
                        }
                    }
                }
                if(count>=48)break;
            }
            summaries.Add(new{Band=band.name,Reaches=count,Probes=bandProbes,OpenEdgeProbes=bandBad});
            Console.WriteLine($"BANK {band.name}: {count} reaches, {bandProbes} probes, {bandBad} exposed edges");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output,JsonSerializer.Serialize(new{Version=typeof(LocalTerrain).GetField("Version")!.GetRawConstantValue(),PreparationSeconds=preparation,FittingMs=terrain.Amplification.RiverscapeFittingMilliseconds,UsedCached=terrain.Amplification.UsedCachedRiverDatums,Probes=probes,OpenEdgeProbes=bad,CoveredByOtherWaterOrFan=wetCovered,Bands=summaries,Failures=failures,Scope="Sampled actual scalar ground at paired outer render edges, excluding fragments removed inside rendered fans; native mesh/perimeter validation still required"},new JsonSerializerOptions{WriteIndented=true}));
        if(bad>0)throw new Exception($"River bank contact: {bad}/{probes} sampled edges end above uncovered terrain; evidence retained");
        Console.WriteLine($"PASS {probes} sampled river bank-contact probes");
    }
}
