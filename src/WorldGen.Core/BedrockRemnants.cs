using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>Sparse resistant bedrock masses in admitted geology. Coarse
    /// cells select candidates by physical area; indexed finite footprints own
    /// local relief, instead of turning every noise crest into another hill.</summary>
    public sealed class BedrockRemnants
    {
        public sealed class Outcrop
        {
            public int Cell;public V Center,Axis,Side;
            public double RadiusX,RadiusZ,HeightM;public bool Blocky,Dome,Jointed;
            public double RadiusM=>Math.Max(RadiusX,RadiusZ);
            public double Coordinate(V p,double radius)
            {
                V d=(p-Center)*radius;double x=Dot(d,Axis)/RadiusX,z=Dot(d,Side)/RadiusZ;
                double phase=(Rng.Mix((uint)Cell)&1023)*.006135923;
                if(Dome)
                {
                    x/=1+.035*Math.Sin(z*3.2+phase);z/=1+.045*Math.Sin(x*2.7-phase);
                    return Math.Sqrt(x*x+z*z);
                }
                // Broad uneven shoulders shape the mass. Fine runoff belongs
                // in its surface material: a periodic height corrugation here
                // cut repeated metre-scale teeth into every steep shoulder.
                double shoulderZ=1+.08*Math.Sin(x*3.7+phase)+.035*Math.Sin(x*10.3+phase*2);
                double shoulderX=1+.065*Math.Sin(z*4.1-phase)+.025*Math.Sin(z*12.1+phase);
                x=Math.Abs(x/shoulderX);z=Math.Abs(z/shoulderZ);
                return Blocky?Math.Pow(x*x*x*x+z*z*z*z,.25):Math.Sqrt(x*x+z*z);
            }
            public double Cap(V p,double radius)
            {
                V d=(p-Center)*radius;double x=Dot(d,Axis)/RadiusX,z=Dot(d,Side)/RadiusZ;
                double cap=1+.025*x-.03*z+.025*Math.Sin(x*3+z*2+Cell*.37);
                return Dome?cap*GraniteDomes.JointFace(x,Jointed):cap;
            }
            public double Profile(double r)
            {
                if(r>=1)return 0;
                if(Dome)return GraniteDomes.Profile(r);
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
                var profile=profiles[cell];if(world.ElevationM[cell]<20||world.Hydrology.OceanConnected[cell])continue;
                bool dry=profile.Landscape.Remnant>=.45;
                double domeAdmission=dry?0:GraniteDomes.Admission(profile);if(!dry&&domeAdmission<=.01)continue;
                bool coastal=false;foreach(int n in world.Surface.Cells[cell].Neighbors)if(world.Hydrology.OceanConnected[n]){coastal=true;break;}if(coastal)continue;
                V center=world.Surface.Cells[cell].Position;var random=new Rng(world.Recipe.Seed,dry?"bedrock-remnant-1":"granite-dome-1",cell);
                double province=OrogenMorphology.Smooth(.50,.75,.5+.5*SphereNoise.Sample(center*radius,world.Recipe.Seed^0x6c251,1/14000d));
                double chance=dry?world.Surface.Cells[cell].AreaM2*scale*scale/250000000d*province*profile.Landscape.Remnant
                    :world.Surface.Cells[cell].AreaM2*scale*scale/500000000d*province*domeAdmission;
                if(random.Unit()>=Math.Min(.2,chance))continue;
                V east=V.Tangent(center,new V(0,0,1));if(east.Length<.5)east=V.Tangent(center,new V(1,0,0));
                V north=V.Cross(center,east).Unit;double angle=random.Unit()*Math.PI*2;
                V axis=(east*Math.Cos(angle)+north*Math.Sin(angle)).Unit;
                double offset=Math.Min(3000,Math.Sqrt(world.Surface.Cells[cell].AreaM2)*scale*.35);
                center=(center+east*((random.Unit()*2-1)*offset/radius)+north*((random.Unit()*2-1)*offset/radius)).Unit;
                axis=V.Tangent(center,axis).Unit;
                double a=dry?450+750*random.Unit():500+950*random.Unit(),b=a*(dry?.35+.35*random.Unit():.65+.30*random.Unit());
                var form=new Outcrop{Cell=cell,Center=center,Axis=axis,Side=V.Cross(center,axis).Unit,RadiusX=a,RadiusZ=b,
                    HeightM=dry?Math.Min(420,b*(.55+.35*random.Unit())):Math.Min(650,b*(.35+.40*random.Unit())),
                    Blocky=dry&&random.Unit()<(profile.Ground.RockWeight(2)>.4?.55:.25),Dome=!dry,Jointed=!dry&&random.Unit()<.35};
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
        public double PresentationRelief(V point,int face)
        {
            var candidates=faceCandidates[face];if(candidates==null)return 0;
            double relief=0;
            foreach(int index in candidates)
            {
                var form=Forms[index];double r=form.Coordinate(point,radius);
                double wall=OrogenMorphology.Smooth(.40,.70,r)*(1-OrogenMorphology.Smooth(1.02,1.30,r));
                relief=Math.Max(relief,Math.Min(100,form.HeightM*(form.Dome?.07:.20))*wall);
            }
            return relief;
        }
        public double Apply(V point,int face,double ground,out double exposure)
        {
            exposure=0;var candidates=faceCandidates[face];if(candidates==null)return ground;double relief=0;
            foreach(int index in candidates)
            {
                var form=Forms[index];double r=form.Coordinate(point,radius);if(r>=1)continue;
                double shape=form.Profile(r),cap=form.Cap(point,radius);relief=Math.Max(relief,form.HeightM*shape*cap);
                exposure=Math.Max(exposure,OrogenMorphology.Smooth(.02,.25,form.Dome?shape*cap:shape));
            }
            return ground+relief;
        }
        static double Dot(V a,V b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    }
}
