using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static partial class HistoryAtlas
    {
        static string SwatchHex(V color)=>"#"+((int)Math.Round(color.X*255)).ToString("X2",CultureInfo.InvariantCulture)+((int)Math.Round(color.Y*255)).ToString("X2",CultureInfo.InvariantCulture)+((int)Math.Round(color.Z*255)).ToString("X2",CultureInfo.InvariantCulture);
        static void LivingWorldSections(PrehistoryState s,string kind,int id,int cell,World? world,Prehistory? engine,List<AtlasSection> sections)
        {
            if(world?.Ecology==null||s.History.Version!=Prehistory.LivingWorldHistoryVersion||cell<0||cell>=world.Surface.Cells.Length)return;
            if(!world.Hydrology.OceanConnected[cell])
            {
                var resource=HistoricalNaturalResources.At(world,cell);
                sections.Add(new AtlasSection{Title="Local resource potential",Bars=true,Unit="/ 1,000",Text="Relative potential near this recorded location, not proved reserves, stockpiles or output. Extracting and moving materials still requires workers, knowledge, infrastructure and actual trade links."+(resource.SyntheticEarthGeology?" Earth's relief is measured, but this mineral geography is synthetic—not a real-world deposit map.":""),
                    Links=Enumerable.Range(0,HistoricalNaturalResources.Count).Select(k=>new AtlasLink{Name=HistoricalNaturalResources.Name((HistoricalNaturalResource)k),Value=resource[(HistoricalNaturalResource)k],Detail=resource[(HistoricalNaturalResource)k]>=500?"Strong local potential":resource[(HistoricalNaturalResource)k]>=200?"Moderate local potential":"Limited local potential"}).ToArray()});
                int province=world.Ecology.Province[cell];var anomaly=HistoricalClimateVariability.At(world.Recipe.Seed,province,s.Years/10*10);
                double temperature=anomaly.TemperatureTenthsC/10.0;int rain=anomaly.PrecipitationPermille;
                sections.Add(new AtlasSection{Title="Climate and livelihoods",Text="Historical wet/dry and temperature fluctuations act on food support and settlement security around the generated map's long-term climate. They do not repaint the saved climate or create a new coastline.",Facts=new[]{
                    F("Recent temperature",temperature.ToString("+0.#;-0.#;0",CultureInfo.InvariantCulture)+" °C relative to the map baseline"),
                    F("Recent precipitation",Math.Abs(rain-1000)/10.0+"% "+(rain<1000?"drier":rain>1000?"wetter":"different")+" than the map baseline"),
                    F("Local vulnerability",world.Ecology.Habitat[cell].AridityMilli<500?"Dryland harvests are exposed to rainfall failure; reliable river supply can buffer it.":world.Ecology.Habitat[cell].MeanTenthsC<100?"Cold growing seasons are sensitive to temperature; warming can help before other limits dominate.":"Seasonal crops, excessive wetness and food transport affect provisioning.")}});
            }
            if(engine?.World!=world)return;
            if(kind=="state")
            {
                var politics=engine.PoliticalProfile(s,id);
                if(politics!=null)
                {
                    sections.Add(new AtlasSection{Title="Political interests",Bars=true,Unit="/ 1,000",Text=politics.Form+". "+politics.Participation+" These are modeled sources of political influence, not measured party memberships or votes.",
                        Facts=new[]{F("Transfer of power",politics.Succession)},Links=politics.Blocs.Select((name,k)=>new AtlasLink{Name=name,Value=politics.InfluencePermille[k],Detail=(politics.InfluencePermille[k]/10.0).ToString("0.#",CultureInfo.InvariantCulture)+"% of the modeled influence mix"}).ToArray()});
                    sections.Add(new AtlasSection{Title="Public priorities",Bars=true,Unit="/ 1,000",Text="The government divides a finite funded program among competing priorities. These shares express allocation within that program—not a full GDP estimate. An empty treasury cannot fund it.",Links=new[]{
                        new AtlasLink{Name="Defense",Value=politics.DefensePermille},new AtlasLink{Name="Civil works",Value=politics.WorksPermille},new AtlasLink{Name="Relief",Value=politics.ReliefPermille},new AtlasLink{Name="Learning",Value=politics.LearningPermille}}});
                }
            }
            HumanAppearance? appearance=null;
            if(kind=="people")appearance=engine.CultureAppearance(s,id);
            else if(kind=="city"||kind=="province")appearance=engine.RegionalAppearance(s,engine.CellPatch[cell]);
            if(appearance?.Available!=true)return;
            string pigment(int value)=>value<2500?"Light pigmentation":value<6000?"Intermediate pigmentation":"Dark pigmentation";
            sections.Add(new AtlasSection{Title="People and appearance",Text=appearance.Basis+" Pigmentation responds slowly to UV exposure, while inheritance, migration and drift preserve variation. Appearance has no effect on intelligence, political capacity or technology.",Facts=new[]{
                new AtlasFact{Label="Skin",Value=pigment(appearance.SkinPigmentation),Swatch=SwatchHex(appearance.SkinColor)},
                new AtlasFact{Label="Hair",Value=pigment(appearance.HairPigmentation),Swatch=SwatchHex(appearance.HairColor)},
                new AtlasFact{Label="Iris",Value=pigment(appearance.IrisPigmentation),Swatch=SwatchHex(appearance.IrisColor)},
                F("Hair form",appearance.HairCurl<3000?"Mostly straighter forms":appearance.HairCurl>7000?"Mostly tighter curls":"Mixed wavy and curly forms"),
                F("Body-form tendency",appearance.BodyCompactness<3500?"Relatively slender":appearance.BodyCompactness>6500?"Relatively compact":"Intermediate build")}});
        }
    }
}
