using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class SubcellTests
{
    public static void Run(Action<bool,string> check)
    {
        foreach(string preset in new[]{"Preview","Earth"})
        {
            var recipe=Recipe.Preset(preset);recipe.Detail=20000;var pipeline=new WorldPipeline();var w=pipeline.Generate(recipe);
            SubcellGeography.Validate(w);check(w.Subcells.Islands.Length>0,preset+" has unresolved islands");
            foreach(var g in w.Subcells.Cells)
            {
                var list=w.Subcells.Islands.Skip(g.FirstIsland).Take(g.IslandCount).ToArray();
                check(list.Sum(i=>i.AreaM2)==g.TotalAreaM2 && list.Max(i=>i.AreaM2)==g.LargestIslandAreaM2,"Individual and cell island areas agree");
                check(g.CoverageFraction>0 && g.CoverageFraction<=.5,"Island coverage leaves host cell ocean");
                foreach(var island in list)check(island.Outline.All(p=>w.Surface.Cells[g.Cell].Neighbors.All(j=>V.Dot(p,w.Surface.Cells[j].Position)<=V.Dot(p,w.Surface.Cells[g.Cell].Position)+1e-10)),"Island outline stays inside its owning cell");
            }
            var restored=WorldFile.Read(WorldFile.Write(w));check(StateHash.Compute(restored.Subcells)==StateHash.Compute(w.Subcells),"Sub-cell IDs, outlines and areas persist losslessly");
            check(StateHash.Compute(WorldJson.ReadWorld(WorldJson.Write(w)).Subcells)==StateHash.Compute(w.Subcells),"JSON sub-cell persistence");
            w.Subcells.Cells[0].TotalAreaM2++;
            try{WorldFile.Read(WorldFile.Write(w));check(false,"Tampered island accounting rejected");}catch(ArgumentException){check(true,"Tampered island accounting rejected");}
            w.Subcells.Cells[0].TotalAreaM2--;
            var old=recipe.Clone();old.AlgorithmVersion=Recipe.FeatureBlendAlgorithm;var previous=pipeline.Generate(old);
            check(previous.Subcells==null,"Historical recipes retain null sub-cell data");
            check(StateHash.Compute(new object[]{w.ElevationM,w.Climate,w.Hydrology,w.Ecology})==StateHash.Compute(new object[]{previous.ElevationM,previous.Climate,previous.Hydrology,previous.Ecology}),"New island metadata does not alter existing terrain, hydrology, climate or ecology");
            long vertices=0;double area=0;
            foreach(var piece in NaturalSurface.Chunks(w,Projection.Map,default))
            {
                check(piece.NaturalHeight.Length==piece.CellIds.Length,"Signed contour elevations survive projection");vertices+=piece.CellIds.Length;
                for(int t=0;t<piece.Triangles.Length;t+=3){int a=piece.Triangles[t]*3,b=piece.Triangles[t+1]*3,c=piece.Triangles[t+2]*3;area+=Math.Abs((piece.Positions[b]-piece.Positions[a])*(piece.Positions[c+1]-piece.Positions[a+1])-(piece.Positions[b+1]-piece.Positions[a+1])*(piece.Positions[c]-piece.Positions[a]))/2;}
            }
            check(Math.Abs(area-8)<.0001,"Continuous map has full seam and pole coverage");
            check(vertices<w.Surface.Cells.Length*3,"Continuous mesh is not subdivided into more simulation cells");
            check(NaturalSurface.WaterAndIslands(w,Projection.Map,default).All(p=>p.Positions.All(float.IsFinite)),"Small features project to finite positions");
            int lakeCell=Enumerable.Range(0,w.Surface.Cells.Length).First(i=>!w.Hydrology.OceanConnected[i]);
            foreach(double fraction in new[]{.0001,.01,.5})
            {
                int n=w.Surface.Cells.Length;var sample=new World{Recipe=w.Recipe,Surface=w.Surface,ElevationM=w.ElevationM,Hydrology=new HydrologyResult{OceanConnected=new bool[n],LakeCoverageFraction=new double[n],WaterBody=new int[n],Waters=new[]{new WaterBody{SurfaceElevationM=w.ElevationM[lakeCell]+2}}}};
                sample.Hydrology.LakeCoverageFraction[lakeCell]=fraction;double footprint=0;
                foreach(var patch in NaturalSurface.WaterAndIslands(sample,Projection.Globe,default))
                {
                    V Point(int id)=>new V(patch.Positions[id*3],patch.Positions[id*3+1],patch.Positions[id*3+2]).Unit;
                    for(int t=0;t<patch.Triangles.Length;t+=3){V a=Point(patch.Triangles[t]),b=Point(patch.Triangles[t+1]),c=Point(patch.Triangles[t+2]);footprint+=2*Math.Atan2(Math.Abs(a.X*(b.Y*c.Z-b.Z*c.Y)+a.Y*(b.Z*c.X-b.X*c.Z)+a.Z*(b.X*c.Y-b.Y*c.X)),1+V.Dot(a,b)+V.Dot(b,c)+V.Dot(c,a))*6371000.0*6371000.0;}
                }
                double target=fraction*w.Surface.Cells[lakeCell].AreaM2;check(Math.Abs(footprint-target)/target<.03,$"Rendered lake footprint tracks stored fraction: {preset} f={fraction} actual={footprint} target={target}");
            }
            {
                int n=w.Surface.Cells.Length,j=w.Surface.Cells[lakeCell].Neighbors[0];
                var sample=new World{Recipe=w.Recipe,Surface=w.Surface,ElevationM=w.ElevationM,Hydrology=new HydrologyResult{OceanConnected=new bool[n],LakeCoverageFraction=new double[n],WaterBody=new int[n],Waters=new[]{new WaterBody{SurfaceElevationM=10}}}};
                sample.Hydrology.LakeCoverageFraction[lakeCell]=.4;sample.Hydrology.LakeCoverageFraction[j]=.5;
                var shapes=new ShoreTerrainSampler(sample).Features;
                check(shapes.Length==1 && shapes[0].Kind==ShoreKind.Lake,"Adjacent substantial lake fractions form one continuous basin footprint, not linked cell fans");
            }
            Console.WriteLine($"{preset}: {w.Subcells.Islands.Length} islands in {w.Subcells.Cells.Length} ocean cells; {w.Subcells.Cells.Count(g=>g.IslandCount>1)} multi-island cells; {w.Hydrology.LakeCoverageFraction.Count(f=>f>0&&f<.9)} fractional lake cells");
        }
    }
}
