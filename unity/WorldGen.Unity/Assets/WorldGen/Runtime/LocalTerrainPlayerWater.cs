using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    Texture2D waterNormalField;LocalWaterScreen waterScreen;
    double waterSipAt,waterAimAt;WaterTarget aimedWater;
    readonly Dictionary<(int,LocalWaterKind,ulong,int,int,int),WaterSourceCache> waterSourceCache=new Dictionary<(int,LocalWaterKind,ulong,int,int,int),WaterSourceCache>();
    sealed class WaterSourceCache {public long SolarHour,WeatherHour;public WaterSourceEnvironment Environment;}
    Vector3 waterAimEye,waterAimDirection;
    sealed class WaterTarget {public Vector3 Point;public float Distance;public WaterQuality Quality;}
    void InitializePlayerWater()
    {
        var shader=Resources.Load<Shader>("WorldGenWaterSurface");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Water surface shader unavailable");
        waterMaterial=new Material(shader);waterMaterial.SetFloat("_Water",1);
        // A periodic slope DATA texture, not an image projected onto the sea.
        waterNormalField=new Texture2D(128,128,TextureFormat.RGBA32,true,true){name="Periodic water slopes",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=2};
        const int harmonics=24;var random=new System.Random(734911);var kx=new int[harmonics];var ky=new int[harmonics];var phases=new double[harmonics];
        for(int k=0;k<harmonics;k++){kx[k]=random.Next(2,25);ky[k]=random.Next(-23,24);phases[k]=random.NextDouble()*Math.PI*2;}
        var pixels=new Color[128*128];
        for(int y=0;y<128;y++)for(int x=0;x<128;x++)
        {
            double sx=0,sz=0,height=0;
            for(int k=0;k<kx.Length;k++){double f=Math.Sqrt(kx[k]*kx[k]+ky[k]*ky[k]),a=.06/Math.Pow(f,.8),angle=Math.PI*2*(kx[k]*x+ky[k]*y)/128+phases[k],c=Math.Cos(angle);sx+=kx[k]*a*c;sz+=ky[k]*a*c;height+=Math.Sin(angle);}
            // Sparse, smoothly shaped floating flecks make current direction
            // readable without painting an entire calm stream white.
            double flecks=D.Clamp((height/Math.Sqrt(harmonics*.5)-1.2)*.65);
            pixels[y*128+x]=new Color((float)(.5+.22*sx),(float)(.5+.22*sz),(float)flecks,1);
        }
        waterNormalField.SetPixels(pixels);waterNormalField.Apply(true,true);waterMaterial.SetTexture("_WaterNormals",waterNormalField);
        var data=view.GetUniversalAdditionalCameraData();data.requiresDepthTexture=true;data.requiresColorTexture=true;
        waterScreen=view.gameObject.AddComponent<LocalWaterScreen>();
    }
    LocalTerrainSample PlayerWaterSample(double x,double z)=>walkingPatch.Sample(frame.Position(x,z));
    MotionWater SampleMotionWater(double x,double z)
    {
        if(!Ready||walkingPatch==null)return new MotionWater(double.NaN);
        var position=frame.Position(x,z);var sample=walkingPatch.Sample(position);
        if(!sample.Underwater||sample.WaterKind==LocalWaterKind.None)return new MotionWater(double.NaN);
        double level=frame.SurfaceY(position,sample.WaterSurfaceM),floor=VisibleGround(x,z,frame.SurfaceY(position,sample.ElevationM));
        if(level<=floor+.015)return new MotionWater(double.NaN);
        if(sample.WaterKind!=LocalWaterKind.River)level+=WaterSurface.Swell(x,z,Time.time,CurrentWeather.WindEastMps,CurrentWeather.WindNorthMps,level-floor,sample.WaterKind==LocalWaterKind.Ocean);
        return new MotionWater(level,V.Dot(sample.WaterFlow,frame.East)*sample.WaterSpeedMPerS,V.Dot(sample.WaterFlow,frame.North)*sample.WaterSpeedMPerS);
    }
    WaterQuality PlayerWaterAppearance(LocalTerrainSample sample)
    {
        var ecology=sourceWorld?.Ecology;HabitatCell habitat=ecology!=null&&sample.Cell>=0&&sample.Cell<ecology.Habitat.Length?ecology.Habitat[sample.Cell]:null;double lakeSalinity=double.NaN;
        if(sample.WaterKind==LocalWaterKind.Lake&&ecology!=null)
        {int body=sourceWorld.Hydrology.WaterBody[sample.Cell];if(body>=0&&body<ecology.WaterSalinityPpt.Length)lakeSalinity=ecology.WaterSalinityPpt[body];}
        return WaterQuality.From(sample,habitat,lakeSalinity);
    }
    WaterQuality PlayerWaterQuality(LocalTerrainSample sample,V? at=null)
    {
        var water=PlayerWaterAppearance(sample);if(sourceWorld==null||sample.WaterKind==LocalWaterKind.None)return water;
        V p=at??Position;double radius=frame.RadiusM;
        int x=(int)Math.Floor(p.X*radius/WaterContamination.PatchM),y=(int)Math.Floor(p.Y*radius/WaterContamination.PatchM),z=(int)Math.Floor(p.Z*radius/WaterContamination.PatchM);
        var key=(sample.Cell,sample.WaterKind,sample.WaterBodyKey,x,y,z);
        long solarHour=(long)Math.Floor(PlayerRules.SolarDays*24),weatherHour=(long)Math.Floor(weatherDays*24);
        if(!waterSourceCache.TryGetValue(key,out var cached)||cached.SolarHour!=solarHour||cached.WeatherHour!=weatherHour)
        {
            if(waterSourceCache.Count>=128)waterSourceCache.Clear();
            // Canonical planetary site: not the camera or floating terrain origin.
            var site=new V(x+.5,y+.5,z+.5).Unit;
            int cell=sample.WaterKind==LocalWaterKind.River&&sample.WaterBodyKey>0?(int)(sample.WaterBodyKey-1):sample.Cell;
            double use=WaterContamination.CatchmentAnimalUse(sourceWorld,cell);
            if(Wildlife?.Groups!=null)foreach(var group in Wildlife.Groups)
            {
                if(group.Intent!=WildlifeIntent.Drink||(group.Position-site).Length*radius>180)continue;
                use+=Math.Min(.18,group.Members*.012);
            }
            double runoff=0;
            if(worldWeather!=null)
            {
                // Analytical recent rainfall survives loading and leaving an area.
                // Hour-cached; no weather or wildlife sweep in the frame render path.
                double when=weatherHour/24d;
                var weather=worldWeather.AtPosition(site);
                double recent=weather.Between(when-1,when).RainMm,week=weather.Between(when-7,when).RainMm;
                double wetness=1-Math.Exp(-week/65);
                runoff=D.Clamp(Math.Max(0,recent-12*(1-wetness))/25);
            }
            // There is no sanitation/waste or protected spring system yet: do not
            // invent sewage around every fire, or advertise ordinary streams as springs.
            cached=new WaterSourceCache{SolarHour=solarHour,WeatherHour=weatherHour,Environment=new WaterSourceEnvironment(use,runoff:runoff)};
            waterSourceCache[key]=cached;
        }
        return WaterContamination.Apply(water,sourceWorld.Recipe.Seed,sample.WaterBodyKey,p,radius,PlayerRules.SolarDays,cached.Environment);
    }
    WaterTarget AimedWater(float maximum)
    {
        var eye=view.transform.localPosition;var direction=view.transform.forward;
        if(Time.unscaledTimeAsDouble<waterAimAt&&(eye-waterAimEye).sqrMagnitude<.01&&Vector3.Dot(direction,waterAimDirection)>.999)return aimedWater!=null&&aimedWater.Distance<maximum?aimedWater:null;
        waterAimAt=Time.unscaledTimeAsDouble+.1;waterAimEye=eye;waterAimDirection=direction;aimedWater=null;
        if(direction.y>-.03f)return null;
        float reach=Mathf.Min(15,3+(float)humanCamera.Distance),priorY=eye.y;
        for(float t=.1f;t<=reach;t+=.2f)
        {
            Vector3 point=eye+direction*t;double x=point.x+originX,z=point.z+originZ;var sample=PlayerWaterSample(x,z);
            double floor=VisibleGround(x,z,frame.SurfaceY(frame.Position(x,z),sample.ElevationM));
            if(sample.Underwater)
            {
                double water=SampleMotionWater(x,z).Surface;
                if(double.IsFinite(water)&&point.y<=water+.02&&priorY>=water-.1)
                {
                    point.y=(float)water;var feet=PlayerFeet();if((point-feet).sqrMagnitude>2.8f*2.8f)return null;
                    var obstruction=TraceCombatObstacle(new Ray(view.transform.position,view.transform.forward),t,null,false);if(obstruction!=null&&obstruction.Distance<t-.1)return null;
                    aimedWater=new WaterTarget{Point=point,Distance=t,Quality=PlayerWaterQuality(sample,frame.Position(x,z))};return t<maximum?aimedWater:null;
                }
            }
            if(point.y<floor-.03)return null;priorY=point.y;
        }
        return null;
    }
    string WaterPrompt(float itemDistance)
    {
        var water=AimedWater(itemDistance);if(water==null)return null;
        return water.Quality.Name+(water.Quality.Fresh?" — untreated  [E]":" — salty / unsafe  [E]");
    }
    bool TryWaterInteract(float itemDistance)
    {
        var water=AimedWater(itemDistance);if(water==null||Spectating||Vitals.Incapacitated)return false;
        if(Time.unscaledTimeAsDouble<waterSipAt)return true;waterSipAt=Time.unscaledTimeAsDouble+1.15;
        CancelItemCharge();CancelWorldToolWork();PauseItemCraft(ActiveItemCraft);var equipment=PrepareWorkHands(Array.Empty<string>());
        try
        {
            double chance=Rng.Value(sourceWorld.Recipe.Seed,"player-water-exposure",Vitals.WaterExposureSequence);
            WaterDrinking.Drink(Vitals,water.Quality,.25,chance,Mode);
            PlayBodyCue("drink");
            BeginConsumeGesture(true);
            InventoryFeedback(water.Quality.Fresh?"You drink freshwater. It is untreated.":"You taste salty water. It cannot be relied on for hydration.");
        }
        finally{RestoreWorkHands(new ItemCraftJob{Equipment=equipment});}
        return true;
    }
    void UpdatePlayerWater(bool immediate=false)
    {
        if(!Ready||waterScreen==null)return;
        var point=view.transform.localPosition;var sample=PlayerWaterSample(point.x+originX,point.z+originZ);
        var water=SampleMotionWater(point.x+originX,point.z+originZ);var quality=PlayerWaterAppearance(sample);
        waterScreen.Set(view,water.Surface,quality,Mode!=GameMode.Survival,(float)(SkyFrame?.Daylight??0),CurrentWeather.TemperatureC,immediate);
        if(!SurvivalActive||paused||inventoryOpen||mapOpen||chatOpen||ecologyOpen||DeathVisible)return;
        double dt=Math.Max(0,Math.Min(.25,Time.unscaledDeltaTime));var bodyWater=SampleMotionWater(motion.X,motion.Z);
        bool headUnder=double.IsFinite(bodyWater.Surface)&&motion.Y+humanEyeOffset<bodyWater.Surface-.06;
        Vitals.BreathDebtSeconds=Math.Max(0,Math.Min(3600,Vitals.BreathDebtSeconds+dt*(headUnder?(motion.Swimming&&survivalMovementSpeed>1?1.3:1):-5)));
        if(headUnder&&Vitals.BreathDebtSeconds>45)PlayerDamageRules.Apply(Vitals,Mode,dt*8,PlayerDeathCause.Drowning);
        if(motion.Swimming)Vitals.Wetness=1;
    }
    void DisposePlayerWater(){motion.Water=null;if(waterScreen!=null)Destroy(waterScreen);if(waterNormalField!=null)Destroy(waterNormalField);}
}
