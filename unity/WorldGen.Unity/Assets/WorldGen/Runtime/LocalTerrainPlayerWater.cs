using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    Texture2D waterNormalField;LocalWaterScreen waterScreen;
    LocalTerrain.LocalTerrainPatch waterProjectionPatch;
    double waterSipAt,waterAimAt;WaterTarget aimedWater;
    readonly Dictionary<(int,LocalWaterKind,ulong,int,int,int),WaterSourceCache> waterSourceCache=new Dictionary<(int,LocalWaterKind,ulong,int,int,int),WaterSourceCache>();
    sealed class WaterSourceCache {public long SolarHour,WeatherHour;public WaterSourceEnvironment Environment;}
    Vector3 waterAimEye,waterAimDirection;
    int waterSampleFrame=-1,waterSampleSlots;
    LocalTerrainFrame waterSampleChart;
    LocalTerrain waterSampleTerrain;
    readonly double[] waterSampleX=new double[2],waterSampleZ=new double[2];
    readonly LocalTerrainSample[] waterSamples=new LocalTerrainSample[2];
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
    LocalTerrainSample PlayerWaterSample(double x,double z)
    {
        if(WorldGenPerformance.ForceFreshWaterSamples)return PlayerWaterSampleFresh(x,z);
        // Camera appearance, motion water and breath checks ask for exactly the
        // same full landscape sample in one frame. Share that immutable result;
        // dynamic swell, quality/weather and submersion still update normally.
        if(waterSampleFrame!=Time.frameCount||!ReferenceEquals(waterSampleChart,frame)||!ReferenceEquals(waterSampleTerrain,terrain))
        {waterSampleFrame=Time.frameCount;waterSampleChart=frame;waterSampleTerrain=terrain;waterSampleSlots=0;}
        for(int i=0;i<waterSampleSlots;i++)if(waterSampleX[i]==x&&waterSampleZ[i]==z)return waterSamples[i];
        var sample=PlayerWaterSampleFresh(x,z);int slot=waterSampleSlots<2?waterSampleSlots++:1;
        waterSampleX[slot]=x;waterSampleZ[slot]=z;waterSamples[slot]=sample;return sample;
    }
    LocalTerrainSample PlayerWaterSampleFresh(double x,double z)
    {
        V at=frame.Position(x,z);
        bool Contains(LocalTerrain.LocalTerrainPatch patch)=>patch!=null&&(at-patch.Center).Length*terrain.RadiusM<patch.HalfExtentM*.9;
        if(Contains(walkingPatch))return walkingPatch.Sample(at);
        // A spectator or detached review camera can be far from the player.
        // Query its actual site, retaining just one small additional index.
        if(!Contains(waterProjectionPatch))waterProjectionPatch=terrain.Patch(at,240/HorizontalScale);
        return waterProjectionPatch.Sample(at);
    }
    public string PerformanceVerifyWaterSamples()
    {
        int compared=0;
        foreach(var point in new[]{new Vector2(0,0),new Vector2(2,3),new Vector2(-15,12),new Vector2(230,-125)})
        {
            double x=motion.X+point.x,z=motion.Z+point.y;
            var expected=PlayerWaterSampleFresh(x,z);
            for(int repeat=0;repeat<3;repeat++)
            {if(!expected.Equals(PlayerWaterSample(x,z)))throw new InvalidOperationException("Cached water sample changed physical terrain/water");compared++;}
        }
        return "PASS "+compared+" exact cached/fresh terrain-water samples; swell and exposure remain time-dependent";
    }
    MotionWater SampleMotionWater(double x,double z)
    {
        if(!Ready||walkingPatch==null)return new MotionWater(double.NaN);
        var position=frame.Position(x,z);var sample=PlayerWaterSample(x,z);
        if(!sample.Underwater||sample.WaterKind==LocalWaterKind.None)return new MotionWater(double.NaN);
        double level=frame.SurfaceY(position,sample.WaterSurfaceM),floor=VisibleGround(x,z,frame.SurfaceY(position,sample.ElevationM));
        if(level<=floor+.015)return new MotionWater(double.NaN);
        if(sample.WaterKind!=LocalWaterKind.River)level+=WaterSurface.Swell(x,z,Time.time,CurrentWeather.WindEastMps,CurrentWeather.WindNorthMps,level-floor,sample.WaterKind==LocalWaterKind.Ocean);
        return new MotionWater(level,V.Dot(sample.WaterFlow,frame.East)*sample.WaterSpeedMPerS,V.Dot(sample.WaterFlow,frame.North)*sample.WaterSpeedMPerS);
    }
    WaterQuality PlayerWaterAppearance(LocalTerrainSample sample)
    {
        var ecology=sourceWorld?.Ecology;HabitatCell habitat=ecology!=null&&sample.Cell>=0&&sample.Cell<ecology.Habitat.Length?ecology.Habitat[sample.Cell]:null;double lakeSalinity=double.NaN;
        if(sample.WaterKind==LocalWaterKind.Lake&&!SmallWaterBodies.IsLocal(sample.WaterBodyKey)&&ecology!=null)
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
    // Broad phase against the water that is actually drawn. The old prompt
    // evaluated the entire landscape every 20cm, even on completely dry land.
    // Unknown/unloaded owners retain that exact fallback; wet candidates still
    // use the original physical surface, swell, reach and obstruction checks.
    bool WaterAimMayIntersect(Vector3 eye,Vector3 direction,float reach)
    {
        double unit=TerrainLod.BaseSizeM*HorizontalScale;
        double x=eye.x+originX,z=eye.z+originZ,endX=x+direction.x*reach,endZ=z+direction.z*reach;
        int x0=(int)Math.Floor(Math.Min(x,endX)/unit),x1=(int)Math.Floor(Math.Max(x,endX)/unit);
        int z0=(int)Math.Floor(Math.Min(z,endZ)/unit),z1=(int)Math.Floor(Math.Max(z,endZ)/unit);
        for(int b=z0;b<=z1;b++)for(int a=x0;a<=x1;a++)
        {
            var tile=VisibleGroundOwner((a+.5)*unit,(b+.5)*unit,out var key);
            if(tile==null||!tile.WaterBoundsKnown)return true;
            if(!tile.HasWater)continue;
            // Stored mesh bounds already include 3.2m of vertical swell. The
            // extra margin also covers the original discrete march's crossing
            // tolerance and horizontal/vertical floating point rounding.
            var bounds=tile.WaterBounds;bounds.Expand(.5f);
            var ray=new Ray(new Vector3((float)(x-key.X*key.SizeM),eye.y,(float)(z-key.Z*key.SizeM)),direction);
            if(bounds.Contains(ray.origin)||bounds.IntersectRay(ray,out float distance)&&distance<=reach+.25f)return true;
        }
        return false;
    }
    public string PerformanceVerifyWaterAim()
    {
        var rotation=view.transform.rotation;bool original=WorldGenPerformance.FullWaterAim;
        int compared=0,wet=0,rejected=0;float reach=Mathf.Min(15,3+(float)humanCamera.Distance);
        void Compare()
        {
            WorldGenPerformance.FullWaterAim=true;waterAimAt=0;var expected=AimedWater(float.PositiveInfinity);
            WorldGenPerformance.FullWaterAim=false;waterAimAt=0;var actual=AimedWater(float.PositiveInfinity);
            if((expected==null)!=(actual==null)||expected!=null&&(expected.Point!=actual.Point||expected.Distance!=actual.Distance||expected.Quality.Name!=actual.Quality.Name||expected.Quality.Fresh!=actual.Quality.Fresh))
                throw new InvalidOperationException("Water broad phase changed an actual drink target");
            if(expected!=null)wet++;if(!WaterAimMayIntersect(view.transform.localPosition,view.transform.forward,reach))rejected++;compared++;
        }
        try
        {
            Compare();
            for(int yaw=0;yaw<24;yaw++)foreach(float tilt in new[]{8f,30f,65f})
            {view.transform.rotation=Quaternion.Euler(tilt,yaw*15,0);Compare();}
        }
        finally{view.transform.rotation=rotation;WorldGenPerformance.FullWaterAim=original;waterAimAt=0;aimedWater=null;}
        return "PASS "+compared+" original/bounds-filtered water aims, "+wet+" wet targets, "+rejected+" broad-phase rejections";
    }
    WaterTarget AimedWater(float maximum)
    {
        var eye=view.transform.localPosition;var direction=view.transform.forward;
        if(Time.unscaledTimeAsDouble<waterAimAt&&(eye-waterAimEye).sqrMagnitude<.01&&Vector3.Dot(direction,waterAimDirection)>.999)return aimedWater!=null&&aimedWater.Distance<maximum?aimedWater:null;
        waterAimAt=Time.unscaledTimeAsDouble+.1;waterAimEye=eye;waterAimDirection=direction;aimedWater=null;
        if(direction.y>-.03f)return null;
        float reach=Mathf.Min(15,3+(float)humanCamera.Distance),priorY=eye.y;
        if(!WorldGenPerformance.FullWaterAim&&!WaterAimMayIntersect(eye,direction,reach))return null;
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
        CancelItemCharge();CancelWorldToolWork();PauseItemCraft(ActiveItemCraft);
        // The consume gesture already hides held props. Moving their ownership
        // to the ground for an instantaneous sip could strand them below a bank
        // (especially with no bag), where the retrieval sight check then fails.
        double chance=Rng.Value(sourceWorld.Recipe.Seed,"player-water-exposure",Vitals.WaterExposureSequence);
        WaterDrinking.Drink(Vitals,water.Quality,.25,chance,Mode);
        PlayBodyCue("drink");
        BeginConsumeGesture(true);
        InventoryFeedback(water.Quality.Fresh?"You drink freshwater. It is untreated.":"You taste salty water. It cannot be relied on for hydration.");
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
