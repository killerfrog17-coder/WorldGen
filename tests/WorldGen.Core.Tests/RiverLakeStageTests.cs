using System.Reflection;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class RiverLakeStageTests
{
    public static void Run(string path)
    {
        var world=WorldFile.ReadFile(path);string hash=StateHash.Compute(world);
        var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);var amp=terrain.Amplification;
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        object Field(string name)=>typeof(HyperAmplification).GetField(name,flags)!.GetValue(amp)!;
        var accepted=((int[])Field("lakeFaces")).Where(i=>i>=0).ToHashSet();
        var nodes=(Array)Field("rootNodes");var levels=(double[])Field("lakeLevels");var owners=(int[])Field("lakeOutletOwners");
        var datums=amp.ExportRiverDatums();var active=(bool[])Field("riverActive");
        int checks=0,fixedNodes=0,spills=0,activeNodes=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        for(int i=0;i<nodes.Length;i++)
        {
            object node=nodes.GetValue(i)!;object Value(string name)=>node.GetType().GetField(name)!.GetValue(node)!;
            if(Value("Type").ToString()=="Lake")
            {
                int id=(int)(ulong)Value("LakeKey")-1;
                Check(accepted.Contains(id),$"Rejected basin cannot remain a fixed lake control at {i}");
                double expected=amp.Fjords.OceanNode(i)?0:levels[id];
                if(expected<0&&!world.Hydrology.Waters[id].Endorheic&&datums[i]==0)
                {
                    var at=world.Surface.Cells[i].Position;var actual=terrain.Patch(at,3000).Geometry(at);
                    Check(actual.WaterKind==LocalWaterKind.Ocean&&actual.WaterSurfaceM==0,"A below-sea catalog node may adopt the sea datum only where the actual receiving sea renders");expected=0;
                }
                Check(Math.Abs(datums[i]-expected)<1e-8,$"Accepted lake {id} keeps its rendered stage at node {i}: {datums[i]:R}/{expected:R}");fixedNodes++;
            }
            // A below-sea catalog lake draining to the sea is backwater at0;
            // genuinely enclosed below-sea basins retain their negative stage.
            if(owners[i]>=0){int id=owners[i];double expected=amp.Fjords.OceanNode(i)?0:world.Hydrology.Waters[id].Endorheic?levels[id]:Math.Max(0,levels[id]);Check(accepted.Contains(id)&&Math.Abs(datums[i]-expected)<1e-8,$"Accepted spill {i} keeps its physical stage {datums[i]:R}/{expected:R}");spills++;}
            int next=world.Hydrology.Receiver[i];if(!active[i]||next<0)continue;
            activeNodes++;Check(datums[i]>=datums[next]-1e-8,$"Broad drainage stays downhill across node {i}->{next}");
        }
        Check(fixedNodes>0&&spills>0&&activeNodes>100,"Real world exercises lakes, controlled spills and actual river chains");
        // These formerly failed because discarded basin201 forced an8m head
        // through accepted basin188 (6.309816m). Keep actual historical site.
        if(world.Recipe.Seed==73421&&nodes.Length>49173)
        {
            var reach=amp.RiverReach(46966);Check(reach.Length>0,"Historical backwater tributary remains an actual river");
            Check(Math.Abs(reach[^1].LevelB-levels[188])<1e-8,"Historical tributary meets the lake that actually renders");
        }
        Check(StateHash.Compute(world)==hash,"Derived lake repair preserves source world/history");
        Console.WriteLine($"PASS {checks} lake/river stage controls; {fixedNodes} standing nodes, {spills} spills, {activeNodes} admitted links");
    }
}
