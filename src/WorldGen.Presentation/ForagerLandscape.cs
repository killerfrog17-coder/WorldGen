using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static class ForagerLandscape
    {
        public static bool Affected(Layer layer)=>layer==Layer.Climate||layer==Layer.Satellite||layer==Layer.Ecoregions||layer==Layer.Biomes||layer==Layer.Aridity||layer==Layer.Permafrost||layer==Layer.FirePotential||layer==Layer.TreeDensity||Quarter(layer)>=0;
        static int Quarter(Layer layer)=>layer==Layer.TemperatureSpring||layer==Layer.PrecipitationSpring?0:layer==Layer.TemperatureSummer||layer==Layer.PrecipitationSummer?1:layer==Layer.TemperatureAutumn||layer==Layer.PrecipitationAutumn?2:layer==Layer.TemperatureWinter||layer==Layer.PrecipitationWinter?3:-1;
        public static bool TryColor(World w,PrehistoryState state,int cell,Layer layer,out V color)
        {
            color=new V();if(state?.Foragers==null||!Affected(layer)||w.Hydrology.OceanConnected[cell])return false;
            var local=ForagerHabitat.For(w,state).Cell(cell);if(local==null)return false;
            if(GlobeView.TryVisibleWater(w,cell,out _)){color=new V(.055,.34,.68);return true;}
            int q=Quarter(layer);
            if(q>=0){color=layer==Layer.TemperatureSpring||layer==Layer.TemperatureSummer||layer==Layer.TemperatureAutumn||layer==Layer.TemperatureWinter?ClimateColors.Temperature(D.M(local.Weather.TemperatureC[q]*10)):ClimateColors.Rain(D.M(local.Weather.MonthlyPrecipitationMm[q]*3));return true;}
            switch(layer)
            {
                case Layer.Climate:color=ClimateColors.Classification(local.Classification);break;
                case Layer.Satellite:color=ClimateColors.Satellite(local.Classification,w.ElevationM[cell],local.Habitat.WarmTenthsC/10d);break;
                case Layer.TreeDensity:color=new V(.75,.66,.42)*(1-local.ForestCover)+new V(.06,.3,.12)*local.ForestCover;break;
                default:color=EcologyView.Color(w,cell,layer,local.Habitat);break;
            }
            return true;
        }
    }
}
