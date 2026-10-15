using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public enum ShoreKind { Lake, Wetland, Island }
    public struct ShoreSample
    {
        public double ElevationM, WaterSurfaceM;
        public bool OpenWater, Wetland;
        public int FeatureId;
    }
    /// <summary>Replaceable derived terrain prototype, NOT a new world-generation pipeline or a placement contract.</summary>
    public sealed class ShoreTerrainSampler
    {
        public const string Version="shore-prototype-1";
        public static bool IsWetlandCell(World w,int i)
        {
            var h=w.Hydrology;if(i>=h.LakeCoverageFraction.Length||h.LakeCoverageFraction[i]<=0||h.LakeCoverageFraction[i]>=.9)return false;
            double area=h.LakeCoverageFraction[i]*w.Surface.Cells[i].AreaM2;
            var habitat=w.Ecology?.Habitat;bool marsh=habitat!=null&&habitat.Length>i&&(habitat[i].Biome==Biome.Marsh||habitat[i].Biome==Biome.Peatland||habitat[i].Biome==Biome.SaltMarsh||habitat[i].Biome==Biome.Mangrove);
            double depth=h.LakeWaterVolumeM3.Length>i&&area>0?h.LakeWaterVolumeM3[i]/area:10;
            return marsh||depth<1.5;
        }
        public sealed class Feature
        {
            public int Id, Cell, SourceId; public ShoreKind Kind;
            public V Center, Axis, Side;
            public double AreaM2, LevelM, ReliefM, ScaleM, Aspect, Phase, Variation, RadiusM;
            // Continuous seeded polar field. The outline and point queries use exactly this function.
            public double Radius(double angle) => ScaleM*(1+Variation*Math.Sin(3*angle+Phase)+Variation*.45*Math.Sin(5*angle-Phase)+.045*Math.Sin(2*angle+Phase*.7));
            public V Boundary(double angle,double planetRadius)
            {
                double r=Radius(angle);return (Center+(Axis*(Math.Cos(angle)*Math.Sqrt(Aspect))+Side*(Math.Sin(angle)/Math.Sqrt(Aspect)))*(r/planetRadius)).Unit;
            }
            public double Coordinate(V p,double planetRadius)
            {
                double dot=V.Dot(p,Center);if(dot<=0)return double.PositiveInfinity;
                double x=V.Dot(p,Axis)*planetRadius/dot/Math.Sqrt(Aspect),y=V.Dot(p,Side)*planetRadius/dot*Math.Sqrt(Aspect);
                return Math.Sqrt(x*x+y*y)/Radius(Math.Atan2(y,x));
            }
        }
        readonly World world; readonly double radius; readonly Lazy<CellLocator> cells,features;
        readonly Lazy<Tuple<int[],int[]>> topology; readonly double searchRadius;
        public readonly Feature[] Features;
        public ShoreTerrainSampler(World world,CancellationToken token=default)
        {
            this.world=world;radius=world.Recipe.RadiusKm*1000.0;cells=new Lazy<CellLocator>(()=>new CellLocator(world.Surface.Cells));
            var list=new List<Feature>();
            Feature Make(ShoreKind kind,int cell,int source,V center,double area,double aspect,double heading,double level,double relief)
            {
                V axis=V.Tangent(center,new V(0,0,1));if(axis.Length<.5)axis=V.Tangent(center,new V(1,0,0));
                V side=V.Cross(center,axis).Unit;
                var f=new Feature{Id=list.Count,Cell=cell,SourceId=source,Kind=kind,Center=center,Axis=axis*Math.Cos(heading)+side*Math.Sin(heading),AreaM2=area,Aspect=aspect,LevelM=level,ReliefM=relief};
                f.Side=V.Cross(center,f.Axis).Unit;f.Phase=Rng.Value(world.Recipe.Seed,"shore-phase-1",source^(int)kind*193)*2*Math.PI;
                f.Variation=.08+.17*Rng.Value(world.Recipe.Seed,"shore-shape-1",source^(int)kind*431);
                double integral=0;for(int k=0;k<128;k++){f.ScaleM=1;double r=f.Radius((k+.5)*2*Math.PI/128);integral+=r*r*Math.PI/128;}
                f.ScaleM=Math.Sqrt(area/integral);f.RadiusM=f.ScaleM*Math.Sqrt(aspect)*(1+1.45*f.Variation+.045);list.Add(f);return f;
            }
            // Preserve all individual saved islands, not one aggregate blob per ocean cell.
            if(world.Subcells!=null)foreach(var island in world.Subcells.Islands)
            {
                token.ThrowIfCancellationRequested();
                Make(ShoreKind.Island,island.Cell,island.Id,island.Center,island.AreaM2,1+2.5*Rng.Value(world.Recipe.Seed,"shore-island-aspect",island.Id),Rng.Value(world.Recipe.Seed,"shore-island-heading",island.Id)*Math.PI,0,island.PeakM);
            }
            var h=world.Hydrology;int n=world.Surface.Cells.Length;var seen=new bool[n];var queue=new Queue<int>();
            bool Wet(int i)
            {
                return IsWetlandCell(world,i);
            }
            for(int first=0;first<h.LakeCoverageFraction.Length;first++)
            {
                if((first&1023)==0)token.ThrowIfCancellationRequested();
                if(seen[first]||h.LakeCoverageFraction[first]<=0||h.OceanConnected[first]||h.WaterBody[first]<0)continue;
                int body=h.WaterBody[first];bool wet=Wet(first);seen[first]=true;queue.Enqueue(first);var group=new List<int>();
                while(queue.Count>0)
                {
                    if((group.Count&1023)==0)token.ThrowIfCancellationRequested();
                    int i=queue.Dequeue();group.Add(i);
                    // A trace of water is not evidence of a lake-wide connecting channel.
                    if(h.LakeCoverageFraction[i]<.08)continue;
                    foreach(int j in world.Surface.Cells[i].Neighbors)if(!seen[j]&&!h.OceanConnected[j]&&h.WaterBody[j]==body&&h.LakeCoverageFraction[j]>=.08&&Wet(j)==wet){seen[j]=true;queue.Enqueue(j);}
                }
                group.Sort();double area=0,volume=0;V sum=new V();
                foreach(int i in group){double a=h.LakeCoverageFraction[i]*world.Surface.Cells[i].AreaM2;area+=a;sum+=world.Surface.Cells[i].Position*a;if(h.LakeWaterVolumeM3.Length>i)volume+=h.LakeWaterVolumeM3[i];}
                if(area<=0)continue;V center=sum.Unit,east=V.Tangent(center,new V(0,0,1));if(east.Length<.5)east=V.Tangent(center,new V(1,0,0));V north=V.Cross(center,east).Unit;
                double xx=0,yy=0,xy=0;
                foreach(int i in group){double a=h.LakeCoverageFraction[i]*world.Surface.Cells[i].AreaM2/area,x=V.Dot(world.Surface.Cells[i].Position,east)*radius,y=V.Dot(world.Surface.Cells[i].Position,north)*radius;xx+=a*x*x;yy+=a*y*y;xy+=a*x*y;}
                double floor=area/(Math.PI*4),disc=Math.Sqrt((xx-yy)*(xx-yy)+4*xy*xy),aspect=Math.Sqrt((xx+yy+disc+floor)/(xx+yy-disc+floor));
                Make(wet?ShoreKind.Wetland:ShoreKind.Lake,first,first,center,area,Math.Min(5,Math.Max(1.15,aspect)),.5*Math.Atan2(2*xy,xx-yy),h.Waters[body].SurfaceElevationM,Math.Max(.2,Math.Min(300,2*volume/area)));
            }
            Features=list.ToArray();searchRadius=Features.Length==0?0:Features.Max(f=>f.RadiusM)*1.35/radius;
            features=Features.Length==0?null:new Lazy<CellLocator>(()=>new CellLocator(Features.Select((f,i)=>new Cell{Id=i,Position=f.Center}).ToArray()));
            topology=new Lazy<Tuple<int[],int[]>>(()=>{
                var offsets=new int[n+1];foreach(int i in world.Surface.Triangles)offsets[i+1]++;for(int i=1;i<offsets.Length;i++)offsets[i]+=offsets[i-1];
                var faces=new int[world.Surface.Triangles.Length];var cursor=(int[])offsets.Clone();for(int t=0;t<faces.Length;t+=3)for(int j=0;j<3;j++)faces[cursor[world.Surface.Triangles[t+j]]++]=t;
                return Tuple.Create(offsets,faces);
            });
        }
        public V[] Outline(Feature f,int segments=64)
        {
            if(segments<12||segments>512)throw new ArgumentOutOfRangeException(nameof(segments));
            var points=new V[segments];for(int i=0;i<segments;i++)points[i]=f.Boundary(i*2*Math.PI/segments,radius);return points;
        }
        public ShoreSample Sample(V position)
        {
            V p=position.Unit;int cell=cells.Value.Nearest(p);double elevation=world.ElevationM[cell],signed=world.Hydrology.OceanConnected[cell]?-1:1;
            var offsets=topology.Value.Item1;var faces=topology.Value.Item2;
            // The same center-to-center triangles used by the map, with no nearest-cell height steps.
            for(int k=offsets[cell];k<offsets[cell+1];k++)
            {
                int t=faces[k],a=world.Surface.Triangles[t],b=world.Surface.Triangles[t+1],c=world.Surface.Triangles[t+2];V pa=world.Surface.Cells[a].Position,pb=world.Surface.Cells[b].Position,pc=world.Surface.Cells[c].Position;
                double denominator=Triple(pa,pb,pc);if(Math.Abs(denominator)<1e-16)continue;
                double x=Triple(p,pb,pc)/denominator,y=Triple(pa,p,pc)/denominator,z=Triple(pa,pb,p)/denominator,total=x+y+z;
                if(Math.Min(x,Math.Min(y,z))<-.000001)continue;x/=total;y/=total;z/=total;
                elevation=x*world.ElevationM[a]+y*world.ElevationM[b]+z*world.ElevationM[c];
                double H(int i)=>world.Hydrology.OceanConnected[i]?Math.Min(-1,world.ElevationM[i]):Math.Max(1,world.ElevationM[i]);
                signed=x*H(a)+y*H(b)+z*H(c);break;
            }
            var result=new ShoreSample{ElevationM=elevation,OpenWater=signed<0,WaterSurfaceM=signed<0?0:double.NaN,FeatureId=-1};
            if(features==null)return result;
            foreach(int id in features.Value.Within(p,searchRadius*searchRadius))
            {
                var f=Features[id];double r=f.Coordinate(p,radius);if(r>=1.35)continue;
                if(f.Kind==ShoreKind.Wetland && result.FeatureId>=0 && Features[result.FeatureId].Kind==ShoreKind.Lake)continue;
                double blend=D.Clamp((1.35-r)/.35);blend=blend*blend*(3-2*blend);
                double target=f.LevelM+(f.Kind==ShoreKind.Island?f.ReliefM:-f.ReliefM)*Math.Max(0,1-r*r);
                result.ElevationM=result.ElevationM*(1-blend)+target*blend;
                if(r<=1){result.FeatureId=id;result.OpenWater=f.Kind==ShoreKind.Lake;result.Wetland=f.Kind==ShoreKind.Wetland;result.WaterSurfaceM=f.Kind==ShoreKind.Island?double.NaN:f.LevelM;}
            }
            result.ElevationM=D.Q(result.ElevationM,1000);return result;
        }
        static double Triple(V a,V b,V c)=>a.X*(b.Y*c.Z-b.Z*c.Y)+a.Y*(b.Z*c.X-b.X*c.Z)+a.Z*(b.X*c.Y-b.Y*c.X);
    }
}
