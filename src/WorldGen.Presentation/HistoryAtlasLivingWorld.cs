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
            if(world?.Ecology==null||s.History.Version!=Prehistory.LivingWorldHistoryVersion||cell<0||cell>=world.Surface.Cells.Length||cell>=world.Hydrology.OceanConnected.Length||cell>=world.Ecology.Habitat.Length||cell>=world.Ecology.Province.Length)return;
            if(!world.Hydrology.OceanConnected[cell])
            {
                var resource=HistoricalNaturalResources.At(world,cell);
                int localTown=s.History.Aggregates?.TownAt[cell]??-1;string near=localTown>=0&&localTown<s.Society.Settlements.Length?"Near "+s.Society.Settlements[localTown].Name+". ":"Near the recorded location. ";
                sections.Add(new AtlasSection{Title="Local resource potential",Bars=true,CompactGrid=true,Unit="/ 1,000",Text=near+"Potential, not reserves or output. Extraction and freight require workers, skills, infrastructure and actual connections."+(resource.SyntheticEarthGeology?" Earth's mineral geography here is synthetic, not measured deposits.":""),
                    Links=Enumerable.Range(0,HistoricalNaturalResources.Count).Select(k=>new AtlasLink{Name=HistoricalNaturalResources.Name((HistoricalNaturalResource)k),Value=resource[(HistoricalNaturalResource)k],Detail=resource[(HistoricalNaturalResource)k]>=500?"Strong local potential":resource[(HistoricalNaturalResource)k]>=200?"Moderate local potential":"Limited local potential"}).ToArray()});
                int province=world.Ecology.Province[cell];var anomaly=HistoricalClimateVariability.At(world.Recipe.Seed,province,s.Years/10*10);
                double temperature=anomaly.TemperatureTenthsC/10.0;int rain=anomaly.PrecipitationPermille;
                sections.Add(new AtlasSection{Title="Climate and livelihoods",Text="Historical wet/dry and temperature fluctuations act on food support and settlement security around the generated map's long-term climate. They do not repaint the saved climate or create a new coastline.",Facts=new[]{
                    F("Recent temperature",temperature.ToString("+0.#;-0.#;0",CultureInfo.InvariantCulture)+" °C relative to the map baseline"),
                    F("Recent precipitation",rain==1000?"At the map baseline":(Math.Abs(rain-1000)/10.0).ToString("0.#",CultureInfo.InvariantCulture)+"% "+(rain<1000?"drier":"wetter")+" than the map baseline"),
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
                    int[] shares={politics.DefensePermille,politics.WorksPermille,politics.ReliefPermille,politics.LearningPermille};string[] priorities={"Defense","Civil works","Relief","Learning"};int leading=Array.IndexOf(shares,shares.Max());
                    sections.Add(new AtlasSection{Title="Public priorities",Bars=true,Unit="/ 1,000",Text=priorities[leading]+" receives the largest share of the government's funded program. Other priorities compete for the same treasury; these are program shares, not percentages of the whole economy.",Links=new[]{
                        new AtlasLink{Name="Defense",Value=politics.DefensePermille,Detail=(politics.DefensePermille/10.0).ToString("0.#",CultureInfo.InvariantCulture)+"% of the funded program"},new AtlasLink{Name="Civil works",Value=politics.WorksPermille,Detail=(politics.WorksPermille/10.0).ToString("0.#",CultureInfo.InvariantCulture)+"% of the funded program"},new AtlasLink{Name="Relief",Value=politics.ReliefPermille,Detail=(politics.ReliefPermille/10.0).ToString("0.#",CultureInfo.InvariantCulture)+"% of the funded program"},new AtlasLink{Name="Learning",Value=politics.LearningPermille,Detail=(politics.LearningPermille/10.0).ToString("0.#",CultureInfo.InvariantCulture)+"% of the funded program"}}});
                }
                var international=engine.InternationalProfile(s,id);
                if(international.Available&&(international.Organizations.Length>0||international.CoalitionEntries>0))
                {
                    sections.Add(new AtlasSection{Title="Diplomatic institutions",Text="These organizations arise from existing diplomatic contacts and paid contributions. Their charters can ease trade or support uncertain mediation; they do not erase wars, borders or transport costs.",Facts=new[]{F("Known relationships",international.KnownRelations.ToString("N0",CultureInfo.InvariantCulture)),F("Recorded coalition entries",international.CoalitionEntries.ToString("N0",CultureInfo.InvariantCulture))},
                        Links=international.Organizations.Select(o=>new AtlasLink{Kind="organization",Id=o.Id,Name=o.Name,Detail=o.Purpose+" · "+o.Members.ToString("N0",CultureInfo.InvariantCulture)+" members · "+o.Standing}).ToArray()});
                }
                var nuclear=engine.NuclearProfile(s,id);
                if(nuclear.Available&&(nuclear.ProgramYear>=0||nuclear.FirstWeaponYear>=0||nuclear.Stockpile>0))
                {
                    sections.Add(new AtlasSection{Title="Nuclear capabilities",Text=nuclear.Status+". "+nuclear.Delivery+". Stockpile units are an abstract strategic measure, not calibrated real-world warhead counts. Knowledge alone does not create a weapon.",Facts=new[]{
                        F("Weapons in custody",nuclear.Stockpile.ToString("N0",CultureInfo.InvariantCulture)),F("Operational readiness",(nuclear.ReadinessMilli/10.0).ToString("0.#",CultureInfo.InvariantCulture)+"%"),
                        F("Programme began",nuclear.ProgramYear<0?"Not recorded":Y(s,nuclear.ProgramYear)),F("First physical arsenal",nuclear.FirstWeaponYear<0?"Not yet recorded":Y(s,nuclear.FirstWeaponYear)),
                        F("Weapons used",nuclear.Used.ToString("N0",CultureInfo.InvariantCulture)),F("Recorded deterrence episodes",nuclear.DeterrenceEpisodes.ToString("N0",CultureInfo.InvariantCulture)),
                        F("Programme investment",nuclear.Investment.ToString("N0",CultureInfo.InvariantCulture)+" treasury units"),F("Maintenance paid",nuclear.UpkeepPaid.ToString("N0",CultureInfo.InvariantCulture)+" treasury units"),
                        F("Attributed civilian deaths",nuclear.InflictedCivilianDeaths.ToString("N0",CultureInfo.InvariantCulture))}});
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
