using WorldGen.Core;
using WorldGen.Presentation;

static class RiverPlanformTests
{
    public static void World(string path,string output,int pinned=-1)
    {
        var world=WorldGen.Serialization.WorldFile.ReadFile(path);var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        var rows=new List<object>();int active=0,filters=0,bent=0,banks=0;
        var candidates=terrain.Hydrology.Channels.Where(c=>(pinned<0||c.From==pinned)&&c.WidthM>12&&c.WidthM<150&&c.StartM>40&&c.StartM<1600&&(c.StartM-c.EndM)/Math.Max(1,c.LengthM)<.008).ToArray();
        Console.WriteLine($"Metadata candidates {candidates.Length} / {terrain.Hydrology.Channels.Length}");
        foreach(var channel in candidates.OrderBy(c=>Rng.Mix(unchecked((uint)c.From^(uint)world.Recipe.Seed))).Take(160))
        {
            var spans=terrain.Amplification.RiverReach(channel.From);if(spans.Length<8)continue;active++;
            double length=spans.Sum(s=>(s.B-s.A).Length),chord=(spans[spans.Length-1].B-spans[0].A).Length,sinuosity=length/Math.Max(1e-9,chord);
            bool filter=channel.WidthM>12&&channel.WidthM<150&&channel.StartM>40&&channel.StartM<1600&&(channel.StartM-channel.EndM)/Math.Max(1,channel.LengthM)<.008;
            if(filter)filters++;if(sinuosity>1.03)bent++;
            var middle=spans[spans.Length/2];double half=(middle.WidthA+middle.WidthB)*.5;
            V center=(middle.A+middle.B).Unit,bank=(center+middle.SideA*((half*2+6)/(terrain.RadiusM*.1))).Unit;
            bool dry=false;
            if(sinuosity>1.03){var s=terrain.Patch(bank,10000).Sample(bank);dry=!s.Underwater;if(dry)banks++;}
            rows.Add(new{channel.From,channel.WidthM,channel.StartM,Grade=(channel.StartM-channel.EndM)/channel.LengthM,Count=spans.Length,LengthM=length*terrain.RadiusM*.1,MinimumHalfWidth=spans.Min(s=>Math.Min(s.WidthA,s.WidthB)),MaximumHalfWidth=spans.Max(s=>Math.Max(s.WidthA,s.WidthB)),Sinuosity=sinuosity,DryBank=dry,Filter=filter});
        }
        File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(rows,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"River selection: {active} active, {filters} channel filter, {bent} sinuous, {banks} dry banks");
    }
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        const double radius=637100,length=6000;const int count=256;
        V Spine(double t)=>new V(1,(t-.5)*length/radius,0).Unit;
        var low=RiverPlanform.Build(Spine,count,length,14,.001,.8,.15,radius,7312);
        var repeat=RiverPlanform.Build(Spine,count,length,14,.001,.8,.15,radius,7312);
        var steep=RiverPlanform.Build(Spine,count,length,14,.08,.8,.15,radius,7312);
        double distance=0,maxSide=0,maxCurvature=0;
        for(int i=0;i<=count;i++)
        {
            Check((low[i]-repeat[i]).Length==0,"Recreated reach has identical bends");
            Check((steep[i]-Spine(i/(double)count)).Length<1e-14,"Steep confined river keeps its drainage path");
            Check(double.IsFinite(low[i].X)&&Math.Abs(low[i].Length-1)<1e-8,"Finite spherical channel centre within canonical coordinate precision");
            maxSide=Math.Max(maxSide,Math.Abs(low[i].Z)*radius);
            if(i>0){distance+=(low[i]-low[i-1]).Length*radius;Check(low[i].Y>low[i-1].Y,"Bends never loop over themselves or reverse the reach");}
            if(i>0&&i<count)
            {
                V a=(low[i]-low[i-1])*radius,b=(low[i+1]-low[i])*radius;
                double cross=V.Cross(a,b).Length;
                if(cross>1e-9)maxCurvature=Math.Max(maxCurvature,2*cross/(a.Length*b.Length*(a+b).Length));
            }
        }
        Check((low[0]-Spine(0)).Length==0&&(low[count]-Spine(1)).Length==0,"Confluence positions remain exact");
        Check(maxSide>40&&maxSide<90,"Lowland reach develops material-scale bends within its valley envelope");
        Check(distance>length*1.01&&distance<length*1.5,"Lowland river has bounded nontrivial sinuosity");
        Check(1/maxCurvature>14*2,"Bend radius exceeds channel width; opposite banks do not fold over");
        Check(Math.Abs(low[1].Z)*radius<.05&&Math.Abs(low[count-1].Z)*radius<.05,"Shared junction tangent collars are smooth");
        Console.WriteLine($"PASS {checks} planform checks; sinuosity {distance/length:F3}, offset {maxSide:F2}m, minimum radius {1/maxCurvature:F2}m");
    }
}
