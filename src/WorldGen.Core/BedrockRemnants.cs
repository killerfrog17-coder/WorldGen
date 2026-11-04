using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>Sparse resistant bedrock masses in admitted dry geology. Coarse
    /// cells select candidates by physical area; indexed finite footprints own
    /// local relief, instead of turning every noise crest into another hill.</summary>
    public sealed class BedrockRemnants
    {
        public sealed class Outcrop
        {
            public int Cell;public V Center,Axis,Side;
            public double RadiusX,RadiusZ,HeightM;public bool Blocky;
            public double RadiusM=>Math.Max(RadiusX,RadiusZ);
            public double Coordinate(V p,double radius)
            {
                V d=(p-Center)*radius;double x=Math.Abs(Dot(d,Axis))/RadiusX,z=Math.Abs(Dot(d,Side))/RadiusZ;
                return Blocky?Math.Pow(x*x*x*x+z*z*z*z,.25):Math.Sqrt(x*x+z*z);
            }
            public double Profile(double r)
            {
                if(r>=1)return 0;
                return (.68+.32*Math.Sqrt(Math.Max(0,1-r*r)))*(1-RiverscapeKernel.Smooth((r-.78)/.22));
            }
        }
        readonly int[][] faceCandidates;readonly double radius;
        public readonly Outcrop[] Forms;
        public int IndexedFaces {get;}
        public BedrockRemnants(World world,LocalTerrainProfile[] profiles,double scale,CancellationToken token=default)
        {
            radius=world.Recipe.RadiusKm*1000*scale;var forms=new List<Outcrop>();
            var cells=new Dictionary<int,List<int>>();double margin=0;
            foreach(var profile in profiles)margin=Math.Max(margin,profile.SupportM*scale);
            for(int cell=0;cell<profiles.Length;cell++)
            {
                if((cell&4095)==0)token.ThrowIfCancellationRequested();
                var profile=profiles[cell];if(profile.Landscape.Remnant<.45||world.ElevationM[cell]<20||world.Hydrology.OceanConnected[cell])continue;
                bool coastal=false;foreach(int n in world.Surface.Cells[cell].Neighbors)if(world.Hydrology.OceanConnected[n]){coastal=true;break;}if(coastal)continue;
                V center=world.Surface.Cells[cell].Position;var random=new Rng(world.Recipe.Seed,"bedrock-remnant-1",cell);
                double province=OrogenMorphology.Smooth(.50,.75,.5+.5*SphereNoise.Sample(center*radius,world.Recipe.Seed^0x6c251,1/14000d));
                double chance=world.Surface.Cells[cell].AreaM2*scale*scale/250000000d*province*profile.Landscape.Remnant;
                if(random.Unit()>=Math.Min(.2,chance))continue;
                V east=V.Tangent(center,new V(0,0,1));if(east.Length<.5)east=V.Tangent(center,new V(1,0,0));
                V north=V.Cross(center,east).Unit;double angle=random.Unit()*Math.PI*2;
                V axis=(east*Math.Cos(angle)+north*Math.Sin(angle)).Unit;
                double offset=Math.Min(3000,Math.Sqrt(world.Surface.Cells[cell].AreaM2)*scale*.35);
                center=(center+east*((random.Unit()*2-1)*offset/radius)+north*((random.Unit()*2-1)*offset/radius)).Unit;
                axis=V.Tangent(center,axis).Unit;
                double a=450+750*random.Unit(),b=a*(.35+.35*random.Unit());
                var form=new Outcrop{Cell=cell,Center=center,Axis=axis,Side=V.Cross(center,axis).Unit,RadiusX=a,RadiusZ=b,
                    HeightM=Math.Min(420,b*(.55+.35*random.Unit())),Blocky=profile.Ground.RockWeight(2)>.4||random.Unit()<.25};
                int index=forms.Count;forms.Add(form);var queue=new Queue<int>();var visited=new HashSet<int>();queue.Enqueue(cell);visited.Add(cell);
                while(queue.Count>0)
                {
                    int current=queue.Dequeue();
                    if((world.Surface.Cells[current].Position-center).Length*radius>form.RadiusM*1.3+margin)continue;
                    if(!cells.TryGetValue(current,out var list)){list=new List<int>();cells.Add(current,list);}list.Add(index);
                    foreach(int adjacent in world.Surface.Cells[current].Neighbors)if(visited.Add(adjacent))queue.Enqueue(adjacent);
                }
            }
            Forms=forms.ToArray();faceCandidates=new int[world.Surface.Triangles.Length/3][];
            for(int face=0;face<faceCandidates.Length;face++)
            {
                if((face&8191)==0)token.ThrowIfCancellationRequested();List<int> selected=null;
                for(int k=0;k<3;k++)if(cells.TryGetValue(world.Surface.Triangles[face*3+k],out var list))
                    foreach(int id in list){if(selected==null)selected=new List<int>();if(!selected.Contains(id))selected.Add(id);}
                if(selected!=null){selected.Sort();faceCandidates[face]=selected.ToArray();IndexedFaces++;}
            }
        }
        public double Apply(V point,int face,double ground,out double exposure)
        {
            exposure=0;var candidates=faceCandidates[face];if(candidates==null)return ground;double relief=0;
            foreach(int index in candidates)
            {
                var form=Forms[index];double r=form.Coordinate(point,radius);if(r>=1)continue;
                double shape=form.Profile(r);relief=Math.Max(relief,form.HeightM*shape);
                exposure=Math.Max(exposure,OrogenMorphology.Smooth(.02,.25,shape));
            }
            return ground+relief;
        }
        static double Dot(V a,V b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    }
}
