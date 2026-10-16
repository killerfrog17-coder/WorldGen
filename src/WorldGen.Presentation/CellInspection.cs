using System;
using System.Linq;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static class CellInspection
    {
        public static string Describe(World w,int i,Layer layer)
        {
            if(w==null || i<0 || i>=w.Surface.Cells.Length)return "Select a location";
            string Number(double v,string unit)=>v.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+unit;
            if(layer==Layer.Terrain || layer==Layer.Satellite)
            {
                var islands=SubcellGeography.Find(w,i);
                if(islands!=null)return islands.IslandCount+" islands · "+Number(islands.CoverageFraction*100,"% land")+" · largest "+Number(islands.LargestIslandAreaM2/1e6," km²");
                if(w.Hydrology.LakeCoverageFraction.Length>i && w.Hydrology.LakeCoverageFraction[i]>0)return Number(w.Hydrology.LakeCoverageFraction[i]*100,"% lake")+" · "+Number(w.Hydrology.LakeCoverageFraction[i]*w.Surface.Cells[i].AreaM2/1e6," km² water");
            }
            if(layer==Layer.Microplates)return "Plate "+(w.CellMicroplate[i]+1);
            if(layer==Layer.Continents)return w.Continent[i]<0?"Ocean":"Continent "+(w.Continent[i]+1);
            if(layer==Layer.Climate)return GlobeView.TryVisibleWater(w,i,out var climateWater)&&climateWater.Kind!=WaterKind.Ocean
                ? climateWater.Kind==WaterKind.TectonicInlandSea?"Inland sea":"Lake":w.Climate.Classification[i].ToString();
            if(layer==Layer.Ecoregions){if(w.Ecology==null)return "Unavailable in this saved world";int id=w.Ecology.Region[i];return id<0?"Ocean":w.Ecology.Regions[id].Name+" — "+w.Ecology.Regions[id].Biome;}
            if(EcologyView.IsLayer(layer))
            {
                if(w.Ecology==null)return "Unavailable in this saved world";var a=w.Ecology.Habitat[i];
                switch(layer){case Layer.Biomes:return a.Biome.ToString();case Layer.Soils:return a.Soil.ToString();case Layer.Aridity:return Number(a.AridityMilli/1000.0," P/PET");case Layer.Floodplains:return Number(a.FloodFractionPermille/10.0,"% floodplain");case Layer.WaterTable:return Number(a.WaterTableCm/100.0," m water-table depth");case Layer.Permafrost:return Number(a.PermafrostPermille/10.0,"% permafrost potential");case Layer.Salinity:return Number(a.SalinityMilliPpt/1000.0," ppt");case Layer.FirePotential:return Number(a.FirePotentialPermille/10.0,"% fire potential");}
            }
            var season=layer.ToString().EndsWith("Spring")?w.Climate.NorthernSpring:layer.ToString().EndsWith("Autumn")?w.Climate.NorthernAutumn:layer.ToString().EndsWith("Winter")?w.Climate.NorthernWinter:w.Climate.NorthernSummer;
            if(layer.ToString().StartsWith("Temperature"))return Number(season.TemperatureTenthsC[i]/10.0," °C");
            if(layer.ToString().StartsWith("Precipitation"))return Number(season.PrecipitationMm[i]," mm / season");
            if(layer.ToString().StartsWith("Wind"))return Number(Math.Sqrt((double)season.WindEastMilli[i]*season.WindEastMilli[i]+(double)season.WindNorthMilli[i]*season.WindNorthMilli[i])/1000," wind index");
            if(layer.ToString().StartsWith("OceanCurrents"))return Number(season.OceanWarmthMilli[i]/1000.0," current warmth index");
            switch(layer)
            {
                case Layer.Satellite:return w.Climate.Classification[i].ToString();
                case Layer.ParentPlates:return "Superplate "+(w.Microplates[w.CellMicroplate[i]].ParentAffinityId+1);
                case Layer.Crust:return w.Continent[i]<0?"Oceanic crust":"Continental crust";
                case Layer.OceanConnectivity:return w.Hydrology.OceanConnected[i]?"Ocean":"Land";
                case Layer.Stress:return Number(w.Stress[i]," stress index");
                case Layer.Mantle:return Number(w.MantlePermille[i]/1000.0," mantle index");
                case Layer.Continentality:return Number(w.Climate.ContinentalityPermille[i]/10.0,"% continentality");
                case Layer.Watersheds:return "Watershed "+(w.Hydrology.Watershed[i]+1);
                case Layer.ContributingArea:return Number(w.Hydrology.ContributingAreaM2[i]/1000000.0," km² contributing area");
                case Layer.Rivers:var r=w.Hydrology.Rivers.FirstOrDefault(x=>x.From==i);return r==null?"No mapped river":Number(r.WidthM," m river width");
                case Layer.WaterBodies:case Layer.LakesAndInlandSeas:int id=w.Hydrology.WaterBody[i];return id<0?"Land":w.Hydrology.Waters[id].Kind.ToString();
                case Layer.BoundaryType:var b=w.Boundaries.FirstOrDefault(x=>w.Surface.Edges[x.EdgeId].A==i||w.Surface.Edges[x.EdgeId].B==i);return b==null?"Plate interior":b.Kind.ToString();
                case Layer.TectonicElevation:return Number(w.TectonicM[i]," m");
                case Layer.PreErosionElevation:return Number(w.PreErosionM[i]," m");
                case Layer.ErosionDelta:return Number(w.ElevationM[i]-w.PreErosionM[i]," m elevation change");
                default:return Number(w.ElevationM[i]," m");
            }
        }
    }
}
