using System;

namespace WorldGen.Core
{
    /// <summary>Source exposure proxies, not a claim that wildlife or sediment
    /// automatically contains human-infectious organisms. Waste/protection are
    /// hooks for future sanitation and genuinely protected groundwater sources.</summary>
    public readonly struct WaterSourceEnvironment
    {
        public readonly double AnimalUse,HumanWaste,Runoff,Protection;
        public WaterSourceEnvironment(double animalUse=0,double humanWaste=0,double runoff=0,double protection=0)
        {AnimalUse=D.Clamp(animalUse);HumanWaste=D.Clamp(humanWaste);Runoff=D.Clamp(runoff);Protection=D.Clamp(protection);}
    }

    /// <summary>Random-access, persistent 300m source patches with multi-day
    /// contamination episodes. No visitation-dependent RNG or world-wide ticks.
    /// Values are calibrated gameplay hazards per litre, NOT pathogen counts
    /// or measured infection rates. Turbidity is deliberately not an input.</summary>
    public static class WaterContamination
    {
        public const string Version="source-water-1";
        public const double PatchM=300;
        public static double CatchmentAnimalUse(World world,int cell)
        {
            // A bounded dominant-upstream walk supplies a background wildlife
            // exposure prior even when no nearby models have streamed in.
            if(world?.Ecology==null||cell<0||cell>=world.Ecology.Habitat.Length)return 0;
            double use=0,total=0,weight=1;int previous=-1;
            for(int step=0;step<5;step++)
            {
                var h=world.Ecology.Habitat[cell];
                if(h!=null){use+=weight*(D.Clamp(h.ProductivityGCM2Year/2200d)*.35+((h.Habitat&Habitat.Wetland)!=0?.12:0));total+=weight;}
                if(cell>=world.Surface.Cells.Length||world.Surface.Cells[cell].Neighbors==null)break;
                int upstream=-1;long area=-1;
                foreach(int n in world.Surface.Cells[cell].Neighbors)
                    if(n!=previous&&n!=cell&&n>=0&&n<world.Hydrology.Receiver.Length&&n<world.Ecology.Habitat.Length&&world.Hydrology.Receiver[n]==cell)
                    {long a=n<world.Hydrology.ContributingAreaM2.Length?world.Hydrology.ContributingAreaM2[n]:0;if(a>area){area=a;upstream=n;}}
                if(upstream<0)break;previous=cell;cell=upstream;weight*=.55;
            }
            return total>0?D.Clamp(use/total):0;
        }
        static uint Key(ulong source,int x,int y,int z)
            =>Rng.Mix((uint)source^Rng.Mix((uint)(source>>32))^Rng.Mix(unchecked((uint)x))^
                Rng.Mix(unchecked((uint)y)+0x9e3779b9u)^Rng.Mix(unchecked((uint)z)+0x85ebca6bu));
        static double Value(int seed,uint key,long episode,int stream)
            =>Rng.Value(unchecked(seed^(int)Rng.Mix(key^(uint)stream)),"source-water",unchecked((int)Rng.Mix((uint)episode^Rng.Mix((uint)(episode>>32)))));
        static double Node(int seed,uint key,double day,WaterSourceEnvironment e)
        {
            double character=Value(seed,key,0,1);
            double chronic=character<.85?0:character<.97?.0002+(character-.85)/.12*.0018:
                character<.995?.006+(character-.97)/.025*.034:.12+(character-.995)/.005*.38;
            double load=chronic*(1+e.AnimalUse*2+e.Runoff*1.5)+.1*e.HumanWaste*e.HumanWaste;
            double phase=Value(seed,key,0,2)*4,clock=day+phase;
            long slot=(long)Math.Floor(clock/4);
            // Evaluate overlapping smooth pulses; no midnight/slot-boundary jump.
            for(long k=slot-1;k<=slot;k++)
            {
                double probability=.012+e.AnimalUse*.07+e.HumanWaste*.3;
                if(Value(seed,key,k,3)>=probability)continue;
                double age=clock-k*4,duration=1+2*Value(seed,key,k,4);
                if(age<=0||age>=duration)continue;
                double pulse=Math.Sin(Math.PI*age/duration);pulse*=pulse;
                load+=pulse*(.012+.04*Value(seed,key,k,5))*(1+e.AnimalUse*3+e.HumanWaste*8)*(1+e.Runoff*3);
            }
            return load*(1-.98*e.Protection);
        }
        public static double Hazard(int seed,ulong source,V position,double radiusM,double solarDays,WaterSourceEnvironment environment)
        {
            if(!double.IsFinite(radiusM)||radiusM<=0||!double.IsFinite(solarDays)||solarDays<0||!double.IsFinite(position.Length))throw new ArgumentException("Invalid water source location or time.");
            double px=position.X*radiusM/PatchM,py=position.Y*radiusM/PatchM,pz=position.Z*radiusM/PatchM;
            int x=(int)Math.Floor(px),y=(int)Math.Floor(py),z=(int)Math.Floor(pz);
            double Smooth(double t)=>t*t*(3-2*t);
            double u=Smooth(px-x),v=Smooth(py-y),w=Smooth(pz-z),result=0;
            for(int dx=0;dx<=1;dx++)for(int dy=0;dy<=1;dy++)for(int dz=0;dz<=1;dz++)
                result+=Node(seed,Key(source,x+dx,y+dy,z+dz),solarDays,environment)*(dx==0?1-u:u)*(dy==0?1-v:v)*(dz==0?1-w:w);
            return D.Clamp(result);
        }
        public static WaterQuality Apply(WaterQuality water,int seed,ulong source,V position,double radiusM,double solarDays,WaterSourceEnvironment environment)
            =>water.Kind==LocalWaterKind.None?water:water.WithContamination(Hazard(seed,source^((ulong)water.Kind<<60),position,radiusM,solarDays,environment));
    }
}
