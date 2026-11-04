using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>Broad incised valleys in dry uplifted sedimentary provinces.
    /// A bounded local plateau overprint supplies uplift, while existing
    /// drainage owns the incised floor datum. No extra world entities.</summary>
    public sealed class CanyonValleys
    {
        public sealed class Reach
        {
            public int Source;public V A,B;
            public double LevelA,LevelB,HalfWidth,Relief,Strength;
            public double Uplift(double distance)
                =>Relief*.7*Math.Sqrt(Strength)*(1-OrogenMorphology.Smooth(HalfWidth*9,HalfWidth*12,distance));
            public double Floor(double distance,double level)
            {
                double r=D.Clamp(distance/HalfWidth),t=Math.Max(0,(r-.08)/.92);
                double h=level+3+Relief*Math.Pow(t,1.55),q=h/28,b=Math.Floor(q),f=q-b;
                // Resistant beds form short cliffs; weaker intervals form
                // benches. Bedding remains horizontal across neighbouring cuts.
                double terraced=28*(b+RiverscapeKernel.Smooth((f-.28)/.34));
                return Math.Max(level+1,terraced);
            }
            public double Cut(double ground,double distance,double level,out double exposure)
            {
                exposure=0;double r=distance/HalfWidth;if(r>=1)return ground;
                double weight=Strength*RiverscapeKernel.Smooth((1-r)/.20);
                double cut=Math.Min(0,Floor(distance,level)-ground)*weight;
                exposure=OrogenMorphology.Smooth(3,45,-cut)*OrogenMorphology.Smooth(.08,.35,r)*(1-OrogenMorphology.Smooth(.75,1,r));
                return ground+cut;
            }
        }
        readonly double radius;readonly int[][] candidates;
        readonly int seed;
        public readonly Reach[] Reaches;
        public int IndexedFaces {get;}
        static double Dot(V a,V b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        public CanyonValleys(World world,LocalTerrainProfile[] profiles,double scale,double[] levels,double[] widths,CancellationToken token=default)
        {
            radius=world.Recipe.RadiusKm*1000*scale;seed=world.Recipe.Seed;var reaches=new List<Reach>();var cells=new Dictionary<int,List<int>>();double margin=0;
            foreach(var p in profiles)margin=Math.Max(margin,p.SupportM*scale);
            for(int source=0;source<profiles.Length;source++)
            {
                if((source&4095)==0)token.ThrowIfCancellationRequested();
                int next=world.Hydrology.Receiver[source];var p=profiles[source];
                if(next<0||world.Hydrology.OceanConnected[source]||world.Hydrology.OceanConnected[next]||widths[source]<6||levels[source]<150||p.ElevationM<400||p.Glacial>.1)continue;
                double sedimentary=p.Ground.RockWeight(2)+p.Ground.RockWeight(3)+p.Ground.RockWeight(4);
                if(sedimentary<.5||p.Plateau<.25||p.Landscape.Arid<.2)continue;
                V a=world.Surface.Cells[source].Position,b=world.Surface.Cells[next].Position,center=(a+b).Unit;
                double province=OrogenMorphology.Smooth(.40,.65,.5+.5*SphereNoise.Sample(center*radius,world.Recipe.Seed^0x63c417,1/35000d));
                double strength=OrogenMorphology.Smooth(.25,.65,p.Plateau)*sedimentary*OrogenMorphology.Smooth(.2,.65,p.Landscape.Arid)*province;
                if(strength<.04)continue;
                // Admission is rare; a well admitted province should display
                // an incised plateau rather than a barely visible dent. Keep
                // the weakest margins gradual instead of scaling every cliff
                // down to a shallow grassland valley.
                strength=OrogenMorphology.Smooth(.04,.36,strength);
                double relief=Math.Min(1200,100+.7*p.CoarseReliefM),width=Math.Min(2800,Math.Max(400,.9*relief+widths[source]*16));
                var reach=new Reach{Source=source,A=a,B=b,LevelA=levels[source],LevelB=levels[next],HalfWidth=width,Relief=relief,Strength=strength};
                int index=reaches.Count;reaches.Add(reach);
                double support=(a-b).Length*radius*.5+width*12+margin;
                var queue=new Queue<int>();var visited=new HashSet<int>();queue.Enqueue(source);visited.Add(source);
                while(queue.Count>0)
                {
                    int cell=queue.Dequeue();if((world.Surface.Cells[cell].Position-center).Length*radius>support)continue;
                    if(!cells.TryGetValue(cell,out var list)){list=new List<int>();cells.Add(cell,list);}list.Add(index);
                    foreach(int adjacent in world.Surface.Cells[cell].Neighbors)if(visited.Add(adjacent))queue.Enqueue(adjacent);
                }
            }
            Reaches=reaches.ToArray();candidates=new int[world.Surface.Triangles.Length/3][];
            for(int face=0;face<candidates.Length;face++)
            {
                if((face&8191)==0)token.ThrowIfCancellationRequested();List<int> list=null;
                for(int k=0;k<3;k++)if(cells.TryGetValue(world.Surface.Triangles[face*3+k],out var found))
                    foreach(int index in found){if(list==null)list=new List<int>();if(!list.Contains(index))list.Add(index);}
                if(list!=null){list.Sort();candidates[face]=list.ToArray();IndexedFaces++;}
            }
        }
        public static double ErodedDistance(V metres,int seed,double distance,double halfWidth)
        {
            if(distance>=halfWidth*1.6)return distance;
            // Connected ridges in the noise field carve short tributary-like
            // gullies into the walls, while leaving the drainage datum intact.
            // Broad shoulders and finer ravines break continuous railway-like
            // benches. The same coordinates are used across reaches and tiles.
            double broad=SphereNoise.Sample(metres,seed^0x45c731,1/1300d);
            double ridge=1-Math.Abs(SphereNoise.Sample(metres,seed^0x71c315,1/550d)
                +.25*SphereNoise.Sample(metres,seed^0x137c51,1/160d));
            double gully=OrogenMorphology.Smooth(.25,.90,ridge);
            double shoulder=1+.14*broad;
            return distance*Math.Max(.56,shoulder-.32*gully*gully*gully);
        }
        public double Apply(V point,int face,double ground,out double exposure)
        {
            exposure=0;var found=candidates[face];if(found==null)return ground;
            double uplift=0;
            foreach(int index in found)
            {
                var reach=Reaches[index];V d=reach.B-reach.A,p=point-reach.A;
                double u=D.Clamp(Dot(p,d)/Math.Max(1e-30,Dot(d,d)));
                uplift=Math.Max(uplift,reach.Uplift((p-d*u).Length*radius));
            }
            ground+=uplift;
            foreach(int index in found)
            {
                var reach=Reaches[index];V d=reach.B-reach.A,p=point-reach.A;
                double raw=Dot(p,d)/Math.Max(1e-30,Dot(d,d)),u=D.Clamp(raw),distance=(p-d*u).Length*radius;
                // Extrapolate the grade through round end caps; a clamped low
                // downstream level must not undercut the upstream river bank.
                double level=reach.LevelA+(reach.LevelB-reach.LevelA)*raw;
                double eroded=ErodedDistance(point*radius,seed,distance,reach.HalfWidth);
                ground=reach.Cut(ground,eroded,level,out double bare);exposure=Math.Max(exposure,bare);
            }
            return ground;
        }
    }
}
