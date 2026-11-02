using System.Globalization;
using WorldGen.Core;
using WorldGen.Serialization;
static class ForagerHistoryAudit
{
    public static void Run(string file,string output)
    {
        Directory.CreateDirectory(output);var w=WorldFile.ReadFile(file);var s=w.Prehistory;var f=s.Foragers;
        double Lat(V p)=>Math.Asin(p.Z)*180/Math.PI;double Lon(V p)=>Math.Atan2(p.Y,p.X)*180/Math.PI;
        var lineages=f.Lineages.Select(l=>new{l.Id,l.Name,originLat=Lat(f.Districts[l.OriginDistrict].Center),originLon=Lon(f.Districts[l.OriginDistrict].Center),people=f.Cohorts.Where(c=>c.Lineage==l.Id).Sum(c=>c.Population),districts=f.Cohorts.Count(c=>c.Lineage==l.Id&&c.Population>=1),l.AdultMassKg,l.Compactness}).ToArray();
        File.WriteAllText(Path.Combine(output,"lineages.json"),System.Text.Json.JsonSerializer.Serialize(lineages,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        foreach(var l in lineages)Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(l));
        Console.WriteLine("Ocean moisture access: "+f.Districts.Count(d=>d.OceanMoistureAccess>.01)+" / "+f.Districts.Length+" districts; maximum "+f.Districts.Max(d=>d.OceanMoistureAccess).ToString("0.000"));
        var lines=new List<string>{"cohort,district,lineage,culture,language,family,belief,identity,latitude,longitude,population,capacity,food,winter,health,children,adults,elders,birthRate,adultMortality,connectivity,isolation,conflict,yearFounded,births,deaths,immigration,emigration,skillCount,useCount,plantStock,gameStock,plantAvailability,gameAvailability,forest,winterC,summerC,ice,occupation"};
        foreach(var c in f.Cohorts)
        {
            var d=f.Districts[c.District];var e=ForagerEcology.At(w.Recipe.Seed,d,s.Years,w.ElevationM[d.Cell]);var bio=f.Biota[d.Id];
            var values=new double[]{c.Id,c.District,c.Lineage,c.Culture,c.Language,f.Languages[c.Language].Family,c.Belief,c.Identity,Lat(d.Center),Lon(d.Center),c.Population,c.Capacity,c.FoodSecurity,c.WinterSecurity,c.Health,c.Children,c.Adults,c.Elders,c.BirthRate,c.AdultMortality,c.Connectivity,c.Isolation,c.ConflictPressure,c.FoundedYear,c.Births,c.Deaths,c.Immigration,c.Emigration,c.Skill.Count(v=>v>.5),c.Use.Count(v=>v>.5),f.Resources[d.Id].Stock[0],f.Resources[d.Id].Stock[2],bio.FoodAvailability[0],bio.FoodAvailability[2],bio.ForestCover,e.WinterC,e.SummerC,e.IceCover,c.Occupation};
            lines.Add(string.Join(",",values.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
        }
        File.WriteAllLines(Path.Combine(output,"cohorts.csv"),lines);
        var technologies=ForagerTechnology.All.Select(t=>new{t.Id,t.Name,skilled=f.Cohorts.Count(c=>c.Population>=1&&c.Skill[t.Id]>.5),used=f.Cohorts.Count(c=>c.Population>=1&&c.Use[t.Id]>.5),sapiens=f.Cohorts.Count(c=>c.Population>=1&&c.Lineage==0&&c.Skill[t.Id]>.5),origins=f.Discoveries.Where(d=>d.Technique==t.Id).Select(d=>new{d.Year,d.District,d.Cohort}).ToArray()});
        File.WriteAllText(Path.Combine(output,"technologies.json"),System.Text.Json.JsonSerializer.Serialize(technologies,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        var districts=f.Districts.Select(d=>new{d.Id,d.Patch,d.Cells,latitude=Lat(d.Center),longitude=Lon(d.Center),d.AreaKm2,d.Canopy,d.Productivity,d.Water,d.Coast,d.River,d.Stone,d.Relief,d.PetMm,d.AnnualRainMm,d.OceanMoistureAccess,environment=ForagerEcology.At(w.Recipe.Seed,d,s.Years,w.ElevationM[d.Cell]),biota=f.Biota[d.Id]}).ToArray();
        File.WriteAllText(Path.Combine(output,"districts.json"),System.Text.Json.JsonSerializer.Serialize(districts,new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));
        File.WriteAllText(Path.Combine(output,"summary.json"),System.Text.Json.JsonSerializer.Serialize(new{year=s.Years,s.Hash,f.InitialPopulation,cohorts=f.Cohorts.Length,active=f.Cohorts.Count(c=>c.Population>=1),families=f.LanguageFamilies.Length,discoveries=f.Discoveries.Length,livePopulation=f.Cohorts.Sum(c=>c.Population),ledgerBalance=f.Cohorts.Sum(c=>c.Population+c.Deaths-c.Births)-f.InitialPopulation,immigrationBalance=f.Cohorts.Sum(c=>c.Immigration-c.Emigration)}));
    }
}
