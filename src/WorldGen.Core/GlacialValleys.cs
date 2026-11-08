using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>Inherited glacial troughs along existing mountain drainage.
    /// Static indexed terrain primitives; no per-frame or historical glacier tick.
    /// River fitting sees the same eroded floor as terrain and collision.</summary>
    public sealed class GlacialValleys
    {
        public readonly struct Reach
        {
            public readonly int Source,Target;
            public readonly V A,B;
            public readonly double HalfWidth,Relief,Strength;
            public Reach(int source,int target,V a,V b,double width,double relief,double strength)
            {Source=source;Target=target;A=a;B=b;HalfWidth=width;Relief=relief;Strength=strength;}
            public double Floor(double distance,double level)
            {
                // Broad, gently inclined sediment floor bounded by steep sides.
                // The river retains its own narrower channel and alluvial banks.
                double r=Math.Max(0,distance/HalfWidth),side=Math.Max(0,(r-.28)/.72);
                return Math.Max(30,level)+3+Relief*(.025*r*r+.975*side*side*side);
            }
            public double Cut(double ground,double distance,double level,out double exposure)
            {
                exposure=0;double r=distance/HalfWidth;if(r>=1)return ground;
                double fade=RiverscapeKernel.Smooth((1-r)/.18);
                double cut=Math.Min(0,Floor(distance,level)-ground)*Strength*fade;
                exposure=OrogenMorphology.Smooth(8,65,-cut)*OrogenMorphology.Smooth(.28,.60,r)*(1-OrogenMorphology.Smooth(.82,1,r));
                return ground+cut;
            }
        }
        readonly double radius;
        readonly double[] levels;
        // Numeric compressed rows avoid a planet-sized managed reference array
        // and one tiny managed allocation per indexed face.
        readonly int[] offsets,indices;
        public readonly Reach[] Reaches;
        public int IndexedFaces {get;}
        public int IndexBytes=>sizeof(int)*(offsets.Length+indices.Length);
        public int MaximumCandidates {get;}
        public int CandidateCount(int face)=>offsets[face+1]-offsets[face];
        static double Dot(V a,V b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        public GlacialValleys(World world,LocalTerrainProfile[] profiles,double scale,double[] riverLevels,double[] widths,CancellationToken token=default)
        {
            radius=world.Recipe.RadiusKm*1000*scale;levels=riverLevels;
            var reaches=new List<Reach>();var cells=new Dictionary<int,List<int>>();double margin=0;
            foreach(var p in profiles)margin=Math.Max(margin,p.SupportM*scale);
            for(int source=0;source<profiles.Length;source++)
            {
                if((source&2047)==0)token.ThrowIfCancellationRequested();
                int next=world.Hydrology.Receiver[source];var p=profiles[source];
                if(next<0||world.Hydrology.OceanConnected[source]||world.Hydrology.OceanConnected[next]||widths[source]<6||levels[source]<150||levels[next]<80||p.ElevationM<300)continue;
                // Require mapped glacial erosion and genuine mountain relief.
                // Cold flat tundra, tropical hills and every small creek do not
                // acquire a scenic U-shaped valley merely from their latitude.
                double glacial=Math.Min(p.Glacial,profiles[next].Glacial);
                double strength=OrogenMorphology.Smooth(.2,.85,glacial)*OrogenMorphology.Smooth(250,800,p.CoarseReliefM);
                if(strength<.08)continue;
                double relief=Math.Min(950,Math.Max(120,p.CoarseReliefM*.65));
                double halfWidth=Math.Min(1900,Math.Max(380,180+relief*.65+widths[source]*10));
                V a=world.Surface.Cells[source].Position,b=world.Surface.Cells[next].Position,center=(a+b).Unit;
                int index=reaches.Count;reaches.Add(new Reach(source,next,a,b,halfWidth,relief,strength));
                double support=(a-b).Length*radius*.5+halfWidth+margin;
                var queue=new Queue<int>();var visited=new HashSet<int>();queue.Enqueue(source);visited.Add(source);
                while(queue.Count>0)
                {
                    int cell=queue.Dequeue();if((world.Surface.Cells[cell].Position-center).Length*radius>support)continue;
                    if(!cells.TryGetValue(cell,out var list)){list=new List<int>();cells.Add(cell,list);}list.Add(index);
                    foreach(int adjacent in world.Surface.Cells[cell].Neighbors)if(visited.Add(adjacent))queue.Enqueue(adjacent);
                }
            }
            Reaches=reaches.ToArray();int faces=world.Surface.Triangles.Length/3;offsets=new int[faces+1];
            var selected=new List<int>();var packed=new List<int>();
            for(int face=0;face<faces;face++)
            {
                if((face&8191)==0)token.ThrowIfCancellationRequested();selected.Clear();
                for(int k=0;k<3;k++)if(cells.TryGetValue(world.Surface.Triangles[face*3+k],out var found))
                    foreach(int index in found)if(!selected.Contains(index))selected.Add(index);
                if(selected.Count>0){selected.Sort();packed.AddRange(selected);IndexedFaces++;MaximumCandidates=Math.Max(MaximumCandidates,selected.Count);}
                offsets[face+1]=packed.Count;
            }
            indices=packed.ToArray();
        }
        public double Level(Reach reach,double fraction)=>levels[reach.Source]+(levels[reach.Target]-levels[reach.Source])*fraction;
        public double PresentationRelief(V point,int face)
        {
            double relief=0;
            for(int k=offsets[face];k<offsets[face+1];k++)
            {
                var r=Reaches[indices[k]];V d=r.B-r.A,p=point-r.A;double u=D.Clamp(Dot(p,d)/Math.Max(1e-30,Dot(d,d)));
                double ratio=(p-d*u).Length*radius/r.HalfWidth;
                double wall=OrogenMorphology.Smooth(.1,.3,ratio)*(1-OrogenMorphology.Smooth(1,1.5,ratio));
                relief=Math.Max(relief,Math.Min(80,r.Relief*.08*r.Strength)*wall);
            }
            return relief;
        }
        public double Apply(V point,int face,double ground,out double exposure)
        {
            exposure=0;
            for(int i=offsets[face];i<offsets[face+1];i++)
            {
                var reach=Reaches[indices[i]];V d=reach.B-reach.A,p=point-reach.A;
                double raw=Dot(p,d)/Math.Max(1e-30,Dot(d,d)),along=D.Clamp(raw);
                double distance=(p-d*along).Length*radius;
                // Extrapolate the shared grade across rounded end caps so the
                // lower downstream endpoint cannot gouge the upstream bank.
                ground=reach.Cut(ground,distance,Level(reach,raw),out double bare);
                exposure=Math.Max(exposure,bare);
            }
            return ground;
        }
    }
}
