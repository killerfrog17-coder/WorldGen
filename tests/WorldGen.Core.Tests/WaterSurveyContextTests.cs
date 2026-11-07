using System.Reflection;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class WaterSurveyContextTests
{
    public static void Run(string path)
    {
        var world=WorldFile.ReadFile(path);string history=world.Prehistory.Hash;
        var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);var owner=terrain.Amplification;
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        const BindingFlags All=Hidden|BindingFlags.Public;
        var ownerType=typeof(HyperAmplification);var type=ownerType.GetNestedType("Patch",BindingFlags.NonPublic);
        var roots=(Array)ownerType.GetField("roots",Hidden).GetValue(owner);
        var rootType=roots.GetType().GetElementType();var fa=rootType.GetField("A",All);var fb=rootType.GetField("B",All);var fc=rootType.GetField("C",All);
        var offsets=(int[])ownerType.GetField("riverFaceOffsets",Hidden).GetValue(owner);
        var faces=(int[])ownerType.GetField("riverFaces",Hidden).GetValue(owner);
        object Region(int[] source)=>Activator.CreateInstance(type,All,null,new object[]{owner,source,false},null);
        var sample=type.GetMethod("Sample",BindingFlags.Instance|BindingFlags.Public);var resultType=sample.ReturnType;
        var height=resultType.GetField("Height",All);var water=resultType.GetField("Water",All);var kind=resultType.GetField("Kind",All);var province=resultType.GetField("Landscape",All);
        // Exact pre-optimization implementation: freshly create the same sorted
        // two-ring neighbourhood for EVERY query and start from its roots.
        SmallWaterGround Reference(V point)
        {
            int cell=terrain.NearestCell(point);var nearby=new HashSet<int>();
            void Add(int vertex){for(int i=offsets[vertex];i<offsets[vertex+1];i++)nearby.Add(faces[i]*3);}
            for(int i=offsets[cell];i<offsets[cell+1];i++)
            {var root=roots.GetValue(faces[i]);Add((int)fa.GetValue(root));Add((int)fb.GetValue(root));Add((int)fc.GetValue(root));}
            var sorted=nearby.Order().ToArray();var value=sample.Invoke(Region(sorted),new object[]{point,false,0d,false});
            return new SmallWaterGround((double)height.GetValue(value),(double)water.GetValue(value),(LocalWaterKind)kind.GetValue(value),(LandscapeControls)province.GetValue(value));
        }
        object parent=Region(Array.Empty<int>());
        var actual=(Func<V,SmallWaterGround>)Delegate.CreateDelegate(typeof(Func<V,SmallWaterGround>),parent,type.GetMethod("SmallWaterGroundAt",Hidden));
        var cells=Enumerable.Range(0,world.Surface.Cells.Length).GroupBy(i=>terrain.Profile(i).Biome).Select(g=>g.First()).ToArray();
        var points=new List<V>{new V(.004921601,-.464940574,-.88532821).Unit,new V(.032332564,-.451417,-.891727143).Unit,new V(.716451886,-.196812427,-.669299308).Unit};
        foreach(int cell in cells)
        {
            V at=world.Surface.Cells[cell].Position;points.Add(at);
            foreach(int neighbour in world.Surface.Cells[cell].Neighbors.Take(2))
            {
                V other=world.Surface.Cells[neighbour].Position,edge=(at+other).Unit,tangent=(other-at).Unit;
                points.Add(edge);points.Add((edge+tangent*1e-9).Unit);points.Add((edge-tangent*1e-9).Unit);
            }
        }
        var expected=points.Select(Reference).ToArray();int checks=0;
        for(int i=0;i<points.Count;i++)
        {if(!actual(points[i]).Equals(expected[i]))throw new Exception("Survey context changed complete literal ground at "+i);checks++;}
        for(int i=points.Count-1;i>=0;i--)
        {if(!actual(points[i]).Equals(expected[i]))throw new Exception("Reverse/slot replacement changed complete ground at "+i);checks++;}
        Parallel.For(0,points.Count*4,new ParallelOptions{MaxDegreeOfParallelism=4},i=>
        {int at=i%points.Count;if(!actual(points[at]).Equals(expected[at]))throw new Exception("Concurrent bounded survey context changed ground at "+at);});
        checks+=points.Count*4;
        var cached=(Array)type.GetField("waterSurveyContexts",Hidden).GetValue(parent);
        if(cached.Length!=4)throw new Exception("Survey context cache is not bounded to four slots");
        foreach(object context in cached)
        {
            if(context==null)continue;
            object region=context.GetType().GetField("Region").GetValue(context);
            if(type.GetField("waterSurveyContexts",Hidden).GetValue(region)!=null||type.GetField("last",Hidden).GetValue(region)!=null)
                throw new Exception("Survey context retained recursive contexts or point-order-dependent terminal faces");
        }
        if(world.Prehistory.Hash!=history)throw new Exception("Survey contexts mutated source history");
        Console.WriteLine($"PASS {checks} full literal/cached/reversed/concurrent samples across {cells.Length} biome contexts and coarse-edge probes; fixed4slots, no nested contexts/terminal retention, history unchanged");
    }
}
