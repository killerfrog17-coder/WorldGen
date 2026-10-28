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
        // Derived read-only from the shipped Earth snapshot: accessible tropical
        // coast, actual wet sample and nearby rooted soil. No terrain edits.
        V bank=TerrainMapViewport.FromDegrees(.17930170506363216,117.31300341710354);
        V wet=TerrainMapViewport.FromDegrees(.17924039827946195,117.31312959624624);
        Teleport(bank);paused=false;double deadline=Time.unscaledTimeAsDouble+70;
        while(!CenterReady&&Time.unscaledTimeAsDouble<deadline)yield return null;paused=true;
        var wetSample=walkingPatch.Sample(wet);Check(CenterReady&&!walkingPatch.Sample(bank).Underwater,"Actual dry shore terrain loads");
        Check(!double.IsNaN(wetSample.WaterSurfaceM)&&wetSample.WaterSurfaceM-wetSample.ElevationM>.3,"Real water lies within reach of the shore; scale "+HorizontalScale+", bed "+wetSample.ElevationM+", water "+wetSample.WaterSurfaceM);
        TerrainHandoff.Coordinates(frame,bank,out double bx,out double bz);V digAt=default;bool roots=false;
        for(int ring=0;ring<10&&!roots;ring++)for(int side=0;side<8&&!roots;side++)
        {double a=side*Math.PI/4;var p=frame.Position(bx+Math.Sin(a)*ring*3,bz+Math.Cos(a)*ring*3);var s=walkingPatch.Sample(p);if(!s.Underwater&&s.Ground.DepthM>=.12&&s.Ground.RootSupport>=.12){digAt=p;roots=true;}}
        Check(roots,"Nearby unfrozen rooted soil exists for the real digging action");
        TerrainHandoff.Coordinates(frame,digAt,out double dx,out double dz);motion.Place(dx,dz,Ground(dx,dz));UpdateHumans();UpdateCamera();
        ExecuteGive(new[]{"/give","wooden","spear"});var spear=Inventory.Items.First(p=>p.Kind==PossessionKind.WoodenSpear);ExecuteGive(new[]{"/give","digging","stick"});var digging=Inventory.Items.First(p=>p.Kind==PossessionKind.DiggingStick);
        Check(spear.Place==PossessionPlace.RightHand,"Shore work begins with the original spear actually held");
        BeginDigging(digging);Check(worldToolWork!=null,"Digging starts through the actual ground/tool checks; temperature "+CurrentWeather.TemperatureC);
        paused=false;deadline=Time.unscaledTimeAsDouble+30;while(worldToolWork!=null&&Time.unscaledTimeAsDouble<deadline)yield return null;paused=true;
        Check(worldToolWork==null&&Inventory.Items.Any(p=>p.Kind==PossessionKind.RawFibre)&&spear.Place==PossessionPlace.RightHand,"Actual timed digging grants fibres and restores the displaced spear; work="+(worldToolWork?.Done.ToString()??"ended")+", fibres="+Inventory.Items.Count(p=>p.Kind==PossessionKind.RawFibre)+", spear="+spear.Place+", message="+inventoryNotice);
        motion.Place(bx,bz,Ground(bx,bz));UpdateHumans();UpdateCamera();var fishProfile=animalProfiles.First(p=>p.slug=="coastal-forage-fish-guild");
        paused=false;deadline=Time.unscaledTimeAsDouble+35;portableCoatWanted.Add(fishProfile.slug);
        while(!animalModels.ContainsKey(fishProfile.slug)&&Time.unscaledTimeAsDouble<deadline)yield return null;paused=true;Check(animalModels.ContainsKey(fishProfile.slug),"Original coastal fish model and hit skin load");
        var spawn=new FaunaPrototypeSpawn{Id=++animalManualSequence,Profile=fishProfile,Position=wet,GroundM=wetSample.ElevationM,WaterM=wetSample.WaterSurfaceM,HeightM=-.15,Taxon=-1,Hash=455};
        AddAnimal(spawn,animalModels[fishProfile.slug],true);var fish=animals[spawn.Id];fish.Fade=1;UpdateAnimal(fish,0);
        var directionToFish=(fish.Renderer.bounds.center-view.transform.position).normalized;heading=Mathf.Atan2(directionToFish.x,directionToFish.z)*Mathf.Rad2Deg;pitch=-Mathf.Asin(directionToFish.y)*Mathf.Rad2Deg;UpdateCamera();
        ExecuteGive(new[]{"/give","hand","line"});var line=Inventory.Items.First(p=>p.Kind==PossessionKind.HandLine);BeginFishing(line);
        Check(worldToolWork?.Fish==fish&&fish.FishingTethered&&worldToolWork.Line!=null,"Actual hand-line action tethers the nearby fish and draws its line");
        paused=false;deadline=Time.unscaledTimeAsDouble+18;while(worldToolWork!=null&&Time.unscaledTimeAsDouble<deadline)yield return null;paused=true;
        Check(worldToolWork==null&&fish.Dead&&fish.Ragdoll!=null&&!fish.FishingTethered,"Timed hand-line lands the same whole fish; no replacement or processed meat");
        Check(!walkingPatch.Sample(fish.Planet).Underwater&&spear.Place==PossessionPlace.RightHand,"Catch is brought back onto the bank and the original hands return");
        var netSpawn=new FaunaPrototypeSpawn{Id=++animalManualSequence,Profile=fishProfile,Position=wet,GroundM=wetSample.ElevationM,WaterM=wetSample.WaterSurfaceM,HeightM=-.15,Taxon=-1,Hash=456};
        AddAnimal(netSpawn,animalModels[fishProfile.slug],true);var netFish=animals[netSpawn.Id];netFish.Fade=1;UpdateAnimal(netFish,0);
        ExecuteGive(new[]{"/give","net"});var net=Inventory.Items.First(p=>p.Kind==PossessionKind.FibreNet);BeginFishing(net);
        Check(worldToolWork?.Fish==netFish&&netFish.FishingTethered&&worldToolWork.Line==null,"Actual hand net reaches a shallow fish without creating a fishing line");
        paused=false;deadline=Time.unscaledTimeAsDouble+10;while(worldToolWork!=null&&Time.unscaledTimeAsDouble<deadline)yield return null;paused=true;
        Check(worldToolWork==null&&netFish.Dead&&!netFish.FishingTethered&&spear.Place==PossessionPlace.RightHand,"Timed net action lands the actual fish and restores equipment");
        CaptureWildlife();Inventory.Validate();Wildlife.Validate();File.WriteAllLines(Path.Combine(output,"water-checks.txt"),report);
    }
}
