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
        PlayerRules.Mode=GameMode.Survival;Vitals.Restore();Vitals.Thirst=50;ApplyPlayerMode();UpdateHumans();UpdateCamera();
        var level=SampleMotionWater(nx,nz).Surface;view.transform.LookAt(new Vector3((float)(nx-originX),(float)level,(float)(nz-originZ)));waterAimAt=0;
        Check(AimedWater(float.PositiveInfinity)!=null,"Centre aim reaches actual water from the dry bank");
        Check(TryWaterInteract(float.PositiveInfinity)&&Vitals.Thirst>50,"Actual E interaction with sea water worsens hydration");
        Check(Inventory.AtHand(PossessionPlace.RightHand)==stone,"Drinking restores the original occupied hand");
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
        UpdateHumans();AnimateHuman(humanPlayer,1,false,false,.3f,true);
        int swim=Array.IndexOf(humanPlayer.Definition.Roles,"Swim");Check(swim>=0&&humanPlayer.Weights[swim]>.9f,"Player uses the existing authored swimming clip");
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
        var spans=walkingPatch.RiverSpans();var span=spans.Where(s=>s.Reach==river.From).OrderBy(s=>((s.A+s.B).Unit-at).Length).First();
        var mid=(span.A+span.B).Unit;TerrainHandoff.Coordinates(frame,mid,out double rx,out double rz);var rs=PlayerWaterSample(rx,rz);
        Check(rs.WaterKind==LocalWaterKind.River&&PlayerWaterQuality(rs).Fresh,"Actual inland flowing river is freshwater");
        motion.Place(rx,rz,Ground(rx,rz),true,5);motion.Y=Math.Max(motion.Y,SampleMotionWater(rx,rz).Surface+4);heading=45;pitch=30;UpdateHumans();UpdateCamera();UpdatePlayerWater();yield return null;Image("04-river-surface");
        Check(waterMaterial.shader.isSupported&&waterNormalField!=null,"Animated river/ocean water shader and periodic slope data are supported");
        File.WriteAllLines(Path.Combine(output,"checks.txt"),report);
    }
}
