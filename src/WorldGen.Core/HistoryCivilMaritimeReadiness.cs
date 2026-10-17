using System;
namespace WorldGen.Core
{
    public static class HistoricalCivilMaritimeEffects
    {
        public static double CohesionFloor(int revision,double bureaucracy,int preceding)
            =>revision<27?preceding:HistoricalRealmPoliticalEffects.FleetCohesionFloor(double.IsNaN(bureaucracy)||double.IsInfinity(bureaucracy)?0:bureaucracy);
        public static double IdleReviewYears(int revision,double returningFleet,double roll)
        {
            if(revision<27)return 5+15*roll;
            double value=double.IsNaN(roll)||double.IsInfinity(roll)?1:D.Clamp(roll);
            bool advanced=!double.IsNaN(returningFleet)&&!double.IsInfinity(returningFleet)&&returningFleet>=.2;
            return advanced?1+2*value:5+15*value;
        }
    }
    public sealed partial class Prehistory
    {
        bool CivilMaritimeHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=27;
        double CivilMaritimeCohesionFloor(PrehistoryState s,int polity,int preceding)
            =>CivilMaritimeHistory(s)?HistoricalCivilMaritimeEffects.CohesionFloor(27,RealmBureaucracy(s,polity),preceding):preceding;
    }
}
