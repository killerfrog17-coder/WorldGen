using WorldGen.Core;
static class ForagerMechanismTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        Check(ForagerTechnology.Count==43&&ForagerTechnology.ValidGraph(),"Expanded toolkit graph is acyclic, with stable original item IDs");
        Check(!ForagerTechnology.All[17].Prerequisites.Contains(16),"A bow is not incorrectly gated on a pressure-flaked stone arrowhead");
        var dugout=new double[ForagerTechnology.Count];dugout[3]=dugout[5]=dugout[38]=1;
        Check(ForagerTechnology.Prerequisites(dugout,30)==1,"A dugout-making pathway is not incorrectly forced through a raft or hide-container tradition");
        var d=new ForagerDistrict{Id=0,AreaKm2=10000,Center=new V(1,0,0),Productivity=700,WinterC=12,SummerC=28,MeanC=21,Aridity=1,GrowingDays=300,Water=.8,Coast=.2,River=.3,Canopy=.6,Stone=.8,Fiber=.8,Wood=.8,Resin=.5,Food=new[]{.8,.5,.5,.3,.2},Seasonality=new[]{.2,.1,.2,.2,.1}};
        var culture=new ForagerCulture{FoodPreference=Enumerable.Repeat(.6,5).ToArray(),TechniquePreference=Enumerable.Repeat(.6,ForagerTechnology.Count).ToArray(),ExchangeOpenness=.7};
        var lineage=new HomininLineage();var c=new ForagerCohort{Adults=90,Children=50,Elders=10,Knowledge=Enumerable.Repeat(.9,ForagerTechnology.Count).ToArray(),Skill=Enumerable.Repeat(.9,ForagerTechnology.Count).ToArray(),Use=new double[ForagerTechnology.Count],Teaching=new double[ForagerTechnology.Count],Innovation=new double[ForagerTechnology.Count]};
        var resources=new ForagerResources();var warm=ForagerEcology.At(42,d,0);var basic=ForagerEcology.Livelihood(d,warm,lineage,c,culture,resources);
        Check(basic.Capacity>50&&basic.Capacity<3000,"Warm productive district has a bounded forager carrying opportunity, not agricultural density");
        var cold=d.Copy();cold.Center=new V(.6,0,.8);cold.WinterC=-28;cold.SummerC=14;cold.MeanC=-7;cold.GrowingDays=100;
        var coldEnvironment=ForagerEcology.At(42,cold,179000);var unprotected=ForagerEcology.Livelihood(cold,coldEnvironment,lineage,c,culture,resources);
        var equipped=c.Copy();Array.Fill(equipped.Use,1);var protectedLife=ForagerEcology.Livelihood(cold,coldEnvironment,lineage,equipped,culture,resources);
        Check(protectedLife.Capacity>unprotected.Capacity*20&&protectedLife.WinterSecurity>unprotected.WinterSecurity,"Clothing/fire/shelter materially change winter habitability; body form alone cannot substitute");
        var frozen=cold.Copy();frozen.SummerC=-4;frozen.WinterC=-45;frozen.MeanC=-22;
        var ice=ForagerEcology.At(42,frozen,179000);Check(ice.IceCover>.5&&ForagerEcology.Livelihood(frozen,ice,lineage,equipped,culture,resources).Capacity<protectedLife.Capacity,"Perennial ice suppresses settlement even with a complete cold toolkit");
        var arid=d.Copy();arid.Water=.01;arid.Aridity=.1;
        Check(ForagerEcology.Livelihood(arid,ForagerEcology.At(42,arid,0),lineage,c,culture,resources).Capacity<basic.Capacity*.5,"Water access independently limits foraging populations");
        var rich=d.Copy();rich.Pathogens=1;var sick=ForagerEcology.Livelihood(rich,ForagerEcology.At(42,rich,0),lineage,c,culture,resources);
        Check(sick.Health<basic.Health&&sick.Capacity<basic.Capacity,"Pathogen burden is not silently replaced by higher productivity");
        var hunted=new ForagerResources();ForagerEcology.RecoverResources(hunted,100,1000,200,new[]{0d,0d,1d,0d,0d});
        Check(hunted.Stock[2]<.4&&hunted.Stock[0]>.99,"Repeated game pressure affects game rather than indiscriminately deleting all resources");
        double low=hunted.Stock[2];ForagerEcology.RecoverResources(hunted,100,10,200,new[]{0d,0d,1d,0d,0d});Check(hunted.Stock[2]>low,"Depleted resources recover when pressure is removed");
        var warmLearner=c.Copy();var coldLearner=c.Copy();var teachers=Enumerable.Repeat(.9,ForagerTechnology.Count).ToArray();
        var productiveCold=cold.Copy();productiveCold.WinterC=-14;productiveCold.SummerC=22;productiveCold.MeanC=4;var viableCold=ForagerEcology.At(42,productiveCold,75000);
        for(int year=50;year<=10000;year+=50)
        {
            ForagerTechnology.Advance(42,warmLearner,d,warm,culture,50,year,teachers,150);
            ForagerTechnology.Advance(42,coldLearner,productiveCold,viableCold,culture,50,year,teachers,150);
        }
        Check(coldLearner.Use[13]>warmLearner.Use[13]*5&&coldLearner.Use[14]>warmLearner.Use[14]*5,"Equal population/contact/time does not force identical clothing use in different climates");
        Check(warmLearner.Knowledge[13]>warmLearner.Use[13],"Knowing clothing exists is distinct from routinely making/using it");
        Check(warmLearner.Skill[0]>.8&&warmLearner.Skill[3]>.7&&coldLearner.Skill[14]>.7,"Daily practised methods retain expert transmission instead of eroding through multiplied prerequisite/material caps");
        var lost=c.Copy();Array.Fill(lost.Use,.1);double prior=lost.Skill[24];var dryInland=arid.Copy();dryInland.Coast=dryInland.River=0;dryInland.Food[3]=dryInland.Food[4]=0;
        for(int year=50;year<=10000;year+=50)ForagerTechnology.Advance(79,lost,dryInland,ForagerEcology.At(79,dryInland,year),culture,50,year,new double[ForagerTechnology.Count],0);
        Check(lost.Skill[24]<prior*.5,"Unneeded specialised skills can be lost in isolation");
        var visitor=c.Copy();visitor.Knowledge[4]=visitor.Skill[4]=visitor.Use[4]=0;var regular=visitor.Copy();
        double slowEarly=0,fastEarly=0;
        for(int year=50;year<=20000;year+=50){ForagerTechnology.Advance(42,visitor,d,warm,culture,50,year,teachers,10,.025);ForagerTechnology.Advance(42,regular,d,warm,culture,50,year,teachers,150,1);if(year==500){slowEarly=visitor.Knowledge[4];fastEarly=regular.Knowledge[4];}}
        Check(visitor.Knowledge[4]>.6&&fastEarly>slowEarly+.1,"Rare skilled visitors teach slowly rather than having their expertise permanently diluted into an unlearnable ceiling");
        var copy=c.Copy();copy.Skill[0]=0;Check(c.Skill[0]==.9,"Cohort copy isolates skill arrays");
        var before=ForagerEcology.At(42,cold,180000);var after=ForagerEcology.At(42,cold,180000+1/365.0);
        Check(Math.Abs(before.WinterC-after.WinterC)<.01&&Math.Abs(before.Moisture-after.Moisture)<.01,"Climate/resources do not jump on a gameplay day boundary");
        var corridor=d.Copy();corridor.Center=new V(Math.Cos(23*Math.PI/180),0,Math.Sin(23*Math.PI/180));corridor.PetMm=2000;corridor.AnnualRainMm=40;corridor.Aridity=.02;corridor.Water=.01;corridor.Productivity=20;corridor.Canopy=0;corridor.OceanMoistureAccess=.8;corridor.Food=new[]{.003,.01,.01,0d,0d};
        var wetWindow=ForagerEcology.At(42,corridor,75000);var dryWindow=ForagerEcology.At(42,corridor,64000);
        Check(wetWindow.RainMm>dryWindow.RainMm+150&&wetWindow.Aridity>dryWindow.Aridity*3,"Orbital wet windows add real rain to a modern hyperarid corridor rather than multiplying almost-zero rain");
        Check(wetWindow.Productivity>dryWindow.Productivity*3&&wetWindow.Food[0]>dryWindow.Food[0]*4&&wetWindow.Water>dryWindow.Water,"Wet corridors change vegetation/food/water together");
        var blocked=corridor.Copy();blocked.OceanMoistureAccess=0;Check(ForagerEcology.At(42,blocked,75000).RainMm<wetWindow.RainMm*.3,"Warm-sea access and mountain isolation constrain the wet envelope");
        var nextDay=ForagerEcology.At(42,corridor,75000+1/365d);Check(Math.Abs(wetWindow.RainMm-nextDay.RainMm)<.01&&Math.Abs(wetWindow.Productivity-nextDay.Productivity)<.01,"Wet-period ecology changes smoothly at gameplay day scales");
        var demographic=c.Copy();ForagerDemography.SetPopulation(demographic,200);ForagerDemography.Rates(demographic,basic,warm,culture,500);
        var projection=ForagerDemography.Project(demographic,5,0);Check(Math.Abs(projection.Population-(200+projection.Births-projection.Deaths))<.0000001,"Births, deaths and ageing conserve population without a hidden capacity clamp");
        var destination=c.Copy();ForagerDemography.SetPopulation(destination,0);double total=demographic.Population+destination.Population;ForagerDemography.MovePopulation(demographic,destination,25);
        Check(Math.Abs(demographic.Population+destination.Population-total)<.0000001&&Math.Abs(destination.Population-25)<.0000001,"Migration transfers age stocks instead of spawning or deleting people");
        var doomed=demographic.Copy();ForagerDemography.Rates(doomed,basic,warm,culture,0);Check(ForagerDemography.Project(doomed,5,0).Population<doomed.Population*.01,"An uninhabitable district cannot sustain a permanent residual population");
        Console.WriteLine($"PASS {checks} forager mechanism checks; warm capacity={basic.Capacity:F1}; cold unprotected={unprotected.Capacity:F4}; protected={protectedLife.Capacity:F1}; warm fitted={warmLearner.Use[13]:F3}; cold fitted={coldLearner.Use[13]:F3}");
    }
}
