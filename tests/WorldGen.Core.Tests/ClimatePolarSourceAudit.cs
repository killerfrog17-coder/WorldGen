using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

public static class ClimatePolarSourceAudit
{
    public static void Run(string path,string output)
    {
        if(File.Exists(output))throw new ArgumentException("Refusing to overwrite a climate-source audit.");var w=WorldFile.Read(File.ReadAllBytes(path));var seasons=new[]{w.Climate.NorthernSpring,w.Climate.NorthernSummer,w.Climate.NorthernAutumn,w.Climate.NorthernWinter};var coarse=new CellLocator(w.CoarseSurface.Cells);var fine=new CellLocator(w.Surface.Cells);
        var ocean=Enumerable.Range(0,w.Surface.Cells.Length).Where(c=>w.Hydrology.OceanConnected[c]).ToArray();
        double Lat(int cell)=>Math.Asin(w.Surface.Cells[cell].Position.Z)*180/Math.PI;
        object Point(int cell)
        {
            var p=w.Surface.Cells[cell].Position;double lat=D.Pi/2-D.Angle(p,new V(0,0,1)),sine=D.Sin(Math.Abs(lat)),s2=sine*sine,ice=D.Kernel(D.Pi/2-Math.Abs(lat),.25),contrast=1+2*D.Clamp((Math.Abs(lat)-.6)/.5);
            int center=coarse.Nearest(p);var ids=new[]{center}.Concat(w.CoarseSurface.Cells[center].Neighbors).ToArray();var weights=ids.Select(id=>1/Math.Max(1e-12,V.Dot(p-w.CoarseSurface.Cells[id].Position,p-w.CoarseSurface.Cells[id].Position))).ToArray();double total=weights.Sum();for(int k=0;k<weights.Length;k++)weights[k]/=total;
            double baseHeight=0;for(int k=0;k<ids.Length;k++)if(w.Climate.AtmosphericOceanPermille[ids[k]]<500)baseHeight+=weights[k]*w.Climate.AtmosphericElevationM[ids[k]];
            return new{Cell=cell,Latitude=Lat(cell),Longitude=Math.Atan2(p.Y,p.X)*180/Math.PI,ClimateLatitude=lat*180/Math.PI,Ocean=w.Hydrology.OceanConnected[cell],Elevation=w.ElevationM[cell],Biome=w.Ecology.Habitat[cell].Biome.ToString(),Classification=w.Climate.Classification[cell].ToString(),OceanBaselineC=29-30*s2-s2*s2*s2*s2,SeaIceKernel=ice,PolarLandKernel=D.Kernel(D.Pi/2-Math.Abs(lat),.4),PositiveCurrentAmplification=contrast,ThermalBias=ClimateCalibration.Weight(w.Recipe,0),CoarseCenter=center,CoarseCenterElevation=w.Climate.AtmosphericElevationM[center],InterpolatedThermalHeight=baseHeight,FineLapseAdjustmentC=(baseHeight-Math.Max(0,w.ElevationM[cell]))*.006,Seasons=Enumerable.Range(0,4).Select(q=>{
                double coarseT=0;for(int month=q*3;month<q*3+3;month++)for(int k=0;k<ids.Length;k++)coarseT+=weights[k]*w.Climate.AtmosphericMonths[month].TemperatureTenthsC[ids[k]]/30.0;
                int warmth=seasons[q].OceanWarmthMilli[cell];double currentC=warmth*(warmth<0?.004*(1+D.Kernel(Math.Abs(lat),.6)):.004*contrast);
                return new{Season=q,FineC=seasons[q].TemperatureTenthsC[cell]/10.0,WeightedCoarseC=coarseT,OceanWarmthMilli=warmth,CurrentThermalTermC=currentC,WindEastMilli=seasons[q].WindEastMilli[cell],WindNorthMilli=seasons[q].WindNorthMilli[cell],CurrentEastMilli=seasons[q].CurrentEastMilli[cell],CurrentNorthMilli=seasons[q].CurrentNorthMilli[cell]};}).ToArray()};
        }
        var ids=new List<int>();foreach(int target in new[]{7889,8287,7041})
        {
            if(target>=w.Surface.Cells.Length)continue;ids.Add(target);var p=w.Surface.Cells[target].Position;ids.Add(ocean.OrderBy(c=>D.Angle(p,w.Surface.Cells[c].Position)).First());var mirror=new V(p.X,p.Y,-p.Z);ids.Add(fine.Nearest(mirror));ids.Add(ocean.OrderBy(c=>D.Angle(mirror,w.Surface.Cells[c].Position)).First());
        }
        var bands=new List<object>();foreach(int hemisphere in new[]{-1,1})foreach(bool water in new[]{true,false})
        {
            var cells=Enumerable.Range(0,w.Surface.Cells.Length).Where(c=>w.Hydrology.OceanConnected[c]==water&&Lat(c)*hemisphere>=64&&Lat(c)*hemisphere<=69&&(water||w.ElevationM[c]<200)).ToArray();
            bands.Add(new{Hemisphere=hemisphere,Ocean=water,Count=cells.Length,QuarterMeanC=Enumerable.Range(0,4).Select(q=>cells.Length>0?cells.Average(c=>seasons[q].TemperatureTenthsC[c])/10.0:0).ToArray(),QuarterMaxC=Enumerable.Range(0,4).Select(q=>cells.Length>0?cells.Max(c=>seasons[q].TemperatureTenthsC[c])/10.0:0).ToArray(),QuarterWarmthMilli=Enumerable.Range(0,4).Select(q=>cells.Length>0?cells.Average(c=>seasons[q].OceanWarmthMilli[c]):0).ToArray()});
        }
        File.WriteAllText(output,JsonSerializer.Serialize(new{Input=path,w.Recipe.ClimateWeights,w.Recipe.TemperatureOffset,Warning="Static accepted climate diagnosis only; no recomputation or edits. Wind/current milli are relative fields, notm/s. Fine lapse adjustment uses0.006 illustrative lapse; actualmonthly humidity varies0.0055–0.0065.",Bands=bands,Points=ids.Distinct().Select(Point).ToArray()},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(output);
    }
}
