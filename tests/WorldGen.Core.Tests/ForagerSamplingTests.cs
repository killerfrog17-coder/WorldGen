using WorldGen.Core;
using WorldGen.Serialization;
static class ForagerSamplingTests
{
    public static void Run(string path,string output="")
    {
        var w=WorldFile.ReadFile(path);var engine=new Prehistory(w);var s=w.Prehistory;var f=s.Foragers;int checks=0;
        void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        var selected=f.Cohorts.Where(c=>c.Population>60).OrderByDescending(c=>c.Population).Take(30).ToArray();
        var locator=new CellLocator(w.Surface.Cells);var samples=new List<ForagerBandSample>();
        foreach(var cohort in selected)
        {
            var a=engine.ForagerBand(s,cohort.Id);var b=engine.ForagerBand(s,cohort.Id);
            Check(a.Id==b.Id&&a.Members.Select(p=>p.Id).SequenceEqual(b.Members.Select(p=>p.Id)),"Repeated local queries preserve bands, people and household IDs");
            Check(a.Members.Length>=4&&a.Members.Any(p=>p.AgeYears>=18),"Sampled resident bands include an adult population");
            Check(a.Members.Select(p=>p.Id).Distinct().Count()==a.Members.Length,"No duplicated people within a band");
            Check(a.Members.All(p=>p.Languages.Length>0&&p.Languages[0].Native&&p.Languages[0].Language==a.Language),"Individuals retain their own band/home language rather than only an unassigned bilingual percentage");
            Check(a.Members.All(p=>p.Languages.Select(l=>l.Language).Distinct().Count()==p.Languages.Length&&p.Languages.All(l=>l.Language>=0&&l.Language<f.Languages.Length&&double.IsFinite(l.Fluency)&&l.Fluency>=0&&l.Fluency<=1)),"Sampled language knowledge has unique real language identities and bounded fluency");
            Check(a.Members.Where(p=>p.AgeYears<4).All(p=>p.Languages.Length==1&&p.Languages[0].Fluency<.7),"Young children do not appear as fluent adult multilingual speakers");
            var accessibleLanguages=new HashSet<int>(cohort.Speech.Select(x=>x.Id));
            foreach(var exchange in f.Exchanges.Where(e=>e.A==cohort.Id||e.B==cohort.Id))
            {
                var other=f.Cohorts[exchange.A==cohort.Id?exchange.B:exchange.A];foreach(var speech in other.Speech)accessibleLanguages.Add(speech.Id);
            }
            Check(a.Members.All(p=>p.Languages.All(l=>accessibleLanguages.Contains(l.Language))),"No sampled person learns a language from an absent/distant uncontacted group");
            Check(a.Members.All(p=>p.AgeYears>=0&&p.AgeYears<100&&p.Skills.Length==ForagerTechnology.Count),"Age and taught-skill samples stay plausible");
            Check(a.SeasonalCamps.Length==(int)Math.Round(a.MoveFrequencyPerYear)&&a.SeasonalCamps.All(c=>c.Season>=0&&c.Season<4&&f.CellDistrict[c.Cell]==cohort.District&&c.RequiresLocalGroundValidation),"Residence-specific seasonal itineraries remain in real accessible land districts and require local placement checks");
            var habitat=ForagerHabitat.For(w,s);
            Check(a.SeasonalCamps.All(camp=>camp.ClimateSuitable?habitat.Cell(camp.Cell).IceCover<=.6:camp.Exposure==1&&camp.WaterAccess==0&&camp.FoodOpportunity==0),"Camp candidates follow the dated ice/water/biota state and explicitly reject unusable climate rather than silently promising a habitable NPC camp");
            Check(f.CellDistrict[locator.Nearest(a.Home)]==cohort.District,"Band homes do not drift over a district boundary or into ocean");
            Check(a.SeasonalCamps.All(c=>f.CellDistrict[locator.Nearest(c.Position)]==cohort.District),"Seasonal offsets do not push camps outside their land district");
            for(int k=0;k<a.Techniques.Length;k++)if(a.Techniques[k])Check(ForagerTechnology.All[k].Prerequisites.All(p=>a.Techniques[p]),"Band skills retain necessary prerequisite techniques");
            Check(a.RoutineTechniques.Length==ForagerTechnology.Count&&a.WinterTechniques.Length==ForagerTechnology.Count,"NPC samples expose routine and prepared winter equipment separately from capability");
            Check(f.Version=="foragers-5"||Enumerable.Range(0,ForagerTechnology.Count).All(k=>(!a.RoutineTechniques[k]||a.Techniques[k])&&(!a.WinterTechniques[k]||a.Techniques[k])),"Legacy loadouts require resident makers; shared-production worlds may carry goods made elsewhere in the community");
            Check(Enumerable.Range(0,ForagerTechnology.Count).All(k=>!a.RoutineTechniques[k]||a.WinterTechniques[k]),"Winter preparation retains ordinary equipment instead of rerolling it away");
            Check(Enumerable.Range(0,ForagerTechnology.Count).All(k=>a.Techniques[k]==a.Members.Any(p=>p.Skills[k]>=.35)),"Band making capability is supported by actual capable resident makers");
            Check(a.Members.All(p=>p.Knowledge.Length==ForagerTechnology.Count&&Enumerable.Range(0,ForagerTechnology.Count).All(k=>p.Knowledge[k]>=p.Skills[k]&&ForagerTechnology.All[k].Prerequisites.All(q=>p.Skills[q]>=p.Skills[k]))),"Familiarity differs from trained proficiency; makers retain their prerequisite skills");
            var records=a.FamilyRecords.ToDictionary(p=>p.Id);
            foreach(var child in a.Members.Where(p=>p.Mother>=0))
            {
                Check(records.TryGetValue(child.Mother,out var mother)&&mother.BirthYear+18<=child.BirthYear&&mother.DeathYear>child.BirthYear,"Every sampled child has an adult mother who was alive at birth");
                Check(records.TryGetValue(child.Father,out var father)&&father.BirthYear+18<=child.BirthYear&&father.DeathYear>child.BirthYear-.75,"A child's father was alive at conception, not generated after his death");
            }
            Check(a.Members.Where(p=>p.AgeYears<18).All(p=>p.Partner<0),"Children are not labelled as existing spouses");
            Check(a.Members.GroupBy(p=>p.Household).All(g=>g.Any(p=>p.AgeYears>=18)),"Resident households preserve an adult guardian instead of individually truncating families");
            int key=unchecked(cohort.Id*104729);
            var contactReference=f.Exchanges.Where(e=>e.A==cohort.Id||e.B==cohort.Id).Where(e=>e.Strength>.005).OrderByDescending(e=>e.Strength).Take(8).Select(e=>e.A==cohort.Id?e.B:e.A).Select(id=>f.Cohorts.First(c=>c.Id==id)).Where(c=>engine.ForagerBandCount(s,c)>0).Select(c=>((long)(c.Id+1)<<32)|(uint)(1+(int)(Rng.Value(w.Recipe.Seed,"band-marriage-contact",key^c.Id)*engine.ForagerBandCount(s,c)))).ToArray();
            Check(a.ContactBands.SequenceEqual(contactReference),"The local contact index preserves every sampled relation and stable tie order from the full exchange ledger");
            samples.Add(a);
        }
        Check(samples.Select(b=>b.Id).Distinct().Count()==samples.Count,"Neighbouring bands do not share identity IDs");
        // Independent exhaustive spatial lookup: no district/home pruning and
        // no candidate cap before checking the actual seasonal positions.
        var layoutMethod=typeof(Prehistory).GetMethod("ForagerBandLayout",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        double Distance(V a,V b)
        {
            double latA=Math.Atan2(a.Z,Math.Sqrt(a.X*a.X+a.Y*a.Y)),latB=Math.Atan2(b.Z,Math.Sqrt(b.X*b.X+b.Y*b.Y));
            double longitude=Math.Atan2(a.Y,a.X)-Math.Atan2(b.Y,b.X);
            double h=Math.Pow(Math.Sin((latA-latB)*.5),2)+Math.Cos(latA)*Math.Cos(latB)*Math.Pow(Math.Sin(longitude*.5),2);
            return 2*w.Recipe.RadiusKm*Math.Atan2(Math.Sqrt(Math.Max(0,h)),Math.Sqrt(Math.Max(0,1-h)));
        }
        var allPositions=new List<(int cohort,int ordinal,V position)>();
        var positionTimer=System.Diagnostics.Stopwatch.StartNew();
        foreach(var c in f.Cohorts.Where(x=>x.Population>=12))
        {
            int count=engine.ForagerBandCount(s,c);
            for(int ordinal=0;ordinal<count;ordinal++)
            {
                var layout=(ForagerBandSample)layoutMethod.Invoke(engine,new object[]{s,c,ordinal,count})!;
                allPositions.Add((c.Id,ordinal,layout.Position));
            }
        }
        positionTimer.Stop();
        ForagerBandSample[] Reference(V position,double radius,int maximum)
            =>allPositions.Select(x=>(x.cohort,x.ordinal,distance:Distance(position,x.position))).Where(x=>x.distance<=radius).OrderBy(x=>x.distance).ThenBy(x=>x.cohort).ThenBy(x=>x.ordinal).Take(maximum).Select(x=>engine.ForagerBand(s,x.cohort,x.ordinal)).ToArray();
        double referenceMilliseconds=0,indexedMilliseconds=0;
        foreach(var band in samples.Take(3))foreach(double radius in new[]{2d,100d})
        {
            var queryTimer=System.Diagnostics.Stopwatch.StartNew();var expected=Reference(band.Position,radius,8);referenceMilliseconds+=queryTimer.Elapsed.TotalMilliseconds;
            queryTimer.Restart();var actual=engine.ForagerBandsNear(s,band.Position,radius,8);indexedMilliseconds+=queryTimer.Elapsed.TotalMilliseconds;
            Check(ForagerRecordTransportTests.Fingerprint(expected)==ForagerRecordTransportTests.Fingerprint(actual),"District-index queries preserve the complete exhaustive query result, resident identities and ordering");
        }
        var first=samples[0];var near=engine.ForagerBandsNear(s,first.Position,2,64);Check(near.Any(b=>b.Id==first.Id),"Spatial lookup finds an actual resident at its current camp");
        foreach(var band in samples)
        {
            Check(engine.ForagerBandsNear(s,band.Position,.1,1).Length>0,"A 100m one-band local query must not miss a known camp through rounded angular distance or premature home ranking");
            Check(ForagerRecordTransportTests.Fingerprint(Reference(band.Position,2,1))==ForagerRecordTransportTests.Fingerprint(engine.ForagerBandsNear(s,band.Position,2,1)),"One-band queries return the nearest current camp, regardless of permanent-home order");
        }
        var later=engine.AdvanceSeconds(s,SimulationTime.Day);var tomorrow=engine.ForagerBand(later,first.Cohort);
        var common=first.Members.Join(tomorrow.Members,p=>p.Id,p=>p.Id,(a,b)=>(a,b)).ToArray();
        Check(common.Length>=first.Members.Length-1&&common.All(p=>Math.Abs(p.b.AgeYears-p.a.AgeYears-1/365d)<.000001),"A gameplay day ages the same residents instead of rerolling them");
        var expanded=s.Copy();var expandedCohort=expanded.Foragers.Cohorts.First(c=>c.Id==first.Cohort);expandedCohort.FrontierKm*=1.5;ForagerDemography.SetPopulation(expandedCohort,expandedCohort.Population*1.4);
        var expandedBand=engine.ForagerBand(expanded,first.Cohort);
        Check(expandedBand.Home.X==first.Home.X&&expandedBand.Home.Y==first.Home.Y&&expandedBand.Home.Z==first.Home.Z,"Population growth/frontier expansion do not reroll an existing band's home");
        // A low aggregate share of makers must yield rare competent specialists,
        // not universally weak workers unable to make the community's tools.
        ForagerDemography.SetPopulation(expandedCohort,500);Array.Fill(expandedCohort.Skill,.9);Array.Fill(expandedCohort.Knowledge,.95);expandedCohort.Skill[17]=.1;expandedCohort.Knowledge[17]=.6;
        var specialists=Enumerable.Range(0,Math.Min(12,engine.ForagerBandCount(expanded,expandedCohort))).SelectMany(ordinal=>engine.ForagerBand(expanded,expandedCohort.Id,ordinal).Members).Where(p=>p.AgeYears>=23).ToArray();
        Check(specialists.Any(p=>p.Skills[17]>=.6)&&specialists.Count(p=>p.Skills[17]>=.35)<specialists.Length*.4,"Rare bow-making prevalence creates actual proficient specialists among many non-makers");
        if(f.Version=="foragers-5")
        {
            Array.Clear(expandedCohort.Skill);Array.Clear(expandedCohort.Use);expandedCohort.Skill[13]=.005;expandedCohort.Use[13]=.8;
            var consumers=Enumerable.Range(0,Math.Min(12,engine.ForagerBandCount(expanded,expandedCohort))).Select(ordinal=>engine.ForagerBand(expanded,expandedCohort.Id,ordinal)).ToArray();
            Check(consumers.Any(b=>b.RoutineTechniques[13]&&!b.Techniques[13]),"A band can wear goods supplied by community makers without gaining nonexistent resident crafting expertise");
        }
        Check(s.Hash==s.ComputeHash(),"Sampling and future snapshots cannot alter source history");
        if(output.Length>0)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"band-samples.json"),System.Text.Json.JsonSerializer.Serialize(new{s.Years,s.Hash,checks,bands=samples},new System.Text.Json.JsonSerializerOptions{IncludeFields=true,Converters={new SampleVectorConverter()}}));
        }
        Console.WriteLine($"Language samples: {samples.Sum(b=>b.Members.Count(p=>p.Languages.Length>1))} multilingual / {samples.Sum(b=>b.Members.Length)} people");
        Console.WriteLine($"Six exact local queries: cached exhaustive={referenceMilliseconds:F2}ms indexed={indexedMilliseconds:F2}ms; exhaustive layout preparation={positionTimer.Elapsed.TotalMilliseconds:F2}ms/{allPositions.Count} bands; records={f.Cohorts.Length}, active={f.Cohorts.Count(c=>c.Population>=1)}");
        Console.WriteLine($"PASS {checks} band/household/seasonal-camp checks across {samples.Count} communities");
    }
    sealed class SampleVectorConverter:System.Text.Json.Serialization.JsonConverter<V>
    {
        public override V Read(ref System.Text.Json.Utf8JsonReader reader,Type type,System.Text.Json.JsonSerializerOptions options)=>throw new NotSupportedException();
        public override void Write(System.Text.Json.Utf8JsonWriter writer,V value,System.Text.Json.JsonSerializerOptions options)
        {writer.WriteStartObject();writer.WriteNumber("X",value.X);writer.WriteNumber("Y",value.Y);writer.WriteNumber("Z",value.Z);writer.WriteEndObject();}
    }

}
