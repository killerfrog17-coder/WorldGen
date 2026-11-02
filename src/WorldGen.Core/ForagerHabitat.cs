using System;
using System.Linq;
using System.Runtime.CompilerServices;
namespace WorldGen.Core
{
    // Derived, dated local conditions. Original ecology region membership and
    // geological/hydrological provenance remain unchanged in the saved world.
    public sealed class ForagerHabitat
    {
        public sealed class Local
        {
            public HabitatCell Habitat;public WeatherClimate Weather;public Koppen Classification;
            public double ForestCover,IceCover;
        }
        static readonly ConditionalWeakTable<PrehistoryState,ForagerHabitat> Cache=new ConditionalWeakTable<PrehistoryState,ForagerHabitat>();
        public static ForagerHabitat For(World world,PrehistoryState state=null)
        {state=state??world.Prehistory;return state?.Foragers==null?null:Cache.GetValue(state,s=>new ForagerHabitat(world,s));}
        readonly World world;readonly PrehistoryState state;readonly Local[] cells;readonly object gate=new object();
        ForagerHabitat(World w,PrehistoryState s){world=w;state=s;cells=new Local[w.Surface.Cells.Length];}
        public Local Cell(int cell)
        {
            if(cell<0||cell>=cells.Length||state.Foragers.CellDistrict[cell]<0)return null;
            if(cells[cell]!=null)return cells[cell];lock(gate){if(cells[cell]==null)cells[cell]=Build(cell);return cells[cell];}
        }
        Local Build(int cell)
        {
            var f=state.Foragers;var d=f.Districts[f.CellDistrict[cell]];var h=world.Ecology.Habitat[cell];var point=world.Surface.Cells[cell].Position;
            var source=new[]{world.Climate.NorthernSpring,world.Climate.NorthernSummer,world.Climate.NorthernAutumn,world.Climate.NorthernWinter};
            var local=new ForagerDistrict{Id=d.Id,Province=d.Province,Center=point,Productivity=h.ProductivityGCM2Year,WinterC=h.ColdTenthsC/10d,SummerC=h.WarmTenthsC/10d,MeanC=h.MeanTenthsC/10d,
                Aridity=h.AridityMilli/1000d,GrowingDays=h.GrowingDays,Water=d.Water,Coast=d.Coast,River=d.River,Relief=h.ReliefM,Canopy=AnimalEcotypes.Canopy(h)/1000d,
                PetMm=h.PetMm,AnnualRainMm=source.Sum(s=>(double)s.PrecipitationMm[cell]),OceanMoistureAccess=d.OceanMoistureAccess,Pathogens=d.Pathogens,Food=d.Food,Seasonality=d.Seasonality};
            double years=f.Seconds/(double)SimulationTime.Year;
            var e=ForagerEcology.At(world.Recipe.Seed,local,years,world.ElevationM[cell]);
            double delta=PaleolithicRules.Climate(world.Recipe.Seed,years,point.Z,world.ElevationM[cell],d.Province).TemperatureDeltaC;
            var original=new WeatherClimate(source.Select(s=>s.TemperatureTenthsC[cell]/10d).ToArray(),source.Select(s=>s.PrecipitationMm[cell]/3d).ToArray(),source.Select(s=>s.MoisturePermille.Length>cell?s.MoisturePermille[cell]/1000d:.5).ToArray(),world.ElevationM[cell]);
            var climate=ForagerClimate.Apply(original,local,e,delta);
            int growing=0,deficit=0,dry=0,pet=0;
            for(int q=0;q<4;q++)
            {
                int demand=Ecology.PotentialEvaporationMm(point.Z,climate.TemperatureC[q],q);pet+=demand;
                double rain=climate.MonthlyPrecipitationMm[q]*3;deficit+=D.M(Math.Max(0,demand-rain));if(climate.TemperatureC[q]>5&&rain>Math.Max(10,demand*.2))growing+=91;
                if(demand>rain*1.5&&demand>30)dry++;
            }
            var a=new HabitatCell{PetMm=pet,AridityMilli=D.M(e.RainMm/Math.Max(1,pet)*1000),WaterDeficitMm=deficit,GrowingDays=growing,
                ColdTenthsC=D.M(climate.TemperatureC.Min()*10),WarmTenthsC=D.M(climate.TemperatureC.Max()*10),MeanTenthsC=D.M(climate.TemperatureC.Average()*10),
                WarmSeasonDryPermille=h.WarmSeasonDryPermille,WaterTableCm=h.WaterTableCm,PermafrostPermille=D.M(1000*D.Clamp((-e.MeanC+1)/7)),
                FloodFractionPermille=h.FloodFractionPermille,FloodDaysPerYear=h.FloodDaysPerYear,FloodPulsesPerYear=h.FloodPulsesPerYear,SalinityMilliPpt=h.SalinityMilliPpt,
                SoilFertilityPermille=h.SoilFertilityPermille,ProductivityGCM2Year=D.M(e.Productivity),ReliefM=h.ReliefM,Soil=e.MeanC< -4?SoilKind.Frozen:h.Soil,Flooding=h.Flooding};
            a.FirePotentialPermille=D.M(1000*D.Clamp(a.ProductivityGCM2Year/500d)*D.Clamp(dry/2d)*(1-a.FloodFractionPermille/1000d));
            a.Biome=e.IceCover>.6?Biome.Ice:Ecology.Classify(a,false,world.Hydrology.LakeCoverageFraction[cell],world.ElevationM[cell]);a.Habitat=Ecology.HabitatOf(a.Biome);
            double districtPotential=ForagerEcology.At(world.Recipe.Seed,d,years,world.ElevationM[d.Cell]).Canopy;
            return new Local{Habitat=a,Weather=climate,Classification=Climate.ClassifySeasons(climate.TemperatureC,climate.MonthlyPrecipitationMm.Select(p=>p*3).ToArray(),point.Z>=0),
                ForestCover=D.Clamp(e.Canopy*f.Biota[d.Id].ForestCover/Math.Max(.02,districtPotential)),IceCover=e.IceCover};
        }
    }
}
