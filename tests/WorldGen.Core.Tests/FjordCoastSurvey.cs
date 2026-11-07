using WorldGen.Core;
using WorldGen.Serialization;

static class FjordCoastSurvey
{
    public static void Run(string input,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadPlanetaryFile(input);int count=world.Surface.Cells.Length;
        var best=new int[count];Array.Fill(best,-1);
        double Flow(int i)=>world.Hydrology.NorthernSummerDischargeM3PerS.Length==count?
            .25*(world.Hydrology.NorthernSpringDischargeM3PerS[i]+world.Hydrology.NorthernSummerDischargeM3PerS[i]+world.Hydrology.NorthernAutumnDischargeM3PerS[i]+world.Hydrology.NorthernWinterDischargeM3PerS[i]):0;
        for(int i=0;i<count;i++)
        {
            int next=world.Hydrology.Receiver[i];if(next<0||world.Hydrology.OceanConnected[i])continue;
            if(best[next]<0||Flow(i)>Flow(best[next])||Flow(i)==Flow(best[next])&&i<best[next])best[next]=i;
        }
        var rows=new List<object>();int coastal=0,glacialMouth=0,upstreamIce=0,reliefIce=0;
        foreach(int source in Enumerable.Range(0,count).Where(i=>!world.Hydrology.OceanConnected[i]&&world.Hydrology.Receiver[i]>=0&&world.Hydrology.OceanConnected[world.Hydrology.Receiver[i]]&&world.ElevationM[i]>5))
        {
            coastal++;var path=new List<int>{source};int at=source,ice=-1;double length=0,maximum=world.ElevationM[source];
            double coastLandHigh=world.ElevationM[source];foreach(int n in world.Surface.Cells[source].Neighbors)if(!world.Hydrology.OceanConnected[n])coastLandHigh=Math.Max(coastLandHigh,world.ElevationM[n]);
            for(int step=0;step<16;step++)
            {
                if(world.Glaciated.Length>at&&world.Glaciated[at])ice=at;
                maximum=Math.Max(maximum,world.ElevationM[at]);
                int next=best[at];if(next<0)break;
                double distance=(world.Surface.Cells[at].Position-world.Surface.Cells[next].Position).Length*world.Recipe.RadiusKm*1000*.1;
                if(length+distance>50000||world.ElevationM[next]>2200)break;
                length+=distance;path.Add(next);at=next;
            }
            bool mouth=world.Glaciated.Length>source&&world.Glaciated[source];if(mouth)glacialMouth++;
            if(ice<0)continue;upstreamIce++;
            double landRelief=Math.Max(coastLandHigh,maximum)-world.ElevationM[source];if(landRelief>=300)reliefIce++;
            V p=world.Surface.Cells[source].Position;
            rows.Add(new{Source=source,Ocean=world.Hydrology.Receiver[source],Height=world.ElevationM[source],CoastLandHigh=coastLandHigh,MaximumLand=maximum,LandRelief=landRelief,GlacialMouth=mouth,UpstreamIce=ice,LengthM=length,Flow=Flow(source),Position=new[]{p.X,p.Y,p.Z},Path=path.ToArray()});
        }
        File.WriteAllText(Path.Combine(output,"coasts.json"),System.Text.Json.JsonSerializer.Serialize(rows,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Survey only: {coastal} land-to-ocean drainage mouths; {glacialMouth} mapped glacial mouths; {upstreamIce} have mapped ice within bounded upstream path; {reliefIce} also have300m landward relief. No geography changed.");
    }
}
