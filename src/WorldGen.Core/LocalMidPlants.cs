using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    public sealed partial class LocalScatter
    {
        void AddMidPlants(ScatterKey key,LocalTerrain.LocalTerrainPatch patch,List<ScatterInstance> result,CancellationToken token)
        {
            const int side=48;int divisions=Divisions(false);double nominal=2*terrain.RadiusM*scale/divisions;
            Span<double> weights=stackalloc double[14];
            for(int y=0;y<side;y++)for(int x=0;x<side;x++)
            {
                token.ThrowIfCancellationRequested();int slot=y*side+x;ulong id=key.Id(2,slot*4);
                var r=new Rng(seed,"mid-placement-1",unchecked((int)Rng.Mix((uint)id^(uint)(id>>32))));
                double u=(x+.05+.9*r.Unit())/side,v=(y+.05+.9*r.Unit())/side;
                V p=Position(key,u,v);
                // Cheap planet-stable stand mask before expensive terrain/taxon queries.
                if(r.Unit()>MidFlora.StandDensity(p,seed,terrain.RadiusM*scale))continue;
                var sample=patch.Sample(p);
                if(sample.Underwater||sample.Biome==Biome.Ice||sample.ElevationM<2)continue;
                var e=world.Ecology;var habitat=e!=null&&sample.Cell<e.Habitat.Length?e.Habitat[sample.Cell]:null;
                var species=terrain.FloraRegion(sample.Cell).MidPlants;
                double warm=habitat==null?18:habitat.WarmTenthsC*.1-.006*(sample.ElevationM-world.ElevationM[sample.Cell]),sum=0;
                for(int i=0;i<species.Length;i++)
                {
                    double fit=MidFlora.Suitability(species[i],habitat,sample.Ground,sample.Biome,warm);
                    weights[i]=fit<.005?0:fit*MidFlora.Colony(species[i],p,seed,terrain.RadiusM*scale);sum+=weights[i];
                }
                double cu=(key.X+u)*2/divisions-1,cv=(key.Y+v)*2/divisions-1,jacobian=1/Math.Pow(1+cu*cu+cv*cv,1.5);
                double occupancy=D.Clamp(sum*.55);
                if(r.Unit()>D.Clamp(nominal*nominal*jacobian/(side*side)*.012*occupancy)||sum<.001)continue;
                double pick=r.Unit()*sum;int selected=species.Length-1;for(int i=0;i<species.Length;i++){pick-=weights[i];if(pick<=0){selected=i;break;}}
                var s=species[selected];V east=V.Cross(Math.Abs(p.Z)<.99?new V(0,0,1):new V(0,1,0),p).Unit,north=V.Cross(p,east).Unit;
                double delta=1/(terrain.RadiusM*scale);
                double sx=(patch.Sample((p+east*delta).Unit).ElevationM-patch.Sample((p-east*delta).Unit).ElevationM)*.5;
                double sz=(patch.Sample((p+north*delta).Unit).ElevationM-patch.Sample((p-north*delta).Unit).ElevationM)*.5;
                if(sx*sx+sz*sz>.85*.85)continue;
                int members=MidFlora.ColonyMembers(s.Form);
                for(int member=0;member<members;member++)
                {
                    double angle=r.Unit()*D.Pi*2,spread=member==0?0:s.WidthM*(.6+.4*r.Unit());
                    var at=member==0?p:(p+(east*Math.Cos(angle)+north*Math.Sin(angle))*(spread/(terrain.RadiusM*scale))).Unit;
                    var ground=member==0?sample:patch.Sample(at);
                    if(ground.Underwater||Math.Abs(ground.ElevationM-sample.ElevationM)>spread*.85+.1)continue;
                    double size=.72+.56*r.Unit(),tint=.91+.16*r.Unit();
                    result.Add(new ScatterInstance{Id=key.Id(2,slot*4+member),Kind=ScatterKind.MidPlant,Variant=s.MorphologyCode|(int)(r.Unit()*4),Position=at,Normal=(at-east*sx-north*sz).Unit,
                        GroundM=ground.ElevationM,HeightM=D.Q(s.HeightM*size,1000),WidthM=D.Q(s.WidthM*size*(.88+.24*r.Unit()),1000),
                        YawDegrees=D.Q(r.Unit()*360,1000),Color=new V(tint,tint,tint)});
                }
            }
        }
    }
}
