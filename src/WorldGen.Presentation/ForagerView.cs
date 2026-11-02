using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static class ForagerView
    {
        static AtlasFact F(string k,string v)=>new AtlasFact{Label=k,Value=v};
        static string Percent(double x)=>(x*100).ToString("0")+"%";
        static string Year(int year)=>year<0?(-year).ToString("N0")+" years before the starting year":"Year "+year.ToString("N0");
        public static string Age(PrehistoryState s)
        {var live=s.Foragers.Cohorts.Where(c=>c.Population>=1).ToArray();return live.Any(c=>c.Use[17]+c.Use[21]+c.Use[22]>1)?"Late Paleolithic traditions":"Middle Paleolithic traditions";}
        public static AtlasLink? Selection(PrehistoryState s,int cell,HistoryOverlay overlay)
        {
            var map=ForagerMaps.For(s);var c=map.Dominant(cell);if(c==null)return null;
            string kind=overlay==HistoryOverlay.Languages?"language":overlay==HistoryOverlay.Ethnicities?"ethnicity":overlay==HistoryOverlay.Religions?"religion":overlay==HistoryOverlay.Technology?"technology":overlay==HistoryOverlay.HumanSpecies?"human-species":overlay==HistoryOverlay.Cultures?"people":"";
            if(kind.Length==0)return null;int id=kind=="technology"?c.Culture:kind=="human-species"?c.Lineage:map.Id(cell,overlay);
            return new AtlasLink{Kind=kind,Id=id,Cell=cell,Name=kind=="technology"?s.Foragers.Cultures[c.Culture].Name+" toolkit":kind=="human-species"?s.Foragers.Lineages[c.Lineage].Name:map.Name(id,overlay)};
        }
        static AtlasLink[] Techniques(ForagerCohort c)=>ForagerTechnology.All.Select(t=>new AtlasLink{Name=t.Name,Id=t.Id,Value=(long)Math.Round(c.Skill[t.Id]*1000),Detail="Familiarity "+Percent(c.Knowledge[t.Id])+" · making skill "+Percent(c.Skill[t.Id])+" · routine use "+Percent(c.Use[t.Id])+"\nRequirements: "+ForagerTechnology.Requirements(t.Id)+(c.FirstKnownYear.Length>t.Id&&c.FirstKnownYear[t.Id]>=0?"\nLocal learning: "+(c.FirstKnownYear[t.Id]==0?"inherited at the starting year":Year(c.FirstKnownYear[t.Id])):"")}).ToArray();
        public static SelectionCard Card(PrehistoryState s,int cell,HistoryOverlay overlay,Prehistory? engine)
        {
            var f=s.Foragers;var map=ForagerMaps.For(s);double P(ForagerCohort p)=>map.Population(p);var local=map.Residents(cell);var c=local.FirstOrDefault();
            var card=new SelectionCard{Title="No resident community",Subtitle="Paleolithic"};
            if(overlay==HistoryOverlay.States||overlay==HistoryOverlay.Civilizations||overlay==HistoryOverlay.Territories||overlay==HistoryOverlay.Provinces||overlay==HistoryOverlay.ReligiousInstitutions)
            {card.Title="No "+(overlay==HistoryOverlay.ReligiousInstitutions?"formal religious institutions":"state administration");card.Note="Mobile communities use home ranges, seasonal camps and reciprocal access. These are not administrative territories.";return card;}
            if(c==null){card.Note="No surviving human population is recorded in this district.";return card;}
            var d=f.Districts[c.District];card.Title=map.Name(map.Id(cell,overlay),overlay);card.Subtitle=Year(s.Years)+" · "+f.Lineages[c.Lineage].Name;
            var facts=new List<AtlasFact>{F("District population",local.Sum(p=>P(p)).ToString("N0")),F("Occupied home-range area",(d.AreaKm2*local.Max(p=>p.Occupation)).ToString("N0")+" km²"),F("Local density",(local.Sum(p=>P(p))/d.AreaKm2*100).ToString("0.00")+" people / 100 km²")};
            if(overlay==HistoryOverlay.HumanSpecies)
            {facts.AddRange(local.Select(p=>F(f.Lineages[p.Lineage].Name,P(p).ToString("N0")+" residents · "+Percent(p.Admixture[p.Lineage])+" lineage ancestry")));card.Note="Coexisting lineages share local food opportunities; ancestry and cultural identity are separate.";}
            else if(overlay==HistoryOverlay.Technology)
            {card.Title=f.Cultures[c.Culture].Name+" toolkit";card.Bars=Techniques(c);card.Note="Map colours distinguish toolkit specialisations. Familiarity, making skill and actual use are different; climate, materials and teachers affect each.";}
            else if(overlay==HistoryOverlay.Languages)
            {
                var l=f.Languages[map.Id(cell,overlay)];facts.Add(F("Recognisable family",f.LanguageFamilies[l.Family].Name));facts.Add(F("Word order",l.Sounds.WordOrder));facts.Add(F("Bilingual contacts",Percent(c.BilingualShare)));
                if(c.MinorityLanguage>=0&&c.MinorityLanguageShare>.001)facts.Add(F("Minority speech",f.Languages[c.MinorityLanguage].Name+" · "+Percent(c.MinorityLanguageShare)));
                facts.Add(F("Example",ForagerLanguages.Utterance(l,c,"we","gather","food","tomorrow","food")));card.Note="Related dialects share inherited vocabulary. Old ancestry remains recorded when accumulated change makes a remote family unrecognisable.";
            }
            else if(overlay==HistoryOverlay.Religions)
            {var b=f.Beliefs[map.Id(cell,overlay)];facts.Add(F("Sacred places",b.SacredLandscape));facts.Add(F("Seasonal gathering",b.SeasonalRite));facts.Add(F("Mortuary custom",b.MortuaryPractice));if(c.MinorityBelief>=0&&c.MinorityBeliefShare>.001)facts.Add(F("Other local tradition",f.Beliefs[c.MinorityBelief].Name+" · "+Percent(c.MinorityBeliefShare)));card.Note="Local oral traditions, household practices and shared rites; no church or state religion is implied.";}
            else
            {facts.Add(F("Residence",Residence(f.Cultures[c.Culture].Residence)));facts.Add(F("Food security",Percent(c.FoodSecurity)));facts.Add(F("Winter protection",Percent(c.WinterSecurity)));facts.Add(F("Speech",f.Languages[c.Language].Name));facts.Add(F("Tradition",f.Beliefs[c.Belief].Name));card.Note="Seasonal camps and kin/exchange connections generate local bands and household candidates.";}
            if(engine!=null)
            {var e=ForagerEcology.At(engine.World.Recipe.Seed,d,f.Seconds/(double)SimulationTime.Year,engine.World.ElevationM[cell]);facts.Add(F("Winter / summer",e.WinterC.ToString("0.0")+" / "+e.SummerC.ToString("0.0")+" °C"));facts.Add(F("Perennial ice",Percent(e.IceCover)));}
            card.Facts=facts.ToArray();return card;
        }
        public static string Residence(ForagerResidence r)=>r==ForagerResidence.CollectorCamps?"Seasonal base camps and collecting parties":r==ForagerResidence.AquaticResidence?"Aquatic seasonal residence":r==ForagerResidence.SeasonalBaseCamps?"Returning seasonal base camps":"Mobile residential camps";
        public static string Describe(Prehistory engine,PrehistoryState s,int cell,HistoryOverlay overlay)
        {var c=Card(s,cell,overlay,engine);return c.Title+"\n"+c.Subtitle+"\n"+string.Join("\n",c.Facts.Select(f=>f.Label+": "+f.Value))+"\n\n"+c.Note;}
        public static AtlasLink[] Directory(PrehistoryState s,string kind,string search,bool past)
        {
            var f=s.Foragers;var map=ForagerMaps.For(s);double P(ForagerCohort c)=>map.Population(c);
            var active=f.Cohorts.Where(c=>P(c)>=1).ToArray();IEnumerable<AtlasLink> rows=Array.Empty<AtlasLink>();
            int Cell(int district)=>f.Districts[district].Cell;
            if(kind=="people"||kind=="technology")
            {
                var residents=active.GroupBy(c=>c.Culture).ToDictionary(g=>g.Key,g=>g.ToArray());
                rows=f.Cultures.Where(c=>past||residents.ContainsKey(c.Id)).Select(c=>new AtlasLink{Kind=kind,Id=c.Id,Cell=Cell(c.OriginDistrict),Name=c.Name+(kind=="technology"?" toolkit":""),Historical=!residents.ContainsKey(c.Id),Value=residents.TryGetValue(c.Id,out var local)?(long)Math.Round(local.Sum(P)):0});
            }
            if(kind=="ethnicity")
            {
                var residents=active.GroupBy(c=>c.Identity).ToDictionary(g=>g.Key,g=>g.Sum(P));
                rows=f.Peoples.Where(p=>past||residents.ContainsKey(p.Id)).Select(p=>new AtlasLink{Kind=kind,Id=p.Id,Cell=Cell(p.OriginDistrict),Name=p.Name,Historical=!residents.ContainsKey(p.Id),Value=residents.TryGetValue(p.Id,out double number)?(long)Math.Round(number):0});
            }
            if(kind=="language"||kind=="religion")
            {
                var residents=active.SelectMany(c=>(kind=="language"?c.Speech:c.Traditions).Select(x=>(x.Id,cohort:c,number:P(c)*x.Share))).GroupBy(x=>x.Id).ToDictionary(g=>g.Key,g=>(population:g.Sum(x=>x.number),owner:g.OrderByDescending(x=>x.number).First().cohort));
                bool Living(int id)=>residents.TryGetValue(id,out var local)&&local.population>=.5;
                AtlasLink Entry(int id,int district,string name,string detail)=>new AtlasLink{Kind=kind,Id=id,Cell=Cell(residents.TryGetValue(id,out var local)?local.owner.District:district),Name=name,Detail=detail,Historical=!Living(id),Value=residents.TryGetValue(id,out var people)?(long)Math.Round(people.population):0};
                rows=kind=="language"?f.Languages.Where(l=>past||Living(l.Id)).Select(l=>Entry(l.Id,l.OriginDistrict,l.Name,f.LanguageFamilies[l.Family].Name)):f.Beliefs.Where(b=>past||Living(b.Id)).Select(b=>Entry(b.Id,b.OriginDistrict,b.Name,b.SacredLandscape));
            }
            if(kind=="human-species")
            {
                var residents=active.GroupBy(c=>c.Lineage).ToDictionary(g=>g.Key,g=>g.ToArray());
                rows=f.Lineages.Where(l=>past||residents.ContainsKey(l.Id)).Select(l=>new AtlasLink{Kind=kind,Id=l.Id,Cell=Cell(residents.TryGetValue(l.Id,out var local)?local.OrderByDescending(P).First().District:l.OriginDistrict),Name=l.Name,Detail=l.Basis,Historical=!residents.ContainsKey(l.Id),Value=residents.TryGetValue(l.Id,out var people)?(long)Math.Round(people.Sum(P)):0});
            }
            if(kind=="language-family")
            {
                var liveFamilies=active.SelectMany(c=>c.Speech.Where(x=>x.Share*P(c)>=.1).Select(x=>f.Languages[x.Id].Family)).ToHashSet();
                rows=f.LanguageFamilies.Where(l=>past||liveFamilies.Contains(l.Id)).Select(l=>new AtlasLink{Kind=kind,Id=l.Id,Cell=Cell(l.OriginDistrict),Name=l.Name,Historical=!liveFamilies.Contains(l.Id)});
            }
            return rows.Where(r=>r.Name.IndexOf(search??"",StringComparison.OrdinalIgnoreCase)>=0).OrderByDescending(r=>r.Value).ThenBy(r=>r.Name).ToArray();
        }
        static AtlasLink Link(string kind,int id,int cell,string name)=>new AtlasLink{Kind=kind,Id=id,Cell=cell,Name=name};
        public static AtlasDocument Document(PrehistoryState s,string kind,int id,Prehistory? engine)
        {
            var f=s.Foragers;var map=ForagerMaps.For(s);double P(ForagerCohort p)=>map.Population(p);var live=f.Cohorts.Where(c=>P(c)>=1).ToArray();var doc=new AtlasDocument{Kind=kind,Id=id,Year=s.Years,EpochYear=-1,Title="Paleolithic world",Subtitle=Year(s.Years)+" · "+Age(s)};var sections=new List<AtlasSection>();
            if(kind=="world")
            {
                doc.Facts=new[]{F("All human lineages",live.Sum(c=>P(c)).ToString("N0")),F("Homo sapiens",live.Where(c=>f.Lineages[c.Lineage].PlayerSpecies).Sum(c=>P(c)).ToString("N0")),F("Resident districts",live.Select(c=>c.District).Distinct().Count().ToString()),F("Speech varieties / families",live.SelectMany(c=>c.Speech.Where(x=>x.Share*P(c)>=1).Select(x=>x.Id)).Distinct().Count()+" / "+live.SelectMany(c=>c.Speech.Where(x=>x.Share*P(c)>=1).Select(x=>f.Languages[x.Id].Family)).Distinct().Count()),F("Local traditions",live.SelectMany(c=>c.Traditions.Where(x=>x.Share*P(c)>=1).Select(x=>x.Id)).Distinct().Count().ToString()),F("States / farms","0")};
                sections.Add(new AtlasSection{Title="Human lineages",Links=Directory(s,"human-species","",false)});sections.Add(new AtlasSection{Title="People and local toolkits",Links=Directory(s,"people","",false)});sections.Add(new AtlasSection{Title="Recorded speech communities",Links=Directory(s,"language","",false)});sections.Add(new AtlasSection{Title="Local spiritual traditions",Links=Directory(s,"religion","",false)});doc.Sections=sections.ToArray();return doc;
            }
            var entry=Directory(s,kind,"",true).FirstOrDefault(r=>r.Id==id);if(entry==null){doc.Title="Record unavailable";return doc;}doc.Title=entry.Name;doc.Cell=entry.Cell;
            ForagerCohort? owner=kind=="people"||kind=="technology"?f.Cohorts.Where(c=>c.Culture==id).OrderByDescending(c=>P(c)).FirstOrDefault():ForagerMaps.For(s).Dominant(entry.Cell);
            if(kind=="human-species")
            {
                var l=f.Lineages[id];var people=live.Where(c=>c.Lineage==id).ToArray();doc.Introduction=l.Basis;
                doc.Facts=new[]{F("Population",people.Sum(c=>P(c)).ToString("N0")),F("Resident districts",people.Select(c=>c.District).Distinct().Count().ToString()),F("Ancestral dispersal",l.AncestralRoute.Length+" connected districts"),F("Body mass / stature",l.AdultMassKg.ToString("0")+" kg / "+l.StatureM.ToString("0.00")+" m"),F("Regional separation",l.SeparationYears.ToString("N0")+" years before the starting year")};
                sections.Add(new AtlasSection{Title="Regional descendants",Links=people.Select(c=>Link("people",c.Culture,f.Districts[c.District].Cell,f.Cultures[c.Culture].Name)).ToArray()});
            }
            else if(kind=="language")
            {
                var l=f.Languages[id];doc.Facts=new[]{F("Recognisable family",f.LanguageFamilies[l.Family].Name),F("Recorded speech branch",Year(l.FoundedYear)),F("Word order",l.Sounds.WordOrder),F("Transmission","Oral"),F("Narrative / technical register",Percent(l.NarrativePractice)+" / "+Percent(l.TechnicalRegister))};
                doc.Introduction="Vocabulary has inherited root identities, systematic sound changes, unequal replacement rates and contact loans. A recognisable language family is distinct from remote genealogical ancestry.";
                sections.Add(new AtlasSection{Title="Sounds and reusable roots",Facts=new[]{"water","fire","spear","mother","hunt","tomorrow","because","spirit"}.Select(c=>F(c,ForagerLanguages.Word(l,c).Text)).ToArray(),Text=ForagerLanguages.Sentence(l,"we","hunt","deer")});
                sections.Add(new AtlasSection{Title="Speech ancestry",Links=(l.Parent<0?Array.Empty<AtlasLink>():new[]{Link("language",l.Parent,f.Districts[f.Languages[l.Parent].OriginDistrict].Cell,f.Languages[l.Parent].Name)}).Concat(new[]{Link("language-family",l.Family,entry.Cell,f.LanguageFamilies[l.Family].Name)}).Concat(f.Languages.Where(x=>x.Parent==id).Select(x=>Link("language",x.Id,f.Districts[x.OriginDistrict].Cell,x.Name))).ToArray()});
            }
            else if(kind=="language-family")
            {var family=f.LanguageFamilies[id];doc.Facts=new[]{F("Recognisable branch",Year(family.FoundedYear)),F("Remote parent",family.Parent<0?"Earlier ancestry outside the recorded speech ledger":f.LanguageFamilies[family.Parent].Name)};sections.Add(new AtlasSection{Title="Recorded member languages",Links=f.Languages.Where(l=>l.Family==id).Select(l=>Link("language",l.Id,f.Districts[l.OriginDistrict].Cell,l.Name)).ToArray()});}
            else if(kind=="religion")
            {
                var b=f.Beliefs[id];doc.Introduction=b.SeasonalRite+". Sacred associations: "+b.SacredLandscape+" and "+b.SacredAnimal+".";
                doc.Facts=new[]{F("Recorded local branch",Year(b.FoundedYear)),F("Mortuary practice",b.MortuaryPractice),F("Sacred places",b.SacredLandscape),F("Parent tradition",b.Parent<0?"Ancestral local tradition":f.Beliefs[b.Parent].Name)};
                sections.Add(new AtlasSection{Title="Belief and practice",Facts=new[]{F("Animacy",Percent(b.Animacy)),F("Ancestor presence",Percent(b.AncestorPresence)),F("Afterlife",Percent(b.Afterlife)),F("Ritual specialists",Percent(b.RitualSpecialists)),F("Healing rites",Percent(b.HealingRites)),F("Collective rites",Percent(b.CollectiveRites)),F("Reciprocity with game",Percent(b.ReciprocityWithGame)),F("Creator narratives",Percent(b.CreatorNarrative))}});
            }
            else if(kind=="ethnicity")
            {var p=f.Peoples[id];doc.Introduction=p.HeritageStory;doc.Facts=new[]{F("Population",live.Where(c=>c.Identity==id).Sum(c=>P(c)).ToString("N0")),F("Recorded branch",Year(p.FoundedYear)),F("Parent heritage",p.Parent<0?"Ancestral regional identity":f.Peoples[p.Parent].Name)};}
            else if(owner!=null)
            {
                var c=owner;var culture=f.Cultures[c.Culture];var district=f.Districts[c.District];doc.Cell=district.Cell;
                doc.Facts=new[]{F("Residents",P(c).ToString("N0")),F("Residence",Residence(culture.Residence)),F("Food / winter security",Percent(c.FoodSecurity)+" / "+Percent(c.WinterSecurity)),F("Birth / death rates",(c.BirthRate*100).ToString("0.0")+"% reproductive-age basis / "+(c.AdultMortality*100).ToString("0.0")+"% young-adult yearly mortality"),F("Population ages",c.Children.ToString("N0")+" children · "+c.Adults.ToString("N0")+" adults · "+c.Elders.ToString("N0")+" elders")};
                sections.Add(new AtlasSection{Title="Local knowledge and use",Bars=true,Unit="% making skill",Links=Techniques(c).Select(t=>new AtlasLink{Name=t.Name,Value=t.Value/10,Detail=t.Detail}).ToArray()});
                sections.Add(new AtlasSection{Title="People, speech and tradition",Links=new[]{Link("human-species",c.Lineage,doc.Cell,f.Lineages[c.Lineage].Name),Link("ethnicity",c.Identity,doc.Cell,f.Peoples[c.Identity].Name),Link("language",c.Language,doc.Cell,f.Languages[c.Language].Name),Link("religion",c.Belief,doc.Cell,f.Beliefs[c.Belief].Name),Link("technology",c.Culture,doc.Cell,"Local toolkit")}});
                sections.Add(new AtlasSection{Title="Contacts and reciprocal access",Links=f.Exchanges.Where(e=>e.A==c.Id||e.B==c.Id).OrderByDescending(e=>e.Strength).Take(16).Select(e=>{var other=f.Cohorts.First(x=>x.Id==(e.A==c.Id?e.B:e.A));var link=Link("people",other.Culture,f.Districts[other.District].Cell,f.Cultures[other.Culture].Name);link.Detail="Contact "+Percent(e.Strength)+" · bilingual exchange "+Percent(e.Bilingualism);return link;}).ToArray()});
                if(engine!=null&&P(c)>=12)
                {var band=engine.ForagerBand(s,c.Id);if(band!=null)sections.Add(new AtlasSection{Title="Example household camp",Text=band.Name+" · "+band.Members.Length+" sampled residents · "+band.HomeRangeKm.ToString("0")+" km home-range radius. Camp candidates require local ground placement.",Facts=band.Members.Select(p=>F(p.Name,p.AgeYears.ToString("0")+" · "+p.Role)).ToArray()});}
                sections.Add(new AtlasSection{Title="Local history",Timeline=f.Events.Where(e=>e.Cohort==c.Id||e.District==c.District).TakeLast(20).Select(e=>F(Year((int)(e.Seconds/SimulationTime.Year)),e.Text)).ToArray()});
            }
            doc.Sections=sections.ToArray();return doc;
        }
    }
}
