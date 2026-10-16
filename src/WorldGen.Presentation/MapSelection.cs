using System.Globalization;
using System.Text.RegularExpressions;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static class MapSelection
    {
        static AtlasFact Fact(string label,string value)=>new AtlasFact{Label=label,Value=value};
        static string Number(double n)=>n.ToString("0.#",CultureInfo.InvariantCulture);
        public static SelectionCard Read(World? world,int cell,Layer layer)
        {
            var card=new SelectionCard{Title=world==null?"Select a location":CellInspection.Describe(world,cell,layer),Subtitle=GlobeView.LayerName(layer)};
            if(world?.Ecology==null||cell<0||cell>=world.Surface.Cells.Length||layer!=Layer.Ecoregions)return card;
            int id=world.Ecology.Region[cell];if(id<0)return card;
            var region=world.Ecology.Regions[id];var habitat=world.Ecology.Habitat[cell];card.Title=region.Name;
            card.Subtitle=Regex.Replace(habitat.Biome.ToString(),"([a-z])([A-Z])","$1 $2");
            card.IllustrationKey=HabitatPortrait.At(world,cell)?.Key??"";
            card.Facts=new[]{Fact("Region area",Number(region.AreaM2/1e6)+" km²"),Fact("Growing season",habitat.GrowingDays+" days"),Fact("Seasonal temperatures",Number(habitat.ColdTenthsC/10.0)+"–"+Number(habitat.WarmTenthsC/10.0)+" °C"),Fact("Flood exposure",Number(habitat.FloodFractionPermille/10.0)+"% · "+habitat.FloodDaysPerYear+" days/yr"),Fact("Soil",habitat.Soil.ToString()),Fact("Recorded taxa",region.Species.Length.ToString())};
            card.Note="Habitat estimates · open the food web for recorded species and feeding links.";return card;
        }
    }
}
