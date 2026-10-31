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
    public IEnumerator CheckWaterUpdateFlow(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("WATER UPDATE "+text);report.Add("PASS "+text);Debug.Log("WATER UPDATE PASS "+text);}
        void Image(string name)
        {
            var previous=RenderTexture.active;var old=view.targetTexture;var target=new RenderTexture(1280,800,24);target.Create();view.targetTexture=target;view.Render();RenderTexture.active=target;
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            RenderTexture.active=previous;view.targetTexture=old;target.Release();Destroy(target);Destroy(image);
        }
        PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();humanCamera.Restore(0,false);
        Teleport(TerrainMapViewport.FromDegrees(.17930170506363216,117.31300341710354));paused=false;
        double deadline=Time.unscaledTimeAsDouble+80;while(!PerformanceGroundSettled&&Time.unscaledTimeAsDouble<deadline)yield return null;paused=true;
        Check(CenterReady,"Actual coastal terrain loads without modifying the world");
        PlayCommands.Execute(PlayerRules,0,"/time set noon",Math.Atan2(Position.Y,Position.X)*180/Math.PI);WeatherControl.Set("clear");SetDaylight(LocalSolarHour,skyYear);
        V near=TerrainMapViewport.FromDegrees(.17924039827946195,117.31312959624624);TerrainHandoff.Coordinates(frame,near,out double nx,out double nz);
        var sample=PlayerWaterSample(nx,nz);var quality=PlayerWaterQuality(sample);Check(sample.Underwater&&!quality.Fresh,"Real sea water is identified as saline");
        Inventory.Items.Clear();InitializeInventory();var stone=PortableTechnology.Create(PossessionKind.StoneTippedSpear,Position);stone.Place=PossessionPlace.RightHand;Inventory.Items.Add(stone);
        PlayerRules.Mode=GameMode.Survival;Vitals.Restore();PlayerMetabolism.Hydrate(Vitals,-1.1);ApplyPlayerMode();UpdateHumans();UpdateCamera();
        var level=SampleMotionWater(nx,nz).Surface;view.transform.LookAt(new Vector3((float)(nx-originX),(float)level,(float)(nz-originZ)));waterAimAt=0;
        Check(AimedWater(float.PositiveInfinity)!=null,"Centre aim reaches actual water from the dry bank");
        Check(TryWaterInteract(float.PositiveInfinity)&&Vitals.Thirst>50,"Actual E interaction with sea water worsens hydration");
        Check(Inventory.AtHand(PossessionPlace.RightHand)==stone,"Drinking restores the original occupied hand");
        double dryX=motion.X,dryZ=motion.Z,shallowX=0,shallowZ=0;bool shallow=false;
        var shoreDirection=new Vector2((float)(nx-dryX),(float)(nz-dryZ)).normalized;
        for(int step=-40;step<=120&&!shallow;step++)for(int side=-2;side<=2&&!shallow;side++)
        {
            var direction=Quaternion.Euler(0,side*15,0)*new Vector3(shoreDirection.x,0,shoreDirection.y);double x=nx+direction.x*step*.1,z=nz+direction.z*step*.1;
            var water=SampleMotionWater(x,z);double depth=water.Surface-CampFloor(x,z);
            if(double.IsFinite(depth)&&depth>.06&&depth<.6&&Math.Sqrt((x-dryX)*(x-dryX)+(z-dryZ)*(z-dryZ))<3.5){shallowX=x;shallowZ=z;shallow=true;}
        }
        Check(shallow,"A real shallow-water site exists for the dry-ground sleep guard");
        motion.Place(shallowX,shallowZ,CampFloor(shallowX,shallowZ));BeginCampSleep(1,true);
        Check(motion.Grounded&&!motion.Swimming&&!campResting&&!campSleeping,"Standing in shallow water cannot start ground rest or sleep");
        motion.Place(dryX,dryZ,CampFloor(dryX,dryZ));
        var floodedBed=new CampObject{Kind=CampKind.Bedding,Position=frame.Position(shallowX,shallowZ)};Camp.Objects.Add(floodedBed);
        try
        {
            BeginCampSleep(1,true,floodedBed);
            Check(!campResting&&!campSleeping&&motion.X==dryX&&motion.Z==dryZ&&inventoryNotice.Contains("underwater"),"Dry-bank player cannot move onto flooded bedding to rest");
        }
        finally{Camp.Objects.Remove(floodedBed);}
        double deepX=0,deepZ=0;bool deep=false;var start=new Vector2((float)(nx-motion.X),(float)(nz-motion.Z)).normalized;
        for(int ring=1;ring<=60&&!deep;ring++)for(int side=-2;side<=2&&!deep;side++)
        {
            var direction=Quaternion.Euler(0,side*20,0)*new Vector3(start.x,0,start.y);double x=nx+direction.x*ring*2,z=nz+direction.z*ring*2;
            var water=SampleMotionWater(x,z);if(double.IsFinite(water.Surface)&&water.Surface-Ground(x,z)>5){deepX=x;deepZ=z;deep=true;}
        }
        Check(deep,"A genuine deep-water site exists near this coast");
        motion.Place(deepX,deepZ,Ground(deepX,deepZ));motion.Y=SampleMotionWater(deepX,deepZ).Surface-.5-humanEyeOffset;motion.Water=SampleMotionWater;motion.WaterEyeOffset=humanEyeOffset;
        for(int i=0;i<300;i++)motion.Move(1d/60,0,0,0,heading,0,false,false,PlayerSolidGround);
        Check(motion.Swimming&&Math.Abs(motion.Y+humanEyeOffset-SampleMotionWater(motion.X,motion.Z).Surface-.12)<.15,"Actual coast supports floating instead of sinking to the seabed");
        Vitals.Restore();var swimEnvironment=new SurvivalEnvironment{AirTemperatureC=24,Humidity=.4};
        paused=false;
        for(int i=0;i<600;i++)
        {
            motion.Move(1d/60,1,0,0,heading,0,false,false,PlayerSolidGround);
            swimEnvironment.Swimming=motion.Swimming;swimEnvironment.SwimEffort=motion.SwimEffort;
            SurvivalRules.Advance(Vitals,1d/60,60,swimEnvironment,1.45,false);UpdatePlayerWater();
        }
        paused=true;Check(motion.Swimming&&Vitals.Stamina>98&&Vitals.Exertion.Burst>.99,"Ten seconds of actual sea swimming retains stamina through the full water and survival code paths");
        motion.Place(deepX,deepZ,Ground(deepX,deepZ));motion.Y=SampleMotionWater(deepX,deepZ).Surface+.12-humanEyeOffset;Vitals.Restore();
        UpdateHumans();AnimateHuman(humanPlayer,1,false,false,.3f,true);
        int swim=Array.IndexOf(humanPlayer.Definition.Roles,"Swim");
        bool swimPlaying=SharedHumanMotion?humanPlayer.Shared.Clips.ContainsKey("Swim")&&humanPlayer.Shared.LocomotionWeight("Swim")>.9f:swim>=0&&humanPlayer.Weights[swim]>.9f;
        Check(swimPlaying,"Player uses the active animation mode's native swimming clip");
        pitch=14;UpdateCamera();UpdatePlayerWater();yield return null;Image("01-ocean-surface");
        double x0=motion.X,z0=motion.Z;for(int i=0;i<120;i++)motion.Move(1d/60,1,0,-1,heading,15,false,false,PlayerSolidGround);
        Check(motion.Swimming&&motion.Y+humanEyeOffset<SampleMotionWater(motion.X,motion.Z).Surface-1,"Looking/down input can swim and submerge in actual sea water");
        Check(Math.Sqrt(Math.Pow(motion.X-x0,2)+Math.Pow(motion.Z-z0,2))>1,"Swimming moves through real terrain coordinates");
        pitch=0;UpdateHumans();UpdateCamera();UpdatePlayerWater();yield return new WaitForSecondsRealtime(.3f);
        Check(waterScreen.Blend>.99&&waterScreen.BlurEyes,"Survival underwater view has full water absorption and naked-eye defocus");Image("02-underwater-unmasked");
        Check(waterScreen.DrawCount>0,"Underwater render-graph passes actually execute");
        PlayerRules.Mode=GameMode.Creative;UpdatePlayerWater();Image("03-underwater-goggles");Check(!waterScreen.BlurEyes,"Creative retains water haze but bypasses eye blur");
        PlayerRules.Mode=GameMode.Survival;Vitals.Restore();Vitals.BreathDebtSeconds=46;paused=false;UpdatePlayerWater();paused=true;
        Check(Vitals.Health<100,"Remaining submerged after breath runs out damages health");
        var saved=WorldJson.Read<PlaySession>(WorldJson.Write(PlayerRules));saved.Validate();Check(saved.Survival.BreathDebtSeconds>45,"Breath state round-trips in the real play-session save format");
        Vitals.Restore();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();
        var river=terrain.Hydrology.Channels.Where(c=>c.WidthM>10&&c.StartM>20&&c.StartM<1500).OrderBy(c=>(c.A-TerrainMapViewport.FromDegrees(30.5,31.2)).Length).First();
        var at=terrain.Amplification.RiverPoint(river.From,.5);Teleport(at);paused=false;deadline=Time.unscaledTimeAsDouble+80;
        while(!PerformanceGroundSettled&&Time.unscaledTimeAsDouble<deadline)yield return null;paused=true;
        PlayerRules.SetLocalSolarHour(12,Math.Atan2(Position.Y,Position.X)*180/Math.PI);nextDaylightAt=0;SetDaylight(LocalSolarHour,skyYear);
        var spans=walkingPatch.RiverSpans();var span=spans.Where(s=>s.Reach==river.From).OrderBy(s=>((s.A+s.B).Unit-at).Length).First();
        var mid=(span.A+span.B).Unit;TerrainHandoff.Coordinates(frame,mid,out double rx,out double rz);var rs=PlayerWaterSample(rx,rz);
        Check(rs.WaterKind==LocalWaterKind.River&&PlayerWaterQuality(rs).Fresh,"Actual inland flowing river is freshwater");
        motion.Place(rx,rz,Ground(rx,rz),true,5);motion.Y=Math.Max(motion.Y,SampleMotionWater(rx,rz).Surface+4);heading=45;pitch=30;UpdateHumans();UpdateCamera();UpdatePlayerWater();yield return null;Image("04-river-surface");
        Check(waterMaterial.shader.isSupported&&waterNormalField!=null,"Animated river/ocean water shader and periodic slope data are supported");
        yield return CheckSourceWaterIllness(output,report,rs,mid);
        File.WriteAllLines(Path.Combine(output,"checks.txt"),report);
    }
    IEnumerator CheckSourceWaterIllness(string output,List<string> report,LocalTerrainSample river,V at)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("SOURCE WATER "+text);report.Add("PASS "+text);Debug.Log("SOURCE WATER PASS "+text);}
        var quality=PlayerWaterQuality(river,at);var repeated=PlayerWaterQuality(river,at);
        Check(quality.Contamination==repeated.Contamination,"Actual river source quality persists across repeated queries");
        var appearance=PlayerWaterAppearance(river);var mud=river;mud.Sediment=1;mud.WaterTurbulence=1;
        Check(PlayerWaterQuality(mud,at).Contamination==quality.Contamination,"Changing only water sediment/turbulence cannot create pathogens");
        Check(appearance.Contamination==0&&waterSourceCache.Count<=128,"Frame-render appearance avoids disease queries and source cache is bounded");
        TerrainHandoff.Coordinates(frame,at,out double x,out double z);motion.Place(x,z,Ground(x,z));
        motion.Y=SampleMotionWater(x,z).Surface+.35-humanEyeOffset;heading=45;pitch=70;
        PlayerRules.Mode=GameMode.Survival;Vitals.Restore();PlayerMetabolism.Hydrate(Vitals,-1);ApplyPlayerMode();UpdateHumans();UpdateCamera();
        waterAimAt=waterSipAt=0;double before=Vitals.WaterDeficitLitres;
        Check(AimedWater(float.PositiveInfinity)?.Quality.Fresh==true,"Actual freshwater surface is reachable through the normal aim path");
        Check(TryWaterInteract(float.PositiveInfinity)&&Vitals.WaterDeficitLitres<before-.2,"Actual freshwater E interaction hydrates with source-specific exposure");
        Vitals.Restore();Vitals.WaterExposureThreshold=.1;Vitals.WaterExposureResponse=.65;
        WaterDrinking.Drink(Vitals,new WaterQuality(LocalWaterKind.River,.08,.5,.05),.25,.99,Mode);
        Check(Vitals.WaterSicknessSeconds>0&&Vitals.WaterSicknessSeverity>.05&&Vitals.WaterSicknessSeverity<.2,"A controlled contaminated dose produces a genuinely mild illness course");
        Check(SurvivalHudValues.From(Vitals,22).Sickness==0,"Contaminated exposure stays invisible during incubation in the native player");
        double incubation=Vitals.WaterSicknessDelaySeconds,duration=Vitals.WaterSicknessSeconds;
        WaterDrinking.Drink(Vitals,new WaterQuality(LocalWaterKind.River,.08,1,0),1,0,Mode);
        Check(Vitals.WaterSicknessDelaySeconds==incubation&&Vitals.WaterSicknessSeconds==duration,"Extra contaminated water cannot restart an existing native illness");
        var checkpoint=new PlaySession();StorePlayer(checkpoint);var saved=WorldJson.Read<PlaySession>(WorldJson.Write(checkpoint));saved.Validate();
        Check(saved.Survival.WaterSicknessTotalSeconds==Vitals.WaterSicknessTotalSeconds&&saved.Survival.WaterSicknessSeverity==Vitals.WaterSicknessSeverity,"Actual player capture and serializer preserve the new illness course");
        WaterDrinking.AdvanceIllness(Vitals,incubation+6*3600);double mild=WaterDrinking.SymptomSeverity(Vitals);
        Check(mild>.05&&mild<.2&&Vitals.WaterDeficitLitres<.05,"Native mild symptoms remain a small physiological setback");
        paused=false;UpdatePlayerSurvival(0,0);UpdateSurvivalHud();paused=true;
        var previousMode=survivalHudCanvas.renderMode;var previousCamera=survivalHudCanvas.worldCamera;int mask=view.cullingMask;
        // The game HUD is an overlay. Camera-mode diagnostic capture would let
        // the very close held rig depth-occlude it, so exclude that rig here.
        bool previousArmsVisible=viewArmsVisible;viewArmsVisible=false;
        survivalHudCanvas.renderMode=RenderMode.ScreenSpaceCamera;survivalHudCanvas.worldCamera=view;survivalHudCanvas.planeDistance=.15f;view.cullingMask|=1<<5;survivalHudCanvas.enabled=true;
        Color32[] Image(string name)
        {
            var previous=RenderTexture.active;var old=view.targetTexture;var target=new RenderTexture(1280,800,24);target.Create();view.targetTexture=target;view.Render();RenderTexture.active=target;
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();var pixels=image.GetPixels32();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            RenderTexture.active=previous;view.targetTexture=old;target.Release();Destroy(target);Destroy(image);return pixels;
        }
        try
        {
            void Hud(){survivalHud.SetValues(SurvivalHudValues.From(Vitals,22),true);survivalHud.SetAllDirty();Canvas.ForceUpdateCanvases();}
            Hud();var faint=Image("05-water-mild");Vitals.WaterSicknessSeverity=0;Hud();var clear=Image("06-water-unnoticed");
            Vitals.WaterSicknessSeverity=.9;Hud();var strong=Image("07-water-severe");
            long faintDifference=0,strongDifference=0;int greenPixels=0;
            for(int i=0;i<clear.Length;i++)
            {faintDifference+=Math.Abs(faint[i].r-clear[i].r)+Math.Abs(faint[i].g-clear[i].g)+Math.Abs(faint[i].b-clear[i].b);strongDifference+=Math.Abs(strong[i].r-clear[i].r)+Math.Abs(strong[i].g-clear[i].g)+Math.Abs(strong[i].b-clear[i].b);if(strong[i].g-strong[i].r>clear[i].g-clear[i].r+8)greenPixels++;}
            File.WriteAllText(Path.Combine(output,"nausea-hud-metrics.txt"),"mild="+mild+"; displayed severe="+survivalHud.Displayed.Sickness+"; faint difference="+faintDifference+"; strong difference="+strongDifference+"; greener pixels="+greenPixels);
            Check(faintDifference>0&&strongDifference>faintDifference*2&&greenPixels>10,"Real GPU stomach tint distinguishes unnoticed, faint and severe nausea");
        }
        finally{viewArmsVisible=previousArmsVisible;survivalHudCanvas.renderMode=previousMode;survivalHudCanvas.worldCamera=previousCamera;view.cullingMask=mask;}
        WaterDrinking.AdvanceIllness(Vitals,10*86400);Check(Vitals.WaterSicknessSeconds==0&&SurvivalHudValues.From(Vitals,22).Sickness==0,"Native illness resolves without direct scripted health loss");
        Vitals.Restore();Vitals.WaterExposureThreshold=.8;Vitals.WaterExposureHazard=.2;Vitals.WaterExposureResponse=.6;Vitals.WaterExposureAgeSeconds=900;Vitals.WaterExposureSequence=9;
        checkpoint=new PlaySession();StorePlayer(checkpoint);saved=WorldJson.Read<PlaySession>(WorldJson.Write(checkpoint));saved.Validate();
        Check(saved.Survival.WaterExposureHazard==.2&&saved.Survival.WaterExposureThreshold==.8&&saved.Survival.WaterExposureSequence==9,"Actual capture preserves partial exposure and its susceptibility draw");
        Vitals.Restore();PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();yield return null;
    }
}
