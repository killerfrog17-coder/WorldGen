using System.Diagnostics;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;
// Reports outcomes of the coupled model. Broad error gates catch impossible
// worlds; the distributions and maps still require substantive review.
static class ForagerBehaviorAudit
{
    public static void Run(string file,string output)
    {
        Directory.CreateDirectory(output);var timer=Stopwatch.StartNew();var w=WorldFile.ReadFile(file);double readSeconds=timer.Elapsed.TotalSeconds;
        var s=w.Prehistory;var f=s.Foragers;var engine=new Prehistory(w);engine.ValidateForagers(s);
        var live=f.Cohorts.Where(c=>c.Population>=1).ToArray();double total=live.Sum(c=>c.Population);int checks=0;
        void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var environments=f.Districts.Select(d=>ForagerBiota.Apply(ForagerEcology.At(w.Recipe.Seed,d,s.Years,w.ElevationM[d.Cell]),f.Biota[d.Id])).ToArray();
        double Mean(IEnumerable<ForagerCohort> people,Func<ForagerCohort,double> value){var rows=people.ToArray();return rows.Length==0?0:rows.Sum(c=>c.Population*value(c))/rows.Sum(c=>c.Population);}
        var districts=live.GroupBy(c=>c.District).Select(g=>new{district=g.Key,population=g.Sum(c=>c.Population),density=g.Sum(c=>c.Population)/f.Districts[g.Key].AreaKm2,ice=environments[g.Key].IceCover,winter=environments[g.Key].WinterC}).ToArray();
        var speech=live.SelectMany(c=>c.Speech.Select(x=>(id:x.Id,number:x.Share*c.Population))).GroupBy(x=>x.id).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.number));
        var traditions=live.SelectMany(c=>c.Traditions.Select(x=>(id:x.Id,number:x.Share*c.Population))).GroupBy(x=>x.id).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.number));
        var observedLanguages=speech.Where(p=>p.Value>=1).ToArray();
        var families=observedLanguages.GroupBy(p=>f.Languages[p.Key].Family).Select(g=>new{id=g.Key,varieties=g.Count(),speakers=g.Sum(x=>x.Value)}).OrderByDescending(g=>g.speakers).ToArray();
        double exposed=live.Where(c=>c.Population>=5&&environments[c.District].IceCover>.9&&c.WinterSecurity<.05).Sum(c=>c.Population);
        Check(total>0&&double.IsFinite(total),"The world has a finite surviving human population");
        Check(districts.All(d=>d.density<=10),"No district exceeds ten foragers per square kilometre, even before a stricter habitat-specific review");
        Check(exposed<=Math.Max(10,total*.001),"No substantial unprotected resident population is sustained on perennial ice");
        Check(Math.Abs(f.Cohorts.Sum(c=>c.Population+c.Deaths-c.Births)-f.InitialPopulation)<Math.Max(.1,f.Cohorts.Length*.001),"Long-run births/deaths conserve the founding population");
        Check(Math.Abs(speech.Values.Sum()-total)<.001&&Math.Abs(traditions.Values.Sum()-total)<.001,"All resident affiliations account for exactly the living population");
        Check(observedLanguages.Length<=total/20+f.Lineages.Length,"Registered resident speech communities do not approach one language per person");
        Check(f.Cohorts.Where(c=>c.Population==0).All(c=>c.Teaching.Length==0&&c.TeachingFrom.Length==0&&c.Innovation.Length==0),"Retired groups retain no live incoming teaching context");
        Check(s.History==null||s.History.Polities.Length==0,"Forager home ranges have not become political states");
        var toolkits=live.Select(c=>string.Join("",c.Use.Select(v=>v>=.4?'1':'0'))).Distinct().Count();
        if(s.Years>=10000&&live.Length>100)Check(toolkits>2,"Long histories have distinct actual-use toolkits, not only different names");
        var comparisons=new List<object>();
        foreach(var group in new[]{("cold",live.Where(c=>environments[c.District].WinterC<0)),("warm",live.Where(c=>environments[c.District].WinterC>12)),("aquatic",live.Where(c=>c.Diet[3]+c.Diet[4]>.35)),("inland",live.Where(c=>c.Diet[3]+c.Diet[4]<.08))})
        {
            var rows=group.Item2.ToArray();comparisons.Add(new{habitat=group.Item1,population=rows.Sum(c=>c.Population),fitted=Mean(rows,c=>c.Use[13]),insulated=Mean(rows,c=>c.Use[14]),shelter=Mean(rows,c=>c.Use[31]),line=Mean(rows,c=>c.Use[19]),nets=Mean(rows,c=>c.Use[24]),boats=Mean(rows,c=>c.Use[30]),preserving=Mean(rows,c=>c.Use[23]),health=Mean(rows,c=>c.Health),food=Mean(rows,c=>c.FoodSecurity)});
        }
        var pairs=new List<(double km,double roots,double intelligibility,bool sameFamily)>();
        var languageOwners=observedLanguages.Select(p=>(id:p.Key,owner:live.Where(c=>c.Speech.Any(x=>x.Id==p.Key)).OrderByDescending(c=>c.Population*ForagerAffiliations.Share(c.Speech,p.Key)).First())).OrderBy(p=>Rng.Mix((uint)p.id)).Take(160).ToArray();
        for(int a=0;a<languageOwners.Length;a++)for(int b=a+1;b<languageOwners.Length;b++)
        {
            var x=languageOwners[a];var y=languageOwners[b];var lx=f.Languages[x.id];var ly=f.Languages[y.id];double km=w.Recipe.RadiusKm*Math.Acos(D.Clamp(V.Dot(f.Districts[x.owner.District].Center,f.Districts[y.owner.District].Center),-1,1));
            pairs.Add((km,ForagerLanguages.SharedRoots(lx,ly),ForagerLanguages.Intelligibility(lx,ly),lx.Family==ly.Family));
        }
        var distances=new[]{(0d,400d),(400d,1500d),(1500d,4000d),(4000d,30000d)}.Select(range=>{var rows=pairs.Where(p=>p.km>=range.Item1&&p.km<range.Item2).ToArray();return new{minimumKm=range.Item1,maximumKm=range.Item2,pairs=rows.Length,sharedRoots=rows.Length==0?0:rows.Average(p=>p.roots),intelligibility=rows.Length==0?0:rows.Average(p=>p.intelligibility),sameFamily=rows.Length==0?0:rows.Count(p=>p.sameFamily)/(double)rows.Length};}).ToArray();
        var biota=f.Biota.SelectMany(row=>row.Taxa.Select((id,k)=>(row.District,id,value:row.Abundance[k]))).ToArray();
        object Distribution(string name,Func<ForagerCohort,double> measure)
        {double mean=Mean(live,measure),variance=Mean(live,c=>Math.Pow(measure(c)-mean,2));return new{name,mean,populationWeightedStdDev=Math.Sqrt(variance),minimum=live.Min(measure),maximum=live.Max(measure)};}
        var social=new[]{Distribution("Food sharing",c=>f.Cultures[c.Culture].FoodSharing),Distribution("Guest access",c=>f.Cultures[c.Culture].GuestAccess),Distribution("Marriage mobility",c=>f.Cultures[c.Culture].MarriageMobility),Distribution("Conflict avoidance",c=>f.Cultures[c.Culture].ConflictAvoidance),Distribution("Childcare cooperation",c=>f.Cultures[c.Culture].ChildcareCooperation),Distribution("Narrative register",c=>f.Languages[c.Language].NarrativePractice),Distribution("Technical register",c=>f.Languages[c.Language].TechnicalRegister),Distribution("Animacy",c=>f.Beliefs[c.Belief].Animacy),Distribution("Ancestor presence",c=>f.Beliefs[c.Belief].AncestorPresence),Distribution("Ritual specialists",c=>f.Beliefs[c.Belief].RitualSpecialists)};
        var beliefContent=live.Select(c=>f.Beliefs[c.Belief]).Distinct().ToArray();
        var residence=live.GroupBy(c=>f.Cultures[c.Culture].Residence).Select(g=>new{mode=g.Key.ToString(),population=g.Sum(c=>c.Population)}).ToArray();
        var byRealm=live.GroupBy(c=>f.Districts[c.District].Realm).Select(g=>new{realm=g.Key,population=g.Sum(c=>c.Population),districts=g.Select(c=>c.District).Distinct().Count(),lineages=g.Select(c=>c.Lineage).Distinct().Count(),languages=g.SelectMany(c=>c.Speech.Where(x=>x.Share*c.Population>=1).Select(x=>x.Id)).Distinct().Count(),meanSkill=Mean(g,c=>c.Skill.Sum())}).ToArray();
        var result=new{source=Environment.GetEnvironmentVariable("WORLDGEN_FORAGER_SOURCE")??"working tree",s.Years,s.Hash,checks,readSeconds,saveBytes=new FileInfo(file).Length,total,active=live.Length,retired=f.Cohorts.Length-live.Length,inhabitedDistricts=districts.Length,maximumDensity=districts.Max(d=>d.density),exposedIcePopulation=exposed,toolkits,languageVarieties=observedLanguages.Length,languageFamilies=observedLanguages.Select(p=>f.Languages[p.Key].Family).Distinct().Count(),medianSpeakers=observedLanguages.Select(p=>p.Value).OrderBy(x=>x).ElementAt(observedLanguages.Length/2),singletonFamilyPopulationShare=families.Where(x=>x.varieties==1).Sum(x=>x.speakers)/total,largestFamilies=families.Take(12).ToArray(),familySizeDistribution=families.GroupBy(x=>x.varieties).ToDictionary(g=>g.Key,g=>g.Count()),beliefTraditions=traditions.Count(p=>p.Value>=1),comparisons,distances,social,residence,mortuaryModes=beliefContent.Select(b=>b.MortuaryPractice).Distinct().Count(),ritualModes=beliefContent.Select(b=>b.SeasonalRite).Distinct().Count(),ageShares=new[]{live.Sum(c=>c.Children)/total,live.Sum(c=>c.Adults)/total,live.Sum(c=>c.Elders)/total},byRealm,biotaRecords=biota.Length,establishedTaxa=biota.Count(x=>x.value>.03),colonisedTaxa=biota.Count(x=>x.value>.03&&!f.Districts[x.District].Cells.Any(cell=>Biogeography.Present(w.Ecology,cell,x.id)))};
        File.WriteAllText(Path.Combine(output,"behavior.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {checks} coupled-world error gates; full distributions in {output}");
    }
}
