using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
using System.Text.Json;
static class GenerationAdoptionTests
{
    public static void Run(string source,string output)
    {
        Directory.CreateDirectory(output);int checks=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
        var w=WorldFile.ReadFile(source);w.Prehistory=null;var engine=new Prehistory(w);var initial=engine.StartForagers(engine.PrepareForagers());w.Prehistory=initial;
        File.WriteAllBytes(Path.Combine(output,"initial.world"),WorldFile.Write(w));
        var curve=new AdoptionCurve{Start=10000,End=13000,From=0,To=1,Shape=AdoptionShape.SocialSpread};
        Check(curve.At(9999)==0&&curve.At(10000)==0&&curve.At(11500)==.5&&curve.At(13000)==1&&curve.At(300000)==1,"Dated social adoption is gradual and terminates exactly at its stored endpoint");
        Check(new AdoptionCurve{Start=0,End=100,To=1,Shape=AdoptionShape.Linear}.At(25)==.25,"Linear curve has a literal linear interpretation");
        var test=initial.Copy();var f=test.Foragers;var resident=f.Cohorts.Where(c=>c.Population>=1000).OrderBy(c=>c.Population).First();
        var plan=ForagerAdoption.Seed(resident);f.Adoption[resident.District]=plan;
        foreach(int k in new[]{0,3,7}){plan.Knowledge[k]=AdoptionCurve.Fixed(1);plan.Access[k]=AdoptionCurve.Fixed(1);}
        plan.Knowledge[17]=curve;plan.Access[17]=new AdoptionCurve{Start=10500,End=14000,To=.9,Shape=AdoptionShape.SocialSpread};plan.Use[17]=new AdoptionCurve{Start=11000,End=15000,To=.6,Shape=AdoptionShape.SocialSpread};
        f.Seconds=11500*SimulationTime.Year;test.Years=11500;var before=StateHash.Compute(f);var middle=ForagerAdoption.Project(f,resident);
        Check(middle.Knowledge[17]==.5&&ForagerAdoption.Access(middle,17)<.5&&middle.Use[17]<ForagerAdoption.Access(middle,17),"Knowing, practical access and routine adoption remain separate tribal measures");
        for(int i=0;i<1000;i++)ForagerAdoption.Project(f,resident);
        Check(StateHash.Compute(f)==before,"Inspecting and memoizing current adoption never changes authoritative history");
        var map=ForagerMaps.For(test);Check(map.Dominant(f.Districts[resident.District].Cell).Knowledge[17]==.5,"Technology map reads today's curve instead of the last event snapshot");
        var bands=Enumerable.Range(0,engine.ForagerBandCount(test,resident)).Take(100).Select(i=>engine.ForagerBand(test,resident.Id,i)).ToArray();
        double fraction=bands.Count(b=>b.Techniques[17])/(double)bands.Length;
        Check(bands.Length>=30&&fraction>.25&&fraction<.75,"A half-adopted regional tradition generates approximately half knowledgeable tribes");
        Check(bands.All(b=>!b.Techniques[17]||b.Members.Any(p=>p.Skills[17]>=.35)),"Knowledgeable tribes have actual competent local makers");
        Check(bands.All(b=>!b.Techniques[17]||new[]{0,3,7}.All(k=>b.Techniques[k])),"Generated tribal knowledge respects the same prerequisite paths");
        var color0=map.Color(0,HistoryOverlay.Civilizations);var color1=map.Color(1,HistoryOverlay.Civilizations);
        Check(color0.X<1&&color0.X>color1.X&&color0.X==color0.Y&&color1.X==color1.Z,"Civilizations distinguishes light-grey human species and medium-grey sapiens");
        foreach(var c in f.Cohorts.Where(c=>c.Population>=1))
        {
            int cell=f.Districts[c.District].Cell;bool sapiens=f.Lineages.Any(l=>l.PlayerSpecies&&f.SpeciesShare(c,l.Id)*map.Population(c)>=1);
            Check(map.Id(cell,HistoryOverlay.Civilizations)==(sapiens?1:0),"Civilization presence follows current species shares in district "+c.District);
        }
        var a=initial.Copy();a.Foragers.Seconds=150000*SimulationTime.Year;var b=initial.Copy();b.Foragers.Seconds=150001*SimulationTime.Year;
        double maximumTemperature=0,maximumRain=0;int localVariation=0;
        foreach(int cell in initial.Foragers.Districts.Where(d=>d.Cells.Length>2).Take(100).SelectMany(d=>d.Cells.Take(3)))
        {
            var ha=ForagerHabitat.For(w,a).Cell(cell);var hb=ForagerHabitat.For(w,b).Cell(cell);
            for(int q=0;q<4;q++){maximumTemperature=Math.Max(maximumTemperature,Math.Abs(ha.Weather.TemperatureC[q]-hb.Weather.TemperatureC[q]));maximumRain=Math.Max(maximumRain,Math.Abs(ha.Weather.MonthlyPrecipitationMm[q]-hb.Weather.MonthlyPrecipitationMm[q]));}
            if(ha.Classification!=ForagerHabitat.For(w,a).Cell(initial.Foragers.Districts[initial.Foragers.CellDistrict[cell]].Cell).Classification)localVariation++;
        }
        Check(maximumTemperature<.01&&maximumRain<.5,"Long-term climate changes continuously between adjacent years");
        Check(localVariation>0,"Climate uses local cell conditions rather than painting a representative region over all its cells");
        File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{checks,tribes=bands.Length,knowledgeFraction=fraction,maximumTemperature,maximumRain,localVariation}));Console.WriteLine("PASS "+checks+" generation/adoption/climate checks");
    }
}
