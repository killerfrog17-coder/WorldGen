using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;
public static class HistoryProtocentreTests
{
    static object Call(Prehistory e,string method,params object[] values)=>typeof(Prehistory).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(e,values);
    public static void Run(Action<bool,string> check)
    {
        double old=HistoricalProtocentreEffects.MaximumResidents(.7,.25,550,0);
        check(old==HistoricalProtocentreEffects.MaximumResidents(.7,.25,550,0,0),"The four-argument source API preserves its exact zero-local-farmer behavior");
        double local=HistoricalProtocentreEffects.MaximumResidents(.7,.25,550,0,21000);
        check(local>old*1.5&&Math.Abs((local-250)/(old-250)-1.7)<.00001,"Actual neighboring farming residents can supply a pre-state community network without inventing a second city");
        check(HistoricalProtocentreEffects.MaximumResidents(.7,.25,550,0,1000000000)==local,"The local farming-network contribution saturates instead of converting all potential acreage into unlimited urban scale");
        check(HistoricalProtocentreEffects.MaximumResidents(.7,.25,550,0,-1)==old,"Negative supplied farm totals cannot raise the network prior");
        check(HistoricalProtocentreEffects.MaximumResidents(.7,.25,550,3,21000)==HistoricalProtocentreEffects.MaximumResidents(.7,.25,550,3),"Existing strong city links are not double-counted with the same farming community");
        check(HistoricalProtocentreEffects.MaximumResidents(.8,.02,200,0,21000)<800,"Even many actual farmers cannot bypass a weak storable-food and institutional base");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;string hash=source.ComputeHash();var e=new Prehistory(world);e.Validate(source);var s=source.Copy();
        var a=s.History.Aggregates;var rows=new List<object>();int tested=0;
        foreach(var town in s.Society.Settlements.Where(t=>t.Population>0&&a.Resolve(t.PolityId,s.History)<0&&a.Living.Cities[t.Id].AbandonedYear<0&&a.Cities[t.Id].Rural>=10000&&a.Cities[t.Id].Neighbors.Length==0).OrderByDescending(t=>t.Population).Take(8))
        {
            var city=a.Cities[town.Id];double environment=(double)Call(e,"EnvironmentalEstablishment",s,town.Cell,town.CultureId),stored=(double)Call(e,"LivingCropLandscape",s,town.CultureId,town.Cell,true);
            double legacy=HistoricalProtocentreEffects.MaximumResidents(environment,stored,s.History.Cultures[town.CultureId].Institution,0),grounded=HistoricalProtocentreEffects.MaximumResidents(environment,stored,s.History.Cultures[town.CultureId].Institution,0,city.Rural);
            long actualFood=(long)Call(e,"CommunityUrbanCapacity",s,town.Id),actualCap=(long)Call(e,"LivingProtocentreCapacity",s,town.Id,actualFood);
            check(grounded>legacy&&actualCap==Math.Min(actualFood,(long)grounded),"An actual unruled farming center receives its local rural network, bounded by actual worker-supported food");
            long rural=city.Rural;city.Rural=0;long withoutLocal=(long)Call(e,"LivingProtocentreCapacity",s,town.Id,long.MaxValue);city.Rural=rural;
            check(withoutLocal==(long)legacy,"Removing ONLY local farming residents removes the added network; regional and cultural totals cannot substitute");
            rows.Add(new{town.Id,town.Name,town.Population,LocalFarmers=city.Rural,ActualFood=actualFood,LegacyProtoCap=legacy,GroundedProtoCap=grounded,ActualBoundedCap=actualCap,Environment=environment,Stored=stored});tested++;
        }
        if(tested==0)throw new Exception("The protocentre fixture needs an actual unruled isolated town with at least ten thousand local farming residents.");
        Console.WriteLine(JsonSerializer.Serialize(new{Year=s.Years,Rule=s.History.Version,Counterfactuals=rows},new JsonSerializerOptions{WriteIndented=true}));
        check(s.ComputeHash()==hash&&source.ComputeHash()==hash,"Counterfactual inspection restores its copied local accounts and preserves every original saved value and hash");
    }
}
