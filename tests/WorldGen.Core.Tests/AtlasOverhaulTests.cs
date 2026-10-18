using System.Reflection;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
public static class AtlasOverhaulTests
{
    static object Call(Prehistory e,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args)!;
    public static void Run(Action<bool,string> check)
    {
        var rows=new[]{2,1000,10}.Select((n,i)=>new AtlasLink{Id=i,Name="Record "+n,Measures=new[]{new AtlasMeasure{Key="Population",Number=n,Numeric=true,Text=n.ToString("N0")}}}).ToArray();
        check(AtlasTables.Sort(rows,"Population",true).Select(r=>r.Measures[0].Number).SequenceEqual(new[]{1000d,10d,2d}),"Population columns sort actual numbers, not formatted strings");
        check(AtlasTables.Sort(rows,"Population",false).Select(r=>r.Measures[0].Number).SequenceEqual(new[]{2d,10d,1000d}),"Numeric ordering reverses exactly");
        foreach(var category in AtlasNavigation.Categories)check(AtlasTables.Columns(category.Kind).Length>=4&&AtlasTables.Columns(category.Kind).Distinct().Count()==AtlasTables.Columns(category.Kind).Length,"Tab-specific sortable columns: "+category.Name);
        check(AtlasDashboard.Visual(new AtlasSection{Title="Sounds and reusable roots",Timeline=new[]{new AtlasFact()}})==AtlasVisual.Phonetics,"Word/root cards are not misrepresented as historical events");
        check(AtlasDashboard.Panels(new AtlasDocument{Sections=new[]{new AtlasSection{Title="Knowledge",Bars=true,Links=new[]{new AtlasLink{Value=0}}}}}).Length==0,"World dashboard suppresses empty knowledge graphs");
        var estimate=new AtlasFact{Value="12,000 (estimate from year 713)"};check(AtlasDashboard.FactValue(estimate)=="12,000"&&AtlasDashboard.FactContext(estimate)=="estimate from year 713","Hero metrics retain estimate context without turning the main value into a paragraph");
        check(AtlasDashboard.FactValue(new AtlasFact{Value="Council (local) with assemblies"})=="Council (local) with assemblies","Only a trailing parenthetical becomes a fact footnote");
        check(AtlasDashboard.Panels(new AtlasDocument{Kind="state",Sections=new[]{new AtlasSection{Title="Empire and constituent governments",Facts=new[]{new AtlasFact{Value="0"}}}}}).Length==0,"A state without any imperial relationships does not get an empty imperial diagram on its dashboard");
        var useful=new[]{new AtlasSection{Title="Climate and livelihoods",Facts=new[]{new AtlasFact()}},new AtlasSection{Title="Ways of making a living",Bars=true,Links=new[]{new AtlasLink{Value=600}}}};
        check(AtlasDashboard.Panels(new AtlasDocument{Kind="people",Sections=useful})[0].Title=="Ways of making a living","Peoples lead with their actual subsistence section, not an incidental climate proxy");
        check(AtlasDashboard.Panels(new AtlasDocument{Kind="ruler",Sections=new[]{new AtlasSection{Title="Recorded history",Timeline=new[]{new AtlasFact()}},new AtlasSection{Title="Leadership profile",Links=new[]{new AtlasLink{Value=600}}}}})[0].Title=="Leadership profile","Ruler overview prioritizes leadership statistics over generic background");
        var profile=new HumanAppearance{Available=true,Region=7,SkinPigmentation=7000,HairPigmentation=8000,IrisPigmentation=7000,HairCurl=4000,BodyCompactness=5000,FacialVariation=4200,NasalVariation=6200,StaturePotential=5500};
        var samples=AtlasPortrait.Samples(profile);check(samples.Select(p=>p.SkinPigmentation).Distinct().Count()>1,"People sharing regional heritage are visibly variable individuals");
        for(int i=0;i<6;i++)foreach(HumanAppearanceTrait trait in Enum.GetValues<HumanAppearanceTrait>())
        {check(samples[i].Value(trait)>=0&&samples[i].Value(trait)<=10000,"NPC model parameter remains bounded");check(samples[i].Value(trait)==AtlasPortrait.Samples(profile)[i].Value(trait),"NPC appearance query is deterministic");}
        check(AtlasPortrait.Svg(profile).Contains("Six illustrative individuals")&&AtlasPortrait.Svg(profile)==AtlasPortrait.Svg(profile),"Visual samples are labelled and reproducible");
    }
    public static void World(string input,string output,Action<bool,string> check)
    {
        Directory.CreateDirectory(output);var world=WorldFile.Read(File.ReadAllBytes(input));var e=new Prehistory(world);var old=world.Prehistory;string oldHash=old.ComputeHash();
        var upgraded=e.UpgradeRealmLifecycles(old);e.Validate(upgraded);
        check(upgraded.History.Identities.Version==2&&upgraded.History.Aggregates.Empires.Connections.Realms.Version==36,"Explicit36 upgrade enables culture-independent regional heritage");
        check(old.ComputeHash()==oldHash&&old.History.Identities.Version==1,"Old35 remains literal until explicit upgrade");
        check(ReferenceEquals(upgraded,e.UpgradeRealmLifecycles(upgraded)),"Heritage upgrade is idempotent");
        check(upgraded.Population.SequenceEqual(old.Population)&&upgraded.Ancestry.SequenceEqual(old.Ancestry),"Upgrade invents neither people nor earlier physical variation");
        string before=upgraded.ComputeHash();int docs=0;
        var overview=HistoryAtlas.Read(upgraded,"world",-1,world,e);
        check(overview.Sections.Single(p=>p.Title=="Population accounts").Links.Sum(p=>p.Value)==SocietyView.Population(upgraded),"World population graphic reconciles with the actual total");
        foreach(var category in AtlasNavigation.Categories)
        {
            var directory=HistoryAtlas.Directory(upgraded,category.Kind,past:true);
            foreach(var row in directory.Take(3))
            {var doc=HistoryAtlas.Read(upgraded,row.Kind,row.Id,world,e);check(doc.Title==row.Name,"Every category opens its actual subject: "+category.Name);check(AtlasTables.Columns(row.Kind).Skip(1).Any(k=>AtlasTables.Cell(row,k).Text!="—"),"Directory has substantive data: "+category.Name);foreach(var panel in AtlasDashboard.Panels(doc))check(AtlasDashboard.Maximum(panel)>=1,"Chart scales are finite and nonzero");docs++;}
        }
        check(upgraded.ComputeHash()==before,"All Atlas queries, charts and directories are read-only");
        world.Prehistory=upgraded;File.WriteAllBytes(Path.Combine(output,"mature.world"),WorldFile.Write(world));
        var later=e.AdvanceYears(upgraded,12);e.Validate(later);var split=e.AdvanceYears(e.AdvanceYears(upgraded,5),7);check(later.Hash==split.Hash,"36 continuation is partition invariant");
        world.Prehistory=upgraded;var cold=WorldFile.Read(WorldFile.Write(world));var coldEnd=new Prehistory(cold).AdvanceYears(cold.Prehistory,12);check(coldEnd.Hash==later.Hash,"36 mature cold replay is exact");
        File.WriteAllText(Path.Combine(output,"mature12.hash"),later.Hash);
        world.Prehistory=null;e=new Prehistory(world);var start=e.Start(e.Prepare());e.Validate(start);
        check(start.Society.Cultures.Length==0&&Prehistory.PrimaryEthnicityAt(start,e.Patches[start.Origin].Cell)>=0,"Founding foragers have a heritage without an advanced culture");
        int original=Prehistory.PrimaryEthnicityAt(start,e.Patches[start.Origin].Cell);check(start.History.Identities.Peoples[original].Culture==-1,"Pre-cultural heritage is not a hidden fabricated culture actor");
        var inherited=start.Copy();int other=Enumerable.Range(0,e.Patches.Length).First(i=>i!=start.Origin&&e.Patches[i].Capacity>=25);
        Call(e,"CarryRegionalHeritage",inherited,start.Origin,other,50L,0L);
        check(Prehistory.PrimaryEthnicityAt(inherited,e.Patches[other].Cell)==original,"Actual household-carrying hook retains source heritage across a frontier");
        check(inherited.Population.SequenceEqual(start.Population)&&inherited.History.Aggregates.TotalPopulation==start.History.Aggregates.TotalPopulation,"Heritage accounts do not add a second population ledger");
        var early=e.AdvanceYears(start,16000);e.Validate(early);
        world.Prehistory=e.AdvanceYears(start,6000);var earlyCold=WorldFile.Read(WorldFile.Write(world));var continued=new Prehistory(earlyCold).AdvanceYears(earlyCold.Prehistory,10000);
        check(early.Hash==continued.Hash,"Natural forager expansion, divergence and appearance have exact cold/partition replay");
        int identities=early.History.Identities.Peoples.Length,occupied=Enumerable.Range(0,early.Population.Length).Count(i=>early.Population[i]>0);
        check(occupied>1&&identities>1,"Long-isolated forager communities differentiate before advanced culture is required");
        check(Enumerable.Range(0,early.Population.Length).Where(i=>early.Population[i]>0).All(i=>Prehistory.PrimaryEthnicityAt(early,e.Patches[i].Cell)>=0),"Occupied forager regions have inspectable identities");
        check(Enumerable.Range(0,early.Population.Length).Where(i=>early.Population[i]>0).Select(i=>early.Ancestry[i*8]).Distinct().Count()>1,"Regional inherited appearance really diverges outside cultures");
        world.Prehistory=early;File.WriteAllBytes(Path.Combine(output,"prehistory.world"),WorldFile.Write(world));
        var result=new{OldHash=oldHash,UpgradedHash=before,Mature12Hash=later.Hash,PrehistoryHash=early.Hash,OccupiedRegions=occupied,Identities=identities,Cultures=early.Society.Cultures.Length,Documents=docs};
        File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(result,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
    }
}
