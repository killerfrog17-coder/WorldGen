using System;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        static bool BorderContactHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=6;
        int RegionalContactCell(PrehistoryState s,Route route,int region)
        {
            bool first=route.A==region;
            int cell=!route.Sea&&BorderContactHistory(s)?first?route.LandCellA:route.LandCellB:first?route.CellA:route.CellB;
            return cell>=0?cell:route.Sea&&developmentCoast[region]>=0?developmentCoast[region]:s.History.Aggregates.Regions[region].BestCell;
        }
        double RegionalLandRelayDistance(PrehistoryState s,Route route,int culture,int fromRegion,int toRegion,int from,int to)
        {
            if(route.Sea||!BorderContactHistory(s))return route.DistanceM/1000.0+Math.Min(450,Patches[fromRegion].RadiusKm+Patches[toRegion].RadiusKm);
            // Meeting across a real border is not an877km expedition between
            // favored sites. But carrying the practices through each district
            // still takes time and meets its forests, passes and dry interiors.
            var a=s.History.Aggregates;int source=a.Regions[fromRegion].BestCell,target=a.Regions[toRegion].BestCell;
            double Distance(int x,int y)=>D.Angle(World.Surface.Cells[x].Position,World.Surface.Cells[y].Position)*World.Recipe.RadiusKm;
            double insideSource=Distance(source,from)/Math.Max(.00001,EnvironmentalTransmission(s,source,from,culture));
            double insideTarget=Distance(to,target)/Math.Max(.00001,EnvironmentalTransmission(s,to,target,culture));
            return D.Q(insideSource+Distance(from,to)+insideTarget,1000000);
        }
    }
}
