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
        void Aim(double x,double z,double up=0){UpdateHumans();UpdateCamera();view.transform.LookAt(transform.TransformPoint(new Vector3((float)(x-originX),(float)CampFloor(x,z)+(float)up,(float)(z-originZ))));waterAimAt=0;}
        Possession Supply(PossessionKind kind){var p=PortableTechnology.Create(kind,Position);p.Place=PossessionPlace.Ground;Inventory.Items.Add(p);inventoryNearbyAt=0;return p;}
        Possession Wood(string id){var p=WoodCrafting.Branch(new ScatterInstance{Position=Position,Kind=ScatterKind.Branch,WidthM=.8,HeightM=.035,Variant=0});p.Id=id;p.NaturalId=0;Inventory.Items.Add(p);SetCraftWorkArea(p);return p;}
        IEnumerator Craft(string recipe,Possession primary,PossessionKind result,Possession original)
        {
            RefreshInventoryNearby(true);Check(StartPortableCraft(recipe,primary.Id),"Actual camp resource recipe starts: "+recipe);
            paused=false;double end=Time.unscaledTimeAsDouble+12;while(Inventory.Crafts.Count>0&&Time.unscaledTimeAsDouble<end)yield return null;paused=true;
            Check(Inventory.Crafts.Count==0&&primary.Kind==result&&Inventory.AtHand(PossessionPlace.RightHand)==original,"Camp recipe completes and restores occupied hands: "+recipe);Inventory.Validate();
        }
        CampObject Place(Possession p,CampKind kind,double x,double z)
        {Aim(x,z);BeginCampPlacement(p,kind);Check(campPlaceValid,"Actual placement ray accepts dry soil: "+kind+" / "+campPlaceReason);ConfirmCampPlacement();return Camp.Objects.Last();}
        PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();humanCamera.Restore(0,false);Vitals.Restore();Inventory.Items.Clear();Camp.Objects.Clear();motion.Flying=false;
        double audioUntil=Time.unscaledTimeAsDouble+15;while(!CampAudioReady&&Time.unscaledTimeAsDouble<audioUntil)yield return null;
        Check(CampAudioReady&&CampRecordingCount==3,"Three generated mono campfire recordings load from the editable sound folder");
        Check(terrain.Amplification.UsedCachedRiverDatums,"Player reuses the terrain worker's identical fitted river data");
        WeatherControl.Set("clear");PlayerRules.Settings.RealMinutesPerDay=60;PlayerRules.Settings.ItemCraftingTimeMultiplier=.1;
        bool found=false;double sx=motion.X,sz=motion.Z;
        for(int ring=0;ring<10&&!found;ring++)for(int side=0;side<12&&!found;side++)
        {
            double a=side*Math.PI/6,x=motion.X+Math.Sin(a)*ring*2,z=motion.Z+Math.Cos(a)*ring*2;var s=walkingPatch.Sample(frame.Position(x,z));
            double grade=Math.Max(Math.Abs(CampFloor(x+2,z)-CampFloor(x-2,z)),Math.Abs(CampFloor(x,z+2)-CampFloor(x,z-2)))/4;
            if(!s.Underwater&&s.Ground.DepthM>.18&&grade<.18&&s.Cover.Grasses.Length>.08){sx=x;sz=z;found=true;}
        }
        Check(found,"Actual grass-covered, plantable dry camp site exists");motion.Place(sx,sz,CampFloor(sx,sz));paused=true;
        double longitude=Math.Atan2(Position.Y,Position.X)*180/Math.PI;PlayerRules.SetLocalSolarHour(12,longitude);SetDaylight(LocalSolarHour,skyYear);Aim(sx,sz+1);
        int before=Inventory.Items.Count;GatherCampGrass();Check(Inventory.Items.Count==before+1&&Inventory.Items.Last().Kind==PossessionKind.DryGrass,"Actual grassy ground supplies a physical bedding/tinder bundle");
        var bed=Place(Inventory.Items.Last(),CampKind.Bedding,sx,sz+1.3);Check(!Inventory.Items.Any(p=>bed.Materials.Any(m=>m.Id==p.Id)),"Placing removes the original item from carried/ground ownership");
        for(int i=0;i<6;i++)AddCampMaterial(bed,Supply(PossessionKind.DryGrass));Check(bed.BeddingDepthM>.06,"Adding actual loose materials builds a thick bedding layer");
        UpdateCamp();Check(campVisuals.ContainsKey(bed.Id)&&campVisuals[bed.Id].Root.GetComponentsInChildren<MeshFilter>().Any(f=>f.sharedMesh.vertexCount>1000),"Bedding renders as a layered grass mesh");
        TerrainHandoff.Coordinates(frame,bed.Position,out double bedX,out double bedZ);
        Check(Math.Abs(campVisuals[bed.Id].Root.transform.localPosition.y-VisibleGround(bedX,bedZ,double.NaN))<.001,"Bedding rests on the actual rendered terrain, rather than the different analytic height");
        var branch=WoodCrafting.Branch(new ScatterInstance{Position=Position,Kind=ScatterKind.Branch,WidthM=1.8,HeightM=.03,Variant=0});branch.Id="native-post";branch.NaturalId=0;Inventory.Items.Add(branch);inventoryNearbyAt=0;
        var stake=Place(branch,CampKind.Stake,sx+1.5,sz-.5);double embed=stake.EmbedM;
        var rock=new Possession{Id="native-camp-rock",Kind=PossessionKind.SmallRock,Place=PossessionPlace.RightHand,Position=Position,WidthM=.08,HeightM=.07,DepthM=.08,MassKg=.7,Variant=ToolStone.Encode(ToolStoneMaterial.Chert,0)};Inventory.Items.Add(rock);
        TerrainHandoff.Coordinates(frame,stake.Position,out double tx,out double tz);motion.Place(tx-.5,tz,CampFloor(tx-.5,tz));Aim(tx,tz,1.3);combatGlobalReady=combatRightReady=0;StrikeWithItem(rock,false);
        Check(stake.EmbedM>embed,"Actual rock attack drives the aimed planted stick farther into soil");
        humanCamera.Restore(4,false);Aim(tx,tz,1.3);Check(AimedCamp(out _)==stake,"Third-person camera can target the same physically reachable planted stick");
        double previousThirdPersonEmbed=stake.EmbedM;combatGlobalReady=combatRightReady=0;StrikeWithItem(rock,false);Check(stake.EmbedM>previousThirdPersonEmbed,"Third-person hammering measures reach from the body, not the distant camera");humanCamera.Restore(0,false);
        Check(stake.Materials[0].HeightM==branch.HeightM,"Visible post retains its original full length while burial changes");
        var postRoot=campVisuals[stake.Id].Root;float postBottom=postRoot.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(vertex=>transform.InverseTransformPoint(f.transform.TransformPoint(vertex)).y)).Min();
        Check(Math.Abs(postRoot.transform.localPosition.y-VisibleGround(tx,tz,double.NaN))<.001&&postBottom<VisibleGround(tx,tz,double.NaN)-.02,"The rendered post actually enters the visible ground instead of floating above it");
        double previous=stake.EmbedM;for(int i=0;i<100;i++)CampRules.Drive(stake,rock);Check(stake.EmbedM>previous&&stake.EmbedM<=stake.SoilDepthM,"Repeated hammering respects the site's physical soil depth");InvalidateCampVisual(stake);
        Inventory.Items.Remove(rock);motion.Place(sx,sz,CampFloor(sx,sz));
        var spear=Supply(PossessionKind.StoneTippedSpear);spear.Place=PossessionPlace.RightHand;var cutter=Supply(PossessionKind.StoneBlade);
        var tinder=Supply(PossessionKind.DryGrass);yield return Craft("shred-tinder",tinder,PossessionKind.TinderBundle,spear);
        var kindling=Wood("native-kindling-source");yield return Craft("split-kindling",kindling,PossessionKind.KindlingBundle,spear);
        var drill=Wood("native-drill-source");Wood("native-hearthboard-source");yield return Craft("hand-drill",drill,PossessionKind.HandDrillSet,spear);
        var fire=Place(tinder,CampKind.Fire,sx-1.3,sz-.6);AddCampMaterial(fire,kindling);
        var fuel=WoodCrafting.Branch(new ScatterInstance{Position=Position,Kind=ScatterKind.Branch,WidthM=1.4,HeightM=.04,Variant=0});fuel.Id="native-fuel";fuel.NaturalId=0;Inventory.Items.Add(fuel);inventoryNearbyAt=0;AddCampMaterial(fire,fuel);
        Check(CampRules.IgniteReason(fire,false).Length>0,"Survival cannot light a bare fire lay without a fire-making tool");
        PlayerRules.Mode=GameMode.Survival;Vitals.Restore();ApplyPlayerMode();
        LightCamp(fire);Check(worldToolWork!=null&&worldToolWork.Tool==drill,"Lighting actually borrows the accessible hand-drill set");
        paused=false;double deadline=Time.unscaledTimeAsDouble+15;while(worldToolWork!=null&&Time.unscaledTimeAsDouble<deadline)yield return null;paused=true;
        Check(fire.Burning&&Inventory.AtHand(PossessionPlace.RightHand)==spear,"Timed fire making ignites tinder and restores the original held spear");
        campUpdateAt=0;UpdateCamp();BindCampLights(view);Check(campLightCount>0&&campVisuals[fire.Id].Sound.isPlaying,"A burning fire binds real shader lighting and spatial crackling");
        var originalVoice=campVisuals[fire.Id].Sound;var originalClip=originalVoice.clip;
        Check(originalVoice.mute&&originalVoice.spatialBlend==1&&originalVoice.clip.length>20,"Campfire audio is spatial, long-form and muted while paused");
        InvalidateCampVisual(fire);campUpdateAt=0;UpdateCamp();
        Check(campVisuals[fire.Id].Sound==originalVoice&&originalVoice.clip==originalClip,"Fuel geometry rebuild retains the playing voice instead of restarting its sound");
        var smoke=campVisuals[fire.Id].Smoke;var wind=smoke.velocityOverLifetime;
        Check(smoke.main.maxParticles==32&&wind.x.mode==wind.y.mode&&wind.y.mode==wind.z.mode&&smoke.GetComponent<Renderer>().sharedMaterial.shader.isSupported,"Wind-driven smoke has a bounded emitter, supported shader and valid velocity curves");
        double mass=fire.MassKg;paused=false;yield return new WaitForSecondsRealtime(.6f);paused=true;Check(fire.MassKg<mass,"The live world's camp clock consumes fuel over time");
        double nearHeat=CampWarmth();motion.Place(sx+6,sz,CampFloor(sx+6,sz));Check(CampWarmth()<nearHeat,"Moving away reduces radiant warmth");motion.Place(sx,sz,CampFloor(sx,sz));
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
        var pack=Supply(PossessionKind.HidePack);Check(Inventory.Move(pack.Id,PossessionPlace.Back,"",0,0,false,out _) ,"A real worn carrier is available for placed-material ownership checks");
        var carriedGrass=Supply(PossessionKind.DryGrass);Check(Inventory.Move(carriedGrass.Id,PossessionPlace.Container,pack.Id,0,0,false,out _),"Loose bedding material can start inside a worn pack");
        var packedBed=Place(carriedGrass,CampKind.Bedding,sx-1.3,sz+1.7);Check(packedBed.Materials[0].ContainerId==""&&!packedBed.Materials[0].GroundMoving,"Placed material is detached from its original container and drop motion");
        RemoveCamp(packedBed);var recovered=Inventory.Find(carriedGrass.Id);Check(recovered!=null&&recovered.Place==PossessionPlace.Ground&&recovered.ContainerId==""&&recovered.GroundMoving,"Taking bedding apart returns a physical item without phantom pack contents");Inventory.Validate();Inventory.Items.Remove(recovered);Inventory.Items.Remove(pack);
        foreach(var kind in new[]{PossessionKind.DryGrass,PossessionKind.LeafLitter,PossessionKind.TinderBundle,PossessionKind.KindlingBundle,PossessionKind.HandDrillSet})
        {var p=PortableTechnology.Create(kind,Position);var mesh=LocalScatterMeshes.BuildPortable(p);Check(mesh.vertexCount>12&&mesh.bounds.size.sqrMagnitude>0,"New resource/tool has renderable item geometry: "+kind);Destroy(mesh);}
        PlayerRules.Mode=GameMode.Survival;Vitals.Restore();ApplyPlayerMode();TerrainHandoff.Coordinates(frame,fire.Position,out double fireX,out double fireZ);motion.Place(fireX,fireZ,CampFloor(fireX,fireZ));paused=false;UpdatePlayerSurvival(0,0);paused=true;
        Check(Vitals.Health<100,"Standing in the actual burning fire causes contact damage");motion.Place(sx,sz,CampFloor(sx,sz));
        fire.Burning=false;fire.EmberSeconds=30;InvalidateCampVisual(fire);campUpdateAt=0;UpdateCamp();Check(campVisuals[fire.Id].Root.GetComponentsInChildren<Transform>().Any(t=>t.name=="Live ember bed"),"An extinguished fire retains a visible live ember bed");
        fire.EmberSeconds=0;campUpdateAt=0;UpdateCamp();Check(!campVisuals[fire.Id].Root.GetComponentsInChildren<Transform>().Any(t=>t.name=="Live ember bed"),"Ember glow retires when the saved ember state cools");
        PlayerRules.Mode=GameMode.Creative;Vitals.Restore();ApplyPlayerMode();paused=false;File.WriteAllLines(Path.Combine(output,"checks.txt"),report);
    }
}
