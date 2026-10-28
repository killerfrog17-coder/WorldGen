using System;
using System.IO;
using System.Linq;
using System.Threading;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class FireflyTests
{
    public static void Run(string worldPath)
    {
        int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        foreach(string slug in new[]{"butterflies-and-moths-guild","fireflies-derived-lineage"})
        {
            var p=new FaunaPrototypeProfile{slug=slug,tiny=true,radiusM=22,derivedFirefly=slug.StartsWith("fireflies")};
            var b=WildlifeBehaviour.Describe(slug,.001,Food.None,true,true);
            Check(p.VisibleRadiusM==220&&b.TrackingM==220&&b.Tier==WildlifeTrackingTier.Local,"Visible insects use the220m local tier: "+slug);
        }
        Check(WildlifeBehaviour.Describe("ant",.001,Food.None,false,true).TrackingM==35,"Other minute insects keep their existing short range");
        var world=WorldFile.Read(File.ReadAllBytes(worldPath));
        var profiles=WorldJson.Read<FaunaPrototypeCatalogue>(File.ReadAllText("unity/WorldGen.Unity/Assets/WorldGen/Resources/WorldGenFaunaCatalogue.json")).animals;
        bool spawned=false;
        foreach(var at in new[]{(0d,25d),(.18,117.31),(50d,10d)})
        {
            var point=TerrainMapViewport.FromDegrees(at.Item1,at.Item2);var terrain=new LocalTerrain(world,horizontalScale:.1);
            var local=terrain.Patch(point,32000).Sample(point);int region=world.Ecology.Region[local.Cell];
            var species=AmbientFauna.For(world).Region(region).Single(s=>s.Family==AmbientFaunaFamily.Firefly);
            Check(ReferenceEquals(species,AmbientFauna.For(world).Region(region).Single(s=>s.Family==AmbientFaunaFamily.Firefly))&&species.Display!=null,"Spawner shares the cached generated regional firefly profile");
            double year=(at.Item1>=0?.56:.06)+species.Activity.SeasonPeakOffsetDays/365,hour=(species.Activity.StartSolarHour+2)%24,temp=species.Activity.PreferredTemperatureC;
            double gain=FireflyDisplay.ActivityGain(species,hour,year,at.Item1,temp,.9,0,0);
            Check(gain>.8,"Warm moist calm display-season evening is active");
            Check(FireflyDisplay.ActivityGain(species,12,year,at.Item1,temp,.9,0,0)==0,"No flashing in daylight");
            Check(FireflyDisplay.ActivityGain(species,hour,year,at.Item1,0,.9,0,0)==0,"No cold-weather display");
            Check(FireflyDisplay.ActivityGain(species,hour,year,at.Item1,temp,.9,30,20)<gain*.02,"Heavy rain and wind suppress display");
            Check(FireflyDisplay.ActivityGain(species,hour,year,at.Item1,temp,.1,0,0)==0,"Very dry air suppresses activity");
            if(species.Activity.SeasonCue==InsectSeasonCue.AnnualWarmSeason)Check(FireflyDisplay.ActivityGain(species,hour,year+.5,at.Item1,temp,.9,0,0)==0,"Temperate adult display does not occur in the opposite season");
            var wet=local;wet.WaterSurfaceM=wet.ElevationM+2;wet.WaterMaskM=0;Check(FireflyDisplay.HabitatGain(species,world.Ecology.Habitat[local.Cell],wet)==0,"No insects spawned under water");
            double max=0,min=1;bool different=false,bounded=true;
            for(int i=0;i<2000;i++)
            {double a=FireflyDisplay.Pulse(species.Display,i*.01,12345678),b=FireflyDisplay.Pulse(species.Display,i*.01,3012345678);max=Math.Max(max,a);min=Math.Min(min,a);different|=Math.Abs(a-b)>.1;bounded&=a>=0&&a<=1;}
            Check(bounded,"Pulse stays finite and bounded over many frames");
            Check(max>.99&&min==0&&different,"Individual flash bouts have real on/off periods and independent phase");
            var planner=new FaunaPrototypePlanner(world,terrain,profiles,.1);
            var night=planner.Build(point,.1,hour,CancellationToken.None,year,temp,.9,0,0).Where(s=>s.Profile.derivedFirefly).ToArray();
            var noon=planner.Build(point,.1,12,CancellationToken.None,year,temp,.9,0,0);
            Check(!noon.Any(s=>s.Profile.derivedFirefly),"Actual planner produces no noon firefly candidates");
            Check(night.All(s=>(s.Position-point).Length*terrain.RadiusM*.1<=220.001),"Actual visual firefly candidates respect220m");
            spawned|=night.Any(s=>(s.Position-point).Length*terrain.RadiusM*.1>35);
            Console.WriteLine("Firefly site "+at+": region "+region+", habitat "+FireflyDisplay.HabitatGain(species,world.Ecology.Habitat[local.Cell],local)+", evening candidates "+night.Length);
        }
        Check(spawned,"Generated ecoregions produce actual night fireflies beyond the old35m tracking boundary");
        Console.WriteLine("PASS "+checks+" regional firefly activity, spawning, pulse and visibility checks");
    }
}
