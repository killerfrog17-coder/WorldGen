using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Presentation;

public static class DisplayPerformanceTests
{
    public static void Run(Action<bool,string> check,int detail,bool meshOnly=false)
    {
        var recipe=Recipe.Preset("Preview");recipe.Detail=detail;
        var timer=Stopwatch.StartNew();
        var w=meshOnly?new World{Recipe=recipe,Surface=SphericalMesh.Build(recipe,default),ElevationM=new int[detail],CellMicroplate=new int[detail]}:new WorldPipeline().Generate(recipe);
        Console.WriteLine($"{(meshOnly?"Mesh":"Generation")} {detail}: {timer.Elapsed.TotalSeconds:F2}s; hash {(meshOnly?"not generated":w.Stages.Last().Hash)}");
        double totalArea=0;
        timer.Restart();long vertices=0,indices=0;int chunks=0;
        foreach(var piece in DisplayCache.Geometry(w,Projection.Globe,default))
        {
            var d=piece.Data;var dual=MapView.Project(d,0,0,true);chunks++;
            check(dual.SpherePositions.Length==dual.Positions.Length && dual.SphereLines.Length==dual.Lines.Length,"Dual projection coordinates match");
            check(dual.Triangles.All(i=>i>=0 && i<dual.CellIds.Length),"Indexed projection has valid indices");
            check(dual.Positions.All(float.IsFinite) && dual.SpherePositions.All(float.IsFinite),"Finite dual geometry");
            if(piece.Kind!="surface")continue;
            vertices+=dual.CellIds.Length;indices+=dual.Triangles.Length;
            double area=0;
            for(int t=0;t<dual.Triangles.Length;t+=3)
            {
                int a=dual.Triangles[t]*3,b=dual.Triangles[t+1]*3,c=dual.Triangles[t+2]*3;
                area+=Math.Abs((dual.Positions[b]-dual.Positions[a])*(dual.Positions[c+1]-dual.Positions[a+1])-(dual.Positions[b+1]-dual.Positions[a+1])*(dual.Positions[c]-dual.Positions[a]))/2;
                check(dual.CellIds[a/3]==dual.CellIds[b/3] && dual.CellIds[a/3]==dual.CellIds[c/3],"Flat cell colors never interpolate between owners");
            }
            totalArea+=area;
        }
        check(Math.Abs(totalArea-8)<.0001,"Projected mesh covers whole map, including seam and polar caps");
        check(vertices<indices*.65,"Indexed geometry removes at least 35 percent of fan vertices");
        check(chunks<=DisplayCache.ExpectedChunkCount(w),"Progress denominator covers all cached overlays");
        Console.WriteLine($"Dual geometry: {timer.Elapsed.TotalSeconds:F2}s; {vertices:N0} vertices / {indices:N0} indices; {chunks} chunks; map area {totalArea:F8}");
        var old=recipe.Clone();old.AlgorithmVersion=Recipe.RangeControlsAlgorithm;old.MountainStrengthPercent=100;old.LakeAmountPercent=100;
        ClimateCalibration.Upgrade(old);check(old.MountainStrengthPercent==100 && old.LakeAmountPercent==100,"3.29 upgrades do not rebase controls twice");
        var locator=new CellLocator(w.Surface.Cells);var p=w.Surface.Cells[0].Position;
        var actual=locator.Within(p,.005);var expected=w.Surface.Cells.Where(c=>V.Dot(c.Position-p,c.Position-p)<=.005).Select(c=>c.Id).ToArray();
        check(actual.SequenceEqual(expected),"Spatial radius query matches brute-force sorted result");
        if(!meshOnly)
        {
            var hotspots=w.Features.Where(f=>f.Kind==FeatureKind.Hotspot || f.Kind==FeatureKind.RidgeHotspot || f.Kind==FeatureKind.IslandChain).ToArray();
            check(hotspots.All(f=>f.WidthM<200000),"Hotspot edifice supports stay physically small");
            bool curved=false;
            foreach(var chain in hotspots.GroupBy(f=>f.Name.Split(' ').Last().Split('/')[0]))
            {
                var points=chain.OrderBy(f=>f.AgeMyr).ToArray();if(points.Length<3)continue;
                var normal=V.Cross(points[0].Center,points[1].Center).Unit;
                if(points.Skip(2).Any(f=>Math.Abs(V.Dot(normal,f.Center))>.00001))curved=true;
            }
            check(curved,"Hotspot tracks depart from a single great-circle line");
            int emerged=hotspots.Count(f=>f.AgeMyr>0 && f.Cells.Any(i=>w.Continent[i]<0 && w.ElevationM[i]>0));
            if(detail>=80000)check(emerged>0,"Older oceanic hotspot edifices can remain emerged");
            Console.WriteLine($"Hotspots/trail nodes: {hotspots.Length}; emerged older oceanic nodes: {emerged}");
        }
    }
}
