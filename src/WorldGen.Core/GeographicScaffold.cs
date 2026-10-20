using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>Disposable, drainage-conditioned control mesh. Preparation is
    /// world-scale; sampling is local. Never a history tick or a metre raster.</summary>
    public sealed class GeographicScaffold
    {
        public const string Version="geography-scaffold-1";
        public readonly V[] Positions,Gradients;
        public readonly double[] Heights,Discharge,Areas;
        public readonly int[] Receiver,Order,Parents,Triangles;
        public readonly bool[] Ocean;
        readonly LocalTerrainProfile[] profiles;
        readonly double radius,scale;
        readonly int seed;
        public readonly int OriginalCount;
        public readonly bool Refined;
        public double PreparationMilliseconds {get;private set;}
        public double MeshMilliseconds {get;private set;}
        public double RoutingMilliseconds {get;private set;}
        public long RetainedBytes=>Positions.LongLength*48+Heights.LongLength*24+Receiver.LongLength*12+Triangles.LongLength*4+Ocean.LongLength;
        static ulong Edge(int a,int b)=>(ulong)(uint)Math.Min(a,b)<<32|(uint)Math.Max(a,b);
        sealed class EdgeComparer:IEqualityComparer<ulong>
        {
            public bool Equals(ulong a,ulong b)=>a==b;
            // UInt64's default upper/lower XOR degenerates for nearby mesh IDs.
            public int GetHashCode(ulong x)=>unchecked((int)Rng.Mix((uint)x^Rng.Mix((uint)(x>>32))));
        }

        internal GeographicScaffold(World world,LocalTerrainProfile[] profiles,double scale,CancellationToken token)
        {
            var watch=Stopwatch.StartNew();this.profiles=profiles;this.scale=scale;radius=world.Recipe.RadiusKm*1000;seed=world.Recipe.Seed;
            var mesh=world.Surface;OriginalCount=mesh.Cells.Length;
            Refined=OriginalCount<=250000;
            int count=OriginalCount+(Refined?mesh.Edges.Length:0);
            Positions=new V[count];Gradients=new V[count];Heights=new double[count];Discharge=new double[count];Areas=new double[count];
            Receiver=new int[count];Order=new int[count];Parents=new int[count];Ocean=new bool[count];
            var runoff=new double[count];var initial=new double[count];var fixedWater=new bool[count];
            var coarseRunoff=new double[OriginalCount];var coarseFlow=new double[OriginalCount];
            for(int i=0;i<OriginalCount;i++)
            {
                if((i&4095)==0)token.ThrowIfCancellationRequested();
                var h=world.Hydrology;
                double q=i<h.NorthernSummerDischargeM3PerS.Length?.25*(h.NorthernSpringDischargeM3PerS[i]+h.NorthernSummerDischargeM3PerS[i]+h.NorthernAutumnDischargeM3PerS[i]+h.NorthernWinterDischargeM3PerS[i]):0;
                coarseFlow[i]=coarseRunoff[i]=Math.Max(0,q);
            }
            for(int i=0;i<OriginalCount;i++){int r=world.Hydrology.Receiver[i];if(r>=0)coarseRunoff[r]-=coarseFlow[i];}
            for(int i=0;i<OriginalCount;i++)
            {
                Positions[i]=mesh.Cells[i].Position;Parents[i]=i;Heights[i]=world.ElevationM[i];Ocean[i]=world.Hydrology.OceanConnected[i];
                runoff[i]=Math.Max(0,coarseRunoff[i])*(Refined?.25:1);Areas[i]=mesh.Cells[i].AreaM2*(Refined?.25:1);
                int body=i<world.Hydrology.WaterBody.Length?world.Hydrology.WaterBody[i]:-1;
                if(body>=0&&body<world.Hydrology.Waters.Length&&world.Hydrology.LakeCoverageFraction[i]>.5)
                {
                    Heights[i]=world.Hydrology.Waters[body].SurfaceElevationM;
                    fixedWater[i]=world.Hydrology.Waters[body].Endorheic;
                }
            }
            var mids=new Dictionary<ulong,int>(Refined?mesh.Edges.Length:0,new EdgeComparer());
            if(Refined)foreach(var e in mesh.Edges)
            {
                if((e.Id&4095)==0)token.ThrowIfCancellationRequested();
                int id=OriginalCount+e.Id,a=e.A,b=e.B;double t=.4+.2*Rng.Value(seed,"scaffold-edge-position-1",e.Id);
                V p=(Positions[a]*(1-t)+Positions[b]*t).Unit;
                // A shared, small lateral displacement removes the obligation to
                // pass through cell centres/straight coarse edges. Both faces
                // use exactly the same midpoint and therefore the same border.
                V side=V.Cross(Positions[a],Positions[b]).Unit;
                p=(p+side*((Positions[a]-Positions[b]).Length*.065*(2*Rng.Value(seed,"scaffold-edge-side-1",e.Id)-1))).Unit;
                Positions[id]=p;Parents[id]=t<.5?a:b;Ocean[id]=Ocean[a]&&Ocean[b];
                double m=(profiles[a].MountainHeightM+profiles[b].MountainHeightM)*.5;
                double detail=SphereNoise.Sample(p*radius,seed^0x5197,.000035)*Math.Min(350,m*.22);
                double shore=OrogenMorphology.Smooth(30,300,Math.Min(Heights[a],Heights[b]));
                Heights[id]=Heights[a]*(1-t)+Heights[b]*t+detail*shore;
                if(world.Hydrology.Receiver[a]==b||world.Hydrology.Receiver[b]==a)
                    Heights[id]-=Math.Min(45,Math.Abs(Heights[a]-Heights[b])*.035);
                runoff[id]=.75*(Math.Max(0,coarseRunoff[a])/Math.Max(1,mesh.Cells[a].Neighbors.Length)+Math.Max(0,coarseRunoff[b])/Math.Max(1,mesh.Cells[b].Neighbors.Length));
                Areas[id]=.75*(mesh.Cells[a].AreaM2/(double)Math.Max(1,mesh.Cells[a].Neighbors.Length)+mesh.Cells[b].AreaM2/(double)Math.Max(1,mesh.Cells[b].Neighbors.Length));
                mids.Add(Edge(a,b),id);
            }
            Triangles=new int[mesh.Triangles.Length*(Refined?4:1)];
            for(int t=0;t<mesh.Triangles.Length;t+=3)
            {
                int a=mesh.Triangles[t],b=mesh.Triangles[t+1],c=mesh.Triangles[t+2],at=t*4;
                if(!Refined){Triangles[t]=a;Triangles[t+1]=b;Triangles[t+2]=c;continue;}
                int ab=mids[Edge(a,b)],bc=mids[Edge(b,c)],ca=mids[Edge(c,a)];
                int[] children={a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca};Array.Copy(children,0,Triangles,at,12);
            }
            // Packed adjacency: no Cell objects or second planet-wide locator.
            var offsets=new int[count+1];foreach(int v in Triangles)offsets[v+1]+=2;
            for(int i=1;i<offsets.Length;i++)offsets[i]+=offsets[i-1];
            var neighbours=new int[offsets[count]];var cursor=(int[])offsets.Clone();
            for(int t=0;t<Triangles.Length;t+=3)
            {int a=Triangles[t],b=Triangles[t+1],c=Triangles[t+2];neighbours[cursor[a]++]=b;neighbours[cursor[a]++]=c;neighbours[cursor[b]++]=a;neighbours[cursor[b]++]=c;neighbours[cursor[c]++]=a;neighbours[cursor[c]++]=b;}
            MeshMilliseconds=watch.Elapsed.TotalMilliseconds;
            Array.Copy(Heights,initial,count);
            // Two bounded route/stream-power/hillslope sweeps, not a geological
            // time loop. Route is recalculated after the terrain responds.
            for(int pass=0;pass<2;pass++)
            {
                Route(offsets,neighbours,fixedWater,token);
                Array.Copy(runoff,Discharge,count);
                for(int k=count-1;k>=0;k--){int i=Order[k],r=Receiver[i];if(r>=0)Discharge[r]+=Discharge[i];}
                foreach(int i in Order)
                {
                    if((i&4095)==0)token.ThrowIfCancellationRequested();
                    int r=Receiver[i];if(r<0||Ocean[i]||fixedWater[i])continue;
                    var profile=profiles[Parents[i]];
                    double erodibility=.7+.6*(2-profile.Sharpness);
                    double alpha=Math.Min(.8,.055*Math.Pow(Discharge[i],.35))*erodibility;
                    double target=(initial[i]+alpha*Heights[r])/(1+alpha);
                    double cut=Math.Max(5,Math.Min(600,profile.CoarseReliefM*.4+profile.MountainHeightM*.15));
                    target=Math.Max(initial[i]-cut,target);
                    // Explicit hillslope bound, independent of discharge. It
                    // applies to the geographic scale, not centimetre roughness.
                    double slope=.55+.25*D.Clamp(profile.Sharpness-1);
                    for(int k=offsets[i];k<offsets[i+1];k++)
                    {int j=neighbours[k];double dx=Positions[j].X-Positions[i].X,dy=Positions[j].Y-Positions[i].Y,dz=Positions[j].Z-Positions[i].Z;
                        double d=Math.Sqrt(dx*dx+dy*dy+dz*dz)*radius;target=Math.Min(target,Heights[j]+d*slope);}
                    Heights[i]=Math.Max(Heights[r],target);
                }
            }
            Route(offsets,neighbours,fixedWater,token);Array.Copy(runoff,Discharge,count);
            for(int k=count-1;k>=0;k--){int i=Order[k],r=Receiver[i];if(r>=0)Discharge[r]+=Discharge[i];}
            RoutingMilliseconds=watch.Elapsed.TotalMilliseconds-MeshMilliseconds;
            // Shared vertex derivatives give shared cubic edge curves.
            for(int i=0;i<count;i++)
            {
                if((i&4095)==0)token.ThrowIfCancellationRequested();
                V g=new V();int degree=0;
                for(int k=offsets[i];k<offsets[i+1];k++)
                {int j=neighbours[k];V d=Positions[j]-Positions[i];double d2=d.X*d.X+d.Y*d.Y+d.Z*d.Z;g+=d*((Heights[j]-Heights[i])/Math.Max(1,d2*radius));degree++;}
                Gradients[i]=g*(2.0/Math.Max(1,degree));
            }
            PreparationMilliseconds=watch.Elapsed.TotalMilliseconds;
        }

        void Route(int[] offsets,int[] neighbours,bool[] fixedWater,CancellationToken token)
        {
            int count=Positions.Length;var seen=new bool[count];var levels=new double[count];var heap=new Heap();int at=0;
            for(int i=0;i<count;i++)
            {
                Receiver[i]=-1;
                if(Ocean[i]){seen[i]=true;levels[i]=0;Order[at++]=i;}
            }
            for(int i=0;i<count;i++)
            {
                if(Ocean[i])
                {for(int k=offsets[i];k<offsets[i+1];k++)if(!Ocean[neighbours[k]]){heap.Push(0,i);break;}}
                else if(fixedWater[i]){seen[i]=true;levels[i]=Heights[i];heap.Push((long)Math.Round(levels[i]*1000),i);}
            }
            if(heap.Count==0){int pit=0;for(int i=1;i<count;i++)if(Heights[i]<Heights[pit])pit=i;seen[pit]=true;levels[pit]=Heights[pit];heap.Push((long)Math.Round(levels[pit]*1000),pit);}
            while(heap.Count>0)
            {
                if((at&4095)==0)token.ThrowIfCancellationRequested();int i=heap.Pop().Id;
                if(!Ocean[i])Order[at++]=i;
                for(int k=offsets[i];k<offsets[i+1];k++)
                {
                    int j=neighbours[k];if(seen[j])continue;seen[j]=true;Receiver[j]=i;levels[j]=Math.Max(Heights[j],levels[i]);
                    heap.Push((long)Math.Round(levels[j]*1000),j);
                }
            }
            if(at!=count)throw new InvalidOperationException("Scaffold routing did not cover the control mesh.");
            // Spill conditioning is part of terrain preparation. It is not a
            // licence for water ribbons floating above the unconditioned floor.
            for(int i=0;i<count;i++)if(!Ocean[i]&&!fixedWater[i])Heights[i]=Math.Max(Heights[i],levels[i]);
        }

        internal LocalTerrainProfile Vertex(int id)=>new LocalTerrainProfile{Center=Positions[id],ElevationM=Heights[id],Gradient=Gradients[id]};
        internal double Ridge(int a,int b)
        {
            if(Ocean[a]||Ocean[b]||Receiver[a]==b||Receiver[b]==a)return 0;
            var p=profiles[Parents[a]];var q=profiles[Parents[b]];
            double distance=(Positions[a]-Positions[b]).Length*radius*scale;
            double height=(Math.Abs(Heights[a]-Heights[b])*.16+(p.MountainHeightM+q.MountainHeightM)*.12)*(1-.4*(p.Sediment+q.Sediment));
            return Math.Max(0,Math.Min(distance*.12,height));
        }
    }
}
