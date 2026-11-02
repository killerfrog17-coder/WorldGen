using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
static class ForagerHabitatTests
{
    public static void Run(string file)
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var w=WorldFile.ReadFile(file);var source=w.Prehistory;string hash=source.ComputeHash();var regions=(int[])w.Ecology.Region.Clone();
        // Controlled climate dates isolate the observer; these are explicitly
        // not claims of independently simulated demographic histories.
        var warm=source.Copy();warm.Foragers.Seconds=75000*SimulationTime.Year;
        var cold=source.Copy();cold.Foragers.Seconds=179000*SimulationTime.Year;
        var a=ForagerHabitat.For(w,warm);var b=ForagerHabitat.For(w,cold);int changed=0,iceWarm=0,iceCold=0;double tempChange=0;int land=0;
        for(int i=0;i<w.Surface.Cells.Length;i++)
        {
            var x=a.Cell(i);var y=b.Cell(i);if(x==null||y==null)continue;land++;
            if(x.Classification!=y.Classification)changed++;if(x.IceCover>.5)iceWarm++;if(y.IceCover>.5)iceCold++;
            tempChange+=y.Habitat.MeanTenthsC-x.Habitat.MeanTenthsC;
            Check(x.Habitat.GrowingDays>=0&&x.Habitat.GrowingDays<=364&&y.ForestCover>=0&&y.ForestCover<=1,"Dated habitat outputs remain within physical/proxy bounds");
        }
        Check(changed>land*.03&&iceCold>iceWarm&&tempChange/land< -10,"Glacial forcing visibly changes climate classes, ice and thermal habitat instead of only human growth rates");
        var warmColors=GlobeView.CellValues(w,Layer.TemperatureWinter,history:warm).Colors;var coldColors=GlobeView.CellValues(w,Layer.TemperatureWinter,history:cold).Colors;
        Check(warmColors.Where((v,i)=>Math.Abs(v-coldColors[i])>.001).Count()>land,"Actual map colour generation reads the requested historical date");
        int cell=source.Foragers.Districts.First().Cell;var atlas=EcologyAtlas.Build(w,cell);
        Check(atlas.Articles.Any(x=>x.Body.Contains("CURRENT ASSEMBLAGE")),"Ecology articles expose the current assemblage separately from source records");
        var probe=source.Copy();int district=probe.Foragers.CellDistrict[cell];var biota=probe.Foragers.Biota[district];var recorded=w.Ecology.Regions[w.Ecology.Region[cell]].Species.Select(p=>p.TaxonId).ToHashSet();
        int outsider=w.Ecology.Taxa.First(t=>!recorded.Contains(t.Id)).Id;var arrivals=biota.Taxa.Select((id,k)=>(id,value:biota.Abundance[k])).ToDictionary(p=>p.id,p=>p.value);arrivals[outsider]=.5;
        biota.Taxa=arrivals.Keys.OrderBy(x=>x).ToArray();biota.Abundance=biota.Taxa.Select(id=>arrivals[id]).ToArray();w.Prehistory=probe;
        var arrived=EcologyAtlas.Build(w,cell);Check(arrived.Articles.Any(x=>x.Title==w.Ecology.Taxa[outsider].Name&&x.Body.Contains("arrived after")),"A colonising species absent from the ancestral region roster appears in the actual ecology atlas");w.Prehistory=source;
        Check(source.ComputeHash()==hash&&w.Ecology.Region.SequenceEqual(regions),"Historical projection and colonist inspection preserve source history and all ecoregion boundaries");
        Console.WriteLine($"PASS {checks} dated habitat/map/assemblage checks; changed classes={changed}/{land}; ice={iceWarm}->{iceCold}");
    }
}
