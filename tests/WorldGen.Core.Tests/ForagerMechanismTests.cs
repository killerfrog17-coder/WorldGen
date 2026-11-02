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
        var experiment=c.Copy();experiment.Adults=40;experiment.Children=0;experiment.Elders=0;Array.Fill(experiment.Knowledge,.9);Array.Fill(experiment.Skill,.9);experiment.Knowledge[17]=experiment.Skill[17]=experiment.Use[17]=0;
        experiment.FirstKnownYear=Enumerable.Repeat(-1,ForagerTechnology.Count).ToArray();
        var experimentEnvironment=ForagerEcology.At(42,d,0);
        ulong learned=ForagerTechnology.Advance(42,experiment,d,experimentEnvironment,culture,5,5,null,1e9);
        Check((learned&(1UL<<17))!=0&&experiment.Skill[17]>0,"An observed invention introduces actual trained makers rather than only knowledge with no possible teacher");
        experiment.FirstKnownYear[17]=5;
        for(int year=10;year<=1500;year+=5)ForagerTechnology.Advance(42,experiment,d,experimentEnvironment,culture,5,year,null,0);
        Check(experiment.Skill[17]>.35,"A useful new practice can become self-sustaining after temporary experimentation support ends");
        var fluent=c.Copy();var unfamiliar=c.Copy();fluent.Adults=unfamiliar.Adults=40;fluent.Knowledge[17]=unfamiliar.Knowledge[17]=0;fluent.Skill[17]=unfamiliar.Skill[17]=0;fluent.Use[17]=unfamiliar.Use[17]=0;
        for(int year=5;year<=100;year+=5)
        {
            ForagerTechnology.Advance(42,fluent,d,experimentEnvironment,culture,5,year,Enumerable.Repeat(.9,ForagerTechnology.Count).ToArray(),0,explanatoryAccess:1);
            ForagerTechnology.Advance(42,unfamiliar,d,experimentEnvironment,culture,5,year,Enumerable.Repeat(.9,ForagerTechnology.Count).ToArray(),0,explanatoryAccess:0);
        }
        Check(fluent.Knowledge[17]>unfamiliar.Knowledge[17]*2&&unfamiliar.Knowledge[17]>0,"Shared language/bilingual explanation speeds complex teaching while observation still allows slower learning");
        var expert=new double[ForagerTechnology.Count];expert[17]=.9;
        var nobody=new double[ForagerTechnology.Count];
        var rareContext=ForagerTechnology.TeacherContext(new[]{(expert,.025),(nobody,.8),(nobody,.7)});
        Check(rareContext[17]>.89&&Math.Abs(rareContext[ForagerTechnology.Count+17]-.025)<1e-8,"A rare expert retains expertise while unrelated unskilled contacts add no teaching access for that method");
        var rareLearner=c.Copy();var frequentLearner=c.Copy();rareLearner.Adults=frequentLearner.Adults=40;rareLearner.Knowledge[17]=rareLearner.Skill[17]=rareLearner.Use[17]=frequentLearner.Knowledge[17]=frequentLearner.Skill[17]=frequentLearner.Use[17]=0;
        var frequentContext=ForagerTechnology.TeacherContext(new[]{(expert,.8)});double rareAt100=0,frequentAt100=0;
        for(int year=5;year<=20000;year+=5)
        {
            ForagerTechnology.Advance(42,rareLearner,d,experimentEnvironment,culture,5,year,rareContext,0,1);
            ForagerTechnology.Advance(42,frequentLearner,d,experimentEnvironment,culture,5,year,frequentContext,0,1);
            if(year==100){rareAt100=rareLearner.Knowledge[17];frequentAt100=frequentLearner.Knowledge[17];}
        }
        Check(frequentAt100>rareAt100*3,"Technique-specific access makes rare instruction slower than frequent instruction");
        Check(rareLearner.Knowledge[17]>.6&&rareLearner.Skill[17]>.35,"Repeated rare expert visits can eventually establish a useful practice without an artificial averaged-expertise ceiling");
        var resources=new ForagerResources();var warm=ForagerEcology.At(42,d,0);var basic=ForagerEcology.Livelihood(d,warm,lineage,c,culture,resources);
        Check(basic.Capacity>50&&basic.Capacity<3000,"Warm productive district has a bounded forager carrying opportunity, not agricultural density");
        var cold=d.Copy();cold.Center=new V(.6,0,.8);cold.WinterC=-28;cold.SummerC=14;cold.MeanC=-7;cold.GrowingDays=100;
        var coldEnvironment=ForagerEcology.At(42,cold,179000);var unprepared=c.Copy();Array.Clear(unprepared.Skill);var unprotected=ForagerEcology.Livelihood(cold,coldEnvironment,lineage,unprepared,culture,resources);
        var equipped=c.Copy();Array.Fill(equipped.Use,1);var protectedLife=ForagerEcology.Livelihood(cold,coldEnvironment,lineage,equipped,culture,resources);
        Check(protectedLife.Capacity>unprotected.Capacity*20&&protectedLife.WinterSecurity>unprotected.WinterSecurity,"Clothing/fire/shelter materially change winter habitability; body form alone cannot substitute");
        var seasonalEquipment=ForagerEcology.Livelihood(cold,coldEnvironment,lineage,c,culture,resources);
        Check(seasonalEquipment.WinterSecurity>unprotected.WinterSecurity&&seasonalEquipment.Capacity>unprotected.Capacity*20,"Skilled people prepare known winter gear; low annual use does not force them to enter winter naked");
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
        var coastal=d.Copy();coastal.Coast=1;var coastEnvironment=ForagerEcology.At(42,coastal,0);
        var shortWater=new ForagerPassage{Sea=true,DistanceM=240000,WaterDistanceM=12000};var deepWater=new ForagerPassage{Sea=true,DistanceM=400000,WaterDistanceM=250000};
        var navigator=c.Copy();Array.Clear(navigator.Use);var noBoat=navigator.Copy();Array.Clear(noBoat.Skill);
        Check(ForagerPassages.Open(42,shortWater,coastal,coastEnvironment,coastEnvironment,navigator)&&!ForagerPassages.Open(42,shortWater,coastal,coastEnvironment,coastEnvironment,noBoat),"Known boat-building competence prepares a short crossing even when boats were not used every day; untrained people cannot do so");
        Check(!ForagerPassages.Open(42,deepWater,coastal,coastEnvironment,coastEnvironment,navigator),"Paleolithic boats do not unlock arbitrary deep-ocean continent hopping");
        var demographic=c.Copy();ForagerDemography.SetPopulation(demographic,200);ForagerDemography.Rates(demographic,basic,warm,culture,500);
        var projection=ForagerDemography.Project(demographic,5,0);Check(Math.Abs(projection.Population-(200+projection.Births-projection.Deaths))<.0000001,"Births, deaths and ageing conserve population without a hidden capacity clamp");
        var destination=c.Copy();ForagerDemography.SetPopulation(destination,0);double total=demographic.Population+destination.Population;ForagerDemography.MovePopulation(demographic,destination,25);
        Check(Math.Abs(demographic.Population+destination.Population-total)<.0000001&&Math.Abs(destination.Population-25)<.0000001,"Migration transfers age stocks instead of spawning or deleting people");
        var doomed=demographic.Copy();ForagerDemography.Rates(doomed,basic,warm,culture,0);Check(ForagerDemography.Project(doomed,5,0).Population<doomed.Population*.01,"An uninhabitable district cannot sustain a permanent residual population");
        double oldSkill=doomed.Skill[0];for(int year=0;year<100;year+=5)ForagerDemography.Materialize(doomed,5,year);
        Check(doomed.Population==0&&doomed.Teaching.Length==0&&doomed.Innovation.Length==0&&doomed.TeachingFrom.Length==0&&doomed.Skill[0]==oldSkill,"Retired communities retain historical skills and ancestry without live teaching/invention caches");
        Console.WriteLine($"PASS {checks} forager mechanism checks; warm capacity={basic.Capacity:F1}; cold unprotected={unprotected.Capacity:F4}; protected={protectedLife.Capacity:F1}; warm fitted={warmLearner.Use[13]:F3}; cold fitted={coldLearner.Use[13]:F3}");
    }
}
