using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckItemWaterFlow(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("ITEM WATER "+text);report.Add("PASS "+text);Debug.Log("ITEM WATER PASS "+text);}
        PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();PlayerRules.Settings.ItemCraftingTimeMultiplier=1;PlayerRules.Settings.RealMinutesPerDay=60;
        var river=terrain.Hydrology.Channels.Where(c=>c.WidthM>40&&c.WidthM<200&&c.StartM>20&&c.StartM<1500).OrderBy(c=>Math.Abs(c.Position(.5).Z)).First();
        Teleport(river.Position(.5));paused=false;double deadline=Time.unscaledTimeAsDouble+70;
        while(!CenterReady&&Time.unscaledTimeAsDouble<deadline)yield return null;Check(CenterReady,"Actual riverbank terrain loads");paused=true;
        var spans=walkingPatch.RiverSpans().Where(s=>s.Depth>.3&&s.WidthA<50&&s.WidthB<50).OrderBy(s=>((s.A+s.B).Unit-Position).Length).ToArray();
        bool centerFound=false;
        foreach(var span in spans)
        {
            var point=(span.A+span.B).Unit;var s=walkingPatch.Sample(point);if(double.IsNaN(s.WaterSurfaceM)||s.WaterSurfaceM-s.ElevationM<.3)continue;
            Teleport(point);paused=false;deadline=Time.unscaledTimeAsDouble+40;while(!CenterReady&&Time.unscaledTimeAsDouble<deadline)yield return null;paused=true;centerFound=true;break;
        }
        Check(centerFound,"Fixture follows the current rendered riverscape rather than the older coarse channel curve");
        V bank=default,wet=default;LocalTerrainSample wetSample=default;bool found=false;
        var probes=new List<string>{"direction,distance,ground,water,kind,underwater"};
        for(int direction=0;direction<8&&!found;direction++)
        {
            V lastWet=default;LocalTerrainSample previous=default;bool hasWet=false;double angle=direction*Math.PI/4;
            for(int step=0;step<200;step++)
            {
                var point=frame.Position(Math.Sin(angle)*step*2,Math.Cos(angle)*step*2);var sample=walkingPatch.Sample(point);
                probes.Add(direction+","+step*2+","+sample.ElevationM+","+sample.WaterSurfaceM+","+sample.WaterKind+","+sample.Underwater);
                if(!double.IsNaN(sample.WaterSurfaceM)&&sample.WaterSurfaceM-sample.ElevationM>.3){lastWet=point;previous=sample;hasWet=true;}
                if(hasWet&&!sample.Underwater&&(double.IsNaN(sample.WaterSurfaceM)||sample.WaterSurfaceM<=sample.ElevationM+.03)&&(point-lastWet).Length*frame.RadiusM<6){bank=point;wet=lastWet;wetSample=previous;found=true;break;}
            }
        }
        File.WriteAllLines(Path.Combine(output,"bank-probes.csv"),probes);
        Check(found,"A dry bank lies within hand-line reach of real water");
        TerrainHandoff.Coordinates(frame,bank,out double bx,out double bz);V digAt=default;bool roots=false;
        for(int ring=0;ring<10&&!roots;ring++)for(int side=0;side<8&&!roots;side++)
        {double a=side*Math.PI/4;var p=frame.Position(bx+Math.Sin(a)*ring*3,bz+Math.Cos(a)*ring*3);var s=walkingPatch.Sample(p);if(!s.Underwater&&s.Ground.DepthM>=.12&&s.Ground.RootSupport>=.12){digAt=p;roots=true;}}
        Check(roots,"Nearby unfrozen rooted soil exists for the real digging action");
        TerrainHandoff.Coordinates(frame,digAt,out double dx,out double dz);motion.Place(dx,dz,Ground(dx,dz));UpdateHumans();UpdateCamera();
        ExecuteGive(new[]{"/give","wooden","spear"});var spear=Inventory.Items.First(p=>p.Kind==PossessionKind.WoodenSpear);ExecuteGive(new[]{"/give","digging","stick"});var digging=Inventory.Items.First(p=>p.Kind==PossessionKind.DiggingStick);
        BeginDigging(digging);Check(worldToolWork!=null,"Digging starts through the actual ground/tool checks; temperature "+CurrentWeather.TemperatureC);
        paused=false;deadline=Time.unscaledTimeAsDouble+12;while(worldToolWork!=null&&Time.unscaledTimeAsDouble<deadline)yield return null;paused=true;
        Check(worldToolWork==null&&Inventory.Items.Any(p=>p.Kind==PossessionKind.RawFibre)&&spear.Place==PossessionPlace.RightHand,"Actual timed digging grants fibres and restores the displaced spear");
        motion.Place(bx,bz,Ground(bx,bz));UpdateHumans();UpdateCamera();var fishProfile=animalProfiles.First(p=>p.slug=="small-native-freshwater-fish-guild");
        paused=false;deadline=Time.unscaledTimeAsDouble+35;while(animalLoading!=null&&Time.unscaledTimeAsDouble<deadline)yield return null;
        if(!animalModels.ContainsKey(fishProfile.slug))animalLoading=StartCoroutine(LoadAnimalModel(fishProfile));
        while(!animalModels.ContainsKey(fishProfile.slug)&&Time.unscaledTimeAsDouble<deadline)yield return null;paused=true;Check(animalModels.ContainsKey(fishProfile.slug),"Original freshwater fish model and hit skin load");
        var spawn=new FaunaPrototypeSpawn{Id=++animalManualSequence,Profile=fishProfile,Position=wet,GroundM=wetSample.ElevationM,WaterM=wetSample.WaterSurfaceM,HeightM=-.15,Taxon=-1,Hash=455};
        AddAnimal(spawn,animalModels[fishProfile.slug],true);var fish=animals[spawn.Id];fish.Fade=1;UpdateAnimal(fish,0);
        var directionToFish=(fish.Renderer.bounds.center-view.transform.position).normalized;heading=Mathf.Atan2(directionToFish.x,directionToFish.z)*Mathf.Rad2Deg;pitch=-Mathf.Asin(directionToFish.y)*Mathf.Rad2Deg;UpdateCamera();
        ExecuteGive(new[]{"/give","hand","line"});var line=Inventory.Items.First(p=>p.Kind==PossessionKind.HandLine);BeginFishing(line);
        Check(worldToolWork?.Fish==fish&&fish.FishingTethered&&worldToolWork.Line!=null,"Actual hand-line action tethers the nearby fish and draws its line");
        paused=false;deadline=Time.unscaledTimeAsDouble+18;while(worldToolWork!=null&&Time.unscaledTimeAsDouble<deadline)yield return null;paused=true;
        Check(worldToolWork==null&&fish.Dead&&fish.Ragdoll!=null&&!fish.FishingTethered,"Timed hand-line lands the same whole fish; no replacement or processed meat");
        Check(!walkingPatch.Sample(fish.Planet).Underwater&&spear.Place==PossessionPlace.RightHand,"Catch is brought back onto the bank and the original hands return");
        CaptureWildlife();Inventory.Validate();Wildlife.Validate();File.WriteAllLines(Path.Combine(output,"water-checks.txt"),report);
    }
}
