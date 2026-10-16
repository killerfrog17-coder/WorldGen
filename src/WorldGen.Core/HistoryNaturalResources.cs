using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    // Stable ordered IDs. These describe local potential, not proved reserves,
    // operational mines, tradable inventory, household access or free income.
    public enum HistoricalNaturalResource
    {
        Copper,Tin,Iron,PreciousMetals,Coal,Oil,Uranium,Stone,Timber,Salt,Fertility,Freshwater,Pasture
    }
    public readonly struct HistoricalResourcePotential
    {
        public readonly int Copper,Tin,Iron,PreciousMetals,Coal,Oil,Uranium,Stone,Timber,Salt,Fertility,Freshwater,Pasture;
        public readonly bool SyntheticEarthGeology;
        public HistoricalResourcePotential(int copper,int tin,int iron,int precious,int coal,int oil,int uranium,int stone,int timber,int salt,int fertility,int freshwater,int pasture,bool syntheticEarthGeology)
        {Copper=copper;Tin=tin;Iron=iron;PreciousMetals=precious;Coal=coal;Oil=oil;Uranium=uranium;Stone=stone;Timber=timber;Salt=salt;Fertility=fertility;Freshwater=freshwater;Pasture=pasture;SyntheticEarthGeology=syntheticEarthGeology;}
        public int this[HistoricalNaturalResource resource]
        {
            get
            {
                switch(resource)
                {
                    case HistoricalNaturalResource.Copper:return Copper;case HistoricalNaturalResource.Tin:return Tin;
                    case HistoricalNaturalResource.Iron:return Iron;case HistoricalNaturalResource.PreciousMetals:return PreciousMetals;
                    case HistoricalNaturalResource.Coal:return Coal;case HistoricalNaturalResource.Oil:return Oil;
                    case HistoricalNaturalResource.Uranium:return Uranium;case HistoricalNaturalResource.Stone:return Stone;
                    case HistoricalNaturalResource.Timber:return Timber;case HistoricalNaturalResource.Salt:return Salt;
                    case HistoricalNaturalResource.Fertility:return Fertility;case HistoricalNaturalResource.Freshwater:return Freshwater;
                    case HistoricalNaturalResource.Pasture:return Pasture;default:throw new ArgumentOutOfRangeException(nameof(resource));
                }
            }
        }
        // Explicit comparative substitutes, not a universal production recipe.
        public int CharcoalPotential=>Timber*65/100;
        public int EarlyBuildingPotential=>Math.Max(Stone,Timber);
    }
    public static class HistoricalNaturalResources
    {
        public const string Version="history-natural-resources-1";
        public const int Count=13;
        public static string Name(HistoricalNaturalResource resource)
        {
            switch(resource)
            {
                case HistoricalNaturalResource.Copper:return "Copper";case HistoricalNaturalResource.Tin:return "Tin";
                case HistoricalNaturalResource.Iron:return "Iron";case HistoricalNaturalResource.PreciousMetals:return "Precious metals";
                case HistoricalNaturalResource.Coal:return "Coal";case HistoricalNaturalResource.Oil:return "Oil and gas";
                case HistoricalNaturalResource.Uranium:return "Uranium";case HistoricalNaturalResource.Stone:return "Building stone";
                case HistoricalNaturalResource.Timber:return "Timber";case HistoricalNaturalResource.Salt:return "Salt";
                case HistoricalNaturalResource.Fertility:return "Soil fertility";case HistoricalNaturalResource.Freshwater:return "Fresh water";
                case HistoricalNaturalResource.Pasture:return "Natural pasture";default:throw new ArgumentOutOfRangeException(nameof(resource));
            }
        }
        static int Permille(double value)=>(int)Math.Round(D.Clamp(value)*1000,MidpointRounding.AwayFromZero);
        static double Field(V position,int seed,int domain,double scale)
        {
            var rng=new Rng(seed,Version,domain);int key=unchecked((int)rng.Next());
            return .5+.5*SphereNoise.Sample(position,key,scale);
        }
        static double Deposit(V position,int seed,int domain,double radiusScale,double threshold=.40)
        {
            double value=.68*Field(position,seed,domain,radiusScale*8)+.32*Field(position,seed,domain+100,radiusScale*36);
            double t=D.Clamp((value-threshold)/(1-threshold));return t*t*(3-2*t);
        }
        static double Geological(int[] values,int cell,double fallback)=>values!=null&&cell<values.Length?D.Clamp(values[cell]/1000.0):fallback;
        static double Discharge(double[] values,int cell)=>values!=null&&cell<values.Length?Math.Max(0,values[cell]):0;
        public static HistoricalResourcePotential At(World world,int cell)
        {
            if(world?.Surface?.Cells==null||cell<0||cell>=world.Surface.Cells.Length)throw new ArgumentOutOfRangeException(nameof(cell));
            if(world.Ecology?.Habitat==null||cell>=world.Ecology.Habitat.Length)throw new ArgumentException("Resource potential requires the saved habitat substrate.",nameof(world));
            var h=world.Ecology.Habitat[cell];var position=world.Surface.Cells[cell].Position;int seed=world.Recipe.Seed;
            double scale=Math.Max(.1,world.Recipe.RadiusKm/6371.0),relief=D.Clamp(h.ReliefM/1800.0);
            bool earth=world.Recipe.TerrainSource=="Earth",ocean=world.Hydrology?.OceanConnected!=null&&cell<world.Hydrology.OceanConnected.Length&&world.Hydrology.OceanConnected[cell];
            // Earth has measured relief but no measured bedrock/ore asset. Its
            // procedural plate scaffolding is NOT real Earth mineral evidence.
            // Deep geology remains explicitly synthetic on that map too.
            double basement=Field(position,seed,701,scale*5),sediments=Field(position,seed,702,scale*6);
            double craton=earth?basement:Geological(world.CratonPermille,cell,basement);
            double basin=earth?D.Clamp(.65*sediments+.35*(1-relief)):Geological(world.BasinPermille,cell,sediments);
            double fold=earth?D.Clamp(.6*relief+.4*Field(position,seed,703,scale*10)):Geological(world.FoldPermille,cell,relief);
            double magmatic=D.Clamp(.7*fold+.3*Field(position,seed,704,scale*12));
            double granite=D.Clamp(.45*craton+.55*fold);
            int copper=Permille((.06+.82*magmatic+.12*basin)*Deposit(position,seed,0,scale));
            int tin=Permille((.025+.975*granite)*Deposit(position,seed,1,scale,.48));
            int iron=Permille((.15+.68*craton+.17*basin)*Deposit(position,seed,2,scale,.32));
            int precious=Permille((.04+.61*fold+.35*craton)*Deposit(position,seed,3,scale,.47));
            // Coal and petroleum require ancient depositional/thermal history,
            // not today's tree cover, rainfall or the modern desert biome.
            double coalHistory=.25+.75*Field(position,seed,705,scale*9);
            double petroleumSystem=.15+.85*Field(position,seed,706,scale*7);
            int coal=Permille((.025+.975*basin)*coalHistory*Deposit(position,seed,4,scale,.38));
            int oil=Permille((.015+.985*basin)*petroleumSystem*Deposit(position,seed,5,scale,.40));
            int uranium=Permille((.08+.52*craton+.40*basin)*Deposit(position,seed,6,scale,.52));
            int stone=Permille(.20+.45*relief+.20*fold+.15*(h.Soil==SoilKind.Rock?1:.25));
            double forest=(h.Habitat&Habitat.Forest)!=0?1:h.Biome==Biome.MediterraneanWoodland?.5:0;
            int timber=Permille(forest*D.Clamp(h.ProductivityGCM2Year/950.0)*(.2+.8*D.Clamp(h.GrowingDays/250.0)));
            double salinity=D.Clamp(h.SalinityMilliPpt/35000.0);
            bool shore=false;var neighbors=world.Surface.Cells[cell].Neighbors;
            if(neighbors!=null&&world.Hydrology?.OceanConnected!=null)for(int k=0;k<Math.Min(8,neighbors.Length);k++)
            {int next=neighbors[k];if(next>=0&&next<world.Hydrology.OceanConnected.Length&&world.Hydrology.OceanConnected[next]){shore=true;break;}}
            double evaporite=basin*Deposit(position,seed,7,scale,.48);
            int salt=Permille(Math.Max(salinity,Math.Max(evaporite,(shore?.25:0)*(.2+.8*D.Clamp((900-h.AridityMilli)/900.0)))));
            int fertility=Permille(h.SoilFertilityPermille/1000.0*(1-.90*D.Clamp(h.SalinityMilliPpt/2500.0))*(1-h.PermafrostPermille/1000.0));
            var water=world.Hydrology;double q=water==null?0:(Discharge(water.NorthernSpringDischargeM3PerS,cell)+Discharge(water.NorthernSummerDischargeM3PerS,cell)+Discharge(water.NorthernAutumnDischargeM3PerS,cell)+Discharge(water.NorthernWinterDischargeM3PerS,cell))*.25;
            double river=D.Clamp(Math.Log(1+q)/Math.Log(1001));
            double groundwater=D.Clamp(1-h.WaterTableCm/1500.0),rainwater=D.Clamp(h.AridityMilli/1000.0);
            int freshwater=Permille(Math.Max(river,.70*rainwater+.30*groundwater)*(1-D.Clamp(h.SalinityMilliPpt/2500.0)));
            double open=(h.Habitat&Habitat.Open)!=0?1:h.Biome==Biome.Grassland||h.Biome==Biome.Savanna?1:forest>0?.08:.25;
            int pasture=Permille(open*D.Clamp(h.ProductivityGCM2Year/650.0)*D.Clamp(h.GrowingDays/220.0)*(1-h.PermafrostPermille/1000.0)*(1-D.Clamp(h.SalinityMilliPpt/2500.0)));
            if(ocean)
            {
                // Offshore hydrocarbons are retained on a continental-shelf
                // proxy; deep-ocean mineral mining is not represented here.
                double shelf=world.ElevationM!=null&&cell<world.ElevationM.Length?D.Clamp((world.ElevationM[cell]+3000)/2600.0):0;
                return new HistoricalResourcePotential(0,0,0,0,0,(int)(oil*shelf),0,0,0,1000,0,0,0,earth);
            }
            return new HistoricalResourcePotential(copper,tin,iron,precious,coal,oil,uranium,stone,timber,salt,fertility,freshwater,pasture,earth);
        }
    }
    // Optional execution-only FIFO over an immutable World. Recalculation and
    // eviction produce exactly the same values. No resource is spent or mined.
    public sealed class HistoricalNaturalResourceCache
    {
        readonly World world;readonly int capacity;
        readonly Dictionary<int,HistoricalResourcePotential> values=new Dictionary<int,HistoricalResourcePotential>();
        readonly Queue<int> order=new Queue<int>();
        public HistoricalNaturalResourceCache(World world,int capacity=16384)
        {this.world=world??throw new ArgumentNullException(nameof(world));if(capacity<1||capacity>65536)throw new ArgumentOutOfRangeException(nameof(capacity));this.capacity=capacity;}
        public int Count=>values.Count;
        public HistoricalResourcePotential At(int cell)
        {
            if(values.TryGetValue(cell,out var result))return result;result=HistoricalNaturalResources.At(world,cell);
            if(values.Count>=capacity)values.Remove(order.Dequeue());values.Add(cell,result);order.Enqueue(cell);return result;
        }
        public void Clear(){values.Clear();order.Clear();}
    }
}
