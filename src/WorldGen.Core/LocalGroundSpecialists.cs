using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    public sealed partial class LocalScatter
    {
        void AddGroundSpecialists(ScatterKey key,LocalTerrain.LocalTerrainPatch patch,List<ScatterInstance> result,CancellationToken token)
        {
            const int side=24;double nominal=2*terrain.RadiusM*scale/Divisions(true),area=nominal*nominal;
            Span<double> weights=stackalloc double[10];
            for(int y=0;y<side;y++)for(int x=0;x<side;x++)
            {
                token.ThrowIfCancellationRequested();ulong id=key.Id(2,y*side+x);
                int hash=unchecked((int)Rng.Mix((uint)id^(uint)(id>>32)));var random=new Rng(seed,"local-specialist-placement-1",hash);
                double u=(x+.08+.84*random.Unit())/side,v=(y+.08+.84*random.Unit())/side;
                V p=Position(key,u,v);var sample=patch.Sample(p);var biome=sample.Biome;
                if(sample.Underwater||sample.ElevationM<2||biome==Biome.Desert||biome==Biome.Ice||biome==Biome.Ocean||biome==Biome.Freshwater||biome==Biome.SalineLake)continue;
                var ecology=world.Ecology;var h=ecology!=null&&sample.Cell<ecology.Habitat.Length?ecology.Habitat[sample.Cell]:null;
                var species=terrain.FloraRegion(sample.Cell).Specialists;V physical=p*(terrain.RadiusM*scale/1000);
                double warm=h==null?18:h.WarmTenthsC*.1-.006*(sample.ElevationM-world.ElevationM[sample.Cell]);
                double shade=GroundSpecialists.Canopy(biome)*D.Clamp(.80+.38*SphereNoise.Sample(physical,seed^0x342af,11));
                double sum=0;
                for(int i=0;i<species.Length;i++)
                {
                    var taxon=species[i];double colony=.5+.5*SphereNoise.Sample(physical,seed^taxon.MorphologyCode,1000/taxon.ColonyM);
                    weights[i]=GroundSpecialists.Suitability(taxon,h,sample.Ground,biome,warm,shade)*Math.Pow(D.Clamp((colony-.23)/.6),2);sum+=weights[i];
                }
                double cu=(key.X+u)*2/Divisions(true)-1,cv=(key.Y+v)*2/Divisions(true)-1;
                double jacobian=1/Math.Pow(1+cu*cu+cv*cv,1.5);
                if(sum<.001||random.Unit()>D.Clamp(area*jacobian/(side*side)*.075*D.Clamp(sum)))continue;
                double pick=random.Unit()*sum;int selected=species.Length-1;
                for(int i=0;i<species.Length;i++){pick-=weights[i];if(pick<=0){selected=i;break;}}
                var s=species[selected];
                V east=V.Cross(Math.Abs(p.Z)<.99?new V(0,0,1):new V(0,1,0),p).Unit,north=V.Cross(p,east).Unit;
                double e=1.0/(terrain.RadiusM*scale);
                double sx=(patch.Sample((p+east*e).Unit).ElevationM-patch.Sample((p-east*e).Unit).ElevationM)*.5;
                double sz=(patch.Sample((p+north*e).Unit).ElevationM-patch.Sample((p-north*e).Unit).ElevationM)*.5;
                if(sx*sx+sz*sz>.65*.65)continue;
                double size=.78+.44*random.Unit(),tint=.91+.15*random.Unit();
                if(biome==Biome.Tundra||biome==Biome.Alpine)size=Math.Min(size,.16/Math.Max(.01,s.HeightM));
                result.Add(new ScatterInstance{Id=id,Kind=s.Form==SpecialistForm.Fungus?ScatterKind.Fungus:ScatterKind.Herb,Variant=s.MorphologyCode,
                    Position=p,Normal=(p-east*sx-north*sz).Unit,GroundM=sample.ElevationM,HeightM=D.Q(s.HeightM*size,1000),WidthM=D.Q(s.WidthM*size,1000),
                    YawDegrees=D.Q(random.Unit()*360,1000),Color=new V(tint,tint,tint)});
            }
        }
    }
}
