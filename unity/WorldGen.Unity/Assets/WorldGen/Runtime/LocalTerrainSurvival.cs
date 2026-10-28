using System;
using System.Globalization;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    PlaySession transientPlayer;
    double survivalEnvironmentAt;
    SurvivalEnvironment survivalEnvironment;
    bool survivalSprinting;
    double survivalMovementSpeed;
    UnityEngine.Canvas survivalHudCanvas;
    SurvivalHudView survivalHud;
    Vector2 hudScreenSize;Rect hudSafeArea;
    PlaySession PlayerRules=>playState??(transientPlayer??(transientPlayer=new PlaySession()));
    public GameMode Mode=>PlayerRules.Mode;
    public PlayerSurvival Vitals=>PlayerRules.Survival??(PlayerRules.Survival=new PlayerSurvival());
    public bool UnlimitedResources=>GameModeRules.UnlimitedResources(Mode);
    bool SurvivalActive=>GameModeRules.HasSurvivalNeeds(Mode);
    bool Spectating=>Mode==GameMode.Spectator;
    public double FeltTemperatureC=>SurvivalRules.FeelsLike(survivalEnvironment,Vitals.Wetness,survivalMovementSpeed);
    void ApplyPlayerMode()
    {
        if(Mode==GameMode.Creative&&humanPlayer!=null)ClearHumanBlood(humanPlayer);
        motion.NoClip=GameModeRules.NoClip(Mode);
        if(Spectating)motion.Flying=true;
        else if(SurvivalActive)motion.Flying=false;
        if(Ready&&!Spectating)
        {
            double floor=Ground(motion.X,motion.Z)+TerrainMotion.EyeHeightM;
            if(motion.Y<floor)motion.Y=floor;
            motion.Grounded=!motion.Flying&&motion.Y<=floor+.01;
        }
        if(humanPlayer!=null)humanPlayer.Root.SetActive(GameModeRules.HasBody(Mode));
        survivalEnvironmentAt=0;motion.VerticalSpeed=0;
    }
    PlayCommandResult ExecuteGameMode(string[] words)
    {
        const string usage="Usage: /gamemode survival | creative | spectator";
        if(words.Length==1)
        {
            var s=Vitals;string Number(double n)=>n.ToString("F1",CultureInfo.InvariantCulture);
            return new PlayCommandResult(true,"Game mode: "+Mode.ToString().ToLowerInvariant()+".",
                "Health "+Number(s.Health)+"; stamina "+Number(s.Stamina)+"; energy "+Number(s.Energy)+"; hunger "+Number(s.Hunger)+"; thirst "+Number(s.Thirst)+"; feels "+Number(FeltTemperatureC)+" C; body "+Number(s.CoreTemperatureC)+" C"+(s.Dead?"; dead: "+s.DeathCause:s.Unconscious?"; unconscious":""),usage);
        }
        if(words.Length!=2||!SurvivalRules.TryMode(words[1],out var mode))return new PlayCommandResult(false,usage);
        PlayerRules.Mode=mode;
        if(mode==GameMode.Creative)Vitals.Restore();
        if(!DeathVisible&&deathPresented)ClearPlayerDeath();
        ApplyPlayerMode();if(Ready)UpdateCamera();
        return new PlayCommandResult(true,"Game mode: "+mode.ToString().ToLowerInvariant()+".");
    }
    void UpdatePlayerSurvival(double dx,double dz)
    {
        if(respawnWaiting){if(!CenterReady)return;respawnWaiting=false;}
        if(!SurvivalActive||inventoryOpen&&!CraftWorking||paused||mapOpen||ecologyOpen||chatOpen||!Application.isFocused||!Ready)return;
        double dt=Math.Max(0,Math.Min(.25,Time.unscaledDeltaTime));
        if(weatherProvider==null||dt<=0)return;
        if(Time.unscaledTimeAsDouble>=survivalEnvironmentAt)
        {
            survivalEnvironmentAt=Time.unscaledTimeAsDouble+.5;
            var at=Position;var sample=walkingPatch.Sample(at);
            // Ecology, not shadow/vegetation quality settings, determines shelter.
            double canopy=TreeFlora.CanopyShade(sample.Forest,at,terrain.SourceSeed,terrain.RadiusM*HorizontalScale);
            double feetAboveGround=motion.Y-TerrainMotion.EyeHeightM-Ground(motion.X,motion.Z);
            double water=sample.WaterSurfaceM;
            if(sample.Underwater&&double.IsNaN(water)&&sourceWorld.Hydrology.OceanConnected[sample.Cell])water=0;
            double daylight=SkyFrame?.Daylight??0;
            survivalEnvironment=new SurvivalEnvironment{
                AirTemperatureC=CurrentWeather.TemperatureC,Humidity=CurrentWeather.Humidity,
                WindMps=Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps)*(1-canopy*.65),
                RainMmPerHour=CurrentWeather.RainMmPerHour*(1-canopy*.75),
                Sunlight=daylight*(1-CurrentWeather.CloudCover*.85)*(1-canopy*.8),
                Immersed=!double.IsNaN(water)&&water-(sample.ElevationM+feetAboveGround)>.6};
        }
        double speed=Vitals.Incapacitated?0:Math.Min(TerrainMotion.RunSpeedMps,Math.Sqrt(dx*dx+dz*dz)/dt);survivalMovementSpeed=speed;
        SurvivalRules.Advance(Vitals,dt,PlayerRules.Settings.RealMinutesPerDay,survivalEnvironment,speed,survivalSprinting);
        Vitals.Stamina=Math.Max(0,Vitals.Stamina-dt*Math.Max(0,Inventory.LoadEffort-1)*speed*.3);
    }
    void UpdateSurvivalHud()
    {
        bool show=SurvivalActive&&!inventoryOpen&&!DeathVisible&&Ready&&weatherProvider!=null&&survivalEnvironmentAt>0&&!closed&&!paused&&!mapOpen&&!ecologyOpen&&!(debugOpen&&debugFull);
        if(!show){if(survivalHudCanvas!=null)survivalHudCanvas.enabled=false;return;}
        if(survivalHudCanvas==null)
        {
            var root=new GameObject("Survival HUD",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));root.transform.SetParent(transform,false);root.layer=5;
            survivalHudCanvas=root.GetComponent<Canvas>();survivalHudCanvas.renderMode=RenderMode.ScreenSpaceOverlay;survivalHudCanvas.sortingOrder=5;
            var scaler=root.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=1;
            var group=new GameObject("Approved survival display",typeof(RectTransform),typeof(CanvasRenderer),typeof(SurvivalHudView));group.transform.SetParent(root.transform,false);group.layer=5;
            survivalHud=group.GetComponent<SurvivalHudView>();var rect=survivalHud.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,0);rect.sizeDelta=new Vector2(256,256);
            survivalHudCanvas.enabled=false;
        }
        var size=new Vector2(Screen.width,Screen.height);var safe=Screen.safeArea;
        if(size!=hudScreenSize||safe!=hudSafeArea)
        {hudScreenSize=size;hudSafeArea=safe;float scale=Mathf.Max(.01f,Screen.height/1080f);survivalHud.rectTransform.anchoredPosition=new Vector2(-16-(Screen.width-safe.xMax)/scale,16+safe.yMin/scale);}
        survivalHud.SetValues(SurvivalHudValues.From(Vitals,FeltTemperatureC),!survivalHudCanvas.enabled);survivalHudCanvas.enabled=true;
    }
}
