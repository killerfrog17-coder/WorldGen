using System;
using System.Globalization;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    [Serializable] public sealed class HabitatFaunaCard
    {
        public string Name="",Role="",BodyMass="",Variant="";
    }
    [Serializable] public sealed class HabitatPortrait
    {
        public string Key="",Title="",Caption="Illustrative habitat, not a reconstruction of this location.";
        public AtlasFact[] Conditions=Array.Empty<AtlasFact>();
        public HabitatFaunaCard[] Fauna=Array.Empty<HabitatFaunaCard>();
        public static HabitatPortrait? At(World world,int cell)
        {
            if(world?.Ecology==null||cell<0||cell>=world.Ecology.Habitat.Length||world.Hydrology.OceanConnected[cell])return null;
            var h=world.Ecology.Habitat[cell];string key;
            switch(h.Biome)
            {
                case Biome.Savanna:key="savanna";break;
                case Biome.TropicalRainforest:case Biome.TropicalSeasonalForest:key="rainforest";break;
                case Biome.TemperateForest:case Biome.TemperateRainforest:case Biome.MediterraneanWoodland:key="temperate";break;
                case Biome.BorealForest:key="boreal";break;
                case Biome.Grassland:key="grassland";break;
                case Biome.Desert:key="arid";break;
                case Biome.Floodplain:case Biome.Marsh:case Biome.Freshwater:key="wetland";break;
                case Biome.Peatland:key=h.MeanTenthsC<60?"boreal":"wetland";break;
                case Biome.Tundra:case Biome.Alpine:key="alpine";break;
                default:return null; // No misleading freshwater picture for saline/tidal/ocean habitats.
            }
            string F(double v)=>v.ToString("0.#",CultureInfo.InvariantCulture);
            string biome=System.Text.RegularExpressions.Regex.Replace(h.Biome.ToString(),"([a-z])([A-Z])","$1 $2");
            var portrait=new HabitatPortrait{Key=key,Title=biome,Conditions=new[]{
                new AtlasFact{Label="Growing season",Value=h.GrowingDays+" days"},
                new AtlasFact{Label="Seasonal temperatures",Value=F(h.ColdTenthsC/10.0)+" to "+F(h.WarmTenthsC/10.0)+" °C"},
                new AtlasFact{Label="Flood exposure",Value=F(h.FloodFractionPermille/10.0)+"% of cell · "+h.FloodDaysPerYear+" days/year"}}};
            int region=world.Ecology.Region[cell];if(region<0)return portrait;
            portrait.Fauna=world.Ecology.Regions[region].Species
                .Where(p=>!world.Ecology.Taxa[p.TaxonId].Producer&&!world.Ecology.Taxa[p.TaxonId].BasalResource)
                .Where(p=>Biogeography.Suitable(world.Ecology.Taxa[p.TaxonId],h,world.ElevationM[cell]))
                .OrderByDescending(p=>p.Variant?.MassKg??world.Ecology.Taxa[p.TaxonId].MassKg).ThenBy(p=>p.TaxonId).Take(6)
                .Select(p=>{var t=world.Ecology.Taxa[p.TaxonId];double mass=p.Variant?.MassKg??t.MassKg;
                    return new HabitatFaunaCard{Name=t.Name,Role=Role(t),BodyMass=mass<=0?"Mass not estimated":mass>=1?F(mass)+" kg":mass>=.001?F(mass*1000)+" g":F(mass*1000000)+" mg",Variant=p.Variant?.Name??""};}).ToArray();
            return portrait;
        }
        static string Role(Taxon t)
        {
            bool animal=(t.Eats&(Food.Vertebrate|Food.Invertebrate|Food.Fish))!=0;
            bool plant=(t.Eats&(Food.Grass|Food.Browse|Food.Fruit|Food.Seed|Food.Nectar))!=0;
            return animal&&plant?"Mixed feeder":animal?"Predator":(t.Eats&Food.Detritus)!=0?"Detritivore":plant?"Plant feeder":"Food-web member";
        }
    }
}
