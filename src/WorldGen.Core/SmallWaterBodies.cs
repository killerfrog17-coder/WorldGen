using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    public readonly struct SmallWaterGround
    {
        public readonly double Height,Water;
        public readonly LocalWaterKind Kind;
        public readonly LandscapeControls Province;
        public SmallWaterGround(double height,double water,LocalWaterKind kind,LandscapeControls province)
        {Height=height;Water=water;Kind=kind;Province=province;}
    }
    public sealed class SmallLake
    {
        public V Center,East,North;
        public double Radius,Aspect,Heading,Level,Depth,Phase;
        public ulong Id;
        public double NormalizedRadius(V position,double planetRadius)
        {
            V delta=position-Center;double x=(delta.X*East.X+delta.Y*East.Y+delta.Z*East.Z)*planetRadius;
            double z=(delta.X*North.X+delta.Y*North.Y+delta.Z*North.Z)*planetRadius;
            double c=Math.Cos(Heading),s=Math.Sin(Heading),a=(x*c-z*s)/Aspect,b=x*s+z*c;
            double angle=Math.Atan2(b,a),edge=1+.07*Math.Sin(angle*3+Phase)+.04*Math.Cos(angle*7-Phase);
            return Math.Sqrt(a*a+b*b)/(Radius*edge);
        }
        public double Bed(double original,double radius)
        {
            if(radius>=1.4)return original;
            double blend=1-OrogenMorphology.Smooth(.82,1.4,radius),bowl=Level-Depth*(1-radius*radius);
            return original+(Math.Min(original,bowl)-original)*blend;
        }
    }

    /// <summary>Bounded, sparse derived depressions. Candidate addresses are fixed
    /// physical 3D grid cells on the planet shell, independent of render chunks,
    /// cell resolution and query order. Only visited candidates are surveyed.</summary>
    public sealed class SmallWaterBodies
    {
        public const string Version="small-enclosed-basins-1";
        public const double GridM=768;
        public static bool IsLocal(ulong id)=>(id&0x8000000000000000UL)!=0;
        readonly int seed;readonly double planetRadius;
        readonly object gate=new object();
        readonly Dictionary<(int,int,int),SmallLake> cache=new Dictionary<(int,int,int),SmallLake>();
        readonly Queue<(int,int,int)> order=new Queue<(int,int,int)>();
        struct Candidate{public (int,int,int) Key;public uint Hash;public V Center;public double Radius,Support2;}
        readonly Dictionary<(int,int,int),Candidate[]> neighbourhoods=new Dictionary<(int,int,int),Candidate[]>();
        readonly Queue<(int,int,int)> neighbourhoodOrder=new Queue<(int,int,int)>();
        public int CachedCandidates{get{lock(gate)return cache.Count;}}
        public SmallWaterBodies(int seed,double planetRadius){this.seed=seed;this.planetRadius=planetRadius;}
        static uint Hash(int x,int y,int z,int seed)=>Rng.Mix(unchecked((uint)seed^(uint)x*73856093u^(uint)y*19349663u^(uint)z*83492791u));
        static double Unit(uint hash,int stream)=>Rng.Mix(hash^unchecked((uint)stream*0x9e3779b9u))/(double)uint.MaxValue;
        public V[] Candidates(V position,double rangeM)
        {
            if(!double.IsFinite(rangeM)||rangeM<0||rangeM>10000)throw new ArgumentOutOfRangeException(nameof(rangeM));
            var result=new List<V>();V m=position*planetRadius;int reach=(int)Math.Ceiling(rangeM/GridM)+1;
            int ix=(int)Math.Floor(m.X/GridM),iy=(int)Math.Floor(m.Y/GridM),iz=(int)Math.Floor(m.Z/GridM);
            for(int z=iz-reach;z<=iz+reach;z++)for(int y=iy-reach;y<=iy+reach;y++)for(int x=ix-reach;x<=ix+reach;x++)
            {
                uint hash=Hash(x,y,z,seed);if(Unit(hash,1)>.7)continue;
                var raw=new V((x+.5)*GridM,(y+.5)*GridM,(z+.5)*GridM);if(Math.Abs(raw.Length-planetRadius)>GridM*.56)continue;
                var center=raw.Unit;if((center-position).Length*planetRadius<=rangeM)result.Add(center);
            }
            return result.ToArray();
        }
        SmallLake Survey(V center,uint hash,double radius,Func<V,SmallWaterGround> ground)
        {
            var g=ground(center);var p=g.Province;
            if(g.Kind!=LocalWaterKind.None||g.Height<4||p.Salt>.1||p.Moisture<.18)return null;
            double probability=Math.Max(p.Wet*.8,Math.Max(p.Glacial*.55,p.Moisture*.045));
            if(Unit(hash,6)>probability)return null;
            var east=V.Cross(Math.Abs(center.Z)<.99?new V(0,0,1):new V(0,1,0),center).Unit;
            var north=V.Cross(center,east).Unit;double min=g.Height,max=g.Height;
            double aspect=.72+.48*Unit(hash,3),heading=Unit(hash,4)*Math.PI*2,extent=radius*1.55;
            for(int i=0;i<16;i++)
            {
                double a=i*Math.PI/8,x=Math.Cos(a)*extent*aspect,z=Math.Sin(a)*extent,c=Math.Cos(heading),s=Math.Sin(heading);
                var point=(center+east*((x*c+z*s)/planetRadius)+north*((-x*s+z*c)/planetRadius)).Unit;
                var rim=ground(point);if(rim.Kind!=LocalWaterKind.None||rim.Height<4)return null;
                min=Math.Min(min,rim.Height);max=Math.Max(max,rim.Height);
            }
            // A surveyed gentle enclosed collar is required. Never put an
            // independent fixed water plane on a mountain slope or river mouth.
            if(max-min>Math.Max(1.3,radius*.045))return null;
            ulong identity=0x8000000000000000UL|((ulong)hash<<31)|Hash((int)(center.X*1000000),(int)(center.Y*1000000),(int)(center.Z*1000000),seed^0x35719);
            return new SmallLake{Center=center,East=east,North=north,Radius=radius,Aspect=aspect,Heading=heading,
                Level=min-.65,Depth=1.1+Unit(hash,7)*Math.Min(5,radius*.065),Phase=Unit(hash,8)*Math.PI*2,Id=identity};
        }
        public SmallLake At(V position,Func<V,SmallWaterGround> ground)
        {
            V metres=position*planetRadius;int ix=(int)Math.Floor(metres.X/GridM),iy=(int)Math.Floor(metres.Y/GridM),iz=(int)Math.Floor(metres.Z/GridM);
            var address=(ix,iy,iz);Candidate[] neighbours;lock(gate)neighbourhoods.TryGetValue(address,out neighbours);
            if(neighbours==null)
            {
                var list=new List<Candidate>();
                for(int z=iz-1;z<=iz+1;z++)for(int y=iy-1;y<=iy+1;y++)for(int x=ix-1;x<=ix+1;x++)
                {
                    uint hash=Hash(x,y,z,seed);if(Unit(hash,1)>.7)continue;
                    var raw=new V((x+.5)*GridM,(y+.5)*GridM,(z+.5)*GridM);if(Math.Abs(raw.Length-planetRadius)>GridM*.56)continue;
                    double radius=12+78*Math.Pow(Unit(hash,2),1.5),support=radius*1.4*1.2*1.12;
                    list.Add(new Candidate{Key=(x,y,z),Hash=hash,Center=raw.Unit,Radius=radius,Support2=support*support});
                }
                neighbours=list.ToArray();lock(gate)
                {if(neighbourhoods.TryGetValue(address,out var previous))neighbours=previous;else{if(neighbourhoodOrder.Count>=512)neighbourhoods.Remove(neighbourhoodOrder.Dequeue());neighbourhoods.Add(address,neighbours);neighbourhoodOrder.Enqueue(address);}}
            }
            SmallLake best=null;double nearest=double.PositiveInfinity;
            foreach(var candidate in neighbours)
            {
                double dx=(position.X-candidate.Center.X)*planetRadius,dy=(position.Y-candidate.Center.Y)*planetRadius,dz=(position.Z-candidate.Center.Z)*planetRadius;
                if(dx*dx+dy*dy+dz*dz>candidate.Support2)continue;
                var key=candidate.Key;SmallLake lake;bool found;lock(gate)found=cache.TryGetValue(key,out lake);
                if(!found)
                {
                    lake=Survey(candidate.Center,candidate.Hash,candidate.Radius,ground);
                    lock(gate)
                    {
                        if(cache.TryGetValue(key,out var previous))lake=previous;
                        else{if(order.Count>=4096)cache.Remove(order.Dequeue());cache.Add(key,lake);order.Enqueue(key);}
                    }
                }
                if(lake==null)continue;double r=lake.NormalizedRadius(position,planetRadius);
                if(r<1.4&&(r<nearest||r==nearest&&lake.Id<best.Id)){best=lake;nearest=r;}
            }
            return best;
        }
    }
}
