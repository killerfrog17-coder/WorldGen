using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public sealed class EcologyArticle
    {public string Title="",Category="",Body="";}
    public sealed class EcologyDossier
    {public int Cell,Region=-1,FoodLinks;public string Title="",Overview="";public EcologyArticle[] Articles=Array.Empty<EcologyArticle>();public FoodWebDiagram Diagram=new FoodWebDiagram();}
    /// <summary>Read-only wiki projection of existing generated metadata, not a
    /// new ecology rule, inferred species inventory or added feeding network.</summary>
    public static class EcologyAtlas
    {
        public static EcologyDossier Build(World world,int cell,CancellationToken token=default)
        {
            var d=new EcologyDossier{Cell=cell,Title="Ecology · no regional data",Overview="This world/location has no recorded terrestrial ecoregion."};
            var e=world.Ecology;if(e==null||cell<0||cell>=e.Region.Length)return d;
            int region=e.Region[cell];if(region<0||region>=e.Regions.Length){d.Overview=EcologyView.Summary(world,cell);return d;}
            var r=e.Regions[region];d.Region=region;d.Title=r.Name;d.FoodLinks=r.FoodWeb.Length;
            d.Diagram=FoodWebDiagram.Build(world,cell);
            d.Overview=EcologyView.Summary(world,cell)+"\n\nREGION RECORD\nID "+r.Id+" · province "+r.ProvinceId+" · representative cell "+r.RepresentativeCell+"\nArea "+(r.AreaM2/1e6).ToString("N2",CultureInfo.InvariantCulture)+" km² · "+r.Cells.Length+" source cells · "+r.PatchCount+" patches\nNeighbor regions: "+string.Join(", ",r.Neighbors)+"\n\nLOCAL HABITAT RECORD\n"+Known(e.Habitat[cell],token)+
                "\n\nRecorded taxa and feeding opportunities are distinct from derived procedural plant and small-fauna candidates. Neither is a live population census. Potential feeding links are not measured diets or energy fluxes. Fictional plant hazards are relative indices, not doses, food safety or animal-specific tolerance. Unmodelled information is not fabricated.";
            var articles=new List<EcologyArticle>();
            string Name(int id)=>id>=0&&id<e.Taxa.Length?e.Taxa[id].Name:"Unknown taxon #"+id;
            var byConsumer=r.FoodWeb.GroupBy(l=>l.ConsumerId).ToDictionary(g=>g.Key,g=>g.ToArray());
            var byResource=r.FoodWeb.GroupBy(l=>l.ResourceId).ToDictionary(g=>g.Key,g=>g.ToArray());
            foreach(var presence in r.Species)
            {
                token.ThrowIfCancellationRequested();if(presence.TaxonId<0||presence.TaxonId>=e.Taxa.Length)continue;var t=e.Taxa[presence.TaxonId];
                var b=new StringBuilder(t.Name+"\n"+t.ScientificName+"\n\nRecorded regional "+t.Rank+". "+(Biogeography.Present(e,cell,t.Id)?"Suitable and accessible in this source cell.":"Recorded elsewhere in this ecoregion; not confirmed in this source cell.")+"\n\nTAXON\n"+Known(t,token)+"\nREGIONAL PRESENCE / VARIANT\n"+Known(presence,token));
                if(byConsumer.TryGetValue(t.Id,out var diet)){b.Append("\nFEEDING OPPORTUNITIES\n");foreach(var link in diet)b.Append(Name(link.ResourceId)).Append(" → ").Append(t.Name).Append(" · ").Append(link.Resource).Append(" · ").Append((link.OverlapPermille*.1).ToString("0.#",CultureInfo.InvariantCulture)).Append("% regional overlap\n");}
                if(byResource.TryGetValue(t.Id,out var consumers)){b.Append("\nPOTENTIAL CONSUMERS\n");foreach(var link in consumers)b.Append(Name(link.ConsumerId)).Append(" · ").Append(link.Resource).Append('\n');}
                if(t.ScientificName=="Anura habitat guild"||t.ScientificName=="Understory bird guild")
                {
                    var kind=t.ScientificName=="Anura habitat guild"?FaunaSoundKind.Frog:FaunaSoundKind.UnderstoryBird;
                    b.Append("\nCURRENT LOCAL REPRESENTATION\nAudio only in this pass, not permanently excluded from future physical animals. Calling sites require recorded native cell presence and suitable local habitat. No spawned agents or animal population simulation.\n");
                    if(FaunaAmbience.For(world).TryProfile(cell,kind,out var voice))b.Append("\nREGIONAL CALLING FORM\n").Append(Known(voice,token));
                }
                articles.Add(new EcologyArticle{Title=t.Name,Category=t.BasalResource?"Basal resources":t.Producer?"Recorded flora":"Fauna",Body=b.ToString()});
            }
            var flora=GroundFlora.For(world).Region(region);
            void Plant(string title,string category,object record)
            {token.ThrowIfCancellationRequested();articles.Add(new EcologyArticle{Title=title,Category=category,Body=title+"\n\nDerived regional candidate: habitat/soil/noise determines local occurrence, not guaranteed by catalogue membership. This lineage is not yet linked into the recorded food web.\n\nGENERATED TRAITS\n"+Known(record,token)+"\nStatic anatomy/niche/reproduction potential only. No dynamic growth/fruiting, harvesting, poison effects or implied safe use."});}
            foreach(var s in flora.Species)Plant(s.Name,"Ground cover",s);
            foreach(var s in flora.Specialists)Plant(s.Name,s.Form==SpecialistForm.Fungus?"Fungi":"Flowers & herbs",s);
            foreach(var s in flora.MidPlants)Plant(s.Name,"Mid-height plants",s);
            foreach(var s in flora.Trees)Plant(s.Name,"Trees",s);
            foreach(var s in AmbientFauna.For(world).Region(region))
            {
                token.ThrowIfCancellationRequested();
                double localSupport=AmbientFauna.HabitatSuitability(s,e.Habitat[cell]);
                string title=s.Family+" - "+s.Name;
                articles.Add(new EcologyArticle{Title=title,Category="Derived small fauna",Body=title+
                    "\n\nDerived regional candidate from one broad parent lineage, not a named Earth species or a verified local population.\n"+
                    "Current cell abiotic potential: "+(localSupport*100).ToString("0.#",CultureInfo.InvariantCulture)+"% (suitability, NOT abundance or occupancy probability).\n"+
                    "\nGENERATED PROFILE\n"+Known(s,token)+
                    (s.Representation==AmbientFaunaRepresentation.AudioOnly?
                        "\nAUDIO ONLY: no visible animals or full individual agents. Habitat-based spatial callers are implemented; activity depends on local conditions, season, solar time and weather. A finite clip bank with bounded species pitch/timing variation approximates the profile, not a full synthesis of every acoustic trait.":
                        "\nLIGHTWEIGHT VISUAL: silent flashing fireflies spawn in suitable local habitat during the regional display season, dusk/night hours and warm, moist, calm weather. Regional traits control flash bouts, colour and flight height; visible range is 220 m.")+
                    "\nNo source taxa, saved food links, native-range raster or historical simulation changed. Profiles constrain local audio/display; they are not tracked breeding populations or dynamic evolution."});
            }
            foreach(var pair in byConsumer.OrderBy(p=>p.Key))
            {
                var b=new StringBuilder("FOOD WEB · "+Name(pair.Key)+"\n\nResource → consumer · resource class · regional co-occurrence\n\n");
                foreach(var link in pair.Value){token.ThrowIfCancellationRequested();b.Append(Name(link.ResourceId)).Append(" → ").Append(Name(link.ConsumerId)).Append("\n").Append(link.Resource).Append(" · ").Append((link.OverlapPermille*.1).ToString("0.#",CultureInfo.InvariantCulture)).Append("%\n\n");}
                articles.Add(new EcologyArticle{Category="Food web",Title=Name(pair.Key)+" · "+pair.Value.Length+" links",Body=b+"All recorded links for this consumer, not a sampled subset. Potential feeding, not a population/energy simulation."});
            }
            d.Articles=articles.OrderBy(a=>a.Category,StringComparer.Ordinal).ThenBy(a=>a.Title,StringComparer.Ordinal).ToArray();return d;
        }
        static string Known(object? record,CancellationToken token)
        {
            var b=new StringBuilder();
            void Dump(string name,object? value,int depth)
            {
                token.ThrowIfCancellationRequested();string indent=new string(' ',depth*2),label=Regex.Replace(name,"([a-z])([A-Z])","$1 $2");
                if(value==null){b.Append(indent).Append(label).Append(": not generated / not recorded\n");return;}
                var type=value.GetType();
                if(type.IsPrimitive||type.IsEnum||value is string||value is decimal||value is V)
                {
                    string text=value is double number?number.ToString("0.#####",CultureInfo.InvariantCulture):value is float single?single.ToString("0.#####",CultureInfo.InvariantCulture):value.ToString()??"";
                    if(value is V vector)text="("+vector.X.ToString("0.###",CultureInfo.InvariantCulture)+", "+vector.Y.ToString("0.###",CultureInfo.InvariantCulture)+", "+vector.Z.ToString("0.###",CultureInfo.InvariantCulture)+")";
                    if(value is int i&&name.EndsWith("TenthsC",StringComparison.Ordinal))text=(i*.1).ToString("0.#",CultureInfo.InvariantCulture)+" °C";
                    if(name.EndsWith("Permille",StringComparison.Ordinal))text+=" ‰";
                    if(value is double||value is float||value is long||value is int)
                    {
                        if(name.EndsWith("KgM3",StringComparison.Ordinal))text+=" kg/m³";
                        else if(name.EndsWith("Kg",StringComparison.Ordinal))text+=" kg";
                        else if(name.EndsWith("M2",StringComparison.Ordinal))text+=" m²";
                        else if(name.EndsWith("Mm",StringComparison.Ordinal))text+=" mm";
                        else if(name.EndsWith("Cm",StringComparison.Ordinal))text+=" cm";
                        else if(name.EndsWith("M",StringComparison.Ordinal))text+=" m";
                        else if(name.EndsWith("C",StringComparison.Ordinal)&&!name.EndsWith("TenthsC",StringComparison.Ordinal))text+=" °C";
                        else if(name.EndsWith("Days",StringComparison.Ordinal))text+=" days";
                        else if(name.EndsWith("Years",StringComparison.Ordinal))text+=" years";
                        else if(name.IndexOf("Toxicity",StringComparison.Ordinal)>=0||name=="ContactIrritation")text+=" (relative 0–1 index)";
                    }
                    b.Append(indent).Append(label).Append(": ").Append(text.Length==0?"(empty)":text).Append('\n');return;
                }
                b.Append(indent).Append(label).Append(":\n");
                if(value is IEnumerable sequence){int i=0;foreach(var item in sequence)Dump("#"+i++,item,depth+1);return;}
                foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.Name,StringComparer.Ordinal))Dump(field.Name,field.GetValue(value),depth+1);
                foreach(var property in type.GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.CanRead&&p.GetIndexParameters().Length==0).OrderBy(p=>p.Name,StringComparer.Ordinal))Dump(property.Name,property.GetValue(value),depth+1);
            }
            if(record!=null)Dump(record.GetType().Name,record,0);return b.ToString();
        }
    }
}
