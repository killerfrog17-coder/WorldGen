using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
using System.Text.Json;

static class DuneFormTests
{
    public static void Survey(string path,string output)
    {
        var world=WorldFile.ReadPlanetaryFile(path);var model=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        var c=world.Climate;int[] histogram=new int[10],seasonalHistogram=new int[10];var rows=new List<(double variability,double barchan,double star,object value)>();
        for(int i=0;i<world.Surface.Cells.Length;i++)
        {
            var profile=model.Profile(i);var p=profile.Landscape;if(profile.ElevationM<0||p.Sand<.2||p.Frozen>.2||p.Arid<.6)continue;
            var at=world.Surface.Cells[i].Position;
            double sand=LandscapeProvince.SandSea(at*(model.RadiusM*.1),world.Recipe.Seed,p);if(sand<.2)continue;
            if(c.NorthernSpring.WindEastMilli.Length<=i||c.NorthernSummer.WindEastMilli.Length<=i||c.NorthernAutumn.WindEastMilli.Length<=i||c.NorthernWinter.WindEastMilli.Length<=i)continue;
            double seasonal=DuneForms.SeasonalVariability(c,i),v=p.DuneWindVariability;
            histogram[Math.Min(9,(int)(v*10))]++;
            seasonalHistogram[Math.Min(9,(int)(seasonal*10))]++;
            double chart=DuneForms.Coordinates(at*(model.RadiusM*.1),out _,out _);
            DuneForms.FieldWeights(at*(model.RadiusM*.1),p,sand,out double barchan,out _,out double star,out _,out _);
            rows.Add((v,barchan,star,new{Cell=i,Position=new{at.X,at.Y,at.Z},Sand=sand,SeasonalVariability=seasonal,WindVariability=v,ChartConfidence=chart,BarchanWeight=barchan,StarWeight=star,profile.CoarseReliefM,profile.ElevationM}));
        }
        File.WriteAllText(output,JsonSerializer.Serialize(new{Count=rows.Count,Histogram=histogram,SeasonalHistogram=seasonalHistogram,MostVariable=rows.OrderByDescending(r=>r.variability).Take(12).Select(r=>r.value),MostPersistent=rows.OrderBy(r=>r.variability).Take(12).Select(r=>r.value),StarRegions=rows.OrderByDescending(r=>r.star).Take(12).Select(r=>r.value),BarchanRegions=rows.OrderByDescending(r=>r.barchan).Take(12).Select(r=>r.value)},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Dune climate candidates {rows.Count}; variability deciles {string.Join(',',histogram)}");
    }
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        Check(DuneForms.WindVariability(1,0,2,0,3,0,4,0)==0,"Persistent winds retain a unidirectional regime despite changing strength");
        Check(DuneForms.WindVariability(1,0,-1,0,1,0,-1,0)==.5,"Opposing seasonal winds remain an axial regime rather than multidirectional stars");
        Check(DuneForms.WindVariability(1,0,0,1,-1,0,0,-1)==1,"Four balanced wind directions produce a multidirectional regime");
        V site=new V(.3,.8,.51).Unit*637100;
        Check(Math.Abs(DuneForms.TerrainVariability(.3,0,site,173)-.3)<1e-12,"Flat ground cannot invent orographic wind scattering");
        double mountain=DuneForms.TerrainVariability(.3,1800,site,173);
        Check(mountain>=.3&&mountain<=1,"Subgrid mountain scattering is a bounded addition to measured seasonal variability");
        var blend=LandscapeControls.Mix(new LandscapeControls{DuneWindVariability=.2f},new LandscapeControls{DuneWindVariability=.6f},new LandscapeControls{DuneWindVariability=1},.2,.3,.5);
        Check(Math.Abs(blend.DuneWindVariability-.72)<1e-6,"Shared terrain controls interpolate the wind regime instead of resetting it at refined vertices");
        V chartPole=new V(-.6805,.4507,.5777).Unit*637100;
        DuneForms.FieldWeights(chartPole,new LandscapeControls{DuneWindVariability=0},.5,out double crescent,out double ridge,out _,out _,out _);
        Check(crescent>.99&&ridge<.01,"Persistent winds and lower sand supply support isolated crescents");
        DuneForms.FieldWeights(chartPole,new LandscapeControls{DuneWindVariability=0},.95,out crescent,out ridge,out _,out _,out _);
        Check(crescent==0&&ridge==1,"Abundant sand retains connected transverse ridges even with persistent winds");
        DuneForms.FieldWeights(chartPole,new LandscapeControls{DuneWindVariability=1},.8,out _,out ridge,out double starWeight,out _,out _);
        Check(starWeight>.99&&ridge<.01,"Multidirectional winds and sufficient sand support star dunes");
        Check(DuneForms.Barchan(0,0,20,120,72,130)==20,"A barchan has one central crest height");
        Check(DuneForms.Barchan(-60,0,20,120,72,130)>0&&DuneForms.Barchan(60,0,20,120,72,130)==0,"The stoss extends farther upwind than the steep lee extends downwind");
        Check(DuneForms.Barchan(0,121,20,120,72,130)==0,"A crescent has finite transverse extent");
        double horn=72*.7*.7;
        Check(DuneForms.Barchan(horn,84,20,120,72,130)>5&&horn>0,"Both low horns advance downwind of the central crest");
        Check(DuneForms.Barchan(horn,84,20,120,72,130)==DuneForms.Barchan(horn,-84,20,120,72,130),"The reference crescent mirrors across its wind axis");
        Check(DuneForms.Star(0,0,150,900,1,0)==150,"A star summit has one height, independent of approach direction");
        Check(DuneForms.Star(901,0,150,900,1,0)==0,"A star dune has finite support");
        double arm=DuneForms.Star(450,0,150,900,1,0);
        double valley=DuneForms.Star(225,450*Math.Sin(Math.PI/3),150,900,1,0);
        Check(arm>valley*1.15,"A star has distinct ridges and intervening lower slopes");
        for(int i=0;i<3;i++)
        {
            double a=i*Math.PI*2/3;
            Check(Math.Abs(DuneForms.Star(Math.Cos(a)*450,Math.Sin(a)*450,150,900,1,0)-arm)<1e-8,"Three arms descend from the reference summit");
        }
        for(int i=0;i<=1000;i++)
        {
            DuneForms.Weights(i/1000d,out double b,out double t,out double s);
            Check(b>=0&&t>=0&&s>=0&&b<=1&&t<=1&&s<=1&&Math.Abs(b+t+s-1)<1e-12,"Regional form weights stay bounded and normalized");
        }
        int gaps=0,crests=0,changed=0;double maximumSlope=0;
        for(int z=-50;z<=50;z++)for(int x=-50;x<=50;x++)
        {
            double u=x*27,v=z*27,h=DuneForms.Barchans(u,v,173),star=DuneForms.Stars(u,v,173);
            Check(double.IsFinite(h)&&h>=0&&h<=37&&double.IsFinite(star)&&star>=0&&star<=185,"Finite fields stay within their prepared height bounds");
            if(h==0)gaps++;else crests++;
            if(h!=DuneForms.Barchans(u,v,174))changed++;
            double dx=(DuneForms.Barchans(u+.05,v,173)-DuneForms.Barchans(u-.05,v,173))/.1;
            double dz=(DuneForms.Barchans(u,v+.05,173)-DuneForms.Barchans(u,v-.05,173))/.1;
            maximumSlope=Math.Max(maximumSlope,Math.Sqrt(dx*dx+dz*dz));
        }
        Check(gaps>100&&crests>100&&changed>100,"A field contains separated finite dunes and seed-specific placement");
        Check(maximumSlope<Math.Tan(36*Math.PI/180),"Crescent faces remain within a conservative dry-sand slope bound");
        foreach(double boundary in new[]{-1040d,-520,0,520,1040})for(int i=-50;i<=50;i++)
        {
            double a=DuneForms.Barchans(boundary-.001,i*19,173),b=DuneForms.Barchans(boundary+.001,i*19,173);
            Check(Math.Abs(a-b)<.01,"Changing candidate-grid cell cannot introduce a height seam");
        }
        Console.WriteLine($"PASS {checks} finite dune form checks; maximum sampled crescent slope {Math.Atan(maximumSlope)*180/Math.PI:F2} degrees. Terrain remains unhooked.");
    }
}
