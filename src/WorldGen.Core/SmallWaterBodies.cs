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
        public bool Complex;
        public double BedExtent=>Complex?1.3:1;
        public double NormalizedRadius(V position,double planetRadius)
        {
            V delta=position-Center;double x=(delta.X*East.X+delta.Y*East.Y+delta.Z*East.Z)*planetRadius;
            double z=(delta.X*North.X+delta.Y*North.Y+delta.Z*North.Z)*planetRadius;
            double c=Math.Cos(Heading),s=Math.Sin(Heading),a=(x*c-z*s)/Aspect,b=x*s+z*c;
            double angle=Math.Atan2(b,a),edge=1+(Complex?.115:.07)*Math.Sin(angle*3+Phase)+(Complex?.065:.04)*Math.Cos(angle*7-Phase);
            return Math.Sqrt(a*a+b*b)/(Radius*edge);
        }
        public double Bed(double original,double radius,V metres)
        {
            if(Complex)
            {
                if(radius>=1.3)return original;
                int key=unchecked((int)((uint)Id^(uint)(Id>>32)));
                double pattern=LandscapeNoise.Value(metres,key,1/55d)+.23*LandscapeNoise.Value(metres,key^0x31b47,1/22d);
                double hummock=OrogenMorphology.Smooth(.10,.48,Math.Abs(pattern));
                double target=Level-Depth+(Depth+.3)*hummock;
                // Retain inherited higher ground. The water table fills low
                // hollows; it must not excavate an entire sloping forest into
                // a deep, flat-bottomed artificial pond.
                target=Math.Max(original-1.2,target);
                double fade=1-OrogenMorphology.Smooth(.8,1.3,radius);
                return original+(Math.Min(original,target)-original)*fade;
            }
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
        public const string Version="small-wetland-complexes-4";
        public const double GridM=768;
        public static bool IsLocal(ulong id)=>(id&0x8000000000000000UL)!=0;
        readonly int seed;readonly double planetRadius,grid;readonly bool wetland;readonly SmallWaterBodies exclusion;
        readonly Func<V,bool> complexRegion;
        readonly object gate=new object();
        readonly Dictionary<(int,int,int),SmallLake> cache=new Dictionary<(int,int,int),SmallLake>();
        readonly Queue<(int,int,int)> order=new Queue<(int,int,int)>();
        struct Candidate{public (int,int,int) Key;public uint Hash;public V Center;public double Radius,Support2;public bool Complex;}
        readonly Dictionary<(int,int,int),Candidate> descriptors=new Dictionary<(int,int,int),Candidate>();
        readonly Queue<(int,int,int)> descriptorOrder=new Queue<(int,int,int)>();
        readonly Dictionary<(int,int,int),Candidate[]> neighbourhoods=new Dictionary<(int,int,int),Candidate[]>();
        readonly Queue<(int,int,int)> neighbourhoodOrder=new Queue<(int,int,int)>();
        public int CachedCandidates{get{lock(gate)return cache.Count;}}
        public SmallWaterBodies(int seed,double planetRadius,bool wetland=false,SmallWaterBodies exclusion=null,Func<V,bool> complexRegion=null)
        {this.seed=seed;this.planetRadius=planetRadius;this.wetland=wetland;this.exclusion=exclusion;this.complexRegion=complexRegion;grid=wetland?128:GridM;}
        static uint Hash(int x,int y,int z,int seed)=>Rng.Mix(unchecked((uint)seed^(uint)x*73856093u^(uint)y*19349663u^(uint)z*83492791u));
        static double Unit(uint hash,int stream)=>Rng.Mix(hash^unchecked((uint)stream*0x9e3779b9u))/(double)uint.MaxValue;
        bool CandidateAt(int x,int y,int z,out Candidate candidate)
        {
            candidate=default;uint hash=Hash(x,y,z,seed);if(Unit(hash,1)>.7)return false;
            var raw=new V((x+.5)*grid,(y+.5)*grid,(z+.5)*grid);if(Math.Abs(raw.Length-planetRadius)>grid*.56)return false;
            var key=(x,y,z);
            if(complexRegion!=null){lock(gate)if(descriptors.TryGetValue(key,out candidate))return true;}
            V center=raw.Unit;bool complex=complexRegion!=null&&complexRegion(center);
            double radius=complex?120+160*Unit(hash,2):wetland?12+36*Unit(hash,2):12+78*Math.Pow(Unit(hash,2),1.5),support=radius*1.4*1.2*1.12;
            candidate=new Candidate{Key=key,Hash=hash,Center=center,Radius=radius,Support2=support*support,Complex=complex};
            if(complexRegion!=null)lock(gate)
            {
                if(descriptors.TryGetValue(key,out var previous))candidate=previous;
                else{if(descriptorOrder.Count>=8192)descriptors.Remove(descriptorOrder.Dequeue());descriptors.Add(key,candidate);descriptorOrder.Enqueue(key);}
            }
            return true;
        }
        bool Separated(Candidate candidate)
        {
            var key=candidate.Key;
            // Two radial shell cells can project onto almost the same place.
            // Deterministic priority thinning keeps only one whole basin and
            // guarantees disjoint bed collars, rather than intersecting planes.
            for(int z=key.Item3-1;z<=key.Item3+1;z++)for(int y=key.Item2-1;y<=key.Item2+1;y++)for(int x=key.Item1-1;x<=key.Item1+1;x++)
            {
                if((x,y,z)==key||!CandidateAt(x,y,z,out var other))continue;
                bool earlier=other.Hash<candidate.Hash||other.Hash==candidate.Hash&&(x<key.Item1||x==key.Item1&&(y<key.Item2||y==key.Item2&&z<key.Item3));
                if(!earlier)continue;
                double dx=(other.Center.X-candidate.Center.X)*planetRadius,dy=(other.Center.Y-candidate.Center.Y)*planetRadius,dz=(other.Center.Z-candidate.Center.Z)*planetRadius;
                double clearance=(other.Radius+candidate.Radius)*1.9;
                if(dx*dx+dy*dy+dz*dz<clearance*clearance)return false;
            }
            return true;
        }
        public V[] Candidates(V position,double rangeM)
        {
            if(!double.IsFinite(rangeM)||rangeM<0||rangeM>10000)throw new ArgumentOutOfRangeException(nameof(rangeM));
            var result=new List<V>();V m=position*planetRadius;int reach=(int)Math.Ceiling(rangeM/grid)+1;
            int ix=(int)Math.Floor(m.X/grid),iy=(int)Math.Floor(m.Y/grid),iz=(int)Math.Floor(m.Z/grid);
            for(int z=iz-reach;z<=iz+reach;z++)for(int y=iy-reach;y<=iy+reach;y++)for(int x=ix-reach;x<=ix+reach;x++)
            {
                if(!CandidateAt(x,y,z,out var candidate)||!Separated(candidate))continue;
                if((candidate.Center-position).Length*planetRadius<=rangeM)result.Add(candidate.Center);
            }
            return result.ToArray();
        }
        SmallLake Survey(V center,uint hash,double radius,bool complex,Func<V,SmallWaterGround> ground)
        {
            if(exclusion!=null&&exclusion.OverlapsCandidate(center,radius))return null;
            var g=ground(center);var p=g.Province;
            if(g.Kind!=LocalWaterKind.None||g.Height<4||p.Salt>.1||p.Moisture<.18||(wetland||complex)&&p.Wet<.5)return null;
            double probability=Math.Max(p.Wet*.8,Math.Max(Math.Max(p.Glacial,p.Frozen)*.55,p.Moisture*.045));
            if(Unit(hash,6)>probability)return null;
            var east=V.Cross(Math.Abs(center.Z)<.99?new V(0,0,1):new V(0,1,0),center).Unit;
            var north=V.Cross(center,east).Unit;double min=g.Height,max=g.Height,innerMin=g.Height;
            double aspect=.72+.48*Unit(hash,3),heading=Unit(hash,4)*Math.PI*2,extent=radius*1.55;
            int rimSamples=complex?64:16;
            for(int i=0;i<rimSamples;i++)
            {
                double a=i*Math.PI*2/rimSamples,x=Math.Cos(a)*extent*aspect,z=Math.Sin(a)*extent,c=Math.Cos(heading),s=Math.Sin(heading);
                var point=(center+east*((x*c+z*s)/planetRadius)+north*((-x*s+z*c)/planetRadius)).Unit;
                var rim=ground(point);if(rim.Kind!=LocalWaterKind.None||rim.Height<4)return null;
                min=Math.Min(min,rim.Height);max=Math.Max(max,rim.Height);
            }
            // A surveyed gentle enclosed collar is required. Never put an
            // independent fixed water plane on a mountain slope or river mouth.
            if(max-min>Math.Max(1.3,radius*(complex?.04:wetland?.08:.045)))return null;
            if(complex)
            {
                for(int i=0;i<32;i++)
                {
                    double a=i*Math.PI/8,r=i<16?.4:.85,x=Math.Cos(a)*radius*r*aspect,z=Math.Sin(a)*radius*r,c=Math.Cos(heading),s=Math.Sin(heading);
                    var point=(center+east*((x*c+z*s)/planetRadius)+north*((-x*s+z*c)/planetRadius)).Unit;
                    var inside=ground(point);if(inside.Kind!=LocalWaterKind.None||inside.Height<4)return null;
                    innerMin=Math.Min(innerMin,inside.Height);
                }
                // At least one interior hollow must be reachable with the
                // bounded 1.2m incision. A plain downhill patch is not a basin.
                if(innerMin>min+.4)return null;
            }
            ulong identity=0x8000000000000000UL|((ulong)hash<<31)|Hash((int)(center.X*1000000),(int)(center.Y*1000000),(int)(center.Z*1000000),seed^0x35719);
            return new SmallLake{Center=center,East=east,North=north,Radius=radius,Aspect=aspect,Heading=heading,
                Level=min-(complex?.45:wetland?.1:.65),Depth=complex?.25+.5*Unit(hash,7):wetland?.18+.67*Unit(hash,7):1.1+Unit(hash,7)*Math.Min(5,radius*.065),Phase=Unit(hash,8)*Math.PI*2,Id=identity,Complex=complex};
        }
        bool OverlapsCandidate(V center,double radius)
        {
            V m=center*planetRadius;int ix=(int)Math.Floor(m.X/grid),iy=(int)Math.Floor(m.Y/grid),iz=(int)Math.Floor(m.Z/grid);
            for(int z=iz-1;z<=iz+1;z++)for(int y=iy-1;y<=iy+1;y++)for(int x=ix-1;x<=ix+1;x++)
            {
                if(!CandidateAt(x,y,z,out var other))continue;
                double clearance=(radius+other.Radius)*1.9;
                if((center-other.Center).Length*planetRadius<clearance)return true;
            }
            return false;
        }
        public SmallLake At(V position,Func<V,SmallWaterGround> ground)
        {
            V metres=position*planetRadius;int ix=(int)Math.Floor(metres.X/grid),iy=(int)Math.Floor(metres.Y/grid),iz=(int)Math.Floor(metres.Z/grid);
            var address=(ix,iy,iz);Candidate[] neighbours;lock(gate)neighbourhoods.TryGetValue(address,out neighbours);
            if(neighbours==null)
            {
                var list=new List<Candidate>();
                for(int z=iz-1;z<=iz+1;z++)for(int y=iy-1;y<=iy+1;y++)for(int x=ix-1;x<=ix+1;x++)
                {
                    if(CandidateAt(x,y,z,out var candidate)&&Separated(candidate))list.Add(candidate);
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
                    lake=Survey(candidate.Center,candidate.Hash,candidate.Radius,candidate.Complex,ground);
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
