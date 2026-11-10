using System;
using System.Linq;

namespace WorldGen.Core
{
    public static class CampWaterHeating
    {
        // One thermal reservoir per hearth, not separately ticking rock agents.
        // Reference zero is a cool 20 C stone; no negative stored energy.
        public static double StoneMass(CampObject fire)=>fire?.Materials.Where(CampRules.FireStone).Sum(p=>p.MassKg)??0;
        public static double StoneTemperature(CampObject fire)=>20+(StoneMass(fire)>0?fire.StoneHeatJ/(StoneMass(fire)*840):0);
        public static void AdvanceStones(CampObject fire,double heatedSeconds,double remainingSeconds,double power,bool embers)
        {
            double mass=StoneMass(fire);if(mass<=0){fire.StoneHeatJ=0;return;}
            double capacity=mass*840,tau=600*Math.Pow(Math.Max(.1,mass),1d/3);
            if(heatedSeconds>0){double target=Math.Min(650,250+50*power)-20;fire.StoneHeatJ=capacity*target+(fire.StoneHeatJ-capacity*target)*Math.Exp(-heatedSeconds/tau);}
            if(remainingSeconds>0){double target=embers?90:0;fire.StoneHeatJ=capacity*target+(fire.StoneHeatJ-capacity*target)*Math.Exp(-remainingSeconds/(tau*2));}
            fire.StoneHeatJ=Math.Max(0,fire.StoneHeatJ);
        }
        public static double RequiredEnergy(Possession vessel)=>vessel==null?double.PositiveInfinity:vessel.WaterLitres*4186*80/.65+25000;
        public static string Reason(CampObject fire,Possession vessel)
        {
            if(!PaleolithicWater.Vessel(vessel)||vessel.WaterLitres<.05)return "Fill a water vessel first.";
            if(vessel.WaterBoiled)return "This water has already been boiled.";
            if(fire==null||fire.Kind!=CampKind.Fire)return "Stay beside a hearth with heated stones.";
            if(StoneMass(fire)<.3)return "Add stones to the hearth and let them heat.";
            if(StoneTemperature(fire)<120||fire.StoneHeatJ<RequiredEnergy(vessel))return "The hearth stones need more heat for this much water.";
            return "";
        }
        public static bool Boil(CampObject fire,Possession vessel)
        {
            if(Reason(fire,vessel).Length>0)return false;
            fire.StoneHeatJ=Math.Max(0,fire.StoneHeatJ-RequiredEnergy(vessel));
            PaleolithicWater.Heat(vessel,100,60);return true;
        }
        public static bool DirectVessel(Possession p)=>p?.Kind==PossessionKind.FiredClayPot;
        public static void HeatPot(Possession p,double heatDose)
        {
            if(!DirectVessel(p)||p.WaterLitres<=0||p.WaterBoiled)return;
            // Dose includes the cold-water warm-up before the one-minute boil.
            p.CookingSeconds+=heatDose;
            if(p.CookingSeconds>=120+240*p.WaterLitres)PaleolithicWater.Heat(p,100,60);
        }
    }
}
