using WorldGen.Core;
using WorldGen.Serialization;
using System.Text.Json;

static class RiverNetworkStats
{
    public static void Run(string path,string output)
    {
        var w=WorldFile.ReadFile(path);var h=w.Hydrology;const double scale=.1;double radius=w.Recipe.RadiusKm*1000;
        double Q(int i)=>(h.NorthernSpringDischargeM3PerS[i]+h.NorthernSummerDischargeM3PerS[i]+h.NorthernAutumnDischargeM3PerS[i]+h.NorthernWinterDischargeM3PerS[i])*.25;
        var reaches=Enumerable.Range(0,w.Surface.Cells.Length).Where(i=>h.Receiver[i]>=0&&!h.OceanConnected[i]&&Q(i)>=.04).ToArray();
        double landKm2=w.Surface.Cells.Where(c=>!h.OceanConnected[c.Id]).Sum(c=>(double)c.AreaM2)*scale*scale/1e6;
        double Length(int i)=>(w.Surface.Cells[i].Position-w.Surface.Cells[h.Receiver[i]].Position).Length*radius*scale/1000;
        var policies=new List<object>();foreach(double minimum in new[]{.02,.1,.2,.5,1,2,5,10,20,50})
        {
            var active=reaches.Where(i=>Q(i)*scale*scale>=minimum).ToArray();double km=active.Sum(Length);
            policies.Add(new{localMinimumM3S=minimum,sourceMinimumM3S=minimum/(scale*scale),reaches=active.Length,rootKm=km,rootKmPerKm2=km/landKm2});
        }
        var largest=reaches.OrderByDescending(Q).Take(12).Select(i=>new{source=i,receiver=h.Receiver[i],q=Q(i),lengthKm=Length(i),order=h.RiverOrder[i],lat=Math.Asin(w.Surface.Cells[i].Position.Z)*180/Math.PI,lon=Math.Atan2(w.Surface.Cells[i].Position.Y,w.Surface.Cells[i].Position.X)*180/Math.PI}).ToArray();
        var result=new{w.Recipe.TerrainSource,w.Recipe.Seed,landKm2,policies,largest};Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));File.WriteAllText(output,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(result));
    }
}
