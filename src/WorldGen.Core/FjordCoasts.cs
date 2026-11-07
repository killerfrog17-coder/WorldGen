using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>Ocean-inundated glacial drainage troughs. Prepared once from
    /// mapped ice, landward relief and the existing main-stem drainage; queries
    /// visit only a numeric compressed row of nearby finite channel sections.</summary>
    public sealed class FjordCoasts
    {
        public readonly struct Inlet
        {
            public readonly int Mouth,Ocean,Head,First,Count;
            public readonly double LengthM,HalfWidth,Depth,Relief;
            public Inlet(int mouth,int ocean,int head,int first,int count,double length,double width,double depth,double relief)
            {Mouth=mouth;Ocean=ocean;Head=head;First=first;Count=count;LengthM=length;HalfWidth=width;Depth=depth;Relief=relief;}
        }
        public readonly struct Section
        {
            public readonly V A,B;
            public readonly double WidthA,WidthB,DepthA,DepthB,Relief;
            public Section(V a,V b,double wa,double wb,double da,double db,double relief)
            {A=a;B=b;WidthA=wa;WidthB=wb;DepthA=da;DepthB=db;Relief=relief;}
            public double Cut(V point,double radius,double ground,out double footprint,out double exposure)
            {
                V delta=B-A,p=point-A;
                double along=D.Clamp(Dot(p,delta)/Math.Max(1e-30,Dot(delta,delta)));
                double distance=(p-delta*along).Length*radius;
                double width=WidthA+(WidthB-WidthA)*along,r=distance/width;
                footprint=width-distance;exposure=0;if(r>=1)return ground;
                double depth=DepthA+(DepthB-DepthA)*along;
                double floor=-depth+(depth+Relief)*r*r*r*r;
                double cut=Math.Min(0,floor-ground)*RiverscapeKernel.Smooth((1-r)/.16);
                exposure=OrogenMorphology.Smooth(8,80,-cut)*OrogenMorphology.Smooth(.35,.70,r)*(1-OrogenMorphology.Smooth(.83,1,r));
                return ground+cut;
            }
        }
        readonly double radius;
        readonly int[] offsets,indices;
        readonly HashSet<int> oceanNodes=new HashSet<int>();
        public bool OceanNode(int cell)=>oceanNodes.Contains(cell);
        public readonly Inlet[] Inlets;
        public readonly Section[] Sections;
        public int IndexedFaces {get;}
        public int MaximumCandidates {get;}
        public int IndexBytes=>sizeof(int)*(offsets.Length+indices.Length);
        public int CandidateCount(int face)=>offsets.Length==0?0:offsets[face+1]-offsets[face];
        static double Dot(V a,V b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        public FjordCoasts(World world,LocalTerrainProfile[] profiles,double scale,CellLocator locator,Func<int,double,V> channel,CancellationToken token=default)
        {
            radius=world.Recipe.RadiusKm*1000*scale;int count=profiles.Length;
            var best=new int[count];for(int i=0;i<count;i++)best[i]=-1;
            var h=world.Hydrology;
            double Flow(int i)=>h.NorthernSummerDischargeM3PerS.Length==count?
                .25*(h.NorthernSpringDischargeM3PerS[i]+h.NorthernSummerDischargeM3PerS[i]+h.NorthernAutumnDischargeM3PerS[i]+h.NorthernWinterDischargeM3PerS[i]):0;
            bool Lake(int i)=>h.WaterBody[i]>=0&&h.LakeCoverageFraction[i]>=.5;
            for(int i=0;i<count;i++)
            {
                int next=h.Receiver[i];if(next<0||h.OceanConnected[i])continue;
                if(best[next]<0||Flow(i)>Flow(best[next])||Flow(i)==Flow(best[next])&&i<best[next])best[next]=i;
            }
            var inlets=new List<Inlet>();var sections=new List<Section>();
            for(int mouth=0;mouth<count;mouth++)
            {
                if((mouth&2047)==0)token.ThrowIfCancellationRequested();
                int ocean=h.Receiver[mouth];
                if(ocean<0||h.OceanConnected[mouth]||!h.OceanConnected[ocean]||Lake(mouth)||world.ElevationM[mouth]<=5||world.ElevationM[mouth]>450||
                    mouth>=world.Glaciated.Length||!world.Glaciated[mouth]||Flow(mouth)<5)continue;
                var path=new List<int>{mouth};int at=mouth;double length=0,high=world.ElevationM[mouth];
                foreach(int neighbor in world.Surface.Cells[mouth].Neighbors)
                    if(!h.OceanConnected[neighbor])high=Math.Max(high,world.ElevationM[neighbor]);
                for(int n=0;n<16;n++)
                {
                    int next=best[at];if(next<0||Lake(next)||profiles[next].Glacial<.35||world.ElevationM[next]>1400||Flow(next)<.04)break;
                    double run=(world.Surface.Cells[at].Position-world.Surface.Cells[next].Position).Length*radius;
                    if(length+run>35000)break;
                    path.Add(next);length+=run;high=Math.Max(high,world.ElevationM[next]);at=next;
                }
                double relief=high-world.ElevationM[mouth];
                if(path.Count<2||length<3500||relief<300)continue;
                double width=Math.Min(1800,Math.Max(450,300+relief*.35+Math.Sqrt(length)*2));
                double depth=Math.Min(450,Math.Max(45,relief*.20));int first=sections.Count;
                // Head to open sea. Sections share endpoint dimensions exactly;
                // finite rounded caps close the head without an abrupt trench.
                path.Reverse();path.Add(ocean);double total=length+(world.Surface.Cells[mouth].Position-world.Surface.Cells[ocean].Position).Length*radius,walked=0;
                foreach(int node in path)oceanNodes.Add(node);
                for(int link=0;link<path.Count-1;link++)
                {
                    int source=path[link],target=path[link+1];double run=(world.Surface.Cells[source].Position-world.Surface.Cells[target].Position).Length*radius;
                    int steps=Math.Max(4,Math.Min(32,(int)Math.Ceiling(run/650)));
                    V a=channel(source,0);
                    for(int j=1;j<=steps;j++)
                    {
                        V b=channel(source,j/(double)steps);double ta=(walked+run*(j-1)/steps)/total,tb=(walked+run*j/steps)/total;
                        double Width(double t)=>width*(.55+.50*RiverscapeKernel.Smooth(t));
                        // A shallow mouth sill and a deeper inland basin, all
                        // below sea level. This is inherited erosion, not tide.
                        double Depth(double t)=>depth*(.35+.65*Math.Sin(Math.PI*Math.Min(1,t)));
                        sections.Add(new Section(a,b,Width(ta),Width(tb),Depth(ta),Depth(tb),Math.Min(1600,relief)));a=b;
                    }
                    walked+=run;
                }
                inlets.Add(new Inlet(mouth,ocean,path[0],first,sections.Count-first,length,width,depth,relief));
            }
            Inlets=inlets.ToArray();Sections=sections.ToArray();
            if(Sections.Length==0){offsets=indices=Array.Empty<int>();return;}
            double margin=0;foreach(var p in profiles)margin=Math.Max(margin,p.SupportM*scale);
            var cells=new Dictionary<int,List<int>>();
            foreach(var inlet in Inlets)for(int i=inlet.First;i<inlet.First+inlet.Count;i++)
            {
                var section=Sections[i];V center=(section.A+section.B).Unit;
                double support=(section.A-section.B).Length*radius*.5+Math.Max(section.WidthA,section.WidthB)+margin;
                // Reuse the world's immutable spatial locator; never rescan
                // every planetary cell for each short inlet section.
                int nearest=locator.Nearest(center);
                var queue=new Queue<int>();var visited=new HashSet<int>();queue.Enqueue(nearest);visited.Add(nearest);
                while(queue.Count>0)
                {
                    int cell=queue.Dequeue();if((world.Surface.Cells[cell].Position-center).Length*radius>support)continue;
                    if(!cells.TryGetValue(cell,out var list)){list=new List<int>();cells.Add(cell,list);}list.Add(i);
                    foreach(int n in world.Surface.Cells[cell].Neighbors)if(visited.Add(n))queue.Enqueue(n);
                }
            }
            int faces=world.Surface.Triangles.Length/3;offsets=new int[faces+1];var selected=new List<int>();var packed=new List<int>();
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
        public double Apply(V point,int face,double ground,out double footprint,out double exposure)
        {
            footprint=double.NegativeInfinity;exposure=0;double result=ground;
            if(offsets.Length==0)return ground;
            for(int i=offsets[face];i<offsets[face+1];i++)
            {
                double cut=Sections[indices[i]].Cut(point,radius,ground,out double localFootprint,out double bare);
                result=Math.Min(result,cut);footprint=Math.Max(footprint,localFootprint);exposure=Math.Max(exposure,bare);
            }
            return result;
        }
    }
}
