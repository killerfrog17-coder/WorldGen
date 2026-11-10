using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

static class PaleolithicResourceWorldTests
{
    public static void Run(string input,string output)
    {
        var world=WorldFile.Read(File.ReadAllBytes(input));var ground=GroundEnvironment.For(world);var flora=GroundFlora.For(world);
        var terrain=new LocalTerrain(world,horizontalScale:.1);var scatter=new LocalScatter(terrain,.1);var seen=new HashSet<PossessionKind>();var rows=new List<object>();int attempted=0;
        // Bounded QA survey of actual worlds/placed trees, not an additional
        // runtime tree census or invented source spawned by the test.
        for(int i=0;i<world.Surface.Cells.Length&&seen.Count<4;i+=37)
        {
            if(world.ElevationM[i]<1)continue;var at=world.Surface.Cells[i].Position;var g=ground.Cell(i);int region=world.Ecology.Region[i];var species=flora.Region(region).Trees;
            bool wantClay=!seen.Contains(PossessionKind.RawClay)&&PaleolithicClay.ClayGround(g);
            bool wantOchre=!seen.Contains(PossessionKind.RawOchre)&&PaleolithicBinders.OchreGround(g);
            bool wantTree=attempted<24&&species.Any(s=>PaleolithicBinders.Exudate(s).HasValue&&!seen.Contains(PaleolithicBinders.Exudate(s).Value));
            if(!wantClay&&!wantOchre&&!wantTree)continue;
            var actual=terrain.Patch(at,1000).Sample(at);
            if(!actual.Underwater)
            {
                if(wantClay&&PaleolithicClay.ClayGround(actual.Ground)){seen.Add(PossessionKind.RawClay);rows.Add(new{kind="clay",cell=i,region,position=new[]{at.X,at.Y,at.Z},actual.Ground.Clay,actual.Ground.Iron,actual.Ground.DepthM});}
                if(wantOchre&&PaleolithicBinders.OchreGround(actual.Ground)){seen.Add(PossessionKind.RawOchre);rows.Add(new{kind="ochre",cell=i,region,position=new[]{at.X,at.Y,at.Z},actual.Ground.Clay,actual.Ground.Iron,actual.Ground.DepthM});}
            }
            if(!wantTree||actual.Underwater||actual.Forest.Coverage<.12)continue;attempted++;
            var patch=scatter.Build(scatter.Address(at));
            foreach(var tree in patch.Instances.Where(p=>p.Kind==ScatterKind.GeneratedTree))
            {
                var s=species.FirstOrDefault(s=>s.MorphologyCode==(tree.Variant&~7));if(s==null)continue;var kind=PaleolithicBinders.Exudate(s);if(!kind.HasValue||seen.Contains(kind.Value))continue;
                var product=Foraging.Products(s).FirstOrDefault(p=>p.Kind==kind.Value);if(product==null)throw new Exception("Eligible placed tree lost its forage product.");
                var item=Foraging.Gather(product,tree.Position);new PlayerInventory{Items={item}}.Validate();seen.Add(kind.Value);
                rows.Add(new{kind=kind.ToString(),cell=i,region,tree.Id,position=new[]{tree.Position.X,tree.Position.Y,tree.Position.Z},s.Name,s.MorphologyCode,item.MassKg,item.Kind});
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllText(output,JsonSerializer.Serialize(new{seen=seen.Select(k=>k.ToString()).ToArray(),attempted,rows},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        if(seen.Count!=4)throw new Exception("Actual world resource survey missing: "+string.Join(",",new[]{PossessionKind.RawClay,PossessionKind.RawOchre,PossessionKind.PlantGum,PossessionKind.PlantResin}.Where(k=>!seen.Contains(k))));
        Console.WriteLine($"PASS actual world clay/ochre deposits and placed gum/resin trees; {attempted} bounded tree plots; {output}");
    }
}
