using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalUrbanDemography
    {
        public int Year;
        public long NaturalChange,RuralInflow,RuralOutflow,ChronicLoss;
        public long TotalRuralInflow,TotalRuralOutflow,TotalChronicLoss;
        public HistoricalUrbanDemography Copy()=>(HistoricalUrbanDemography)MemberwiseClone();
    }
    public static class HistoricalUrbanEffects
    {
        // Bounded demographic priors. Small settlements are not forced to be
        // mortality sinks; crowding/contact burden rises gradually with scale.
        public static double ChronicLossRate(long residents,int publicWorks,double vectorHabitat)
            =>D.Q((.0003+.005*D.Clamp(Math.Log(1+Math.Max(0,residents)/4000.0)/3))*(1-.35*D.Clamp(publicWorks/1000.0))*(1+.25*D.Clamp(vectorHabitat)),1000000000);
        public static double MigrationRate(double vacancy,double accessibility,double familiarity,int stability,bool disease)
            =>D.Q(.003*D.Clamp(vacancy)*D.Clamp(accessibility)*D.Clamp(familiarity)*D.Clamp((stability-300)/450.0)*(disease?.15:1),1000000000);
        public static long BoundedMigration(long requested,long available,long vacancies)
            =>Math.Max(0,Math.Min(Math.Max(0,requested),Math.Min(Math.Max(0,available),Math.Max(0,vacancies))));
    }
    public sealed partial class Prehistory
    {
        readonly struct UrbanMigration
        {
            public readonly int Patch,Town;public readonly long People;
            public UrbanMigration(int patch,int town,long people){Patch=patch;Town=town;People=people;}
        }
        readonly struct MigrationCatchment
        {
            public readonly int Cell;public readonly double DistanceKm;
            public MigrationCatchment(int cell,double distance){Cell=cell;DistanceKm=distance;}
        }
        IEnumerable<MigrationCatchment> MigrationCatchments(PrehistoryState s,CancellationToken token)
        {
            var index=WorkIndex(s);var market=s.History.CellMarket;var towns=s.Society.Settlements;
            if(index!=null&&ReferenceEquals(index.MigrationMarket,market)&&index.MigrationCells!=null)return index.MigrationCells;
            var cells=new List<MigrationCatchment>();
            using(ProfileWork("migration catchment index",market.Length))
                for(int cell=0;cell<market.Length;cell++)
                {
                    if((cell&2047)==0)token.ThrowIfCancellationRequested();int town=market[cell];if(town<0||CellPatch[cell]<0)continue;
                    cells.Add(new MigrationCatchment(cell,D.Angle(World.Surface.Cells[cell].Position,World.Surface.Cells[towns[town].Cell].Position)*World.Recipe.RadiusKm));
                }
            var result=cells.ToArray();if(index!=null){index.MigrationMarket=market;index.MigrationCells=result;}return result;
        }
        UrbanMigration[] PlanLocalUrbanMigration(PrehistoryState s,CancellationToken token)
        {
            PrepareHistoricalEnvironment();var towns=s.Society.Settlements;var requests=new SortedDictionary<(int Patch,int Town),long>();
            var room=towns.Select(t=>Math.Max(0,t.FoodCapacity-t.Population)).ToArray();var available=(long[])s.Population.Clone();
            // A market assignment has already established a real land route.
            // Only the local allocated residents are eligible, not an entire
            // distant ecoregion just because it contains the destination town.
            foreach(var catchment in MigrationCatchments(s,token))
            {
                int cell=catchment.Cell;
                if((cell&2047)==0)token.ThrowIfCancellationRequested();int townId=s.History.CellMarket[cell],patch=CellPatch[cell];if(townId<0||patch<0)continue;
                var town=towns[townId];if(town.Population<100||room[townId]<=0||town.FoodReserve<town.Population/2)continue;
                int sourceCulture=s.Society.CellCulture[cell];double familiarity=sourceCulture==town.CultureId?1:sourceCulture<0?.45:s.History.Cultures[sourceCulture].LanguageId==s.History.Cultures[town.CultureId].LanguageId?.8:.35;
                double distance=catchment.DistanceKm;
                double reach=MarketReach(s,s.History.Cultures[town.CultureId],town.Cell)/1000.0;
                double access=Math.Exp(-distance/Math.Max(1,reach));
                double rate=HistoricalUrbanEffects.MigrationRate(room[townId]/(double)Math.Max(1,town.FoodCapacity),access,familiarity,town.Stability,s.History.Towns[townId].DiseaseUntil>=s.Years);
                double demand=D.Q(RuralCellPopulation(s,cell)*rate,1000000);long count=(long)Math.Floor(demand);
                if(HistoryRoll("local-urban-migration-11",cell,s.Years)<demand-count)count++;
                var key=(patch,townId);requests[key]=(requests.TryGetValue(key,out long prior)?prior:0)+count;
            }
            var moves=new List<UrbanMigration>();
            foreach(var pair in requests)
            {
                long count=HistoricalUrbanEffects.BoundedMigration(pair.Value,available[pair.Key.Patch],room[pair.Key.Town]);if(count<=0)continue;
                available[pair.Key.Patch]-=count;room[pair.Key.Town]-=count;moves.Add(new UrbanMigration(pair.Key.Patch,pair.Key.Town,count));
            }
            return moves.ToArray();
        }
        void ApplyLocalUrbanMigration(PrehistoryState s,UrbanMigration[] moves)
        {
            foreach(var move in moves)
            {
                var town=s.Society.Settlements[move.Town];var record=s.History.Towns[move.Town];
                // Annual starvation/flight may have reduced rural supply since
                // planning. Never create people or overfill a destination.
                long count=HistoricalUrbanEffects.BoundedMigration(move.People,s.Population[move.Patch],town.FoodCapacity-town.Population);
                s.Population[move.Patch]-=count;town.Population+=count;record.Demography.RuralInflow+=count;record.Demography.TotalRuralInflow+=count;
            }
        }
    }
}
