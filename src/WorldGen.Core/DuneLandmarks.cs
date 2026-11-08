using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public sealed class DuneLandmark
    {
        public V Position,Downwind;
        public string Form;
        public int Source;
        public double FeatureHeightM,RadiusM,Sand,WindVariability,Weight;
    }
    // Explicit asynchronous landmark/review search, never a per-frame system.
    // Both the review and gameplay command use the actual terrain evaluator.
    public static class DuneLandmarks
    {
        public static DuneLandmark Find(World world,LocalTerrain terrain,bool star,CancellationToken token=default,int source=-1,Action<string> report=null)
        {
            double radius=terrain.RadiusM*terrain.HorizontalScale;
            var candidates=new List<(int cell,double score)>();
            for(int i=0;i<world.Surface.Cells.Length;i++)
            {
                if((i&1023)==0)token.ThrowIfCancellationRequested();if(source>=0&&source!=i)continue;
                var profile=terrain.Profile(i);var p=profile.Landscape;
                if(profile.ElevationM<5||p.Sand<.2||p.Frozen>.2||p.Arid<.6)continue;
                V metres=world.Surface.Cells[i].Position*radius;
                double sand=LandscapeProvince.SandSea(metres,world.Recipe.Seed,p);if(sand<.35)continue;
                // A coarse centre can fall outside a kilometre-scale wind
                // province. Admit its maximum supported local variability for
                // the SEARCH only; the actual chosen feature is checked below.
                var admission=p;
                if(star){admission.DuneWindVariability=1-(1-p.DuneWindVariability)*(1-p.DuneWindDeflection);admission.DuneWindDeflection=0;}
                DuneForms.FieldWeights(metres,admission,sand,out double b,out _,out double s,out _,out _,world.Recipe.Seed);
                double weight=star?s:b;if(weight<.6)continue;
                candidates.Add((i,weight*sand/(1+profile.CoarseReliefM/1800)));
            }
            report?.Invoke("Dune "+(star?"star":"barchan")+" eligible coarse regions: "+candidates.Count);
            foreach(var candidate in candidates.OrderByDescending(p=>p.score).ThenBy(p=>p.cell).Take(48))
            {
                token.ThrowIfCancellationRequested();V center=world.Surface.Cells[candidate.cell].Position;
                V east=V.Cross(Math.Abs(center.Z)<.99?new V(0,0,1):new V(0,1,0),center).Unit,north=V.Cross(center,east).Unit;
                var controls=terrain.Profile(candidate.cell).Landscape;
                V At(double x,double z)=>(center+east*(x/radius)+north*(z/radius)).Unit;
                double Height(V at,LandscapeControls p,out double weight,out double sand)
                {
                    V metres=at*radius;sand=LandscapeProvince.SandSea(metres,world.Recipe.Seed,p);
                    DuneForms.FieldWeights(metres,p,sand,out double b,out _,out double s,out double u,out double v,world.Recipe.Seed);weight=star?s:b;
                    double envelope=OrogenMorphology.Smooth(.08,.4,sand);
                    return weight*(star?DuneForms.Stars(u,v,world.Recipe.Seed,envelope):DuneForms.Barchans(u,v,world.Recipe.Seed,envelope));
                }
                double range=star?2400:900,step=star?160:60,best=0,bx=0,bz=0;
                for(double z=-range;z<=range;z+=step)for(double x=-range;x<=range;x+=step)
                {
                    double h=Height(At(x,z),controls,out _,out _);if(h>best){best=h;bx=x;bz=z;}
                }
                if(best<(star?45:6))continue;
                double anchorX=bx,anchorZ=bz;
                for(int z=-4;z<=4;z++)for(int x=-4;x<=4;x++)
                {
                    double px=anchorX+x*step/8,pz=anchorZ+z*step/8,h=Height(At(px,pz),controls,out _,out _);
                    if(h>best){best=h;bx=px;bz=pz;}
                }
                V point=At(bx,bz);var patch=terrain.Patch(point,3000/terrain.HorizontalScale);var sample=patch.Sample(point);
                double actual=Height(point,sample.Province,out double actualWeight,out double actualSand);
                report?.Invoke($"Dune {candidate.cell}: coarse peak {best:F2}m; fine peak {actual:F2}m, weight {actualWeight:F3}, sand {actualSand:F3}, seasonal {sample.Province.DuneWindVariability:F3}, deflection {sample.Province.DuneWindDeflection:F3}, local wind {DuneForms.LocalVariability(point*radius,sample.Province,world.Recipe.Seed):F3}, frozen {sample.Province.Frozen:F3}, glacial {sample.Province.Glacial:F3}, underwater {sample.Underwater}");
                if(sample.Underwater||actualWeight<.55||actual<(star?40:5)||sample.Province.Frozen>.2)continue;
                double first=point.X*.73+point.Y*.41+point.Z*.54;
                double blend=OrogenMorphology.Smooth(.2,.6,1-first*first);
                V wind=blend>=.5?new V(.73,.41,.54):new V(.13,.85,-.51);
                return new DuneLandmark{Position=point,Downwind=V.Tangent(point,wind),Form=star?"star":"barchan",Source=candidate.cell,
                    FeatureHeightM=actual,RadiusM=star?1050:240,Sand=actualSand,WindVariability=DuneForms.LocalVariability(point*radius,sample.Province,world.Recipe.Seed),Weight=actualWeight};
            }
            return null;
        }
    }
}
