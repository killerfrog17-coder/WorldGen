using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public static class HistoricalTerritorialEffects
    {
        // Game priors, not estimated archaeological constants. Urban support is
        // deliberately distinct from the rural population gained by coloring land.
        public static double ReachKm(long urbanPeople,int administration,int publicWorks,bool writing,bool roads)
            =>D.Q((45+.18*administration+.08*publicWorks+(writing?85:0)+(roads?170:0))*(.25+.75*D.Clamp(D.Sqrt(Math.Max(0,urbanPeople)/150000.0))),1000000);
        public static double BattleChance(double attack,double defense)
        {
            if(attack<=0)return 0;if(defense<=0)return 1;
            double ratio=Math.Min(20,attack/defense);double power=ratio*ratio*ratio;
            return D.Q(power/(1+power),1000000000);
        }
        public static double WithdrawalChance(int cohesion,long treasury,int strain)
            =>D.Q(.04+.12*D.Clamp((600-cohesion)/600.0)+(treasury==0?.10:0)+.10*D.Clamp(strain/700.0),1000000000);
    }
    public sealed partial class Prehistory
    {
        void WithdrawUnsupportedTerritory(PrehistoryState s,int[] support,CancellationToken token)
        {
            var h=s.History;var towns=s.Society.Settlements.Where(t=>t.PolityId>=0&&t.Population>=100).ToDictionary(t=>t.Cell);
            var lost=new SortedDictionary<int,int>();
            foreach(int cell in PoliticalCells(s).ToArray())
            {
                if((cell&2047)==0)token.ThrowIfCancellationRequested();int owner=h.CellPolity[cell];
                if(owner<0||support[cell]==owner||towns.ContainsKey(cell)&&(!LocalHistory(s)||s.Society.Settlements[s.History.Polities[owner].CapitalId].Cell==cell))continue;
                var p=h.Polities[owner];var ds=h.Dynamics.States[owner];
                if(HistoryRoll("unsupported-frontier",cell,s.Years)>=HistoricalTerritorialEffects.WithdrawalChance(p.Cohesion,p.Treasury,ds.Strain))continue;
                SetPoliticalOwner(s,cell,-1);lost[owner]=(lost.TryGetValue(owner,out int count)?count:0)+1;
                // A declined site survives as a record, not an owner mismatch.
                foreach(var town in s.Society.Settlements.Where(t=>t.Cell==cell)){town.PolityId=-1;town.Government="Local households";}
            }
            foreach(var pair in lost)
            {
                var p=h.Polities[pair.Key];
                HistoryEvent(s,"frontier-retreat",p.Name+" withdraws its administration from unsupported frontier districts; local communities remain.",s.Society.Settlements[p.CapitalId].Cell,p.CultureId,p.Id,category:ChronicleCategory.Politics);
            }
        }
        void TransferAdministrativeDistrict(PrehistoryState s,int capturedTown,int from,int to,CancellationToken token=default)
        {
            var h=s.History;int n=h.CellPolity.Length;var distance=Enumerable.Repeat(int.MaxValue,n).ToArray();var district=Enumerable.Repeat(-1,n).ToArray();var heap=new Heap();
            foreach(var town in s.Society.Settlements.Where(t=>(t.PolityId==from||t.Id==capturedTown)&&t.Population>=100).OrderBy(t=>t.Id))
            {distance[town.Cell]=0;district[town.Cell]=town.Id;heap.Push(0,town.Cell);}
            while(heap.Count>0)
            {
                var x=heap.Pop();if(x.Key!=distance[x.Id])continue;token.ThrowIfCancellationRequested();var cell=World.Surface.Cells[x.Id];
                for(int k=0;k<cell.Neighbors.Length;k++)
                {
                    int j=cell.Neighbors[k];if(h.CellPolity[j]!=from||World.Hydrology.OceanConnected[j]||World.Hydrology.LakeCoverageFraction[j]>=.9)continue;
                    long next=x.Key+TravelCost(x.Id,j,cell.DistanceM[k],h.Cultures[h.Polities[from].CultureId]);
                    if(next<distance[j]||next==distance[j]&&district[x.Id]<district[j]){distance[j]=(int)next;district[j]=district[x.Id];heap.Push(next,j);}
                }
            }
            foreach(int cell in PolityCells(s,from).ToArray())if(district[cell]==capturedTown)SetPoliticalOwner(s,cell,to);
            foreach(var town in s.Society.Settlements.Where(t=>t.PolityId==from&&h.CellPolity[t.Cell]==to)){town.PolityId=to;town.Government=h.Polities[to].Government;h.Towns[town.Id].UnrestPolityId=to;h.Towns[town.Id].AutonomyPressure=0;}
        }
    }
}
