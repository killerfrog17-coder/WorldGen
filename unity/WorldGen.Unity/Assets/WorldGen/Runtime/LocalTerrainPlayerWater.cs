using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    Texture2D waterNormalField;LocalWaterScreen waterScreen;
    double waterSipAt,waterAimAt;int waterSipSequence;WaterTarget aimedWater;
    Vector3 waterAimEye,waterAimDirection;
    sealed class WaterTarget {public Vector3 Point;public float Distance;public WaterQuality Quality;}
    void InitializePlayerWater()
    {
        var shader=Resources.Load<Shader>("WorldGenWaterSurface");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Water surface shader unavailable");
        waterMaterial=new Material(shader);waterMaterial.SetFloat("_Water",1);
        // A periodic slope DATA texture, not an image projected onto the sea.
        waterNormalField=new Texture2D(128,128,TextureFormat.RGBA32,true,true){name="Periodic water slopes",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=2};
        var pixels=new Color[128*128];int[] kx={3,7,1,11,17,23},ky={1,-2,5,8,-5,17};
        for(int y=0;y<128;y++)for(int x=0;x<128;x++)
        {
            double sx=0,sz=0;
            for(int k=0;k<kx.Length;k++){double f=Math.Sqrt(kx[k]*kx[k]+ky[k]*ky[k]),a=.11/Math.Pow(f,.7),c=Math.Cos(Math.PI*2*(kx[k]*x+ky[k]*y)/128+k*1.731);sx+=kx[k]*a*c;sz+=ky[k]*a*c;}
            pixels[y*128+x]=new Color((float)(.5+.22*sx),(float)(.5+.22*sz),1,1);
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
    WaterQuality PlayerWaterQuality(LocalTerrainSample sample)
    {
        var ecology=sourceWorld?.Ecology;HabitatCell habitat=ecology?.Habitat[sample.Cell];double lakeSalinity=double.NaN;
        if(sample.WaterKind==LocalWaterKind.Lake&&ecology!=null)
        {int body=sourceWorld.Hydrology.WaterBody[sample.Cell];if(body>=0&&body<ecology.WaterSalinityPpt.Length)lakeSalinity=ecology.WaterSalinityPpt[body];}
        return WaterQuality.From(sample,habitat,lakeSalinity);
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
                    aimedWater=new WaterTarget{Point=point,Distance=t,Quality=PlayerWaterQuality(sample)};return t<maximum?aimedWater:null;
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
            double chance=Rng.Value(sourceWorld.Recipe.Seed,"player-water-sip",unchecked((int)(PlayerRules.SolarDays*86400)+waterSipSequence++));
            WaterDrinking.Drink(Vitals,water.Quality,.25,chance,Mode);
            InventoryFeedback(water.Quality.Fresh?"You drink freshwater. It is untreated.":"You taste salty water. It cannot be relied on for hydration.");
        }
        finally{RestoreWorkHands(new ItemCraftJob{Equipment=equipment});}
        return true;
    }
    void UpdatePlayerWater()
    {
        if(!Ready||waterScreen==null)return;
        var point=view.transform.localPosition;var sample=PlayerWaterSample(point.x+originX,point.z+originZ);
        var water=SampleMotionWater(point.x+originX,point.z+originZ);var quality=PlayerWaterQuality(sample);
        waterScreen.Set(view,water.Surface,quality,Mode!=GameMode.Survival,(float)(SkyFrame?.Daylight??0),CurrentWeather.TemperatureC);
        if(!SurvivalActive||paused||inventoryOpen||mapOpen||chatOpen||ecologyOpen||DeathVisible)return;
        double dt=Math.Max(0,Math.Min(.25,Time.unscaledDeltaTime));var bodyWater=SampleMotionWater(motion.X,motion.Z);
        bool headUnder=double.IsFinite(bodyWater.Surface)&&motion.Y+humanEyeOffset<bodyWater.Surface-.06;
        Vitals.BreathDebtSeconds=Math.Max(0,Math.Min(3600,Vitals.BreathDebtSeconds+dt*(headUnder?(motion.Swimming&&survivalMovementSpeed>1?1.3:1):-5)));
        if(headUnder&&Vitals.BreathDebtSeconds>45)PlayerDamageRules.Apply(Vitals,Mode,dt*8,PlayerDeathCause.Drowning);
        if(motion.Swimming){Vitals.Wetness=1;Vitals.Stamina=Math.Max(0,Vitals.Stamina-dt*(survivalSprinting?9:survivalMovementSpeed>.1?3.5:.8));}
    }
    void DisposePlayerWater(){motion.Water=null;if(waterScreen!=null)Destroy(waterScreen);if(waterNormalField!=null)Destroy(waterNormalField);}
}
