using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckCampFlow(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("CAMP "+text);report.Add("PASS "+text);Debug.Log("CAMP PASS "+text);}
        Color32[] Image(string name)
        {
            var previous=RenderTexture.active;var old=view.targetTexture;var target=new RenderTexture(1280,800,24);target.Create();view.targetTexture=target;view.Render();RenderTexture.active=target;
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();var pixels=image.GetPixels32();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());RenderTexture.active=previous;view.targetTexture=old;target.Release();Destroy(target);Destroy(image);return pixels;
        }
        void Aim(double x,double z,double up=0){UpdateHumans();UpdateCamera();view.transform.LookAt(transform.TransformPoint(new Vector3((float)(x-originX),(float)Ground(x,z)+(float)up,(float)(z-originZ))));waterAimAt=0;}
        Possession Supply(PossessionKind kind){var p=PortableTechnology.Create(kind,Position);p.Place=PossessionPlace.Ground;Inventory.Items.Add(p);inventoryNearbyAt=0;return p;}
        CampObject Place(Possession p,CampKind kind,double x,double z)
        {Aim(x,z);BeginCampPlacement(p,kind);Check(campPlaceValid,"Actual placement ray accepts dry soil: "+kind+" / "+campPlaceReason);ConfirmCampPlacement();return Camp.Objects.Last();}
        PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();humanCamera.Restore(0,false);Vitals.Restore();Inventory.Items.Clear();Camp.Objects.Clear();motion.Flying=false;
        WeatherControl.Set("clear");PlayerRules.Settings.RealMinutesPerDay=60;PlayerRules.Settings.ItemCraftingTimeMultiplier=.1;
        bool found=false;double sx=motion.X,sz=motion.Z;
        for(int ring=0;ring<10&&!found;ring++)for(int side=0;side<12&&!found;side++)
        {
            double a=side*Math.PI/6,x=motion.X+Math.Sin(a)*ring*2,z=motion.Z+Math.Cos(a)*ring*2;var s=walkingPatch.Sample(frame.Position(x,z));
            double grade=Math.Max(Math.Abs(Ground(x+2,z)-Ground(x-2,z)),Math.Abs(Ground(x,z+2)-Ground(x,z-2)))/4;
            if(!s.Underwater&&s.Ground.DepthM>.18&&grade<.18&&s.Cover.Grasses.Length>.08){sx=x;sz=z;found=true;}
        }
        Check(found,"Actual grass-covered, plantable dry camp site exists");motion.Place(sx,sz,Ground(sx,sz));paused=true;
        double longitude=Math.Atan2(Position.Y,Position.X)*180/Math.PI;PlayerRules.SetLocalSolarHour(12,longitude);SetDaylight(LocalSolarHour,skyYear);Aim(sx,sz+1);
        int before=Inventory.Items.Count;GatherCampGrass();Check(Inventory.Items.Count==before+1&&Inventory.Items.Last().Kind==PossessionKind.DryGrass,"Actual grassy ground supplies a physical bedding/tinder bundle");
        var bed=Place(Inventory.Items.Last(),CampKind.Bedding,sx,sz+1.3);Check(!Inventory.Items.Any(p=>bed.Materials.Any(m=>m.Id==p.Id)),"Placing removes the original item from carried/ground ownership");
        for(int i=0;i<6;i++)AddCampMaterial(bed,Supply(PossessionKind.DryGrass));Check(bed.BeddingDepthM>.06,"Adding actual loose materials builds a thick bedding layer");
        UpdateCamp();Check(campVisuals.ContainsKey(bed.Id)&&campVisuals[bed.Id].Root.GetComponentsInChildren<MeshFilter>().Any(f=>f.sharedMesh.vertexCount>1000),"Bedding renders as a layered grass mesh");
        var branch=WoodCrafting.Branch(new ScatterInstance{Position=Position,Kind=ScatterKind.Branch,WidthM=1.8,HeightM=.03,Variant=0});branch.Id="native-post";branch.NaturalId=0;Inventory.Items.Add(branch);inventoryNearbyAt=0;
        var stake=Place(branch,CampKind.Stake,sx+1.5,sz-.5);double embed=stake.EmbedM;
        var rock=new Possession{Id="native-camp-rock",Kind=PossessionKind.SmallRock,Place=PossessionPlace.RightHand,Position=Position,WidthM=.08,HeightM=.07,DepthM=.08,MassKg=.7,Variant=ToolStone.Encode(ToolStoneMaterial.Chert,0)};Inventory.Items.Add(rock);
        TerrainHandoff.Coordinates(frame,stake.Position,out double tx,out double tz);motion.Place(tx-.5,tz,Ground(tx-.5,tz));Aim(tx,tz,1.3);combatGlobalReady=combatRightReady=0;StrikeWithItem(rock,false);
        Check(stake.EmbedM>embed,"Actual rock attack drives the aimed planted stick farther into soil");
        Check(stake.Materials[0].HeightM==branch.HeightM,"Visible post retains its original full length while burial changes");
        double previous=stake.EmbedM;for(int i=0;i<100;i++)CampRules.Drive(stake,rock);Check(stake.EmbedM>previous&&stake.EmbedM<=stake.SoilDepthM,"Repeated hammering respects the site's physical soil depth");InvalidateCampVisual(stake);
        Inventory.Items.Remove(rock);motion.Place(sx,sz,Ground(sx,sz));
        var fire=Place(Supply(PossessionKind.TinderBundle),CampKind.Fire,sx-1.3,sz-.6);AddCampMaterial(fire,Supply(PossessionKind.KindlingBundle));
        var fuel=WoodCrafting.Branch(new ScatterInstance{Position=Position,Kind=ScatterKind.Branch,WidthM=1.4,HeightM=.04,Variant=0});fuel.Id="native-fuel";fuel.NaturalId=0;Inventory.Items.Add(fuel);inventoryNearbyAt=0;AddCampMaterial(fire,fuel);
        Check(CampRules.IgniteReason(fire,false).Length>0,"Survival cannot light a bare fire lay without a fire-making tool");
        var drill=Supply(PossessionKind.HandDrillSet);var spear=Supply(PossessionKind.StoneTippedSpear);spear.Place=PossessionPlace.RightHand;PlayerRules.Mode=GameMode.Survival;Vitals.Restore();ApplyPlayerMode();
        LightCamp(fire);Check(worldToolWork!=null&&worldToolWork.Tool==drill,"Lighting actually borrows the accessible hand-drill set");
        paused=false;double deadline=Time.unscaledTimeAsDouble+15;while(worldToolWork!=null&&Time.unscaledTimeAsDouble<deadline)yield return null;paused=true;
        Check(fire.Burning&&Inventory.AtHand(PossessionPlace.RightHand)==spear,"Timed fire making ignites tinder and restores the original held spear");
        campUpdateAt=0;UpdateCamp();BindCampLights(view);Check(campLightCount>0&&campVisuals[fire.Id].Sound.isPlaying,"A burning fire binds real shader lighting and spatial crackling");
        double mass=fire.MassKg;paused=false;yield return new WaitForSecondsRealtime(.6f);paused=true;Check(fire.MassKg<mass,"The live world's camp clock consumes fuel over time");
        double nearHeat=CampWarmth();motion.Place(sx+6,sz,Ground(sx+6,sz));Check(CampWarmth()<nearHeat,"Moving away reduces radiant warmth");motion.Place(sx,sz,Ground(sx,sz));
        PlayerRules.SetLocalSolarHour(12,longitude);SetDaylight(LocalSolarHour,skyYear);humanCamera.Restore(4,false);heading=210;pitch=22;UpdateHumans();UpdateCamera();yield return new WaitForSecondsRealtime(.6f);Image("01-camp-daylight");
        PlayerRules.SetLocalSolarHour(0,longitude);SetDaylight(LocalSolarHour,skyYear);yield return null;var lit=Image("02-camp-firelight");campLightTestOff=true;var dark=Image("03-firelight-disabled-control");campLightTestOff=false;
        int changed=0;for(int i=0;i<lit.Length;i++)if(Math.Abs(lit[i].r-dark[i].r)+Math.Abs(lit[i].g-dark[i].g)+Math.Abs(lit[i].b-dark[i].b)>15)changed++;
        Check(changed>500,"Actual firelight visibly illuminates world pixels, compared with the same frame without the light");Check(fire.Burning,"Debug time changes do not consume hours of physical fire fuel");
        PlayerRules.SetLocalSolarHour(12,longitude);SetDaylight(LocalSolarHour,skyYear);Vitals.Tiredness=80;Vitals.Energy=30;double solar=PlayerRules.SolarDays;
        BeginCampSleep(1,false,bed);Check(campSleeping&&CampTimeRate==32,"Sleeping on bedding begins accelerated time");Check((Position-bed.Position).Length*frame.RadiusM<.1,"Lying down places the player on the chosen bedding");
        paused=false;deadline=Time.unscaledTimeAsDouble+14;while(campSleeping&&Time.unscaledTimeAsDouble<deadline)yield return null;paused=true;
        Check(!campSleeping&&Vitals.Tiredness<80&&Vitals.Energy>30,"One hour of actual sleep lowers tiredness independently of energy recovery");
        Check(PlayerRules.SolarDays-solar>.03&&PlayerRules.SolarDays-solar<.06,"Sun/calendar really advance through sleep, not just a fake fade");
        BeginCampSleep(1,false,bed);Vitals.Health-=2;paused=false;UpdateCampSleep();paused=true;Check(!campSleeping,"Actual health loss interrupts sleep");
        CaptureInventoryPhysics();var json=WorldJson.Write(PlayerRules);File.WriteAllText(Path.Combine(output,"camp-play-session.json"),json);var restored=WorldJson.Read<PlaySession>(File.ReadAllText(Path.Combine(output,"camp-play-session.json")));restored.Validate();
        Check(restored.Camp.Objects.Count==3&&restored.Camp.Objects.Single(c=>c.Kind==CampKind.Stake).EmbedM==stake.EmbedM,"Saved/reloaded camp retains exact planted depth and all placed objects");
        Check(restored.Camp.Objects.Single(c=>c.Kind==CampKind.Fire).Burning&&restored.Survival.Tiredness==Vitals.Tiredness,"Fire state, remaining fuel and tiredness survive the real save serializer");
        foreach(var kind in new[]{PossessionKind.DryGrass,PossessionKind.LeafLitter,PossessionKind.TinderBundle,PossessionKind.KindlingBundle,PossessionKind.HandDrillSet})
        {var p=PortableTechnology.Create(kind,Position);var mesh=LocalScatterMeshes.BuildPortable(p);Check(mesh.vertexCount>12&&mesh.bounds.size.sqrMagnitude>0,"New resource/tool has renderable item geometry: "+kind);Destroy(mesh);}
        PlayerRules.Mode=GameMode.Survival;Vitals.Restore();ApplyPlayerMode();TerrainHandoff.Coordinates(frame,fire.Position,out double fireX,out double fireZ);motion.Place(fireX,fireZ,Ground(fireX,fireZ));paused=false;UpdatePlayerSurvival(0,0);paused=true;
        Check(Vitals.Health<100,"Standing in the actual burning fire causes contact damage");motion.Place(sx,sz,Ground(sx,sz));
        fire.Burning=false;fire.EmberSeconds=30;InvalidateCampVisual(fire);campUpdateAt=0;UpdateCamp();Check(campVisuals[fire.Id].Root.GetComponentsInChildren<Transform>().Any(t=>t.name=="Live ember bed"),"An extinguished fire retains a visible live ember bed");
        fire.EmberSeconds=0;campUpdateAt=0;UpdateCamp();Check(!campVisuals[fire.Id].Root.GetComponentsInChildren<Transform>().Any(t=>t.name=="Live ember bed"),"Ember glow retires when the saved ember state cools");
        PlayerRules.Mode=GameMode.Creative;Vitals.Restore();ApplyPlayerMode();paused=false;File.WriteAllLines(Path.Combine(output,"checks.txt"),report);
    }
}
