using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    public sealed partial class LocalScatter
    {
        void AddGeneratedTrees(ScatterKey key,LocalTerrain.LocalTerrainPatch patch,List<ScatterInstance> result,CancellationToken token)
        {
            const int side=96;int divisions=Divisions(false);double nominal=2*terrain.RadiusM*scale/divisions;
            Span<double> weights=stackalloc double[13];
            for(int y=0;y<side;y++)for(int x=0;x<side;x++)
            {
                token.ThrowIfCancellationRequested();int slot=y*side+x;ulong id=key.Id(0,slot);
                var r=new Rng(seed,"generated-tree-placement-2",unchecked((int)Rng.Mix((uint)id^(uint)(id>>32))));
                double u=(x+.08+.84*r.Unit())/side,v=(y+.08+.84*r.Unit())/side;V at=Position(key,u,v);
                double stand=TreeFlora.Stand(at,seed,terrain.RadiusM*scale);if(r.Unit()>stand)continue;
                double cu=(key.X+u)*2/divisions-1,cv=(key.Y+v)*2/divisions-1,jacobian=1/Math.Pow(1+cu*cu+cv*cv,1.5);
                // Cheap upper admission before expensive local surface queries.
                double densityCap=D.Clamp(nominal*nominal*jacobian/(side*side)*TreeFlora.PlacementPerM2);
                if(r.Unit()>densityCap)continue;
                var sample=patch.Sample(at);if(sample.Underwater||sample.ElevationM<2)continue;
                var e=world.Ecology;var h=e!=null&&sample.Cell<e.Habitat.Length?e.Habitat[sample.Cell]:null;
                var species=terrain.FloraRegion(sample.Cell).Trees;double sum=0;
                for(int i=0;i<species.Length;i++)
                {
                    weights[i]=TreeFlora.SpeciesWeight(species[i],h,sample,at,seed,terrain.RadiusM*scale,world.ElevationM[sample.Cell]);sum+=weights[i];
                }
                if(sum<.001)continue;
                // The surface profile blends habitat potential across source
                // cells. The old nearest-biome switch made abrupt forest edges.
                if(r.Unit()>TreeFlora.HabitatAdmission(sample))continue;
                double pick=r.Unit()*sum;int chosen=species.Length-1;for(int i=0;i<species.Length;i++){pick-=weights[i];if(pick<=0){chosen=i;break;}}
                var s=species[chosen];V east=V.Cross(Math.Abs(at.Z)<.99?new V(0,0,1):new V(0,1,0),at).Unit,north=V.Cross(at,east).Unit;
                double delta=1.5/(terrain.RadiusM*scale);
                double sx=(patch.Sample((at+east*delta).Unit).ElevationM-patch.Sample((at-east*delta).Unit).ElevationM)/3;
                double sz=(patch.Sample((at+north*delta).Unit).ElevationM-patch.Sample((at-north*delta).Unit).ElevationM)/3;
                double up=1/Math.Sqrt(1+sx*sx+sz*sz);
                if(r.Unit()>TreeFlora.SlopeAdmission(up))continue;
                // Static size cohorts, not simulated ages. Saplings keep their adult lineage.
                double cohort=r.Unit(),size=cohort<.12?.18+.16*r.Unit():cohort<.30?.40+.25*r.Unit():.72+.36*r.Unit();
                double tint=.93+.12*r.Unit();
                result.Add(new ScatterInstance{Id=id,Kind=ScatterKind.GeneratedTree,Variant=s.MorphologyCode|(int)(r.Unit()*6),Position=at,Normal=(at-east*sx-north*sz).Unit,
                    GroundM=sample.ElevationM,HeightM=D.Q(s.HeightM*size,1000),WidthM=D.Q(s.WidthM*size*(.88+.24*r.Unit()),1000),
                    YawDegrees=D.Q(r.Unit()*360,1000),Color=new V(tint,tint,tint)});
            }
        }
    }
}
