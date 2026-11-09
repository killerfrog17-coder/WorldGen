using System;
using System.Linq;
using System.Text;

namespace WorldGen.Core
{
    public static class NpcLocalKnowledge
    {
        public static string Daypart(double hour,double daylight)
        {
            hour=(hour%24+24)%24;double dawn=12-daylight*.5,dusk=12+daylight*.5;
            return Math.Abs(hour-dawn)<.65?"around sunrise":Math.Abs(hour-dusk)<.65?"around sunset":hour<dawn||hour>dusk?"night":hour<10.5?"morning":hour<13.5?"around midday":"afternoon";
        }
        public static string Weather(WeatherFrame w)
        {
            double speed=Math.Sqrt(w.WindEastMps*w.WindEastMps+w.WindNorthMps*w.WindNorthMps);
            string temperature=w.TemperatureC< -15?"bitterly cold":w.TemperatureC<0?"freezing":w.TemperatureC<10?"chilly":w.TemperatureC<22?"mild":w.TemperatureC<30?"warm":w.TemperatureC<37?"hot":"scorching hot";
            string wet=w.SnowWaterMmPerHour>.15?"snowing":w.RainMmPerHour>4?"heavy rain":w.RainMmPerHour>.15?"raining":w.CloudCover>.75?"overcast":w.CloudCover>.35?"partly cloudy":"mostly clear";
            return temperature+", "+wet+", "+(speed<.7?"almost still air":speed<3?"a light breeze":speed<7?"a steady wind":"strong wind")+(w.Thunderstorm>.4?", thunder nearby":"")+(w.Fog>.3?", fog limits sight":"");
        }
        public static string Terrain(Biome biome)=>biome==Biome.BorealForest?"cold coniferous forest":biome==Biome.TemperateRainforest?"wet temperate forest":biome==Biome.TropicalRainforest?"tropical rainforest":biome==Biome.TropicalSeasonalForest?"seasonally dry tropical forest":biome==Biome.MediterraneanWoodland?"dry summer woodland":biome==Biome.TemperateForest?"temperate forest":biome==Biome.Peatland?"peat bogs":biome==Biome.SaltMarsh?"tidal salt marsh":biome==Biome.SalineLake?"salt-lake country":biome==Biome.Freshwater?"freshwater margins":biome.ToString().ToLowerInvariant();
        public static string Food(int kind,Biome biome)
            =>kind==1?"small game":kind==2?"large game":kind==3?"freshwater fish and water-side foods":kind==4?"shellfish, fish and other coastal foods":biome==Biome.Desert?"seasonal seeds, edible roots and other desert plants":biome==Biome.Tundra||biome==Biome.Alpine?"seasonal berries, roots and other edible plants":"edible roots, nuts, fruit, seeds and other seasonal plants";
        public static string Environment(Biome biome,ForagerDistrict district,ForagerCulture culture,ForagerBiotaDistrict biota,Taxon[] taxa)
        {
            var food=Enumerable.Range(0,5).OrderByDescending(i=>district.Food[i]*(.6+.4*culture.FoodPreference[i])*(biota?.FoodAvailability?.Length>i?biota.FoodAvailability[i]:1)).Take(3).Where(i=>district.Food[i]>.03).ToArray();
            var text=new StringBuilder("Home environment: "+Terrain(biome)+". Ordinary regional food sources: "+string.Join("; ",food.Select(i=>Food(i,biome)))+". Food is seasonal and must actually be gathered or obtained; this list is not food currently in your hands.");
            if(biota!=null&&taxa!=null)
            {
                var game=Enumerable.Range(0,biota.Taxa.Length).Where(i=>biota.Abundance[i]>.001&&biota.Taxa[i]>=0&&biota.Taxa[i]<taxa.Length)
                    .Select(i=>(taxon:taxa[biota.Taxa[i]],weight:biota.Abundance[i])).Where(t=>!t.taxon.Producer&&!t.taxon.BasalResource&&t.taxon.MassKg>.2&&(t.taxon.Provides&WorldGen.Core.Food.Vertebrate)!=0)
                    .OrderByDescending(t=>t.weight*((t.taxon.Eats&WorldGen.Core.Food.Vertebrate)!=0?.12:1)).Take(5).Select(t=>t.taxon.Name).ToArray();
                if(game.Length>0)text.Append(" Familiar regional animals include ").Append(string.Join(", ",game)).Append("; these are regional knowledge, not a claim that you can see them now.");
            }
            text.Append(district.Wood<.18?" Wood is scarce here and useful poles are valuable.":district.Canopy>.55?" Trees supply wood and useful plants, but dense cover can conceal danger.":" Useful wood and plants vary around the camp.");
            return text.ToString();
        }
        public static string Beliefs(ForagerBelief b,Biome biome,double devotion)
        {
            var text=new StringBuilder("Belief tradition: "+b.Name+". ");
            if(b.Animacy>.6)text.Append("Animals and places can have intentions or spiritual significance. ");
            if(b.AncestorPresence>.6)text.Append("Ancestors are considered present in living people's affairs. ");
            if(b.Afterlife>.6)text.Append("The dead continue in another existence. ");
            text.Append("Important places: ").Append(b.SacredLandscape).Append(". Customary rite: ").Append(b.SeasonalRite).Append(". Treatment of the dead: ").Append(b.MortuaryPractice).Append('.');
            if(b.ReciprocityWithGame>.65)text.Append(" Taking game carries obligations of respect and sharing.");
            if(b.RitualSpecialists>.6)text.Append(" Experienced ritual/healing specialists have influence.");
            if(b.FoodTaboos?.Length>0)text.Append(" Food customs restrict ").Append(string.Join(", ",b.FoodTaboos.Where(i=>i>=0&&i<5).Select(i=>Food(i,biome)))).Append(" in some social or ritual situations.");
            text.Append(devotion>.7?" You take these customs seriously.":devotion<.25?" You are more practically minded than devout, even though you know these customs.":" These customs are familiar parts of life; you need not mention them in every conversation.");
            return text.ToString();
        }
    }
}
