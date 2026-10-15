using System;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public static class HistoricalLivelihoodEffects
    {
        public static int FishingTarget(long aquaticResidents,long residents)
            =>50+(int)Math.Round(500*Math.Sqrt(D.Clamp(aquaticResidents/(double)Math.Max(1,residents))));
        public static int Approach(int previous,int target,double elapsedYears)
            =>Math.Max(0,Math.Min(1000,previous+Math.Sign(target-previous)*Math.Min(Math.Abs(target-previous),(int)Math.Round(12*Math.Max(0,elapsedYears)/50))));
    }
    public sealed partial class Prehistory
    {
        void AdaptHistoricalLivelihoods(PrehistoryState s,CancellationToken token,double elapsedYears)
        {
            var cultures=s.Society.Cultures;var total=new long[cultures.Length];var aquatic=new long[cultures.Length];
            double Opportunity(int cell)
            {
                bool coast=World.Hydrology.OceanConnected[cell]||World.Surface.Cells[cell].Neighbors.Any(j=>World.Hydrology.OceanConnected[j]);
                return D.Q(Math.Max(coast?.9:0,D.Clamp(HistoricalRiverAccess(cell)*4)),1000000);
            }
            foreach(int cell in CulturalCells(s))
            {
                if((cell&2047)==0)token.ThrowIfCancellationRequested();int owner=s.Society.CellCulture[cell];if(owner<0)continue;
                long residents=RuralCellPopulation(s,cell);total[owner]+=residents;aquatic[owner]+=(long)Math.Round(residents*Opportunity(cell));
            }
            foreach(var town in s.Society.Settlements){total[town.CultureId]+=town.Population;aquatic[town.CultureId]+=(long)Math.Round(town.Population*Opportunity(town.Cell));}
            foreach(var c in cultures)
            {
                if(total[c.Id]<=0)continue;int target=HistoricalLivelihoodEffects.FishingTarget(aquatic[c.Id],total[c.Id]);
                int next=HistoricalLivelihoodEffects.Approach(c.Fishing,target,elapsedYears),change=next-c.Fishing;
                if(change>0){change=Math.Min(change,c.Gathering+c.Hunting);int gathering=Math.Min(change,c.Gathering);c.Gathering-=gathering;c.Hunting-=change-gathering;}
                else c.Gathering-=change;
                c.Fishing+=change;
                if(c.Farming==0)c.Subsistence=c.Fishing>=300?"Coastal and river fishing with wild-food gathering":World.Ecology.Habitat[c.OriginCell].Habitat.HasFlag(Habitat.Forest)?"Woodland gathering and hunting":"Seasonal hunting and wild-food gathering";
            }
        }
    }
}
