using WorldGen.Core;
static class ForagerBiotaTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        var species=new Taxon{Id=0,Name="Warm browsing mammal",MassKg=80,MinColdTenthsC=60,MaxColdTenthsC=400,MinWarmTenthsC=160,MaxWarmTenthsC=450,MinAridityMilli=100,MaxAridityMilli=8000,MaxElevationM=3000,Habitats=Habitat.Open,Eats=Food.Browse,Provides=Food.Vertebrate,Occupies=Stratum.Ground};
        ForagerDistrict District(int id,bool cold=false)=>new ForagerDistrict{Id=id,Cell=id,Cells=new[]{id},Province=id,Center=new V(Math.Cos(id*.02),Math.Sin(id*.02),0),AreaKm2=10000,WinterC=cold?-35:18,SummerC=cold?0:32,MeanC=cold?-15:24,Productivity=650,Aridity=1,Water=.8,Canopy=.1,Food=new[]{.7,.5,.5,.1,.1}};
        var world=new World{Recipe=new Recipe{Seed=231},ElevationM=new[]{100,100,100},Ecology=new EcologyResult{Taxa=new[]{species}}};
        ForagerState State()
        {
            var f=new ForagerState{Districts=new[]{District(0),District(1,true),District(2)},Resources=Enumerable.Range(0,3).Select(i=>new ForagerResources{District=i}).ToArray()};
            f.Districts[0].Passages=new[]{new ForagerPassage{To=1,DistanceM=100000}};f.Districts[1].Passages=new[]{new ForagerPassage{To=0,DistanceM=100000}};
            f.Biota=new[]{new ForagerBiotaDistrict{District=0,Taxa=new[]{0},Abundance=new[]{.8},BaselineFood=new[]{0d,0,1,0,0}},new ForagerBiotaDistrict{District=1},new ForagerBiotaDistrict{District=2}};return f;
        }
        var f=State();var warm=ForagerEcology.At(world.Recipe.Seed,f.Districts[0],0);var cold=ForagerEcology.At(world.Recipe.Seed,f.Districts[1],0);
        Check(ForagerBiota.Suitability(species,f.Districts[0],warm,100)>.5&&ForagerBiota.Suitability(species,f.Districts[1],cold,100)==0,"A taxon's thermal limits change actual habitat suitability");
        ForagerEnvironment[] EnvironmentAt(int year)=>f.Districts.Select(d=>ForagerEcology.At(world.Recipe.Seed,d,year)).ToArray();
        for(int year=250;year<=2000;year+=250)ForagerBiota.Advance(world,f,EnvironmentAt(year),year,250);
        Check(f.Biota[1].Presence(0)==0&&f.Biota[2].Presence(0)==0,"Neither a frozen neighbour nor a remote suitable island receives an instant population");
        f.Districts[1]=District(1);f.Districts[1].Passages=new[]{new ForagerPassage{To=0,DistanceM=100000}};
        for(int year=2250;year<=10000;year+=250)ForagerBiota.Advance(world,f,EnvironmentAt(year),year,250);
        Check(f.Biota[1].Presence(0)>.4,"After warming, neighbouring source populations can colonise through a real route");
        Check(f.Biota[2].Presence(0)==0,"Warm isolated habitat cannot manufacture an absent animal");
        var copy=f.Copy();copy.Biota[0].Abundance[0]=0;Check(f.Biota[0].Presence(0)>0,"Ecological checkpoint copies isolate sparse abundance arrays");
        foreach(var d in f.Districts){d.Passages=Array.Empty<ForagerPassage>();d.WinterC=-40;d.SummerC=-4;d.MeanC=-20;}
        for(int year=10250;year<=11000;year+=250)ForagerBiota.Advance(world,f,EnvironmentAt(year),year,250);
        Check(f.Biota.All(b=>b.Presence(0)==0),"Prolonged unsuitable conditions remove real local populations");
        Check(f.Biota[0].FoodAvailability[2]==0,"Extinct local game does not leave a phantom minimum meat supply");
        foreach(var d in f.Districts){d.WinterC=18;d.SummerC=32;d.MeanC=24;}
        for(int year=11250;year<=13000;year+=250)ForagerBiota.Advance(world,f,EnvironmentAt(year),year,250);
        Check(f.Biota.All(b=>b.Presence(0)==0),"After complete source extinction, warming alone does not resurrect a taxon");
        var changed=ForagerBiota.Apply(warm,f.Biota[0]);Check(changed.Food[2]==0&&warm.Food[2]>0,"The ecological record actually constrains the human food opportunity");
        Console.WriteLine($"PASS {checks} dynamic-biota/refugium/dispersal/food-coupling checks");
    }
}
