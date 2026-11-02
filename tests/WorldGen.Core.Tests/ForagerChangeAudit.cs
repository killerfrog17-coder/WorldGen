using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;
static class ForagerChangeAudit
{
    public static void Run(string beforeFile,string afterFile,string output)
    {
        var a=WorldFile.ReadFile(beforeFile);var b=WorldFile.ReadFile(afterFile);var before=a.Prehistory;var after=b.Prehistory;var x=before.Foragers;var y=after.Foragers;int checks=0;
        void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        Check(a.Stages.Last().Hash==b.Stages.Last().Hash&&x.Districts.Length==y.Districts.Length,"Dated comparisons use the exact same source world and stable districts");
        var changes=new Dictionary<int,(int gained,int lost,int increased,int declined)>();int arrivals=0,losses=0,up=0,down=0;double forestArea=0,forestDelta=0,maximumForestDelta=0;
        var places=new List<object>();
        for(int i=0;i<x.Biota.Length;i++)
        {
            var old=x.Biota[i];var current=y.Biota[i];int localNew=0,localLoss=0;
            foreach(int id in old.Taxa.Union(current.Taxa))
            {
                double earlier=old.Presence(id),later=current.Presence(id);bool gain=earlier<=.03&&later>.03,loss=earlier>.03&&later<=.03,increase=earlier>.03&&later>earlier*1.5,decline=later>.03&&later<earlier*.5;
                changes.TryGetValue(id,out var previous);changes[id]=(previous.gained+(gain?1:0),previous.lost+(loss?1:0),previous.increased+(increase?1:0),previous.declined+(decline?1:0));
                if(gain){arrivals++;localNew++;}if(loss){losses++;localLoss++;}if(increase)up++;if(decline)down++;
            }
            double delta=current.ForestCover-old.ForestCover;forestDelta+=delta*x.Districts[i].AreaKm2;forestArea+=x.Districts[i].AreaKm2;maximumForestDelta=Math.Max(maximumForestDelta,Math.Abs(delta));
            if(localNew+localLoss>0)places.Add(new{district=i,sourcePatch=x.Districts[i].Patch,arrivals=localNew,localExtinctions=localLoss,forestBefore=old.ForestCover,forestAfter=current.ForestCover});
        }
        Check(arrivals>0&&losses>0,"The actual dated species roster has both colonisation and local disappearance");
        Check(maximumForestDelta>.05,"At least some actual vegetation cover changes materially, rather than only its date label");
        var oldPeople=x.Cohorts.Where(c=>c.Population>=1).ToArray();var newPeople=y.Cohorts.Where(c=>c.Population>=1).ToArray();
        double Mean(ForagerCohort[] people,int k,bool use)=>people.Sum(c=>c.Population*(use?c.Use[k]:c.Skill[k]))/people.Sum(c=>c.Population);
        var techniques=ForagerTechnology.All.Select(t=>new{t.Id,t.Name,skillBefore=Mean(oldPeople,t.Id,false),skillAfter=Mean(newPeople,t.Id,false),useBefore=Mean(oldPeople,t.Id,true),useAfter=Mean(newPeople,t.Id,true)}).ToArray();
        var species=changes.Select(p=>new{id=p.Key,name=b.Ecology.Taxa[p.Key].Name,producer=b.Ecology.Taxa[p.Key].Producer,gained=p.Value.gained,lost=p.Value.lost,increased=p.Value.increased,declined=p.Value.declined}).OrderByDescending(v=>v.gained+v.lost).ToArray();
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"changes.json"),JsonSerializer.Serialize(new{checks,worldHash=a.Stages.Last().Hash,fromYear=before.Years,toYear=after.Years,arrivals,localExtinctions=losses,abundanceIncreases=up,abundanceDeclines=down,meanForestCoverChange=forestDelta/forestArea,maximumForestDelta,changedDistricts=places.Count,techniques,species,places},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {checks} actual dated-world change gates; arrivals={arrivals}, local disappearances={losses}, changed districts={places.Count}");
    }
}
