using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Serialization;

static class HumanSiteTests
{
    public static void Run(string path)
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var world=WorldFile.ReadFile(path);var terrain=new LocalTerrain(world,horizontalScale:.1);var scatter=new LocalScatter(terrain,.1);
        var forest=world.Ecology.Regions.OrderByDescending(r=>world.Ecology.Habitat[r.RepresentativeCell].ProductivityGCM2Year).First();
        var points=new[]{world.Surface.Cells[forest.RepresentativeCell].Position,new V(1,1,1).Unit};
        foreach(var point in points)
        {
            var coldWatch=Stopwatch.StartNew();var cold=new LocalScatter(terrain,.1).SettlementSolids(point,150);Console.WriteLine($"Camp solids COLD 150m: {cold.Length}, {coldWatch.Elapsed.TotalMilliseconds:F2}ms background work");
            var all=scatter.Around(point,900).SelectMany(k=>scatter.Build(k).Instances).ToArray();
            bool Eligible(ScatterInstance item)
            {
                bool tree=item.Kind==ScatterKind.GeneratedTree||item.Kind==ScatterKind.Broadleaf||item.Kind==ScatterKind.Conifer||item.Kind==ScatterKind.SavannaTree;
                bool rock=item.Kind==ScatterKind.Boulder||item.Kind==ScatterKind.Outcrop||item.Kind==ScatterKind.Stone;
                return tree&&item.HeightM>=1.8||rock&&item.HeightM>=.35&&item.WidthM>=.4;
            }
            foreach(double radius in new[]{150d,350})
            {
                var reference=all.Where(Eligible).Where(item=>
                {
                    bool tree=item.Kind==ScatterKind.GeneratedTree||item.Kind==ScatterKind.Broadleaf||item.Kind==ScatterKind.Conifer||item.Kind==ScatterKind.SavannaTree;
                    double spread=tree?item.WidthM*(item.Kind==ScatterKind.GeneratedTree?new TreeMorphology(item.Variant).TrunkRadius:.035):item.WidthM*.6;
                    return (item.Position-point).Length*terrain.RadiusM*.1<=radius+spread;
                }).Select(p=>p.Id).OrderBy(id=>id).ToArray();
                var timer=Stopwatch.StartNew();var bounded=scatter.SettlementSolids(point,radius);
                Check(reference.SequenceEqual(bounded.Select(p=>p.Id)),"Bounded camp survey matches exhaustive canonical vegetation/rock placement, including cube-face edges");
                if(radius==150)Check(reference.SequenceEqual(cold.Select(p=>p.Id)),"Cold clipped tree survey agrees before any complete tree tiles are cached");
                Check(bounded.Select(p=>p.Id).Distinct().Count()==bounded.Length,"Survey contains no duplicate natural objects");
                Console.WriteLine($"Camp solids {radius}m: {bounded.Length}, {timer.Elapsed.TotalMilliseconds:F2}ms background work");
            }
        }
        bool cancelled=false;try{scatter.SettlementSolids(points[0],200,new CancellationToken(true));}catch(OperationCanceledException){cancelled=true;}
        Check(cancelled,"Camp survey respects cancellation");
        var grass=PortableTechnology.Create(PossessionKind.DryGrass,points[0]);grass.MassKg=10;
        var hide=PortableTechnology.Create(PossessionKind.RawHide,points[0]);hide.MassKg=4;
        var bed=new CampObject{Id="old-bed",Kind=CampKind.Bedding,Position=points[0],ClearedWidthM=5,ClearedDepthM=4,Materials=new(){grass,hide}};
        var segmented=bed.Copy();HumanCampLayout.AgeBedding(bed,50,2200);for(int i=0;i<10;i++)HumanCampLayout.AgeBedding(segmented,5,2200);
        Check(Math.Abs(bed.Materials[0].MassKg-segmented.Materials[0].MassKg)<1e-12,"Bedding decay is independent of revisit/update partition");
        Check(bed.Materials[0].MassKg<2&&bed.Materials[1].MassKg==4,"Abandoned vegetation decays while hide retains its distinct material");
        var dry=segmented.Copy();HumanCampLayout.AgeBedding(dry,50,200);var wet=segmented.Copy();HumanCampLayout.AgeBedding(wet,50,2500);
        Check(wet.Materials[0].MassKg<dry.Materials[0].MassKg,"Wet camps decompose faster than dry camps");
        var local=new HumanLocalState();local.Camps.Add(new HumanCampMemory{Site=4,Group=3,Position=points[0],LastDecayCalendarDay=42,AbandonedCalendarDay=12,Objects=new(){bed}});local.Validate();
        var roundtrip=WorldJson.Read<HumanLocalState>(WorldJson.Write(local));roundtrip.Validate();
        Check(roundtrip.Camps[0].LastDecayCalendarDay==42&&roundtrip.Camps[0].Objects[0].ClearedWidthM==5,"Saved camp traces retain decay date and floor clearing footprint");
        Console.WriteLine($"PASS {checks} camp site and trace checks");
    }
}
