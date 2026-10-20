using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>Derived drainage constraints. One primitive per occupied coarse edge, never a fine planet raster.
    /// Random-access patches discretize only nearby curves; no runtime erosion or upstream walks.</summary>
    public sealed class LocalHydrology
    {
        public const string Version="local-hydrology-1";
        public struct Piece {public V A,B;public double HalfWidthM;}
        public sealed class Channel
        {
            public int From,To;public V A,B,Side,TangentA,TangentB;
            public double StartM,EndM,WidthM,DepthM,LengthM,ValleyM,Bend,Phase,Glacial;
            public V Position(double t)
            {
                // Endpoint-flat meanders preserve exact confluences. Mountain streams
                // have little lateral freedom; flat alluvial reaches meander more.
                double envelope=Math.Sin(Math.PI*t);envelope*=envelope;
                double offset=Bend*envelope*(.7*Math.Sin(t*4*Math.PI+Phase)+.3*Math.Sin(t*10*Math.PI-Phase));
                double t2=t*t,t3=t2*t;
                V curve=A*(2*t3-3*t2+1)+B*(-2*t3+3*t2)+TangentA*(t3-2*t2+t)+TangentB*(t3-t2);
                return (curve+Side*offset).Unit;
            }
            public double Level(double t)=>StartM+(EndM-StartM)*t;
        }
        readonly double radius,scale,maxChannelRadius,maxShoreRadius;
        readonly int seed;
        readonly CellLocator channelIndex,shoreIndex;
        public readonly Channel[] Channels;
        public readonly ShoreTerrainSampler.Feature[] Shores;
        public LocalHydrology(World world,double horizontalScale,CancellationToken token=default)
        {
            radius=world.Recipe.RadiusKm*1000.0;scale=horizontalScale;seed=world.Recipe.Seed;
            var h=world.Hydrology;int n=world.Surface.Cells.Length;
            var levels=new double[n];var flows=new double[n];
            for(int i=0;i<n;i++)
            {
                if((i&1023)==0)token.ThrowIfCancellationRequested();
                flows[i]=i<h.NorthernSummerDischargeM3PerS.Length?
                    .25*(h.NorthernSpringDischargeM3PerS[i]+h.NorthernSummerDischargeM3PerS[i]+h.NorthernAutumnDischargeM3PerS[i]+h.NorthernWinterDischargeM3PerS[i]):0;
                levels[i]=Math.Max(0,world.ElevationM[i]-Math.Min(12,1+Math.Pow(Math.Max(0,flows[i]),.25)));
                int body=i<h.WaterBody.Length?h.WaterBody[i]:-1;
                if(body>=0&&body<h.Waters.Length&&h.Waters[body].Kind!=WaterKind.Ocean&&h.LakeCoverageFraction[i]>=.08)
                    levels[i]=h.Waters[body].SurfaceElevationM;
            }
            // Source-first DAG reduction conditions below-resolution spillways rather
            // than permitting uphill rivers. Ocean endpoints remain exactly sea level.
            for(int k=h.VisitOrder.Length-1;k>=0;k--)
            {
                if((k&1023)==0)token.ThrowIfCancellationRequested();
                int i=h.VisitOrder[k],to=h.Receiver[i];
                if(to>=0&&!h.OceanConnected[to])levels[to]=Math.Min(levels[to],levels[i]);
            }
            var upstream=Enumerable.Repeat(-1,n).ToArray();
            for(int i=0;i<h.Receiver.Length;i++){int to=h.Receiver[i];if(to>=0&&(upstream[to]<0||flows[i]>flows[upstream[to]]))upstream[to]=i;}
            V Tangent(int i,V fallback)
            {
                int before=upstream[i],after=i<h.Receiver.Length?h.Receiver[i]:-1;
                V direction=before>=0&&after>=0?world.Surface.Cells[after].Position-world.Surface.Cells[before].Position:fallback;
                V up=world.Surface.Cells[i].Position;direction=(direction-up*V.Dot(direction,up)).Unit;
                return V.Dot(direction,fallback.Unit)>.15?direction:fallback.Unit;
            }
            var list=new List<Channel>();
            for(int i=0;i<h.Receiver.Length;i++)
            {
                if((i&1023)==0)token.ThrowIfCancellationRequested();
                int to=h.Receiver[i];if(to<0||h.OceanConnected[i]||flows[i]<.04)continue;
                if(h.LakeCoverageFraction.Length>i&&h.LakeCoverageFraction[i]>=.9&&h.WaterBody[i]==h.WaterBody[to]&&h.Waters[h.WaterBody[i]].OutletCell!=i)continue;
                V a=world.Surface.Cells[i].Position,b=world.Surface.Cells[to].Position;
                double length=(a-b).Length*radius*scale;if(length<1)continue;
                double slope=Math.Max(0,(levels[i]-levels[to])/length);
                double width=Math.Min(Math.Max(2,4.8*Math.Sqrt(flows[i])),Math.Min(1200,length*.12));
                double relief=Math.Abs(world.ElevationM[i]-world.ElevationM[to]);
                double valley=Math.Min(2200,Math.Max(60,width*5+Math.Sqrt(relief)*16));
                double bend=Math.Min(length*.10,width*5+length*.025)*(1-OrogenMorphology.Smooth(.005,.08,slope));
                V difference=b-a;double chord=difference.Length;
                var channel=new Channel{From=i,To=to,A=a,B=b,Side=V.Cross(a,b).Unit,TangentA=Tangent(i,difference)*chord,TangentB=Tangent(to,difference)*chord,
                    StartM=levels[i],EndM=Math.Min(levels[i],levels[to]),WidthM=width,DepthM=Math.Min(18,.4+Math.Pow(flows[i],.3)*.6),
                    LengthM=length,ValleyM=valley,Bend=bend/(radius*scale),Phase=Rng.Value(seed,"local-river-meander-1",i)*Math.PI*2,
                    Glacial=world.Glaciated.Length>i&&world.Glaciated[i]?1:0};
                list.Add(channel);maxChannelRadius=Math.Max(maxChannelRadius,(length*.8+valley+bend)/(radius*scale));
            }
            Shores=new ShoreTerrainSampler(world,token).Features;
            // The retained fractional footprint may end a little before the
            // sampled outlet cell. Join only that short shore gap, never draw
            // arbitrary canals across disconnected components of a water body.
            foreach(var f in Shores)
            {
                if(f.Kind!=ShoreKind.Lake||f.Cell>=h.WaterBody.Length)continue;
                int body=h.WaterBody[f.Cell];if(body<0)continue;var saved=h.Waters[body];int outlet=saved.OutletCell;
                if(outlet<0||saved.Endorheic||flows[outlet]<.04)continue;
                V b=world.Surface.Cells[outlet].Position;double r=f.Coordinate(b,radius);if(r<=1||r>1.6)continue;
                double dot=V.Dot(b,f.Center),x=V.Dot(b,f.Axis)/dot/Math.Sqrt(f.Aspect),y=V.Dot(b,f.Side)/dot*Math.Sqrt(f.Aspect);
                V edge=f.Boundary(Math.Atan2(y,x),radius),a=(f.Center+(edge-f.Center)*.98).Unit;
                V difference=b-a;double length=difference.Length*radius*scale;
                if(length<1)continue;
                double width=Math.Min(600,Math.Max(2,4.8*Math.Sqrt(flows[outlet])));
                list.Add(new Channel{From=f.Cell,To=outlet,A=a,B=b,Side=V.Cross(a,b).Unit,TangentA=difference,TangentB=difference,
                    StartM=f.LevelM,EndM=Math.Min(f.LevelM,levels[outlet]),WidthM=width,DepthM=1+Math.Pow(flows[outlet],.25),LengthM=length,ValleyM=Math.Max(80,width*5)});
                maxChannelRadius=Math.Max(maxChannelRadius,(length*.8+Math.Max(80,width*5))/(radius*scale));
            }
            Channels=list.ToArray();
            if(Channels.Length>0)channelIndex=new CellLocator(Channels.Select((c,i)=>new Cell{Id=i,Position=(c.A+c.B).Unit}).ToArray());
            foreach(var f in Shores)maxShoreRadius=Math.Max(maxShoreRadius,f.RadiusM*1.35/radius);
            if(Shores.Length>0)shoreIndex=new CellLocator(Shores.Select((f,i)=>new Cell{Id=i,Position=f.Center}).ToArray());
        }
        public Patch Region(V center,double extent)
        {
            double cr=Math.Min(2,maxChannelRadius+extent*1.5/radius+20/radius);
            double sr=Math.Min(2,maxShoreRadius+extent*1.5/radius+20/radius);
            int[] channels=channelIndex==null?Array.Empty<int>():channelIndex.Within(center,cr*cr);
            int[] shores=shoreIndex==null?Array.Empty<int>():shoreIndex.Within(center,sr*sr);
            return new Patch(this,center,extent,channels,shores);
        }
        public sealed class Patch
        {
            struct Segment
            {
                public double X,Y,DX,DY,Length2,Start,End,Half,Depth,Valley,Glacial;
                public V A,Delta;
            }
            readonly LocalHydrology owner;readonly V center,east,north;
            readonly Segment[] segments;readonly ShoreTerrainSampler.Feature[] shores;
            readonly Dictionary<long,int[]> bins=new Dictionary<long,int[]>();
            readonly Dictionary<long,int[]> shoreBins=new Dictionary<long,int[]>();
            const double Box=1024;
            public int ChannelSegments=>segments.Length;
            public int ShoreCount=>shores.Length;
            public Piece[] Pieces()=>segments.Select(s=>new Piece{A=(s.A/(owner.radius*owner.scale)).Unit,B=((s.A+s.Delta)/(owner.radius*owner.scale)).Unit,HalfWidthM=s.Half}).ToArray();
            static long Key(int x,int y)=>((long)x<<32)|(uint)y;
            internal Patch(LocalHydrology owner,V center,double extent,int[] channels,int[] shores)
            {
                this.owner=owner;this.center=center;
                east=V.Cross(Math.Abs(center.Z)<.99?new V(0,0,1):new V(0,1,0),center).Unit;north=V.Cross(center,east).Unit;
                var list=new List<Segment>();var building=new Dictionary<long,List<int>>();double radius=owner.radius*owner.scale,bound=extent*owner.scale*1.5+20;
                foreach(int id in channels)
                {
                    var c=owner.Channels[id];int count=Math.Max(4,Math.Min(256,(int)Math.Ceiling(c.LengthM/256)));
                    V a=c.Position(0);
                    for(int k=1;k<=count;k++)
                    {
                        double t=k/(double)count;V b=c.Position(t),da=a-center,db=b-center;
                        double x=V.Dot(da,east)*radius,y=V.Dot(da,north)*radius,xx=V.Dot(db,east)*radius,yy=V.Dot(db,north)*radius;
                        double pad=c.ValleyM+2;
                        if(Math.Min(x,xx)-pad<=bound&&Math.Max(x,xx)+pad>=-bound&&Math.Min(y,yy)-pad<=bound&&Math.Max(y,yy)+pad>=-bound)
                        {
                            V start=a*radius,difference=(b-a)*radius;
                            int index=list.Count;list.Add(new Segment{X=x,Y=y,DX=xx-x,DY=yy-y,A=start,Delta=difference,Length2=V.Dot(difference,difference),Start=c.Level((k-1)/(double)count),End=c.Level(t),Half=c.WidthM*.5,Depth=c.DepthM,Valley=c.ValleyM,Glacial=c.Glacial});
                            for(int by=(int)Math.Floor((Math.Min(y,yy)-pad)/Box);by<=(int)Math.Floor((Math.Max(y,yy)+pad)/Box);by++)
                            for(int bx=(int)Math.Floor((Math.Min(x,xx)-pad)/Box);bx<=(int)Math.Floor((Math.Max(x,xx)+pad)/Box);bx++)
                            {
                                long key=Key(bx,by);if(!building.TryGetValue(key,out var bucket)){bucket=new List<int>();building.Add(key,bucket);}bucket.Add(index);
                            }
                        }
                        a=b;
                    }
                }
                segments=list.ToArray();foreach(var pair in building)bins.Add(pair.Key,pair.Value.ToArray());
                this.shores=shores.Select(i=>owner.Shores[i]).Where(f=>(f.Center-center).Length*owner.radius<=f.RadiusM*1.35+extent*1.5+20).ToArray();
                building.Clear();
                for(int i=0;i<this.shores.Length;i++)
                {
                    var f=this.shores[i];V d=f.Center-center;double x=V.Dot(d,east)*radius,y=V.Dot(d,north)*radius,pad=f.RadiusM*owner.scale*1.35+2;
                    for(int by=(int)Math.Floor(Math.Max(-bound,y-pad)/Box);by<=(int)Math.Floor(Math.Min(bound,y+pad)/Box);by++)
                    for(int bx=(int)Math.Floor(Math.Max(-bound,x-pad)/Box);bx<=(int)Math.Floor(Math.Min(bound,x+pad)/Box);bx++)
                    {long key=Key(bx,by);if(!building.TryGetValue(key,out var bucket)){bucket=new List<int>();building.Add(key,bucket);}bucket.Add(i);}
                }
                foreach(var pair in building)shoreBins.Add(pair.Key,pair.Value.ToArray());
            }
            public void Apply(V point,ref double terrain,ref double water,ref double waterMask,out double riverInfluence)
            {
                double radius=owner.radius*owner.scale;V delta=point-center;
                double x=V.Dot(delta,east)*radius,y=V.Dot(delta,north)*radius;
                riverInfluence=0;
                // Lakes and individual unresolved islands share the existing global
                // map prototype's outline, common water level and allocated area.
                long address=Key((int)Math.Floor(x/Box),(int)Math.Floor(y/Box));
                if(shoreBins.TryGetValue(address,out var localShores))foreach(int shoreId in localShores)
                {
                    var f=shores[shoreId];
                    double r=f.Coordinate(point,owner.radius);if(r>=1.35||f.Kind==ShoreKind.Wetland)continue;
                    if(r>.7)
                    {
                        double shore=SphereNoise.Sample(point*(owner.radius*owner.scale),owner.seed^0x6b37,.0017);
                        r+=shore*Math.Min(45,f.ScaleM*owner.scale*.025)/(f.ScaleM*owner.scale)*OrogenMorphology.Smooth(.7,.9,r);
                    }
                    double blend=1-OrogenMorphology.Smooth(1,1.35,r);
                    double target=f.LevelM+(f.Kind==ShoreKind.Island?f.ReliefM:-f.ReliefM)*(1-r*r);
                    terrain=terrain*(1-blend)+target*blend;
                    if(f.Kind==ShoreKind.Lake)
                    {
                        water=f.LevelM;waterMask=(1-r)*f.ScaleM*owner.scale;
                    }
                    else if(r<1){water=0;waterMask=terrain<0?-terrain:-1;}
                }
                if(!bins.TryGetValue(address,out var candidates))return;
                double best=double.PositiveInfinity,riverWater=double.NaN,riverBed=terrain;
                foreach(int id in candidates)
                {
                    var s=segments[id];V from=point*radius-s.A;double t=D.Clamp(V.Dot(from,s.Delta)/Math.Max(.000001,s.Length2));
                    double d=(from-s.Delta*t).Length;if(d>=s.Valley)continue;
                    double level=s.Start+(s.End-s.Start)*t;
                    double cross=OrogenMorphology.Smooth(s.Half,s.Valley,d);
                    if(s.Glacial>0)cross*=cross;
                    double cut=d<s.Half?level-s.Depth*(1-d*d/(s.Half*s.Half)):
                        level+Math.Max(1,terrain-level)*cross;
                    riverBed=Math.Min(riverBed,cut);
                    double distance=d-s.Half;
                    if(distance<best){best=distance;riverWater=level;}
                    riverInfluence=Math.Max(riverInfluence,1-OrogenMorphology.Smooth(s.Half,s.Valley,d));
                }
                terrain=riverBed;
                if((!double.IsNaN(riverWater))&&(double.IsNaN(water)||waterMask<0||best<0&&riverWater>water))
                {water=riverWater;waterMask=-best;}
            }
        }
    }
}
