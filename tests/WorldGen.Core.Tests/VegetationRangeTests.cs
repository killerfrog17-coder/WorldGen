using WorldGen.Core;
using WorldGen.Presentation;

static class VegetationRangeTests
{
    public static void Run()
    {
        int checks=0;
        foreach(ScatterKind kind in Enum.GetValues<ScatterKind>())
        foreach(double cap in new[]{1d,25,70,100,140,300,650,1400,1800})
        foreach(double largest in new[]{.02,.4,1,3,12,30})
        {
            double range=ScatterLodBlend.MaximumDistance(kind,largest,cap);
            foreach(double smaller in new[]{largest,largest*.9,largest*.2})
            foreach(double distance in new[]{range,range+.1,range+10,range*2})
            {
                if(ScatterLodBlend.At(kind,smaller,distance,maximumRange:cap,nearEnd:0,farEnd:0).Visibility!=0)
                    throw new Exception($"A kind/width range rejected a visible {kind} at {distance}, cap {cap}");
                checks++;
            }
        }
        Console.WriteLine($"PASS {checks} conservative same-kind group ranges across sizes and quality caps");
    }
}
